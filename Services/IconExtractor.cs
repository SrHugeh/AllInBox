using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace AllInBox.Services;

public static class IconExtractor
{
    private static readonly string AppDataIconsDir = Path.Combine(AppPaths.DataDirectory, "Icons");

    static IconExtractor()
    {
        try
        {
            if (!Directory.Exists(AppDataIconsDir))
            {
                Directory.CreateDirectory(AppDataIconsDir);
            }
        }
        catch
        {
            // Ignore error
        }
    }

    public static string? ExtractAndCacheIcon(string filePath, string gameId, string? gameTitle = null, string? installDir = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) && string.IsNullOrWhiteSpace(installDir))
            return null;

        try
        {
            var bitmapSource = ResolveBestIconSource(filePath, gameTitle, installDir);
            if (bitmapSource == null)
                return null;

            string safeId = string.IsNullOrWhiteSpace(gameId) ? Guid.NewGuid().ToString() : gameId;
            string targetFile = Path.Combine(AppDataIconsDir, $"{safeId}.png");

            using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(fileStream);

            return targetFile;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsWrapperOrGenericExecutable(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        string name = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
        string lowerPath = filePath.ToLowerInvariant();

        if (name is "dotnet" or "javaw" or "java" or "busybox64" or "busybox" or "cmd" or "powershell" or "conhost" or "wscript" or "cscript")
            return true;

        if (lowerPath.EndsWith(".bat") || lowerPath.EndsWith(".cmd") || lowerPath.EndsWith(".sh"))
            return true;

        if (lowerPath.Contains(@"\dotnet\") || lowerPath.Contains(@"\launchutils\"))
            return true;

        return false;
    }

    public static BitmapSource? ResolveBestIconSource(string filePath, string? gameTitle = null, string? installDir = null)
    {
        // 1. Check if it's a wrapper/runtime executable (like tModLoader's dotnet.exe) or if Steam Registry has an explicit DisplayIcon
        bool isWrapper = IsWrapperOrGenericExecutable(filePath);

        string? steamIconPath = TryFindSteamIcon(filePath, gameTitle, installDir);
        if (isWrapper && !string.IsNullOrWhiteSpace(steamIconPath) && File.Exists(steamIconPath))
        {
            var steamBmp = LoadImageFileAsBitmapSource(steamIconPath) ?? ExtractIconSourceFromFile(steamIconPath);
            if (steamBmp != null) return steamBmp;
        }

        // 2. If it's a wrapper and no Steam icon matched yet, check the game directory for .ico or icon.png
        if (isWrapper)
        {
            string? localIcon = TryFindLocalFolderIcon(filePath, installDir);
            if (!string.IsNullOrWhiteSpace(localIcon) && File.Exists(localIcon))
            {
                var localBmp = LoadImageFileAsBitmapSource(localIcon) ?? ExtractIconSourceFromFile(localIcon);
                if (localBmp != null) return localBmp;
            }
        }

        // 3. Try extracting directly from executable if not a generic wrapper
        if (!isWrapper && !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            var exeBmp = ExtractIconSourceFromFile(filePath);
            if (exeBmp != null) return exeBmp;
        }

        // 4. Fallback to Steam icon even for non-wrappers if executable icon failed
        if (!string.IsNullOrWhiteSpace(steamIconPath) && File.Exists(steamIconPath))
        {
            var steamBmp = LoadImageFileAsBitmapSource(steamIconPath) ?? ExtractIconSourceFromFile(steamIconPath);
            if (steamBmp != null) return steamBmp;
        }

        // 5. Fallback to local folder .ico/.png
        string? fallbackLocal = TryFindLocalFolderIcon(filePath, installDir);
        if (!string.IsNullOrWhiteSpace(fallbackLocal) && File.Exists(fallbackLocal))
        {
            var localBmp = LoadImageFileAsBitmapSource(fallbackLocal) ?? ExtractIconSourceFromFile(fallbackLocal);
            if (localBmp != null) return localBmp;
        }

        // 6. Last resort: extract from filePath anyway
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            return ExtractIconSourceFromFile(filePath);
        }

        return null;
    }

    private static BitmapSource? LoadImageFileAsBitmapSource(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".ico" or ".png" or ".jpg" or ".jpeg" or ".bmp")
            {
                if (ext == ".ico")
                {
                    var extracted = ExtractIconSourceFromFile(path);
                    if (extracted != null) return extracted;
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 128;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }
        catch { }
        return null;
    }

    public static string? TryFindSteamIcon(string? filePath, string? gameTitle, string? installDir)
    {
        try
        {
            string targetRoot = GameLibraryService.ResolveGameRootDirectory(filePath ?? string.Empty, installDir ?? string.Empty);
            string folderName = !string.IsNullOrWhiteSpace(targetRoot) ? Path.GetFileName(targetRoot) : string.Empty;

            // A. Check Windows Registry Uninstall keys for Steam App *
            string[] regPaths =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var regPath in regPaths)
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(regPath);
                if (baseKey == null) continue;

                foreach (var subName in baseKey.GetSubKeyNames())
                {
                    if (!subName.StartsWith("Steam App ", StringComparison.OrdinalIgnoreCase))
                        continue;

                    using var subKey = baseKey.OpenSubKey(subName);
                    if (subKey == null) continue;

                    string? displayName = subKey.GetValue("DisplayName") as string;
                    string? displayIcon = subKey.GetValue("DisplayIcon") as string;
                    string? installLocation = subKey.GetValue("InstallLocation") as string;

                    if (string.IsNullOrWhiteSpace(displayIcon)) continue;

                    // Clean up icon path if it has ",0" index suffix
                    int commaIdx = displayIcon.LastIndexOf(',');
                    if (commaIdx > 0) displayIcon = displayIcon[..commaIdx].Trim('"');
                    else displayIcon = displayIcon.Trim('"');

                    if (!File.Exists(displayIcon)) continue;

                    bool matched = false;
                    if (!string.IsNullOrWhiteSpace(installLocation) && !string.IsNullOrWhiteSpace(targetRoot))
                    {
                        if (string.Equals(Path.GetFullPath(installLocation).TrimEnd('\\', '/'),
                                          Path.GetFullPath(targetRoot).TrimEnd('\\', '/'),
                                          StringComparison.OrdinalIgnoreCase))
                        {
                            matched = true;
                        }
                    }

                    if (!matched && !string.IsNullOrWhiteSpace(displayName))
                    {
                        if (!string.IsNullOrWhiteSpace(gameTitle) &&
                            NormalizeTitle(displayName).Equals(NormalizeTitle(gameTitle), StringComparison.OrdinalIgnoreCase))
                        {
                            matched = true;
                        }
                        else if (!string.IsNullOrWhiteSpace(folderName) &&
                                 NormalizeTitle(displayName).Equals(NormalizeTitle(folderName), StringComparison.OrdinalIgnoreCase))
                        {
                            matched = true;
                        }
                    }

                    if (matched)
                    {
                        return displayIcon;
                    }
                }
            }

            // B. Check Steam appmanifest_*.acf -> appid -> Steam\appcache\librarycache\<appid>
            if (!string.IsNullOrWhiteSpace(targetRoot))
            {
                var steamAppsDir = Directory.GetParent(targetRoot)?.Parent; // .../steamapps/common/<Game> -> .../steamapps
                if (steamAppsDir != null && steamAppsDir.Exists)
                {
                    foreach (var acfFile in steamAppsDir.GetFiles("appmanifest_*.acf"))
                    {
                        try
                        {
                            string content = File.ReadAllText(acfFile.FullName);
                            if (content.Contains($"\"installdir\"\t\t\"{folderName}\"", StringComparison.OrdinalIgnoreCase))
                            {
                                string fileName = Path.GetFileNameWithoutExtension(acfFile.Name); // appmanifest_1281930
                                string appId = fileName.Replace("appmanifest_", "");
                                var steamRoot = steamAppsDir.Parent;
                                if (steamRoot != null)
                                {
                                    string libCacheDir = Path.Combine(steamRoot.FullName, "appcache", "librarycache", appId);
                                    if (Directory.Exists(libCacheDir))
                                    {
                                        // Find square icon hash jpg or logo.png
                                        var hashSquare = Directory.GetFiles(libCacheDir, "*.jpg")
                                            .FirstOrDefault(f => !Path.GetFileName(f).StartsWith("header", StringComparison.OrdinalIgnoreCase) &&
                                                                 !Path.GetFileName(f).StartsWith("library_", StringComparison.OrdinalIgnoreCase));
                                        if (hashSquare != null) return hashSquare;

                                        string logoPng = Path.Combine(libCacheDir, "logo.png");
                                        if (File.Exists(logoPng)) return logoPng;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private static string? TryFindLocalFolderIcon(string? filePath, string? installDir)
    {
        try
        {
            string root = GameLibraryService.ResolveGameRootDirectory(filePath ?? string.Empty, installDir ?? string.Empty);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;

            var icoFiles = Directory.GetFiles(root, "*.ico", SearchOption.TopDirectoryOnly);
            if (icoFiles.Length > 0) return icoFiles[0];

            var pngFiles = Directory.GetFiles(root, "*icon*.png", SearchOption.TopDirectoryOnly);
            if (pngFiles.Length > 0) return pngFiles[0];
        }
        catch { }
        return null;
    }

    private static string NormalizeTitle(string s)
    {
        return new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public static BitmapSource? ExtractIconSource(string filePath)
    {
        return ResolveBestIconSource(filePath);
    }

    public static BitmapSource? ExtractIconSourceFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        IntPtr[] phicon = new IntPtr[1];
        uint[] piconid = new uint[1];

        int[] sizesToTry = { 256, 128, 64, 48, 32 };
        uint count = 0;

        foreach (int size in sizesToTry)
        {
            count = PrivateExtractIcons(filePath, 0, size, size, phicon, piconid, 1, 0);
            if (count > 0 && phicon[0] != IntPtr.Zero)
                break;
        }

        if (count > 0 && phicon[0] != IntPtr.Zero)
        {
            try
            {
                var bs = Imaging.CreateBitmapSourceFromHIcon(
                    phicon[0],
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                );
                bs.Freeze();
                return bs;
            }
            finally
            {
                DestroyIcon(phicon[0]);
            }
        }

        // Final fallback: SHGetFileInfo
        var shinfo = new SHFILEINFO();
        IntPtr hImg = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
        if (hImg != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
        {
            try
            {
                var bs = Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                );
                bs.Freeze();
                return bs;
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }

        return null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern uint PrivateExtractIcons(
        string szFileName,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        IntPtr[] phicon,
        uint[] piconid,
        uint nIcons,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
