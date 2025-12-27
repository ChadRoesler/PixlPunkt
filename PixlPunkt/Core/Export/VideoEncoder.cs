using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PixlPunkt.Core.Logging;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PixlPunkt.Core.Export
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

        /// <summary>WMV (Windows Media Video).</summary>
        Wmv
    }

    /// <summary>
    /// Encodes animation frames to video formats using Windows Media Foundation.
    /// </summary>
    public static class VideoEncoder
    {
        /// <summary>
        /// Encodes frames to a video file.
        /// </summary>
        /// <param name="frames">List of rendered frames.</param>
        /// <param name="outputPath">Output video file path.</param>
        /// <param name="format">Video format to use.</param>
        /// <param name="fps">Frames per second (if 0, uses frame durations).</param>
        /// <param name="quality">Video quality (0-100).</param>
        /// <param name="progress">Optional progress callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task EncodeAsync(
            IReadOnlyList<AnimationExportService.RenderedFrame> frames,
            string outputPath,
            VideoFormat format = VideoFormat.Mp4,
            int fps = 0,
            int quality = 80,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (frames == null || frames.Count == 0)
                throw new ArgumentException("No frames to encode", nameof(frames));

            var firstFrame = frames[0];
            int width = firstFrame.Width;
            int height = firstFrame.Height;

            // Calculate effective FPS
            if (fps <= 0)
            {
                // Use average frame duration
                int totalMs = 0;
                foreach (var f in frames)
                    totalMs += f.DurationMs;
                fps = Math.Max(1, (int)Math.Round(frames.Count * 1000.0 / totalMs));
            }

            LoggingService.Info("Encoding video: {FrameCount} frames, {Width}x{Height}, {FPS} fps, format={Format}",
                frames.Count, width, height, fps, format);

            // Ensure output directory exists
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Use Windows.Media.Editing (MediaComposition)
            await EncodeWithMediaCompositionAsync(frames, outputPath, format, fps, quality, progress, cancellationToken);
        }

        /// <summary>
        /// Encodes using Windows.Media.Editing API (MediaComposition).
        /// </summary>
        private static async Task EncodeWithMediaCompositionAsync(
            IReadOnlyList<AnimationExportService.RenderedFrame> frames,
            string outputPath,
            VideoFormat format,
            int fps,
            int quality,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var firstFrame = frames[0];
            int width = firstFrame.Width;
            int height = firstFrame.Height;

            // Create a MediaComposition
            var composition = new Windows.Media.Editing.MediaComposition();

            // We need to create temporary image files for each frame
            var tempFolder = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(
                $"VideoExport_{Guid.NewGuid():N}",
                CreationCollisionOption.ReplaceExisting);

            var tempFiles = new List<StorageFile>();

            try
            {
                // Save each frame as a PNG
                for (int i = 0; i < frames.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var frame = frames[i];
                    var pngFile = await tempFolder.CreateFileAsync($"frame_{i:D5}.png", CreationCollisionOption.ReplaceExisting);
                    tempFiles.Add(pngFile);

                    await SaveFrameAsPngAsync(frame, pngFile);

                    progress?.Report((double)(i + 1) / (frames.Count * 2)); // First half is frame creation
                }

                // Add each frame as a clip
                double frameDurationTicks = 10_000_000.0 / fps; // Duration in 100-nanosecond units

                for (int i = 0; i < tempFiles.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var imageFile = tempFiles[i];
                    var frame = frames[i];

                    // Calculate duration for this frame
                    double durationTicks = fps > 0
                        ? frameDurationTicks
                        : frame.DurationMs * 10_000.0; // ms to 100-ns

                    var clip = await Windows.Media.Editing.MediaClip.CreateFromImageFileAsync(
                        imageFile,
                        TimeSpan.FromTicks((long)durationTicks));

                    composition.Clips.Add(clip);
                }

                // Create output file using direct file creation (more reliable than StorageFile APIs for arbitrary paths)
                StorageFile outputFile;
                try
                {
                    // First, ensure the file exists by creating it with System.IO
                    // This handles paths that aren't in known folders
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                    
                    // Create empty file
                    await using (File.Create(outputPath)) { }
                    
                    // Now get the StorageFile reference
                    outputFile = await StorageFile.GetFileFromPathAsync(outputPath);
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to create output file at {Path}: {Error}", outputPath, ex.Message);
                    throw new IOException($"Cannot create output file at '{outputPath}'. Please ensure the path is valid and you have write permissions.", ex);
                }

                // Configure encoding profile
                // IMPORTANT: Set output dimensions to match frame dimensions exactly
                // This prevents MediaComposition from scaling (which uses bilinear interpolation)
                // and preserves crisp pixel art edges
                var profile = GetEncodingProfile(format, width, height, fps, quality);

                // Render the composition
                var renderOp = composition.RenderToFileAsync(outputFile,
                    Windows.Media.Editing.MediaTrimmingPreference.Precise,
                    profile);

                renderOp.Progress = (info, progressValue) =>
                {
                    progress?.Report(0.5 + progressValue / 200.0); // Second half is rendering
                };

                var result = await renderOp;

                if (result != Windows.Media.Transcoding.TranscodeFailureReason.None)
                {
                    throw new Exception($"Video encoding failed: {result}");
                }

                LoggingService.Info("Video encoding complete: {Path}", outputPath);
            }
            finally
            {
                // Clean up temp files
                try
                {
                    await tempFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
                catch (Exception ex)
                {
                    LoggingService.Debug("Failed to clean up temp folder: {Error}", ex.Message);
                }
            }
        }

        private static async Task SaveFrameAsPngAsync(AnimationExportService.RenderedFrame frame, StorageFile file)
        {
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);

            encoder.SetPixelData(
                Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                (uint)frame.Width,
                (uint)frame.Height,
                96, 96,
                frame.Pixels);

            await encoder.FlushAsync();
        }

        private static MediaEncodingProfile GetEncodingProfile(VideoFormat format, int width, int height, int fps, int quality)
        {
            MediaEncodingProfile profile;

            // Map quality (0-100) to video bitrate
            // For pixel art, use higher bitrate to preserve sharp edges
            uint baseBitrate = (uint)(width * height * fps / 5); // Higher base for crisp output
            uint bitrate = (uint)(baseBitrate * (0.5 + quality / 100.0)); // Scale by quality
            bitrate = Math.Max(1_000_000u, Math.Min(bitrate, 100_000_000u)); // Higher minimum for quality

            switch (format)
            {
                case VideoFormat.Mp4:
                    profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                    break;

                case VideoFormat.Wmv:
                    profile = MediaEncodingProfile.CreateWmv(VideoEncodingQuality.Auto);
                    break;

                case VideoFormat.Avi:
                    profile = MediaEncodingProfile.CreateAvi(VideoEncodingQuality.Auto);
                    break;

                case VideoFormat.WebM:
                    // WebM isn't directly supported, use MP4 as fallback
                    profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                    break;

                default:
                    profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                    break;
            }

            // Override video properties - MUST match frame dimensions exactly for crisp pixel art
            if (profile.Video != null)
            {
                profile.Video.Width = (uint)width;
                profile.Video.Height = (uint)height;
                profile.Video.Bitrate = bitrate;
                profile.Video.FrameRate.Numerator = (uint)fps;
                profile.Video.FrameRate.Denominator = 1;
            }

            // Remove audio (we're exporting animation, not video with audio)
            profile.Audio = null;

            return profile;
        }

        /// <summary>
        /// Gets the file extension for a video format.
        /// </summary>
        public static string GetExtension(VideoFormat format)
        {
            return format switch
            {
                VideoFormat.Mp4 => ".mp4",
                VideoFormat.WebM => ".webm",
                VideoFormat.Avi => ".avi",
                VideoFormat.Wmv => ".wmv",
                _ => ".mp4"
            };
        }

        /// <summary>
        /// Gets supported video formats for the current system.
        /// </summary>
        public static IReadOnlyList<(VideoFormat format, string displayName)> GetSupportedFormats()
        {
            return new List<(VideoFormat, string)>
            {
                (VideoFormat.Mp4, "MP4 (H.264)"),
                (VideoFormat.Wmv, "Windows Media Video"),
                (VideoFormat.Avi, "AVI")
            };
        }
    }
}
