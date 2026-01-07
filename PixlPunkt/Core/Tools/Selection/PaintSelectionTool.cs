using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Selection;
using PixlPunkt.Core.Tools.Settings;
using Windows.Foundation;

namespace PixlPunkt.Core.Tools.Selection
{
    /// <summary>
    /// Paint selection tool for painting selection masks with brush strokes.
    /// </summary>
    public sealed class PaintSelectionTool : SelectionToolBase
    {
        private readonly Func<SelectionRegion> _getSelectionRegion;
        private readonly Action _requestRedraw;
        private readonly Func<(int w, int h)> _getDocumentSize;
        private readonly Func<IReadOnlyList<(int dx, int dy)>> _getBrushOffsets;

        private Point _lastDocPos;
        private bool _hasLastPos;

        // Current brush cursor position for preview
        private Point _cursorPos;
        private bool _showCursor;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaintSelectionTool"/> class.
        /// </summary>
        /// <param name="getSelectionRegion">Function to get the selection region.</param>
        /// <param name="requestRedraw">Action to request canvas redraw.</param>
        /// <param name="getDocumentSize">Function to get document dimensions.</param>
        /// <param name="getBrushOffsets">Function to get current brush mask offsets.</param>
        public PaintSelectionTool(
            Func<SelectionRegion> getSelectionRegion,
            Action requestRedraw,
            Func<(int w, int h)> getDocumentSize,
            Func<IReadOnlyList<(int dx, int dy)>> getBrushOffsets)
        {
            _getSelectionRegion = getSelectionRegion;
            _requestRedraw = requestRedraw;
            _getDocumentSize = getDocumentSize;
            _getBrushOffsets = getBrushOffsets;
        }

        /// <summary>
        /// Creates a new instance from a SelectionToolContext.
        /// </summary>
        public PaintSelectionTool(SelectionToolContext context)
            : this(context.GetSelectionRegion, context.RequestRedraw, context.GetDocumentSize, context.GetBrushOffsets)
        {
        }

        // ====================================================================
        // ISelectionTool IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        public override string Id => ToolIds.PaintSelect;

        /// <inheritdoc/>
        public override bool HasPreview => false; // Brush preview is handled by the main brush overlay system

        /// <inheritdoc/>
        public override bool NeedsContinuousRender => true;

        /// <inheritdoc/>
        public override void Configure(ToolSettingsBase settings)
        {
            // PaintSelectToolSettings uses Size and Shape which are already provided
            // through GetBrushOffsets - no additional configuration needed
        }

        /// <inheritdoc/>
        public override void DrawPreview(CanvasDrawingSession ds, Rect destRect, double scale, float antsPhase)
        {
            // Brush cursor preview is handled by the main brush overlay system in CanvasViewHost.BrushOverlay.cs
            // This method is intentionally empty - PaintSelect participates in the standard brush overlay
            // like Blur, Jumble, Eraser, Replacer, etc.
        }

        /// <summary>
        /// Updates the cursor position for preview rendering.
        /// </summary>
        public void UpdateCursor(Point docPos, bool visible)
        {
            _cursorPos = docPos;
            _showCursor = visible;
        }

        // ====================================================================
        // SELECTION LOGIC
        // ====================================================================

        /// <inheritdoc/>
        protected override bool OnPressed(Point docPos, PointerRoutedEventArgs e)
        {
            var (w, h) = _getDocumentSize();
            var region = _getSelectionRegion();
            region.EnsureSize(w, h);

            // Clear existing selection if in Replace mode (no modifiers)
            if (CombineMode == SelectionCombineMode.Replace)
            {
                region.Clear();
            }

            // Initial stamp at press position
            StampBrushAt((int)docPos.X, (int)docPos.Y);

            _lastDocPos = docPos;
            _hasLastPos = true;

            return true;
        }

        /// <inheritdoc/>
        protected override bool OnMoved(Point docPos, PointerRoutedEventArgs e)
        {
            // Update cursor for preview
            _cursorPos = docPos;
            _showCursor = true;

            if (!_hasLastPos)
            {
                _lastDocPos = docPos;
                _hasLastPos = true;
                return true;
            }

            // Interpolate between last and current position (Bresenham-style stroke)
            StampLine(
                (int)_lastDocPos.X, (int)_lastDocPos.Y,
                (int)docPos.X, (int)docPos.Y
            );

            _lastDocPos = docPos;
            return true;
        }

        /// <inheritdoc/>
        protected override bool OnReleased(Point docPos, PointerRoutedEventArgs e)
        {
            _hasLastPos = false;
            _requestRedraw?.Invoke();
            return true;
        }

        /// <inheritdoc/>
        public override void Deactivate()
        {
            base.Deactivate();
            _hasLastPos = false;
            _showCursor = false;
        }

        // ====================================================================
        // BRUSH STAMPING
        // ====================================================================

        /// <summary>
        /// Stamps the brush shape at a specific position, adding or subtracting from the selection.
        /// </summary>
        private void StampBrushAt(int x, int y)
        {
            var region = _getSelectionRegion();
            var offsets = _getBrushOffsets();
            var (w, h) = _getDocumentSize();

            // Determine operation based on combine mode
            // Replace mode acts like Add after the initial clear
            bool adding = CombineMode switch
            {
                SelectionCombineMode.Subtract => false,
                _ => true // Replace and Add both add pixels
            };

            // **OPTIMIZATION**: For subtract mode, batch all pixel operations before triggering
            // expensive bounds recomputation. This reduces O(pixels × W×H) to O(W×H) per stamp.
            if (!adding)
            {
                // Batch subtract: modify mask directly without bounds recalculation
                foreach (var (dx, dy) in offsets)
                {
                    int px = x + dx;
                    int py = y + dy;

                    if (px < 0 || py < 0 || px >= w || py >= h)
                        continue;

                    // Direct mask modification (SubtractRect internals without bounds recompute)
                    region.SubtractPixelFast(px, py);
                }

                // Single bounds recomputation after all pixels modified
                region.RecomputeBoundsIfNeeded();
            }
            else
            {
                // Add mode: existing behavior (bounds expansion is cheap)
                foreach (var (dx, dy) in offsets)
                {
                    int px = x + dx;
                    int py = y + dy;

                    if (px < 0 || py < 0 || px >= w || py >= h)
                        continue;

                    region.AddRect(new Windows.Graphics.RectInt32(px, py, 1, 1));
                }
            }

            _requestRedraw?.Invoke();
        }

        /// <summary>
        /// Stamps the brush along a line using Bresenham interpolation.
        /// </summary>
        private void StampLine(int x0, int y0, int x1, int y1)
        {
            // Bresenham's line algorithm with brush stamping
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            int x = x0;
            int y = y0;

            while (true)
            {
                StampBrushAt(x, y);

                if (x == x1 && y == y1)
                    break;

                int e2 = 2 * err;

                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }
    }
}
