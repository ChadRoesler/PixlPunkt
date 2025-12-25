using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Strategy interface for fill operations (flood fill and global fill).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IFillPainter"/> enables plugin-friendly fill tool architecture.
    /// Plugins can provide custom fill implementations with different algorithms:
    /// </para>
    /// <list type="bullet">
    /// <item>Contiguous flood fill (4-neighbor or 8-neighbor)</item>
    /// <item>Global color replacement</item>
    /// <item>Pattern-based fills</item>
    /// <item>Gradient fills</item>
    /// </list>
    /// <para><strong>Workflow:</strong></para>
    /// <list type="number">
    /// <item>User clicks on canvas at seed point</item>
    /// <item><see cref="FillAt"/> is called with seed coordinates and context</item>
    /// <item>Returns <see cref="History.IRenderResult"/> for unified history handling</item>
    /// </list>
    /// <para><strong>Plugin Implementation:</strong></para>
    /// <para>
    /// Plugin authors should return <see cref="History.PixelChangeItem"/> for standard pixel
    /// operations, or implement custom <see cref="History.IRenderResult"/> types for
    /// specialized fill operations (e.g., vector fills, procedural patterns).
    /// </para>
    /// </remarks>
    public interface IFillPainter : IToolPainter
    {
        /// <summary>
        /// Performs a fill operation starting at the specified seed point.
        /// </summary>
        /// <param name="layer">The target raster layer for the fill.</param>
        /// <param name="x">Seed X coordinate in document space.</param>
        /// <param name="y">Seed Y coordinate in document space.</param>
        /// <param name="context">Fill configuration (color, tolerance, mode).</param>
        /// <returns>
        /// An <see cref="IRenderResult"/> containing all modifications made by the fill,
        /// or null if no changes were made. Typically returns <see cref="PixelChangeItem"/>
        /// for standard pixel operations.
        /// </returns>
        IRenderResult? FillAt(RasterLayer layer, int x, int y, FillContext context);
    }
}
