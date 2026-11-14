using System;
using System.IO;
using System.Text.Json;

namespace AllInBox.Services;

public class AppSettings
{
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllInBox",
        "settings.json"
    );

    public string Language { get; set; } = "auto"; // "auto", "pt-BR", "en-US", "es-ES", "ja-JP"
    public string Theme { get; set; } = "auto"; // "auto", "dark", "light", "custom"
    public string CustomBgHex { get; set; } = "#0F1319";
    public string CustomSurfaceHex { get; set; } = "#151A22";
    public string CustomAccentHex { get; set; } = "#4A8FE7";
    public string CustomTextHex { get; set; } = "#E6EDF3";

    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool MinimizeToTrayOnLaunch { get; set; } = false;

    private static AppSettings? _instance;
    public static AppSettings Instance => _instance ??= Load();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch { }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(SettingsFile)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }
}
