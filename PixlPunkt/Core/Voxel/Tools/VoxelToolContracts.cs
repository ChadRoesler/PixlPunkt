using PixlPunkt.Core.Document;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Voxel;

namespace PixlPunkt.Core.Voxel.Tools
{
    /// <summary>Core/runtime voxel tool registration contract.</summary>
    public interface IVoxelToolRegistration
    {
        string Id { get; }
        string DisplayName { get; }
        VoxelToolCategory Category { get; }
        ToolSettingsBase? Settings { get; }
        IVoxelToolBehavior? Behavior { get; }
        IVoxelToolHandler CreateHandler(IVoxelToolContext context);
    }

    /// <summary>Core/runtime voxel tool behavior metadata.</summary>
    public interface IVoxelToolBehavior
    {
        string ToolId { get; }
        VoxelToolInputPattern InputPattern { get; }
        bool HandlesRightClick { get; }
        bool SuppressRmbSample { get; }
        bool ModifiesVoxelData { get; }
        bool RequiresFacePick { get; }
        bool RequiresVoxelPick { get; }
    }

    /// <summary>Core/runtime voxel tool handler contract.</summary>
    public interface IVoxelToolHandler
    {
        bool PointerPressed(VoxelPointerEvent e);
        bool PointerMoved(VoxelPointerEvent e);
        bool PointerReleased(VoxelPointerEvent e);
        void Cancel();
    }

    /// <summary>Core/runtime host services for voxel tools.</summary>
    public interface IVoxelToolContext
    {
        CanvasDocument Document { get; }
        IVoxelDocumentReadOnly DocumentInfo { get; }
        IVoxelModelReadOnly Model { get; }
        IVoxelSelectionReadOnly Selection { get; }

        bool TryPickFace(float screenX, float screenY, out VoxelFaceHit hit);
        bool TryPickVoxel(float screenX, float screenY, out VoxelVoxelHit hit);

        void SetFaceColor(int x, int y, int z, VoxelFace face, uint bgra);
        void ClearFaceColorOverride(int x, int y, int z, VoxelFace face);
        void SetVoxel(int x, int y, int z, uint colorBgra);
        void ClearVoxel(int x, int y, int z);
        void MoveSelection(Int3 delta, VoxelMoveMode mode = VoxelMoveMode.CutPaste);

        void ClearSelection();
        void SetSelection(IEnumerable<Int3> voxels, VoxelSelectionMode mode);
        void ExpandSelectionConnected();

        uint Foreground { get; }
        uint Background { get; }
        void SetForeground(uint bgra);
        void SetBackground(uint bgra);

        VoxelViewportState ViewportState { get; }
        void RequestRedraw();
        void RequestRebuildRenderCache();

        void BeginHistoryTransaction(string name);
        void CommitHistoryTransaction();
        void CancelHistoryTransaction();

        VoxelLightingSettings LightingSettings { get; }
        void UpdateLightingSettings(Action<VoxelLightingSettings> edit);
    }
}
