using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.IO;
using PixlPunkt.Core.Plugins;

namespace PixlPunkt.UI
{
    /// <summary>
    /// Partial class for plugin import/export menu management.
    /// Builds dynamic menus for plugin-provided import/export handlers.
    /// </summary>
    public sealed partial class PixlPunktMainWindow : Window
    {
        // ═══════════════════════════════════════════════════════════════════════
        // MENU BUILDING
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds all plugin import/export menus.
        /// Call this after plugins are loaded and when plugins change.
        /// </summary>
        private void BuildPluginIOMenus()
        {
            BuildFileImportPluginMenu();
            BuildFileExportPluginMenu();
            BuildPaletteImportPluginMenu();
            BuildPaletteExportPluginMenu();
        }

        /// <summary>
        /// Builds the File > Import From > Custom (Plugins) menu.
        /// Shows document/layer import handlers from plugins.
        /// </summary>
        private void BuildFileImportPluginMenu()
        {
            if (File_ImportFrom_Custom_Submenu == null) return;

            File_ImportFrom_Custom_Submenu.Items.Clear();

            // Get plugin-provided document import handlers
            var docHandlers = ImportRegistry.Instance.GetPluginHandlers(ImportCategory.Document);
            var layerHandlers = ImportRegistry.Instance.GetPluginHandlers(ImportCategory.Layer);

            bool hasAny = docHandlers.Count > 0 || layerHandlers.Count > 0;
            File_ImportFrom_Custom_Submenu.IsEnabled = hasAny;

            if (!hasAny)
            {
                var emptyItem = new MenuFlyoutItem
                {
                    Text = "(No plugin formats)",
                    IsEnabled = false
                };
                File_ImportFrom_Custom_Submenu.Items.Add(emptyItem);
                return;
            }

            // Add document import handlers
            if (docHandlers.Count > 0)
            {
                var docHeader = new MenuFlyoutItem
                {
                    Text = "- Documents -",
                    IsEnabled = false
                };
                File_ImportFrom_Custom_Submenu.Items.Add(docHeader);

                foreach (var handler in docHandlers)
                {
                    var pluginId = PluginRegistry.Instance.GetPluginForImport(handler.Id);
                    var plugin = pluginId != null ? PluginRegistry.Instance.GetPlugin(pluginId) : null;
                    var pluginName = plugin?.Manifest.Name ?? "Plugin";

                    var item = new MenuFlyoutItem
                    {
                        Text = $"{handler.Format.DisplayName} ({handler.Format.Extension})",
                        Tag = handler
                    };
                    ToolTipService.SetToolTip(item, $"{handler.Format.Description}\nProvided by: {pluginName}");
                    item.Click += PluginDocumentImport_Click;
                    File_ImportFrom_Custom_Submenu.Items.Add(item);
                }
            }

            // Add layer import handlers
            if (layerHandlers.Count > 0)
            {
                if (docHandlers.Count > 0)
                    File_ImportFrom_Custom_Submenu.Items.Add(new MenuFlyoutSeparator());

                var layerHeader = new MenuFlyoutItem
                {
                    Text = "- Layers -",
                    IsEnabled = false
                };
                File_ImportFrom_Custom_Submenu.Items.Add(layerHeader);

                foreach (var handler in layerHandlers)
                {
                    var pluginId = PluginRegistry.Instance.GetPluginForImport(handler.Id);
                    var plugin = pluginId != null ? PluginRegistry.Instance.GetPlugin(pluginId) : null;
                    var pluginName = plugin?.Manifest.Name ?? "Plugin";

                    var item = new MenuFlyoutItem
                    {
                        Text = $"{handler.Format.DisplayName} ({handler.Format.Extension})",
                        Tag = handler
                    };
                    ToolTipService.SetToolTip(item, $"{handler.Format.Description}\nProvided by: {pluginName}");
                    item.Click += PluginLayerImport_Click;
                    File_ImportFrom_Custom_Submenu.Items.Add(item);
                }
            }
        }

        /// <summary>
        /// Builds the File > Export To > Custom (Plugins) menu.
        /// Shows document/layer export handlers from plugins.
        /// </summary>
        private void BuildFileExportPluginMenu()
        {
            if (File_ExportTo_Custom_Submenu == null) return;

            File_ExportTo_Custom_Submenu.Items.Clear();

            // Get plugin-provided document export handlers
            var docHandlers = ExportRegistry.Instance.GetPluginHandlers(ExportCategory.Document);
            var layerHandlers = ExportRegistry.Instance.GetPluginHandlers(ExportCategory.Layer);

            bool hasAny = docHandlers.Count > 0 || layerHandlers.Count > 0;
            File_ExportTo_Custom_Submenu.IsEnabled = hasAny;

            if (!hasAny)
            {
                var emptyItem = new MenuFlyoutItem
                {
                    Text = "(No plugin formats)",
                    IsEnabled = false
                };
                File_ExportTo_Custom_Submenu.Items.Add(emptyItem);
                return;
            }

            // Add document export handlers
            if (docHandlers.Count > 0)
            {
                var docHeader = new MenuFlyoutItem
                {
                    Text = "- Documents -",
                    IsEnabled = false
                };
                File_ExportTo_Custom_Submenu.Items.Add(docHeader);

                foreach (var handler in docHandlers)
                {
                    var pluginId = PluginRegistry.Instance.GetPluginForExport(handler.Id);
                    var plugin = pluginId != null ? PluginRegistry.Instance.GetPlugin(pluginId) : null;
                    var pluginName = plugin?.Manifest.Name ?? "Plugin";

                    var item = new MenuFlyoutItem
                    {
                        Text = $"{handler.Format.DisplayName} ({handler.Format.Extension})",
                        Tag = handler
                    };
                    ToolTipService.SetToolTip(item, $"{handler.Format.Description}\nProvided by: {pluginName}");
                    item.Click += PluginDocumentExport_Click;
                    File_ExportTo_Custom_Submenu.Items.Add(item);
                }
            }

            // Add layer export handlers
            if (layerHandlers.Count > 0)
            {
                if (docHandlers.Count > 0)
                    File_ExportTo_Custom_Submenu.Items.Add(new MenuFlyoutSeparator());

                var layerHeader = new MenuFlyoutItem
                {
                    Text = "- Layers -",
                    IsEnabled = false
                };
                File_ExportTo_Custom_Submenu.Items.Add(layerHeader);

                foreach (var handler in layerHandlers)
                {
                    var pluginId = PluginRegistry.Instance.GetPluginForExport(handler.Id);
                    var plugin = pluginId != null ? PluginRegistry.Instance.GetPlugin(pluginId) : null;
                    var pluginName = plugin?.Manifest.Name ?? "Plugin";

                    var item = new MenuFlyoutItem
                    {
                        Text = $"{handler.Format.DisplayName} ({handler.Format.Extension})",
                        Tag = handler
                    };
                    ToolTipService.SetToolTip(item, $"{handler.Format.Description}\nProvided by: {pluginName}");
                    item.Click += PluginLayerExport_Click;
                    File_ExportTo_Custom_Submenu.Items.Add(item);
                }
            }
        }

        /// <summary>
        /// Builds the Palette > Import Palette from > Custom (Plugins) menu.
        /// </summary>
        private void BuildPaletteImportPluginMenu()
        {
            if (Palette_ImportFrom_Custom_Submenu == null) return;

            Palette_ImportFrom_Custom_Submenu.Items.Clear();

            var handlers = ImportRegistry.Instance.GetPluginHandlers(ImportCategory.Palette);
            bool hasAny = handlers.Count > 0;
            Palette_ImportFrom_Custom_Submenu.IsEnabled = hasAny;

            if (!hasAny)
            {
                var emptyItem = new MenuFlyoutItem
                {
                    Text = "(No plugin formats)",
                    IsEnabled = false
                };
                Palette_ImportFrom_Custom_Submenu.Items.Add(emptyItem);
                return;
            }

            foreach (var handler in handlers)
            {
                var pluginId = PluginRegistry.Instance.GetPluginForImport(handler.Id);
                var plugin = pluginId != null ? PluginRegistry.Instance.GetPlugin(pluginId) : null;
                var pluginName = plugin?.Manifest.Name ?? "Plugin";

                var item = new MenuFlyoutItem
                {
                    Text = $"{handler.Format.DisplayName} ({handler.Format.Extension})",
                    Tag = handler
                };
                ToolTipService.SetToolTip(item, $"{handler.Format.Description}\nProvided by: {pluginName}");
                item.Click += PluginPaletteImport_Click;
                Palette_ImportFrom_Custom_Submenu.Items.Add(item);
            }
        }

        /// <summary>
        /// Builds the Palette > Export Palette to > Custom (Plugins) menu.
        /// </summary>
        private void BuildPaletteExportPluginMenu()
        {
            if (Palette_ExportTo_Custom_Submenu == null) return;

            Palette_ExportTo_Custom_Submenu.Items.Clear();

            var handlers = ExportRegistry.Instance.GetPluginHandlers(ExportCategory.Palette);
            bool hasAny = handlers.Count > 0;
            Palette_ExportTo_Custom_Submenu.IsEnabled = hasAny;

            if (!hasAny)
            {
                var emptyItem = new MenuFlyoutItem
                {
                    Text = "(No plugin formats)",
                    IsEnabled = false
                };
                Palette_ExportTo_Custom_Submenu.Items.Add(emptyItem);
                return;
            }

            foreach (var handler in handlers)
            {
                var pluginId = PluginRegistry.Instance.GetPluginForExport(handler.Id);
                var plugin = pluginId != null ? PluginRegistry.Instance.GetPlugin(pluginId) : null;
                var pluginName = plugin?.Manifest.Name ?? "Plugin";

                var item = new MenuFlyoutItem
                {
                    Text = $"{handler.Format.DisplayName} ({handler.Format.Extension})",
                    Tag = handler
                };
                ToolTipService.SetToolTip(item, $"{handler.Format.Description}\nProvided by: {pluginName}");
                item.Click += PluginPaletteExport_Click;
                Palette_ExportTo_Custom_Submenu.Items.Add(item);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PLUGIN IMPORT HANDLERS
        // ═══════════════════════════════════════════════════════════════════════

        private async void PluginDocumentImport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not IImportRegistration handler)
                return;

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(handler.Format.Extension);

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            try
            {
                var context = new FileImportContext(file.Path);
                var result = handler.Import(context);

                if (result is ImageImportResult imageResult)
                {
                    if (!string.IsNullOrEmpty(imageResult.ErrorMessage))
                    {
                        await new ContentDialog
                        {
                            XamlRoot = Content.XamlRoot,
                            Title = "Import failed",
                            Content = imageResult.ErrorMessage,
                            CloseButtonText = "OK"
                        }.ShowAsync();
                        return;
                    }

                    if (imageResult.Pixels == null || imageResult.Width <= 0 || imageResult.Height <= 0)
                    {
                        await new ContentDialog
                        {
                            XamlRoot = Content.XamlRoot,
                            Title = "Import failed",
                            Content = "No image data found in file.",
                            CloseButtonText = "OK"
                        }.ShowAsync();
                        return;
                    }

                    // Create document from imported image
                    var width = imageResult.Width;
                    var height = imageResult.Height;
                    var tileSize = new Windows.Graphics.SizeInt32(8, 8);
                    var tileCounts = new Windows.Graphics.SizeInt32(
                        Math.Max(1, width / 8),
                        Math.Max(1, height / 8));

                    var docName = imageResult.SuggestedName ?? System.IO.Path.GetFileNameWithoutExtension(file.Name);
                    var doc = new Core.Document.CanvasDocument(docName, width, height, tileSize, tileCounts);

                    // Copy pixels to the active layer (convert from uint[] to byte[])
                    if (doc.ActiveLayer != null)
                    {
                        var surface = doc.ActiveLayer.Surface;
                        var pixels = imageResult.Pixels;
                        var surfacePixels = surface.Pixels;

                        for (int i = 0; i < pixels.Length && i * 4 < surfacePixels.Length; i++)
                        {
                            uint p = pixels[i];
                            int idx = i * 4;
                            surfacePixels[idx + 0] = (byte)(p & 0xFF);         // B
                            surfacePixels[idx + 1] = (byte)((p >> 8) & 0xFF);  // G
                            surfacePixels[idx + 2] = (byte)((p >> 16) & 0xFF); // R
                            surfacePixels[idx + 3] = (byte)((p >> 24) & 0xFF); // A
                        }
                    }

                    _workspace.Add(doc);
                    _documentPaths[doc] = string.Empty;
                    _autoSave.RegisterDocument(doc);
                    var tab = MakeTab(doc);
                    DocsTab.TabItems.Add(tab);
                    DocsTab.SelectedItem = tab;
                }
                else
                {
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Import failed",
                        Content = "Plugin returned unexpected result type.",
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
                    Content = $"Could not import file.\n{ex.Message}",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        private async void PluginLayerImport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not IImportRegistration handler)
                return;

            if (CurrentHost?.Document == null)
            {
                await new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import failed",
                    Content = "No document is open. Please open or create a document first.",
                    CloseButtonText = "OK"
                }.ShowAsync();
                return;
            }

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(handler.Format.Extension);

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            try
            {
                var context = new FileImportContext(file.Path);
                var result = handler.Import(context);

                if (result is ImageImportResult imageResult)
                {
                    if (!string.IsNullOrEmpty(imageResult.ErrorMessage))
                    {
                        await new ContentDialog
                        {
                            XamlRoot = Content.XamlRoot,
                            Title = "Import failed",
                            Content = imageResult.ErrorMessage,
                            CloseButtonText = "OK"
                        }.ShowAsync();
                        return;
                    }

                    // TODO: Add layer to document with imported pixels
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Import successful",
                        Content = $"Imported {imageResult.Width}x{imageResult.Height} image.\n(Layer creation not yet implemented)",
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
                    Content = $"Could not import file.\n{ex.Message}",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        private async void PluginPaletteImport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not IImportRegistration handler)
                return;

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(handler.Format.Extension);

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            try
            {
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
                            Content = "No colors found in file.",
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
                        Content = "Plugin returned unexpected result type.",
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

        // ═══════════════════════════════════════════════════════════════════════
        // PLUGIN EXPORT HANDLERS
        // ═══════════════════════════════════════════════════════════════════════

        private async void PluginDocumentExport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not IExportRegistration handler)
                return;

            var doc = CurrentHost?.Document;
            if (doc == null)
            {
                await new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Export failed",
                    Content = "No document is open.",
                    CloseButtonText = "OK"
                }.ShowAsync();
                return;
            }

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.SuggestedFileName = doc.Name ?? "Untitled";
            savePicker.FileTypeChoices.Add(handler.Format.DisplayName, new[] { handler.Format.Extension });

            var saveFile = await savePicker.PickSaveFileAsync();
            if (saveFile is null) return;

            try
            {
                // Composite the document first
                doc.CompositeTo(doc.Surface);

                var width = doc.PixelWidth;
                var height = doc.PixelHeight;

                // Convert byte[] to uint[]
                var pixels = ConvertBytesToUints(doc.Surface.Pixels, width, height);

                var exportContext = new FileExportContext(saveFile.Path);
                var exportData = new ImageExportData(pixels, width, height, doc.Name ?? "Untitled");

                bool success = handler.Export(exportContext, exportData);

                if (success)
                {
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Export Successful",
                        Content = $"Document exported to:\n{saveFile.Path}",
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
                    Content = $"Could not export document.\n{ex.Message}",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        private async void PluginLayerExport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not IExportRegistration handler)
                return;

            var layer = CurrentHost?.Document?.ActiveLayer;
            if (layer == null)
            {
                await new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Export failed",
                    Content = "No active layer.",
                    CloseButtonText = "OK"
                }.ShowAsync();
                return;
            }

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.SuggestedFileName = layer.Name ?? "Layer";
            savePicker.FileTypeChoices.Add(handler.Format.DisplayName, new[] { handler.Format.Extension });

            var saveFile = await savePicker.PickSaveFileAsync();
            if (saveFile is null) return;

            try
            {
                var surface = layer.Surface;

                // Convert byte[] to uint[]
                var pixels = ConvertBytesToUints(surface.Pixels, surface.Width, surface.Height);

                var exportContext = new FileExportContext(saveFile.Path);
                var exportData = new ImageExportData(pixels, surface.Width, surface.Height, layer.Name ?? "Layer");

                bool success = handler.Export(exportContext, exportData);

                if (success)
                {
                    await new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Export Successful",
                        Content = $"Layer exported to:\n{saveFile.Path}",
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
                    Content = $"Could not export layer.\n{ex.Message}",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        private async void PluginPaletteExport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not IExportRegistration handler)
                return;

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

            // Get palette name from user
            var nameBox = new TextBox
            {
                PlaceholderText = "My Palette",
                Text = "My Palette"
            };

            var nameDlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Export Palette",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Palette Name:" },
                        nameBox,
                        new TextBlock { Text = $"Colors: {_palette.Colors.Count}", Opacity = 0.7 }
                    }
                },
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel"
            };

            if (await nameDlg.ShowAsync() != ContentDialogResult.Primary)
                return;

            var paletteName = string.IsNullOrWhiteSpace(nameBox.Text) ? "My Palette" : nameBox.Text.Trim();

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.SuggestedFileName = paletteName;
            savePicker.FileTypeChoices.Add(handler.Format.DisplayName, new[] { handler.Format.Extension });

            var saveFile = await savePicker.PickSaveFileAsync();
            if (saveFile is null) return;

            try
            {
                var exportContext = new FileExportContext(saveFile.Path);
                var exportData = new PaletteExportData(_palette.Colors.ToList(), paletteName);

                bool success = handler.Export(exportContext, exportData);

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

        // ═══════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Converts a byte[] pixel buffer (BGRA) to uint[] format.
        /// </summary>
        private static uint[] ConvertBytesToUints(byte[] bytes, int width, int height)
        {
            int pixelCount = width * height;
            var result = new uint[pixelCount];

            for (int i = 0; i < pixelCount && i * 4 + 3 < bytes.Length; i++)
            {
                int idx = i * 4;
                byte b = bytes[idx + 0];
                byte g = bytes[idx + 1];
                byte r = bytes[idx + 2];
                byte a = bytes[idx + 3];

                // Pack as ARGB uint
                result[i] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
            }

            return result;
        }
    }
}
