namespace PixlPunkt.PluginSdk.Shapes
{
    /// <summary>
    /// Base class for shape builders that provides common functionality.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ShapeBuilderBase"/> provides:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Coordinate normalization:</strong> Ensures shapes can be drawn in any direction</item>
    /// <item><strong>Standard modifier handling:</strong> Shift for constrained proportions, Ctrl for center-out</item>
    /// <item><strong>Bounding box calculation:</strong> Helper for getting normalized bounds</item>
    /// </list>
    /// <para>
    /// Plugin shape builders should inherit from this class and override <see cref="BuildOutlinePointsCore"/>
    /// and <see cref="BuildFilledPointsCore"/> to implement their specific geometry. The base class
    /// handles coordinate normalization automatically.
    /// </para>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// public class StarShapeBuilder : ShapeBuilderBase
    /// {
    ///     public override string DisplayName => "Star";
    ///     
    ///     protected override HashSet&lt;(int x, int y)&gt; BuildOutlinePointsCore(int x0, int y0, int width, int height)
    ///     {
    ///         // x0, y0 is always top-left; width and height are always positive
    ///         var points = new HashSet&lt;(int x, int y)&gt;();
    ///         // ... build star outline ...
    ///         return points;
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public abstract class ShapeBuilderBase : IShapeBuilder
    {
        /// <inheritdoc/>
        public abstract string DisplayName { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// The default implementation provides standard modifier behavior:
        /// <list type="bullet">
        /// <item><strong>Shift:</strong> Constrains to equal width/height (square/circle)</item>
        /// <item><strong>Ctrl:</strong> Draws from center outward</item>
        /// </list>
        /// Override this method if your shape needs custom modifier behavior.
        /// </remarks>
        public virtual (int x0, int y0, int x1, int y1) ApplyModifiers(int startX, int startY, int endX, int endY, bool shift, bool ctrl)
        {
            int x0 = startX, y0 = startY, x1 = endX, y1 = endY;

            if (!ctrl)
            {
                // Corner-to-corner mode (default)
                x1 = endX;
                y1 = endY;

                if (shift)
                {
                    // Constrain to equal dimensions: use larger dimension for both axes
                    int dx = Math.Abs(x1 - x0);
                    int dy = Math.Abs(y1 - y0);
                    int d = Math.Max(dx, dy);
                    x1 = (x1 >= x0) ? x0 + d : x0 - d;
                    y1 = (y1 >= y0) ? y0 + d : y0 - d;
                }
            }
            else
            {
                // Center-out mode: start point is center, end point defines extent
                int dx = endX - startX;
                int dy = endY - startY;

                if (shift)
                {
                    // Constrain to equal dimensions in center-out mode
                    int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    dx = (dx >= 0) ? d : -d;
                    dy = (dy >= 0) ? d : -d;
                }

                // Expand from center: x0,y0 becomes one corner, x1,y1 the opposite
                x0 = startX - dx;
                y0 = startY - dy;
                x1 = startX + dx;
                y1 = startY + dy;
            }

            // DO NOT normalize here - the host handles that when calling Build methods
            return (x0, y0, x1, y1);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This method normalizes coordinates and delegates to <see cref="BuildOutlinePointsCore"/>.
        /// </remarks>
        public HashSet<(int x, int y)> BuildOutlinePoints(int x0, int y0, int x1, int y1)
        {
            // Normalize coordinates so the Core method always gets positive width/height
            NormalizeCoordinates(ref x0, ref y0, ref x1, ref y1, out int width, out int height);
            return BuildOutlinePointsCore(x0, y0, width, height);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This method normalizes coordinates and delegates to <see cref="BuildFilledPointsCore"/>.
        /// </remarks>
        public HashSet<(int x, int y)> BuildFilledPoints(int x0, int y0, int x1, int y1)
        {
            // Normalize coordinates so the Core method always gets positive width/height
            NormalizeCoordinates(ref x0, ref y0, ref x1, ref y1, out int width, out int height);
            return BuildFilledPointsCore(x0, y0, width, height);
        }

        /// <summary>
        /// Builds the set of outline points for this shape using normalized coordinates.
        /// </summary>
        /// <param name="x0">Left X coordinate (always &lt;= x0 + width).</param>
        /// <param name="y0">Top Y coordinate (always &lt;= y0 + height).</param>
        /// <param name="width">Shape width (always &gt;= 0).</param>
        /// <param name="height">Shape height (always &gt;= 0).</param>
        /// <returns>Set of (x, y) pixel coordinates on the shape's outline.</returns>
        /// <remarks>
        /// Override this method to implement your shape's outline geometry.
        /// The coordinates are guaranteed to be normalized (x0 is left, y0 is top,
        /// width and height are positive).
        /// </remarks>
        protected abstract HashSet<(int x, int y)> BuildOutlinePointsCore(int x0, int y0, int width, int height);

        /// <summary>
        /// Builds the set of filled points for this shape using normalized coordinates.
        /// </summary>
        /// <param name="x0">Left X coordinate (always &lt;= x0 + width).</param>
        /// <param name="y0">Top Y coordinate (always &lt;= y0 + height).</param>
        /// <param name="width">Shape width (always &gt;= 0).</param>
        /// <param name="height">Shape height (always &gt;= 0).</param>
        /// <returns>Set of (x, y) pixel coordinates inside the shape.</returns>
        /// <remarks>
        /// Override this method to implement your shape's fill geometry.
        /// The coordinates are guaranteed to be normalized (x0 is left, y0 is top,
        /// width and height are positive).
        /// </remarks>
        protected abstract HashSet<(int x, int y)> BuildFilledPointsCore(int x0, int y0, int width, int height);

        /// <summary>
        /// Normalizes coordinates so x0 &lt;= x1 and y0 &lt;= y1, and computes width/height.
        /// </summary>
        /// <param name="x0">First X coordinate (modified to be minimum).</param>
        /// <param name="y0">First Y coordinate (modified to be minimum).</param>
        /// <param name="x1">Second X coordinate (modified to be maximum).</param>
        /// <param name="y1">Second Y coordinate (modified to be maximum).</param>
        /// <param name="width">Output: positive width (x1 - x0).</param>
        /// <param name="height">Output: positive height (y1 - y0).</param>
        protected static void NormalizeCoordinates(ref int x0, ref int y0, ref int x1, ref int y1, out int width, out int height)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (y0 > y1) (y0, y1) = (y1, y0);
            width = x1 - x0;
            height = y1 - y0;
        }

        /// <summary>
        /// Gets the normalized bounding box from two corner points.
        /// </summary>
        /// <param name="x0">First X coordinate.</param>
        /// <param name="y0">First Y coordinate.</param>
        /// <param name="x1">Second X coordinate.</param>
        /// <param name="y1">Second Y coordinate.</param>
        /// <returns>Normalized bounding box (left, top, width, height).</returns>
        protected static (int left, int top, int width, int height) GetBounds(int x0, int y0, int x1, int y1)
        {
            int left = Math.Min(x0, x1);
            int top = Math.Min(y0, y1);
            int width = Math.Abs(x1 - x0);
            int height = Math.Abs(y1 - y0);
            return (left, top, width, height);
        }

        /// <summary>
        /// Draws a line between two points using Bresenham's algorithm.
        /// </summary>
        /// <param name="points">The set to add points to.</param>
        /// <param name="x0">Start X.</param>
        /// <param name="y0">Start Y.</param>
        /// <param name="x1">End X.</param>
        /// <param name="y1">End Y.</param>
        protected static void DrawLine(HashSet<(int x, int y)> points, int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                points.Add((x0, y0));

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        /// <summary>
        /// Fills a polygon using the scanline algorithm.
        /// </summary>
        /// <param name="points">The set to add points to.</param>
        /// <param name="vertices">The polygon vertices in order.</param>
        /// <param name="minY">Minimum Y coordinate to scan.</param>
        /// <param name="maxY">Maximum Y coordinate to scan.</param>
        protected static void FillPolygon(HashSet<(int x, int y)> points, IReadOnlyList<(double x, double y)> vertices, int minY, int maxY)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var intersections = new List<double>();

                // Find intersections with polygon edges
                for (int i = 0; i < vertices.Count; i++)
                {
                    var v1 = vertices[i];
                    var v2 = vertices[(i + 1) % vertices.Count];

                    if ((v1.y <= y && v2.y > y) || (v2.y <= y && v1.y > y))
                    {
                        double x = v1.x + (y - v1.y) / (v2.y - v1.y) * (v2.x - v1.x);
                        intersections.Add(x);
                    }
                }

                // Sort intersections and fill between pairs
                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    int xStart = (int)Math.Ceiling(intersections[i]);
                    int xEnd = (int)Math.Floor(intersections[i + 1]);

                    for (int x = xStart; x <= xEnd; x++)
                    {
                        points.Add((x, y));
                    }
                }
            }
        }
    }
}
