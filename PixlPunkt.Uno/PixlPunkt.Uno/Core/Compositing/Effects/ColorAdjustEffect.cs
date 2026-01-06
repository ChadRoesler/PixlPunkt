using System;
using PixlPunkt.Uno.Core.Coloring.Helpers;

namespace PixlPunkt.Uno.Core.Compositing.Effects
{
    /// <summary>
    /// Adjusts layer colors by modifying hue, saturation, and brightness (value) in HSV color space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect provides independent control over three fundamental color properties: hue (color wheel
    /// rotation), saturation (color purity/intensity), and value (brightness). All adjustments are performed
    /// in HSV color space which separates these perceptual attributes, making intuitive color corrections
    /// possible without affecting alpha channel.
    /// </para>
    /// <para><strong>Parameters:</strong></para>
    /// <list type="bullet">
    /// <item><strong>HueShiftDegrees</strong> (-180..180°): Rotates colors around the color wheel.
    /// ±180° inverts hue (red ↔ cyan, green ↔ magenta, blue ↔ yellow). Useful for colorizing,
    /// creating duotone effects, or correcting color casts.</item>
    /// <item><strong>SaturationScale</strong> (0..2): Multiplies color saturation. At 0, produces
    /// grayscale. At 1, no change. At 2, doubles saturation (oversaturated/neon colors).</item>
    /// <item><strong>ValueScale</strong> (0..2): Multiplies brightness. At 0, pure black. At 1,
    /// no change. At 2, doubles brightness (may clip to white).</item>
    /// </list>
    /// <para><strong>HSV Color Space:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Hue (H)</strong>: Angular position on color wheel (0-360°). 0°=red, 120°=green, 240°=blue.</item>
    /// <item><strong>Saturation (S)</strong>: Color purity (0..1). 0 = gray, 1 = pure spectral color.</item>
    /// <item><strong>Value (V)</strong>: Brightness (0..1). 0 = black, 1 = maximum brightness.</item>
    /// </list>
    /// <para><strong>Algorithm:</strong></para>
    /// <list type="number">
    /// <item>Convert pixel from RGB to HSV using standard conversion formulas.</item>
    /// <item>Apply transformations: H' = (H + shift) mod 360, S' = clamp(S × satScale, 0, 1), V' = clamp(V × valScale, 0, 1).</item>
    /// <item>Convert back from HSV to RGB for final output pixel.</item>
    /// </list>
    /// <para><strong>Use Cases:</strong></para>
    /// <para>
    /// Color grading, white balance correction, desaturation (grayscale), brightness adjustment,
    /// colorization effects, and complementary color shifts for artistic stylization.
    /// </para>
    /// </remarks>
    public sealed class ColorAdjustEffect : LayerEffectBase
    {
        public override string DisplayName => "Color Adjust";

        // degrees, -180..180
        private double _hueShiftDegrees = 0.0;
        public double HueShiftDegrees
        {
            get => _hueShiftDegrees;
            set
            {
                double clamped = Math.Clamp(value, -180.0, 180.0);
                if (Math.Abs(_hueShiftDegrees - clamped) > double.Epsilon)
                {
                    _hueShiftDegrees = clamped;
                    OnPropertyChanged();
                }
            }
        }

        // saturation multiplier, 0..2
        private double _saturationScale = 1.0;
        public double SaturationScale
        {
            get => _saturationScale;
            set
            {
                double clamped = Math.Clamp(value, 0.0, 2.0);
                if (Math.Abs(_saturationScale - clamped) > double.Epsilon)
                {
                    _saturationScale = clamped;
                    OnPropertyChanged();
                }
            }
        }

        // value/brightness multiplier, 0..2
        private double _valueScale = 1.0;
        public double ValueScale
        {
            get => _valueScale;
            set
            {
                double clamped = Math.Clamp(value, 0.0, 2.0);
                if (Math.Abs(_valueScale - clamped) > double.Epsilon)
                {
                    _valueScale = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            int len = width * height;
            if (len == 0 || pixels.Length < len) return;

            double hueShift = HueShiftDegrees;
            double satScale = SaturationScale;
            double valScale = ValueScale;

            for (int i = 0; i < len; i++)
            {
                uint c = pixels[i];
                ColorUtil.Unpack(c, out byte a, out byte r, out byte g, out byte b);
                if (a == 0) continue;

                double rf = r / 255.0;
                double gf = g / 255.0;
                double bf = b / 255.0;

                ColorUtil.RgbToHsv(rf, gf, bf, out double h, out double s, out double v);

                h = (h + hueShift) % 360.0;
                if (h < 0) h += 360.0;
                s = Math.Clamp(s * satScale, 0.0, 1.0);
                v = Math.Clamp(v * valScale, 0.0, 1.0);

                ColorUtil.HsvToRgb(h, s, v, out double nr, out double ng, out double nb);

                byte nrB = (byte)Math.Clamp((int)Math.Round(nr * 255.0), 0, 255);
                byte ngB = (byte)Math.Clamp((int)Math.Round(ng * 255.0), 0, 255);
                byte nbB = (byte)Math.Clamp((int)Math.Round(nb * 255.0), 0, 255);

                pixels[i] = ColorUtil.Pack(a, nrB, ngB, nbB);
            }
        }

    }
}
