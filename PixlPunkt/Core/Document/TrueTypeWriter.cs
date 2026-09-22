using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PixlPunkt.Core.Document
{
    /// <summary>What goes into a TrueType file beyond the glyphs themselves.</summary>
    /// <param name="FamilyName">The family name, as shown in a font menu.</param>
    /// <param name="StyleName">The style, normally "Regular".</param>
    /// <param name="UnitsPerEmTarget">The units per em to aim for; the real value is a multiple of the pixel grid.</param>
    /// <param name="StrikeScales">
    /// Multiples of the em to embed as bitmaps, or null for none. At those sizes a reader uses the
    /// picture rather than rasterising the outline, so there is nothing left to get wrong.
    /// </param>
    public readonly record struct TrueTypeOptions(
        string FamilyName,
        string StyleName,
        int UnitsPerEmTarget = 1024,
        IReadOnlyList<int>? StrikeScales = null);

    /// <summary>
    /// Writes a pixel font out as a TrueType file.
    /// </summary>
    /// <remarks>
    /// Two things here are what make the result crisp rather than merely correct. The units per em
    /// is an exact multiple of the pixel grid, so a pixel is a whole number of font units and never
    /// lands between them. And a gasp table asks for grid fitting without grey, which stops a
    /// rasteriser anti-aliasing edges that were drawn as hard pixels.
    ///
    /// Outlines are straight lines along pixel boundaries with every point on curve. No curve
    /// fitting is attempted and none should be.
    /// </remarks>
    public static class TrueTypeWriter
    {
        private const uint MagicNumber = 0x5F0F3CF5;
        private const uint ChecksumMagic = 0xB1B0AFBA;

        /// <summary>One glyph ready to be written: its outline and the advance that goes with it.</summary>
        private sealed class Glyph
        {
            public int Codepoint;
            public List<List<OutlinePoint>> Contours = new();
            public int Advance;
            public int XMin, YMin, XMax, YMax;
            public bool IsEmpty => Contours.Count == 0;
        }

        /// <summary>Builds the whole font file.</summary>
        public static byte[] Build(CanvasDocument doc, in TrueTypeOptions options)
        {
            var state = doc.FontState;
            int emHeight = FontMetricsOps.EmHeightOf(doc);
            int unitsPerPixel = FontOutlineOps.ChooseUnitsPerPixel(emHeight, options.UnitsPerEmTarget);
            int unitsPerEm = emHeight * unitsPerPixel;

            var glyphs = CollectGlyphs(doc, unitsPerPixel);

            // Glyph zero is always .notdef, and it is left empty rather than drawn as a box: a
            // missing character showing as blank is less alarming than one showing as a rectangle.
            glyphs.Insert(0, new Glyph { Codepoint = -1, Advance = MaxAdvance(glyphs) });

            var em = FontMetricsOps.EmBox(doc);
            int ascender = (state.BaselineY - em.Y) * unitsPerPixel;
            int descender = (em.Y + em.Height - state.BaselineY) * unitsPerPixel;

            var tables = new Dictionary<string, byte[]>
            {
                ["glyf"] = BuildGlyf(glyphs, out var glyphOffsets),
                ["cmap"] = BuildCmap(glyphs),
                ["hmtx"] = BuildHmtx(glyphs),
                ["name"] = BuildName(options, unitsPerEm),
                ["post"] = BuildPost(),
                ["gasp"] = BuildGasp(),
            };

            tables["loca"] = BuildLoca(glyphOffsets);
            tables["maxp"] = BuildMaxp(glyphs);
            tables["hhea"] = BuildHhea(glyphs, ascender, descender, state.LineGap * unitsPerPixel);
            tables["OS/2"] = BuildOs2(glyphs, ascender, descender, state.LineGap * unitsPerPixel, unitsPerEm);
            tables["head"] = BuildHead(glyphs, unitsPerEm);

            AddStrikes(doc, options, glyphs.Count, tables);

            return Assemble(tables);
        }

        /// <summary>
        /// Builds the embedded bitmap tables, if any sizes were asked for.
        /// </summary>
        /// <remarks>
        /// The pair is EBLC, which says where every picture is and how it sits against the
        /// baseline, and EBDT, which holds the pictures. They are written together or not at all:
        /// one without the other describes a font that cannot be drawn.
        /// </remarks>
        private static void AddStrikes(
            CanvasDocument doc, in TrueTypeOptions options, int glyphCount, Dictionary<string, byte[]> tables)
        {
            if (options.StrikeScales is not { Count: > 0 } scales) return;

            var strikes = new List<FontStrike>();
            foreach (int scale in scales)
            {
                if (!FontStrikeOps.CanBuild(doc, scale)) continue;
                strikes.Add(FontStrikeOps.Build(doc, scale));
            }

            if (strikes.Count == 0) return;

            var (location, data) = BuildEmbeddedBitmaps(strikes, glyphCount);
            tables["EBLC"] = location;
            tables["EBDT"] = data;
        }

        /// <summary>Traces every mapped character, in codepoint order, which becomes glyph order.</summary>
        private static List<Glyph> CollectGlyphs(CanvasDocument doc, int unitsPerPixel)
        {
            var result = new List<Glyph>();

            foreach (var summary in FontGlyphOps.Summarize(doc))
            {
                var glyph = new Glyph
                {
                    Codepoint = summary.Codepoint,
                    Contours = FontOutlineOps.TraceGlyph(doc, summary.Codepoint, unitsPerPixel),
                    Advance = summary.Advance * unitsPerPixel,
                };

                MeasureBounds(glyph);
                result.Add(glyph);
            }

            return result;
        }

        private static void MeasureBounds(Glyph glyph)
        {
            if (glyph.IsEmpty) return;

            int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;

            foreach (var contour in glyph.Contours)
            {
                foreach (var point in contour)
                {
                    xMin = Math.Min(xMin, point.X);
                    yMin = Math.Min(yMin, point.Y);
                    xMax = Math.Max(xMax, point.X);
                    yMax = Math.Max(yMax, point.Y);
                }
            }

            glyph.XMin = xMin;
            glyph.YMin = yMin;
            glyph.XMax = xMax;
            glyph.YMax = yMax;
        }

        private static int MaxAdvance(List<Glyph> glyphs)
        {
            int max = 0;
            foreach (var glyph in glyphs) max = Math.Max(max, glyph.Advance);
            return max;
        }

        // ── glyf and loca ───────────────────────────────────────────────

        private static byte[] BuildGlyf(List<Glyph> glyphs, out List<uint> offsets)
        {
            offsets = new List<uint>(glyphs.Count + 1);
            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            foreach (var glyph in glyphs)
            {
                offsets.Add((uint)stream.Length);
                if (glyph.IsEmpty) continue;   // an empty glyph occupies no bytes at all

                writer.Int16((short)glyph.Contours.Count);
                writer.Int16((short)glyph.XMin);
                writer.Int16((short)glyph.YMin);
                writer.Int16((short)glyph.XMax);
                writer.Int16((short)glyph.YMax);

                int pointIndex = 0;
                foreach (var contour in glyph.Contours)
                {
                    pointIndex += contour.Count;
                    writer.UInt16((ushort)(pointIndex - 1));
                }

                writer.UInt16(0);   // no hinting instructions

                // Every point is on curve, so every flag is the same and none of the short or
                // repeat encodings are used. Straight lines are the whole point of a pixel font.
                foreach (var contour in glyph.Contours)
                    for (int i = 0; i < contour.Count; i++)
                        writer.Byte(0x01);

                WriteCoordinates(writer, glyph, horizontal: true);
                WriteCoordinates(writer, glyph, horizontal: false);

                while (stream.Length % 4 != 0) writer.Byte(0);
            }

            offsets.Add((uint)stream.Length);
            return stream.ToArray();
        }

        /// <summary>Coordinates are stored as differences from the previous point, not absolutes.</summary>
        private static void WriteCoordinates(BigEndianWriter writer, Glyph glyph, bool horizontal)
        {
            int previous = 0;

            foreach (var contour in glyph.Contours)
            {
                foreach (var point in contour)
                {
                    int value = horizontal ? point.X : point.Y;
                    writer.Int16((short)(value - previous));
                    previous = value;
                }
            }
        }

        private static byte[] BuildLoca(List<uint> offsets)
        {
            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            // The long form throughout: it costs a few bytes and removes the even-offset rule that
            // the short form imposes, along with the class of bug that comes with forgetting it.
            foreach (uint offset in offsets) writer.UInt32(offset);

            return stream.ToArray();
        }

        // ── cmap ────────────────────────────────────────────────────────

        private static byte[] BuildCmap(List<Glyph> glyphs)
        {
            byte[] subtable = BuildCmapFormat4(glyphs);

            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            // One subtable, pointed at by both the Windows and the Unicode records, because some
            // readers look only for one of the two.
            writer.UInt16(0);
            writer.UInt16(2);

            uint subtableOffset = 4 + (8 * 2);
            writer.UInt16(3); writer.UInt16(1); writer.UInt32(subtableOffset);
            writer.UInt16(0); writer.UInt16(3); writer.UInt32(subtableOffset);

            writer.Bytes(subtable);
            return stream.ToArray();
        }

        /// <summary>Segments of consecutive characters that also have consecutive glyph numbers.</summary>
        private static byte[] BuildCmapFormat4(List<Glyph> glyphs)
        {
            var segments = new List<(int Start, int End, int Delta)>();

            for (int i = 1; i < glyphs.Count; i++)
            {
                int codepoint = glyphs[i].Codepoint;
                if (codepoint is < 0 or > 0xFFFF) continue;   // format 4 covers the basic plane only

                int delta = i - codepoint;

                if (segments.Count > 0)
                {
                    var last = segments[^1];
                    if (last.End + 1 == codepoint && last.Delta == delta)
                    {
                        segments[^1] = (last.Start, codepoint, delta);
                        continue;
                    }
                }

                segments.Add((codepoint, codepoint, delta));
            }

            // The format requires a final segment ending at 0xFFFF.
            segments.Add((0xFFFF, 0xFFFF, 1));

            int segCount = segments.Count;
            int length = 16 + (segCount * 8);

            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            writer.UInt16(4);
            writer.UInt16((ushort)length);
            writer.UInt16(0);   // language

            writer.UInt16((ushort)(segCount * 2));
            int entrySelector = (int)Math.Floor(Math.Log2(segCount));
            int searchRange = 2 * (1 << entrySelector);
            writer.UInt16((ushort)searchRange);
            writer.UInt16((ushort)entrySelector);
            writer.UInt16((ushort)((segCount * 2) - searchRange));

            foreach (var segment in segments) writer.UInt16((ushort)segment.End);
            writer.UInt16(0);   // reserved pad
            foreach (var segment in segments) writer.UInt16((ushort)segment.Start);
            foreach (var segment in segments) writer.Int16((short)segment.Delta);
            foreach (var _ in segments) writer.UInt16(0);   // no idRangeOffset: deltas carry it all

            return stream.ToArray();
        }

        // ── the metric and description tables ───────────────────────────

        private static byte[] BuildHmtx(List<Glyph> glyphs)
        {
            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            foreach (var glyph in glyphs)
            {
                writer.UInt16((ushort)Math.Max(0, glyph.Advance));
                writer.Int16((short)(glyph.IsEmpty ? 0 : glyph.XMin));
            }

            return stream.ToArray();
        }

        private static byte[] BuildHead(List<Glyph> glyphs, int unitsPerEm)
        {
            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            writer.UInt32(0x00010000);        // version
            writer.UInt32(0x00010000);        // font revision
            writer.UInt32(0);                 // checksum adjustment, filled in once the file exists
            writer.UInt32(MagicNumber);
            writer.UInt16(0x000B);            // baseline at y=0, left sidebearing at x=0
            writer.UInt16((ushort)unitsPerEm);
            long now = LongDateTime();
            writer.Int64(now);                // created
            writer.Int64(now);                // modified

            writer.Int16((short)Min(glyphs, g => g.XMin));
            writer.Int16((short)Min(glyphs, g => g.YMin));
            writer.Int16((short)Max(glyphs, g => g.XMax));
            writer.Int16((short)Max(glyphs, g => g.YMax));

            writer.UInt16(0);                 // macStyle
            writer.UInt16(1);                 // lowestRecPPEM
            writer.Int16(2);                  // fontDirectionHint
            writer.Int16(1);                  // loca is in the long form
            writer.Int16(0);                  // glyphDataFormat

            return stream.ToArray();
        }

        /// <summary>
        /// The current time in the form a font file wants it: seconds since the start of 1904.
        /// Left at zero, readers report the font as damaged and guess at a unix timestamp instead.
        /// </summary>
        private static long LongDateTime()
        {
            const long SecondsFrom1904To1970 = 2082844800;
            return SecondsFrom1904To1970 + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static byte[] BuildHhea(List<Glyph> glyphs, int ascender, int descender, int lineGap)
        {
            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            writer.UInt32(0x00010000);
            writer.Int16((short)ascender);
            writer.Int16((short)-descender);   // stored as a negative number
            writer.Int16((short)lineGap);
            writer.UInt16((ushort)MaxAdvance(glyphs));
            writer.Int16((short)Min(glyphs, g => g.XMin));
            writer.Int16((short)Min(glyphs, g => g.Advance - g.XMax));
            writer.Int16((short)Max(glyphs, g => g.XMax));
            writer.Int16(1);                   // caret slope rise
            writer.Int16(0);                   // caret slope run
            writer.Int16(0);                   // caret offset
            for (int i = 0; i < 4; i++) writer.Int16(0);
            writer.Int16(0);                   // metric data format
            writer.UInt16((ushort)glyphs.Count);

            return stream.ToArray();
        }

        private static byte[] BuildMaxp(List<Glyph> glyphs)
        {
            int maxPoints = 0, maxContours = 0;
            foreach (var glyph in glyphs)
            {
                int points = 0;
                foreach (var contour in glyph.Contours) points += contour.Count;
                maxPoints = Math.Max(maxPoints, points);
                maxContours = Math.Max(maxContours, glyph.Contours.Count);
            }

            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            writer.UInt32(0x00010000);
            writer.UInt16((ushort)glyphs.Count);
            writer.UInt16((ushort)maxPoints);
            writer.UInt16((ushort)maxContours);
            writer.UInt16(0);                  // max composite points
            writer.UInt16(0);                  // max composite contours
            writer.UInt16(2);                  // max zones
            writer.UInt16(0);                  // twilight points
            writer.UInt16(0);                  // storage
            writer.UInt16(0);                  // function definitions
            writer.UInt16(0);                  // instruction definitions
            writer.UInt16(0);                  // stack elements
            writer.UInt16(0);                  // size of instructions
            writer.UInt16(0);                  // component elements
            writer.UInt16(0);                  // component depth

            return stream.ToArray();
        }

        private static byte[] BuildOs2(List<Glyph> glyphs, int ascender, int descender, int lineGap, int unitsPerEm)
        {
            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            writer.UInt16(4);                              // version
            writer.Int16((short)AverageAdvance(glyphs));
            writer.UInt16(400);                            // weight: regular
            writer.UInt16(5);                              // width: medium
            writer.UInt16(0);                              // embedding: installable
            writer.Int16((short)(unitsPerEm / 8));         // subscript and superscript metrics
            writer.Int16((short)(unitsPerEm / 8));
            writer.Int16(0);
            writer.Int16((short)(unitsPerEm / 8));
            writer.Int16((short)(unitsPerEm / 8));
            writer.Int16((short)(unitsPerEm / 8));
            writer.Int16(0);
            writer.Int16((short)(unitsPerEm / 4));
            writer.Int16((short)(unitsPerEm / 16));        // strikeout size
            writer.Int16((short)(unitsPerEm / 4));         // strikeout position
            writer.Int16(0);                               // family class

            for (int i = 0; i < 10; i++) writer.Byte(0);   // panose

            // Unicode and codepage coverage: the Latin bit only, which is what these fonts hold.
            writer.UInt32(1); writer.UInt32(0); writer.UInt32(0); writer.UInt32(0);
            writer.Bytes(Encoding.ASCII.GetBytes("PXPT"));  // vendor
            writer.UInt16(0);                              // selection: regular
            writer.UInt16((ushort)FirstCodepoint(glyphs));
            writer.UInt16((ushort)LastCodepoint(glyphs));
            writer.Int16((short)ascender);                 // typographic ascender
            writer.Int16((short)-descender);
            writer.Int16((short)lineGap);
            writer.UInt16((ushort)ascender);               // Windows ascent and descent
            writer.UInt16((ushort)descender);
            writer.UInt32(1); writer.UInt32(0);            // codepage range: Latin 1
            writer.Int16((short)ascender);                 // x height and cap height, approximated
            writer.Int16((short)ascender);
            writer.UInt16(0);                              // default char
            writer.UInt16(32);                             // break char is the space
            writer.UInt16(2);                              // max context

            return stream.ToArray();
        }

        private static byte[] BuildPost()
        {
            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            writer.UInt32(0x00030000);   // version 3: no glyph names carried
            writer.UInt32(0);            // italic angle
            writer.Int16(0);             // underline position
            writer.Int16(0);             // underline thickness
            writer.UInt32(0);            // not fixed pitch
            for (int i = 0; i < 4; i++) writer.UInt32(0);

            return stream.ToArray();
        }

        /// <summary>
        /// Asks for grid fitting without grey, at every size. This is the table that stops a
        /// rasteriser softening edges that were drawn as hard pixels.
        /// </summary>
        private static byte[] BuildGasp()
        {
            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            writer.UInt16(0);        // version
            writer.UInt16(1);        // one range
            writer.UInt16(0xFFFF);   // covering every size
            writer.UInt16(0x0001);   // grid fit, and no grey

            return stream.ToArray();
        }

        private static byte[] BuildName(in TrueTypeOptions options, int unitsPerEm)
        {
            string family = Clean(options.FamilyName, "PixlPunkt Font");
            string style = Clean(options.StyleName, "Regular");
            string full = $"{family} {style}";
            string postScript = full.Replace(" ", string.Empty);

            var entries = new (ushort Id, string Value)[]
            {
                (0, $"Made in PixlPunkt at {unitsPerEm} units per em"),
                (1, family),
                (2, style),
                (3, $"{family}-{style}"),
                (4, full),
                (5, "Version 1.0"),
                (6, postScript),
            };

            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            // Each string is recorded twice, once for readers that want Macintosh ASCII and once
            // for those that want Windows UTF-16.
            int recordCount = entries.Length * 2;
            writer.UInt16(0);
            writer.UInt16((ushort)recordCount);
            writer.UInt16((ushort)(6 + (recordCount * 12)));

            using var strings = new MemoryStream();

            foreach (var (id, value) in entries)
            {
                var mac = Encoding.ASCII.GetBytes(value);
                writer.UInt16(1); writer.UInt16(0); writer.UInt16(0); writer.UInt16(id);
                writer.UInt16((ushort)mac.Length); writer.UInt16((ushort)strings.Length);
                strings.Write(mac, 0, mac.Length);
            }

            foreach (var (id, value) in entries)
            {
                var windows = Encoding.BigEndianUnicode.GetBytes(value);
                writer.UInt16(3); writer.UInt16(1); writer.UInt16(0x0409); writer.UInt16(id);
                writer.UInt16((ushort)windows.Length); writer.UInt16((ushort)strings.Length);
                strings.Write(windows, 0, windows.Length);
            }

            writer.Bytes(strings.ToArray());
            return stream.ToArray();
        }

        private static string Clean(string? value, string fallback)
        {
            value = value?.Trim();
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        // ── putting the file together ───────────────────────────────────

        private static byte[] Assemble(Dictionary<string, byte[]> tables)
        {
            var tags = new List<string>(tables.Keys);
            tags.Sort(StringComparer.Ordinal);   // the directory must be in tag order

            int count = tags.Count;
            int entrySelector = (int)Math.Floor(Math.Log2(count));
            int searchRange = 16 * (1 << entrySelector);

            using var stream = new MemoryStream();
            var writer = new BigEndianWriter(stream);

            writer.UInt32(0x00010000);
            writer.UInt16((ushort)count);
            writer.UInt16((ushort)searchRange);
            writer.UInt16((ushort)entrySelector);
            writer.UInt16((ushort)((count * 16) - searchRange));

            int offset = 12 + (count * 16);
            var placed = new List<(string Tag, int Offset, int Length)>();

            foreach (string tag in tags)
            {
                var data = tables[tag];
                placed.Add((tag, offset, data.Length));
                offset += (data.Length + 3) & ~3;   // every table starts on a four byte boundary
            }

            foreach (var (tag, tableOffset, length) in placed)
            {
                writer.Bytes(Encoding.ASCII.GetBytes(tag));
                writer.UInt32(Checksum(tables[tag]));
                writer.UInt32((uint)tableOffset);
                writer.UInt32((uint)length);
            }

            foreach (var (tag, _, _) in placed)
            {
                writer.Bytes(tables[tag]);
                while (stream.Length % 4 != 0) writer.Byte(0);
            }

            var file = stream.ToArray();
            WriteChecksumAdjustment(file, placed);
            return file;
        }

        /// <summary>
        /// The head table carries a number that makes the whole file sum to a fixed value, so it can
        /// only be worked out once every other byte is in place.
        /// </summary>
        private static void WriteChecksumAdjustment(byte[] file, List<(string Tag, int Offset, int Length)> placed)
        {
            foreach (var (tag, offset, _) in placed)
            {
                if (tag != "head") continue;

                uint adjustment = ChecksumMagic - Checksum(file);
                int position = offset + 8;

                file[position] = (byte)(adjustment >> 24);
                file[position + 1] = (byte)(adjustment >> 16);
                file[position + 2] = (byte)(adjustment >> 8);
                file[position + 3] = (byte)adjustment;
                return;
            }
        }

        /// <summary>Sums the data as big-endian words, treating anything past the end as zero.</summary>
        private static uint Checksum(byte[] data)
        {
            uint sum = 0;

            for (int i = 0; i < data.Length; i += 4)
            {
                uint word = 0;
                for (int b = 0; b < 4; b++)
                    word = (word << 8) | (i + b < data.Length ? data[i + b] : 0u);

                unchecked { sum += word; }
            }

            return sum;
        }

        private static int Min(List<Glyph> glyphs, Func<Glyph, int> select)
        {
            int result = 0;
            bool any = false;
            foreach (var glyph in glyphs)
            {
                if (glyph.IsEmpty) continue;
                int value = select(glyph);
                result = any ? Math.Min(result, value) : value;
                any = true;
            }
            return result;
        }

        private static int Max(List<Glyph> glyphs, Func<Glyph, int> select)
        {
            int result = 0;
            bool any = false;
            foreach (var glyph in glyphs)
            {
                if (glyph.IsEmpty) continue;
                int value = select(glyph);
                result = any ? Math.Max(result, value) : value;
                any = true;
            }
            return result;
        }

        private static int AverageAdvance(List<Glyph> glyphs)
        {
            if (glyphs.Count == 0) return 0;
            long total = 0;
            foreach (var glyph in glyphs) total += glyph.Advance;
            return (int)(total / glyphs.Count);
        }

        private static int FirstCodepoint(List<Glyph> glyphs)
        {
            foreach (var glyph in glyphs)
                if (glyph.Codepoint is >= 0 and <= 0xFFFF) return glyph.Codepoint;
            return 32;
        }

        private static int LastCodepoint(List<Glyph> glyphs)
        {
            int last = 32;
            foreach (var glyph in glyphs)
                if (glyph.Codepoint is >= 0 and <= 0xFFFF) last = glyph.Codepoint;
            return last;
        }

        // -- embedded bitmaps --------------------------------------------

        /// <summary>
        /// Writes the location and data tables for every strike.
        /// </summary>
        /// <remarks>
        /// Each strike covers the whole glyph range in one block, using index format 1, which stores
        /// an offset per glyph and so lets a blank character take no space by sharing the offset of
        /// the next one. Image format 1 pairs small metrics with rows padded to whole bytes, which
        /// costs a few bits per row and removes all the bit shifting the packed form would need.
        /// </remarks>
        private static (byte[] Location, byte[] Data) BuildEmbeddedBitmaps(
            List<FontStrike> strikes, int glyphCount)
        {
            using var dataStream = new MemoryStream();
            var data = new BigEndianWriter(dataStream);
            data.UInt32(0x00020000);   // version

            // Glyph zero is notdef and is never drawn, so every strike starts at glyph one.
            int firstGlyph = 1;
            int lastGlyph = glyphCount - 1;

            var subTables = new List<byte[]>();

            foreach (var strike in strikes)
            {
                uint imageDataOffset = (uint)dataStream.Length;
                var offsets = new List<uint>();

                for (int id = firstGlyph; id <= lastGlyph; id++)
                {
                    offsets.Add((uint)(dataStream.Length - imageDataOffset));

                    int index = id - firstGlyph;
                    if (index >= strike.Glyphs.Count) continue;

                    var glyph = strike.Glyphs[index];
                    if (glyph.IsEmpty) continue;   // no bytes at all, so the offsets come out equal

                    data.Byte((byte)glyph.Height);
                    data.Byte((byte)glyph.Width);
                    data.Byte(unchecked((byte)(sbyte)glyph.BearingX));
                    data.Byte(unchecked((byte)(sbyte)glyph.BearingY));
                    data.Byte((byte)glyph.Advance);
                    data.Bytes(glyph.Rows);
                }

                offsets.Add((uint)(dataStream.Length - imageDataOffset));

                using var subStream = new MemoryStream();
                var sub = new BigEndianWriter(subStream);
                sub.UInt16(1);                 // index format 1: an offset for every glyph
                sub.UInt16(1);                 // image format 1: small metrics, rows on byte boundaries
                sub.UInt32(imageDataOffset);
                foreach (uint offset in offsets) sub.UInt32(offset);

                subTables.Add(subStream.ToArray());
            }

            using var locationStream = new MemoryStream();
            var location = new BigEndianWriter(locationStream);

            location.UInt32(0x00020000);
            location.UInt32((uint)strikes.Count);

            // Each size record is 48 bytes, then each strike's array entry and its index sub table.
            int arrayStart = 8 + (strikes.Count * 48);
            var arrayOffsets = new List<int>();
            int running = arrayStart;

            for (int i = 0; i < strikes.Count; i++)
            {
                arrayOffsets.Add(running);
                running += 8 + subTables[i].Length;
            }

            for (int i = 0; i < strikes.Count; i++)
            {
                var strike = strikes[i];

                location.UInt32((uint)arrayOffsets[i]);
                location.UInt32((uint)(8 + subTables[i].Length));
                location.UInt32(1);            // one index sub table
                location.UInt32(0);            // colour reference, unused

                WriteLineMetrics(location, strike);   // horizontal
                WriteLineMetrics(location, strike);   // vertical, the same values

                location.UInt16((ushort)firstGlyph);
                location.UInt16((ushort)lastGlyph);
                location.Byte((byte)strike.PixelsPerEm);
                location.Byte((byte)strike.PixelsPerEm);
                location.Byte(1);              // one bit per pixel
                location.Byte(1);              // horizontal metrics
            }

            for (int i = 0; i < strikes.Count; i++)
            {
                location.UInt16((ushort)firstGlyph);
                location.UInt16((ushort)lastGlyph);
                location.UInt32(8);            // the sub table sits straight after this entry
                location.Bytes(subTables[i]);
            }

            return (locationStream.ToArray(), dataStream.ToArray());
        }

        private static void WriteLineMetrics(BigEndianWriter writer, FontStrike strike)
        {
            writer.Byte(unchecked((byte)(sbyte)strike.Ascender));
            writer.Byte(unchecked((byte)(sbyte)(-strike.Descender)));
            writer.Byte((byte)Math.Min(255, strike.MaxAdvance));
            writer.Byte(1);    // caret slope numerator
            writer.Byte(0);    // caret slope denominator
            writer.Byte(0);    // caret offset
            writer.Byte(0);    // min origin side bearing
            writer.Byte(0);    // min advance side bearing
            writer.Byte(unchecked((byte)(sbyte)strike.Ascender));
            writer.Byte(unchecked((byte)(sbyte)(-strike.Descender)));
            writer.Byte(0);    // padding
            writer.Byte(0);
        }

        /// <summary>Everything in a font file is big-endian, which no BinaryWriter does by default.</summary>
        private sealed class BigEndianWriter
        {
            private readonly Stream _stream;

            public BigEndianWriter(Stream stream) => _stream = stream;

            public void Byte(byte value) => _stream.WriteByte(value);

            public void Bytes(byte[] value) => _stream.Write(value, 0, value.Length);

            public void UInt16(ushort value)
            {
                _stream.WriteByte((byte)(value >> 8));
                _stream.WriteByte((byte)value);
            }

            public void Int16(short value) => UInt16(unchecked((ushort)value));

            public void UInt32(uint value)
            {
                _stream.WriteByte((byte)(value >> 24));
                _stream.WriteByte((byte)(value >> 16));
                _stream.WriteByte((byte)(value >> 8));
                _stream.WriteByte((byte)value);
            }

            public void Int64(long value)
            {
                for (int shift = 56; shift >= 0; shift -= 8)
                    _stream.WriteByte((byte)(value >> shift));
            }
        }
    }
}
