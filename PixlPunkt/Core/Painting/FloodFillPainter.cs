using System;
using System.Buffers;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Painting.Helpers;
using PixlPunkt.Core.Structs;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Default fill painter implementing contiguous flood fill and global color replacement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This painter supports two fill modes:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Contiguous:</strong> Stack-based 4-neighbor flood fill from seed point</item>
    /// <item><strong>Global:</strong> Replaces all matching pixels across the entire surface</item>
    /// </list>
    /// <para>
    /// Both modes support tolerance-based color matching using Chebyshev distance.
    /// When a selection is active, only pixels inside the selection are affected.
    /// </para>
    /// </remarks>
    public sealed class FloodFillPainter : IFillPainter
    {
        /// <summary>
        /// Shared singleton instance for common usage.
        /// </summary>
        public static FloodFillPainter Shared { get; } = new();

        /// <inheritdoc/>
        public IRenderResult? FillAt(RasterLayer layer, int x, int y, FillContext context)
        {
            if (layer == null || context.Surface == null)
                return null;

            var surface = context.Surface;
            int w = surface.Width, h = surface.Height;

            if ((uint)x >= (uint)w || (uint)y >= (uint)h)
                return null;

            // Check if seed point is inside selection (if selection is active)
            if (!context.IsInSelection(x, y))
                return null;

            int seedIdx = surface.IndexOf(x, y);
            uint seedColor = Bgra.ReadUIntFromBytes(surface.Pixels, seedIdx);
            uint newColor = context.Color;

            // Early exit if seed matches new color exactly and tolerance is 0
            if (context.Tolerance == 0 && ColorUtil.ExactRGBA(seedColor, newColor))
                return null;

            var description = context.Contiguous ? "Fill" : "Global Fill";
            var item = new PixelChangeItem(layer, description, Icon.PaintBucket);

            if (context.Contiguous)
            {
                FillContiguous(surface, x, y, seedColor, newColor, context.Tolerance, w, h, context, item);
            }
            else
            {
                FillGlobal(surface, seedColor, newColor, context.Tolerance, w, h, context, item);
            }

            return item.IsEmpty ? null : item;
        }

        /// <summary>
        /// Flood fill (contiguous) using stack-based DFS with tolerance.
        /// Respects selection mask when active.
        /// Uses ArrayPool for the seen array to reduce GC pressure.
        /// </summary>
        private static void FillContiguous(
            Imaging.PixelSurface surface,
            int sx, int sy,
            uint seed, uint newColor,
            int tolerance,
            int w, int h,
            FillContext context,
            PixelChangeItem item)
        {
            int arraySize = w * h;

            // Rent array from pool to reduce GC pressure for large canvases
            var seen = ArrayPool<bool>.Shared.Rent(arraySize);

            try
            {
                // Clear the rented portion we'll use (pool may return larger array)
                Array.Clear(seen, 0, arraySize);

                var stack = new Stack<(int x, int y)>(Math.Min(1024, arraySize / 4));
                stack.Push((sx, sy));
                seen[sy * w + sx] = true;

                while (stack.Count > 0)
                {
                    var (x, y) = stack.Pop();

                    // Check selection mask - skip pixels outside selection
                    if (!context.IsInSelection(x, y))
                        continue;

                    int idx = surface.IndexOf(x, y);

                    uint cur = Bgra.ReadUIntFromBytes(surface.Pixels, idx);

                    if (!StrokeUtil.SimilarRGBA(cur, seed, tolerance))
                        continue;

                    if (cur != newColor)
                    {
                        item.Add(idx, cur, newColor);
                        Bgra.WriteUIntToBytes(surface.Pixels, idx, newColor);
                    }

                    // 4-neighbor expansion
                    if (x > 0 && !seen[y * w + (x - 1)])
                    {
                        seen[y * w + (x - 1)] = true;
                        stack.Push((x - 1, y));
                    }
                    if (x + 1 < w && !seen[y * w + (x + 1)])
                    {
                        seen[y * w + (x + 1)] = true;
                        stack.Push((x + 1, y));
                    }
                    if (y > 0 && !seen[(y - 1) * w + x])
                    {
                        seen[(y - 1) * w + x] = true;
                        stack.Push((x, y - 1));
                    }
                    if (y + 1 < h && !seen[(y + 1) * w + x])
                    {
                        seen[(y + 1) * w + x] = true;
                        stack.Push((x, y + 1));
                    }
                }
            }
            finally
            {
                // Always return array to pool
                ArrayPool<bool>.Shared.Return(seen);
            }
        }

        /// <summary>
        /// Global fill: scans entire surface applying tolerance-based color replacement.
        /// Respects selection mask when active.
        /// </summary>
        private static void FillGlobal(
            Imaging.PixelSurface surface,
            uint seed, uint newColor,
            int tolerance,
            int w, int h,
            FillContext context,
            PixelChangeItem item)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Check selection mask - skip pixels outside selection
                    if (!context.IsInSelection(x, y))
                        continue;

                    int idx = (y * w + x) * 4;
                    uint cur = Bgra.ReadUIntFromBytes(surface.Pixels, idx);

                    if (!StrokeUtil.SimilarRGBA(cur, seed, tolerance))
                        continue;

                    if (cur == newColor)
                        continue;

                    item.Add(idx, cur, newColor);
                    Bgra.WriteUIntToBytes(surface.Pixels, idx, newColor);
                }
            }
        }
    }
}
