using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using PixlPunkt.Uno.Core.Coloring.Helpers;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Imaging;

namespace PixlPunkt.Uno.Core.Palette.Helpers;

/// <summary>
/// Utilities for extracting unique color palettes from pixel surfaces, documents, and image files.
/// </summary>
/// <remarks>
/// <para>
/// PaletteExtractor analyzes pixel data to build lists of unique colors, useful for palette generation,
/// color quantization analysis, and color-picking UI. Colors are returned as packed BGRA uint values
/// (format: 0xAARRGGBB in memory as B, G, R, A bytes).
/// </para>
/// <para><strong>Configuration Options:</strong></para>
/// <list type="bullet">
/// <item><strong>ignoreAlpha</strong>: When true, treats all pixels as opaque (alpha=255) for uniqueness testing.
/// This merges colors that differ only in transparency, useful for palette extraction where alpha variations
/// should be ignored (e.g., extracting base colors independent of opacity).</item>
/// <item><strong>includeFullyTransparent</strong>: When false, skips pixels with alpha=0 entirely.
/// When true, includes fully transparent colors in the palette. Set false to extract only visible colors.</item>
/// </list>
/// <para><strong>Output Format:</strong></para>
/// <para>
/// All methods return colors sorted ascending by numeric value (0xAARRGGBB) for stable, consistent ordering.
/// This ensures palette lists remain predictable across extractions and makes comparison/diffing reliable.
/// </para>
/// <para><strong>Performance Considerations:</strong></para>
/// <para>
/// Uses <see cref="HashSet{T}"/> for efficient uniqueness tracking. Surface and document extraction use
/// direct byte array access for speed. File extraction uses <see cref="Bitmap.GetPixel"/> for simplicity;
/// for very large images, consider BitmapData locking if performance becomes an issue.
/// </para>
/// </remarks>
/// <seealso cref="ColorUtil"/>
/// <seealso cref="PixelSurface"/>
/// <seealso cref="CanvasDocument"/>
public static class PaletteExtractor
{
    /// <summary>
    /// Extracts unique colors from a <see cref="PixelSurface"/>.
    /// </summary>
    /// <param name="surface">Source pixel surface with BGRA byte layout.</param>
    /// <param name="ignoreAlpha">
    /// If true, treats all pixels as fully opaque (alpha=255) for uniqueness.
    /// This merges colors differing only by alpha channel.
    /// </param>
    /// <param name="includeFullyTransparent">
    /// If false, excludes pixels with alpha=0 from the palette.
    /// If true, includes fully transparent colors.
    /// </param>
    /// <returns>A sorted list of unique BGRA packed colors (uint values).</returns>
    /// <remarks>
    /// Iterates all pixels in the surface, building a HashSet of unique colors.
    /// Results are sorted numerically for consistent ordering.
    /// </remarks>
    public static List<uint> ExtractUniqueColorsFromSurface(
        PixelSurface surface,
        bool ignoreAlpha = true,
        bool includeFullyTransparent = false)
    {
        var pixels = surface.Pixels;
        int w = surface.Width;
        int h = surface.Height;

        var unique = new HashSet<uint>();
        int totalPixels = w * h;

        for (int i = 0; i < totalPixels; i++)
        {
            int idx = i * 4;
            byte b = pixels[idx + 0];
            byte g = pixels[idx + 1];
            byte r = pixels[idx + 2];
            byte a = pixels[idx + 3];

            if (!includeFullyTransparent && a == 0)
                continue;

            uint packed = ignoreAlpha
                ? ColorUtil.PackBGRA(b, g, r, 255)
                : ColorUtil.PackBGRA(b, g, r, a);

            unique.Add(packed);
        }

        return unique.OrderBy(c => c).ToList();
    }

    /// <summary>
    /// Extracts unique colors from all visible layers in a <see cref="CanvasDocument"/>.
    /// </summary>
    /// <param name="document">Source document with one or more layers.</param>
    /// <param name="ignoreAlpha">
    /// If true, treats all pixels as fully opaque (alpha=255) for uniqueness.
    /// This merges colors differing only by alpha channel.
    /// </param>
    /// <param name="includeFullyTransparent">
    /// If false, excludes pixels with alpha=0 from the palette.
    /// If true, includes fully transparent colors.
    /// </param>
    /// <returns>A sorted list of unique BGRA packed colors (uint values) from all layers combined.</returns>
    /// <remarks>
    /// <para>
    /// Processes all layers in the document, accumulating unique colors into a single unified palette.
    /// Layer visibility, blend modes, and effects are ignored - this extracts raw pixel data from
    /// each layer's surface.
    /// </para>
    /// <para>
    /// Useful for analyzing total color usage across a multi-layer document, generating master palettes,
    /// or checking color limits for export to constrained formats.
    /// </para>
    /// </remarks>
    public static List<uint> ExtractUniqueColorsFromDocument(
        CanvasDocument document,
        bool ignoreAlpha = true,
        bool includeFullyTransparent = false)
    {
        var unique = new HashSet<uint>();
        foreach (var layer in document.Layers)
        {
            var pixels = layer.Surface.Pixels;
            int w = layer.Surface.Width;
            int h = layer.Surface.Height;


            int totalPixels = w * h;

            for (int i = 0; i < totalPixels; i++)
            {
                int idx = i * 4;
                byte b = pixels[idx + 0];
                byte g = pixels[idx + 1];
                byte r = pixels[idx + 2];
                byte a = pixels[idx + 3];

                if (!includeFullyTransparent && a == 0)
                    continue;

                uint packed = ignoreAlpha
                    ? ColorUtil.PackBGRA(b, g, r, 255)
                    : ColorUtil.PackBGRA(b, g, r, a);

                unique.Add(packed);
            }
        }
        return unique.OrderBy(c => c).ToList();
    }

    /// <summary>
    /// Extracts unique colors from an image file on disk.
    /// </summary>
    /// <param name="path">Path to an image file (PNG, BMP, JPEG, GIF, etc.) loadable by <see cref="Bitmap"/>.</param>
    /// <param name="ignoreAlpha">
    /// If true, treats all pixels as fully opaque (alpha=255) for uniqueness.
    /// This merges colors differing only by alpha channel.
    /// </param>
    /// <param name="includeFullyTransparent">
    /// If false, excludes pixels with alpha=0 from the palette.
    /// If true, includes fully transparent colors.
    /// </param>
    /// <returns>A sorted list of unique BGRA packed colors (uint values).</returns>
    /// <remarks>
    /// <para><strong>Implementation:</strong></para>
    /// <para>
    /// Uses <see cref="Bitmap.GetPixel"/> for simplicity and compatibility with all GDI+ supported formats.
    /// This approach is sufficient for one-off palette extraction and import operations.
    /// </para>
    /// <para><strong>Performance Note:</strong></para>
    /// <para>
    /// For very large images (megapixels), GetPixel can be slow. If performance becomes critical,
    /// consider using <see cref="Bitmap.LockBits(System.Drawing.Rectangle, System.Drawing.Imaging.ImageLockMode, System.Drawing.Imaging.PixelFormat)"/> with <see cref="System.Drawing.Imaging.BitmapData"/>
    /// for direct memory access (similar to <see cref="ExtractUniqueColorsFromSurface"/>).
    /// </para>
    /// <para><strong>Use Cases:</strong></para>
    /// <para>
    /// - Importing existing artwork and generating matching palettes
    /// <br/>- Analyzing color usage in reference images
    /// <br/>- Building custom palettes from photo sources
    /// <br/>- Color quantization preprocessing
    /// </para>
    /// </remarks>
    public static List<uint> ExtractUniqueColorsFromFile(
        string path,
        bool ignoreAlpha = true,
        bool includeFullyTransparent = false)
    {
        using var bmp = new Bitmap(path);
        var unique = new HashSet<uint>();

        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var c = bmp.GetPixel(x, y);

                if (!includeFullyTransparent && c.A == 0)
                    continue;

                uint packed = ignoreAlpha
                    ? ColorUtil.PackBGRA(c.B, c.G, c.R, 255)
                    : ColorUtil.PackBGRA(c.B, c.G, c.R, c.A);

                unique.Add(packed);
            }
        }

        return unique.OrderBy(c => c).ToList();
    }
}
