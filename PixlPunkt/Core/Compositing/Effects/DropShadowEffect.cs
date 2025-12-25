using System;
using PixlPunkt.Constants;
using PixlPunkt.Core.Coloring.Helpers;

namespace PixlPunkt.Core.Compositing.Effects
{
    /// <summary>
    /// Creates a shadow beneath the layer with configurable offset, blur, color, and opacity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect generates a drop shadow by creating a duplicate of the layer's alpha channel,
    /// offsetting it, optionally blurring it, tinting it with a color, and compositing it behind
    /// the original layer. Commonly used to add depth and separation between UI elements or layers.
    /// </para>
    /// <para><strong>Parameters:</strong></para>
    /// <list type="bullet">
    /// <item><strong>OffsetX/OffsetY</strong> (pixels): Shadow displacement from original layer position.</item>
    /// <item><strong>Opacity</strong> (0..1): Shadow transparency. At 1, shadow is fully opaque (respecting color alpha).</item>
    /// <item><strong>BlurRadius</strong> (0..MaxRadius pixels): Softness of shadow edges using box blur algorithm.</item>
    /// <item><strong>Color</strong> (BGRA): Shadow tint color (default: black). Alpha channel controls base shadow opacity.</item>
    /// </list>
    /// </remarks>
    public sealed class DropShadowEffect : LayerEffectBase
    {
        public override string DisplayName => "Drop Shadow";

        private int _offsetX = EffectLimits.DefaultOffset;
        public int OffsetX
        {
            get => _offsetX;
            set
            {
                if (_offsetX != value)
                {
                    _offsetX = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _offsetY = EffectLimits.DefaultOffset;
        public int OffsetY
        {
            get => _offsetY;
            set
            {
                if (_offsetY != value)
                {
                    _offsetY = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _opacity = 0.5; // 0..1
        public double Opacity
        {
            get => _opacity;
            set
            {
                double clamped = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_opacity - clamped) > double.Epsilon)
                {
                    _opacity = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private int _blurRadius = 1;
        public int BlurRadius
        {
            get => _blurRadius;
            set
            {
                int clamped = Math.Clamp(value, EffectLimits.MinRadius, EffectLimits.MaxRadius);
                if (_blurRadius != clamped)
                {
                    _blurRadius = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private uint _color = 0xFF000000; // black
        public uint Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    OnPropertyChanged();
                }
            }
        }



        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            int len = width * height;
            if (len == 0 || pixels.Length < len) return;

            uint[] src = pixels.ToArray();
            uint[] shadow = new uint[len];

            // Base shadow from alpha mask
            ColorUtil.Unpack(Color, out byte ca, out byte cr, out byte cg, out byte cb);
            float colorAlpha = ca / 255f;
            float opacity = (float)Opacity;

            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = rowBase + x;
                    uint c = src[idx];
                    ColorUtil.Unpack(c, out byte a, out _, out _, out _);
                    if (a == 0) continue;

                    float srcA = a / 255f;
                    float shadowA = srcA * colorAlpha * opacity;
                    if (shadowA <= 0f) continue;

                    int sx = x + OffsetX;
                    int sy = y + OffsetY;
                    if ((uint)sx >= (uint)width || (uint)sy >= (uint)height) continue;

                    int sIdx = sy * width + sx;
                    uint existing = shadow[sIdx];

                    // Compose (new shadow) over existing shadow
                    uint newShadow = ColorUtil.Pack(
                        (byte)(shadowA * 255f),
                        cr, cg, cb);

                    shadow[sIdx] = AlphaOver(newShadow, existing);
                }
            }

            // Optional blur
            if (BlurRadius > 0)
            {
                shadow = BoxBlur(shadow, width, height, BlurRadius);
            }

            // Composite original over shadow
            for (int i = 0; i < len; i++)
            {
                uint s = shadow[i];
                uint o = src[i];
                pixels[i] = AlphaOver(o, s); // original on top
            }
        }

        private static uint AlphaOver(uint top, uint bottom)
        {
            ColorUtil.Unpack(top, out byte ta, out byte tr, out byte tg, out byte tb);
            ColorUtil.Unpack(bottom, out byte ba, out byte br, out byte bg, out byte bb);

            float tAf = ta / 255f;
            float bAf = ba / 255f;

            float outAf = tAf + bAf * (1f - tAf);
            if (outAf <= 0f)
                return 0;

            float trP = (tr / 255f);
            float tgP = (tg / 255f);
            float tbP = (tb / 255f);
            float brP = (br / 255f);
            float bgP = (bg / 255f);
            float bbP = (bb / 255f);

            float orP = (trP * tAf + brP * bAf * (1f - tAf)) / outAf;
            float ogP = (tgP * tAf + bgP * bAf * (1f - tAf)) / outAf;
            float obP = (tbP * tAf + bbP * bAf * (1f - tAf)) / outAf;

            byte oa = (byte)Math.Clamp((int)MathF.Round(outAf * 255f), 0, 255);
            byte or_ = (byte)Math.Clamp((int)MathF.Round(orP * 255f), 0, 255);
            byte og = (byte)Math.Clamp((int)MathF.Round(ogP * 255f), 0, 255);
            byte ob = (byte)Math.Clamp((int)MathF.Round(obP * 255f), 0, 255);

            return ColorUtil.Pack(oa, or_, og, ob);
        }

        private static uint[] BoxBlur(uint[] src, int width, int height, int radius)
        {
            int len = src.Length;
            uint[] tmp = new uint[len];
            uint[] dst = new uint[len];

            // Simple 2D box blur
            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = rowBase + x;

                    int sumA = 0, sumR = 0, sumG = 0, sumB = 0, count = 0;
                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        int ny = y + oy;
                        if ((uint)ny >= (uint)height) continue;

                        int nRow = ny * width;
                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            int nx = x + ox;
                            if ((uint)nx >= (uint)width) continue;

                            uint c = src[nRow + nx];
                            ColorUtil.Unpack(c, out byte a, out byte r, out byte g, out byte b);
                            sumA += a;
                            sumR += r;
                            sumG += g;
                            sumB += b;
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        byte a = (byte)(sumA / count);
                        byte r = (byte)(sumR / count);
                        byte g = (byte)(sumG / count);
                        byte b = (byte)(sumB / count);
                        tmp[idx] = ColorUtil.Pack(a, r, g, b);
                    }
                    else
                    {
                        tmp[idx] = src[idx];
                    }
                }
            }

            // Second pass (can reuse same kernel)
            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = rowBase + x;

                    int sumA = 0, sumR = 0, sumG = 0, sumB = 0, count = 0;
                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        int ny = y + oy;
                        if ((uint)ny >= (uint)height) continue;

                        int nRow = ny * width;
                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            int nx = x + ox;
                            if ((uint)nx >= (uint)width) continue;

                            uint c = tmp[nRow + nx];
                            ColorUtil.Unpack(c, out byte a, out byte r, out byte g, out byte b);
                            sumA += a;
                            sumR += r;
                            sumG += g;
                            sumB += b;
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        byte a = (byte)(sumA / count);
                        byte r = (byte)(sumR / count);
                        byte g = (byte)(sumG / count);
                        byte b = (byte)(sumB / count);
                        dst[idx] = ColorUtil.Pack(a, r, g, b);
                    }
                    else
                    {
                        dst[idx] = tmp[idx];
                    }
                }
            }

            return dst;
        }
    }
}
