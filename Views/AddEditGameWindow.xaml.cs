using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AllInBox.Models;
using AllInBox.Services;
using Microsoft.Win32;

namespace AllInBox.Views;

public partial class AddEditGameWindow : Window
{
    public GameItem Game { get; private set; }
    private readonly bool _isEditMode;
    private string? _customIconPath;

    public AddEditGameWindow(GameItem? existingGame = null)
    {
        InitializeComponent();
        
        _isEditMode = existingGame != null;
        Game = existingGame ?? new GameItem();

        if (_isEditMode)
        {
            TxtHeaderTitle.Text = "Editar Jogo";
            BtnSave.Content = "Salvar Alterações";
            
            TxtTitle.Text = Game.Title;
            TxtExePath.Text = Game.ExecutablePath;
            TxtWorkDir.Text = Game.WorkingDirectory;
            TxtArguments.Text = Game.Arguments;
            CmbPlatform.Text = Game.Platform;
            TxtCategory.Text = Game.Category;
            ChkFavorite.IsChecked = Game.IsFavorite;
            TxtNotes.Text = Game.Notes;
            _customIconPath = Game.IconPath;

            UpdateIconPreview(_customIconPath ?? Game.ExecutablePath);
        }
        else
        {
            CmbPlatform.SelectedIndex = 0;
        }

        MouseDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        };
    }

    private void BtnBrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Selecionar Executável ou Atalho do Jogo",
            Filter = "Executáveis e Atalhos (*.exe;*.bat;*.lnk;*.url)|*.exe;*.bat;*.lnk;*.url|Todos os Arquivos (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog(this) == true)
        {
            string selectedPath = openFileDialog.FileName;
            var (targetExe, workDir, args) = ShortcutResolver.ResolveShortcut(selectedPath);

            TxtExePath.Text = targetExe;
            TxtWorkDir.Text = workDir;
            if (!string.IsNullOrWhiteSpace(args))
                TxtArguments.Text = args;

            if (string.IsNullOrWhiteSpace(TxtTitle.Text))
            {
                TxtTitle.Text = GameLibraryService.InferGameTitleFromPath(selectedPath, targetExe);
            }

            if (string.IsNullOrWhiteSpace(CmbPlatform.Text) || CmbPlatform.SelectedIndex == 0)
            {
                CmbPlatform.Text = DetectPlatform(targetExe);
            }

            UpdateIconPreview(targetExe);
        }
    }

    private void TxtExePath_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isEditMode && string.IsNullOrWhiteSpace(_customIconPath))
        {
            UpdateIconPreview(TxtExePath.Text.Trim());
        }
    }

    private void BorderIcon_Click(object sender, MouseButtonEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Selecionar Ícone ou Imagem",
            Filter = "Imagens e Ícones (*.ico;*.png;*.jpg;*.jpeg;*.exe)|*.ico;*.png;*.jpg;*.jpeg;*.exe|Todos os Arquivos (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog(this) == true)
        {
            _customIconPath = openFileDialog.FileName;
            UpdateIconPreview(_customIconPath);
        }
    }

    private void UpdateIconPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ImgIconPreview.Source = null;
            TxtNoIcon.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var iconSource = IconExtractor.ResolveBestIconSource(path, TxtTitle.Text.Trim(), TxtWorkDir.Text.Trim());
            if (iconSource != null)
            {
                ImgIconPreview.Source = iconSource;
                TxtNoIcon.Visibility = Visibility.Collapsed;
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            ImgIconPreview.Source = bitmap;
            TxtNoIcon.Visibility = Visibility.Collapsed;
        }
        catch
        {
            ImgIconPreview.Source = null;
            TxtNoIcon.Visibility = Visibility.Visible;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string exePath = TxtExePath.Text.Trim();
        string title = TxtTitle.Text.Trim();

        if (string.IsNullOrWhiteSpace(exePath))
        {
            AppDialogWindow.Show(this, "Por favor, selecione ou informe o caminho do executável do jogo.", "Campo Obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = Path.GetFileNameWithoutExtension(exePath);
        }

        Game.Title = title;
        Game.ExecutablePath = exePath;
        Game.WorkingDirectory = TxtWorkDir.Text.Trim();
        Game.Arguments = TxtArguments.Text.Trim();
        Game.Platform = string.IsNullOrWhiteSpace(CmbPlatform.Text) ? "PC / Outros" : CmbPlatform.Text.Trim();
        Game.Category = string.IsNullOrWhiteSpace(TxtCategory.Text) ? "Geral" : TxtCategory.Text.Trim();
        Game.IsFavorite = ChkFavorite.IsChecked ?? false;
        Game.Notes = TxtNotes.Text.Trim();

        GameLibraryService.RepairSpecialGameEntry(Game);

        if (!string.IsNullOrWhiteSpace(_customIconPath))
        {
            if (_customIconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || _customIconPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                Game.IconPath = IconExtractor.ExtractAndCacheIcon(_customIconPath, Game.Id, Game.Title, Game.WorkingDirectory) ?? string.Empty;
            }
            else
            {
                Game.IconPath = _customIconPath;
            }
        }
        else if (string.IsNullOrWhiteSpace(Game.IconPath) && File.Exists(Game.ExecutablePath))
        {
            Game.IconPath = IconExtractor.ExtractAndCacheIcon(Game.ExecutablePath, Game.Id, Game.Title, Game.WorkingDirectory) ?? string.Empty;
        }

        if (Game.InstallSizeBytes <= 0)
        {
            Game.InstallSizeBytes = GameLibraryService.CalculateGameInstallSize(Game.ExecutablePath, Game.WorkingDirectory);
        }

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

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
}
