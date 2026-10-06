namespace PixlPunkt.Tests;

using System.Linq;
using FluentAssertions;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Symmetry;

/// <summary>
/// Mirroring a painted pixel across a symmetry axis.
/// </summary>
/// <remarks>
/// The axis sits on a pixel edge while a painted pixel is counted by index, so the reflection has
/// to be of the pixel's centre. Reflecting the index instead lands a pixel too far, which paints a
/// one pixel gap beside the axis going one way and an overlap going the other. The tell is that
/// mirroring twice does not return where it started.
/// </remarks>
[TestFixture]
public class SymmetryMirrorTests
{
    private const int W = 16;
    private const int H = 16;

    private static SymmetryService Service(SymmetryMode mode, double axisX = 0.5, double axisY = 0.5)
    {
        var settings = new SymmetrySettings();
        settings.SetMode(mode);
        settings.Enabled = true;
        settings.SetAxisPosition(axisX, axisY);
        return new SymmetryService(settings);
    }

    /// <summary>
    /// The mirrored points only. The service always yields the painted pixel itself first, so the
    /// stroke engine can treat the whole set the same way; the reflections are what is under test.
    /// </summary>
    private static (int x, int y)[] Points(SymmetryService s, int x, int y) =>
        s.GetSymmetryPoints(x, y, W, H).Skip(1).ToArray();

    // ── across a vertical axis ──────────────────────────────────────

    [Test]
    public void TheTwoPixelsEitherSideOfTheAxisMapToEachOther()
    {
        // The reported bug. With a 16 wide canvas the axis sits between 7 and 8, so painting 7
        // must mark 8 and nothing in between.
        var mirror = Service(SymmetryMode.Horizontal);

        Points(mirror, 7, 4).Should().ContainSingle().Which.Should().Be((8, 4));
        Points(mirror, 8, 4).Should().ContainSingle().Which.Should().Be((7, 4));
    }

    [Test]
    public void TheEdgesMapToEachOther()
    {
        var mirror = Service(SymmetryMode.Horizontal);

        Points(mirror, 0, 3).Should().ContainSingle().Which.Should().Be((W - 1, 3));
        Points(mirror, W - 1, 3).Should().ContainSingle().Which.Should().Be((0, 3));
    }

    [Test]
    public void MirroringTwiceReturnsToTheOriginal()
    {
        // The property the old arithmetic broke: it drifted a pixel further each time.
        var mirror = Service(SymmetryMode.Horizontal);

        for (int x = 0; x < W; x++)
        {
            var once = Points(mirror, x, 5).Single();
            var twice = Points(mirror, once.x, once.y).Single();
            twice.Should().Be((x, 5), $"x of {x} must come back to itself");
        }
    }

    [Test]
    public void EveryMirroredPixelStaysOnTheCanvas()
    {
        var mirror = Service(SymmetryMode.Horizontal);

        for (int x = 0; x < W; x++)
        {
            var p = Points(mirror, x, 5).Single();
            p.x.Should().BeInRange(0, W - 1, $"x of {x} mirrored off the canvas");
        }
    }

    // ── across a horizontal axis ────────────────────────────────────

    [Test]
    public void TopAndBottomMirrorWithoutAGap()
    {
        var mirror = Service(SymmetryMode.Vertical);

        Points(mirror, 4, 7).Should().ContainSingle().Which.Should().Be((4, 8));
        Points(mirror, 4, 8).Should().ContainSingle().Which.Should().Be((4, 7));
        Points(mirror, 4, 0).Should().ContainSingle().Which.Should().Be((4, H - 1));
    }

    [Test]
    public void VerticalMirroringAlsoReturnsToTheOriginal()
    {
        var mirror = Service(SymmetryMode.Vertical);

        for (int y = 0; y < H; y++)
        {
            var once = Points(mirror, 6, y).Single();
            var twice = Points(mirror, once.x, once.y).Single();
            twice.Should().Be((6, y));
        }
    }

    // ── both axes at once ───────────────────────────────────────────

    [Test]
    public void BothAxesGiveTheOtherThreeQuadrants()
    {
        var mirror = Service(SymmetryMode.Both);

        var points = Points(mirror, 2, 3);

        points.Should().HaveCount(3);
        points.Should().BeEquivalentTo(new[] { (13, 3), (2, 12), (13, 12) });
    }

    [Test]
    public void BothAxesAreSymmetricNextToTheCrossing()
    {
        var mirror = Service(SymmetryMode.Both);

        Points(mirror, 7, 7).Should().BeEquivalentTo(new[] { (8, 7), (7, 8), (8, 8) },
            "the four pixels meeting at the crossing are each other's mirrors");
    }

    // ── an axis that is not in the middle ───────────────────────────

    [Test]
    public void AnOffCentreAxisStillReflectsAboutItself()
    {
        // A quarter of the way across a 16 wide canvas puts the axis between 3 and 4.
        var mirror = Service(SymmetryMode.Horizontal, axisX: 0.25);

        Points(mirror, 3, 1).Should().ContainSingle().Which.Should().Be((4, 1));
        Points(mirror, 4, 1).Should().ContainSingle().Which.Should().Be((3, 1));
        Points(mirror, 0, 1).Should().ContainSingle().Which.Should().Be((7, 1));
    }

    [Test]
    public void AnAxisOnAPixelEdgeRatherThanBetweenTwo()
    {
        // Three sixteenths lands the axis inside a pixel rather than on a boundary, which is the
        // case the rounding has to settle rather than drift.
        var mirror = Service(SymmetryMode.Horizontal, axisX: 3.0 / 16.0);

        var once = Points(mirror, 1, 1).Single();
        Points(mirror, once.x, once.y).Single().Should().Be((1, 1), "still its own inverse");
    }

    // ── switched off ────────────────────────────────────────────────

    [Test]
    public void NothingIsMirroredWhenSymmetryIsOff()
    {
        var settings = new SymmetrySettings();
        settings.SetMode(SymmetryMode.Horizontal);
        settings.Enabled = false;

        var points = new SymmetryService(settings).GetSymmetryPoints(3, 3, W, H).ToArray();

        points.Should().ContainSingle().Which.Should().Be((3, 3),
            "the painted pixel is still reported, just with nothing reflected");
    }
}
