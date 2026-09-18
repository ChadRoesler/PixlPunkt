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
        /// Rebuilds the mask as the original marquee rectangle scaled to (baseW, baseH) and
        /// rotated by <paramref name="angleDeg"/> about (centerX, centerY), clipped to
        /// <paramref name="dstClamp"/>. Used at commit so the marquee keeps its geometric shape.
        /// </summary>
        public static void RebuildAsRotatedRect(
            SelectionRegion region,
            int centerX, int centerY, int baseW, int baseH,
            double angleDeg, RectInt32 dstClamp, int surfW, int surfH)
        {
            region.EnsureSize(surfW, surfH);
            region.Clear();

            if (baseW <= 0 || baseH <= 0 || dstClamp.Width <= 0 || dstClamp.Height <= 0)
                return;

            double rad = angleDeg * Math.PI / 180.0;
            double cosA = Math.Cos(rad);
            double sinA = Math.Sin(rad);
            double halfW = baseW / 2.0;
            double halfH = baseH / 2.0;

            int x0 = dstClamp.X, y0 = dstClamp.Y;
            int x1 = dstClamp.X + dstClamp.Width;
            int y1 = dstClamp.Y + dstClamp.Height;

            for (int y = y0; y < y1; y++)
            {
                int runStart = -1;
                double dy = (y + 0.5) - centerY;
                for (int x = x0; x < x1; x++)
                {
                    double dx = (x + 0.5) - centerX;
                    double localX = cosA * dx + sinA * dy;
                    double localY = -sinA * dx + cosA * dy;
                    bool inside = localX >= -halfW && localX < halfW &&
                                  localY >= -halfH && localY < halfH;
                    if (inside)
                    {
                        if (runStart < 0) runStart = x;
                    }
                    else if (runStart >= 0)
                    {
                        region.AddRect(CreateRect(runStart, y, x - runStart, 1));
                        runStart = -1;
                    }
                }
                if (runStart >= 0)
                    region.AddRect(CreateRect(runStart, y, x1 - runStart, 1));
            }
        }

        /// <summary>
        /// Rebuilds the mask from the alpha channel of a (transformed) buffer placed at
        /// <paramref name="dstRect"/>, clipped to <paramref name="dstClamp"/>. Used after a scale
        /// bake on non-rectangular selections so the marquee follows the true shape.
        /// </summary>
        public static void RebuildFromTransformedBuffer(
            SelectionRegion region,
            RectInt32 dstRect, RectInt32 dstClamp, byte[] buf,
            int bufW, int bufH, int surfW, int surfH)
        {
            if (buf == null || bufW <= 0 || bufH <= 0) return;

            var r = ClampToSurface(dstClamp, surfW, surfH);
            region.EnsureSize(surfW, surfH);
            region.Clear();
            if (r.Width <= 0 || r.Height <= 0) return;

            int x0 = r.X, y0 = r.Y;
            int x1 = r.X + r.Width, y1 = r.Y + r.Height;

            for (int y = y0; y < y1; y++)
            {
                int sy = y - dstRect.Y;
                if ((uint)sy >= (uint)bufH) continue;

                int runStart = -1;
                for (int x = x0; x < x1; x++)
                {
                    int sx = x - dstRect.X;
                    bool on = (uint)sx < (uint)bufW && buf[(sy * bufW + sx) * 4 + 3] != 0;
                    if (on)
                    {
                        if (runStart < 0) runStart = x;
                    }
                    else if (runStart >= 0)
                    {
                        region.AddRect(CreateRect(runStart, y, x - runStart, 1));
                        runStart = -1;
                    }
                }
                if (runStart >= 0)
                    region.AddRect(CreateRect(runStart, y, x1 - runStart, 1));
            }
        }

        /// <summary>
        /// Rebuilds the mask from a floating selection's buffer alpha at its current position.
        /// The mask is kept document-sized; parts of the float that are off-canvas are simply not
        /// in the mask (they cannot be painted anyway, and hit-testing a floating selection uses
        /// the buffer directly). Also refreshes <see cref="FloatingSelection.RegionNonRectangular"/>.
        /// </summary>
        public static void RebuildFromFloating(SelectionRegion region, FloatingSelection f, int docW, int docH)
        {
            region.EnsureSize(docW, docH);
            region.Clear();

            var buf = f.Pixels;
            int bw = f.Width, bh = f.Height;
            bool isRect = true;

            for (int y = 0; y < bh; y++)
            {
                int docY = f.Y + y;
                int runStart = -1;
                for (int x = 0; x < bw; x++)
                {
                    bool opaque = buf[(y * bw + x) * 4 + 3] > 0;
                    if (!opaque) isRect = false;

                    if (opaque && runStart < 0)
                        runStart = x;
                    else if (!opaque && runStart >= 0)
                    {
                        if ((uint)docY < (uint)docH)
                            region.AddRect(CreateRect(f.X + runStart, docY, x - runStart, 1));
                        runStart = -1;
                    }
                }
                if (runStart >= 0 && (uint)docY < (uint)docH)
                    region.AddRect(CreateRect(f.X + runStart, docY, bw - runStart, 1));
            }

            f.RegionNonRectangular = !isRect;
        }

        /// <summary>
        /// Makes <paramref name="region"/> the floating selection's mask under its current scale
        /// and rotation, centred on the transform centre exactly like the pixels are drawn and
        /// committed. Transparent pixels inside the marquee stay selected because the mask, not
        /// the alpha, is the source.
        /// </summary>
        public static void RebuildFromMask(SelectionRegion region, FloatingSelection f, int docW, int docH)
        {
            region.EnsureSize(docW, docH);
            region.Clear();
            if (f.Mask.Length != f.Width * f.Height) return;   // offloaded; nothing to build from

            var (mask, mw, mh) = SelectionBufferOps.BuildTransformedMask(
                f.Mask, f.Width, f.Height, f.ScaleX, f.ScaleY, f.CumulativeAngleDeg + f.AngleDeg, f.RotMode);
            int ox = f.OrigCenterX - mw / 2;
            int oy = f.OrigCenterY - mh / 2;

            // The region keeps its document-sized local mask (history refresh re-asserts that
            // size) and places it in world space through its offset, the same way a live drag
            // does, so a shape hanging off the canvas is kept whole rather than clipped. The
            // shape sits at local (0,0); only a shape larger than the canvas loses its excess.
            region.SetOffset(ox, oy);

            for (int y = 0; y < Math.Min(mh, docH); y++)
            {
                int row = y * mw;
                int runStart = -1;
                int limit = Math.Min(mw, docW);
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

        private static RectInt32 ClampToSurface(RectInt32 r, int w, int h)
        {
            int x0 = Math.Clamp(r.X, 0, w), y0 = Math.Clamp(r.Y, 0, h);
            int x1 = Math.Clamp(r.X + r.Width, 0, w), y1 = Math.Clamp(r.Y + r.Height, 0, h);
            return CreateRect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }
    }
}
