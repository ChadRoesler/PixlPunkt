namespace PixlPunkt.Core.Effects
{
    /// <summary>
    /// String constants for built-in effect IDs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All built-in effects follow the convention <c>pixlpunkt.effect.{name}</c>.
    /// Plugin effects should use their own vendor prefix (e.g., <c>com.mycompany.effect.blur</c>).
    /// </para>
    /// <para>
    /// These IDs are used to:
    /// </para>
    /// <list type="bullet">
    /// <item>Register effects with <see cref="EffectRegistry"/></item>
    /// <item>Look up effect registrations by ID</item>
    /// <item>Associate effect instances with their registration metadata</item>
    /// </list>
    /// </remarks>
    public static class EffectIds
    {
        //////////////////////////////////////////////////////////////////
        // STYLIZE EFFECTS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// ID for the drop shadow effect that adds a shadow beneath the layer.
        /// </summary>
        public const string DropShadow = "pixlpunkt.effect.dropshadow";

        /// <summary>
        /// ID for the outline effect that draws a border around opaque pixels.
        /// </summary>
        public const string Outline = "pixlpunkt.effect.outline";

        /// <summary>
        /// ID for the glow/bloom effect that adds a soft glow around bright areas.
        /// </summary>
        public const string GlowBloom = "pixlpunkt.effect.glowbloom";

        /// <summary>
        /// ID for the chromatic aberration effect that simulates lens distortion.
        /// </summary>
        public const string ChromaticAberration = "pixlpunkt.effect.chromaticaberration";

        //////////////////////////////////////////////////////////////////
        // FILTER EFFECTS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// ID for the scan lines effect that simulates CRT horizontal lines.
        /// </summary>
        public const string ScanLines = "pixlpunkt.effect.scanlines";

        /// <summary>
        /// ID for the grain effect that adds film grain or noise texture.
        /// </summary>
        public const string Grain = "pixlpunkt.effect.grain";

        /// <summary>
        /// ID for the vignette effect that darkens edges of the layer.
        /// </summary>
        public const string Vignette = "pixlpunkt.effect.vignette";

        /// <summary>
        /// ID for the CRT effect that simulates a curved CRT monitor display.
        /// </summary>
        public const string Crt = "pixlpunkt.effect.crt";

        /// <summary>
        /// ID for the pixelate effect that creates a mosaic/pixelated appearance.
        /// </summary>
        public const string Pixelate = "pixlpunkt.effect.pixelate";

        //////////////////////////////////////////////////////////////////
        // COLOR EFFECTS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// ID for the color adjust effect that modifies hue, saturation, and brightness.
        /// </summary>
        public const string ColorAdjust = "pixlpunkt.effect.coloradjust";

        /// <summary>
        /// ID for the palette quantize effect that reduces colors to a specific palette.
        /// </summary>
        public const string PaletteQuantize = "pixlpunkt.effect.palettequantize";

        /// <summary>
        /// ID for the ASCII effect that converts the layer to ASCII art representation.
        /// </summary>
        public const string Ascii = "pixlpunkt.effect.ascii";
    }
}
