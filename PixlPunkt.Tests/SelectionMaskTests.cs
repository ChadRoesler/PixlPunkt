namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Selection;
using Windows.Graphics;

/// <summary>
/// Stage 1 of selection-as-shape: the float carries its marquee as a mask that follows the
/// pixels through paste, lift, scale bake, flips and undo snapshots. Nothing reads it yet.
/// </summary>
[TestFixture]
public class SelectionMaskTests
{
    private static CanvasDocument NewDoc() =>
        new("t", 64, 64, new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = 4, Height = 4 });

    private static RectInt32 R(int x, int y, int w, int h) => new() { X = x, Y = y, Width = w, Height = h };

    private static void Select(CanvasDocument doc, params RectInt32[] rects)
    {
        doc.Selection.EnsureSize(doc.PixelWidth, doc.PixelHeight);
        doc.Selection.Clear();
        foreach (var r in rects) doc.Selection.AddRect(r);
    }

    private static FloatingSelection LiftL(CanvasDocument doc)
    {
        Select(doc, R(10, 10, 4, 2), R(10, 12, 2, 2));     // L: 4x2 bar, 2x2 leg bottom-left
        doc.History.Push(FloatingSelectionOps.Lift(doc, doc.Layers[0])!);
        return doc.Floating!;
    }

    [Test]
    public void Paste_MaskIsAllOnes()
    {
        var doc = NewDoc();
        doc.History.Push(FloatingSelectionOps.Paste(doc, doc.Layers[0], new byte[3 * 3 * 4], 3, 3, 5, 5));
        doc.Floating!.Mask.Should().HaveCount(9).And.AllBeEquivalentTo((byte)1);
    }

    [Test]
    public void Lift_MaskIsTheMarquee_NotPixelAlpha()
    {
        var f = LiftL(NewDoc());                           // the layer is blank: alpha is 0 everywhere
        f.Width.Should().Be(4); f.Height.Should().Be(4);
        f.Mask[0 * 4 + 3].Should().Be(1, "bar, top-right");
        f.Mask[3 * 4 + 0].Should().Be(1, "leg, bottom-left");
        f.Mask[3 * 4 + 3].Should().Be(0, "the notch");
        f.Pixels.Should().AllBeEquivalentTo((byte)0);
    }

    [Test]
    public void ResampleMask_FollowsAScaleBake()
    {
        var f = LiftL(NewDoc());
        f.ResampleMask(8, 8);
        f.Mask.Should().HaveCount(64);
        f.Mask[0 * 8 + 7].Should().Be(1); f.Mask[7 * 8 + 0].Should().Be(1); f.Mask[7 * 8 + 7].Should().Be(0);
        f.Mask[4 * 8 + 5].Should().Be(0, "the notch scales with the shape");
    }

    [Test]
    public void Flips_MirrorTheMask()
    {
        var f = LiftL(NewDoc());
        f.FlipMaskHorizontal();
        f.Mask[3 * 4 + 3].Should().Be(1); f.Mask[3 * 4 + 0].Should().Be(0);
        f.FlipMaskVertical();
        f.Mask[0 * 4 + 3].Should().Be(1); f.Mask[3 * 4 + 3].Should().Be(1, "bar moved to the bottom");
        f.Mask[0 * 4 + 0].Should().Be(0);
    }

    [Test]
    public void Snapshot_WithBuffer_RestoresTheMask()
    {
        var f = LiftL(NewDoc());
        var before = SelectionTransformItem.Capture(f, includeBuffer: true);
        f.FlipMaskHorizontal();
        SelectionTransformItem.ApplyTo(f, before);
        f.Mask[3 * 4 + 0].Should().Be(1); f.Mask[3 * 4 + 3].Should().Be(0);
    }

    [Test]
    public void Clone_CopiesTheMask()
    {
        var f = LiftL(NewDoc());
        var c = f.Clone();
        c.Mask.Should().Equal(f.Mask);
        c.Mask.Should().NotBeSameAs(f.Mask);
    }
}
