using System;
using PixlPunkt.Constants;

namespace PixlPunkt.Core.Compositing.Effects
{
    /// <summary>
    /// Applies radial darkening/tinting from the center toward edges, creating a vignette effect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect simulates the natural light falloff seen in camera lenses, darkening the corners
    /// and edges of the image while keeping the center bright.
    /// </para>
    /// </remarks>
    public sealed class VignetteEffect : LayerEffectBase
    {
        public override string DisplayName => "Vignette";

        private double _radius = 0.75;   // 0..MaxVignetteRadius – where darkening starts
        private double _strength = 0.8;  // 0..1 – how dark the corners get
        private double _softness = EffectLimits.DefaultVignetteSoftness;  // 0..1 – feather width
        private bool _applyOnAlpha = false;   // apply on transparent pixels

        // Tint color for the vignette (BGRA). Default = black.
        private uint _color = 0xFF000000;
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

        public double Radius
        {
            get => _radius;
            set
            {
                value = Math.Clamp(value, EffectLimits.MinVignetteRadius, EffectLimits.MaxVignetteRadius);
                if (Math.Abs(_radius - value) < double.Epsilon) return;
                _radius = value;
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

        public double Softness
        {
            get => _softness;
            set
            {
                value = Math.Clamp(value, EffectLimits.MinVignetteSoftness, EffectLimits.MaxVignetteSoftness);
                if (Math.Abs(_softness - value) < double.Epsilon) return;
                _softness = value;
                OnPropertyChanged();
            }
        }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            if (width <= 0 || height <= 0) return;

            int total = width * height;
            if (pixels.Length < total) return;

            double cx = (width - 1) * 0.5;
            double cy = (height - 1) * 0.5;
            double maxR = Math.Sqrt(cx * cx + cy * cy);
            if (maxR <= 0) return;

            double radius = Radius;
            double soft = Softness;
            double strength = Strength;

            double inner = radius;
            double outer = Math.Min(1.0, radius + soft * (1.0 - radius));
            if (outer <= inner) outer = inner + 1e-6;

            // Tint
            byte tintA = (byte)(Color >> 24);
            byte tintR = (byte)(Color >> 16);
            byte tintG = (byte)(Color >> 8);
            byte tintB = (byte)Color;
            double tintAf = tintA / 255.0;

            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                double dy = y - cy;

                for (int x = 0; x < width; x++, idx++)
                {
                    uint col = pixels[idx];
                    byte a = (byte)(col >> 24);
                    bool wasTransparent = (a == 0);

                    if (wasTransparent && !ApplyOnAlpha)
                        continue;

                    byte r = (byte)(col >> 16);
                    byte g = (byte)(col >> 8);
                    byte b = (byte)col;

                    if (wasTransparent)
                    {
                        // ignore any garbage RGB under alpha=0
                        r = g = b = 0;
                    }

                    double dx = x - cx;
                    double rNorm = Math.Sqrt(dx * dx + dy * dy) / maxR;
                    rNorm = Math.Clamp(rNorm, 0.0, 1.0);

                    // factor = how much of original we keep (1 center → 1-strength at corners)
                    double factor = 1.0;
                    if (rNorm > inner)
                    {
                        if (rNorm >= outer)
                        {
                            factor = 1.0 - strength;
                        }
                        else
                        {
                            double t = (rNorm - inner) / (outer - inner); // 0..1
                            double fade = t * t * (3.0 - 2.0 * t);        // smoothstep
                            factor = 1.0 - strength * fade;
                        }
                    }

                    // edge = how much vignette/tint we apply (0 center → up to Strength at corners)
                    double edge = 1.0 - factor;
                    if (edge < 0.0) edge = 0.0;
                    if (edge > 1.0) edge = 1.0;

                    double srcFactor = factor;
                    double tintFactor = edge * tintAf;

                    double rOut = r * srcFactor + tintR * tintFactor;
                    double gOut = g * srcFactor + tintG * tintFactor;
                    double bOut = b * srcFactor + tintB * tintFactor;

                    byte rFinal = (byte)Math.Clamp((int)Math.Round(rOut), 0, 255);
                    byte gFinal = (byte)Math.Clamp((int)Math.Round(gOut), 0, 255);
                    byte bFinal = (byte)Math.Clamp((int)Math.Round(bOut), 0, 255);

                    byte aFinal;
                    if (wasTransparent)
                    {
                        // new alpha driven by vignette/tint strength at this radius
                        double alphaOut = tintAf * edge;
                        aFinal = (byte)Math.Clamp((int)Math.Round(alphaOut * 255.0), 0, 255);
                        if (aFinal == 0)
                            continue; // still effectively invisible
                    }
                    else
                    {
                        aFinal = a;
                    }

                    pixels[idx] = (uint)aFinal << 24 | (uint)rFinal << 16 | (uint)gFinal << 8 | bFinal;
                }
            }
        }

    }
}
