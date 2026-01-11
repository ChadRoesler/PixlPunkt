using System;
using PixlPunkt.Constants;
using PixlPunkt.Core.Coloring.Helpers;

namespace PixlPunkt.Core.Compositing.Effects
{
    /// <summary>
    /// Simulates a cathode ray tube (CRT) monitor with barrel distortion and radial vignette.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect recreates the characteristic visual appearance of vintage CRT displays by combining
    /// barrel/pincushion distortion with edge darkening.
    /// </para>
    /// </remarks>
    public sealed class CrtEffect : LayerEffectBase
    {
        public override string DisplayName => "CRT";

        // Strength = 0..1 → how much we push towards the tint at the edges
        private double _strength = 0.6;
        // Curvature = 0..MaxCurvature → how strong the bubble distortion is
        private double _curvature = EffectLimits.DefaultCurvature;
        private bool _applyOnAlpha = false;
        public bool ApplyOnAlpha
        {
            get => _applyOnAlpha;
            set
            {
                if (_applyOnAlpha != value)
                {
                    _applyOnAlpha = value;
                    OnPropertyChanged();
                }
            }
        }

        private uint _color = 0xFF000000; // tint color for the corners (default black)
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

        public double Curvature
        {
            get => _curvature;
            set
            {
                value = Math.Clamp(value, EffectLimits.MinCurvature, EffectLimits.MaxCurvature);
                if (Math.Abs(_curvature - value) < double.Epsilon) return;
                _curvature = value;
                OnPropertyChanged();
            }
        }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            if (width <= 0 || height <= 0) return;

            int total = width * height;
            if (pixels.Length < total) return;

            // Copy source so we never read from what we're writing into
            uint[] src = pixels.ToArray();
            Span<uint> dst = pixels;

            // Clear destination (outside CRT mask stays transparent)
            for (int i = 0; i < total; i++)
                dst[i] = 0;

            double cx = (width - 1) * 0.5;
            double cy = (height - 1) * 0.5;
            double maxR = Math.Sqrt(cx * cx + cy * cy);
            if (maxR <= 0) return;
            double invMaxR = 1.0 / maxR;

            double kDistort = Curvature; // bubble
            double kVignette = Strength;  // corner tint amount

            // normalize warped radius so edges stay inside 0..1
            double warpNormFactor = 1.0 / (1.0 + kDistort);

            // Tint color for corners
            ColorUtil.Unpack(Color, out byte tintA, out byte tintR, out byte tintG, out byte tintB);
            double tintAf = tintA / 255.0;

            for (int y = 0; y < height; y++)
            {
                double dy = y - cy;

                for (int x = 0; x < width; x++)
                {
                    double dx = x - cx;

                    // radius in 0..1
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    double rNorm = r * invMaxR;
                    if (rNorm > 1.0)
                        continue; // outside CRT mask

                    // Barrel distortion
                    double rWarp = rNorm * (1.0 + kDistort * rNorm * rNorm);
                    rWarp *= warpNormFactor;

                    double scale = (rNorm > 1e-6) ? (rWarp / rNorm) : 1.0;

                    double sx = cx + dx * scale;
                    double sy = cy + dy * scale;

                    int ix = (int)Math.Round(sx);
                    int iy = (int)Math.Round(sy);
                    if ((uint)ix >= (uint)width || (uint)iy >= (uint)height)
                        continue;

                    uint srcCol = src[iy * width + ix];
                    ColorUtil.Unpack(srcCol, out byte srcA, out byte srcR, out byte srcG, out byte srcB);
                    bool wasTransparent = (srcA == 0);

                    if (wasTransparent && !ApplyOnAlpha)
                        continue;

                    if (wasTransparent)
                    {
                        // ignore any RGB under alpha=0
                        srcR = srcG = srcB = 0;
                    }

                    // Vignette/tint amount based on original radius
                    double falloff = Math.Pow(rNorm, 2.0 + 2.0 * kDistort);
                    double edge = kVignette * falloff;
                    if (edge < 0.0) edge = 0.0;
                    if (edge > 1.0) edge = 1.0;

                    double srcFactor = 1.0 - edge;
                    double tintFactor = edge * tintAf;

                    double rOut = srcR * srcFactor + tintR * tintFactor;
                    double gOut = srcG * srcFactor + tintG * tintFactor;
                    double bOut = srcB * srcFactor + tintB * tintFactor;

                    byte aOut;
                    if (wasTransparent)
                    {
                        // New alpha only where CRT vignette really applies
                        double alphaOut = tintAf * edge;
                        aOut = (byte)Math.Clamp((int)Math.Round(alphaOut * 255.0), 0, 255);
                        if (aOut == 0)
                            continue;
                    }
                    else
                    {
                        // keep original alpha → looks like distortion over existing content
                        aOut = srcA;
                    }

                    byte rFinal = (byte)Math.Clamp((int)Math.Round(rOut), 0, 255);
                    byte gFinal = (byte)Math.Clamp((int)Math.Round(gOut), 0, 255);
                    byte bFinal = (byte)Math.Clamp((int)Math.Round(bOut), 0, 255);

                    dst[y * width + x] =
                        (uint)(aOut << 24) |
                        ((uint)rFinal << 16) |
                        ((uint)gFinal << 8) |
                        bFinal;
                }
            }
        }

    }
}
