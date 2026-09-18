using System;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Core.Selection
{
    /// <summary>
    /// Ways of rebuilding the document's selection mask from a floating selection. These used
    /// to live in the view (duplicated); history items need them, so they live in Core.
    /// </summary>
    public static class SelectionRegionBuilders
    {
        /// <summary>
        /// Makes <paramref name="region"/> the floating selection's mask under its current scale
        /// and rotation, centred on the transform centre exactly like the pixels are drawn and
        /// committed. Transparent pixels inside the marquee stay selected because the mask, not
        /// the alpha, is the source.
        /// </summary>
        public static void RebuildFromMask(SelectionRegion region, FloatingSelection f, int docW, int docH)
        {
            region.Clear();
            if (f.Mask.Length != f.Width * f.Height)
            {
                region.EnsureSize(docW, docH);
                return;   // offloaded; nothing to build from
            }

            var (mask, mw, mh) = f.GetTransformedShape();
            int ox = f.OrigCenterX - mw / 2;
            int oy = f.OrigCenterY - mh / 2;

            // The shape is placed in world space through the region's offset, the same way a
            // live drag does, so a shape hanging off the canvas is kept whole. The local mask is
            // at least the document's size and grows to fit a shape scaled past the canvas
            // (EnsureSize is grow-only, so the history refresh cannot shrink it back).
            region.EnsureSize(Math.Max(docW, mw), Math.Max(docH, mh));
            region.SetOffset(ox, oy);

            for (int y = 0; y < mh; y++)
            {
                int row = y * mw;
                int runStart = -1;
                int limit = mw;
                for (int x = 0; x < limit; x++)
                {
                    bool on = mask[row + x] != 0;
                    if (on && runStart < 0) runStart = x;
                    else if (!on && runStart >= 0)
                    {
                        region.AddRect(CreateRect(runStart, y, x - runStart, 1));
                        runStart = -1;
                    }
                }
                if (runStart >= 0)
                    region.AddRect(CreateRect(runStart, y, limit - runStart, 1));
            }
        }

        /// <summary>True when every pixel inside <paramref name="bounds"/> is selected.</summary>
        public static bool IsRectangular(SelectionRegion region, RectInt32 bounds)
        {
            for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
                for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
                    if (!region.Contains(x, y)) return false;
            return true;
        }

    }
}
