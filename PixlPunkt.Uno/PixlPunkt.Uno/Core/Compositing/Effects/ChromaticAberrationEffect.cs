using System;
using PixlPunkt.Uno.Constants;

namespace PixlPunkt.Uno.Core.Compositing.Effects
{
    /// <summary>
    /// Simulates lens chromatic aberration by radially displacing red and blue color channels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect mimics the optical distortion caused by lens imperfections where different wavelengths
    /// of light refract at slightly different angles. Red and blue channels are shifted outward/inward
    /// from the image center while green remains fixed, creating colored fringes around high-contrast edges.
    /// </para>
    /// <para><strong>Parameters:</strong></para>
    /// <list type="bullet">
    /// <item><strong>OffsetPixels</strong> (0..8 px): Radial displacement distance for red and blue channels.
    /// Red shifts outward (away from center) while blue shifts inward (toward center).</item>
    /// <item><strong>Strength</strong> (0..1): Blend factor between original color and shifted channels.
    /// At 0, no aberration. At 1, full channel displacement.</item>
    /// </list>
    /// <para><strong>Algorithm:</strong></para>
    /// <list type="number">
    /// <item>For each pixel, compute normalized direction vector from image center: (dx, dy) / distance.</item>
    /// <item>Sample red channel from position shifted outward along radial direction by OffsetPixels.</item>
    /// <item>Sample blue channel from position shifted inward along radial direction by OffsetPixels.</item>
    /// <item>Keep green channel unchanged (acts as luminance anchor).</item>
    /// <item>Blend sampled channels with original based on Strength parameter.</item>
    /// </list>
    /// <para><strong>Visual Effect:</strong></para>
    /// <para>
    /// Creates colorful fringing on edges and details, particularly visible in high-contrast areas.
    /// Commonly used for artistic "lens distortion" effects or to simulate vintage camera optics.
    /// </para>
    /// </remarks>
    public sealed class ChromaticAberrationEffect : LayerEffectBase
    {
        public override string DisplayName => "Chromatic Aberration";

        private double _offsetPixels = 1.0; // 0..8 px
        private double _strength = 1.0;     // 0..1 mix between original and shifted

        public double OffsetPixels
        {
            get => _offsetPixels;
            set
            {
                value = Math.Clamp(value, EffectLimits.MinRadius, EffectLimits.MaxRadius);
                if (Math.Abs(_offsetPixels - value) < double.Epsilon) return;
                _offsetPixels = value;
                OnPropertyChanged();
            }
        }

        public double Strength
        {
            get => _strength;
            set
            {
                value = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_strength - value) < double.Epsilon) return;
                _strength = value;
                OnPropertyChanged();
            }
        }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            if (width <= 0 || height <= 0) return;
            int total = width * height;
            if (pixels.Length < total) return;
            if (OffsetPixels <= 0.0 || Strength <= 0.0) return;

            // Work from a copy so we don't smear as we read
            uint[] src = pixels.ToArray();

            double cx = (width - 1) * 0.5;
            double cy = (height - 1) * 0.5;
            double strength = Strength;
            double offset = OffsetPixels;

            for (int y = 0; y < height; y++)
            {
                double dy = y - cy;
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    uint baseCol = src[idx];
                    byte a0 = (byte)(baseCol >> 24);
                    if (a0 == 0)
                        continue;

                    byte r0 = (byte)(baseCol >> 16);
                    byte g0 = (byte)(baseCol >> 8);
                    byte b0 = (byte)baseCol;

                    double dx = x - cx;
                    double len = Math.Sqrt(dx * dx + dy * dy) + 1e-6;
                    double nx = dx / len;
                    double ny = dy / len;

                    int rX = (int)Math.Round(x + nx * offset);
                    int rY = (int)Math.Round(y + ny * offset);
                    int bX = (int)Math.Round(x - nx * offset);
                    int bY = (int)Math.Round(y - ny * offset);

                    rX = Math.Clamp(rX, 0, width - 1);
                    rY = Math.Clamp(rY, 0, height - 1);
                    bX = Math.Clamp(bX, 0, width - 1);
                    bY = Math.Clamp(bY, 0, height - 1);

                    uint rCol = src[rY * width + rX];
                    uint bCol = src[bY * width + bX];

                    byte rShift = (byte)(rCol >> 16);
                    byte bShift = (byte)bCol;

                    // mix shifted with original based on Strength
                    byte rNew = (byte)Math.Clamp(
                        (int)Math.Round((1.0 - strength) * r0 + strength * rShift), 0, 255);
                    byte gNew = g0; // green acts as "luminance anchor"
                    byte bNew = (byte)Math.Clamp(
                        (int)Math.Round((1.0 - strength) * b0 + strength * bShift), 0, 255);

                    pixels[idx] = (uint)(a0 << 24 | rNew << 16 | gNew << 8 | bNew);
                }
            }
        }
    }
}
