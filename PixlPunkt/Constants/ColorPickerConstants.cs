namespace PixlPunkt.Constants
{
    /// <summary>
    /// Constants for color picker dialogs and color space conversions.
    /// </summary>
    public static class ColorPickerConstants
    {
        // ════════════════════════════════════════════════════════════════════
        // WINDOW SIZING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Default minimum window width.</summary>
        public const double DefaultMinWidth = 420;

        /// <summary>Default minimum window height.</summary>
        public const double DefaultMinHeight = 380;

        /// <summary>Maximum fraction of screen size for window.</summary>
        public const double DefaultMaxScreenFraction = 0.90;

        // ════════════════════════════════════════════════════════════════════
        // HSL VALUE RANGES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Minimum hue value (degrees).</summary>
        public const double HueMin = 0.0;

        /// <summary>Maximum hue value (degrees).</summary>
        public const double HueMax = 360.0;

        /// <summary>Minimum saturation value (0-1).</summary>
        public const double SaturationMin = 0.0;

        /// <summary>Maximum saturation value (0-1).</summary>
        public const double SaturationMax = 1.0;

        /// <summary>Minimum lightness value (0-1).</summary>
        public const double LightnessMin = 0.0;

        /// <summary>Maximum lightness value (0-1).</summary>
        public const double LightnessMax = 1.0;

        // ════════════════════════════════════════════════════════════════════
        // RGB BYTE RANGES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Minimum RGB byte value.</summary>
        public const int RgbMin = 0;

        /// <summary>Maximum RGB byte value.</summary>
        public const int RgbMax = 255;

        // ════════════════════════════════════════════════════════════════════
        // PERCENTAGE CONVERSION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Divisor for converting to percentage (0-99 scale).</summary>
        public const double PercentageDivisor = 99.0;

        /// <summary>Multiplier for converting to percentage (0-99 scale).</summary>
        public const double PercentageMultiplier = 99.0;
    }
}
