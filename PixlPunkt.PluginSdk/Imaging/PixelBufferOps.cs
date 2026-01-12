namespace PixlPunkt.PluginSdk.Imaging;

/// <summary>
/// Provides low-level pixel buffer operations for resizing, comparison, and cropping.
/// </summary>
/// <remarks>
/// <para>
/// PixelBufferOps contains platform-agnostic algorithms for common pixel buffer manipulations.
/// All methods operate on raw BGRA byte arrays for maximum flexibility and performance.
/// </para>
/// <para>
/// <strong>Color Format:</strong> All methods use BGRA byte order:
/// <br/>- byte[]: [B, G, R, A, B, G, R, A, ...] (4 bytes per pixel)
/// </para>
/// </remarks>
public static class PixelBufferOps
{
    /// <summary>
    /// Resizes an image using nearest-neighbor sampling.
    /// </summary>
    /// <param name="src">Source buffer (BGRA byte array).</param>
    /// <param name="srcWidth">Source width in pixels.</param>
    /// <param name="srcHeight">Source height in pixels.</param>
    /// <param name="dstWidth">Destination width in pixels.</param>
    /// <param name="dstHeight">Destination height in pixels.</param>
    /// <returns>Resized buffer as byte array, or empty array if dimensions are invalid.</returns>
    /// <remarks>
    /// <para>
    /// Fast integer-based scaling that preserves hard edges (no antialiasing). Each output pixel
    /// samples the nearest source pixel. Ideal for pixel art and when crisp edges are desired.
    /// </para>
    /// <para>
    /// Includes safety checks for invalid dimensions and buffer underruns.
    /// </para>
    /// </remarks>
    public static byte[] ResizeNearest(byte[] src, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
    {
        if (srcWidth <= 0 || srcHeight <= 0 || dstWidth <= 0 || dstHeight <= 0)
            return [];

        int needed = srcWidth * srcHeight * 4;
        if (src.Length < needed)
        {
            int px = src.Length / 4;
            if (px == 0) return new byte[dstWidth * dstHeight * 4];
            if (srcWidth > px) srcWidth = px;
            srcHeight = Math.Max(1, px / Math.Max(1, srcWidth));
        }

        var dst = new byte[dstWidth * dstHeight * 4];
        double sx = (double)srcWidth / dstWidth;
        double sy = (double)srcHeight / dstHeight;

        for (int y = 0; y < dstHeight; y++)
        {
            int syi = Math.Min(srcHeight - 1, (int)(y * sy));
            int srow = syi * srcWidth * 4;
            int diRow = y * dstWidth * 4;

            for (int x = 0; x < dstWidth; x++)
            {
                int sxi = Math.Min(srcWidth - 1, (int)(x * sx));
                int si = srow + sxi * 4;
                int di = diRow + x * 4;

                if (si + 3 >= src.Length) continue;

                dst[di + 0] = src[si + 0];
                dst[di + 1] = src[si + 1];
                dst[di + 2] = src[si + 2];
                dst[di + 3] = src[si + 3];
            }
        }
        return dst;
    }

    /// <summary>
    /// Resizes an image using bilinear interpolation for smooth scaling.
    /// </summary>
    /// <param name="src">Source buffer (BGRA byte array).</param>
    /// <param name="srcWidth">Source width in pixels.</param>
    /// <param name="srcHeight">Source height in pixels.</param>
    /// <param name="dstWidth">Destination width in pixels.</param>
    /// <param name="dstHeight">Destination height in pixels.</param>
    /// <returns>Resized buffer as byte array.</returns>
    /// <remarks>
    /// <para>
    /// Samples a 2×2 grid of source pixels for each output pixel with bilinear weighting,
    /// producing anti-aliased results. Higher quality than nearest-neighbor but slower.
    /// </para>
    /// <para>
    /// Recommended for downscaling and general-purpose image resizing where smoothness is preferred.
    /// </para>
    /// </remarks>
    public static byte[] ResizeBilinear(byte[] src, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
    {
        if (srcWidth <= 0 || srcHeight <= 0 || dstWidth <= 0 || dstHeight <= 0)
            return new byte[Math.Max(0, dstWidth) * Math.Max(0, dstHeight) * 4];

        var dst = new byte[dstWidth * dstHeight * 4];
        double gx = (double)(srcWidth - 1) / (dstWidth - 1 == 0 ? 1 : dstWidth - 1);
        double gy = (double)(srcHeight - 1) / (dstHeight - 1 == 0 ? 1 : dstHeight - 1);

        int di = 0;
        for (int y = 0; y < dstHeight; y++)
        {
            double syf = y * gy;
            int y0 = (int)Math.Floor(syf);
            int y1 = Math.Min(srcHeight - 1, y0 + 1);
            double fy = syf - y0;

            for (int x = 0; x < dstWidth; x++, di += 4)
            {
                double sxf = x * gx;
                int x0 = (int)Math.Floor(sxf);
                int x1 = Math.Min(srcWidth - 1, x0 + 1);
                double fx = sxf - x0;

                int i00 = (y0 * srcWidth + x0) * 4;
                int i10 = (y0 * srcWidth + x1) * 4;
                int i01 = (y1 * srcWidth + x0) * 4;
                int i11 = (y1 * srcWidth + x1) * 4;

                for (int c = 0; c < 4; c++)
                {
                    double v =
                        src[i00 + c] * (1 - fx) * (1 - fy) +
                        src[i10 + c] * fx * (1 - fy) +
                        src[i01 + c] * (1 - fx) * fy +
                        src[i11 + c] * fx * fy;
                    dst[di + c] = (byte)Math.Round(v);
                }
            }
        }
        return dst;
    }

    /// <summary>
    /// Tests if two pixels in a buffer are similar within a color tolerance.
    /// </summary>
    /// <param name="pixels">Pixel buffer (BGRA byte array).</param>
    /// <param name="indexA">Byte index of first pixel (must be aligned to 4-byte boundary).</param>
    /// <param name="indexB">Byte index of second pixel (must be aligned to 4-byte boundary).</param>
    /// <param name="tolerance">Color difference tolerance (0-255). Higher values match more colors.</param>
    /// <param name="includeAlpha">If true, include alpha channel in comparison.</param>
    /// <returns><c>true</c> if pixels are similar within tolerance; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// <para>
    /// Uses squared Euclidean distance for efficiency (avoids sqrt). The comparison formula is:
    /// <code>
    /// dist² = (R1-R2)² + (G1-G2)² + (B1-B2)² [+ (A1-A2)²]
    /// similar = dist² ? tolerance² × 3
    /// </code>
    /// </para>
    /// <para>
    /// This is useful for flood-fill, magic wand, and color replacement tools.
    /// </para>
    /// </remarks>
    public static bool PixelsSimilar(byte[] pixels, int indexA, int indexB, int tolerance, bool includeAlpha = false)
    {
        int db = pixels[indexA] - pixels[indexB];
        int dg = pixels[indexA + 1] - pixels[indexB + 1];
        int dr = pixels[indexA + 2] - pixels[indexB + 2];
        int da = includeAlpha ? pixels[indexA + 3] - pixels[indexB + 3] : 0;

        int dist = db * db + dg * dg + dr * dr + da * da;
        return dist <= tolerance * tolerance * 3;
    }

    /// <summary>
    /// Tests if two BGRA colors are similar within a color tolerance.
    /// </summary>
    /// <param name="colorA">First color in BGRA format (0xAARRGGBB).</param>
    /// <param name="colorB">Second color in BGRA format (0xAARRGGBB).</param>
    /// <param name="tolerance">Color difference tolerance (0-255). Higher values match more colors.</param>
    /// <param name="includeAlpha">If true, include alpha channel in comparison.</param>
    /// <returns><c>true</c> if colors are similar within tolerance; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Convenience overload that works with packed BGRA colors instead of buffer indices.
    /// </remarks>
    public static bool ColorsSimilar(uint colorA, uint colorB, int tolerance, bool includeAlpha = false)
    {
        int bA = (int)(colorA & 0xFF);
        int gA = (int)((colorA >> 8) & 0xFF);
        int rA = (int)((colorA >> 16) & 0xFF);
        int aA = (int)((colorA >> 24) & 0xFF);

        int bB = (int)(colorB & 0xFF);
        int gB = (int)((colorB >> 8) & 0xFF);
        int rB = (int)((colorB >> 16) & 0xFF);
        int aB = (int)((colorB >> 24) & 0xFF);

        int db = bA - bB;
        int dg = gA - gB;
        int dr = rA - rB;
        int da = includeAlpha ? aA - aB : 0;

        int dist = db * db + dg * dg + dr * dr + da * da;
        return dist <= tolerance * tolerance * 3;
    }

    /// <summary>
    /// Crops a buffer to its minimal bounding box by removing fully transparent edges.
    /// </summary>
    /// <param name="src">Source buffer (BGRA byte array).</param>
    /// <param name="srcWidth">Source width in pixels.</param>
    /// <param name="srcHeight">Source height in pixels.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item><c>buffer</c>: The cropped pixel buffer</item>
    /// <item><c>width</c>: Width of the cropped buffer</item>
    /// <item><c>height</c>: Height of the cropped buffer</item>
    /// <item><c>offsetX</c>: X offset from original top-left to cropped top-left</item>
    /// <item><c>offsetY</c>: Y offset from original top-left to cropped top-left</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// Scans the image to find the bounding box of all non-transparent pixels (alpha > 0),
    /// then extracts only that region. Useful for optimizing sprite storage and export.
    /// </para>
    /// <para>
    /// If the image is fully transparent, returns a minimal 1×1 transparent buffer with offset (0, 0).
    /// </para>
    /// </remarks>
    public static (byte[] buffer, int width, int height, int offsetX, int offsetY) CropToOpaque(
        byte[] src, int srcWidth, int srcHeight)
    {
        int minX = srcWidth, maxX = -1, minY = srcHeight, maxY = -1;

        // Find bounding box of non-transparent pixels
        for (int y = 0; y < srcHeight; y++)
        {
            for (int x = 0; x < srcWidth; x++)
            {
                int idx = (y * srcWidth + x) * 4 + 3; // alpha channel
                if (src[idx] > 0)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        // Handle fully transparent image
        if (maxX < 0)
            return (new byte[4], 1, 1, 0, 0);

        int cropW = maxX - minX + 1;
        int cropH = maxY - minY + 1;
        var dst = new byte[cropW * cropH * 4];

        for (int y = 0; y < cropH; y++)
        {
            int srcRow = (minY + y) * srcWidth * 4 + minX * 4;
            int dstRow = y * cropW * 4;
            Buffer.BlockCopy(src, srcRow, dst, dstRow, cropW * 4);
        }

        return (dst, cropW, cropH, minX, minY);
    }

    /// <summary>
    /// Creates a copy of a rectangular region from a pixel buffer.
    /// </summary>
    /// <param name="src">Source buffer (BGRA byte array).</param>
    /// <param name="srcWidth">Source buffer width in pixels.</param>
    /// <param name="srcHeight">Source buffer height in pixels.</param>
    /// <param name="x">X coordinate of the region's top-left corner.</param>
    /// <param name="y">Y coordinate of the region's top-left corner.</param>
    /// <param name="width">Width of the region to copy.</param>
    /// <param name="height">Height of the region to copy.</param>
    /// <returns>A new buffer containing the copied region, or empty array if region is invalid.</returns>
    /// <remarks>
    /// The region is clipped to source bounds. If the resulting region has zero area, returns an empty array.
    /// </remarks>
    public static byte[] CopyRegion(byte[] src, int srcWidth, int srcHeight, int x, int y, int width, int height)
    {
        int x0 = Math.Clamp(x, 0, srcWidth);
        int y0 = Math.Clamp(y, 0, srcHeight);
        int x1 = Math.Clamp(x + width, 0, srcWidth);
        int y1 = Math.Clamp(y + height, 0, srcHeight);
        int rw = Math.Max(0, x1 - x0);
        int rh = Math.Max(0, y1 - y0);

        if (rw == 0 || rh == 0)
            return [];

        var dst = new byte[rw * rh * 4];
        int srcStride = srcWidth * 4;
        int dstStride = rw * 4;

        for (int row = 0; row < rh; row++)
        {
            Buffer.BlockCopy(src, (y0 + row) * srcStride + x0 * 4, dst, row * dstStride, dstStride);
        }

        return dst;
    }
}
