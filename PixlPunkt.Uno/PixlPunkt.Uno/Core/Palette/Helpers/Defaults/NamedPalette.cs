namespace PixlPunkt.Uno.Core.Palette.Helpers.Defaults
{
    /// <summary>
    /// Immutable record representing a named color palette with a display name and color array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NamedPalette provides a simple container for predefined color palettes used throughout
    /// PixlPunkt. Each palette consists of a human-readable name and an array of BGRA colors.
    /// Being a record type, instances are immutable and support value-based equality comparison.
    /// </para>
    /// <para><strong>Usage:</strong></para>
    /// <para>
    /// Palettes are primarily defined in <see cref="DefaultPalettes"/> and used by the
    /// <see cref="PaletteService"/> for color picking, palette cycling effects, and color quantization.
    /// The immutable nature ensures palette definitions remain consistent and can be safely shared
    /// across UI components.
    /// </para>
    /// </remarks>
    /// <seealso cref="DefaultPalettes"/>
    /// <seealso cref="PaletteService"/>
    public sealed record NamedPalette
    {
        /// <summary>
        /// Gets or initializes the display name of the palette.
        /// </summary>
        /// <value>
        /// A human-readable string describing the palette (e.g., "PICO-8 (16)", "Game Boy (DMG)")
        /// </value>
        public string Name { get; init; }

        /// <summary>
        /// Gets or initializes the array of colors in this palette.
        /// </summary>
        /// <value>
        /// An array of BGRA packed 32-bit color values (0xAARRGGBB format).
        /// Array length varies by palette (typically 4-16 colors for retro palettes, up to 100+ for extended sets).
        /// </value>
        public uint[] Colors { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPalette"/> record.
        /// </summary>
        /// <param name="name">The display name for this palette.</param>
        /// <param name="colors">The array of BGRA colors comprising this palette.</param>
        public NamedPalette(string name, uint[] colors)
        {
            Name = name;
            Colors = colors;
        }
    }
}
