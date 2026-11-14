using System;
using System.IO;

namespace AllInBox.Services;

/// <summary>Single source of truth for where the app keeps its data (library, settings, icon cache, WebView2 profile).</summary>
public static class AppPaths
{
    /// <summary>
    /// %LocalAppData%\AllInBox by default. Setting the ALLINBOX_DATA_DIR environment variable redirects
    /// everything, so tests and demos can run against a throw-away folder instead of the real library.
    /// </summary>
    public static string DataDirectory { get; } = ResolveDataDirectory();

    private static string ResolveDataDirectory()
    {
        string? custom = Environment.GetEnvironmentVariable("ALLINBOX_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(custom)) return custom;

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AllInBox");
    }
}
