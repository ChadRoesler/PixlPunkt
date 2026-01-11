using System;
using System.Collections.Generic;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Selection;

namespace PixlPunkt.Core.Tools.Selection
{
    /// <summary>
    /// Context passed to selection tool factories for dependency injection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SelectionToolContext"/> provides all dependencies needed by selection tools
    /// without requiring constructor parameter sprawl. Tools receive this context when created
    /// by the factory and use it to interact with the document and UI.
    /// </para>
    /// <para><strong>Dependencies Provided:</strong></para>
    /// <list type="bullet">
    /// <item><strong>GetSelectionRegion</strong>: Access to the current selection mask.</item>
    /// <item><strong>RequestRedraw</strong>: Trigger canvas invalidation for preview updates.</item>
    /// <item><strong>GetDocumentSize</strong>: Current document dimensions for bounds checking.</item>
    /// <item><strong>GetActiveLayer</strong>: Access to active raster layer for pixel sampling (wand tool).</item>
    /// <item><strong>GetBrushOffsets</strong>: Brush shape offsets for paint selection tool.</item>
    /// </list>
    /// <para><strong>Usage Pattern:</strong></para>
    /// <code>
    /// var ctx = new SelectionToolContext
    /// {
    ///     GetSelectionRegion = () => _selRegion,
    ///     RequestRedraw = () => CanvasView.Invalidate(),
    ///     GetDocumentSize = () => (Document.PixelWidth, Document.PixelHeight),
    ///     GetActiveLayer = () => Document.ActiveLayer,
    ///     GetBrushOffsets = () => _stroke?.GetCurrentBrushOffsets() ?? Array.Empty&lt;(int, int)&gt;()
    /// };
    /// 
    /// var tool = registration.CreateTool(ctx);
    /// </code>
    /// </remarks>
    public sealed class SelectionToolContext
    {
        /// <summary>
        /// Gets the function to retrieve the current selection region.
        /// </summary>
        /// <remarks>
        /// Selection tools use this to add/subtract from the selection mask.
        /// </remarks>
        public required Func<SelectionRegion> GetSelectionRegion { get; init; }

        /// <summary>
        /// Gets the action to request a canvas redraw.
        /// </summary>
        /// <remarks>
        /// Called when preview geometry changes to trigger visual update.
        /// </remarks>
        public required Action RequestRedraw { get; init; }

        /// <summary>
        /// Gets the function to retrieve document pixel dimensions.
        /// </summary>
        /// <remarks>
        /// Returns (width, height) tuple for bounds checking and region sizing.
        /// </remarks>
        public required Func<(int Width, int Height)> GetDocumentSize { get; init; }

        /// <summary>
        /// Gets the function to retrieve the active raster layer.
        /// </summary>
        /// <remarks>
        /// Used by wand tool for pixel sampling. Returns null if no raster layer is active.
        /// </remarks>
        public required Func<RasterLayer?> GetActiveLayer { get; init; }

        /// <summary>
        /// Gets the function to retrieve current brush shape offsets.
        /// </summary>
        /// <remarks>
        /// Used by paint selection tool to determine which pixels to select based on brush shape.
        /// Returns empty collection if no brush configuration is available.
        /// </remarks>
        public required Func<IReadOnlyList<(int dx, int dy)>> GetBrushOffsets { get; init; }
    }
}
