namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Selection;
using Windows.Graphics;

/// <summary>
/// Groups (one undo step for a multi-item gesture) and coalescing (a run of nudges is one step).
/// </summary>
[TestFixture]
public class HistoryGroupingTests
{
    private static CanvasDocument NewDoc() =>
        new("t", 64, 64, new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = 4, Height = 4 });

    private static void Paint(RasterLayer l, int x0, int y0, int w, int h, byte r)
    {
        var p = l.Surface.Pixels; int stride = l.Surface.Width * 4;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++) { int i = y * stride + x * 4; p[i + 2] = r; p[i + 3] = 255; }
    }

    private static byte Alpha(RasterLayer l, int x, int y) => l.Surface.Pixels[(y * l.Surface.Width + x) * 4 + 3];

    private static void Select(CanvasDocument doc, int x, int y, int w, int h)
    {
        doc.Selection.EnsureSize(doc.PixelWidth, doc.PixelHeight);
        doc.Selection.Clear();
        doc.Selection.AddRect(new RectInt32 { X = x, Y = y, Width = w, Height = h });
    }

    [Test]
    public void Group_TwoItems_UndoAsOne()
    {
        var doc = NewDoc(); var l = doc.Layers[0];
        Select(doc, 0, 0, 4, 4);

        doc.History.BeginGroup("Two changes");
        var r1 = doc.Selection.Clone(); Select(doc, 10, 10, 2, 2);
        doc.History.Push(new SelectionChangeItem(doc, SelectionChangeItem.SelectionChangeKind.Create, r1, doc.Selection.Clone()));
        var r2 = doc.Selection.Clone(); Select(doc, 20, 20, 2, 2);
        doc.History.Push(new SelectionChangeItem(doc, SelectionChangeItem.SelectionChangeKind.Create, r2, doc.Selection.Clone()));
        doc.History.EndGroup();

        doc.History.UndoCount.Should().Be(1, "the group is a single step");
        doc.History.UndoDescription.Should().Be("Two changes");

        doc.History.Undo();
        doc.Selection.Contains(1, 1).Should().BeTrue("both changes undone together");
        doc.Selection.Contains(21, 21).Should().BeFalse();

        doc.History.Redo();
        doc.Selection.Contains(21, 21).Should().BeTrue();
    }

    [Test]
    public void Group_SingleItem_PushesTheItemItself()
    {
        var doc = NewDoc();
        Select(doc, 0, 0, 4, 4);
        doc.History.BeginGroup("Solo");
        var r1 = doc.Selection.Clone(); Select(doc, 10, 10, 2, 2);
        doc.History.Push(new SelectionChangeItem(doc, SelectionChangeItem.SelectionChangeKind.Create, r1, doc.Selection.Clone()));
        doc.History.EndGroup();

        doc.History.PeekUndo().Should().BeOfType<SelectionChangeItem>();
    }

    [Test]
    public void CancelGroup_RollsBackCollectedItems()
    {
        var doc = NewDoc();
        Select(doc, 0, 0, 4, 4);
        doc.History.BeginGroup("Aborted");
        var r1 = doc.Selection.Clone(); Select(doc, 10, 10, 2, 2);
        doc.History.Push(new SelectionChangeItem(doc, SelectionChangeItem.SelectionChangeKind.Create, r1, doc.Selection.Clone()));
        doc.History.CancelGroup();

        doc.History.UndoCount.Should().Be(0);
        doc.Selection.Contains(1, 1).Should().BeTrue("the change inside the aborted group was undone");
    }

    [Test]
    public void Nudges_CoalesceIntoOneMoveStep()
    {
        var doc = NewDoc(); var l = doc.Layers[0];
        Paint(l, 5, 5, 10, 10, 255);
        Select(doc, 5, 5, 10, 10);
        doc.History.Push(FloatingSelectionOps.Lift(doc, l)!);

        for (int i = 0; i < 5; i++)
        {
            var before = SelectionTransformItem.Capture(doc.Floating!, false);
            doc.Floating!.X += 1; doc.Floating.OrigCenterX += 1;
            doc.History.Push(new SelectionTransformItem(doc, SelectionTransformItem.TransformKind.Move, before, SelectionTransformItem.Capture(doc.Floating, false)));
        }

        doc.History.UndoCount.Should().Be(2, "lift + one coalesced move");
        doc.Floating!.X.Should().Be(10);

        doc.History.Undo();
        doc.Floating!.X.Should().Be(5, "one undo reverts the whole run of nudges");
    }

    [Test]
    public void Coalescing_StopsAcrossADifferentItem()
    {
        var doc = NewDoc(); var l = doc.Layers[0];
        Paint(l, 5, 5, 10, 10, 255);
        Select(doc, 5, 5, 10, 10);
        doc.History.Push(FloatingSelectionOps.Lift(doc, l)!);

        var b1 = SelectionTransformItem.Capture(doc.Floating!, false); doc.Floating!.X += 1;
        doc.History.Push(new SelectionTransformItem(doc, SelectionTransformItem.TransformKind.Move, b1, SelectionTransformItem.Capture(doc.Floating, false)));

        var b2 = SelectionTransformItem.Capture(doc.Floating, true); doc.Floating.ScaleX = 2.0;
        doc.History.Push(new SelectionTransformItem(doc, SelectionTransformItem.TransformKind.Scale, b2, SelectionTransformItem.Capture(doc.Floating, true)));

        var b3 = SelectionTransformItem.Capture(doc.Floating, false); doc.Floating.X += 1;
        doc.History.Push(new SelectionTransformItem(doc, SelectionTransformItem.TransformKind.Move, b3, SelectionTransformItem.Capture(doc.Floating, false)));

        doc.History.UndoCount.Should().Be(4, "move, scale, move do not merge across kinds");
    }

    [Test]
    public void Coalescing_DoesNotReachAcrossRedoBoundary()
    {
        var doc = NewDoc(); var l = doc.Layers[0];
        Paint(l, 5, 5, 10, 10, 255);
        Select(doc, 5, 5, 10, 10);
        doc.History.Push(FloatingSelectionOps.Lift(doc, l)!);

        var b1 = SelectionTransformItem.Capture(doc.Floating!, false); doc.Floating!.X += 1;
        doc.History.Push(new SelectionTransformItem(doc, SelectionTransformItem.TransformKind.Move, b1, SelectionTransformItem.Capture(doc.Floating, false)));
        doc.History.Undo();
        doc.History.Redo();

        var b2 = SelectionTransformItem.Capture(doc.Floating!, false); doc.Floating!.X += 1;
        doc.History.Push(new SelectionTransformItem(doc, SelectionTransformItem.TransformKind.Move, b2, SelectionTransformItem.Capture(doc.Floating, false)));

        // After an undo/redo the top item is the same object, so it still merges (the redo stack is empty).
        doc.History.UndoCount.Should().Be(2);
        doc.History.Undo();
        doc.Floating!.X.Should().Be(5);
    }
}
