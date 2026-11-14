using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AllInBox.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNullOrEmpty = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
        if (Invert) isNullOrEmpty = !isNullOrEmpty;
        return isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FavoriteStarIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isFav = value is bool b && b;
        return isFav ? "★" : "☆";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FavoriteStarBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isFav = value is bool b && b;
        if (isFav)
        {
            return Application.Current?.Resources["WarningBrush"] as Brush
                   ?? new SolidColorBrush(Color.FromRgb(210, 153, 34));
        }
        return Application.Current?.Resources["TextMutedBrush"] as Brush
               ?? new SolidColorBrush(Color.FromRgb(100, 116, 139));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SafeImagePathConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static BitmapImage? _defaultIcon;

    public static void InvalidateCache(string? path = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            Cache.Clear();
        else
            Cache.TryRemove(path, out _);
    }

    private static BitmapImage? DefaultIcon
    {
        get
        {
            if (_defaultIcon == null)
            {
                try
                {
                    var uri = new Uri("pack://application:,,,/Assets/icon_all_in_box.png", UriKind.Absolute);
                    var bmp = new BitmapImage(uri);
                    bmp.Freeze();
                    _defaultIcon = bmp;
                }
                catch { }
            }
            return _defaultIcon;
        }
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            if (File.Exists(path))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 128; // Optimize memory and GPU upload
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();

                    Cache[path] = bitmap;
                    return bitmap;
                }
                catch
                {
                    return DefaultIcon;
                }
            }
        }
        return DefaultIcon;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PlatformBadgeBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string platform = value as string ?? string.Empty;

        return platform.ToLowerInvariant() switch
        {
            "steam" => new SolidColorBrush(Color.FromRgb(22, 45, 75)),
            "epic games" => new SolidColorBrush(Color.FromRgb(226, 232, 240)), // Light silver for Epic
            "gog" => new SolidColorBrush(Color.FromRgb(78, 28, 96)),
            "riot games" => new SolidColorBrush(Color.FromRgb(168, 24, 38)), // Riot Red
            "minecraft" => new SolidColorBrush(Color.FromRgb(46, 92, 42)),  // Minecraft Green
            "ubisoft" => new SolidColorBrush(Color.FromRgb(0, 85, 155)),    // Ubisoft Blue
            "ea app" => new SolidColorBrush(Color.FromRgb(165, 35, 35)),
            "emulador" => new SolidColorBrush(Color.FromRgb(24, 85, 70)),
            _ => new SolidColorBrush(Color.FromRgb(35, 45, 62))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PlatformBadgeForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string platform = value as string ?? string.Empty;

        if (platform.Equals("epic games", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Color.FromRgb(15, 20, 30));
        }

        return new SolidColorBrush(Color.FromRgb(255, 255, 255));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PlatformIconConverter : IValueConverter
{
    private static readonly BitmapImage? SteamIcon = LoadAssetImage("steam_icon.png");
    private static readonly BitmapImage? EpicIcon = LoadAssetImage("epic_icon.png");
    private static readonly BitmapImage? GogIcon = LoadAssetImage("gog_icon.png");
    private static readonly BitmapImage? RiotIcon = LoadAssetImage("riot_icon.png");
    private static readonly BitmapImage? MinecraftIcon = LoadAssetImage("minecraft_icon.png");
    private static readonly BitmapImage? UbisoftIcon = LoadAssetImage("ubisoft_icon.png");

    private static BitmapImage? LoadAssetImage(string fileName)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/{fileName}", UriKind.Absolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 48;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string platform = value as string ?? string.Empty;
        string lower = platform.ToLowerInvariant();

        if (lower.Contains("steam")) return SteamIcon;
        if (lower.Contains("epic")) return EpicIcon;
        if (lower.Contains("gog")) return GogIcon;
        if (lower.Contains("riot") || lower.Contains("valorant") || lower.Contains("league of legends")) return RiotIcon;
        if (lower.Contains("minecraft")) return MinecraftIcon;
        if (lower.Contains("ubisoft") || lower.Contains("uplay")) return UbisoftIcon;

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
