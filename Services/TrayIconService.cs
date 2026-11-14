using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AllInBox.Models;

namespace AllInBox.Services;

public sealed class TrayIconService : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 101;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const uint LR_DEFAULTSIZE = 0x00000040;

    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private IntPtr _hIcon;
    private bool _isAdded;
    private bool _disposed;

    private readonly Action _onRestoreRequested;
    private readonly Action _onExitRequested;
    private Func<IEnumerable<GameItem>>? _getGames;
    private Func<GameItem, Task>? _onLaunchGame;

    private ContextMenu? _trayMenu;
    private string _openText = "Abrir All In Box";
    private string _exitText = "Sair Completamente";

    public TrayIconService(
        Action onRestoreRequested,
        Action onExitRequested,
        Func<IEnumerable<GameItem>>? getGames = null,
        Func<GameItem, Task>? onLaunchGame = null)
    {
        _onRestoreRequested = onRestoreRequested;
        _onExitRequested = onExitRequested;
        _getGames = getGames;
        _onLaunchGame = onLaunchGame;
    }

    public void SetGameCallbacks(Func<IEnumerable<GameItem>> getGames, Func<GameItem, Task> onLaunchGame)
    {
        _getGames = getGames;
        _onLaunchGame = onLaunchGame;
    }

    public void Initialize(Window window)
    {
        if (_disposed) return;

        try
        {
            _hwnd = new WindowInteropHelper(window).EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(WndProc);

            string iconFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "favicon.ico");
            if (File.Exists(iconFile))
            {
                _hIcon = LoadImage(IntPtr.Zero, iconFile, IMAGE_ICON, 16, 16, LR_LOADFROMFILE | LR_DEFAULTSIZE);
            }

            if (_hIcon == IntPtr.Zero)
            {
                string exePath = Environment.ProcessPath ?? string.Empty;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    IntPtr[] large = new IntPtr[1];
                    IntPtr[] small = new IntPtr[1];
                    ExtractIconEx(exePath, 0, large, small, 1);
                    _hIcon = small[0] != IntPtr.Zero ? small[0] : large[0];
                }
            }

            _trayMenu = new ContextMenu
            {
                Placement = PlacementMode.MousePoint,
                MinWidth = 250
            };

            RebuildContextMenu();
            AddOrUpdateTrayIcon("All In Box - Central de Jogos");
        }
        catch
        {
            // Safeguard against Windows shell anomalies
        }
    }

    public void RebuildContextMenu()
    {
        if (_trayMenu == null) return;

        _trayMenu.Items.Clear();

        var allGames = _getGames?.Invoke()?.ToList() ?? new List<GameItem>();
        var runningGames = allGames.Where(g => g.IsRunning).ToList();

        // Top Header / Active Game Indicator
        if (runningGames.Count > 0)
        {
            string runningNames = string.Join(", ", runningGames.Select(g => g.Title));
            var statusHeader = new MenuItem
            {
                IsEnabled = true,
                Header = CreateStatusHeaderElement($"Jogando agora: {runningNames}", isRunning: true)
            };
            statusHeader.Click += (s, e) => _onRestoreRequested();
            _trayMenu.Items.Add(statusHeader);
        }
        else
        {
            var titleHeader = new MenuItem
            {
                IsEnabled = false,
                Header = CreateStatusHeaderElement("JOGOS RECENTES (CLIQUE PARA JOGAR)", isRunning: false)
            };
            _trayMenu.Items.Add(titleHeader);
        }

        _trayMenu.Items.Add(new Separator
        {
            Background = GetResourceBrush("DividerBrush", "#242D3C"),
            Margin = new Thickness(4, 3, 4, 3)
        });

        // Up to 6 most recently played games (or top games if none played yet)
        var recentGames = allGames
            .OrderByDescending(g => g.IsRunning)
            .ThenByDescending(g => g.LastPlayed.HasValue)
            .ThenByDescending(g => g.LastPlayed ?? DateTime.MinValue)
            .ThenByDescending(g => g.PlayTimeSeconds)
            .ThenBy(g => g.Title)
            .Take(6)
            .ToList();

        if (recentGames.Count == 0)
        {
            var emptyItem = new MenuItem
            {
                IsEnabled = false,
                Header = new TextBlock
                {
                    Text = "Nenhum jogo na biblioteca",
                    FontSize = 11,
                    Foreground = GetResourceBrush("TextMutedBrush", "#64748B")
                }
            };
            _trayMenu.Items.Add(emptyItem);
        }
        else
        {
            foreach (var game in recentGames)
            {
                var capturedGame = game;
                var gameMenuItem = new MenuItem
                {
                    Header = CreateGameMenuRow(capturedGame),
                    Padding = new Thickness(8, 5, 8, 5)
                };

                gameMenuItem.Click += async (s, e) =>
                {
                    if (_onLaunchGame != null)
                    {
                        await _onLaunchGame(capturedGame);
                    }
                };

                _trayMenu.Items.Add(gameMenuItem);
            }
        }

        _trayMenu.Items.Add(new Separator
        {
            Background = GetResourceBrush("DividerBrush", "#242D3C"),
            Margin = new Thickness(4, 3, 4, 3)
        });

        var itemOpen = new MenuItem
        {
            Header = new TextBlock
            {
                Text = _openText,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            },
            Padding = new Thickness(8, 5, 8, 5)
        };
        itemOpen.Click += (s, e) => _onRestoreRequested();

        var itemExit = new MenuItem
        {
            Header = new TextBlock
            {
                Text = _exitText,
                FontSize = 12,
                Foreground = GetResourceBrush("DangerBrush", "#E5534B")
            },
            Padding = new Thickness(8, 5, 8, 5)
        };
        itemExit.Click += (s, e) => _onExitRequested();

        _trayMenu.Items.Add(itemOpen);
        _trayMenu.Items.Add(itemExit);
    }

    private UIElement CreateStatusHeaderElement(string text, bool isRunning)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var dot = new Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = isRunning
                ? GetResourceBrush("SuccessBrush", "#2EA043")
                : GetResourceBrush("AccentBrush", "#4A8FE7"),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(dot);

        var tb = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = isRunning
                ? GetResourceBrush("SuccessBrush", "#2EA043")
                : GetResourceBrush("TextSecondaryBrush", "#94A3B8"),
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 220,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        panel.Children.Add(tb);

        return panel;
    }

    private UIElement CreateGameMenuRow(GameItem game)
    {
        var grid = new Grid
        {
            MinWidth = 230
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Icon
        var iconBorder = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(2),
            Background = GetResourceBrush("InputBgBrush", "#11151C"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var bmp = TryLoadSmallBitmap(game.IconPath);
        if (bmp != null)
        {
            iconBorder.Child = new Image
            {
                Source = bmp,
                Width = 16,
                Height = 16,
                Stretch = Stretch.UniformToFill
            };
        }
        else
        {
            iconBorder.Child = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = GetResourceBrush("AccentBrush", "#4A8FE7"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        Grid.SetColumn(iconBorder, 0);
        grid.Children.Add(iconBorder);

        // Title + Subtitle
        var titleBlock = new TextBlock
        {
            Text = game.Title,
            FontSize = 12,
            FontWeight = game.IsRunning ? FontWeights.Bold : FontWeights.Normal,
            Foreground = game.IsRunning
                ? GetResourceBrush("SuccessBrush", "#2EA043")
                : GetResourceBrush("TextPrimaryBrush", "#E6EDF3"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 155,
            Margin = new Thickness(2, 0, 8, 0)
        };
        Grid.SetColumn(titleBlock, 1);
        grid.Children.Add(titleBlock);

        // Status / Action badge on right
        var badgeBorder = new Border
        {
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(5, 1, 5, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Background = game.IsRunning
                ? GetResourceBrush("SuccessBrush", "#2EA043")
                : GetResourceBrush("BadgeBgBrush", "#1C2432")
        };

        var badgeText = new TextBlock
        {
            Text = game.IsRunning ? "● JOGANDO" : "▶ JOGAR",
            FontSize = 9.5,
            FontWeight = FontWeights.Bold,
            Foreground = game.IsRunning
                ? Brushes.White
                : GetResourceBrush("AccentBrush", "#4A8FE7")
        };
        badgeBorder.Child = badgeText;
        Grid.SetColumn(badgeBorder, 2);
        grid.Children.Add(badgeBorder);

        return grid;
    }

    private static BitmapImage? TryLoadSmallBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.DecodePixelWidth = 24;
            bi.UriSource = new Uri(path, UriKind.Absolute);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            return null;
        }
    }

    private static Brush GetResourceBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources[key] is Brush b)
            return b;
        return new SolidColorBrush(ThemeService.ParseColor(fallbackHex, Colors.Gray));
    }

    public void UpdateMenuLocalization(string openText, string exitText)
    {
        _openText = openText;
        _exitText = exitText;
        RebuildContextMenu();
    }

    public void UpdateRunningStatusTooltip()
    {
        var allGames = _getGames?.Invoke()?.ToList() ?? new List<GameItem>();
        var running = allGames.Where(g => g.IsRunning).ToList();
        if (running.Count > 0)
        {
            AddOrUpdateTrayIcon($"All In Box — Jogando: {string.Join(", ", running.Select(g => g.Title))}");
        }
        else
        {
            AddOrUpdateTrayIcon("All In Box — Central de Jogos");
        }
    }

    public void AddOrUpdateTrayIcon(string tooltip)
    {
        if (_hwnd == IntPtr.Zero || _disposed) return;

        try
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = tooltip.Length >= 127 ? tooltip[..126] : tooltip,
                szInfo = string.Empty,
                szInfoTitle = string.Empty
            };

            if (!_isAdded)
            {
                _isAdded = Shell_NotifyIcon(NIM_ADD, ref data);
            }
            else
            {
                Shell_NotifyIcon(NIM_MODIFY, ref data);
            }
        }
        catch { }
    }

    public void ShowBalloonNotification(string title, string message)
    {
        if (!_isAdded || _hwnd == IntPtr.Zero || _disposed) return;

        try
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_INFO,
                szInfoTitle = title.Length >= 63 ? title[..62] : title,
                szInfo = message.Length >= 255 ? message[..254] : message,
                szTip = "All In Box",
                dwInfoFlags = 0x00000001 // NIIF_INFO
            };
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }
        catch { }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int mouseMsg = (int)(lParam.ToInt64() & 0xFFFF);
            if (mouseMsg == WM_LBUTTONUP || mouseMsg == WM_LBUTTONDBLCLK)
            {
                Application.Current?.Dispatcher?.BeginInvoke(_onRestoreRequested);
                handled = true;
            }
            else if (mouseMsg == WM_RBUTTONUP)
            {
                Application.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    if (_trayMenu != null)
                    {
                        RebuildContextMenu();
                        SetForegroundWindow(_hwnd);
                        _trayMenu.IsOpen = true;
                    }
                });
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_isAdded && _hwnd != IntPtr.Zero)
            {
                var data = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _hwnd,
                    uID = 1
                };
                Shell_NotifyIcon(NIM_DELETE, ref data);
                _isAdded = false;
            }

            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

            _hwndSource?.RemoveHook(WndProc);
        }
        catch { }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
