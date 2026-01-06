using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PixlPunkt.Uno.Core.Selection
{
    /// <summary>
    /// Builds polygon outlines from <see cref="SelectionMask"/> using convex hull or exact pixel tracing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionOutlineBuilder converts boolean pixel masks into vector polygons suitable for rendering
    /// and transform operations. Two algorithms are provided to balance simplicity vs. accuracy:
    /// </para>
    /// <para><strong>Algorithm Comparison:</strong></para>
    /// <list type="table">
    /// <listheader>
    /// <term>Algorithm</term>
    /// <description>Characteristics</description>
    /// </listheader>
    /// <item>
    /// <term>Convex Hull</term>
    /// <description>Fast Graham scan (O(N log N)). Produces minimal polygon that encloses all selected
    /// pixels. Loses concave features and holes. Ideal for simple shape transforms.</description>
    /// </item>
    /// <item>
    /// <term>Exact Pixel Trace</term>
    /// <description>Marching squares perimeter walk (O(perimeter)). Follows exact pixel boundaries,
    /// preserving concave regions and details. Required for accurate lasso and magic wand results.</description>
    /// </item>
    /// </list>
    /// <para><strong>Output Format:</strong></para>
    /// <para>
    /// All polygons are returned with vertices snapped to pixel corners (integer coordinates).
    /// Exact trace mode includes polygon simplification to remove collinear points, reducing
    /// vertex count without losing shape fidelity.
    /// </para>
    /// </remarks>
    /// <seealso cref="SelectionMask"/>
    /// <seealso cref="PixelSelection"/>
    public static class SelectionOutlineBuilder
    {
        // ─────────────────────────────────────────────────────────────
        // MAIN ENTRY POINT
        // mask → polygon outline
        // choose between exact or convex-hull outlines
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a polygon outline from a selection mask.
        /// </summary>
        /// <param name="mask">Source selection mask to trace.</param>
        /// <param name="useExactTrace">If true, uses pixel-accurate tracing (marching squares).
        /// If false, uses fast convex hull approximation.</param>
        /// <returns>List of polygon vertices in document pixel space, or empty list if mask is empty.</returns>
        /// <remarks>
        /// <para>
        /// Vertices are snapped to pixel corners for clean rendering. Exact trace mode produces
        /// polygons that perfectly follow the mask boundary. Convex hull mode produces simpler
        /// polygons at the cost of losing concave features.
        /// </para>
        /// <para><strong>Performance:</strong></para>
        /// <para>
        /// - Convex hull: O(N log N) where N = number of selected pixels
        /// <br/>- Exact trace: O(perimeter) where perimeter = outline edge count
        /// </para>
        /// </remarks>
        public static List<Vector2> BuildOutline(SelectionMask mask, bool useExactTrace = true)
        {
            var filled = mask.EnumerateFilledPixels().ToList();
            if (filled.Count == 0)
                return [];

            if (!useExactTrace)
                return BuildConvexHull(filled);

            return BuildExactPixelOutline(filled);
        }

        // ─────────────────────────────────────────────────────────────
        // CONVEX HULL (Graham scan)
        // approximates outline with minimal polygon
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a convex hull outline using Graham scan algorithm.
        /// </summary>
        /// <param name="pts">List of selected pixel coordinates.</param>
        /// <returns>Minimal convex polygon containing all points.</returns>
        /// <remarks>
        /// <para><strong>Algorithm Steps:</strong></para>
        /// <list type="number">
        /// <item>Convert pixel coords to center points (x+0.5, y+0.5)</item>
        /// <item>Find lowest Y point (bottom-most, then left-most) as pivot</item>
        /// <item>Sort remaining points by polar angle from pivot</item>
        /// <item>Graham scan: iteratively build hull by removing right turns</item>
        /// <item>Snap final vertices to pixel corners</item>
        /// </list>
        /// <para>
        /// Cross product test determines turn direction: positive = left turn (keep),
        /// negative/zero = right turn or collinear (remove). Results in CCW-oriented polygon.
        /// </para>
        /// </remarks>
        private static List<Vector2> BuildConvexHull(List<(int x, int y)> pts)
        {
            // Convert pixel list to points at pixel centers
            var points = pts.Select(p => new Vector2(p.x + 0.5f, p.y + 0.5f)).ToList();
            if (points.Count <= 3) return points;

            // 1. Find lowest Y (then leftmost)
            points.Sort((a, b) =>
                a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));

            var pivot = points[0];

            // 2. Sort by angle from pivot
            points.Sort(1, points.Count - 1,
                Comparer<Vector2>.Create((a, b) =>
                {
                    float angA = MathF.Atan2(a.Y - pivot.Y, a.X - pivot.X);
                    float angB = MathF.Atan2(b.Y - pivot.Y, b.X - pivot.X);
                    return angA.CompareTo(angB);
                }));

            // 3. Graham scan
            var hull = new List<Vector2>
            {
                points[0],
                points[1]
            };

            for (int i = 2; i < points.Count; i++)
            {
                while (hull.Count >= 2 &&
                       Cross(hull[^2], hull[^1], points[i]) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }
                hull.Add(points[i]);
            }

            // Snap vertices to pixel corners
            for (int i = 0; i < hull.Count; i++)
                hull[i] = SnapToPixel(hull[i]);

            return hull;
        }

        /// <summary>
        /// Computes 2D cross product (Z component) for turn direction test.
        /// </summary>
        /// <param name="a">First point.</param>
        /// <param name="b">Second point (turn vertex).</param>
        /// <param name="c">Third point.</param>
        /// <returns>Positive = left turn, negative = right turn, zero = collinear.</returns>
        /// <remarks>
        /// Formula: (b.X - a.X) × (c.Y - a.Y) - (b.Y - a.Y) × (c.X - a.X).
        /// Used by Graham scan to determine if point C is left or right of line AB.
        /// </remarks>
        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.X - a.X) * (c.Y - a.Y) -
                   (b.Y - a.Y) * (c.X - a.X);
        }

        // ─────────────────────────────────────────────────────────────
        // EXACT PIXEL OUTLINE TRACING (Marching Squares)
        // final outline matches pixel mask shape exactly
        // ─────────────────────────────────────────────────────────────

        private struct Node { public int X, Y; }

        private static readonly (int dx, int dy)[] dirs =
        [
            (1,0),  // right
            (0,1),  // down
            (-1,0), // left
            (0,-1), // up
        ];

        /// <summary>
        /// Builds exact pixel outline using marching squares perimeter tracing.
        /// </summary>
        /// <param name="pixels">List of selected pixel coordinates.</param>
        /// <returns>Polygon following exact pixel boundaries.</returns>
        /// <remarks>
        /// <para><strong>Marching Squares Algorithm:</strong></para>
        /// <list type="number">
        /// <item>Find starting pixel (lowest Y, then leftmost X)</item>
        /// <item>Begin at pixel's bottom-left corner, facing right</item>
        /// <item>At each step:
        /// <list type="bullet">
        /// <item>Try turning right (follow outer edge)</item>
        /// <item>If blocked, continue forward</item>
        /// <item>If blocked again, turn left (inner corner)</item>
        /// </list>
        /// </item>
        /// <item>Record corner positions until returning to start</item>
        /// <item>Simplify polygon to remove collinear points</item>
        /// </list>
        /// <para>
        /// This "wall-following" approach produces a CCW-oriented polygon that traces the selection's
        /// outer perimeter. Works correctly for concave shapes and preserves pixel-accurate boundaries.
        /// </para>
        /// </remarks>
        private static List<Vector2> BuildExactPixelOutline(List<(int x, int y)> pixels)
        {
            if (pixels.Count == 0)
                return [];

            var set = new HashSet<(int x, int y)>(pixels);

            // find starting pixel (lowest, then leftmost)
            var start = pixels.OrderBy(p => p.y).ThenBy(p => p.x).First();

            // trace perimeter by hugging edges
            List<Vector2> outline = [];
            var current = (start.x, start.y);
            int dir = 0; // start facing right

            // Convert pixel to bottom-left corner in doc space
            static Vector2 Corner(int px, int py) => new(px, py + 1);

            // Marching squares perimeter walk
            do
            {
                outline.Add(SnapToPixel(Corner(current.x, current.y)));

                // Look right first (turn right)
                int rightDir = (dir + 3) % 4;
                if (!set.Contains(Neighbor(current, rightDir)))
                {
                    dir = rightDir;
                }
                else
                {
                    // Look forward
                    if (set.Contains(Neighbor(current, dir)))
                    {
                        // keep direction
                    }
                    else
                    {
                        // turn left
                        dir = (dir + 1) % 4;
                    }
                }

                current = Neighbor(current, dir);

            } while (current != (start.x, start.y));

            // Final dedupe & simplified poly
            outline = Simplify(outline);

            return outline;
        }

        /// <summary>
        /// Gets neighboring pixel in specified direction.
        /// </summary>
        /// <param name="p">Current pixel coordinates.</param>
        /// <param name="d">Direction index (0=right, 1=down, 2=left, 3=up).</param>
        /// <returns>Neighbor pixel coordinates.</returns>
        private static (int x, int y) Neighbor((int x, int y) p, int d)
        {
            var (dx, dy) = dirs[d];
            return (p.x + dx, p.y + dy);
        }

        // ─────────────────────────────────────────────────────────────
        // POLY CLEANUP & SNAP
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Snaps a vector to nearest integer pixel corner coordinates.
        /// </summary>
        /// <param name="v">Input vector (may have fractional components).</param>
        /// <returns>Vector with X and Y rounded to nearest integer.</returns>
        /// <remarks>
        /// Ensures polygon vertices align with pixel grid for clean rendering and consistent
        /// hit-testing. Uses <see cref="Math.Round(double)"/> for banker's rounding.
        /// </remarks>
        private static Vector2 SnapToPixel(Vector2 v)
        {
            return new Vector2(
                (float)Math.Round(v.X),
                (float)Math.Round(v.Y)
            );
        }

        /// <summary>
        /// Removes collinear points from polygon to reduce vertex count.
        /// </summary>
        /// <param name="pts">Input polygon vertices.</param>
        /// <returns>Simplified polygon with collinear points removed.</returns>
        /// <remarks>
        /// <para>
        /// Tests each triple of consecutive points using cross product. If points are nearly
        /// collinear (area &lt; 0.001), the middle point is removed. This reduces polygon
        /// complexity without losing shape accuracy.
        /// </para>
        /// <para>
        /// Essential for marching squares output which can produce many redundant vertices
        /// along straight edges.
        /// </para>
        /// </remarks>
        private static List<Vector2> Simplify(List<Vector2> pts)
        {
            if (pts.Count < 3) return pts;

            List<Vector2> outPts = [];
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                var c = pts[(i + 2) % pts.Count];

                if (!IsCollinear(a, b, c))
                    outPts.Add(b);
            }
            return outPts;
        }

        /// <summary>
        /// Tests if three points are approximately collinear.
        /// </summary>
        /// <param name="a">First point.</param>
        /// <param name="b">Second point (tested for removal).</param>
        /// <param name="c">Third point.</param>
        /// <param name="eps">Epsilon threshold for area test. Default is 0.001.</param>
        /// <returns><c>true</c> if triangle ABC has negligible area; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Computes triangle area via cross product. If area is below epsilon, points are
        /// considered collinear and B can be safely removed.
        /// </remarks>
        private static bool IsCollinear(Vector2 a, Vector2 b, Vector2 c, float eps = 0.001f)
        {
            float area = Math.Abs((b.X - a.X) * (c.Y - a.Y) -
                                  (b.Y - a.Y) * (c.X - a.X));
            return area < eps;
        }
    }
}
