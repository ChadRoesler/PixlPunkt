namespace PixlPunkt.Tests;

using System.IO;
using FluentAssertions;
using PixlPunkt.Core.Document;
using Windows.Graphics;

/// <summary>
/// Stage 1 of the font editor: a font is a canvas whose tile size is the em box, plus metrics.
/// These cover the model, the cell geometry and the spacing rules, with no UI in sight.
/// </summary>
[TestFixture]
public class FontMetricsTests
{
    private const int Em = 8;

    /// <summary>A 4x2 sheet of 8x8 cells: eight glyphs on a 32x16 canvas.</summary>
    private static CanvasDocument NewFontDoc()
    {
        var doc = new CanvasDocument("f", Em * 4, Em * 2,
            new SizeInt32 { Width = Em, Height = Em },
            new SizeInt32 { Width = 4, Height = 2 });
        doc.FontState.HasState = true;
        doc.FontState.FamilyName = "Test";
        doc.FontState.SetDefaultGuides(Em);
        return doc;
    }

    /// <summary>Fills a rectangle of the active layer, in document pixels.</summary>
    private static void Ink(CanvasDocument doc, int x0, int y0, int w, int h)
    {
        var l = doc.Layers[0];
        var p = l.Surface.Pixels;
        int stride = l.Surface.Width * 4;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                int i = y * stride + x * 4;
                p[i + 2] = 255;
                p[i + 3] = 255;
            }
        doc.CompositeTo(doc.Surface);
    }

    [Test]
    public void OrdinaryDocument_HasInertFontState()
    {
        var doc = new CanvasDocument("t", 64, 64,
            new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = 4, Height = 4 });

        doc.FontState.Should().NotBeNull();
        doc.FontState.HasState.Should().BeFalse();
        doc.FontState.Glyphs.Should().BeEmpty();
    }

    [Test]
    public void DefaultGuides_LeaveDescenderRoom()
    {
        var state = new FontDocumentState();

        state.SetDefaultGuides(8);
        (state.ToplineY, state.BaselineY).Should().Be((0, 6), "two rows below the baseline on an 8px em");

        state.SetDefaultGuides(16);
        state.BaselineY.Should().Be(12);
    }

    [Test]
    public void Cells_AreNumberedRowMajor()
    {
        var doc = NewFontDoc();

        FontMetricsOps.CellCount(doc).Should().Be(8);
        FontMetricsOps.GetCellRect(doc, 0).Should().Be(new RectInt32 { X = 0, Y = 0, Width = 8, Height = 8 });
        FontMetricsOps.GetCellRect(doc, 3).Should().Be(new RectInt32 { X = 24, Y = 0, Width = 8, Height = 8 });
        FontMetricsOps.GetCellRect(doc, 4).Should().Be(new RectInt32 { X = 0, Y = 8, Width = 8, Height = 8 });
    }

    [Test]
    public void MeasureInk_IsCellLocal_AndNullWhenEmpty()
    {
        var doc = NewFontDoc();
        // Two columns of ink inside cell 1, which starts at x=8.
        Ink(doc, 10, 2, 2, 4);

        var ink = FontMetricsOps.MeasureInk(doc, 1);
        ink.Should().NotBeNull();
        ink!.Value.Should().Be(new RectInt32 { X = 2, Y = 2, Width = 2, Height = 4 });

        FontMetricsOps.MeasureInk(doc, 0).Should().BeNull("cell 0 was never painted");
    }

    [Test]
    public void AutoFit_PutsTheOriginOneBearingLeftOfTheInk()
    {
        var doc = NewFontDoc();
        doc.FontState.SideBearing = 1;
        doc.FontState.GetOrAdd('a').CellIndex = 1;
        Ink(doc, 10, 2, 3, 4);   // cell-local column 2, three columns of ink

        var (originX, advance) = FontMetricsOps.ResolveMetrics(doc, 'a');

        originX.Should().Be(1, "one blank column before ink that starts at column 2");
        advance.Should().Be(5, "one column each side of three columns of ink");
        FontMetricsOps.LeftBearingOf(doc, 'a').Should().Be(1, "what a font file would store");
    }

    [Test]
    public void EmptyGlyph_IsASpace()
    {
        var doc = NewFontDoc();
        doc.FontState.SideBearing = 2;
        doc.FontState.GetOrAdd(' ').CellIndex = 0;

        var (originX, advance) = FontMetricsOps.ResolveMetrics(doc, ' ');

        originX.Should().Be(0);
        advance.Should().Be(4);
    }

    [Test]
    public void ManualMetrics_BeatAutoFit_AndCanTuck()
    {
        var doc = NewFontDoc();
        var g = doc.FontState.GetOrAdd('T');
        g.CellIndex = 1;
        g.AutoFit = false;
        g.OriginX = 2;            // origin right of the ink, so the glyph tucks under its neighbour
        g.Advance = 6;
        Ink(doc, 8, 0, 8, 8);     // fills the cell; auto-fit would have said 10 wide

        FontMetricsOps.ResolveMetrics(doc, 'T').Should().Be((2, 6));
        FontMetricsOps.LeftBearingOf(doc, 'T')
            .Should().Be(-2, "tucking is a negative left bearing once it reaches a font file");
    }

    [Test]
    public void GlyphSpacing_IsOneUndoStep()
    {
        var doc = NewFontDoc();
        doc.FontState.SideBearing = 1;
        doc.FontState.GetOrAdd('a').CellIndex = 1;
        Ink(doc, 10, 2, 3, 4);
        var g = doc.FontState.GetOrAdd('a');
        g.AutoFit.Should().BeTrue();

        // What a spacing drag does: freeze the resolved values, then move a post.
        int beforeOrigin = g.OriginX, beforeAdvance = g.Advance;
        bool beforeAuto = g.AutoFit;
        var (originX, advance) = FontMetricsOps.ResolveMetrics(doc, 'a');
        g.OriginX = originX; g.Advance = advance; g.AutoFit = false;
        g.Advance = 7;

        var item = new PixlPunkt.Core.History.FontGlyphMetricsItem(
            doc, 'a', beforeOrigin, beforeAdvance, beforeAuto, "Space Glyph");
        item.HasChange.Should().BeTrue();
        doc.History.Push(item);

        doc.History.Undo().Should().BeTrue();
        doc.FontState.Glyphs['a'].AutoFit.Should().BeTrue("undo puts the glyph back on auto-fit");
        FontMetricsOps.ResolveMetrics(doc, 'a').Advance.Should().Be(5);

        doc.History.Redo().Should().BeTrue();
        doc.FontState.Glyphs['a'].AutoFit.Should().BeFalse();
        FontMetricsOps.ResolveMetrics(doc, 'a').Advance.Should().Be(7);
    }

    [Test]
    public void Monospace_OverridesEverything()
    {
        var doc = NewFontDoc();
        doc.FontState.Monospace = true;
        var g = doc.FontState.GetOrAdd('i');
        g.CellIndex = 1;
        g.AutoFit = false;
        g.Advance = 2;
        Ink(doc, 11, 1, 1, 6);

        FontMetricsOps.ResolveMetrics(doc, 'i').Advance.Should().Be(Em);
    }

    [Test]
    public void UnmappedCharacter_HasNoMetrics()
    {
        var doc = NewFontDoc();
        FontMetricsOps.ResolveMetrics(doc, 'z').Should().Be((0, 0));
    }

    [Test]
    public void MapPrintableAscii_FillsCellsInOrder()
    {
        var state = new FontDocumentState();

        int next = state.MapPrintableAscii();

        next.Should().Be(95, "space through tilde");
        state.Glyphs[' '].CellIndex.Should().Be(0);
        state.Glyphs['A'].CellIndex.Should().Be('A' - ' ');
        state.CodepointAtCell('A' - ' ').Should().Be('A');
        state.CodepointAtCell(999).Should().Be(-1);
    }

    [Test]
    public void CleanSizes_AreTheMultiplesOfTheEm()
    {
        var doc = NewFontDoc();   // 8px em
        FontMetricsOps.CleanSizes(doc).Should().Equal(8, 16, 24, 32, 40, 48);
    }

    [Test]
    public void FontState_RoundTripsThroughTheDocumentFormat()
    {
        var doc = NewFontDoc();
        var st = doc.FontState;
        st.FamilyName = "Vale";
        st.StyleName = "Regular";
        st.Monospace = false;
        st.SideBearing = 2;
        st.LineGap = 1;
        st.BaselineY = 6;
        st.ToplineY = 1;
        st.MapPrintableAscii();
        var t = st.GetOrAdd('T');
        t.AutoFit = false;
        t.OriginX = -1;
        t.Advance = 7;

        var loaded = DocumentIO.Load(new MemoryStream(DocumentIO.SaveToBytes(doc)));
        var r = loaded.FontState;

        r.HasState.Should().BeTrue();
        r.FamilyName.Should().Be("Vale");
        r.SideBearing.Should().Be(2);
        r.LineGap.Should().Be(1);
        (r.ToplineY, r.BaselineY).Should().Be((1, 6));
        r.Glyphs.Should().HaveCount(st.Glyphs.Count);
        r.Glyphs['A'].CellIndex.Should().Be('A' - ' ');
        r.Glyphs['T'].AutoFit.Should().BeFalse();
        r.Glyphs['T'].OriginX.Should().Be(-1);
        r.Glyphs['T'].Advance.Should().Be(7);
    }

    [Test]
    public void SetGuides_ClampsInsideTheEm()
    {
        var st = new FontDocumentState();

        st.SetGuides(toplineY: 2, baselineY: 99, cellHeight: 8).Should().BeTrue();
        st.BaselineY.Should().Be(8, "the baseline cannot leave the em box");

        st.SetGuides(toplineY: 7, baselineY: 4, cellHeight: 8);
        st.ToplineY.Should().Be(3, "cap height is pushed above the baseline");

        st.SetGuides(toplineY: -5, baselineY: 0, cellHeight: 8);
        (st.ToplineY, st.BaselineY).Should().Be((0, 1));

        st.SetGuides(st.ToplineY, st.BaselineY, 8).Should().BeFalse("nothing moved");
    }

    [Test]
    public void GuideMove_IsOneUndoStep()
    {
        var doc = NewFontDoc();
        var st = doc.FontState;
        int raised = 0;
        doc.FontChanged += () => raised++;
        (st.ToplineY, st.BaselineY).Should().Be((0, 6));

        int beforeTop = st.ToplineY, beforeBase = st.BaselineY;
        st.SetGuides(2, 5, Em);
        var item = new PixlPunkt.Core.History.FontGuideItem(doc, beforeTop, beforeBase, "Move Font Guide");
        item.HasChange.Should().BeTrue();
        doc.History.Push(item);

        doc.History.Undo().Should().BeTrue();
        (st.ToplineY, st.BaselineY).Should().Be((0, 6));
        doc.History.Redo().Should().BeTrue();
        (st.ToplineY, st.BaselineY).Should().Be((2, 5));
        raised.Should().Be(2, "undo and redo each notify the view");
    }

    [Test]
    public void GuideItem_WithNoMovement_ReportsNoChange()
    {
        var doc = NewFontDoc();
        var st = doc.FontState;

        new PixlPunkt.Core.History.FontGuideItem(doc, st.ToplineY, st.BaselineY, "Move Font Guide")
            .HasChange.Should().BeFalse();
    }

    [Test]
    public void GlyphInk_MayOverhangIntoItsNeighbours()
    {
        // "a W a" where the middle glyph is drawn full width but advances only half of it, so its
        // ink sits over both neighbours. This is the script-font case: overhang is spacing, not
        // extra drawing room.
        var doc = NewFontDoc();
        var st = doc.FontState;
        foreach (var (ch, cell) in new[] { ('a', 0), ('W', 1) })
            st.GetOrAdd(ch).CellIndex = cell;

        Ink(doc, 2, 2, 4, 4);     // 'a' in cell 0: cell-local columns 2..5
        Ink(doc, 8, 0, 8, 8);     // 'W' in cell 1: the whole 8-wide cell

        var w = st.GetOrAdd('W');
        w.AutoFit = false;
        w.OriginX = 2;            // pen sits two columns into the ink
        w.Advance = 4;            // and moves on after four, half the ink width

        var line = FontLayoutOps.LayoutLine(doc, "aWa");
        line.Should().HaveCount(3);

        var (a1, W, a2) = (line[0], line[1], line[2]);
        a1.PenX.Should().Be(0);
        W.PenX.Should().Be(6, "'a' advances by 6: four columns of ink plus a bearing each side");
        W.DrawX.Should().Be(4, "the cell is drawn two columns left of the pen");
        a2.PenX.Should().Be(10, "'W' advances only 4 despite being 8 wide");

        // The W's ink spans 4..11 while its neighbours occupy 1..6 and 11..16: it overlaps both.
        (W.DrawX + 8).Should().BeGreaterThan(a2.DrawX, "the W reaches into the glyph after it");
        W.DrawX.Should().BeLessThan(a1.DrawX + 8, "and back over the one before it");
    }

    [Test]
    public void MeasureLine_IsAdvance_NotInkWidth()
    {
        var doc = NewFontDoc();
        var st = doc.FontState;
        st.GetOrAdd('W').CellIndex = 1;
        Ink(doc, 8, 0, 8, 8);
        var w = st.GetOrAdd('W');
        w.AutoFit = false; w.OriginX = 0; w.Advance = 3;

        FontLayoutOps.MeasureLine(doc, "WW").Should().Be(6, "two advances of three");
        FontLayoutOps.MeasureInkBounds(doc, "WW")!.Value.Width
            .Should().Be(11, "but the ink covers eleven columns because the glyphs overlap");
    }

    [Test]
    public void UnmappedCharacters_AreSkippedByLayout()
    {
        var doc = NewFontDoc();
        doc.FontState.GetOrAdd('a').CellIndex = 0;
        Ink(doc, 2, 2, 4, 4);

        FontLayoutOps.LayoutLine(doc, "aQa").Should().HaveCount(2);
        FontLayoutOps.MeasureInkBounds(doc, "QQ").Should().BeNull();
    }

    [Test]
    public void EmBox_DefaultsToTheWholeCell()
    {
        var doc = NewFontDoc();

        FontMetricsOps.EmBox(doc).Should().Be(new RectInt32 { X = 0, Y = 0, Width = 8, Height = 8 });
        FontMetricsOps.EmHeightOf(doc).Should().Be(8);
        FontMetricsOps.CleanSizes(doc).Should().Equal(8, 16, 24, 32, 40, 48);
    }

    [Test]
    public void SmallerEmInABiggerCell_GivesOverhangRoom_AndDrivesTheCleanSizes()
    {
        // A 12x12 cell holding an 8x8 em: two columns and two rows of overhang on every side, so
        // a glyph can put ink past its own body without stealing a neighbour's cell.
        var doc = new CanvasDocument("f", 12 * 2, 12,
            new SizeInt32 { Width = 12, Height = 12 }, new SizeInt32 { Width = 2, Height = 1 });
        var st = doc.FontState;
        st.HasState = true;
        st.SetEmBox(emWidth: 8, emHeight: 8, cellWidth: 12, cellHeight: 12);
        st.SetDefaultGuides(12);

        FontMetricsOps.EmBox(doc).Should().Be(new RectInt32 { X = 2, Y = 2, Width = 8, Height = 8 });
        FontMetricsOps.EmHeightOf(doc).Should().Be(8, "the em is the design size, not the cell");
        FontMetricsOps.CleanSizes(doc).Should().Equal(8, 16, 24, 32, 40, 48);

        st.ToplineY.Should().Be(2, "cap height starts at the top of the em, not the cell");
        st.BaselineY.Should().Be(8);
        st.Ascent.Should().Be(6);
        st.Descent(12).Should().Be(2);
    }

    [Test]
    public void Monospace_AdvancesByTheEm_NotTheCell()
    {
        var doc = new CanvasDocument("f", 12, 12,
            new SizeInt32 { Width = 12, Height = 12 }, new SizeInt32 { Width = 1, Height = 1 });
        var st = doc.FontState;
        st.HasState = true;
        st.Monospace = true;
        st.SetEmBox(8, 8, 12, 12);
        st.GetOrAdd('m').CellIndex = 0;

        FontMetricsOps.ResolveMetrics(doc, 'm').Should().Be((2, 8),
            "the pen starts at the em's left edge and moves on by the em width");
    }

    [Test]
    public void EmBox_RoundTripsThroughTheDocumentFormat()
    {
        var doc = new CanvasDocument("f", 24, 12,
            new SizeInt32 { Width = 12, Height = 12 }, new SizeInt32 { Width = 2, Height = 1 });
        doc.FontState.HasState = true;
        doc.FontState.SetEmBox(8, 9, 12, 12);

        var r = DocumentIO.Load(new MemoryStream(DocumentIO.SaveToBytes(doc))).FontState;

        (r.EmLeft, r.EmTop, r.EmWidth, r.EmHeight).Should().Be((2, 1, 8, 9));
    }

    [Test]
    public void ExtensionFor_FollowsTheFontState()
    {
        var plain = new CanvasDocument("plain", 32, 32,
            new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = 2, Height = 2 });

        DocumentIO.ExtensionFor(plain).Should().Be(".pxp");
        DocumentIO.ExtensionFor(NewFontDoc()).Should().Be(".pxpf");
    }

    [Test]
    public void IsNativeExtension_AcceptsBothAndNothingElse()
    {
        DocumentIO.IsNativeExtension(".pxp").Should().BeTrue();
        DocumentIO.IsNativeExtension(".PXPF").Should().BeTrue("the shell does not promise a case");
        DocumentIO.IsNativeExtension(".png").Should().BeFalse();
        DocumentIO.IsNativeExtension(null).Should().BeFalse();
    }

    [Test]
    public void OrdinaryDocument_StillRoundTrips()
    {
        var doc = new CanvasDocument("plain", 32, 32,
            new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = 2, Height = 2 });

        var loaded = DocumentIO.Load(new MemoryStream(DocumentIO.SaveToBytes(doc)));

        loaded.FontState.HasState.Should().BeFalse();
        loaded.FontState.Glyphs.Should().BeEmpty();
    }

    [Test]
    public void OverhangRoom_LetsAScriptGlyphReachOverBothNeighbours()
    {
        // The case that motivated splitting the em from the cell: a swash whose ink is wider than
        // its own body, sitting between two plain letters, as in "aᾮa".
        var doc = new CanvasDocument("f", 12 * 3, 12,
            new SizeInt32 { Width = 12, Height = 12 }, new SizeInt32 { Width = 3, Height = 1 });
        var st = doc.FontState;
        st.HasState = true;
        st.SideBearing = 0;
        st.SetEmBox(emWidth: 8, emHeight: 8, cellWidth: 12, cellHeight: 12);

        st.GetOrAdd('a').CellIndex = 0;
        var swash = st.GetOrAdd('s');
        swash.CellIndex = 1;

        // Ink fills the swash's whole cell, overhang columns included.
        var surf = doc.Surface;
        var cell = FontMetricsOps.GetCellRect(doc, 1);
        for (int y = cell.Y; y < cell.Y + cell.Height; y++)
            for (int x = cell.X; x < cell.X + cell.Width; x++)
                surf.Pixels[(y * surf.Width + x) * 4 + 3] = 255;

        FontMetricsOps.MeasureInk(doc, 1)!.Value.Width.Should().Be(12,
            "the ink runs the full cell, two columns wider than the em on each side");

        // Pinned so the swash advances by its body only, leaving the flourish to overhang.
        swash.AutoFit = false;
        swash.OriginX = 2;
        swash.Advance = 8;

        var placed = FontLayoutOps.LayoutLine(doc, "asa");
        var swashPlacement = placed.Single(p => p.Codepoint == 's');

        // Cell drawn at pen - origin, so its left edge sits two columns before the pen.
        swashPlacement.DrawX.Should().Be(swashPlacement.PenX - 2);
        (swashPlacement.DrawX + 12).Should().BeGreaterThan(swashPlacement.PenX + 8,
            "the flourish reaches past the advance and over the letter that follows");
    }
}
