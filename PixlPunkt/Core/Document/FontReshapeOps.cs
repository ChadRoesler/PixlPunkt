using System;
using System.Collections.Generic;
using System.Text;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Core.Document
{
    /// <summary>Where the old cell's pixels sit inside the new one when the cell size changes.</summary>
    public enum GlyphAnchor
    {
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
    }

    /// <summary>
    /// What reshaping a font would do, worked out in full before anything is touched. The dialog
    /// shows this so the destructive parts are stated in advance rather than discovered afterwards.
    /// </summary>
    /// <param name="EmSize">The new em box.</param>
    /// <param name="DrawingRoom">Blank pixels around the em on every side.</param>
    /// <param name="CellSize">The em plus the drawing room, which is the new tile size.</param>
    /// <param name="Columns">Cells across the sheet.</param>
    /// <param name="Rows">Cells down the sheet.</param>
    /// <param name="CanvasSize">The whole sheet in pixels.</param>
    /// <param name="Characters">The new character set, de-duplicated and in order.</param>
    /// <param name="Removed">Characters the font has today that the new set drops.</param>
    /// <param name="CellShrinks">Whether the cell gets smaller in either direction.</param>
    public readonly record struct FontReshapePlan(
        SizeInt32 EmSize,
        int DrawingRoom,
        SizeInt32 CellSize,
        int Columns,
        int Rows,
        SizeInt32 CanvasSize,
        string Characters,
        IReadOnlyList<int> Removed,
        bool CellShrinks)
    {
        /// <summary>Characters would be dropped, taking whatever was drawn in them.</summary>
        public bool LosesCharacters => Removed.Count > 0;

        /// <summary>The cell gets smaller, so ink can fall outside it and be cut off.</summary>
        public bool CropsGlyphs => CellShrinks;

        /// <summary>Whether anything drawn today could be lost by going ahead.</summary>
        public bool IsDestructive => LosesCharacters || CropsGlyphs;
    }

    /// <summary>
    /// Changes a font's em box, drawing room or character set after it has been created, moving
    /// every glyph's pixels to wherever its cell ends up.
    /// </summary>
    /// <remarks>
    /// This cannot go through the ordinary canvas resize, which shifts the whole surface by one
    /// offset. Changing the cell size moves every cell by a different amount, so each glyph is
    /// copied from its old rectangle to its new one out of a snapshot taken beforehand.
    /// </remarks>
    public static class FontReshapeOps
    {
        /// <summary>The characters a font has today, in codepoint order.</summary>
        public static string CurrentCharacters(CanvasDocument doc)
        {
            var sb = new StringBuilder();
            foreach (var g in FontGlyphOps.Summarize(doc))
                sb.Append(char.ConvertFromUtf32(g.Codepoint));
            return sb.ToString();
        }

        /// <summary>Removes duplicates and line breaks, keeping the order given.</summary>
        public static string Normalise(string? characters)
        {
            var seen = new HashSet<char>();
            var sb = new StringBuilder();
            foreach (char c in characters ?? string.Empty)
            {
                if (c is '\r' or '\n' or '\t') continue;
                if (seen.Add(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Works out the whole reshape without touching the document, including what it would cost.
        /// </summary>
        /// <param name="doc">The font document.</param>
        /// <param name="emWidth">New em width.</param>
        /// <param name="emHeight">New em height.</param>
        /// <param name="drawingRoom">New drawing room on every side of the em.</param>
        /// <param name="characters">The new character set, or null to keep the current one.</param>
        /// <param name="columns">Cells across, or zero to keep the current column count.</param>
        public static FontReshapePlan Plan(
            CanvasDocument doc,
            int emWidth,
            int emHeight,
            int drawingRoom,
            string? characters = null,
            int columns = 0)
        {
            emWidth = Math.Max(1, emWidth);
            emHeight = Math.Max(1, emHeight);
            drawingRoom = Math.Clamp(drawingRoom, 0, 64);

            int cellW = emWidth + drawingRoom * 2;
            int cellH = emHeight + drawingRoom * 2;

            string chars = Normalise(characters ?? CurrentCharacters(doc));
            int cols = columns > 0 ? columns : Math.Max(1, doc.TileCounts.Width);
            cols = Math.Clamp(cols, 1, Math.Max(1, chars.Length));
            int rows = chars.Length == 0 ? 1 : (chars.Length + cols - 1) / cols;

            var kept = new HashSet<int>();
            foreach (char c in chars) kept.Add(c);

            var removed = new List<int>();
            foreach (var g in FontGlyphOps.Summarize(doc))
                if (!kept.Contains(g.Codepoint)) removed.Add(g.Codepoint);

            bool shrinks = cellW < doc.TileSize.Width || cellH < doc.TileSize.Height;

            return new FontReshapePlan(
                EmSize: CreateSize(emWidth, emHeight),
                DrawingRoom: drawingRoom,
                CellSize: CreateSize(cellW, cellH),
                Columns: cols,
                Rows: rows,
                CanvasSize: CreateSize(cols * cellW, rows * cellH),
                Characters: chars,
                Removed: removed,
                CellShrinks: shrinks);
        }

        /// <summary>The cell a position in the new sheet occupies, computed from the plan alone.</summary>
        public static RectInt32 CellRectIn(in FontReshapePlan plan, int cellIndex)
        {
            int col = cellIndex % Math.Max(1, plan.Columns);
            int row = cellIndex / Math.Max(1, plan.Columns);
            return CreateRect(
                col * plan.CellSize.Width,
                row * plan.CellSize.Height,
                plan.CellSize.Width,
                plan.CellSize.Height);
        }

        /// <summary>
        /// Where the old cell's content sits inside the new cell. Works for a shrinking cell too:
        /// the delta goes negative and the anchor then decides which edge survives.
        /// </summary>
        public static (int X, int Y) AnchorOffset(GlyphAnchor anchor, int deltaWidth, int deltaHeight)
        {
            int x = anchor switch
            {
                GlyphAnchor.TopLeft or GlyphAnchor.MiddleLeft or GlyphAnchor.BottomLeft => 0,
                GlyphAnchor.TopRight or GlyphAnchor.MiddleRight or GlyphAnchor.BottomRight => deltaWidth,
                _ => Halve(deltaWidth),
            };

            int y = anchor switch
            {
                GlyphAnchor.TopLeft or GlyphAnchor.TopCenter or GlyphAnchor.TopRight => 0,
                GlyphAnchor.BottomLeft or GlyphAnchor.BottomCenter or GlyphAnchor.BottomRight => deltaHeight,
                _ => Halve(deltaHeight),
            };

            return (x, y);
        }

        /// <summary>Halves toward zero, so a centred shrink and a centred grow stay symmetric.</summary>
        private static int Halve(int delta) => delta >= 0 ? delta / 2 : -((-delta) / 2);

        /// <summary>
        /// Carries out the plan: moves every surviving glyph's pixels to its new cell, resizes the
        /// sheet and rewrites the font's mapping, spacing and guides to match.
        /// </summary>
        public static void Apply(CanvasDocument doc, in FontReshapePlan plan, GlyphAnchor anchor)
        {
            var state = doc.FontState;
            if (!state.HasState) return;

            int oldCellW = Math.Max(1, doc.TileSize.Width);
            int oldCellH = Math.Max(1, doc.TileSize.Height);
            int newCellW = plan.CellSize.Width;
            int newCellH = plan.CellSize.Height;

            var (offsetX, offsetY) = AnchorOffset(anchor, newCellW - oldCellW, newCellH - oldCellH);

            // Old rectangles and old metrics, captured before any of it moves.
            var oldRects = new Dictionary<int, RectInt32>();
            var oldMetrics = new Dictionary<int, GlyphMetrics>();
            foreach (var (codepoint, glyph) in state.Glyphs)
            {
                oldMetrics[codepoint] = glyph.Clone();
                if (glyph.CellIndex >= 0)
                    oldRects[codepoint] = FontMetricsOps.GetCellRect(doc, glyph.CellIndex);
            }

            int newW = plan.CanvasSize.Width;
            int newH = plan.CanvasSize.Height;

            doc.RaiseBeforeStructureChanged();

            foreach (var layer in doc.GetAllRasterLayers())
                RemapLayer(layer.Surface, plan, oldRects, oldCellW, oldCellH, offsetX, offsetY, newW, newH);

            // Geometry first: the spacing clamp below reads the new tile size off the document.
            doc.RestoreDimensions(newW, newH, CreateSize(plan.Columns, plan.Rows));
            doc.SetTileSize(plan.CellSize);

            RemapFontState(doc, plan, oldMetrics, offsetX, offsetY, newCellW, newCellH);

            doc.Surface.Resize(newW, newH, null);
            doc.CompositeTo(doc.Surface);

            doc.RaiseStructureChanged();
            doc.RaiseFontChanged();
        }

        /// <summary>Rebuilds one layer's pixels, glyph by glyph, out of a snapshot of the old ones.</summary>
        private static void RemapLayer(
            Imaging.PixelSurface surface,
            in FontReshapePlan plan,
            Dictionary<int, RectInt32> oldRects,
            int oldCellW,
            int oldCellH,
            int offsetX,
            int offsetY,
            int newW,
            int newH)
        {
            byte[] source = surface.Pixels;
            int srcW = surface.Width;
            int srcH = surface.Height;
            var destination = new byte[newW * newH * 4];

            int cellIndex = 0;
            foreach (char c in plan.Characters)
            {
                var to = CellRectIn(plan, cellIndex++);
                if (!oldRects.TryGetValue(c, out var from)) continue;

                BlitCell(source, srcW, srcH, from, oldCellW, oldCellH,
                         destination, newW, newH, to, offsetX, offsetY);
            }

            surface.Resize(newW, newH, destination);
        }

        /// <summary>
        /// Copies one cell, placing the old content at the anchor offset inside the new cell and
        /// dropping whatever falls outside it. That clipping is the cropping the dialog warns about.
        /// </summary>
        private static void BlitCell(
            byte[] source, int srcW, int srcH, RectInt32 from, int oldCellW, int oldCellH,
            byte[] destination, int destW, int destH, RectInt32 to, int offsetX, int offsetY)
        {
            for (int y = 0; y < oldCellH; y++)
            {
                int sy = from.Y + y;
                int dy = to.Y + y + offsetY;
                if (sy < 0 || sy >= srcH || dy < to.Y || dy >= to.Y + to.Height || dy < 0 || dy >= destH)
                    continue;

                for (int x = 0; x < oldCellW; x++)
                {
                    int sx = from.X + x;
                    int dx = to.X + x + offsetX;
                    if (sx < 0 || sx >= srcW || dx < to.X || dx >= to.X + to.Width || dx < 0 || dx >= destW)
                        continue;

                    Buffer.BlockCopy(source, (sy * srcW + sx) * 4, destination, (dy * destW + dx) * 4, 4);
                }
            }
        }

        /// <summary>
        /// Rewrites the mapping so the characters land in their new cells, carries each glyph's
        /// spacing across by the anchor offset, and moves the guides with the ink.
        /// </summary>
        private static void RemapFontState(
            CanvasDocument doc,
            in FontReshapePlan plan,
            Dictionary<int, GlyphMetrics> oldMetrics,
            int offsetX,
            int offsetY,
            int newCellW,
            int newCellH)
        {
            var state = doc.FontState;
            int oldTopline = state.ToplineY;
            int oldBaseline = state.BaselineY;

            state.Clear();

            int cellIndex = 0;
            foreach (char c in plan.Characters)
            {
                var glyph = state.GetOrAdd(c);
                glyph.CellIndex = cellIndex++;

                if (!oldMetrics.TryGetValue(c, out var old)) continue;

                glyph.AutoFit = old.AutoFit;
                var (originX, advance) = FontMetricsOps.ClampToCell(doc, old.OriginX + offsetX, old.Advance);
                glyph.OriginX = originX;
                glyph.Advance = advance;
            }

            state.SetEmBox(plan.EmSize.Width, plan.EmSize.Height, newCellW, newCellH);

            // The guides follow the ink rather than staying on their old row numbers, so a glyph
            // that was sitting on the baseline still is once the cell has changed size.
            state.SetGuides(oldTopline + offsetY, oldBaseline + offsetY, newCellH);
        }
    }
}
