using System;
using PixlPunkt.Constants;
using PixlPunkt.Core.Coloring.Helpers;

namespace PixlPunkt.Core.Compositing.Effects
{
    /// <summary>
    /// Reduces image resolution by averaging pixels into uniform blocks, creating a mosaic effect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect divides the image into a grid of square blocks and replaces all pixels within each
    /// block with the block's average color. Creates a "chunky pixel" aesthetic commonly used for
    /// censorship, retro game styling, or stylized low-resolution appearance.
    /// </para>
    /// </remarks>
    public sealed class PixelateEffect : LayerEffectBase
    {
        public override string DisplayName => "Pixelate";

        private int _blockSize = EffectLimits.DefaultBlockSize;
        public int BlockSize
        {
            get => _blockSize;
            set
            {
                int clamped = Math.Clamp(value, EffectLimits.MinBlockSize, EffectLimits.MaxBlockSize);
                if (_blockSize != clamped)
                {
                    _blockSize = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            int len = width * height;
            if (len == 0 || pixels.Length < len) return;

            int bs = Math.Clamp(BlockSize, EffectLimits.MinBlockSize, Math.Max(width, height));
            if (bs <= 1) return;

            uint[] src = pixels.ToArray();

            for (int by = 0; by < height; by += bs)
            {
                int blockH = Math.Min(bs, height - by);
                for (int bx = 0; bx < width; bx += bs)
                {
                    int blockW = Math.Min(bs, width - bx);

                    int sumA = 0, sumR = 0, sumG = 0, sumB = 0, count = 0;

                    for (int y = 0; y < blockH; y++)
                    {
                        int py = by + y;
                        int rowBase = py * width;
                        for (int x = 0; x < blockW; x++)
                        {
                            int px = bx + x;
                            int idx = rowBase + px;
                            uint c = src[idx];
                            ColorUtil.Unpack(c, out byte a, out byte r, out byte g, out byte b);

                            sumA += a;
                            sumR += r;
                            sumG += g;
                            sumB += b;
                            count++;
                        }
                    }

                    if (count == 0) continue;

                    byte avgA = (byte)(sumA / count);
                    byte avgR = (byte)(sumR / count);
                    byte avgG = (byte)(sumG / count);
                    byte avgB = (byte)(sumB / count);
                    uint blockColor = ColorUtil.Pack(avgA, avgR, avgG, avgB);

                    for (int y = 0; y < blockH; y++)
                    {
                        int py = by + y;
                        int rowBase = py * width;
                        for (int x = 0; x < blockW; x++)
                        {
                            int px = bx + x;
                            int idx = rowBase + px;
                            pixels[idx] = blockColor;
                        }
                    }
                }
            }
        }
    }
}
