namespace PixlPunkt.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using PixlPunkt.Core.Document;
using Windows.Graphics;

/// <summary>
/// Drawing a font at a fixed size for embedding in a font file. A strike is what a reader uses
/// instead of rasterising the outline, so if one is wrong the font looks broken at exactly the
/// sizes it was supposed to be perfect at.
/// </summary>
[TestFixture]
public class FontStrikeTests
{
    private const int Cell = 12;
    private const int Em = 8;

    private static CanvasDocument NewFont(string chars = "AB")
    {
        var doc = new CanvasDocument("f", chars.Length * Cell, Cell,
            new SizeInt32 { Width = Cell, Height = Cell },
            new SizeInt32 { Width = chars.Length, Height = 1 });

        var state = doc.FontState;
        state.HasState = true;
        state.SideBearing = 0;
        state.SetEmBox(Em, Em, Cell, Cell);
        state.SetDefaultGuides(Cell);

        for (int i = 0; i < chars.Length; i++)
            state.GetOrAdd(chars[i]).CellIndex = i;

        return doc;
    }

    /// <summary>Paints a shape into a glyph's cell from rows of text, top left of the cell.</summary>
    private static void Paint(CanvasDocument doc, char c, params string[] rows)
    {
        var cell = FontMetricsOps.GetCellRect(doc, doc.FontState.Glyphs[c].CellIndex);
        var surface = doc.Surface;

        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if (rows[y][x] == '#')
                    surface.Pixels[((cell.Y + y) * surface.Width + cell.X + x) * 4 + 3] = 255;
    }

    /// <summary>Renders a strike bitmap back into text, so the shape can be compared directly.</summary>
    private static string[] Render(GlyphStrikeBitmap glyph)
    {
        int stride = (glyph.Width + 7) / 8;
        var lines = new List<string>();

        for (int y = 0; y < glyph.Height; y++)
        {
            var line = new StringBuilder();
            for (int x = 0; x < glyph.Width; x++)
            {
                bool on = (glyph.Rows[(y * stride) + (x >> 3)] & (0x80 >> (x & 7))) != 0;
                line.Append(on ? '#' : '.');
            }
            lines.Add(line.ToString());
        }

        return lines.ToArray();
    }

    private static GlyphStrikeBitmap GlyphOf(FontStrike strike, char c) =>
        strike.Glyphs.Single(g => g.Codepoint == c);

    // ── which sizes can be built ────────────────────────────────────

    [Test]
    public void SmallMultiplesCanBeBuiltAndVeryLargeOnesCannot()
    {
        var doc = NewFont();

        FontStrikeOps.CanBuild(doc, 1).Should().BeTrue();
        FontStrikeOps.CanBuild(doc, 8).Should().BeTrue();

        // The fields a strike is stored in are single bytes, so there is a ceiling.
        FontStrikeOps.CanBuild(doc, 40).Should().BeFalse("a cell of 480 px cannot be recorded in a byte");
        FontStrikeOps.CanBuild(doc, 0).Should().BeFalse();
    }

    [Test]
    public void AvailableScalesAreTheOnesThatFit()
    {
        var scales = FontStrikeOps.AvailableScales(NewFont());

        scales.Should().NotBeEmpty();
        scales.Should().BeInAscendingOrder();
        scales.Should().OnlyContain(s => FontStrikeOps.CanBuild(NewFont(), s));
    }

    [Test]
    public void ADocumentThatIsNotAFontHasNoStrikes()
    {
        var doc = new CanvasDocument("plain", 16, 16,
            new SizeInt32 { Width = 8, Height = 8 }, new SizeInt32 { Width = 2, Height = 2 });

        FontStrikeOps.CanBuild(doc, 1).Should().BeFalse();
        FontStrikeOps.AvailableScales(doc).Should().BeEmpty();
    }

    // ── the pictures themselves ─────────────────────────────────────

    [Test]
    public void AStrikeAtOneToOneIsTheGlyphExactlyAsDrawn()
    {
        var doc = NewFont();
        Paint(doc, 'A',
            "..###",
            "..#.#",
            "..###");

        var glyph = GlyphOf(FontStrikeOps.Build(doc, 1), 'A');

        glyph.Width.Should().Be(3);
        glyph.Height.Should().Be(3);
        Render(glyph).Should().Equal("###", "#.#", "###");
    }

    [Test]
    public void ScalingUpRepeatsWholePixels()
    {
        var doc = NewFont();
        Paint(doc, 'A',
            "..#.",
            "..##");

        var glyph = GlyphOf(FontStrikeOps.Build(doc, 2), 'A');

        glyph.Width.Should().Be(4);
        glyph.Height.Should().Be(4);
        Render(glyph).Should().Equal("##..", "##..", "####", "####");
    }

    [Test]
    public void TheImageCoversTheInkAndNothingElse()
    {
        // A strike storing whole cells would carry every blank margin with it, and the drawing
        // room around the em is usually blank.
        var doc = NewFont();
        Paint(doc, 'A',
            "....",
            ".##.",
            "....");

        var glyph = GlyphOf(FontStrikeOps.Build(doc, 1), 'A');

        glyph.Width.Should().Be(2, "only the two lit pixels");
        glyph.Height.Should().Be(1);
    }

    [Test]
    public void ABlankCharacterHasNoImageButKeepsItsAdvance()
    {
        var doc = NewFont();
        doc.FontState.SideBearing = 2;

        var glyph = GlyphOf(FontStrikeOps.Build(doc, 2), 'A');

        glyph.IsEmpty.Should().BeTrue();
        glyph.Rows.Should().BeEmpty();
        glyph.Advance.Should().Be(4 * 2, "a space still moves the pen");
    }

    // ── how it sits against the baseline and the pen ────────────────

    [Test]
    public void BearingsAreMeasuredFromThePenAndTheBaseline()
    {
        var doc = NewFont();
        doc.FontState.SideBearing = 0;

        // One pixel at column 3, row 2. The baseline of this font sits on row 8.
        Paint(doc, 'A',
            "....",
            "....",
            "...#");

        var glyph = GlyphOf(FontStrikeOps.Build(doc, 1), 'A');

        glyph.BearingX.Should().Be(0, "auto-fit puts the pen at the left edge of the ink");
        glyph.BearingY.Should().Be(6, "the ink starts six rows above the baseline");
    }

    [Test]
    public void EverythingScalesTogether()
    {
        var doc = NewFont();
        Paint(doc, 'A',
            "..##",
            "..##");

        var single = GlyphOf(FontStrikeOps.Build(doc, 1), 'A');
        var triple = GlyphOf(FontStrikeOps.Build(doc, 3), 'A');

        triple.Width.Should().Be(single.Width * 3);
        triple.Height.Should().Be(single.Height * 3);
        triple.BearingX.Should().Be(single.BearingX * 3);
        triple.BearingY.Should().Be(single.BearingY * 3);
        triple.Advance.Should().Be(single.Advance * 3);
    }

    [Test]
    public void TheStrikeReportsTheSizeAndTheLineMetrics()
    {
        var strike = FontStrikeOps.Build(NewFont(), 3);

        strike.PixelsPerEm.Should().Be(24, "eight pixels to the em, tripled");
        strike.Scale.Should().Be(3);
        strike.Ascender.Should().Be(6 * 3, "baseline to the top of the em");
        strike.Descender.Should().Be(2 * 3, "baseline to the bottom of the em, as a positive number");
    }

    [Test]
    public void GlyphsComeBackInCodepointOrder()
    {
        var strike = FontStrikeOps.Build(NewFont("BA"), 1);

        strike.Glyphs.Select(g => g.Codepoint).Should().Equal((int)'A', (int)'B');
    }

    // ── embedded in the font file ───────────────────────────────────

    [Test]
    public void AskingForStrikesAddsTheTwoTablesThatHoldThem()
    {
        var doc = NewFont();
        Paint(doc, 'A', "..##", "..##");

        var withStrikes = TrueTypeWriter.Build(doc,
            new TrueTypeOptions("Probe", "Regular", 1024, new[] { 1, 2 }));
        var without = TrueTypeWriter.Build(doc, new TrueTypeOptions("Probe", "Regular"));

        TagsOf(withStrikes).Should().Contain(new[] { "EBLC", "EBDT" });
        TagsOf(without).Should().NotContain("EBLC").And.NotContain("EBDT");
    }

    [Test]
    public void AStrikeSizeThatCannotBeBuiltIsSkippedRatherThanWrittenWrong()
    {
        var doc = NewFont();
        Paint(doc, 'A', "..##", "..##");

        // 40 is past what a byte can hold, so only the workable size survives.
        var file = TrueTypeWriter.Build(doc,
            new TrueTypeOptions("Probe", "Regular", 1024, new[] { 2, 40 }));

        TagsOf(file).Should().Contain("EBLC");

        var tables = TablesOf(file);
        int eblc = tables["EBLC"].Offset;
        ((file[eblc + 4] << 24) | (file[eblc + 5] << 16) | (file[eblc + 6] << 8) | file[eblc + 7])
            .Should().Be(1, "one strike written, not two");
    }

    [Test]
    public void AFileWithStrikesStillSumsToTheValueTheFormatExpects()
    {
        var doc = NewFont("ABC");
        Paint(doc, 'A', "..##", "..##");
        Paint(doc, 'B', "..#.", "..##");

        var file = TrueTypeWriter.Build(doc,
            new TrueTypeOptions("Probe", "Regular", 1024, new[] { 1, 2, 3 }));

        uint sum = 0;
        for (int i = 0; i < file.Length; i += 4)
        {
            uint word = 0;
            for (int b = 0; b < 4; b++)
                word = (word << 8) | (i + b < file.Length ? file[i + b] : 0u);
            unchecked { sum += word; }
        }

        sum.Should().Be(0xB1B0AFBA);
    }

    private static List<string> TagsOf(byte[] file)
    {
        int count = (file[4] << 8) | file[5];
        return Enumerable.Range(0, count)
            .Select(i => Encoding.ASCII.GetString(file, 12 + (i * 16), 4))
            .ToList();
    }

    private static Dictionary<string, (int Offset, int Length)> TablesOf(byte[] file)
    {
        var tables = new Dictionary<string, (int, int)>();
        int count = (file[4] << 8) | file[5];

        for (int i = 0; i < count; i++)
        {
            int record = 12 + (i * 16);
            string tag = Encoding.ASCII.GetString(file, record, 4);
            int offset = (file[record + 8] << 24) | (file[record + 9] << 16) | (file[record + 10] << 8) | file[record + 11];
            int length = (file[record + 12] << 24) | (file[record + 13] << 16) | (file[record + 14] << 8) | file[record + 15];
            tables[tag] = (offset, length);
        }

        return tables;
    }
}
