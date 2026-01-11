using System;
using PixlPunkt.Core.Imaging;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Context for fill operations containing all configuration needed for the fill algorithm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This context is passed to <see cref="IFillPainter.FillAt"/> and contains
    /// all the information needed to perform a fill operation.
    /// </para>
    /// </remarks>
    public sealed class FillContext
    {
        /// <summary>
        /// Gets the target pixel surface for the fill operation.
        /// </summary>
        public required PixelSurface Surface { get; init; }

        /// <summary>
        /// Gets the replacement color (BGRA) to fill with.
        /// </summary>
        public required uint Color { get; init; }

        /// <summary>
        /// Gets the color tolerance for matching (0 = exact match, 255 = match all).
        /// </summary>
        public required int Tolerance { get; init; }

        /// <summary>
        /// Gets whether to use contiguous (flood) fill or global replacement.
        /// </summary>
        /// <value>
        /// <c>true</c> for contiguous 4-neighbor flood fill;
        /// <c>false</c> for global color replacement across entire surface.
        /// </value>
        public required bool Contiguous { get; init; }

        /// <summary>
        /// Gets the description for the history item.
        /// </summary>
        public string Description { get; init; } = "Fill";

        /// <summary>
        /// Gets the optional selection mask delegate.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When non-null, the fill operation should only affect pixels where this delegate returns true.
        /// The delegate takes (x, y) document coordinates and returns whether that pixel
        /// is inside the active selection.
        /// </para>
        /// <para>
        /// If null, no selection is active and all pixels can be filled.
        /// </para>
        /// </remarks>
        public Func<int, int, bool>? SelectionMask { get; init; }

        /// <summary>
        /// Checks if the given coordinates are inside the active selection.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>True if no selection is active or the pixel is inside the selection.</returns>
        public bool IsInSelection(int x, int y)
            => SelectionMask == null || SelectionMask(x, y);
    }
}
