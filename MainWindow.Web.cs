using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AllInBox.Models;
using AllInBox.Services;
using AllInBox.Views;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace AllInBox;

/// <summary>
/// Bridge between the WPF shell and the HTML front-end (Web/index.html).
/// JS -> C#: chrome.webview.postMessage({ type, ... })      (handled in Web_WebMessageReceived)
/// C# -> JS: CoreWebView2.PostWebMessageAsJson({ type, ... }) (init, games, theme, lang, toast, busy, win, picked, scan*)
/// </summary>
public partial class MainWindow
{
    private const string AppHost = "app.allinbox";
    private const string IconHost = "icons.allinbox";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".ico", ".bmp", ".gif", ".webp"
    };

    private static readonly string[] ScanAllPlatforms = { "Steam", "Epic Games", "Riot Games", "Minecraft", "Ubisoft", "GOG" };

    private readonly GameScannerService _scanner = new();
    private Dictionary<string, DiscoveredGame> _lastScan = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherTimer? _pushTimer;
    private bool _webReady;

    #region WebView2 setup

    private async Task InitializeWebAsync()
    {
        try
        {
            string userData = Path.Combine(AppPaths.DataDirectory, "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await Web.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            AppDialogWindow.Show(this,
                "O All In Box precisa do Microsoft Edge WebView2 Runtime para exibir a interface.\n\n" +
                "Instale em: https://developer.microsoft.com/microsoft-edge/webview2/\n\n" + ex.Message,
                "WebView2 não encontrado", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var core = Web.CoreWebView2;
        var settings = core.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
#if DEBUG
        settings.AreDevToolsEnabled = true;
#else
        settings.AreDevToolsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
#endif
        // Lets the page's title bar (CSS `app-region: drag`) move, snap and maximize the window like a native caption.
        try { settings.IsNonClientRegionSupportEnabled = true; } catch { /* older runtime: title bar just won't drag */ }

        string webDir = Path.Combine(AppContext.BaseDirectory, "Web");
        core.SetVirtualHostNameToFolderMapping(AppHost, webDir, CoreWebView2HostResourceAccessKind.Allow);

        core.AddWebResourceRequestedFilter($"https://{IconHost}/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += Web_WebResourceRequested;
        core.WebMessageReceived += Web_WebMessageReceived;

        // The page is the whole UI: never let it navigate away (e.g. when a file is dropped on it).
        core.NavigationStarting += (s, e) =>
        {
            if (!e.Uri.StartsWith($"https://{AppHost}/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true;
        };
        core.NewWindowRequested += (s, e) => e.Handled = true;

        ApplyWebBackground();
        core.Navigate($"https://{AppHost}/index.html");
    }

    private void ApplyWebBackground()
    {
        if (Application.Current?.Resources["BgBrush"] is SolidColorBrush b)
        {
            Web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(b.Color.A, b.Color.R, b.Color.G, b.Color.B);
        }
    }

    /// <summary>Serves game icons from disk to the page: https://icons.allinbox/?p=&lt;escaped path&gt;</summary>
    private void Web_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var uri = new Uri(e.Request.Uri);
            string? path = null;
            foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("p=", StringComparison.Ordinal))
                {
                    path = Uri.UnescapeDataString(part[2..]);
                    break;
                }
            }

            if (!string.IsNullOrEmpty(path) && Path.IsPathRooted(path) &&
                ImageExtensions.Contains(Path.GetExtension(path)) && File.Exists(path))
            {
                string contentType = Path.GetExtension(path).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".ico" => "image/x-icon",
                    ".bmp" => "image/bmp",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/png"
                };
                var stream = new MemoryStream(File.ReadAllBytes(path));
                e.Response = Web.CoreWebView2.Environment.CreateWebResourceResponse(
                    stream, 200, "OK", $"Content-Type: {contentType}\r\nCache-Control: max-age=86400");
                return;
            }
        }
        catch { }

        e.Response = Web.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "");
    }

    #endregion

    #region C# -> JS

    private void Post(object message)
    {
        if (!_webReady || Web?.CoreWebView2 == null) return;
        Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }

    private static string IconUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ImageExtensions.Contains(Path.GetExtension(path)) || !File.Exists(path))
            return string.Empty;

        long version = 0;
        try { version = File.GetLastWriteTimeUtc(path).Ticks; } catch { }
        return $"https://{IconHost}/?p={Uri.EscapeDataString(path)}&v={version}";
    }

    private static long? ToUnixMs(DateTime? value) =>
        value.HasValue ? new DateTimeOffset(value.Value).ToUnixTimeMilliseconds() : null;

    private static object ToDto(GameItem g) => new
    {
        id = g.Id,
        title = g.Title,
        platform = g.Platform,
        category = g.Category,
        favorite = g.IsFavorite,
        hidden = g.IsHidden,
        running = g.IsRunning,
        initial = g.Initial,
        icon = IconUrl(g.IconPath),
        cover = string.Empty,
        exe = g.ExecutablePath,
        workDir = g.WorkingDirectory,
        args = g.Arguments,
        notes = g.Notes,
        size = g.InstallSizeBytes,
        sizeText = g.SizeText,
        playSeconds = g.PlayTimeSeconds,
        playText = g.HasPlayTime ? g.PlayTimeText : string.Empty,
        lastPlayed = ToUnixMs(g.LastPlayed),
        lastText = g.HasLastPlayed ? g.LastPlayedText : string.Empty,
        added = ToUnixMs(g.DateAdded) ?? 0
    };

    private static readonly (string Css, string Key)[] ThemeMap =
    {
        ("bg", "BgBrush"), ("panel", "PanelBgBrush"), ("card", "CardBgBrush"), ("card-hover", "CardHoverBgBrush"),
        ("card-selected", "CardSelectedBgBrush"), ("card-border", "CardBorderBrush"), ("divider", "DividerBrush"),
        ("input", "InputBgBrush"), ("badge", "BadgeBgBrush"), ("accent", "AccentBrush"), ("accent-hover", "AccentHoverBrush"),
        ("accent-pressed", "AccentPressedBrush"), ("accent-subtle", "AccentSubtleBgBrush"), ("accent-fg", "AccentForegroundBrush"),
        ("text", "TextPrimaryBrush"), ("text-2", "TextSecondaryBrush"), ("text-3", "TextMutedBrush"),
        ("success", "SuccessBrush"), ("warning", "WarningBrush"), ("danger", "DangerBrush")
    };

    /// <summary>The WPF theme brushes as CSS custom properties, so the page always matches the active theme.</summary>
    private static Dictionary<string, string> ThemeVars()
    {
        var vars = new Dictionary<string, string>();
        foreach (var (css, key) in ThemeMap)
        {
            if (Application.Current?.Resources[key] is SolidColorBrush b)
            {
                vars[css] = $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}";
            }
        }
        return vars;
    }

    private static object SettingsDto()
    {
        var s = AppSettings.Instance;
        return new
        {
            language = string.IsNullOrWhiteSpace(s.Language) ? "auto" : s.Language,
            theme = s.Theme ?? "auto",
            bg = s.CustomBgHex,
            surface = s.CustomSurfaceHex,
            accent = s.CustomAccentHex,
            text = s.CustomTextHex,
            trayClose = s.MinimizeToTrayOnClose,
            trayLaunch = s.MinimizeToTrayOnLaunch
        };
    }

    private void PushTheme()
    {
        ApplyWebBackground();
        Post(new
        {
            type = "theme",
            vars = ThemeVars(),
            themeMode = AppSettings.Instance.Theme ?? "auto",
            systemLight = ThemeService.IsSystemLightTheme(),
            settings = SettingsDto()
        });
    }

    private void PushLanguage() => Post(new { type = "lang", lang = LocalizationService.Instance.CurrentLanguageCode, settings = SettingsDto() });

    private void PushGames() => Post(new { type = "games", games = _libraryService.Games.Select(ToDto).ToList() });

    private void SendInit() => Post(new
    {
        type = "init",
        lang = LocalizationService.Instance.CurrentLanguageCode,
        theme = ThemeVars(),
        themeMode = AppSettings.Instance.Theme ?? "auto",
        systemLight = ThemeService.IsSystemLightTheme(),
        settings = SettingsDto(),
        maximized = WindowState == WindowState.Maximized,
        games = _libraryService.Games.Select(ToDto).ToList()
    });

    /// <summary>Coalesces bursts of changes (icon extraction, size scans) into a single update.</summary>
    private void SchedulePush()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(SchedulePush);
            return;
        }

        if (_pushTimer == null)
        {
            _pushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _pushTimer.Tick += (s, e) =>
            {
                _pushTimer!.Stop();
                PushGames();
            };
        }
        _pushTimer.Stop();
        _pushTimer.Start();
    }

    private void HookLibrary()
    {
        foreach (var g in _libraryService.Games) Subscribe(g);

        _libraryService.Games.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null) foreach (GameItem g in e.NewItems) Subscribe(g);
            if (e.OldItems != null) foreach (GameItem g in e.OldItems) g.PropertyChanged -= GameChanged;

            SchedulePush();
            _trayService.RebuildContextMenu();
        };
    }

    private void Subscribe(GameItem g)
    {
        g.PropertyChanged -= GameChanged; // avoid double subscription when items are re-added
        g.PropertyChanged += GameChanged;
    }

    private void GameChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameItem.IsSelected)) return;
        SchedulePush();
    }

    /// <summary>In-page toast.</summary>
    private void Notify(string text) => Post(new { type = "toast", text });

    private void SetBusy(bool busy) => Post(new { type = "busy", value = busy });

    #endregion

    #region JS -> C#

    private async void Web_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            string type = root.GetProperty("type").GetString() ?? string.Empty;

            switch (type)
            {
                case "ready":
                    _webReady = true;
                    SendInit();
                    CleanUpNonGamesOnce();
                    break;

                case "win":
                    HandleWindowAction(Str(root, "action"));
                    break;

                case "themeCycle":
                    CycleTheme();
                    break;

                case "setSetting":
                    ApplySetting(Str(root, "key"), root.GetProperty("value"));
                    break;

                case "applyPreset":
                    ApplyCustomColors(Str(root, "bg"), Str(root, "surface"), Str(root, "accent"), Str(root, "text"), Str(root, "mode"));
                    break;

                case "setCustomColors":
                    ApplyCustomColors(Str(root, "bg"), Str(root, "surface"), Str(root, "accent"), Str(root, "text"), "custom");
                    break;

                case "play":
                    if (FindGame(root) is { } toPlay) await LaunchGameFromTrayOrUIAsync(toPlay);
                    break;

                case "fav":
                    if (FindGame(root) is { } toFav)
                    {
                        toFav.IsFavorite = !toFav.IsFavorite;
                        _ = Task.Run(() => _libraryService.SaveGames());
                    }
                    break;

                case "hide":
                    SetHidden(FindGames(root), root.GetProperty("hidden").GetBoolean());
                    break;

                case "remove":
                    RemoveGames(FindGames(root));
                    break;

                case "folder":
                    if (FindGame(root) is { } toOpen) OpenGameFolder(toOpen);
                    break;

                case "pickExe":
                    PickExecutable();
                    break;

                case "pickIcon":
                    await PickIconAsync();
                    break;

                case "pickFolder":
                    PickFolder();
                    break;

                case "previewIcon":
                    await PreviewIconAsync(Str(root, "path"), Str(root, "title"), Str(root, "workDir"));
                    break;

                case "saveGame":
                    await SaveGameAsync(root);
                    break;

                case "scanRun":
                    await RunScanAsync(root);
                    break;

                case "scanImport":
                    await ImportScanSelectionAsync(root);
                    break;

                case "optimize":
                    await OptimizeLibraryAsync();
                    break;

                case "drop":
                    // File handles must be read inside the handler, before the first await
                    var dropped = e.AdditionalObjects?.OfType<CoreWebView2File>().Select(f => f.Path).ToList() ?? new List<string>();
                    await HandleDroppedPathsAsync(dropped);
                    break;
            }
        }
        catch (Exception ex)
        {
            Notify($"Erro: {ex.Message}");
        }
    }

    private static string Str(JsonElement msg, string name) =>
        msg.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private GameItem? FindGame(JsonElement msg) =>
        msg.TryGetProperty("id", out var id) ? _libraryService.Games.FirstOrDefault(g => g.Id == id.GetString()) : null;

    private List<GameItem> FindGames(JsonElement msg)
    {
        if (!msg.TryGetProperty("ids", out var ids)) return new List<GameItem>();
        var wanted = ids.EnumerateArray().Select(x => x.GetString()).ToHashSet();
        return _libraryService.Games.Where(g => wanted.Contains(g.Id)).ToList();
    }

    #endregion

    #region Settings

    private static bool IsValidHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return false;
        string clean = hex.StartsWith('#') ? hex[1..] : hex;
        return clean.Length == 6 && clean.All(Uri.IsHexDigit);
    }

    private void ApplySetting(string key, JsonElement value)
    {
        var settings = AppSettings.Instance;
        switch (key)
        {
            case "language":
                LocalizationService.Instance.SetLanguage(value.GetString() ?? "auto"); // saves, then raises LanguageChanged
                return;
            case "theme":
                settings.Theme = value.GetString() ?? "auto";
                settings.Save();
                ThemeService.ApplyCurrentSettings(); // raises ThemeChanged -> PushTheme
                return;
            case "trayClose":
                settings.MinimizeToTrayOnClose = value.GetBoolean();
                break;
            case "trayLaunch":
                settings.MinimizeToTrayOnLaunch = value.GetBoolean();
                break;
        }
        settings.Save();
        PushTheme();
    }

    private void ApplyCustomColors(string bg, string surface, string accent, string text, string mode)
    {
        if (!IsValidHex(bg) || !IsValidHex(surface) || !IsValidHex(accent) || !IsValidHex(text)) return;

        var settings = AppSettings.Instance;
        settings.CustomBgHex = bg;
        settings.CustomSurfaceHex = surface;
        settings.CustomAccentHex = accent;
        settings.CustomTextHex = text;
        settings.Theme = string.IsNullOrWhiteSpace(mode) ? "custom" : mode;
        settings.Save();
        ThemeService.ApplyCurrentSettings();
    }

    #endregion

    #region Game actions

    private void SetHidden(IReadOnlyCollection<GameItem> targets, bool hidden)
    {
        if (targets.Count == 0) return;
        foreach (var g in targets) g.IsHidden = hidden;

        _libraryService.SaveGames();
        _trayService.RebuildContextMenu();
        SchedulePush();

        Notify(hidden
            ? $"{targets.Count} item(ns) ocultado(s). Eles continuam na aba Ocultos e podem ser restaurados."
            : $"{targets.Count} item(ns) restaurado(s) para a biblioteca.");
    }

    /// <summary>The page already asked for confirmation.</summary>
    private void RemoveGames(IReadOnlyCollection<GameItem> targets)
    {
        if (targets.Count == 0) return;

        string first = targets.First().Title;
        foreach (var g in targets) _libraryService.RemoveGame(g);

        _trayService.RebuildContextMenu();
        SchedulePush();
        Notify(targets.Count == 1
            ? $"Jogo '{first}' removido da biblioteca."
            : $"{targets.Count} jogo(s) removido(s) da biblioteca.");
    }

    private void OpenGameFolder(GameItem game)
    {
        try
        {
            if (File.Exists(game.ExecutablePath))
            {
                Process.Start("explorer.exe", $"/select,\"{game.ExecutablePath}\"");
            }
            else if (Directory.Exists(game.WorkingDirectory))
            {
                Process.Start("explorer.exe", $"\"{game.WorkingDirectory}\"");
            }
        }
        catch (Exception ex)
        {
            AppDialogWindow.Show(this, $"Não foi possível abrir o local do arquivo:\n\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OptimizeLibraryAsync()
    {
        SetBusy(true);

        try
        {
            int removed = _libraryService.DeduplicateExistingLibrary();
            int hiddenNonGames = _libraryService.HideSuspectedNonGames();
            await _libraryService.RefreshIconsAndSizesInBackgroundAsync(() =>
            {
                SchedulePush();
                _trayService.RebuildContextMenu();
            });

            string hiddenNote = hiddenNonGames > 0
                ? $" {hiddenNonGames} item(ns) que não parecem jogos foram ocultados (aba Ocultos)."
                : string.Empty;
            Notify((removed > 0
                ? $"Otimização concluída: {removed} jogo(s) duplicado(s) removido(s) e ícones/tamanhos atualizados."
                : "Biblioteca otimizada: ícones e tamanhos atualizados, sem duplicados.") + hiddenNote);
        }
        finally
        {
            SetBusy(false);
            SchedulePush();
        }
    }

    /// <summary>Runs the non-game cleanup a single time on libraries that existed before the feature.</summary>
    private void CleanUpNonGamesOnce()
    {
        var settings = AppSettings.Instance;
        if (settings.NonGameCleanupDone) return;

        settings.NonGameCleanupDone = true;
        settings.Save();

        int hidden = _libraryService.HideSuspectedNonGames();
        if (hidden > 0)
        {
            SchedulePush();
            Notify($"{hidden} item(ns) que não parecem jogos foram ocultados (aba Ocultos).");
        }
    }

    #endregion

    #region Add / edit game (file pickers + save)

    private static string DetectPlatform(string exePath)
    {
        string lower = exePath.ToLowerInvariant();
        if (lower.Contains("steamapps") || lower.Contains("steam")) return "Steam";
        if (lower.Contains("epic games") || lower.Contains("epicgames")) return "Epic Games";
        if (lower.Contains("gog galaxy") || lower.Contains("gog games")) return "GOG";
        if (lower.Contains("ubisoft")) return "Ubisoft";
        if (lower.Contains("ea games") || lower.Contains("origin games")) return "EA App";
        if (lower.Contains("rpcs3") || lower.Contains("pcsx2") || lower.Contains("dolphin") || lower.Contains("retroarch")) return "Emulador";
        return "PC / Outros";
    }

    /// <summary>Icon file the page can display for a given executable/image (cached extraction for .exe/.ico).</summary>
    private static string PreviewIconFor(string path, string title, string workDir)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;

        string ext = Path.GetExtension(path);
        if (ImageExtensions.Contains(ext) && !ext.Equals(".ico", StringComparison.OrdinalIgnoreCase)) return IconUrl(path);

        string? cached = IconExtractor.ExtractAndCacheIcon(path, "preview", title, workDir);
        return IconUrl(cached);
    }

    private void PickExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar Executável ou Atalho do Jogo",
            Filter = "Executáveis e Atalhos (*.exe;*.bat;*.lnk;*.url)|*.exe;*.bat;*.lnk;*.url|Todos os Arquivos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        string selected = dialog.FileName;
        var (targetExe, workDir, args) = ShortcutResolver.ResolveShortcut(selected);
        string title = GameLibraryService.InferGameTitleFromPath(selected, targetExe);

        Post(new
        {
            type = "picked",
            kind = "exe",
            path = targetExe,
            workDir,
            args,
            title,
            platform = DetectPlatform(targetExe),
            icon = PreviewIconFor(targetExe, title, workDir)
        });
    }

    private async Task PickIconAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar Ícone ou Imagem",
            Filter = "Imagens e Ícones (*.ico;*.png;*.jpg;*.jpeg;*.exe)|*.ico;*.png;*.jpg;*.jpeg;*.exe|Todos os Arquivos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        string path = dialog.FileName;
        string icon = await Task.Run(() => PreviewIconFor(path, string.Empty, string.Empty));
        Post(new { type = "picked", kind = "icon", path, icon });
    }

    private void PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Selecionar Pasta para Escanear Jogos", Multiselect = false };
        if (dialog.ShowDialog(this) == true)
        {
            Post(new { type = "picked", kind = "folder", path = dialog.FolderName });
        }
    }

    private async Task PreviewIconAsync(string path, string title, string workDir)
    {
        string icon = await Task.Run(() => PreviewIconFor(path, title, workDir));
        Post(new { type = "picked", kind = "preview", icon });
    }

    private async Task SaveGameAsync(JsonElement f)
    {
        string exePath = Str(f, "exe").Trim();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            Post(new { type = "saveResult", ok = false, message = "Informe ou selecione o executável do jogo." });
            return;
        }

        string? id = f.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
        var existing = id == null ? null : _libraryService.Games.FirstOrDefault(g => g.Id == id);
        bool isNew = existing == null;
        var game = existing ?? new GameItem();

        string title = Str(f, "title").Trim();
        if (string.IsNullOrWhiteSpace(title)) title = Path.GetFileNameWithoutExtension(exePath);
        string platform = Str(f, "platform").Trim();
        string category = Str(f, "category").Trim();
        string customIcon = Str(f, "customIcon").Trim();

        game.Title = title;
        game.ExecutablePath = exePath;
        game.WorkingDirectory = Str(f, "workDir").Trim();
        game.Arguments = Str(f, "args").Trim();
        game.Platform = string.IsNullOrWhiteSpace(platform) ? "PC / Outros" : platform;
        game.Category = string.IsNullOrWhiteSpace(category) ? "Geral" : category;
        game.IsFavorite = f.TryGetProperty("favorite", out var fav) && fav.ValueKind == JsonValueKind.True;
        game.Notes = Str(f, "notes").Trim();

        GameLibraryService.RepairSpecialGameEntry(game);

        // Icon extraction and folder-size scanning can take a while: keep them off the UI thread
        var (iconPath, sizeBytes) = await Task.Run(() =>
        {
            string icon = game.IconPath;
            if (!string.IsNullOrWhiteSpace(customIcon))
            {
                icon = customIcon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || customIcon.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                    ? IconExtractor.ExtractAndCacheIcon(customIcon, game.Id, game.Title, game.WorkingDirectory) ?? string.Empty
                    : customIcon;
            }
            else if (string.IsNullOrWhiteSpace(icon) && File.Exists(game.ExecutablePath))
            {
                icon = IconExtractor.ExtractAndCacheIcon(game.ExecutablePath, game.Id, game.Title, game.WorkingDirectory) ?? string.Empty;
            }

            long size = game.InstallSizeBytes > 0 ? game.InstallSizeBytes : GameLibraryService.CalculateGameInstallSize(game.ExecutablePath, game.WorkingDirectory);
            return (icon, size);
        });

        game.IconPath = iconPath;
        game.InstallSizeBytes = sizeBytes;

        if (isNew)
        {
            _libraryService.AddGamesBatch(new[] { game });
            Notify($"Jogo '{game.Title}' adicionado à biblioteca!");
        }
        else
        {
            _libraryService.SaveGames();
        }

        _trayService.RebuildContextMenu();
        SchedulePush();
        Post(new { type = "saveResult", ok = true });
    }

    #endregion

    #region Scan

    private IEnumerable<DiscoveredGame> OnlyNewGames(IEnumerable<DiscoveredGame> found)
    {
        var existing = new HashSet<string>(_libraryService.Games.Select(x => x.ExecutablePath), StringComparer.OrdinalIgnoreCase);
        return found
            .Where(x => !GameLibraryService.IsBogusLauncherOrTool(x.FilePath, x.SuggestedTitle))
            .GroupBy(x => GameLibraryService.ResolveGameRootDirectory(x.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(x => !existing.Contains(x.FilePath));
    }

    private async Task RunScanAsync(JsonElement msg)
    {
        string mode = Str(msg, "mode");
        string platform = Str(msg, "platform");
        string path = Str(msg, "path");

        Post(new { type = "scanState", loading = true });

        try
        {
            var found = new List<DiscoveredGame>();
            bool noDirs = false;

            if (mode == "all")
            {
                foreach (var p in ScanAllPlatforms)
                {
                    foreach (var dir in GameScannerService.GetDirectoriesForPlatform(p).Where(Directory.Exists))
                    {
                        found.AddRange(await _scanner.ScanDirectoryAsync(dir, p));
                    }
                }
                found.AddRange(GameScannerService.GetEpicManifestGames());
            }
            else if (mode == "platform")
            {
                var dirs = GameScannerService.GetDirectoriesForPlatform(platform);
                noDirs = dirs.Count == 0;
                foreach (var dir in dirs.Where(Directory.Exists))
                {
                    found.AddRange(await _scanner.ScanDirectoryAsync(dir, platform));
                }
            }
            else if (mode == "folder")
            {
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    Post(new { type = "scanResults", items = Array.Empty<object>() });
                    Notify("Pasta inválida: selecione uma pasta existente.");
                    return;
                }
                found.AddRange(await _scanner.ScanDirectoryAsync(path));
            }

            var fresh = OnlyNewGames(found).ToList();
            _lastScan = fresh.ToDictionary(x => x.FilePath, StringComparer.OrdinalIgnoreCase);

            Post(new
            {
                type = "scanResults",
                noDirs,
                platform,
                items = fresh.Select(x => new { path = x.FilePath, title = x.SuggestedTitle, platform = x.Platform, size = x.FileSizeBytes }).ToList()
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "scanResults", items = Array.Empty<object>() });
            Notify($"Erro ao escanear: {ex.Message}");
        }
    }

    private async Task ImportScanSelectionAsync(JsonElement msg)
    {
        if (!msg.TryGetProperty("paths", out var paths)) return;

        var chosen = paths.EnumerateArray()
            .Select(p => p.GetString())
            .Where(p => p != null && _lastScan.ContainsKey(p))
            .Select(p => _lastScan[p!])
            .ToList();

        if (chosen.Count > 0) await ImportDiscoveredGamesAsync(chosen);
    }

    #endregion

    #region Drag & drop

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    // Reached only for drops on the thin native frame; drops on the page arrive as a "drop" message.
    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            await HandleDroppedPathsAsync(files);
        }
    }

    private async Task HandleDroppedPathsAsync(IEnumerable<string> files)
    {
        var discovered = new List<DiscoveredGame>();
        foreach (var file in files)
        {
            if (File.Exists(file) && (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                                      file.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                                      file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                                      file.EndsWith(".url", StringComparison.OrdinalIgnoreCase)))
            {
                discovered.Add(new DiscoveredGame
                {
                    FilePath = file,
                    SuggestedTitle = Path.GetFileNameWithoutExtension(file),
                    Platform = "PC / Outros",
                    IsSelected = true
                });
            }
            else if (Directory.Exists(file))
            {
                discovered.AddRange(await _scanner.ScanDirectoryAsync(file));
            }
        }

        if (discovered.Count > 0)
        {
            await ImportDiscoveredGamesAsync(discovered);
        }
    }

    private async Task ImportDiscoveredGamesAsync(IReadOnlyList<DiscoveredGame> games)
    {
        var toImport = games
            .Where(g => !_libraryService.Games.Any(x => string.Equals(x.ExecutablePath, g.FilePath, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (toImport.Count == 0)
        {
            Notify("Nenhum jogo novo para importar.");
            return;
        }

        SetBusy(true);

        try
        {
            var createdItems = await Task.Run(() =>
                toImport.Select(g => _libraryService.CreateGameItemFromPath(g.FilePath, g.Platform, g.SuggestedTitle)).ToList());

            int addedCount = _libraryService.AddGamesBatch(createdItems);

            _trayService.RebuildContextMenu();
            Notify($"{addedCount} {(addedCount == 1 ? "jogo importado" : "jogos importados")} para sua biblioteca!");
        }
        finally
        {
            SetBusy(false);
            SchedulePush();
        }
    }

    #endregion
}
