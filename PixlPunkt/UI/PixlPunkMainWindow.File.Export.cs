using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Constants;
using PixlPunkt.Core.Brush;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Export;
using PixlPunkt.Core.FIleOps;
using PixlPunkt.Core.Imaging;
using PixlPunkt.UI.Helpers;
using PixlPunkt.UI.Rendering;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace PixlPunkt.UI
{
    public sealed partial class PixlPunktMainWindow : Window
    {
        private static readonly int[] IconSizes = [256, 128, 64, 48, 32, 16];
        private static readonly int[] CursorSizes = [128, 64, 48, 32];

        // ═══════════════════════════════════════════════════════════════
        // TIMELAPSE EXPORT
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Menu handler: Export Timelapse from History
        /// </summary>
        private async void File_Export_Timelapse_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "No document",
                    Content = "Open a document before exporting.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // Check if there's enough history to export
            int historyCount = doc.History.TotalCount;
            if (historyCount < 2)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Insufficient history",
                    Content = "Not enough history steps to create a timelapse.\n\n" +
                              "Draw on your canvas to build up history, then try again.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // Show timelapse export dialog
            var dialog = new PixlPunkt.UI.Dialogs.Export.TimelapseExportDialog(doc)
            {
                XamlRoot = Content.XamlRoot
            };

            var result = await ShowDialogGuardedAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            try
            {
                var exportService = new TimelapseExportService();
                var settings = dialog.BuildSettings();
                string format = dialog.SelectedFormat;

                // Render timelapse frames
                var frames = await exportService.RenderTimelapseAsync(doc, settings);

                if (frames.Count == 0)
                {
                    await ShowDialogGuardedAsync(new ContentDialog
                    {
                        Title = "No frames",
                        Content = "No frames were generated. Try adjusting the range or settings.",
                        CloseButtonText = DialogMessages.ButtonOK,
                        XamlRoot = Content.XamlRoot
                    });
                    return;
                }

                // Convert to AnimationExportService.RenderedFrame for reuse of export methods
                var animFrames = new List<AnimationExportService.RenderedFrame>();
                foreach (var frame in frames)
                {
                    animFrames.Add(new AnimationExportService.RenderedFrame
                    {
                        Pixels = frame.Pixels,
                        Width = frame.Width,
                        Height = frame.Height,
                        DurationMs = frame.DurationMs,
                        Index = frame.Index
                    });
                }

                // Export based on format
                string baseName = $"{doc.Name ?? "timelapse"}_timelapse";
                switch (format)
                {
                    case "gif":
                        await ExportAsGifAsync(animFrames, baseName, loop: true);
                        break;

                    case "mp4":
                        // Calculate effective FPS from first frame duration
                        int effectiveFps = frames.Count > 0 && frames[0].DurationMs > 0
                            ? Math.Max(1, 1000 / frames[0].DurationMs)
                            : 12;
                        await ExportAsVideoAsync(animFrames, baseName, "mp4", 80, effectiveFps);
                        break;

                    case "png":
                        await ExportAsImageSequenceAsync(animFrames, baseName, "png");
                        break;
                }

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export complete",
                    Content = $"Timelapse exported successfully!\n\n" +
                              $"{frames.Count} frames from {settings.RangeEnd - settings.RangeStart} history steps.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
            catch (OperationCanceledException)
            {
                // User cancelled, no message needed
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export failed",
                    Content = $"Could not export timelapse: {ex.Message}",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ANIMATION EXPORT
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Menu handler: Export Animation (GIF, Video, Image Sequence)
        /// </summary>
        private async void File_Export_Animation_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "No document",
                    Content = "Open a document before exporting.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // Check if there's any animation to export
            bool hasTileAnim = doc.TileAnimationState.SelectedReel?.FrameCount > 0;
            bool hasCanvasAnim = doc.CanvasAnimationState.FrameCount > 0;

            if (!hasTileAnim && !hasCanvasAnim)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "No animation",
                    Content = "No animation frames found. Create a tile animation reel or canvas animation first.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // Get the current animation mode from the animation panel
            var currentMode = AnimationPanel.CurrentMode;

            // Show animation export dialog with preferred mode
            var dialog = new PixlPunkt.UI.Dialogs.Export.AnimationExportDialog(doc, currentMode)
            {
                XamlRoot = Content.XamlRoot
            };

            await dialog.LoadPreviewAsync();

            var result = await ShowDialogGuardedAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            try
            {
                var exportService = new AnimationExportService();
                var options = dialog.GetExportOptions();
                string format = dialog.SelectedFormat;

                // Render frames based on source selection
                List<AnimationExportService.RenderedFrame> frames;
                Dictionary<string, List<AnimationExportService.RenderedFrame>>? framesByLayer = null;

                if (dialog.ExportTileAnimation && doc.TileAnimationState.SelectedReel != null)
                {
                    frames = await exportService.RenderTileAnimationAsync(
                        doc, doc.TileAnimationState.SelectedReel, options);
                }
                else if (dialog.SeparateLayers && (format == "png" || format == "jpg"))
                {
                    framesByLayer = await exportService.RenderCanvasAnimationByLayerAsync(doc, options);
                    frames = []; // Not used for per-layer export
                }
                else
                {
                    frames = await exportService.RenderCanvasAnimationAsync(doc, options);
                }

                // Export based on format
                switch (format)
                {
                    case "gif":
                        await ExportAsGifAsync(frames, doc.Name ?? "animation", dialog.Loop);
                        break;

                    case "mp4":
                    case "wmv":
                    case "avi":
                        await ExportAsVideoAsync(frames, doc.Name ?? "animation", format, dialog.VideoQuality, dialog.Fps);
                        break;

                    case "png":
                    case "jpg":
                        if (framesByLayer != null && framesByLayer.Count > 0)
                        {
                            await ExportAsImageSequenceByLayerAsync(framesByLayer, doc.Name ?? "animation", format);
                        }
                        else
                        {
                            await ExportAsImageSequenceAsync(frames, doc.Name ?? "animation", format);
                        }
                        break;
                }

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export complete",
                    Content = "Animation exported successfully!",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
            catch (OperationCanceledException)
            {
                // User cancelled, no message needed
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export failed",
                    Content = $"Could not export animation: {ex.Message}",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        private async Task ExportAsGifAsync(List<AnimationExportService.RenderedFrame> frames, string baseName, bool loop)
        {
            var savePicker = WindowHost.CreateFileSavePicker(this, baseName, ".gif");
            savePicker.DefaultFileExtension = ".gif";
            var file = await savePicker.PickSaveFileAsync();
            if (file is null) return;

            await GifEncoder.EncodeAsync(frames, file.Path, loop);
        }

        private async Task ExportAsVideoAsync(
            List<AnimationExportService.RenderedFrame> frames,
            string baseName,
            string format,
            int quality,
            int fps)
        {
            var videoFormat = format switch
            {
                "mp4" => VideoFormat.Mp4,
                "wmv" => VideoFormat.Wmv,
                "avi" => VideoFormat.Avi,
                _ => VideoFormat.Mp4
            };

            string extension = VideoEncoder.GetExtension(videoFormat);
            var savePicker = WindowHost.CreateFileSavePicker(this, baseName, extension);
            savePicker.DefaultFileExtension = extension;
            var file = await savePicker.PickSaveFileAsync();
            if (file is null) return;

            await VideoEncoder.EncodeAsync(frames, file.Path, videoFormat, fps, quality);
        }

        private async Task ExportAsImageSequenceAsync(
            List<AnimationExportService.RenderedFrame> frames,
            string baseName,
            string format)
        {
            var folderPicker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder is null) return;

            var imageFormat = format == "jpg" ? ImageSequenceFormat.Jpeg : ImageSequenceFormat.Png;
            await ImageSequenceExporter.ExportAsync(frames, folder.Path, baseName, imageFormat);
        }

        private async Task ExportAsImageSequenceByLayerAsync(
            Dictionary<string, List<AnimationExportService.RenderedFrame>> framesByLayer,
            string baseName,
            string format)
        {
            var folderPicker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder is null) return;

            var imageFormat = format == "jpg" ? ImageSequenceFormat.Jpeg : ImageSequenceFormat.Png;
            await ImageSequenceExporter.ExportByLayerAsync(framesByLayer, folder.Path, imageFormat);
        }

        // ═══════════════════════════════════════════════════════════════
        // BRUSH EXPORT
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Menu handler: Export to custom brush (.mrk)
        /// </summary>
        private async void File_Export_Brush_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "No document",
                    Content = "Open a document before exporting.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // ═══════════════════════════════════════════════════════════════
            // Use dedicated BrushExportDialog
            // ═══════════════════════════════════════════════════════════════
            var dialog = new PixlPunkt.UI.Dialogs.Export.BrushExportDialog(doc)
            {
                XamlRoot = Content.XamlRoot
            };

            // Analyze document and generate preview
            await dialog.AnalyzeLayersAsync();

            // Show dialog
            var result = await ShowDialogGuardedAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            // Check document is valid for export
            if (!dialog.IsValidForExport)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Invalid document",
                    Content = "Document must be 16x16 pixels with content to export as a brush.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // Generate brush template
            var brush = dialog.GenerateBrushTemplate();

            // Use author.brushname format for filename
            var suggestedFileName = BrushExportConstants.GetFileName(brush.Author, brush.Name);

            // Save .mrk file
            var savePicker = WindowHost.CreateFileSavePicker(
                this,
                suggestedFileName.Replace(BrushExportConstants.FileExtension, ""),
                BrushExportConstants.FileExtension
            );
            savePicker.DefaultFileExtension = BrushExportConstants.FileExtension;

            var file = await savePicker.PickSaveFileAsync();
            if (file is null) return;

            try
            {
                // Save to temp file first, then copy to StorageFile
                var tempPath = Path.GetTempFileName();
                var mrkPath = tempPath + BrushExportConstants.FileExtension;

                try
                {
                    BrushMarkIO.Save(brush, mrkPath);
                    var mrkBytes = File.ReadAllBytes(mrkPath);
                    await FileIO.WriteBytesAsync(file, mrkBytes);
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    if (File.Exists(mrkPath)) File.Delete(mrkPath);
                }

                // Register with brush service
                BrushDefinitionService.Instance.AddBrush(brush);

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export complete",
                    Content = $"Brush exported successfully!\n\n" +
                              $"Identifier: {brush.FullName}\n" +
                              $"Icon: {brush.IconName}\n\n" +
                              $"Place .mrk files in:\n%AppData%\\PixlPunkt\\Brushes\\",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export failed",
                    Content = $"Could not export brush: {ex.Message}",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ICON EXPORT
        // ═══════════════════════════════════════════════════════════════

        // Menu handler: Export to .ico
        private async void File_Export_Icon_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "No document",
                    Content = "Open a document before exporting.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // ═══════════════════════════════════════════════════════════════
            // Use dedicated IconExportDialog
            // ══════════════════════════════════════════════════════════════
            var dialog = new PixlPunkt.UI.Dialogs.Export.IconExportDialog(doc)
            {
                XamlRoot = Content.XamlRoot
            };

            // Load previews
            await dialog.LoadPreviewsAsync();

            // Show dialog
            var result = await ShowDialogGuardedAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            // Generate final PNGs using selected scale mode
            var pngList = await dialog.GenerateFinalIconPngsAsync();

            // Save .ico file
            var savePicker = WindowHost.CreateFileSavePicker(
                this,
                doc.Name ?? FileDefaults.DefaultIconName,
                IconExportConstants.FileExtension
            );
            savePicker.DefaultFileExtension = IconExportConstants.FileExtension;

            var file = await savePicker.PickSaveFileAsync();
            if (file is null) return;

            try
            {
                await SaveIcoFileAsync(file, pngList, IconExportConstants.StandardSizes);

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export complete",
                    Content = "Icon exported successfully.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export failed",
                    Content = $"Could not export icon: {ex.Message}",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        // Choose PixelOps implementation based on ScaleMode
        private static byte[] ResizeUsingMode(ScaleMode mode, byte[] src, int sw, int sh, int dw, int dh)
        {
            return mode switch
            {
                ScaleMode.NearestNeighbor => PixelOps.ResizeNearest(src, sw, sh, dw, dh),
                ScaleMode.Bilinear => PixelOps.ResizeBilinear(src, sw, sh, dw, dh),
                ScaleMode.EPX => PixelOps.ScaleBy2xStepsThenNearest(src, sw, sh, dw, dh, true).buf,
                ScaleMode.Scale2x => PixelOps.ScaleBy2xStepsThenNearest(src, sw, sh, dw, dh, false).buf,
                _ => PixelOps.ResizeNearest(src, sw, sh, dw, dh)
            };
        }

        // Encode BGRA buffer to PNG bytes using BitmapEncoder
        // For cursor export: use Ignore alpha mode to ensure Windows compatibility
        private static async Task<byte[]> EncodePngToBytesAsync(byte[] bgra, int w, int h)
        {
            using var ras = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ras);
            // For cursors: Use Premultiplied (standard for Windows) or Ignore
            // Straight alpha can cause issues with some Windows cursor loaders
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                (uint)w, (uint)h, 96, 96, bgra);
            await encoder.FlushAsync();
            ras.Seek(0);
            var reader = new DataReader(ras.GetInputStreamAt(0));
            var bytes = new byte[ras.Size];
            await reader.LoadAsync((uint)ras.Size);
            reader.ReadBytes(bytes);
            return bytes;
        }

        // Save ICO file composed of PNG images (PNG-in-ICO)
        private static async Task SaveIcoFileAsync(StorageFile file, List<byte[]> pngImages, int[] sizes)
        {
            if (pngImages == null || pngImages.Count == 0)
                throw new ArgumentException("No images supplied.", nameof(pngImages));
            if (pngImages.Count != sizes.Length)
                throw new ArgumentException("Number of images and sizes must match.");

            using var ms = new MemoryStream();

            // Little-endian writer over the in-memory buffer
            using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                // ICONDIR header
                bw.Write((ushort)0);                 // reserved
                bw.Write((ushort)1);                 // type = 1 (icon)
                bw.Write((ushort)pngImages.Count);   // count

                // Offset where the first image will start:
                // 6-byte header + 16 bytes per entry
                int offset = 6 + 16 * pngImages.Count;

                // Directory entries
                for (int i = 0; i < pngImages.Count; i++)
                {
                    var png = pngImages[i];
                    int size = sizes[i];

                    // 0 means 256 in ICO
                    byte w = (byte)(size >= 256 ? 0 : size);
                    byte h = (byte)(size >= 256 ? 0 : size);

                    bw.Write(w);                     // width
                    bw.Write(h);                     // height
                    bw.Write((byte)0);               // color count (0 = use PNG)
                    bw.Write((byte)0);               // reserved
                    bw.Write((ushort)1);             // planes (1 is conventional)
                    bw.Write((ushort)32);            // bit count (32-bit w/ alpha)
                    bw.Write((uint)png.Length);      // bytes in resource
                    bw.Write((uint)offset);          // offset to image data

                    offset += png.Length;
                }

                // Actual PNG image data blobs
                for (int i = 0; i < pngImages.Count; i++)
                    bw.Write(pngImages[i]);

                bw.Flush();
            }

            // Dump the in-memory ICO to the StorageFile, truncating any old contents
            await FileIO.WriteBytesAsync(file, ms.ToArray());
        }

        private static string SanitizeFileName(string input)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');
            return input;
        }
        // Menu handler wired from XAML: "Image" export entry
        private async void File_Export_Image_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "No document",
                    Content = "Open a document before exporting.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // ═══════════════════════════════════════════════════════════════
            // Use dedicated ImageExportDialog
            // ═══════════════════════════════════════════════════════════════
            var dialog = new PixlPunkt.UI.Dialogs.Export.ImageExportDialog(doc)
            {
                XamlRoot = Content.XamlRoot
            };

            // Load preview
            await dialog.LoadPreviewAsync();

            // Show dialog
            var result = await ShowDialogGuardedAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            // Get settings from dialog
            int scale = dialog.Scale;
            string format = dialog.SelectedFormat;
            bool separateLayers = dialog.SeparateLayers;
            uint bgColorU = dialog.BackgroundColor;

            try
            {
                if (separateLayers)
                {
                    // Export each layer separately
                    var folderPicker = new FolderPicker();
                    WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                    folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                    var folder = await folderPicker.PickSingleFolderAsync();
                    if (folder is null) return;

                    // Export each layer
                    int idx = 0;
                    foreach (var layer in doc.Layers)
                    {
                        idx++;
                        string baseName = SanitizeFileName($"{doc.Name ?? FileDefaults.UntitledDocument}_{idx}_{layer.Name}");
                        string ext = format;
                        var file = await folder.CreateFileAsync($"{baseName}.{ext}", CreationCollisionOption.ReplaceExisting);

                        // Compose pixel data for single layer
                        var src = layer.Surface.Pixels;
                        int w = doc.PixelWidth;
                        int h = doc.PixelHeight;

                        var outBgra = ComposePixelsForExport(src, w, h, layer.Opacity, bgColorU, format);
                        await SaveBgraToFileAsync(outBgra, w, h, scale, file, format);
                    }
                }
                else
                {
                    // Export single composed image
                    var tmp = new Core.Imaging.PixelSurface(doc.PixelWidth, doc.PixelHeight);
                    doc.CompositeTo(tmp);
                    var src = tmp.Pixels;
                    int w = doc.PixelWidth;
                    int h = doc.PixelHeight;

                    // Pick file save location
                    var savePicker = new FileSavePicker();
                    WinRT.Interop.InitializeWithWindow.Initialize(savePicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                    savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                    savePicker.SuggestedFileName = doc.Name ?? FileDefaults.DefaultExportName;

                    // Add all supported file types
                    foreach (var (description, extension) in ImageExportConstants.FileTypeChoices)
                    {
                        savePicker.FileTypeChoices.Add(description, [extension]);
                    }

                    // Set default extension based on selected format
                    savePicker.DefaultFileExtension = "." + format;
                    var file = await savePicker.PickSaveFileAsync();
                    if (file is null) return;

                    var outBgra = ComposePixelsForExport(src, w, h, 255, bgColorU, format);
                    await SaveBgraToFileAsync(outBgra, w, h, scale, file, format);
                }

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export complete",
                    Content = "Export finished successfully.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export failed",
                    Content = $"Could not export: {ex.Message}",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        // Compose input BGRA byte[] for export.
        // src is BGRA byte[] (length == w*h*4).
        // opacity: 0..255 applied to source pixels (useful when exporting individual layers),
        // bgColorU applied for formats that don't support alpha (bmp/jpeg) - otherwise alpha preserved.
        private static byte[] ComposePixelsForExport(byte[] src, int w, int h, byte layerOpacity, uint bgColorU, string format)
        {
            bool requiresFlatten = format == "bmp" || format == "jpeg";
            int len = w * h * 4;
            var outBuf = new byte[len];

            byte bgB = (byte)(bgColorU & 0xFF);
            byte bgG = (byte)((bgColorU >> 8) & 0xFF);
            byte bgR = (byte)((bgColorU >> 16) & 0xFF);
            byte bgA = (byte)((bgColorU >> 24) & 0xFF);

            for (int i = 0; i < len; i += 4)
            {
                byte sb = src[i + 0];
                byte sg = src[i + 1];
                byte sr = src[i + 2];
                byte sa = src[i + 3];

                // apply layer opacity
                if (layerOpacity != 255)
                    sa = (byte)((sa * layerOpacity) / 255);

                if (requiresFlatten)
                {
                    // flatten over background color (assume straight alpha)
                    if (sa == 0)
                    {
                        outBuf[i + 0] = bgB;
                        outBuf[i + 1] = bgG;
                        outBuf[i + 2] = bgR;
                        outBuf[i + 3] = 255;
                    }
                    else if (sa == 255)
                    {
                        outBuf[i + 0] = sb;
                        outBuf[i + 1] = sg;
                        outBuf[i + 2] = sr;
                        outBuf[i + 3] = 255;
                    }
                    else
                    {
                        int invA = 255 - sa;
                        outBuf[i + 0] = (byte)((sb * sa + bgB * invA) / 255);
                        outBuf[i + 1] = (byte)((sg * sa + bgG * invA) / 255);
                        outBuf[i + 2] = (byte)((sr * sa + bgR * invA) / 255);
                        outBuf[i + 3] = 255;
                    }
                }
                else
                {
                    // keep alpha intact (encoder will handle it)
                    outBuf[i + 0] = sb;
                    outBuf[i + 1] = sg;
                    outBuf[i + 2] = sr;
                    outBuf[i + 3] = sa;
                }
            }

            return outBuf;
        }

        // Save BGRA buffer to file using BitmapEncoder, with optional integer scale.
        private static async Task SaveBgraToFileAsync(byte[] srcBgra, int srcW, int srcH, int scale, StorageFile file, string format)
        {
            // choose encoder id
            Guid encoderId = format switch
            {
                "png" => BitmapEncoder.PngEncoderId,
                "gif" => BitmapEncoder.GifEncoderId,
                "bmp" => BitmapEncoder.BmpEncoderId,
                "jpeg" => BitmapEncoder.JpegEncoderId,
                "tiff" => BitmapEncoder.TiffEncoderId,
                _ => BitmapEncoder.PngEncoderId
            };

            int dstW = srcW * Math.Max(1, scale);
            int dstH = srcH * Math.Max(1, scale);

            // Scale (nearest neighbour) while copying to destination buffer.
            var dst = new byte[dstW * dstH * 4];
            for (int y = 0; y < dstH; y++)
            {
                int sy = Math.Min(srcH - 1, y / Math.Max(1, scale));
                for (int x = 0; x < dstW; x++)
                {
                    int sx = Math.Min(srcW - 1, x / Math.Max(1, scale));
                    int sIdx = (sy * srcW + sx) * 4;
                    int dIdx = (y * dstW + x) * 4;
                    dst[dIdx + 0] = srcBgra[sIdx + 0];
                    dst[dIdx + 1] = srcBgra[sIdx + 1];
                    dst[dIdx + 2] = srcBgra[sIdx + 2];
                    dst[dIdx + 3] = srcBgra[sIdx + 3];
                }
            }

            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
            // Pixel format must match BGRA8
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                (uint)dstW, (uint)dstH, 96, 96, dst);
            await encoder.FlushAsync();
        }

        private async void File_Export_Cursor_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "No document",
                    Content = "Open a document before exporting.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // ═══════════════════════════════════════════════════════════════
            // Use dedicated CursorExportDialog
            // ═══════════════════════════════════════════════════════════════
            var dialog = new PixlPunkt.UI.Dialogs.Export.CursorExportDialog(doc)
            {
                XamlRoot = Content.XamlRoot
            };

            // Load preview
            await dialog.LoadPreviewAsync();

            // Show dialog
            var result = await ShowDialogGuardedAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            // Get settings from dialog
            var scaleMode = dialog.SelectedScaleMode;
            int hotspotX = dialog.HotspotX;
            int hotspotY = dialog.HotspotY;
            bool separateLayers = dialog.SeparateLayers;
            bool multiRes = dialog.MultiResolution;

            // Compose full document
            var composed = new Core.Imaging.PixelSurface(doc.PixelWidth, doc.PixelHeight);
            doc.CompositeTo(composed);

            try
            {
                if (separateLayers)
                {
                    // Export each layer separately
                    var folderPicker = new FolderPicker();
                    WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                    folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

                    var folder = await folderPicker.PickSingleFolderAsync();
                    if (folder is null) return;

                    int idx = 0;
                    foreach (var layer in doc.Layers)
                    {
                        idx++;
                        string baseName = SanitizeFileName($"{doc.Name ?? FileDefaults.UntitledDocument}_{idx}_{layer.Name}");
                        var file = await folder.CreateFileAsync($"{baseName}.cur", CreationCollisionOption.ReplaceExisting);

                        if (multiRes)
                        {
                            await SaveMultiResCurFileAsync(file, layer.Surface.Pixels, doc.PixelWidth, doc.PixelHeight,
                                scaleMode, hotspotX, hotspotY);
                        }
                        else
                        {
                            var resized = ResizeUsingMode(scaleMode, layer.Surface.Pixels, doc.PixelWidth, doc.PixelHeight, 32, 32);
                            await SaveCurFileAsync(file, resized, 32, 32, hotspotX, hotspotY);
                        }
                    }
                }
                else
                {
                    // Export single composed cursor
                    var savePicker = WindowHost.CreateFileSavePicker(
                        this,
                        doc.Name ?? FileDefaults.DefaultCursorName,
                        CursorExportConstants.FileExtension
                    );
                    savePicker.DefaultFileExtension = CursorExportConstants.FileExtension;

                    var file = await savePicker.PickSaveFileAsync();
                    if (file is null) return;

                    if (multiRes)
                    {
                        await SaveMultiResCurFileAsync(file, composed.Pixels, doc.PixelWidth, doc.PixelHeight,
                            scaleMode, hotspotX, hotspotY);
                    }
                    else
                    {
                        var resized = ResizeUsingMode(scaleMode, composed.Pixels, doc.PixelWidth, doc.PixelHeight, 32, 32);
                        await SaveCurFileAsync(file, resized, 32, 32, hotspotX, hotspotY);
                    }
                }

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export complete",
                    Content = "Cursor exported successfully!",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
            catch (Exception ex)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export failed",
                    Content = $"Could not export cursor: {ex.Message}",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CURSOR EXPORT HELPER METHODS
        // ═══════════════════════════════════════════════════════════════

        private static void CompositeTransparencyOverStripes(byte[] buf, int w, int h)
        {
            const int STRIPE_BAND = 8;
            byte wR = TransparencyStripeMixer.LightR;
            byte wG = TransparencyStripeMixer.LightG;
            byte wB = TransparencyStripeMixer.LightB;
            byte gR = TransparencyStripeMixer.DarkR;
            byte gG = TransparencyStripeMixer.DarkG;
            byte gB = TransparencyStripeMixer.DarkB;

            for (int y = 0; y < h; y++)
            {
                int row = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int i = row + x * 4;
                    byte b = buf[i + 0];
                    byte g = buf[i + 1];
                    byte r = buf[i + 2];
                    byte a = buf[i + 3];

                    bool stripeToggle = (((x + y) / STRIPE_BAND) & 1) == 0;
                    byte baseR = stripeToggle ? wR : gR;
                    byte baseG = stripeToggle ? wG : gG;
                    byte baseB = stripeToggle ? wB : gB;

                    if (a == 0)
                    {
                        buf[i + 0] = baseB;
                        buf[i + 1] = baseG;
                        buf[i + 2] = baseR;
                        buf[i + 3] = 255;
                    }
                    else if (a < 255)
                    {
                        int invA = 255 - a;
                        buf[i + 0] = (byte)((b * a + baseB * invA) / 255);
                        buf[i + 1] = (byte)((g * a + baseG * invA) / 255);
                        buf[i + 2] = (byte)((r * a + baseR * invA) / 255);
                        buf[i + 3] = 255;
                    }
                    else
                    {
                        buf[i + 3] = 255;
                    }
                }
            }
        }

        // Save CUR file using the proven DIB format via ImageExport
        private static async Task SaveCurFileAsync(
            StorageFile file,
            byte[] pixels32,     // BGRA, width*height*4
            int width,
            int height,
            int hotspotX,
            int hotspotY)
        {
            if (pixels32 is null) throw new ArgumentNullException(nameof(pixels32));
            if (pixels32.Length < width * height * 4)
                throw new ArgumentException("pixels32 too small for width/height");

            // Use the proven ImageExport.SaveAsCursor which uses DIB format
            // This is more reliable with LoadCursorFromFileW than PNG-in-CUR
            var pixelSurface = new Core.Imaging.PixelSurface(width, height);
            Array.Copy(pixels32, pixelSurface.Pixels, pixels32.Length);

            // Save to temp file using proven DIB implementation
            var tempPath = System.IO.Path.GetTempFileName();
            var curPath = tempPath + ".cur";

            try
            {
                ImageExport.Save(pixelSurface, curPath, ImageFileFormat.Cur,
                    (ushort)hotspotX, (ushort)hotspotY);

                // Copy temp file bytes to StorageFile
                var curBytes = File.ReadAllBytes(curPath);
                await FileIO.WriteBytesAsync(file, curBytes);

            }
            finally
            {
                // Clean up temp files
                if (File.Exists(tempPath)) File.Delete(tempPath);
                if (File.Exists(curPath)) File.Delete(curPath);
            }
        }

        // Save multi-resolution CUR file with multiple sizes (32×32, 48×48, 64×64, 128×128)
        private static async Task SaveMultiResCurFileAsync(
            StorageFile file,
            byte[] sourcePixels,  // BGRA source pixels
            int sourceWidth,
            int sourceHeight,
            ScaleMode scaleMode,
            int hotspotX,
            int hotspotY)
        {
            if (sourcePixels is null) throw new ArgumentNullException(nameof(sourcePixels));

            // Generate all cursor sizes using the selected scaling algorithm
            var cursorImages = new List<byte[]>();
            var tempFiles = new List<string>();

            try
            {
                // Generate each size
                foreach (var size in CursorSizes)
                {
                    var resized = ResizeUsingMode(scaleMode, sourcePixels, sourceWidth, sourceHeight, size, size);

                    // Save to temp file using ImageExport
                    var pixelSurface = new Core.Imaging.PixelSurface(size, size);
                    Array.Copy(resized, pixelSurface.Pixels, resized.Length);

                    var tempPath = System.IO.Path.GetTempFileName();
                    var curPath = tempPath + $"_{size}.cur";
                    tempFiles.Add(tempPath);
                    tempFiles.Add(curPath);

                    // Scale hotspot proportionally
                    int scaledHotspotX = (int)Math.Round(hotspotX * size / 32.0);
                    int scaledHotspotY = (int)Math.Round(hotspotY * size / 32.0);
                    scaledHotspotX = Math.Clamp(scaledHotspotX, 0, size - 1);
                    scaledHotspotY = Math.Clamp(scaledHotspotY, 0, size - 1);

                    ImageExport.Save(pixelSurface, curPath, ImageFileFormat.Cur,
                        (ushort)scaledHotspotX, (ushort)scaledHotspotY);

                    var curBytes = File.ReadAllBytes(curPath);
                    cursorImages.Add(curBytes);
                }

                // Now combine all cursor images into one multi-resolution CUR file
                await SaveMultiImageCurFileAsync(file, cursorImages, CursorSizes);
            }
            finally
            {
                // Clean up all temp files
                foreach (var tempFile in tempFiles)
                {
                    if (File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); } catch { }
                    }
                }
            }
        }

        // Save multi-image CUR file (similar to multi-image ICO)
        private static async Task SaveMultiImageCurFileAsync(StorageFile file, List<byte[]> cursorImages, int[] sizes)
        {
            if (cursorImages == null || cursorImages.Count == 0)
                throw new ArgumentException("No cursor images supplied.", nameof(cursorImages));
            if (cursorImages.Count != sizes.Length)
                throw new ArgumentException("Number of images and sizes must match.");

            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                // ICONDIR header
                bw.Write((ushort)0);                    // reserved
                bw.Write((ushort)2);                    // type = 2 (cursor)
                bw.Write((ushort)cursorImages.Count);   // count

                // Each cursor image is already a complete CUR file with header
                // We need to extract the DIB data and directory entry from each
                // For simplicity, we'll parse each CUR file and extract the image data

                // Calculate offset where first image will start
                int offset = 6 + 16 * cursorImages.Count;

                var imageDataList = new List<byte[]>();
                var hotspotList = new List<(ushort x, ushort y)>();

                // Parse each cursor file to extract image data and hotspot
                foreach (var curBytes in cursorImages)
                {
                    using var curMs = new MemoryStream(curBytes);
                    using var curBr = new BinaryReader(curMs);

                    // Skip ICONDIR header (6 bytes)
                    curBr.ReadUInt16(); // reserved
                    curBr.ReadUInt16(); // type
                    curBr.ReadUInt16(); // count

                    // Read ICONDIRENTRY (16 bytes)
                    curBr.ReadByte(); // width
                    curBr.ReadByte(); // height
                    curBr.ReadByte(); // color count
                    curBr.ReadByte(); // reserved
                    ushort hotX = curBr.ReadUInt16();
                    ushort hotY = curBr.ReadUInt16();
                    uint bytesInRes = curBr.ReadUInt32();
                    uint imageOffsetInSingleCur = curBr.ReadUInt32();

                    hotspotList.Add((hotX, hotY));

                    // Read the actual image data (DIB)
                    curMs.Seek(imageOffsetInSingleCur, SeekOrigin.Begin);
                    var imageData = new byte[bytesInRes];
                    curBr.Read(imageData, 0, (int)bytesInRes);
                    imageDataList.Add(imageData);
                }

                // Write directory entries
                for (int i = 0; i < cursorImages.Count; i++)
                {
                    int size = sizes[i];
                    byte w = (byte)(size >= 256 ? 0 : size);
                    byte h = (byte)(size >= 256 ? 0 : size);

                    bw.Write(w);                            // width
                    bw.Write(h);                            // height
                    bw.Write((byte)0);                      // color count
                    bw.Write((byte)0);                      // reserved
                    bw.Write(hotspotList[i].x);             // hotspot X
                    bw.Write(hotspotList[i].y);             // hotspot Y
                    bw.Write((uint)imageDataList[i].Length); // bytes in resource
                    bw.Write((uint)offset);                 // offset to image data

                    offset += imageDataList[i].Length;
                }

                // Write all image data
                foreach (var imageData in imageDataList)
                {
                    bw.Write(imageData);
                }

                bw.Flush();
            }

            await FileIO.WriteBytesAsync(file, ms.ToArray());
        }
    }
}