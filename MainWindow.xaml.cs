using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using AllInBox.Models;
using AllInBox.Services;

namespace AllInBox;

/// <summary>
/// Shell window: system tray, game launching and the Win32 plumbing for a borderless window.
/// The whole UI, title bar included, is an HTML/CSS/JS page hosted in WebView2 (see MainWindow.Web.cs).
/// </summary>
public partial class MainWindow : Window
{
    private const double FrameThickness = 5;

    private readonly GameLibraryService _libraryService;
    private readonly GameLauncherService _launcherService;
    private readonly TrayIconService _trayService;
    private bool _forceExit;

    public MainWindow()
    {
        InitializeComponent();

        _libraryService = new GameLibraryService();
        _launcherService = new GameLauncherService(OnDataUpdated, () => _libraryService.Games.ToList());
        _trayService = new TrayIconService(
            RestoreFromTray,
            ExitApplicationCompletely,
            () => _libraryService.Games.Where(g => !g.IsHidden),
            LaunchGameFromTrayOrUIAsync
        );

        StateChanged += MainWindow_StateChanged;
        Loaded += MainWindow_Loaded;

        HookLibrary();

        ThemeService.ThemeChanged += () =>
        {
            PushTheme();
            _trayService.RebuildContextMenu();
        };

        LocalizationService.Instance.LanguageChanged += UpdateLocalizedUI;
        UpdateLocalizedUI();
        UpdateWindowState();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _trayService.Initialize(this);
        await InitializeWebAsync();
        await _libraryService.RefreshIconsAndSizesInBackgroundAsync(() =>
        {
            SchedulePush();
            _trayService.RebuildContextMenu();
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
        Web?.Dispose();
        base.OnClosing(e);
    }

    private void CycleTheme()
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

    private void UpdateLocalizedUI()
    {
        Title = LocalizationService.Instance.Get("AppTitle");

        foreach (var g in _libraryService.Games)
        {
            g.RefreshLocalization();
        }

        PushLanguage();
        SchedulePush();
    }

    private void OnDataUpdated()
    {
        _libraryService.SaveGames();
        _trayService.UpdateRunningStatusTooltip();
        _trayService.RebuildContextMenu();
        SchedulePush();
    }

    #region Window chrome

    /// <summary>Called by the page's own title-bar buttons.</summary>
    private void HandleWindowAction(string action)
    {
        switch (action)
        {
            case "minimize": WindowState = WindowState.Minimized; break;
            case "maximize": WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; break;
            case "tray": HideToTray(showBalloon: true); break;
            case "close": Close(); break;
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e) => UpdateWindowState();

    /// <summary>Resize frame only exists while the window is not maximized; tell the page which icon to show.</summary>
    private void UpdateWindowState()
    {
        bool maximized = WindowState == WindowState.Maximized;
        if (WinChrome != null)
        {
            WinChrome.ResizeBorderThickness = maximized ? new Thickness(0) : new Thickness(FrameThickness);
        }
        if (Web != null)
        {
            Web.Margin = maximized ? new Thickness(0) : new Thickness(FrameThickness);
        }
        Post(new { type = "win", maximized });
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
