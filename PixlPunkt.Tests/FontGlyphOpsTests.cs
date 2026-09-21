namespace PixlPunkt.Tests;

using System.Linq;
using FluentAssertions;
using PixlPunkt.Core.Document;
using Windows.Graphics;

/// <summary>
/// Stage 5 of the font editor: what the glyphs panel lists, how empties are spotted and how typing
/// finds a character. All of it is a query over the document, so none of it needs a window.
/// </summary>
[TestFixture]
public class FontGlyphOpsTests
{
    private const int Em = 8;

    /// <summary>A 4x2 sheet of 8x8 cells mapped to "ABCDEFGH", all of them blank to start.</summary>
    private static CanvasDocument NewFontDoc()
    {
        var doc = new CanvasDocument("f", Em * 4, Em * 2,
            new SizeInt32 { Width = Em, Height = Em },
            new SizeInt32 { Width = 4, Height = 2 });
        var st = doc.FontState;
        st.HasState = true;
        st.SideBearing = 0;
        st.MapRange(firstCell: 0, firstCodepoint: 'A', lastCodepoint: 'H');
        return doc;
    }

    /// <summary>Fills a cell's whole area, which is the simplest thing that counts as drawn.</summary>
    private static void Ink(CanvasDocument doc, int cellIndex)
    {
        var surf = doc.Surface;
        var cell = FontMetricsOps.GetCellRect(doc, cellIndex);
        for (int y = cell.Y; y < cell.Y + cell.Height; y++)
            for (int x = cell.X; x < cell.X + cell.Width; x++)
                surf.Pixels[(y * surf.Width + x) * 4 + 3] = 255;
    }

    [Test]
    public void Summarize_ListsEveryMappedCharacterInCodepointOrder()
    {
        var glyphs = FontGlyphOps.Summarize(NewFontDoc());

        glyphs.Select(g => g.Codepoint).Should().Equal("ABCDEFGH".Select(c => (int)c));
        glyphs.Select(g => g.CellIndex).Should().Equal(0, 1, 2, 3, 4, 5, 6, 7);
    }

    [Test]
    public void Summarize_OnADocumentThatIsNotAFont_IsEmpty()
    {
        var doc = new CanvasDocument("plain", 16, 16,
            new SizeInt32 { Width = 8, Height = 8 }, new SizeInt32 { Width = 2, Height = 2 });

        FontGlyphOps.Summarize(doc).Should().BeEmpty();
    }

    [Test]
    public void AnUndrawnCharacter_IsReportedAsSuch_AndCounted()
    {
        var doc = NewFontDoc();
        Ink(doc, 0);
        Ink(doc, 3);

        var glyphs = FontGlyphOps.Summarize(doc);

        glyphs.Single(g => g.Codepoint == 'A').HasInk.Should().BeTrue();
        glyphs.Single(g => g.Codepoint == 'B').HasInk.Should().BeFalse();
        FontGlyphOps.UndrawnCount(glyphs).Should().Be(6, "only two of the eight were drawn");
    }

    [Test]
    public void Summarize_ReportsResolvedSpacing_AndWhetherItWasPinned()
    {
        var doc = NewFontDoc();
        Ink(doc, 0);

        var pinned = doc.FontState.GetOrAdd('B');
        pinned.AutoFit = false;
        pinned.OriginX = 2;
        pinned.Advance = 5;

        var glyphs = FontGlyphOps.Summarize(doc);

        var a = glyphs.Single(g => g.Codepoint == 'A');
        a.AutoFit.Should().BeTrue();
        a.Advance.Should().Be(Em, "auto-fit measured the ink, which fills the cell");

        var b = glyphs.Single(g => g.Codepoint == 'B');
        b.AutoFit.Should().BeFalse();
        (b.OriginX, b.Advance).Should().Be((2, 5), "pinned spacing is reported as set, ink or none");
    }

    [Test]
    public void TypingACharacter_JumpsToIt()
    {
        var glyphs = FontGlyphOps.Summarize(NewFontDoc());

        FontGlyphOps.FindJumpIndex(glyphs, "C").Should().Be(2);
        FontGlyphOps.FindJumpIndex(glyphs, "A").Should().Be(0);
    }

    [Test]
    public void TypingACharacterTheFontLacks_LandsOnTheNextOne()
    {
        var glyphs = FontGlyphOps.Summarize(NewFontDoc());

        // '5' sorts before 'A', so the first glyph is the nearest thing to it.
        FontGlyphOps.FindJumpIndex(glyphs, "5").Should().Be(0);

        // 'Z' is past the end, so there is nowhere to go.
        FontGlyphOps.FindJumpIndex(glyphs, "Z").Should().Be(-1);
    }

    [Test]
    public void ACodepointCanBeTyped_ForCharactersTheKeyboardWillNotProduce()
    {
        FontGlyphOps.ParseJumpTarget("U+0043").Should().Be('C');
        FontGlyphOps.ParseJumpTarget("0x43").Should().Be('C');
        FontGlyphOps.ParseJumpTarget("1FAE").Should().Be(0x1FAE);
    }

    [Test]
    public void ALoneDigit_MeansTheDigitGlyph_NotAControlCode()
    {
        // Typing "8" in a font editor means the glyph eight, not backspace.
        FontGlyphOps.ParseJumpTarget("8").Should().Be('8');
        FontGlyphOps.ParseJumpTarget("").Should().Be(-1);
        FontGlyphOps.ParseJumpTarget("   ").Should().Be(-1);
    }

    [Test]
    public void OrdinaryTypingFallsBackToTheFirstCharacter()
    {
        // Someone typing a word is looking for its first letter, not a hex code.
        FontGlyphOps.ParseJumpTarget("Cat").Should().Be('C');
    }

    [Test]
    public void NeighboursCome_FromTheFontsOwnOrder()
    {
        var glyphs = FontGlyphOps.Summarize(NewFontDoc());

        FontGlyphOps.NeighboursOf(glyphs, 'D').Should().Be(((int)'C', (int)'E'));
    }

    [Test]
    public void AtEitherEndOfTheSet_OneSideHasNothing()
    {
        var glyphs = FontGlyphOps.Summarize(NewFontDoc());

        FontGlyphOps.NeighboursOf(glyphs, 'A').Should().Be((-1, (int)'B'));
        FontGlyphOps.NeighboursOf(glyphs, 'H').Should().Be(((int)'G', -1));
    }

    [Test]
    public void TheSample_ShowsAGlyphInCompany_NotAlone()
    {
        var glyphs = FontGlyphOps.Summarize(NewFontDoc());

        FontGlyphOps.SampleAround(glyphs, 'D').Should().Be("CDE");
        FontGlyphOps.SampleAround(glyphs, 'A').Should().Be("AB", "there is nothing to its left");
        FontGlyphOps.SampleAround(glyphs, 'H').Should().Be("GH");
    }

    [Test]
    public void PinningASide_HoldsThatCharacterWhicheverGlyphIsSelected()
    {
        // Checking every letter against a fixed reference is how spacing is actually judged.
        var glyphs = FontGlyphOps.Summarize(NewFontDoc());

        FontGlyphOps.SampleAround(glyphs, 'D', pinnedLeft: 'H', pinnedRight: 'H').Should().Be("HDH");
        FontGlyphOps.SampleAround(glyphs, 'E', pinnedLeft: 'H', pinnedRight: 'H').Should().Be("HEH");
    }

    [Test]
    public void PinningACharacterTheFontLacks_LeavesThatSideEmpty()
    {
        var glyphs = FontGlyphOps.Summarize(NewFontDoc());

        FontGlyphOps.SampleAround(glyphs, 'D', pinnedLeft: 'Z').Should().Be("DE",
            "a character with no glyph cannot be drawn beside anything");
    }

    [Test]
    public void AskingAboutAGlyphTheFontLacks_GivesNothing()
    {
        var glyphs = FontGlyphOps.Summarize(NewFontDoc());

        FontGlyphOps.SampleAround(glyphs, 'Z').Should().BeEmpty();
    }

    [Test]
    public void LabelFor_NamesTheCharactersThatShowNothing()
    {
        FontGlyphOps.LabelFor('A').Should().Be("A");
        FontGlyphOps.LabelFor(' ').Should().Be("SP", "a blank label reads as a bug");
        FontGlyphOps.LabelFor(0x7F).Should().Be("U+7F");
    }
}
