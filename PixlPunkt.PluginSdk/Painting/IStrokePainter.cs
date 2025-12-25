using PixlPunkt.PluginSdk.Imaging;

namespace PixlPunkt.PluginSdk.Painting
{
    /// <summary>
    /// Result of a rendering operation, used for history tracking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IRenderResult"/> is a marker interface for render results that can be
    /// pushed to the undo/redo history system.
    /// </para>
    /// <para>
    /// Plugin painters should return <see cref="PixelChangeResult"/> from their
    /// <see cref="IStrokePainter.End"/> method to support proper undo/redo.
    /// </para>
    /// </remarks>
    public interface IRenderResult
    {
        /// <summary>
        /// Gets whether this result contains any actual changes.
        /// </summary>
        bool HasChanges { get; }
    }

    /// <summary>
    /// Base interface for tool painters (stroke painters, fill painters, shape renderers).
    /// </summary>
    public interface IToolPainter
    {
    }

    /// <summary>
    /// Strategy interface for tool-specific painting operations.
    /// Each brush-like tool owns an implementation of this interface
    /// to handle its unique pixel manipulation logic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IStrokePainter"/> is the core abstraction enabling plugin-friendly
    /// tool architecture. By extracting tool-specific painting logic into individual
    /// painter implementations, new tools can be added without modifying the engine.
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
    /// This allows direct property access without runtime lookups, and enables
    /// painters to own per-stroke buffers naturally.
    /// </para>
    /// <para><strong>Plugin Implementation:</strong></para>
    /// <para>
    /// Plugin authors should extend <see cref="PainterBase"/> for common functionality
    /// (accumulation tracking, line stamping) and override <see cref="StampAt"/> for
    /// custom pixel operations.
    /// </para>
    /// </remarks>
    public interface IStrokePainter : IToolPainter
    {
        /// <summary>
        /// Gets whether this painter requires a snapshot of the surface at stroke start.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, the stroke engine will clone the surface pixels before calling
        /// <see cref="Begin"/> and pass the snapshot to the painter via context.
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
        /// <param name="surface">The target pixel surface for painting.</param>
        /// <param name="snapshot">
        /// Clone of surface pixels at stroke start, or null if <see cref="NeedsSnapshot"/> is false.
        /// </param>
        /// <remarks>
        /// Implementations should:
        /// <list type="bullet">
        /// <item>Store the surface reference for use during stamping</item>
        /// <item>Clear any per-stroke accumulation structures</item>
        /// <item>Initialize mode-specific buffers</item>
        /// </list>
        /// </remarks>
        void Begin(PixelSurface surface, byte[]? snapshot);

        /// <summary>
        /// Stamps the tool effect at a single point.
        /// </summary>
        /// <param name="cx">Center X coordinate in document space.</param>
        /// <param name="cy">Center Y coordinate in document space.</param>
        /// <param name="context">
        /// Shared context containing brush configuration, colors, and helper functions.
        /// </param>
        /// <remarks>
        /// This is the core pixel manipulation method. Implementations iterate over
        /// BrushOffsets and apply their effect to each pixel, using ComputeAlphaAtOffset
        /// for density-aware alpha computation.
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
        /// Default implementations typically use Bresenham-style iteration calling
        /// <see cref="StampAt"/> at intervals based on brush size.
        /// </remarks>
        void StampLine(int x0, int y0, int x1, int y1, StrokeContext context);

        /// <summary>
        /// Called when the stroke ends. Returns accumulated changes for history.
        /// </summary>
        /// <param name="description">Description of the operation for undo/redo UI.</param>
        /// <returns>
        /// An <see cref="IRenderResult"/> containing all modifications made during
        /// the stroke, or null if no changes were made.
        /// </returns>
        /// <remarks>
        /// Implementations should:
        /// <list type="bullet">
        /// <item>Collect all before/after pixel values from accumulation structures</item>
        /// <item>Clear per-stroke state</item>
        /// <item>Return a result that can be pushed directly to history</item>
        /// </list>
        /// </remarks>
        IRenderResult? End(string description = "Brush Stroke");
    }

    /// <summary>
    /// Result containing pixel changes made during a stroke operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="PixelChangeResult"/> stores before/after values for modified pixels,
    /// enabling the host application to construct proper undo/redo history entries.
    /// </para>
    /// <para>
    /// Plugin painters should use <see cref="PainterBase"/> which handles accumulation
    /// automatically and returns this result type from <see cref="IStrokePainter.End"/>.
    /// </para>
    /// </remarks>
    public sealed class PixelChangeResult : IRenderResult
    {
        private readonly List<PixelChange> _changes = new();

        /// <summary>
        /// Gets the description of this operation for undo/redo UI.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the pixel surface that was modified.
        /// </summary>
        public PixelSurface Surface { get; }

        /// <summary>
        /// Gets whether this result contains any actual changes.
        /// </summary>
        public bool HasChanges => _changes.Count > 0;

        /// <summary>
        /// Gets the list of pixel changes.
        /// </summary>
        public IReadOnlyList<PixelChange> Changes => _changes;

        /// <summary>
        /// Creates a new pixel change result.
        /// </summary>
        /// <param name="surface">The surface that was modified.</param>
        /// <param name="description">Description for undo/redo UI.</param>
        public PixelChangeResult(PixelSurface surface, string description = "Brush Stroke")
        {
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));
            Description = description;
        }

        /// <summary>
        /// Adds a pixel change record.
        /// </summary>
        /// <param name="byteIndex">The byte index into the pixel array.</param>
        /// <param name="before">The pixel value before the change (BGRA).</param>
        /// <param name="after">The pixel value after the change (BGRA).</param>
        public void Add(int byteIndex, uint before, uint after)
        {
            if (before != after)
            {
                _changes.Add(new PixelChange(byteIndex, before, after));
            }
        }
    }

    /// <summary>
    /// Represents a single pixel change with before/after values.
    /// </summary>
    /// <param name="ByteIndex">The byte index into the pixel array.</param>
    /// <param name="Before">The pixel value before the change (BGRA packed).</param>
    /// <param name="After">The pixel value after the change (BGRA packed).</param>
    public readonly record struct PixelChange(int ByteIndex, uint Before, uint After);
}
