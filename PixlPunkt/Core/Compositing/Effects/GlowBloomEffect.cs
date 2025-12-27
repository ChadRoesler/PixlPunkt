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

        /// <summary>
        /// Separable box blur using O(n) running sum algorithm for RGB float channels.
        /// Two-pass (horizontal then vertical) with running sum per scanline.
        /// </summary>
        /// <remarks>
        /// This implementation is O(width*height*2) regardless of blur radius,
        /// compared to the naive O(width*height*radius²) approach.
        /// </remarks>
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

            float countInv = 1f / (radius * 2 + 1);

            // PASS 1: Horizontal blur using running sum
            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;

                // Running sums for RGB channels
                float sumR = 0, sumG = 0, sumB = 0;

                // Initialize running sum for first pixel (left edge handling)
                for (int rx = -radius; rx <= radius; rx++)
                {
                    int px = Math.Clamp(rx, 0, width - 1);
                    int idx = rowBase + px;
                    sumR += srcR[idx];
                    sumG += srcG[idx];
                    sumB += srcB[idx];
                }

                for (int x = 0; x < width; x++)
                {
                    int idx = rowBase + x;

                    // Store the averaged pixel
                    tmpR[idx] = sumR * countInv;
                    tmpG[idx] = sumG * countInv;
                    tmpB[idx] = sumB * countInv;

                    // Slide the window: remove left edge, add right edge
                    int leftEdge = Math.Clamp(x - radius, 0, width - 1);
                    int rightEdge = Math.Clamp(x + radius + 1, 0, width - 1);

                    int leftIdx = rowBase + leftEdge;
                    int rightIdx = rowBase + rightEdge;

                    sumR += srcR[rightIdx] - srcR[leftIdx];
                    sumG += srcG[rightIdx] - srcG[leftIdx];
                    sumB += srcB[rightIdx] - srcB[leftIdx];
                }
            }

            // PASS 2: Vertical blur using running sum
            for (int x = 0; x < width; x++)
            {
                // Running sums for RGB channels
                float sumR = 0, sumG = 0, sumB = 0;

                // Initialize running sum for first pixel (top edge handling)
                for (int ry = -radius; ry <= radius; ry++)
                {
                    int py = Math.Clamp(ry, 0, height - 1);
                    int idx = py * width + x;
                    sumR += tmpR[idx];
                    sumG += tmpG[idx];
                    sumB += tmpB[idx];
                }

                for (int y = 0; y < height; y++)
                {
                    int idx = y * width + x;

                    // Store the averaged pixel
                    dstR[idx] = sumR * countInv;
                    dstG[idx] = sumG * countInv;
                    dstB[idx] = sumB * countInv;

                    // Slide the window: remove top edge, add bottom edge
                    int topEdge = Math.Clamp(y - radius, 0, height - 1);
                    int bottomEdge = Math.Clamp(y + radius + 1, 0, height - 1);

                    int topIdx = topEdge * width + x;
                    int bottomIdx = bottomEdge * width + x;

                    sumR += tmpR[bottomIdx] - tmpR[topIdx];
                    sumG += tmpG[bottomIdx] - tmpG[topIdx];
                    sumB += tmpB[bottomIdx] - tmpB[topIdx];
                }
            }

            return (dstR, dstG, dstB);
        }
    }
}
