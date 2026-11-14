using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AllInBox.Models;

namespace AllInBox.Services;

public class GameLauncherService : IDisposable
{
    private readonly Action _onDataUpdated;
    private readonly Func<IList<GameItem>> _getGamesFunc;
    private readonly DispatcherTimer _monitorTimer;
    private readonly ConcurrentDictionary<string, DateTime> _sessionStartTimes = new(StringComparer.OrdinalIgnoreCase);
    private bool _isPolling;
    private bool _disposed;

    public GameLauncherService(Action onDataUpdated, Func<IList<GameItem>> getGamesFunc)
    {
        _onDataUpdated = onDataUpdated;
        _getGamesFunc = getGamesFunc;

        _monitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _monitorTimer.Tick += MonitorTimer_Tick;
        _monitorTimer.Start();
    }

    private async void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        if (_isPolling || _disposed) return;
        _isPolling = true;

        try
        {
            var games = _getGamesFunc();
            if (games.Count == 0) return;

            var runningStates = await Task.Run(() => DetectRunningGames(games));
            bool anyChange = false;
            var now = DateTime.Now;

            foreach (var game in games)
            {
                bool isCurrentlyRunning = runningStates.TryGetValue(game.Id, out bool r) && r;

                if (isCurrentlyRunning && !game.IsRunning)
                {
                    game.IsRunning = true;
                    game.LastPlayed = now;
                    _sessionStartTimes[game.Id] = now;
                    anyChange = true;
                }
                else if (!isCurrentlyRunning && game.IsRunning)
                {
                    // Avoid immediately marking false within 12 seconds of launching (slow wrappers/launchers)
                    if (_sessionStartTimes.TryGetValue(game.Id, out var startTime) && (now - startTime).TotalSeconds < 12)
                    {
                        continue;
                    }

                    game.IsRunning = false;
                    if (_sessionStartTimes.TryRemove(game.Id, out var startedAt))
                    {
                        long elapsed = (long)Math.Max(1, (now - startedAt).TotalSeconds);
                        game.PlayTimeSeconds += elapsed;
                    }
                    anyChange = true;
                }
                else if (isCurrentlyRunning && game.IsRunning)
                {
                    // Incrementally accumulate playtime every 15 seconds while running so stats stay live
                    if (_sessionStartTimes.TryGetValue(game.Id, out var startedAt))
                    {
                        long elapsed = (long)(now - startedAt).TotalSeconds;
                        if (elapsed >= 15)
                        {
                            game.PlayTimeSeconds += elapsed;
                            _sessionStartTimes[game.Id] = now;
                            anyChange = true;
                        }
                    }
                    else
                    {
                        _sessionStartTimes[game.Id] = now;
                    }
                }
            }

            if (anyChange)
            {
                _onDataUpdated();
            }
        }
        catch { }
        finally
        {
            _isPolling = false;
        }
    }

    private static Dictionary<string, bool> DetectRunningGames(IList<GameItem> games)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        Process[] allProcs = Array.Empty<Process>();

        try
        {
            allProcs = Process.GetProcesses();
            var runningNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in allProcs)
            {
                try
                {
                    if (!string.IsNullOrEmpty(p.ProcessName))
                    {
                        runningNames.Add(p.ProcessName);
                    }
                }
                catch { }
            }

            foreach (var game in games)
            {
                if (string.IsNullOrWhiteSpace(game.ExecutablePath)) continue;

                string exeName = Path.GetFileNameWithoutExtension(game.ExecutablePath);
                if (string.IsNullOrWhiteSpace(exeName)) continue;

                // Direct process name match (skipping generic wrapper names like dotnet/cmd unless verified)
                if (!IconExtractor.IsWrapperOrGenericExecutable(game.ExecutablePath))
                {
                    if (runningNames.Contains(exeName))
                    {
                        result[game.Id] = true;
                        continue;
                    }

                    // Also check common shipping suffix variants (e.g. Game -> Game-Win64-Shipping)
                    if (runningNames.Any(rn => rn.StartsWith(exeName + "-", StringComparison.OrdinalIgnoreCase) &&
                                               rn.Contains("Shipping", StringComparison.OrdinalIgnoreCase)))
                    {
                        result[game.Id] = true;
                        continue;
                    }
                }
                else
                {
                    // For tModLoader or wrapper games, check if dotnet/javaw is running from the game's root folder
                    if (game.Title.Contains("tModLoader", StringComparison.OrdinalIgnoreCase) && runningNames.Contains("dotnet"))
                    {
                        string root = GameLibraryService.ResolveGameRootDirectory(game.ExecutablePath, game.WorkingDirectory);
                        if (!string.IsNullOrWhiteSpace(root))
                        {
                            foreach (var dp in allProcs.Where(x => SafeGetProcessName(x).Equals("dotnet", StringComparison.OrdinalIgnoreCase)))
                            {
                                try
                                {
                                    string? modFile = dp.MainModule?.FileName;
                                    if (!string.IsNullOrWhiteSpace(modFile) && modFile.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                                    {
                                        result[game.Id] = true;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
        }
        catch { }
        finally
        {
            foreach (var p in allProcs)
            {
                try { p.Dispose(); } catch { }
            }
        }

        return result;
    }

    private static string SafeGetProcessName(Process p)
    {
        try { return p.ProcessName; } catch { return string.Empty; }
    }

    public static string? TryFindEpicAppName(string executablePath)
    {
        string manifestsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests"
        );

        if (!Directory.Exists(manifestsPath)) return null;

        try
        {
            foreach (var file in Directory.GetFiles(manifestsPath, "*.item"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string installLocation = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() ?? "" : "";
                    string appName = root.TryGetProperty("AppName", out var an) ? an.GetString() ?? "" : "";

                    if (!string.IsNullOrWhiteSpace(installLocation) && !string.IsNullOrWhiteSpace(appName))
                    {
                        if (executablePath.StartsWith(installLocation, StringComparison.OrdinalIgnoreCase) ||
                            installLocation.StartsWith(Path.GetDirectoryName(executablePath) ?? "___", StringComparison.OrdinalIgnoreCase))
                        {
                            return appName;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }

        return null;
    }

    public static string? TryFindSteamAppId(string executablePath, string? workingDir)
    {
        try
        {
            string rootDir = GameLibraryService.ResolveGameRootDirectory(executablePath, workingDir);
            if (string.IsNullOrWhiteSpace(rootDir)) return null;

            string folderName = Path.GetFileName(rootDir);
            var steamAppsDir = Directory.GetParent(rootDir)?.Parent;
            if (steamAppsDir != null && steamAppsDir.Exists && steamAppsDir.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var acf in steamAppsDir.GetFiles("appmanifest_*.acf"))
                {
                    try
                    {
                        string content = File.ReadAllText(acf.FullName);
                        if (content.Contains($"\"installdir\"\t\t\"{folderName}\"", StringComparison.OrdinalIgnoreCase))
                        {
                            return Path.GetFileNameWithoutExtension(acf.Name).Replace("appmanifest_", "");
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
        return null;
    }

    public Task<bool> LaunchGameAsync(GameItem game)
    {
        if (string.IsNullOrWhiteSpace(game.ExecutablePath) || !File.Exists(game.ExecutablePath))
        {
            Views.AppDialogWindow.Show(
                null,
                $"O executável do jogo não foi encontrado no caminho especificado:\n\n{game.ExecutablePath}",
                "Executável Não Encontrado",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return Task.FromResult(false);
        }

        try
        {
            var startTime = DateTime.Now;

            // 1. Check if it's an Epic Games title that requires Epic URI authentication
            string? epicAppName = null;
            if (string.Equals(game.Platform, "Epic Games", StringComparison.OrdinalIgnoreCase) ||
                game.ExecutablePath.Contains("Epic Games", StringComparison.OrdinalIgnoreCase))
            {
                epicAppName = TryFindEpicAppName(game.ExecutablePath);
            }

            if (!string.IsNullOrWhiteSpace(epicAppName))
            {
                var epicUriInfo = new ProcessStartInfo
                {
                    FileName = $"com.epicgames.launcher://apps/{epicAppName}?action=launch&silent=true",
                    UseShellExecute = true
                };
                using var p = Process.Start(epicUriInfo);
            }
            else if (game.Title.Contains("tModLoader", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(game.Platform, "Steam", StringComparison.OrdinalIgnoreCase))
            {
                string? steamAppId = TryFindSteamAppId(game.ExecutablePath, game.WorkingDirectory) ?? "1281930";
                var steamUriInfo = new ProcessStartInfo
                {
                    FileName = $"steam://rungameid/{steamAppId}",
                    UseShellExecute = true
                };
                using var p = Process.Start(steamUriInfo);
            }
            else
            {
                string workDir = game.WorkingDirectory;
                if (string.IsNullOrWhiteSpace(workDir) || !Directory.Exists(workDir))
                {
                    workDir = Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = game.ExecutablePath,
                    Arguments = game.Arguments ?? string.Empty,
                    WorkingDirectory = workDir,
                    UseShellExecute = true
                };

                using var p = Process.Start(startInfo);
            }

            game.IsRunning = true;
            game.LastPlayed = startTime;
            _sessionStartTimes[game.Id] = startTime;
            _onDataUpdated();

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Views.AppDialogWindow.Show(
                null,
                $"Não foi possível iniciar o jogo '{game.Title}':\n\n{ex.Message}",
                "Erro ao Executar",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            game.IsRunning = false;
            return Task.FromResult(false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitorTimer.Stop();
    }
}
