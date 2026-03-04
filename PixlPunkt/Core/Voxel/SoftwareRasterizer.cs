using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// Software 3D rasterizer that renders <see cref="ColoredQuad"/> meshes
    /// to a BGRA pixel buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements a minimal forward-rendering pipeline:
    /// </para>
    /// <list type="number">
    /// <item>Transform vertices by a view-projection matrix.</item>
    /// <item>Perspective divide to NDC.</item>
    /// <item>Skip quads with any vertex behind the near plane.</item>
    /// <item>Viewport transform to screen coordinates.</item>
    /// <item>Split each quad into two triangles.</item>
    /// <item>Back-face cull via screen-space winding order.</item>
    /// <item>Rasterize with edge-function test and per-pixel z-buffer.</item>
    /// </list>
    /// <para>
    /// All triangles are flat-shaded (single color per face). No texture mapping,
    /// interpolation, or anti-aliasing is performed, which preserves the crisp
    /// pixel-art aesthetic.
    /// </para>
    /// <para>
    /// Output is a BGRA byte array compatible with <c>WriteableBitmap</c>,
    /// <see cref="Imaging.PixelSurface"/>, and the existing export pipeline.
    /// </para>
    /// </remarks>
    public static class SoftwareRasterizer
    {
        /// <summary>
        /// Renders a mesh of colored quads to a BGRA pixel buffer.
        /// </summary>
        /// <param name="mesh">List of <see cref="ColoredQuad"/> to render.</param>
        /// <param name="viewProj">Combined view × projection matrix.</param>
        /// <param name="width">Output image width in pixels.</param>
        /// <param name="height">Output image height in pixels.</param>
        /// <param name="clearColor">Background color (BGRA packed uint).</param>
        /// <returns>BGRA byte array of length <c>width × height × 4</c>.</returns>
        public static byte[] Render(
            IReadOnlyList<ColoredQuad> mesh,
            Matrix4x4 viewProj,
            int width, int height,
            uint clearColor = 0xFF1E1E1E)
        {
            int pixelCount = width * height;
            var buffer = new byte[pixelCount * 4];
            var zbuffer = new float[pixelCount];

            // Fill background and depth buffer
            FillBackground(buffer, zbuffer, pixelCount, clearColor);

            // Render each quad as two triangles
            foreach (var quad in mesh)
            {
                RenderQuad(buffer, zbuffer, width, height, quad, viewProj);
            }

            return buffer;
        }

        /// <summary>
        /// Renders a mesh using a pre-allocated buffer pair (avoids allocation per frame).
        /// </summary>
        /// <param name="mesh">List of <see cref="ColoredQuad"/> to render.</param>
        /// <param name="viewProj">Combined view × projection matrix.</param>
        /// <param name="width">Output image width in pixels.</param>
        /// <param name="height">Output image height in pixels.</param>
        /// <param name="buffer">Pre-allocated BGRA buffer (must be width × height × 4).</param>
        /// <param name="zbuffer">Pre-allocated depth buffer (must be width × height).</param>
        /// <param name="clearColor">Background color (BGRA packed uint).</param>
        public static void Render(
            IReadOnlyList<ColoredQuad> mesh,
            Matrix4x4 viewProj,
            int width, int height,
            byte[] buffer, float[] zbuffer,
            uint clearColor = 0xFF1E1E1E)
        {
            int pixelCount = width * height;
            FillBackground(buffer, zbuffer, pixelCount, clearColor);

            foreach (var quad in mesh)
            {
                RenderQuad(buffer, zbuffer, width, height, quad, viewProj);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // QUAD / TRIANGLE RENDERING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Transforms a quad's vertices, splits into two triangles, and rasterizes.
        /// </summary>
        private static void RenderQuad(
            byte[] buffer, float[] zbuffer,
            int width, int height,
            in ColoredQuad quad, in Matrix4x4 viewProj)
        {
            // Transform to clip space
            var c0 = TransformToClip(quad.V0, viewProj);
            var c1 = TransformToClip(quad.V1, viewProj);
            var c2 = TransformToClip(quad.V2, viewProj);
            var c3 = TransformToClip(quad.V3, viewProj);

            // Skip if any vertex is behind the near plane (W ≤ 0)
            if (c0.W <= 0 || c1.W <= 0 || c2.W <= 0 || c3.W <= 0) return;

            // Perspective divide → NDC, then to screen coordinates
            var s0 = ToScreen(c0, width, height);
            var s1 = ToScreen(c1, width, height);
            var s2 = ToScreen(c2, width, height);
            var s3 = ToScreen(c3, width, height);

            // Pack color as BGRA for the output buffer
            uint bgra = PackBgra(quad.Color);

            // Rasterize two triangles: (V0, V1, V2) and (V0, V2, V3)
            RasterizeTriangle(buffer, zbuffer, width, height, s0, s1, s2, bgra);
            RasterizeTriangle(buffer, zbuffer, width, height, s0, s2, s3, bgra);
        }

        /// <summary>
        /// Rasterizes a single triangle with flat shading and z-buffering.
        /// </summary>
        /// <remarks>
        /// Uses the edge function (cross product) approach for point-in-triangle
        /// testing. Back-face culling is performed by checking the screen-space
        /// winding order (positive signed area = CCW = front-facing).
        /// </remarks>
        private static void RasterizeTriangle(
            byte[] buffer, float[] zbuffer,
            int width, int height,
            Vector3 v0, Vector3 v1, Vector3 v2,
            uint bgra)
        {
            // Signed area × 2 (screen space)
            // Positive = CCW = front-facing; negative/zero = back-facing or degenerate
            float area = EdgeFunction(v0, v1, v2);
            if (area <= 0) return;

            float invArea = 1f / area;

            // Bounding box (clamped to viewport)
            int minX = Math.Max(0, (int)MathF.Floor(Min3(v0.X, v1.X, v2.X)));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(Max3(v0.X, v1.X, v2.X)));
            int minY = Math.Max(0, (int)MathF.Floor(Min3(v0.Y, v1.Y, v2.Y)));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(Max3(v0.Y, v1.Y, v2.Y)));

            // Pre-extract color bytes
            byte bB = (byte)(bgra & 0xFF);
            byte bG = (byte)((bgra >> 8) & 0xFF);
            byte bR = (byte)((bgra >> 16) & 0xFF);
            byte bA = (byte)((bgra >> 24) & 0xFF);

            for (int py = minY; py <= maxY; py++)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    // Sample at pixel center
                    float cx = px + 0.5f;
                    float cy = py + 0.5f;

                    // Barycentric coordinates via edge functions
                    float w0 = EdgeFunctionXY(v1, v2, cx, cy) * invArea;
                    float w1 = EdgeFunctionXY(v2, v0, cx, cy) * invArea;
                    float w2 = 1f - w0 - w1;

                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;

                    // Interpolate depth
                    float depth = w0 * v0.Z + w1 * v1.Z + w2 * v2.Z;

                    // Z-buffer test (smaller depth = closer to camera)
                    int idx = py * width + px;
                    if (depth >= zbuffer[idx]) continue;

                    zbuffer[idx] = depth;

                    int bi = idx * 4;
                    buffer[bi]     = bB;
                    buffer[bi + 1] = bG;
                    buffer[bi + 2] = bR;
                    buffer[bi + 3] = bA;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // MATH HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Transforms a 3D position by the view-projection matrix → clip space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 TransformToClip(Vector3 pos, in Matrix4x4 m)
        {
            return new Vector4(
                pos.X * m.M11 + pos.Y * m.M21 + pos.Z * m.M31 + m.M41,
                pos.X * m.M12 + pos.Y * m.M22 + pos.Z * m.M32 + m.M42,
                pos.X * m.M13 + pos.Y * m.M23 + pos.Z * m.M33 + m.M43,
                pos.X * m.M14 + pos.Y * m.M24 + pos.Z * m.M34 + m.M44);
        }

        /// <summary>
        /// Perspective divide + viewport transform.
        /// Returns (screenX, screenY, ndcDepth).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ToScreen(Vector4 clip, int width, int height)
        {
            float invW = 1f / clip.W;
            float ndcX = clip.X * invW;
            float ndcY = clip.Y * invW;
            float ndcZ = clip.Z * invW;

            // NDC [-1,1] → screen [0, width/height], Y flipped
            float sx = (ndcX + 1f) * 0.5f * width;
            float sy = (1f - ndcY) * 0.5f * height;

            return new Vector3(sx, sy, ndcZ);
        }

        /// <summary>
        /// Edge function: signed area of the parallelogram formed by (a→b) and (a→c).
        /// Positive when c is to the left of the edge a→b (CCW winding).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EdgeFunction(Vector3 a, Vector3 b, Vector3 c)
            => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        /// <summary>
        /// Edge function variant that takes raw x/y to avoid Vector3 construction in the inner loop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EdgeFunctionXY(Vector3 a, Vector3 b, float cx, float cy)
            => (b.X - a.X) * (cy - a.Y) - (b.Y - a.Y) * (cx - a.X);

        /// <summary>
        /// Packs an <see cref="Rgba32"/> color into a BGRA uint for the output buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackBgra(Rgba32 c)
            => (uint)(c.B | (c.G << 8) | (c.R << 16) | (c.A << 24));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Min3(float a, float b, float c)
            => MathF.Min(a, MathF.Min(b, c));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Max3(float a, float b, float c)
            => MathF.Max(a, MathF.Max(b, c));

        /// <summary>
        /// Renders face outlines by drawing edges of each visible quad.
        /// Call after the fill render pass so edges appear on top of filled faces.
        /// </summary>
        /// <param name="mesh">List of <see cref="ColoredQuad"/> to outline.</param>
        /// <param name="viewProj">Combined view × projection matrix.</param>
        /// <param name="width">Output image width in pixels.</param>
        /// <param name="height">Output image height in pixels.</param>
        /// <param name="buffer">Pre-allocated BGRA buffer.</param>
        /// <param name="outlineColor">Outline color (BGRA packed uint).</param>
        public static void RenderOutlines(
            IReadOnlyList<ColoredQuad> mesh,
            Matrix4x4 viewProj,
            int width, int height,
            byte[] buffer,
            uint outlineColor)
        {
            byte oB = (byte)(outlineColor & 0xFF);
            byte oG = (byte)((outlineColor >> 8) & 0xFF);
            byte oR = (byte)((outlineColor >> 16) & 0xFF);
            byte oA = (byte)((outlineColor >> 24) & 0xFF);

            foreach (var quad in mesh)
            {
                var c0 = TransformToClip(quad.V0, viewProj);
                var c1 = TransformToClip(quad.V1, viewProj);
                var c2 = TransformToClip(quad.V2, viewProj);
                var c3 = TransformToClip(quad.V3, viewProj);

                if (c0.W <= 0 || c1.W <= 0 || c2.W <= 0 || c3.W <= 0) continue;

                var s0 = ToScreen(c0, width, height);
                var s1 = ToScreen(c1, width, height);
                var s2 = ToScreen(c2, width, height);
                var s3 = ToScreen(c3, width, height);

                // Back-face cull: check if first triangle is front-facing
                float area = EdgeFunction(s0, s1, s2);
                if (area <= 0) continue;

                // Draw 4 edges of the quad
                DrawLine(buffer, width, height, s0, s1, oB, oG, oR, oA);
                DrawLine(buffer, width, height, s1, s2, oB, oG, oR, oA);
                DrawLine(buffer, width, height, s2, s3, oB, oG, oR, oA);
                DrawLine(buffer, width, height, s3, s0, oB, oG, oR, oA);
            }
        }

        /// <summary>
        /// Draws a line between two screen-space points using Bresenham's algorithm.
        /// </summary>
        private static void DrawLine(
            byte[] buffer, int width, int height,
            Vector3 a, Vector3 b,
            byte bB, byte bG, byte bR, byte bA)
        {
            int x0 = (int)MathF.Round(a.X);
            int y0 = (int)MathF.Round(a.Y);
            int x1 = (int)MathF.Round(b.X);
            int y1 = (int)MathF.Round(b.Y);

            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
                {
                    int bi = (y0 * width + x0) * 4;
                    buffer[bi]     = bB;
                    buffer[bi + 1] = bG;
                    buffer[bi + 2] = bR;
                    buffer[bi + 3] = bA;
                }

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // BUFFER INITIALIZATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fills the color buffer with the clear color and the depth buffer with max depth.
        /// </summary>
        private static void FillBackground(byte[] buffer, float[] zbuffer, int pixelCount, uint clearColor)
        {
            byte bB = (byte)(clearColor & 0xFF);
            byte bG = (byte)((clearColor >> 8) & 0xFF);
            byte bR = (byte)((clearColor >> 16) & 0xFF);
            byte bA = (byte)((clearColor >> 24) & 0xFF);

            for (int i = 0; i < pixelCount; i++)
            {
                int bi = i * 4;
                buffer[bi]     = bB;
                buffer[bi + 1] = bG;
                buffer[bi + 2] = bR;
                buffer[bi + 3] = bA;

                zbuffer[i] = float.MaxValue;
            }
        }
    }
}
