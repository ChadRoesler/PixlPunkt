namespace PixlPunkt.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using PixlPunkt.Core.Document;
using Windows.Graphics;

/// <summary>
/// Writing a TrueType file. Nothing here is visible on screen, so the bytes are read back and
/// checked against the format: the table directory, the checksums, the character map, the glyph
/// outlines and their winding. A font that another program silently refuses is worse than none.
/// </summary>
[TestFixture]
public class TrueTypeWriterTests
{
    private const int Cell = 12;
    private const int Em = 8;

    /// <summary>A font of a few characters, each holding one pixel at a known place.</summary>
    private static CanvasDocument NewFont(string chars = "AB")
    {
        var doc = new CanvasDocument("f", chars.Length * Cell, Cell,
            new SizeInt32 { Width = Cell, Height = Cell },
            new SizeInt32 { Width = chars.Length, Height = 1 });

        var state = doc.FontState;
        state.HasState = true;
        state.FamilyName = "Probe";
        state.SideBearing = 0;
        state.SetEmBox(Em, Em, Cell, Cell);
        state.SetDefaultGuides(Cell);

        for (int i = 0; i < chars.Length; i++)
            state.GetOrAdd(chars[i]).CellIndex = i;

        return doc;
    }

    /// <summary>Fills a rectangle of a glyph's cell, in cell-local pixels.</summary>
    private static void Fill(CanvasDocument doc, char c, int x0, int y0, int x1, int y1)
    {
        var cell = FontMetricsOps.GetCellRect(doc, doc.FontState.Glyphs[c].CellIndex);
        var surface = doc.Surface;

        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                surface.Pixels[((cell.Y + y) * surface.Width + cell.X + x) * 4 + 3] = 255;
    }

    private static byte[] Build(CanvasDocument doc) =>
        TrueTypeWriter.Build(doc, new TrueTypeOptions("Probe", "Regular"));

    // ── a small reader, enough to check what was written ────────────

    private static ushort U16(byte[] f, int at) => (ushort)((f[at] << 8) | f[at + 1]);

    private static short I16(byte[] f, int at) => unchecked((short)U16(f, at));

    private static uint U32(byte[] f, int at) =>
        ((uint)f[at] << 24) | ((uint)f[at + 1] << 16) | ((uint)f[at + 2] << 8) | f[at + 3];

    private static Dictionary<string, (int Offset, int Length)> Directory(byte[] file)
    {
        var tables = new Dictionary<string, (int, int)>();
        int count = U16(file, 4);

        for (int i = 0; i < count; i++)
        {
            int record = 12 + (i * 16);
            string tag = Encoding.ASCII.GetString(file, record, 4);
            tables[tag] = ((int)U32(file, record + 8), (int)U32(file, record + 12));
        }

        return tables;
    }

    private static uint Checksum(byte[] file, int offset, int length)
    {
        uint sum = 0;
        for (int i = 0; i < length; i += 4)
        {
            uint word = 0;
            for (int b = 0; b < 4; b++)
                word = (word << 8) | (i + b < length ? file[offset + i + b] : 0u);
            unchecked { sum += word; }
        }
        return sum;
    }

    /// <summary>Walks the format 4 character map and returns the glyph number for a character.</summary>
    private static int GlyphFor(byte[] file, Dictionary<string, (int Offset, int Length)> tables, char c)
    {
        int cmap = tables["cmap"].Offset;
        int subtable = cmap + (int)U32(file, cmap + 4 + 4);   // first encoding record's offset

        int segCount = U16(file, subtable + 6) / 2;
        int endCodes = subtable + 14;
        int startCodes = endCodes + (segCount * 2) + 2;
        int deltas = startCodes + (segCount * 2);

        for (int i = 0; i < segCount; i++)
        {
            int end = U16(file, endCodes + (i * 2));
            if (c > end) continue;

            int start = U16(file, startCodes + (i * 2));
            if (c < start) return 0;

            return (ushort)(c + I16(file, deltas + (i * 2)));
        }

        return 0;
    }

    private static (int Offset, int Length) GlyphData(
        byte[] file, Dictionary<string, (int Offset, int Length)> tables, int glyphId)
    {
        int loca = tables["loca"].Offset;
        int start = (int)U32(file, loca + (glyphId * 4));
        int end = (int)U32(file, loca + ((glyphId + 1) * 4));
        return (tables["glyf"].Offset + start, end - start);
    }

    // ── the file as a whole ─────────────────────────────────────────

    [Test]
    public void TheFileCarriesEveryTableAReaderNeeds()
    {
        var tables = Directory(Build(NewFont()));

        tables.Keys.Should().Contain(new[]
        {
            "head", "hhea", "maxp", "hmtx", "cmap", "glyf", "loca", "name", "post", "OS/2", "gasp",
        });
    }

    [Test]
    public void TheTableDirectoryIsInTagOrder()
    {
        var file = Build(NewFont());
        int count = U16(file, 4);

        var tags = Enumerable.Range(0, count)
            .Select(i => Encoding.ASCII.GetString(file, 12 + (i * 16), 4))
            .ToList();

        tags.Should().BeInAscendingOrder(StringComparer.Ordinal,
            "a reader is allowed to binary search the directory");
    }

    [Test]
    public void EveryTableChecksumMatchesWhatTheDirectoryClaims()
    {
        var file = Build(NewFont("ABCDE"));
        int count = U16(file, 4);

        for (int i = 0; i < count; i++)
        {
            int record = 12 + (i * 16);
            string tag = Encoding.ASCII.GetString(file, record, 4);
            // tag, then checksum, then offset, then length: four bytes each.
            uint claimed = U32(file, record + 4);
            int offset = (int)U32(file, record + 8);
            int length = (int)U32(file, record + 12);

            // The head table is the exception: it holds a value written after its own sum was taken.
            if (tag == "head") continue;

            Checksum(file, offset, length).Should().Be(claimed, $"the {tag} table must sum as claimed");
        }
    }

    [Test]
    public void TheWholeFileSumsToTheValueTheFormatExpects()
    {
        var file = Build(NewFont("ABCDE"));

        // head carries an adjustment chosen so the sum of every byte lands on this constant.
        Checksum(file, 0, file.Length).Should().Be(0xB1B0AFBA);
    }

    [Test]
    public void TablesStartOnFourByteBoundaries()
    {
        foreach (var (offset, _) in Directory(Build(NewFont("ABCDE"))).Values)
            (offset % 4).Should().Be(0);
    }

    // ── head, and the em that everything depends on ─────────────────

    [Test]
    public void TheEmIsAWholeNumberOfPixelsAndTheMagicNumberIsRight()
    {
        var file = Build(NewFont());
        var tables = Directory(file);
        int head = tables["head"].Offset;

        U32(file, head + 12).Should().Be(0x5F0F3CF5, "the magic number marks the table as a head");
        U16(file, head + 18).Should().Be(1024, "eight pixels at 128 units each");
        I16(file, head + 50).Should().Be(1, "the long form of loca, so offsets are plain byte counts");
    }

    [Test]
    public void TheDatesAreNotLeftAtZero()
    {
        var file = Build(NewFont());
        int head = Directory(file)["head"].Offset;

        // Zero is 1904, which readers report as damaged and then try to reinterpret.
        U32(file, head + 20 + 4).Should().BeGreaterThan(0, "created");
        U32(file, head + 28 + 4).Should().BeGreaterThan(0, "modified");
    }

    // ── the character map ───────────────────────────────────────────

    [Test]
    public void CharactersMapToGlyphsAndGlyphZeroIsLeftForNotdef()
    {
        var file = Build(NewFont("AB"));
        var tables = Directory(file);

        GlyphFor(file, tables, 'A').Should().Be(1, "glyph zero is reserved for the missing character");
        GlyphFor(file, tables, 'B').Should().Be(2);
        GlyphFor(file, tables, 'Z').Should().Be(0, "a character the font lacks maps to notdef");
    }

    [Test]
    public void TheGlyphCountCoversNotdefAsWell()
    {
        var file = Build(NewFont("ABC"));
        var tables = Directory(file);

        U16(file, tables["maxp"].Offset + 4).Should().Be(4, "three characters plus notdef");
    }

    [Test]
    public void LocaHasOneMoreEntryThanThereAreGlyphs_AndNeverGoesBackwards()
    {
        var file = Build(NewFont("ABC"));
        var tables = Directory(file);

        int glyphCount = U16(file, tables["maxp"].Offset + 4);
        tables["loca"].Length.Should().Be((glyphCount + 1) * 4);

        int loca = tables["loca"].Offset;
        for (int i = 0; i < glyphCount; i++)
        {
            U32(file, loca + ((i + 1) * 4)).Should().BeGreaterThanOrEqualTo(U32(file, loca + (i * 4)),
                "each glyph runs from its offset to the next one");
        }
    }

    // ── glyph outlines ──────────────────────────────────────────────

    [Test]
    public void ADrawnGlyphBecomesOneContourOfFourPoints()
    {
        var doc = NewFont("AB");
        Fill(doc, 'A', 2, 2, 4, 4);   // a two by two block

        var file = Build(doc);
        var tables = Directory(file);
        var (offset, length) = GlyphData(file, tables, GlyphFor(file, tables, 'A'));

        length.Should().BeGreaterThan(0);
        I16(file, offset).Should().Be(1, "one contour");
        U16(file, offset + 10).Should().Be(3, "four points, so the last is numbered three");
    }

    [Test]
    public void AGlyphWithAHoleGetsTwoContours()
    {
        var doc = NewFont("AB");
        Fill(doc, 'A', 2, 2, 8, 8);
        Fill(doc, 'A', 4, 4, 6, 6);   // fills the middle again, which is not a hole

        var withoutHole = Build(doc);
        var tablesA = Directory(withoutHole);
        I16(withoutHole, GlyphData(withoutHole, tablesA, GlyphFor(withoutHole, tablesA, 'A')).Offset)
            .Should().Be(1, "a solid block is one contour however it was painted");

        // Now punch the middle out by clearing it.
        var cell = FontMetricsOps.GetCellRect(doc, doc.FontState.Glyphs['A'].CellIndex);
        for (int y = 4; y < 6; y++)
            for (int x = 4; x < 6; x++)
                doc.Surface.Pixels[((cell.Y + y) * doc.Surface.Width + cell.X + x) * 4 + 3] = 0;

        var withHole = Build(doc);
        var tablesB = Directory(withHole);
        I16(withHole, GlyphData(withHole, tablesB, GlyphFor(withHole, tablesB, 'A')).Offset)
            .Should().Be(2, "the outside and the hole");
    }

    [Test]
    public void AnUndrawnGlyphTakesUpNoSpaceAtAll()
    {
        var doc = NewFont("AB");
        Fill(doc, 'A', 2, 2, 4, 4);

        var file = Build(doc);
        var tables = Directory(file);

        GlyphData(file, tables, GlyphFor(file, tables, 'B')).Length.Should().Be(0,
            "a blank glyph is recorded by two equal offsets, not by an empty outline");
    }

    [Test]
    public void EveryPointIsOnCurve()
    {
        var doc = NewFont("AB");
        Fill(doc, 'A', 2, 2, 5, 6);

        var file = Build(doc);
        var tables = Directory(file);
        var (offset, _) = GlyphData(file, tables, GlyphFor(file, tables, 'A'));

        int contours = I16(file, offset);
        int points = U16(file, offset + 10 + ((contours - 1) * 2)) + 1;
        int flags = offset + 10 + (contours * 2) + 2;

        for (int i = 0; i < points; i++)
            (file[flags + i] & 0x01).Should().Be(1, "a pixel font has no curves in it");
    }

    [Test]
    public void TheAdvanceMatchesTheFontsOwnSpacing()
    {
        var doc = NewFont("AB");
        Fill(doc, 'A', 2, 2, 6, 6);

        var glyph = doc.FontState.GetOrAdd('A');
        glyph.AutoFit = false;
        glyph.OriginX = 1;
        glyph.Advance = 5;

        var file = Build(doc);
        var tables = Directory(file);
        int glyphId = GlyphFor(file, tables, 'A');

        U16(file, tables["hmtx"].Offset + (glyphId * 4)).Should().Be(5 * 128,
            "five pixels at 128 units each");
    }

    // ── the table that keeps it crisp ───────────────────────────────

    [Test]
    public void GaspAsksForGridFittingWithoutGrey()
    {
        var file = Build(NewFont());
        int gasp = Directory(file)["gasp"].Offset;

        U16(file, gasp).Should().Be(0, "version");
        U16(file, gasp + 2).Should().Be(1, "one range");
        U16(file, gasp + 4).Should().Be(0xFFFF, "covering every size");
        U16(file, gasp + 6).Should().Be(0x0001, "grid fit, and no grey, so nothing is softened");
    }

    [Test]
    public void AscenderAndDescenderComeFromTheEmBox()
    {
        var file = Build(NewFont());
        int hhea = Directory(file)["hhea"].Offset;

        // Baseline six pixels below the top of the em, two above the bottom.
        I16(file, hhea + 4).Should().Be(6 * 128, "ascender");
        I16(file, hhea + 6).Should().Be(-2 * 128, "descender, recorded as a negative");
    }
}
