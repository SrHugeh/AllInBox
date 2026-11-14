using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AllInBox.Views;

public partial class AppDialogWindow : Window
{
    public bool DialogResultValue { get; private set; } = false;

    public AppDialogWindow()
    {
        InitializeComponent();
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResultValue = true;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResultValue = false;
        DialogResult = false;
        Close();
    }

    public static bool Show(Window? owner, string message, string title = "All In Box", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
    {
        var activeOwner = owner ?? Application.Current?.MainWindow;
        var dlg = new AppDialogWindow
        {
            Owner = activeOwner != null && activeOwner.IsLoaded ? activeOwner : null,
            WindowStartupLocation = activeOwner != null && activeOwner.IsLoaded ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
        };

        dlg.TxtTitle.Text = title;
        dlg.TxtMessage.Text = message;

        // Configure Icon & Accent Colors
        dlg.BorderIcon.SetResourceReference( System.Windows.Controls.Border.BackgroundProperty, "CardBgBrush");
        switch (icon)
        {
            case MessageBoxImage.Error:
                dlg.TxtIcon.Text = "✕";
                dlg.TxtIcon.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "DangerBrush");
                dlg.BorderIcon.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "DangerBrush");
                dlg.BtnPrimary.Style = (Style)dlg.FindResource("DialogDangerButton");
                break;

            case MessageBoxImage.Warning:
                dlg.TxtIcon.Text = "⚠";
                dlg.TxtIcon.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "WarningBrush");
                dlg.BorderIcon.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "WarningBrush");
                break;

            case MessageBoxImage.Question:
                dlg.TxtIcon.Text = "❓";
                dlg.TxtIcon.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "AccentBrush");
                dlg.BorderIcon.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "AccentBrush");
                break;

            default:
                dlg.TxtIcon.Text = "ℹ";
                dlg.TxtIcon.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "AccentBrush");
                dlg.BorderIcon.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "AccentBrush");
                break;
        }

        // Configure Buttons
        if (buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.OKCancel)
        {
            dlg.BtnSecondary.Visibility = Visibility.Visible;
            dlg.BtnSecondary.Content = buttons == MessageBoxButton.YesNo ? "Não" : "Cancelar";
            dlg.BtnPrimary.Content = buttons == MessageBoxButton.YesNo ? "Sim" : "OK";
        }
        else
        {
            dlg.BtnSecondary.Visibility = Visibility.Collapsed;
            dlg.BtnPrimary.Content = "Entendi";
        }

        bool? res = dlg.ShowDialog();
        return res == true || dlg.DialogResultValue;
    }

    public static bool Confirm(Window? owner, string message, string title = "Confirmar", string confirmText = "Confirmar", string cancelText = "Cancelar", bool isDanger = false)
    {
        var activeOwner = owner ?? Application.Current?.MainWindow;
        var dlg = new AppDialogWindow
        {
            Owner = activeOwner != null && activeOwner.IsLoaded ? activeOwner : null,
            WindowStartupLocation = activeOwner != null && activeOwner.IsLoaded ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
        };

        dlg.TxtTitle.Text = title;
        dlg.TxtMessage.Text = message;

        dlg.BorderIcon.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "CardBgBrush");
        if (isDanger)
        {
            dlg.TxtIcon.Visibility = Visibility.Collapsed;
            dlg.ImageIcon.Visibility = Visibility.Visible;
            dlg.BorderIcon.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "DangerBrush");
            dlg.BtnPrimary.Style = (Style)dlg.FindResource("DialogDangerButton");
        }
        else
        {
            dlg.TxtIcon.Visibility = Visibility.Visible;
            dlg.ImageIcon.Visibility = Visibility.Collapsed;
            dlg.TxtIcon.Text = "❓";
            dlg.TxtIcon.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "AccentBrush");
            dlg.BorderIcon.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "AccentBrush");
        }

        dlg.BtnSecondary.Visibility = Visibility.Visible;
        dlg.BtnSecondary.Content = cancelText;
        dlg.BtnPrimary.Content = confirmText;

        bool? res = dlg.ShowDialog();
        return res == true || dlg.DialogResultValue;
    }
}
