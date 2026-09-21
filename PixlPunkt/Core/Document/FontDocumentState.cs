using System;
using System.Collections.Generic;

namespace PixlPunkt.Core.Document
{
    /// <summary>
    /// What one glyph needs beyond its pixels: which cell of the sheet it lives in, and how it
    /// spaces against its neighbours. All values are in design pixels.
    /// </summary>
    public sealed class GlyphMetrics
    {
        /// <summary>Cell in the glyph sheet, row-major; -1 when the character has no cell yet.</summary>
        public int CellIndex { get; set; } = -1;

        /// <summary>
        /// The column inside the cell that the glyph's origin sits on. When text is laid out the
        /// pen lands here, so the cell is drawn at <c>pen - OriginX</c>. Anchored to the cell
        /// rather than to the ink, so painting another pixel never moves spacing you already set.
        /// Pushing it right of the ink tucks the glyph under whatever precedes it, which is how a
        /// bitmap font fakes kerning.
        /// </summary>
        public int OriginX { get; set; }

        /// <summary>Columns from this glyph's origin to the next glyph's origin.</summary>
        public int Advance { get; set; }

        /// <summary>
        /// Derive <see cref="OriginX"/> and <see cref="Advance"/> from the ink and the font's
        /// side bearing rather than the stored values. Cleared as soon as a spacing line is dragged.
        /// </summary>
        public bool AutoFit { get; set; } = true;

        public GlyphMetrics Clone() => new()
        {
            CellIndex = CellIndex,
            OriginX = OriginX,
            Advance = Advance,
            AutoFit = AutoFit,
        };
    }

    /// <summary>
    /// Turns a canvas into a pixel font. The document's tile size is the em box and its tile cells
    /// are glyph cells, so drawing, layers, selection and undo need no special cases; this holds
    /// only what the pixels cannot say. Optional in the same way the voxel state is: an ordinary
    /// document simply leaves <see cref="HasState"/> false.
    /// </summary>
    /// <remarks>
    /// The em box is the tile size, which is what decides the sizes this font renders cleanly at:
    /// an eight pixel em is even at eight, sixteen and twenty four, and lumpy everywhere else.
    /// </remarks>
    public sealed class FontDocumentState
    {
        private readonly Dictionary<int, GlyphMetrics> _glyphs = new();

        /// <summary>False for an ordinary document; true once it is a font.</summary>
        public bool HasState { get; set; }

        public string FamilyName { get; set; } = string.Empty;
        public string StyleName { get; set; } = "Regular";

        /// <summary>
        /// Row inside the em cell that glyphs sit on; rows below it are descender space. Uniform
        /// across every glyph, which is what keeps a pixel font from wobbling.
        /// </summary>
        public int BaselineY { get; set; }

        /// <summary>Row inside the em cell marking cap height. A drawing guide, also uniform.</summary>
        public int ToplineY { get; set; }

        /// <summary>Every glyph advances by the em width whatever its ink.</summary>
        public bool Monospace { get; set; }

        /// <summary>Blank columns auto-fit leaves either side of the ink.</summary>
        public int SideBearing { get; set; } = 1;

        /// <summary>Extra rows between baselines, on top of the em height.</summary>
        public int LineGap { get; set; }

        // ── the em box ──────────────────────────────────────────────────
        // The cell is how much room there is to draw in; the em is the design size the finished
        // font is measured by. They are usually the same, but a cell larger than the em gives a
        // glyph somewhere to put ink that reaches past its own body, which is what a script face
        // or a heavily accented character needs. Zero on either size means "the whole cell", so a
        // font that never overhangs carries no extra concepts.

        /// <summary>Column of the cell the em box starts at. Columns left of it are overhang room.</summary>
        public int EmLeft { get; set; }

        /// <summary>Row of the cell the em box starts at. Rows above it are overhang room.</summary>
        public int EmTop { get; set; }

        /// <summary>Width of the em box, or zero for the whole cell.</summary>
        public int EmWidth { get; set; }

        /// <summary>
        /// Height of the em box, or zero for the whole cell. This, not the cell height, is the
        /// design size: the font renders evenly at multiples of it.
        /// </summary>
        public int EmHeight { get; set; }

        public int ResolveEmWidth(int cellWidth) => EmWidth > 0 ? EmWidth : Math.Max(1, cellWidth);

        public int ResolveEmHeight(int cellHeight) => EmHeight > 0 ? EmHeight : Math.Max(1, cellHeight);

        /// <summary>Baseline to the top of the em box, as a font file would record it.</summary>
        public int Ascent => BaselineY - EmTop;

        /// <summary>Baseline to the bottom of the em box, as a font file would record it.</summary>
        public int Descent(int cellHeight) => EmTop + ResolveEmHeight(cellHeight) - BaselineY;

        /// <summary>Gives the em box a size and centres it in a cell, leaving equal overhang room.</summary>
        public void SetEmBox(int emWidth, int emHeight, int cellWidth, int cellHeight)
        {
            EmWidth = Math.Clamp(emWidth, 1, Math.Max(1, cellWidth));
            EmHeight = Math.Clamp(emHeight, 1, Math.Max(1, cellHeight));
            EmLeft = Math.Max(0, (cellWidth - EmWidth) / 2);
            EmTop = Math.Max(0, (cellHeight - EmHeight) / 2);
        }

        public IReadOnlyDictionary<int, GlyphMetrics> Glyphs => _glyphs;

        public GlyphMetrics GetOrAdd(int codepoint)
        {
            if (!_glyphs.TryGetValue(codepoint, out var g))
            {
                g = new GlyphMetrics();
                _glyphs[codepoint] = g;
            }
            return g;
        }

        public bool TryGet(int codepoint, out GlyphMetrics glyph) => _glyphs.TryGetValue(codepoint, out glyph!);

        public bool Remove(int codepoint) => _glyphs.Remove(codepoint);

        public void Clear() => _glyphs.Clear();

        /// <summary>The character assigned to a cell, or -1 when the cell is unassigned.</summary>
        public int CodepointAtCell(int cellIndex)
        {
            foreach (var (cp, g) in _glyphs)
                if (g.CellIndex == cellIndex) return cp;
            return -1;
        }

        /// <summary>
        /// Places the baseline and cap-height guides for a fresh font: a quarter of the em left
        /// below the baseline for descenders, cap height at the top of the cell.
        /// </summary>
        public void SetDefaultGuides(int cellHeight)
        {
            int em = ResolveEmHeight(cellHeight);
            BaselineY = EmTop + em - Math.Max(1, em / 4);
            ToplineY = EmTop;
        }

        /// <summary>
        /// Moves both guides, clamped so the baseline stays inside the cell and the cap-height
        /// line stays above it. Returns whether anything actually moved.
        /// </summary>
        public bool SetGuides(int toplineY, int baselineY, int cellHeight)
        {
            cellHeight = Math.Max(1, cellHeight);
            baselineY = Math.Clamp(baselineY, 1, cellHeight);
            toplineY = Math.Clamp(toplineY, 0, baselineY - 1);

            if (BaselineY == baselineY && ToplineY == toplineY) return false;
            BaselineY = baselineY;
            ToplineY = toplineY;
            return true;
        }

        /// <summary>Assigns a codepoint range to consecutive cells. Returns the next free cell.</summary>
        public int MapRange(int firstCell, int firstCodepoint, int lastCodepoint)
        {
            int cell = Math.Max(0, firstCell);
            for (int cp = firstCodepoint; cp <= lastCodepoint; cp++)
                GetOrAdd(cp).CellIndex = cell++;
            return cell;
        }

        /// <summary>Assigns space through tilde, the printable ASCII range, to consecutive cells.</summary>
        public int MapPrintableAscii(int firstCell = 0) => MapRange(firstCell, ' ', '~');
    }
}
