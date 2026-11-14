using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using AllInBox.Models;

namespace AllInBox.Services;

public class GameLibraryService
{
    private static readonly string DataDirectory = AppPaths.DataDirectory;

    private static readonly string DatabaseFile = Path.Combine(DataDirectory, "games.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ObservableCollection<GameItem> Games { get; } = new();

    public GameLibraryService()
    {
        EnsureDirectory();
        LoadGames();
    }

    private void EnsureDirectory()
    {
        try
        {
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }
        }
        catch { }
    }

    public void LoadGames()
    {
        Games.Clear();

        if (!File.Exists(DatabaseFile))
            return;

        try
        {
            string json = File.ReadAllText(DatabaseFile);
            var loaded = JsonSerializer.Deserialize<List<GameItem>>(json, JsonOptions);
            if (loaded != null)
            {
                var cleanedList = DeduplicateAndRepairList(loaded);
                foreach (var g in cleanedList)
                {
                    Games.Add(g);
                }
                if (cleanedList.Count != loaded.Count)
                {
                    SaveGames();
                }
            }
        }
        catch
        {
            // Fallback gracefully on corrupt json
        }
    }

    public void SaveGames()
    {
        try
        {
            EnsureDirectory();
            string json = JsonSerializer.Serialize(Games, JsonOptions);
            File.WriteAllText(DatabaseFile, json);
        }
        catch { }
    }

    /// <summary>
    /// Deduplicates games by ExecutablePath, Game Root Directory, or Normalized Title+Platform,
    /// removes bogus non-game launcher entries, and repairs wrapper executables (like tModLoader).
    /// </summary>
    private List<GameItem> DeduplicateAndRepairList(List<GameItem> rawList)
    {
        var result = new List<GameItem>();

        foreach (var item in rawList)
        {
            if (string.IsNullOrWhiteSpace(item.ExecutablePath))
                continue;

            // Filter out bogus non-game entries (like Epic Games Launcher itself or Steamworks Shared)
            if (IsBogusLauncherOrTool(item.ExecutablePath, item.Title))
                continue;

            // Repair tModLoader if it was pointing to dotnet\dotnet.exe
            RepairSpecialGameEntry(item);

            // If executable no longer exists on disk and drive is mounted, skip uninstalled game
            string? rootDrive = Path.GetPathRoot(item.ExecutablePath);
            if (!string.IsNullOrWhiteSpace(rootDrive) && Directory.Exists(rootDrive) && !File.Exists(item.ExecutablePath))
                continue;

            // Check if a duplicate already exists in result
            var existing = result.FirstOrDefault(x => AreSameGame(x, item));
            if (existing != null)
            {
                MergeGameItems(existing, item);
            }
            else
            {
                result.Add(item);
            }
        }

        return result;
    }

    public int DeduplicateExistingLibrary()
    {
        var current = Games.ToList();
        int initialCount = current.Count;
        var deduplicated = DeduplicateAndRepairList(current);

        int removedCount = initialCount - deduplicated.Count;
        if (removedCount > 0)
        {
            Games.Clear();
            foreach (var g in deduplicated)
            {
                Games.Add(g);
            }
            SaveGames();
        }
        return removedCount;
    }

    private static readonly string[] NonGameNameMarkers =
    {
        "aseprite", "soundpad", "wallpaper engine", "wallpaper32", "wallpaper64",
        "redistributable", "crash reporter", "crashreporter", "benchmark tool"
    };

    /// <summary>
    /// Deliberately narrow: only names that are unmistakably utilities. Anything ambiguous stays visible,
    /// because hiding a real game is worse than showing a tool. Titles are NOT matched on generic words such as
    /// "update" or "launcher" (Half-Life 2 Update is a game; Minecraft Launcher / tModLoader can be how you play).
    /// </summary>
    public static bool LooksLikeNonGame(GameItem game)
    {
        string title = game.Title ?? string.Empty;
        string exeName = Path.GetFileNameWithoutExtension(game.ExecutablePath ?? string.Empty);

        foreach (var marker in NonGameNameMarkers)
        {
            if (title.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
                exeName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Hides entries that look like utilities. Each entry is judged once (AutoHideChecked), so a game the user
    /// restored from "Ocultos" is never hidden again. Returns how many were newly hidden.
    /// </summary>
    public int HideSuspectedNonGames()
    {
        int hidden = 0;
        bool changed = false;

        foreach (var game in Games)
        {
            if (game.AutoHideChecked) continue;

            game.AutoHideChecked = true;
            changed = true;

            if (!game.IsHidden && LooksLikeNonGame(game))
            {
                game.IsHidden = true;
                hidden++;
            }
        }

        if (changed) SaveGames();
        return hidden;
    }

    public static bool IsBogusLauncherOrTool(string exePath, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return true;
        string lowerPath = exePath.ToLowerInvariant().Replace('/', '\\');
        string fileName = Path.GetFileName(lowerPath);

        if (fileName is "unrealenginelauncher.exe" or "epicgameslauncher.exe" or "riotclientservices.exe" or "riot client.exe" or "steam.exe")
            return true;

        if (lowerPath.Contains(@"\epic games\launcher\") ||
            lowerPath.Contains(@"\steamworks shared\") ||
            lowerPath.Contains(@"\steam controller configs\"))
            return true;

        if (!string.IsNullOrWhiteSpace(title) && title.Equals("Launcher", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static void RepairSpecialGameEntry(GameItem game)
    {
        try
        {
            string lowerExe = game.ExecutablePath.ToLowerInvariant().Replace('/', '\\');

            // Repair tModLoader pointing to dotnet\dotnet.exe
            if (lowerExe.Contains(@"\tmodloader\") && lowerExe.EndsWith(@"dotnet.exe"))
            {
                string root = ResolveGameRootDirectory(game.ExecutablePath, game.WorkingDirectory);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    string batPath = Path.Combine(root, "start-tModLoader.bat");
                    if (File.Exists(batPath))
                    {
                        game.ExecutablePath = batPath;
                        game.WorkingDirectory = root;
                    }
                    else
                    {
                        game.WorkingDirectory = root;
                        if (string.IsNullOrWhiteSpace(game.Arguments))
                            game.Arguments = "tModLoader.dll";
                    }
                }
            }

            // Repair title if it was set to "Windows" (e.g. Pogo Rogue in common\Windows\Pogo Rogue.exe)
            if (string.Equals(game.Title, "Windows", StringComparison.OrdinalIgnoreCase) && File.Exists(game.ExecutablePath))
            {
                string exeName = Path.GetFileNameWithoutExtension(game.ExecutablePath);
                if (!string.Equals(exeName, "Windows", StringComparison.OrdinalIgnoreCase))
                {
                    game.Title = CleanGameTitle(exeName);
                }
            }
        }
        catch { }
    }

    public static bool AreSameGame(GameItem a, GameItem b)
    {
        if (string.Equals(a.ExecutablePath, b.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            return true;

        string rootA = ResolveGameRootDirectory(a.ExecutablePath, a.WorkingDirectory);
        string rootB = ResolveGameRootDirectory(b.ExecutablePath, b.WorkingDirectory);

        if (!string.IsNullOrWhiteSpace(rootA) && !string.IsNullOrWhiteSpace(rootB) &&
            string.Equals(rootA, rootB, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normTitleA = NormalizeKey(a.Title);
        string normTitleB = NormalizeKey(b.Title);

        if (!string.IsNullOrWhiteSpace(normTitleA) && normTitleA == normTitleB &&
            string.Equals(a.Platform, b.Platform, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static void MergeGameItems(GameItem target, GameItem duplicate)
    {
        target.PlayTimeSeconds = Math.Max(target.PlayTimeSeconds, duplicate.PlayTimeSeconds);
        target.IsFavorite = target.IsFavorite || duplicate.IsFavorite;
        if (duplicate.LastPlayed.HasValue && (!target.LastPlayed.HasValue || duplicate.LastPlayed > target.LastPlayed))
        {
            target.LastPlayed = duplicate.LastPlayed;
        }
        if (target.InstallSizeBytes <= 0 && duplicate.InstallSizeBytes > 0)
        {
            target.InstallSizeBytes = duplicate.InstallSizeBytes;
        }

        // Prefer launcher.exe or 64-bit over 32-bit if both exist
        if (!File.Exists(target.ExecutablePath) && File.Exists(duplicate.ExecutablePath))
        {
            target.ExecutablePath = duplicate.ExecutablePath;
            target.WorkingDirectory = duplicate.WorkingDirectory;
        }
        else if (target.ExecutablePath.Contains("32", StringComparison.OrdinalIgnoreCase) &&
                 !duplicate.ExecutablePath.Contains("32", StringComparison.OrdinalIgnoreCase) &&
                 File.Exists(duplicate.ExecutablePath))
        {
            target.ExecutablePath = duplicate.ExecutablePath;
            target.WorkingDirectory = duplicate.WorkingDirectory;
        }
    }

    private static string NormalizeKey(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return Regex.Replace(input.ToLowerInvariant(), @"[^a-z0-9]", "");
    }

    public async Task RefreshIconsAndSizesInBackgroundAsync(Action? onUpdated = null)
    {
        var snapshot = Games.ToList();

        // 1. First pass: fast icon repair + Steam manifest sizes (completes in < 300ms)
        bool fastChanged = await Task.Run(() =>
        {
            bool changed = false;
            foreach (var game in snapshot)
            {
                try
                {
                    string beforeExe = game.ExecutablePath;
                    string beforeTitle = game.Title;
                    RepairSpecialGameEntry(game);
                    if (game.ExecutablePath != beforeExe || game.Title != beforeTitle)
                        changed = true;

                    bool needsIconRefresh = string.IsNullOrWhiteSpace(game.IconPath) ||
                                            !File.Exists(game.IconPath) ||
                                            IconExtractor.IsWrapperOrGenericExecutable(game.ExecutablePath) ||
                                            game.Title.Contains("tModLoader", StringComparison.OrdinalIgnoreCase);

                    if (needsIconRefresh)
                    {
                        string? newIcon = IconExtractor.ExtractAndCacheIcon(game.ExecutablePath, game.Id, game.Title, game.WorkingDirectory);
                        if (!string.IsNullOrWhiteSpace(newIcon))
                        {
                            Converters.SafeImagePathConverter.InvalidateCache(newIcon);
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                game.IconPath = string.Empty;
                                game.IconPath = newIcon;
                            });
                            changed = true;
                        }
                    }

                    // Try fast Steam manifest size first
                    if (game.InstallSizeBytes <= 0)
                    {
                        long fastSize = TryGetFastSteamManifestSize(game.ExecutablePath, game.WorkingDirectory);
                        if (fastSize > 0)
                        {
                            Application.Current?.Dispatcher?.Invoke(() => game.InstallSizeBytes = fastSize);
                            changed = true;
                        }
                    }
                }
                catch { }
            }
            return changed;
        });

        if (fastChanged)
        {
            SaveGames();
            Application.Current?.Dispatcher?.Invoke(() => onUpdated?.Invoke());
        }

        // 2. Second pass: directory enumeration for remaining games without manifest size
        bool deepChanged = await Task.Run(() =>
        {
            bool changed = false;
            foreach (var game in snapshot.Where(g => g.InstallSizeBytes <= 0))
            {
                try
                {
                    long size = CalculateGameInstallSize(game.ExecutablePath, game.WorkingDirectory);
                    if (size > 0)
                    {
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            game.InstallSizeBytes = size;
                            onUpdated?.Invoke();
                        });
                        SaveGames();
                        changed = true;
                    }
                }
                catch { }
            }
            return changed;
        });

        if (deepChanged)
        {
            SaveGames();
            Application.Current?.Dispatcher?.Invoke(() => onUpdated?.Invoke());
        }
    }

    public static string ResolveGameRootDirectory(string executablePath, string? workingDirectory = null)
    {
        try
        {
            string basePath = !string.IsNullOrWhiteSpace(executablePath) ? executablePath : (workingDirectory ?? string.Empty);
            if (string.IsNullOrWhiteSpace(basePath)) return string.Empty;

            string fullPath = Path.GetFullPath(basePath);
            string lower = fullPath.ToLowerInvariant().Replace('/', '\\');

            // 1. Steam pattern: ...\steamapps\common\<GameFolder>\...
            string steamMarker = @"\steamapps\common\";
            int steamIdx = lower.IndexOf(steamMarker, StringComparison.OrdinalIgnoreCase);
            if (steamIdx != -1)
            {
                string afterCommon = fullPath[(steamIdx + steamMarker.Length)..];
                int nextSlash = afterCommon.IndexOfAny(new[] { '\\', '/' });
                string folderName = nextSlash != -1 ? afterCommon[..nextSlash] : afterCommon;
                return fullPath[..(steamIdx + steamMarker.Length + folderName.Length)];
            }

            // 2. Epic Games pattern: ...\epic games\<GameFolder>\...
            string epicMarker = @"\epic games\";
            int epicIdx = lower.IndexOf(epicMarker, StringComparison.OrdinalIgnoreCase);
            if (epicIdx != -1)
            {
                string afterEpic = fullPath[(epicIdx + epicMarker.Length)..];
                int nextSlash = afterEpic.IndexOfAny(new[] { '\\', '/' });
                string folderName = nextSlash != -1 ? afterEpic[..nextSlash] : afterEpic;
                return fullPath[..(epicIdx + epicMarker.Length + folderName.Length)];
            }

            // 3. Riot Games pattern: ...\riot games\<GameFolder>\...
            string riotMarker = @"\riot games\";
            int riotIdx = lower.IndexOf(riotMarker, StringComparison.OrdinalIgnoreCase);
            if (riotIdx != -1)
            {
                string afterRiot = fullPath[(riotIdx + riotMarker.Length)..];
                int nextSlash = afterRiot.IndexOfAny(new[] { '\\', '/' });
                string folderName = nextSlash != -1 ? afterRiot[..nextSlash] : afterRiot;
                return fullPath[..(riotIdx + riotMarker.Length + folderName.Length)];
            }

            // 4. XboxGames pattern: ...\xboxgames\<GameFolder>\...
            string xboxMarker = @"\xboxgames\";
            int xboxIdx = lower.IndexOf(xboxMarker, StringComparison.OrdinalIgnoreCase);
            if (xboxIdx != -1)
            {
                string afterXbox = fullPath[(xboxIdx + xboxMarker.Length)..];
                int nextSlash = afterXbox.IndexOfAny(new[] { '\\', '/' });
                string folderName = nextSlash != -1 ? afterXbox[..nextSlash] : afterXbox;
                return fullPath[..(xboxIdx + xboxMarker.Length + folderName.Length)];
            }

            // 5. Generic folder resolution: climb out of bin, binaries, win64, x64, dotnet, game, live, content
            string? dir = File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) : (Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath));
            if (string.IsNullOrWhiteSpace(dir)) return string.Empty;

            var subFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bin", "binaries", "win64", "win32", "x64", "x86", "dotnet", "launchutils", "game", "live", "content", "shipping", "engine"
            };

            DirectoryInfo? current = new DirectoryInfo(dir);
            int climbs = 0;
            while (current != null && current.Parent != null && climbs < 3)
            {
                if (subFolderNames.Contains(current.Name))
                {
                    current = current.Parent;
                    climbs++;
                }
                else
                {
                    break;
                }
            }

            return current?.FullName ?? dir;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static long TryGetFastSteamManifestSize(string executablePath, string? workingDirectory = null)
    {
        try
        {
            string rootDir = ResolveGameRootDirectory(executablePath, workingDirectory);
            if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir)) return 0;

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
                            var m = Regex.Match(content, "\"SizeOnDisk\"\\s+\"(\\d+)\"");
                            if (m.Success && long.TryParse(m.Groups[1].Value, out long acfBytes) && acfBytes > 0)
                            {
                                return acfBytes;
                            }
                        }
                    }
                    catch { }
                }
            }

            // Also check Epic Games ManifestSize / InstallSize if in Epic Games
            string manifestsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );
            if (Directory.Exists(manifestsPath))
            {
                foreach (var itemFile in Directory.GetFiles(manifestsPath, "*.item"))
                {
                    try
                    {
                        string json = File.ReadAllText(itemFile);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        string installLoc = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() ?? "" : "";
                        if (!string.IsNullOrWhiteSpace(installLoc) &&
                            string.Equals(Path.GetFullPath(installLoc).TrimEnd('\\', '/'),
                                          Path.GetFullPath(rootDir).TrimEnd('\\', '/'),
                                          StringComparison.OrdinalIgnoreCase))
                        {
                            if (root.TryGetProperty("InstallSize", out var isz) && isz.TryGetInt64(out long epicBytes) && epicBytes > 0)
                            {
                                return epicBytes;
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
        return 0;
    }

    public static long CalculateGameInstallSize(string executablePath, string? workingDirectory = null)
    {
        try
        {
            long fast = TryGetFastSteamManifestSize(executablePath, workingDirectory);
            if (fast > 0) return fast;

            string rootDir = ResolveGameRootDirectory(executablePath, workingDirectory);
            if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            {
                if (File.Exists(executablePath))
                    return new FileInfo(executablePath).Length;
                return 0;
            }

            // Safe recursive directory enumeration with EnumerationOptions (ignores permission errors & reparse points)
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            long totalBytes = 0;
            var dirInfo = new DirectoryInfo(rootDir);
            foreach (var fi in dirInfo.EnumerateFiles("*", options))
            {
                try
                {
                    totalBytes += fi.Length;
                }
                catch { }
            }

            return totalBytes;
        }
        catch
        {
            return 0;
        }
    }

    public GameItem CreateGameItemFromPath(string filePath, string? platform = null, string? suggestedTitle = null)
    {
        var (targetExe, workDir, args) = ShortcutResolver.ResolveShortcut(filePath);

        string title = !string.IsNullOrWhiteSpace(suggestedTitle)
            ? suggestedTitle
            : InferGameTitleFromPath(filePath, targetExe);

        var game = new GameItem
        {
            Title = title,
            ExecutablePath = targetExe,
            WorkingDirectory = workDir,
            Arguments = args,
            Platform = platform ?? DetectPlatform(targetExe),
            DateAdded = DateTime.Now
        };

        RepairSpecialGameEntry(game);

        // Extract and cache icon
        string? iconPath = IconExtractor.ExtractAndCacheIcon(game.ExecutablePath, game.Id, game.Title, game.WorkingDirectory);
        if (!string.IsNullOrWhiteSpace(iconPath))
            game.IconPath = iconPath;

        // Calculate install size
        game.InstallSizeBytes = CalculateGameInstallSize(game.ExecutablePath, game.WorkingDirectory);

        return game;
    }

    public GameItem AddGameFromPath(string filePath, string? platform = null)
    {
        var game = CreateGameItemFromPath(filePath, platform);
        var existing = Games.FirstOrDefault(x => AreSameGame(x, game));
        if (existing != null)
        {
            MergeGameItems(existing, game);
            SaveGames();
            return existing;
        }

        Games.Add(game);
        SaveGames();
        return game;
    }

    public int AddGamesBatch(IEnumerable<GameItem> items)
    {
        int added = 0;
        foreach (var item in items)
        {
            if (IsBogusLauncherOrTool(item.ExecutablePath, item.Title))
                continue;

            var existing = Games.FirstOrDefault(x => AreSameGame(x, item));
            if (existing != null)
            {
                MergeGameItems(existing, item);
            }
            else
            {
                Games.Add(item);
                added++;
            }
        }
        SaveGames();
        return added;
    }

    public void RemoveGame(GameItem game)
    {
        if (Games.Remove(game))
        {
            if (!string.IsNullOrWhiteSpace(game.IconPath) && File.Exists(game.IconPath))
            {
                try { File.Delete(game.IconPath); } catch { }
            }
            SaveGames();
        }
    }

    public static string InferGameTitleFromPath(string filePath, string targetExe)
    {
        // 1. If it's a shortcut (.lnk), shortcut name is usually already human-friendly
        if (filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            string lnkTitle = CleanGameTitle(Path.GetFileNameWithoutExtension(filePath));
            if (!string.IsNullOrWhiteSpace(lnkTitle)) return lnkTitle;
        }

        string fullPath = Path.GetFullPath(string.IsNullOrWhiteSpace(targetExe) ? filePath : targetExe);
        string lower = fullPath.ToLowerInvariant();

        // 2. Check Steam common pattern: .../steamapps/common/<GameFolder>/...
        int commonIdx = lower.IndexOf(@"\steamapps\common\", StringComparison.OrdinalIgnoreCase);
        if (commonIdx == -1) commonIdx = lower.IndexOf("/steamapps/common/", StringComparison.OrdinalIgnoreCase);

        if (commonIdx != -1)
        {
            string sub = fullPath.Substring(commonIdx + 18);
            string[] parts = sub.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                string folder = parts[0];
                if (folder.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                {
                    return CleanGameTitle(Path.GetFileNameWithoutExtension(targetExe));
                }
                return CleanGameTitle(folder);
            }
        }

        // 3. Check Epic Games pattern: .../epic games/<GameFolder>/...
        int epicIdx = lower.IndexOf(@"\epic games\", StringComparison.OrdinalIgnoreCase);
        if (epicIdx == -1) epicIdx = lower.IndexOf("/epic games/", StringComparison.OrdinalIgnoreCase);
        if (epicIdx != -1)
        {
            string sub = fullPath.Substring(epicIdx + 12);
            string[] parts = sub.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                return CleanGameTitle(parts[0]);
            }
        }

        // 4. Check GOG pattern
        int gogIdx = lower.IndexOf(@"\gog galaxy\games\", StringComparison.OrdinalIgnoreCase);
        if (gogIdx == -1) gogIdx = lower.IndexOf(@"\gog games\", StringComparison.OrdinalIgnoreCase);
        if (gogIdx != -1)
        {
            int gogLength = lower.Contains(@"\gog galaxy\games\") ? 18 : 11;
            if (gogIdx + gogLength < fullPath.Length)
            {
                string sub = fullPath.Substring(gogIdx + gogLength);
                string[] parts = sub.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    return CleanGameTitle(parts[0]);
                }
            }
        }

        // 5. Check Games / Jogos pattern
        int gamesIdx = lower.IndexOf(@"\games\", StringComparison.OrdinalIgnoreCase);
        if (gamesIdx == -1) gamesIdx = lower.IndexOf(@"\jogos\", StringComparison.OrdinalIgnoreCase);
        if (gamesIdx != -1)
        {
            string sub = fullPath.Substring(gamesIdx + 7);
            string[] parts = sub.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                return CleanGameTitle(parts[0]);
            }
        }

        // Check XboxGames pattern
        int xboxIdx = lower.IndexOf(@"\xboxgames\", StringComparison.OrdinalIgnoreCase);
        if (xboxIdx == -1) xboxIdx = lower.IndexOf("/xboxgames/", StringComparison.OrdinalIgnoreCase);
        if (xboxIdx != -1)
        {
            string sub = fullPath.Substring(xboxIdx + 11);
            string[] parts = sub.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                return CleanGameTitle(parts[0]);
            }
        }

        string exeName = Path.GetFileNameWithoutExtension(targetExe);
        if (exeName.Length <= 4 || 
            exeName.Equals("game", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("shipping", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("main", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("app", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("start", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("minecraft", StringComparison.OrdinalIgnoreCase))
        {
            string root = ResolveGameRootDirectory(targetExe);
            if (!string.IsNullOrWhiteSpace(root))
            {
                string rootName = Path.GetFileName(root);
                if (!string.IsNullOrWhiteSpace(rootName)) return CleanGameTitle(rootName);
            }
        }

        return CleanGameTitle(exeName);
    }

    public static string CleanGameTitle(string rawName)
    {
        var cleaned = rawName.Replace('_', ' ').Trim();
        if (cleaned.StartsWith("start-", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[6..].Trim();
        if (cleaned.StartsWith("Launch ", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[7..].Trim();
        if (cleaned.EndsWith(" - Shortcut", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[..^11].Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? rawName : cleaned;
    }

    private static string DetectPlatform(string exePath)
    {
        string lower = exePath.ToLowerInvariant();
        if (lower.Contains("steamapps") || lower.Contains("steam"))
            return "Steam";
        if (lower.Contains("epic games") || lower.Contains("epicgames"))
            return "Epic Games";
        if (lower.Contains("gog galaxy") || lower.Contains("gog games"))
            return "GOG";
        if (lower.Contains("ubisoft") || lower.Contains("ubisoft game launcher"))
            return "Ubisoft";
        if (lower.Contains("ea games") || lower.Contains("origin games") || lower.Contains("electronic arts"))
            return "EA App";
        if (lower.Contains("riot games") || lower.Contains("league of legends") || lower.Contains("valorant") || lower.Contains("riotclient"))
            return "Riot Games";
        if (lower.Contains("minecraft") || lower.Contains("modrinth") || lower.Contains("curseforge") || lower.Contains("lunarclient") || lower.Contains("feather") || lower.Contains("prismlauncher") || lower.Contains("badlion") || lower.Contains("tlauncher") || lower.Contains(".minecraft") || lower.Contains("bedrock"))
            return "Minecraft";
        if (lower.Contains("rpcs3") || lower.Contains("pcsx2") || lower.Contains("dolphin") || lower.Contains("yuzu") || lower.Contains("ryujinx") || lower.Contains("retroarch"))
            return "Emulador";

        return "PC / Outros";
    }
}
