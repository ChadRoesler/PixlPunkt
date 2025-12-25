using PixlPunkt.PluginSdk.Compositing;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.ExamplePlugin.Effects
{
    /// <summary>
    /// A halftone effect that creates a dot pattern overlay.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an example implementation of <see cref="LayerEffectBase"/> that demonstrates:
    /// </para>
    /// <list type="bullet">
    /// <item>Implementing a custom layer effect</item>
    /// <item>Exposing configurable properties</item>
    /// <item>Processing pixels efficiently</item>
    /// <item>Providing settings options for UI generation</item>
    /// </list>
    /// </remarks>
    public sealed class HalftoneEffect : LayerEffectBase
    {
        /// <inheritdoc/>
        public override string DisplayName => "Halftone";

        // ====================================================================
        // EFFECT PROPERTIES
        // ====================================================================

        private int _dotSize = 4;
        /// <summary>
        /// Gets or sets the size of halftone dots (2-16 pixels).
        /// </summary>
        public int DotSize
        {
            get => _dotSize;
            set
            {
                int clamped = Math.Clamp(value, 2, 16);
                if (_dotSize != clamped)
                {
                    _dotSize = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private double _angle = 45.0;
        /// <summary>
        /// Gets or sets the rotation angle of the dot pattern (0-360 degrees).
        /// </summary>
        public double Angle
        {
            get => _angle;
            set
            {
                double clamped = Math.Clamp(value, 0.0, 360.0);
                if (Math.Abs(_angle - clamped) > 0.001)
                {
                    _angle = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private double _contrast = 1.0;
        /// <summary>
        /// Gets or sets the contrast of the halftone pattern (0.5-2.0).
        /// </summary>
        public double Contrast
        {
            get => _contrast;
            set
            {
                double clamped = Math.Clamp(value, 0.5, 2.0);
                if (Math.Abs(_contrast - clamped) > 0.001)
                {
                    _contrast = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private bool _monochrome = true;
        /// <summary>
        /// Gets or sets whether the output is monochrome (black/white) or colored.
        /// </summary>
        public bool Monochrome
        {
            get => _monochrome;
            set
            {
                if (_monochrome != value)
                {
                    _monochrome = value;
                    OnPropertyChanged();
                }
            }
        }

        // ====================================================================
        // EFFECT APPLICATION
        // ====================================================================

        /// <inheritdoc/>
        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;

            double radians = _angle * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    uint pixel = pixels[idx];

                    // Extract components
                    byte a = (byte)(pixel >> 24);
                    byte r = (byte)(pixel >> 16);
                    byte g = (byte)(pixel >> 8);
                    byte b = (byte)pixel;

                    // Skip fully transparent pixels
                    if (a == 0) continue;

                    // Calculate luminance
                    double lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;

                    // Apply contrast
                    lum = Math.Clamp((lum - 0.5) * _contrast + 0.5, 0.0, 1.0);

                    // Calculate rotated position for pattern
                    double rx = x * cos - y * sin;
                    double ry = x * sin + y * cos;

                    // Calculate distance from nearest dot center
                    double cellX = Math.Floor(rx / _dotSize + 0.5) * _dotSize;
                    double cellY = Math.Floor(ry / _dotSize + 0.5) * _dotSize;
                    double dx = rx - cellX;
                    double dy = ry - cellY;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    // Calculate dot radius based on luminance
                    double maxRadius = _dotSize * 0.5;
                    double dotRadius = maxRadius * (1.0 - lum);

                    // Determine if pixel is inside dot
                    bool insideDot = dist <= dotRadius;

                    if (_monochrome)
                    {
                        // Monochrome: black dots on white, or white dots on black
                        if (insideDot)
                        {
                            pixels[idx] = (uint)((a << 24) | 0x000000); // Black
                        }
                        else
                        {
                            pixels[idx] = (uint)((a << 24) | 0xFFFFFF); // White
                        }
                    }
                    else
                    {
                        // Color: modulate original color based on dot pattern
                        if (!insideDot)
                        {
                            // Outside dot - lighten
                            r = (byte)Math.Min(255, r + 64);
                            g = (byte)Math.Min(255, g + 64);
                            b = (byte)Math.Min(255, b + 64);
                            pixels[idx] = (uint)((a << 24) | (r << 16) | (g << 8) | b);
                        }
                        // Inside dot - keep original color
                    }
                }
            }
        }

        // ====================================================================
        // SETTINGS OPTIONS
        // ====================================================================

        /// <summary>
        /// Gets the settings options for this effect instance.
        /// </summary>
        /// <returns>Enumerable of tool options for the settings UI.</returns>
        public IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption(
                "dotSize", "Dot Size", 2, 16, DotSize,
                v => DotSize = (int)v,
                Order: 0, Tooltip: "Size of halftone dots in pixels");

            yield return new SliderOption(
                "angle", "Angle", 0, 360, Angle,
                v => Angle = v,
                Order: 1, Step: 5, Tooltip: "Rotation angle of dot pattern");

            yield return new SliderOption(
                "contrast", "Contrast", 0.5, 2.0, Contrast,
                v => Contrast = v,
                Order: 2, Step: 0.1, Tooltip: "Contrast of the pattern");

            yield return new ToggleOption(
                "monochrome", "Monochrome", Monochrome,
                v => Monochrome = v,
                Order: 3, Tooltip: "Output black and white only");
        }
    }
}
