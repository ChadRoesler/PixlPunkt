using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Brush;
using PixlPunkt.Core.Canvas;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Palette.Helpers.Defaults;
using PixlPunkt.Core.Plugins;
using PixlPunkt.Core.Settings;
using PixlPunkt.Core.Tile;
using PixlPunkt.Core.Tools;
using Serilog.Events;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PixlPunkt.UI.Settings
{
    /// <summary>
    /// View model for displaying a brush in the settings list.
    /// </summary>
    public sealed class BrushListItem
    {
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public ImageSource? Icon { get; set; }
    }

    /// <summary>
    /// View model for palette selection in settings.
    /// </summary>
    public sealed class PaletteSelectionItem
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = ""; // "Built-in" or "Custom"
        public bool IsCustom { get; set; }

        public override string ToString() => Name;
    }

    public sealed partial class SettingsWindow : Window
    {
        public ObservableCollection<CustomCanvasTemplate> CustomTemplates { get; } = new();
        public ObservableCollection<CustomPalette> CustomPalettes { get; } = new();
        public ObservableCollection<LoadedPlugin> LoadedPlugins { get; } = new();
        public ObservableCollection<BrushListItem> CustomBrushes { get; } = new();
        public ObservableCollection<PaletteSelectionItem> AvailablePalettes { get; } = new();

        private ToolState? _toolState;

        public SettingsWindow()
        {
            this.InitializeComponent();

            NavList.SelectionChanged += NavList_SelectionChanged;
            NavList.SelectedIndex = 0;

            StorageFolderPickBtn.Click += StorageFolderPickBtn_Click;
            OpenLogFolderBtn.Click += OpenLogFolderBtn_Click;
            SaveBtn.Click += SaveBtn_Click;
            CancelBtn.Click += CancelBtn_Click;
            ApplyBtn.Click += ApplyBtn_Click;

            OpenPaletteFolderBtn.Click += OpenPaletteFolderBtn_Click;
            RefreshPalettesBtn.Click += RefreshPalettesBtn_Click;
            DeletePaletteBtn.Click += DeletePaletteBtn_Click;
            PaletteListView.SelectionChanged += PaletteListView_SelectionChanged;
            PaletteListView.ItemsSource = CustomPalettes;

            // Default Palette ComboBox
            DefaultPaletteCombo.ItemsSource = AvailablePalettes;
            DefaultPaletteCombo.DisplayMemberPath = "Name";

            OpenBrushFolderBtn.Click += OpenBrushFolderBtn_Click;
            RefreshBrushesBtn.Click += RefreshBrushesBtn_Click;
            BrushListView.ItemsSource = CustomBrushes;

            OpenTemplateFolderBtn.Click += OpenTemplateFolderBtn_Click;
            RefreshTemplatesBtn.Click += RefreshTemplatesBtn_Click;
            DeleteTemplateBtn.Click += DeleteTemplateBtn_Click;
            TemplateListView.SelectionChanged += TemplateListView_SelectionChanged;
            TemplateListView.ItemsSource = CustomTemplates;

            OpenPluginFolderBtn.Click += OpenPluginFolderBtn_Click;
            RefreshPluginsBtn.Click += RefreshPluginsBtn_Click;
            PluginListView.ItemsSource = LoadedPlugins;

            // Tiles panel
            BrowseTileSetBtn.Click += BrowseTileSetBtn_Click;
            ClearTileSetBtn.Click += ClearTileSetBtn_Click;

            // Load settings
            var s = AppSettings.Instance;
            StorageFolderBox.Text = string.IsNullOrEmpty(s.StorageFolderPath)
                ? GetDefaultAutoSavePath()
                : s.StorageFolderPath;
            BackupIntervalBox.Value = s.AutoBackupMinutes;
            MaxBackupCountBox.Value = s.MaxBackupCount;
            PaletteSwatchSizeBox.Value = s.PaletteSwatchSize;
            TileSwatchSizeBox.Value = s.TileSwatchSize;
            DefaultTileSetPathBox.Text = s.DefaultTileSetPath;
            AppThemeChoice.SelectedIndex = (int)s.AppTheme;
            StripeChoice.SelectedIndex = (int)s.StripeTheme;
            DefaultPaletteSortCombo.SelectedIndex = (int)s.DefaultPaletteSortMode;

            // Initialize log path display
            LogPathBox.Text = LoggingService.LogDirectory;

            // Initialize log level combo selection
            InitializeLogLevelCombo(s.LogLevel);

            // Initialize all panels
            UpdateBrushPanelInfo();
            UpdateTemplatePanelInfo();
            UpdatePalettePanelInfo();
            UpdatePluginPanelInfo();
            UpdateTilesPanelInfo();

            // Initialize default palette combo
            PopulateAvailablePalettes();
            SelectDefaultPalette(s.DefaultPalette);

            // Show current runtime log level
            try
            {
                CurrentLogLevelText.Text = LoggingService.CurrentLevel.ToString();
            }
            catch { }
        }

        /// <summary>
        /// Initializes the settings window with the application's tool state.
        /// Call this after construction to enable shortcut settings.
        /// </summary>
        /// <param name="toolState">The application's tool state.</param>
        public void InitializeWithToolState(ToolState toolState)
        {
            _toolState = toolState;
            ShortcutsPanel.Initialize(toolState);
        }

        private void InitializeLogLevelCombo(string? configured)
        {
            if (string.IsNullOrEmpty(configured)) configured = "Information";

            var levelName = configured;

            int index = 2; // default to Information
            switch (levelName.ToLowerInvariant())
            {
                case "verbose": index = 0; break;
                case "debug": index = 1; break;
                case "information": index = 2; break;
                case "warning": index = 3; break;
                case "error": index = 4; break;
                case "fatal": index = 5; break;
            }

            try
            {
                LogLevelCombo.SelectedIndex = index;
            }
            catch
            {
                // If the combo isn't present for some reason, ignore
            }
        }

        // ─────────────────────────────────────────────────────────────
        // DEFAULT PALETTE SELECTION
        // ─────────────────────────────────────────────────────────────

        private void PopulateAvailablePalettes()
        {
            AvailablePalettes.Clear();

            // Add built-in palettes first
            foreach (var palette in DefaultPalettes.All)
            {
                AvailablePalettes.Add(new PaletteSelectionItem
                {
                    Name = palette.Name,
                    Category = "Built-in",
                    IsCustom = false
                });
            }

            // Add custom palettes
            CustomPaletteService.Instance.Initialize();
            foreach (var palette in CustomPaletteService.Instance.Palettes)
            {
                AvailablePalettes.Add(new PaletteSelectionItem
                {
                    Name = palette.Name,
                    Category = "Custom",
                    IsCustom = true
                });
            }
        }

        private void SelectDefaultPalette(string paletteName)
        {
            if (string.IsNullOrEmpty(paletteName))
                paletteName = AppSettings.FallbackPaletteName;

            // Find the palette in the list
            for (int i = 0; i < AvailablePalettes.Count; i++)
            {
                if (AvailablePalettes[i].Name.Equals(paletteName, StringComparison.OrdinalIgnoreCase))
                {
                    DefaultPaletteCombo.SelectedIndex = i;
                    return;
                }
            }

            // Not found - select the fallback
            for (int i = 0; i < AvailablePalettes.Count; i++)
            {
                if (AvailablePalettes[i].Name.Equals(AppSettings.FallbackPaletteName, StringComparison.OrdinalIgnoreCase))
                {
                    DefaultPaletteCombo.SelectedIndex = i;
                    return;
                }
            }

            // Last resort - select first item
            if (AvailablePalettes.Count > 0)
                DefaultPaletteCombo.SelectedIndex = 0;
        }

        private string GetSelectedDefaultPalette()
        {
            if (DefaultPaletteCombo.SelectedItem is PaletteSelectionItem item)
                return item.Name;
            return AppSettings.FallbackPaletteName;
        }

        // ─────────────────────────────────────────────────────────────
        // TILES PANEL
        // ─────────────────────────────────────────────────────────────

        private void UpdateTilesPanelInfo()
        {
            var path = AppSettings.Instance.DefaultTileSetPath;
            DefaultTileSetPathBox.Text = path;

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var data = TileIO.Load(path);
                    TileSetInfoText.Text = $"{data.Tiles.Count} tiles ({data.TileWidth}x{data.TileHeight})";
                    TileSetInfoPanel.Visibility = Visibility.Visible;
                }
                catch
                {
                    TileSetInfoText.Text = "Invalid or corrupted file";
                    TileSetInfoPanel.Visibility = Visibility.Visible;
                }
            }
            else if (!string.IsNullOrEmpty(path))
            {
                TileSetInfoText.Text = "File not found";
                TileSetInfoPanel.Visibility = Visibility.Visible;
            }
            else
            {
                TileSetInfoPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void BrowseTileSetBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(TileIO.FileExtension);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                DefaultTileSetPathBox.Text = file.Path;
                UpdateTilesPanelInfo();
            }
        }

        private void ClearTileSetBtn_Click(object sender, RoutedEventArgs e)
        {
            DefaultTileSetPathBox.Text = string.Empty;
            TileSetInfoPanel.Visibility = Visibility.Collapsed;
        }

        // ─────────────────────────────────────────────────────────────
        // PALETTES PANEL
        // ─────────────────────────────────────────────────────────────

        private void UpdatePalettePanelInfo()
        {
            PalettePathBox.Text = CustomPaletteIO.GetPalettesDirectory();

            CustomPalettes.Clear();
            CustomPaletteService.Instance.RefreshPalettes();

            foreach (var palette in CustomPaletteService.Instance.Palettes)
            {
                CustomPalettes.Add(palette);
            }

            PaletteCountText.Text = CustomPalettes.Count.ToString();
            DeletePaletteBtn.IsEnabled = false;

            // Also refresh the default palette combo
            PopulateAvailablePalettes();
            SelectDefaultPalette(AppSettings.Instance.DefaultPalette);

            LoggingService.Debug("Palette panel refreshed");
        }

        private void OpenPaletteFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = CustomPaletteIO.GetPalettesDirectory();

            try
            {
                CustomPaletteIO.EnsureDirectoryExists();
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info($"Opened palette folder: {path}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open palette folder: {path}", ex);
            }
        }

        private void RefreshPalettesBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdatePalettePanelInfo();
        }

        private void PaletteListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeletePaletteBtn.IsEnabled = PaletteListView.SelectedItem != null;
        }

        private async void DeletePaletteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PaletteListView.SelectedItem is not CustomPalette selected)
                return;

            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Delete Palette",
                Content = $"Are you sure you want to delete the palette \"{selected.Name}\"?\n\nThis cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = GetEffectiveTheme()
            };

            var result = await dlg.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                CustomPaletteService.Instance.DeletePalette(selected.Name);
                UpdatePalettePanelInfo();
                LoggingService.Info($"Deleted palette: {selected.Name}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // BRUSHES PANEL
        // ─────────────────────────────────────────────────────────────

        private async void UpdateBrushPanelInfo()
        {
            BrushPathBox.Text = BrushMarkIO.GetBrushDirectory();

            CustomBrushes.Clear();
            BrushDefinitionService.Instance.Initialize();

            foreach (var brush in BrushDefinitionService.Instance.GetAllBrushes())
            {
                var item = new BrushListItem
                {
                    Name = brush.DisplayName,
                    FullName = brush.FullName
                };

                // Load icon asynchronously
                var iconData = CustomBrushIcons.Instance.GetIcon(brush);
                if (iconData != null)
                {
                    item.Icon = await CustomBrushIcons.ToImageSourceAsync(iconData);
                }

                CustomBrushes.Add(item);
            }

            BrushCountText.Text = CustomBrushes.Count.ToString();
            LoggingService.Debug($"Brush panel refreshed: {CustomBrushes.Count} brushes");
        }

        private void OpenBrushFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = BrushMarkIO.GetBrushDirectory();

            try
            {
                System.IO.Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info($"Opened brush folder: {path}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open brush folder: {path}", ex);
            }
        }

        private void RefreshBrushesBtn_Click(object sender, RoutedEventArgs e)
        {
            BrushDefinitionService.Instance.RefreshBrushes();
            UpdateBrushPanelInfo();
        }

        // ─────────────────────────────────────────────────────────────
        // TEMPLATES PANEL
        // ─────────────────────────────────────────────────────────────

        private void UpdateTemplatePanelInfo()
        {
            TemplatePathBox.Text = CustomTemplateIO.GetTemplatesDirectory();

            CustomTemplates.Clear();
            CustomTemplateService.Instance.RefreshTemplates();

            foreach (var template in CustomTemplateService.Instance.Templates)
            {
                CustomTemplates.Add(template);
            }

            TemplateCountText.Text = CustomTemplates.Count.ToString();
            DeleteTemplateBtn.IsEnabled = false;
            LoggingService.Debug("Template panel refreshed");
        }

        private void OpenTemplateFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = CustomTemplateIO.GetTemplatesDirectory();

            try
            {
                CustomTemplateIO.EnsureDirectoryExists();
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info($"Opened template folder: {path}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open template folder: {path}", ex);
            }
        }

        private void RefreshTemplatesBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateTemplatePanelInfo();
        }

        private void TemplateListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeleteTemplateBtn.IsEnabled = TemplateListView.SelectedItem != null;
        }

        private async void DeleteTemplateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateListView.SelectedItem is not CustomCanvasTemplate selected)
                return;

            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Delete Template",
                Content = $"Are you sure you want to delete the template \"{selected.Name}\"?\n\nThis cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = GetEffectiveTheme()
            };

            var result = await dlg.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                CustomTemplateService.Instance.DeleteTemplate(selected.Name);
                UpdateTemplatePanelInfo();
                LoggingService.Info($"Deleted template: {selected.Name}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // PLUGINS PANEL
        // ─────────────────────────────────────────────────────────────

        private void UpdatePluginPanelInfo()
        {
            PluginPathBox.Text = PluginRegistry.Instance.PluginsDirectory;

            LoadedPlugins.Clear();

            foreach (var plugin in PluginRegistry.Instance.GetAllPlugins())
            {
                LoadedPlugins.Add(plugin);
            }

            PluginCountText.Text = LoadedPlugins.Count.ToString();
            LoggingService.Debug($"Plugin panel refreshed: {LoadedPlugins.Count} plugins");
        }

        private void OpenPluginFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = PluginRegistry.Instance.PluginsDirectory;

            try
            {
                System.IO.Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info($"Opened plugin folder: {path}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open plugin folder: {path}", ex);
            }
        }

        private void RefreshPluginsBtn_Click(object sender, RoutedEventArgs e)
        {
            PluginRegistry.Instance.RefreshPlugins();
            UpdatePluginPanelInfo();
            LoggingService.Info("Plugins refreshed via settings window");
        }

        // ─────────────────────────────────────────────────────────────
        // LOGGING
        // ─────────────────────────────────────────────────────────────

        private void OpenLogFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = LoggingService.LogDirectory;

            try
            {
                AppPaths.EnsureDirectoryExists(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info("Opened log folder: {LogPath}", path);
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open log folder: {path}", ex);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // GENERAL SETTINGS
        // ─────────────────────────────────────────────────────────────

        private async void StorageFolderPickBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                StorageFolderBox.Text = folder.Path;
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            LoggingService.Info("Settings saved via settings window");
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) => Close();

        private static string GetDefaultAutoSavePath() => AppPaths.AutoSaveDirectory;

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var idx = NavList.SelectedIndex;
            GeneralPanel.Visibility = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
            ShortcutsPanel.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
            PalettePanel.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
            TilesPanel.Visibility = idx == 3 ? Visibility.Visible : Visibility.Collapsed;
            BrushesPanel.Visibility = idx == 4 ? Visibility.Visible : Visibility.Collapsed;
            TemplatePanel.Visibility = idx == 5 ? Visibility.Visible : Visibility.Collapsed;
            PluginsPanel.Visibility = idx == 6 ? Visibility.Visible : Visibility.Collapsed;

            // Refresh data when switching tabs
            switch (idx)
            {
                case 1:
                    if (_toolState != null)
                        ShortcutsPanel.RefreshShortcutList();
                    break;
                case 2: UpdatePalettePanelInfo(); break;
                case 3: UpdateTilesPanelInfo(); break;
                case 4: UpdateBrushPanelInfo(); break;
                case 5: UpdateTemplatePanelInfo(); break;
                case 6: UpdatePluginPanelInfo(); break;
            }
        }

        private void ApplyBtn_Click(object? sender, RoutedEventArgs e)
        {
            ApplySettings();
            LoggingService.Info("Settings applied via Apply button");
        }

        private void ApplySettings()
        {
            try
            {
                var s = AppSettings.Instance;

                // Apply general settings
                s.StorageFolderPath = StorageFolderBox.Text ?? GetDefaultAutoSavePath();
                s.AutoBackupMinutes = Math.Max(4, (int)BackupIntervalBox.Value);
                s.MaxBackupCount = Math.Max(1, (int)MaxBackupCountBox.Value);
                s.PaletteSwatchSize = (int)PaletteSwatchSizeBox.Value;
                s.TileSwatchSize = (int)TileSwatchSizeBox.Value;
                s.DefaultTileSetPath = DefaultTileSetPathBox.Text ?? string.Empty;
                s.AppTheme = (AppThemeChoice)AppThemeChoice.SelectedIndex;
                s.StripeTheme = (StripeThemeChoice)StripeChoice.SelectedIndex;
                s.DefaultPalette = GetSelectedDefaultPalette();
                s.DefaultPaletteSortMode = (PaletteSortMode)DefaultPaletteSortCombo.SelectedIndex;

                // Apply log level
                if (LogLevelCombo.SelectedItem is ComboBoxItem item && item.Content is string selected)
                {
                    s.LogLevel = selected;
                    if (Enum.TryParse<LogEventLevel>(selected, true, out var lvl))
                    {
                        LoggingService.SetMinimumLevel(lvl);
                    }
                }

                // Save settings to disk
                s.Save();

                // Apply shortcut settings
                ShortcutsPanel.ApplySettings();

                // Update display
                CurrentLogLevelText.Text = LoggingService.CurrentLevel.ToString();

                // Apply settings to app immediately
                try
                {
                    var main = App.PixlPunktMainWindow as PixlPunkt.UI.PixlPunktMainWindow;
                    if (main is not null)
                    {
                        main.SetAppTheme(s.AppTheme);
                        main.SetStripeTheme(s.StripeTheme);
                        main.SetPaletteSwatchSize(s.PaletteSwatchSize);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to apply settings from settings UI", ex);
            }
        }

        /// <summary>
        /// Gets the effective theme based on app settings.
        /// </summary>
        private static ElementTheme GetEffectiveTheme()
        {
            try
            {
                var settings = AppSettings.Instance;
                return settings.AppTheme switch
                {
                    Core.Settings.AppThemeChoice.Light => ElementTheme.Light,
                    Core.Settings.AppThemeChoice.Dark => ElementTheme.Dark,
                    _ => GetSystemTheme()
                };
            }
            catch
            {
                return ElementTheme.Dark;
            }
        }

        /// <summary>
        /// Gets the system theme preference.
        /// </summary>
        private static ElementTheme GetSystemTheme()
        {
            try
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var foreground = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);
                return foreground.R > 128 ? ElementTheme.Dark : ElementTheme.Light;
            }
            catch
            {
                return ElementTheme.Dark;
            }
        }
    }
}
