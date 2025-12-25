using FluentIcons.Common;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Strategy interface for tool-specific painting operations.
    /// Each brush-like tool owns an implementation of this interface
    /// to handle its unique pixel manipulation logic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IStrokePainter"/> is the core abstraction enabling plugin-friendly
    /// tool architecture. By extracting tool-specific painting logic from
    /// <see cref="StrokeEngine"/> into individual painter implementations,
    /// new tools can be added without modifying the engine.
    /// </para>
    /// <para><strong>Lifecycle:</strong></para>
    /// <list type="number">
    /// <item><see cref="Begin"/> - Called when stroke starts; initialize buffers</item>
    /// <item><see cref="StampAt"/>/<see cref="StampLine"/> - Called during painting</item>
    /// <item><see cref="End"/> - Called when stroke ends; return changes for history</item>
    /// </list>
    /// <para><strong>Stateful Design:</strong></para>
    /// <para>
    /// Painters are stateful and hold a reference to their tool's settings object.
    /// This allows direct property access (e.g., <c>_settings.Strength</c>) without
    /// runtime lookups, and enables painters to own per-stroke buffers naturally
    /// (e.g., smudge's logical float channels, gradient's palette map).
    /// </para>
    /// <para><strong>Plugin Implementation:</strong></para>
    /// <para>
    /// Plugin authors should extend <see cref="PluginSdk.Painting.PainterBase"/> for common functionality
    /// (accumulation tracking, line stamping) and override <see cref="PluginSdk.Painting.IStrokePainter.StampAt"/> for
    /// custom pixel operations. Return <see cref="PluginSdk.Painting.PixelChangeResult"/> from End()
    /// for standard undo/redo support.
    /// </para>
    /// </remarks>
    public interface IStrokePainter : IToolPainter
    {
        /// <summary>
        /// Gets whether this painter requires a snapshot of the surface at stroke start.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, <see cref="StrokeEngine"/> will clone the surface pixels
        /// before calling <see cref="Begin"/> and pass the snapshot to the painter.
        /// </para>
        /// <para>
        /// Painters that sample from the original state (blur, smudge, jumble)
        /// should return true. Simple painting tools (brush, eraser) return false.
        /// </para>
        /// </remarks>
        bool NeedsSnapshot { get; }

        /// <summary>
        /// Called when a stroke begins. Initialize per-stroke state here.
        /// </summary>
        /// <param name="layer">The target raster layer for painting.</param>
        /// <param name="snapshot">
        /// Clone of surface pixels at stroke start, or null if <see cref="NeedsSnapshot"/> is false.
        /// </param>
        /// <remarks>
        /// <para>
        /// Implementations should:
        /// </para>
        /// <list type="bullet">
        /// <item>Store the layer reference for use during stamping and history creation</item>
        /// <item>Clear any per-stroke accumulation structures</item>
        /// <item>Initialize mode-specific buffers (e.g., smudge logical channels)</item>
        /// </list>
        /// </remarks>
        void Begin(RasterLayer layer, byte[]? snapshot);

        /// <summary>
        /// Stamps the tool effect at a single point.
        /// </summary>
        /// <param name="cx">Center X coordinate in document space.</param>
        /// <param name="cy">Center Y coordinate in document space.</param>
        /// <param name="context">
        /// Shared context containing brush configuration, colors, and helper functions.
        /// </param>
        /// <remarks>
        /// <para>
        /// This is the core pixel manipulation method. Implementations iterate over
        /// <see cref="StrokeContext.BrushOffsets"/> and apply their effect to each
        /// pixel, using <see cref="StrokeContext.ComputeAlphaAtOffset"/> for
        /// density-aware alpha computation.
        /// </para>
        /// <para>
        /// Changes should be tracked for history (typically via an accumulation
        /// dictionary mapping pixel index to before/after values).
        /// </para>
        /// </remarks>
        void StampAt(int cx, int cy, StrokeContext context);

        /// <summary>
        /// Stamps the tool effect along a line from (x0,y0) to (x1,y1).
        /// </summary>
        /// <param name="x0">Start X coordinate.</param>
        /// <param name="y0">Start Y coordinate.</param>
        /// <param name="x1">End X coordinate.</param>
        /// <param name="y1">End Y coordinate.</param>
        /// <param name="context">
        /// Shared context containing brush configuration, colors, and helper functions.
        /// </param>
        /// <remarks>
        /// <para>
        /// Default implementations typically use Bresenham-style iteration calling
        /// <see cref="StampAt"/> at intervals based on brush size. Stride is usually
        /// <c>Math.Max(1, size / 3)</c> to ensure continuous coverage without gaps.
        /// </para>
        /// <para>
        /// Specialized tools (like smudge) may override to track stroke direction.
        /// </para>
        /// </remarks>
        void StampLine(int x0, int y0, int x1, int y1, StrokeContext context);

        /// <summary>
        /// Called when the stroke ends. Returns accumulated changes for history.
        /// </summary>
        /// <param name="description">Description of the operation for undo/redo UI.</param>
        /// <param name="icon">Icon representing the tool for history display.</param>
        /// <returns>
        /// An <see cref="IRenderResult"/> containing all modifications made during
        /// the stroke, or null if no changes were made. Typically returns
        /// <see cref="PixelChangeItem"/> for standard pixel operations.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Implementations should:
        /// </para>
        /// <list type="bullet">
        /// <item>Collect all before/after pixel values from accumulation structures</item>
        /// <item>Clear per-stroke state (buffers, tracking dictionaries)</item>
        /// <item>Return a result that can be pushed directly to history</item>
        /// </list>
        /// <para>
        /// Returning null indicates the stroke made no actual pixel changes
        /// (e.g., user clicked but didn't move, and all pixels were unchanged).
        /// </para>
        /// </remarks>
        IRenderResult? End(string description = "Brush Stroke", Icon icon = Icon.History);
    }
}
