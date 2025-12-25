using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document;
using PixlPunkt.UI.CanvasHost;
using PixlPunkt.UI.Dialogs.Import;
using PixlPunkt.UI.Helpers;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Provider;


namespace PixlPunkt.UI
{
    public sealed partial class PixlPunktMainWindow : Window
    {
        // ------------- Import Image ------------

        /// <summary>
        /// Imports an image file (BMP, PNG, JPEG, TIFF) as a new document.
        /// Shows a dialog allowing the user to configure tile dimensions.
        /// </summary>
        private async void File_Import_Image(object sender, RoutedEventArgs e)
        {
            var openPicker = WindowHost.CreateFileOpenPicker(this, [".png", ".bmp", ".jpg", ".jpeg", ".tif", ".tiff"]);
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is null)
                return;

            try
            {
                // Load the image using Windows.Graphics.Imaging
                using var stream = await file.OpenReadAsync();
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);

                int width = (int)decoder.PixelWidth;
                int height = (int)decoder.PixelHeight;

                if (width <= 0 || height <= 0)
                {
                    await ShowImportErrorAsync("Invalid image dimensions.");
                    return;
                }

                // Get pixel data
                var pixelData = await decoder.GetPixelDataAsync(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    new Windows.Graphics.Imaging.BitmapTransform(),
                    Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation,
                    Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);

                byte[] pixels = pixelData.DetachPixelData();

                // Show import dialog for tile configuration
                var dialog = new ImageImportDialog(file.Name, width, height, pixels)
                {
                    XamlRoot = Content.XamlRoot
                };

                // Load preview
                await dialog.LoadPreviewAsync();

                var result = await ShowDialogGuardedAsync(dialog);
                if (result != ContentDialogResult.Primary)
                    return;

                // Create document with user-specified tile configuration
                var docName = Path.GetFileNameWithoutExtension(file.Name);
                var tileSize = dialog.TileSize;
                var tileCounts = dialog.TileCounts;
                int canvasWidth = dialog.CanvasWidth;
                int canvasHeight = dialog.CanvasHeight;

                var doc = new CanvasDocument(docName, canvasWidth, canvasHeight, tileSize, tileCounts);

                // Copy pixels to the active layer (image placed at top-left)
                if (doc.ActiveLayer != null)
                {
                    var surface = doc.ActiveLayer.Surface;
                    var surfacePixels = surface.Pixels;

                    // Copy row by row since canvas may be larger than image
                    int srcStride = width * 4;
                    int dstStride = canvasWidth * 4;
                    int copyWidth = Math.Min(width, canvasWidth);
                    int copyHeight = Math.Min(height, canvasHeight);
                    int rowBytes = copyWidth * 4;

                    for (int y = 0; y < copyHeight; y++)
                    {
                        int srcOffset = y * srcStride;
                        int dstOffset = y * dstStride;
                        Array.Copy(pixels, srcOffset, surfacePixels, dstOffset, rowBytes);
                    }

                    doc.ActiveLayer.UpdatePreview();
                }

                // Recomposite
                doc.CompositeTo(doc.Surface);

                // Register document & open it in a new tab
                _workspace.Add(doc);
                _documentPaths[doc] = string.Empty; // no path yet (imported, not saved)
                _autoSave.RegisterDocument(doc);
                var tab = MakeTab(doc);
                DocsTab.TabItems.Add(tab);
                // Defer selection to ensure tab is fully in visual tree (fixes Release build timing issue)
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    DocsTab.SelectedItem = tab;
                });

                // Update session state
                UpdateSessionState();
            }
            catch (Exception ex)
            {
                await ShowImportErrorAsync($"Could not import image.\n{ex.Message}");
            }
        }

        private async Task ShowImportErrorAsync(string message)
        {
            await new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Import failed",
                Content = message,
                CloseButtonText = "OK"
            }.ShowAsync();
        }

        // ------------- Open -------------

        private async void File_Import_PyxelEdit(object sender, RoutedEventArgs e)
        {
            var openPicker = WindowHost.CreateFileOpenPicker(this, ".pyxel");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is null)
                return;
            CanvasDocument doc;
            doc = ForeignDocumentImporter.ImportPyxel(file.Path);
            // Register document & open it in a new tab
            _workspace.Add(doc);
            _documentPaths[doc] = string.Empty; // no path yet
            _autoSave.RegisterDocument(doc);
            var tab = MakeTab(doc);
            DocsTab.TabItems.Add(tab);
            // Defer selection to ensure tab is fully in visual tree (fixes Release build timing issue)
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                DocsTab.SelectedItem = tab;
            });
        }

        private async void File_Import_Icon(object sender, RoutedEventArgs e)
        {
            var openPicker = WindowHost.CreateFileOpenPicker(this, ".ico");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is null)
                return;
            CanvasDocument doc;
            doc = ForeignDocumentImporter.ImportIconAsDocument(file.Path);
            // Register document & open it in a new tab
            _workspace.Add(doc);
            _documentPaths[doc] = string.Empty; // no path yet
            _autoSave.RegisterDocument(doc);
            var tab = MakeTab(doc);
            DocsTab.TabItems.Add(tab);
            // Defer selection to ensure tab is fully in visual tree (fixes Release build timing issue)
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                DocsTab.SelectedItem = tab;
            });
        }

        private async void File_Import_Cursor(object sender, RoutedEventArgs e)
        {
            var openPicker = WindowHost.CreateFileOpenPicker(this, ".cur");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is null)
                return;
            CanvasDocument doc;
            doc = ForeignDocumentImporter.ImportCursorAsDocument(file.Path);
            // Register document & open it in a new tab
            _workspace.Add(doc);
            _documentPaths[doc] = string.Empty; // no path yet
            _autoSave.RegisterDocument(doc);
            var tab = MakeTab(doc);
            DocsTab.TabItems.Add(tab);
            // Defer selection to ensure tab is fully in visual tree (fixes Release build timing issue)
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                DocsTab.SelectedItem = tab;
            });
        }

        private async void File_Import_Aseprite(object sender, RoutedEventArgs e)
        {
            var openPicker = WindowHost.CreateFileOpenPicker(this, [".ase", ".aseprite"]);
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is null)
                return;

            try
            {
                var doc = ForeignDocumentImporter.ImportAseprite(file.Path);

                // Register document & open it in a new tab
                _workspace.Add(doc);
                _documentPaths[doc] = string.Empty; // no path yet
                _autoSave.RegisterDocument(doc);
                var tab = MakeTab(doc);
                DocsTab.TabItems.Add(tab);
                // Defer selection to ensure tab is fully in visual tree (fixes Release build timing issue)
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    DocsTab.SelectedItem = tab;
                });
            }
            catch (Exception ex)
            {
                await ShowImportErrorAsync($"Could not import Aseprite file.\n{ex.Message}");
            }
        }

        private async void File_Import_Tmx(object sender, RoutedEventArgs e)
        {
            var openPicker = WindowHost.CreateFileOpenPicker(this, ".tmx");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is null)
                return;

            try
            {
                var doc = ForeignDocumentImporter.ImportTmx(file.Path);

                // Register document & open it in a new tab
                _workspace.Add(doc);
                _documentPaths[doc] = string.Empty; // no path yet
                _autoSave.RegisterDocument(doc);
                var tab = MakeTab(doc);
                DocsTab.TabItems.Add(tab);
                // Defer selection to ensure tab is fully in visual tree (fixes Release build timing issue)
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    DocsTab.SelectedItem = tab;
                });
            }
            catch (Exception ex)
            {
                await ShowImportErrorAsync($"Could not import TMX tilemap.\n{ex.Message}");
            }
        }

        private async void File_Import_Tsx(object sender, RoutedEventArgs e)
        {
            var openPicker = WindowHost.CreateFileOpenPicker(this, ".tsx");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is null)
                return;

            try
            {
                var doc = ForeignDocumentImporter.ImportTsx(file.Path);

                // Register document & open it in a new tab
                _workspace.Add(doc);
                _documentPaths[doc] = string.Empty; // no path yet
                _autoSave.RegisterDocument(doc);
                var tab = MakeTab(doc);
                DocsTab.TabItems.Add(tab);
                // Defer selection to ensure tab is fully in visual tree (fixes Release build timing issue)
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    DocsTab.SelectedItem = tab;
                });
            }
            catch (Exception ex)
            {
                await ShowImportErrorAsync($"Could not import TSX tileset.\n{ex.Message}");
            }
        }



        // ------------- Save -------------


        private async Task SaveDocumentAsAsync()
        {

            var host = CurrentHost;
            if (host == null) return;
            var doc = host.Document;

            var savePicker = WindowHost.CreateFileSavePicker(this, doc.Name ?? "Untitled", ".pxp");

            StorageFile? file = await savePicker.PickSaveFileAsync();
            if (file is null)
                return;

            CachedFileManager.DeferUpdates(file);

            using (var stream = await file.OpenStreamForWriteAsync())
            {
                stream.SetLength(0); // overwrite existing
                DocumentIO.Save(doc, stream);
                await stream.FlushAsync(); // Ensure stream is flushed before completing updates
            }

            var status = await CachedFileManager.CompleteUpdatesAsync(file);
            if (status == FileUpdateStatus.Complete)
            {
                _documentPaths[doc] = file.Path;
                doc.MarkSaved();
                TrackRecent(file.Path);
                // Update document name from filename (without extension)
                doc.Name = Path.GetFileNameWithoutExtension(file.Path);
                UpdateTabHeaderForDocument(doc);
            }
        }

        // Update tab header after a Save As (if you want to show the doc name)
        private void UpdateTabHeaderForDocument(CanvasDocument doc)
        {
            foreach (TabViewItem tab in DocsTab.TabItems.Cast<TabViewItem>())
            {
                if (tab.Content is CanvasViewHost host &&
                    ReferenceEquals(host.Document, doc))
                {
                    tab.Header = MakeTabHeader(doc, tab);
                    break;
                }
            }
        }
    }
}
