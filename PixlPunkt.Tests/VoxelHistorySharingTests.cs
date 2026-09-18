namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Voxel;
using PixlPunkt.Core.Voxel.Editing;
using PixlPunkt.PluginSdk.Voxel;
using Windows.Graphics;

/// <summary>Voxel edits on the document's history stack: one timeline, one dirty flag.</summary>
[TestFixture]
public class VoxelHistorySharingTests
{
    private static CanvasDocument NewDoc() =>
        new("t", 64, 64, new SizeInt32 { Width = 16, Height = 16 }, new SizeInt32 { Width = 4, Height = 4 });

    [Test]
    public void VoxelEdit_LandsOnDocumentStack_AndDirtiesDocument()
    {
        var doc = NewDoc();
        doc.MarkSaved();
        var engine = new VoxelEditEngine(doc.VoxelModel, doc.History);

        engine.InitializeModel(4, 4, 4, VoxelModelSourceKind.Manual);

        doc.History.PeekUndo().Should().BeOfType<VoxelHistoryItem>();
        doc.IsDirty.Should().BeTrue();
        engine.History.CanUndoVoxel.Should().BeTrue();

        doc.History.Undo().Should().BeTrue("the canvas side can step voxel items");
        doc.VoxelModel.HasModel.Should().BeFalse();
        doc.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Transaction_IsOneGroup_OnTheSharedStack()
    {
        var doc = NewDoc();
        var engine = new VoxelEditEngine(doc.VoxelModel, doc.History);
        engine.InitializeModel(4, 4, 4);
        int before = doc.History.UndoCount;

        engine.BeginHistoryTransaction("Txn");
        engine.CreateVoxel(1, 1, 1, 0xFF112233).Should().BeTrue();
        engine.CreateVoxel(2, 2, 2, 0xFF112233).Should().BeTrue();
        engine.CommitHistoryTransaction();

        doc.History.UndoCount.Should().Be(before + 1);
        doc.History.PeekUndo().Should().BeOfType<HistoryGroupItem>();
        engine.Undo().Should().BeFalse("a group is not a voxel item; the host steps it");
        doc.History.Undo().Should().BeTrue();
        doc.VoxelModel.IsOccupied(1, 1, 1).Should().BeFalse();
        doc.VoxelModel.IsOccupied(2, 2, 2).Should().BeFalse();
    }

    [Test]
    public void VoxelUndo_LeavesCanvasItemsForTheHost()
    {
        var doc = NewDoc();
        var engine = new VoxelEditEngine(doc.VoxelModel, doc.History);
        engine.InitializeModel(4, 4, 4);
        doc.AddLayer();                                   // a canvas item on top

        engine.History.CanUndoVoxel.Should().BeFalse();
        engine.Undo().Should().BeFalse();
        doc.History.CanUndo.Should().BeTrue();
        doc.Layers.Count.Should().Be(2, "nothing was undone");
    }
}
