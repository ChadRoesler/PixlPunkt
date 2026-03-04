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
    /// Optional lighting uses a point-light preview model (diffuse with distance attenuation).
    /// When lighting is disabled, face colors are rendered flat/unlit.
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
            /// <summary>Backdrop cage half-extent scale relative to model half-size.</summary>
            public float BackdropCageScale { get; set; } = 1.6f;
            /// <summary>Draw source projection tiles on the corresponding cage faces.</summary>
            public bool DrawBackdropProjectionTiles { get; set; }
            /// <summary>Front (+Z) projection image for the cage.</summary>
            public ImageData? BackdropFrontProjection { get; set; }
            /// <summary>Back (-Z) projection image for the cage.</summary>
            public ImageData? BackdropBackProjection { get; set; }
            /// <summary>Left (-X) projection image for the cage.</summary>
            public ImageData? BackdropLeftProjection { get; set; }
            /// <summary>Right (+X) projection image for the cage.</summary>
            public ImageData? BackdropRightProjection { get; set; }
            /// <summary>Top (+Y) projection image for the cage.</summary>
            public ImageData? BackdropTopProjection { get; set; }
            /// <summary>Bottom (-Y) projection image for the cage.</summary>
            public ImageData? BackdropBottomProjection { get; set; }
            /// <summary>Draw a visible grid over the model's voxel surface boundaries.</summary>
            public bool DrawSurfaceVoxelGrid { get; set; }
            /// <summary>Surface voxel grid line color (packed BGRA, alpha supported).</summary>
            public uint SurfaceVoxelGridColor { get; set; } = 0xB0000000;
            /// <summary>Enable lighting preview shading. Off = flat/unlit face colors.</summary>
            public bool LightingEnabled { get; set; }
            /// <summary>World-space point light position in voxel units.</summary>
            public Vector3 LightPosition { get; set; } = new(32f, 48f, 32f);
            /// <summary>Point light color (packed BGRA).</summary>
            public uint LightColor { get; set; } = 0xFFFFFFFF;
            /// <summary>Shadow tint color (packed BGRA; alpha controls tint strength).</summary>
            public uint ShadowColor { get; set; } = 0xC0000000;
            /// <summary>Overall shadow influence [0..1].</summary>
            public float ShadowStrength { get; set; } = 1f;
            /// <summary>Diffuse intensity multiplier.</summary>
            public float LightIntensity { get; set; } = 1f;
            /// <summary>Baseline ambient contribution [0..1] applied when lighting is enabled.</summary>
            public float AmbientIntensity { get; set; } = 0.22f;
            /// <summary>Distance attenuation factor (0 = none).</summary>
            public float LightFalloff { get; set; } = 0.05f;
            /// <summary>Enable hard voxel cast shadows for the point light.</summary>
            public bool LightCastShadows { get; set; }
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
            public int SurfaceId;
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
            int[]? surfaceIdBuffer = null;
            if (options.DrawSurfaceVoxelGrid)
            {
                surfaceIdBuffer = new int[width * height];
                Array.Fill(surfaceIdBuffer, -1);
            }

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

            if (options.DrawBackdropGrid || options.DrawBackdropProjectionTiles)
            {
                DrawBackdropCage(
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
                            bool inShadow = options.LightingEnabled &&
                                            options.LightCastShadows &&
                                            IsFaceShadowed(volume, size, half, x, y, z, center, face.Normal, options.LightPosition);
                            uint color = ApplyLighting(c, face.Normal, center, options, inShadow);

                            facesToDraw.Add(new FaceInstance
                            {
                                Depth = depth,
                                Color = color,
                                SurfaceId = (volume.Index(x, y, z) << 3) | (int)face.Face,
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
                    f.P0x, f.P0y, f.P0z, f.P1x, f.P1y, f.P1z, f.P2x, f.P2y, f.P2z, f.Color,
                    surfaceIdBuffer, f.SurfaceId);
                RasterizeTriangleFillDepth(buffer, depthBuffer, width, height,
                    f.P0x, f.P0y, f.P0z, f.P2x, f.P2y, f.P2z, f.P3x, f.P3y, f.P3z, f.Color,
                    surfaceIdBuffer, f.SurfaceId);
            }

            if (options.DrawSurfaceVoxelGrid && surfaceIdBuffer != null)
            {
                ApplySurfaceVoxelGrid(buffer, depthBuffer, surfaceIdBuffer, width, height, options.SurfaceVoxelGridColor);
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
        private static uint ApplyLighting(Rgba32 c, Vector3 faceNormal, Vector3 faceCenter, RenderOptions opts, bool inShadow)
        {
            if (!opts.LightingEnabled)
            {
                return (uint)(c.B | (c.G << 8) | (c.R << 16) | (c.A << 24));
            }

            Vector3 toLight = opts.LightPosition - faceCenter;
            float distSq = MathF.Max(1e-6f, toLight.LengthSquared());
            float invDist = 1f / MathF.Sqrt(distSq);
            float dist = distSq * invDist;
            Vector3 lightDir = toLight * invDist;

            float ndotl = MathF.Max(0f, Vector3.Dot(Vector3.Normalize(faceNormal), lightDir));
            float attenuation = 1f / (1f + MathF.Max(0f, opts.LightFalloff) * dist);
            float diffuse = ndotl * MathF.Max(0f, opts.LightIntensity) * attenuation;
            float ambient = Math.Clamp(opts.AmbientIntensity, 0f, 1f);
            if (inShadow)
            {
                // Hard cast shadow for direct term; shadow tint still applies below.
                diffuse *= 0.05f;
            }

            float lr = ((opts.LightColor >> 16) & 0xFF) / 255f;
            float lg = ((opts.LightColor >> 8) & 0xFF) / 255f;
            float lb = (opts.LightColor & 0xFF) / 255f;
            float shadowR = ((opts.ShadowColor >> 16) & 0xFF) / 255f;
            float shadowG = ((opts.ShadowColor >> 8) & 0xFF) / 255f;
            float shadowB = (opts.ShadowColor & 0xFF) / 255f;
            float shadowA = ((opts.ShadowColor >> 24) & 0xFF) / 255f;
            float litScalar = Math.Clamp(ambient + diffuse, 0f, 1f);
            float shadowStrength = Math.Clamp(opts.ShadowStrength, 0f, 1f);
            float shadowMix = (1f - litScalar) * shadowA * shadowStrength;
            if (inShadow)
            {
                float castShadowMix = shadowA * shadowStrength;
                shadowMix = MathF.Max(shadowMix, castShadowMix);
            }
            shadowMix = Math.Clamp(shadowMix, 0f, 1f);

            float litR = c.R * (ambient + (diffuse * lr));
            float litG = c.G * (ambient + (diffuse * lg));
            float litB = c.B * (ambient + (diffuse * lb));
            float outR = litR * (1f - shadowMix) + (shadowR * 255f * shadowMix);
            float outG = litG * (1f - shadowMix) + (shadowG * 255f * shadowMix);
            float outB = litB * (1f - shadowMix) + (shadowB * 255f * shadowMix);

            byte r = ClampByte(outR);
            byte g = ClampByte(outG);
            byte b = ClampByte(outB);

            return (uint)(b | (g << 8) | (r << 16) | (c.A << 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFaceShadowed(
            VoxelVolume volume,
            int size,
            float half,
            int sourceX,
            int sourceY,
            int sourceZ,
            Vector3 faceCenter,
            Vector3 faceNormal,
            Vector3 lightPosition)
        {
            Vector3 toLight = lightPosition - faceCenter;
            float distSq = toLight.LengthSquared();
            if (distSq <= 1e-6f)
                return false;

            float dist = MathF.Sqrt(distSq);
            Vector3 dir = toLight / dist;
            if (Vector3.Dot(faceNormal, dir) <= 0f)
                return false;

            // Step just off the face surface to avoid self-intersection.
            Vector3 rayStartWorld = faceCenter + faceNormal * 0.02f + dir * 0.02f;
            Vector3 rayEndWorld = lightPosition - dir * 0.02f;

            Vector3 rayStartGrid = rayStartWorld + new Vector3(half, half, half);
            Vector3 rayEndGrid = rayEndWorld + new Vector3(half, half, half);

            return RayIntersectsOccupiedVoxel(volume, size, sourceX, sourceY, sourceZ, rayStartGrid, rayEndGrid);
        }

        private static bool RayIntersectsOccupiedVoxel(
            VoxelVolume volume,
            int size,
            int sourceX,
            int sourceY,
            int sourceZ,
            Vector3 startGrid,
            Vector3 endGrid)
        {
            Vector3 delta = endGrid - startGrid;
            float maxT = delta.Length();
            if (maxT <= 1e-6f)
                return false;

            Vector3 dir = delta / maxT;

            int x = (int)MathF.Floor(startGrid.X);
            int y = (int)MathF.Floor(startGrid.Y);
            int z = (int)MathF.Floor(startGrid.Z);

            int endX = (int)MathF.Floor(endGrid.X);
            int endY = (int)MathF.Floor(endGrid.Y);
            int endZ = (int)MathF.Floor(endGrid.Z);

            int stepX = dir.X > 1e-6f ? 1 : dir.X < -1e-6f ? -1 : 0;
            int stepY = dir.Y > 1e-6f ? 1 : dir.Y < -1e-6f ? -1 : 0;
            int stepZ = dir.Z > 1e-6f ? 1 : dir.Z < -1e-6f ? -1 : 0;

            float tMaxX = stepX == 0 ? float.PositiveInfinity : DistanceToNextGridBoundary(startGrid.X, dir.X, stepX);
            float tMaxY = stepY == 0 ? float.PositiveInfinity : DistanceToNextGridBoundary(startGrid.Y, dir.Y, stepY);
            float tMaxZ = stepZ == 0 ? float.PositiveInfinity : DistanceToNextGridBoundary(startGrid.Z, dir.Z, stepZ);

            float tDeltaX = stepX == 0 ? float.PositiveInfinity : 1f / MathF.Abs(dir.X);
            float tDeltaY = stepY == 0 ? float.PositiveInfinity : 1f / MathF.Abs(dir.Y);
            float tDeltaZ = stepZ == 0 ? float.PositiveInfinity : 1f / MathF.Abs(dir.Z);

            float t = 0f;
            while (t <= maxT)
            {
                if ((uint)x < (uint)size &&
                    (uint)y < (uint)size &&
                    (uint)z < (uint)size &&
                    !(x == sourceX && y == sourceY && z == sourceZ) &&
                    volume.IsOccupied(x, y, z))
                {
                    return true;
                }

                if (x == endX && y == endY && z == endZ)
                    break;

                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    x += stepX;
                    t = tMaxX;
                    tMaxX += tDeltaX;
                }
                else if (tMaxY <= tMaxX && tMaxY <= tMaxZ)
                {
                    y += stepY;
                    t = tMaxY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    z += stepZ;
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DistanceToNextGridBoundary(float position, float direction, int step)
        {
            if (step > 0)
            {
                float nextBoundary = MathF.Floor(position) + 1f;
                return (nextBoundary - position) / direction;
            }

            if (step < 0)
            {
                float prevBoundary = MathF.Floor(position);
                return (position - prevBoundary) / -direction;
            }

            return float.PositiveInfinity;
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
            uint bgra,
            int[]? surfaceIdBuffer,
            int surfaceId)
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
                    if (surfaceIdBuffer != null)
                    {
                        surfaceIdBuffer[idx] = surfaceId;
                    }

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

        private static void ApplySurfaceVoxelGrid(
            byte[] buffer,
            float[] depthBuffer,
            int[] surfaceIdBuffer,
            int width,
            int height,
            uint gridColor)
        {
            int pixelCount = width * height;
            if (surfaceIdBuffer.Length != pixelCount || depthBuffer.Length != pixelCount)
                return;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    if (float.IsPositiveInfinity(depthBuffer[idx]))
                        continue;

                    int sid = surfaceIdBuffer[idx];
                    if (sid < 0)
                        continue;

                    bool draw = false;

                    if (x + 1 < width)
                    {
                        int r = idx + 1;
                        if (!float.IsPositiveInfinity(depthBuffer[r]) &&
                            surfaceIdBuffer[r] >= 0 &&
                            surfaceIdBuffer[r] != sid)
                        {
                            draw = true;
                        }
                    }

                    if (!draw && y + 1 < height)
                    {
                        int d = idx + width;
                        if (!float.IsPositiveInfinity(depthBuffer[d]) &&
                            surfaceIdBuffer[d] >= 0 &&
                            surfaceIdBuffer[d] != sid)
                        {
                            draw = true;
                        }
                    }

                    if (draw)
                    {
                        AlphaBlendPixel(buffer, idx * 4, gridColor);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AlphaBlendPixel(byte[] buffer, int bi, uint bgra)
        {
            byte srcB = (byte)(bgra & 0xFF);
            byte srcG = (byte)((bgra >> 8) & 0xFF);
            byte srcR = (byte)((bgra >> 16) & 0xFF);
            byte srcA = (byte)((bgra >> 24) & 0xFF);

            if (srcA == 0)
                return;

            if (srcA == 255)
            {
                buffer[bi] = srcB;
                buffer[bi + 1] = srcG;
                buffer[bi + 2] = srcR;
                buffer[bi + 3] = 255;
                return;
            }

            int invA = 255 - srcA;
            buffer[bi] = (byte)((srcB * srcA + buffer[bi] * invA) / 255);
            buffer[bi + 1] = (byte)((srcG * srcA + buffer[bi + 1] * invA) / 255);
            buffer[bi + 2] = (byte)((srcR * srcA + buffer[bi + 2] * invA) / 255);
            buffer[bi + 3] = 255;
        }

        // ════════════════════════════════════════════════════════════════════
        // BACKDROP CAGE (voxel-unit 3D grid + optional projection tiles)
        // ════════════════════════════════════════════════════════════════════

        private static void DrawBackdropCage(
            byte[] buffer, int width, int height,
            int volumeSize,
            OrbitCamera.CameraPose pose,
            OrbitCamera.CameraBasis basis,
            float frCenterX, float frCenterY,
            float frWidth, float frHeight,
            float viewportW, float viewportH,
            RenderOptions options)
        {
            float modelHalf = MathF.Max(0.5f, volumeSize * 0.5f);
            float margin = MathF.Max(1f, options.BackdropGridMarginVoxels);
            float cageHalf = MathF.Max(modelHalf + margin, modelHalf * MathF.Max(1.05f, options.BackdropCageScale));
            int extent = Math.Max(1, (int)MathF.Round(cageHalf));
            int majorEvery = Math.Max(0, options.BackdropGridMajorEvery);
            float gridPhase = (volumeSize & 1) == 0 ? 0f : 0.5f;
            bool farXPositive = basis.Forward.X >= 0f;
            bool farYPositive = basis.Forward.Y >= 0f;
            bool farZPositive = basis.Forward.Z >= 0f;

            if (options.DrawBackdropProjectionTiles)
            {
                float panelHalf = modelHalf;
                float panelInset = 0.02f;

                DrawProjectionPanel(
                    buffer, width, height,
                    farZPositive ? options.BackdropFrontProjection : options.BackdropBackProjection,
                    center: new Vector3(0f, 0f, farZPositive ? (cageHalf - panelInset) : (-cageHalf + panelInset)),
                    axisU: farZPositive ? Vector3.UnitX : -Vector3.UnitX,
                    axisV: Vector3.UnitY,
                    halfExtent: panelHalf,
                    flipU: !farZPositive,
                    flipV: false,
                    pose, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);

                DrawProjectionPanel(
                    buffer, width, height,
                    farXPositive ? options.BackdropLeftProjection : options.BackdropRightProjection,
                    center: new Vector3(farXPositive ? (cageHalf - panelInset) : (-cageHalf + panelInset), 0f, 0f),
                    axisU: farXPositive ? -Vector3.UnitZ : Vector3.UnitZ,
                    axisV: Vector3.UnitY,
                    halfExtent: panelHalf,
                    flipU: true,
                    flipV: false,
                    pose, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);

                DrawProjectionPanel(
                    buffer, width, height,
                    farYPositive ? options.BackdropBottomProjection : options.BackdropTopProjection,
                    center: new Vector3(0f, farYPositive ? (cageHalf - panelInset) : (-cageHalf + panelInset), 0f),
                    axisU: Vector3.UnitX,
                    axisV: farYPositive ? Vector3.UnitZ : -Vector3.UnitZ,
                    halfExtent: panelHalf,
                    // Top/bottom panels should mirror consistently around world-Z expectations;
                    // invert U from the previous mapping to avoid the observed Z-axis flip.
                    flipU: farYPositive,
                    flipV: farYPositive,
                    pose, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
            }

            if (!options.DrawBackdropGrid)
                return;

            DrawGridPlane(
                buffer, width, height,
                center: new Vector3(0f, 0f, farZPositive ? cageHalf : -cageHalf),
                axisA: Vector3.UnitX, axisB: Vector3.UnitY,
                extentA: extent, extentB: extent,
                offsetA: gridPhase, offsetB: gridPhase,
                majorEvery, options.BackdropGridMinorColor, options.BackdropGridMajorColor,
                pose, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);

            DrawGridPlane(
                buffer, width, height,
                center: new Vector3(farXPositive ? cageHalf : -cageHalf, 0f, 0f),
                axisA: Vector3.UnitZ, axisB: Vector3.UnitY,
                extentA: extent, extentB: extent,
                offsetA: gridPhase, offsetB: gridPhase,
                majorEvery, options.BackdropGridMinorColor, options.BackdropGridMajorColor,
                pose, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);

            DrawGridPlane(
                buffer, width, height,
                center: new Vector3(0f, farYPositive ? cageHalf : -cageHalf, 0f),
                axisA: Vector3.UnitX, axisB: Vector3.UnitZ,
                extentA: extent, extentB: extent,
                offsetA: gridPhase, offsetB: gridPhase,
                majorEvery, options.BackdropGridMinorColor, options.BackdropGridMajorColor,
                pose, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
        }

        private static void DrawGridPlane(
            byte[] buffer, int width, int height,
            Vector3 center,
            Vector3 axisA, Vector3 axisB,
            int extentA, int extentB,
            float offsetA,
            float offsetB,
            int majorEvery,
            uint minorColor, uint majorColor,
            OrbitCamera.CameraPose pose,
            OrbitCamera.CameraBasis basis,
            float frCenterX, float frCenterY,
            float frWidth, float frHeight,
            float viewportW, float viewportH)
        {
            for (int a = -extentA; a <= extentA; a++)
            {
                bool major = majorEvery > 0 && (Math.Abs(a) % majorEvery) == 0;
                uint color = major ? majorColor : minorColor;
                float aPos = a + offsetA;

                Vector3 p0 = center + (axisA * aPos) + (axisB * (-extentB + offsetB));
                Vector3 p1 = center + (axisA * aPos) + (axisB * (extentB + offsetB));
                var q0 = ProjectOrtho(p0, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                var q1 = ProjectOrtho(p1, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                if (q0.Z <= 0f && q1.Z <= 0f) continue;

                RasterizeLine(buffer, width, height, q0.X, q0.Y, q1.X, q1.Y, color);
            }

            for (int b = -extentB; b <= extentB; b++)
            {
                bool major = majorEvery > 0 && (Math.Abs(b) % majorEvery) == 0;
                uint color = major ? majorColor : minorColor;
                float bPos = b + offsetB;

                Vector3 p0 = center + (axisB * bPos) + (axisA * (-extentA + offsetA));
                Vector3 p1 = center + (axisB * bPos) + (axisA * (extentA + offsetA));
                var q0 = ProjectOrtho(p0, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                var q1 = ProjectOrtho(p1, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
                if (q0.Z <= 0f && q1.Z <= 0f) continue;

                RasterizeLine(buffer, width, height, q0.X, q0.Y, q1.X, q1.Y, color);
            }
        }

        private static void DrawProjectionPanel(
            byte[] buffer, int width, int height,
            ImageData? image,
            Vector3 center,
            Vector3 axisU,
            Vector3 axisV,
            float halfExtent,
            bool flipU,
            bool flipV,
            OrbitCamera.CameraPose pose,
            OrbitCamera.CameraBasis basis,
            float frCenterX, float frCenterY,
            float frWidth, float frHeight,
            float viewportW, float viewportH)
        {
            if (image == null || image.Width <= 0 || image.Height <= 0)
                return;

            Vector3 p0w = center - (axisU * halfExtent) + (axisV * halfExtent); // top-left
            Vector3 p1w = center + (axisU * halfExtent) + (axisV * halfExtent); // top-right
            Vector3 p2w = center + (axisU * halfExtent) - (axisV * halfExtent); // bottom-right
            Vector3 p3w = center - (axisU * halfExtent) - (axisV * halfExtent); // bottom-left

            var p0 = ProjectOrtho(p0w, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
            var p1 = ProjectOrtho(p1w, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
            var p2 = ProjectOrtho(p2w, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);
            var p3 = ProjectOrtho(p3w, pose.Position, basis, frCenterX, frCenterY, frWidth, frHeight, viewportW, viewportH);

            if (p0.Z <= 0f && p1.Z <= 0f && p2.Z <= 0f && p3.Z <= 0f)
                return;

            float u0 = flipU ? 1f : 0f;
            float u1 = flipU ? 0f : 1f;
            float v0 = flipV ? 1f : 0f;
            float v1 = flipV ? 0f : 1f;

            RasterizeTexturedTriangle(
                buffer, width, height, image,
                p0.X, p0.Y, p0.Z, u0, v0,
                p1.X, p1.Y, p1.Z, u1, v0,
                p2.X, p2.Y, p2.Z, u1, v1);

            RasterizeTexturedTriangle(
                buffer, width, height, image,
                p0.X, p0.Y, p0.Z, u0, v0,
                p2.X, p2.Y, p2.Z, u1, v1,
                p3.X, p3.Y, p3.Z, u0, v1);
        }

        private static void RasterizeTexturedTriangle(
            byte[] buffer, int width, int height,
            ImageData image,
            float ax, float ay, float az, float au, float av,
            float bx, float by, float bz, float bu, float bv,
            float cx, float cy, float cz, float cu, float cv)
        {
            float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (MathF.Abs(area) < 1e-6f)
                return;

            if (area < 0f)
            {
                (bx, cx) = (cx, bx);
                (by, cy) = (cy, by);
                (bz, cz) = (cz, bz);
                (bu, cu) = (cu, bu);
                (bv, cv) = (cv, bv);
                area = -area;
            }

            float invArea = 1f / area;
            int minX = Math.Max(0, (int)MathF.Floor(Min3(ax, bx, cx)));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(Max3(ax, bx, cx)));
            int minY = Math.Max(0, (int)MathF.Floor(Min3(ay, by, cy)));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(Max3(ay, by, cy)));

            int srcW = image.Width;
            int srcH = image.Height;
            var src = image.Rgba;

            for (int py = minY; py <= maxY; py++)
            {
                float pcy = py + 0.5f;
                for (int px = minX; px <= maxX; px++)
                {
                    float pcx = px + 0.5f;

                    float w0 = ((bx - pcx) * (cy - pcy) - (by - pcy) * (cx - pcx));
                    float w1 = ((cx - pcx) * (ay - pcy) - (cy - pcy) * (ax - pcx));
                    if (w0 < 0f || w1 < 0f) continue;
                    float w2 = area - w0 - w1;
                    if (w2 < 0f) continue;

                    float depth = (w0 * az + w1 * bz + w2 * cz) * invArea;
                    if (depth <= 0f) continue;

                    float u = (w0 * au + w1 * bu + w2 * cu) * invArea;
                    float v = (w0 * av + w1 * bv + w2 * cv) * invArea;
                    u = Math.Clamp(u, 0f, 1f);
                    v = Math.Clamp(v, 0f, 1f);

                    int sx = Math.Clamp((int)MathF.Floor(u * srcW), 0, srcW - 1);
                    int sy = Math.Clamp((int)MathF.Floor(v * srcH), 0, srcH - 1);
                    int si = (sy * srcW + sx) * 4;

                    byte srcR = src[si];
                    byte srcG = src[si + 1];
                    byte srcB = src[si + 2];
                    byte srcA = src[si + 3];
                    if (srcA == 0)
                        continue;

                    int di = (py * width + px) * 4;
                    if (srcA == 255)
                    {
                        buffer[di] = srcB;
                        buffer[di + 1] = srcG;
                        buffer[di + 2] = srcR;
                        buffer[di + 3] = 255;
                        continue;
                    }

                    int invA = 255 - srcA;
                    buffer[di] = (byte)((srcB * srcA + buffer[di] * invA) / 255);
                    buffer[di + 1] = (byte)((srcG * srcA + buffer[di + 1] * invA) / 255);
                    buffer[di + 2] = (byte)((srcR * srcA + buffer[di + 2] * invA) / 255);
                    buffer[di + 3] = 255;
                }
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
