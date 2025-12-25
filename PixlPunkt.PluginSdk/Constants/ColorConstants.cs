namespace PixlPunkt.PluginSdk.Constants
{
    /// <summary>
    /// Constants for color manipulation and pixel operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ColorConstants"/> provides common color-related constants for plugin development.
    /// These constants are useful for:
    /// </para>
    /// <list type="bullet">
    /// <item>Bit manipulation for packed BGRA colors</item>
    /// <item>Channel extraction and masking</item>
    /// <item>Alpha value ranges</item>
    /// <item>Byte value ranges</item>
    /// </list>
    /// <para>
    /// <strong>Plugin Usage:</strong>
    /// </para>
    /// <code>
    /// // Extract RGB, ignoring alpha
    /// uint rgb = color &amp; ColorConstants.RgbMask;
    /// 
    /// // Set full opacity
    /// uint opaqueColor = rgb | (ColorConstants.FullAlpha &lt;&lt; ColorConstants.AlphaShift);
    /// 
    /// // Check if fully transparent
    /// byte alpha = (byte)(color >> ColorConstants.AlphaShift);
    /// if (alpha == ColorConstants.TransparentAlpha)
    /// {
    ///     // Handle transparent pixel
    /// }
    /// </code>
    /// </remarks>
    public static class ColorConstants
    {
        // ====================================================================
        // COLOR MASKS
        // ====================================================================

        /// <summary>
        /// Mask to extract RGB channels from a packed BGRA value (strips alpha).
        /// </summary>
        /// <remarks>
        /// Usage: <c>uint rgb = bgra &amp; RgbMask;</c>
        /// </remarks>
        public const uint RgbMask = 0x00FFFFFFu;

        /// <summary>
        /// Alias for RgbMask using uppercase naming convention.
        /// </summary>
        public const uint RGBMask = RgbMask;

        /// <summary>
        /// Mask to extract only the alpha channel from a packed BGRA value.
        /// </summary>
        /// <remarks>
        /// Usage: <c>uint alphaOnly = bgra &amp; AlphaMask;</c>
        /// </remarks>
        public const uint AlphaMask = 0xFF000000u;

        /// <summary>
        /// Bit shift amount to access the alpha channel in packed BGRA format.
        /// </summary>
        /// <remarks>
        /// Usage: <c>byte alpha = (byte)(bgra >> AlphaShift);</c>
        /// </remarks>
        public const int AlphaShift = 24;

        /// <summary>
        /// Bit shift amount to access the red channel in packed BGRA format.
        /// </summary>
        public const int RedShift = 16;

        /// <summary>
        /// Bit shift amount to access the green channel in packed BGRA format.
        /// </summary>
        public const int GreenShift = 8;

        /// <summary>
        /// Bit shift amount to access the blue channel in packed BGRA format (no shift needed).
        /// </summary>
        public const int BlueShift = 0;

        // ====================================================================
        // BYTE RANGES
        // ====================================================================

        /// <summary>Minimum byte value (0).</summary>
        public const byte MinByte = 0;

        /// <summary>Maximum byte value (255).</summary>
        public const byte MaxByte = 255;

        /// <summary>Maximum byte value as int for calculations (255).</summary>
        public const int MaxByteValue = 255;

        /// <summary>Half of max byte value, useful for rounding bias (127).</summary>
        public const int HalfByteValue = 127;

        // ====================================================================
        // ALPHA VALUES
        // ====================================================================

        /// <summary>Fully opaque alpha value (255).</summary>
        public const byte FullAlpha = 255;

        /// <summary>Fully transparent alpha value (0).</summary>
        public const byte TransparentAlpha = 0;

        /// <summary>Pre-packed fully opaque alpha for BGRA (0xFF000000).</summary>
        public const uint OpaqueAlphaMask = 0xFF000000u;

        // ====================================================================
        // COMMON COLORS (BGRA PACKED)
        // ====================================================================

        /// <summary>Transparent black (0x00000000).</summary>
        public const uint TransparentBlack = 0x00000000u;

        /// <summary>Opaque black (0xFF000000).</summary>
        public const uint OpaqueBlack = 0xFF000000u;

        /// <summary>Opaque white (0xFFFFFFFF).</summary>
        public const uint OpaqueWhite = 0xFFFFFFFFu;

        // ====================================================================
        // PIXEL FORMAT
        // ====================================================================

        /// <summary>Number of bytes per pixel in BGRA format.</summary>
        public const int BytesPerPixel = 4;

        /// <summary>Number of bits per pixel in BGRA format.</summary>
        public const int BitsPerPixel = 32;

        // ====================================================================
        // UI OPACITY VALUES
        // ====================================================================

        /// <summary>Opacity for info text overlays (70%).</summary>
        public const double InfoTextOpacity = 0.7;

        /// <summary>Opacity for disabled UI elements (50%).</summary>
        public const double DisabledOpacity = 0.5;

        /// <summary>Full opacity for UI elements (100%).</summary>
        public const double FullOpacity = 1.0;

        // ====================================================================
        // PALETTE MERGING
        // ====================================================================

        /// <summary>Minimum color merge tolerance for palette consolidation.</summary>
        public const int ColorMergeToleranceMin = 0;

        /// <summary>Maximum color merge tolerance for palette consolidation.</summary>
        public const int ColorMergeToleranceMax = 64;

        /// <summary>Default step for color merge tolerance slider.</summary>
        public const int ColorMergeToleranceStep = 1;
    }
}
