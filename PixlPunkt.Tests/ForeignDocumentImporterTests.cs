namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;

/// <summary>
/// Import tests driven by <c>Assets/RawFiles/PyxelImportTesting.pyxel</c>, a file authored
/// specifically to exercise layer nesting, blend modes, hidden/collapsed flags, opacity,
/// tile references and a tile animation.
/// </summary>
[TestFixture]
public class ForeignDocumentImporterTests
{
    private static string PyxelFixture =>
        Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "PixlPunkt", "Assets", "RawFiles", "PyxelImportTesting.pyxel"));

    private static IEnumerable<LayerBase> Flatten(IEnumerable<LayerBase> items)
    {
        foreach (var item in items)
        {
            yield return item;
            if (item is LayerFolder f)
                foreach (var c in Flatten(f.Children)) yield return c;
        }
    }

    [Test]
    public void ImportPyxel_BuildsExpectedLayerTree()
    {
        File.Exists(PyxelFixture).Should().BeTrue($"fixture missing at {PyxelFixture}");

        var doc = ForeignDocumentImporter.ImportPyxel(PyxelFixture);

        doc.PixelWidth.Should().Be(128);
        doc.PixelHeight.Should().Be(64);

        var all = Flatten(doc.RootItems).ToList();
        all.Should().HaveCount(20, "the fixture has 20 layers/groups and the starter layer must be gone");
        all.OfType<LayerFolder>().Should().HaveCount(3);
        all.Should().NotContain(l => l.Name.StartsWith("Layer ") && l is RasterLayer && ((RasterLayer)l).Surface.Pixels.All(b => b == 0),
            "no blank starter layer should survive the import");

        // Root order is bottom-to-top: Pyxel index 19 (BaseLayerNormal) is the bottom layer.
        doc.RootItems.First().Name.Should().Be("BaseLayerNormal");
        doc.RootItems.Last().Name.Should().Be("AnimationLayer");

        // Nesting: FolderFolder > [FolderInAFolder > FoldInFoldLayer, LayerInFolderFolder]
        var folderFolder = all.OfType<LayerFolder>().Single(f => f.Name == "FolderFolder");
        folderFolder.Children.Select(c => c.Name).Should().BeEquivalentTo(
            new[] { "FolderInAFolder", "LayerInFolderFolder" });
        var inner = folderFolder.Children.OfType<LayerFolder>().Single();
        inner.Children.Select(c => c.Name).Should().Equal("FoldInFoldLayer");
        folderFolder.IsExpanded.Should().BeTrue();
        all.OfType<LayerFolder>().Single(f => f.Name == "LayerFolder").IsExpanded.Should().BeFalse("it is collapsed in Pyxel");
    }

    [Test]
    public void ImportPyxel_MapsLayerProperties()
    {
        var doc = ForeignDocumentImporter.ImportPyxel(PyxelFixture);
        var rasters = Flatten(doc.RootItems).OfType<RasterLayer>().ToDictionary(l => l.Name);

        rasters["SubtractLayer"].Blend.Should().Be(BlendMode.Subtract);
        rasters["ScreenLayer"].Blend.Should().Be(BlendMode.Screen);
        rasters["OverlayLayer"].Blend.Should().Be(BlendMode.Overlay);
        rasters["InvertLayer"].Blend.Should().Be(BlendMode.Invert);
        rasters["HardLightLayer"].Blend.Should().Be(BlendMode.HardLight);
        rasters["LightenLayer"].Blend.Should().Be(BlendMode.Lighten);
        rasters["DarkenLayer"].Blend.Should().Be(BlendMode.Darken);
        rasters["DifferenceLayer"].Blend.Should().Be(BlendMode.Difference);
        rasters["AddLayer"].Blend.Should().Be(BlendMode.Add);
        rasters["MultiplyLayer"].Blend.Should().Be(BlendMode.Multiply);

        rasters["Opacity100Layer"].Opacity.Should().Be(100);
        rasters["AnimationLayer"].Visible.Should().BeTrue();
        rasters["SubtractLayer"].Visible.Should().BeFalse();
    }

    [Test]
    public void ImportPyxel_ImportsTilesAndTileReferences()
    {
        var doc = ForeignDocumentImporter.ImportPyxel(PyxelFixture);

        doc.TileSet.Count.Should().Be(3, "the fixture ships tile0..tile2");

        var tiles = Flatten(doc.RootItems).OfType<RasterLayer>().Single(l => l.Name == "TilesLayer");
        tiles.TileMapping.Should().NotBeNull();

        // Pyxel tile index N maps to the Nth imported tile (ids are assigned in index order,
        // starting at 1). Cell 0 -> pyxel tile 1, cell 1 -> pyxel tile 2 (128px canvas = 8 cells wide).
        var map = tiles.TileMapping!;
        var ids = doc.TileSet.TileIds.ToList();
        map.GetTileId(0, 0).Should().Be(ids[1]);
        map.GetTileId(1, 0).Should().Be(ids[2]);
        map.GetTileId(4, 1).Should().Be(ids[1], "cell 12 = (4,1)");
        map.GetTileId(7, 0).Should().Be(ids[2], "cell 7 = (7,0)");
        map.GetTileId(2, 0).Should().Be(-1, "unreferenced cells stay empty");
    }

    [Test]
    public void ImportPyxel_LeavesHistoryCleanAndDocumentNotDirty()
    {
        var doc = ForeignDocumentImporter.ImportPyxel(PyxelFixture);

        doc.History.CanUndo.Should().BeFalse("an import must not leave undo entries behind");
        doc.IsDirty.Should().BeFalse();
    }
}
