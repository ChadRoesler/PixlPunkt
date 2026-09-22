using System;
using System.Collections.Generic;

namespace PixlPunkt.Core.Document
{
    /// <summary>A point on a glyph outline, in font units with the origin on the baseline.</summary>
    public readonly record struct OutlinePoint(int X, int Y);

    /// <summary>
    /// Turns a glyph's pixels into outlines: the boundary of the filled area, following pixel edges
    /// exactly, with no curve fitting of any kind.
    /// </summary>
    /// <remarks>
    /// Fitting curves to a pixel font would be a betrayal of it. Every edge here is a straight line
    /// along a whole pixel boundary, so a stem is exactly as wide as it was drawn and stays that way
    /// at any whole multiple of the em.
    ///
    /// The boundary is found by cancellation rather than by walking pixels: every filled pixel
    /// contributes its four edges, and an edge shared by two filled pixels is traversed once in each
    /// direction and cancels out. What remains is the outline, and holes come out of it with the
    /// opposite winding without needing to be looked for.
    /// </remarks>
    public static class FontOutlineOps
    {
        /// <summary>
        /// How many font units one pixel becomes, chosen so the em lands on a whole number near the
        /// target. Getting this exact is the difference between crisp output and the uneven stems
        /// that come of an em the pixel grid does not divide.
        /// </summary>
        /// <param name="emHeightPixels">The em box height in pixels.</param>
        /// <param name="target">The units per em to aim for; 1024 is the usual choice.</param>
        public static int ChooseUnitsPerPixel(int emHeightPixels, int target = 1024)
        {
            emHeightPixels = Math.Max(1, emHeightPixels);
            int perPixel = Math.Max(1, (int)Math.Round(target / (double)emHeightPixels));

            // Font units are stored as 16-bit values, so the em has to stay well inside that.
            while (emHeightPixels * perPixel > 16384 && perPixel > 1) perPixel--;

            return perPixel;
        }

        /// <summary>Units per em that follows from the pixel grid, always an exact multiple of it.</summary>
        public static int UnitsPerEm(int emHeightPixels, int target = 1024) =>
            Math.Max(1, emHeightPixels) * ChooseUnitsPerPixel(emHeightPixels, target);

        /// <summary>Which pixels of a glyph's cell have any ink, row-major.</summary>
        public static bool[] MaskOfCell(CanvasDocument doc, int cellIndex)
        {
            var cell = FontMetricsOps.GetCellRect(doc, cellIndex);
            var surface = doc.Surface;
            var mask = new bool[cell.Width * cell.Height];

            for (int y = 0; y < cell.Height; y++)
            {
                int sourceY = cell.Y + y;
                if (sourceY < 0 || sourceY >= surface.Height) continue;

                for (int x = 0; x < cell.Width; x++)
                {
                    int sourceX = cell.X + x;
                    if (sourceX < 0 || sourceX >= surface.Width) continue;

                    mask[y * cell.Width + x] = surface.Pixels[(sourceY * surface.Width + sourceX) * 4 + 3] != 0;
                }
            }

            return mask;
        }

        /// <summary>
        /// Traces a pixel mask into closed contours in font units.
        /// </summary>
        /// <param name="mask">Filled pixels, row-major, top row first.</param>
        /// <param name="width">Mask width in pixels.</param>
        /// <param name="height">Mask height in pixels.</param>
        /// <param name="unitsPerPixel">Font units per pixel.</param>
        /// <param name="baselineRow">The pixel row the baseline sits on; it becomes y of zero.</param>
        /// <param name="originColumn">The pixel column the pen sits on; it becomes x of zero.</param>
        public static List<List<OutlinePoint>> Trace(
            bool[] mask,
            int width,
            int height,
            int unitsPerPixel,
            int baselineRow,
            int originColumn)
        {
            var edges = new HashSet<((int X, int Y) From, (int X, int Y) To)>();

            void AddEdge((int X, int Y) from, (int X, int Y) to)
            {
                // The same edge walked the other way means two filled pixels meet here, so the
                // boundary does not run along it at all.
                if (!edges.Remove((to, from))) edges.Add((from, to));
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!mask[y * width + x]) continue;

                    // Clockwise around the pixel, reading the grid with y downward.
                    AddEdge((x, y), (x + 1, y));
                    AddEdge((x + 1, y), (x + 1, y + 1));
                    AddEdge((x + 1, y + 1), (x, y + 1));
                    AddEdge((x, y + 1), (x, y));
                }
            }

            var contours = new List<List<OutlinePoint>>();
            foreach (var loop in ChainIntoLoops(edges))
                contours.Add(ToFontUnits(Simplify(loop), unitsPerPixel, baselineRow, originColumn));

            return contours;
        }

        /// <summary>Traces one character of a font document.</summary>
        public static List<List<OutlinePoint>> TraceGlyph(CanvasDocument doc, int codepoint, int unitsPerPixel)
        {
            if (!doc.FontState.TryGet(codepoint, out var glyph) || glyph.CellIndex < 0)
                return new List<List<OutlinePoint>>();

            var cell = FontMetricsOps.GetCellRect(doc, glyph.CellIndex);
            var (originX, _) = FontMetricsOps.ResolveMetrics(doc, codepoint);

            return Trace(
                MaskOfCell(doc, glyph.CellIndex),
                cell.Width,
                cell.Height,
                unitsPerPixel,
                baselineRow: doc.FontState.BaselineY,
                originColumn: originX);
        }

        /// <summary>
        /// Joins the loose boundary edges into closed loops.
        /// </summary>
        /// <remarks>
        /// Where two pixels meet only at a corner, four edges arrive at one point and the walk has a
        /// choice. It always takes the sharpest right turn, which keeps the two pixels as separate
        /// loops rather than pinching them into one that crosses itself.
        /// </remarks>
        private static List<List<(int X, int Y)>> ChainIntoLoops(
            HashSet<((int X, int Y) From, (int X, int Y) To)> edges)
        {
            var outgoing = new Dictionary<(int X, int Y), List<(int X, int Y)>>();
            foreach (var (from, to) in edges)
            {
                if (!outgoing.TryGetValue(from, out var list))
                    outgoing[from] = list = new List<(int X, int Y)>();
                list.Add(to);
            }

            var loops = new List<List<(int X, int Y)>>();

            while (edges.Count > 0)
            {
                var start = FirstEdge(edges);
                var loop = new List<(int X, int Y)> { start.From };

                var current = start;
                while (true)
                {
                    edges.Remove(current);
                    outgoing[current.From].Remove(current.To);

                    if (current.To == start.From) break;

                    loop.Add(current.To);

                    var next = PickNext(outgoing, current);
                    if (next is not { } chosen) break;   // open chain: cannot happen, but never loop forever
                    current = (current.To, chosen);
                }

                if (loop.Count >= 3) loops.Add(loop);
            }

            return loops;
        }

        private static ((int X, int Y) From, (int X, int Y) To) FirstEdge(
            HashSet<((int X, int Y) From, (int X, int Y) To)> edges)
        {
            foreach (var edge in edges) return edge;
            throw new InvalidOperationException("No edges left.");
        }

        /// <summary>The next edge to walk: sharpest right turn first, then straight on, then left.</summary>
        private static (int X, int Y)? PickNext(
            Dictionary<(int X, int Y), List<(int X, int Y)>> outgoing,
            ((int X, int Y) From, (int X, int Y) To) current)
        {
            if (!outgoing.TryGetValue(current.To, out var candidates) || candidates.Count == 0)
                return null;

            var direction = (X: current.To.X - current.From.X, Y: current.To.Y - current.From.Y);

            // Reading the grid with y downward, a right turn rotates (dx, dy) to (-dy, dx).
            var right = (X: -direction.Y, Y: direction.X);
            var left = (X: direction.Y, Y: -direction.X);

            foreach (var preferred in new[] { right, direction, left })
            {
                var wanted = (X: current.To.X + preferred.X, Y: current.To.Y + preferred.Y);
                if (candidates.Contains(wanted)) return wanted;
            }

            return candidates[0];
        }

        /// <summary>Drops points that only continue a straight run, which most of them do.</summary>
        private static List<(int X, int Y)> Simplify(List<(int X, int Y)> loop)
        {
            if (loop.Count < 3) return loop;

            var result = new List<(int X, int Y)>(loop.Count);

            for (int i = 0; i < loop.Count; i++)
            {
                var previous = loop[(i - 1 + loop.Count) % loop.Count];
                var point = loop[i];
                var next = loop[(i + 1) % loop.Count];

                int ax = point.X - previous.X, ay = point.Y - previous.Y;
                int bx = next.X - point.X, by = next.Y - point.Y;

                // Cross product of nothing means the three are in line, so the middle one says nothing.
                if (ax * by - ay * bx != 0) result.Add(point);
            }

            return result.Count >= 3 ? result : loop;
        }

        /// <summary>
        /// Moves a loop into font units, y upward from the baseline.
        /// </summary>
        /// <remarks>
        /// The order is left alone. Each pixel is walked clockwise while reading the grid downward,
        /// and turning y upward reverses that on its own, which lands on the clockwise outer winding
        /// TrueType fills. Reversing here as well would put every contour back the wrong way round,
        /// and a font whose outers are wound like holes renders as nothing at all.
        /// </remarks>
        private static List<OutlinePoint> ToFontUnits(
            List<(int X, int Y)> loop, int unitsPerPixel, int baselineRow, int originColumn)
        {
            var points = new List<OutlinePoint>(loop.Count);

            foreach (var (x, y) in loop)
            {
                points.Add(new OutlinePoint(
                    (x - originColumn) * unitsPerPixel,
                    (baselineRow - y) * unitsPerPixel));
            }

            return points;
        }

        /// <summary>
        /// Twice the signed area of a contour. Negative is clockwise with y upward, which is what an
        /// outer contour should be; a hole comes back positive.
        /// </summary>
        public static long SignedArea(IReadOnlyList<OutlinePoint> contour)
        {
            long total = 0;
            for (int i = 0; i < contour.Count; i++)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Count];
                total += (long)a.X * b.Y - (long)b.X * a.Y;
            }
            return total;
        }
    }
}
