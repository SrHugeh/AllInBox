using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AllInBox.Services;
using Microsoft.Win32;

namespace AllInBox.Views;

public partial class ScanFolderWindow : Window
{
    private readonly GameScannerService _scanner = new();
    public ObservableCollection<DiscoveredGame> DiscoveredGames { get; } = new();
    public List<DiscoveredGame> SelectedGames { get; private set; } = new();
    public HashSet<string> ExistingExecutablePaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ScanFolderWindow() : this(null) { }

    public ScanFolderWindow(IEnumerable<string>? existingPaths = null)
    {
        InitializeComponent();
        ListDiscovered.ItemsSource = DiscoveredGames;

        if (existingPaths != null)
        {
            foreach (var p in existingPaths)
            {
                if (!string.IsNullOrWhiteSpace(p))
                    ExistingExecutablePaths.Add(p);
            }
        }

        MouseDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        };

        // Default path
        var commonDirs = GameScannerService.GetCommonGameDirectories();
        if (commonDirs.Count > 0)
        {
            TxtFolderPath.Text = commonDirs[0];
        }

        UpdateLocalizedUI();
    }

    private void UpdateLocalizedUI()
    {
        var loc = LocalizationService.Instance;
        Title = loc.Get("Scan_Title");
        TxtHeaderTitle.Text = loc.Get("Scan_Title");
        TxtHeaderSubtitle.Text = loc.Get("Scan_Subtitle");
        TxtDefaultLocations.Text = loc.Get("Scan_DefaultLocations");
        TxtImportAll.Text = loc.Get("Scan_ImportAll");
        TxtCustomFolder.Text = loc.Get("Scan_CustomFolder");
        BtnBrowseFolder.Content = loc.Get("Scan_Browse");
        BtnScan.Content = loc.Get("Scan_ScanFolderBtn");
        TxtSearchingProgress.Text = loc.Get("Scan_SearchingProgress");
        TxtEmptyTitle.Text = loc.Get("Scan_EmptyTitle");
        TxtEmptySub.Text = loc.Get("Scan_EmptySub");
        BtnShowManualFolder.Content = loc.Get("Scan_ManualBtn");
        BtnSelectAll.Content = loc.Get("Scan_SelectAll");
        BtnDeselectAll.Content = loc.Get("Scan_DeselectAll");
        BtnCancel.Content = loc.Get("Cancelar");

        UpdateSelectedCount();
    }

    private void SetActivePresetButton(Button? activeBtn)
    {
        var buttons = new Button[]
        {
            BtnPresetAll, BtnPresetSteam, BtnPresetEpic, BtnPresetRiot,
            BtnPresetMinecraft, BtnPresetUbisoft, BtnPresetGOG
        };

        var activeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2B42"));
        var activeBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF"));
        var activeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF"));

        var inactiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#151C2A"));
        var inactiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222F44"));
        var inactiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));

        foreach (var b in buttons)
        {
            if (b == null) continue;
            if (b == activeBtn)
            {
                b.Background = activeBg;
                b.BorderBrush = activeBorder;
                b.Foreground = activeFg;
                b.FontWeight = FontWeights.Bold;
            }
            else
            {
                b.Background = inactiveBg;
                b.BorderBrush = inactiveBorder;
                b.Foreground = inactiveFg;
                b.FontWeight = FontWeights.Normal;
            }
        }
    }

    private void BtnShowManualFolder_Click(object sender, RoutedEventArgs e)
    {
        PanelManualFolder.Visibility = Visibility.Visible;
        TxtFolderPath.Focus();
    }

    private async void BtnImportAllPlatforms_Click(object sender, RoutedEventArgs e)
    {
        SetActivePresetButton(BtnPresetAll);
        PanelProgress.Visibility = Visibility.Visible;
        PanelEmpty.Visibility = Visibility.Collapsed;
        BtnScan.IsEnabled = false;

        try
        {
            DiscoveredGames.Clear();
            var allResults = new List<DiscoveredGame>();
            string[] platforms = new[] { "Steam", "Epic Games", "Riot Games", "Minecraft", "Ubisoft", "GOG" };

            foreach (var platform in platforms)
            {
                var dirs = GameScannerService.GetDirectoriesForPlatform(platform);
                foreach (var dir in dirs)
                {
                    if (Directory.Exists(dir))
                    {
                        var results = await _scanner.ScanDirectoryAsync(dir, platform);
                        allResults.AddRange(results);
                    }
                }
            }

            // Also check Epic Manifest games directly
            var epicManifests = GameScannerService.GetEpicManifestGames();
            allResults.AddRange(epicManifests);

            // Filter out already imported games and keep distinct by FilePath and Game Root
            var newGames = allResults
                .Where(x => !GameLibraryService.IsBogusLauncherOrTool(x.FilePath, x.SuggestedTitle))
                .GroupBy(x => GameLibraryService.ResolveGameRootDirectory(x.FilePath), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Where(x => !ExistingExecutablePaths.Contains(x.FilePath))
                .ToList();

            foreach (var g in newGames)
            {
                DiscoveredGames.Add(g);
            }

            if (DiscoveredGames.Count > 0)
            {
                ListDiscovered.Visibility = Visibility.Visible;
                PanelEmpty.Visibility = Visibility.Collapsed;
            }
            else
            {
                ListDiscovered.Visibility = Visibility.Collapsed;
                PanelEmpty.Visibility = Visibility.Visible;

                AppDialogWindow.Show(
                    this,
                    "Nenhum novo jogo encontrado nas plataformas.\n\nTodos os jogos instalados detectados já estão adicionados à sua caixa!",
                    "Varredura de Todas as Plataformas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }

            UpdateSelectedCount();
        }
        catch (Exception ex)
        {
            AppDialogWindow.Show(this, $"Erro ao escanear plataformas:\n\n{ex.Message}", "Erro no Scanner", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PanelProgress.Visibility = Visibility.Collapsed;
            BtnScan.IsEnabled = true;
        }
    }

    private async void BtnPlatformPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string platform)
        {
            SetActivePresetButton(btn);

            var dirs = GameScannerService.GetDirectoriesForPlatform(platform);
            if (dirs.Count == 0)
            {
                PanelManualFolder.Visibility = Visibility.Visible;
                TxtFolderPath.Focus();
                AppDialogWindow.Show(this, $"Nenhum diretório padrão de instalação foi encontrado para {platform} neste computador.\n\nA opção de busca manual por pasta foi aberta abaixo para você selecionar o local onde o jogo está instalado.", 
                    "Plataforma Não Encontrada", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtFolderPath.Text = dirs[0];

            PanelProgress.Visibility = Visibility.Visible;
            PanelEmpty.Visibility = Visibility.Collapsed;
            BtnScan.IsEnabled = false;

            try
            {
                DiscoveredGames.Clear();
                var allResults = new List<DiscoveredGame>();

                foreach (var dir in dirs)
                {
                    if (Directory.Exists(dir))
                    {
                        var results = await _scanner.ScanDirectoryAsync(dir, platform);
                        allResults.AddRange(results);
                    }
                }

                // Filter out already imported games and distinct by FilePath and Game Root
                var distinct = allResults
                    .Where(x => !GameLibraryService.IsBogusLauncherOrTool(x.FilePath, x.SuggestedTitle))
                    .GroupBy(x => GameLibraryService.ResolveGameRootDirectory(x.FilePath), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .Where(x => !ExistingExecutablePaths.Contains(x.FilePath))
                    .ToList();

                foreach (var g in distinct)
                {
                    DiscoveredGames.Add(g);
                }

                if (DiscoveredGames.Count > 0)
                {
                    ListDiscovered.Visibility = Visibility.Visible;
                    PanelEmpty.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ListDiscovered.Visibility = Visibility.Collapsed;
                    PanelEmpty.Visibility = Visibility.Visible;
                }

                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                AppDialogWindow.Show(this, $"Erro ao escanear {platform}:\n\n{ex.Message}", "Erro no Scanner", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                PanelProgress.Visibility = Visibility.Collapsed;
                BtnScan.IsEnabled = true;
            }
        }
    }

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecionar Pasta para Escanear Jogos",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            TxtFolderPath.Text = dialog.FolderName;
        }
    }

    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        SetActivePresetButton(null);
        string path = TxtFolderPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            AppDialogWindow.Show(this, "Por favor, selecione uma pasta válida existente.", "Pasta Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PanelProgress.Visibility = Visibility.Visible;
        PanelEmpty.Visibility = Visibility.Collapsed;
        BtnScan.IsEnabled = false;

        try
        {
            var results = await _scanner.ScanDirectoryAsync(path);

            var newResults = results
                .Where(x => !GameLibraryService.IsBogusLauncherOrTool(x.FilePath, x.SuggestedTitle))
                .GroupBy(x => GameLibraryService.ResolveGameRootDirectory(x.FilePath), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Where(x => !ExistingExecutablePaths.Contains(x.FilePath))
                .ToList();

            DiscoveredGames.Clear();
            foreach (var g in newResults)
            {
                DiscoveredGames.Add(g);
            }

            if (DiscoveredGames.Count > 0)
            {
                ListDiscovered.Visibility = Visibility.Visible;
                PanelEmpty.Visibility = Visibility.Collapsed;
            }
            else
            {
                ListDiscovered.Visibility = Visibility.Collapsed;
                PanelEmpty.Visibility = Visibility.Visible;
            }

            UpdateSelectedCount();
        }
        catch (Exception ex)
        {
            AppDialogWindow.Show(this, $"Erro ao escanear pasta:\n\n{ex.Message}", "Erro no Scanner", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PanelProgress.Visibility = Visibility.Collapsed;
            BtnScan.IsEnabled = true;
        }
    }

    private void UpdateSelectedCount()
    {
        int count = DiscoveredGames.Count(x => x.IsSelected);
        BtnImport.Content = LocalizationService.Instance.Get("Scan_ImportSelected", count);
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var g in DiscoveredGames)
        {
            g.IsSelected = true;
        }
        ListDiscovered.Items.Refresh();
        UpdateSelectedCount();
    }

    private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var g in DiscoveredGames)
        {
            g.IsSelected = false;
        }
        ListDiscovered.Items.Refresh();
        UpdateSelectedCount();
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        SelectedGames = DiscoveredGames.Where(x => x.IsSelected).ToList();
        if (SelectedGames.Count == 0)
        {
            AppDialogWindow.Show(this, "Nenhum jogo selecionado para importar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
