using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Selection;
using PixlPunkt.Core.Tools.Settings;
using Windows.Foundation;

namespace PixlPunkt.Core.Tools.Selection
{
    /// <summary>
    /// Magic wand selection tool for selecting regions of similar color.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WandSelectionTool allows users to click on a pixel to select all similar colors.
    /// Configuration includes:
    /// - Tolerance: Color distance threshold (0-255)
    /// - Contiguous: Flood-fill vs. global selection
    /// - UseAlpha: Include alpha channel in comparison
    /// - EightWay: 8-way vs. 4-way connectivity
    /// </para>
    /// <para>
    /// The tool reads settings directly from <see cref="WandToolSettings"/> to ensure
    /// UI changes are immediately reflected in tool behavior.
    /// </para>
    /// </remarks>
    public sealed class WandSelectionTool : SelectionToolBase
    {
        private readonly Func<RasterLayer?> _getActiveLayer;
        private readonly Func<SelectionRegion> _getSelectionRegion;
        private readonly Action _requestRedraw;
        private readonly Func<(int w, int h)> _getDocumentSize;

        /// <summary>
        /// Reference to the tool settings for live reading of tolerance, contiguous, etc.
        /// </summary>
        private WandToolSettings? _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="WandSelectionTool"/> class.
        /// </summary>
        /// <param name="getActiveLayer">Function to get current active raster layer.</param>
        /// <param name="getSelectionRegion">Function to get the selection region.</param>
        /// <param name="requestRedraw">Action to request canvas redraw.</param>
        /// <param name="getDocumentSize">Function to get document dimensions.</param>
        public WandSelectionTool(
            Func<RasterLayer?> getActiveLayer,
            Func<SelectionRegion> getSelectionRegion,
            Action requestRedraw,
            Func<(int w, int h)> getDocumentSize)
        {
            _getActiveLayer = getActiveLayer;
            _getSelectionRegion = getSelectionRegion;
            _requestRedraw = requestRedraw;
            _getDocumentSize = getDocumentSize;
        }

        /// <summary>
        /// Creates a new instance from a SelectionToolContext.
        /// </summary>
        public WandSelectionTool(SelectionToolContext context)
            : this(context.GetActiveLayer, context.GetSelectionRegion, context.RequestRedraw, context.GetDocumentSize)
        {
        }

        // ====================================================================
        // ISelectionTool IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        public override string Id => ToolIds.Wand;

        /// <inheritdoc/>
        public override bool HasPreview => false; // Wand is click-based, no preview

        /// <inheritdoc/>
        public override void Configure(ToolSettingsBase settings)
        {
            // Store reference to settings for live reading during selection operations
            if (settings is WandToolSettings wand)
            {
                _settings = wand;
            }
        }

        /// <inheritdoc/>
        public override void DrawPreview(CanvasDrawingSession ds, Rect destRect, double scale, float antsPhase)
        {
            // Wand tool has no preview - selection happens instantly on click
        }

        // ====================================================================
        // SELECTION LOGIC
        // ====================================================================

        /// <inheritdoc/>
        protected override bool OnPressed(Point docPos, PointerRoutedEventArgs e)
        {
            int x = (int)docPos.X;
            int y = (int)docPos.Y;

            var layer = _getActiveLayer();
            if (layer == null)
                return false;

            var (w, h) = _getDocumentSize();
            if (x < 0 || y < 0 || x >= w || y >= h)
                return false;

            // Perform magic wand selection
            PerformWandSelection(x, y);

            return true;
        }

        /// <inheritdoc/>
        protected override bool OnMoved(Point docPos, PointerRoutedEventArgs e)
        {
            // Magic wand is click-based, no drag interaction
            return false;
        }

        /// <inheritdoc/>
        protected override bool OnReleased(Point docPos, PointerRoutedEventArgs e)
        {
            // Nothing to do on release for wand tool
            return true;
        }

        // ====================================================================
        // MAGIC WAND ALGORITHM
        // ====================================================================

        /// <summary>
        /// Executes the magic wand selection algorithm with current settings.
        /// </summary>
        private void PerformWandSelection(int startX, int startY)
        {
            var layer = _getActiveLayer();
            if (layer == null)
                return;

            var surf = layer.Surface;
            int w = surf.Width;
            int h = surf.Height;

            // Read settings directly from WandToolSettings for live updates
            int tolerance = _settings?.Tolerance ?? 0;
            bool contiguous = _settings?.Contiguous ?? true;
            bool useAlpha = _settings?.UseAlpha ?? true;
            bool eightWay = _settings?.EightWay ?? true;

            // Get seed pixel
            int idxStart = (startY * w + startX) * 4;
            var pix = surf.Pixels;

            // Build temporary region for wand result
            var wandRegion = new SelectionRegion();
            wandRegion.EnsureSize(w, h);

            bool[,] visited = new bool[w, h];
            var queue = new Queue<(int x, int y)>();

            // Neighbor offsets (4-way or 8-way)
            var neigh = eightWay
                ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) }
                : new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

            queue.Enqueue((startX, startY));
            visited[startX, startY] = true;

            // Flood fill with tolerance
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                int idx = (y * w + x) * 4;

                // Check if pixel matches seed within tolerance
                if (!Imaging.PixelOps.PixelsSimilar(pix, idx, idxStart, tolerance, useAlpha))
                    continue;

                // Add to wand region
                wandRegion.AddRect(new Windows.Graphics.RectInt32(x, y, 1, 1));

                // Expand to neighbors (only if contiguous mode)
                if (contiguous)
                {
                    foreach (var (dx, dy) in neigh)
                    {
                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            continue;

                        if (visited[nx, ny])
                            continue;

                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            // Non-contiguous mode: find ALL matching pixels globally
            if (!contiguous)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (!visited[x, y])
                        {
                            int idx = (y * w + x) * 4;
                            if (Imaging.PixelOps.PixelsSimilar(pix, idx, idxStart, tolerance, useAlpha))
                            {
                                wandRegion.AddRect(new Windows.Graphics.RectInt32(x, y, 1, 1));
                            }
                        }
                    }
                }
            }

            // Apply wand result to selection region based on combine mode
            var region = _getSelectionRegion();
            region.EnsureSize(w, h);

            switch (CombineMode)
            {
                case SelectionCombineMode.Replace:
                    region.Clear();
                    region.AddRegion(wandRegion);
                    break;

                case SelectionCombineMode.Add:
                    region.AddRegion(wandRegion);
                    break;

                case SelectionCombineMode.Subtract:
                    region.SubtractRegion(wandRegion);
                    break;
            }

            _requestRedraw?.Invoke();
        }
    }
}
