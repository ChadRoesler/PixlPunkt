using System;
using PixlPunkt.Core.Imaging;
using Windows.Graphics;

namespace PixlPunkt.Core.Selection
{
    /// <summary>
    /// Manages selection state for "floating" selection operations with cut/paste semantics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionState implements the classic image editor pattern where a rectangular selection can be
    /// "lifted" from the canvas into a floating buffer, moved around, and either committed back or canceled.
    /// This enables non-destructive repositioning of selected content.
    /// </para>
    /// <para><strong>State Machine:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Inactive</strong>: No selection exists (<see cref="Active"/> = false).</item>
    /// <item><strong>Active</strong>: Rectangle selected but not floating. Pixels remain in place.</item>
    /// <item><strong>Floating</strong>: Selection lifted to buffer. Original pixels cleared. Can be moved freely.</item>
    /// </list>
    /// <para><strong>Workflow:</strong></para>
    /// <code>
    /// 1. Begin(rect)      → Active, not floating
    /// 2. Lift(pixels)     → Floating (copies pixels, clears original)
    /// 3. MoveTo(x, y)     → Updates float position
    /// 4. Commit(pixels)   → Blits back to new position, Active
    ///    OR Cancel(pixels) → Restores original position, clears state
    /// </code>
    /// <para><strong>Use Cases:</strong></para>
    /// <para>
    /// - Marquee selection with drag-to-move
    /// <br/>- Cut and reposition operations
    /// <br/>- Temporary layer-like manipulation of regions
    /// <br/>- Undo-friendly pixel transforms (backup preserved until commit)
    /// </para>
    /// <para><strong>Backup System:</strong></para>
    /// <para>
    /// When <see cref="Lift"/> is called, original pixels are copied to internal backup before clearing.
    /// This enables <see cref="Cancel"/> to restore the original state if the operation is aborted.
    /// </para>
    /// </remarks>
    /// <seealso cref="SelectionEngine"/>
    /// <seealso cref="PixelOps"/>
    public sealed class SelectionState
    {
        /// <summary>
        /// Gets a value indicating whether a selection exists.
        /// </summary>
        /// <value>
        /// <c>true</c> if a selection rectangle is defined; otherwise, <c>false</c>.
        /// </value>
        public bool Active { get; private set; }

        /// <summary>
        /// Gets the current selection rectangle in document pixel coordinates.
        /// </summary>
        /// <value>
        /// Normalized rectangle (non-negative width/height) defining the selection bounds.
        /// </value>
        public RectInt32 Rect { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the selection is floating (lifted from document).
        /// </summary>
        /// <value>
        /// <c>true</c> if selection is in floating buffer; <c>false</c> if pixels remain in place.
        /// </value>
        public bool IsFloating { get; private set; }

        /// <summary>
        /// Gets the X coordinate of the floating buffer's top-left corner.
        /// </summary>
        /// <value>
        /// Document pixel X position. Only valid when <see cref="IsFloating"/> is true.
        /// </value>
        public int FloatX { get; private set; }

        /// <summary>
        /// Gets the Y coordinate of the floating buffer's top-left corner.
        /// </summary>
        /// <value>
        /// Document pixel Y position. Only valid when <see cref="IsFloating"/> is true.
        /// </value>
        public int FloatY { get; private set; }

        /// <summary>
        /// Gets the floating pixel buffer (BGRA format).
        /// </summary>
        /// <value>
        /// Array of packed BGRA pixels, or null if not floating. Length = <see cref="FloatW"/> × <see cref="FloatH"/> × 4.
        /// </value>
        public uint[]? FloatPixels { get; private set; }

        /// <summary>
        /// Gets the width of the floating buffer in pixels.
        /// </summary>
        public int FloatW { get; private set; }

        /// <summary>
        /// Gets the height of the floating buffer in pixels.
        /// </summary>
        public int FloatH { get; private set; }

        private uint[]? _backup;
        private int _srcW;
        private int _srcH;

        /// <summary>
        /// Begins a new selection with the specified rectangle.
        /// </summary>
        /// <param name="r">Selection rectangle (will be normalized to positive width/height).</param>
        /// <remarks>
        /// Sets <see cref="Active"/> to true and <see cref="IsFloating"/> to false.
        /// Rectangle dimensions are normalized to ensure non-negative width/height.
        /// </remarks>
        public void Begin(RectInt32 r)
        {
            Rect = Normalize(r);
            Active = true;
            IsFloating = false;
        }

        /// <summary>
        /// Updates the selection rectangle (e.g., during interactive drag).
        /// </summary>
        /// <param name="r">New rectangle (will be normalized).</param>
        /// <remarks>
        /// Only updates <see cref="Rect"/>. Does not affect floating state or buffers.
        /// Useful for live-updating marquee selection during drag operations.
        /// </remarks>
        public void Update(RectInt32 r)
        {
            Rect = Normalize(r);
        }

        /// <summary>
        /// Clears the selection and discards all state.
        /// </summary>
        /// <remarks>
        /// Resets <see cref="Active"/> and <see cref="IsFloating"/> to false.
        /// Releases floating buffer and backup references. No pixels are modified.
        /// </remarks>
        public void Clear()
        {
            Active = false;
            IsFloating = false;
            FloatPixels = null;
            _backup = null;
        }

        /// <summary>
        /// Lifts the selected rectangle from the layer into a floating buffer.
        /// </summary>
        /// <param name="layer">Source layer pixel array (BGRA packed uint).</param>
        /// <param name="lw">Layer width in pixels.</param>
        /// <param name="lh">Layer height in pixels.</param>
        /// <remarks>
        /// <para><strong>Operation:</strong></para>
        /// <list type="number">
        /// <item>Copies pixels from <paramref name="layer"/> within <see cref="Rect"/> to internal backup</item>
        /// <item>Clears the original rectangle (replaces with transparent pixels)</item>
        /// <item>Sets <see cref="IsFloating"/> to true and populates <see cref="FloatPixels"/></item>
        /// <item>Initializes float position to original <see cref="Rect"/> location</item>
        /// </list>
        /// <para>
        /// Does nothing if already floating or not active. The backup enables <see cref="Cancel"/>
        /// to restore original pixels if the operation is aborted.
        /// </para>
        /// </remarks>
        public void Lift(uint[] layer, int lw, int lh)
        {
            if (!Active || IsFloating) return;

            _backup = Imaging.PixelOps.CopyRect(layer, lw, lh, Rect);
            _srcW = Rect.Width;
            _srcH = Rect.Height;

            Imaging.PixelOps.ClearRect(layer, lw, lh, Rect);

            FloatPixels = _backup;
            FloatW = _srcW;
            FloatH = _srcH;
            FloatX = Rect.X;
            FloatY = Rect.Y;
            IsFloating = true;
        }

        /// <summary>
        /// Moves the floating buffer to a new position.
        /// </summary>
        /// <param name="x">New X coordinate for top-left corner.</param>
        /// <param name="y">New Y coordinate for top-left corner.</param>
        /// <remarks>
        /// Updates <see cref="FloatX"/> and <see cref="FloatY"/>. Only affects position;
        /// pixel content is unchanged. Does nothing if not floating.
        /// </remarks>
        public void MoveTo(int x, int y)
        {
            if (IsFloating)
            {
                FloatX = x;
                FloatY = y;
            }
        }

        /// <summary>
        /// Commits the floating buffer back to the layer at its current position.
        /// </summary>
        /// <param name="layer">Target layer pixel array (BGRA packed uint).</param>
        /// <param name="lw">Layer width in pixels.</param>
        /// <param name="lh">Layer height in pixels.</param>
        /// <remarks>
        /// <para><strong>Operation:</strong></para>
        /// <list type="number">
        /// <item>Blits <see cref="FloatPixels"/> to <paramref name="layer"/> at (<see cref="FloatX"/>, <see cref="FloatY"/>)</item>
        /// <item>Sets <see cref="IsFloating"/> to false</item>
        /// <item>Updates <see cref="Rect"/> to new position/size</item>
        /// <item>Releases floating buffer and backup references</item>
        /// </list>
        /// <para>
        /// Selection remains active after commit but is no longer floating. Does nothing if not floating.
        /// </para>
        /// </remarks>
        public void Commit(uint[] layer, int lw, int lh)
        {
            if (!IsFloating || FloatPixels is null) return;

            Imaging.PixelOps.Blit(layer, lw, lh, FloatX, FloatY, FloatPixels, FloatW, FloatH);
            IsFloating = false;
            _backup = null;
            FloatPixels = null;
            Rect = new RectInt32(FloatX, FloatY, FloatW, FloatH);
        }

        /// <summary>
        /// Cancels the floating operation and restores original pixels.
        /// </summary>
        /// <param name="layer">Target layer pixel array (BGRA packed uint).</param>
        /// <param name="lw">Layer width in pixels.</param>
        /// <param name="lh">Layer height in pixels.</param>
        /// <remarks>
        /// <para>
        /// If floating, restores backup pixels to original <see cref="Rect"/> position and calls <see cref="Clear"/>.
        /// If not floating, simply clears the selection. This enables undo-like behavior for lift operations.
        /// </para>
        /// </remarks>
        public void Cancel(uint[] layer, int lw, int lh)
        {
            if (!IsFloating || _backup is null)
            {
                Clear();
                return;
            }

            Imaging.PixelOps.Blit(layer, lw, lh, Rect.X, Rect.Y, _backup, _srcW, _srcH);
            Clear();
        }

        /// <summary>
        /// Deletes the selected content (clears pixels to transparent).
        /// </summary>
        /// <param name="layer">Target layer pixel array (BGRA packed uint).</param>
        /// <param name="lw">Layer width in pixels.</param>
        /// <param name="lh">Layer height in pixels.</param>
        /// <remarks>
        /// <para>
        /// If floating, discards the floating buffer. Clears the rectangle at <see cref="Rect"/> position
        /// and sets <see cref="Active"/> to false. Equivalent to pressing Delete key on selection.
        /// </para>
        /// </remarks>
        public void Delete(uint[] layer, int lw, int lh)
        {
            if (!Active) return;

            if (IsFloating)
            {
                FloatPixels = null;
                IsFloating = false;
            }

            Imaging.PixelOps.ClearRect(layer, lw, lh, Rect);
            Active = false;
        }

        /// <summary>
        /// Normalizes a rectangle to have non-negative width and height.
        /// </summary>
        /// <param name="r">Input rectangle (may have negative dimensions).</param>
        /// <returns>Rectangle with positive width/height, adjusting X/Y if necessary.</returns>
        /// <remarks>
        /// Converts rectangles created by dragging bottom-left to top-right (negative dimensions)
        /// into standard top-left anchored form. Essential for consistent rect operations.
        /// </remarks>
        private static RectInt32 Normalize(RectInt32 r)
        {
            int x = r.Width >= 0 ? r.X : r.X + r.Width;
            int y = r.Height >= 0 ? r.Y : r.Y + r.Height;
            int w = Math.Abs(r.Width);
            int h = Math.Abs(r.Height);
            return new RectInt32(x, y, w, h);
        }
    }
}
