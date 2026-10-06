namespace PixlPunkt.Tests;

using System.Linq;
using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using Windows.Graphics;

/// <summary>
/// Supplying hand-drawn artwork for particular icon sizes by naming a folder after the size.
/// </summary>
/// <remarks>
/// The failure that matters here is silent: a size folder that leaks into the large icon, or one
/// that is not picked up at all. Neither throws, both look like a bad export, so the source chosen
/// for each size is checked by reading pixels back rather than trusted.
/// </remarks>
[TestFixture]
public class IconExportTests
{
    private const byte Master = 40;
    private const byte Small = 200;

    private static CanvasDocument NewDoc() => new("icon", 8, 8,
        new SizeInt32 { Width = 8, Height = 8 }, new SizeInt32 { Width = 1, Height = 1 });

    /// <summary>Fills a layer with a recognisable value so its pixels can be identified later.</summary>
    private static void Fill(RasterLayer layer, byte value)
    {
        var pixels = layer.Surface.Pixels;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = value;
            pixels[i + 1] = value;
            pixels[i + 2] = value;
            pixels[i + 3] = 255;
        }
    }

    private static RasterLayer AddFilled(CanvasDocument doc, byte value, LayerFolder? into = null)
    {
        doc.AddLayer("L", into: into);
        var layer = into is null
            ? doc.RootItems.OfType<RasterLayer>().Last()
            : into.Children.OfType<RasterLayer>().Last();
        Fill(layer, value);
        return layer;
    }

    private static byte FirstBlue(byte[] pixels) => pixels[0];

    // ── reading a size out of a folder name ─────────────────────────

    [Test]
    public void AFolderNamedForASizeIsRecognised()
    {
        IconExportOps.ParseSizeName("16").Should().Be(16);
        IconExportOps.ParseSizeName("16x16").Should().Be(16);
        IconExportOps.ParseSizeName("16X16").Should().Be(16);
        IconExportOps.ParseSizeName(" 32 x 32 ").Should().Be(32);
        IconExportOps.ParseSizeName("256×256").Should().Be(256, "a real multiplication sign counts too");
    }

    [Test]
    public void AnOrdinaryFolderNameIsNotASize()
    {
        IconExportOps.ParseSizeName("Background").Should().BeNull();
        IconExportOps.ParseSizeName("").Should().BeNull();
        IconExportOps.ParseSizeName(null).Should().BeNull();
        IconExportOps.ParseSizeName("16px").Should().BeNull();
        IconExportOps.ParseSizeName("v2 16x16").Should().BeNull();
    }

    [Test]
    public void ARectangleIsRejectedRatherThanGuessedAt()
    {
        // Icons are square. "32x16" is far more likely a mistake than a request.
        IconExportOps.ParseSizeName("32x16").Should().BeNull();
        IconExportOps.ParseSizeName("0").Should().BeNull();
    }

    // ── finding the folders ─────────────────────────────────────────

    [Test]
    public void SizeFoldersAreFoundWhereverTheySit()
    {
        var doc = NewDoc();
        doc.AddFolder("16x16");
        var art = doc.AddFolder("Art");
        doc.AddFolder("32x32", into: art);

        var folders = IconExportOps.SizeFolders(doc);

        folders.Keys.Should().BeEquivalentTo(new[] { 16, 32 });
        IconExportOps.HasFolderFor(doc, 16).Should().BeTrue();
        IconExportOps.HasFolderFor(doc, 48).Should().BeFalse();
    }

    [Test]
    public void AFolderInsideASizeFolderIsNotTreatedAsAnotherSize()
    {
        var doc = NewDoc();
        var sixteen = doc.AddFolder("16x16");
        doc.AddFolder("32x32", into: sixteen);

        IconExportOps.SizeFolders(doc).Keys.Should().BeEquivalentTo(new[] { 16 },
            "what is inside a size folder is that size's business");
    }

    // ── choosing the source for a size ──────────────────────────────

    [Test]
    public void ASizeWithItsOwnFolderUsesThatArtwork()
    {
        var doc = NewDoc();
        AddFilled(doc, Master);
        var folder = doc.AddFolder("16x16");
        AddFilled(doc, Small, into: folder);

        FirstBlue(IconExportOps.CompositeSourceFor(doc, 16)).Should().Be(Small);
    }

    [Test]
    public void ASizeWithNothingLargerThanItUsesTheMasterArtwork()
    {
        var doc = NewDoc();
        AddFilled(doc, Master);
        var folder = doc.AddFolder("16x16");
        AddFilled(doc, Small, into: folder);

        FirstBlue(IconExportOps.CompositeSourceFor(doc, 256)).Should().Be(Master);
        IconExportOps.FolderSizeFor(doc, 256).Should().BeNull();
    }

    [Test]
    public void ASizeWithoutAFolderInheritsFromTheNearestLargerOne()
    {
        // A thirty two drawn by hand is already simplified, so a sixteen is far better served by
        // shrinking that than by shrinking the full detail of the master.
        var doc = NewDoc();
        AddFilled(doc, Master);
        var thirtyTwo = doc.AddFolder("32x32");
        AddFilled(doc, Small, into: thirtyTwo);

        IconExportOps.FolderSizeFor(doc, 16).Should().Be(32);
        FirstBlue(IconExportOps.CompositeSourceFor(doc, 16)).Should().Be(Small);
    }

    [Test]
    public void ItInheritsFromTheNearestLargerFolder_NotTheLargestOne()
    {
        var doc = NewDoc();
        AddFilled(doc, Master);

        var fortyEight = doc.AddFolder("48x48");
        var thirtyTwo = doc.AddFolder("32x32");
        AddFilled(doc, 90, into: fortyEight);
        AddFilled(doc, Small, into: thirtyTwo);

        IconExportOps.FolderSizeFor(doc, 16).Should().Be(32, "thirty two is nearer than forty eight");
        FirstBlue(IconExportOps.CompositeSourceFor(doc, 16)).Should().Be(Small);
    }

    [Test]
    public void AnOwnFolderAlwaysBeatsAnInheritedOne()
    {
        var doc = NewDoc();
        AddFilled(doc, Master);

        var thirtyTwo = doc.AddFolder("32x32");
        var sixteen = doc.AddFolder("16x16");
        AddFilled(doc, 90, into: thirtyTwo);
        AddFilled(doc, Small, into: sixteen);

        IconExportOps.FolderSizeFor(doc, 16).Should().Be(16);
        FirstBlue(IconExportOps.CompositeSourceFor(doc, 16)).Should().Be(Small);
    }

    [Test]
    public void InheritanceOnlyEverLooksUpward()
    {
        // A sixteen must never feed a thirty two. Detail cannot be invented on the way back up.
        var doc = NewDoc();
        AddFilled(doc, Master);
        var sixteen = doc.AddFolder("16x16");
        AddFilled(doc, Small, into: sixteen);

        IconExportOps.FolderSizeFor(doc, 32).Should().BeNull();
        FirstBlue(IconExportOps.CompositeSourceFor(doc, 32)).Should().Be(Master);
    }

    [Test]
    public void SizeFoldersNeverLeakIntoTheOtherSizes()
    {
        // The quiet failure this whole feature could cause: the sixteen pixel drawing showing up
        // inside the two hundred and fifty six pixel icon.
        var doc = NewDoc();
        AddFilled(doc, Master);
        var folder = doc.AddFolder("16x16");
        AddFilled(doc, Small, into: folder);

        var master = IconExportOps.CompositeSourceFor(doc, 48);

        master.Where((_, i) => i % 4 == 0).Should().OnlyContain(v => v == Master,
            "nothing from a size folder belongs in any other size");
    }

    [Test]
    public void AHiddenSizeFolderStillExports()
    {
        // You hide the small versions while drawing the big one. That must not silently empty them.
        var doc = NewDoc();
        AddFilled(doc, Master);
        var folder = doc.AddFolder("16x16");
        AddFilled(doc, Small, into: folder);
        folder.Visible = false;

        FirstBlue(IconExportOps.CompositeSourceFor(doc, 16)).Should().Be(Small);
    }

    [Test]
    public void AHiddenLayerInsideASizeFolderIsStillHidden()
    {
        var doc = NewDoc();
        AddFilled(doc, Master);
        var folder = doc.AddFolder("16x16");
        var inner = AddFilled(doc, Small, into: folder);
        inner.Visible = false;

        FirstBlue(IconExportOps.CompositeSourceFor(doc, 16)).Should().Be(0,
            "hiding a layer inside the folder hides it, the same as anywhere else");
    }

    [Test]
    public void ADocumentWithNoSizeFoldersBehavesExactlyAsBefore()
    {
        var doc = NewDoc();
        AddFilled(doc, Master);

        IconExportOps.SizeFolders(doc).Should().BeEmpty();

        foreach (int size in new[] { 16, 32, 48, 256 })
        {
            IconExportOps.FolderSizeFor(doc, size).Should().BeNull();
            FirstBlue(IconExportOps.CompositeSourceFor(doc, size)).Should().Be(Master);
        }
    }

    [Test]
    public void TheSourceIsAlwaysAtTheDocumentsOwnSize()
    {
        // It is the artwork to shrink, not the finished icon, so it comes back at canvas size.
        var doc = NewDoc();
        AddFilled(doc, Master);
        doc.AddFolder("16x16");

        IconExportOps.CompositeSourceFor(doc, 16).Length
            .Should().Be(doc.PixelWidth * doc.PixelHeight * 4);
    }
}
