namespace PixlPunkt.Tests;

using System.Linq;
using FluentAssertions;
using PixlPunkt.Core.Document;
using Windows.Graphics;

/// <summary>
/// Reshaping a font after it exists: changing the em, the drawing room or the character set.
/// Every one of these can destroy work, so the plan is checked separately from the act, and the
/// pixel moves are checked glyph by glyph rather than trusted.
/// </summary>
[TestFixture]
public class FontReshapeTests
{
    /// <summary>A sheet of 8x8 cells mapped to the first characters given, four across.</summary>
    private static CanvasDocument NewFont(string chars, int cellSize = 8, int columns = 4)
    {
        int rows = (chars.Length + columns - 1) / columns;
        var doc = new CanvasDocument("f", columns * cellSize, rows * cellSize,
            new SizeInt32 { Width = cellSize, Height = cellSize },
            new SizeInt32 { Width = columns, Height = rows });

        var st = doc.FontState;
        st.HasState = true;
        st.SideBearing = 0;
        for (int i = 0; i < chars.Length; i++)
            st.GetOrAdd(chars[i]).CellIndex = i;

        return doc;
    }

    /// <summary>Marks one pixel of a glyph's cell so it can be followed through a reshape.</summary>
    private static void Mark(CanvasDocument doc, char c, int localX, int localY, byte value)
    {
        var g = doc.FontState.Glyphs[c];
        var cell = FontMetricsOps.GetCellRect(doc, g.CellIndex);
        var layer = doc.GetAllRasterLayers()[0].Surface;
        int i = ((cell.Y + localY) * layer.Width + cell.X + localX) * 4;
        layer.Pixels[i] = value;
        layer.Pixels[i + 1] = value;
        layer.Pixels[i + 2] = value;
        layer.Pixels[i + 3] = 255;
    }

    /// <summary>Reads back the marker value at a glyph-local position, or 0 for nothing.</summary>
    private static byte Read(CanvasDocument doc, char c, int localX, int localY)
    {
        var g = doc.FontState.Glyphs[c];
        var cell = FontMetricsOps.GetCellRect(doc, g.CellIndex);
        var layer = doc.GetAllRasterLayers()[0].Surface;
        int x = cell.X + localX, y = cell.Y + localY;
        if (x < 0 || x >= layer.Width || y < 0 || y >= layer.Height) return 0;
        int i = (y * layer.Width + x) * 4;
        return layer.Pixels[i + 3] == 0 ? (byte)0 : layer.Pixels[i];
    }

    // ── planning ────────────────────────────────────────────────────

    [Test]
    public void APlanReportsTheNewGeometry_WithoutTouchingAnything()
    {
        var doc = NewFont("ABCDEFGH");

        var plan = FontReshapeOps.Plan(doc, emWidth: 8, emHeight: 8, drawingRoom: 2);

        plan.CellSize.Should().Be(new SizeInt32 { Width = 12, Height = 12 });
        plan.Columns.Should().Be(4);
        plan.Rows.Should().Be(2);
        plan.CanvasSize.Should().Be(new SizeInt32 { Width = 48, Height = 24 });

        doc.TileSize.Width.Should().Be(8, "planning must not change the document");
        doc.PixelWidth.Should().Be(32);
    }

    [Test]
    public void GrowingIsNotDestructive()
    {
        var doc = NewFont("ABCD");

        var plan = FontReshapeOps.Plan(doc, 8, 8, drawingRoom: 2);

        plan.IsDestructive.Should().BeFalse();
        plan.CropsGlyphs.Should().BeFalse();
        plan.LosesCharacters.Should().BeFalse();
    }

    [Test]
    public void AShrinkingCellIsFlaggedAsCropping()
    {
        var doc = NewFont("ABCD", cellSize: 12);

        FontReshapeOps.Plan(doc, emWidth: 6, emHeight: 6, drawingRoom: 0)
            .CropsGlyphs.Should().BeTrue("ink outside the smaller cell is cut off");
    }

    [Test]
    public void DroppingCharactersIsFlagged_AndNamesThem()
    {
        var doc = NewFont("ABCD");

        var plan = FontReshapeOps.Plan(doc, 8, 8, 0, characters: "AB");

        plan.LosesCharacters.Should().BeTrue();
        plan.Removed.Should().Equal((int)'C', (int)'D');
        plan.IsDestructive.Should().BeTrue();
    }

    [Test]
    public void AddingCharactersKeepsEveryoneAndIsNotDestructive()
    {
        var doc = NewFont("ABCD");

        var plan = FontReshapeOps.Plan(doc, 8, 8, 0, characters: "ABCDEF");

        plan.Removed.Should().BeEmpty();
        plan.IsDestructive.Should().BeFalse();
        plan.Characters.Should().Be("ABCDEF");
    }

    [Test]
    public void ACharacterSetIsDeDuplicatedAndStrippedOfBreaks()
    {
        FontReshapeOps.Normalise("AAB\nC\tD").Should().Be("ABCD");
    }

    [Test]
    public void CurrentCharactersComeBackInOrder()
    {
        FontReshapeOps.CurrentCharacters(NewFont("DCBA")).Should().Be("ABCD",
            "the font is listed in codepoint order, whatever order the cells were filled in");
    }

    // ── anchoring ───────────────────────────────────────────────────

    [Test]
    public void AnchorDecidesWhereTheOldCellSitsInTheNewOne()
    {
        FontReshapeOps.AnchorOffset(GlyphAnchor.TopLeft, 4, 4).Should().Be((0, 0));
        FontReshapeOps.AnchorOffset(GlyphAnchor.MiddleCenter, 4, 4).Should().Be((2, 2));
        FontReshapeOps.AnchorOffset(GlyphAnchor.BottomRight, 4, 4).Should().Be((4, 4));
    }

    [Test]
    public void AnchorAlsoDecidesWhichEdgeSurvivesAShrink()
    {
        // Negative deltas: the anchored edge is the one that keeps its pixels.
        FontReshapeOps.AnchorOffset(GlyphAnchor.TopLeft, -4, -4).Should().Be((0, 0));
        FontReshapeOps.AnchorOffset(GlyphAnchor.BottomRight, -4, -4).Should().Be((-4, -4));
        FontReshapeOps.AnchorOffset(GlyphAnchor.MiddleCenter, -4, -4).Should().Be((-2, -2));
    }

    // ── applying ────────────────────────────────────────────────────

    [Test]
    public void GrowingTheCell_MovesEveryGlyphToItsNewPlace()
    {
        var doc = NewFont("ABCDEFGH");
        Mark(doc, 'A', 0, 0, 11);
        Mark(doc, 'F', 3, 4, 22);

        var plan = FontReshapeOps.Plan(doc, 8, 8, drawingRoom: 2);
        FontReshapeOps.Apply(doc, plan, GlyphAnchor.MiddleCenter);

        doc.TileSize.Should().Be(new SizeInt32 { Width = 12, Height = 12 });
        doc.PixelWidth.Should().Be(48);
        doc.PixelHeight.Should().Be(24);

        // Centred in a cell four pixels bigger, so everything moved two across and two down.
        Read(doc, 'A', 2, 2).Should().Be(11);
        Read(doc, 'F', 5, 6).Should().Be(22);
    }

    [Test]
    public void TheEmBoxEndsUpCentredInTheNewCell()
    {
        var doc = NewFont("ABCD");

        FontReshapeOps.Apply(doc, FontReshapeOps.Plan(doc, 8, 8, 2), GlyphAnchor.MiddleCenter);

        FontMetricsOps.EmBox(doc).Should().Be(new RectInt32 { X = 2, Y = 2, Width = 8, Height = 8 });
        FontMetricsOps.EmHeightOf(doc).Should().Be(8);
    }

    [Test]
    public void GuidesFollowTheInkRatherThanTheirOldRowNumbers()
    {
        var doc = NewFont("ABCD");
        doc.FontState.SetDefaultGuides(8);
        int baselineBefore = doc.FontState.BaselineY;

        FontReshapeOps.Apply(doc, FontReshapeOps.Plan(doc, 8, 8, 2), GlyphAnchor.MiddleCenter);

        doc.FontState.BaselineY.Should().Be(baselineBefore + 2,
            "the glyphs moved down two rows, so the baseline did too");
    }

    [Test]
    public void SpacingMovesWithTheGlyph()
    {
        var doc = NewFont("ABCD");
        var a = doc.FontState.GetOrAdd('A');
        a.AutoFit = false;
        a.OriginX = 1;
        a.Advance = 5;

        FontReshapeOps.Apply(doc, FontReshapeOps.Plan(doc, 8, 8, 2), GlyphAnchor.MiddleCenter);

        var moved = doc.FontState.Glyphs['A'];
        moved.AutoFit.Should().BeFalse("a hand-set glyph stays hand-set");
        moved.OriginX.Should().Be(3, "the pen moved with the ink");
        moved.Advance.Should().Be(5, "the width of the letter did not change");
    }

    [Test]
    public void ShrinkingTheCell_CropsInkOutsideItAndKeepsTheRest()
    {
        var doc = NewFont("ABCD", cellSize: 8);
        Mark(doc, 'A', 0, 0, 33);   // top-left, survives a top-left anchored shrink
        Mark(doc, 'A', 7, 7, 44);   // bottom-right, falls outside a 4x4 cell

        var plan = FontReshapeOps.Plan(doc, emWidth: 4, emHeight: 4, drawingRoom: 0);
        plan.CropsGlyphs.Should().BeTrue();
        FontReshapeOps.Apply(doc, plan, GlyphAnchor.TopLeft);

        doc.TileSize.Should().Be(new SizeInt32 { Width = 4, Height = 4 });
        Read(doc, 'A', 0, 0).Should().Be(33, "the anchored corner is kept");
        doc.GetAllRasterLayers()[0].Surface.Pixels.Count(b => b == 44).Should().Be(0,
            "what fell outside the smaller cell is gone");
    }

    [Test]
    public void DroppedCharacters_LeaveTheFontEntirely()
    {
        var doc = NewFont("ABCD");
        Mark(doc, 'C', 1, 1, 55);

        FontReshapeOps.Apply(doc, FontReshapeOps.Plan(doc, 8, 8, 0, characters: "ABD"), GlyphAnchor.TopLeft);

        doc.FontState.Glyphs.Keys.Should().BeEquivalentTo(new[] { (int)'A', (int)'B', (int)'D' });
        doc.GetAllRasterLayers()[0].Surface.Pixels.Count(b => b == 55).Should().Be(0,
            "a dropped character takes its pixels with it");
    }

    [Test]
    public void AddedCharactersGetEmptyCells_AndTheOthersKeepTheirInk()
    {
        var doc = NewFont("ABCD");
        Mark(doc, 'D', 2, 2, 66);

        FontReshapeOps.Apply(doc, FontReshapeOps.Plan(doc, 8, 8, 0, characters: "ABCDEF"), GlyphAnchor.TopLeft);

        doc.FontState.Glyphs.Count.Should().Be(6);
        Read(doc, 'D', 2, 2).Should().Be(66, "an existing glyph keeps what was drawn in it");
        FontMetricsOps.MeasureInk(doc, doc.FontState.Glyphs['E'].CellIndex).Should().BeNull(
            "a new character starts blank");
    }

    [Test]
    public void ReorderingTheSetMovesGlyphsToTheirNewCells()
    {
        var doc = NewFont("ABCD");
        Mark(doc, 'A', 1, 1, 77);
        Mark(doc, 'D', 1, 1, 88);

        // Same characters, and the mapping is rebuilt in codepoint order either way.
        FontReshapeOps.Apply(doc, FontReshapeOps.Plan(doc, 8, 8, 0, characters: "DCBA"), GlyphAnchor.TopLeft);

        Read(doc, 'A', 1, 1).Should().Be(77, "a glyph follows its own character, not its old cell");
        Read(doc, 'D', 1, 1).Should().Be(88);
    }

    [Test]
    public void UndoPutsBackTheGlyphsAndTheGeometry()
    {
        var doc = NewFont("ABCD");
        Mark(doc, 'C', 1, 1, 99);

        var item = new PixlPunkt.Core.History.FontReshapeItem(doc, "Reshape Font");
        FontReshapeOps.Apply(doc, FontReshapeOps.Plan(doc, 8, 8, 2, characters: "AB"), GlyphAnchor.MiddleCenter);
        item.CaptureAfter();

        doc.FontState.Glyphs.Count.Should().Be(2);

        item.Undo();

        doc.TileSize.Should().Be(new SizeInt32 { Width = 8, Height = 8 });
        doc.PixelWidth.Should().Be(32);
        doc.FontState.Glyphs.Keys.Should().BeEquivalentTo(new[] { (int)'A', (int)'B', (int)'C', (int)'D' });
        Read(doc, 'C', 1, 1).Should().Be(99, "a dropped character's ink comes back");
    }

    [Test]
    public void RedoAppliesTheReshapeAgain()
    {
        var doc = NewFont("ABCD");

        var item = new PixlPunkt.Core.History.FontReshapeItem(doc, "Reshape Font");
        FontReshapeOps.Apply(doc, FontReshapeOps.Plan(doc, 8, 8, 2), GlyphAnchor.MiddleCenter);
        item.CaptureAfter();

        item.Undo();
        item.Redo();

        doc.TileSize.Should().Be(new SizeInt32 { Width = 12, Height = 12 });
        FontMetricsOps.EmBox(doc).Should().Be(new RectInt32 { X = 2, Y = 2, Width = 8, Height = 8 });
    }

    [Test]
    public void ReshapingANonFontDocumentDoesNothing()
    {
        var doc = new CanvasDocument("plain", 16, 16,
            new SizeInt32 { Width = 8, Height = 8 }, new SizeInt32 { Width = 2, Height = 2 });

        FontReshapeOps.Apply(doc, FontReshapeOps.Plan(doc, 4, 4, 0), GlyphAnchor.TopLeft);

        doc.TileSize.Should().Be(new SizeInt32 { Width = 8, Height = 8 });
        doc.PixelWidth.Should().Be(16);
    }
}
