using System;
using Windows.Graphics;

namespace PixlPunkt.Core.Imaging
{
    /// <summary>
    /// Provides low-level pixel buffer operations including rotation, scaling, upsampling, and compositing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PixelOps contains performance-critical image transformation algorithms used for selection transforms,
    /// layer effects, and export operations. All methods operate on raw pixel buffers (byte[] or uint[])
    /// in BGRA format for maximum efficiency.
    /// </para>
    /// <para>
    /// <strong>Key Functionality:</strong>
    /// - **Rotation**: Nearest-neighbor and bilinear rotation with automatic bounds expansion
    /// - **Scaling**: Nearest-neighbor, bilinear, EPX (edge-preserving), Scale2x pixel art scaling
    /// - **Hybrid Scaling**: Multi-stage upsampling (EPX → rotate → downsample) for pixel-art rotation
    /// - **Blitting**: Fast buffer-to-buffer copying with clipping
    /// - **Utility**: Rectangle extraction, clearing, similarity testing
    /// </para>
    /// <para>
    /// <strong>Color Format:</strong> All methods use BGRA byte order unless otherwise specified:
    /// - byte[]: [B, G, R, A, B, G, R, A, ...] (4 bytes per pixel)
    /// - uint[]: packed BGRA as 32-bit integers
    /// </para>
    /// <para>
    /// <strong>Performance Notes:</strong>
    /// - Nearest-neighbor methods are fastest for real-time preview
    /// - Bilinear methods produce smoother results for high-quality output
    /// - EPX/Scale2x are specialized for pixel art (preserve hard edges while interpolating)
    /// </para>
    /// </remarks>
    public static class PixelOps
    {
        /// <summary>
        /// Extracts a rectangular region from a source buffer.
        /// </summary>
        /// <param name="src">Source buffer (packed BGRA uint array).</param>
        /// <param name="w">Source buffer width in pixels.</param>
        /// <param name="h">Source buffer height in pixels.</param>
        /// <param name="r">Rectangle to extract (clamped to source bounds).</param>
        /// <returns>New buffer containing only the specified rectangle's pixels.</returns>
        /// <remarks>
        /// Out-of-bounds regions are clamped to source dimensions. If the resulting rectangle is
        /// empty (zero width or height), returns an empty array.
        /// </remarks>
        public static uint[] CopyRect(uint[] src, int w, int h, RectInt32 r)
        {
            var x0 = Math.Clamp(r.X, 0, w);
            var y0 = Math.Clamp(r.Y, 0, h);
            var x1 = Math.Clamp(r.X + r.Width, 0, w);
            var y1 = Math.Clamp(r.Y + r.Height, 0, h);
            int rw = Math.Max(0, x1 - x0), rh = Math.Max(0, y1 - y0);

            var dst = new uint[rw * rh];
            if (rw == 0 || rh == 0) return dst;

            for (int y = 0; y < rh; y++)
                Array.Copy(src, (y0 + y) * w + x0, dst, y * rw, rw);
            return dst;
        }

        /// <summary>
        /// Blits (copies) a buffer into a destination buffer at the specified position with optional destination clearing.
        /// </summary>
        /// <param name="dst">Destination buffer (packed BGRA uint array).</param>
        /// <param name="w">Destination width in pixels.</param>
        /// <param name="h">Destination height in pixels.</param>
        /// <param name="dx">Destination X offset.</param>
        /// <param name="dy">Destination Y offset.</param>
        /// <param name="buf">Source buffer to copy.</param>
        /// <param name="bw">Source buffer width.</param>
        /// <param name="bh">Source buffer height.</param>
        /// <param name="eraseDst">If true, clears destination pixels before copying (for transparent background).</param>
        /// <remarks>
        /// <para>
        /// Performs clipped copying where only the overlapping region is transferred. No blending
        /// is performed - source pixels directly replace destination pixels (or transparent if eraseDst=true).
        /// </para>
        /// <para>
        /// Used for stamping floating selections, pasting clipboard contents, and layer composition.
        /// </para>
        /// </remarks>
        public static void Blit(uint[] dst, int w, int h, int dx, int dy, uint[] buf, int bw, int bh, bool eraseDst = false)
        {
            int x0 = Math.Max(0, dx), y0 = Math.Max(0, dy);
            int x1 = Math.Min(w, dx + bw), y1 = Math.Min(h, dy + bh);
            if (x1 <= x0 || y1 <= y0) return;

            for (int y = y0; y < y1; y++)
            {
                int sy = y - dy;
                int dRow = y * w + x0;
                int sRow = sy * bw + (x0 - dx);

                if (eraseDst)
                    Array.Fill(dst, 0u, y * w + x0, x1 - x0);

                Array.Copy(buf, sRow, dst, dRow, x1 - x0);
            }
        }


        /// <summary>
        /// Rotates a byte buffer using nearest-neighbor sampling.
        /// </summary>
        /// <param name="src">Source buffer (BGRA byte array).</param>
        /// <param name="sw">Source width in pixels.</param>
        /// <param name="sh">Source height in pixels.</param>
        /// <param name="angleDeg">Rotation angle in degrees (clockwise positive).</param>
        /// <returns>Tuple of (rotated buffer, new width, new height).</returns>
        /// <remarks>
        /// <para>
        /// The output buffer is sized to fit the entire rotated image without clipping (axis-aligned
        /// bounding box). Rotation is performed around the image center with nearest-neighbor sampling.
        /// </para>
        /// <para>
        /// Pixels outside the source region after rotation are filled with fully transparent (0,0,0,0).
        /// This method is fast but may produce jagged edges on diagonals.
        /// </para>
        /// </remarks>
        public static (byte[] buf, int w, int h) RotateNearest(byte[] src, int sw, int sh, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            RotatedBounds(sw, sh, rad, out int dw, out int dh);

            var dst = new byte[dw * dh * 4];
            // Pre-fill with fully transparent (alpha=0) - this is correct for transparency
            // No need to set RGB values since alpha=0 means the color doesn't matter

            double cxS = (sw - 1) * 0.5, cyS = (sh - 1) * 0.5;
            double cxD = (dw - 1) * 0.5, cyD = (dh - 1) * 0.5;

            double cos = Math.Cos(-rad), sin = Math.Sin(-rad); // inverse map

            for (int y = 0; y < dh; y++)
            {
                double dy = y - cyD;
                int diRow = y * dw * 4;
                for (int x = 0; x < dw; x++)
                {
                    double dx = x - cxD;

                    double sx = dx * cos - dy * sin + cxS;
                    double sy = dx * sin + dy * cos + cyS;

                    int sxi = (int)Math.Round(sx);
                    int syi = (int)Math.Round(sy);

                    int di = diRow + x * 4;

                    if (sxi >= 0 && sxi < sw && syi >= 0 && syi < sh)
                    {
                        int si = (syi * sw + sxi) * 4;
                        dst[di + 0] = src[si + 0];
                        dst[di + 1] = src[si + 1];
                        dst[di + 2] = src[si + 2];
                        dst[di + 3] = src[si + 3];
                    }
                    // else: leave as transparent (0,0,0,0) - already initialized
                }
            }
            return (dst, dw, dh);
        }

        /// <summary>
        /// Rotates a pixel-art image using a hybrid RotSprite-inspired algorithm.
        /// </summary>
        /// <param name="src">Source buffer (BGRA byte array).</param>
        /// <param name="sw">Source width in pixels.</param>
        /// <param name="sh">Source height in pixels.</param>
        /// <param name="angleDeg">Rotation angle in degrees.</param>
        /// <returns>Tuple of (rotated buffer, new width, new height).</returns>
        /// <remarks>
        /// <para>
        /// Multi-stage algorithm designed to preserve sharp pixel-art edges during rotation:
        /// 1. **Upsample 2× with EPX** (edge-preserving scale) to add detail at corners
        /// 2. **Rotate with bilinear filtering** for smooth diagonals at higher resolution
        /// 3. **Unpremultiply alpha** to convert back to straight alpha format
        /// 4. **Downsample with nearest-neighbor** to restore crisp pixel boundaries
        /// </para>
        /// <para>
        /// This approximates the RotSprite algorithm's edge-detection and sub-pixel positioning,
        /// producing significantly better results than direct nearest-neighbor rotation for pixel art.
        /// </para>
        /// <para>
        /// **Performance**: ~3× slower than nearest-neighbor, but visually superior for sprites and tiles.
        /// </para>
        /// </remarks>
        public static (byte[] buf, int w, int h) RotateSpriteApprox(byte[] src, int sw, int sh, double angleDeg)
        {
            // Up 2x with EPX to preserve corners
            var (buf, w, h) = PixelOps.EPX2x(src, sw, sh);

            // Rotate with bilinear (outputs premultiplied alpha for smooth edges)
            var rot = RotateBilinear(buf, w, h, angleDeg);

            // Unpremultiply the rotated buffer before downsampling
            // This converts premultiplied alpha back to straight alpha
            UnpremultiplyAlpha(rot.buf, rot.w, rot.h);

            // Downsample back close to original scale with NN to keep pixels crisp
            int targetW = Math.Max(1, (int)Math.Round(sw * (double)rot.w / w));
            int targetH = Math.Max(1, (int)Math.Round(sh * (double)rot.h / h));
            var dn = ResizeNearest(rot.buf, rot.w, rot.h, targetW, targetH);

            return (dn, targetW, targetH);
        }
        /// <summary>
        /// Crops a buffer to its minimal bounding box (removes transparent edges).
        /// Returns the cropped buffer and new dimensions, plus the offset from original top-left.
        /// </summary>
        public static (byte[] buf, int w, int h, int offsetX, int offsetY) CropToOpaque(byte[] src, int sw, int sh)
        {
            int minX = sw, maxX = -1, minY = sh, maxY = -1;

            // Find bounding box of non-transparent pixels
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    int idx = (y * sw + x) * 4 + 3; // alpha
                    if (src[idx] > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            // Handle empty image
            if (maxX < 0)
                return (new byte[4], 1, 1, 0, 0);

            int cropW = maxX - minX + 1;
            int cropH = maxY - minY + 1;
            var dst = new byte[cropW * cropH * 4];

            for (int y = 0; y < cropH; y++)
            {
                int srcRow = (minY + y) * sw * 4 + minX * 4;
                int dstRow = y * cropW * 4;
                Buffer.BlockCopy(src, srcRow, dst, dstRow, cropW * 4);
            }

            return (dst, cropW, cropH, minX, minY);
        }
        /// <summary>
        /// Converts a premultiplied alpha buffer to straight alpha in-place.
        /// </summary>
        /// <param name="buf">Buffer to unpremultiply (BGRA byte array).</param>
        /// <param name="w">Buffer width.</param>
        /// <param name="h">Buffer height.</param>
        /// <remarks>
        /// Divides RGB values by alpha to recover the original color.
        /// Pixels with alpha=0 are left unchanged (color doesn't matter when fully transparent).
        /// </remarks>
        private static void UnpremultiplyAlpha(byte[] buf, int w, int h)
        {
            int len = w * h * 4;
            for (int i = 0; i < len; i += 4)
            {
                byte a = buf[i + 3];
                if (a == 0 || a == 255) continue; // Skip fully transparent or fully opaque

                double alpha = a / 255.0;
                double invAlpha = 1.0 / alpha;

                // Unpremultiply: RGB_straight = RGB_premul / alpha
                buf[i + 0] = (byte)Math.Clamp(Math.Round(buf[i + 0] * invAlpha), 0, 255); // B
                buf[i + 1] = (byte)Math.Clamp(Math.Round(buf[i + 1] * invAlpha), 0, 255); // G
                buf[i + 2] = (byte)Math.Clamp(Math.Round(buf[i + 2] * invAlpha), 0, 255); // R
            }
        }

        /// <summary>
        /// Calculates the axis-aligned bounding box dimensions for a rotated rectangle.
        /// </summary>
        /// <param name="w">Original width.</param>
        /// <param name="h">Original height.</param>
        /// <param name="rad">Rotation angle in radians.</param>
        /// <param name="outW">Output: new width to contain rotated image.</param>
        /// <param name="outH">Output: new height to contain rotated image.</param>
        /// <remarks>
        /// Uses the formula:
        /// <code>
        /// outW = |w·cos(θ)| + |h·sin(θ)|
        /// outH = |w·sin(θ)| + |h·cos(θ)|
        /// </code>
        /// Ensures minimum dimensions of 1×1 even for degenerate cases.
        /// </remarks>
        public static void RotatedBounds(int w, int h, double rad, out int outW, out int outH)
        {
            double cos = Math.Abs(Math.Cos(rad));
            double sin = Math.Abs(Math.Sin(rad));
            outW = Math.Max(1, (int)Math.Ceiling(w * cos + h * sin));
            outH = Math.Max(1, (int)Math.Ceiling(w * sin + h * cos));
        }

        /// <summary>
        /// Rotates a byte buffer using bilinear interpolation with premultiplied alpha output for proper compositing.
        /// </summary>
        /// <param name="src">Source buffer (BGRA byte array with straight alpha).</param>
        /// <param name="sw">Source width in pixels.</param>
        /// <param name="sh">Source height in pixels.</param>
        /// <param name="angleDeg">Rotation angle in degrees.</param>
        /// <returns>Tuple of (rotated buffer with PREMULTIPLIED alpha, new width, new height).</returns>
        /// <remarks>
        /// <para>
        /// Samples four surrounding pixels with bilinear weighting to produce anti-aliased edges.
        /// Uses **premultiplied alpha blending** internally and outputs premultiplied alpha data
        /// suitable for direct use with Win2D's B8G8R8A8UIntNormalized format.
        /// </para>
        /// <para>
        /// **Premultiplied Alpha Algorithm**:
        /// 1. For each source pixel, premultiply RGB by alpha: Cpre = C × alpha
        /// 2. Blend premultiplied values using bilinear weights
        /// 3. Output remains premultiplied for correct compositing with graphics APIs
        /// </para>
        /// <para>
        /// This ensures edge pixels fade to transparent while preserving the original color,
        /// preventing the "dark bleeding" artifact where edges appear to blend toward black.
        /// The output format matches Win2D's expectations for CanvasBitmap.CreateFromBytes.
        /// </para>
        /// </remarks>
        public static (byte[] buf, int w, int h) RotateBilinear(byte[] src, int sw, int sh, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            RotatedBounds(sw, sh, rad, out int dw, out int dh);
            var dst = new byte[dw * dh * 4];

            double cxS = (sw - 1) * 0.5, cyS = (sh - 1) * 0.5;
            double cxD = (dw - 1) * 0.5, cyD = (dh - 1) * 0.5;

            double cos = Math.Cos(-rad), sin = Math.Sin(-rad);

            for (int y = 0; y < dh; y++)
            {
                double dy = y - cyD;
                int diRow = y * dw * 4;
                for (int x = 0; x < dw; x++)
                {
                    double dx = x - cxD;

                    double sx = dx * cos - dy * sin + cxS;
                    double sy = dx * sin + dy * cos + cyS;

                    int di = diRow + x * 4;

                    int x0 = (int)Math.Floor(sx);
                    int y0 = (int)Math.Floor(sy);
                    int x1 = x0 + 1;
                    int y1 = y0 + 1;

                    // Check bounds for all four sampling points
                    bool has00 = x0 >= 0 && x0 < sw && y0 >= 0 && y0 < sh;
                    bool has10 = x1 >= 0 && x1 < sw && y0 >= 0 && y0 < sh;
                    bool has01 = x0 >= 0 && x0 < sw && y1 >= 0 && y1 < sh;
                    bool has11 = x1 >= 0 && x1 < sw && y1 >= 0 && y1 < sh;

                    if (!has00 && !has10 && !has01 && !has11)
                    {
                        // Completely outside - transparent (already zero-initialized)
                        continue;
                    }

                    double fx = sx - x0, fy = sy - y0;

                    // Bilinear weights
                    double w00 = (1 - fx) * (1 - fy);
                    double w10 = fx * (1 - fy);
                    double w01 = (1 - fx) * fy;
                    double w11 = fx * fy;

                    // Accumulate PREMULTIPLIED color channels (RGB × alpha) and alpha separately
                    double sumPremulB = 0.0;
                    double sumPremulG = 0.0;
                    double sumPremulR = 0.0;
                    double sumAlpha = 0.0;

                    // Sample point (x0, y0)
                    if (has00)
                    {
                        int i00 = (y0 * sw + x0) * 4;
                        double a = src[i00 + 3] / 255.0;
                        double r = src[i00 + 2] / 255.0;
                        double g = src[i00 + 1] / 255.0;
                        double b = src[i00 + 0] / 255.0;

                        sumAlpha += a * w00;
                        sumPremulR += (r * a) * w00;  // Premultiply RGB by alpha
                        sumPremulG += (g * a) * w00;
                        sumPremulB += (b * a) * w00;
                    }

                    // Sample point (x1, y0)
                    if (has10)
                    {
                        int i10 = (y0 * sw + x1) * 4;
                        double a = src[i10 + 3] / 255.0;
                        double r = src[i10 + 2] / 255.0;
                        double g = src[i10 + 1] / 255.0;
                        double b = src[i10 + 0] / 255.0;

                        sumAlpha += a * w10;
                        sumPremulR += (r * a) * w10;
                        sumPremulG += (g * a) * w10;
                        sumPremulB += (b * a) * w10;
                    }

                    // Sample point (x0, y1)
                    if (has01)
                    {
                        int i01 = (y1 * sw + x0) * 4;
                        double a = src[i01 + 3] / 255.0;
                        double r = src[i01 + 2] / 255.0;
                        double g = src[i01 + 1] / 255.0;
                        double b = src[i01 + 0] / 255.0;

                        sumAlpha += a * w01;
                        sumPremulR += (r * a) * w01;
                        sumPremulG += (g * a) * w01;
                        sumPremulB += (b * a) * w01;
                    }

                    // Sample point (x1, y1)
                    if (has11)
                    {
                        int i11 = (y1 * sw + x1) * 4;
                        double a = src[i11 + 3] / 255.0;
                        double r = src[i11 + 2] / 255.0;
                        double g = src[i11 + 1] / 255.0;
                        double b = src[i11 + 0] / 255.0;

                        sumAlpha += a * w11;
                        sumPremulR += (r * a) * w11;
                        sumPremulG += (g * a) * w11;
                        sumPremulB += (b * a) * w11;
                    }

                    // Output alpha is the blended alpha
                    byte outA = (byte)Math.Clamp(Math.Round(sumAlpha * 255.0), 0, 255);

                    // **CRITICAL**: Keep RGB premultiplied (do NOT divide by alpha)
                    // Win2D's B8G8R8A8UIntNormalized format expects premultiplied alpha data
                    byte outR = (byte)Math.Clamp(Math.Round(sumPremulR * 255.0), 0, 255);
                    byte outG = (byte)Math.Clamp(Math.Round(sumPremulG * 255.0), 0, 255);
                    byte outB = (byte)Math.Clamp(Math.Round(sumPremulB * 255.0), 0, 255);

                    dst[di + 0] = outB;
                    dst[di + 1] = outG;
                    dst[di + 2] = outR;
                    dst[di + 3] = outA;
                }
            }
            return (dst, dw, dh);
        }

        /// <summary>
        /// Scales an image using repeated 2× upsampling followed by nearest-neighbor downsampling.
        /// </summary>
        /// <param name="src">Source buffer (BGRA byte array).</param>
        /// <param name="sw">Source width.</param>
        /// <param name="sh">Source height.</param>
        /// <param name="outW">Target width.</param>
        /// <param name="outH">Target height.</param>
        /// <param name="epx">If true, use EPX algorithm; otherwise, use Scale2x.</param>
        /// <returns>Tuple of (scaled buffer, output width, output height).</returns>
        /// <remarks>
        /// <para>
        /// Iteratively doubles the image size using pixel-art-aware algorithms (EPX or Scale2x) until
        /// the image is at least as large as the target, then uses nearest-neighbor for final adjustment.
        /// </para>
        /// <para>
        /// This approach preserves sharp edges better than direct bilinear scaling for pixel art and sprites.
        /// </para>
        /// </remarks>
        public static (byte[] buf, int w, int h) ScaleBy2xStepsThenNearest(
            byte[] src, int sw, int sh, int outW, int outH, bool epx)
        {
            byte[] cur = src;
            int cw = sw, ch = sh;

            while ((cw * 2) <= outW || (ch * 2) <= outH)
            {
                bool doW = (cw * 2) <= outW;
                bool doH = (ch * 2) <= outH;

                if (!doW && !doH) break;

                var (buf, w, h) = epx ? EPX2x(cur, cw, ch) : Scale2x(cur, cw, ch);
                cur = buf;
                cw = w;
                ch = h;
            }

            if (cw != outW || ch != outH)
                cur = ResizeNearest(cur, cw, ch, outW, outH);

            return (cur, outW, outH);
        }

        /// <summary>
        /// Resizes an image using nearest-neighbor sampling.
        /// </summary>
        /// <param name="src">Source buffer (BGRA byte array).</param>
        /// <param name="sw">Source width.</param>
        /// <param name="sh">Source height.</param>
        /// <param name="dw">Destination width.</param>
        /// <param name="dh">Destination height.</param>
        /// <returns>Resized buffer as byte array.</returns>
        /// <remarks>
        /// <para>
        /// Fast integer-based scaling that preserves hard edges (no antialiasing). Each output pixel
        /// samples the nearest source pixel: <c>srcX = (x * srcWidth) / dstWidth</c>.
        /// </para>
        /// <para>
        /// Includes safety checks for invalid dimensions and buffer underruns.
        /// </para>
        /// </remarks>
        public static byte[] ResizeNearest(byte[] src, int sw, int sh, int dw, int dh)
        {
            if (sw <= 0 || sh <= 0 || dw <= 0 || dh <= 0)
                return [];

            int needed = sw * sh * 4;
            if (src.Length < needed)
            {
                int px = src.Length / 4;
                if (px == 0) return new byte[dw * dh * 4];
                if (sw > px) sw = px;
                sh = Math.Max(1, px / Math.Max(1, sw));
            }

            var dst = new byte[dw * dh * 4];
            double sx = (double)sw / dw;
            double sy = (double)sh / dh;

            for (int y = 0; y < dh; y++)
            {
                int syi = Math.Min(sh - 1, (int)(y * sy));
                int srow = syi * sw * 4;
                int diRow = y * dw * 4;

                for (int x = 0; x < dw; x++)
                {
                    int sxi = Math.Min(sw - 1, (int)(x * sx));
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
        /// <param name="sw">Source width.</param>
        /// <param name="sh">Source height.</param>
        /// <param name="dw">Destination width.</param>
        /// <param name="dh">Destination height.</param>
        /// <returns>Resized buffer as byte array.</returns>
        /// <remarks>
        /// <para>
        /// Samples a 2×2 grid of source pixels for each output pixel with bilinear weighting,
        /// producing anti-aliased results. Significantly higher quality than nearest-neighbor
        /// but approximately 2× slower.
        /// </para>
        /// <para>
        /// Recommended for downscaling and general-purpose image resizing where smoothness is preferred.
        /// </para>
        /// </remarks>
        public static byte[] ResizeBilinear(byte[] src, int sw, int sh, int dw, int dh)
        {
            var dst = new byte[dw * dh * 4];
            double gx = (double)(sw - 1) / (dw - 1 == 0 ? 1 : dw - 1);
            double gy = (double)(sh - 1) / (dh - 1 == 0 ? 1 : dh - 1);

            int di = 0;
            for (int y = 0; y < dh; y++)
            {
                double syf = y * gy;
                int y0 = (int)Math.Floor(syf);
                int y1 = Math.Min(sh - 1, y0 + 1);
                double fy = syf - y0;

                for (int x = 0; x < dw; x++, di += 4)
                {
                    double sxf = x * gx;
                    int x0 = (int)Math.Floor(sxf);
                    int x1 = Math.Min(sw - 1, x0 + 1);
                    double fx = sxf - x0;

                    int i00 = (y0 * sw + x0) * 4;
                    int i10 = (y0 * sw + x1) * 4;
                    int i01 = (y1 * sw + x0) * 4;
                    int i11 = (y1 * sw + x1) * 4;

                    for (int c = 0; c < 4; c++)
                    {
                        double v =
                            src[i00 + c] * (1 - fx) * (1 - fy) +
                            src[i10 + c] * (fx) * (1 - fy) +
                            src[i01 + c] * (1 - fx) * (fy) +
                            src[i11 + c] * (fx) * (fy);
                        dst[di + c] = (byte)Math.Round(v);
                    }
                }
            }
            return dst;
        }

        /// <summary>
        /// Upscales an image 2× using the EPX (Scale2×/AdvMAME2x) edge-preserving algorithm.
        /// </summary>
        /// <param name="src">Source buffer (BGRA byte array).</param>
        /// <param name="w">Source width.</param>
        /// <param name="h">Source height.</param>
        /// <returns>Tuple of (scaled buffer, new width=w×2, new height=h×2).</returns>
        /// <remarks>
        /// <para>
        /// EPX algorithm detects edges and corners to preserve sharpness during upscaling:
        /// <code>
        ///   A
        /// D E F    →    E0 E1
        ///   H           E2 E3
        /// 
        /// if B==D &amp;&amp; B!=F &amp;&amp; D!=H: E0=B  (corner detected)
        /// else: E0=E (no corner, keep center)
        /// </code>
        /// Similar rules apply to E1, E2, E3.
        /// </para>
        /// <para>
        /// Designed for pixel art to avoid the "blurring" of traditional interpolation while
        /// reducing visible aliasing on curves and diagonals.
        /// </para>
        /// </remarks>
        public static (byte[] buf, int w, int h) EPX2x(byte[] src, int w, int h)
        {
            int dw = w * 2, dh = h * 2;
            var dst = new byte[dw * dh * 4];

            uint P(int x, int y)
            {
                x = Math.Clamp(x, 0, w - 1);
                y = Math.Clamp(y, 0, h - 1);
                int i = (y * w + x) * 4;
                return (uint)(src[i] | (src[i + 1] << 8) | (src[i + 2] << 16) | (src[i + 3] << 24));
            }

            void W(int dx, int dy, uint v)
            {
                int i = (dy * dw + dx) * 4;
                dst[i] = (byte)(v & 0xFF);
                dst[i + 1] = (byte)((v >> 8) & 0xFF);
                dst[i + 2] = (byte)((v >> 16) & 0xFF);
                dst[i + 3] = (byte)((v >> 24) & 0xFF);
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    uint A = P(x - 1, y - 1), B = P(x, y - 1), C = P(x + 1, y - 1);
                    uint D = P(x - 1, y), E = P(x, y), F = P(x + 1, y);
                    uint G = P(x - 1, y + 1), H = P(x, y + 1), I = P(x + 1, y + 1);

                    uint E0 = E, E1 = E, E2 = E, E3 = E;

                    if (B == D && B != F && D != H) E0 = B;
                    if (B == F && B != D && F != H) E1 = B;
                    if (D == H && D != B && H != F) E2 = D;
                    if (H == F && D != F && B != H) E3 = F;

                    int dx = x * 2, dy = y * 2;
                    W(dx, dy, E0);
                    W(dx + 1, dy, E1);
                    W(dx, dy + 1, E2);
                    W(dx + 1, dy + 1, E3);
                }
            }
            return (dst, dw, dh);
        }

        /// <summary>
        /// Upscales an image 2× using the Scale2x algorithm.
        /// </summary>
        /// <param name="src">Source buffer (BGRA byte array).</param>
        /// <param name="w">Source width.</param>
        /// <param name="h">Source height.</param>
        /// <returns>Tuple of (scaled buffer, new width=w×2, new height=h×2).</returns>
        /// <remarks>
        /// <para>
        /// Scale2x is similar to EPX but with slightly different edge detection rules:
        /// <code>
        /// if B≠H &amp;&amp; D≠F:
        ///   E0 = (D==B) ? D : E
        ///   E1 = (B==F) ? F : E
        ///   E2 = (D==H) ? D : E
        ///   E3 = (H==F) ? F : E
        /// </code>
        /// </para>
        /// <para>
        /// Produces slightly different edge handling than EPX; both are suitable for pixel art upscaling.
        /// </para>
        /// </remarks>
        public static (byte[] buf, int w, int h) Scale2x(byte[] src, int w, int h)
        {
            int dw = w * 2, dh = h * 2;
            var dst = new byte[dw * dh * 4];

            uint P(int x, int y)
            {
                x = Math.Clamp(x, 0, w - 1);
                y = Math.Clamp(y, 0, h - 1);
                int i = (y * w + x) * 4;
                return (uint)(src[i] | (src[i + 1] << 8) | (src[i + 2] << 16) | (src[i + 3] << 24));
            }

            void W(int dx, int dy, uint v)
            {
                int i = (dy * dw + dx) * 4;
                dst[i] = (byte)(v & 0xFF);
                dst[i + 1] = (byte)((v >> 8) & 0xFF);
                dst[i + 2] = (byte)((v >> 16) & 0xFF);
                dst[i + 3] = (byte)((v >> 24) & 0xFF);
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    uint A = P(x - 1, y - 1), B = P(x, y - 1), C = P(x + 1, y - 1);
                    uint D = P(x - 1, y), E = P(x, y), F = P(x + 1, y);
                    uint G = P(x - 1, y + 1), H = P(x, y + 1), I = P(x + 1, y + 1);

                    uint E0 = E, E1 = E, E2 = E, E3 = E;

                    if (B != H && D != F)
                    {
                        E0 = (D == B) ? D : E;
                        E1 = (B == F) ? F : E;
                        E2 = (D == H) ? D : E;
                        E3 = (H == F) ? F : E;
                    }

                    int dx = x * 2, dy = y * 2;
                    W(dx, dy, E0);
                    W(dx + 1, dy, E1);
                    W(dx, dy + 1, E2);
                    W(dx + 1, dy + 1, E3);
                }
            }
            return (dst, dw, dh);
        }

        /// <summary>
        /// Clears (fills with transparent black) a rectangular region in a buffer.
        /// </summary>
        /// <param name="dst">Destination buffer (packed BGRA uint array).</param>
        /// <param name="w">Buffer width.</param>
        /// <param name="h">Buffer height.</param>
        /// <param name="r">Rectangle to clear (clamped to buffer bounds).</param>
        public static void ClearRect(uint[] dst, int w, int h, RectInt32 r)
        {
            int x0 = Math.Clamp(r.X, 0, w);
            int y0 = Math.Clamp(r.Y, 0, h);
            int x1 = Math.Clamp(r.X + r.Width, 0, w);
            int y1 = Math.Clamp(r.Y + r.Height, 0, h);
            for (int y = y0; y < y1; y++)
                Array.Fill(dst, 0u, y * w + x0, x1 - x0);
        }

        /// <summary>
        /// Scales a uint buffer using nearest-neighbor sampling.
        /// </summary>
        /// <param name="src">Source buffer (packed BGRA uint array).</param>
        /// <param name="sw">Source width.</param>
        /// <param name="sh">Source height.</param>
        /// <param name="dw">Destination width.</param>
        /// <param name="dh">Destination height.</param>
        /// <returns>Scaled buffer as uint array.</returns>
        /// <remarks>
        /// Integer-only implementation for maximum performance when working with uint buffers.
        /// Uses long arithmetic to prevent overflow in intermediate calculations.
        /// </remarks>
        public static uint[] ScaleNN(uint[] src, int sw, int sh, int dw, int dh)
        {
            var dst = new uint[Math.Max(0, dw) * Math.Max(0, dh)];
            if (dw <= 0 || dh <= 0 || sw <= 0 || sh <= 0) return dst;

            for (int y = 0; y < dh; y++)
            {
                int sy = (int)((long)y * sh / dh);
                int dRow = y * dw;
                int sRow = sy * sw;
                for (int x = 0; x < dw; x++)
                {
                    int sx = (int)((long)x * sw / dw);
                    dst[dRow + x] = src[sRow + sx];
                }
            }
            return dst;
        }

        /// <summary>
        /// Tests if two pixels in a buffer are similar within a tolerance.
        /// </summary>
        /// <param name="pix">Pixel buffer (BGRA byte array).</param>
        /// <param name="idxA">Byte index of first pixel.</param>
        /// <param name="idxB">Byte index of second pixel.</param>
        /// <param name="tolerance">Color difference tolerance (Euclidean distance threshold).</param>
        /// <param name="useAlpha">If true, include alpha channel in comparison.</param>
        /// <returns><c>true</c> if pixels are similar; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Uses Euclidean distance: <c>sqrt(dR² + dG² + dB² [+ dA²])</c> and compares against tolerance.
        /// Squared comparison is used internally to avoid sqrt calculation.
        /// </remarks>
        public static bool PixelsSimilar(byte[] pix, int idxA, int idxB, int tolerance, bool useAlpha)
        {
            int db = pix[idxA] - pix[idxB];
            int dg = pix[idxA + 1] - pix[idxB + 1];
            int dr = pix[idxA + 2] - pix[idxB + 2];
            int da = useAlpha ? pix[idxA + 3] - pix[idxB + 3] : 0;

            int dist = db * db + dg * dg + dr * dr + da * da;
            return dist <= (tolerance * tolerance * 3);
        }

        /// <summary>
        /// Rotates a uint buffer using nearest-neighbor sampling.
        /// </summary>
        /// <param name="src">Source buffer (packed BGRA uint array).</param>
        /// <param name="sw">Source width.</param>
        /// <param name="sh">Source height.</param>
        /// <param name="radians">Rotation angle in radians.</param>
        /// <param name="outW">Output: new width.</param>
        /// <param name="outH">Output: new height.</param>
        /// <returns>Rotated buffer as uint array.</returns>
        /// <remarks>
        /// Similar to <see cref="RotateNearest"/> but operates on uint[] buffers instead of byte[].
        /// </remarks>
        public static uint[] RotateNN(uint[] src, int sw, int sh, double radians, out int outW, out int outH)
        {
            double cos = Math.Cos(radians), sin = Math.Sin(radians);
            var xs = new[] { -sw / 2.0, sw / 2.0, sw / 2.0, -sw / 2.0 };
            var ys = new[] { -sh / 2.0, -sh / 2.0, sh / 2.0, sh / 2.0 };
            double minX = 1e9, maxX = -1e9, minY = 1e9, maxY = -1e9;
            for (int i = 0; i < 4; i++)
            {
                double X = xs[i] * cos - ys[i] * sin;
                double Y = xs[i] * sin + ys[i] * cos;
                minX = Math.Min(minX, X); maxX = Math.Max(maxX, X);
                minY = Math.Min(minY, Y); maxY = Math.Max(maxY, Y);
            }
            outW = (int)Math.Ceiling(maxX - minX);
            outH = (int)Math.Ceiling(maxY - minY);
            var dst = new uint[outW * outH];

            double cxS = (sw - 1) / 2.0, cyS = (sh - 1) / 2.0;
            double cxD = (outW - 1) / 2.0, cyD = (outH - 1) / 2.0;

            for (int y = 0; y < outH; y++)
            {
                for (int x = 0; x < outW; x++)
                {
                    double xd = x - cxD, yd = y - cyD;
                    double xsr = xd * cos + yd * sin + cxS;
                    double ysr = -xd * sin + yd * cos + cyS;
                    int sx = (int)Math.Round(xsr);
                    int sy = (int)Math.Round(ysr);
                    uint c = 0u;
                    if (sx >= 0 && sx < sw && sy >= 0 && sy < sh)
                        c = src[sy * sw + sx];
                    dst[y * outW + x] = c;
                }
            }
            return dst;
        }
    }
}
