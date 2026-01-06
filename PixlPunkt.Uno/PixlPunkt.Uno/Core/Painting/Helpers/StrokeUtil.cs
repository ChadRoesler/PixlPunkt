using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PixlPunkt.Uno.Core.Coloring.Helpers;

namespace PixlPunkt.Uno.Core.Painting.Helpers
{
    /// <summary>
    /// Provides utility methods for stroke rendering, color manipulation, blending, and statistical sampling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// StrokeUtil is a static helper class containing performance-critical operations used throughout
    /// the painting and compositing pipeline. Many methods are marked with <see cref="MethodImplOptions.AggressiveInlining"/>
    /// for optimal performance in hot paths.
    /// </para>
    /// <para>
    /// <strong>Key Functionality:</strong>
    /// - Color channel extraction (A, R, G, B from packed BGRA uint)
    /// - Color interpolation (RGBA and RGB-only lerping)
    /// - Alpha compositing (Porter-Duff "over" blending with premultiplied alpha)
    /// - Similarity testing (tolerance-based color matching for flood fill)
    /// - Pixel buffer read/write operations (BGRA byte array ↔ uint conversion)
    /// - Radial falloff and locality weighting (for brush and jumble effects)
    /// - Statistical sampling (CDF-based weighted random selection)
    /// - Luminance and contrast calculations (WCAG 2.0 relative luminance)
    /// - Mask geometry utilities (grid building for stamp patterns)
    /// </para>
    /// <para>
    /// <strong>Color Format:</strong> All packed colors use BGRA 32-bit format where:
    /// - Bits 0-7: Blue
    /// - Bits 8-15: Green
    /// - Bits 16-23: Red
    /// - Bits 24-31: Alpha
    /// </para>
    /// </remarks>
    public static class StrokeUtil
    {
        private const int ALPHA_EMPTY = 8;             // Threshold below which alpha is considered "empty" (transparent)
        private const int ALPHA_WEIGHT = 1;            // Weight multiplier for alpha channel in similarity calculations






        /// <summary>
        /// Determines if two colors are on the same side of the transparency threshold.
        /// </summary>
        /// <param name="s">First color (BGRA).</param>
        /// <param name="c">Second color (BGRA).</param>
        /// <returns><c>true</c> if both colors are below or both are above the empty threshold (<see cref="ALPHA_EMPTY"/>).</returns>
        /// <remarks>
        /// Used by flood fill to prevent bleeding across transparent/opaque boundaries.
        /// The threshold is 8/255 (approximately 3% opacity).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SameEmptySide(uint s, uint c)
        {
            bool sEmpty = ColorUtil.GetA(s) <= ALPHA_EMPTY;
            bool cEmpty = ColorUtil.GetA(c) <= ALPHA_EMPTY;
            return sEmpty == cEmpty;
        }

        /// <summary>
        /// Tests if two colors are similar within a tolerance, considering transparency boundaries.
        /// </summary>
        /// <param name="c">Color to test (BGRA).</param>
        /// <param name="seed">Reference color (BGRA).</param>
        /// <param name="tol">Tolerance for maximum channel difference (0 = exact match).</param>
        /// <returns><c>true</c> if colors are similar; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// <para>
        /// Uses Chebyshev distance (max of channel differences) rather than Euclidean distance.
        /// Alpha differences are weighted by <see cref="ALPHA_WEIGHT"/> (currently 1x).
        /// </para>
        /// <para>
        /// First checks if colors are on the same side of the transparency threshold via
        /// <see cref="SameEmptySide"/> to prevent transparent/opaque mixing during flood fill operations.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SimilarRGBA(uint c, uint seed, int tol)
        {
            if (!SameEmptySide(seed, c))
                return false;

            int dr = Math.Abs(ColorUtil.GetR(c) - ColorUtil.GetR(seed));
            int dg = Math.Abs(ColorUtil.GetG(c) - ColorUtil.GetG(seed));
            int db = Math.Abs(ColorUtil.GetB(c) - ColorUtil.GetB(seed));
            int dA = Math.Abs(ColorUtil.GetA(c) - ColorUtil.GetA(seed)) * ALPHA_WEIGHT;

            int dRGB = Math.Max(dr, Math.Max(dg, db));
            int d = Math.Max(dRGB, dA);

            return d <= tol;
        }

        /// <summary>
        /// Computes a radial falloff weight using a power-law (gamma) curve.
        /// </summary>
        /// <param name="dx">X offset from center.</param>
        /// <param name="dy">Y offset from center.</param>
        /// <param name="radius">Maximum radius (pixels beyond this return 0).</param>
        /// <param name="gamma">Falloff exponent (higher values concentrate weight near center).</param>
        /// <returns>Weight in range [0, 1], where 1 is at center and 0 is at/beyond radius.</returns>
        /// <remarks>
        /// <para>
        /// Formula: <c>weight = (1 - distance/radius)^gamma</c>
        /// </para>
        /// <para>
        /// Used for brush intensity falloff and jumble effect probability weighting. Gamma is clamped
        /// to minimum 0.05 to prevent division-by-zero or degenerate curves.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double RadialFalloffWeight(int dx, int dy, int radius, double gamma)
        {
            if (radius <= 0) return 0;
            double d = Math.Sqrt((double)dx * dx + (double)dy * dy);
            if (d >= radius) return 0;
            double t = 1.0 - (d / radius);
            gamma = Math.Max(0.05, gamma);
            return Math.Pow(t, gamma);
        }

        /// <summary>
        /// Computes locality acceptance probability for pair selection in jumble operations.
        /// </summary>
        /// <param name="distance">Distance between candidate points.</param>
        /// <param name="radius">Maximum radius (brush size).</param>
        /// <param name="locality">Locality factor (0 = any distance accepted, 1 = very local preference).</param>
        /// <returns>Acceptance probability in range [0, 1].</returns>
        /// <remarks>
        /// <para>
        /// Formula: <c>acceptance = (1 - distance/radius)^(2 + 8·locality)</c>
        /// </para>
        /// <para>
        /// Higher locality values cause the jumble effect to prefer swapping nearby pixels rather
        /// than distant ones, creating more coherent scrambling patterns.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double LocalityAcceptance(double distance, double radius, double locality)
        {
            if (radius <= 0) return 0;
            locality = ColorUtil.Clamp01(locality);
            double x = ColorUtil.Clamp01(distance / radius);
            double exp = 2.0 + 8.0 * locality;
            return Math.Pow(1.0 - x, exp);
        }

        /// <summary>
        /// Builds a cumulative distribution function (CDF) from a list of weights for random sampling.
        /// </summary>
        /// <param name="w">Weight list (negative weights treated as zero).</param>
        /// <param name="total">Output: total sum of all weights.</param>
        /// <returns>CDF array where each element is the cumulative sum up to that index.</returns>
        /// <remarks>
        /// <para>
        /// Used with <see cref="SampleIndex"/> for weighted random selection without replacement.
        /// The CDF enables O(log n) binary search sampling from arbitrary discrete distributions.
        /// </para>
        /// <para>
        /// If all weights are zero, <paramref name="total"/> will be 0 and sampling should be skipped.
        /// </para>
        /// </remarks>
        public static double[] BuildCdf(IReadOnlyList<double> w, out double total)
        {
            var cdf = new double[w.Count];
            double run = 0;
            for (int i = 0; i < w.Count; i++)
            {
                run += Math.Max(0.0, w[i]);
                cdf[i] = run;
            }
            total = run;
            return cdf;
        }

        /// <summary>
        /// Samples a random index from a cumulative distribution function using binary search.
        /// </summary>
        /// <param name="rng">Random number generator.</param>
        /// <param name="cdf">Cumulative distribution function array (from <see cref="BuildCdf"/>).</param>
        /// <param name="total">Total sum of weights.</param>
        /// <returns>Sampled index (0-based), or -1 if distribution is empty (<paramref name="total"/> ≤ 0).</returns>
        /// <remarks>
        /// <para>
        /// Generates a uniform random value in [0, total), then finds the smallest CDF index
        /// where CDF[i] ≥ value. This gives weighted random sampling with O(log n) complexity.
        /// </para>
        /// <para>
        /// Used for jumble pixel pair selection where radial falloff creates non-uniform probabilities.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SampleIndex(Random rng, double[] cdf, double total)
        {
            if (total <= 0) return -1;
            double u = rng.NextDouble() * total;
            int lo = 0, hi = cdf.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (u <= cdf[mid]) hi = mid;
                else lo = mid + 1;
            }
            return lo;
        }

        /// <summary>
        /// Builds a 2D boolean grid from a brush footprint mask for edge detection and neighbor queries.
        /// </summary>
        /// <param name="mask">List of (dx, dy) offsets defining the brush shape.</param>
        /// <param name="grid">Output: 2D boolean array where true indicates a mask pixel.</param>
        /// <param name="minDx">Output: minimum X offset in mask (grid origin X).</param>
        /// <param name="minDy">Output: minimum Y offset in mask (grid origin Y).</param>
        /// <param name="w">Output: grid width.</param>
        /// <param name="h">Output: grid height.</param>
        /// <remarks>
        /// <para>
        /// Converts a sparse offset list into a dense 2D grid for O(1) lookup. The grid is sized
        /// to the bounding box of all offsets, with (minDx, minDy) representing the offset of the
        /// grid origin relative to the brush center.
        /// </para>
        /// <para>
        /// Used for custom brush shape rendering and edge/neighbor detection during stamping.
        /// </para>
        /// </remarks>
        public static void BuildMaskGrid(IReadOnlyList<(int dx, int dy)> mask,
            out bool[,] grid, out int minDx, out int minDy, out int w, out int h)
        {
            minDx = int.MaxValue; int maxDx = int.MinValue;
            minDy = int.MaxValue; int maxDy = int.MinValue;

            foreach (var (dx, dy) in mask)
            {
                if (dx < minDx) minDx = dx;
                if (dx > maxDx) maxDx = dx;
                if (dy < minDy) minDy = dy;
                if (dy > maxDy) maxDy = dy;
            }

            w = maxDx - minDx + 1;
            h = maxDy - minDy + 1;
            grid = new bool[w, h];

            foreach (var (dx, dy) in mask)
                grid[dx - minDx, dy - minDy] = true;
        }

        /// <summary>
        /// Checks if a grid cell is occupied (safe bounds checking).
        /// </summary>
        /// <param name="g">Grid array.</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns><c>true</c> if (x, y) is within bounds and the grid cell is true; otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Occup(bool[,] g, int w, int h, int x, int y)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) return false;
            return g[x, y];
        }

        /// <summary>
        /// Calculates the number of jumble swap events for an iteration based on eligible pixel count and strength.
        /// </summary>
        /// <param name="eligibleCount">Number of pixels eligible for swapping.</param>
        /// <param name="strength">Jumble strength factor (0–1).</param>
        /// <returns>Number of swap events to perform (minimum 1).</returns>
        /// <remarks>
        /// Formula: <c>events = max(1, round(strength × eligibleCount))</c>
        /// Higher strength results in more chaotic scrambling.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EventsPerIteration(int eligibleCount, double strength)
        {
            strength = ColorUtil.Clamp01(strength);
            return Math.Max(1, (int)Math.Round(strength * eligibleCount));
        }

        /// <summary>
        /// Computes the fractional center offset for odd vs. even brush sizes.
        /// </summary>
        /// <param name="sz">Brush size (diameter/side length).</param>
        /// <returns>
        /// 0.0 for even sizes (center falls between pixels), 0.5 for odd sizes (center on pixel).
        /// </returns>
        /// <remarks>
        /// <para>
        /// Ensures brush stamps are visually centered correctly:
        /// - Odd size (e.g., 5): radius = 2.5, center at integer coordinate
        /// - Even size (e.g., 4): radius = 2.0, center at half-pixel offset
        /// </para>
        /// <para>
        /// Used in distance calculations for brush falloff and shape rendering.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ParityFrac(int sz)
        {
            double r = sz / 2.0;
            return r - Math.Floor(r);
        }

        /// <summary>
        /// Computes the maximum pixel radius from brush center for iteration bounds.
        /// </summary>
        /// <param name="sz">Brush size (diameter/side length).</param>
        /// <returns>
        /// The maximum offset magnitude that covers the entire brush footprint.
        /// For size 4: returns 2 (offsets range from -2 to +1)
        /// For size 5: returns 2 (offsets range from -2 to +2)
        /// </returns>
        /// <remarks>
        /// <para>
        /// This matches the offset calculation in <see cref="BrushMaskCache.BuildOffsets"/>
        /// which uses <c>ox = (int)Math.Floor(size / 2.0)</c> for centering.
        /// </para>
        /// <para>
        /// For even sizes: offsets go from -sz/2 to sz/2-1 (e.g., size 4: -2 to +1)
        /// For odd sizes: offsets go from -floor(sz/2) to +floor(sz/2) (e.g., size 5: -2 to +2)
        /// </para>
        /// <para>
        /// Use this for iteration bounds when painters need to manually enumerate pixels
        /// in a brush footprint area (e.g., Jumble, Blur with custom sampling).
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BrushRadius(int sz)
        {
            // For BrushMaskCache: ox = (int)Math.Floor(sz / 2.0)
            // For size 4: ox = 2, offsets are i-ox for i in 0..3 = -2,-1,0,1
            // So max absolute offset is 2 (the negative side)
            return (int)Math.Floor(sz / 2.0);
        }
    }
}