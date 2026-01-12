using System;
using System.Collections.Generic;

namespace PixlPunkt.Core.Selection
{
    /// <summary>
    /// Represents a 2D boolean mask for pixel-level selection tracking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionMask provides a simple boolean grid where each pixel can be either selected (true)
    /// or not selected (false). It serves as the authoritative hit-test data for selection operations
    /// like flood fill, magic wand, and lasso tools.
    /// </para>
    /// <para>
    /// Key features:
    /// - Fixed dimensions matching document size
    /// - Rectangle-based modification (AddRect for marquee selection)
    /// - Point containment testing
    /// - Enumeration of all selected pixels (for outline generation after transforms)
    /// </para>
    /// <para>
    /// The mask uses a 2D boolean array indexed as [x, y] for efficient access. For large documents,
    /// consider using a more memory-efficient sparse representation if only a small percentage of
    /// pixels are typically selected.
    /// </para>
    /// </remarks>
    public class SelectionMask
    {
        private readonly int _w, _h;
        private readonly bool[,] _mask;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionMask"/> class with specified dimensions.
        /// </summary>
        /// <param name="w">The width of the mask in pixels (must match document width).</param>
        /// <param name="h">The height of the mask in pixels (must match document height).</param>
        /// <remarks>
        /// Creates an empty mask (all pixels unselected). The dimensions are fixed at construction
        /// and cannot be changed. If document dimensions change, a new mask must be created.
        /// </remarks>
        public SelectionMask(int w, int h)
        {
            _w = w; _h = h;
            _mask = new bool[w, h];
        }

        /// <summary>
        /// Clears the entire mask (deselects all pixels).
        /// </summary>
        /// <remarks>
        /// Sets all elements in the mask to false. This is faster than creating a new mask instance.
        /// </remarks>
        public void Clear()
        {
            Array.Clear(_mask, 0, _mask.Length);
        }

        /// <summary>
        /// Adds a rectangular region to the selection (marks pixels as selected).
        /// </summary>
        /// <param name="x">The X coordinate of the rectangle's top-left corner.</param>
        /// <param name="y">The Y coordinate of the rectangle's top-left corner.</param>
        /// <param name="w">The width of the rectangle in pixels.</param>
        /// <param name="h">The height of the rectangle in pixels.</param>
        /// <remarks>
        /// <para>
        /// Sets all pixels within the specified rectangle to true (selected). Coordinates are
        /// automatically clamped to mask bounds; out-of-bounds pixels are ignored.
        /// </para>
        /// <para>
        /// This method is typically used for marquee (rectangular) selection or for building
        /// up complex selections by combining multiple rectangles.
        /// </para>
        /// </remarks>
        public void AddRect(int x, int y, int w, int h)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                {
                    if (xx >= 0 && xx < _w && yy >= 0 && yy < _h)
                        _mask[xx, yy] = true;
                }
        }

        /// <summary>
        /// Determines whether the specified pixel is selected.
        /// </summary>
        /// <param name="x">The X coordinate of the pixel.</param>
        /// <param name="y">The Y coordinate of the pixel.</param>
        /// <returns>
        /// <c>true</c> if the pixel at (x, y) is selected; otherwise, <c>false</c>.
        /// Returns false for out-of-bounds coordinates.
        /// </returns>
        /// <remarks>
        /// This is the primary hit-test method used to determine if drawing operations should
        /// affect a specific pixel. Out-of-bounds checks are performed for safety.
        /// </remarks>
        public bool Contains(int x, int y)
        {
            if (x < 0 || x >= _w || y < 0 || y >= _h)
                return false;
            return _mask[x, y];
        }

        /// <summary>
        /// Enumerates all selected pixels in the mask.
        /// </summary>
        /// <returns>
        /// An enumerable of (x, y) coordinate tuples for all pixels where the mask is true.
        /// Pixels are enumerated in row-major order (top-to-bottom, left-to-right).
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is used after transform operations (rotate, scale) to rebuild the
        /// selection polygon outline from the transformed pixel footprint.
        /// </para>
        /// <para>
        /// For large selections, this can produce many coordinates. Consider using the selection
        /// outline polygon (<see cref="PixelSelection.Poly"/>) for visual representation instead
        /// of iterating all pixels.
        /// </para>
        /// </remarks>
        public IEnumerable<(int x, int y)> EnumerateFilledPixels()
        {
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                    if (_mask[x, y])
                        yield return (x, y);
        }
    }
}
