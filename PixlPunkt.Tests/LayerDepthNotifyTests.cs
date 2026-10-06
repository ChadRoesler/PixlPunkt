namespace PixlPunkt.Tests;

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using Windows.Graphics;

/// <summary>
/// Depth has to announce itself when it changes.
/// </summary>
/// <remarks>
/// The layers panel indents each row by its layer's depth, and since the panel started reusing
/// rows rather than rebuilding them, a row that moves keeps its binding. A computed property that
/// never raises a change leaves the row sitting at its old indentation, which reads as the move
/// not having happened.
/// </remarks>
[TestFixture]
public class LayerDepthNotifyTests
{
    private static CanvasDocument NewDoc() => new("d", 16, 16,
        new SizeInt32 { Width = 8, Height = 8 }, new SizeInt32 { Width = 2, Height = 2 });

    /// <summary>Records which properties a layer announced.</summary>
    private static List<string> Watch(LayerBase layer)
    {
        var seen = new List<string>();
        ((INotifyPropertyChanged)layer).PropertyChanged += (_, e) => seen.Add(e.PropertyName ?? "");
        return seen;
    }

    [Test]
    public void MovingALayerIntoAFolderAnnouncesItsNewDepth()
    {
        var doc = NewDoc();
        var folder = doc.AddFolder("Box");
        doc.AddLayer("Art");
        var art = doc.RootItems.OfType<RasterLayer>().Last();

        var seen = Watch(art);
        doc.MoveLayerToFolder(art, folder);

        art.Depth.Should().Be(1);
        seen.Should().Contain(nameof(LayerBase.Depth));
    }

    [Test]
    public void MovingALayerBackToRootAnnouncesItToo()
    {
        var doc = NewDoc();
        var folder = doc.AddFolder("Box");
        doc.AddLayer("Art", into: folder);
        var art = folder.Children.OfType<RasterLayer>().Last();

        var seen = Watch(art);
        doc.MoveLayerToFolder(art, null);

        art.Depth.Should().Be(0);
        seen.Should().Contain(nameof(LayerBase.Depth));
    }

    [Test]
    public void MovingAFolderAnnouncesTheNewDepthOfEverythingInsideIt()
    {
        // The case no per-layer notification would ever catch: the children's own parent did not
        // change, only their grandparent did, yet every one of their depths moved.
        var doc = NewDoc();
        var outer = doc.AddFolder("Outer");
        var inner = doc.AddFolder("Inner");
        doc.AddLayer("Art", into: inner);
        var art = inner.Children.OfType<RasterLayer>().Last();

        art.Depth.Should().Be(1);

        var seenArt = Watch(art);
        var seenInner = Watch(inner);

        doc.MoveLayerToFolder(inner, outer);

        inner.Depth.Should().Be(1);
        art.Depth.Should().Be(2, "it rode along one level deeper");

        seenInner.Should().Contain(nameof(LayerBase.Depth));
        seenArt.Should().Contain(nameof(LayerBase.Depth), "a descendant's depth changed with no parent change of its own");
    }

    [Test]
    public void ADeepNestIsAnnouncedAllTheWayDown()
    {
        var doc = NewDoc();
        var top = doc.AddFolder("Top");
        var middle = doc.AddFolder("Middle");
        var bottom = doc.AddFolder("Bottom", into: middle);
        doc.AddLayer("Art", into: bottom);
        var art = bottom.Children.OfType<RasterLayer>().Last();

        var seen = Watch(art);
        doc.MoveLayerToFolder(middle, top);

        art.Depth.Should().Be(3);
        seen.Should().Contain(nameof(LayerBase.Depth));
    }

    [Test]
    public void LiftingAFolderOutAnnouncesItDownward()
    {
        var doc = NewDoc();
        var outer = doc.AddFolder("Outer");
        var inner = doc.AddFolder("Inner", into: outer);
        doc.AddLayer("Art", into: inner);
        var art = inner.Children.OfType<RasterLayer>().Last();

        art.Depth.Should().Be(2);

        var seen = Watch(art);
        doc.MoveOutOfParent(inner);

        art.Depth.Should().Be(1);
        seen.Should().Contain(nameof(LayerBase.Depth));
    }

    [Test]
    public void MovingWithinTheSameFolderLeavesTheDepthRight()
    {
        // A move is carried out as a remove and a re-insert, so the parent goes to null and back
        // and the depth is announced twice even though it never actually differs. Harmless, since
        // the binding simply reads the same number again, but worth writing down so the extra
        // notifications are not mistaken later for a sign that something moved.
        var doc = NewDoc();
        var folder = doc.AddFolder("Box");
        doc.AddLayer("Art", into: folder);
        var art = folder.Children.OfType<RasterLayer>().Last();

        var seen = Watch(art);
        doc.MoveLayerToFolder(art, folder);

        art.Depth.Should().Be(1, "it is still one level down, wherever it sits in the folder");
        art.Parent.Should().BeSameAs(folder);
        seen.Should().Contain(nameof(LayerBase.Depth));
    }
}
