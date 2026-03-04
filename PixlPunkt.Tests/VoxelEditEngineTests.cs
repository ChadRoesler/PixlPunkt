namespace PixlPunkt.Tests;

using PixlPunkt.Core.Document;
using PixlPunkt.Core.Voxel;
using PixlPunkt.Core.Voxel.Editing;
using PixlPunkt.PluginSdk.Voxel;

[TestFixture]
public sealed class VoxelEditEngineTests
{
    [Test]
    public void InitializeModel_UndoRedo_RestoresState()
    {
        var model = new VoxelModelDocumentState();
        var engine = new VoxelEditEngine(model);

        engine.InitializeModel(4, 4, 4, VoxelModelSourceKind.Manual);

        model.HasModel.Should().BeTrue();
        model.Width.Should().Be(4);
        model.SourceKind.Should().Be(VoxelModelSourceKind.Manual);

        engine.Undo().Should().BeTrue();
        model.HasModel.Should().BeFalse();

        engine.Redo().Should().BeTrue();
        model.HasModel.Should().BeTrue();
        model.Width.Should().Be(4);
    }

    [Test]
    public void CreateVoxel_UndoRedo_Works()
    {
        var model = new VoxelModelDocumentState();
        var engine = new VoxelEditEngine(model);
        engine.InitializeModel(4, 4, 4);

        const uint color = 0xFF112233;
        engine.CreateVoxel(1, 1, 1, color).Should().BeTrue();

        model.IsOccupied(1, 1, 1).Should().BeTrue();
        model.GetFaceColorBgra(1, 1, 1, Face.Front).Should().Be(color);

        engine.Undo().Should().BeTrue();
        model.IsOccupied(1, 1, 1).Should().BeFalse();

        engine.Redo().Should().BeTrue();
        model.IsOccupied(1, 1, 1).Should().BeTrue();
        model.GetFaceColorBgra(1, 1, 1, Face.Front).Should().Be(color);
    }

    [Test]
    public void SetFaceColor_UndoRedo_Works()
    {
        var model = new VoxelModelDocumentState();
        var engine = new VoxelEditEngine(model);
        engine.InitializeModel(4, 4, 4);

        const uint startColor = 0xFF556677;
        const uint endColor = 0xFFCCDDEE;
        engine.CreateVoxel(1, 1, 1, startColor).Should().BeTrue();

        engine.SetFaceColor(1, 1, 1, Face.Top, endColor).Should().BeTrue();
        model.GetFaceColorBgra(1, 1, 1, Face.Top).Should().Be(endColor);

        engine.Undo().Should().BeTrue();
        model.GetFaceColorBgra(1, 1, 1, Face.Top).Should().Be(startColor);

        engine.Redo().Should().BeTrue();
        model.GetFaceColorBgra(1, 1, 1, Face.Top).Should().Be(endColor);
    }

    [Test]
    public void MoveSelection_CutPaste_UndoRedo_Works()
    {
        var model = new VoxelModelDocumentState();
        var engine = new VoxelEditEngine(model);
        engine.InitializeModel(6, 6, 6);

        const uint color = 0xFFAA6600;
        engine.CreateVoxel(1, 1, 1, color).Should().BeTrue();

        engine.SetSelection([new Int3(1, 1, 1)], VoxelSelectionMode.Replace).Should().BeTrue();
        engine.MoveSelection(new Int3(2, 0, 0), VoxelMoveMode.CutPaste).Should().BeTrue();

        model.IsOccupied(1, 1, 1).Should().BeFalse();
        model.IsOccupied(3, 1, 1).Should().BeTrue();
        engine.Selection.Contains(new Int3(3, 1, 1)).Should().BeTrue();

        engine.Undo().Should().BeTrue();
        model.IsOccupied(1, 1, 1).Should().BeTrue();
        model.IsOccupied(3, 1, 1).Should().BeFalse();
        engine.Selection.Contains(new Int3(1, 1, 1)).Should().BeTrue();

        engine.Redo().Should().BeTrue();
        model.IsOccupied(1, 1, 1).Should().BeFalse();
        model.IsOccupied(3, 1, 1).Should().BeTrue();
        engine.Selection.Contains(new Int3(3, 1, 1)).Should().BeTrue();
    }

    [Test]
    public void HistoryTransaction_Cancel_RevertsPendingChanges()
    {
        var model = new VoxelModelDocumentState();
        var engine = new VoxelEditEngine(model);
        engine.InitializeModel(4, 4, 4);

        engine.BeginHistoryTransaction("Txn");
        engine.CreateVoxel(2, 2, 2, 0xFF123456).Should().BeTrue();
        engine.SetSelection([new Int3(2, 2, 2)], VoxelSelectionMode.Replace).Should().BeTrue();
        engine.CancelHistoryTransaction();

        model.IsOccupied(2, 2, 2).Should().BeFalse();
        engine.Selection.Count.Should().Be(0);
        engine.History.CanUndo.Should().BeTrue("initialize model remains undoable");
    }

    [Test]
    public void ReplaceModelFromVolume_UndoRedo_Works()
    {
        var model = new VoxelModelDocumentState();
        var engine = new VoxelEditEngine(model);

        var volume = new VoxelVolume(3);
        volume.SetVoxel(1, 1, 1, new Rgba32(10, 20, 30, 255));
        volume.SetFaceColor(1, 1, 1, Face.Front, new Rgba32(10, 20, 30, 255));

        engine.ReplaceModelFromVolume(volume, VoxelModelSourceKind.TileOrthoGenerated);

        model.HasModel.Should().BeTrue();
        model.IsOccupied(1, 1, 1).Should().BeTrue();
        model.SourceKind.Should().Be(VoxelModelSourceKind.TileOrthoGenerated);

        engine.Undo().Should().BeTrue();
        model.HasModel.Should().BeFalse();

        engine.Redo().Should().BeTrue();
        model.HasModel.Should().BeTrue();
        model.IsOccupied(1, 1, 1).Should().BeTrue();
    }
}
