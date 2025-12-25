using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PixlPunkt.Core.Logging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PixlPunkt.Core.Export
{
    /// <summary>
    /// Image format for sequence export.
    /// </summary>
    public enum ImageSequenceFormat
    {
        /// <summary>PNG with alpha channel.</summary>
        Png,

        /// <summary>JPEG (no alpha, white background).</summary>
        Jpeg,

        /// <summary>BMP (no alpha, white background).</summary>
        Bmp
    }

    /// <summary>
    /// Exports animation frames as an image sequence to a folder.
    /// </summary>
    public static class ImageSequenceExporter
    {
        /// <summary>
        /// Exports frames to a folder as individual images.
        /// </summary>
        /// <param name="frames">Frames to export.</param>
        /// <param name="outputFolder">Target folder path.</param>
        /// <param name="baseName">Base name for output files (e.g., "frame").</param>
        /// <param name="format">Image format to use.</param>
        /// <param name="backgroundColor">Background color for non-alpha formats (BGRA).</param>
        /// <param name="progress">Optional progress callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of exported file paths.</returns>
        public static async Task<List<string>> ExportAsync(
            IReadOnlyList<AnimationExportService.RenderedFrame> frames,
            string outputFolder,
            string baseName,
            ImageSequenceFormat format = ImageSequenceFormat.Png,
            uint backgroundColor = 0xFFFFFFFF,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (frames == null || frames.Count == 0)
                throw new ArgumentException("No frames to export", nameof(frames));

            // Ensure folder exists
            Directory.CreateDirectory(outputFolder);

            var exportedFiles = new List<string>();
            string extension = GetExtension(format);
            Guid encoderId = GetEncoderId(format);

            LoggingService.Info("Exporting {FrameCount} frames to {Folder} as {Format}",
                frames.Count, outputFolder, format);

            for (int i = 0; i < frames.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var frame = frames[i];
                string fileName = $"{baseName}_{i:D5}{extension}";
                string filePath = Path.Combine(outputFolder, fileName);

                // Prepare pixels (apply background if needed)
                byte[] pixels = PreparePixelsForFormat(frame.Pixels, frame.Width, frame.Height, format, backgroundColor);

                await SaveImageAsync(pixels, frame.Width, frame.Height, filePath, encoderId, format);

                exportedFiles.Add(filePath);
                progress?.Report((double)(i + 1) / frames.Count);
            }

            LoggingService.Info("Exported {Count} images to {Folder}", exportedFiles.Count, outputFolder);

            return exportedFiles;
        }

        /// <summary>
        /// Exports frames grouped by layer to subfolders.
        /// </summary>
        /// <param name="framesByLayer">Dictionary mapping layer names to their frames.</param>
        /// <param name="outputFolder">Root output folder.</param>
        /// <param name="format">Image format to use.</param>
        /// <param name="backgroundColor">Background color for non-alpha formats.</param>
        /// <param name="progress">Optional progress callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary mapping layer names to their exported file paths.</returns>
        public static async Task<Dictionary<string, List<string>>> ExportByLayerAsync(
            Dictionary<string, List<AnimationExportService.RenderedFrame>> framesByLayer,
            string outputFolder,
            ImageSequenceFormat format = ImageSequenceFormat.Png,
            uint backgroundColor = 0xFFFFFFFF,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (framesByLayer == null || framesByLayer.Count == 0)
                throw new ArgumentException("No layers to export", nameof(framesByLayer));

            var result = new Dictionary<string, List<string>>();
            int totalFrames = 0;
            foreach (var kvp in framesByLayer)
                totalFrames += kvp.Value.Count;

            int processedFrames = 0;
            string extension = GetExtension(format);
            Guid encoderId = GetEncoderId(format);

            LoggingService.Info("Exporting {LayerCount} layers ({TotalFrames} total frames) to {Folder}",
                framesByLayer.Count, totalFrames, outputFolder);

            foreach (var kvp in framesByLayer)
            {
                string layerName = kvp.Key;
                var frames = kvp.Value;

                // Create subfolder for this layer
                string layerFolder = Path.Combine(outputFolder, SanitizeFolderName(layerName));
                Directory.CreateDirectory(layerFolder);

                var layerFiles = new List<string>();

                for (int i = 0; i < frames.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var frame = frames[i];
                    string fileName = $"{layerName}_{i:D5}{extension}";
                    string filePath = Path.Combine(layerFolder, fileName);

                    byte[] pixels = PreparePixelsForFormat(frame.Pixels, frame.Width, frame.Height, format, backgroundColor);
                    await SaveImageAsync(pixels, frame.Width, frame.Height, filePath, encoderId, format);

                    layerFiles.Add(filePath);
                    processedFrames++;
                    progress?.Report((double)processedFrames / totalFrames);
                }

                result[layerName] = layerFiles;
            }

            LoggingService.Info("Exported {LayerCount} layers to {Folder}", result.Count, outputFolder);

            return result;
        }

        //////////////////////////////////////////////////////////////////
        // PRIVATE HELPERS
        //////////////////////////////////////////////////////////////////

        private static string GetExtension(ImageSequenceFormat format)
        {
            return format switch
            {
                ImageSequenceFormat.Png => ".png",
                ImageSequenceFormat.Jpeg => ".jpg",
                ImageSequenceFormat.Bmp => ".bmp",
                _ => ".png"
            };
        }

        private static Guid GetEncoderId(ImageSequenceFormat format)
        {
            return format switch
            {
                ImageSequenceFormat.Png => BitmapEncoder.PngEncoderId,
                ImageSequenceFormat.Jpeg => BitmapEncoder.JpegEncoderId,
                ImageSequenceFormat.Bmp => BitmapEncoder.BmpEncoderId,
                _ => BitmapEncoder.PngEncoderId
            };
        }

        private static byte[] PreparePixelsForFormat(byte[] pixels, int width, int height, ImageSequenceFormat format, uint backgroundColor)
        {
            // PNG supports alpha, so return as-is
            if (format == ImageSequenceFormat.Png)
                return pixels;

            // JPEG/BMP need alpha composited over background
            byte[] result = new byte[pixels.Length];

            byte bgB = (byte)(backgroundColor & 0xFF);
            byte bgG = (byte)((backgroundColor >> 8) & 0xFF);
            byte bgR = (byte)((backgroundColor >> 16) & 0xFF);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i + 0];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                byte a = pixels[i + 3];

                if (a == 0)
                {
                    result[i + 0] = bgB;
                    result[i + 1] = bgG;
                    result[i + 2] = bgR;
                    result[i + 3] = 255;
                }
                else if (a == 255)
                {
                    result[i + 0] = b;
                    result[i + 1] = g;
                    result[i + 2] = r;
                    result[i + 3] = 255;
                }
                else
                {
                    // Alpha blend over background
                    int invA = 255 - a;
                    result[i + 0] = (byte)((b * a + bgB * invA) / 255);
                    result[i + 1] = (byte)((g * a + bgG * invA) / 255);
                    result[i + 2] = (byte)((r * a + bgR * invA) / 255);
                    result[i + 3] = 255;
                }
            }

            return result;
        }

        private static async Task SaveImageAsync(byte[] pixels, int width, int height, string filePath, Guid encoderId, ImageSequenceFormat format)
        {
            using var stream = File.Create(filePath);
            using var randomAccessStream = stream.AsRandomAccessStream();

            var encoder = await BitmapEncoder.CreateAsync(encoderId, randomAccessStream);

            // Set JPEG quality if applicable
            if (format == ImageSequenceFormat.Jpeg)
            {
                var props = new BitmapPropertySet
                {
                    { "ImageQuality", new BitmapTypedValue(0.95f, Windows.Foundation.PropertyType.Single) }
                };
                await encoder.BitmapProperties.SetPropertiesAsync(props);
            }

            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                format == ImageSequenceFormat.Png ? BitmapAlphaMode.Premultiplied : BitmapAlphaMode.Ignore,
                (uint)width,
                (uint)height,
                96, 96,
                pixels);

            await encoder.FlushAsync();
        }

        private static string SanitizeFolderName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
