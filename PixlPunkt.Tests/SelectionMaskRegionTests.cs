namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Selection;
using Windows.Graphics;

/// <summary>Stage 2: the selection region is built from the float's mask, not pixel alpha.</summary>
[TestFixture]
public class SelectionMaskRegionTests
{
    private static CanvasDocument NewDoc() =>
        new("t", 64, 64, new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = 4, Height = 4 });

    private static RectInt32 R(int x, int y, int w, int h) => new() { X = x, Y = y, Width = w, Height = h };

    private static FloatingSelection Lift(CanvasDocument doc, params RectInt32[] rects)
    {
        doc.Selection.EnsureSize(doc.PixelWidth, doc.PixelHeight);
        doc.Selection.Clear();
        foreach (var r in rects) doc.Selection.AddRect(r);
        doc.History.Push(FloatingSelectionOps.Lift(doc, doc.Layers[0])!);
        return doc.Floating!;
    }

    [Test]
    public void AfterScaleBake_RegionKeepsTheMarquee_TransparentPixelsIncluded()
    {
        var doc = NewDoc();
        var f = Lift(doc, R(10, 10, 4, 2), R(10, 12, 2, 2));  // L, blank layer
        // what BakeTransformsOnRelease does for a 2x scale
        f.ResampleMask(8, 8);
        f.Pixels = new byte[8 * 8 * 4]; f.Width = 8; f.Height = 8; f.OrigW = 8; f.OrigH = 8;

        SelectionRegionBuilders.RebuildFromMask(doc.Selection, f, doc.PixelWidth, doc.PixelHeight);

        // 8x8 centred on (12,12): x 8..15, y 8..15
        doc.Selection.Contains(8, 8).Should().BeTrue();
        doc.Selection.Contains(15, 11).Should().BeTrue("bar spans the width");
        doc.Selection.Contains(9, 15).Should().BeTrue("leg");
        doc.Selection.Contains(13, 13).Should().BeFalse("notch");
        doc.Selection.Contains(7, 8).Should().BeFalse();
    }

    [Test]
    public void Rotate90_Nearest_SwapsTheShape()
    {
        var doc = NewDoc();
        var f = Lift(doc, R(10, 10, 6, 2));
        f.RotMode = RotationMode.NearestNeighbor;
        f.CumulativeAngleDeg = 90.0;

        SelectionRegionBuilders.RebuildFromMask(doc.Selection, f, doc.PixelWidth, doc.PixelHeight);

        var b = doc.Selection.Bounds;
        (b.Width, b.Height).Should().Be((2, 6));
    }

    [Test]
    public void Rotate90_RotSprite_UsesThePixelsRotationMethod()
    {
        var doc = NewDoc();
        var f = Lift(doc, R(10, 10, 6, 2));
        f.RotMode = RotationMode.RotSprite;
        f.CumulativeAngleDeg = 90.0;

        SelectionRegionBuilders.RebuildFromMask(doc.Selection, f, doc.PixelWidth, doc.PixelHeight);

        var b = doc.Selection.Bounds;
        b.Height.Should().BeGreaterThan(b.Width);
        b.Height.Should().BeInRange(5, 7);
    }

    [Test]
    public void OffCanvas_ShapeIsKeptWhole()
    {
        var doc = NewDoc();
        var f = Lift(doc, R(0, 0, 4, 4));
        f.OrigCenterX = 0; f.OrigCenterY = 0;              // half the shape hangs off the top-left

        SelectionRegionBuilders.RebuildFromMask(doc.Selection, f, doc.PixelWidth, doc.PixelHeight);

        var b = doc.Selection.Bounds;
        (b.X, b.Y, b.Width, b.Height).Should().Be((-2, -2, 4, 4));
        doc.Selection.Contains(-2, -2).Should().BeTrue("off-canvas pixels stay selected, as during a drag");
        doc.Selection.Contains(1, 1).Should().BeTrue();
        doc.Selection.Contains(2, 2).Should().BeFalse();
        doc.Selection.Width.Should().Be(doc.PixelWidth, "the local mask stays document-sized");
    }

    [Test]
    public void OffCanvas_SurvivesUndoRedo()
    {
        var doc = NewDoc();
        var f = Lift(doc, R(10, 10, 4, 4));
        var before = PixlPunkt.Core.History.SelectionTransformItem.Capture(f, false);
        f.X = -2; f.Y = -2; f.OrigCenterX = 0; f.OrigCenterY = 0;   // moved half off the canvas
        var after = PixlPunkt.Core.History.SelectionTransformItem.Capture(f, false);
        doc.History.Push(new PixlPunkt.Core.History.SelectionTransformItem(doc,
            PixlPunkt.Core.History.SelectionTransformItem.TransformKind.Move, before, after));

        doc.History.Undo().Should().BeTrue();
        doc.Selection.Bounds.Should().Be(R(10, 10, 4, 4));
        doc.History.Redo().Should().BeTrue();
        doc.Selection.Bounds.Should().Be(R(-2, -2, 4, 4));
        doc.Selection.Contains(-1, -1).Should().BeTrue();
    }

    [Test]
    public void UndoOfAScaleBake_RestoresTheMarquee_NotTheSilhouette()
    {
        var doc = NewDoc();
        var f = Lift(doc, R(10, 10, 4, 2), R(10, 12, 2, 2));  // L on a blank layer: alpha is 0 everywhere
        var before = PixlPunkt.Core.History.SelectionTransformItem.Capture(f, includeBuffer: true);

        // a 2x scale bake, as the host does it
        f.ResampleMask(8, 8);
        f.Pixels = new byte[8 * 8 * 4]; f.Width = 8; f.Height = 8; f.OrigW = 8; f.OrigH = 8;
        SelectionRegionBuilders.RebuildFromMask(doc.Selection, f, doc.PixelWidth, doc.PixelHeight);
        var after = PixlPunkt.Core.History.SelectionTransformItem.Capture(f, includeBuffer: true);
        doc.History.Push(new PixlPunkt.Core.History.SelectionTransformItem(doc,
            PixlPunkt.Core.History.SelectionTransformItem.TransformKind.Scale, before, after));
        doc.Selection.Bounds.Width.Should().Be(8);

        doc.History.Undo().Should().BeTrue();

        var b = doc.Selection.Bounds;
        (b.X, b.Y, b.Width, b.Height).Should().Be((10, 10, 4, 4));
        doc.Selection.Contains(13, 10).Should().BeTrue("bar");
        doc.Selection.Contains(13, 13).Should().BeFalse("notch");
        doc.Selection.IsEmpty.Should().BeFalse("with alpha as the source this would have been empty");

        doc.History.Redo().Should().BeTrue();
        doc.Selection.Bounds.Width.Should().Be(8);
    }
}
