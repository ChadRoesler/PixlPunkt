using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.Export
{
    /// <summary>
    /// Encodes animation frames to GIF format.
    /// Uses a pure C# implementation for cross-platform support.
    /// </summary>
    public static class GifEncoder
    {
        /// <summary>
        /// Encodes frames to an animated GIF.
        /// </summary>
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

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var bytes = await Task.Run(() => EncodeGifBytes(frames, loop, progress, cancellationToken), cancellationToken);
            await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);

            LoggingService.Info("GIF encoding complete: {Path}", outputPath);
        }

        /// <summary>
        /// Encodes frames to a GIF byte array.
        /// </summary>
        public static Task<byte[]> EncodeToBytesAsync(
            IReadOnlyList<AnimationExportService.RenderedFrame> frames,
            bool loop = true,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (frames == null || frames.Count == 0)
                throw new ArgumentException("No frames to encode", nameof(frames));

            return Task.Run(() => EncodeGifBytes(frames, loop, progress, cancellationToken), cancellationToken);
        }

        private static byte[] EncodeGifBytes(
            IReadOnlyList<AnimationExportService.RenderedFrame> frames,
            bool loop,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            var firstFrame = frames[0];
            int width = firstFrame.Width;
            int height = firstFrame.Height;

            // GIF Header
            writer.Write(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }); // "GIF89a"

            // Logical Screen Descriptor
            writer.Write((ushort)width);
            writer.Write((ushort)height);
            writer.Write((byte)0xF7); // Global Color Table Flag, 256 colors (8 bits)
            writer.Write((byte)0);    // Background color index
            writer.Write((byte)0);    // Pixel aspect ratio

            // Global Color Table (256 colors)
            // We'll use a simple palette - actual frames will use local color tables
            for (int i = 0; i < 256; i++)
            {
                writer.Write((byte)i); // R
                writer.Write((byte)i); // G
                writer.Write((byte)i); // B
            }

            // NETSCAPE extension for looping
            if (loop)
            {
                writer.Write((byte)0x21); // Extension introducer
                writer.Write((byte)0xFF); // Application extension
                writer.Write((byte)0x0B); // Block size
                writer.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                writer.Write((byte)0x03); // Sub-block size
                writer.Write((byte)0x01); // Loop sub-block ID
                writer.Write((ushort)0);  // Loop count (0 = infinite)
                writer.Write((byte)0x00); // Block terminator
            }

            // Encode each frame
            for (int i = 0; i < frames.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var frame = frames[i];
                EncodeFrame(writer, frame.Pixels, frame.Width, frame.Height, frame.DurationMs);

                progress?.Report((double)(i + 1) / frames.Count);
            }

            // GIF Trailer
            writer.Write((byte)0x3B);

            return ms.ToArray();
        }

        private static void EncodeFrame(BinaryWriter writer, byte[] pixels, int width, int height, int durationMs)
        {
            // Build color palette from frame (max 256 colors)
            var palette = BuildPalette(pixels, width, height, out var indexedPixels);

            // Graphic Control Extension
            writer.Write((byte)0x21); // Extension introducer
            writer.Write((byte)0xF9); // Graphic control label
            writer.Write((byte)0x04); // Block size
            
            // Packed fields: disposal method, user input flag, transparent flag
            byte packedFields = 0x04; // Disposal method: restore to background
            int transparentIndex = FindTransparentIndex(palette);
            if (transparentIndex >= 0)
            {
                packedFields |= 0x01; // Transparent flag
            }
            writer.Write(packedFields);
            
            writer.Write((ushort)(durationMs / 10)); // Delay time in centiseconds
            writer.Write((byte)(transparentIndex >= 0 ? transparentIndex : 0)); // Transparent color index
            writer.Write((byte)0x00); // Block terminator

            // Image Descriptor
            writer.Write((byte)0x2C); // Image separator
            writer.Write((ushort)0);  // Left position
            writer.Write((ushort)0);  // Top position
            writer.Write((ushort)width);
            writer.Write((ushort)height);
            
            // Packed fields: local color table flag, interlace, sort, size
            int colorBits = GetColorBits(palette.Count);
            byte imagePackedFields = (byte)(0x80 | (colorBits - 1)); // Local color table, size
            writer.Write(imagePackedFields);

            // Local Color Table
            int tableSize = 1 << colorBits;
            for (int i = 0; i < tableSize; i++)
            {
                if (i < palette.Count)
                {
                    var color = palette[i];
                    writer.Write((byte)((color >> 16) & 0xFF)); // R
                    writer.Write((byte)((color >> 8) & 0xFF));  // G
                    writer.Write((byte)(color & 0xFF));         // B
                }
                else
                {
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                }
            }

            // LZW Minimum Code Size
            writer.Write((byte)Math.Max(2, colorBits));

            // LZW Compressed Data
            var lzwData = LzwEncode(indexedPixels, Math.Max(2, colorBits));
            
            // Write in sub-blocks (max 255 bytes each)
            int offset = 0;
            while (offset < lzwData.Length)
            {
                int blockSize = Math.Min(255, lzwData.Length - offset);
                writer.Write((byte)blockSize);
                writer.Write(lzwData, offset, blockSize);
                offset += blockSize;
            }

            writer.Write((byte)0x00); // Block terminator
        }

        private static List<uint> BuildPalette(byte[] pixels, int width, int height, out byte[] indexedPixels)
        {
            var colorToIndex = new Dictionary<uint, byte>();
            var palette = new List<uint>();
            indexedPixels = new byte[width * height];

            // Reserve index 0 for transparency if needed
            bool hasTransparency = false;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] < 128)
                {
                    hasTransparency = true;
                    break;
                }
            }

            if (hasTransparency)
            {
                palette.Add(0xFF00FF00); // Magenta for transparency (will be marked transparent)
                colorToIndex[0xFF00FF00] = 0;
            }

            // Build palette from pixels
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width + x) * 4;
                    byte b = pixels[idx + 0];
                    byte g = pixels[idx + 1];
                    byte r = pixels[idx + 2];
                    byte a = pixels[idx + 3];

                    uint color;
                    if (a < 128)
                    {
                        // Transparent - use reserved index
                        indexedPixels[y * width + x] = 0;
                        continue;
                    }
                    else
                    {
                        color = (uint)((r << 16) | (g << 8) | b);
                    }

                    if (!colorToIndex.TryGetValue(color, out byte paletteIndex))
                    {
                        if (palette.Count < 256)
                        {
                            paletteIndex = (byte)palette.Count;
                            palette.Add(color);
                            colorToIndex[color] = paletteIndex;
                        }
                        else
                        {
                            // Find closest color in palette
                            paletteIndex = FindClosestColor(palette, color);
                        }
                    }

                    indexedPixels[y * width + x] = paletteIndex;
                }
            }

            // Ensure at least 2 colors
            while (palette.Count < 2)
            {
                palette.Add(0);
            }

            return palette;
        }

        private static byte FindClosestColor(List<uint> palette, uint color)
        {
            int r = (int)((color >> 16) & 0xFF);
            int g = (int)((color >> 8) & 0xFF);
            int b = (int)(color & 0xFF);

            int bestIndex = 0;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < palette.Count; i++)
            {
                int pr = (int)((palette[i] >> 16) & 0xFF);
                int pg = (int)((palette[i] >> 8) & 0xFF);
                int pb = (int)(palette[i] & 0xFF);

                int dr = r - pr;
                int dg = g - pg;
                int db = b - pb;
                int distance = dr * dr + dg * dg + db * db;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return (byte)bestIndex;
        }

        private static int FindTransparentIndex(List<uint> palette)
        {
            // Index 0 is reserved for transparency if we have it
            if (palette.Count > 0 && palette[0] == 0xFF00FF00)
                return 0;
            return -1;
        }

        private static int GetColorBits(int colorCount)
        {
            if (colorCount <= 2) return 1;
            if (colorCount <= 4) return 2;
            if (colorCount <= 8) return 3;
            if (colorCount <= 16) return 4;
            if (colorCount <= 32) return 5;
            if (colorCount <= 64) return 6;
            if (colorCount <= 128) return 7;
            return 8;
        }

        private static byte[] LzwEncode(byte[] data, int minCodeSize)
        {
            using var output = new MemoryStream();
            var bitWriter = new LzwBitWriter(output);

            int clearCode = 1 << minCodeSize;
            int endCode = clearCode + 1;
            int nextCode = endCode + 1;
            int codeSize = minCodeSize + 1;

            var codeTable = new Dictionary<string, int>();

            // Initialize code table
            for (int i = 0; i < clearCode; i++)
            {
                codeTable[((char)i).ToString()] = i;
            }

            // Write clear code
            bitWriter.WriteBits(clearCode, codeSize);

            string buffer = "";
            foreach (byte b in data)
            {
                string next = buffer + (char)b;

                if (codeTable.ContainsKey(next))
                {
                    buffer = next;
                }
                else
                {
                    bitWriter.WriteBits(codeTable[buffer], codeSize);

                    if (nextCode < 4096)
                    {
                        codeTable[next] = nextCode++;

                        if (nextCode > (1 << codeSize) && codeSize < 12)
                        {
                            codeSize++;
                        }
                    }
                    else
                    {
                        // Table full, send clear code and reset
                        bitWriter.WriteBits(clearCode, codeSize);
                        codeTable.Clear();
                        for (int i = 0; i < clearCode; i++)
                        {
                            codeTable[((char)i).ToString()] = i;
                        }
                        nextCode = endCode + 1;
                        codeSize = minCodeSize + 1;
                    }

                    buffer = ((char)b).ToString();
                }
            }

            // Write remaining buffer
            if (buffer.Length > 0)
            {
                bitWriter.WriteBits(codeTable[buffer], codeSize);
            }

            // Write end code
            bitWriter.WriteBits(endCode, codeSize);
            bitWriter.Flush();

            return output.ToArray();
        }

        private class LzwBitWriter
        {
            private readonly Stream _stream;
            private int _bitBuffer;
            private int _bitCount;

            public LzwBitWriter(Stream stream)
            {
                _stream = stream;
            }

            public void WriteBits(int code, int bitCount)
            {
                _bitBuffer |= code << _bitCount;
                _bitCount += bitCount;

                while (_bitCount >= 8)
                {
                    _stream.WriteByte((byte)(_bitBuffer & 0xFF));
                    _bitBuffer >>= 8;
                    _bitCount -= 8;
                }
            }

            public void Flush()
            {
                if (_bitCount > 0)
                {
                    _stream.WriteByte((byte)(_bitBuffer & 0xFF));
                }
            }
        }
    }
}
