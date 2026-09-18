namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Selection;
using PixlPunkt.UI.CanvasHost.Selection;
using Windows.Graphics;
using static PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem;

/// <summary>
/// Two consecutive scale drags with bake-on-release, driven the way the canvas host does it;
/// after each bake the region must cover the whole float.
/// </summary>
[TestFixture]
public class SelectionTwoScaleTests
{
    private static CanvasDocument NewDoc(int size = 64) =>
        new("t", size, size, new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = size / 16, Height = size / 16 });

    private static (CanvasDocument doc, SelectionSubsystem s, SelectionTransformOps ops) Lift(int size, int x, int y, int w, int h)
    {
        var doc = NewDoc(size);
        doc.Selection.EnsureSize(doc.PixelWidth, doc.PixelHeight);
        doc.Selection.Clear();
        doc.Selection.AddRect(new RectInt32 { X = x, Y = y, Width = w, Height = h });
        doc.History.Push(FloatingSelectionOps.Lift(doc, doc.Layers[0])!);
        var s = new SelectionSubsystem(doc.Selection) { Lifted = doc.Floating, Active = true, State = SelectionState.Armed };
        return (doc, s, new SelectionTransformOps(s));
    }

    private static void DragEast(SelectionSubsystem s, SelectionTransformOps ops, int toX)
    {
        s.Drag = SelDrag.Scale; s.ActiveHandle = SelHandle.E;
        s.ScaleStartFX = s.FloatX; s.ScaleStartFY = s.FloatY;
        s.ScaleStartW = s.ScaledW; s.ScaleStartH = s.ScaledH;
        ops.UpdateScaleFromHandle(toX, s.FloatY + s.ScaledH / 2);
    }

    /// <summary>BakeTransformsOnRelease, scale part, as the host does it.</summary>
    private static void Bake(CanvasDocument doc, SelectionSubsystem s)
    {
        int cx = s.OrigCenterX, cy = s.OrigCenterY;
        var (buf, tw, th) = SelectionBufferOps.BuildScaled(s.Buffer!, s.BufferWidth, s.BufferHeight, s.ScaleX, s.ScaleY, s.ScaleFilter);
        s.Lifted!.ResampleMask(tw, th);
        s.OrigW = tw; s.OrigH = th;
        s.Buffer = buf; s.BufferWidth = tw; s.BufferHeight = th;
        s.FloatX = cx - tw / 2; s.FloatY = cy - th / 2;
        s.ScaleX = 1.0; s.ScaleY = 1.0;
        s.PreviewBuf = null;
        SelectionRegionBuilders.RebuildFromMask(doc.Selection, s.Lifted, doc.PixelWidth, doc.PixelHeight);
        s.Rect = doc.Selection.Bounds;
        s.Drag = SelDrag.None;
    }

    private static void RegionCoversFloat(CanvasDocument doc, SelectionSubsystem s, string because)
    {
        int x0 = s.FloatX, y0 = s.FloatY, x1 = s.FloatX + s.BufferWidth - 1, y1 = s.FloatY + s.BufferHeight - 1;
        foreach (var (x, y) in new[] { (x0, y0), (x1, y0), (x0, y1), (x1, y1), ((x0 + x1) / 2, (y0 + y1) / 2) })
            doc.Selection.Contains(x, y).Should().BeTrue($"({x},{y}) is inside the float; {because}");
        doc.Selection.Contains(x1 + 1, y0).Should().BeFalse();
        doc.Selection.Contains(x0 - 1, y0).Should().BeFalse();
    }

    [Test]
    public void TwoScales_RegionCoversTheFloatBothTimes()
    {
        var (doc, s, ops) = Lift(64, 10, 10, 10, 10);

        DragEast(s, ops, 25);            // 10 -> 15 wide
        Bake(doc, s);
        s.BufferWidth.Should().Be(15);
        RegionCoversFloat(doc, s, "after the first bake");

        DragEast(s, ops, s.FloatX + 15 + 5);  // 15 -> 20 wide
        Bake(doc, s);
        s.BufferWidth.Should().Be(20);
        RegionCoversFloat(doc, s, "after the second bake");
    }

    [Test]
    public void ScaledBeyondTheCanvas_RegionStillCoversTheFloat()
    {
        var (doc, s, ops) = Lift(32, 4, 4, 24, 24);

        DragEast(s, ops, 4 + 40);        // 24 -> 40 wide on a 32 canvas
        Bake(doc, s);
        s.BufferWidth.Should().Be(40);
        RegionCoversFloat(doc, s, "the float is wider than the canvas");
    }
}
