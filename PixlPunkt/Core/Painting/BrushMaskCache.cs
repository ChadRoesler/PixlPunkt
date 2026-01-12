using System;
using System.Collections.Generic;
using PixlPunkt.Core.Brush;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Caches precomputed brush footprint pixel offsets for different brush shapes and sizes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BrushMaskCache optimizes brush rendering by precomputing which pixels fall within a brush's
    /// footprint for each (shape, size) combination. Once computed, offset lists are cached to avoid
    /// redundant calculations during continuous painting strokes.
    /// </para>
    /// <para><strong>Brush Shape Implementations:</strong></para>
    /// <list type="bullet">
    /// <item><strong><see cref="BrushShape.Square"/></strong>: Simple grid covering size×size pixels.</item>
    /// <item><strong><see cref="BrushShape.Circle"/></strong>: Uses supersampling for smooth circular edges.</item>
    /// <item><strong><see cref="BrushShape.Custom"/></strong>: Scales a 16x16 mask from <see cref="BrushTemplate"/>.</item>
    /// </list>
    /// <para><strong>Custom Brush Scaling:</strong></para>
    /// <para>
    /// Custom brushes use a single 16x16 master mask that is scaled to the requested size:
    /// <br/>• Size 1-16: Scale down using nearest-neighbor
    /// <br/>• Size 17-128: Scale up using nearest-neighbor
    /// </para>
    /// </remarks>
    public sealed class BrushMaskCache
    {
        /// <summary>
        /// Supersampling resolution per pixel dimension (8×8 = 64 samples per pixel).
        /// </summary>
        private const int SS = 8;

        /// <summary>
        /// Coverage threshold for pixel inclusion (0.6 = 60% of subpixels must be inside circle).
        /// </summary>
        private const double COVER = 0.6;

        /// <summary>
        /// The standard mask size for custom brushes (16x16).
        /// </summary>
        private const int MaskSize = 16;

        /// <summary>
        /// Shared singleton instance used by both main canvas and preview to ensure identical masks.
        /// </summary>
        public static readonly BrushMaskCache Shared = new();

        /// <summary>
        /// Cache dictionary mapping (shape, size) tuples to precomputed offset lists.
        /// </summary>
        private readonly Dictionary<(BrushShape shape, int size), List<(int dx, int dy)>> _cache = new();

        /// <summary>
        /// Cache dictionary for custom brush offsets, keyed by (brushFullName, requestedSize).
        /// </summary>
        private readonly Dictionary<(string brushFullName, int size), List<(int dx, int dy)>> _customCache = new();

        /// <summary>
        /// Gets the pixel offset list for a brush with the specified shape and size.
        /// </summary>
        public IReadOnlyList<(int dx, int dy)> GetOffsets(BrushShape shape, int size)
        {
            size = Math.Max(1, size);
            var key = (shape, size);

            if (_cache.TryGetValue(key, out var list))
                return list;

            list = BuildOffsets(shape, size);
            _cache[key] = list;
            return list;
        }

        /// <summary>
        /// Gets the pixel offset list for a custom brush at the specified size.
        /// </summary>
        /// <param name="brush">The custom brush template containing the 16x16 mask.</param>
        /// <param name="size">Requested brush size in pixels (1-128).</param>
        /// <returns>
        /// A read-only list of (dx, dy) offsets relative to brush center. Cached after first computation.
        /// </returns>
        public IReadOnlyList<(int dx, int dy)> GetOffsetsForCustomBrush(BrushTemplate? brush, int size)
        {
            if (brush == null || brush.Mask == null || brush.Mask.Length == 0)
                return Array.Empty<(int, int)>();

            size = Math.Clamp(size, 1, 128);
            var key = (brush.FullName, size);

            if (_customCache.TryGetValue(key, out var list))
                return list;

            list = BuildCustomBrushOffsets(brush, size);
            _customCache[key] = list;
            return list;
        }

        /// <summary>
        /// Clears all cached custom brush offsets.
        /// </summary>
        public void ClearCustomBrushCache()
        {
            _customCache.Clear();
        }

        /// <summary>
        /// Clears cached offsets for a specific custom brush.
        /// </summary>
        public void ClearCustomBrushCache(string brushFullName)
        {
            var keysToRemove = new List<(string, int)>();
            foreach (var key in _customCache.Keys)
            {
                if (key.brushFullName == brushFullName)
                    keysToRemove.Add(key);
            }
            foreach (var key in keysToRemove)
                _customCache.Remove(key);
        }

        /// <summary>
        /// Builds the pixel offset list for a specific brush configuration.
        /// </summary>
        private static List<(int dx, int dy)> BuildOffsets(BrushShape shape, int size)
        {
            var list = new List<(int, int)>(size * size);
            size = Math.Max(1, size);

            double r = size / 2.0;
            int ox = (int)Math.Floor(r), oy = ox;

            if (shape == BrushShape.Square)
            {
                for (int j = 0; j < size; j++)
                    for (int i = 0; i < size; i++)
                        list.Add((i - ox, j - oy));

                return list;
            }

            // Circle: use supersampling for smooth edges  
            double r2 = r * r;

            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    int inside = 0;

                    for (int sy = 0; sy < SS; sy++)
                    {
                        double yy = j + (sy + 0.5) / SS;
                        double dy = yy - r;
                        double dy2 = dy * dy;

                        for (int sx = 0; sx < SS; sx++)
                        {
                            double xx = i + (sx + 0.5) / SS;
                            double dx = xx - r;

                            if (dx * dx + dy2 <= r2)
                                inside++;
                        }
                    }

                    double coverage = (double)inside / (SS * SS);
                    if (coverage >= COVER)
                        list.Add((i - ox, j - oy));
                }
            }

            // Size-1 circle brushes may fail supersampling coverage test
            if (size == 1 && list.Count == 0)
            {
                list.Add((0, 0));
            }
            return list;
        }

        /// <summary>
        /// Builds the pixel offset list for a custom brush at the requested size.
        /// Scales the 16x16 mask to the target size using nearest-neighbor sampling.
        /// </summary>
        private static List<(int dx, int dy)> BuildCustomBrushOffsets(BrushTemplate brush, int targetSize)
        {
            var list = new List<(int, int)>(targetSize * targetSize);

            // Scale factor from 16x16 to target size
            float scale = (float)targetSize / MaskSize;

            // Calculate center of target bounding box using pivot
            int centerX = (int)Math.Round(brush.PivotX * targetSize);
            int centerY = (int)Math.Round(brush.PivotY * targetSize);

            // Sample the 16x16 mask at scaled positions
            for (int ty = 0; ty < targetSize; ty++)
            {
                // Map target Y back to mask Y (0-15)
                int maskY = (int)Math.Floor(ty / scale);
                maskY = Math.Clamp(maskY, 0, MaskSize - 1);

                for (int tx = 0; tx < targetSize; tx++)
                {
                    // Map target X back to mask X (0-15)
                    int maskX = (int)Math.Floor(tx / scale);
                    maskX = Math.Clamp(maskX, 0, MaskSize - 1);

                    if (brush.GetPixel(maskX, maskY))
                    {
                        int dx = tx - centerX;
                        int dy = ty - centerY;
                        list.Add((dx, dy));
                    }
                }
            }

            // Ensure at least center pixel is included
            if (list.Count == 0)
                list.Add((0, 0));

            return list;
        }
    }
}