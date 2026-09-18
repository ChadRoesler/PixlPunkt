namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Selection;
using Windows.Graphics;

/// <summary>
/// The floating-selection lifecycle as pure document operations: lift, transform, commit,
/// cancel, delete, paste, each a history item, each round-tripping through undo/redo with no
/// view involved. This is the property the old design could not have: there is no state the
/// history stack does not know about.
/// </summary>
[TestFixture]
public class SelectionHistoryTests
{
    private const uint Red = 0xFF0000FF;   // BGRA bytes B=00 G=00 R=FF A=FF → as uint 0xFFFF0000? keep explicit below

    private static CanvasDocument NewDoc()
    {
        var doc = new CanvasDocument("t", 64, 64, new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = 4, Height = 4 });
        return doc;
    }

    private static RasterLayer Layer(CanvasDocument doc) => doc.Layers[0];

    private static void Paint(RasterLayer l, int x0, int y0, int w, int h, byte b, byte g, byte r)
    {
        var p = l.Surface.Pixels; int stride = l.Surface.Width * 4;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                int i = y * stride + x * 4;
                p[i] = b; p[i + 1] = g; p[i + 2] = r; p[i + 3] = 255;
            }
    }

    private static (byte b, byte g, byte r, byte a) Px(RasterLayer l, int x, int y)
    {
        var p = l.Surface.Pixels; int i = (y * l.Surface.Width + x) * 4;
        return (p[i], p[i + 1], p[i + 2], p[i + 3]);
    }

    private static void Select(CanvasDocument doc, int x, int y, int w, int h)
    {
        doc.Selection.EnsureSize(doc.PixelWidth, doc.PixelHeight);
        doc.Selection.Clear();
        doc.Selection.AddRect(new RectInt32 { X = x, Y = y, Width = w, Height = h });
    }

    [Test]
    public void Lift_ClearsLayerAndCreatesFloating_AndUndoRestoresBoth()
    {
        var doc = NewDoc(); var l = Layer(doc);
        Paint(l, 5, 5, 10, 10, 0, 0, 255);
        Select(doc, 5, 5, 10, 10);

        var lift = FloatingSelectionOps.Lift(doc, l);
        lift.Should().NotBeNull();
        doc.History.Push(lift!);

        Px(l, 7, 7).a.Should().Be(0, "lifted pixels are cleared on the layer");
        doc.Floating.Should().NotBeNull();
        doc.Floating!.Layer.Should().BeSameAs(l);
        doc.Floating.Pixels[(2 * 10 + 2) * 4 + 2].Should().Be(255, "the floating buffer holds the red pixels");
        doc.Floating.HasSource.Should().BeTrue();

        doc.History.Undo();
        Px(l, 7, 7).Should().Be(((byte)0, (byte)0, (byte)255, (byte)255));
        doc.Floating.Should().BeNull();

        doc.History.Redo();
        Px(l, 7, 7).a.Should().Be(0);
        doc.Floating.Should().NotBeNull();
    }

    [Test]
    public void Lift_Move_Commit_RoundTripsThroughUndoRedo_WithNoHole()
    {
        var doc = NewDoc(); var l = Layer(doc);
        Paint(l, 5, 5, 10, 10, 0, 0, 255);
        Select(doc, 5, 5, 10, 10);

        doc.History.Push(FloatingSelectionOps.Lift(doc, l)!);

        // Move it 20px right, recorded exactly as the view does it.
        var before = SelectionTransformItem.Capture(doc.Floating!, includeBuffer: false);
        doc.Floating!.X += 20; doc.Floating.OrigCenterX += 20;
        var after = SelectionTransformItem.Capture(doc.Floating, includeBuffer: false);
        doc.History.Push(new SelectionTransformItem(doc, SelectionTransformItem.TransformKind.Move, before, after));

        doc.History.Push(FloatingSelectionOps.Commit(doc)!);

        Px(l, 27, 7).Should().Be(((byte)0, (byte)0, (byte)255, (byte)255), "committed at the new position");
        Px(l, 7, 7).a.Should().Be(0, "the original spot stays empty after a move");
        doc.Floating.Should().BeNull();
        doc.Selection.Contains(27, 7).Should().BeTrue("the marquee follows the committed pixels");

        // Undo commit → floating again at the moved position, destination cleared
        doc.History.Undo();
        doc.Floating.Should().NotBeNull();
        doc.Floating!.X.Should().Be(25);
        Px(l, 27, 7).a.Should().Be(0);

        // Undo move → floating back at origin
        doc.History.Undo();
        doc.Floating!.X.Should().Be(5);
        doc.Selection.Contains(7, 7).Should().BeTrue();

        // Undo lift → pixels back, nothing floating. THIS is the step that used to leave a hole.
        doc.History.Undo();
        doc.Floating.Should().BeNull();
        Px(l, 7, 7).Should().Be(((byte)0, (byte)0, (byte)255, (byte)255));

        // Redo everything
        doc.History.Redo(); doc.History.Redo(); doc.History.Redo();
        Px(l, 27, 7).Should().Be(((byte)0, (byte)0, (byte)255, (byte)255));
        Px(l, 7, 7).a.Should().Be(0);
        doc.Floating.Should().BeNull();
    }

    [Test]
    public void Cancel_PutsPixelsBack_AndIsUndoable()
    {
        var doc = NewDoc(); var l = Layer(doc);
        Paint(l, 5, 5, 10, 10, 0, 0, 255);
        Select(doc, 5, 5, 10, 10);
        doc.History.Push(FloatingSelectionOps.Lift(doc, l)!);
        doc.Floating!.X += 30; // moved, then cancelled: pixels return to the ORIGINAL spot

        doc.History.Push(FloatingSelectionOps.Cancel(doc)!);

        Px(l, 7, 7).Should().Be(((byte)0, (byte)0, (byte)255, (byte)255));
        Px(l, 37, 7).a.Should().Be(0);
        doc.Floating.Should().BeNull();
        doc.Selection.IsEmpty.Should().BeTrue();

        doc.History.Undo();
        doc.Floating.Should().NotBeNull();
        doc.Floating!.X.Should().Be(35);
        Px(l, 7, 7).a.Should().Be(0, "undoing the cancel re-lifts");

        doc.History.Redo();
        Px(l, 7, 7).a.Should().Be(255);
        doc.Floating.Should().BeNull();
    }

    [Test]
    public void Delete_WhileFloating_LeavesHoleButIsUndoable()
    {
        var doc = NewDoc(); var l = Layer(doc);
        Paint(l, 5, 5, 10, 10, 0, 0, 255);
        Select(doc, 5, 5, 10, 10);
        doc.History.Push(FloatingSelectionOps.Lift(doc, l)!);

        doc.History.Push(FloatingSelectionOps.Discard(doc)!);
        Px(l, 7, 7).a.Should().Be(0);
        doc.Floating.Should().BeNull();

        doc.History.Undo();                 // floating comes back
        doc.Floating.Should().NotBeNull();
        doc.History.Undo();                 // lift undone → pixels back
        Px(l, 7, 7).a.Should().Be(255);
    }

    [Test]
    public void Paste_CreatesFloating_UndoRemovesIt_RestoringPreviousMask()
    {
        var doc = NewDoc(); var l = Layer(doc);
        Select(doc, 0, 0, 4, 4);
        var buf = new byte[3 * 3 * 4]; for (int i = 3; i < buf.Length; i += 4) buf[i] = 255;

        doc.History.Push(FloatingSelectionOps.Paste(doc, l, buf, 3, 3, 20, 20));
        doc.Floating.Should().NotBeNull();
        doc.Floating!.HasSource.Should().BeFalse();
        doc.Selection.Contains(21, 21).Should().BeTrue();

        doc.History.Undo();
        doc.Floating.Should().BeNull();
        doc.Selection.Contains(21, 21).Should().BeFalse();
        doc.Selection.Contains(1, 1).Should().BeTrue("the pre-paste marquee is back");
    }

    [Test]
    public void LayerBinding_CommitGoesToSourceLayer_EvenIfActiveLayerChanged()
    {
        var doc = NewDoc(); var a = Layer(doc);
        Paint(a, 5, 5, 10, 10, 0, 0, 255);
        Select(doc, 5, 5, 10, 10);
        doc.History.Push(FloatingSelectionOps.Lift(doc, a)!);

        int bIdx = doc.AddLayer("B");
        var b = doc.Layers[bIdx];
        doc.ActiveLayer.Should().BeSameAs(b);

        doc.History.Push(FloatingSelectionOps.Commit(doc)!);

        Px(a, 7, 7).a.Should().Be(255, "pixels return to the layer they were lifted from");
        Px(b, 7, 7).a.Should().Be(0, "the newly active layer is untouched");
    }

    [Test]
    public void SaveWhileFloating_WritesWhatTheUserSees_WithoutMutatingTheDocument()
    {
        var doc = NewDoc(); var l = Layer(doc);
        Paint(l, 5, 5, 10, 10, 0, 0, 255);
        Select(doc, 5, 5, 10, 10);
        doc.History.Push(FloatingSelectionOps.Lift(doc, l)!);
        doc.Floating!.X += 20; doc.Floating.OrigCenterX += 20;

        var bytes = DocumentIO.SaveToBytes(doc);

        doc.Floating.Should().NotBeNull("saving must not commit");
        Px(l, 27, 7).a.Should().Be(0, "saving must not mutate the layer");

        var loaded = DocumentIO.Load(new MemoryStream(bytes));
        Px(loaded.Layers[0], 27, 7).Should().Be(((byte)0, (byte)0, (byte)255, (byte)255), "the file contains the floating pixels at their moved position");
        Px(loaded.Layers[0], 7, 7).a.Should().Be(0);
    }

    [Test]
    public void SelectionRegionCodec_RoundTripsMaskAndOffset()
    {
        var r = new SelectionRegion();
        r.EnsureSize(40, 30);
        r.AddRect(new RectInt32 { X = 3, Y = 4, Width = 10, Height = 5 });
        r.SubtractRect(new RectInt32 { X = 6, Y = 6, Width = 2, Height = 1 });
        r.SetOffset(7, -2);

        var data = SelectionRegionCodec.Encode(r);
        var back = new SelectionRegion();
        SelectionRegionCodec.Decode(data, back);

        back.Width.Should().Be(40); back.Height.Should().Be(30);
        back.OffsetX.Should().Be(7); back.OffsetY.Should().Be(-2);
        for (int y = 0; y < 30; y++)
            for (int x = 0; x < 40; x++)
                back.Contains(x + 7, y - 2).Should().Be(r.Contains(x + 7, y - 2), $"pixel {x},{y}");
        data.Length.Should().BeLessThan(40 * 30, "RLE beats the raw mask");
    }

    [Test]
    public void SelectionChangeItem_UndoRedo_RestoresMask()
    {
        var doc = NewDoc();
        Select(doc, 2, 2, 5, 5);
        var before = doc.Selection.Clone();
        Select(doc, 10, 10, 3, 3);
        var after = doc.Selection.Clone();

        var item = new SelectionChangeItem(doc, SelectionChangeItem.SelectionChangeKind.Create, before, after);
        item.HasChanges.Should().BeTrue();
        doc.History.Push(item);

        doc.History.Undo();
        doc.Selection.Contains(3, 3).Should().BeTrue();
        doc.Selection.Contains(11, 11).Should().BeFalse();

        doc.History.Redo();
        doc.Selection.Contains(3, 3).Should().BeFalse();
        doc.Selection.Contains(11, 11).Should().BeTrue();
    }
}
