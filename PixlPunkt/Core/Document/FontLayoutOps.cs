using System;
using System.Collections.Generic;
using Windows.Graphics;

namespace PixlPunkt.Core.Document
{
    /// <summary>Where one glyph's cell lands when a string is laid out.</summary>
    /// <param name="Codepoint">The character.</param>
    /// <param name="CellIndex">Its cell in the glyph sheet, or -1 when the character is unmapped.</param>
    /// <param name="PenX">Where the pen stood for this glyph.</param>
    /// <param name="DrawX">
    /// Where the cell's left edge goes, being the pen less the glyph's origin. The whole cell is
    /// blitted here, so ink may reach outside the advance in either direction and overlap the
    /// glyphs either side; that overlap is the point, not a bug.
    /// </param>
    public readonly record struct GlyphPlacement(int Codepoint, int CellIndex, int PenX, int DrawX);

    /// <summary>
    /// Lays a string out from a font document's metrics. Shared by the preview and, later, by the
    /// exporters, so what you see and what ships cannot disagree.
    /// </summary>
    public static class FontLayoutOps
    {
        /// <summary>
        /// Places each character of a single line. Unmapped characters are skipped entirely rather
        /// than drawn as a blank, since a font with no glyph for something should say so by absence.
        /// </summary>
        public static List<GlyphPlacement> LayoutLine(CanvasDocument doc, string text, int startPenX = 0)
        {
            var placements = new List<GlyphPlacement>();
            if (string.IsNullOrEmpty(text)) return placements;

            var surf = doc.Surface;
            int pen = startPenX;

            foreach (char c in text)
            {
                if (!doc.FontState.TryGet(c, out var g) || g.CellIndex < 0)
                    continue;

                var (originX, advance) =
                    FontMetricsOps.ResolveMetrics(doc, c, surf.Pixels, surf.Width, surf.Height);

                placements.Add(new GlyphPlacement(c, g.CellIndex, pen, pen - originX));
                pen += advance;
            }

            return placements;
        }

        /// <summary>Total advance of a line, which is not the same as the width of its ink.</summary>
        public static int MeasureLine(CanvasDocument doc, string text)
        {
            int total = 0;
            var surf = doc.Surface;
            foreach (char c in text ?? string.Empty)
            {
                if (!doc.FontState.TryGet(c, out var g) || g.CellIndex < 0) continue;
                total += FontMetricsOps.ResolveMetrics(doc, c, surf.Pixels, surf.Width, surf.Height).Advance;
            }
            return total;
        }

        /// <summary>
        /// Baseline to baseline, being the em plus whatever line gap the font asks for. Measured on
        /// the em rather than the cell, since the overhang room is drawing space and not part of
        /// how tall the type is.
        /// </summary>
        public static int LineAdvance(CanvasDocument doc) =>
            Math.Max(1, FontMetricsOps.EmHeightOf(doc) + Math.Max(0, doc.FontState.LineGap));

        /// <summary>Splits a sample into lines the way a text box hands them over.</summary>
        public static string[] SplitLines(string? text) =>
            (text ?? string.Empty).Split(LineBreaks, StringSplitOptions.None);

        private static readonly string[] LineBreaks = { "\r\n", "\r", "\n" };

        /// <summary>
        /// How much room a block of text needs, in font pixels before any scaling: the widest line's
        /// advance by the number of lines. Width is advance, not ink, so a trailing overhang can
        /// still reach beyond it.
        /// </summary>
        public static SizeInt32 MeasureBlock(CanvasDocument doc, string? text)
        {
            var lines = SplitLines(text);
            int widest = 0;
            foreach (string line in lines)
                widest = Math.Max(widest, MeasureLine(doc, line));

            return new SizeInt32 { Width = widest, Height = lines.Length * LineAdvance(doc) };
        }

        /// <summary>
        /// The bounds the ink actually covers, which can start left of zero and run past the
        /// measured advance when glyphs overhang their neighbours. Null when nothing is drawn.
        /// </summary>
        public static RectInt32? MeasureInkBounds(CanvasDocument doc, string text)
        {
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;

            foreach (var p in LayoutLine(doc, text))
            {
                var ink = FontMetricsOps.MeasureInk(doc, p.CellIndex);
                if (ink is not { } r) continue;

                minX = Math.Min(minX, p.DrawX + r.X);
                maxX = Math.Max(maxX, p.DrawX + r.X + r.Width);
                minY = Math.Min(minY, r.Y);
                maxY = Math.Max(maxY, r.Y + r.Height);
            }

            if (maxX == int.MinValue) return null;
            return new RectInt32 { X = minX, Y = minY, Width = maxX - minX, Height = maxY - minY };
        }
    }
}
