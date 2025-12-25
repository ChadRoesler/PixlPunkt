namespace PixlPunkt.PluginSdk.Constants
{
    /// <summary>
    /// Mathematical constants for plugin development.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="MathConstants"/> provides common mathematical constants useful for:
    /// </para>
    /// <list type="bullet">
    /// <item>Angular calculations (degrees, radians)</item>
    /// <item>Floating-point comparisons (epsilon values)</item>
    /// <item>Normalized value ranges (0-1)</item>
    /// <item>Percentage calculations</item>
    /// </list>
    /// <para>
    /// <strong>Plugin Usage:</strong>
    /// </para>
    /// <code>
    /// // Convert degrees to radians
    /// double radians = degrees * Math.PI / MathConstants.HalfCircle;
    /// 
    /// // Check for near-zero with epsilon
    /// if (Math.Abs(value) &lt; MathConstants.EpsilonSmall)
    /// {
    ///     // Treat as zero
    /// }
    /// 
    /// // Clamp to normalized range
    /// double normalized = Math.Clamp(value, MathConstants.NormalizedMin, MathConstants.NormalizedMax);
    /// </code>
    /// </remarks>
    public static class MathConstants
    {
        // ====================================================================
        // ANGULAR CONSTANTS (DEGREES)
        // ====================================================================

        /// <summary>Number of degrees in a full circle (360°).</summary>
        public const double DegreesInCircle = 360.0;

        /// <summary>Number of degrees in a half circle (180°).</summary>
        public const double HalfCircle = 180.0;

        /// <summary>Number of degrees in a quarter circle (90°).</summary>
        public const double QuarterCircle = 90.0;

        /// <summary>Number of degrees per HSL/HSV hue sector (60°).</summary>
        public const double HueSectorDegrees = 60.0;

        // ====================================================================
        // ANGULAR CONSTANTS (RADIANS)
        // ====================================================================

        /// <summary>Value of 2? (full circle in radians).</summary>
        public const double TwoPi = Math.PI * 2.0;

        /// <summary>Value of ?/2 (quarter circle in radians).</summary>
        public const double HalfPi = Math.PI / 2.0;

        /// <summary>Conversion factor: multiply degrees by this to get radians.</summary>
        public const double DegreesToRadians = Math.PI / 180.0;

        /// <summary>Conversion factor: multiply radians by this to get degrees.</summary>
        public const double RadiansToDegrees = 180.0 / Math.PI;

        // ====================================================================
        // EPSILON VALUES (FLOATING POINT COMPARISONS)
        // ====================================================================

        /// <summary>
        /// Small epsilon value for general floating-point comparisons (10??).
        /// </summary>
        /// <remarks>
        /// Use this for most floating-point equality checks where values are
        /// expected to be in a reasonable range (0-1000).
        /// </remarks>
        public const double EpsilonSmall = 1e-6;

        /// <summary>
        /// Tiny epsilon value for precise floating-point comparisons (10?¹?).
        /// </summary>
        /// <remarks>
        /// Use this when very high precision is required, such as for
        /// accumulated calculations or geometric algorithms.
        /// </remarks>
        public const double EpsilonTiny = 1e-10;

        /// <summary>
        /// Threshold for HSL/HSV delta comparison (10??).
        /// </summary>
        /// <remarks>
        /// Used in color space conversion to detect achromatic colors
        /// (no saturation) where hue is undefined.
        /// </remarks>
        public const double ColorDeltaEpsilon = 1e-9;

        // ====================================================================
        // NORMALIZED RANGES
        // ====================================================================

        /// <summary>Minimum normalized value (0.0).</summary>
        public const double NormalizedMin = 0.0;

        /// <summary>Maximum normalized value (1.0).</summary>
        public const double NormalizedMax = 1.0;

        /// <summary>Half normalized value (0.5).</summary>
        public const double NormalizedHalf = 0.5;

        // ====================================================================
        // PERCENTAGE
        // ====================================================================

        /// <summary>Maximum percentage value (100%).</summary>
        public const double PercentageMax = 100.0;

        /// <summary>Half percentage value (50%).</summary>
        public const double PercentageHalf = 50.0;

        // ====================================================================
        // INTERPOLATION
        // ====================================================================

        /// <summary>
        /// Hue wrap-around threshold (540°) used for shortest-path hue interpolation.
        /// </summary>
        /// <remarks>
        /// When interpolating hues, add 540 then mod 360 to find shortest path direction.
        /// </remarks>
        public const double HueWrapThreshold = 540.0;

        /// <summary>
        /// sRGB linearization threshold (0.03928).
        /// </summary>
        /// <remarks>
        /// Values below this threshold use linear scaling (÷12.92),
        /// values above use gamma curve.
        /// </remarks>
        public const double SrgbLinearThreshold = 0.03928;

        /// <summary>
        /// sRGB gamma exponent (2.4).
        /// </summary>
        public const double SrgbGamma = 2.4;
    }
}
