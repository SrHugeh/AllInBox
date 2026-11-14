using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AllInBox.Models;
using AllInBox.Services;
using AllInBox.Views;

namespace AllInBox;

public partial class MainWindow : Window
{
    private readonly GameLibraryService _libraryService;
    private readonly GameLauncherService _launcherService;
    private readonly TrayIconService _trayService;
    private ICollectionView? _gamesView;
    private bool _isBatchMode;
    private bool _forceExit;
    private bool _isTableMode;

    private ScanFolderWindow? _activeScanWindow;
    private AddEditGameWindow? _activeAddEditWindow;

    public MainWindow()
    {
        InitializeComponent();

        Tag = false; // IsBatchMode = false

        _libraryService = new GameLibraryService();
        _launcherService = new GameLauncherService(OnDataUpdated, () => _libraryService.Games.ToList());
        _trayService = new TrayIconService(
            RestoreFromTray,
            ExitApplicationCompletely,
            () => _libraryService.Games,
            LaunchGameFromTrayOrUIAsync
        );

        StateChanged += MainWindow_StateChanged;
        Loaded += MainWindow_Loaded;

        InitializeCollectionView();
        UpdateStats();
        UpdateViewToggleVisuals();
        UpdateThemeButtonLabel();

        ThemeService.ThemeChanged += () =>
        {
            UpdateThemeButtonLabel();
            UpdateViewToggleVisuals();
            _trayService.RebuildContextMenu();
        };

        LocalizationService.Instance.LanguageChanged += UpdateLocalizedUI;
        UpdateLocalizedUI();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _trayService.Initialize(this);
        await _libraryService.RefreshIconsAndSizesInBackgroundAsync(() =>
        {
            UpdateStats();
            _gamesView?.Refresh();
            _trayService.RebuildContextMenu();
            if (RadioCharts?.IsChecked == true)
            {
                UpdateChartsAndStatistics();
            }
        });
    }

    private async Task LaunchGameFromTrayOrUIAsync(GameItem game)
    {
        if (game == null) return;

        // Immediately reflect running state in UI and Tray before/during launch
        game.IsRunning = true;
        OnDataUpdated();

        bool started = await _launcherService.LaunchGameAsync(game);
        OnDataUpdated();

        if (started && AppSettings.Instance.MinimizeToTrayOnLaunch && IsVisible)
        {
            HideToTray(showBalloon: false);
        }
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void HideToTray(bool showBalloon = false)
    {
        Hide();
        _trayService.RebuildContextMenu();
        if (showBalloon)
        {
            _trayService.ShowBalloonNotification(
                "All In Box na Bandeja",
                "Clique com o botão direito no ícone da bandeja para iniciar seus jogos recentes rapidamente."
            );
        }
    }

    private void ExitApplicationCompletely()
    {
        _forceExit = true;
        _libraryService.SaveGames();
        _launcherService.Dispose();
        _trayService.Dispose();
        Close();
        Application.Current?.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceExit && AppSettings.Instance.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            HideToTray(showBalloon: true);
            return;
        }

        _libraryService.SaveGames();
        _launcherService.Dispose();
        _trayService.Dispose();
        base.OnClosing(e);
    }

    private void BtnQuickTheme_Click(object sender, RoutedEventArgs e)
    {
        string current = AppSettings.Instance.Theme?.ToLowerInvariant() ?? "auto";
        string next = current switch
        {
            "auto" => "dark",
            "dark" => "light",
            "light" => "auto",
            _ => "auto"
        };

        AppSettings.Instance.Theme = next;
        AppSettings.Instance.Save();
        ThemeService.ApplyCurrentSettings();
    }

    private void UpdateThemeButtonLabel()
    {
        if (TxtQuickThemeLabel == null) return;
        string mode = AppSettings.Instance.Theme?.ToLowerInvariant() ?? "auto";
        TxtQuickThemeLabel.Text = mode switch
        {
            "dark" => "Tema: Escuro",
            "light" => "Tema: Claro (Papel)",
            "custom" => "Tema: Personalizado",
            _ => $"Tema: Auto ({(ThemeService.IsSystemLightTheme() ? "Claro" : "Escuro")})"
        };
    }

    private void UpdateLocalizedUI()
    {
        var loc = LocalizationService.Instance;
        Title = loc.Get("AppTitle");

        if (TxtBtnAddGame != null) TxtBtnAddGame.Text = loc.Get("AdicionarJogo");
        if (TxtBtnScan != null) TxtBtnScan.Text = loc.Get("Escanear");
        if (TxtHeaderLibrary != null) TxtHeaderLibrary.Text = loc.Get("Biblioteca");
        if (TxtNavAll != null) TxtNavAll.Text = loc.Get("TodosJogos");
        if (TxtNavFavorites != null) TxtNavFavorites.Text = loc.Get("Favoritos");
        if (TxtNavRecent != null) TxtNavRecent.Text = loc.Get("Recentes");
        if (TxtHeaderPlatforms != null) TxtHeaderPlatforms.Text = loc.Get("Plataformas");
        if (TxtSearchPlaceholder != null) TxtSearchPlaceholder.Text = loc.Get("BuscarJogos");
        if (TxtSearch != null) TxtSearch.Tag = loc.Get("BuscarJogosTag");
        if (TxtLabelSort != null) TxtLabelSort.Text = loc.Get("Ordenar");
        if (BtnSettings != null) BtnSettings.ToolTip = loc.Get("Configuracoes");
        if (TxtBatchSelectLabel != null) TxtBatchSelectLabel.Text = (ChkBatchAll?.IsChecked == true) ? loc.Get("DesmarcarTudo") : loc.Get("SelecionarTudo");

        if (CmbSort != null && CmbSort.Items.Count >= 9)
        {
            if (CmbSort.Items[0] is ComboBoxItem item0) item0.Content = loc.Get("Sort_NameAsc");
            if (CmbSort.Items[1] is ComboBoxItem item1) item1.Content = loc.Get("Sort_NameDesc");
            if (CmbSort.Items[2] is ComboBoxItem item2) item2.Content = loc.Get("Sort_Recent");
            if (CmbSort.Items[3] is ComboBoxItem item3) item3.Content = loc.Get("Sort_MostPlayed");
            if (CmbSort.Items[4] is ComboBoxItem item4) item4.Content = loc.Get("Sort_SizeDesc");
            if (CmbSort.Items[5] is ComboBoxItem item5) item5.Content = loc.Get("Sort_SizeAsc");
            if (CmbSort.Items[6] is ComboBoxItem item6) item6.Content = loc.Get("Sort_DateAdded");
            if (CmbSort.Items[7] is ComboBoxItem item7) item7.Content = loc.Get("Sort_Favorites");
            if (CmbSort.Items[8] is ComboBoxItem item8) item8.Content = loc.Get("Sort_Platform");
        }

        foreach (var g in _libraryService.Games)
        {
            g.RefreshLocalization();
        }

        UpdateStats();
        UpdateThemeButtonLabel();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WindowProc);
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO();
            GetMonitorInfo(monitor, monitorInfo);
            var rcWorkArea = monitorInfo.rcWork;
            var rcMonitorArea = monitorInfo.rcMonitor;
            mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
            mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
            mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
            mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
            mmi.ptMinTrackSize.X = 860;
            mmi.ptMinTrackSize.Y = 540;
        }
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeButtonVisual();
    }

    private void UpdateMaximizeButtonVisual()
    {
        bool isMaximized = WindowState == WindowState.Maximized;
        if (WinChrome != null)
        {
            WinChrome.ResizeBorderThickness = isMaximized ? new Thickness(0) : new Thickness(5);
        }

        if (BtnMaximize.Template?.FindName("IconMax", BtnMaximize) is System.Windows.Shapes.Path iconMax &&
            BtnMaximize.Template?.FindName("IconRestore", BtnMaximize) is System.Windows.Shapes.Path iconRestore)
        {
            iconMax.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
            iconRestore.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void InitializeCollectionView()
    {
        _gamesView = CollectionViewSource.GetDefaultView(_libraryService.Games);
        _gamesView.Filter = FilterGameItem;
        ApplySorting();

        ItemsGrid.ItemsSource = _gamesView;
        ListGames.ItemsSource = _gamesView;

        _libraryService.Games.CollectionChanged += (s, e) =>
        {
            UpdateStats();
            CheckEmptyState();
            _trayService.RebuildContextMenu();
        };

        CheckEmptyState();
    }

    private bool FilterGameItem(object obj)
    {
        if (obj is not GameItem game) return false;

        // 1. Text Search filter
        string search = TxtSearch?.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(search))
        {
            bool matchTitle = game.Title.Contains(search, StringComparison.OrdinalIgnoreCase);
            bool matchCategory = game.Category.Contains(search, StringComparison.OrdinalIgnoreCase);
            bool matchPlatform = game.Platform.Contains(search, StringComparison.OrdinalIgnoreCase);
            if (!matchTitle && !matchCategory && !matchPlatform)
                return false;
        }

        // 2. Navigation Category / Platform filter
        if (RadioFavorites?.IsChecked == true && !game.IsFavorite) return false;
        if (RadioRecent?.IsChecked == true && !game.LastPlayed.HasValue) return false;
        if (RadioSteam?.IsChecked == true && !string.Equals(game.Platform, "Steam", StringComparison.OrdinalIgnoreCase)) return false;
        if (RadioEpic?.IsChecked == true && !string.Equals(game.Platform, "Epic Games", StringComparison.OrdinalIgnoreCase)) return false;
        if (RadioRiot?.IsChecked == true && !string.Equals(game.Platform, "Riot Games", StringComparison.OrdinalIgnoreCase)) return false;
        if (RadioMinecraft?.IsChecked == true && !string.Equals(game.Platform, "Minecraft", StringComparison.OrdinalIgnoreCase)) return false;
        if (RadioUbisoft?.IsChecked == true && !string.Equals(game.Platform, "Ubisoft", StringComparison.OrdinalIgnoreCase)) return false;
        if (RadioGOG?.IsChecked == true && !string.Equals(game.Platform, "GOG", StringComparison.OrdinalIgnoreCase) && !string.Equals(game.Platform, "GOG Games", StringComparison.OrdinalIgnoreCase)) return false;
        if (RadioEmulators?.IsChecked == true && !string.Equals(game.Platform, "Emulador", StringComparison.OrdinalIgnoreCase) && !string.Equals(game.Platform, "Emuladores", StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    private void ApplySorting()
    {
        if (_gamesView == null) return;

        _gamesView.SortDescriptions.Clear();

        int selectedIndex = CmbSort?.SelectedIndex ?? 0;
        switch (selectedIndex)
        {
            case 0: // Nome A-Z
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.Title), ListSortDirection.Ascending));
                break;
            case 1: // Nome Z-A
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.Title), ListSortDirection.Descending));
                break;
            case 2: // Recentemente Jogados
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.LastPlayed), ListSortDirection.Descending));
                break;
            case 3: // Mais Jogados
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.PlayTimeSeconds), ListSortDirection.Descending));
                break;
            case 4: // Tamanho (Maior - Menor)
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.InstallSizeBytes), ListSortDirection.Descending));
                break;
            case 5: // Tamanho (Menor - Maior)
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.InstallSizeBytes), ListSortDirection.Ascending));
                break;
            case 6: // Data de Adição
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.DateAdded), ListSortDirection.Descending));
                break;
            case 7: // Favoritos Primeiro
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.IsFavorite), ListSortDirection.Descending));
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.Title), ListSortDirection.Ascending));
                break;
            case 8: // Plataforma (A - Z)
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.Platform), ListSortDirection.Ascending));
                _gamesView.SortDescriptions.Add(new SortDescription(nameof(GameItem.Title), ListSortDirection.Ascending));
                break;
        }

        _gamesView.Refresh();
    }

    private void UpdateStats()
    {
        var games = _libraryService.Games;
        int totalGames = games.Count;
        long totalSeconds = games.Sum(x => x.PlayTimeSeconds);
        long totalBytes = games.Sum(x => x.InstallSizeBytes);

        if (TxtTotalGames != null) TxtTotalGames.Text = totalGames.ToString();
        if (TxtTotalPlaytime != null) TxtTotalPlaytime.Text = totalSeconds > 0 ? GameItem.FormatPlayTime(totalSeconds) : "0s";
        if (TxtTotalSize != null) TxtTotalSize.Text = totalBytes > 0 ? GameItem.FormatSize(totalBytes) : "0 MB";

        // Update Filter Count Badges
        if (TxtCountAll != null) TxtCountAll.Text = totalGames.ToString();
        if (TxtCountFavorites != null) TxtCountFavorites.Text = games.Count(x => x.IsFavorite).ToString();
        if (TxtCountRecent != null) TxtCountRecent.Text = games.Count(x => x.LastPlayed.HasValue).ToString();

        if (TxtCountSteam != null) TxtCountSteam.Text = games.Count(x => string.Equals(x.Platform, "Steam", StringComparison.OrdinalIgnoreCase)).ToString();
        if (TxtCountEpic != null) TxtCountEpic.Text = games.Count(x => string.Equals(x.Platform, "Epic Games", StringComparison.OrdinalIgnoreCase)).ToString();
        if (TxtCountRiot != null) TxtCountRiot.Text = games.Count(x => string.Equals(x.Platform, "Riot Games", StringComparison.OrdinalIgnoreCase)).ToString();
        if (TxtCountMinecraft != null) TxtCountMinecraft.Text = games.Count(x => string.Equals(x.Platform, "Minecraft", StringComparison.OrdinalIgnoreCase)).ToString();
        if (TxtCountUbisoft != null) TxtCountUbisoft.Text = games.Count(x => string.Equals(x.Platform, "Ubisoft", StringComparison.OrdinalIgnoreCase)).ToString();
        if (TxtCountGOG != null) TxtCountGOG.Text = games.Count(x => string.Equals(x.Platform, "GOG", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Platform, "GOG Games", StringComparison.OrdinalIgnoreCase)).ToString();
        if (TxtCountEmulators != null) TxtCountEmulators.Text = games.Count(x => string.Equals(x.Platform, "Emulador", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Platform, "Emuladores", StringComparison.OrdinalIgnoreCase)).ToString();

        // Populate Right Side-Dock: Quick Recent Games (top 5)
        if (ItemsQuickRecent != null)
        {
            var recent = games
                .OrderByDescending(g => g.IsRunning)
                .ThenByDescending(g => g.LastPlayed.HasValue)
                .ThenByDescending(g => g.LastPlayed ?? DateTime.MinValue)
                .ThenByDescending(g => g.PlayTimeSeconds)
                .ThenBy(g => g.Title)
                .Take(5)
                .ToList();
            ItemsQuickRecent.ItemsSource = recent;
        }

        // Populate Right Side-Dock: Heaviest Games on PC (top 5)
        if (ItemsQuickHeavy != null)
        {
            var heaviest = games
                .Where(g => g.InstallSizeBytes > 0)
                .OrderByDescending(g => g.InstallSizeBytes)
                .Take(5)
                .ToList();

            if (heaviest.Count > 0)
            {
                long maxBytes = heaviest[0].InstallSizeBytes;
                var list = new List<RankedGameStatItem>();
                for (int i = 0; i < heaviest.Count; i++)
                {
                    var g = heaviest[i];
                    list.Add(new RankedGameStatItem
                    {
                        Rank = i + 1,
                        Game = g,
                        ValueFormatted = g.SizeText,
                        ProgressPercentage = maxBytes > 0 ? (g.InstallSizeBytes / (double)maxBytes) * 100 : 0
                    });
                }
                ItemsQuickHeavy.ItemsSource = list;
            }
            else
            {
                ItemsQuickHeavy.ItemsSource = null;
            }
        }

        CheckEmptyState();
    }

    private void CheckEmptyState()
    {
        if (PanelEmptyState == null || ScrollGrid == null || GridTableView == null) return;

        bool isEmpty = _gamesView == null || _gamesView.IsEmpty;
        PanelEmptyState.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;

        if (isEmpty)
        {
            ScrollGrid.Visibility = Visibility.Collapsed;
            GridTableView.Visibility = Visibility.Collapsed;
        }
        else
        {
            ScrollGrid.Visibility = _isTableMode ? Visibility.Collapsed : Visibility.Visible;
            GridTableView.Visibility = _isTableMode ? Visibility.Visible : Visibility.Collapsed;
        }

        if (isEmpty)
        {
            string search = TxtSearch?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(search))
            {
                PanelEmptyActions.Visibility = Visibility.Collapsed;
                SetEmptyDisplay(null, $"Nenhum jogo encontrado para '{search}'", "Tente buscar por outro termo ou limpe o campo de busca.");
            }
            else if (RadioFavorites?.IsChecked == true)
            {
                PanelEmptyActions.Visibility = Visibility.Collapsed;
                SetEmptyDisplay(null, "Nenhum Jogo Favorito", "Clique na estrela de qualquer jogo para adicioná-lo aos favoritos.");
            }
            else if (RadioRecent?.IsChecked == true)
            {
                PanelEmptyActions.Visibility = Visibility.Collapsed;
                SetEmptyDisplay(null, "Nenhum Jogo Jogado Recentemente", "Os jogos que você iniciar aparecerão aqui com seu histórico.");
            }
            else if (RadioSteam?.IsChecked == true)
            {
                PanelEmptyActions.Visibility = Visibility.Visible;
                SetEmptyDisplay("steam_icon.png", "Nenhum Jogo da Steam", "Clique em 'Escanear' para detectar seus jogos instalados na Steam.");
            }
            else if (RadioEpic?.IsChecked == true)
            {
                PanelEmptyActions.Visibility = Visibility.Visible;
                SetEmptyDisplay("epic_icon.png", "Nenhum Jogo da Epic Games", "Clique em 'Escanear' para detectar seus jogos instalados na Epic Games.");
            }
            else if (RadioRiot?.IsChecked == true)
            {
                PanelEmptyActions.Visibility = Visibility.Visible;
                SetEmptyDisplay("riot_icon.png", "Nenhum Jogo da Riot Games", "Adicione ou escaneie League of Legends, VALORANT e outros jogos da Riot.");
            }
            else if (RadioMinecraft?.IsChecked == true)
            {
                PanelEmptyActions.Visibility = Visibility.Visible;
                SetEmptyDisplay("minecraft_icon.png", "Nenhum Jogo do Minecraft", "Detecte instalações do Minecraft Oficial, Modrinth, CurseForge ou .minecraft.");
            }
            else if (RadioUbisoft?.IsChecked == true)
            {
                PanelEmptyActions.Visibility = Visibility.Visible;
                SetEmptyDisplay("ubisoft_icon.png", "Nenhum Jogo da Ubisoft", "Escaneie sua biblioteca do Ubisoft Connect ou aponte os executáveis.");
            }
            else if (RadioGOG?.IsChecked == true)
            {
                PanelEmptyActions.Visibility = Visibility.Visible;
                SetEmptyDisplay("gog_icon.png", "Nenhum Jogo do GOG", "Escaneie a pasta do GOG Galaxy / Games ou arraste seus executáveis.");
            }
            else if (RadioEmulators?.IsChecked == true)
            {
                PanelEmptyActions.Visibility = Visibility.Visible;
                SetEmptyDisplay(null, "Nenhum Emulador", "Adicione executáveis de emuladores (RPCS3, PCSX2, Dolphin, RetroArch, etc.).");
            }
            else
            {
                PanelEmptyActions.Visibility = Visibility.Visible;
                SetEmptyDisplay("icon_all_in_box.png", "Sua Biblioteca está Vazia", "Arraste executáveis (.exe) ou atalhos (.lnk) para cá ou use os botões abaixo.");
            }
        }
    }

    private void SetEmptyDisplay(string? assetName, string title, string subtitle)
    {
        TxtEmptyTitle.Text = title;
        TxtEmptySubtitle.Text = subtitle;

        if (!string.IsNullOrEmpty(assetName))
        {
            var img = LoadEmptyStateAsset(assetName);
            if (img != null)
            {
                ImgEmptyIcon.Source = img;
                ImgEmptyIcon.Visibility = Visibility.Visible;
                PathEmptyIcon.Visibility = Visibility.Collapsed;
                return;
            }
        }

        ImgEmptyIcon.Visibility = Visibility.Collapsed;
        PathEmptyIcon.Visibility = Visibility.Visible;
    }

    private static BitmapImage? LoadEmptyStateAsset(string fileName)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/{fileName}", UriKind.Absolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 96;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private void OnDataUpdated()
    {
        _libraryService.SaveGames();
        UpdateStats();
        _trayService.UpdateRunningStatusTooltip();
        _trayService.RebuildContextMenu();

        var runningGames = _libraryService.Games.Where(x => x.IsRunning).ToList();
        if (runningGames.Count > 0)
        {
            string names = string.Join(", ", runningGames.Select(g => g.Title));
            DotRunning.Visibility = Visibility.Visible;
            TxtStatus.Text = $"Em execução: {names}";
            TxtStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessBrush");

            if (BorderTopRunningBanner != null && TxtTopRunningBanner != null && TxtTitlebarStats != null)
            {
                TxtTitlebarStats.Visibility = Visibility.Collapsed;
                BorderTopRunningBanner.Visibility = Visibility.Visible;
                TxtTopRunningBanner.Text = $"EM JOGO: {names}";
            }
        }
        else
        {
            DotRunning.Visibility = Visibility.Collapsed;
            TxtStatus.Text = "Pronto";
            TxtStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            if (BorderTopRunningBanner != null && TxtTitlebarStats != null)
            {
                BorderTopRunningBanner.Visibility = Visibility.Collapsed;
                TxtTitlebarStats.Visibility = Visibility.Visible;
            }
        }
    }

    #region Window Chrome

    private void Titlebar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                if (WindowState == WindowState.Maximized)
                {
                    Point mousePos = PointToScreen(e.GetPosition(this));
                    double percentX = e.GetPosition(this).X / ActualWidth;
                    WindowState = WindowState.Normal;
                    double restoreW = RestoreBounds.Width > 0 ? RestoreBounds.Width : 1180;
                    Left = mousePos.X - (restoreW * percentX);
                    Top = Math.Max(0, mousePos.Y - 14);
                }
                try
                {
                    DragMove();
                }
                catch { }
            }
        }
    }

    private void BtnHideToTray_Click(object sender, RoutedEventArgs e)
    {
        HideToTray(showBalloon: true);
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeButtonVisual();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Filter, Search, Quick Sort & Column Header Sort

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _gamesView?.Refresh();
        CheckEmptyState();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (PanelLibraryView == null || PanelChartsView == null) return;

        if (RadioCharts?.IsChecked == true)
        {
            PanelLibraryView.Visibility = Visibility.Collapsed;
            PanelChartsView.Visibility = Visibility.Visible;
            UpdateChartsAndStatistics();
        }
        else
        {
            PanelLibraryView.Visibility = Visibility.Visible;
            PanelChartsView.Visibility = Visibility.Collapsed;
            _gamesView?.Refresh();
            CheckEmptyState();
        }
    }

    private void BtnQuickSortSize_Click(object sender, RoutedEventArgs e)
    {
        if (CmbSort != null) CmbSort.SelectedIndex = 4; // Tamanho (Maior - Menor)
    }

    private void BtnQuickSortRecent_Click(object sender, RoutedEventArgs e)
    {
        if (CmbSort != null) CmbSort.SelectedIndex = 2; // Jogados Recentemente
    }

    private void BtnQuickSortName_Click(object sender, RoutedEventArgs e)
    {
        if (CmbSort != null) CmbSort.SelectedIndex = 0; // Nome A-Z
    }

    private void HeaderTitle_Click(object sender, MouseButtonEventArgs e)
    {
        if (CmbSort == null) return;
        CmbSort.SelectedIndex = CmbSort.SelectedIndex == 0 ? 1 : 0;
    }

    private void HeaderPlatform_Click(object sender, MouseButtonEventArgs e)
    {
        if (CmbSort != null) CmbSort.SelectedIndex = 8;
    }

    private void HeaderSize_Click(object sender, MouseButtonEventArgs e)
    {
        if (CmbSort == null) return;
        CmbSort.SelectedIndex = CmbSort.SelectedIndex == 4 ? 5 : 4;
    }

    private void HeaderTime_Click(object sender, MouseButtonEventArgs e)
    {
        if (CmbSort != null) CmbSort.SelectedIndex = 3;
    }

    private void HeaderRecent_Click(object sender, MouseButtonEventArgs e)
    {
        if (CmbSort != null) CmbSort.SelectedIndex = 2;
    }

    private void UpdateChartsAndStatistics()
    {
        var loc = LocalizationService.Instance;
        var games = _libraryService.Games.ToList();
        int totalGames = games.Count;

        // 1. KPI Cards
        long totalPlaytimeSeconds = games.Sum(x => x.PlayTimeSeconds);
        long totalDiskBytes = games.Sum(x => x.InstallSizeBytes);

        TxtStatTotalPlaytime.Text = totalPlaytimeSeconds > 0 ? GameItem.FormatPlayTime(totalPlaytimeSeconds) : "0s";
        TxtStatTotalDiskSize.Text = totalDiskBytes > 0 ? GameItem.FormatSize(totalDiskBytes) : "0 MB";
        TxtStatTotalCount.Text = $"{totalGames}";

        var playedGames = games.Where(x => x.PlayTimeSeconds > 0).ToList();
        var neverPlayedGames = games.Where(x => x.PlayTimeSeconds <= 0).ToList();

        double playedPct = totalGames > 0 ? (playedGames.Count / (double)totalGames) * 100 : 0;
        double neverPct = totalGames > 0 ? (neverPlayedGames.Count / (double)totalGames) * 100 : 0;

        TxtStatPlayedCount.Text = $"{playedGames.Count} ({playedPct:0}%)";
        TxtStatNeverPlayedCount.Text = $"{neverPlayedGames.Count} ({neverPct:0}%)";

        // 2. Top Heaviest Games (by InstallSizeBytes)
        var heaviest = games.Where(x => x.InstallSizeBytes > 0).OrderByDescending(x => x.InstallSizeBytes).Take(7).ToList();
        if (heaviest.Count > 0)
        {
            long maxBytes = heaviest[0].InstallSizeBytes;
            var heavyStats = new List<RankedGameStatItem>();
            for (int i = 0; i < heaviest.Count; i++)
            {
                var g = heaviest[i];
                heavyStats.Add(new RankedGameStatItem
                {
                    Rank = i + 1,
                    Game = g,
                    ValueFormatted = g.SizeText,
                    ProgressPercentage = maxBytes > 0 ? (g.InstallSizeBytes / (double)maxBytes) * 100 : 0
                });
            }
            ItemsHeaviestGames.ItemsSource = heavyStats;
        }
        else
        {
            ItemsHeaviestGames.ItemsSource = null;
        }

        // 3. Most Played Games (Top 5 by Playtime)
        var topPlayed = playedGames.OrderByDescending(x => x.PlayTimeSeconds).Take(5).ToList();
        if (topPlayed.Count > 0)
        {
            TxtEmptyMostPlayed.Visibility = Visibility.Collapsed;
            ItemsMostPlayed.Visibility = Visibility.Visible;

            long maxTime = topPlayed[0].PlayTimeSeconds;
            var mostPlayedStats = new List<RankedGameStatItem>();
            for (int i = 0; i < topPlayed.Count; i++)
            {
                var g = topPlayed[i];
                mostPlayedStats.Add(new RankedGameStatItem
                {
                    Rank = i + 1,
                    Game = g,
                    PlayTimeFormatted = GameItem.FormatPlayTime(g.PlayTimeSeconds),
                    ProgressPercentage = maxTime > 0 ? (g.PlayTimeSeconds / (double)maxTime) * 100 : 0
                });
            }

            ItemsMostPlayed.ItemsSource = mostPlayedStats;
        }
        else
        {
            TxtEmptyMostPlayed.Visibility = Visibility.Visible;
            ItemsMostPlayed.Visibility = Visibility.Collapsed;
        }

        // 4. Never Played (Backlog)
        if (neverPlayedGames.Count > 0)
        {
            TxtEmptyNeverPlayed.Visibility = Visibility.Collapsed;
            ItemsNeverPlayed.Visibility = Visibility.Visible;
            ItemsNeverPlayed.ItemsSource = neverPlayedGames.OrderByDescending(x => x.InstallSizeBytes).ToList();
        }
        else
        {
            TxtEmptyNeverPlayed.Visibility = Visibility.Visible;
            ItemsNeverPlayed.Visibility = Visibility.Collapsed;
        }

        // 5. Platform Distribution
        var platformGroups = games
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Platform) ? "PC / Outros" : x.Platform, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ToList();

        var platformStats = new List<PlatformStatItem>();
        foreach (var grp in platformGroups)
        {
            string pName = grp.Key;
            double pct = totalGames > 0 ? (grp.Count() / (double)totalGames) * 100 : 0;
            platformStats.Add(new PlatformStatItem
            {
                Platform = pName,
                Count = grp.Count(),
                Percentage = pct,
                BarColor = "#4A8FE7"
            });
        }
        ItemsPlatformStats.ItemsSource = platformStats;

        // 6. Summary / Insights
        int favCount = games.Count(x => x.IsFavorite);
        TxtStatFavoritesCount.Text = loc.Get("JogosCount", favCount);

        var topPlatform = platformStats.FirstOrDefault();
        TxtStatTopPlatform.Text = topPlatform != null ? $"{topPlatform.Platform} ({topPlatform.Percentage:0.#}%)" : loc.Get("Nenhuma");

        if (playedGames.Count > 0)
        {
            long avgSeconds = (long)(totalPlaytimeSeconds / (double)playedGames.Count);
            TxtStatAveragePlaytime.Text = GameItem.FormatPlayTime(avgSeconds);
        }
        else
        {
            TxtStatAveragePlaytime.Text = "0s";
        }
    }

    private void CmbSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySorting();
    }

    private void UpdateViewToggleVisuals()
    {
        if (BtnViewGrid == null || BtnViewList == null) return;

        if (!_isTableMode)
        {
            BtnViewGrid.SetResourceReference(Control.BackgroundProperty, "CardSelectedBgBrush");
            BtnViewGrid.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
            BtnViewGrid.FontWeight = FontWeights.SemiBold;

            BtnViewList.Background = Brushes.Transparent;
            BtnViewList.SetResourceReference(Control.ForegroundProperty, "TextSecondaryBrush");
            BtnViewList.FontWeight = FontWeights.Normal;
        }
        else
        {
            BtnViewList.SetResourceReference(Control.BackgroundProperty, "CardSelectedBgBrush");
            BtnViewList.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
            BtnViewList.FontWeight = FontWeights.SemiBold;

            BtnViewGrid.Background = Brushes.Transparent;
            BtnViewGrid.SetResourceReference(Control.ForegroundProperty, "TextSecondaryBrush");
            BtnViewGrid.FontWeight = FontWeights.Normal;
        }
    }

    private void BtnViewGrid_Click(object sender, RoutedEventArgs e)
    {
        _isTableMode = false;
        UpdateViewToggleVisuals();
        CheckEmptyState();
    }

    private void BtnViewList_Click(object sender, RoutedEventArgs e)
    {
        _isTableMode = true;
        UpdateViewToggleVisuals();
        CheckEmptyState();
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double delta = e.Delta > 0 ? -96 : 96;
        if (sender is ScrollViewer scv)
        {
            scv.ScrollToVerticalOffset(scv.VerticalOffset + delta);
            e.Handled = true;
        }
        else if (sender is ListView lv)
        {
            var border = VisualTreeHelper.GetChild(lv, 0) as Decorator;
            if (border?.Child is ScrollViewer listScv)
            {
                listScv.ScrollToVerticalOffset(listScv.VerticalOffset + delta);
                e.Handled = true;
            }
        }
    }

    #endregion

    #region Batch Management & Deduplication

    private async void BtnDeduplicate_Click(object sender, RoutedEventArgs e)
    {
        BtnDeduplicate.IsEnabled = false;
        TxtStatus.Text = "Verificando duplicados, ícones e tamanhos de instalação...";

        int removed = _libraryService.DeduplicateExistingLibrary();
        await _libraryService.RefreshIconsAndSizesInBackgroundAsync(() =>
        {
            UpdateStats();
            _gamesView?.Refresh();
            _trayService.RebuildContextMenu();
            if (RadioCharts?.IsChecked == true)
            {
                UpdateChartsAndStatistics();
            }
        });

        BtnDeduplicate.IsEnabled = true;
        TxtStatus.Text = removed > 0
            ? $"Otimização concluída: {removed} jogo(s) duplicado(s) removido(s) e ícones/tamanhos atualizados."
            : "Biblioteca otimizada: ícones e tamanhos atualizados, sem duplicados.";
    }

    private void BatchCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        int selectedCount = _libraryService.Games.Count(x => x.IsSelected);
        int totalVisible = _gamesView?.Cast<object>().Count() ?? _libraryService.Games.Count;
        _isBatchMode = selectedCount > 0;
        Tag = _isBatchMode;

        PanelBatchBar.Visibility = _isBatchMode ? Visibility.Visible : Visibility.Collapsed;
        TxtBatchCount.Text = selectedCount.ToString();
        BtnDeleteSelectedBatch.IsEnabled = selectedCount > 0;

        bool allSelected = totalVisible > 0 && selectedCount >= totalVisible;
        ChkBatchAll.IsChecked = allSelected;
        TxtBatchSelectLabel.Text = allSelected ? LocalizationService.Instance.Get("DesmarcarTudo") : LocalizationService.Instance.Get("SelecionarTudo");
    }

    private void ChkBatchAll_Click(object sender, RoutedEventArgs e)
    {
        ToggleSelectAll();
    }

    private void TxtBatchSelectLabel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        ToggleSelectAll();
    }

    private void ToggleSelectAll()
    {
        int selectedCount = _libraryService.Games.Count(x => x.IsSelected);
        int totalVisible = _gamesView?.Cast<object>().Count() ?? _libraryService.Games.Count;
        bool allSelected = totalVisible > 0 && selectedCount >= totalVisible;

        if (allSelected)
        {
            foreach (var g in _libraryService.Games) g.IsSelected = false;
        }
        else
        {
            if (_gamesView != null)
            {
                foreach (var item in _gamesView)
                {
                    if (item is GameItem g) g.IsSelected = true;
                }
            }
            else
            {
                foreach (var g in _libraryService.Games) g.IsSelected = true;
            }
        }

        UpdateSelectionState();
    }

    private void BtnDeleteSelectedBatch_Click(object sender, RoutedEventArgs e)
    {
        var selected = _libraryService.Games.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) return;

        bool confirmed = AppDialogWindow.Confirm(
            this,
            $"Tem certeza que deseja remover {selected.Count} jogo(s) da sua biblioteca?\n\n(Os arquivos dos jogos NÃO serão apagados do seu computador)",
            "Remover Jogos",
            confirmText: "Remover",
            cancelText: "Cancelar",
            isDanger: true
        );

        if (confirmed)
        {
            foreach (var g in selected)
            {
                _libraryService.RemoveGame(g);
            }

            _gamesView?.Refresh();
            UpdateStats();
            UpdateSelectionState();
            _trayService.RebuildContextMenu();
            TxtStatus.Text = $"{selected.Count} jogo(s) removido(s) da biblioteca.";
        }
    }

    #endregion

    #region Drag & Drop

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            PanelDragOverlay.Visibility = Visibility.Visible;
        }
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        PanelDragOverlay.Visibility = Visibility.Collapsed;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        PanelDragOverlay.Visibility = Visibility.Collapsed;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
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
                        var scanner = new GameScannerService();
                        var scanned = await scanner.ScanDirectoryAsync(file);
                        discovered.AddRange(scanned);
                    }
                }

                if (discovered.Count > 0)
                {
                    await ImportDiscoveredGamesAsync(discovered);
                }
            }
        }
    }

    #endregion

    #region Game Actions & Dialogs

    private void BtnAddGame_Click(object sender, RoutedEventArgs e)
    {
        if (_activeAddEditWindow != null && _activeAddEditWindow.IsLoaded)
        {
            if (_activeAddEditWindow.WindowState == WindowState.Minimized)
                _activeAddEditWindow.WindowState = WindowState.Normal;
            _activeAddEditWindow.Activate();
            _activeAddEditWindow.Focus();
            return;
        }

        var dialog = new AddEditGameWindow { Owner = this };
        _activeAddEditWindow = dialog;

        bool? result = dialog.ShowDialog();
        _activeAddEditWindow = null;

        if (result == true)
        {
            _libraryService.AddGamesBatch(new[] { dialog.Game });
            _gamesView?.Refresh();
            UpdateStats();
            _trayService.RebuildContextMenu();
            TxtStatus.Text = $"Jogo '{dialog.Game.Title}' adicionado à biblioteca!";
        }
    }

    private async void BtnScanFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_activeScanWindow != null && _activeScanWindow.IsLoaded)
        {
            if (_activeScanWindow.WindowState == WindowState.Minimized)
                _activeScanWindow.WindowState = WindowState.Normal;
            _activeScanWindow.Activate();
            _activeScanWindow.Focus();
            return;
        }

        var existingPaths = _libraryService.Games.Select(x => x.ExecutablePath);
        var dialog = new ScanFolderWindow(existingPaths) { Owner = this };
        _activeScanWindow = dialog;

        bool? result = dialog.ShowDialog();
        _activeScanWindow = null;

        if (result == true && dialog.SelectedGames.Count > 0)
        {
            await ImportDiscoveredGamesAsync(dialog.SelectedGames);
        }
    }

    private async Task ImportDiscoveredGamesAsync(IReadOnlyList<DiscoveredGame> games)
    {
        var toImport = games
            .Where(g => !_libraryService.Games.Any(x => string.Equals(x.ExecutablePath, g.FilePath, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (toImport.Count == 0)
        {
            TxtStatus.Text = "Nenhum jogo novo para importar.";
            return;
        }

        PanelLoadingOverlay.Visibility = Visibility.Visible;
        TxtLoadingTitle.Text = $"Importando {toImport.Count} {(toImport.Count == 1 ? "jogo" : "jogos")}...";
        TxtLoadingSubtitle.Text = "Extraindo ícones, calculando tamanho e desduplicando...";

        var createdItems = await Task.Run(() =>
        {
            var list = new List<GameItem>();
            foreach (var g in toImport)
            {
                var item = _libraryService.CreateGameItemFromPath(g.FilePath, g.Platform, g.SuggestedTitle);
                list.Add(item);
            }
            return list;
        });

        int addedCount = _libraryService.AddGamesBatch(createdItems);

        PanelLoadingOverlay.Visibility = Visibility.Collapsed;
        _gamesView?.Refresh();
        UpdateStats();
        CheckEmptyState();
        _trayService.RebuildContextMenu();
        TxtStatus.Text = $"{addedCount} {(addedCount == 1 ? "jogo importado" : "jogos importados")} para sua biblioteca!";
    }

    private async void BtnCardPlay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GameItem game)
        {
            await LaunchGameFromTrayOrUIAsync(game);
        }
    }

    private async void GameCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GameItem game)
        {
            if (_isBatchMode)
            {
                game.IsSelected = !game.IsSelected;
                UpdateSelectionState();
                return;
            }

            if (e.ClickCount == 2)
            {
                await LaunchGameFromTrayOrUIAsync(game);
            }
        }
    }

    private void ToggleFavorite(GameItem game)
    {
        game.IsFavorite = !game.IsFavorite;
        UpdateStats();
        _ = Task.Run(() => _libraryService.SaveGames());

        if (RadioFavorites?.IsChecked == true || CmbSort?.SelectedIndex == 7)
        {
            _gamesView?.Refresh();
        }
    }

    private void BtnCardFav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GameItem game)
        {
            ToggleFavorite(game);
        }
    }

    private async void MenuPlay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is GameItem game)
        {
            await LaunchGameFromTrayOrUIAsync(game);
        }
    }

    private void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is GameItem game)
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
    }

    private void MenuToggleFav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is GameItem game)
        {
            ToggleFavorite(game);
        }
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWin = new SettingsWindow { Owner = this };
        settingsWin.ShowDialog();
    }

    private void MenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is GameItem game)
        {
            if (_activeAddEditWindow != null && _activeAddEditWindow.IsLoaded)
            {
                if (_activeAddEditWindow.WindowState == WindowState.Minimized)
                    _activeAddEditWindow.WindowState = WindowState.Normal;
                _activeAddEditWindow.Activate();
                _activeAddEditWindow.Focus();
                return;
            }

            var dialog = new AddEditGameWindow(game) { Owner = this };
            _activeAddEditWindow = dialog;

            bool? result = dialog.ShowDialog();
            _activeAddEditWindow = null;

            if (result == true)
            {
                _libraryService.SaveGames();
                _gamesView?.Refresh();
                UpdateStats();
                _trayService.RebuildContextMenu();
            }
        }
    }

    private void MenuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is GameItem game)
        {
            var loc = LocalizationService.Instance;
            bool confirmed = AppDialogWindow.Confirm(
                this,
                loc.Get("ConfirmarRemocao", game.Title),
                loc.Get("RemoverDaCaixa"),
                confirmText: loc.Get("Remover"),
                cancelText: loc.Get("Cancelar"),
                isDanger: true
            );

            if (confirmed)
            {
                _libraryService.RemoveGame(game);
                _gamesView?.Refresh();
                UpdateStats();
                _trayService.RebuildContextMenu();
                TxtStatus.Text = $"Jogo '{game.Title}' removido da biblioteca.";
            }
        }
    }

    #endregion

    #region Win32 Monitor Sizing Helpers (Zero-Clipping Maximize)

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MONITORINFO
    {
        public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        public RECT rcMonitor = new RECT();
        public RECT rcWork = new RECT();
        public int dwFlags = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

    #endregion
}
