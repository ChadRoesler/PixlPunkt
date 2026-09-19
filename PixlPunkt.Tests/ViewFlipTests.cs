namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Export;
using PixlPunkt.Core.History;
using Windows.Graphics;

/// <summary>Non-destructive view flips: undoable, on the stack, never dirtying or touching pixels.</summary>
[TestFixture]
public class ViewFlipTests
{
    private static CanvasDocument NewDoc() =>
        new("t", 8, 8, new SizeInt32 { Width = 8, Height = 8 }, new SizeInt32 { Width = 1, Height = 1 });

    [Test]
    public void Flip_TogglesViewState_AndUndoes()
    {
        var doc = NewDoc();
        int raised = 0; doc.ViewTransformChanged += () => raised++;
        var item = new ViewFlipItem(doc, horizontal: true);
        item.Redo(); doc.History.Push(item);

        doc.ViewFlipHorizontal.Should().BeTrue(); doc.ViewFlipVertical.Should().BeFalse();
        raised.Should().Be(1);

        doc.History.Undo().Should().BeTrue();
        doc.ViewFlipHorizontal.Should().BeFalse();
        doc.History.Redo().Should().BeTrue();
        doc.ViewFlipHorizontal.Should().BeTrue();
    }

    [Test]
    public void Flip_DoesNotDirtyTheDocument_ButContentStillDoes()
    {
        var doc = NewDoc();
        doc.AddLayer();
        doc.MarkSaved();
        doc.IsDirty.Should().BeFalse();

        var flip = new ViewFlipItem(doc, horizontal: false);
        flip.Redo(); doc.History.Push(flip);
        doc.IsDirty.Should().BeFalse("a view flip is not a document change");

        doc.History.Undo();
        doc.IsDirty.Should().BeFalse();

        doc.AddLayer();
        doc.IsDirty.Should().BeTrue();
        doc.History.Undo();
        doc.IsDirty.Should().BeFalse("back at the save point");
    }

    [Test]
    public void SaveWithFlipOnTop_ThenUnflip_StaysClean()
    {
        var doc = NewDoc();
        var flip = new ViewFlipItem(doc, horizontal: true);
        flip.Redo(); doc.History.Push(flip);
        doc.MarkSaved();

        doc.History.Undo();                   // un-flip
        doc.IsDirty.Should().BeFalse();
        var flip2 = new ViewFlipItem(doc, horizontal: false);
        flip2.Redo(); doc.History.Push(flip2);
        doc.IsDirty.Should().BeFalse();
    }

    [Test]
    public void FlipPixels_MirrorsBothAxes()
    {
        // 2x2: TL red, TR green, BL blue, BR white (BGRA)
        var px = new byte[] { 0,0,255,255,  0,255,0,255,  255,0,0,255,  255,255,255,255 };
        TimelapseExportService.FlipPixels(px, 2, 2, flipH: true, flipV: false);
        px[0 * 4 + 1].Should().Be(255, "green is now top-left");
        px[1 * 4 + 2].Should().Be(255, "red is now top-right");
        TimelapseExportService.FlipPixels(px, 2, 2, flipH: false, flipV: true);
        px[0 * 4 + 0].Should().Be(255).And.Be(px[0 * 4 + 1], "white is now top-left");
        px[3 * 4 + 2].Should().Be(255, "red is now bottom-right");
    }

    [Test]
    public void Rotate_IsUndoable_Normalised_AndClean()
    {
        var doc = NewDoc();
        doc.MarkSaved();
        var item = new ViewRotateItem(doc, 0.0, 200.0);
        item.Redo(); doc.History.Push(item);

        doc.ViewRotationDeg.Should().BeApproximately(-160.0, 1e-9, "normalised to (-180, 180]");
        doc.IsDirty.Should().BeFalse();

        doc.History.Undo().Should().BeTrue();
        doc.ViewRotationDeg.Should().Be(0.0);

        new ViewRotateItem(doc, 30.0, 30.0).HasChange.Should().BeFalse();
    }
}
