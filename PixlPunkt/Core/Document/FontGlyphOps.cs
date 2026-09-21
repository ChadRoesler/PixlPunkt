using System;
using System.Collections.Generic;
using System.Globalization;

namespace PixlPunkt.Core.Document
{
    /// <summary>One row of the glyphs panel: everything it shows without touching pixels again.</summary>
    /// <param name="Codepoint">The character this cell stands for.</param>
    /// <param name="CellIndex">Its cell in the glyph sheet.</param>
    /// <param name="HasInk">False for a character that is mapped but never drawn.</param>
    /// <param name="AutoFit">Whether spacing is derived from the ink or pinned by hand.</param>
    /// <param name="OriginX">Resolved origin, whichever way it was arrived at.</param>
    /// <param name="Advance">Resolved advance, whichever way it was arrived at.</param>
    public readonly record struct GlyphSummary(
        int Codepoint,
        int CellIndex,
        bool HasInk,
        bool AutoFit,
        int OriginX,
        int Advance);

    /// <summary>
    /// Queries over a font's mapped characters, for the glyphs panel and the character strip.
    /// Kept apart from <see cref="FontMetricsOps"/> because nothing here decides spacing; it only
    /// reports what spacing came out as.
    /// </summary>
    public static class FontGlyphOps
    {
        /// <summary>
        /// Every mapped character in codepoint order, which is the order a person reads a character
        /// set in. Reads the surface once for the whole sheet rather than per glyph.
        /// </summary>
        public static List<GlyphSummary> Summarize(CanvasDocument doc)
        {
            var result = new List<GlyphSummary>();
            if (!doc.FontState.HasState) return result;

            var surf = doc.Surface;
            var codepoints = new List<int>(doc.FontState.Glyphs.Keys);
            codepoints.Sort();

            foreach (int cp in codepoints)
            {
                if (!doc.FontState.TryGet(cp, out var g) || g.CellIndex < 0) continue;

                var cell = FontMetricsOps.GetCellRect(doc, g.CellIndex);
                bool hasInk = FontMetricsOps.MeasureInk(surf.Pixels, surf.Width, surf.Height, cell) is not null;
                var (originX, advance) =
                    FontMetricsOps.ResolveMetrics(doc, cp, surf.Pixels, surf.Width, surf.Height);

                result.Add(new GlyphSummary(cp, g.CellIndex, hasInk, g.AutoFit, originX, advance));
            }

            return result;
        }

        /// <summary>Characters that are mapped but still blank, which is the panel's progress count.</summary>
        public static int UndrawnCount(IReadOnlyList<GlyphSummary> glyphs)
        {
            int n = 0;
            for (int i = 0; i < glyphs.Count; i++)
                if (!glyphs[i].HasInk) n++;
            return n;
        }

        /// <summary>Position of a character in the list, or -1.</summary>
        public static int IndexOf(IReadOnlyList<GlyphSummary> glyphs, int codepoint)
        {
            for (int i = 0; i < glyphs.Count; i++)
                if (glyphs[i].Codepoint == codepoint) return i;
            return -1;
        }

        /// <summary>
        /// Resolves what someone typed into the jump box to a codepoint. A single character means
        /// itself, which covers nearly every case; "U+1FAE", "0x1FAE" and a bare "1FAE" mean that
        /// codepoint, which is the only way to reach a character the keyboard will not produce.
        /// Returns -1 when the text means nothing.
        /// </summary>
        public static int ParseJumpTarget(string? typed)
        {
            if (string.IsNullOrWhiteSpace(typed)) return -1;
            string t = typed.Trim();

            // A lone character is itself, even when it looks like a digit: typing "8" in a font
            // editor means the glyph eight far more often than it means a control character.
            if (t.Length == 1) return t[0];

            string hex = t;
            if (hex.StartsWith("U+", StringComparison.OrdinalIgnoreCase) ||
                hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex[2..];
            else if (hex.Length is < 2 or > 6)
                return t[0];   // ordinary typing, not a code: use the first character.

            if (hex.Length is > 0 and <= 6 &&
                int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp) &&
                cp is > 0 and <= 0x10FFFF)
                return cp;

            return t[0];
        }

        /// <summary>
        /// The glyph to select for typed text: an exact match when there is one, otherwise the next
        /// character in order, so typing into a partial font still lands somewhere sensible.
        /// Returns -1 when the font has no glyphs at or after it.
        /// </summary>
        public static int FindJumpIndex(IReadOnlyList<GlyphSummary> glyphs, string? typed)
        {
            int cp = ParseJumpTarget(typed);
            if (cp < 0 || glyphs.Count == 0) return -1;

            int exact = IndexOf(glyphs, cp);
            if (exact >= 0) return exact;

            for (int i = 0; i < glyphs.Count; i++)
                if (glyphs[i].Codepoint > cp) return i;

            return -1;
        }

        /// <summary>
        /// The characters either side of one in the font's own order. Returns -1 on a side where
        /// there is nothing, which is the case at each end of the set.
        /// </summary>
        public static (int Left, int Right) NeighboursOf(IReadOnlyList<GlyphSummary> glyphs, int codepoint)
        {
            int index = IndexOf(glyphs, codepoint);
            if (index < 0) return (-1, -1);

            int left = index > 0 ? glyphs[index - 1].Codepoint : -1;
            int right = index < glyphs.Count - 1 ? glyphs[index + 1].Codepoint : -1;
            return (left, right);
        }

        /// <summary>
        /// The short sample used to judge a glyph's spacing: the glyph with one character on each
        /// side. Spacing only means anything in company, so a glyph is never shown alone.
        /// </summary>
        /// <param name="glyphs">The font's characters, in order.</param>
        /// <param name="codepoint">The glyph being worked on.</param>
        /// <param name="pinnedLeft">
        /// A character to hold on the left whatever glyph is selected, or null to follow the set.
        /// Pinning is how you check one letter against a fixed reference such as n or o.
        /// </param>
        /// <param name="pinnedRight">The same on the right.</param>
        public static string SampleAround(
            IReadOnlyList<GlyphSummary> glyphs,
            int codepoint,
            int? pinnedLeft = null,
            int? pinnedRight = null)
        {
            if (IndexOf(glyphs, codepoint) < 0) return string.Empty;

            var (autoLeft, autoRight) = NeighboursOf(glyphs, codepoint);
            int left = pinnedLeft ?? autoLeft;
            int right = pinnedRight ?? autoRight;

            var sb = new System.Text.StringBuilder(3);
            if (left > 0 && IndexOf(glyphs, left) >= 0) sb.Append(char.ConvertFromUtf32(left));
            sb.Append(char.ConvertFromUtf32(codepoint));
            if (right > 0 && IndexOf(glyphs, right) >= 0) sb.Append(char.ConvertFromUtf32(right));
            return sb.ToString();
        }

        /// <summary>
        /// How a character is labelled in the panel. Space and the control characters have nothing
        /// to show, so they get their name instead of a blank that looks like a bug.
        /// </summary>
        public static string LabelFor(int codepoint) => codepoint switch
        {
            ' ' => "SP",
            '\t' => "TAB",
            _ when codepoint < 0x20 || codepoint == 0x7F => $"U+{codepoint:X2}",
            _ when codepoint > 0xFFFF => char.ConvertFromUtf32(codepoint),
            _ => ((char)codepoint).ToString(),
        };
    }
}
