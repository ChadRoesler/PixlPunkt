namespace PixlPunkt.PluginSdk.Constants
{
    /// <summary>
    /// Constants defining valid ranges for tool settings (brush size, opacity, density, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ToolLimits"/> provides centralized constants for all tool-related settings
    /// to ensure consistency across built-in tools and plugins. Use these constants for:
    /// </para>
    /// <list type="bullet">
    /// <item>Slider min/max values in tool options UI</item>
    /// <item>Clamping values in SetXxx methods</item>
    /// <item>Default values for new tool settings</item>
    /// </list>
    /// <para>
    /// <strong>Plugin Usage:</strong>
    /// </para>
    /// <code>
    /// yield return new SliderOption("size", "Size", 
    ///     ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize, 
    ///     _size, v => SetSize((int)v));
    /// </code>
    /// </remarks>
    public static class ToolLimits
    {
        // ====================================================================
        // BRUSH SIZE
        // ====================================================================

        /// <summary>Minimum brush/stroke size in pixels.</summary>
        public const int MinBrushSize = 1;

        /// <summary>Maximum brush/stroke size in pixels.</summary>
        public const int MaxBrushSize = 128;

        /// <summary>Default brush/stroke size for new tools.</summary>
        public const int DefaultBrushSize = 8;

        // ====================================================================
        // OPACITY
        // ====================================================================

        /// <summary>Minimum opacity value (fully transparent).</summary>
        public const byte MinOpacity = 0;

        /// <summary>Maximum opacity value (fully opaque).</summary>
        public const byte MaxOpacity = 255;

        /// <summary>Default opacity for new brush tools.</summary>
        public const byte DefaultOpacity = 255;

        // ====================================================================
        // DENSITY (SOFTNESS/HARDNESS)
        // ====================================================================

        /// <summary>Minimum density value (softest edge).</summary>
        public const byte MinDensity = 0;

        /// <summary>Maximum density value (hard edge).</summary>
        public const byte MaxDensity = 255;

        /// <summary>Default density for new brush tools.</summary>
        public const byte DefaultDensity = 255;

        // ====================================================================
        // TOLERANCE (FILL, WAND, ETC.)
        // ====================================================================

        /// <summary>Minimum color tolerance (exact match only).</summary>
        public const int MinTolerance = 0;

        /// <summary>Maximum color tolerance (very loose matching).</summary>
        public const int MaxTolerance = 255;

        /// <summary>Default tolerance for fill/wand tools.</summary>
        public const int DefaultTolerance = 32;

        // ====================================================================
        // STRENGTH (PERCENTAGE-BASED EFFECTS)
        // ====================================================================

        /// <summary>Minimum strength percentage.</summary>
        public const int MinStrengthPercent = 0;

        /// <summary>Maximum strength percentage.</summary>
        public const int MaxStrengthPercent = 100;

        /// <summary>Default strength for smudge/blur tools.</summary>
        public const int DefaultStrengthPercent = 50;

        // ====================================================================
        // GAMMA (FALLOFF CURVES)
        // ====================================================================

        /// <summary>Minimum gamma value.</summary>
        public const double MinGamma = 0.1;

        /// <summary>Maximum gamma value.</summary>
        public const double MaxGamma = 5.0;

        /// <summary>Default gamma for falloff curves.</summary>
        public const double DefaultGamma = 1.0;

        // ====================================================================
        // STROKE WIDTH (SHAPE TOOLS)
        // ====================================================================

        /// <summary>Minimum stroke width for shape outlines.</summary>
        public const int MinStrokeWidth = 1;

        /// <summary>Maximum stroke width for shape outlines.</summary>
        public const int MaxStrokeWidth = 64;

        /// <summary>Default stroke width for shape tools.</summary>
        public const int DefaultStrokeWidth = 1;
    }
}
