using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Constants;
using PixlPunkt.Core.Brush;
using PixlPunkt.Core.Document;
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

            var settings = dialog.BuildSettings();
            string format = dialog.SelectedFormat;

            // Pick file location BEFORE starting the export
            string baseName = $"{doc.Name ?? "timelapse"}_timelapse";
            string? outputPath = null;

            switch (format)
            {
                case "gif":
                    var gifPicker = WindowHost.CreateFileSavePicker(this, baseName, ".gif");
                    gifPicker.DefaultFileExtension = ".gif";
                    var gifFile = await gifPicker.PickSaveFileAsync();
                    if (gifFile is null) return;
                    outputPath = gifFile.Path;
                    break;

                case "mp4":
                    var mp4Picker = WindowHost.CreateFileSavePicker(this, baseName, ".mp4");
                    mp4Picker.DefaultFileExtension = ".mp4";
                    var mp4File = await mp4Picker.PickSaveFileAsync();
                    if (mp4File is null) return;
                    outputPath = mp4File.Path;
                    break;

                case "png":
                    var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                    WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                    folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                    var folder = await folderPicker.PickSingleFolderAsync();
                    if (folder is null) return;
                    outputPath = folder.Path;
                    break;
            }

            // Create and show progress dialog
            var progressDialog = new PixlPunkt.UI.Dialogs.Export.ExportProgressDialog
            {
                XamlRoot = Content.XamlRoot
            };

            // Start showing the dialog (fire-and-forget, will be awaited later)
            var dialogTask = ShowDialogGuardedAsync(progressDialog);

            // Wait for dialog to be visible on screen before starting export
            await progressDialog.WaitUntilShownAsync();
            progressDialog.Start();

            // Now start the export (dialog is visible, progress will show correctly)
            var exportTask = RunTimelapseExportAsync(doc, settings, format, outputPath!, baseName, progressDialog);

            try
            {
                await exportTask;

                if (!progressDialog.WasCancelled)
                {
                    // Wait for progress dialog to close
                    await dialogTask;

                    await ShowDialogGuardedAsync(new ContentDialog
                    {
                        Title = "Export complete",
                        Content = $"Timelapse exported successfully!",
                        CloseButtonText = DialogMessages.ButtonOK,
                        XamlRoot = Content.XamlRoot
                    });
                }
                else
                {
                    await dialogTask;
                }
            }
            catch (OperationCanceledException)
            {
                // User cancelled, no message needed
                await dialogTask;
            }
            catch (Exception ex)
            {
                progressDialog.Complete(false);
                await dialogTask;
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export failed",
                    Content = $"Could not export timelapse: {ex.Message}",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        /// <summary>
        /// Runs the timelapse export with progress updates.
        /// </summary>
        private async Task RunTimelapseExportAsync(
            CanvasDocument doc,
            TimelapseExportSettings settings,
            string format,
            string outputPath,
            string baseName,
            PixlPunkt.UI.Dialogs.Export.ExportProgressDialog progressDialog)
        {
            var exportService = new TimelapseExportService();

            // Phase 1: Render frames (0-50%)
            progressDialog.UpdateProgress(0, "Rendering timelapse frames...", "Preparing...");

            var renderProgress = new Progress<double>(p =>
            {
                int frameNum = (int)(p * (settings.RangeEnd - settings.RangeStart));
                progressDialog.UpdateProgress(p * 0.5, "Rendering timelapse frames...", $"Frame {frameNum}");
            });

            var frames = await exportService.RenderTimelapseAsync(
                doc, settings, renderProgress, progressDialog.CancellationToken);

            if (frames.Count == 0)
            {
                progressDialog.Complete(false);
                throw new InvalidOperationException("No frames were generated. Try adjusting the range or settings.");
            }

            // Convert to AnimationExportService.RenderedFrame
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

            // Phase 2: Encode output (50-100%)
            progressDialog.UpdateProgress(0.5, "Encoding output...", $"{frames.Count} frames");

            var encodeProgress = new Progress<double>(p =>
            {
                progressDialog.UpdateProgress(0.5 + p * 0.5, "Encoding output...");
            });

            switch (format)
            {
                case "gif":
                    await GifEncoder.EncodeAsync(animFrames, outputPath, loop: true);
                    break;

                case "mp4":
                    int effectiveFps = frames.Count > 0 && frames[0].DurationMs > 0
                        ? Math.Max(1, 1000 / frames[0].DurationMs)
                        : 12;
                    await VideoEncoder.EncodeAsync(animFrames, outputPath, VideoFormat.Mp4, effectiveFps, 80, encodeProgress, progressDialog.CancellationToken);
                    break;

                case "png":
                    await ImageSequenceExporter.ExportAsync(animFrames, outputPath, baseName, ImageSequenceFormat.Png);
                    break;
            }

            progressDialog.Complete(true);
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

            var options = dialog.GetExportOptions();
            string format = dialog.SelectedFormat;
            bool separateLayers = dialog.SeparateLayers;
            bool exportTileAnim = dialog.ExportTileAnimation;
            bool loop = dialog.Loop;
            int videoQuality = dialog.VideoQuality;
            int fps = dialog.Fps;

            // Pick file/folder location BEFORE starting export
            string baseName = doc.Name ?? "animation";
            string? outputPath = null;
            StorageFolder? outputFolder = null;

            switch (format)
            {
                case "gif":
                    var gifPicker = WindowHost.CreateFileSavePicker(this, baseName, ".gif");
                    gifPicker.DefaultFileExtension = ".gif";
                    var gifFile = await gifPicker.PickSaveFileAsync();
                    if (gifFile is null) return;
                    outputPath = gifFile.Path;
                    break;

                case "mp4":
                case "wmv":
                case "avi":
                    var videoFormat = format switch
                    {
                        "mp4" => VideoFormat.Mp4,
                        "wmv" => VideoFormat.Wmv,
                        "avi" => VideoFormat.Avi,
                        _ => VideoFormat.Mp4
                    };
                    string extension = VideoEncoder.GetExtension(videoFormat);
                    var videoPicker = WindowHost.CreateFileSavePicker(this, baseName, extension);
                    videoPicker.DefaultFileExtension = extension;
                    var videoFile = await videoPicker.PickSaveFileAsync();
                    if (videoFile is null) return;
                    outputPath = videoFile.Path;
                    break;

                case "png":
                case "jpg":
                    var folderPicker = new FolderPicker();
                    WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                    folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                    outputFolder = await folderPicker.PickSingleFolderAsync();
                    if (outputFolder is null) return;
                    outputPath = outputFolder.Path;
                    break;
            }

            // Create and show progress dialog
            var progressDialog = new PixlPunkt.UI.Dialogs.Export.ExportProgressDialog
            {
                XamlRoot = Content.XamlRoot
            };

            // Start export in background
            var exportTask = RunAnimationExportAsync(
                doc, options, format, outputPath!, baseName,
                exportTileAnim, separateLayers, loop, videoQuality, fps,
                progressDialog);

            // Show progress dialog
            progressDialog.Start();
            _ = ShowDialogGuardedAsync(progressDialog);

            try
            {
                await exportTask;

                if (!progressDialog.WasCancelled)
                {
                    await ShowDialogGuardedAsync(new ContentDialog
                    {
                        Title = "Export complete",
                        Content = "Animation exported successfully!",
                        CloseButtonText = DialogMessages.ButtonOK,
                        XamlRoot = Content.XamlRoot
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // User cancelled
            }
            catch (Exception ex)
            {
                progressDialog.Complete(false);
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export failed",
                    Content = $"Could not export animation: {ex.Message}",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        /// <summary>
        /// Runs the animation export with progress updates.
        /// </summary>
        private async Task RunAnimationExportAsync(
            Core.Document.CanvasDocument doc,
            AnimationExportService.ExportOptions options,
            string format,
            string outputPath,
            string baseName,
            bool exportTileAnim,
            bool separateLayers,
            bool loop,
            int videoQuality,
            int fps,
            PixlPunkt.UI.Dialogs.Export.ExportProgressDialog progressDialog)
        {
            var exportService = new AnimationExportService();

            // Phase 1: Render frames (0-50%)
            progressDialog.UpdateProgress(0, "Rendering animation frames...", "Preparing...");

            List<AnimationExportService.RenderedFrame> frames;
            Dictionary<string, List<AnimationExportService.RenderedFrame>>? framesByLayer = null;

            if (exportTileAnim && doc.TileAnimationState.SelectedReel != null)
            {
                frames = await exportService.RenderTileAnimationAsync(
                    doc, doc.TileAnimationState.SelectedReel, options);
                progressDialog.UpdateProgress(0.5, "Rendering complete", $"{frames.Count} frames");
            }
            else if (separateLayers && (format == "png" || format == "jpg"))
            {
                framesByLayer = await exportService.RenderCanvasAnimationByLayerAsync(doc, options);
                frames = [];
                int totalFrames = 0;
                foreach (var layerFrames in framesByLayer.Values)
                    totalFrames += layerFrames.Count;
                progressDialog.UpdateProgress(0.5, "Rendering complete", $"{totalFrames} frames across {framesByLayer.Count} layers");
            }
            else
            {
                frames = await exportService.RenderCanvasAnimationAsync(doc, options);
                progressDialog.UpdateProgress(0.5, "Rendering complete", $"{frames.Count} frames");
            }

            progressDialog.CancellationToken.ThrowIfCancellationRequested();

            // Phase 2: Encode output (50-100%)
            progressDialog.UpdateProgress(0.5, "Encoding output...");

            var encodeProgress = new Progress<double>(p =>
            {
                progressDialog.UpdateProgress(0.5 + p * 0.5, "Encoding output...");
            });

            switch (format)
            {
                case "gif":
                    await GifEncoder.EncodeAsync(frames, outputPath, loop);
                    break;

                case "mp4":
                case "wmv":
                case "avi":
                    var videoFormat = format switch
                    {
                        "mp4" => VideoFormat.Mp4,
                        "wmv" => VideoFormat.Wmv,
                        "avi" => VideoFormat.Avi,
                        _ => VideoFormat.Mp4
                    };
                    await VideoEncoder.EncodeAsync(frames, outputPath, videoFormat, fps, videoQuality, encodeProgress, progressDialog.CancellationToken);
                    break;

                case "png":
                case "jpg":
                    var imageFormat = format == "jpg" ? ImageSequenceFormat.Jpeg : ImageSequenceFormat.Png;
                    if (framesByLayer != null && framesByLayer.Count > 0)
                    {
                        await ImageSequenceExporter.ExportByLayerAsync(framesByLayer, outputPath, imageFormat);
                    }
                    else
                    {
                        await ImageSequenceExporter.ExportAsync(frames, outputPath, baseName, imageFormat);
                    }
                    break;
            }

            progressDialog.Complete(true);
        }

        /// <summary>
        /// Menu handler: Batch Export Tile Animations
        /// </summary>
        private async void File_Export_BatchTileAnimation_Click(object sender, RoutedEventArgs e)
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

            // Check if there are any tile animation reels
            var reels = doc.TileAnimationState.Reels;
            if (reels.Count == 0 || !reels.Any(r => r.FrameCount > 0))
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "No tile animations",
                    Content = "No tile animation reels with frames found.\n\n" +
                              "Create some tile animations first, then try again.",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // Show batch export dialog
            var dialog = new PixlPunkt.UI.Dialogs.Export.BatchTileAnimationExportDialog(doc)
            {
                XamlRoot = Content.XamlRoot
            };

            var result = await ShowDialogGuardedAsync(dialog);
            if (result != ContentDialogResult.Primary) return;

            var selectedReels = dialog.SelectedReels;
            if (selectedReels.Count == 0) return;

            var options = dialog.GetExportOptions();
            string format = dialog.SelectedFormat;
            bool loop = dialog.Loop;
            int videoQuality = dialog.VideoQuality;
            int fps = dialog.Fps;
            bool isImageSequence = dialog.IsImageSequence;
            bool isSpriteStrip = dialog.IsSpriteStrip;

            // Pick output folder
            var folderPicker = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            var outputFolder = await folderPicker.PickSingleFolderAsync();
            if (outputFolder is null) return;

            // Create and show progress dialog
            var progressDialog = new PixlPunkt.UI.Dialogs.Export.ExportProgressDialog
            {
                XamlRoot = Content.XamlRoot
            };

            // Start showing the dialog (fire-and-forget, will be awaited later)
            var dialogTask = ShowDialogGuardedAsync(progressDialog);

            // Wait for dialog to be visible on screen before starting export
            await progressDialog.WaitUntilShownAsync();
            progressDialog.Start();

            // Now start the batch export (dialog is visible, progress will show correctly)
            var exportTask = RunBatchTileAnimationExportAsync(
                doc, selectedReels, options, format, outputFolder.Path,
                loop, videoQuality, fps, isImageSequence, isSpriteStrip,
                progressDialog);

            try
            {
                await exportTask;

                if (!progressDialog.WasCancelled)
                {
                    // Wait for progress dialog to close
                    await dialogTask;

                    await ShowDialogGuardedAsync(new ContentDialog
                    {
                        Title = "Batch export complete",
                        Content = $"Successfully exported {selectedReels.Count} animation{(selectedReels.Count > 1 ? "s" : "")}!\n\n" +
                                  $"Location: {outputFolder.Path}",
                        CloseButtonText = DialogMessages.ButtonOK,
                        XamlRoot = Content.XamlRoot
                    });
                }
                else
                {
                    await dialogTask;
                }
            }
            catch (OperationCanceledException)
            {
                // User cancelled
                await dialogTask;
            }
            catch (Exception ex)
            {
                progressDialog.Complete(false);
                await dialogTask;
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export failed",
                    Content = $"Could not complete batch export: {ex.Message}",
                    CloseButtonText = DialogMessages.ButtonOK,
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        /// <summary>
        /// Runs the batch tile animation export with progress updates.
        /// </summary>
        private async Task RunBatchTileAnimationExportAsync(
            Core.Document.CanvasDocument doc,
            IReadOnlyList<Core.Animation.TileAnimationReel> reels,
            AnimationExportService.ExportOptions options,
            string format,
            string outputFolderPath,
            bool loop,
            int videoQuality,
            int fps,
            bool isImageSequence,
            bool isSpriteStrip,
            PixlPunkt.UI.Dialogs.Export.ExportProgressDialog progressDialog)
        {
            var exportService = new AnimationExportService();
            int totalReels = reels.Count;
            int completedReels = 0;

            foreach (var reel in reels)
            {
                progressDialog.CancellationToken.ThrowIfCancellationRequested();

                string reelName = SanitizeFileName(reel.Name);
                progressDialog.UpdateProgress(
                    (double)completedReels / totalReels,
                    $"Exporting: {reel.Name}",
                    $"Animation {completedReels + 1} of {totalReels}");

                // Render frames for this reel
                var frames = await exportService.RenderTileAnimationAsync(doc, reel, options);

                if (frames.Count == 0)
                {
                    completedReels++;
                    continue;
                }

                // Calculate effective FPS
                int effectiveFps = fps > 0 ? fps : Math.Max(1, 1000 / reel.DefaultFrameTimeMs);

                // Encode based on format
                switch (format)
                {
                    case "gif":
                        string gifPath = Path.Combine(outputFolderPath, $"{reelName}.gif");
                        await GifEncoder.EncodeAsync(frames, gifPath, loop);
                        break;

                    case "mp4":
                        string mp4Path = Path.Combine(outputFolderPath, $"{reelName}.mp4");
                        await VideoEncoder.EncodeAsync(frames, mp4Path, VideoFormat.Mp4, effectiveFps, videoQuality, 
                            cancellationToken: progressDialog.CancellationToken);
                        break;

                    case "png":
                        string pngFolder = Path.Combine(outputFolderPath, reelName);
                        Directory.CreateDirectory(pngFolder);
                        await ImageSequenceExporter.ExportAsync(frames, pngFolder, reelName, ImageSequenceFormat.Png);
                        break;

                    case "strip":
                        string stripPath = Path.Combine(outputFolderPath, $"{reelName}.png");
                        await ExportSpriteStripAsync(frames, stripPath);
                        break;
                }

                completedReels++;
                progressDialog.UpdateProgress(
                    (double)completedReels / totalReels,
                    completedReels == totalReels ? "Export complete!" : $"Exported: {reel.Name}");
            }

            progressDialog.Complete(true);
        }

        /// <summary>
        /// Exports animation frames as a horizontal sprite strip PNG.
        /// </summary>
        private static async Task ExportSpriteStripAsync(List<AnimationExportService.RenderedFrame> frames, string outputPath)
        {
            if (frames.Count == 0) return;

            // All frames should have the same dimensions
            int frameWidth = frames[0].Width;
            int frameHeight = frames[0].Height;
            int frameCount = frames.Count;

            // Create the sprite strip - all frames side by side horizontally
            int stripWidth = frameWidth * frameCount;
            int stripHeight = frameHeight;

            byte[] stripPixels = new byte[stripWidth * stripHeight * 4];

            // Copy each frame into the strip
            for (int f = 0; f < frameCount; f++)
            {
                var frame = frames[f];
                int offsetX = f * frameWidth;

                // Copy row by row
                for (int y = 0; y < frameHeight; y++)
                {
                    int srcRowStart = y * frameWidth * 4;
                    int dstRowStart = (y * stripWidth + offsetX) * 4;

                    Array.Copy(frame.Pixels, srcRowStart, stripPixels, dstRowStart, frameWidth * 4);
                }
            }

            // Save as PNG - create file if it doesn't exist
            StorageFile file;
            try
            {
                file = await StorageFile.GetFileFromPathAsync(outputPath);
            }
            catch
            {
                // File doesn't exist, create it
                var folder = Path.GetDirectoryName(outputPath)!;
                var fileName = Path.GetFileName(outputPath);
                var storageFolder = await StorageFolder.GetFolderFromPathAsync(folder);
                file = await storageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            }

            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                (uint)stripWidth, (uint)stripHeight, 96, 96, stripPixels);
            await encoder.FlushAsync();
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