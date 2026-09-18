namespace PixlPunkt.Tests;

using System.Collections.Generic;
using FluentAssertions;
using PixlPunkt.Core.Document;
using Windows.Graphics;

/// <summary>Deleting a tile clears its mapping cells; renumbering closes id gaps and is one undo step.</summary>
[TestFixture]
public class TileRenumberTests
{
    private static CanvasDocument NewDoc() =>
        new("t", 64, 64, new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = 4, Height = 4 });

    private static List<int> AddTiles(CanvasDocument doc, int n)
    {
        var ids = new List<int>();
        for (int i = 0; i < n; i++) ids.Add(doc.TileSet.AddEmptyTile());
        return ids;
    }

    [Test]
    public void RemoveTile_ClearsMappingCellsAndVoxelReferences()
    {
        var doc = NewDoc();
        var ids = AddTiles(doc, 3);
        var map = doc.Layers[0].GetOrCreateTileMapping(4, 4);
        map.SetTileId(0, 0, ids[1]);
        map.SetTileId(3, 3, ids[1]);
        map.SetTileId(1, 1, ids[2]);
        doc.VoxelWorkspace.FrontTileId3 = ids[1];

        doc.TileSet.RemoveTile(ids[1]).Should().BeTrue();

        map.GetTileId(0, 0).Should().Be(-1);
        map.GetTileId(3, 3).Should().Be(-1);
        map.GetTileId(1, 1).Should().Be(ids[2], "other tiles are untouched");
        doc.VoxelWorkspace.FrontTileId3.Should().Be(-1);
    }

    [Test]
    public void RenumberTiles_ClosesGaps_RewritesMappings_UndoRestores()
    {
        var doc = NewDoc();
        var ids = AddTiles(doc, 5);                       // 1..5
        doc.TileSet.RemoveTile(ids[1]);                   // 1,3,4,5
        doc.TileSet.RemoveTile(ids[3]);                   // 1,3,5
        var map = doc.Layers[0].GetOrCreateTileMapping(4, 4);
        map.SetTileId(0, 0, 3);
        map.SetTileId(1, 0, 5);
        doc.VoxelPreviewState.TopTileId6 = 5;
        var tile5 = doc.TileSet.GetTile(5)!;
        tile5.Name = "five";

        doc.RenumberTiles().Should().Be(2);

        doc.TileSet.TileIds.Should().Equal(1, 2, 3);
        map.GetTileId(0, 0).Should().Be(2);
        map.GetTileId(1, 0).Should().Be(3);
        doc.VoxelPreviewState.TopTileId6.Should().Be(3);
        doc.TileSet.GetTile(3)!.Name.Should().Be("five");
        doc.TileSet.GetTile(3)!.Pixels.Should().BeSameAs(tile5.Pixels, "renumbering does not copy pixels");
        doc.TileSet.AddEmptyTile().Should().Be(4, "next id continues after the renumbered range");
        doc.TileSet.RemoveTile(4);

        doc.History.Undo().Should().BeTrue();
        doc.TileSet.TileIds.Should().Equal(1, 3, 5);
        map.GetTileId(0, 0).Should().Be(3);
        map.GetTileId(1, 0).Should().Be(5);
        doc.VoxelPreviewState.TopTileId6.Should().Be(5);

        doc.History.Redo().Should().BeTrue();
        doc.TileSet.TileIds.Should().Equal(1, 2, 3);
        map.GetTileId(1, 0).Should().Be(3);
    }

    [Test]
    public void RenumberTiles_AlreadySequential_IsNoOpWithNoHistory()
    {
        var doc = NewDoc();
        AddTiles(doc, 3);
        int before = doc.History.UndoCount;

        doc.RenumberTiles().Should().Be(0);

        doc.History.UndoCount.Should().Be(before);
    }

    [Test]
    public void AddFolder_IntoSelectedFolder_NestsIt()
    {
        var doc = NewDoc();
        var outer = doc.AddFolder("outer");

        var inner = doc.AddFolder("inner", into: outer);

        inner.Parent.Should().BeSameAs(outer);
        outer.Children.Should().Contain(inner);
        doc.History.Undo().Should().BeTrue();
        outer.Children.Should().NotContain(inner);
    }
}
