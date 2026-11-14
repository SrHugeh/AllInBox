using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AllInBox.Services;

public class DiscoveredGame
{
    public string FilePath { get; set; } = string.Empty;
    public string SuggestedTitle { get; set; } = string.Empty;
    public string Platform { get; set; } = "PC / Outros";
    public bool IsSelected { get; set; } = true;
    public long FileSizeBytes { get; set; }
}

public class GameScannerService
{
    private static readonly HashSet<string> BlacklistedExeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "unins000.exe", "uninstall.exe", "uninstaller.exe", "uninst.exe",
        "unitycrashhandler32.exe", "unitycrashhandler64.exe", "crashreport.exe",
        "crashreporter.exe", "crashhandler.exe", "crashhandler64.exe", "werfault.exe",
        "vcredist_x86.exe", "vcredist_x64.exe", "dxsetup.exe", "directx_redist.exe",
        "oalinst.exe", "dotnetfx.exe", "epicgameslauncher.exe", "steamservice.exe",
        "steam.exe", "steamerrorreporter.exe", "steamerrorreporter64.exe",
        "easyanticheat.exe", "easyanticheat_setup.exe", "easyanticheat_eos.exe", "easyanticheat_eos_setup.exe",
        "battleye.exe", "beservice.exe", "beservice_x64.exe",
        "patcher.exe", "update.exe", "updater.exe",
        "config.exe", "settings.exe", "setup.exe", "redist.exe",
        "cefprocess.exe", "crashpad_handler.exe", "qtwebengineprocess.exe",
        "vc_redist.x64.exe", "vc_redist.x86.exe", "dxwebsetup.exe",
        "dedicated.exe", "server.exe", "dedicatedserver.exe", "benchmark.exe"
    };

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$recycle.bin", "_commonredist", "directx", "redist", "support", "installers",
        "install", "crashreport", "crashreports", "docs", "documentation",
        "thirdparty", "dotNetFx", "vcredist", "dependencies",
        "launcher", "riot client", "steamworks shared", "steam controller configs"
    };

    public static List<string> GetCommonGameDirectories()
    {
        var dirs = new List<string>();

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Steam
        string steamCommon = Path.Combine(programFilesX86, "Steam", "steamapps", "common");
        if (Directory.Exists(steamCommon)) dirs.Add(steamCommon);

        // Epic Games
        string epic = Path.Combine(programFiles, "Epic Games");
        if (Directory.Exists(epic)) dirs.Add(epic);

        // GOG
        string gog = Path.Combine(programFilesX86, "GOG Galaxy", "Games");
        if (Directory.Exists(gog)) dirs.Add(gog);

        // Ubisoft
        string ubi = Path.Combine(programFilesX86, "Ubisoft", "Ubisoft Game Launcher", "games");
        if (Directory.Exists(ubi)) dirs.Add(ubi);

        // Common custom game root folders on all available drives
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                // Riot Games
                string riot = Path.Combine(drive.RootDirectory.FullName, "Riot Games");
                if (Directory.Exists(riot) && !dirs.Contains(riot)) dirs.Add(riot);

                // Steam Alt
                string steamAlt = Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps", "common");
                if (Directory.Exists(steamAlt) && !dirs.Contains(steamAlt)) dirs.Add(steamAlt);

                string gamesRoot = Path.Combine(drive.RootDirectory.FullName, "Games");
                if (Directory.Exists(gamesRoot) && !dirs.Contains(gamesRoot)) dirs.Add(gamesRoot);

                string jogosRoot = Path.Combine(drive.RootDirectory.FullName, "Jogos");
                if (Directory.Exists(jogosRoot) && !dirs.Contains(jogosRoot)) dirs.Add(jogosRoot);
            }
        }

        // Minecraft
        string mcDir = Path.Combine(appData, ".minecraft");
        if (Directory.Exists(mcDir)) dirs.Add(mcDir);

        return dirs;
    }

    public static List<string> GetDirectoriesForPlatform(string platform)
    {
        var dirs = new List<string>();
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string p = platform.ToLowerInvariant();

        if (p.Contains("steam"))
        {
            string steamCommon = Path.Combine(programFilesX86, "Steam", "steamapps", "common");
            if (Directory.Exists(steamCommon)) dirs.Add(steamCommon);

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string steamAlt = Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps", "common");
                    if (Directory.Exists(steamAlt) && !dirs.Contains(steamAlt)) dirs.Add(steamAlt);
                }
            }
        }
        else if (p.Contains("epic"))
        {
            string epic = Path.Combine(programFiles, "Epic Games");
            if (Directory.Exists(epic)) dirs.Add(epic);

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string epicAlt = Path.Combine(drive.RootDirectory.FullName, "Epic Games");
                    if (Directory.Exists(epicAlt) && !dirs.Contains(epicAlt)) dirs.Add(epicAlt);
                }
            }
        }
        else if (p.Contains("riot"))
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string riot = Path.Combine(drive.RootDirectory.FullName, "Riot Games");
                    if (Directory.Exists(riot) && !dirs.Contains(riot)) dirs.Add(riot);
                }
            }
        }
        else if (p.Contains("minecraft"))
        {
            // 1. Official Minecraft Launcher (Microsoft Store / Xbox App / Win32)
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            string mcStore1 = Path.Combine(programFilesX86, "Minecraft Launcher");
            if (Directory.Exists(mcStore1)) dirs.Add(mcStore1);

            string mcStore2 = Path.Combine(programFiles, "Minecraft Launcher");
            if (Directory.Exists(mcStore2)) dirs.Add(mcStore2);

            string mcStore3 = Path.Combine(localAppData, "Programs", "Minecraft Launcher");
            if (Directory.Exists(mcStore3)) dirs.Add(mcStore3);

            // 2. Modrinth App (AppData\Local\Modrinth App & AppData\Local\Programs\Modrinth App)
            string modrinth0 = Path.Combine(localAppData, "Modrinth App");
            if (Directory.Exists(modrinth0)) dirs.Add(modrinth0);

            string modrinth1 = Path.Combine(localAppData, "Programs", "Modrinth App");
            if (Directory.Exists(modrinth1)) dirs.Add(modrinth1);

            string modrinth2 = Path.Combine(appData, "com.modrinth.theseus");
            if (Directory.Exists(modrinth2)) dirs.Add(modrinth2);

            string modrinth3 = Path.Combine(appData, "Modrinth App");
            if (Directory.Exists(modrinth3)) dirs.Add(modrinth3);

            // 3. CurseForge (AppData\Local\Programs\CurseForge Windows & AppData\Local\CurseForge)
            string curseWin = Path.Combine(localAppData, "Programs", "CurseForge Windows");
            if (Directory.Exists(curseWin)) dirs.Add(curseWin);

            string curse1 = Path.Combine(localAppData, "Programs", "CurseForge");
            if (Directory.Exists(curse1)) dirs.Add(curse1);

            string curse2 = Path.Combine(localAppData, "CurseForge");
            if (Directory.Exists(curse2)) dirs.Add(curse2);

            string curseInstances = Path.Combine(userProfile, "curseforge", "minecraft", "Instances");
            if (Directory.Exists(curseInstances)) dirs.Add(curseInstances);

            // 4. Prism Launcher & MultiMC
            string prism1 = Path.Combine(localAppData, "Programs", "PrismLauncher");
            if (Directory.Exists(prism1)) dirs.Add(prism1);

            string prism2 = Path.Combine(appData, "PrismLauncher");
            if (Directory.Exists(prism2)) dirs.Add(prism2);

            // 5. Lunar Client & Feather Client & Badlion
            string lunar = Path.Combine(localAppData, "Programs", "lunarclient");
            if (Directory.Exists(lunar)) dirs.Add(lunar);

            string feather1 = Path.Combine(localAppData, "Programs", "Feather Launcher");
            if (Directory.Exists(feather1)) dirs.Add(feather1);

            string feather2 = Path.Combine(appData, ".feather");
            if (Directory.Exists(feather2)) dirs.Add(feather2);

            string badlion = Path.Combine(localAppData, "Programs", "Badlion Client");
            if (Directory.Exists(badlion)) dirs.Add(badlion);

            // 6. Standard .minecraft folder
            string mcDir = Path.Combine(appData, ".minecraft");
            if (Directory.Exists(mcDir)) dirs.Add(mcDir);

            // 7. XboxGames on all available drives
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string xboxMinecraft = Path.Combine(drive.RootDirectory.FullName, "XboxGames", "Minecraft Launcher", "Content");
                    if (Directory.Exists(xboxMinecraft) && !dirs.Contains(xboxMinecraft)) dirs.Add(xboxMinecraft);

                    string xboxBedrock = Path.Combine(drive.RootDirectory.FullName, "XboxGames", "Minecraft: Java & Bedrock Edition for PC", "Content");
                    if (Directory.Exists(xboxBedrock) && !dirs.Contains(xboxBedrock)) dirs.Add(xboxBedrock);
                }
            }
        }
        else if (p.Contains("ubisoft"))
        {
            string ubi = Path.Combine(programFilesX86, "Ubisoft", "Ubisoft Game Launcher", "games");
            if (Directory.Exists(ubi)) dirs.Add(ubi);

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string ubiAlt = Path.Combine(drive.RootDirectory.FullName, "Ubisoft", "Ubisoft Game Launcher", "games");
                    if (Directory.Exists(ubiAlt) && !dirs.Contains(ubiAlt)) dirs.Add(ubiAlt);
                }
            }
        }
        else if (p.Contains("gog"))
        {
            string gog = Path.Combine(programFilesX86, "GOG Galaxy", "Games");
            if (Directory.Exists(gog)) dirs.Add(gog);

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string gogAlt = Path.Combine(drive.RootDirectory.FullName, "GOG Games");
                    if (Directory.Exists(gogAlt) && !dirs.Contains(gogAlt)) dirs.Add(gogAlt);
                }
            }
        }

        return dirs;
    }

    public Task<List<DiscoveredGame>> ScanDirectoryAsync(string rootPath, string? targetPlatform = null)
    {
        return Task.Run(() =>
        {
            var results = new List<DiscoveredGame>();
            if (!Directory.Exists(rootPath)) return results;

            bool isContainerFolder = IsGameContainerFolder(rootPath);

            if (isContainerFolder)
            {
                var gameDirs = Directory.GetDirectories(rootPath);
                foreach (var gameDir in gameDirs)
                {
                    string dirName = Path.GetFileName(gameDir);
                    if (IgnoredDirectoryNames.Contains(dirName) || dirName.StartsWith("."))
                        continue;

                    var bestExe = FindBestGameExecutable(gameDir, dirName, targetPlatform);
                    if (bestExe != null)
                    {
                        results.Add(bestExe);
                    }
                }
            }
            else
            {
                string rootName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var bestExe = FindBestGameExecutable(rootPath, rootName, targetPlatform);
                if (bestExe != null)
                {
                    results.Add(bestExe);
                }
                else
                {
                    var subDirs = Directory.GetDirectories(rootPath);
                    foreach (var dir in subDirs)
                    {
                        string dirName = Path.GetFileName(dir);
                        if (IgnoredDirectoryNames.Contains(dirName) || dirName.StartsWith("."))
                            continue;

                        var match = FindBestGameExecutable(dir, dirName, targetPlatform);
                        if (match != null)
                        {
                            results.Add(match);
                        }
                    }
                }
            }

            return results;
        });
    }

    private static bool IsGameContainerFolder(string path)
    {
        string lower = path.ToLowerInvariant();
        if (lower.EndsWith("common") || lower.EndsWith("steamapps\\common") || lower.EndsWith("steamapps/common"))
            return true;
        if (lower.EndsWith("epic games") || lower.EndsWith("gog games") || lower.EndsWith("riot games"))
            return true;
        if (lower.EndsWith("\\games") || lower.EndsWith("/games") || lower.EndsWith("\\jogos") || lower.EndsWith("/jogos"))
            return true;

        try
        {
            var subDirs = Directory.GetDirectories(path);
            var rootExes = Directory.GetFiles(path, "*.exe", SearchOption.TopDirectoryOnly);
            if (subDirs.Length >= 2 && rootExes.Length == 0)
                return true;
        }
        catch { }

        return false;
    }

    public static List<DiscoveredGame> GetEpicManifestGames()
    {
        var list = new List<DiscoveredGame>();
        string manifestsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");

        if (Directory.Exists(manifestsPath))
        {
            try
            {
                foreach (var file in Directory.GetFiles(manifestsPath, "*.item"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        string displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() ?? "" : "";
                        string installLocation = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() ?? "" : "";
                        string launchExe = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() ?? "" : "";

                        if (!string.IsNullOrWhiteSpace(installLocation) && !string.IsNullOrWhiteSpace(launchExe))
                        {
                            string fullExePath = Path.Combine(installLocation, launchExe);
                            if (File.Exists(fullExePath))
                            {
                                var fi = new FileInfo(fullExePath);
                                list.Add(new DiscoveredGame
                                {
                                    FilePath = fullExePath,
                                    SuggestedTitle = string.IsNullOrWhiteSpace(displayName) ? FormatCleanTitle(Path.GetFileName(installLocation)) : displayName,
                                    Platform = "Epic Games",
                                    FileSizeBytes = fi.Length,
                                    IsSelected = true
                                });
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        return list;
    }

    private static DiscoveredGame? FindBestGameExecutable(string gameDirectory, string gameFolderName, string? overridePlatform = null)
    {
        try
        {
            // 1. If folder is in Epic Games, check official Epic manifests for the exact LaunchExecutable
            string lowerDir = gameDirectory.ToLowerInvariant();
            if (lowerDir.Contains("epic games") || (overridePlatform != null && overridePlatform.Contains("epic", StringComparison.OrdinalIgnoreCase)))
            {
                var epicGames = GetEpicManifestGames();
                var match = epicGames.FirstOrDefault(x =>
                    x.FilePath.StartsWith(gameDirectory, StringComparison.OrdinalIgnoreCase) ||
                    gameDirectory.StartsWith(Path.GetDirectoryName(x.FilePath) ?? "___", StringComparison.OrdinalIgnoreCase)
                );
                if (match != null)
                {
                    return match;
                }
            }

            // 1.5 Check if folder has a dedicated start-<Game>.bat script (e.g. start-tModLoader.bat)
            string startBat = Path.Combine(gameDirectory, $"start-{gameFolderName}.bat");
            if (File.Exists(startBat))
            {
                var batFi = new FileInfo(startBat);
                return new DiscoveredGame
                {
                    FilePath = batFi.FullName,
                    SuggestedTitle = ResolveGameTitle(gameDirectory, gameFolderName, batFi.FullName),
                    Platform = overridePlatform ?? DetectPlatform(batFi.FullName),
                    FileSizeBytes = batFi.Length,
                    IsSelected = true
                };
            }

            // 2. Search root & depth 1 first! (Most launchers live in root or 1 level down)
            var rootAndDepth1Exes = new List<FileInfo>();
            CollectCandidateExes(gameDirectory, rootAndDepth1Exes, depth: 0, maxDepth: 1);

            List<FileInfo> candidateList;
            if (rootAndDepth1Exes.Count > 0)
            {
                candidateList = rootAndDepth1Exes;
            }
            else
            {
                // Only if no .exe was found in root or depth 1, search deeper (up to maxDepth: 3)
                candidateList = new List<FileInfo>();
                CollectCandidateExes(gameDirectory, candidateList, depth: 0, maxDepth: 3);
            }

            if (candidateList.Count == 0)
                return null;

            var scoredExes = candidateList.Select(fi => new
            {
                File = fi,
                Score = CalculateExecutableScore(fi, gameDirectory, gameFolderName)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

            var top = scoredExes.FirstOrDefault();
            if (top == null || top.Score <= 0)
                return null;

            string cleanTitle = ResolveGameTitle(gameDirectory, gameFolderName, top.File.FullName);

            return new DiscoveredGame
            {
                FilePath = top.File.FullName,
                SuggestedTitle = cleanTitle,
                Platform = overridePlatform ?? DetectPlatform(top.File.FullName),
                FileSizeBytes = top.File.Length,
                IsSelected = true
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveGameTitle(string gameDirectory, string gameFolderName, string exeFilePath)
    {
        string candidate = gameFolderName;

        var genericFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Content", "bin", "binaries", "win64", "win32", "x64", "x86", "game", "games", "shipping", "Windows"
        };

        if (genericFolderNames.Contains(candidate))
        {
            var parentDir = Directory.GetParent(gameDirectory);
            if (parentDir != null && !genericFolderNames.Contains(parentDir.Name) && !parentDir.Name.Equals("common", StringComparison.OrdinalIgnoreCase))
            {
                candidate = parentDir.Name;
            }
            else
            {
                candidate = Path.GetFileNameWithoutExtension(exeFilePath);
            }
        }

        string lowerDir = gameDirectory.ToLowerInvariant();
        string lowerExe = Path.GetFileNameWithoutExtension(exeFilePath).ToLowerInvariant();

        if (lowerDir.Contains("xboxgames\\minecraft launcher") || lowerDir.Contains("xboxgames/minecraft launcher"))
        {
            return "Minecraft Launcher";
        }
        if (lowerDir.Contains("xboxgames\\minecraft: java & bedrock") || lowerDir.Contains("xboxgames/minecraft: java & bedrock") || lowerDir.Contains("minecraft.windows"))
        {
            return "Minecraft (Bedrock / Java)";
        }
        if (lowerExe.Equals("minecraftlauncher", StringComparison.OrdinalIgnoreCase))
        {
            return "Minecraft Launcher";
        }
        if (lowerExe.Equals("modrinth app", StringComparison.OrdinalIgnoreCase))
        {
            return "Modrinth App";
        }
        if (lowerExe.Equals("curseforge", StringComparison.OrdinalIgnoreCase))
        {
            return "CurseForge";
        }

        return FormatCleanTitle(candidate);
    }

    private static void CollectCandidateExes(string dir, List<FileInfo> list, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;

        try
        {
            string dirName = Path.GetFileName(dir);
            if (IgnoredDirectoryNames.Contains(dirName) || dirName.StartsWith("."))
                return;

            foreach (var file in Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(file);
                if (BlacklistedExeNames.Contains(fileName))
                    continue;

                var fi = new FileInfo(file);
                if (fi.Length < 25 * 1024)
                    continue;

                list.Add(fi);
            }

            foreach (var sub in Directory.GetDirectories(dir))
            {
                CollectCandidateExes(sub, list, depth + 1, maxDepth);
            }
        }
        catch { }
    }

    private static double CalculateExecutableScore(FileInfo fileInfo, string gameRootDir, string gameFolderName)
    {
        double score = 100.0;
        string exeName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        string relPath = Path.GetRelativePath(gameRootDir, fileInfo.FullName);

        string cleanFolder = NormalizeForMatching(gameFolderName);
        string cleanExe = NormalizeForMatching(exeName);

        if (cleanFolder.Equals(cleanExe, StringComparison.OrdinalIgnoreCase))
        {
            score += 250.0;
        }
        else if (cleanFolder.Contains(cleanExe, StringComparison.OrdinalIgnoreCase) || cleanExe.Contains(cleanFolder, StringComparison.OrdinalIgnoreCase))
        {
            score += 150.0;
        }
        else
        {
            double similarity = CalculateFuzzySimilarity(cleanFolder, cleanExe);
            score += similarity * 100.0;
        }

        // Relative depth scoring: Root or Depth 1 is heavily preferred
        bool isRoot = !relPath.Contains(Path.DirectorySeparatorChar) && !relPath.Contains(Path.AltDirectorySeparatorChar);
        int depth = relPath.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);

        if (isRoot)
        {
            score += 200.0; // Root folder executable is standard entry point
        }
        else if (depth == 1)
        {
            score += 60.0;
        }
        else
        {
            // Deep binaries like TBL\Binaries\Win64\TBL-Win64-Shipping.exe are raw engine binaries that skip DRM/EOS/EAC
            score -= (depth * 60.0);
        }

        string lowerName = exeName.ToLowerInvariant();
        if (lowerName.Contains("modrinth") || lowerName.Contains("curseforge") || lowerName.Contains("minecraftlauncher") || lowerName.Contains("prismlauncher") || lowerName.Contains("lunar client") || lowerName.Contains("feather"))
        {
            score += 150.0;
        }

        // Penalty for crash reporters, setup, redist, helpers
        if (lowerName.Contains("crash") || lowerName.Contains("report") || lowerName.Contains("unins") || lowerName.Contains("dxsetup") || lowerName.Contains("vcredist"))
        {
            score -= 150.0;
        }
        if (lowerName.Contains("helper") || lowerName.Contains("service") || lowerName.Contains("tool") || lowerName.Contains("util"))
        {
            score -= 60.0;
        }
        if (lowerName.Contains("redist") || lowerName.Contains("install") || lowerName.Contains("eula"))
        {
            score -= 80.0;
        }

        if (fileInfo.Length > 10 * 1024 * 1024) score += 20.0;
        else if (fileInfo.Length > 2 * 1024 * 1024) score += 10.0;

        return score;
    }

    private static double CalculateFuzzySimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0;
        
        var s1Words = Regex.Matches(s1, @"\b\w");
        string acronym1 = string.Concat(s1Words.Select(m => m.Value)).ToLowerInvariant();
        if (s2.ToLowerInvariant().Contains(acronym1) || acronym1.Contains(s2.ToLowerInvariant()))
        {
            return 0.85;
        }

        var set1 = new HashSet<char>(s1.ToLowerInvariant());
        var set2 = new HashSet<char>(s2.ToLowerInvariant());
        int intersect = set1.Intersect(set2).Count();
        int total = set1.Union(set2).Count();
        return total > 0 ? (double)intersect / total : 0;
    }

    private static string NormalizeForMatching(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        string cleaned = Regex.Replace(input, @"[_\-\.\s\(\)\[\]]", "").ToLowerInvariant();
        cleaned = cleaned.Replace("shipping", "").Replace("win64", "").Replace("x64", "").Replace("the", "");
        return cleaned;
    }

    private static string FormatCleanTitle(string folderName)
    {
        var cleaned = folderName.Replace('_', ' ').Trim();
        cleaned = Regex.Replace(cleaned, @"\s*\((Digital Deluxe|Definitive Edition|GOTY|Enhanced Edition)\)", "", RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(cleaned) ? folderName : cleaned;
    }

    public static string DetectPlatform(string exePath)
    {
        string lower = exePath.ToLowerInvariant();
        if (lower.Contains("steamapps") || lower.Contains("steam")) return "Steam";
        if (lower.Contains("epic games") || lower.Contains("epicgames")) return "Epic Games";
        if (lower.Contains("riot games") || lower.Contains("league of legends") || lower.Contains("valorant") || lower.Contains("riotclient")) return "Riot Games";
        if (lower.Contains("minecraft") || lower.Contains("modrinth") || lower.Contains("curseforge") || lower.Contains("lunarclient") || lower.Contains("feather") || lower.Contains("prismlauncher") || lower.Contains("badlion") || lower.Contains("tlauncher") || lower.Contains(".minecraft") || lower.Contains("bedrock")) return "Minecraft";
        if (lower.Contains("gog galaxy") || lower.Contains("gog games")) return "GOG";
        if (lower.Contains("ubisoft") || lower.Contains("uplay")) return "Ubisoft";
        if (lower.Contains("ea games") || lower.Contains("origin games")) return "EA App";
        if (lower.Contains("rpcs3") || lower.Contains("pcsx2") || lower.Contains("dolphin") || lower.Contains("retroarch")) return "Emulador";
        return "PC / Outros";
    }
}
