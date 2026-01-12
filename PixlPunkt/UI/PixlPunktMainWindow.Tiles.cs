using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Tile;
using PixlPunkt.UI.Dialogs;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PixlPunkt.UI
{
    /// <summary>
    /// Partial class for tile management operations:
    /// - Export tiles (.pxpt)
    /// - Import tiles (from .pxpt, .pxp, or open document)
    /// - Merge duplicates
    /// - Remove unused
    /// </summary>
    public sealed partial class PixlPunktMainWindow : Window
    {
        //////////////////////////////////////////////////////////////////
        // TILE IMPORT FROM OPEN DOCUMENT
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Imports tiles from another open document.
        /// </summary>
        private async void Tiles_ImportFromDocument_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Document",
                    Content = "Open a document before importing tiles.",
                    CloseButtonText = "OK"
                });
                return;
            }

            if (doc.TileSet == null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Tile Set",
                    Content = "The current document does not have a tile set.",
                    CloseButtonText = "OK"
                });
                return;
            }

            // Check if there are other documents with tiles to import from
            var allDocs = _workspace.Documents;
            var sourceDocs = allDocs.Where(d => d != doc && d.TileSet != null && d.TileSet.Count > 0).ToList();
            if (sourceDocs.Count == 0)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Source Documents",
                    Content = "There are no other open documents with tiles to import from.",
                    CloseButtonText = "OK"
                });
                return;
            }

            // Show import dialog
            var dialog = new ImportTilesDialog(doc, allDocs)
            {
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            // Import selected tiles
            var selectedTiles = dialog.GetSelectedTiles();
            if (selectedTiles.Count == 0)
                return;

            int importedCount = 0;
            int skippedCount = 0;

            var targetTileSet = doc.TileSet;
            var sourceTileSet = dialog.SourceTileSet;
            bool needsScale = sourceTileSet != null &&
                              (sourceTileSet.TileWidth != targetTileSet.TileWidth ||
                               sourceTileSet.TileHeight != targetTileSet.TileHeight);

            foreach (var tile in selectedTiles)
            {
                byte[] pixelsToImport = tile.Pixels;

                // Scale if needed
                if (needsScale)
                {
                    pixelsToImport = ScaleTilePixels(
                        tile.Pixels, tile.Width, tile.Height,
                        targetTileSet.TileWidth, targetTileSet.TileHeight);
                }

                // Check for duplicates if requested
                if (dialog.SkipDuplicates && IsDuplicateTile(pixelsToImport, targetTileSet))
                {
                    skippedCount++;
                    continue;
                }

                // Add the tile
                targetTileSet.AddTile(pixelsToImport);
                importedCount++;
            }

            // Show summary
            string message = $"Imported {importedCount} tile(s).";
            if (skippedCount > 0)
            {
                message += $"\nSkipped {skippedCount} duplicate tile(s).";
            }

            await ShowDialogGuardedAsync(new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Import Complete",
                Content = message,
                CloseButtonText = "OK"
            });

            // Refresh tile panel
            TilePanel?.RefreshTiles();
            CurrentHost?.InvalidateCanvas();
        }

        /// <summary>
        /// Scales tile pixels to a new size using nearest-neighbor interpolation.
        /// </summary>
        private static byte[] ScaleTilePixels(byte[] source, int srcW, int srcH, int dstW, int dstH)
        {
            var result = new byte[dstW * dstH * 4];

            for (int y = 0; y < dstH; y++)
            {
                int srcY = y * srcH / dstH;
                for (int x = 0; x < dstW; x++)
                {
                    int srcX = x * srcW / dstW;

                    int srcIdx = (srcY * srcW + srcX) * 4;
                    int dstIdx = (y * dstW + x) * 4;

                    result[dstIdx] = source[srcIdx];
                    result[dstIdx + 1] = source[srcIdx + 1];
                    result[dstIdx + 2] = source[srcIdx + 2];
                    result[dstIdx + 3] = source[srcIdx + 3];
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a tile with identical pixels already exists in the tile set.
        /// </summary>
        private static bool IsDuplicateTile(byte[] pixels, TileSet tileSet)
        {
            foreach (var existingTile in tileSet.Tiles)
            {
                if (existingTile.Pixels.Length != pixels.Length)
                    continue;

                bool match = true;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (existingTile.Pixels[i] != pixels[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return true;
            }

            return false;
        }

        //////////////////////////////////////////////////////////////////
        // TILE EXPORT
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Exports tiles and mappings to a .pxpt file.
        /// </summary>
        private async void Tiles_Export_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Document",
                    Content = "Open a document before exporting tiles.",
                    CloseButtonText = "OK"
                });
                return;
            }

            var tileSet = doc.TileSet;
            if (tileSet == null || tileSet.Count == 0)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Tiles",
                    Content = "The document has no tiles to export.",
                    CloseButtonText = "OK"
                });
                return;
            }

            // Gather layer mappings
            var layerMappings = new List<(string layerName, TileMapping? mapping)>();
            foreach (var layer in doc.Layers)
            {
                layerMappings.Add((layer.Name, layer.TileMapping));
            }

            // Create export data
            var exportData = TileIO.CreateExportData(tileSet, layerMappings);

            // Show save picker
            var savePicker = new FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.SuggestedFileName = $"{doc.Name ?? "tiles"}_tiles";
            savePicker.FileTypeChoices.Add(TileIO.FileTypeDisplayName, new[] { TileIO.FileExtension });
            savePicker.DefaultFileExtension = TileIO.FileExtension;

            var file = await savePicker.PickSaveFileAsync();
            if (file is null) return;

            try
            {
                // Save to temp file first, then copy
                var tempPath = Path.GetTempFileName();
                try
                {
                    TileIO.Save(exportData, tempPath);
                    var bytes = File.ReadAllBytes(tempPath);
                    await FileIO.WriteBytesAsync(file, bytes);
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Export Complete",
                    Content = $"Exported {exportData.Tiles.Count} tile(s) and {exportData.LayerMappings.Count} layer mapping(s).",
                    CloseButtonText = "OK"
                });
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Export Failed",
                    Content = $"Could not export tiles: {ex.Message}",
                    CloseButtonText = "OK"
                });
            }
        }

        //////////////////////////////////////////////////////////////////
        // TILE IMPORT FROM .PXPT
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Imports tiles from a .pxpt file.
        /// </summary>
        private async void Tiles_ImportFromPxpt_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Document",
                    Content = "Open a document before importing tiles.",
                    CloseButtonText = "OK"
                });
                return;
            }

            // Show file picker
            var openPicker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(TileIO.FileExtension);

            var file = await openPicker.PickSingleFileAsync();
            if (file is null) return;

            try
            {
                // Load tile data
                var bytes = await FileIO.ReadBufferAsync(file);
                using var stream = bytes.AsStream();
                var importData = TileIO.Load(stream);

                // Validate dimensions
                if (importData.TileWidth != doc.TileSet.TileWidth ||
                    importData.TileHeight != doc.TileSet.TileHeight)
                {
                    await ShowDialogGuardedAsync(new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Dimension Mismatch",
                        Content = $"The imported tiles are {importData.TileWidth}×{importData.TileHeight} pixels, " +
                                  $"but this document uses {doc.TileSet.TileWidth}×{doc.TileSet.TileHeight} pixel tiles.\n\n" +
                                  "Tile dimensions must match.",
                        CloseButtonText = "OK"
                    });
                    return;
                }

                // Show import mode dialog
                await ShowTileImportDialog(doc, importData);
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import Failed",
                    Content = $"Could not import tiles: {ex.Message}",
                    CloseButtonText = "OK"
                });
            }
        }

        //////////////////////////////////////////////////////////////////
        // TILE IMPORT FROM .PXP
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Imports tiles from a .pxp project file.
        /// </summary>
        private async void Tiles_ImportFromPxp_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Document",
                    Content = "Open a document before importing tiles.",
                    CloseButtonText = "OK"
                });
                return;
            }

            // Show file picker
            var openPicker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".pxp");

            var file = await openPicker.PickSingleFileAsync();
            if (file is null) return;

            try
            {
                // Load the source document
                var sourceDoc = Core.Document.DocumentIO.Load(file.Path);

                // Create export data from the source document's tiles
                var layerMappings = new List<(string layerName, TileMapping? mapping)>();
                foreach (var layer in sourceDoc.Layers)
                {
                    layerMappings.Add((layer.Name, layer.TileMapping));
                }

                var importData = TileIO.CreateExportData(sourceDoc.TileSet, layerMappings);

                if (importData.Tiles.Count == 0)
                {
                    await ShowDialogGuardedAsync(new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "No Tiles",
                        Content = "The selected project has no tiles to import.",
                        CloseButtonText = "OK"
                    });
                    return;
                }

                // Validate dimensions
                if (importData.TileWidth != doc.TileSet.TileWidth ||
                    importData.TileHeight != doc.TileSet.TileHeight)
                {
                    await ShowDialogGuardedAsync(new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Dimension Mismatch",
                        Content = $"The imported tiles are {importData.TileWidth}×{importData.TileHeight} pixels, " +
                                  $"but this document uses {doc.TileSet.TileWidth}×{doc.TileSet.TileHeight} pixel tiles.\n\n" +
                                  "Tile dimensions must match.",
                        CloseButtonText = "OK"
                    });
                    return;
                }

                // Show import mode dialog
                await ShowTileImportDialog(doc, importData);
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import Failed",
                    Content = $"Could not import tiles from project: {ex.Message}",
                    CloseButtonText = "OK"
                });
            }
        }

        //////////////////////////////////////////////////////////////////
        // TILE IMPORT DIALOG
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Shows the tile import dialog and handles Add/Replace modes.
        /// </summary>
        private async Task ShowTileImportDialog(Core.Document.CanvasDocument doc, TileIO.TileExportData importData)
        {
            var contentPanel = new StackPanel { Spacing = 8 };
            contentPanel.Children.Add(new TextBlock
            {
                Text = $"Found {importData.Tiles.Count} tile(s) and {importData.LayerMappings.Count} layer mapping(s).",
                TextWrapping = TextWrapping.Wrap
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = "Choose an import mode:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 0)
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = "• Add: Adds imported tiles to the end of the tile list with new IDs.",
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = "• Replace: Removes all existing tiles and mappings, then imports the new tiles with their original IDs.",
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap
            });

            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Import Tiles",
                Content = contentPanel,
                PrimaryButtonText = "Add",
                SecondaryButtonText = "Replace",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await ShowDialogGuardedAsync(dlg);

            if (result == ContentDialogResult.Primary)
            {
                // Add mode
                await ImportTilesAddMode(doc, importData);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // Replace mode - show warning first
                var warningDlg = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Replace Tiles",
                    Content = "This will remove ALL existing tiles and tile mappings from all layers.\n\n" +
                              "The painted pixel data will remain, but tile references will be lost.\n\n" +
                              "Are you sure you want to continue?",
                    PrimaryButtonText = "Replace",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close
                };

                var warningResult = await ShowDialogGuardedAsync(warningDlg);
                if (warningResult == ContentDialogResult.Primary)
                {
                    await ImportTilesReplaceMode(doc, importData);
                }
            }
        }

        /// <summary>
        /// Imports tiles in Add mode - adds tiles with new IDs.
        /// </summary>
        private async Task ImportTilesAddMode(Core.Document.CanvasDocument doc, TileIO.TileExportData importData)
        {
            try
            {
                var idMapping = TileIO.AddTilesToSet(importData, doc.TileSet);

                int tilesAdded = idMapping.Count;

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import Complete",
                    Content = $"Added {tilesAdded} tile(s) to the tile set.\n\n" +
                              "Note: Layer mappings were not imported in Add mode. " +
                              "Tiles were added with new IDs starting after existing tiles.",
                    CloseButtonText = "OK"
                });

                // Refresh tile panel
                TilePanel?.RefreshTiles();
                CurrentHost?.InvalidateCanvas();
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import Failed",
                    Content = $"Could not add tiles: {ex.Message}",
                    CloseButtonText = "OK"
                });
            }
        }

        /// <summary>
        /// Imports tiles in Replace mode - clears and replaces all tiles and mappings.
        /// </summary>
        private async Task ImportTilesReplaceMode(Core.Document.CanvasDocument doc, TileIO.TileExportData importData)
        {
            try
            {
                // Clear all layer mappings first
                foreach (var layer in doc.Layers)
                {
                    layer.TileMapping?.Clear();
                }

                // Replace tile set
                TileIO.ReplaceTileSet(importData, doc.TileSet);

                // Apply layer mappings from import data
                foreach (var mappingData in importData.LayerMappings)
                {
                    // Find matching layer by name
                    var layer = doc.Layers.FirstOrDefault(l =>
                        l.Name.Equals(mappingData.LayerName, StringComparison.OrdinalIgnoreCase));

                    if (layer != null)
                    {
                        // Ensure layer has a mapping of the correct size
                        if (layer.TileMapping == null ||
                            layer.TileMapping.Width != mappingData.Width ||
                            layer.TileMapping.Height != mappingData.Height)
                        {
                            layer.TileMapping = new TileMapping(mappingData.Width, mappingData.Height);
                        }

                        // Apply the mapping entries
                        foreach (var (x, y, tileId) in mappingData.Entries)
                        {
                            layer.TileMapping.SetTileId(x, y, tileId);
                        }
                    }
                }

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import Complete",
                    Content = $"Replaced tiles with {importData.Tiles.Count} tile(s) and " +
                              $"applied {importData.LayerMappings.Count} layer mapping(s).",
                    CloseButtonText = "OK"
                });

                // Refresh UI
                TilePanel?.RefreshTiles();
                CurrentHost?.InvalidateCanvas();
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Import Failed",
                    Content = $"Could not replace tiles: {ex.Message}",
                    CloseButtonText = "OK"
                });
            }
        }

        //////////////////////////////////////////////////////////////////
        // TILE UTILITIES
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Merges duplicate tiles in the document.
        /// </summary>
        private async void Tiles_MergeDuplicates_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null || doc.TileSet.Count == 0)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Tiles",
                    Content = "No tiles to merge.",
                    CloseButtonText = "OK"
                });
                return;
            }

            var duplicateGroups = doc.TileSet.FindDuplicates().ToList();
            if (duplicateGroups.Count == 0)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Duplicates",
                    Content = "No duplicate tiles found.",
                    CloseButtonText = "OK"
                });
                return;
            }

            int totalDuplicates = duplicateGroups.Sum(g => g.Count() - 1);
            var confirmDlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Merge Duplicates",
                Content = $"Found {totalDuplicates} duplicate tile(s) in {duplicateGroups.Count} group(s).\n\n" +
                          "Duplicate tiles will be removed and their mappings updated to use the first tile in each group.\n\n" +
                          "Continue?",
                PrimaryButtonText = "Merge",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await ShowDialogGuardedAsync(confirmDlg);
            if (result != ContentDialogResult.Primary) return;

            int merged = 0;
            foreach (var group in duplicateGroups)
            {
                var ids = group.ToList();
                int keepId = ids[0];

                for (int i = 1; i < ids.Count; i++)
                {
                    int removeId = ids[i];

                    // Update all layer mappings
                    foreach (var layer in doc.Layers)
                    {
                        layer.TileMapping?.ReplaceTileId(removeId, keepId);
                    }

                    // Remove the duplicate tile
                    doc.TileSet.RemoveTile(removeId);
                    merged++;
                }
            }

            await ShowDialogGuardedAsync(new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Merge Complete",
                Content = $"Merged {merged} duplicate tile(s).",
                CloseButtonText = "OK"
            });

            TilePanel?.RefreshTiles();
            CurrentHost?.InvalidateCanvas();
        }

        /// <summary>
        /// Removes tiles that are not used in any layer mapping.
        /// </summary>
        private async void Tiles_RemoveUnused_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null || doc.TileSet.Count == 0)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Tiles",
                    Content = "No tiles to check.",
                    CloseButtonText = "OK"
                });
                return;
            }

            // Collect all used tile IDs
            var usedIds = new HashSet<int>();
            foreach (var layer in doc.Layers)
            {
                if (layer.TileMapping != null)
                {
                    foreach (var id in layer.TileMapping.GetUsedTileIds())
                    {
                        usedIds.Add(id);
                    }
                }
            }

            // Find unused tiles
            int totalTiles = doc.TileSet.Count;
            int unusedCount = totalTiles - usedIds.Count;

            if (unusedCount == 0)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "No Unused Tiles",
                    Content = "All tiles are being used in layer mappings.",
                    CloseButtonText = "OK"
                });
                return;
            }

            var confirmDlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Remove Unused Tiles",
                Content = $"Found {unusedCount} unused tile(s) out of {totalTiles} total.\n\n" +
                          "These tiles are not referenced in any layer mapping.\n\n" +
                          "Remove them?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await ShowDialogGuardedAsync(confirmDlg);
            if (result != ContentDialogResult.Primary) return;

            int removed = doc.TileSet.RemoveUnusedTiles(usedIds);

            await ShowDialogGuardedAsync(new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Removal Complete",
                Content = $"Removed {removed} unused tile(s).",
                CloseButtonText = "OK"
            });

            TilePanel?.RefreshTiles();
            CurrentHost?.InvalidateCanvas();
        }
    }
}
