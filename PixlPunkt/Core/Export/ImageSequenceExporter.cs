using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Logging;

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
        Bmp,

        /// <summary>WebP (with alpha support).</summary>
        WebP
    }

    /// <summary>
    /// Exports animation frames as an image sequence to a folder.
    /// Uses SkiaSharp for cross-platform image encoding.
    /// </summary>
    public static class ImageSequenceExporter
    {
        /// <summary>
        /// Exports frames to a folder as individual images.
        /// </summary>
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

            Directory.CreateDirectory(outputFolder);

            var exportedFiles = new List<string>();
            string extension = GetExtension(format);
            var skiaFormat = GetSkiaFormat(format);
            int quality = format == ImageSequenceFormat.Jpeg ? 95 : 100;

            LoggingService.Info("Exporting {FrameCount} frames to {Folder} as {Format}",
                frames.Count, outputFolder, format);

            await Task.Run(() =>
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var frame = frames[i];
                    string fileName = $"{baseName}_{i:D5}{extension}";
                    string filePath = Path.Combine(outputFolder, fileName);

                    byte[] pixels = PreparePixelsForFormat(frame.Pixels, frame.Width, frame.Height, format, backgroundColor);
                    SkiaImageEncoder.Encode(pixels, frame.Width, frame.Height, filePath, skiaFormat, quality);

                    exportedFiles.Add(filePath);
                    progress?.Report((double)(i + 1) / frames.Count);
                }
            }, cancellationToken);

            LoggingService.Info("Exported {Count} images to {Folder}", exportedFiles.Count, outputFolder);

            return exportedFiles;
        }

        /// <summary>
        /// Exports frames grouped by layer to subfolders.
        /// </summary>
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

            string extension = GetExtension(format);
            var skiaFormat = GetSkiaFormat(format);
            int quality = format == ImageSequenceFormat.Jpeg ? 95 : 100;

            LoggingService.Info("Exporting {LayerCount} layers ({TotalFrames} total frames) to {Folder}",
                framesByLayer.Count, totalFrames, outputFolder);

            int processedFrames = 0;

            await Task.Run(() =>
            {
                foreach (var kvp in framesByLayer)
                {
                    string layerName = kvp.Key;
                    var frames = kvp.Value;

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
                        SkiaImageEncoder.Encode(pixels, frame.Width, frame.Height, filePath, skiaFormat, quality);

                        layerFiles.Add(filePath);
                        processedFrames++;
                        progress?.Report((double)processedFrames / totalFrames);
                    }

                    result[layerName] = layerFiles;
                }
            }, cancellationToken);

            LoggingService.Info("Exported {LayerCount} layers to {Folder}", result.Count, outputFolder);

            return result;
        }

        private static string GetExtension(ImageSequenceFormat format) => format switch
        {
            ImageSequenceFormat.Png => ".png",
            ImageSequenceFormat.Jpeg => ".jpg",
            ImageSequenceFormat.Bmp => ".bmp",
            ImageSequenceFormat.WebP => ".webp",
            _ => ".png"
        };

        private static SkiaImageEncoder.ImageFormat GetSkiaFormat(ImageSequenceFormat format) => format switch
        {
            ImageSequenceFormat.Png => SkiaImageEncoder.ImageFormat.Png,
            ImageSequenceFormat.Jpeg => SkiaImageEncoder.ImageFormat.Jpeg,
            ImageSequenceFormat.Bmp => SkiaImageEncoder.ImageFormat.Bmp,
            ImageSequenceFormat.WebP => SkiaImageEncoder.ImageFormat.Webp,
            _ => SkiaImageEncoder.ImageFormat.Png
        };

        private static byte[] PreparePixelsForFormat(byte[] pixels, int width, int height, ImageSequenceFormat format, uint backgroundColor)
        {
            // PNG and WebP support alpha
            if (format == ImageSequenceFormat.Png || format == ImageSequenceFormat.WebP)
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
                    int invA = 255 - a;
                    result[i + 0] = (byte)((b * a + bgB * invA) / 255);
                    result[i + 1] = (byte)((g * a + bgG * invA) / 255);
                    result[i + 2] = (byte)((r * a + bgR * invA) / 255);
                    result[i + 3] = 255;
                }
            }

            return result;
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
