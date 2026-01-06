using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Uno.Core.Coloring.Helpers;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.IO;
using PixlPunkt.Uno.Core.Palette;
using PixlPunkt.Uno.Core.Palette.Helpers;
using PixlPunkt.Uno.Core.Palette.Helpers.Defaults;

namespace PixlPunkt.Uno.UI
{
    /// <summary>
    /// Partial class for palette management:
    /// - Preset menu building
    /// - Custom palette menu building
    /// - Import from image/layer/document/JSON/file
    /// - Export to custom palette file
    /// - Color binding between palette and brush
    /// </summary>
    public sealed partial class PixlPunktMainWindow : Window
    {
        //////////////////////////////////////////////////////////////////
        // PALETTE PRESETS MENU
        //////////////////////////////////////////////////////////////////

        private void BuildPalettePresetsMenu()
        {
            if (Palette_Presets_Submenu == null) return;
            Palette_Presets_Submenu.Items.Clear();
            foreach (var preset in DefaultPalettes.All)
            {
                var item = new MenuFlyoutItem { Text = preset.Name, Tag = preset };
                item.Click += PresetMenuItem_Click;
                Palette_Presets_Submenu.Items.Add(item);
            }
        }

        private async void PresetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_palette is null) return;
            if (sender is not FrameworkElement fe || fe.Tag is not NamedPalette preset) return;

            var preview = CreatePalettePreview(preset.Colors);
            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = $"Apply preset: {preset.Name}",
                Content = preview,
                PrimaryButtonText = "Add",
                SecondaryButtonText = "Replace",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var res = await dlg.ShowAsync();
            if (res == ContentDialogResult.None) return;

            if (res == ContentDialogResult.Secondary)
            {
                ReplacePaletteWith(preset.Colors);
            }
            else
            {
                foreach (var c in preset.Colors)
                    _palette.AddColor(c);
            }
        }

        /// <summary>Replaces palette contents with provided colors.</summary>
        private void ReplacePaletteWith(IReadOnlyList<uint> colors)
        {
            for (int i = _palette.Colors.Count - 1; i >= 0; i--)
                _palette.RemoveAt(i);
            foreach (var c in colors)
                _palette.AddColor(c);
        }

        //////////////////////////////////////////////////////////////////
        // CUSTOM PALETTES MENU
        //////////////////////////////////////////////////////////////////

        private void BuildCustomPalettesMenu()
        {
            if (Palette_Custom_Submenu == null) return;
            Palette_Custom_Submenu.Items.Clear();

            // Initialize the service if needed
            CustomPaletteService.Instance.Initialize();

            var customPalettes = CustomPaletteService.Instance.Palettes;

            if (customPalettes.Count == 0)
            {
                var emptyItem = new MenuFlyoutItem
                {
                    Text = "(No custom palettes)",
                    IsEnabled = false
                };
                Palette_Custom_Submenu.Items.Add(emptyItem);
            }
            else
            {
                foreach (var palette in customPalettes)
                {
                    var item = new MenuFlyoutItem { Text = palette.Name, Tag = palette };
                    item.Click += CustomPaletteMenuItem_Click;
                    Palette_Custom_Submenu.Items.Add(item);
                }
            }

            // Add separator and refresh option
            Palette_Custom_Submenu.Items.Add(new MenuFlyoutSeparator());

            var refreshItem = new MenuFlyoutItem { Text = "Refresh Custom Palettes" };
            refreshItem.Click += (s, e) =>
            {
                CustomPaletteService.Instance.RefreshPalettes();
                BuildCustomPalettesMenu();
            };
            Palette_Custom_Submenu.Items.Add(refreshItem);

            var openFolderItem = new MenuFlyoutItem { Text = "Open Palettes Folder" };
            openFolderItem.Click += async (s, e) =>
            {
                var dir = CustomPaletteIO.GetPalettesDirectory();
                CustomPaletteIO.EnsureDirectoryExists();
                try
                {
                    await Windows.System.Launcher.LaunchFolderPathAsync(dir);
                }
                catch (Exception ex)
                {
                    Core.Logging.LoggingService.Debug("Failed to open palettes folder: {Error}", ex.Message);
                }
            };
            Palette_Custom_Submenu.Items.Add(openFolderItem);
        }

        private async void CustomPaletteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_palette is null) return;
            if (sender is not FrameworkElement fe || fe.Tag is not CustomPalette customPalette) return;

            var colors = customPalette.GetBgraColors();
            var preview = CreatePalettePreview(colors);

            // Add description if present
            var contentStack = new StackPanel { Spacing = 8 };
            if (!string.IsNullOrWhiteSpace(customPalette.Description))
            {
                contentStack.Children.Add(new TextBlock
                {
                    Text = customPalette.Description,
                    Opacity = 0.8,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 400
                });
            }
            contentStack.Children.Add(preview);

            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = $"Apply custom palette: {customPalette.Name}",
                Content = contentStack,
                PrimaryButtonText = "Add",
                SecondaryButtonText = "Replace",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var res = await dlg.ShowAsync();
            if (res == ContentDialogResult.None) return;

            if (res == ContentDialogResult.Secondary)
            {
                ReplacePaletteWith(colors);
            }
            else
            {
                foreach (var c in colors)
                    _palette.AddColor(c);
            }
        }

        //////////////////////////////////////////////////////////////////
        // PALETTE IMPORT PREVIEW HELPERS
        //////////////////////////////////////////////////////////////////

        private (UIElement ui, Func<uint[]> getColors) CreatePaletteImportPreview(uint[] originalColors)
        {
            const int maxCols = 16;
            uint[] baseColors = originalColors;
            uint[] currentColors = originalColors;

            var outer = new StackPanel { Spacing = 8 };
            var infoText = new TextBlock { Opacity = 0.7 };
            var mergeRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            var mergeLabel = new TextBlock { Text = "Merge near colors", VerticalAlignment = VerticalAlignment.Center };
            var tolSlider = new Slider
            {
                Minimum = 0,
                Maximum = 64,
                StepFrequency = 1,
                Width = 180,
                Value = 0
            };
            var tolValueText = new TextBlock
            {
                Text = "Tolerance: 0",
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            tolValueText.MinWidth = Math.Ceiling(tolValueText.FontSize * 0.6 * 15.0);

            mergeRow.Children.Add(mergeLabel);
            mergeRow.Children.Add(tolSlider);
            mergeRow.Children.Add(tolValueText);

            var gridStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
            var scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 240,
                Content = gridStack
            };
            bool heightLocked = false;
            scroller.Loaded += (_, __) =>
            {
                if (heightLocked) return;
                heightLocked = true;
                scroller.Height = scroller.ActualHeight;
            };

            outer.Children.Add(infoText);
            outer.Children.Add(mergeRow);
            outer.Children.Add(scroller);

            void RebuildSwatches()
            {
                gridStack.Children.Clear();
                int total = currentColors.Length;
                int rows = (total + maxCols - 1) / maxCols;
                infoText.Text = $"{total} colors ({rows} row{(rows == 1 ? "" : "s")}, {Math.Min(maxCols, total)} max per row)";
                for (int r = 0; r < rows; r++)
                {
                    var rowPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    int start = r * maxCols;
                    int end = Math.Min(start + maxCols, total);
                    for (int i = start; i < end; i++)
                    {
                        var u32 = currentColors[i];
                        rowPanel.Children.Add(new Border
                        {
                            Width = 20,
                            Height = 20,
                            Margin = new Thickness(2),
                            CornerRadius = new CornerRadius(4),
                            Background = new SolidColorBrush(ColorUtil.ToColor(u32)),
                            BorderBrush = new SolidColorBrush(Colors.Gray),
                            BorderThickness = new Thickness(1)
                        });
                    }
                    gridStack.Children.Add(rowPanel);
                }
            }

            void Recompute()
            {
                int tol = (int)tolSlider.Value;
                tolValueText.Text = $"Tolerance: {tol}";
                currentColors = tol <= 0
                    ? baseColors
                    : [.. ColorUtil.MergeNearColors(baseColors, tol, int.MaxValue)];
                RebuildSwatches();
            }

            tolSlider.ValueChanged += (_, __) => Recompute();
            Recompute();
            return (outer, () => [.. currentColors]);
        }

        private StackPanel CreatePalettePreview(uint[] importColors)
        {
            const int maxCols = 16;
            int total = importColors.Length;
            int rows = (total + maxCols - 1) / maxCols;

            var outer = new StackPanel { Spacing = 8 };
            outer.Children.Add(new TextBlock
            {
                Text = $"{total} colors ({rows} row{(rows == 1 ? "" : "s")}, {Math.Min(maxCols, total)} max per row)",
                Opacity = 0.7
            });

            var scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = rows > 6 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                MaxHeight = rows > 6 ? 240 : double.PositiveInfinity
            };

            var gridStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
            for (int r = 0; r < rows; r++)
            {
                var rowPanel = new StackPanel { Orientation = Orientation.Horizontal };
                int start = r * maxCols;
                int end = Math.Min(start + maxCols, total);
                for (int i = start; i < end; i++)
                {
                    var u32 = importColors[i];
                    rowPanel.Children.Add(new Border
                    {
                        Width = 20,
                        Height = 20,
                        Margin = new Thickness(2),
                        CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(ColorUtil.ToColor(u32)),
                        BorderBrush = new SolidColorBrush(Colors.Gray),
                        BorderThickness = new Thickness(1)
                    });
                }
                gridStack.Children.Add(rowPanel);
            }

            scroller.Content = gridStack;
            outer.Children.Add(scroller);
            return outer;
        }

        //////////////////////////////////////////////////////////////////
        // PALETTE IMPORT COMMANDS
        //////////////////////////////////////////////////////////////////

        private async void Palette_ImportFrom_Image_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".gif");

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            try
            {
                var colors = PaletteExtractor
                    .ExtractUniqueColorsFromFile(file.Path, ignoreAlpha: true)
                    .ToArray();
                var (preview, getColors) = CreatePaletteImportPreview(colors);
                var dlg = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = $"Apply Import: {file.Name}",
                    Content = preview,
                    PrimaryButtonText = "Add",
                    SecondaryButtonText = "Replace",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary
                };
                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.None) return;
                var finalColors = getColors();
                if (res == ContentDialogResult.Secondary)
                    ReplacePaletteWith(finalColors);
                else
                    foreach (var c in finalColors) _palette.AddColor(c);
            }
            catch (Exception ex)
            {
                Core.Logging.LoggingService.Debug("Failed to import palette from image '{Path}': {Error}", file.Path, ex.Message);
                _ = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import failed",
                    Content = "Could not read image file.",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        private async void Palette_ImportFrom_Layer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var layer = CurrentHost?.Document?.ActiveLayer;
                if (layer is null)
                {
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Import failed",
                        Content = "No active layer or document to import from.",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                    return;
                }

                var colors = PaletteExtractor
                    .ExtractUniqueColorsFromSurface(layer.Surface, ignoreAlpha: true)
                    .ToArray();
                var (preview, getColors) = CreatePaletteImportPreview(colors);
                var dlg = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = $"Apply Import: {layer.Name}",
                    Content = preview,
                    PrimaryButtonText = "Add",
                    SecondaryButtonText = "Replace",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary
                };
                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.None) return;
                var finalColors = getColors();
                if (res == ContentDialogResult.Secondary)
                    ReplacePaletteWith(finalColors);
                else
                    foreach (var c in finalColors) _palette.AddColor(c);
            }
            catch (Exception ex)
            {
                Core.Logging.LoggingService.Debug("Failed to import palette from layer: {Error}", ex.Message);
                _ = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import failed",
                    Content = "Could not read layer.",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        private async void Palette_ImportFrom_Document_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var document = CurrentHost?.Document;
                if (document is null)
                {
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Import failed",
                        Content = "No active document to import from.",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                    return;
                }

                var colors = PaletteExtractor
                    .ExtractUniqueColorsFromDocument(document, ignoreAlpha: true)
                    .ToArray();
                var (preview, getColors) = CreatePaletteImportPreview(colors);
                var dlg = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = $"Apply Import: {document.Name}",
                    Content = preview,
                    PrimaryButtonText = "Add",
                    SecondaryButtonText = "Replace",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary
                };
                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.None) return;
                var finalColors = getColors();
                if (res == ContentDialogResult.Secondary)
                    ReplacePaletteWith(finalColors);
                else
                    foreach (var c in finalColors) _palette.AddColor(c);
            }
            catch (Exception ex)
            {
                Core.Logging.LoggingService.Debug("Failed to import palette from document: {Error}", ex.Message);
                _ = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import failed",
                    Content = "Could not read document.",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        private async void Palette_ImportFrom_Json_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Import palette (paste JSON)",
                PrimaryButtonText = "Import",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };
            var tb = new TextBox
            {
                AcceptsReturn = true,
                MinWidth = 360,
                MinHeight = 160,
                TextWrapping = TextWrapping.Wrap
            };
            dlg.Content = tb;

            if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    _palette.ImportJson(tb.Text);
                }
                catch (Exception ex)
                {
                    Core.Logging.LoggingService.Debug("Failed to import palette from JSON: {Error}", ex.Message);
                    _ = new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Import failed",
                        Content = "Invalid palette JSON.",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                }
            }
        }

        //////////////////////////////////////////////////////////////////
        // PALETTE EXPORT / RESET COMMANDS
        //////////////////////////////////////////////////////////////////

        private async void Palette_Export_Click(object sender, RoutedEventArgs e)
        {
            if (_palette == null || _palette.Colors.Count == 0)
            {
                await new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Export failed",
                    Content = "No colors in palette to export.",
                    CloseButtonText = "OK"
                }.ShowAsync();
                return;
            }

            // Create dialog content for name and description
            var contentStack = new StackPanel { Spacing = 12, MinWidth = 360 };

            var nameLabel = new TextBlock { Text = "Palette Name:" };
            var nameBox = new TextBox
            {
                PlaceholderText = "My Custom Palette",
                Text = "My Custom Palette"
            };

            var descLabel = new TextBlock { Text = "Description (optional):" };
            var descBox = new TextBox
            {
                PlaceholderText = "A brief description of this palette...",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 80
            };

            // Preview of colors being exported
            var previewLabel = new TextBlock { Text = $"Colors to export: {_palette.Colors.Count}", Opacity = 0.7 };
            var preview = CreatePalettePreview(_palette.Colors.ToArray());

            contentStack.Children.Add(nameLabel);
            contentStack.Children.Add(nameBox);
            contentStack.Children.Add(descLabel);
            contentStack.Children.Add(descBox);
            contentStack.Children.Add(previewLabel);
            contentStack.Children.Add(preview);

            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Save Custom Palette",
                Content = contentStack,
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Copy JSON",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var res = await dlg.ShowAsync();

            if (res == ContentDialogResult.Primary)
            {
                // Save as custom palette file
                var name = string.IsNullOrWhiteSpace(nameBox.Text) ? "My Custom Palette" : nameBox.Text.Trim();
                var description = descBox.Text?.Trim() ?? "";

                try
                {
                    var palette = CustomPaletteService.Instance.SavePalette(name, description, _palette.Colors);
                    BuildCustomPalettesMenu(); // Refresh the menu

                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Palette Saved",
                        Content = $"Palette \"{name}\" saved successfully!\n\nLocation: %AppData%\\PixlPunkt\\Palettes\\",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                }
                catch (Exception ex)
                {
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Save failed",
                        Content = $"Could not save palette: {ex.Message}",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                }
            }
            else if (res == ContentDialogResult.Secondary)
            {
                // Copy JSON to clipboard (legacy behavior)
                var json = _palette.ExportJson();
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(json);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);

                _ = new TeachingTip
                {
                    Title = "Palette exported",
                    Subtitle = "JSON copied to clipboard",
                    IsLightDismissEnabled = true,
                    IsOpen = true
                };
            }
        }

        private void Palette_Reset_Click(object sender, RoutedEventArgs e) => _palette.ResetToDefault();

        //////////////////////////////////////////////////////////////////
        // PALETTE SORTING
        //////////////////////////////////////////////////////////////////

        private void Palette_SortBy_Hue_Click(object sender, RoutedEventArgs e)
            => _palette?.SortPalette(PaletteSortMode.Hue);

        private void Palette_SortBy_Saturation_Click(object sender, RoutedEventArgs e)
            => _palette?.SortPalette(PaletteSortMode.Saturation);

        private void Palette_SortBy_Lightness_Click(object sender, RoutedEventArgs e)
            => _palette?.SortPalette(PaletteSortMode.Lightness);

        private void Palette_SortBy_Luminance_Click(object sender, RoutedEventArgs e)
            => _palette?.SortPalette(PaletteSortMode.Luminance);

        private void Palette_SortBy_Red_Click(object sender, RoutedEventArgs e)
            => _palette?.SortPalette(PaletteSortMode.Red);

        private void Palette_SortBy_Green_Click(object sender, RoutedEventArgs e)
            => _palette?.SortPalette(PaletteSortMode.Green);

        private void Palette_SortBy_Blue_Click(object sender, RoutedEventArgs e)
            => _palette?.SortPalette(PaletteSortMode.Blue);

        private void Palette_SortBy_Reverse_Click(object sender, RoutedEventArgs e)
            => _palette?.SortPalette(PaletteSortMode.Reverse);

        private void Palette_EditFg_Click(object sender, RoutedEventArgs e) { /* reserved */ }
        private void Palette_EditBg_Click(object sender, RoutedEventArgs e) { /* reserved */ }

        //////////////////////////////////////////////////////////////////
        // PALETTE COLOR BINDINGS
        //////////////////////////////////////////////////////////////////

        private void OnForegroundColorPicked(uint c) => ApplyBrushColor(c);
        private void OnBackgroundColorPicked(uint c) { /* reserved */ }
        private void OnPaletteForegroundChanged(uint c) => ApplyBrushColor(c);
        private void OnPaletteBackgroundChanged(uint c) { /* reserved */ }

        private void ApplyBrushColor(uint c)
        {
            uint merged = (c & 0x00FFFFFFu) | ((uint)_toolState.Brush.Opacity << 24);
            _fg = merged;
            CurrentHost?.SetForeground(merged);
        }

        // ====================================================================
        // PALETTE IMPORT FROM FILE (using SDK import registry)
        // ====================================================================

        /// <summary>
        /// Handles importing a palette from a supported file format using the import registry.
        /// </summary>
        private async void Palette_ImportFrom_File_Click(object sender, RoutedEventArgs e)
        {
            // Get all supported palette import extensions from the registry
            var extensions = ImportRegistry.Instance.GetSupportedExtensions(ImportCategory.Palette);
            if (extensions.Count == 0)
            {
                await new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No palette formats available",
                    Content = "No palette import handlers are registered.",
                    CloseButtonText = "OK"
                }.ShowAsync();
                return;
            }

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            foreach (var ext in extensions)
            {
                picker.FileTypeFilter.Add(ext);
            }

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            try
            {
                var ext = System.IO.Path.GetExtension(file.Path);
                var handlers = ImportRegistry.Instance.FindHandlers(ext, ImportCategory.Palette);

                if (handlers.Count == 0)
                {
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Import failed",
                        Content = $"No handler found for file extension '{ext}'.",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                    return;
                }

                // Use the highest priority handler
                var handler = handlers[0];
                var context = new FileImportContext(file.Path);
                var result = handler.Import(context);

                if (result is PaletteImportResult paletteResult)
                {
                    if (!string.IsNullOrEmpty(paletteResult.ErrorMessage))
                    {
                        await new ContentDialog
                        {
                            XamlRoot = Content.XamlRoot,
                            Title = "Import failed",
                            Content = paletteResult.ErrorMessage,
                            CloseButtonText = "OK"
                        }.ShowAsync();
                        return;
                    }

                    if (paletteResult.Colors == null || paletteResult.Colors.Count == 0)
                    {
                        await new ContentDialog
                        {
                            XamlRoot = Content.XamlRoot,
                            Title = "Import failed",
                            Content = "No colors found in the file.",
                            CloseButtonText = "OK"
                        }.ShowAsync();
                        return;
                    }

                    // Show preview dialog
                    var colors = paletteResult.Colors.ToArray();
                    var preview = CreatePalettePreview(colors);
                    var dlg = new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = $"Import Palette: {paletteResult.PaletteName ?? file.Name}",
                        Content = preview,
                        PrimaryButtonText = "Add",
                        SecondaryButtonText = "Replace",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary
                    };

                    var dialogResult = await dlg.ShowAsync();
                    if (dialogResult == ContentDialogResult.None) return;

                    if (dialogResult == ContentDialogResult.Secondary)
                    {
                        ReplacePaletteWith(colors);
                    }
                    else
                    {
                        foreach (var c in colors)
                            _palette.AddColor(c);
                    }
                }
                else
                {
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Import failed",
                        Content = "Unexpected import result type.",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import failed",
                    Content = $"Could not import palette file.\n{ex.Message}",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        // ====================================================================
        // PALETTE EXPORT TO FILE (using SDK export registry)
        // ====================================================================

        /// <summary>
        /// Handles exporting the palette to a supported file format using the export registry.
        /// </summary>
        private async void Palette_ExportTo_File_Click(object sender, RoutedEventArgs e)
        {
            if (_palette == null || _palette.Colors.Count == 0)
            {
                await new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Export failed",
                    Content = "No colors in palette to export.",
                    CloseButtonText = "OK"
                }.ShowAsync();
                return;
            }

            // Get all supported palette export extensions
            var extensions = ExportRegistry.Instance.GetSupportedExtensions(ExportCategory.Palette);
            if (extensions.Count == 0)
            {
                await new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No export formats available",
                    Content = "No palette export handlers are registered.",
                    CloseButtonText = "OK"
                }.ShowAsync();
                return;
            }

            // Create dialog to pick format and enter name
            var contentStack = new StackPanel { Spacing = 12, MinWidth = 360 };

            var nameLabel = new TextBlock { Text = "Palette Name:" };
            var nameBox = new TextBox
            {
                PlaceholderText = "My Palette",
                Text = "My Palette"
            };

            var formatLabel = new TextBlock { Text = "Export Format:" };
            var formatCombo = new ComboBox { MinWidth = 200 };

            // Get all handlers with their display names
            var handlers = ExportRegistry.Instance.GetHandlersByCategory(ExportCategory.Palette);
            foreach (var handler in handlers)
            {
                formatCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{handler.Format.DisplayName} ({handler.Format.Extension})",
                    Tag = handler
                });
            }
            formatCombo.SelectedIndex = 0;

            // Preview
            var previewLabel = new TextBlock { Text = $"Colors to export: {_palette.Colors.Count}", Opacity = 0.7 };
            var preview = CreatePalettePreview(_palette.Colors.ToArray());

            contentStack.Children.Add(nameLabel);
            contentStack.Children.Add(nameBox);
            contentStack.Children.Add(formatLabel);
            contentStack.Children.Add(formatCombo);
            contentStack.Children.Add(previewLabel);
            contentStack.Children.Add(preview);

            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Export Palette",
                Content = contentStack,
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dlg.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            if (formatCombo.SelectedItem is not ComboBoxItem selectedItem ||
                selectedItem.Tag is not IExportRegistration selectedHandler)
            {
                return;
            }

            var paletteName = string.IsNullOrWhiteSpace(nameBox.Text) ? "My Palette" : nameBox.Text.Trim();

            // Show file save picker
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.SuggestedFileName = paletteName;
            savePicker.FileTypeChoices.Add(selectedHandler.Format.DisplayName, new[] { selectedHandler.Format.Extension });

            var saveFile = await savePicker.PickSaveFileAsync();
            if (saveFile is null) return;

            try
            {
                var exportContext = new FileExportContext(saveFile.Path);
                var exportData = new PaletteExportData(_palette.Colors.ToList(), paletteName);

                bool success = selectedHandler.Export(exportContext, exportData);

                if (success)
                {
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Export Successful",
                        Content = $"Palette exported to:\n{saveFile.Path}",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                }
                else
                {
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Export failed",
                        Content = "The export handler reported a failure.",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Export failed",
                    Content = $"Could not export palette.\n{ex.Message}",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }
    }
}
