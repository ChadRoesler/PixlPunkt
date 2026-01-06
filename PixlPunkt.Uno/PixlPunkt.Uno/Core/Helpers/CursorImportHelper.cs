using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace PixlPunkt.Uno.Core.Helpers
{
    /// <summary>
    /// Helper methods for importing Windows cursor (.cur) files.
    /// </summary>
    internal static class CursorImportHelper
    {
        /// <summary>
        /// Loads a cursor file as a Bitmap with proper alpha channel handling.
        /// </summary>
        /// <param name="curFilePath">Path to the .cur file.</param>
        /// <returns>A Bitmap containing the largest cursor image with proper alpha.</returns>
        /// <remarks>
        /// System.Drawing.Icon.ToBitmap() doesn't properly handle cursor alpha channels,
        /// often resulting in all-black images. This method manually extracts the DIB/PNG
        /// image data from the CUR file and loads it directly.
        /// </remarks>
        public static Bitmap LoadCursorBitmap(string curFilePath)
        {
            byte[] curData = File.ReadAllBytes(curFilePath);

            if (curData.Length < 6)
                throw new InvalidDataException("Invalid cursor file: too small");

            // Parse ICONDIR header
            ushort reserved = BitConverter.ToUInt16(curData, 0);
            ushort type = BitConverter.ToUInt16(curData, 2);
            ushort count = BitConverter.ToUInt16(curData, 4);

            if (reserved != 0 || type != 2 || count == 0)
                throw new InvalidDataException($"Not a valid cursor file (type={type}, expected 2)");

            // Find largest image
            int bestArea = 0;
            uint bestOffset = 0;
            uint bestSize = 0;
            int bestWidth = 0;
            int bestHeight = 0;

            int entryOffset = 6; // After ICONDIR header
            for (int i = 0; i < count; i++)
            {
                byte widthByte = curData[entryOffset + 0];
                byte heightByte = curData[entryOffset + 1];
                // Skip colorCount(1) + reserved(1) + hotspotX(2) + hotspotY(2)
                uint bytesInRes = BitConverter.ToUInt32(curData, entryOffset + 8);
                uint imageOffset = BitConverter.ToUInt32(curData, entryOffset + 12);

                int w = widthByte == 0 ? 256 : widthByte;
                int h = heightByte == 0 ? 256 : heightByte;

                int area = w * h;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestWidth = w;
                    bestHeight = h;
                    bestOffset = imageOffset;
                    bestSize = bytesInRes;
                }

                entryOffset += 16; // Size of ICONDIRENTRY
            }

            if (bestSize == 0)
                throw new InvalidDataException("No valid cursor images found");

            // Extract image data
            byte[] imageData = new byte[bestSize];
            Array.Copy(curData, bestOffset, imageData, 0, bestSize);

            // Try to load as PNG first (modern cursors)
            try
            {
                using var ms = new MemoryStream(imageData);
                return new Bitmap(ms);
            }
            catch
            {
                // Fall back to DIB parsing
                return LoadDibBitmap(imageData, bestWidth, bestHeight);
            }
        }

        /// <summary>
        /// Parses a DIB (Device Independent Bitmap) from cursor image data.
        /// </summary>
        /// <param name="dibData">DIB data including BITMAPINFOHEADER.</param>
        /// <param name="expectedWidth">Expected image width.</param>
        /// <param name="expectedHeight">Expected image height.</param>
        /// <returns>A Bitmap with proper alpha channel.</returns>
        private static Bitmap LoadDibBitmap(byte[] dibData, int expectedWidth, int expectedHeight)
        {
            using var ms = new MemoryStream(dibData);
            using var br = new BinaryReader(ms);

            // Read BITMAPINFOHEADER
            uint biSize = br.ReadUInt32();
            int biWidth = br.ReadInt32();
            int biHeight = br.ReadInt32(); // Positive = bottom-up; includes AND mask (height*2)
            ushort biPlanes = br.ReadUInt16();
            ushort biBitCount = br.ReadUInt16();
            uint biCompression = br.ReadUInt32();
            br.ReadUInt32(); // biSizeImage
            br.ReadInt32();  // biXPelsPerMeter
            br.ReadInt32();  // biYPelsPerMeter
            br.ReadUInt32(); // biClrUsed
            br.ReadUInt32(); // biClrImportant

            // For cursors, biHeight includes both color data and AND mask
            int actualHeight = biHeight / 2;

            if (biBitCount != 32)
                throw new NotSupportedException($"Only 32-bit cursors supported (got {biBitCount}-bit)");

            // Create bitmap
            var bitmap = new Bitmap(biWidth, actualHeight, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, biWidth, actualHeight);
            var bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride = bmpData.Stride;
                int rowBytes = biWidth * 4;

                // Read pixel data (bottom-up in DIB format)
                for (int y = 0; y < actualHeight; y++)
                {
                    int destY = actualHeight - 1 - y; // Flip to top-down
                    byte[] rowData = br.ReadBytes(rowBytes);

                    unsafe
                    {
                        byte* destRow = (byte*)bmpData.Scan0 + (destY * stride);
                        fixed (byte* srcRow = rowData)
                        {
                            Buffer.MemoryCopy(srcRow, destRow, stride, rowBytes);
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            return bitmap;
        }

        /// <summary>
        /// Converts a CUR file to a temporary ICO file for compatibility with System.Drawing.Icon.
        /// </summary>
        /// <param name="curFilePath">Path to the .cur file.</param>
        /// <returns>Path to a temporary ICO file that should be deleted after use.</returns>
        /// <remarks>
        /// Note: This method is deprecated in favor of LoadCursorBitmap which properly handles alpha channels.
        /// CUR files use the same format as ICO files (type=2 vs type=1). This creates a temporary ICO
        /// copy with the type byte changed. The caller is responsible for deleting the temporary file.
        /// </remarks>
        public static string ConvertCursorToTempIcon(string curFilePath)
        {
            // Read the entire CUR file
            byte[] curData = File.ReadAllBytes(curFilePath);

            if (curData.Length < 6)
                throw new InvalidDataException("Invalid cursor file: too small");

            // Check the type field (offset 2, 2 bytes little-endian)
            // CUR files have type=2, ICO files have type=1
            ushort type = BitConverter.ToUInt16(curData, 2);

            if (type != 2)
                throw new InvalidDataException($"Not a valid cursor file (type={type}, expected 2)");

            // Modify type byte from 2 (CUR) to 1 (ICO)
            curData[2] = 1;
            curData[3] = 0;

            // Write to a temporary ICO file
            string tempIcoPath = Path.GetTempFileName() + ".ico";
            File.WriteAllBytes(tempIcoPath, curData);

            return tempIcoPath;
        }
    }
}
