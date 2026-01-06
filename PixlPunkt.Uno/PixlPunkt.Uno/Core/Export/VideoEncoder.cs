using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using PixlPunkt.Uno.Core.Audio;
using PixlPunkt.Uno.Core.Logging;

namespace PixlPunkt.Uno.Core.Export
{
    /// <summary>
    /// Supported video export formats.
    /// </summary>
    public enum VideoFormat
    {
        /// <summary>MP4 with H.264 codec (most compatible).</summary>
        Mp4,

        /// <summary>WebM with VP9 codec.</summary>
        WebM,

        /// <summary>AVI container.</summary>
        Avi,

        /// <summary>GIF (animated).</summary>
        Gif,

        /// <summary>MKV with H.265 codec.</summary>
        Mkv
    }

    /// <summary>
    /// Encodes animation frames to video formats using FFmpeg.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FFmpeg binaries are automatically downloaded on first use if not found.
    /// For pixel art, uses nearest-neighbor scaling and high bitrate to preserve crisp edges.
    /// </para>
    /// </remarks>
    public static class VideoEncoder
    {
        /// <summary>
        /// Gets whether video encoding is supported (FFmpeg is available or can be downloaded).
        /// </summary>
        public static bool IsSupported => true; // Always true since we auto-download

        /// <summary>
        /// Gets whether FFmpeg is currently ready (no download needed).
        /// </summary>
        public static bool IsReady => FFmpegService.IsAvailable;

        /// <summary>
        /// Encodes frames to a video file using FFmpeg.
        /// </summary>
        public static async Task EncodeAsync(
            IReadOnlyList<AnimationExportService.RenderedFrame> frames,
            string outputPath,
            VideoFormat format = VideoFormat.Mp4,
            int fps = 0,
            int quality = 80,
            int scale = 1,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (frames == null || frames.Count == 0)
                throw new ArgumentException("No frames to encode", nameof(frames));

            // Ensure FFmpeg is available (auto-download if needed)
            if (!FFmpegService.IsAvailable)
            {
                progress?.Report(0.05);
                var downloadProgress = new Progress<(float p, string s)>(x => progress?.Report(x.p * 0.2));
                bool downloaded = await FFmpegService.EnsureDownloadedAsync(downloadProgress);
                
                if (!downloaded)
                {
                    throw new InvalidOperationException(
                        "FFmpeg is required for video export. Download failed. " +
                        "Please check your internet connection and try again.");
                }
            }

            var firstFrame = frames[0];
            int width = firstFrame.Width * scale;
            int height = firstFrame.Height * scale;

            if (fps <= 0)
            {
                int totalMs = 0;
                foreach (var f in frames)
                    totalMs += f.DurationMs;
                fps = Math.Max(1, (int)Math.Round(frames.Count * 1000.0 / totalMs));
            }

            LoggingService.Info("Encoding video with FFmpeg: {FrameCount} frames, {Width}x{Height}, {FPS} fps, format={Format}, scale={Scale}x",
                frames.Count, width, height, fps, format, scale);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tempDir = Path.Combine(Path.GetTempPath(), $"PixlPunkt_VideoExport_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Save frames as PNG files (20-60% progress)
                progress?.Report(0.2);
                for (int i = 0; i < frames.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var frame = frames[i];
                    var framePath = Path.Combine(tempDir, $"frame_{i:D5}.png");
                    await SaveFrameAsPngAsync(frame, framePath);

                    progress?.Report(0.2 + (0.4 * (i + 1) / frames.Count));
                }

                var inputPattern = Path.Combine(tempDir, "frame_%05d.png");
                
                progress?.Report(0.6);

                var (codec, extraArgs) = GetEncodingSettings(format, quality, scale, width, height);

                // Run FFmpeg (60-100% progress)
                await FFMpegArguments
                    .FromFileInput(inputPattern, verifyExists: false, options => options
                        .WithFramerate(fps))
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        options.WithFramerate(fps);

                        if (!string.IsNullOrEmpty(codec))
                        {
                            options.WithVideoCodec(codec);
                        }

                        if (scale > 1)
                        {
                            options.WithCustomArgument($"-vf \"scale={width}:{height}:flags=neighbor\"");
                        }

                        if (!string.IsNullOrEmpty(extraArgs))
                        {
                            options.WithCustomArgument(extraArgs);
                        }

                        if (format != VideoFormat.Gif)
                        {
                            options.WithCustomArgument("-pix_fmt yuv420p");
                        }
                    })
                    .ProcessAsynchronously();

                progress?.Report(1.0);
                LoggingService.Info("Video encoding complete: {Path}", outputPath);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Debug("Failed to clean up temp folder: {Error}", ex.Message);
                }
            }
        }

        private static (string? codec, string? extraArgs) GetEncodingSettings(
            VideoFormat format, int quality, int scale, int width, int height)
        {
            int crf = Math.Max(0, Math.Min(51, 51 - (quality * 51 / 100)));

            return format switch
            {
                VideoFormat.Mp4 => ("libx264", $"-crf {crf} -preset slow -tune animation"),
                VideoFormat.WebM => ("libvpx-vp9", $"-crf {crf} -b:v 0 -deadline good"),
                VideoFormat.Mkv => ("libx265", $"-crf {crf} -preset slow"),
                VideoFormat.Avi => ("mpeg4", $"-q:v {Math.Max(1, 31 - (quality * 30 / 100))}"),
                VideoFormat.Gif => (null, $"-vf \"fps={30},split[s0][s1];[s0]palettegen=max_colors=256:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=5\""),
                _ => ("libx264", $"-crf {crf}")
            };
        }

        private static async Task SaveFrameAsPngAsync(AnimationExportService.RenderedFrame frame, string path)
        {
            using var fileStream = File.Create(path);
            await WritePngAsync(fileStream, frame.Pixels, frame.Width, frame.Height);
        }

        private static async Task WritePngAsync(Stream stream, byte[] pixels, int width, int height)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            WriteChunk(writer, "IHDR", w =>
            {
                w.Write(ToBigEndian(width));
                w.Write(ToBigEndian(height));
                w.Write((byte)8);
                w.Write((byte)6);
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((byte)0);
            });

            using var compressedStream = new MemoryStream();
            using (var deflate = new System.IO.Compression.DeflateStream(compressedStream, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
            {
                for (int y = 0; y < height; y++)
                {
                    deflate.WriteByte(0);
                    for (int x = 0; x < width; x++)
                    {
                        int idx = (y * width + x) * 4;
                        deflate.WriteByte(pixels[idx + 2]);
                        deflate.WriteByte(pixels[idx + 1]);
                        deflate.WriteByte(pixels[idx + 0]);
                        deflate.WriteByte(pixels[idx + 3]);
                    }
                }
            }

            var compressedData = compressedStream.ToArray();
            var zlibData = new byte[compressedData.Length + 6];
            zlibData[0] = 0x78;
            zlibData[1] = 0x9C;
            Array.Copy(compressedData, 0, zlibData, 2, compressedData.Length);
            
            uint adler = ComputeAdler32(pixels, width, height);
            zlibData[^4] = (byte)(adler >> 24);
            zlibData[^3] = (byte)(adler >> 16);
            zlibData[^2] = (byte)(adler >> 8);
            zlibData[^1] = (byte)adler;

            WriteChunk(writer, "IDAT", w => w.Write(zlibData));
            WriteChunk(writer, "IEND", _ => { });

            ms.Position = 0;
            await ms.CopyToAsync(stream);
        }

        private static void WriteChunk(BinaryWriter writer, string type, Action<BinaryWriter> writeData)
        {
            using var dataStream = new MemoryStream();
            using var dataWriter = new BinaryWriter(dataStream);
            writeData(dataWriter);
            var data = dataStream.ToArray();

            writer.Write(ToBigEndian(data.Length));
            writer.Write(System.Text.Encoding.ASCII.GetBytes(type));
            writer.Write(data);
            writer.Write(ToBigEndian((int)ComputeCrc32(type, data)));
        }

        private static int ToBigEndian(int value) => System.Net.IPAddress.HostToNetworkOrder(value);

        private static uint ComputeCrc32(string type, byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (var b in System.Text.Encoding.ASCII.GetBytes(type))
                crc = UpdateCrc32(crc, b);
            foreach (var b in data)
                crc = UpdateCrc32(crc, b);
            return crc ^ 0xFFFFFFFF;
        }

        private static uint UpdateCrc32(uint crc, byte b)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            return crc;
        }

        private static uint ComputeAdler32(byte[] pixels, int width, int height)
        {
            uint a = 1, b = 0;
            for (int y = 0; y < height; y++)
            {
                a = (a + 0) % 65521; b = (b + a) % 65521;
                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width + x) * 4;
                    a = (a + pixels[idx + 2]) % 65521; b = (b + a) % 65521;
                    a = (a + pixels[idx + 1]) % 65521; b = (b + a) % 65521;
                    a = (a + pixels[idx + 0]) % 65521; b = (b + a) % 65521;
                    a = (a + pixels[idx + 3]) % 65521; b = (b + a) % 65521;
                }
            }
            return (b << 16) | a;
        }

        /// <summary>
        /// Gets the file extension for a video format.
        /// </summary>
        public static string GetExtension(VideoFormat format) => format switch
        {
            VideoFormat.Mp4 => ".mp4",
            VideoFormat.WebM => ".webm",
            VideoFormat.Avi => ".avi",
            VideoFormat.Gif => ".gif",
            VideoFormat.Mkv => ".mkv",
            _ => ".mp4"
        };

        /// <summary>
        /// Gets supported video formats.
        /// </summary>
        public static IReadOnlyList<(VideoFormat format, string displayName)> GetSupportedFormats() =>
            new List<(VideoFormat, string)>
            {
                (VideoFormat.Mp4, "MP4 (H.264) - Most Compatible"),
                (VideoFormat.WebM, "WebM (VP9) - Web Optimized"),
                (VideoFormat.Gif, "GIF - Animated"),
                (VideoFormat.Mkv, "MKV (H.265) - High Quality"),
                (VideoFormat.Avi, "AVI (MPEG4)")
            };

        /// <summary>
        /// Gets the FFmpeg status message for UI display.
        /// </summary>
        public static string GetStatusMessage() => FFmpegService.GetStatusMessage();
    }
}
