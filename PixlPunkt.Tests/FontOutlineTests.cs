namespace PixlPunkt.Tests;

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using PixlPunkt.Core.Document;

/// <summary>
/// Tracing pixels into outlines. This is the one part of the font work that can look right in a
/// preview and still come out wrong in another program, because nothing here is drawn on screen, so
/// the shapes are checked as coordinates and the winding is checked by signed area.
/// </summary>
[TestFixture]
public class FontOutlineTests
{
    /// <summary>Builds a mask from rows of text, where '#' is filled and anything else is not.</summary>
    private static (bool[] Mask, int Width, int Height) MaskOf(params string[] rows)
    {
        int width = rows[0].Length;
        var mask = new bool[width * rows.Length];

        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < width; x++)
                mask[y * width + x] = rows[y][x] == '#';

        return (mask, width, rows.Length);
    }

    /// <summary>Traces at one unit per pixel with the origin at the top left, so points read as pixels.</summary>
    private static List<List<OutlinePoint>> TraceRaw(params string[] rows)
    {
        var (mask, width, height) = MaskOf(rows);
        return FontOutlineOps.Trace(mask, width, height, unitsPerPixel: 1, baselineRow: 0, originColumn: 0);
    }

    private static HashSet<(int X, int Y)> PointsOf(List<OutlinePoint> contour) =>
        contour.Select(p => (p.X, p.Y)).ToHashSet();

    // ── units per em ────────────────────────────────────────────────

    [Test]
    public void UnitsPerEmIsAlwaysAWholeMultipleOfThePixelGrid()
    {
        // The point of the whole exercise: the em must divide into pixels exactly.
        foreach (int em in new[] { 5, 6, 7, 8, 9, 11, 12, 16, 22, 32 })
        {
            int perPixel = FontOutlineOps.ChooseUnitsPerPixel(em);
            int upm = FontOutlineOps.UnitsPerEm(em);

            (upm % em).Should().Be(0, $"an em of {em} px must divide its units per em exactly");
            upm.Should().Be(em * perPixel);
        }
    }

    [Test]
    public void UnitsPerPixelLandsNearTheTarget()
    {
        FontOutlineOps.ChooseUnitsPerPixel(8).Should().Be(128);
        FontOutlineOps.UnitsPerEm(8).Should().Be(1024, "eight pixels at 128 units each");

        FontOutlineOps.UnitsPerEm(16).Should().Be(1024);
        FontOutlineOps.UnitsPerEm(6).Should().Be(1026,
            "1024 is not a multiple of six, so it settles just above rather than rounding the em");
    }

    [Test]
    public void TheEmStaysInsideWhatAFontCanStore()
    {
        FontOutlineOps.UnitsPerEm(128).Should().BeLessThanOrEqualTo(16384);
        FontOutlineOps.ChooseUnitsPerPixel(0).Should().BeGreaterThan(0, "a zero em must not divide by zero");
    }

    // ── tracing ─────────────────────────────────────────────────────

    [Test]
    public void NothingDrawnTracesToNothing()
    {
        TraceRaw(
            "...",
            "...").Should().BeEmpty();
    }

    [Test]
    public void OnePixelBecomesOneSquareOfFourCorners()
    {
        var contours = TraceRaw(
            "...",
            ".#.",
            "...");

        contours.Should().ContainSingle();
        contours[0].Should().HaveCount(4, "a square has four corners and no more");
        PointsOf(contours[0]).Should().BeEquivalentTo(new[] { (1, -1), (2, -1), (2, -2), (1, -2) });
    }

    [Test]
    public void ABlockOfPixelsBecomesOneRectangle_NotOnePerPixel()
    {
        // The inner edges cancel, which is the whole reason for tracing by cancellation.
        var contours = TraceRaw(
            "##",
            "##");

        contours.Should().ContainSingle();
        contours[0].Should().HaveCount(4);
    }

    [Test]
    public void AnLShapeKeepsItsSixCorners()
    {
        var contours = TraceRaw(
            "#.",
            "#.",
            "##");

        contours.Should().ContainSingle();
        contours[0].Should().HaveCount(6, "an L has six corners once the straight runs are dropped");
    }

    [Test]
    public void AHoleBecomesASecondContourWoundTheOtherWay()
    {
        var contours = TraceRaw(
            "###",
            "#.#",
            "###");

        contours.Should().HaveCount(2);

        var outer = contours.OrderByDescending(c => c.Max(p => p.X) - c.Min(p => p.X)).First();
        var hole = contours.First(c => c != outer);

        FontOutlineOps.SignedArea(outer).Should().BeNegative("an outer contour runs clockwise with y upward");
        FontOutlineOps.SignedArea(hole).Should().BePositive("a hole runs the other way, so it is cut out");
    }

    [Test]
    public void TwoSeparateShapesBecomeTwoContours()
    {
        var contours = TraceRaw(
            "#.#",
            "...",
            "#.#");

        contours.Should().HaveCount(4, "four pixels, none touching");
    }

    [Test]
    public void PixelsMeetingOnlyAtACornerStayTwoSeparateLoops()
    {
        // The awkward case: four edges arrive at one point and the walk has to choose. Pinching
        // them into a single self-crossing loop would fill the wrong area.
        var contours = TraceRaw(
            "#.",
            ".#");

        contours.Should().HaveCount(2);
        contours.Should().AllSatisfy(c => c.Should().HaveCount(4));
        contours.Should().AllSatisfy(c => FontOutlineOps.SignedArea(c).Should().BeNegative());
    }

    [Test]
    public void EveryContourOfASolidShapeIsWoundClockwise()
    {
        foreach (var contour in TraceRaw(
            ".##.",
            "####",
            "####",
            ".##."))
        {
            FontOutlineOps.SignedArea(contour).Should().BeNegative();
        }
    }

    // ── placing it against the baseline and the pen ──────────────────

    [Test]
    public void TheBaselineBecomesYOfZeroAndTheOriginBecomesXOfZero()
    {
        var (mask, width, height) = MaskOf(
            "....",
            "....",
            ".#..",
            "....");

        // Baseline on row 3, pen on column 1: the pixel sits directly on the baseline at the pen.
        var contours = FontOutlineOps.Trace(mask, width, height, unitsPerPixel: 1, baselineRow: 3, originColumn: 1);

        PointsOf(contours[0]).Should().BeEquivalentTo(new[] { (0, 1), (1, 1), (1, 0), (0, 0) },
            "the pixel occupies x 0 to 1 and sits on the baseline");
    }

    [Test]
    public void InkBelowTheBaselineComesOutNegative()
    {
        var (mask, width, height) = MaskOf(
            "..",
            "#.");

        var contours = FontOutlineOps.Trace(mask, width, height, unitsPerPixel: 1, baselineRow: 1, originColumn: 0);

        contours[0].Min(p => p.Y).Should().Be(-1, "a descender hangs below the baseline");
    }

    [Test]
    public void UnitsPerPixelScalesEveryCoordinate()
    {
        var (mask, width, height) = MaskOf(
            ".#",
            "..");

        var contours = FontOutlineOps.Trace(mask, width, height, unitsPerPixel: 128, baselineRow: 2, originColumn: 0);

        PointsOf(contours[0]).Should().BeEquivalentTo(new[] { (128, 256), (256, 256), (256, 128), (128, 128) });
    }

    // ── against a real document ─────────────────────────────────────

    [Test]
    public void AGlyphIsTracedFromItsOwnCell()
    {
        var doc = new CanvasDocument("f", 16, 8,
            new Windows.Graphics.SizeInt32 { Width = 8, Height = 8 },
            new Windows.Graphics.SizeInt32 { Width = 2, Height = 1 });

        var state = doc.FontState;
        state.HasState = true;
        state.SideBearing = 0;
        state.SetDefaultGuides(8);
        state.GetOrAdd('A').CellIndex = 0;
        state.GetOrAdd('B').CellIndex = 1;

        // One pixel in B's cell only, so tracing A must find nothing.
        var surface = doc.Surface;
        surface.Pixels[((2 * surface.Width) + 9) * 4 + 3] = 255;

        FontOutlineOps.TraceGlyph(doc, 'A', 1).Should().BeEmpty("A's cell is empty");
        FontOutlineOps.TraceGlyph(doc, 'B', 1).Should().ContainSingle("B has one pixel");
    }

    [Test]
    public void AnUnmappedCharacterTracesToNothing()
    {
        var doc = new CanvasDocument("f", 8, 8,
            new Windows.Graphics.SizeInt32 { Width = 8, Height = 8 },
            new Windows.Graphics.SizeInt32 { Width = 1, Height = 1 });
        doc.FontState.HasState = true;

        FontOutlineOps.TraceGlyph(doc, 'Z', 128).Should().BeEmpty();
    }
}
