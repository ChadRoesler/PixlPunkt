namespace PixlPunkt.PluginSdk.Constants
{
    /// <summary>
    /// Constants defining valid ranges for common effect parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="EffectLimits"/> provides centralized constants for layer effect settings
    /// to ensure consistency across built-in effects and plugins. Use these constants for:
    /// </para>
    /// <list type="bullet">
    /// <item>Slider min/max values in effect settings UI</item>
    /// <item>Clamping values in property setters</item>
    /// <item>Default values for new effects</item>
    /// </list>
    /// <para>
    /// <strong>Plugin Usage:</strong>
    /// </para>
    /// <code>
    /// public override IEnumerable&lt;IToolOption&gt; GetOptions()
    /// {
    ///     yield return new SliderOption("radius", "Radius",
    ///         EffectLimits.MinRadius, EffectLimits.MaxRadius,
    ///         _effect.Radius, v => _effect.Radius = (int)v);
    ///     
    ///     yield return new SliderOption("intensity", "Intensity",
    ///         EffectLimits.MinIntensityPercent, EffectLimits.MaxIntensityPercent,
    ///         _effect.Intensity, v => _effect.Intensity = (int)v);
    /// }
    /// </code>
    /// </remarks>
    public static class EffectLimits
    {
        // ====================================================================
        // RADIUS / SIZE (Blur, Shadow, Glow)
        // ====================================================================

        /// <summary>Minimum radius for blur/glow effects.</summary>
        public const int MinRadius = 0;

        /// <summary>Maximum radius for blur/glow effects.</summary>
        public const int MaxRadius = 64;

        /// <summary>Default radius for blur/glow effects.</summary>
        public const int DefaultRadius = 4;

        // ====================================================================
        // OFFSET (Shadow, Chromatic Aberration)
        // ====================================================================

        /// <summary>Minimum offset value (negative allowed).</summary>
        public const int MinOffset = -64;

        /// <summary>Maximum offset value.</summary>
        public const int MaxOffset = 64;

        /// <summary>Default offset for shadow effects.</summary>
        public const int DefaultOffset = 2;

        // ====================================================================
        // INTENSITY / STRENGTH (Percentage-based)
        // ====================================================================

        /// <summary>Minimum intensity percentage.</summary>
        public const int MinIntensityPercent = 0;

        /// <summary>Maximum intensity percentage.</summary>
        public const int MaxIntensityPercent = 100;

        /// <summary>Default intensity percentage.</summary>
        public const int DefaultIntensityPercent = 50;

        // ====================================================================
        // OPACITY (0-255 byte range)
        // ====================================================================

        /// <summary>Minimum effect opacity.</summary>
        public const byte MinOpacity = 0;

        /// <summary>Maximum effect opacity.</summary>
        public const byte MaxOpacity = 255;

        /// <summary>Default effect opacity.</summary>
        public const byte DefaultOpacity = 255;

        // ====================================================================
        // THICKNESS (Outline, Border)
        // ====================================================================

        /// <summary>Minimum thickness for outline effects.</summary>
        public const int MinThickness = 1;

        /// <summary>Maximum thickness for outline effects.</summary>
        public const int MaxThickness = 32;

        /// <summary>Default thickness for outline effects.</summary>
        public const int DefaultThickness = 1;

        // ====================================================================
        // PIXELATION / BLOCK SIZE
        // ====================================================================

        /// <summary>Minimum block size for pixelation effects.</summary>
        public const int MinBlockSize = 1;

        /// <summary>Maximum block size for pixelation effects.</summary>
        public const int MaxBlockSize = 64;

        /// <summary>Default block size for pixelation effects.</summary>
        public const int DefaultBlockSize = 4;

        // ====================================================================
        // LINE SPACING (Scan Lines, ASCII)
        // ====================================================================

        /// <summary>Minimum line spacing.</summary>
        public const int MinLineSpacing = 1;

        /// <summary>Maximum line spacing.</summary>
        public const int MaxLineSpacing = 32;

        /// <summary>Default line spacing.</summary>
        public const int DefaultLineSpacing = 2;

        // ====================================================================
        // COLOR ADJUSTMENTS (HSV shifts)
        // ====================================================================

        /// <summary>Minimum hue shift in degrees.</summary>
        public const int MinHueShift = -180;

        /// <summary>Maximum hue shift in degrees.</summary>
        public const int MaxHueShift = 180;

        /// <summary>Minimum saturation adjustment.</summary>
        public const int MinSaturationAdjust = -100;

        /// <summary>Maximum saturation adjustment.</summary>
        public const int MaxSaturationAdjust = 100;

        /// <summary>Minimum brightness adjustment.</summary>
        public const int MinBrightnessAdjust = -100;

        /// <summary>Maximum brightness adjustment.</summary>
        public const int MaxBrightnessAdjust = 100;

        /// <summary>Minimum contrast adjustment.</summary>
        public const int MinContrastAdjust = -100;

        /// <summary>Maximum contrast adjustment.</summary>
        public const int MaxContrastAdjust = 100;

        // ====================================================================
        // VIGNETTE
        // ====================================================================

        /// <summary>Minimum vignette radius (0 = full coverage).</summary>
        public const double MinVignetteRadius = 0.0;

        /// <summary>Maximum vignette radius.</summary>
        public const double MaxVignetteRadius = 2.0;

        /// <summary>Default vignette radius.</summary>
        public const double DefaultVignetteRadius = 1.0;

        /// <summary>Minimum vignette softness.</summary>
        public const double MinVignetteSoftness = 0.0;

        /// <summary>Maximum vignette softness.</summary>
        public const double MaxVignetteSoftness = 1.0;

        /// <summary>Default vignette softness.</summary>
        public const double DefaultVignetteSoftness = 0.5;

        // ====================================================================
        // CRT EFFECTS
        // ====================================================================

        /// <summary>Minimum CRT curvature.</summary>
        public const double MinCurvature = 0.0;

        /// <summary>Maximum CRT curvature.</summary>
        public const double MaxCurvature = 1.0;

        /// <summary>Default CRT curvature.</summary>
        public const double DefaultCurvature = 0.3;
    }
}
