using System.Numerics;

namespace PixlPunkt.PluginSdk.Voxel
{
    /// <summary>Integer voxel coordinate.</summary>
    public readonly record struct Int3(int X, int Y, int Z);

    /// <summary>Voxel face directions.</summary>
    public enum VoxelFace
    {
        Front = 0,
        Back = 1,
        Left = 2,
        Right = 3,
        Top = 4,
        Bottom = 5,
    }

    /// <summary>Voxel tool categories for the voxel workspace (separate from 2D ToolCategory).</summary>
    public enum VoxelToolCategory
    {
        Face,
        Edit,
        Utility,
    }

    /// <summary>Voxel tool pointer interaction patterns.</summary>
    public enum VoxelToolInputPattern
    {
        Click,
        Stroke,
        Drag,
        SelectionTransform,
        Utility,
    }

    /// <summary>Selection combination mode.</summary>
    public enum VoxelSelectionMode
    {
        Replace,
        Add,
        Remove,
        Toggle,
    }

    /// <summary>How selection moves are applied.</summary>
    public enum VoxelMoveMode
    {
        CutPaste,
        Copy,
    }

    /// <summary>Read-only document metadata for voxel tools.</summary>
    public interface IVoxelDocumentReadOnly
    {
        string Name { get; }
    }

    /// <summary>Read-only voxel model view exposed to plugins.</summary>
    public interface IVoxelModelReadOnly
    {
        int Width { get; }
        int Height { get; }
        int Depth { get; }
        bool IsOccupied(int x, int y, int z);
        uint GetFaceColor(int x, int y, int z, VoxelFace face);
    }

    /// <summary>Read-only voxel selection view exposed to plugins.</summary>
    public interface IVoxelSelectionReadOnly
    {
        int Count { get; }
        bool Contains(Int3 position);
        IEnumerable<Int3> Enumerate();
    }

    /// <summary>Simple immutable selection snapshot implementation.</summary>
    public sealed class VoxelSelectionSnapshot : IVoxelSelectionReadOnly
    {
        private readonly HashSet<Int3> _voxels;

        public VoxelSelectionSnapshot(IEnumerable<Int3> voxels)
        {
            _voxels = voxels != null ? new HashSet<Int3>(voxels) : [];
        }

        public int Count => _voxels.Count;

        public bool Contains(Int3 position) => _voxels.Contains(position);

        public IEnumerable<Int3> Enumerate() => _voxels;
    }

    /// <summary>Face hit result from host picking.</summary>
    public readonly record struct VoxelFaceHit(
        Int3 Position,
        VoxelFace Face,
        float Distance,
        uint ColorBgra = 0);

    /// <summary>Voxel hit result from host picking.</summary>
    public readonly record struct VoxelVoxelHit(
        Int3 Position,
        float Distance,
        VoxelFace EntryFace = VoxelFace.Front);

    /// <summary>Pointer event payload for voxel tools.</summary>
    public readonly record struct VoxelPointerEvent(
        float ScreenX,
        float ScreenY,
        bool IsLeftButtonPressed,
        bool IsRightButtonPressed,
        bool IsMiddleButtonPressed,
        bool Shift,
        bool Ctrl,
        bool Alt);

    /// <summary>Read-only camera/viewport state snapshot.</summary>
    public readonly record struct VoxelViewportState(
        int ViewportWidth,
        int ViewportHeight,
        float PitchRadians,
        float YawRadians,
        float ZoomPercent,
        string? SnapName,
        bool PixelPreviewEnabled);

    /// <summary>Preview lighting settings for voxel viewport utilities.</summary>
    public sealed class VoxelLightingSettings
    {
        public bool Enabled { get; set; }
        public Vector3 Position { get; set; } = new(32f, 48f, 32f);
        public uint LightColorBgra { get; set; } = 0xFFFFFFFF;
        public float Intensity { get; set; } = 1f;
        public float Falloff { get; set; } = 0.05f;
        public float Ambient { get; set; } = 0f;
        public bool CastShadows { get; set; }

        public VoxelLightingSettings Clone()
            => new()
            {
                Enabled = Enabled,
                Position = Position,
                LightColorBgra = LightColorBgra,
                Intensity = Intensity,
                Falloff = Falloff,
                Ambient = Ambient,
                CastShadows = CastShadows,
            };
    }
}
