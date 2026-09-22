namespace PixlPunkt.Tests;

using System.Linq;
using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using Windows.Graphics;

/// <summary>
/// Where a new layer or folder lands, and lifting a folder out of its parent.
/// </summary>
/// <remarks>
/// The distinction these turn on is that picked and active are not the same thing. The active layer
/// wears the pill and is where painting goes; picking something in the panel is a deliberate choice
/// about structure, so it decides placement.
/// </remarks>
[TestFixture]
public class LayerPlacementTests
{
    private static CanvasDocument NewDoc() => new("d", 16, 16,
        new SizeInt32 { Width = 8, Height = 8 }, new SizeInt32 { Width = 2, Height = 2 });

    // ── new layers ──────────────────────────────────────────────────

    [Test]
    public void APickedFolderTakesTheNewLayerInside()
    {
        var doc = NewDoc();
        var folder = doc.AddFolder("Box");

        doc.AddLayer("Inside", into: folder);

        folder.Children.Should().ContainSingle(c => c.Name == "Inside");
        doc.RootItems.Should().NotContain(c => c.Name == "Inside");
    }

    [Test]
    public void APickedLayerPutsTheNewOneDirectlyAboveIt()
    {
        var doc = NewDoc();
        doc.AddLayer("Lower");
        var lower = doc.RootItems.Single(l => l.Name == "Lower");

        doc.AddLayer("Upper", above: lower);

        int lowerIndex = doc.RootItems.ToList().FindIndex(l => l.Name == "Lower");
        int upperIndex = doc.RootItems.ToList().FindIndex(l => l.Name == "Upper");
        upperIndex.Should().Be(lowerIndex + 1, "higher index is higher in the panel");
    }

    [Test]
    public void APickedLayerInsideAFolderKeepsTheNewOneInThatFolder()
    {
        var doc = NewDoc();
        var folder = doc.AddFolder("Box");
        doc.AddLayer("First", into: folder);
        var first = folder.Children.Single(c => c.Name == "First");

        doc.AddLayer("Second", above: first);

        folder.Children.Select(c => c.Name).Should().Contain(new[] { "First", "Second" });
        folder.IndexOfChild(folder.Children.Single(c => c.Name == "Second"))
            .Should().Be(folder.IndexOfChild(first) + 1);
    }

    [Test]
    public void WithNothingPickedTheNewLayerFollowsTheActiveOne()
    {
        // The behaviour that was there before, which has to survive.
        var doc = NewDoc();
        var folder = doc.AddFolder("Box");
        doc.AddLayer("Active", into: folder);

        doc.AddLayer("Next");

        doc.ActiveLayer!.Name.Should().Be("Next");
        folder.Children.Should().Contain(c => c.Name == "Next",
            "it joins whatever folder the active layer is in");
    }

    // ── new folders ─────────────────────────────────────────────────

    [Test]
    public void APickedFolderTakesANewFolderInside()
    {
        var doc = NewDoc();
        var outer = doc.AddFolder("Outer");

        var inner = doc.AddFolder("Inner", into: outer);

        inner.Parent.Should().BeSameAs(outer);
        outer.Children.Should().ContainSingle(c => c.Name == "Inner");
    }

    [Test]
    public void APickedLayerPutsANewFolderDirectlyAboveIt()
    {
        var doc = NewDoc();
        doc.AddLayer("Art");
        var art = doc.RootItems.Single(l => l.Name == "Art");

        var folder = doc.AddFolder("Group", above: art);

        int artIndex = doc.RootItems.ToList().IndexOf(art);
        doc.RootItems.ToList().IndexOf(folder).Should().Be(artIndex + 1);
    }

    // ── lifting a folder out ────────────────────────────────────────

    [Test]
    public void AFolderCanBeLiftedOutOfItsParent()
    {
        var doc = NewDoc();
        var outer = doc.AddFolder("Outer");
        var inner = doc.AddFolder("Inner", into: outer);

        doc.MoveOutOfParent(inner).Should().BeTrue();

        inner.Parent.Should().BeNull("it came all the way out to the top level");
        outer.Children.Should().NotContain(inner);
        doc.RootItems.Should().Contain(inner);
    }

    [Test]
    public void LiftingOutLandsItJustAboveTheFolderItLeft()
    {
        var doc = NewDoc();
        var outer = doc.AddFolder("Outer");
        var inner = doc.AddFolder("Inner", into: outer);

        doc.MoveOutOfParent(inner);

        var items = doc.RootItems.ToList();
        items.IndexOf(inner).Should().Be(items.IndexOf(outer) + 1);
    }

    [Test]
    public void LiftingFromADeepNestGoesUpOneLevelOnly()
    {
        var doc = NewDoc();
        var top = doc.AddFolder("Top");
        var middle = doc.AddFolder("Middle", into: top);
        var bottom = doc.AddFolder("Bottom", into: middle);

        doc.MoveOutOfParent(bottom);

        bottom.Parent.Should().BeSameAs(top, "one level up, not all the way out");
    }

    [Test]
    public void AFolderAlreadyAtTheTopLevelHasNowhereToGo()
    {
        var doc = NewDoc();
        var folder = doc.AddFolder("Alone");

        doc.MoveOutOfParent(folder).Should().BeFalse();
        folder.Parent.Should().BeNull();
    }

    [Test]
    public void ALayerCanBeLiftedOutToo()
    {
        var doc = NewDoc();
        var folder = doc.AddFolder("Box");
        doc.AddLayer("Art", into: folder);
        var art = folder.Children.Single(c => c.Name == "Art");

        doc.MoveOutOfParent(art).Should().BeTrue();

        art.Parent.Should().BeNull();
        doc.RootItems.Should().Contain(art);
    }

    [Test]
    public void LiftingOutCanBeUndone()
    {
        var doc = NewDoc();
        var outer = doc.AddFolder("Outer");
        var inner = doc.AddFolder("Inner", into: outer);

        doc.MoveOutOfParent(inner);
        doc.History.Undo();

        inner.Parent.Should().BeSameAs(outer, "the move is one step on the history stack");
    }
}
