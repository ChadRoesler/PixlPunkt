using System;
using System.Collections.Generic;
using PixlPunkt.PluginSdk.Shapes;

namespace PixlPunkt.Core.Tools.Shapes
{
    /// <summary>
    /// Shape builder for ellipses and circles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates pixel sets for ellipses using the midpoint ellipse algorithm. Supports:
    /// <list type="bullet">
    /// <item><strong>Filled mode:</strong> All pixels within the ellipse boundary</item>
    /// <item><strong>Outline mode:</strong> Only the 1-pixel ellipse perimeter</item>
    /// <item><strong>Shift modifier:</strong> Constrains to perfect circles</item>
    /// <item><strong>Ctrl modifier:</strong> Draws from center outward</item>
    /// </list>
    /// </para>
    /// <para>
    /// The algorithm uses integer arithmetic for pixel-perfect rendering suitable for pixel art.
    /// </para>
    /// </remarks>
    public sealed class EllipseShapeBuilder : ShapeBuilderBase
    {
        /// <inheritdoc/>
        public override string DisplayName => "Ellipse";

        /// <inheritdoc/>
        protected override HashSet<(int x, int y)> BuildOutlinePointsCore(int x0, int y0, int width, int height)
        {
            var points = new HashSet<(int x, int y)>();

            // Degenerate cases
            if (width <= 0 && height <= 0)
            {
                points.Add((x0, y0));
                return points;
            }

            if (height == 0)
            {
                // Horizontal line
                for (int x = x0; x <= x0 + width; x++)
                    points.Add((x, y0));
                return points;
            }

            if (width == 0)
            {
                // Vertical line
                for (int y = y0; y <= y0 + height; y++)
                    points.Add((x0, y));
                return points;
            }

            // Midpoint ellipse algorithm
            RasterizeEllipse(x0, y0, x0 + width, y0 + height, filled: false, points);

            return points;
        }

        /// <inheritdoc/>
        protected override HashSet<(int x, int y)> BuildFilledPointsCore(int x0, int y0, int width, int height)
        {
            var points = new HashSet<(int x, int y)>();

            // Degenerate cases
            if (width <= 0 && height <= 0)
            {
                points.Add((x0, y0));
                return points;
            }

            if (height == 0)
            {
                // Horizontal line
                for (int x = x0; x <= x0 + width; x++)
                    points.Add((x, y0));
                return points;
            }

            if (width == 0)
            {
                // Vertical line
                for (int y = y0; y <= y0 + height; y++)
                    points.Add((x0, y));
                return points;
            }

            // Midpoint ellipse algorithm with fill
            RasterizeEllipse(x0, y0, x0 + width, y0 + height, filled: true, points);

            return points;
        }

        /// <summary>
        /// Rasterizes an ellipse using the midpoint algorithm.
        /// </summary>
        /// <param name="x0">Left X.</param>
        /// <param name="y0">Top Y.</param>
        /// <param name="x1">Right X.</param>
        /// <param name="y1">Bottom Y.</param>
        /// <param name="filled">True to fill, false for outline only.</param>
        /// <param name="points">Output set to populate with pixel coordinates.</param>
        private static void RasterizeEllipse(int x0, int y0, int x1, int y1, bool filled, HashSet<(int x, int y)> points)
        {
            int a = Math.Abs(x1 - x0);
            int b = Math.Abs(y1 - y0);
            int b1 = b & 1;

            long dx = 4L * (1 - a) * b * b;
            long dy = 4L * (b1 + 1) * a * a;
            long err = dx + dy + b1 * a * a;
            long e2;

            y0 += (b + 1) / 2;
            y1 = y0 - b1;

            long aa8 = 8L * a * a;
            long bb8 = 8L * b * b;

            do
            {
                if (filled)
                {
                    // Fill horizontal spans
                    AddHorizontalSpan(x0, x1, y0, points);
                    if (y0 != y1)
                        AddHorizontalSpan(x0, x1, y1, points);
                }
                else
                {
                    // Outline: add only edge points (4 quadrants)
                    points.Add((x1, y0));
                    points.Add((x0, y0));
                    points.Add((x0, y1));
                    points.Add((x1, y1));
                }

                e2 = 2 * err;
                if (e2 <= dy)
                {
                    y0++;
                    y1--;
                    err += dy += aa8;
                }
                if (e2 >= dx || 2 * err > dy)
                {
                    x0++;
                    x1--;
                    err += dx += bb8;
                }
            }
            while (x0 <= x1);

            // Finish vertical caps
            while ((y0 - y1) <= b)
            {
                if (filled)
                {
                    AddHorizontalSpan(x0 - 1, x1 + 1, y0, points);
                    AddHorizontalSpan(x0 - 1, x1 + 1, y1, points);
                }
                else
                {
                    points.Add((x0 - 1, y0));
                    points.Add((x1 + 1, y0));
                    points.Add((x0 - 1, y1));
                    points.Add((x1 + 1, y1));
                }
                y0++;
                y1--;
            }
        }

        /// <summary>
        /// Adds all points in a horizontal span to the point set.
        /// </summary>
        private static void AddHorizontalSpan(int x0, int x1, int y, HashSet<(int x, int y)> points)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            for (int x = x0; x <= x1; x++)
                points.Add((x, y));
        }
    }
}
