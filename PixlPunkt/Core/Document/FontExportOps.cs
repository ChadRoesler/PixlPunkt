using System;
using System.Globalization;
using System.Text;
using Windows.Graphics;

namespace PixlPunkt.Core.Document
{
    /// <summary>How a font is being written out.</summary>
    /// <param name="Scale">
    /// Output pixels per font pixel. Whole numbers only: the whole point of a pixel font is that a
    /// stem is a stem, and a fractional scale is what turns them lumpy.
    /// </param>
    /// <param name="FaceName">The family name to record in the metrics.</param>
    /// <param name="PageFileName">The image file the metrics should point at.</param>
    public readonly record struct FontExportOptions(int Scale, string FaceName, string PageFileName);

    /// <summary>
    /// Writes a font out as a sprite sheet and a BMFont metrics file, which is what most game
    /// engines and frameworks read.
    /// </summary>
    /// <remarks>
    /// The sheet is the document itself rather than a repacked atlas, and each character's region
    /// is its whole cell. That keeps ink which overhangs the advance, since a glyph is drawn at the
    /// pen less its origin and only the advance moves the pen. Trimming to the ink box would save a
    /// little space and lose exactly the script and accent cases the drawing room exists for.
    /// </remarks>
    public static class FontExportOps
    {
        /// <summary>The size of the exported sheet in pixels.</summary>
        public static SizeInt32 SheetSize(CanvasDocument doc, int scale)
        {
            scale = Math.Max(1, scale);
            return new SizeInt32 { Width = doc.PixelWidth * scale, Height = doc.PixelHeight * scale };
        }

        /// <summary>
        /// The sheet's pixels, magnified by a whole number with no interpolation. Returns the
        /// document's own pixels untouched at a scale of one.
        /// </summary>
        public static byte[] RenderSheet(CanvasDocument doc, int scale)
        {
            scale = Math.Max(1, scale);
            var source = doc.Surface;

            if (scale == 1)
            {
                var copy = new byte[source.Pixels.Length];
                Buffer.BlockCopy(source.Pixels, 0, copy, 0, copy.Length);
                return copy;
            }

            int width = source.Width * scale;
            int height = source.Height * scale;
            var result = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                int sourceRow = (y / scale) * source.Width;
                int destRow = y * width;

                for (int x = 0; x < width; x++)
                    Buffer.BlockCopy(source.Pixels, (sourceRow + x / scale) * 4, result, (destRow + x) * 4, 4);
            }

            return result;
        }

        /// <summary>
        /// The BMFont text format: an info line, a common line, one page, then a line per character.
        /// </summary>
        /// <remarks>
        /// Vertical placement follows the em rather than the cell. A line's top is the top of the
        /// em box, so a cell with drawing room above it gets a negative y offset, which is how the
        /// format expects ink reaching above the line to be described.
        /// </remarks>
        public static string BuildBMFont(CanvasDocument doc, in FontExportOptions options)
        {
            var state = doc.FontState;
            int scale = Math.Max(1, options.Scale);
            var em = FontMetricsOps.EmBox(doc);

            int emHeight = FontMetricsOps.EmHeightOf(doc);
            int lineHeight = FontLayoutOps.LineAdvance(doc);
            int baseline = state.BaselineY - em.Y;
            var sheet = SheetSize(doc, scale);

            var glyphs = FontGlyphOps.Summarize(doc);
            var text = new StringBuilder();

            text.Append("info face=\"").Append(Escape(options.FaceName)).Append('"')
                .Append(" size=").Append(emHeight * scale)
                .Append(" bold=0 italic=0 charset=\"\" unicode=1 stretchH=100")
                // A pixel font is never smoothed or anti-aliased; saying so stops a loader doing it.
                .Append(" smooth=0 aa=1 padding=0,0,0,0 spacing=0,0 outline=0")
                .Append('\n');

            text.Append("common lineHeight=").Append(lineHeight * scale)
                .Append(" base=").Append(baseline * scale)
                .Append(" scaleW=").Append(sheet.Width)
                .Append(" scaleH=").Append(sheet.Height)
                .Append(" pages=1 packed=0 alphaChnl=0 redChnl=0 greenChnl=0 blueChnl=0")
                .Append('\n');

            text.Append("page id=0 file=\"").Append(Escape(options.PageFileName)).Append('"').Append('\n');
            text.Append("chars count=").Append(glyphs.Count).Append('\n');

            foreach (var glyph in glyphs)
            {
                var cell = FontMetricsOps.GetCellRect(doc, glyph.CellIndex);

                text.Append("char id=").Append(glyph.Codepoint.ToString(CultureInfo.InvariantCulture))
                    .Append(" x=").Append(cell.X * scale)
                    .Append(" y=").Append(cell.Y * scale)
                    .Append(" width=").Append(cell.Width * scale)
                    .Append(" height=").Append(cell.Height * scale)
                    // The cell is drawn at the pen less the origin, and its top sits above the
                    // line's top by however much drawing room is above the em.
                    .Append(" xoffset=").Append(-glyph.OriginX * scale)
                    .Append(" yoffset=").Append(-em.Y * scale)
                    .Append(" xadvance=").Append(glyph.Advance * scale)
                    .Append(" page=0 chnl=15")
                    .Append('\n');
            }

            // Stated explicitly: a reader that finds no kerning block may go looking for one.
            text.Append("kernings count=0").Append('\n');

            return text.ToString();
        }

        /// <summary>Keeps a quoted value from breaking the line it sits on.</summary>
        private static string Escape(string? value) =>
            (value ?? string.Empty).Replace("\"", string.Empty).Replace('\n', ' ').Replace('\r', ' ');
    }
}
