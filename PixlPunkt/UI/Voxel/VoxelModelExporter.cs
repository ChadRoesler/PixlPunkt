using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using PixlPunkt.Core.Voxel;

namespace PixlPunkt.UI.Voxel;

/// <summary>
/// Pure-static helpers that build 3D model export data (OBJ, GLB, STL, VOX)
/// from a <see cref="VoxelVolume"/>. Extracted from <c>VoxelWorkspaceControl</c>.
/// </summary>
internal static class VoxelModelExporter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    // ════════════════════════════════════════════════════════════════════
    // PUBLIC FORMAT HELPERS
    // ════════════════════════════════════════════════════════════════════

    internal static string GetPrimaryExtension(ModelExportFormat format)
        => format switch
        {
            ModelExportFormat.Glb => ".glb",
            ModelExportFormat.Stl => ".stl",
            ModelExportFormat.Vox => ".vox",
            _ => ".obj",
        };

    // ════════════════════════════════════════════════════════════════════
    // OBJ
    // ════════════════════════════════════════════════════════════════════

    internal static ObjExportData BuildObjExport(
        VoxelVolume volume,
        string mtlFileName,
        string textureFileName,
        VoxelModelExportOptions options)
    {
        int size = volume.Size;
        float unitScale = Math.Clamp(options.UnitScale, 0.0001f, 10000f);
        Vector3 pivotOffset = GetModelPivotOffset(size, options.PivotPreset);
        var exportQuads = BuildExportFaceQuads(volume, options.MeshMode);
        var colorToIndex = new Dictionary<uint, int>();
        var colors = new List<uint>();
        for (int i = 0; i < exportQuads.Count; i++)
        {
            uint color = exportQuads[i].ColorBgra;
            if (!colorToIndex.ContainsKey(color))
            {
                colorToIndex[color] = colors.Count;
                colors.Add(color);
            }
        }

        int texSize = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, colors.Count))));
        var texPixels = new byte[texSize * texSize * 4];
        for (int i = 0; i < colors.Count; i++)
        {
            uint c = colors[i];
            int tx = i % texSize;
            int ty = i / texSize;
            int o = (ty * texSize + tx) * 4;
            texPixels[o + 0] = (byte)(c & 0xFF);         // B
            texPixels[o + 1] = (byte)((c >> 8) & 0xFF);  // G
            texPixels[o + 2] = (byte)((c >> 16) & 0xFF); // R
            texPixels[o + 3] = (byte)((c >> 24) & 0xFF); // A
        }

        var obj = new StringBuilder(exportQuads.Count * 120 + 256);
        var mtl = new StringBuilder(256);
        obj.AppendLine("# PixlPunkt Voxel Export");
        obj.Append("mtllib ").AppendLine(EncodeObjPathToken(mtlFileName));
        obj.AppendLine("o voxel_model");
        obj.AppendLine("usemtl voxel_material");

        int vIndex = 1;
        int vtIndex = 1;
        foreach (var f in exportQuads)
        {
            GetFaceQuadVertices(
                f.Face,
                f.X + pivotOffset.X,
                f.Y + pivotOffset.Y,
                f.Z + pivotOffset.Z,
                f.Width,
                f.Height,
                out var p0,
                out var p1,
                out var p2,
                out var p3);
            p0 *= unitScale;
            p1 *= unitScale;
            p2 *= unitScale;
            p3 *= unitScale;
            p0 = TransformExportVertex(p0, options.AxisPreset);
            p1 = TransformExportVertex(p1, options.AxisPreset);
            p2 = TransformExportVertex(p2, options.AxisPreset);
            p3 = TransformExportVertex(p3, options.AxisPreset);
            AppendVertex(obj, p0);
            AppendVertex(obj, p1);
            AppendVertex(obj, p2);
            AppendVertex(obj, p3);

            int ci = colorToIndex[f.ColorBgra];
            float u = ((ci % texSize) + 0.5f) / texSize;
            float v = 1f - (((ci / texSize) + 0.5f) / texSize);

            AppendUv(obj, u, v);
            AppendUv(obj, u, v);
            AppendUv(obj, u, v);
            AppendUv(obj, u, v);

            obj.Append("f ")
               .Append(vIndex).Append('/').Append(vtIndex).Append(' ')
               .Append(vIndex + 1).Append('/').Append(vtIndex + 1).Append(' ')
               .Append(vIndex + 2).Append('/').Append(vtIndex + 2).Append(' ')
               .Append(vIndex + 3).Append('/').Append(vtIndex + 3).AppendLine();

            vIndex += 4;
            vtIndex += 4;
        }

        mtl.AppendLine("newmtl voxel_material");
        mtl.AppendLine("Ka 1.000000 1.000000 1.000000");
        mtl.AppendLine("Kd 1.000000 1.000000 1.000000");
        mtl.AppendLine("Ks 0.000000 0.000000 0.000000");
        mtl.AppendLine("d 1.0");
        mtl.AppendLine("illum 1");
        mtl.Append("map_Kd ").AppendLine(EncodeObjPathToken(textureFileName));

        return new ObjExportData(obj.ToString(), mtl.ToString(), texSize, texSize, texPixels);
    }

    // ════════════════════════════════════════════════════════════════════
    // GLB
    // ════════════════════════════════════════════════════════════════════

    internal static async System.Threading.Tasks.Task<byte[]> BuildGlbExportAsync(
        VoxelVolume volume,
        VoxelModelExportOptions options)
    {
        int size = volume.Size;
        var quads = BuildExportFaceQuads(volume, options.MeshMode);
        if (quads.Count == 0)
        {
            const string emptyJson = "{\"asset\":{\"version\":\"2.0\",\"generator\":\"PixlPunkt\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{}]}";
            return BuildGlbContainer(Encoding.UTF8.GetBytes(emptyJson), Array.Empty<byte>());
        }

        var colorToIndex = new Dictionary<uint, int>();
        var colors = new List<uint>();
        for (int i = 0; i < quads.Count; i++)
        {
            uint color = quads[i].ColorBgra;
            if (!colorToIndex.ContainsKey(color))
            {
                colorToIndex[color] = colors.Count;
                colors.Add(color);
            }
        }

        int texSize = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, colors.Count))));
        var texPixels = new byte[texSize * texSize * 4];
        for (int i = 0; i < colors.Count; i++)
        {
            uint c = colors[i];
            int tx = i % texSize;
            int ty = i / texSize;
            int o = (ty * texSize + tx) * 4;
            texPixels[o + 0] = (byte)(c & 0xFF);
            texPixels[o + 1] = (byte)((c >> 8) & 0xFF);
            texPixels[o + 2] = (byte)((c >> 16) & 0xFF);
            texPixels[o + 3] = (byte)((c >> 24) & 0xFF);
        }
        byte[] texturePng = await VoxelImageExporter.EncodeBgraPngBytesAsync(texSize, texSize, texPixels, transparentBackground: true);

        int vertexCount = quads.Count * 6; // two triangles per quad, no index buffer
        var binStream = new MemoryStream(Math.Max(2048, vertexCount * 32));
        using var binWriter = new BinaryWriter(binStream, Utf8NoBom, leaveOpen: true);

        float minX = float.PositiveInfinity, minY = float.PositiveInfinity, minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity, maxZ = float.NegativeInfinity;

        static void UpdateMinMax(Vector3 p, ref float minX, ref float minY, ref float minZ, ref float maxX, ref float maxY, ref float maxZ)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Z < minZ) minZ = p.Z;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
            if (p.Z > maxZ) maxZ = p.Z;
        }

        static void PadTo4(BinaryWriter writer, MemoryStream stream)
        {
            while ((stream.Position & 3) != 0)
                writer.Write((byte)0);
        }

        int posOffset = (int)binStream.Position;
        for (int i = 0; i < quads.Count; i++)
        {
            GetTransformedFaceVertices(quads[i], size, options, out var p0, out var p1, out var p2, out var p3);
            binWriter.Write(p0.X); binWriter.Write(p0.Y); binWriter.Write(p0.Z);
            binWriter.Write(p1.X); binWriter.Write(p1.Y); binWriter.Write(p1.Z);
            binWriter.Write(p2.X); binWriter.Write(p2.Y); binWriter.Write(p2.Z);
            binWriter.Write(p0.X); binWriter.Write(p0.Y); binWriter.Write(p0.Z);
            binWriter.Write(p2.X); binWriter.Write(p2.Y); binWriter.Write(p2.Z);
            binWriter.Write(p3.X); binWriter.Write(p3.Y); binWriter.Write(p3.Z);

            UpdateMinMax(p0, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            UpdateMinMax(p1, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            UpdateMinMax(p2, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            UpdateMinMax(p3, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
        }
        int posLength = (int)binStream.Position - posOffset;

        PadTo4(binWriter, binStream);
        int normalOffset = (int)binStream.Position;
        for (int i = 0; i < quads.Count; i++)
        {
            GetTransformedFaceVertices(quads[i], size, options, out var p0, out var p1, out var p2, out _);
            var n = Vector3.Cross(p1 - p0, p2 - p0);
            if (n.LengthSquared() > 1e-12f)
                n = Vector3.Normalize(n);
            else
                n = Vector3.UnitY;

            for (int v = 0; v < 6; v++)
            {
                binWriter.Write(n.X);
                binWriter.Write(n.Y);
                binWriter.Write(n.Z);
            }
        }
        int normalLength = (int)binStream.Position - normalOffset;

        PadTo4(binWriter, binStream);
        int uvOffset = (int)binStream.Position;
        for (int i = 0; i < quads.Count; i++)
        {
            int colorIndex = colorToIndex[quads[i].ColorBgra];
            float u = ((colorIndex % texSize) + 0.5f) / texSize;
            float v = 1f - (((colorIndex / texSize) + 0.5f) / texSize);
            for (int vt = 0; vt < 6; vt++)
            {
                binWriter.Write(u);
                binWriter.Write(v);
            }
        }
        int uvLength = (int)binStream.Position - uvOffset;

        PadTo4(binWriter, binStream);
        int imageOffset = (int)binStream.Position;
        binWriter.Write(texturePng);
        int imageLength = texturePng.Length;

        var bin = binStream.ToArray();
        string F(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        var json = new StringBuilder(1400);
        string glbDoubleSided = options.GlbDoubleSided ? "true" : "false";
        json.Append('{')
            .Append("\"asset\":{\"version\":\"2.0\",\"generator\":\"PixlPunkt\"},")
            .Append("\"scene\":0,")
            .Append("\"scenes\":[{\"nodes\":[0]}],")
            .Append("\"nodes\":[{\"mesh\":0}],")
            .Append("\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0,\"NORMAL\":1,\"TEXCOORD_0\":2},\"material\":0,\"mode\":4}]}],")
            .Append("\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0},\"metallicFactor\":0,\"roughnessFactor\":1},\"doubleSided\":")
            .Append(glbDoubleSided)
            .Append(",\"alphaMode\":\"BLEND\"}],")
            .Append("\"textures\":[{\"sampler\":0,\"source\":0}],")
            .Append("\"samplers\":[{\"magFilter\":9728,\"minFilter\":9728,\"wrapS\":33071,\"wrapT\":33071}],")
            .Append("\"images\":[{\"bufferView\":3,\"mimeType\":\"image/png\"}],")
            .Append("\"buffers\":[{\"byteLength\":").Append(bin.Length).Append("}],")
            .Append("\"bufferViews\":[")
            .Append("{\"buffer\":0,\"byteOffset\":").Append(posOffset).Append(",\"byteLength\":").Append(posLength).Append(",\"target\":34962},")
            .Append("{\"buffer\":0,\"byteOffset\":").Append(normalOffset).Append(",\"byteLength\":").Append(normalLength).Append(",\"target\":34962},")
            .Append("{\"buffer\":0,\"byteOffset\":").Append(uvOffset).Append(",\"byteLength\":").Append(uvLength).Append(",\"target\":34962},")
            .Append("{\"buffer\":0,\"byteOffset\":").Append(imageOffset).Append(",\"byteLength\":").Append(imageLength).Append("}")
            .Append("],")
            .Append("\"accessors\":[")
            .Append("{\"bufferView\":0,\"componentType\":5126,\"count\":").Append(vertexCount).Append(",\"type\":\"VEC3\",\"min\":[")
            .Append(F(minX)).Append(',').Append(F(minY)).Append(',').Append(F(minZ)).Append("],\"max\":[")
            .Append(F(maxX)).Append(',').Append(F(maxY)).Append(',').Append(F(maxZ)).Append("]},")
            .Append("{\"bufferView\":1,\"componentType\":5126,\"count\":").Append(vertexCount).Append(",\"type\":\"VEC3\"},")
            .Append("{\"bufferView\":2,\"componentType\":5126,\"count\":").Append(vertexCount).Append(",\"type\":\"VEC2\"}")
            .Append("]")
            .Append('}');

        return BuildGlbContainer(Encoding.UTF8.GetBytes(json.ToString()), bin);
    }

    private static byte[] BuildGlbContainer(byte[] jsonUtf8, byte[] bin)
    {
        jsonUtf8 ??= Array.Empty<byte>();
        bin ??= Array.Empty<byte>();

        int jsonPaddedLength = (jsonUtf8.Length + 3) & ~3;
        int binPaddedLength = (bin.Length + 3) & ~3;
        int totalLength = 12 + 8 + jsonPaddedLength + 8 + binPaddedLength;

        using var stream = new MemoryStream(totalLength);
        using var writer = new BinaryWriter(stream, Utf8NoBom, leaveOpen: true);

        writer.Write(0x46546C67); // glTF
        writer.Write(2);          // version
        writer.Write(totalLength);

        writer.Write(jsonPaddedLength);
        writer.Write(0x4E4F534A); // JSON
        writer.Write(jsonUtf8);
        for (int i = jsonUtf8.Length; i < jsonPaddedLength; i++)
            writer.Write((byte)0x20);

        writer.Write(binPaddedLength);
        writer.Write(0x004E4942); // BIN
        writer.Write(bin);
        for (int i = bin.Length; i < binPaddedLength; i++)
            writer.Write((byte)0x00);

        return stream.ToArray();
    }

    // ════════════════════════════════════════════════════════════════════
    // STL
    // ════════════════════════════════════════════════════════════════════

    internal static byte[] BuildStlExport(VoxelVolume volume, VoxelModelExportOptions options)
    {
        var triangles = BuildExportTriangles(volume, options);
        uint triangleCount = (uint)triangles.Count;

        using var stream = new MemoryStream(84 + (int)triangleCount * 50);
        using var writer = new BinaryWriter(stream, Utf8NoBom, leaveOpen: true);

        var header = new byte[80];
        var headerText = Encoding.ASCII.GetBytes("PixlPunkt Voxel STL");
        Buffer.BlockCopy(headerText, 0, header, 0, Math.Min(headerText.Length, header.Length));
        writer.Write(header);
        writer.Write(triangleCount);

        for (int i = 0; i < triangles.Count; i++)
        {
            var t = triangles[i];
            writer.Write(t.Normal.X);
            writer.Write(t.Normal.Y);
            writer.Write(t.Normal.Z);

            writer.Write(t.A.X); writer.Write(t.A.Y); writer.Write(t.A.Z);
            writer.Write(t.B.X); writer.Write(t.B.Y); writer.Write(t.B.Z);
            writer.Write(t.C.X); writer.Write(t.C.Y); writer.Write(t.C.Z);
            writer.Write((ushort)0);
        }

        return stream.ToArray();
    }

    // ════════════════════════════════════════════════════════════════════
    // VOX (MagicaVoxel)
    // ════════════════════════════════════════════════════════════════════

    internal static byte[] BuildVoxExport(VoxelVolume volume, VoxelModelExportOptions options)
    {
        var voxels = new List<(int X, int Y, int Z, byte PaletteIndex)>(Math.Max(1, volume.OccupiedCount));
        var palette = new List<uint>(255);
        var paletteLookup = new Dictionary<uint, byte>();
        int size = volume.Size;

        var bounds = ComputeTransformedVoxelBounds(volume, options.AxisPreset);
        int minX = bounds?.MinX ?? 0;
        int minY = bounds?.MinY ?? 0;
        int minZ = bounds?.MinZ ?? 0;
        int sizeX = bounds?.SizeX ?? 1;
        int sizeY = bounds?.SizeY ?? 1;
        int sizeZ = bounds?.SizeZ ?? 1;

        for (int z = 0; z < size; z++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!volume.IsOccupied(x, y, z))
                        continue;

                    var tc = TransformVoxelCoordinate(x, y, z, options.AxisPreset);

                    uint color = GetRepresentativeVoxelColor(volume, x, y, z);
                    byte paletteIndex = ResolveVoxPaletteIndex(color, palette, paletteLookup);
                    voxels.Add((tc.X, tc.Y, tc.Z, paletteIndex));
                }
            }
        }
        if (sizeX > 255 || sizeY > 255 || sizeZ > 255)
        {
            throw new InvalidOperationException(
                $"VOX export supports <=255 units per axis. Current transformed size is {sizeX}x{sizeY}x{sizeZ}.");
        }

        using var childrenStream = new MemoryStream();
        using var childrenWriter = new BinaryWriter(childrenStream, Utf8NoBom, leaveOpen: true);

        WriteChunk(childrenWriter, "SIZE", contentWriter =>
        {
            contentWriter.Write(sizeX);
            contentWriter.Write(sizeY);
            contentWriter.Write(sizeZ);
        });

        WriteChunk(childrenWriter, "XYZI", contentWriter =>
        {
            contentWriter.Write(voxels.Count);
            for (int i = 0; i < voxels.Count; i++)
            {
                var v = voxels[i];
                contentWriter.Write((byte)(v.X - minX));
                contentWriter.Write((byte)(v.Y - minY));
                contentWriter.Write((byte)(v.Z - minZ));
                contentWriter.Write(v.PaletteIndex);
            }
        });

        WriteChunk(childrenWriter, "RGBA", contentWriter =>
        {
            for (int i = 0; i < 256; i++)
            {
                uint rgba = i == 0 || i > palette.Count ? 0u : BgraToRgba(palette[i - 1]);
                contentWriter.Write((byte)(rgba & 0xFF));
                contentWriter.Write((byte)((rgba >> 8) & 0xFF));
                contentWriter.Write((byte)((rgba >> 16) & 0xFF));
                contentWriter.Write((byte)((rgba >> 24) & 0xFF));
            }
        });

        byte[] childrenBytes = childrenStream.ToArray();
        using var outStream = new MemoryStream(32 + childrenBytes.Length);
        using var writer = new BinaryWriter(outStream, Utf8NoBom, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("VOX "));
        writer.Write(150); // VOX version
        writer.Write(Encoding.ASCII.GetBytes("MAIN"));
        writer.Write(0); // main content size
        writer.Write(childrenBytes.Length); // child chunks size
        writer.Write(childrenBytes);

        return outStream.ToArray();
    }

    internal static TransformedVoxelBounds? ComputeTransformedVoxelBounds(VoxelVolume? volume, ModelAxisPreset axisPreset)
    {
        if (volume == null || volume.OccupiedCount <= 0)
            return null;

        int lMinX = int.MaxValue;
        int lMinY = int.MaxValue;
        int lMinZ = int.MaxValue;
        int lMaxX = int.MinValue;
        int lMaxY = int.MinValue;
        int lMaxZ = int.MinValue;
        int size = volume.Size;

        for (int z = 0; z < size; z++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!volume.IsOccupied(x, y, z))
                        continue;

                    var tc = TransformVoxelCoordinate(x, y, z, axisPreset);
                    if (tc.X < lMinX) lMinX = tc.X;
                    if (tc.Y < lMinY) lMinY = tc.Y;
                    if (tc.Z < lMinZ) lMinZ = tc.Z;
                    if (tc.X > lMaxX) lMaxX = tc.X;
                    if (tc.Y > lMaxY) lMaxY = tc.Y;
                    if (tc.Z > lMaxZ) lMaxZ = tc.Z;
                }
            }
        }

        if (lMinX == int.MaxValue)
            return null;

        return new TransformedVoxelBounds(lMinX, lMinY, lMinZ, lMaxX, lMaxY, lMaxZ);
    }

    // ════════════════════════════════════════════════════════════════════
    // MESH GENERATION
    // ════════════════════════════════════════════════════════════════════

    internal static List<ExportFaceQuad> BuildExportFaceQuads(VoxelVolume volume, ModelMeshMode mode)
        => mode == ModelMeshMode.MergeCoplanar
            ? BuildMergedExportFaceQuads(volume)
            : BuildPerVoxelExportFaceQuads(volume);

    private static List<ExportFaceQuad> BuildPerVoxelExportFaceQuads(VoxelVolume volume)
    {
        int size = volume.Size;
        var quads = new List<ExportFaceQuad>(Math.Max(128, volume.OccupiedCount * 3));

        for (int z = 0; z < size; z++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!volume.IsOccupied(x, y, z))
                        continue;

                    TryAddFace(Face.Front, x, y, z, 0, 0, -1);
                    TryAddFace(Face.Back, x, y, z, 0, 0, 1);
                    TryAddFace(Face.Left, x, y, z, -1, 0, 0);
                    TryAddFace(Face.Right, x, y, z, 1, 0, 0);
                    TryAddFace(Face.Top, x, y, z, 0, 1, 0);
                    TryAddFace(Face.Bottom, x, y, z, 0, -1, 0);
                }
            }
        }

        return quads;

        void TryAddFace(Face face, int x, int y, int z, int nx, int ny, int nz)
        {
            if (volume.IsOccupied(x + nx, y + ny, z + nz))
                return;
            quads.Add(new ExportFaceQuad(face, x, y, z, 1, 1, ToBgra(volume.GetFaceColor(x, y, z, face))));
        }
    }

    private static List<ExportFaceQuad> BuildMergedExportFaceQuads(VoxelVolume volume)
    {
        int size = volume.Size;
        var quads = new List<ExportFaceQuad>(Math.Max(64, volume.OccupiedCount));

        // Front/back faces (planes across Z; merged over X/Y).
        for (int z = 0; z < size; z++)
        {
            BuildPlaneQuads(
                width: size,
                height: size,
                tryGetColor: (int u, int v, out uint color) =>
                {
                    int x = u;
                    int y = v;
                    if (!volume.IsOccupied(x, y, z) || volume.IsOccupied(x, y, z - 1))
                    {
                        color = 0;
                        return false;
                    }

                    color = ToBgra(volume.GetFaceColor(x, y, z, Face.Front));
                    return true;
                },
                emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Front, u, v, z, w, h, color)));

            BuildPlaneQuads(
                width: size,
                height: size,
                tryGetColor: (int u, int v, out uint color) =>
                {
                    int x = u;
                    int y = v;
                    if (!volume.IsOccupied(x, y, z) || volume.IsOccupied(x, y, z + 1))
                    {
                        color = 0;
                        return false;
                    }

                    color = ToBgra(volume.GetFaceColor(x, y, z, Face.Back));
                    return true;
                },
                emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Back, u, v, z, w, h, color)));
        }

        // Left/right faces (planes across X; merged over Z/Y).
        for (int x = 0; x < size; x++)
        {
            BuildPlaneQuads(
                width: size,
                height: size,
                tryGetColor: (int u, int v, out uint color) =>
                {
                    int z1 = u;
                    int y = v;
                    if (!volume.IsOccupied(x, y, z1) || volume.IsOccupied(x - 1, y, z1))
                    {
                        color = 0;
                        return false;
                    }

                    color = ToBgra(volume.GetFaceColor(x, y, z1, Face.Left));
                    return true;
                },
                emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Left, x, v, u, w, h, color)));

            BuildPlaneQuads(
                width: size,
                height: size,
                tryGetColor: (int u, int v, out uint color) =>
                {
                    int z1 = u;
                    int y = v;
                    if (!volume.IsOccupied(x, y, z1) || volume.IsOccupied(x + 1, y, z1))
                    {
                        color = 0;
                        return false;
                    }

                    color = ToBgra(volume.GetFaceColor(x, y, z1, Face.Right));
                    return true;
                },
                emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Right, x, v, u, w, h, color)));
        }

        // Top/bottom faces (planes across Y; merged over X/Z).
        for (int y = 0; y < size; y++)
        {
            BuildPlaneQuads(
                width: size,
                height: size,
                tryGetColor: (int u, int v, out uint color) =>
                {
                    int x = u;
                    int z1 = v;
                    if (!volume.IsOccupied(x, y, z1) || volume.IsOccupied(x, y + 1, z1))
                    {
                        color = 0;
                        return false;
                    }

                    color = ToBgra(volume.GetFaceColor(x, y, z1, Face.Top));
                    return true;
                },
                emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Top, u, y, v, w, h, color)));

            BuildPlaneQuads(
                width: size,
                height: size,
                tryGetColor: (int u, int v, out uint color) =>
                {
                    int x = u;
                    int z1 = v;
                    if (!volume.IsOccupied(x, y, z1) || volume.IsOccupied(x, y - 1, z1))
                    {
                        color = 0;
                        return false;
                    }

                    color = ToBgra(volume.GetFaceColor(x, y, z1, Face.Bottom));
                    return true;
                },
                emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Bottom, u, y, v, w, h, color)));
        }

        return quads;
    }

    private static void BuildPlaneQuads(
        int width,
        int height,
        TryGetPlaneColor tryGetColor,
        Action<int, int, int, int, uint> emit)
    {
        int size = width * height;
        var colors = new uint[size];
        var mask = ArrayPool<bool>.Shared.Rent(size);
        var visited = ArrayPool<bool>.Shared.Rent(size);

        try
        {
            Array.Clear(mask, 0, size);
            Array.Clear(visited, 0, size);

            for (int v = 0; v < height; v++)
            {
                for (int u = 0; u < width; u++)
                {
                    int idx = (v * width) + u;
                    if (!tryGetColor(u, v, out uint color))
                        continue;
                    mask[idx] = true;
                    colors[idx] = color;
                }
            }

            for (int v = 0; v < height; v++)
            {
                for (int u = 0; u < width; u++)
                {
                    int idx = (v * width) + u;
                    if (!mask[idx] || visited[idx])
                        continue;

                    uint color = colors[idx];
                    int quadW = 1;
                    while ((u + quadW) < width)
                    {
                        int n = (v * width) + (u + quadW);
                        if (!mask[n] || visited[n] || colors[n] != color)
                            break;
                        quadW++;
                    }

                    int quadH = 1;
                    while ((v + quadH) < height)
                    {
                        bool rowOk = true;
                        for (int ux = 0; ux < quadW; ux++)
                        {
                            int n = ((v + quadH) * width) + (u + ux);
                            if (!mask[n] || visited[n] || colors[n] != color)
                            {
                                rowOk = false;
                                break;
                            }
                        }

                        if (!rowOk)
                            break;
                        quadH++;
                    }

                    for (int vy = 0; vy < quadH; vy++)
                    {
                        for (int ux = 0; ux < quadW; ux++)
                        {
                            visited[((v + vy) * width) + (u + ux)] = true;
                        }
                    }

                    emit(u, v, quadW, quadH, color);
                }
            }
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(visited);
            ArrayPool<bool>.Shared.Return(mask);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // GEOMETRY HELPERS
    // ════════════════════════════════════════════════════════════════════

    private static List<ExportTriangle> BuildExportTriangles(VoxelVolume volume, VoxelModelExportOptions options)
    {
        int size = volume.Size;
        var quads = BuildExportFaceQuads(volume, options.MeshMode);
        var triangles = new List<ExportTriangle>(Math.Max(1, quads.Count * 2));

        for (int i = 0; i < quads.Count; i++)
        {
            GetTransformedFaceVertices(quads[i], size, options, out var p0, out var p1, out var p2, out var p3);

            var n = Vector3.Cross(p1 - p0, p2 - p0);
            if (n.LengthSquared() <= 1e-12f)
                continue;
            n = Vector3.Normalize(n);

            triangles.Add(new ExportTriangle(p0, p1, p2, n, quads[i].ColorBgra));
            triangles.Add(new ExportTriangle(p0, p2, p3, n, quads[i].ColorBgra));
        }

        return triangles;
    }

    private static void GetTransformedFaceVertices(
        ExportFaceQuad faceQuad,
        int volumeSize,
        VoxelModelExportOptions options,
        out Vector3 p0,
        out Vector3 p1,
        out Vector3 p2,
        out Vector3 p3)
    {
        float unitScale = Math.Clamp(options.UnitScale, 0.0001f, 10000f);
        Vector3 pivotOffset = GetModelPivotOffset(volumeSize, options.PivotPreset);

        GetFaceQuadVertices(
            faceQuad.Face,
            faceQuad.X + pivotOffset.X,
            faceQuad.Y + pivotOffset.Y,
            faceQuad.Z + pivotOffset.Z,
            faceQuad.Width,
            faceQuad.Height,
            out p0,
            out p1,
            out p2,
            out p3);

        p0 *= unitScale;
        p1 *= unitScale;
        p2 *= unitScale;
        p3 *= unitScale;

        p0 = TransformExportVertex(p0, options.AxisPreset);
        p1 = TransformExportVertex(p1, options.AxisPreset);
        p2 = TransformExportVertex(p2, options.AxisPreset);
        p3 = TransformExportVertex(p3, options.AxisPreset);
    }

    private static void GetFaceQuadVertices(
        Face face,
        float x,
        float y,
        float z,
        int width,
        int height,
        out Vector3 p0,
        out Vector3 p1,
        out Vector3 p2,
        out Vector3 p3)
    {
        // Local voxel corners in [0,1] space; matches renderer face orientation.
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        float w = width;
        float h = height;

        switch (face)
        {
            case Face.Back:
                p0 = new Vector3(x + 0f, y + 0f, z + 1f);
                p1 = new Vector3(x + w, y + 0f, z + 1f);
                p2 = new Vector3(x + w, y + h, z + 1f);
                p3 = new Vector3(x + 0f, y + h, z + 1f);
                break;
            case Face.Front:
                p0 = new Vector3(x + w, y + 0f, z + 0f);
                p1 = new Vector3(x + 0f, y + 0f, z + 0f);
                p2 = new Vector3(x + 0f, y + h, z + 0f);
                p3 = new Vector3(x + w, y + h, z + 0f);
                break;
            case Face.Left:
                p0 = new Vector3(x + 0f, y + 0f, z + 0f);
                p1 = new Vector3(x + 0f, y + 0f, z + w);
                p2 = new Vector3(x + 0f, y + h, z + w);
                p3 = new Vector3(x + 0f, y + h, z + 0f);
                break;
            case Face.Right:
                p0 = new Vector3(x + 1f, y + 0f, z + w);
                p1 = new Vector3(x + 1f, y + 0f, z + 0f);
                p2 = new Vector3(x + 1f, y + h, z + 0f);
                p3 = new Vector3(x + 1f, y + h, z + w);
                break;
            case Face.Top:
                p0 = new Vector3(x + 0f, y + 1f, z + h);
                p1 = new Vector3(x + w, y + 1f, z + h);
                p2 = new Vector3(x + w, y + 1f, z + 0f);
                p3 = new Vector3(x + 0f, y + 1f, z + 0f);
                break;
            default:
                p0 = new Vector3(x + 0f, y + 0f, z + 0f);
                p1 = new Vector3(x + w, y + 0f, z + 0f);
                p2 = new Vector3(x + w, y + 0f, z + h);
                p3 = new Vector3(x + 0f, y + 0f, z + h);
                break;
        }
    }

    private static Vector3 TransformExportVertex(Vector3 p, ModelAxisPreset axisPreset)
        => axisPreset switch
        {
            ModelAxisPreset.BlenderZUp => new Vector3(p.X, p.Z, -p.Y),
            _ => p,
        };

    private static (int X, int Y, int Z) TransformVoxelCoordinate(int x, int y, int z, ModelAxisPreset axisPreset)
        => axisPreset switch
        {
            ModelAxisPreset.BlenderZUp => (x, z, -y),
            _ => (x, y, z),
        };

    private static Vector3 GetModelPivotOffset(int size, ModelPivotPreset pivotPreset)
    {
        float half = MathF.Max(1f, size) * 0.5f;
        return pivotPreset switch
        {
            ModelPivotPreset.BottomCenter => new Vector3(-half, 0f, -half),
            ModelPivotPreset.Origin => Vector3.Zero,
            _ => new Vector3(-half, -half, -half),
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // VOX HELPERS
    // ════════════════════════════════════════════════════════════════════

    private static void WriteChunk(BinaryWriter writer, string id, Action<BinaryWriter> writeContent)
    {
        using var contentStream = new MemoryStream();
        using (var contentWriter = new BinaryWriter(contentStream, Utf8NoBom, leaveOpen: true))
        {
            writeContent(contentWriter);
        }

        byte[] contentBytes = contentStream.ToArray();
        writer.Write(Encoding.ASCII.GetBytes(id));
        writer.Write(contentBytes.Length);
        writer.Write(0); // no nested children
        writer.Write(contentBytes);
    }

    private static uint GetRepresentativeVoxelColor(VoxelVolume volume, int x, int y, int z)
    {
        int sumR = 0, sumG = 0, sumB = 0, count = 0;
        foreach (Face face in Enum.GetValues(typeof(Face)))
        {
            var c = volume.GetFaceColor(x, y, z, face);
            if (c.A == 0)
                continue;
            sumR += c.R;
            sumG += c.G;
            sumB += c.B;
            count++;
        }

        if (count <= 0)
        {
            var fallback = volume.GetFaceColor(x, y, z, Face.Front);
            return ToBgra(fallback);
        }

        byte r = (byte)(sumR / count);
        byte g = (byte)(sumG / count);
        byte b = (byte)(sumB / count);
        return (0xFFu << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static byte ResolveVoxPaletteIndex(uint color, List<uint> palette, Dictionary<uint, byte> lookup)
    {
        if (lookup.TryGetValue(color, out byte existing))
            return existing;

        if (palette.Count < 255)
        {
            byte next = (byte)(palette.Count + 1);
            palette.Add(color);
            lookup[color] = next;
            return next;
        }

        byte nearest = FindNearestPaletteIndex(color, palette);
        lookup[color] = nearest;
        return nearest;
    }

    private static byte FindNearestPaletteIndex(uint color, List<uint> palette)
    {
        int r = (int)((color >> 16) & 0xFF);
        int g = (int)((color >> 8) & 0xFF);
        int b = (int)(color & 0xFF);
        int bestScore = int.MaxValue;
        byte bestIndex = 1;

        for (int i = 0; i < palette.Count; i++)
        {
            uint c = palette[i];
            int dr = r - (int)((c >> 16) & 0xFF);
            int dg = g - (int)((c >> 8) & 0xFF);
            int db = b - (int)(c & 0xFF);
            int score = (dr * dr) + (dg * dg) + (db * db);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = (byte)(i + 1);
            }
        }

        return bestIndex;
    }

    private static uint BgraToRgba(uint bgra)
    {
        byte b = (byte)(bgra & 0xFF);
        byte g = (byte)((bgra >> 8) & 0xFF);
        byte r = (byte)((bgra >> 16) & 0xFF);
        byte a = (byte)((bgra >> 24) & 0xFF);
        return ((uint)r) | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
    }

    // ════════════════════════════════════════════════════════════════════
    // OBJ HELPERS
    // ════════════════════════════════════════════════════════════════════

    private static void AppendVertex(StringBuilder sb, Vector3 p)
    {
        sb.Append("v ")
          .Append(p.X.ToString("0.######", CultureInfo.InvariantCulture)).Append(' ')
          .Append(p.Y.ToString("0.######", CultureInfo.InvariantCulture)).Append(' ')
          .Append(p.Z.ToString("0.######", CultureInfo.InvariantCulture)).AppendLine();
    }

    private static void AppendUv(StringBuilder sb, float u, float v)
    {
        sb.Append("vt ")
          .Append(u.ToString("0.######", CultureInfo.InvariantCulture)).Append(' ')
          .Append(v.ToString("0.######", CultureInfo.InvariantCulture)).AppendLine();
    }

    private static string EncodeObjPathToken(string fileName)
        => (fileName ?? string.Empty)
            .Replace('\\', '/')
            .Replace(" ", "\\ ");

    internal static uint ToBgra(Rgba32 c)
        => ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;

    internal static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "voxel_export";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        }

        var safe = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(safe) ? "voxel_export" : safe;
    }
}
