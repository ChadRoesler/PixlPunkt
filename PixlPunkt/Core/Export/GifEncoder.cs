using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using PixlPunkt.Core.Logging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PixlPunkt.Core.Export
{
    /// <summary>
    /// Encodes animation frames to GIF format.
    /// Uses Windows BitmapEncoder with GIF support.
    /// </summary>
    public static class GifEncoder
    {
        /// <summary>
        /// Encodes frames to an animated GIF.
        /// </summary>
        /// <param name="frames">List of rendered frames to encode.</param>
        /// <param name="outputPath">Path to save the GIF file.</param>
        /// <param name="loop">Whether the GIF should loop (0 = infinite loop).</param>
        /// <param name="progress">Optional progress callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task EncodeAsync(
            IReadOnlyList<AnimationExportService.RenderedFrame> frames,
            string outputPath,
            bool loop = true,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (frames == null || frames.Count == 0)
                throw new ArgumentException("No frames to encode", nameof(frames));

            LoggingService.Info("Encoding GIF with {FrameCount} frames to {Path}", frames.Count, outputPath);

            // Ensure directory exists
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var fileStream = File.Create(outputPath);
            using var stream = fileStream.AsRandomAccessStream();

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.GifEncoderId, stream);

            for (int i = 0; i < frames.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var frame = frames[i];

                // Set pixel data
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)frame.Width,
                    (uint)frame.Height,
                    96, 96,
                    frame.Pixels);

                // Set frame delay (GIF uses centiseconds)
                var delayProperty = new BitmapTypedValue(
                    (ushort)(frame.DurationMs / 10), // Convert ms to centiseconds
                    Windows.Foundation.PropertyType.UInt16);

                // For animated GIF, we need to set properties
                var properties = new BitmapPropertySet
                {
                    { "/grctlext/Delay", delayProperty }
                };

                if (i == 0 && loop)
                {
                    // Set loop count on first frame (0 = infinite)
                    properties.Add("/appext/Application", new BitmapTypedValue(
                        System.Text.Encoding.UTF8.GetBytes("NETSCAPE2.0"),
                        Windows.Foundation.PropertyType.UInt8Array));
                    properties.Add("/appext/Data", new BitmapTypedValue(
                        new byte[] { 3, 1, 0, 0 }, // Loop forever
                        Windows.Foundation.PropertyType.UInt8Array));
                }

                try
                {
                    await encoder.BitmapProperties.SetPropertiesAsync(properties);
                }
                catch
                {
                    // Some properties may not be supported, continue anyway
                }

                if (i < frames.Count - 1)
                {
                    await encoder.GoToNextFrameAsync();
                }

                progress?.Report((double)(i + 1) / frames.Count);
            }

            await encoder.FlushAsync();

            LoggingService.Info("GIF encoding complete: {Path}", outputPath);
        }

        /// <summary>
        /// Encodes frames to a GIF byte array (for in-memory use).
        /// </summary>
        public static async Task<byte[]> EncodeToBytesAsync(
            IReadOnlyList<AnimationExportService.RenderedFrame> frames,
            bool loop = true,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (frames == null || frames.Count == 0)
                throw new ArgumentException("No frames to encode", nameof(frames));

            using var stream = new InMemoryRandomAccessStream();

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.GifEncoderId, stream);

            for (int i = 0; i < frames.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var frame = frames[i];

                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)frame.Width,
                    (uint)frame.Height,
                    96, 96,
                    frame.Pixels);

                var delayProperty = new BitmapTypedValue(
                    (ushort)(frame.DurationMs / 10),
                    Windows.Foundation.PropertyType.UInt16);

                var properties = new BitmapPropertySet
                {
                    { "/grctlext/Delay", delayProperty }
                };

                if (i == 0 && loop)
                {
                    properties.Add("/appext/Application", new BitmapTypedValue(
                        System.Text.Encoding.UTF8.GetBytes("NETSCAPE2.0"),
                        Windows.Foundation.PropertyType.UInt8Array));
                    properties.Add("/appext/Data", new BitmapTypedValue(
                        new byte[] { 3, 1, 0, 0 },
                        Windows.Foundation.PropertyType.UInt8Array));
                }

                try
                {
                    await encoder.BitmapProperties.SetPropertiesAsync(properties);
                }
                catch { }

                if (i < frames.Count - 1)
                {
                    await encoder.GoToNextFrameAsync();
                }

                progress?.Report((double)(i + 1) / frames.Count);
            }

            await encoder.FlushAsync();

            // Read bytes from stream
            stream.Seek(0);
            var buffer = new byte[stream.Size];
            await stream.ReadAsync(buffer.AsBuffer(), (uint)buffer.Length, InputStreamOptions.None);

            return buffer;
        }
    }
}
