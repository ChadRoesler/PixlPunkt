using System.Numerics;
using PixlPunkt.Core.Voxel;

namespace PixlPunkt.UI.Voxel;

internal enum ModelMeshMode
{
    MergeCoplanar = 0,
    PerVoxel = 1,
}

internal enum ModelAxisPreset
{
    PixlPunkt = 0,
    BlenderZUp = 1,
}

internal enum ModelExportFormat
{
    Obj = 0,
    Glb = 1,
    Stl = 2,
    Vox = 3,
}

internal enum ModelPivotPreset
{
    Center = 0,
    BottomCenter = 1,
    Origin = 2,
}

internal readonly record struct VoxelImageExportOptions(
    int Scale,
    bool TransparentBackground,
    bool IncludeOutline,
    bool IncludeBackdropCage,
    bool IncludeProjectionTiles,
    bool IncludeModelGrid,
    bool TrimTransparentBounds,
    int TrimPadding,
    bool BatchExportViews,
    bool BatchIncludeCardinalViews,
    bool BatchIncludeDirectionalViews);

internal readonly record struct VoxelModelExportOptions(
    ModelExportFormat Format,
    ModelMeshMode MeshMode,
    ModelAxisPreset AxisPreset,
    float UnitScale,
    ModelPivotPreset PivotPreset,
    bool GlbDoubleSided);

internal readonly record struct VoxelExportPreset(
    VoxelImageExportOptions Image,
    VoxelModelExportOptions Model);

internal sealed class VoxelExportPresetFile
{
    public int Version { get; set; } = 1;
    public VoxelImageExportPresetFile? Image { get; set; }
    public VoxelModelExportPresetFile? Model { get; set; }
}

internal sealed class VoxelImageExportPresetFile
{
    public int Scale { get; set; } = 1;
    public bool TransparentBackground { get; set; }
    public bool IncludeOutline { get; set; } = true;
    public bool IncludeBackdropCage { get; set; } = true;
    public bool IncludeProjectionTiles { get; set; } = true;
    public bool IncludeModelGrid { get; set; }
    public bool TrimTransparentBounds { get; set; }
    public int TrimPadding { get; set; } = 1;
    public bool BatchExportViews { get; set; }
    public bool BatchIncludeCardinalViews { get; set; } = true;
    public bool BatchIncludeDirectionalViews { get; set; } = true;
}

internal sealed class VoxelModelExportPresetFile
{
    public int Format { get; set; }
    public int MeshMode { get; set; }
    public int AxisPreset { get; set; }
    public float UnitScale { get; set; } = 1f;
    public int PivotPreset { get; set; }
    public bool GlbDoubleSided { get; set; } = true;
}

internal readonly record struct ExportFaceQuad(
    Face Face,
    int X,
    int Y,
    int Z,
    int Width,
    int Height,
    uint ColorBgra);

internal readonly record struct ExportTriangle(
    Vector3 A,
    Vector3 B,
    Vector3 C,
    Vector3 Normal,
    uint ColorBgra);

internal readonly record struct TransformedVoxelBounds(
    int MinX,
    int MinY,
    int MinZ,
    int MaxX,
    int MaxY,
    int MaxZ)
{
    public int SizeX => (MaxX - MinX) + 1;
    public int SizeY => (MaxY - MinY) + 1;
    public int SizeZ => (MaxZ - MinZ) + 1;
}

internal readonly record struct ObjExportData(
    string ObjText,
    string MtlText,
    int TextureWidth,
    int TextureHeight,
    byte[] TexturePixelsBgra);

internal delegate bool TryGetPlaneColor(int u, int v, out uint color);
