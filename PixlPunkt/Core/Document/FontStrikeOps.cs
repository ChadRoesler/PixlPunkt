using System;
using System.Collections.Generic;

namespace PixlPunkt.Core.Document
{
    /// <summary>One character drawn at one size, as a one bit per pixel image.</summary>
    public sealed class GlyphStrikeBitmap
    {
        /// <summary>The character this is a picture of.</summary>
        public int Codepoint { get; init; }

        /// <summary>Width of the image in output pixels; zero when the character is blank.</summary>
        public int Width { get; init; }

        /// <summary>Height of the image in output pixels; zero when the character is blank.</summary>
        public int Height { get; init; }

        /// <summary>Left edge of the image measured from the pen.</summary>
        public int BearingX { get; init; }

        /// <summary>Top edge of the image measured up from the baseline.</summary>
        public int BearingY { get; init; }

        /// <summary>How far the pen moves after drawing it.</summary>
        public int Advance { get; init; }

        /// <summary>Rows of pixels, most significant bit first, each row padded out to a whole byte.</summary>
        public byte[] Rows { get; init; } = Array.Empty<byte>();

        public bool IsEmpty => Width <= 0 || Height <= 0;
    }

    /// <summary>Every character of a font drawn at one particular size.</summary>
    public sealed class FontStrike
    {
        /// <summary>The size this strike is for, in pixels per em.</summary>
        public int PixelsPerEm { get; init; }

        /// <summary>Output pixels per drawn pixel.</summary>
        public int Scale { get; init; }

        /// <summary>Baseline to the top of the em, in output pixels.</summary>
        public int Ascender { get; init; }

        /// <summary>Baseline to the bottom of the em, in output pixels, as a positive number.</summary>
        public int Descender { get; init; }

        /// <summary>The widest advance in the strike.</summary>
        public int MaxAdvance { get; init; }

        /// <summary>The characters, in codepoint order.</summary>
        public List<GlyphStrikeBitmap> Glyphs { get; init; } = new();
    }

    /// <summary>
    /// Draws a font at a fixed size as bitmaps, for embedding in a font file alongside the outlines.
    /// </summary>
    /// <remarks>
    /// A pixel font whose outlines already land on the grid should rasterise cleanly on its own, but
    /// only if whatever is drawing it grid-fits the way it was asked to. A strike removes the doubt:
    /// at that size the reader uses the picture and there is nothing left to get wrong.
    ///
    /// The sizes on offer are whole multiples of the em, for the same reason as everywhere else.
    /// </remarks>
    public static class FontStrikeOps
    {
        /// <summary>
        /// The fields a strike is stored in are single bytes, so a size that would overflow any of
        /// them cannot be built. This is what rules out the very large multiples.
        /// </summary>
        public static bool CanBuild(CanvasDocument doc, int scale)
        {
            if (!doc.FontState.HasState || scale < 1) return false;

            var em = FontMetricsOps.EmBox(doc);
            var state = doc.FontState;

            int ascender = (state.BaselineY - em.Y) * scale;
            int descender = (em.Y + em.Height - state.BaselineY) * scale;
            int cellWidth = doc.TileSize.Width * scale;
            int cellHeight = doc.TileSize.Height * scale;

            return ascender <= 127
                && descender <= 127
                && cellWidth <= 255
                && cellHeight <= 255
                && FontMetricsOps.EmHeightOf(doc) * scale <= 255;
        }

        /// <summary>The multiples of the em that can be built as strikes, smallest first.</summary>
        public static int[] AvailableScales(CanvasDocument doc, int maxScale = 8)
        {
            var scales = new List<int>();
            for (int scale = 1; scale <= maxScale; scale++)
                if (CanBuild(doc, scale)) scales.Add(scale);

            return scales.ToArray();
        }

        /// <summary>Draws every mapped character at the given multiple of the em.</summary>
        public static FontStrike Build(CanvasDocument doc, int scale)
        {
            scale = Math.Max(1, scale);

            var state = doc.FontState;
            var em = FontMetricsOps.EmBox(doc);
            var glyphs = new List<GlyphStrikeBitmap>();
            int maxAdvance = 0;

            foreach (var summary in FontGlyphOps.Summarize(doc))
            {
                var bitmap = BuildGlyph(doc, summary, scale);
                glyphs.Add(bitmap);
                maxAdvance = Math.Max(maxAdvance, bitmap.Advance);
            }

            return new FontStrike
            {
                PixelsPerEm = FontMetricsOps.EmHeightOf(doc) * scale,
                Scale = scale,
                Ascender = (state.BaselineY - em.Y) * scale,
                Descender = (em.Y + em.Height - state.BaselineY) * scale,
                MaxAdvance = maxAdvance,
                Glyphs = glyphs,
            };
        }

        /// <summary>
        /// Draws one character. The image covers the ink and nothing else, because a strike that
        /// stored whole cells would carry the overhang room of every blank margin with it.
        /// </summary>
        private static GlyphStrikeBitmap BuildGlyph(CanvasDocument doc, GlyphSummary summary, int scale)
        {
            var ink = FontMetricsOps.MeasureInk(doc, summary.CellIndex);

            if (ink is not { } box)
            {
                return new GlyphStrikeBitmap
                {
                    Codepoint = summary.Codepoint,
                    Advance = summary.Advance * scale,
                };
            }

            var cell = FontMetricsOps.GetCellRect(doc, summary.CellIndex);
            var surface = doc.Surface;

            int width = box.Width * scale;
            int height = box.Height * scale;
            int stride = (width + 7) / 8;
            var rows = new byte[stride * height];

            for (int y = 0; y < height; y++)
            {
                int sourceY = cell.Y + box.Y + (y / scale);

                for (int x = 0; x < width; x++)
                {
                    int sourceX = cell.X + box.X + (x / scale);
                    if (sourceX < 0 || sourceX >= surface.Width || sourceY < 0 || sourceY >= surface.Height)
                        continue;

                    if (surface.Pixels[((sourceY * surface.Width) + sourceX) * 4 + 3] == 0) continue;

                    rows[(y * stride) + (x / 8)] |= (byte)(0x80 >> (x % 8));
                }
            }

            return new GlyphStrikeBitmap
            {
                Codepoint = summary.Codepoint,
                Width = width,
                Height = height,
                BearingX = (box.X - summary.OriginX) * scale,
                BearingY = (doc.FontState.BaselineY - box.Y) * scale,
                Advance = summary.Advance * scale,
                Rows = rows,
            };
        }
    }
}
