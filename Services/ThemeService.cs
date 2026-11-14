using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace AllInBox.Services;

public static class ThemeService
{
    public static event Action? ThemeChanged;

    public static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                object? val = key.GetValue("AppsUseLightTheme");
                if (val is int i)
                {
                    return i == 1;
                }
            }
        }
        catch { }
        return false; // Default to Dark on error
    }

    public static void Initialize()
    {
        ApplyCurrentSettings();

        // Listen for Windows 10 system theme changes dynamically
        try
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General &&
                    string.Equals(AppSettings.Instance.Theme, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    Application.Current?.Dispatcher?.BeginInvoke(ApplyCurrentSettings);
                }
            };
        }
        catch { }
    }

    public static void ApplyCurrentSettings()
    {
        var settings = AppSettings.Instance;
        string mode = settings.Theme?.ToLowerInvariant() ?? "auto";

        if (mode == "auto")
        {
            mode = IsSystemLightTheme() ? "light" : "dark";
        }

        if (mode == "light")
        {
            ApplyLightPaperTheme();
        }
        else if (mode == "custom")
        {
            ApplyCustomTheme(
                settings.CustomBgHex,
                settings.CustomSurfaceHex,
                settings.CustomAccentHex,
                settings.CustomTextHex
            );
        }
        else
        {
            ApplyDarkSlateTheme();
        }

        ThemeChanged?.Invoke();
    }

    public static void ApplyDarkSlateTheme()
    {
        // Preto-azul-cinza (padrão escuro) com azul leve em accents
        SetBrush("BgBrush", "#0F1319");
        SetBrush("PanelBgBrush", "#131820");
        SetBrush("CardBgBrush", "#171D26");
        SetBrush("CardHoverBgBrush", "#1D2531");
        SetBrush("CardSelectedBgBrush", "#1B283A");
        SetBrush("CardBorderBrush", "#242D3C");
        SetBrush("DividerBrush", "#1E2633");
        SetBrush("InputBgBrush", "#11151C");
        SetBrush("BadgeBgBrush", "#1C2432");

        SetBrush("AccentBrush", "#4A8FE7");
        SetBrush("AccentHoverBrush", "#63A1F2");
        SetBrush("AccentPressedBrush", "#3978CA");
        SetBrush("AccentSubtleBgBrush", "#18263A");
        SetBrush("AccentForegroundBrush", "#FFFFFF");

        SetBrush("TextPrimaryBrush", "#E6EDF3");
        SetBrush("TextSecondaryBrush", "#94A3B8");
        SetBrush("TextMutedBrush", "#64748B");

        SetBrush("SuccessBrush", "#2EA043");
        SetBrush("WarningBrush", "#D29922");
        SetBrush("DangerBrush", "#E5534B");
    }

    public static void ApplyLightPaperTheme()
    {
        // Branco-papel com accent azul-papel-branco
        SetBrush("BgBrush", "#F5F3EE");
        SetBrush("PanelBgBrush", "#EBE8E1");
        SetBrush("CardBgBrush", "#FAF9F5");
        SetBrush("CardHoverBgBrush", "#EFECE6");
        SetBrush("CardSelectedBgBrush", "#E1EAF4");
        SetBrush("CardBorderBrush", "#D6D2C8");
        SetBrush("DividerBrush", "#DFDCD3");
        SetBrush("InputBgBrush", "#FFFFFF");
        SetBrush("BadgeBgBrush", "#E2DFD7");

        SetBrush("AccentBrush", "#3A6EA5");
        SetBrush("AccentHoverBrush", "#4B80B8");
        SetBrush("AccentPressedBrush", "#2C5887");
        SetBrush("AccentSubtleBgBrush", "#DCE6F2");
        SetBrush("AccentForegroundBrush", "#FAF9F5");

        SetBrush("TextPrimaryBrush", "#1C2127");
        SetBrush("TextSecondaryBrush", "#4D5766");
        SetBrush("TextMutedBrush", "#768190");

        SetBrush("SuccessBrush", "#1F883D");
        SetBrush("WarningBrush", "#9A6700");
        SetBrush("DangerBrush", "#CF222E");
    }

    public static void ApplyCustomTheme(string bgHex, string surfaceHex, string accentHex, string textHex)
    {
        Color bg = ParseColor(bgHex, Color.FromRgb(15, 19, 25));
        Color surface = ParseColor(surfaceHex, Color.FromRgb(23, 29, 38));
        Color accent = ParseColor(accentHex, Color.FromRgb(74, 143, 231));
        Color text = ParseColor(textHex, Color.FromRgb(230, 237, 243));

        bool isLight = GetPerceivedBrightness(bg) > 140;

        Color panelBg = Blend(bg, surface, 0.55);
        Color hoverBg = isLight ? Darken(surface, 0.05) : Lighten(surface, 0.06);
        Color border = isLight ? Darken(surface, 0.14) : Lighten(surface, 0.10);
        Color divider = isLight ? Darken(bg, 0.09) : Lighten(bg, 0.08);
        Color inputBg = isLight ? Lighten(surface, 0.04) : Darken(bg, 0.03);
        Color selectedBg = Blend(surface, accent, 0.18);
        Color secondaryText = Blend(text, bg, 0.35);
        Color mutedText = Blend(text, bg, 0.55);
        Color accentFg = GetPerceivedBrightness(accent) > 165 ? Color.FromRgb(15, 19, 25) : Colors.White;

        SetColorBrush("BgBrush", bg);
        SetColorBrush("PanelBgBrush", panelBg);
        SetColorBrush("CardBgBrush", surface);
        SetColorBrush("CardHoverBgBrush", hoverBg);
        SetColorBrush("CardSelectedBgBrush", selectedBg);
        SetColorBrush("CardBorderBrush", border);
        SetColorBrush("DividerBrush", divider);
        SetColorBrush("InputBgBrush", inputBg);
        SetColorBrush("BadgeBgBrush", hoverBg);

        SetColorBrush("AccentBrush", accent);
        SetColorBrush("AccentHoverBrush", Lighten(accent, 0.08));
        SetColorBrush("AccentPressedBrush", Darken(accent, 0.08));
        SetColorBrush("AccentSubtleBgBrush", selectedBg);
        SetColorBrush("AccentForegroundBrush", accentFg);

        SetColorBrush("TextPrimaryBrush", text);
        SetColorBrush("TextSecondaryBrush", secondaryText);
        SetColorBrush("TextMutedBrush", mutedText);

        SetBrush("SuccessBrush", isLight ? "#1F883D" : "#2EA043");
        SetBrush("WarningBrush", isLight ? "#9A6700" : "#D29922");
        SetBrush("DangerBrush", isLight ? "#CF222E" : "#E5534B");
    }

    private static void SetBrush(string key, string hex)
    {
        var color = ParseColor(hex, Colors.Gray);
        SetColorBrush(key, color);
    }

    private static void SetColorBrush(string key, Color color)
    {
        if (Application.Current == null) return;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Application.Current.Resources[key] = brush;
    }

    public static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            if (!hex.StartsWith("#")) hex = "#" + hex.Trim();
            if (ColorConverter.ConvertFromString(hex) is Color c)
                return c;
        }
        catch { }
        return fallback;
    }

    private static double GetPerceivedBrightness(Color c)
    {
        return (c.R * 0.299) + (c.G * 0.587) + (c.B * 0.114);
    }

    private static Color Blend(Color c1, Color c2, double amount)
    {
        byte r = (byte)Math.Clamp(c1.R + (c2.R - c1.R) * amount, 0, 255);
        byte g = (byte)Math.Clamp(c1.G + (c2.G - c1.G) * amount, 0, 255);
        byte b = (byte)Math.Clamp(c1.B + (c2.B - c1.B) * amount, 0, 255);
        return Color.FromRgb(r, g, b);
    }

    private static Color Lighten(Color c, double factor)
    {
        return Blend(c, Colors.White, factor);
    }

    private static Color Darken(Color c, double factor)
    {
        return Blend(c, Colors.Black, factor);
    }
}

