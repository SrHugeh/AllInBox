using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AllInBox.Services;

namespace AllInBox.Views;

public partial class SettingsWindow : Window
{
    private bool _initialized;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadCurrentSettings();
        _initialized = true;
    }

    private void LoadCurrentSettings()
    {
        var settings = AppSettings.Instance;

        // 1. Language
        string currentLang = settings.Language;
        int langIndex = 0;
        for (int i = 0; i < CmbLanguage.Items.Count; i++)
        {
            if (CmbLanguage.Items[i] is ComboBoxItem item && item.Tag is string tag &&
                string.Equals(tag, currentLang, StringComparison.OrdinalIgnoreCase))
            {
                langIndex = i;
                break;
            }
        }
        CmbLanguage.SelectedIndex = langIndex;

        // 2. Theme
        string currentTheme = settings.Theme ?? "auto";
        int themeIndex = 0;
        for (int i = 0; i < CmbTheme.Items.Count; i++)
        {
            if (CmbTheme.Items[i] is ComboBoxItem item && item.Tag is string tag &&
                string.Equals(tag, currentTheme, StringComparison.OrdinalIgnoreCase))
            {
                themeIndex = i;
                break;
            }
        }
        CmbTheme.SelectedIndex = themeIndex;

        // 3. Custom Colors
        TxtCustomBg.Text = settings.CustomBgHex;
        TxtCustomSurface.Text = settings.CustomSurfaceHex;
        TxtCustomAccent.Text = settings.CustomAccentHex;
        TxtCustomText.Text = settings.CustomTextHex;
        UpdateSwatches();

        // 4. Tray Settings
        ChkMinimizeOnClose.IsChecked = settings.MinimizeToTrayOnClose;
        ChkMinimizeOnLaunch.IsChecked = settings.MinimizeToTrayOnLaunch;

        UpdateLocalizedTexts();
    }

    private void UpdateSwatches()
    {
        if (SwatchBg != null)
            SwatchBg.Background = new SolidColorBrush(ThemeService.ParseColor(TxtCustomBg.Text, Color.FromRgb(15, 19, 25)));
        if (SwatchSurface != null)
            SwatchSurface.Background = new SolidColorBrush(ThemeService.ParseColor(TxtCustomSurface.Text, Color.FromRgb(23, 29, 38)));
        if (SwatchAccent != null)
            SwatchAccent.Background = new SolidColorBrush(ThemeService.ParseColor(TxtCustomAccent.Text, Color.FromRgb(74, 143, 231)));
        if (SwatchText != null)
            SwatchText.Background = new SolidColorBrush(ThemeService.ParseColor(TxtCustomText.Text, Color.FromRgb(230, 237, 243)));
    }

    private void UpdateLocalizedTexts()
    {
        var loc = LocalizationService.Instance;
        TxtSectionLang.Text = loc.Get("Idioma");
        TxtLangSubtitle.Text = loc.Get("IdiomaDesc");
        BtnSaveClose.Content = loc.Get("Pronto");
    }

    private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        if (CmbTheme.SelectedItem is ComboBoxItem selected && selected.Tag is string themeCode)
        {
            var settings = AppSettings.Instance;
            settings.Theme = themeCode;
            settings.Save();
            ThemeService.ApplyCurrentSettings();
        }
    }

    private void CustomColorInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        UpdateSwatches();

        string bg = TxtCustomBg.Text?.Trim() ?? "";
        string surface = TxtCustomSurface.Text?.Trim() ?? "";
        string accent = TxtCustomAccent.Text?.Trim() ?? "";
        string text = TxtCustomText.Text?.Trim() ?? "";

        if (IsValidHex(bg) && IsValidHex(surface) && IsValidHex(accent) && IsValidHex(text))
        {
            var settings = AppSettings.Instance;
            settings.CustomBgHex = bg;
            settings.CustomSurfaceHex = surface;
            settings.CustomAccentHex = accent;
            settings.CustomTextHex = text;
            settings.Theme = "custom";
            settings.Save();

            _initialized = false;
            CmbTheme.SelectedIndex = 3; // Custom
            _initialized = true;

            ThemeService.ApplyCurrentSettings();
        }
    }

    private static bool IsValidHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return false;
        string clean = hex.StartsWith("#") ? hex[1..] : hex;
        return clean.Length == 6;
    }

    private void ApplyCustomPreset(string bg, string surface, string accent, string text, string themeMode = "custom")
    {
        _initialized = false;
        TxtCustomBg.Text = bg;
        TxtCustomSurface.Text = surface;
        TxtCustomAccent.Text = accent;
        TxtCustomText.Text = text;
        UpdateSwatches();

        var settings = AppSettings.Instance;
        settings.CustomBgHex = bg;
        settings.CustomSurfaceHex = surface;
        settings.CustomAccentHex = accent;
        settings.CustomTextHex = text;
        settings.Theme = themeMode;
        settings.Save();

        CmbTheme.SelectedIndex = themeMode switch
        {
            "dark" => 1,
            "light" => 2,
            "custom" => 3,
            _ => 0
        };
        _initialized = true;

        ThemeService.ApplyCurrentSettings();
    }

    private void BtnPresetDarkSlate_Click(object sender, RoutedEventArgs e)
    {
        ApplyCustomPreset("#0F1319", "#171D26", "#4A8FE7", "#E6EDF3", "dark");
    }

    private void BtnPresetLightPaper_Click(object sender, RoutedEventArgs e)
    {
        ApplyCustomPreset("#F5F3EE", "#FAF9F5", "#3A6EA5", "#1C2127", "light");
    }

    private void BtnPresetNordic_Click(object sender, RoutedEventArgs e)
    {
        ApplyCustomPreset("#161B22", "#1F2630", "#58A6FF", "#F0F6FC", "custom");
    }

    private void BtnPresetWarmPaper_Click(object sender, RoutedEventArgs e)
    {
        ApplyCustomPreset("#EFECE4", "#F7F5F0", "#2B5B84", "#22262A", "custom");
    }

    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        if (CmbLanguage.SelectedItem is ComboBoxItem selected && selected.Tag is string langCode)
        {
            LocalizationService.Instance.SetLanguage(langCode);
            UpdateLocalizedTexts();
        }
    }

    private void TraySetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        var settings = AppSettings.Instance;
        settings.MinimizeToTrayOnClose = ChkMinimizeOnClose.IsChecked == true;
        settings.MinimizeToTrayOnLaunch = ChkMinimizeOnLaunch.IsChecked == true;
        settings.Save();
    }

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
