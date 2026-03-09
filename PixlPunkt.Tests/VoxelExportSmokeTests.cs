namespace PixlPunkt.Tests;

using System.Text;
using PixlPunkt.Core.Voxel;
using PixlPunkt.UI.Voxel;

[TestFixture]
public sealed class VoxelExportSmokeTests
{
    [Test]
    public void ObjExport_ProducesObjMtlAndTextureData()
    {
        var volume = CreateSampleVolume();
        var options = CreateOptions(ModelExportFormat.Obj, glbDoubleSided: true);

        var export = VoxelModelExporter.BuildObjExport(volume, "voxel.mtl", "voxel.png", options);

        export.ObjText.Should().Contain("mtllib voxel.mtl");
        export.ObjText.Should().Contain("usemtl voxel_material");
        export.ObjText.Should().Contain("\nv ");
        export.ObjText.Should().Contain("\nf ");

        export.MtlText.Should().Contain("newmtl voxel_material");
        export.MtlText.Should().Contain("map_Kd voxel.png");

        export.TextureWidth.Should().BeGreaterThan(0);
        export.TextureHeight.Should().BeGreaterThan(0);
        export.TexturePixelsBgra.Length.Should().Be(export.TextureWidth * export.TextureHeight * 4);
    }

    [Test]
    public async Task GlbExport_RespectsDoubleSidedToggle()
    {
        var volume = CreateSampleVolume();

        var trueOptions = CreateOptions(ModelExportFormat.Glb, glbDoubleSided: true);
        var falseOptions = CreateOptions(ModelExportFormat.Glb, glbDoubleSided: false);

        var glbTrue = await VoxelModelExporter.BuildGlbExportAsync(volume, trueOptions);
        var glbFalse = await VoxelModelExporter.BuildGlbExportAsync(volume, falseOptions);

        var jsonTrue = ExtractGlbJsonChunk(glbTrue);
        var jsonFalse = ExtractGlbJsonChunk(glbFalse);

        jsonTrue.Should().Contain("\"doubleSided\":true");
        jsonFalse.Should().Contain("\"doubleSided\":false");
    }

    [Test]
    public void StlExport_WritesExpectedBinaryLayout()
    {
        var volume = CreateSampleVolume();
        var options = CreateOptions(ModelExportFormat.Stl, glbDoubleSided: true);

        var bytes = VoxelModelExporter.BuildStlExport(volume, options);

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
        var options = CreateOptions(ModelExportFormat.Vox, glbDoubleSided: true);

        var bytes = VoxelModelExporter.BuildVoxExport(volume, options);

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

    private static VoxelModelExportOptions CreateOptions(ModelExportFormat format, bool glbDoubleSided)
        => new(format, ModelMeshMode.MergeCoplanar, ModelAxisPreset.PixlPunkt, 1f, ModelPivotPreset.Center, glbDoubleSided);

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
