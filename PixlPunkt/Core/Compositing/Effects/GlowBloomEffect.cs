using System;
using PixlPunkt.Constants;
using PixlPunkt.Core.Coloring.Helpers;

namespace PixlPunkt.Core.Compositing.Effects
{
    /// <summary>
    /// Extracts and emphasizes bright regions, creating a soft glow or bloom effect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect simulates light bleeding or lens bloom by isolating bright pixels above a threshold,
    /// blurring them extensively, and adding the result back to the original image with amplification.
    /// Commonly used in games and cinematics to create dreamy, ethereal lighting or simulate camera
    /// over-exposure around bright light sources.
    /// </para>
    /// <para><strong>Parameters:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Threshold</strong> (0..1): Brightness cutoff for bloom extraction. Pixels with
    /// max(R,G,B) below this value don't contribute to glow. At 0.7, only the brightest 30% of values
    /// produce bloom.</item>
    /// <item><strong>Intensity</strong> (0..2): Amplification factor for the blurred glow when added
    /// back to original. At 1.0, matched brightness. At 2.0, doubles the bloom contribution.</item>
    /// <item><strong>Radius</strong> (0..8 px): Blur kernel size for glow spread. Larger values create
    /// softer, more diffuse halos around bright regions.</item>
    /// </list>
    /// <para><strong>Algorithm:</strong></para>
    /// <list type="number">
    /// <item><strong>Bright-pass filter</strong>: For each pixel, compute brightness = max(R,G,B).
    /// If brightness > threshold, extract color with strength = (brightness - threshold) / (1 - threshold).</item>
    /// <item><strong>Blur extracted glow</strong>: Apply two-pass box blur to glow channels (R, G, B separately)
    /// using the specified radius.</item>
    /// <item><strong>Additive composite</strong>: Add blurred glow back to original pixels scaled by intensity:
    /// <c>result = original + glow × intensity</c>. Clamped to valid color range [0..255].</item>
    /// </list>
    /// <para><strong>Performance Notes:</strong></para>
    /// <para>
    /// Processes three separate float buffers (RGB channels) for glow extraction and blurring.
    /// Box blur complexity is O((2r+1)² × width × height × 2 passes). Total memory overhead is
    /// approximately 9 × width × height × sizeof(float) bytes for intermediate buffers.
    /// </para>
    /// </remarks>
    public sealed class GlowBloomEffect : LayerEffectBase
    {
        public override string DisplayName => "Glow / Bloom";

        private double _threshold = 0.7; // 0..1 brightness
        public double Threshold
        {
            get => _threshold;
            set
            {
                double clamped = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_threshold - clamped) > double.Epsilon)
                {
                    _threshold = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private double _intensity = 1.0; // 0..2
        public double Intensity
        {
            get => _intensity;
            set
            {
                double clamped = Math.Clamp(value, 0.0, 2.0);
                if (Math.Abs(_intensity - clamped) > double.Epsilon)
                {
                    _intensity = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private int _radius = EffectLimits.DefaultRadius;
        public int Radius
        {
            get => _radius;
            set
            {
                int clamped = Math.Clamp(value, EffectLimits.MinRadius, EffectLimits.MaxRadius);
                if (_radius != clamped)
                {
                    _radius = clamped;
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
            float[] glowR = new float[len];
            float[] glowG = new float[len];
            float[] glowB = new float[len];

            float thresh = (float)Threshold;
            float intensity = (float)Intensity;

            // Bright-pass
            for (int i = 0; i < len; i++)
            {
                uint c = src[i];
                ColorUtil.Unpack(c, out byte a, out byte r, out byte g, out byte b);
                if (a == 0) continue;

                float rf = r / 255f;
                float gf = g / 255f;
                float bf = b / 255f;
                float v = Math.Max(rf, Math.Max(gf, bf)); // simple brightness

                if (v <= thresh) continue;

                float factor = (v - thresh) / (1f - thresh); // 0..1
                glowR[i] = rf * factor;
                glowG[i] = gf * factor;
                glowB[i] = bf * factor;
            }

            // Blur glow
            int radius = Radius;
            if (radius > 0)
            {
                (glowR, glowG, glowB) = BoxBlur(glowR, glowG, glowB, width, height, radius);
            }

            // Add back over original
            for (int i = 0; i < len; i++)
            {
                uint c = src[i];
                ColorUtil.Unpack(c, out byte a, out byte r, out byte g, out byte b);

                float rf = r / 255f + glowR[i] * intensity;
                float gf = g / 255f + glowG[i] * intensity;
                float bf = b / 255f + glowB[i] * intensity;

                byte nr = (byte)Math.Clamp((int)MathF.Round(rf * 255f), 0, 255);
                byte ng = (byte)Math.Clamp((int)MathF.Round(gf * 255f), 0, 255);
                byte nb = (byte)Math.Clamp((int)MathF.Round(bf * 255f), 0, 255);

                pixels[i] = ColorUtil.Pack(a, nr, ng, nb);
            }
        }

        private static (float[] R, float[] G, float[] B) BoxBlur(
            float[] srcR, float[] srcG, float[] srcB,
            int width, int height, int radius)
        {
            int len = width * height;
            float[] tmpR = new float[len];
            float[] tmpG = new float[len];
            float[] tmpB = new float[len];
            float[] dstR = new float[len];
            float[] dstG = new float[len];
            float[] dstB = new float[len];

            // First pass
            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = rowBase + x;
                    float sumR = 0, sumG = 0, sumB = 0;
                    int count = 0;

                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        int ny = y + oy;
                        if ((uint)ny >= (uint)height) continue;

                        int nRow = ny * width;
                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            int nx = x + ox;
                            if ((uint)nx >= (uint)width) continue;

                            int nIdx = nRow + nx;
                            sumR += srcR[nIdx];
                            sumG += srcG[nIdx];
                            sumB += srcB[nIdx];
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        tmpR[idx] = sumR / count;
                        tmpG[idx] = sumG / count;
                        tmpB[idx] = sumB / count;
                    }
                    else
                    {
                        tmpR[idx] = srcR[idx];
                        tmpG[idx] = srcG[idx];
                        tmpB[idx] = srcB[idx];
                    }
                }
            }

            // Second pass
            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = rowBase + x;
                    float sumR = 0, sumG = 0, sumB = 0;
                    int count = 0;

                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        int ny = y + oy;
                        if ((uint)ny >= (uint)height) continue;

                        int nRow = ny * width;
                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            int nx = x + ox;
                            if ((uint)nx >= (uint)width) continue;

                            int nIdx = nRow + nx;
                            sumR += tmpR[nIdx];
                            sumG += tmpG[nIdx];
                            sumB += tmpB[nIdx];
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        dstR[idx] = sumR / count;
                        dstG[idx] = sumG / count;
                        dstB[idx] = sumB / count;
                    }
                    else
                    {
                        dstR[idx] = tmpR[idx];
                        dstG[idx] = tmpG[idx];
                        dstB[idx] = tmpB[idx];
                    }
                }
            }

            return (dstR, dstG, dstB);
        }
    }
}
