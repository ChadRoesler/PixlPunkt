using PixlPunkt.Uno.Core.Imaging;

namespace PixlPunkt.Uno.Core.Painting
{
    /// <summary>
    /// Context for shape rendering operations containing all configuration needed
    /// to apply shape points to a surface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This context is passed to <see cref="IShapeRenderer.Render"/> and contains
    /// all the information needed to apply brush-stroked rendering to shape points.
    /// </para>
    /// <para>
    /// The context is designed to be immutable after construction, allowing renderers
    /// to safely cache or share context instances.
    /// </para>
    /// </remarks>
    public sealed class ShapeRenderContext
    {
        /// <summary>
        /// Gets the target pixel surface for rendering.
        /// </summary>
        public required PixelSurface Surface { get; init; }

        /// <summary>
        /// Gets the BGRA color to use for rendering.
        /// </summary>
        public required uint Color { get; init; }

        /// <summary>
        /// Gets the stroke width (brush size) for outline rendering.
        /// For filled shapes, this affects the edge softness.
        /// </summary>
        public required int StrokeWidth { get; init; }

        /// <summary>
        /// Gets the brush shape used for stroke rendering.
        /// </summary>
        public required BrushShape BrushShape { get; init; }

        /// <summary>
        /// Gets the opacity (0-255) applied to the rendered pixels.
        /// </summary>
        public required byte Opacity { get; init; }

        /// <summary>
        /// Gets the density (0-255) controlling soft falloff thickness.
        /// </summary>
        public required byte Density { get; init; }

        /// <summary>
        /// Gets whether this is a filled shape (enables optimized rendering).
        /// </summary>
        /// <remarks>
        /// When true, the renderer can use optimized paths:
        /// <list type="bullet">
        /// <item>Interior pixels receive uniform opacity without per-pixel alpha calculations</item>
        /// <item>Density-based falloff only applies to edge pixels</item>
        /// </list>
        /// </remarks>
        public bool IsFilled { get; init; } = false;

        /// <summary>
        /// Gets the description for the history item (e.g., "Rectangle", "Ellipse").
        /// </summary>
        public string Description { get; init; } = "Draw Shape";
    }
}
