using System;
using System.Buffers;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Coloring.Helpers;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.History;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.Core.Painting.Helpers;
using PixlPunkt.Uno.Core.Structs;

namespace PixlPunkt.Uno.Core.Painting
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
    /// <para>
    /// The flood fill uses a bounded stack with queue fallback to prevent stack overflow
    /// on extremely large fill regions.
    /// </para>
    /// </remarks>
    public sealed class FloodFillPainter : IFillPainter
    {
        /// <summary>
        /// Maximum stack size before switching to queue-based processing.
        /// This prevents stack overflow while maintaining good performance for typical fills.
        /// </summary>
        private const int MaxStackSize = 100_000;

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
        /// Uses bounded stack with queue fallback to prevent stack overflow on large regions.
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

                // Start with stack for typical cases (faster due to cache locality)
                var stack = new Stack<(int x, int y)>(Math.Min(4096, arraySize / 4));
                
                // Queue for overflow handling - only allocated if needed
                Queue<(int x, int y)>? queue = null;
                
                stack.Push((sx, sy));
                seen[sy * w + sx] = true;

                while (stack.Count > 0 || (queue != null && queue.Count > 0))
                {
                    // Prefer stack, fall back to queue
                    (int x, int y) current;
                    if (stack.Count > 0)
                    {
                        current = stack.Pop();
                    }
                    else
                    {
                        current = queue!.Dequeue();
                    }

                    int x = current.x;
                    int y = current.y;

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

                    // 4-neighbor expansion with stack overflow protection
                    AddNeighbor(x - 1, y, w, h, seen, stack, ref queue);
                    AddNeighbor(x + 1, y, w, h, seen, stack, ref queue);
                    AddNeighbor(x, y - 1, w, h, seen, stack, ref queue);
                    AddNeighbor(x, y + 1, w, h, seen, stack, ref queue);
                }
                
                // Log if we had to use the queue (indicates very large fill)
                if (queue != null)
                {
                    LoggingService.Debug("Flood fill used queue fallback for large region");
                }
            }
            finally
            {
                // Always return array to pool
                ArrayPool<bool>.Shared.Return(seen);
            }
        }

        /// <summary>
        /// Adds a neighbor to the processing collection with overflow protection.
        /// </summary>
        private static void AddNeighbor(
            int x, int y, int w, int h,
            bool[] seen,
            Stack<(int x, int y)> stack,
            ref Queue<(int x, int y)>? queue)
        {
            // Bounds check
            if (x < 0 || x >= w || y < 0 || y >= h)
                return;

            int seenIdx = y * w + x;
            if (seen[seenIdx])
                return;

            seen[seenIdx] = true;

            // If stack is getting too large, overflow to queue
            if (stack.Count >= MaxStackSize)
            {
                queue ??= new Queue<(int x, int y)>(4096);
                queue.Enqueue((x, y));
            }
            else
            {
                stack.Push((x, y));
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
