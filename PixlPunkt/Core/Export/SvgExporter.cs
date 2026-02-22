using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.Export
{
    /// <summary>
    /// SVG export mode for pixel art conversion.
    /// </summary>
    /// <remarks>
    /// Inspired by GLORP's pixel-to-SVG methodology.
    /// See <see href="https://github.com/ZackGphom/GLORP"/> for the original Python implementation.
    /// </remarks>
    public enum SvgExportMode
    {
        /// <summary>
        /// Greedy meshing: merges adjacent same-color pixels into larger rectangles,
        /// producing compact <c>&lt;path&gt;</c> elements grouped by color.
        /// Significantly reduces SVG element count and file size.
        /// Recommended for most use cases and ideal for editing in vector software.
        /// </summary>
        Monolith,

        /// <summary>
        /// Each pixel becomes an individual 1×1 <c>&lt;rect&gt;</c>.
        /// Produces an exact pixel representation but with a large element count.
        /// May reduce performance in vector editors for large images.
        /// </summary>
        Block
    }

    /// <summary>
    /// Exports pixel art (BGRA byte arrays) to optimized SVG using greedy meshing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides two export modes:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <strong>Monolith</strong> — Groups pixels by color, then applies a greedy rectangle-merging
    /// algorithm to combine adjacent same-color pixels into the largest possible rectangles.
    /// All rectangles of the same color are emitted as a single SVG <c>&lt;path&gt;</c> with
    /// multiple sub-paths, dramatically reducing the element count.
    /// </item>
    /// <item>
    /// <strong>Block</strong> — Each visible pixel becomes an individual <c>&lt;rect&gt;</c>.
    /// Simple and accurate, but produces very large files for images above ~64×64.
    /// </item>
    /// </list>
    /// <para>
    /// Both modes use <c>shape-rendering="crispEdges"</c> to ensure browsers render
    /// pixel-perfect edges without anti-aliasing.
    /// </para>
    /// </remarks>
    public static class SvgExporter
    {
        /// <summary>
        /// Maximum pixel count safety cap (width × height).
        /// Images exceeding this limit are rejected to prevent runaway SVG generation.
        /// </summary>
        private const int MaxPixelCount = 1_000_000;

        /// <summary>
        /// Exports BGRA pixel data to an SVG string.
        /// </summary>
        /// <param name="pixels">BGRA byte array (4 bytes per pixel, row-major order).</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="mode">Export mode (Monolith or Block).</param>
        /// <param name="scale">Pixel scale factor (1 = native resolution). Each pixel
        /// becomes <paramref name="scale"/>×<paramref name="scale"/> SVG units.</param>
        /// <returns>Complete SVG document as a UTF-8 string.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if pixel data is null, too short, or the image exceeds the safety cap.
        /// </exception>
        public static string Export(byte[] pixels, int width, int height,
            SvgExportMode mode = SvgExportMode.Monolith, int scale = 1)
        {
            if (pixels == null || pixels.Length < width * height * 4)
                throw new ArgumentException("Invalid pixel data.");

            if ((long)width * height > MaxPixelCount)
                throw new ArgumentException(
                    $"Image exceeds {MaxPixelCount:N0} pixel safety cap " +
                    $"({width}×{height} = {(long)width * height:N0}).");

            scale = Math.Max(1, scale);

            LoggingService.Info(
                "SVG export start mode={Mode} size={W}x{H} scale={Scale}",
                mode.ToString(), width, height, scale);

            string result = mode switch
            {
                SvgExportMode.Monolith => ExportMonolith(pixels, width, height, scale),
                SvgExportMode.Block => ExportBlock(pixels, width, height, scale),
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };

            LoggingService.Info(
                "SVG export complete mode={Mode} length={Len}",
                mode.ToString(), result.Length);

            return result;
        }

        /// <summary>
        /// Exports BGRA pixel data to SVG and writes it to a file.
        /// </summary>
        /// <param name="pixels">BGRA byte array.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="outputPath">Output file path.</param>
        /// <param name="mode">Export mode.</param>
        /// <param name="scale">Pixel scale factor.</param>
        public static void ExportToFile(byte[] pixels, int width, int height,
            string outputPath, SvgExportMode mode = SvgExportMode.Monolith, int scale = 1)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var svg = Export(pixels, width, height, mode, scale);
            File.WriteAllText(outputPath, svg, Encoding.UTF8);
        }

        /// <summary>
        /// Exports a <see cref="PixelSurface"/> to SVG and writes it to a file.
        /// </summary>
        /// <param name="surface">The pixel surface to export.</param>
        /// <param name="outputPath">Output file path.</param>
        /// <param name="mode">Export mode.</param>
        /// <param name="scale">Pixel scale factor.</param>
        public static void ExportToFile(PixelSurface surface, string outputPath,
            SvgExportMode mode = SvgExportMode.Monolith, int scale = 1)
        {
            ExportToFile(surface.Pixels, surface.Width, surface.Height, outputPath, mode, scale);
        }

        /// <summary>
        /// Exports BGRA pixel data to SVG as a UTF-8 byte array.
        /// </summary>
        public static byte[] ExportToBytes(byte[] pixels, int width, int height,
            SvgExportMode mode = SvgExportMode.Monolith, int scale = 1)
        {
            return Encoding.UTF8.GetBytes(Export(pixels, width, height, mode, scale));
        }

        // ════════════════════════════════════════════════════════════════════
        // MONOLITH MODE — Greedy Meshing
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Exports using greedy meshing: groups pixels by color, merges adjacent
        /// same-color pixels into larger rectangles, emits one <c>&lt;path&gt;</c> per color.
        /// </summary>
        private static string ExportMonolith(byte[] pixels, int width, int height, int scale)
        {
            // Step 1: Group pixel positions by ARGB color value
            var colorGroups = new Dictionary<uint, List<(int x, int y)>>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte a = pixels[i + 3];

                    if (a == 0) continue;

                    // Pack as ARGB for grouping
                    uint color = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;

                    if (!colorGroups.TryGetValue(color, out var list))
                    {
                        list = [];
                        colorGroups[color] = list;
                    }

                    list.Add((x, y));
                }
            }

            // Step 2: For each color group, run greedy meshing and emit SVG
            var sb = new StringBuilder(colorGroups.Count * 128);
            WriteSvgHeader(sb, width * scale, height * scale);

            foreach (var (color, positions) in colorGroups)
            {
                var rects = GreedyMesh(positions, width, height);
                WriteColorPath(sb, color, rects, scale);
            }

            WriteSvgFooter(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Greedy meshing algorithm: merges adjacent same-color pixels into the
        /// largest possible axis-aligned rectangles.
        /// </summary>
        /// <remarks>
        /// <para><strong>Algorithm:</strong></para>
        /// <para>
        /// Scans through pixel positions in their natural (row-major) order.
        /// For each unvisited pixel:
        /// </para>
        /// <list type="number">
        /// <item>Expand right as far as possible while the next pixel is present and unvisited</item>
        /// <item>Expand down row-by-row as long as the <em>entire</em> row width matches</item>
        /// <item>Mark all covered pixels as visited</item>
        /// </list>
        /// <para>
        /// This produces a near-optimal rectangle covering with O(width × height) complexity per color.
        /// </para>
        /// </remarks>
        private static List<(int x, int y, int w, int h)> GreedyMesh(
            List<(int x, int y)> positions, int width, int height)
        {
            // Build a fast lookup grid for this color
            var grid = new bool[width * height];
            foreach (var (x, y) in positions)
                grid[y * width + x] = true;

            var visited = new bool[width * height];
            var rects = new List<(int x, int y, int w, int h)>();

            foreach (var (px, py) in positions)
            {
                int idx = py * width + px;
                if (visited[idx]) continue;

                // Expand right
                int rectW = 1;
                while (px + rectW < width)
                {
                    int ni = py * width + (px + rectW);
                    if (!grid[ni] || visited[ni]) break;
                    rectW++;
                }

                // Expand down — each new row must have the full width
                int rectH = 1;
                while (py + rectH < height)
                {
                    bool rowOk = true;
                    for (int dx = 0; dx < rectW; dx++)
                    {
                        int ni = (py + rectH) * width + (px + dx);
                        if (!grid[ni] || visited[ni])
                        {
                            rowOk = false;
                            break;
                        }
                    }
                    if (!rowOk) break;
                    rectH++;
                }

                // Mark all pixels in this rectangle as visited
                for (int dy = 0; dy < rectH; dy++)
                {
                    for (int dx = 0; dx < rectW; dx++)
                    {
                        visited[(py + dy) * width + (px + dx)] = true;
                    }
                }

                rects.Add((px, py, rectW, rectH));
            }

            return rects;
        }

        /// <summary>
        /// Writes all rectangles for a single color as one combined SVG <c>&lt;path&gt;</c>.
        /// Each rectangle becomes an <c>M x y h w v h H x Z</c> sub-path.
        /// </summary>
        private static void WriteColorPath(
            StringBuilder sb,
            uint color,
            List<(int x, int y, int w, int h)> rects,
            int scale)
        {
            if (rects.Count == 0) return;

            byte a = (byte)(color >> 24);
            byte r = (byte)(color >> 16);
            byte g = (byte)(color >> 8);
            byte b = (byte)color;

            string fill = $"#{r:x2}{g:x2}{b:x2}";
            string opacity = a < 255
                ? string.Create(CultureInfo.InvariantCulture, $" opacity=\"{a / 255.0:F3}\"")
                : "";

            // Build combined path data — all rects share one <path> element
            var pathData = new StringBuilder(rects.Count * 24);
            foreach (var (x, y, w, h) in rects)
            {
                int sx = x * scale;
                int sy = y * scale;
                int sw = w * scale;
                int sh = h * scale;

                // M=moveto, h=horizontal(relative), v=vertical(relative), Z=close
                pathData.Append(CultureInfo.InvariantCulture, $"M{sx} {sy}h{sw}v{sh}h{-sw}Z");
            }

            sb.Append($"  <path fill=\"{fill}\"{opacity} d=\"{pathData}\"/>\n");
        }

        // ════════════════════════════════════════════════════════════════════
        // Block MODE — Per-Pixel Rects
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Exports with one <c>&lt;rect&gt;</c> per visible pixel.
        /// Simple and exact, but produces large SVG files.
        /// </summary>
        private static string ExportBlock(byte[] pixels, int width, int height, int scale)
        {
            var sb = new StringBuilder(width * height * 40); // rough estimate
            WriteSvgHeader(sb, width * scale, height * scale);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte a = pixels[i + 3];

                    if (a == 0) continue;

                    string fill = $"#{r:x2}{g:x2}{b:x2}";
                    string opacity = a < 255
                        ? string.Create(CultureInfo.InvariantCulture, $" opacity=\"{a / 255.0:F3}\"")
                        : "";

                    sb.Append($"  <rect x=\"{x * scale}\" y=\"{y * scale}\" " +
                              $"width=\"{scale}\" height=\"{scale}\" " +
                              $"fill=\"{fill}\"{opacity}/>\n");
                }
            }

            WriteSvgFooter(sb);
            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        // SVG DOCUMENT STRUCTURE
        // ════════════════════════════════════════════════════════════════════

        private static void WriteSvgHeader(StringBuilder sb, int width, int height)
        {
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" ");
            sb.Append($"viewBox=\"0 0 {width} {height}\" ");
            sb.Append($"width=\"{width}\" height=\"{height}\" ");
            sb.Append("shape-rendering=\"crispEdges\">\n");
        }

        private static void WriteSvgFooter(StringBuilder sb)
        {
            sb.Append("</svg>\n");
        }
    }
}
