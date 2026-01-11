using System.Collections.Generic;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Strategy interface for rendering shape geometry to pixels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IShapeRenderer"/> separates shape pixel application from geometry generation
    /// (<see cref="IShapeBuilder"/>). This enables plugin authors to:
    /// </para>
    /// <list type="bullet">
    /// <item>Use the default <see cref="BrushStrokeShapeRenderer"/> with custom shapes</item>
    /// <item>Provide custom renderers with special effects (glow, texture, patterns)</item>
    /// <item>Combine custom builders with custom renderers for complete control</item>
    /// </list>
    /// <para><strong>Workflow:</strong></para>
    /// <list type="number">
    /// <item><see cref="IShapeBuilder"/> generates point set from user input</item>
    /// <item><see cref="Render"/> applies points to surface with brush settings</item>
    /// <item>Returns <see cref="IRenderResult"/> for unified history handling</item>
    /// </list>
    /// <para><strong>Plugin Implementation:</strong></para>
    /// <para>
    /// Plugin authors should return <see cref="PixelChangeItem"/> for standard pixel
    /// operations, or implement custom <see cref="IRenderResult"/> types for
    /// specialized rendering (e.g., vector output, procedural textures).
    /// </para>
    /// </remarks>
    public interface IShapeRenderer : IToolPainter
    {
        /// <summary>
        /// Renders shape points to the surface with brush-based strokes.
        /// </summary>
        /// <param name="layer">The target raster layer for rendering.</param>
        /// <param name="points">Set of (x, y) coordinates from shape builder.</param>
        /// <param name="context">Rendering configuration (color, brush settings, etc.).</param>
        /// <returns>
        /// An <see cref="IRenderResult"/> containing all pixel modifications,
        /// or null if no changes were made. Typically returns <see cref="PixelChangeItem"/>
        /// for standard pixel operations.
        /// </returns>
        IRenderResult? Render(
            RasterLayer layer,
            HashSet<(int x, int y)> points,
            ShapeRenderContext context);
    }
}
