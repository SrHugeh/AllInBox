using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AllInBox.Models;
using AllInBox.Services;

namespace AllInBox.Views;

/// <summary>Tray menu: a themed popup (recent games + open/exit) shown at the cursor on right-click.</summary>
public partial class TrayPopupWindow : Window
{
    private readonly Func<IEnumerable<GameItem>> _getGames;
    private readonly Func<GameItem, Task> _launch;
    private readonly Action _open;
    private readonly Action _exit;
    private bool _closing;

    public TrayPopupWindow(Func<IEnumerable<GameItem>> getGames, Func<GameItem, Task> launch, Action open, Action exit, string openText, string exitText)
    {
        InitializeComponent();
        _getGames = getGames;
        _launch = launch;
        _open = open;
        _exit = exit;

        BtnOpen.Content = openText;
        BtnExit.Content = exitText;

        Refresh();

        Loaded += (s, e) => PositionNearCursor();
        Deactivated += (s, e) => CloseOnce();
        PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) CloseOnce(); };
    }

    private void CloseOnce()
    {
        if (_closing) return;
        _closing = true;
        try { Close(); } catch { }
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        CloseOnce();
        _open();
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        CloseOnce();
        _exit();
    }

    /// <summary>Rebuilds the game rows from the current library state.</summary>
    public void Refresh()
    {
        var games = _getGames().ToList();
        var running = games.Where(g => g.IsRunning).ToList();

        if (running.Count > 0)
        {
            DotStatus.SetResourceReference(Shape.FillProperty, "SuccessBrush");
            TxtStatus.Text = $"Jogando agora: {string.Join(", ", running.Select(g => g.Title))}";
        }
        else
        {
            DotStatus.SetResourceReference(Shape.FillProperty, "AccentBrush");
            TxtStatus.Text = "Pronto para jogar";
        }

        // Only games that were actually played (or are running), most recent first
        var recent = games
            .Where(g => g.IsRunning || g.LastPlayed.HasValue)
            .OrderByDescending(g => g.IsRunning)
            .ThenByDescending(g => g.LastPlayed ?? DateTime.MinValue)
            .Take(6)
            .ToList();

        TxtSection.Text = "JOGOS RECENTES";
        TxtSection.Visibility = recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        RowsPanel.Children.Clear();
        foreach (var game in recent) RowsPanel.Children.Add(BuildRow(game));

        TxtEmpty.Text = "Você ainda não jogou nada por aqui. Abra a biblioteca para começar.";
        TxtEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private UIElement BuildRow(GameItem game)
    {
        var row = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 2),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Icon tile (falls back to the game's initial)
        var tile = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        tile.SetResourceReference(Border.BackgroundProperty, "InputBgBrush");
        var bmp = TryLoadBitmap(game.IconPath);
        if (bmp != null)
        {
            tile.Child = new Image { Source = bmp, Margin = new Thickness(3), Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(tile.Child, BitmapScalingMode.HighQuality);
        }
        else
        {
            var letter = new TextBlock { Text = game.Initial, FontWeight = FontWeights.Bold, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            letter.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            tile.Child = letter;
        }
        grid.Children.Add(tile);

        // Title + subtitle
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 8, 0) };
        var title = new TextBlock { Text = game.Title, FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        title.SetResourceReference(TextBlock.ForegroundProperty, game.IsRunning ? "SuccessBrush" : "TextPrimaryBrush");
        var sub = new TextBlock { Text = game.IsRunning ? "Em jogo" : game.HasLastPlayed ? game.LastPlayedText : game.Platform, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        texts.Children.Add(title);
        texts.Children.Add(sub);
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        // "Jogar" pill: revealed on hover, replaced by "Em jogo" while running
        // Softly rounded rectangle (not a capsule), consistent with the other buttons
        var pill = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(11, 4, 11, 4), VerticalAlignment = VerticalAlignment.Center };
        var pillText = new TextBlock { FontSize = 11, FontWeight = FontWeights.Bold };
        if (game.IsRunning)
        {
            pill.SetResourceReference(Border.BackgroundProperty, "SuccessBrush");
            pillText.Text = "● Em jogo";
            pillText.Foreground = Brushes.White;
        }
        else
        {
            pill.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
            pillText.Text = "▶ Jogar";
            pillText.SetResourceReference(TextBlock.ForegroundProperty, "AccentForegroundBrush");
            pill.Opacity = 0;
        }
        pill.Child = pillText;
        Grid.SetColumn(pill, 2);
        grid.Children.Add(pill);

        row.Child = grid;

        row.MouseEnter += (s, e) =>
        {
            row.SetResourceReference(Border.BackgroundProperty, "CardHoverBgBrush");
            if (!game.IsRunning) pill.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        };
        row.MouseLeave += (s, e) =>
        {
            row.Background = Brushes.Transparent;
            if (!game.IsRunning) pill.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(120)));
        };
        row.MouseLeftButtonUp += async (s, e) =>
        {
            CloseOnce();
            if (game.IsRunning) _open(); else await _launch(game);
        };

        return row;
    }

    private static BitmapImage? TryLoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.DecodePixelWidth = 68;
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

    /// <summary>Opens above the cursor (the tray lives at the screen edge), kept inside the work area.</summary>
    private void PositionNearCursor()
    {
        try
        {
            GetCursorPos(out var pt);
            var source = PresentationSource.FromVisual(this);
            var cursor = source?.CompositionTarget != null
                ? source.CompositionTarget.TransformFromDevice.Transform(new Point(pt.X, pt.Y))
                : new Point(pt.X, pt.Y);

            var work = SystemParameters.WorkArea;
            double w = ActualWidth, h = ActualHeight;

            double left = Math.Min(Math.Max(work.Left, cursor.X - w + 24), work.Right - w + 8);
            double top = cursor.Y - h + 8;
            if (top < work.Top) top = Math.Min(cursor.Y - 8, work.Bottom - h + 8);

            Left = left;
            Top = top;
        }
        catch { }

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
}
