using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.Imaging;
using PixlPunkt.Uno.Core.Logging;

namespace PixlPunkt.Uno.Core.FIleOps
{
    /// <summary>
    /// Provides image export functionality for saving <see cref="PixelSurface"/> data to various file formats.
    /// </summary>
    public static class ImageExport
    {
        /// <summary>
        /// Saves a pixel surface to a file in the specified image format.
        /// </summary>
        /// <param name="surface">The pixel surface to export.</param>
        /// <param name="path">Target file path. Directory is created if it doesn't exist.</param>
        /// <param name="format">Output format (<see cref="ImageFileFormat"/>).</param>
        /// <param name="hotspotX">Cursor hotspot X coordinate (only used for <see cref="ImageFileFormat.Cur"/>).
        /// Default is 0 (left edge).</param>
        /// <param name="hotspotY">Cursor hotspot Y coordinate (only used for <see cref="ImageFileFormat.Cur"/>).
        /// Default is 0 (top edge).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if format is not recognized.</exception>
        /// <remarks>
        /// Automatically creates parent directories if they don't exist. For JPEG exports, alpha is
        /// flattened over white background. For cursor exports, hotspot coordinates define the click point.
        /// </remarks>
        public static void Save(
            PixelSurface surface,
            string path,
            ImageFileFormat format,
            ushort hotspotX = 0,
            ushort hotspotY = 0)
        {
            try
            {
                LoggingService.Info("Export image start path={Path} format={Format} size={W}x{H}", path, format.ToString(), surface.Width, surface.Height);

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                switch (format)
                {
                    case ImageFileFormat.Png:
                        SaveWithGdi(surface, path, ImageFormat.Png);
                        break;

                    case ImageFileFormat.Bmp:
                        SaveWithGdi(surface, path, ImageFormat.Bmp);
                        break;

                    case ImageFileFormat.Tiff:
                        SaveWithGdi(surface, path, ImageFormat.Tiff);
                        break;

                    case ImageFileFormat.Jpeg:
                        SaveJpegFlattenWhite(surface, path);
                        break;

                    case ImageFileFormat.Cur:
                        SaveAsCursor(surface, path, hotspotX, hotspotY);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(format), format, null);
                }

                LoggingService.Info("Export image complete path={Path} format={Format}", path, format.ToString());
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to export image to {path}", ex);
                throw;
            }
        }

        /// <summary>
        /// Saves a surface to a file using GDI+ <see cref="ImageFormat"/>.
        /// </summary>
        /// <param name="surface">Source pixel surface (BGRA format).</param>
        /// <param name="path">Output file path.</param>
        /// <param name="format">GDI+ image format (Png, Bmp, Tiff, etc.).</param>
        /// <remarks>
        /// <para>
        /// Uses unsafe memory copy to directly transfer BGRA pixels from <see cref="PixelSurface"/>
        /// to locked <see cref="Bitmap"/> bits. Handles stride differences between source (always width × 4)
        /// and destination (may have padding for DWORD alignment).
        /// </para>
        /// <para>
        /// PixelSurface uses BGRA byte order which matches Format32bppArgb in memory on little-endian systems,
        /// enabling efficient direct copy without per-pixel conversion.
        /// </para>
        /// </remarks>
        private static void SaveWithGdi(PixelSurface surface, string path, ImageFormat format)
        {
            int w = surface.Width;
            int h = surface.Height;
            var pixels = surface.Pixels; // BGRA

            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                fixed (byte* srcBase = pixels)
                {
                    byte* dstBase = (byte*)data.Scan0;
                    int srcStride = w * 4;

                    for (int y = 0; y < h; y++)
                    {
                        byte* srcRow = srcBase + y * srcStride;
                        byte* dstRow = dstBase + y * data.Stride;
                        Buffer.MemoryCopy(srcRow, dstRow, data.Stride, srcStride);
                    }
                }
            }

            bmp.UnlockBits(data);
            bmp.Save(path, format);

            LoggingService.Info("Saved image {Path} format={Format} {W}x{H}", path, format.ToString(), w, h);
        }

        /// <summary>
        /// Saves a surface as JPEG with alpha channel flattened over white background.
        /// </summary>
        /// <param name="surface">Source pixel surface (BGRA with alpha).</param>
        /// <param name="path">Output JPEG file path.</param>
        /// <remarks>
        /// <para><strong>Alpha Flattening Algorithm:</strong></para>
        /// <para>
        /// For each pixel, computes: <c>result = color × alpha + white × (1 - alpha)</c>
        /// <br/>This prevents dark halos around semi-transparent edges that would appear if alpha
        /// were simply discarded. The result is a solid opaque image (alpha=255) with color values
        /// pre-composited against white.
        /// </para>
        /// <para><strong>JPEG Quality:</strong></para>
        /// <para>
        /// Uses quality setting of 95 (scale 0-100) for high-quality output with minimal compression
        /// artifacts. This balances file size with visual fidelity suitable for pixel art where
        /// compression artifacts are especially visible.
        /// </para>
        /// </remarks>
        private static void SaveJpegFlattenWhite(PixelSurface surface, string path)
        {
            int w = surface.Width;
            int h = surface.Height;
            var pixels = surface.Pixels;

            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* dstBase = (byte*)data.Scan0;
                int dstStride = data.Stride;

                for (int y = 0; y < h; y++)
                {
                    byte* dstRow = dstBase + y * dstStride;

                    for (int x = 0; x < w; x++)
                    {
                        int srcIndex = (y * w + x) * 4;
                        byte b = pixels[srcIndex + 0];
                        byte g = pixels[srcIndex + 1];
                        byte r = pixels[srcIndex + 2];
                        byte a = pixels[srcIndex + 3];

                        float af = a / 255f;
                        // flatten over white
                        byte fr = (byte)(r * af + 255f * (1f - af));
                        byte fg = (byte)(g * af + 255f * (1f - af));
                        byte fb = (byte)(b * af + 255f * (1f - af));

                        int dstIndex = x * 4;
                        dstRow[dstIndex + 0] = fb;
                        dstRow[dstIndex + 1] = fg;
                        dstRow[dstIndex + 2] = fr;
                        dstRow[dstIndex + 3] = 255;
                    }
                }
            }

            bmp.UnlockBits(data);

            // Optional: set JPEG quality
            var encoder = GetEncoder(ImageFormat.Jpeg);
            if (encoder != null)
            {
                var qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L);
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = qualityParam;
                bmp.Save(path, encoder, encoderParams);
            }
            else
            {
                bmp.Save(path, ImageFormat.Jpeg);
            }

            LoggingService.Info("Saved JPEG {Path} {W}x{H}", path, w, h);
        }

        /// <summary>
        /// Gets the image encoder for a specific format.
        /// </summary>
        /// <param name="format">Target image format.</param>
        /// <returns>Matching <see cref="ImageCodecInfo"/> or null if not found.</returns>
        /// <remarks>
        /// Used to access encoder-specific parameters like JPEG quality settings.
        /// </remarks>
        private static ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var c in codecs)
            {
                if (c.FormatID == format.Guid)
                    return c;
            }
            return null;
        }

        /// <summary>
        /// Saves a surface as a Windows cursor (.cur) file with embedded hotspot coordinates.
        /// </summary>
        /// <param name="surface">Source pixel surface (BGRA format).</param>
        /// <param name="path">Output .cur file path.</param>
        /// <param name="hotspotX">Horizontal hotspot position in pixels from left edge.</param>
        /// <param name="hotspotY">Vertical hotspot position in pixels from top edge.</param>
        /// <remarks>
        /// <para><strong>Cursor File Structure:</strong></para>
        /// <code>
        /// ICONDIR (6 bytes):
        ///   WORD reserved (0)
        ///   WORD type (2 = cursor)
        ///   WORD count (1 = single image)
        /// 
        /// CURSORDIRENTRY (16 bytes):
        ///   BYTE width (0 = 256)
        ///   BYTE height (0 = 256)
        ///   BYTE colorCount (0 for 32bpp)
        ///   BYTE reserved (0)
        ///   WORD hotspotX
        ///   WORD hotspotY
        ///   DWORD bytesInRes (DIB size)
        ///   DWORD imageOffset (22 = header + entry)
        /// 
        /// DIB (Device Independent Bitmap):
        ///   BITMAPINFOHEADER (40 bytes) with biHeight = height × 2
        ///   Pixel data: bottom-up BGRA (width × height × 4 bytes)
        ///   AND mask: 1bpp, all zeros (width_aligned × height bits)
        /// </code>
        /// <para><strong>Hotspot Coordinates:</strong></para>
        /// <para>
        /// The hotspot defines the precise pixel that represents the cursor's "click point". For example,
        /// an arrow cursor might have hotspot at (0,0) for top-left tip, while a crosshair would use
        /// (width/2, height/2) for center point.
        /// </para>
        /// <para><strong>Compatibility Notes:</strong></para>
        /// <para>
        /// Uses 32bpp BGRA format with alpha channel for modern Windows cursor support. The AND mask
        /// is all zeros since alpha provides transparency. Bottom-up DIB orientation matches
        /// Windows bitmap conventions.
        /// </para>
        /// </remarks>
        private static void SaveAsCursor(
            PixelSurface surface,
            string path,
            ushort hotspotX,
            ushort hotspotY)
        {
            int w = surface.Width;
            int h = surface.Height;
            var pixels = surface.Pixels; // BGRA, top-down

            using var fs = File.Create(path);
            using var bw = new BinaryWriter(fs);

            // ICONDIR
            bw.Write((ushort)0);   // reserved
            bw.Write((ushort)2);   // type: 2 = cursor
            bw.Write((ushort)1);   // count

            // ICONDIRENTRY / CURSORDIRENTRY
            byte widthByte = (byte)(w >= 256 ? 0 : w);
            byte heightByte = (byte)(h >= 256 ? 0 : h);

            bw.Write(widthByte);   // width
            bw.Write(heightByte);  // height
            bw.Write((byte)0);     // color count
            bw.Write((byte)0);     // reserved

            bw.Write(hotspotX);    // hotspot x
            bw.Write(hotspotY);    // hotspot y

            // We'll compute bytesInRes after building the image (but we already know size)
            // Compute DIB size
            int headerSize = 40;
            int pixelDataSize = w * h * 4;
            int andStride = ((w + 31) / 32) * 4; // 1bpp mask stride (DWORD aligned)
            int andMaskSize = andStride * h;
            int bytesInRes = headerSize + pixelDataSize + andMaskSize;

            bw.Write(bytesInRes);         // bytes in resource
            bw.Write(22);                 // image offset (6 header + 16 entry)

            // === DIB ===
            // BITMAPINFOHEADER
            bw.Write(headerSize);         // biSize
            bw.Write(w);                  // biWidth
            bw.Write(h * 2);              // biHeight (color + mask)
            bw.Write((ushort)1);          // biPlanes
            bw.Write((ushort)32);         // biBitCount
            bw.Write(0);                  // biCompression = BI_RGB
            bw.Write(pixelDataSize);      // biSizeImage
            bw.Write(0);                  // biXPelsPerMeter
            bw.Write(0);                  // biYPelsPerMeter
            bw.Write(0);                  // biClrUsed
            bw.Write(0);                  // biClrImportant

            // Pixel data: bottom-up BGRA
            for (int y = h - 1; y >= 0; y--)
            {
                int rowIndex = y * w * 4;
                bw.Write(pixels, rowIndex, w * 4);
            }

            // AND mask: all 0 (we rely on alpha channel)
            byte[] maskRow = new byte[andStride];
            for (int y = 0; y < h; y++)
            {
                bw.Write(maskRow);
            }

            LoggingService.Info("Saved cursor {Path} {W}x{H} hotspot={HotX},{HotY}", path, w, h, hotspotX, hotspotY);
        }
    }
}
