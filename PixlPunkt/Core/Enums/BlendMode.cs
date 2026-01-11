namespace PixlPunkt.Core.Enums
{
    /// <summary>
    /// Defines the blending modes used for layer compositing and painting operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Blend modes control how pixels from one layer combine with pixels from layers below.
    /// Each mode uses a different mathematical formula to compute the final color value.
    /// </para>
    /// <para>
    /// These blend modes are commonly used in digital art applications and follow standard
    /// compositing formulas for predictable results across different tools.
    /// </para>
    /// </remarks>
    public enum BlendMode
    {
        /// <summary>
        /// Standard alpha blending with no color modification.
        /// </summary>
        /// <remarks>
        /// Formula: Result = Source * Alpha + Destination * (1 - Alpha)
        /// </remarks>
        Normal,

        /// <summary>
        /// Multiplies the source and destination colors, producing a darker result.
        /// </summary>
        /// <remarks>
        /// Formula: Result = Source * Destination
        /// </remarks>
        Multiply,

        /// <summary>
        /// Adds the source and destination colors together, producing a brighter result.
        /// </summary>
        /// <remarks>
        /// Formula: Result = Source + Destination (clamped to maximum)
        /// </remarks>
        Add,

        /// <summary>
        /// Computes the absolute difference between source and destination colors.
        /// </summary>
        /// <remarks>
        /// Formula: Result = |Source - Destination|
        /// </remarks>
        Difference,

        /// <summary>
        /// Selects the darker of the source and destination colors for each channel.
        /// </summary>
        /// <remarks>
        /// Formula: Result = min(Source, Destination)
        /// </remarks>
        Darken,

        /// <summary>
        /// Selects the lighter of the source and destination colors for each channel.
        /// </summary>
        /// <remarks>
        /// Formula: Result = max(Source, Destination)
        /// </remarks>
        Lighten,

        /// <summary>
        /// Combines Multiply and Screen blend modes based on the destination color.
        /// </summary>
        /// <remarks>
        /// Multiplies or screens colors depending on whether the destination is darker or lighter than 50% gray.
        /// </remarks>
        HardLight,

        /// <summary>
        /// Inverts the destination color based on the source color.
        /// </summary>
        /// <remarks>
        /// Formula: Result = 1 - Destination (or similar inversion formula)
        /// </remarks>
        Invert,

        /// <summary>
        /// Combines Multiply and Screen blend modes based on the source color.
        /// </summary>
        /// <remarks>
        /// Similar to HardLight but uses the source color to determine the blend method.
        /// </remarks>
        Overlay,

        /// <summary>
        /// Inverts both colors, multiplies them, then inverts again, producing a lighter result.
        /// </summary>
        /// <remarks>
        /// Formula: Result = 1 - (1 - Source) * (1 - Destination)
        /// </remarks>
        Screen,

        /// <summary>
        /// Subtracts the source color from the destination, producing a darker result.
        /// </summary>
        /// <remarks>
        /// Formula: Result = Destination - Source (clamped to minimum)
        /// </remarks>
        Subtract
    }
}
