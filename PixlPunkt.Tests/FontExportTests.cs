namespace PixlPunkt.Tests;

using System;
using System.Linq;
using FluentAssertions;
using PixlPunkt.Core.Document;
using Windows.Graphics;

/// <summary>
/// Writing a font out as a sprite sheet and BMFont metrics. The metrics file is what another
/// program reads to lay type out, so every number in it is checked rather than eyeballed.
/// </summary>
[TestFixture]
public class FontExportTests
{
    /// <summary>A 12x12-cell font holding an 8x8 em, four characters across one row.</summary>
    private static CanvasDocument NewFont(string chars = "ABCD", int cell = 12, int em = 8)
    {
        var doc = new CanvasDocument("f", chars.Length * cell, cell,
            new SizeInt32 { Width = cell, Height = cell },
            new SizeInt32 { Width = chars.Length, Height = 1 });

        var state = doc.FontState;
        state.HasState = true;
        state.FamilyName = "TestFace";
        state.SideBearing = 0;
        state.SetEmBox(em, em, cell, cell);
        state.SetDefaultGuides(cell);

        for (int i = 0; i < chars.Length; i++)
            state.GetOrAdd(chars[i]).CellIndex = i;

        return doc;
    }

    private static FontExportOptions Options(int scale = 1) =>
        new(scale, "TestFace", "testface_0.png");

    /// <summary>The value of one key on a given line, as a string.</summary>
    private static string Field(string bmfont, string linePrefix, string key)
    {
        string line = bmfont.Split('\n').First(l => l.StartsWith(linePrefix, StringComparison.Ordinal));
        int start = line.IndexOf(key + "=", StringComparison.Ordinal) + key.Length + 1;
        int end = line.IndexOf(' ', start);
        return end < 0 ? line[start..] : line[start..end];
    }

    // ── the metrics file ────────────────────────────────────────────

    [Test]
    public void TheInfoLineCarriesTheFaceAndTheEmSize()
    {
        string text = FontExportOps.BuildBMFont(NewFont(), Options());

        Field(text, "info", "face").Should().Be("\"TestFace\"");
        Field(text, "info", "size").Should().Be("8", "the em is the design size, not the cell");
        text.Should().Contain("smooth=0", "a pixel font must not be smoothed by whatever loads it");
        text.Should().Contain("unicode=1");
    }

    [Test]
    public void TheCommonLineDescribesTheLineAndTheSheet()
    {
        var doc = NewFont();
        string text = FontExportOps.BuildBMFont(doc, Options());

        Field(text, "common", "lineHeight").Should().Be("8", "baseline to baseline is the em plus the gap");
        Field(text, "common", "base").Should().Be("6", "the baseline measured from the top of the em");
        Field(text, "common", "scaleW").Should().Be("48");
        Field(text, "common", "scaleH").Should().Be("12");
        Field(text, "common", "pages").Should().Be("1");
    }

    [Test]
    public void EveryMappedCharacterGetsALine()
    {
        string text = FontExportOps.BuildBMFont(NewFont("ABCD"), Options());

        Field(text, "chars", "count").Should().Be("4");
        text.Split('\n').Count(l => l.StartsWith("char id=", StringComparison.Ordinal)).Should().Be(4);
    }

    [Test]
    public void ACharactersRegionIsItsWholeCell()
    {
        // Not the ink box: trimming would throw away ink that overhangs the advance, which is
        // exactly what the drawing room exists to allow.
        string text = FontExportOps.BuildBMFont(NewFont("ABCD"), Options());
        string third = text.Split('\n').First(l => l.StartsWith("char id=67", StringComparison.Ordinal));

        third.Should().Contain("x=24").And.Contain("y=0");
        third.Should().Contain("width=12").And.Contain("height=12");
    }

    [Test]
    public void TheVerticalOffsetLiftsTheCellByTheDrawingRoomAboveTheEm()
    {
        // A line's top is the top of the em, so a cell with two rows of room above it starts two
        // rows higher, which the format expects as a negative offset.
        string text = FontExportOps.BuildBMFont(NewFont(), Options());

        Field(text, "char id=65", "yoffset").Should().Be("-2");
    }

    [Test]
    public void SpacingSetByHandIsCarriedThrough()
    {
        var doc = NewFont();
        var a = doc.FontState.GetOrAdd('A');
        a.AutoFit = false;
        a.OriginX = 3;
        a.Advance = 6;

        string text = FontExportOps.BuildBMFont(doc, Options());

        Field(text, "char id=65", "xoffset").Should().Be("-3", "the cell is drawn at the pen less the origin");
        Field(text, "char id=65", "xadvance").Should().Be("6");
    }

    [Test]
    public void ScalingMultipliesEveryMeasurement()
    {
        var doc = NewFont();
        var a = doc.FontState.GetOrAdd('A');
        a.AutoFit = false;
        a.OriginX = 3;
        a.Advance = 6;

        string text = FontExportOps.BuildBMFont(doc, Options(scale: 3));

        Field(text, "info", "size").Should().Be("24");
        Field(text, "common", "lineHeight").Should().Be("24");
        Field(text, "common", "base").Should().Be("18");
        Field(text, "common", "scaleW").Should().Be("144");
        Field(text, "char id=65", "xadvance").Should().Be("18");
        Field(text, "char id=65", "xoffset").Should().Be("-9");
        Field(text, "char id=65", "yoffset").Should().Be("-6");
        Field(text, "char id=66", "x").Should().Be("36");
    }

    [Test]
    public void AKerningBlockIsWrittenEvenThoughThereIsNone()
    {
        FontExportOps.BuildBMFont(NewFont(), Options()).Should().Contain("kernings count=0");
    }

    [Test]
    public void AQuoteInTheFaceNameCannotBreakTheLine()
    {
        string text = FontExportOps.BuildBMFont(NewFont(), new FontExportOptions(1, "Odd\"Name", "p.png"));

        text.Split('\n')[0].Should().Be(
            "info face=\"OddName\" size=8 bold=0 italic=0 charset=\"\" unicode=1 stretchH=100 " +
            "smooth=0 aa=1 padding=0,0,0,0 spacing=0,0 outline=0");
    }

    // ── the sheet ───────────────────────────────────────────────────

    [Test]
    public void TheSheetIsTheDocumentAtScaleOne()
    {
        var doc = NewFont();
        doc.Surface.Pixels[0] = 200;

        var pixels = FontExportOps.RenderSheet(doc, 1);

        FontExportOps.SheetSize(doc, 1).Should().Be(new SizeInt32 { Width = 48, Height = 12 });
        pixels.Length.Should().Be(doc.Surface.Pixels.Length);
        pixels[0].Should().Be(200);
        pixels.Should().NotBeSameAs(doc.Surface.Pixels, "the caller must not be handed the live buffer");
    }

    [Test]
    public void ScalingRepeatsWholePixelsRatherThanBlendingThem()
    {
        var doc = NewFont("A", cell: 2, em: 2);
        var surface = doc.Surface;

        // One opaque red pixel at the top left of a 2x2 sheet.
        surface.Pixels[2] = 255;
        surface.Pixels[3] = 255;

        var pixels = FontExportOps.RenderSheet(doc, 2);

        FontExportOps.SheetSize(doc, 2).Should().Be(new SizeInt32 { Width = 4, Height = 4 });

        // That pixel becomes a solid 2x2 block, and nothing in between is half anything.
        foreach (var (x, y) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
        {
            int i = (y * 4 + x) * 4;
            pixels[i + 2].Should().Be(255, $"({x},{y}) is inside the block");
            pixels[i + 3].Should().Be(255);
        }

        pixels[((0 * 4) + 2) * 4 + 3].Should().Be(0, "(2,0) is outside the block and stays empty");
    }

    [Test]
    public void AScaleBelowOneIsTreatedAsOne()
    {
        var doc = NewFont();

        FontExportOps.SheetSize(doc, 0).Should().Be(new SizeInt32 { Width = 48, Height = 12 });
        FontExportOps.RenderSheet(doc, -3).Length.Should().Be(doc.Surface.Pixels.Length);
    }
}
