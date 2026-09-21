using System;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Core.Document
{
    /// <summary>
    /// Geometry for a font document: where a glyph's cell is, how much ink it holds, and what that
    /// means for spacing. Pure functions over a document and a pixel buffer, so the font model can
    /// be exercised without any UI.
    /// </summary>
    public static class FontMetricsOps
    {
        /// <summary>
        /// The left bearing a font file would store: how far the ink starts from the origin.
        /// Derived, because the editor anchors spacing to the cell instead.
        /// </summary>
        public static int LeftBearingOf(CanvasDocument doc, int codepoint)
        {
            var state = doc.FontState;
            if (!state.TryGet(codepoint, out var g) || g.CellIndex < 0) return 0;
            var ink = MeasureInk(doc, g.CellIndex);
            if (ink is not { } r) return 0;
            return r.X - ResolveMetrics(doc, codepoint).OriginX;
        }

        /// <summary>Number of glyph cells in the sheet.</summary>
        public static int CellCount(CanvasDocument doc) =>
            Math.Max(0, doc.TileCounts.Width * doc.TileCounts.Height);

        /// <summary>
        /// The em box inside a cell, in cell-local pixels. Equal to the whole cell for a font with
        /// no overhang room.
        /// </summary>
        public static RectInt32 EmBox(CanvasDocument doc)
        {
            var st = doc.FontState;
            int cw = Math.Max(1, doc.TileSize.Width);
            int ch = Math.Max(1, doc.TileSize.Height);
            return CreateRect(st.EmLeft, st.EmTop, st.ResolveEmWidth(cw), st.ResolveEmHeight(ch));
        }

        /// <summary>The design size: multiples of this are the sizes that render evenly.</summary>
        public static int EmHeightOf(CanvasDocument doc) =>
            doc.FontState.ResolveEmHeight(doc.TileSize.Height);

        /// <summary>The cell's rectangle in document pixels. Cells are numbered row-major.</summary>
        public static RectInt32 GetCellRect(CanvasDocument doc, int cellIndex)
        {
            int cols = Math.Max(1, doc.TileCounts.Width);
            int w = Math.Max(1, doc.TileSize.Width);
            int h = Math.Max(1, doc.TileSize.Height);
            int col = cellIndex % cols;
            int row = cellIndex / cols;
            return CreateRect(col * w, row * h, w, h);
        }

        /// <summary>
        /// Tight bounds of the non-transparent pixels inside a cell, in cell-local coordinates,
        /// or null when the cell is empty. An empty cell is a legitimate glyph: it's the space.
        /// </summary>
        public static RectInt32? MeasureInk(byte[] pixels, int surfaceW, int surfaceH, RectInt32 cell)
        {
            if (pixels == null || cell.Width <= 0 || cell.Height <= 0) return null;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < cell.Height; y++)
            {
                int sy = cell.Y + y;
                if ((uint)sy >= (uint)surfaceH) continue;
                int row = sy * surfaceW * 4;
                for (int x = 0; x < cell.Width; x++)
                {
                    int sx = cell.X + x;
                    if ((uint)sx >= (uint)surfaceW) continue;
                    if (pixels[row + sx * 4 + 3] == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0) return null;
            return CreateRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>Ink bounds of a cell, measured against the document's composited surface.</summary>
        public static RectInt32? MeasureInk(CanvasDocument doc, int cellIndex)
        {
            var surf = doc.Surface;
            return MeasureInk(surf.Pixels, surf.Width, surf.Height, GetCellRect(doc, cellIndex));
        }

        /// <summary>
        /// The spacing a glyph actually renders with. Monospace overrides everything, a glyph with
        /// auto-fit off uses its stored values, and otherwise the ink is measured and the font's
        /// side bearing applied either side.
        /// </summary>
        public static (int OriginX, int Advance) ResolveMetrics(
            CanvasDocument doc, int codepoint, byte[] pixels, int surfaceW, int surfaceH)
        {
            var state = doc.FontState;
            if (!state.TryGet(codepoint, out var g) || g.CellIndex < 0)
                return (0, 0);

            if (state.Monospace)
            {
                int emWidth = state.ResolveEmWidth(doc.TileSize.Width);
                return ClampToCell(doc, g.AutoFit ? state.EmLeft : g.OriginX, Math.Max(1, emWidth));
            }

            if (!g.AutoFit)
                return ClampToCell(doc, g.OriginX, g.Advance);

            int sb = Math.Max(0, state.SideBearing);
            var ink = MeasureInk(pixels, surfaceW, surfaceH, GetCellRect(doc, g.CellIndex));
            if (ink is not { } r)
                return ClampToCell(doc, 0, Math.Max(1, sb * 2));   // no ink: a space, as wide as its bearings

            return ClampToCell(doc, r.X - sb, r.Width + sb * 2);
        }

        /// <summary>
        /// Holds the pen and the advance post inside the glyph's own cell, overhang room included.
        /// A post outside the cell would move the pen further than the cell it came from, so the
        /// next glyph would be laid down on top of ink that is not its own.
        /// </summary>
        public static (int OriginX, int Advance) ClampToCell(CanvasDocument doc, int originX, int advance)
        {
            int cellW = Math.Max(1, doc.TileSize.Width);
            int origin = Math.Clamp(originX, 0, cellW);
            return (origin, Math.Clamp(advance, 0, cellW - origin));
        }

        /// <summary>Spacing resolved against the document's composited surface.</summary>
        public static (int OriginX, int Advance) ResolveMetrics(CanvasDocument doc, int codepoint)
        {
            var surf = doc.Surface;
            return ResolveMetrics(doc, codepoint, surf.Pixels, surf.Width, surf.Height);
        }

        /// <summary>
        /// Pixel sizes this font renders evenly at, being the multiples of the em that land in a
        /// usable range. Anything else renders with uneven stems however the font is built.
        /// </summary>
        public static int[] CleanSizes(CanvasDocument doc, int minPx = 8, int maxPx = 48)
        {
            int em = EmHeightOf(doc);
            var list = new System.Collections.Generic.List<int>();
            for (int k = 1; k * em <= maxPx; k++)
            {
                int size = k * em;
                if (size >= minPx) list.Add(size);
            }
            if (list.Count == 0) list.Add(em);
            return list.ToArray();
        }
    }
}
