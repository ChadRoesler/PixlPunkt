namespace PixlPunkt.Uno.Core.Enums
{
    /// <summary>
    /// Defines the sorting mode for palette colors.
    /// </summary>
    public enum PaletteSortMode
    {
        /// <summary>
        /// Keep colors in their original order (as defined in the palette file or as added).
        /// </summary>
        Default = 0,

        /// <summary>
        /// Sort colors by hue (0-360 degrees on the color wheel).
        /// Groups colors by their base color family (reds, oranges, yellows, greens, etc.).
        /// </summary>
        Hue = 1,

        /// <summary>
        /// Sort colors by saturation (0-100%).
        /// Orders from grayscale/muted colors to vivid/pure colors.
        /// </summary>
        Saturation = 2,

        /// <summary>
        /// Sort colors by lightness/brightness (0-100%).
        /// Orders from dark to light.
        /// </summary>
        Lightness = 3,

        /// <summary>
        /// Sort colors by luminance (perceived brightness using WCAG formula).
        /// Orders from dark to light based on human perception.
        /// </summary>
        Luminance = 4,

        /// <summary>
        /// Sort colors by red channel value (0-255).
        /// </summary>
        Red = 5,

        /// <summary>
        /// Sort colors by green channel value (0-255).
        /// </summary>
        Green = 6,

        /// <summary>
        /// Sort colors by blue channel value (0-255).
        /// </summary>
        Blue = 7,

        /// <summary>
        /// Reverse the current palette order.
        /// </summary>
        Reverse = 8
    }
}
