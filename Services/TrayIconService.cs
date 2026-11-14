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
using AllInBox.Views;

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

    private TrayPopupWindow? _popup;
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

            AddOrUpdateTrayIcon("All In Box - Central de Jogos");
        }
        catch
        {
            // Safeguard against Windows shell anomalies
        }
    }

    /// <summary>Keeps the open popup (if any) in sync with the library; the popup is rebuilt on every right-click.</summary>
    public void RebuildContextMenu()
    {
        _popup?.Refresh();
    }

    private void ShowPopup()
    {
        if (_disposed) return;

        try
        {
            _popup?.Close();

            var popup = new TrayPopupWindow(
                _getGames ?? (() => Enumerable.Empty<GameItem>()),
                _onLaunchGame ?? (_ => Task.CompletedTask),
                _onRestoreRequested,
                _onExitRequested,
                _openText,
                _exitText);
            popup.Closed += (s, e) => { if (ReferenceEquals(_popup, popup)) _popup = null; };
            _popup = popup;

            SetForegroundWindow(_hwnd);
            popup.Show();
            popup.Activate();
        }
        catch
        {
            // Never let a tray glitch take the app down
        }
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
                Application.Current?.Dispatcher?.BeginInvoke(ShowPopup);
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