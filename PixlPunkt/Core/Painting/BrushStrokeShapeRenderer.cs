using System;
using System.Collections.Generic;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Painting.Helpers;
using PixlPunkt.Core.Structs;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Default shape renderer that applies brush-stroked rendering to shape points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This renderer stamps the current brush shape at each point in the shape geometry,
    /// with proper alpha blending and density-based falloff. It supports:
    /// </para>
    /// <list type="bullet">
    /// <item>Circular and square brush shapes</item>
    /// <item>Variable stroke width</item>
    /// <item>Opacity and density-based soft edges</item>
    /// <item>Maximum alpha accumulation (prevents over-blending)</item>
    /// <item>Optimized fast-path for filled shapes</item>
    /// </list>
    /// <para>
    /// The renderer is stateless and can be shared across multiple render calls.
    /// </para>
    /// </remarks>
    public sealed class BrushStrokeShapeRenderer : IShapeRenderer
    {
        /// <summary>
        /// Shared singleton instance for common usage.
        /// </summary>
        public static BrushStrokeShapeRenderer Shared { get; } = new();

        /// <inheritdoc/>
        public IRenderResult? Render(
            RasterLayer layer,
            HashSet<(int x, int y)> points,
            ShapeRenderContext context)
        {
            if (layer == null || points == null || points.Count == 0)
                return null;

            // Use optimized path for filled shapes
            if (context.IsFilled)
            {
                return RenderFilled(layer, points, context);
            }

            // Standard brush-stroke rendering for outlines
            return RenderOutline(layer, points, context);
        }

        /// <summary>
        /// Optimized rendering for filled shapes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For filled shapes:
        /// - ALL pixels inside the shape get solid color with opacity (no density falloff inside)
        /// - Density-based soft falloff only extends OUTWARD from the shape boundary
        /// </para>
        /// </remarks>
        private PixelChangeItem? RenderFilled(
            RasterLayer layer,
            HashSet<(int x, int y)> points,
            ShapeRenderContext context)
        {
            var surface = context.Surface;
            int w = surface.Width, h = surface.Height;
            byte opacity = context.Opacity;
            byte density = context.Density;
            int strokeWidth = Math.Max(1, context.StrokeWidth);
            uint color = context.Color;
            var brushShape = context.BrushShape;

            var item = new PixelChangeItem(layer, context.Description);
            uint solidColor = (color & 0x00FFFFFFu) | ((uint)opacity << 24);

            // Step 1: Render ALL interior pixels with solid opacity
            foreach (var (px, py) in points)
            {
                if ((uint)px >= (uint)w || (uint)py >= (uint)h)
                    continue;

                int idx = surface.IndexOf(px, py);
                uint before = Bgra.ReadUIntFromBytes(surface.Pixels, idx);
                uint after = ColorUtil.BlendOver(before, solidColor);

                if (before != after)
                {
                    item.Add(idx, before, after);
                    Bgra.WriteUIntToBytes(surface.Pixels, idx, after);
                }
            }

            // Step 2: If density < 255 or strokeWidth > 1, add soft outer edge
            // Only stamp OUTSIDE the shape for soft falloff
            if (density < 255 || strokeWidth > 1)
            {
                // Find boundary pixels (pixels in shape with at least one neighbor outside)
                var boundaryPixels = new HashSet<(int x, int y)>();
                foreach (var (px, py) in points)
                {
                    if ((uint)px >= (uint)w || (uint)py >= (uint)h)
                        continue;

                    // Check 4-connected neighbors
                    if (!points.Contains((px - 1, py)) ||
                        !points.Contains((px + 1, py)) ||
                        !points.Contains((px, py - 1)) ||
                        !points.Contains((px, py + 1)))
                    {
                        boundaryPixels.Add((px, py));
                    }
                }

                // Stamp brush at boundary pixels, but only affect pixels OUTSIDE the shape
                var mask = BrushMaskCache.Shared.GetOffsets(brushShape, strokeWidth);
                var outerPixelAlphas = new Dictionary<(int x, int y), byte>();

                foreach (var (bx, by) in boundaryPixels)
                {
                    foreach (var (dx, dy) in mask)
                    {
                        int px = bx + dx;
                        int py = by + dy;

                        if ((uint)px >= (uint)w || (uint)py >= (uint)h)
                            continue;

                        // ONLY affect pixels OUTSIDE the shape
                        if (points.Contains((px, py)))
                            continue;

                        byte effA = ComputePerPixelAlpha(dx, dy, strokeWidth, brushShape, density, opacity);
                        if (effA == 0) continue;

                        var key = (px, py);
                        if (!outerPixelAlphas.TryGetValue(key, out byte currentAlpha) || effA > currentAlpha)
                        {
                            outerPixelAlphas[key] = effA;
                        }
                    }
                }

                // Apply outer soft edge pixels
                foreach (var (pos, alpha) in outerPixelAlphas)
                {
                    int idx = surface.IndexOf(pos.x, pos.y);
                    uint before = Bgra.ReadUIntFromBytes(surface.Pixels, idx);
                    uint srcWithAlpha = (color & 0x00FFFFFFu) | ((uint)alpha << 24);
                    uint after = ColorUtil.BlendOver(before, srcWithAlpha);

                    if (before != after)
                    {
                        item.Add(idx, before, after);
                        Bgra.WriteUIntToBytes(surface.Pixels, idx, after);
                    }
                }
            }

            return item.IsEmpty ? null : item;
        }

        /// <summary>
        /// Standard brush-stroke rendering for outlined shapes.
        /// </summary>
        private PixelChangeItem? RenderOutline(
            RasterLayer layer,
            HashSet<(int x, int y)> points,
            ShapeRenderContext context)
        {
            var surface = context.Surface;
            int w = surface.Width, h = surface.Height;

            var item = new PixelChangeItem(layer, context.Description);
            var pixelAlphas = new Dictionary<(int x, int y), byte>();

            int strokeWidth = Math.Max(1, context.StrokeWidth);
            var brushShape = context.BrushShape;
            byte opacity = context.Opacity;
            byte density = context.Density;
            uint color = context.Color;

            // Get brush mask offsets
            var mask = BrushMaskCache.Shared.GetOffsets(brushShape, strokeWidth);

            // Accumulate maximum alpha at each pixel position
            foreach (var (ox, oy) in points)
            {
                foreach (var (dx, dy) in mask)
                {
                    int px = ox + dx;
                    int py = oy + dy;

                    if ((uint)px >= (uint)w || (uint)py >= (uint)h)
                        continue;

                    byte effA = ComputePerPixelAlpha(dx, dy, strokeWidth, brushShape, density, opacity);
                    if (effA == 0) continue;

                    var key = (px, py);
                    if (!pixelAlphas.TryGetValue(key, out byte currentAlpha) || effA > currentAlpha)
                    {
                        pixelAlphas[key] = effA;
                    }
                }
            }

            // Apply all accumulated pixels with blending
            foreach (var (pos, alpha) in pixelAlphas)
            {
                if ((uint)pos.x >= (uint)w || (uint)pos.y >= (uint)h)
                    continue;

                int idx = surface.IndexOf(pos.x, pos.y);
                uint before = Bgra.ReadUIntFromBytes(surface.Pixels, idx);
                uint srcWithAlpha = (color & 0x00FFFFFFu) | ((uint)alpha << 24);
                uint after = ColorUtil.BlendOver(before, srcWithAlpha);

                if (before != after)
                {
                    item.Add(idx, before, after);
                    Bgra.WriteUIntToBytes(surface.Pixels, idx, after);
                }
            }

            return item.IsEmpty ? null : item;
        }

        /// <summary>
        /// Computes per-pixel alpha using density-based falloff.
        /// </summary>
        private static byte ComputePerPixelAlpha(int dx, int dy, int size, BrushShape shape, byte density, byte opacity)
        {
            int sz = Math.Max(1, size);
            double Aop = opacity / 255.0;
            if (Aop <= 0.0) return 0;

            double r = sz / 2.0;
            double d = DistanceForShape(dx, dy, sz, shape);
            if (d > r) return 0;

            double D = density / 255.0;
            double Rhard = r * D;

            if (d <= Rhard)
                return (byte)Math.Round(255.0 * Aop);

            double span = Math.Max(1e-6, r - Rhard);
            double t = (d - Rhard) / span;
            double mask = 1.0 - (t * t) * (3 - 2 * t); // Smoothstep falloff
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }

        /// <summary>
        /// Computes distance metric for brush shape (circle: Euclidean, square: Chebyshev).
        /// </summary>
        private static double DistanceForShape(int dx, int dy, int sz, BrushShape shape)
        {
            double frac = StrokeUtil.ParityFrac(sz);
            double px = dx + 0.5 - frac;
            double py = dy + 0.5 - frac;

            return shape == BrushShape.Circle
                ? Math.Sqrt(px * px + py * py)
                : Math.Max(Math.Abs(px), Math.Abs(py));
        }
    }
}
