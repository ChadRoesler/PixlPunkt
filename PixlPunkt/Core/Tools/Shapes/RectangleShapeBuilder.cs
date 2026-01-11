using System.Collections.Generic;
using PixlPunkt.PluginSdk.Shapes;

namespace PixlPunkt.Core.Tools.Shapes
{
    /// <summary>
    /// Shape builder for rectangles and squares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates pixel sets for axis-aligned rectangles. Supports:
    /// <list type="bullet">
    /// <item><strong>Filled mode:</strong> All pixels within the bounding box</item>
    /// <item><strong>Outline mode:</strong> Only the 1-pixel border</item>
    /// <item><strong>Shift modifier:</strong> Constrains to perfect squares</item>
    /// <item><strong>Ctrl modifier:</strong> Draws from center outward</item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class RectangleShapeBuilder : ShapeBuilderBase
    {
        /// <inheritdoc/>
        public override string DisplayName => "Rectangle";

        /// <inheritdoc/>
        protected override HashSet<(int x, int y)> BuildOutlinePointsCore(int x0, int y0, int width, int height)
        {
            var points = new HashSet<(int x, int y)>();

            // Degenerate case: single point
            if (width == 0 && height == 0)
            {
                points.Add((x0, y0));
                return points;
            }

            int x1 = x0 + width;
            int y1 = y0 + height;

            // Top edge
            for (int x = x0; x <= x1; x++)
                points.Add((x, y0));

            // Bottom edge
            for (int x = x0; x <= x1; x++)
                points.Add((x, y1));

            // Left edge (skip corners already added)
            for (int y = y0 + 1; y <= y1 - 1; y++)
                points.Add((x0, y));

            // Right edge (skip corners already added)
            if (x1 != x0)
            {
                for (int y = y0 + 1; y <= y1 - 1; y++)
                    points.Add((x1, y));
            }

            return points;
        }

        /// <inheritdoc/>
        protected override HashSet<(int x, int y)> BuildFilledPointsCore(int x0, int y0, int width, int height)
        {
            var points = new HashSet<(int x, int y)>();

            int x1 = x0 + width;
            int y1 = y0 + height;

            // Fill entire rectangle
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    points.Add((x, y));
                }
            }

            return points;
        }
    }
}
