using System;
using Microsoft.UI;
using PixlPunkt.Core.Rendering;
using Windows.Foundation;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Core.Selection
{
    /// <summary>
    /// Represents a pixel-based selection region using a boolean mask with marching-ants rendering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionRegion provides a lightweight pixel mask for rectangular selection operations with
    /// efficient add/subtract operations and tight bounds tracking. Unlike <see cref="SelectionMask"/>, 
    /// this class focuses on simple rectangle-based operations and includes rendering support for
    /// animated marching-ants outlines.
    /// </para>
    /// <para><strong>Core Features:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Boolean Mask</strong>: Byte array where 0 = unselected, 1 = selected.</item>
    /// <item><strong>Tight Bounds</strong>: Automatically tracks minimal bounding rectangle of selected pixels.
    /// Recomputed after subtractions to maintain accuracy.</item>
    /// <item><strong>Rectangle Operations</strong>: Add/subtract axis-aligned rectangles with efficient clipping.</item>
    /// <item><strong>Marching Ants</strong>: Renders animated boundary outline with alternating black/white segments.</item>
    /// </list>
    /// <para><strong>Marching Ants Algorithm:</strong></para>
    /// <para>
    /// The <see cref="DrawAnts"/> method traces the outer boundary of the selection by detecting edge pixels
    /// (selected pixel adjacent to unselected). Horizontal and vertical edges are traced separately, with 
    /// alternating black/white segments drawn using phase animation. Corner edges use inverted patterns to
    /// create seamless checkerboard effect at intersections.
    /// </para>
    /// <para><strong>Performance:</strong></para>
    /// <para>
    /// Add operations expand bounds in O(1) by unioning rectangles. Subtract operations trigger O(W×H)
    /// bounds recomputation to tighten after potential holes. Point containment is O(1) with bounds culling.
    /// </para>
    /// </remarks>
    /// <seealso cref="SelectionMask"/>
    /// <seealso cref="SelectionEngine"/>
    public sealed class SelectionRegion
    {
        private int _w, _h;
        private byte[] _m = Array.Empty<byte>();
        private RectInt32 _bounds; // tight bounds of any 1s; (0,0,0,0) == empty

        // NEW: Origin offset - allows the region to be positioned anywhere in world space
        private int _offsetX = 0;
        private int _offsetY = 0;

        /// <summary>
        /// Gets the width of the mask buffer.
        /// </summary>
        public int Width => _w;

        /// <summary>
        /// Gets the height of the mask buffer.
        /// </summary>
        public int Height => _h;

        /// <summary>
        /// Gets the X offset of this region in world space.
        /// </summary>
        public int OffsetX => _offsetX;

        /// <summary>
        /// Gets the Y offset of this region in world space.
        /// </summary>
        public int OffsetY => _offsetY;

        /// <summary>
        /// Gets a value indicating whether no pixels are selected.
        /// </summary>
        /// <value>
        /// <c>true</c> if <see cref="Bounds"/> has zero width or height; otherwise, <c>false</c>.
        /// </value>
        public bool IsEmpty => _bounds.Width <= 0 || _bounds.Height <= 0;

        /// <summary>
        /// Gets the tight bounding rectangle containing all selected pixels (in world space).
        /// </summary>
        /// <value>
        /// A <see cref="RectInt32"/> with minimal coverage of selected region, or (0,0,0,0) if empty.
        /// </value>
        public RectInt32 Bounds
        {
            get
            {
                if (IsEmpty) return _bounds;
                // Apply offset to bounds
                return CreateRect(
                    _bounds.X + _offsetX,
                    _bounds.Y + _offsetY,
                    _bounds.Width,
                    _bounds.Height);
            }
        }

        /// <summary>
        /// Sets the world-space offset for this region.
        /// This allows the region to be positioned anywhere, even outside the original document bounds.
        /// </summary>
        public void SetOffset(int x, int y)
        {
            _offsetX = x;
            _offsetY = y;
        }

        /// <summary>
        /// Ensures the internal mask buffer is allocated to at least the specified dimensions.
        /// </summary>
        /// <param name="w">Target width in pixels.</param>
        /// <param name="h">Target height in pixels.</param>
        /// <remarks>
        /// <para>
        /// If dimensions match current size, the existing selection is preserved. If size changes,
        /// the buffer is reallocated and the selection is cleared. Invalid dimensions (≤0) result in
        /// empty state.
        /// </para>
        /// <para>
        /// Call this before performing selection operations to ensure buffer capacity matches document size.
        /// </para>
        /// </remarks>
        public void EnsureSize(int w, int h)
        {
            if (w <= 0 || h <= 0)
            {
                _w = _h = 0;
                _m = Array.Empty<byte>();
                _bounds = CreateRect(0, 0, 0, 0);
                return;
            }

            w = Math.Max(1, w);
            h = Math.Max(1, h);

            if (w == _w && h == _h && _m.Length == _w * _h) return;

            _w = w; _h = h;
            _m = new byte[_w * _h];
            _bounds = CreateRect(0, 0, 0, 0);
        }

        /// <summary>
        /// Clears all selected pixels.
        /// </summary>
        /// <remarks>
        /// Resets the mask buffer to all zeros and sets bounds to empty. Does not deallocate the buffer.
        /// </remarks>
        public void Clear()
        {
            if (_m.Length > 0) Array.Clear(_m, 0, _m.Length);
            _bounds = CreateRect(0, 0, 0, 0);
            _offsetX = 0;
            _offsetY = 0;
        }

        /// <summary>
        /// Adds a rectangle to the selection (union operation).
        /// </summary>
        /// <param name="r">Rectangle to add in pixel coordinates.</param>
        /// <remarks>
        /// Sets all pixels within the rectangle to selected (1). Expands <see cref="Bounds"/> efficiently
        /// without full recomputation. Rectangle is clipped to buffer dimensions.
        /// </remarks>
        public void AddRect(RectInt32 r) => Fill(r, modeAdd: true);

        /// <summary>
        /// Subtracts a rectangle from the selection.
        /// </summary>
        /// <param name="r">Rectangle to remove in pixel coordinates.</param>
        /// <remarks>
        /// Sets all pixels within the rectangle to unselected (0). Triggers bounds recomputation to
        /// tighten bounding box after potential holes. Rectangle is clipped to buffer dimensions.
        /// </remarks>
        public void SubtractRect(RectInt32 r) => Fill(r, modeAdd: false);

        /// <summary>
        /// Tests whether a specific pixel is selected (in world space).
        /// </summary>
        /// <param name="x">X coordinate in world space.</param>
        /// <param name="y">Y coordinate in world space.</param>
        /// <returns><c>true</c> if the pixel is selected; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// <para>
        /// Performs fast rejection using <see cref="Bounds"/> before testing the mask. Out-of-bounds
        /// coordinates return false. This is an O(1) operation with bounds culling.
        /// </para>
        /// </remarks>
        public bool Contains(int x, int y)
        {
            if (IsEmpty) return false;

            // Convert world space to local buffer space
            int localX = x - _offsetX;
            int localY = y - _offsetY;

            if ((uint)localX >= (uint)_w || (uint)localY >= (uint)_h) return false;

            // quick reject via bounds (in local space)
            var b = _bounds;
            if (localX < b.X || localY < b.Y || localX >= b.X + b.Width || localY >= b.Y + b.Height) return false;

            return _m[localY * _w + localX] != 0;
        }

        /// <summary>
        /// Renders animated marching-ants outline along the outer boundary of the selection.
        /// </summary>
        /// <param name="renderer">Canvas renderer for rendering.</param>
        /// <param name="dest">Destination rectangle in view space.</param>
        /// <param name="scale">Zoom scale factor (document pixels → view pixels).</param>
        /// <param name="phase">Animation phase offset in pixels. Increment over time to animate.</param>
        /// <param name="antsOn">Length of white segments in pixels.</param>
        /// <param name="antsOff">Length of black segments in pixels.</param>
        /// <param name="antsThickness">Line thickness in pixels.</param>
        /// <remarks>
        /// <para><strong>Edge Detection Algorithm:</strong></para>
        /// <para>
        /// Scans the selection bounds to identify edge pixels (selected with unselected neighbor).
        /// Four passes detect top, bottom, left, and right edges separately. Consecutive edge pixels
        /// are coalesced into line segments for efficient rendering.
        /// </para>
        /// <para><strong>Pattern Alternation:</strong></para>
        /// <para>
        /// Horizontal edges use normal phase; vertical edges use inverted phase (offset by antsOn).
        /// This creates a checkerboard pattern at corners where edges meet, ensuring seamless animation.
        /// </para>
        /// <para><strong>Performance:</strong></para>
        /// <para>
        /// Complexity is O(boundsArea), only scanning tight bounds rather than full mask. Segment
        /// coalescing minimizes draw calls for continuous edges.
        /// </para>
        /// </remarks>
        public void DrawAnts(ICanvasRenderer renderer, Rect dest, double scale, float phase,
                             float antsOn, float antsOff, float antsThickness)
        {
            if (IsEmpty) return;

            // convert mask-space -> view-space, accounting for world-space offset
            float ox = (float)(dest.X + _offsetX * scale);
            float oy = (float)(dest.Y + _offsetY * scale);
            float s = (float)scale;
            var b = _bounds; // local bounds (not including offset)

            // Horizontal "top" edges: cell=1 and above=0
            for (int y = b.Y; y < b.Y + b.Height; y++)
            {
                int runX0 = -1;
                for (int x = b.X; x <= b.X + b.Width; x++)
                {
                    bool edge = (At(x, y) == 1) && (At(x, y - 1) == 0);
                    if (edge && runX0 < 0) runX0 = x;
                    if ((!edge || x == b.X + b.Width) && runX0 >= 0)
                    {
                        float ex = ox + runX0 * s;
                        float ey = oy + y * s;
                        float len = (x - runX0) * s;
                        DrawAntsH(renderer, ex, ey, len, phase, antsOn, antsOff, antsThickness, invert: false);
                        runX0 = -1;
                    }
                }
            }

            // Bottom edges: cell=1 and below=0 (invert pattern so corners alternate)
            for (int y = b.Y; y < b.Y + b.Height; y++)
            {
                int runX0 = -1;
                for (int x = b.X; x <= b.X + b.Width; x++)
                {
                    bool edge = (At(x, y) == 1) && (At(x, y + 1) == 0);
                    if (edge && runX0 < 0) runX0 = x;
                    if ((!edge || x == b.X + b.Width) && runX0 >= 0)
                    {
                        float ex = ox + runX0 * s;
                        float ey = oy + (y + 1) * s;
                        float len = (x - runX0) * s;
                        DrawAntsH(renderer, ex, ey, len, phase, antsOn, antsOff, antsThickness, invert: true);
                        runX0 = -1;
                    }
                }
            }

            // Left edges: cell=1 and left=0
            for (int x = b.X; x < b.X + b.Width; x++)
            {
                int runY0 = -1;
                for (int y = b.Y; y <= b.Y + b.Height; y++)
                {
                    bool edge = (At(x, y) == 1) && (At(x - 1, y) == 0);
                    if (edge && runY0 < 0) runY0 = y;
                    if ((!edge || y == b.Y + b.Height) && runY0 >= 0)
                    {
                        float ex = ox + x * s;
                        float ey = oy + runY0 * s;
                        float len = (y - runY0) * s;
                        DrawAntsV(renderer, ex, ey, len, phase, antsOn, antsOff, antsThickness, invert: false);
                        runY0 = -1;
                    }
                }
            }

            // Right edges: cell=1 and right=0 (invert)
            for (int x = b.X; x < b.X + b.Width; x++)
            {
                int runY0 = -1;
                for (int y = b.Y; y <= b.Y + b.Height; y++)
                {
                    bool edge = (At(x, y) == 1) && (At(x + 1, y) == 0);
                    if (edge && runY0 < 0) runY0 = y;
                    if ((!edge || y == b.Y + b.Height) && runY0 >= 0)
                    {
                        float ex = ox + (x + 1) * s;
                        float ey = oy + runY0 * s;
                        float len = (y - runY0) * s;
                        DrawAntsV(renderer, ex, ey, len, phase, antsOn, antsOff, antsThickness, invert: true);
                        runY0 = -1;
                    }
                }
            }
        }

        // local helpers using white/black segment technique
        private static void DrawAntsH(ICanvasRenderer renderer, float ex, float ey, float length,
                              float phase, float on, float off, float thick, bool invert)
        {
            float period = on + off;
            float start = -phase + (invert ? on : 0f);
            while (start < length)
            {
                float on0 = Math.Max(0, start);
                float on1 = Math.Min(length, start + on);
                if (on1 > on0)
                {
                    renderer.FillRectangle(ex + on0, ey - thick * 0.5f, on1 - on0, thick, Colors.White);
                    float bs0 = on0 + period * 0.5f, bs1 = on1 + period * 0.5f;
                    if (bs0 < length)
                    {
                        bs0 = Math.Max(0, bs0); bs1 = Math.Min(length, bs1);
                        if (bs1 > bs0) renderer.FillRectangle(ex + bs0, ey - thick * 0.5f, bs1 - bs0, thick, Colors.Black);
                    }
                }
                start += period;
            }
        }

        private static void DrawAntsV(ICanvasRenderer renderer, float ex, float ey, float length,
                              float phase, float on, float off, float thick, bool invert)
        {
            float period = on + off;
            float start = -phase + (invert ? on : 0f);
            while (start < length)
            {
                float on0 = Math.Max(0, start);
                float on1 = Math.Min(length, start + on);
                if (on1 > on0)
                {
                    renderer.FillRectangle(ex - thick * 0.5f, ey + on0, thick, on1 - on0, Colors.White);
                    float bs0 = on0 + period * 0.5f, bs1 = on1 + period * 0.5f;
                    if (bs0 < length)
                    {
                        bs0 = Math.Max(0, bs0); bs1 = Math.Min(length, bs1);
                        if (bs1 > bs0) renderer.FillRectangle(ex - thick * 0.5f, ey + bs0, thick, bs1 - bs0, Colors.Black);
                    }
                }
                start += period;
            }
        }

        // ───────────────────────────── helpers ─────────────────────────────

        /// <summary>
        /// Fills or clears a rectangle in the mask.
        /// </summary>
        private void Fill(RectInt32 r, bool modeAdd)
        {
            if (_w == 0 || _h == 0) return;

            int x0 = Math.Clamp(r.X, 0, _w);
            int y0 = Math.Clamp(r.Y, 0, _h);
            int x1 = Math.Clamp(r.X + r.Width, 0, _w);
            int y1 = Math.Clamp(r.Y + r.Height, 0, _h);
            if (x1 <= x0 || y1 <= y0) return;

            int rowWidth = x1 - x0;
            byte fillValue = modeAdd ? (byte)1 : (byte)0;

            for (int y = y0; y < y1; y++)
            {
                int rowStart = y * _w + x0;
                // Use Array.Fill for efficient row-based fill instead of per-pixel loop
                Array.Fill(_m, fillValue, rowStart, rowWidth);
            }

            if (modeAdd)
            {
                // expand bounds cheaply
                if (IsEmpty) _bounds = CreateRect(x0, y0, x1 - x0, y1 - y0);
                else
                {
                    int bx0 = Math.Min(_bounds.X, x0);
                    int by0 = Math.Min(_bounds.Y, y0);
                    int bx1 = Math.Max(_bounds.X + _bounds.Width, x1);
                    int by1 = Math.Max(_bounds.Y + _bounds.Height, y1);
                    _bounds = CreateRect(bx0, by0, bx1 - bx0, by1 - by0);
                }
            }
            else
            {
                // subtract may create holes; recompute bounds conservatively
                RecomputeBounds();
            }
        }

        /// <summary>
        /// Creates a deep copy of this selection region.
        /// </summary>
        /// <returns>A new <see cref="SelectionRegion"/> with identical mask, bounds, and offset.</returns>
        public SelectionRegion Clone()
        {
            var r = new SelectionRegion();
            r._w = _w;
            r._h = _h;
            r._m = (byte[])_m.Clone();
            r._bounds = _bounds;
            r._offsetX = _offsetX;
            r._offsetY = _offsetY;
            return r;
        }

        /// <summary>
        /// Copies all data from another selection region into this one.
        /// </summary>
        /// <param name="other">The region to copy from.</param>
        /// <remarks>
        /// This replaces all data in this region with a copy from the source region,
        /// including mask data, bounds, and offset. Used for undo/redo operations.
        /// </remarks>
        public void CopyFrom(SelectionRegion other)
        {
            _w = other._w;
            _h = other._h;
            _m = other._m.Length > 0 ? (byte[])other._m.Clone() : Array.Empty<byte>();
            _bounds = other._bounds;
            _offsetX = other._offsetX;
            _offsetY = other._offsetY;
            _boundsInvalid = false;
        }

        /// <summary>
        /// Adds another region to this selection (union).
        /// </summary>
        /// <param name="other">Region to union with this selection.</param>
        /// <exception cref="InvalidOperationException">Thrown if dimensions don't match.</exception>
        /// <remarks>
        /// Performs bitwise OR of both masks. Expands bounds to union of both regions' bounds.
        /// </remarks>
        public void AddRegion(SelectionRegion other)
        {
            if (other._w != _w || other._h != _h)
                throw new InvalidOperationException("SelectionRegion size mismatch");

            int len = _m.Length;
            for (int i = 0; i < len; i++)
                if (other._m[i] != 0)
                    _m[i] = 1;

            // Expand bounds to include other's bounds
            if (IsEmpty)
                _bounds = other._bounds;
            else if (!other.IsEmpty)
            {
                int x0 = Math.Min(_bounds.X, other._bounds.X);
                int y0 = Math.Min(_bounds.Y, other._bounds.Y);
                int x1 = Math.Max(_bounds.X + _bounds.Width, other._bounds.X + other._bounds.Width);
                int y1 = Math.Max(_bounds.Y + _bounds.Height, other._bounds.Y + other._bounds.Height);
                _bounds = CreateRect(x0, y0, x1 - x0, y1 - y0);
            }
        }

        /// <summary>
        /// Intersects this selection with another region.
        /// </summary>
        /// <param name="other">Region to intersect with.</param>
        /// <exception cref="InvalidOperationException">Thrown if dimensions don't match.</exception>
        /// <remarks>
        /// Performs bitwise AND of both masks. Triggers bounds recomputation since intersection
        /// may create gaps or shrinkage.
        /// </remarks>
        public void IntersectRegion(SelectionRegion other)
        {
            if (other._w != _w || other._h != _h)
                throw new InvalidOperationException("SelectionRegion size mismatch");

            int len = _m.Length;
            for (int i = 0; i < len; i++)
                if (other._m[i] == 0)
                    _m[i] = 0;

            RecomputeBounds();
        }

        /// <summary>
        /// Subtracts another region from this selection.
        /// </summary>
        /// <param name="other">Region to subtract.</param>
        /// <exception cref="InvalidOperationException">Thrown if dimensions don't match.</exception>
        /// <remarks>
        /// Performs bitwise AND NOT (clears bits where other is set). Triggers bounds recomputation.
        /// </remarks>
        public void SubtractRegion(SelectionRegion other)
        {
            if (other._w != _w || other._h != _h)
                throw new InvalidOperationException("SelectionRegion size mismatch");

            int len = _m.Length;
            for (int i = 0; i < len; i++)
                if (other._m[i] != 0)
                    _m[i] = 0;

            RecomputeBounds();
        }

        /// <summary>
        /// Recomputes tight bounding rectangle by scanning entire mask.
        /// </summary>
        /// <remarks>
        /// O(W×H) operation. Called after subtract operations that may shrink the selection.
        /// Finds minimal rectangle containing all selected pixels.
        /// </remarks>
        private void RecomputeBounds()
        {
            if (_w == 0 || _h == 0) { _bounds = CreateRect(0, 0, 0, 0); return; }

            int minX = _w, minY = _h, maxX = -1, maxY = -1;
            for (int y = 0; y < _h; y++)
            {
                int row = y * _w;
                for (int x = 0; x < _w; x++)
                {
                    if (_m[row + x] == 0) continue;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            _bounds = (maxX < 0)
                ? CreateRect(0, 0, 0, 0)
                : CreateRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// Safely reads a mask pixel, returning 0 for out-of-bounds coordinates.
        /// </summary>
        private byte At(int x, int y)
        {
            if ((uint)x >= (uint)_w || (uint)y >= (uint)_h) return 0;
            return _m[y * _w + x];
        }

        /// <summary>
        /// Clears a single pixel in the mask without triggering bounds recomputation.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <remarks>
        /// This is a performance optimization for batch subtract operations (e.g., paint selection tool).
        /// After calling this multiple times, you MUST call <see cref="RecomputeBoundsIfNeeded"/> to update bounds.
        /// </remarks>
        public void SubtractPixelFast(int x, int y)
        {
            if (_w == 0 || _h == 0) return;
            if ((uint)x >= (uint)_w || (uint)y >= (uint)_h) return;

            _m[y * _w + x] = 0;
            _boundsInvalid = true; // Mark bounds as needing recomputation
        }

        private bool _boundsInvalid = false;

        /// <summary>
        /// Recomputes bounds if they were invalidated by batch operations.
        /// </summary>
        /// <remarks>
        /// Call this after a series of <see cref="SubtractPixelFast"/> operations to finalize bounds.
        /// </remarks>
        public void RecomputeBoundsIfNeeded()
        {
            if (_boundsInvalid)
            {
                RecomputeBounds();
                _boundsInvalid = false;
            }
        }

        /// <summary>
        /// Inverts the selection within the specified document bounds.
        /// </summary>
        /// <param name="docWidth">Document width in pixels.</param>
        /// <param name="docHeight">Document height in pixels.</param>
        /// <remarks>
        /// <para>
        /// Flips all pixels within the document bounds: selected becomes unselected, and vice versa.
        /// Pixels outside the current mask buffer (if any) are treated as unselected and become selected.
        /// </para>
        /// <para>
        /// The region's offset is reset to (0, 0) since the inverted selection covers the full document.
        /// </para>
        /// </remarks>
        public void Invert(int docWidth, int docHeight)
        {
            if (docWidth <= 0 || docHeight <= 0) return;

            // Ensure buffer is sized for the full document
            if (_w != docWidth || _h != docHeight)
            {
                // Create new buffer at document size
                var newMask = new byte[docWidth * docHeight];

                // Copy existing mask data at correct offset position, then invert
                for (int y = 0; y < docHeight; y++)
                {
                    for (int x = 0; x < docWidth; x++)
                    {
                        // Check if this position was selected in the old mask
                        int oldLocalX = x - _offsetX;
                        int oldLocalY = y - _offsetY;

                        bool wasSelected = false;
                        if (oldLocalX >= 0 && oldLocalX < _w && oldLocalY >= 0 && oldLocalY < _h)
                        {
                            wasSelected = _m[oldLocalY * _w + oldLocalX] != 0;
                        }

                        // Invert: selected becomes unselected, unselected becomes selected
                        newMask[y * docWidth + x] = wasSelected ? (byte)0 : (byte)1;
                    }
                }

                _w = docWidth;
                _h = docHeight;
                _m = newMask;
                _offsetX = 0;
                _offsetY = 0;
            }
            else
            {
                // Same size - just invert in place
                for (int i = 0; i < _m.Length; i++)
                {
                    _m[i] = _m[i] != 0 ? (byte)0 : (byte)1;
                }
                _offsetX = 0;
                _offsetY = 0;
            }

            RecomputeBounds();
        }
    }
}
