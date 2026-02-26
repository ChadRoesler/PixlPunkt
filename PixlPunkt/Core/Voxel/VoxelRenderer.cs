using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// Renders a <see cref="VoxelVolume"/> to a BGRA pixel buffer using
    /// screen-space triangle rasterization with depth testing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Matches the reference SkiaVoxelRenderer rendering approach:
    /// </para>
    /// <list type="number">
    /// <item>For each occupied voxel, emit exposed faces.</item>
    /// <item>Backface cull using camera direction dot product.</item>
    /// <item>Project vertices to screen space via the camera frustum.</item>
    /// <item>Rasterize each face as a filled quad (two triangles) with z-buffering.</item>
    /// <item>Optionally draw a postprocess silhouette outline.</item>
    /// </list>
    /// <para>
    /// Face lighting applies a fixed brightness multiplier per face direction,
    /// simulating directional ambient light.
    /// </para>
    /// </remarks>
    public static class VoxelRenderer
    {
        /// <summary>Configurable render options.</summary>
        public sealed class RenderOptions
        {
            /// <summary>Enable backface culling. Default true.</summary>
            public bool BackfaceCull { get; set; } = true;
            /// <summary>
            /// Cull threshold in dot-product space. Faces are culled only when they
            /// are clearly facing away (dot &lt; -epsilon). Helps stabilize edge-on faces.
            /// </summary>
            public float BackfaceCullEpsilon { get; set; } = 1e-4f;
            /// <summary>Draw silhouette-only outline. Default false.</summary>
            public bool DrawOutline { get; set; }
            /// <summary>Outline color as packed BGRA. Default black.</summary>
            public uint OutlineColor { get; set; } = 0xFF000000;
            /// <summary>Outline thickness in rendered pixels (integer). Default 1.</summary>
            public int OutlineSize { get; set; } = 1;
            /// <summary>Draw a camera-facing backing grid behind the model.</summary>
            public bool DrawBackdropGrid { get; set; }
            /// <summary>Minor grid line color (packed BGRA).</summary>
            public uint BackdropGridMinorColor { get; set; } = 0xFF2A2F35;
            /// <summary>Major grid line color (packed BGRA).</summary>
            public uint BackdropGridMajorColor { get; set; } = 0xFF39424B;
            /// <summary>Draw a brighter line every N grid lines. Set 0 to disable.</summary>
            public int BackdropGridMajorEvery { get; set; } = 4;
            /// <summary>Minimum grid extent past the model bounds in voxel units.</summary>
            public float BackdropGridMarginVoxels { get; set; } = 5f;
            /// <summary>Top face lighting multiplier. Default 1.0.</summary>
            public float LightTop { get; set; } = 1.00f;
            /// <summary>Side face (left/right) lighting multiplier. Default 0.86.</summary>
            public float LightSide { get; set; } = 0.86f;
            /// <summary>Front/back/bottom face lighting multiplier. Default 0.76.</summary>
            public float LightFront { get; set; } = 0.76f;
        }

        // ════════════════════════════════════════════════════════════════════
        // FACE DEFINITIONS (matching reference SkiaVoxelRenderer)
        // ════════════════════════════════════════════════════════════════════

        private readonly struct FaceDef
        {
            public readonly Vector3 V0, V1, V2, V3;
            public readonly Vector3 Normal;
            public readonly Face Face;
            public readonly int Nx, Ny, Nz;

            public FaceDef(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
                Vector3 normal, Face face, int nx, int ny, int nz)
            {
                V0 = v0; V1 = v1; V2 = v2; V3 = v3;
                Normal = normal; Face = face;
                Nx = nx; Ny = ny; Nz = nz;
            }
        }

        private struct FaceInstance
        {
            public float Depth;
            public uint Color; // packed BGRA
            public float P0x, P0y, P0z;
            public float P1x, P1y, P1z;
            public float P2x, P2y, P2z;
            public float P3x, P3y, P3z;
        }

        private static readonly FaceDef[] Faces =
        {
            new(new(0,0,1), new(1,0,1), new(1,1,1), new(0,1,1), new( 0, 0, 1), Face.Back,    0,  0,  1),
            new(new(1,0,0), new(0,0,0), new(0,1,0), new(1,1,0), new( 0, 0,-1), Face.Front,   0,  0, -1),
            new(new(0,0,0), new(0,0,1), new(0,1,1), new(0,1,0), new(-1, 0, 0), Face.Left,   -1,  0,  0),
            new(new(1,0,1), new(1,0,0), new(1,1,0), new(1,1,1), new( 1, 0, 0), Face.Right,   1,  0,  0),
            new(new(0,1,1), new(1,1,1), new(1,1,0), new(0,1,0), new( 0, 1, 0), Face.Top,     0,  1,  0),
            new(new(0,0,0), new(1,0,0), new(1,0,1), new(0,0,1), new( 0,-1, 0), Face.Bottom,  0, -1,  0),
        };

        // ════════════════════════════════════════════════════════════════════
        // RENDER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Renders a voxel volume to a BGRA pixel buffer using painter's algorithm.
        /// </summary>
        /// <param name="volume">The voxel volume to render.</param>
        /// <param name="camera">The orbit camera (provides pose, basis, frustum).</param>
        /// <param name="width">Output image width in pixels.</param>
        /// <param name="height">Output image height in pixels.</param>
        /// <param name="buffer">Pre-allocated BGRA buffer (width × height × 4).</param>
        /// <param name="clearColor">Background color (BGRA packed).</param>
        /// <param name="options">Render options (lighting, outline, culling).</param>
        public static void Render(
            VoxelVolume volume,
            OrbitCamera camera,
            int width, int height,
            byte[] buffer,
            uint clearColor = 0xFF1E1E1E,
            RenderOptions? options = null)
        {
            options ??= new RenderOptions();

            // Clear buffer
            FillBackground(buffer, width * height, clearColor);

            if (volume == null) return;

            var depthBuffer = new float[width * height];
            Array.Fill(depthBuffer, float.PositiveInfinity);

            var pose = camera.GetCameraPose();
            var basis = camera.GetCameraBasis(pose);
            var fr = camera.GetFrustum();
            float viewportW = MathF.Max(1f, camera.ViewportWidth);
            float viewportH = MathF.Max(1f, camera.ViewportHeight);
            float frWidth = MathF.Max(1e-6f, fr.Width);
            float frHeight = MathF.Max(1e-6f, fr.Height);
            float frCenterX = (fr.Left + fr.Right) * 0.5f;
            float frCenterY = (fr.Top + fr.Bottom) * 0.5f;

            int size = volume.Size;
            float half = size * 0.5f;
            var facesToDraw = new List<FaceInstance>(Math.Max(256, size * size * 4));

            if (options.DrawBackdropGrid)
            {
                DrawBackdropGrid(
                    buffer, width, height,
                    size,
                    pose, basis,
                    frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH,
                    options);
            }

            // Collect visible faces
            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (!volume.IsOccupied(x, y, z)) continue;

                        var basePos = new Vector3(x - half, y - half, z - half);

                        for (int fi = 0; fi < Faces.Length; fi++)
                        {
                            var face = Faces[fi];

                            // Skip interior faces
                            int nx = x + face.Nx;
                            int ny = y + face.Ny;
                            int nz = z + face.Nz;
                            if ((uint)nx < (uint)size && (uint)ny < (uint)size && (uint)nz < (uint)size &&
                                volume.IsOccupied(nx, ny, nz))
                            {
                                continue;
                            }

                            // World-space vertices and center
                            var p0w = basePos + face.V0;
                            var p1w = basePos + face.V1;
                            var p2w = basePos + face.V2;
                            var p3w = basePos + face.V3;
                            var center = (p0w + p1w + p2w + p3w) * 0.25f;

                            // Backface cull
                            if (options.BackfaceCull)
                            {
                                var toCamera = pose.Position - center;
                                if (Vector3.Dot(face.Normal, toCamera) < -options.BackfaceCullEpsilon) continue;
                            }

                            // Project vertices via camera frustum
                            var q0 = ProjectOrtho(p0w, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                            var q1 = ProjectOrtho(p1w, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                            var q2 = ProjectOrtho(p2w, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                            var q3 = ProjectOrtho(p3w, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);

                            if (q0.Z <= 0f && q1.Z <= 0f && q2.Z <= 0f && q3.Z <= 0f)
                                continue;

                            // Depth for sorting
                            float depth = Vector3.Dot(center - pose.Position, basis.Forward);

                            // Face color with lighting
                            var c = volume.GetFaceColor(x, y, z, face.Face);
                            uint color = ApplyLighting(c, face.Face, options);

                            facesToDraw.Add(new FaceInstance
                            {
                                Depth = depth,
                                Color = color,
                                P0x = q0.X, P0y = q0.Y, P0z = q0.Z,
                                P1x = q1.X, P1y = q1.Y, P1z = q1.Z,
                                P2x = q2.X, P2y = q2.Y, P2z = q2.Z,
                                P3x = q3.X, P3y = q3.Y, P3z = q3.Z,
                            });
                        }
                    }
                }
            }

            // Sorting still helps reduce overdraw, but correctness comes from z-buffering.
            facesToDraw.Sort(static (a, b) => b.Depth.CompareTo(a.Depth));

            // Rasterize faces
            for (int i = 0; i < facesToDraw.Count; i++)
            {
                var f = facesToDraw[i];

                // Fill the quad as two triangles
                RasterizeTriangleFillDepth(buffer, depthBuffer, width, height,
                    f.P0x, f.P0y, f.P0z, f.P1x, f.P1y, f.P1z, f.P2x, f.P2y, f.P2z, f.Color);
                RasterizeTriangleFillDepth(buffer, depthBuffer, width, height,
                    f.P0x, f.P0y, f.P0z, f.P2x, f.P2y, f.P2z, f.P3x, f.P3y, f.P3z, f.Color);
            }

            if (options.DrawOutline)
            {
                ApplySilhouetteOutline(buffer, depthBuffer, width, height, options.OutlineColor, options.OutlineSize);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PROJECTION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Projects a world point to screen space using the camera frustum directly
        /// (matching the reference SkiaVoxelRenderer projection).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ProjectOrtho(
            Vector3 worldPoint, Vector3 cameraPos,
            OrbitCamera.CameraBasis basis,
            float frCenterX, float frCenterY,
            float frWidth, float frHeight,
            float viewportW, float viewportH)
        {
            var rel = worldPoint - cameraPos;
            float cx = Vector3.Dot(rel, basis.Right);
            float cy = Vector3.Dot(rel, basis.Up);
            float cz = Vector3.Dot(rel, basis.Forward);

            float xNdc = (2f * cx - 2f * frCenterX) / frWidth;
            float yNdc = (2f * cy - 2f * frCenterY) / frHeight;

            float sx = (xNdc * 0.5f + 0.5f) * viewportW;
            float sy = (1f - (yNdc * 0.5f + 0.5f)) * viewportH;
            return new Vector3(sx, sy, cz);
        }

        // ════════════════════════════════════════════════════════════════════
        // FACE LIGHTING
        // ════════════════════════════════════════════════════════════════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ApplyLighting(Rgba32 c, Face face, RenderOptions opts)
        {
            float mul = face switch
            {
                Face.Top => opts.LightTop,
                Face.Left or Face.Right => opts.LightSide,
                _ => opts.LightFront,
            };

            byte r = ClampByte(c.R * mul);
            byte g = ClampByte(c.G * mul);
            byte b = ClampByte(c.B * mul);

            return (uint)(b | (g << 8) | (r << 16) | (c.A << 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ClampByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 255f) return 255;
            return (byte)MathF.Round(v);
        }

        // ════════════════════════════════════════════════════════════════════
        // TRIANGLE RASTERIZATION (z-buffered)
        // ════════════════════════════════════════════════════════════════════

        private static void RasterizeTriangleFillDepth(
            byte[] buffer, float[] depthBuffer, int width, int height,
            float ax, float ay, float az,
            float bx, float by, float bz,
            float cx, float cy, float cz,
            uint bgra)
        {
            // Signed area (CCW winding = positive)
            float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (MathF.Abs(area) < 1e-6f) return; // degenerate

            // Voxel face quads are backface-culled in 3D already. In screen space the
            // Y-flip can invert winding, so normalize to a positive area here.
            if (area < 0f)
            {
                (bx, cx) = (cx, bx);
                (by, cy) = (cy, by);
                (bz, cz) = (cz, bz);
                area = -area;
            }

            float invArea = 1f / area;

            int minX = Math.Max(0, (int)MathF.Floor(Min3(ax, bx, cx)));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(Max3(ax, bx, cx)));
            int minY = Math.Max(0, (int)MathF.Floor(Min3(ay, by, cy)));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(Max3(ay, by, cy)));

            byte pB = (byte)(bgra & 0xFF);
            byte pG = (byte)((bgra >> 8) & 0xFF);
            byte pR = (byte)((bgra >> 16) & 0xFF);
            byte pA = (byte)((bgra >> 24) & 0xFF);

            for (int py = minY; py <= maxY; py++)
            {
                float pcy = py + 0.5f;
                for (int px = minX; px <= maxX; px++)
                {
                    float pcx = px + 0.5f;

                    float w0 = ((bx - pcx) * (cy - pcy) - (by - pcy) * (cx - pcx));
                    float w1 = ((cx - pcx) * (ay - pcy) - (cy - pcy) * (ax - pcx));

                    if (w0 < 0 || w1 < 0) continue;
                    float w2 = area - w0 - w1;
                    if (w2 < 0) continue;

                    float depth = (w0 * az + w1 * bz + w2 * cz) * invArea;
                    int idx = py * width + px;
                    if (depth <= 0f || depth >= depthBuffer[idx]) continue;

                    depthBuffer[idx] = depth;

                    int bi = idx * 4;
                    buffer[bi] = pB;
                    buffer[bi + 1] = pG;
                    buffer[bi + 2] = pR;
                    buffer[bi + 3] = pA;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SILHOUETTE OUTLINE (postprocess, exterior-only)
        // ════════════════════════════════════════════════════════════════════

        private static void ApplySilhouetteOutline(
            byte[] buffer, float[] depthBuffer,
            int width, int height,
            uint outlineColor, int outlineSize)
        {
            if (outlineSize <= 0) return;

            int pixelCount = width * height;
            var objectMask = new bool[pixelCount];
            bool anyObject = false;

            for (int i = 0; i < pixelCount; i++)
            {
                bool occupied = !float.IsPositiveInfinity(depthBuffer[i]);
                objectMask[i] = occupied;
                anyObject |= occupied;
            }

            if (!anyObject) return;

            var exteriorMask = new bool[pixelCount];
            var floodQueue = new int[pixelCount];
            int floodHead = 0, floodTail = 0;

            void TryEnqueueExterior(int x, int y)
            {
                if ((uint)x >= (uint)width || (uint)y >= (uint)height) return;
                int idx = y * width + x;
                if (objectMask[idx] || exteriorMask[idx]) return;
                exteriorMask[idx] = true;
                floodQueue[floodTail++] = idx;
            }

            for (int x = 0; x < width; x++)
            {
                TryEnqueueExterior(x, 0);
                TryEnqueueExterior(x, height - 1);
            }

            for (int y = 1; y < height - 1; y++)
            {
                TryEnqueueExterior(0, y);
                TryEnqueueExterior(width - 1, y);
            }

            while (floodHead < floodTail)
            {
                int idx = floodQueue[floodHead++];
                int x = idx % width;
                int y = idx / width;

                TryEnqueueExterior(x - 1, y);
                TryEnqueueExterior(x + 1, y);
                TryEnqueueExterior(x, y - 1);
                TryEnqueueExterior(x, y + 1);
            }

            int radius = Math.Max(1, outlineSize);
            var distance = new int[pixelCount]; // 0 = unvisited, 1..radius = outline ring
            var outlineQueue = new int[pixelCount];
            int outlineHead = 0, outlineTail = 0;

            // Seed outline with exterior pixels immediately adjacent (8-neighbor)
            // to any object pixel. This gives a square/voxel-like outline footprint.
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    if (!exteriorMask[idx]) continue;

                    bool nearObject = false;
                    int minY = Math.Max(0, y - 1);
                    int maxY = Math.Min(height - 1, y + 1);
                    int minX = Math.Max(0, x - 1);
                    int maxX = Math.Min(width - 1, x + 1);

                    for (int ny = minY; ny <= maxY && !nearObject; ny++)
                    {
                        int nRow = ny * width;
                        for (int nx = minX; nx <= maxX; nx++)
                        {
                            if (objectMask[nRow + nx])
                            {
                                nearObject = true;
                                break;
                            }
                        }
                    }

                    if (!nearObject) continue;
                    distance[idx] = 1;
                    outlineQueue[outlineTail++] = idx;
                }
            }

            // Multi-source BFS in 8-neighbor exterior space. With unit edge cost,
            // this computes a Chebyshev-distance outline (square dilation) exactly.
            while (outlineHead < outlineTail)
            {
                int idx = outlineQueue[outlineHead++];
                int d = distance[idx];
                if (d >= radius) continue;

                int x = idx % width;
                int y = idx / width;
                int minY = Math.Max(0, y - 1);
                int maxY = Math.Min(height - 1, y + 1);
                int minX = Math.Max(0, x - 1);
                int maxX = Math.Min(width - 1, x + 1);

                for (int ny = minY; ny <= maxY; ny++)
                {
                    int nRow = ny * width;
                    for (int nx = minX; nx <= maxX; nx++)
                    {
                        int ni = nRow + nx;
                        if (!exteriorMask[ni]) continue;
                        if (distance[ni] != 0) continue;
                        distance[ni] = d + 1;
                        outlineQueue[outlineTail++] = ni;
                    }
                }
            }

            byte oB = (byte)(outlineColor & 0xFF);
            byte oG = (byte)((outlineColor >> 8) & 0xFF);
            byte oR = (byte)((outlineColor >> 16) & 0xFF);
            byte oA = (byte)((outlineColor >> 24) & 0xFF);

            for (int i = 0; i < outlineTail; i++)
            {
                int idx = outlineQueue[i];
                int bi = idx * 4;
                buffer[bi] = oB;
                buffer[bi + 1] = oG;
                buffer[bi + 2] = oR;
                buffer[bi + 3] = oA;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // BACKDROP GRID (camera-facing, voxel-unit spacing)
        // ════════════════════════════════════════════════════════════════════

        private static void DrawBackdropGrid(
            byte[] buffer, int width, int height,
            int volumeSize,
            OrbitCamera.CameraPose pose,
            OrbitCamera.CameraBasis basis,
            float frCenterX, float frCenterY,
            float frWidth, float frHeight,
            float viewportW, float viewportH,
            RenderOptions options)
        {
            float half = MathF.Max(0.5f, volumeSize * 0.5f);
            float margin = MathF.Max(5f, options.BackdropGridMarginVoxels);

            // Project the cube half-extents onto the camera-facing plane basis so the
            // grid covers the whole model, then extend by a fixed voxel margin.
            float halfAlongForward = half * (MathF.Abs(basis.Forward.X) + MathF.Abs(basis.Forward.Y) + MathF.Abs(basis.Forward.Z));
            float halfAlongRight = half * (MathF.Abs(basis.Right.X) + MathF.Abs(basis.Right.Y) + MathF.Abs(basis.Right.Z));
            float halfAlongUp = half * (MathF.Abs(basis.Up.X) + MathF.Abs(basis.Up.Y) + MathF.Abs(basis.Up.Z));

            int extentX = Math.Max(1, (int)MathF.Ceiling(halfAlongRight + margin));
            int extentY = Math.Max(1, (int)MathF.Ceiling(halfAlongUp + margin));

            // Place the board behind the farthest model point relative to the camera.
            float boardDistanceFromOrigin = halfAlongForward + 1f;
            Vector3 center = -basis.Forward * boardDistanceFromOrigin;

            int majorEvery = Math.Max(0, options.BackdropGridMajorEvery);

            for (int gx = -extentX; gx <= extentX; gx++)
            {
                bool major = majorEvery > 0 && (Math.Abs(gx) % majorEvery) == 0;
                uint color = major ? options.BackdropGridMajorColor : options.BackdropGridMinorColor;

                Vector3 p0 = center + (basis.Right * gx) + (basis.Up * -extentY);
                Vector3 p1 = center + (basis.Right * gx) + (basis.Up * extentY);
                var q0 = ProjectOrtho(p0, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                var q1 = ProjectOrtho(p1, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                if (q0.Z <= 0f && q1.Z <= 0f) continue;

                RasterizeLine(buffer, width, height, q0.X, q0.Y, q1.X, q1.Y, color);
            }

            for (int gy = -extentY; gy <= extentY; gy++)
            {
                bool major = majorEvery > 0 && (Math.Abs(gy) % majorEvery) == 0;
                uint color = major ? options.BackdropGridMajorColor : options.BackdropGridMinorColor;

                Vector3 p0 = center + (basis.Up * gy) + (basis.Right * -extentX);
                Vector3 p1 = center + (basis.Up * gy) + (basis.Right * extentX);
                var q0 = ProjectOrtho(p0, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                var q1 = ProjectOrtho(p1, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                if (q0.Z <= 0f && q1.Z <= 0f) continue;

                RasterizeLine(buffer, width, height, q0.X, q0.Y, q1.X, q1.Y, color);
            }
        }

        private static void RasterizeLine(
            byte[] buffer, int width, int height,
            float x0, float y0, float x1, float y1,
            uint bgra)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            int steps = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(dx), MathF.Abs(dy))));

            float ix = dx / steps;
            float iy = dy / steps;
            float px = x0;
            float py = y0;

            byte b = (byte)(bgra & 0xFF);
            byte g = (byte)((bgra >> 8) & 0xFF);
            byte r = (byte)((bgra >> 16) & 0xFF);
            byte a = (byte)((bgra >> 24) & 0xFF);

            for (int i = 0; i <= steps; i++)
            {
                int sx = (int)MathF.Round(px);
                int sy = (int)MathF.Round(py);
                if ((uint)sx < (uint)width && (uint)sy < (uint)height)
                {
                    int bi = (sy * width + sx) * 4;
                    buffer[bi] = b;
                    buffer[bi + 1] = g;
                    buffer[bi + 2] = r;
                    buffer[bi + 3] = a;
                }

                px += ix;
                py += iy;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static void FillBackground(byte[] buffer, int pixelCount, uint clearColor)
        {
            byte b = (byte)(clearColor & 0xFF);
            byte g = (byte)((clearColor >> 8) & 0xFF);
            byte r = (byte)((clearColor >> 16) & 0xFF);
            byte a = (byte)((clearColor >> 24) & 0xFF);

            for (int i = 0; i < pixelCount; i++)
            {
                int bi = i * 4;
                buffer[bi] = b; buffer[bi + 1] = g;
                buffer[bi + 2] = r; buffer[bi + 3] = a;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Min3(float a, float b, float c) => MathF.Min(a, MathF.Min(b, c));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Max3(float a, float b, float c) => MathF.Max(a, MathF.Max(b, c));
    }
}
