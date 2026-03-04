using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// A flat-colored quad in 3D space, representing one visible face of a voxel.
    /// </summary>
    /// <remarks>
    /// Vertices are in CCW winding order when viewed from outside the voxel.
    /// The quad is split into two triangles (V0,V1,V2) and (V0,V2,V3)
    /// during rasterization.
    /// </remarks>
    public readonly struct ColoredQuad
    {
        /// <summary>Bottom-left vertex.</summary>
        public readonly Vector3 V0;
        /// <summary>Bottom-right vertex.</summary>
        public readonly Vector3 V1;
        /// <summary>Top-right vertex.</summary>
        public readonly Vector3 V2;
        /// <summary>Top-left vertex.</summary>
        public readonly Vector3 V3;
        /// <summary>Outward-facing normal of this face.</summary>
        public readonly Vector3 Normal;
        /// <summary>Flat color for the entire face (shading already applied).</summary>
        public readonly Rgba32 Color;

        public ColoredQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Vector3 normal, Rgba32 color)
        {
            V0 = v0; V1 = v1; V2 = v2; V3 = v3;
            Normal = normal;
            Color = color;
        }
    }

    /// <summary>
    /// Builds renderable geometry from a <see cref="VoxelVolume"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates a list of <see cref="ColoredQuad"/> representing the visible
    /// (exposed) faces of the voxel volume. A face is exposed when the neighboring
    /// voxel in that direction is empty or out of bounds.
    /// </para>
    /// <para>
    /// The resulting mesh is centered at the origin: a volume of size <c>N</c>
    /// spans from <c>−N/2</c> to <c>+N/2</c> on each axis.
    /// </para>
    /// <para>
    /// Optional face shading applies a fixed brightness multiplier per face
    /// direction, simulating top-down ambient lighting:
    /// </para>
    /// <list type="bullet">
    /// <item>Top: 1.00</item>
    /// <item>Front / Back: 0.85</item>
    /// <item>Left / Right: 0.70</item>
    /// <item>Bottom: 0.55</item>
    /// </list>
    /// </remarks>
    public static class CubeGeometry
    {
        // ════════════════════════════════════════════════════════════════════
        // FACE DEFINITIONS
        // ════════════════════════════════════════════════════════════════════
        //
        // Each face is defined as 4 vertex offsets from the voxel's min corner (0,0,0)
        // to its max corner (1,1,1). Vertices are in CCW winding order when viewed
        // from outside the cube.
        //
        //     7────6       Y+
        //    /|   /|       |
        //   4────5 |       |
        //   | 3──|─2       └──── X+
        //   |/   |/       /
        //   0────1       Z+
        //

        /// <summary>Outward normal for each face direction.</summary>
        private static readonly Vector3[] FaceNormals =
        [
            new( 0,  0,  1),   // Front  (+Z)
            new( 0,  0, -1),   // Back   (−Z)
            new(-1,  0,  0),   // Left   (−X)
            new( 1,  0,  0),   // Right  (+X)
            new( 0,  1,  0),   // Top    (+Y)
            new( 0, -1,  0),   // Bottom (−Y)
        ];

        /// <summary>
        /// Vertex offsets for each face (4 vertices per face, CW from outside).
        /// Indexed as FaceVertices[faceIndex][vertexIndex].
        /// </summary>
        /// <remarks>
        /// Winding is clockwise when viewed from outside the cube. This compensates
        /// for the Y-axis flip in the viewport transform, which reverses winding —
        /// after projection these become CCW in screen space and pass the
        /// rasterizer's front-face test.
        /// </remarks>
        private static readonly Vector3[][] FaceVertices =
        [
            // Front (+Z): z=1 face
            [new(0, 1, 1), new(1, 1, 1), new(1, 0, 1), new(0, 0, 1)],
            // Back (−Z): z=0 face
            [new(1, 1, 0), new(0, 1, 0), new(0, 0, 0), new(1, 0, 0)],
            // Left (−X): x=0 face
            [new(0, 1, 0), new(0, 1, 1), new(0, 0, 1), new(0, 0, 0)],
            // Right (+X): x=1 face
            [new(1, 1, 1), new(1, 1, 0), new(1, 0, 0), new(1, 0, 1)],
            // Top (+Y): y=1 face
            [new(0, 1, 0), new(1, 1, 0), new(1, 1, 1), new(0, 1, 1)],
            // Bottom (−Y): y=0 face
            [new(0, 0, 1), new(1, 0, 1), new(1, 0, 0), new(0, 0, 0)],
        ];

        /// <summary>
        /// Neighbor offset for each face direction.
        /// If the voxel at (x + dx, y + dy, z + dz) is empty, the face is exposed.
        /// </summary>
        private static readonly (int dx, int dy, int dz)[] NeighborOffsets =
        [
            ( 0,  0,  1),   // Front  → check z+1
            ( 0,  0, -1),   // Back   → check z−1
            (-1,  0,  0),   // Left   → check x−1
            ( 1,  0,  0),   // Right  → check x+1
            ( 0,  1,  0),   // Top    → check y+1
            ( 0, -1,  0),   // Bottom → check y−1
        ];

        /// <summary>
        /// Per-face shading multiplier for fixed top-down ambient lighting.
        /// Index matches <see cref="Face"/> enum values.
        /// </summary>
        private static readonly float[] FaceShading =
        [
            0.85f,  // Front
            0.85f,  // Back
            0.70f,  // Left
            0.70f,  // Right
            1.00f,  // Top
            0.55f,  // Bottom
        ];

        // ════════════════════════════════════════════════════════════════════
        // MESH BUILDING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a renderable mesh from a <see cref="VoxelVolume"/>.
        /// </summary>
        /// <param name="volume">The voxel volume to generate geometry for.</param>
        /// <param name="applyFaceShading">
        /// When true, applies directional brightness multipliers per face to
        /// simulate top-down ambient lighting.
        /// </param>
        /// <returns>A list of colored quads representing all exposed voxel faces.</returns>
        public static List<ColoredQuad> BuildMesh(VoxelVolume volume, bool applyFaceShading = true)
        {
            int size = volume.Size;
            float half = size / 2f;

            // Pre-allocate with a rough estimate (surface area heuristic)
            var quads = new List<ColoredQuad>(size * size * 6);

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (!volume.IsOccupied(x, y, z)) continue;

                        // Check all 6 faces
                        for (int f = 0; f < 6; f++)
                        {
                            var (dx, dy, dz) = NeighborOffsets[f];
                            int nx = x + dx, ny = y + dy, nz = z + dz;

                            // Face is exposed if neighbor is out of bounds or empty
                            if (!IsEmpty(volume, nx, ny, nz)) continue;

                            // Get face color
                            var color = volume.GetFaceColor(x, y, z, (Face)f);

                            // Apply directional shading
                            if (applyFaceShading)
                                color = ApplyShading(color, FaceShading[f]);

                            // Build quad vertices (offset to world space, centered at origin)
                            var verts = FaceVertices[f];
                            var offset = new Vector3(x - half, y - half, z - half);

                            quads.Add(new ColoredQuad(
                                verts[0] + offset,
                                verts[1] + offset,
                                verts[2] + offset,
                                verts[3] + offset,
                                FaceNormals[f],
                                color));
                        }
                    }
                }
            }

            return quads;
        }

        /// <summary>
        /// Builds an outline (inverted-hull) mesh from a <see cref="VoxelVolume"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Uses the classic inverted-hull technique: takes the same exposed-face
        /// mesh as <see cref="BuildMesh"/>, uniformly scales every vertex outward
        /// from the model center, and reverses the winding order. The rasterizer's
        /// back-face cull then only draws the inside of the expanded shell, which
        /// peeks out around the silhouette edges of the main mesh.
        /// </para>
        /// <para>
        /// Because it's a uniform scale of the exact same geometry, there are no
        /// gaps at corners or faces poking through at concave joints.
        /// </para>
        /// <para>
        /// The scale factor is computed as <c>1 + 2 / size</c>, which expands
        /// the model by 1 voxel (1 pixel of the source tile) on each side.
        /// </para>
        /// </remarks>
        /// <param name="volume">The voxel volume to generate outline geometry for.</param>
        /// <param name="outlineColor">Flat color for the entire outline.</param>
        /// <returns>A list of colored quads with reversed winding for the outline shell.</returns>
        public static List<ColoredQuad> BuildOutlineMesh(
            VoxelVolume volume, Rgba32 outlineColor)
        {
            // Scale factor: expand by 1 voxel on each side
            // Model spans -size/2 to +size/2, so (size/2 + 1) / (size/2) = 1 + 2/size
            float scale = 1f + 2f / volume.Size;

            var mainMesh = BuildMesh(volume, applyFaceShading: false);
            var outline = new List<ColoredQuad>(mainMesh.Count);

            foreach (var quad in mainMesh)
            {
                // Scale from origin (model center), reverse winding, flat outline color
                outline.Add(new ColoredQuad(
                    quad.V3 * scale,
                    quad.V2 * scale,
                    quad.V1 * scale,
                    quad.V0 * scale,
                    -quad.Normal,
                    outlineColor));
            }

            return outline;
        }

        /// <summary>
        /// Returns true if the voxel at (x, y, z) is empty or out of bounds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsEmpty(VoxelVolume volume, int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0) return true;
            if (x >= volume.Size || y >= volume.Size || z >= volume.Size) return true;
            return !volume.IsOccupied(x, y, z);
        }

        /// <summary>
        /// Multiplies RGB channels by a brightness factor, clamping to [0, 255].
        /// Alpha is preserved.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Rgba32 ApplyShading(Rgba32 c, float factor)
        {
            return new Rgba32(
                (byte)Math.Min(255, (int)(c.R * factor)),
                (byte)Math.Min(255, (int)(c.G * factor)),
                (byte)Math.Min(255, (int)(c.B * factor)),
                c.A);
        }
    }
}
