namespace PixlPunkt.Tests;

using System.Reflection;
using System.Text;
using PixlPunkt.Core.Voxel;
using PixlPunkt.UI.Voxel;

[TestFixture]
public sealed class VoxelExportSmokeTests
{
    private static readonly Type WorkspaceType = typeof(VoxelWorkspaceControl);
    private static readonly Type OptionsType = WorkspaceType.GetNestedType("VoxelModelExportOptions", BindingFlags.NonPublic)!;
    private static readonly Type FormatType = WorkspaceType.GetNestedType("ModelExportFormat", BindingFlags.NonPublic)!;
    private static readonly Type MeshModeType = WorkspaceType.GetNestedType("ModelMeshMode", BindingFlags.NonPublic)!;
    private static readonly Type AxisPresetType = WorkspaceType.GetNestedType("ModelAxisPreset", BindingFlags.NonPublic)!;
    private static readonly Type PivotPresetType = WorkspaceType.GetNestedType("ModelPivotPreset", BindingFlags.NonPublic)!;

    [Test]
    public void ObjExport_ProducesObjMtlAndTextureData()
    {
        var volume = CreateSampleVolume();
        var options = CreateOptions(formatName: "Obj", glbDoubleSided: true);

        var export = InvokePrivateStatic("BuildObjExport", volume, "voxel.mtl", "voxel.png", options);
        export.Should().NotBeNull();

        var exportType = export!.GetType();
        var objText = (string)exportType.GetProperty("ObjText")!.GetValue(export)!;
        var mtlText = (string)exportType.GetProperty("MtlText")!.GetValue(export)!;
        var textureWidth = (int)exportType.GetProperty("TextureWidth")!.GetValue(export)!;
        var textureHeight = (int)exportType.GetProperty("TextureHeight")!.GetValue(export)!;
        var texturePixels = (byte[])exportType.GetProperty("TexturePixelsBgra")!.GetValue(export)!;

        objText.Should().Contain("mtllib voxel.mtl");
        objText.Should().Contain("usemtl voxel_material");
        objText.Should().Contain("\nv ");
        objText.Should().Contain("\nf ");

        mtlText.Should().Contain("newmtl voxel_material");
        mtlText.Should().Contain("map_Kd voxel.png");

        textureWidth.Should().BeGreaterThan(0);
        textureHeight.Should().BeGreaterThan(0);
        texturePixels.Length.Should().Be(textureWidth * textureHeight * 4);
    }

    [Test]
    public async Task GlbExport_RespectsDoubleSidedToggle()
    {
        var volume = CreateSampleVolume();

        var trueOptions = CreateOptions(formatName: "Glb", glbDoubleSided: true);
        var falseOptions = CreateOptions(formatName: "Glb", glbDoubleSided: false);

        var glbTrue = await InvokePrivateStaticAsync("BuildGlbExportAsync", volume, trueOptions);
        var glbFalse = await InvokePrivateStaticAsync("BuildGlbExportAsync", volume, falseOptions);

        var jsonTrue = ExtractGlbJsonChunk(glbTrue);
        var jsonFalse = ExtractGlbJsonChunk(glbFalse);

        jsonTrue.Should().Contain("\"doubleSided\":true");
        jsonFalse.Should().Contain("\"doubleSided\":false");
    }

    [Test]
    public void StlExport_WritesExpectedBinaryLayout()
    {
        var volume = CreateSampleVolume();
        var options = CreateOptions(formatName: "Stl", glbDoubleSided: true);

        var bytes = (byte[])InvokePrivateStatic("BuildStlExport", volume, options)!;

        bytes.Length.Should().BeGreaterThanOrEqualTo(84);
        var headerText = Encoding.ASCII.GetString(bytes, 0, 80).TrimEnd('\0');
        headerText.Should().Contain("PixlPunkt Voxel STL");

        uint triangleCount = BitConverter.ToUInt32(bytes, 80);
        triangleCount.Should().BeGreaterThan(0);
        bytes.Length.Should().Be(84 + ((int)triangleCount * 50));
    }

    [Test]
    public void VoxExport_WritesMainSizeXyziAndRgbaChunks()
    {
        var volume = CreateSampleVolume();
        var options = CreateOptions(formatName: "Vox", glbDoubleSided: true);

        var bytes = (byte[])InvokePrivateStatic("BuildVoxExport", volume, options)!;

        bytes.Length.Should().BeGreaterThan(24);
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("VOX ");
        BitConverter.ToInt32(bytes, 4).Should().Be(150);

        ContainsAscii(bytes, "MAIN").Should().BeTrue();
        ContainsAscii(bytes, "SIZE").Should().BeTrue();
        ContainsAscii(bytes, "XYZI").Should().BeTrue();
        ContainsAscii(bytes, "RGBA").Should().BeTrue();
    }

    private static VoxelVolume CreateSampleVolume()
    {
        var volume = new VoxelVolume(4);
        volume.SetVoxel(1, 1, 1, new Rgba32(40, 40, 40, 255));
        volume.SetFaceColor(1, 1, 1, Face.Front, new Rgba32(255, 0, 0, 255));
        volume.SetFaceColor(1, 1, 1, Face.Back, new Rgba32(0, 255, 0, 255));
        volume.SetFaceColor(1, 1, 1, Face.Left, new Rgba32(0, 0, 255, 255));
        volume.SetFaceColor(1, 1, 1, Face.Right, new Rgba32(255, 255, 0, 255));
        volume.SetFaceColor(1, 1, 1, Face.Top, new Rgba32(255, 0, 255, 255));
        volume.SetFaceColor(1, 1, 1, Face.Bottom, new Rgba32(0, 255, 255, 255));
        return volume;
    }

    private static object CreateOptions(string formatName, bool glbDoubleSided)
    {
        var format = Enum.Parse(FormatType, formatName);
        var meshMode = Enum.Parse(MeshModeType, "MergeCoplanar");
        var axisPreset = Enum.Parse(AxisPresetType, "PixlPunkt");
        var pivotPreset = Enum.Parse(PivotPresetType, "Center");
        return Activator.CreateInstance(
            OptionsType,
            [format, meshMode, axisPreset, 1f, pivotPreset, glbDoubleSided])!;
    }

    private static object? InvokePrivateStatic(string methodName, params object[] args)
    {
        var method = WorkspaceType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        return method!.Invoke(null, args);
    }

    private static async Task<byte[]> InvokePrivateStaticAsync(string methodName, params object[] args)
    {
        var task = (Task<byte[]>)InvokePrivateStatic(methodName, args)!;
        return await task.ConfigureAwait(false);
    }

    private static string ExtractGlbJsonChunk(byte[] bytes)
    {
        bytes.Length.Should().BeGreaterThan(24);
        BitConverter.ToUInt32(bytes, 0).Should().Be(0x46546C67); // glTF
        BitConverter.ToUInt32(bytes, 16).Should().Be(0x4E4F534A); // JSON
        int jsonLength = BitConverter.ToInt32(bytes, 12);
        jsonLength.Should().BeGreaterThan(0);
        var json = Encoding.UTF8.GetString(bytes, 20, jsonLength);
        return json.TrimEnd(' ', '\0');
    }

    private static bool ContainsAscii(byte[] bytes, string token)
    {
        var needle = Encoding.ASCII.GetBytes(token);
        if (needle.Length == 0 || needle.Length > bytes.Length)
            return false;

        for (int i = 0; i <= bytes.Length - needle.Length; i++)
        {
            bool matched = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (bytes[i + j] != needle[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                return true;
        }

        return false;
    }
}
