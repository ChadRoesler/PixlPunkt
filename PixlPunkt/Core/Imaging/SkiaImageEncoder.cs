using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace PixlPunkt.Core.Imaging
{
    /// <summary>
    /// Cross-platform image encoding using SkiaSharp.
    /// Replaces Windows.Graphics.Imaging.BitmapEncoder for cross-platform support.
    /// </summary>
    public static class SkiaImageEncoder
    {
        /// <summary>
        /// Supported image formats for encoding.
        /// </summary>
        public enum ImageFormat
        {
            Png,
            Jpeg,
            Webp,
            Bmp
        }

        /// <summary>
        /// Encodes BGRA pixel data to an image file.
        /// </summary>
        /// <param name="pixels">BGRA pixel data (length = width * height * 4).</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <param name="outputPath">Output file path.</param>
        /// <param name="format">Image format to use.</param>
        /// <param name="quality">Quality (0-100) for lossy formats like JPEG/WebP.</param>
        public static void Encode(byte[] pixels, int width, int height, string outputPath, ImageFormat format = ImageFormat.Png, int quality = 95)
        {
            using var bitmap = CreateBitmapFromBgra(pixels, width, height);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = EncodeImage(image, format, quality);
            
            using var fileStream = File.Create(outputPath);
            data.SaveTo(fileStream);
        }

        /// <summary>
        /// Encodes BGRA pixel data to a byte array.
        /// </summary>
        public static byte[] EncodeToBytes(byte[] pixels, int width, int height, ImageFormat format = ImageFormat.Png, int quality = 95)
        {
            using var bitmap = CreateBitmapFromBgra(pixels, width, height);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = EncodeImage(image, format, quality);
            return data.ToArray();
        }

        /// <summary>
        /// Encodes BGRA pixel data to a stream.
        /// </summary>
        public static void EncodeToStream(byte[] pixels, int width, int height, Stream outputStream, ImageFormat format = ImageFormat.Png, int quality = 95)
        {
            using var bitmap = CreateBitmapFromBgra(pixels, width, height);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = EncodeImage(image, format, quality);
            data.SaveTo(outputStream);
        }

        /// <summary>
        /// Encodes an SKBitmap to a file.
        /// </summary>
        public static void Encode(SKBitmap bitmap, string outputPath, ImageFormat format = ImageFormat.Png, int quality = 95)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = EncodeImage(image, format, quality);
            
            using var fileStream = File.Create(outputPath);
            data.SaveTo(fileStream);
        }

        /// <summary>
        /// Creates an SKBitmap from BGRA pixel data.
        /// </summary>
        public static SKBitmap CreateBitmapFromBgra(byte[] pixels, int width, int height)
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var bitmap = new SKBitmap(info);
            
            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    bitmap.SetPixels((IntPtr)ptr);
                }
            }
            
            // Copy pixels to ensure the bitmap owns the data
            var copy = new SKBitmap(info);
            using (var canvas = new SKCanvas(copy))
            {
                canvas.DrawBitmap(bitmap, 0, 0);
            }
            bitmap.Dispose();
            
            return copy;
        }

        /// <summary>
        /// Decodes an image file to BGRA pixel data.
        /// </summary>
        /// <param name="filePath">Path to the image file.</param>
        /// <returns>Tuple of (pixels, width, height).</returns>
        public static (byte[] pixels, int width, int height) Decode(string filePath)
        {
            using var bitmap = SKBitmap.Decode(filePath);
            if (bitmap == null)
                throw new InvalidOperationException($"Failed to decode image: {filePath}");

            return DecodeBitmap(bitmap);
        }

        /// <summary>
        /// Decodes an image from a byte array to BGRA pixel data.
        /// </summary>
        public static (byte[] pixels, int width, int height) DecodeFromBytes(byte[] data)
        {
            using var bitmap = SKBitmap.Decode(data);
            if (bitmap == null)
                throw new InvalidOperationException("Failed to decode image from bytes");

            return DecodeBitmap(bitmap);
        }

        /// <summary>
        /// Decodes an image from a stream to BGRA pixel data.
        /// </summary>
        public static (byte[] pixels, int width, int height) DecodeFromStream(Stream stream)
        {
            using var bitmap = SKBitmap.Decode(stream);
            if (bitmap == null)
                throw new InvalidOperationException("Failed to decode image from stream");

            return DecodeBitmap(bitmap);
        }

        /// <summary>
        /// Gets the file extension for a format.
        /// </summary>
        public static string GetExtension(ImageFormat format) => format switch
        {
            ImageFormat.Png => ".png",
            ImageFormat.Jpeg => ".jpg",
            ImageFormat.Webp => ".webp",
            ImageFormat.Bmp => ".bmp",
            _ => ".png"
        };

        private static SKData EncodeImage(SKImage image, ImageFormat format, int quality)
        {
            var skFormat = format switch
            {
                ImageFormat.Png => SKEncodedImageFormat.Png,
                ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
                ImageFormat.Webp => SKEncodedImageFormat.Webp,
                ImageFormat.Bmp => SKEncodedImageFormat.Bmp,
                _ => SKEncodedImageFormat.Png
            };

            return image.Encode(skFormat, quality);
        }

        private static (byte[] pixels, int width, int height) DecodeBitmap(SKBitmap bitmap)
        {
            // Convert to BGRA8888 if needed
            if (bitmap.ColorType != SKColorType.Bgra8888)
            {
                var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var converted = new SKBitmap(info);
                
                using (var canvas = new SKCanvas(converted))
                {
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                
                bitmap.Dispose();
                bitmap = converted;
            }

            var pixels = new byte[bitmap.Width * bitmap.Height * 4];
            var ptr = bitmap.GetPixels();
            System.Runtime.InteropServices.Marshal.Copy(ptr, pixels, 0, pixels.Length);

            return (pixels, bitmap.Width, bitmap.Height);
        }
    }
}
