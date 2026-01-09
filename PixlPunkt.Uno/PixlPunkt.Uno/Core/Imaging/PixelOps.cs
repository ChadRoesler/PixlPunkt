using System;
using Windows.Graphics;
using PixlPunkt.PluginSdk.Imaging;

namespace PixlPunkt.Uno.Core.Imaging
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
        // ════════════════════════════════════════════════════════════════════
        // SDK DELEGATING METHODS
        // These delegate to PixlPunkt.PluginSdk.Imaging.PixelBufferOps
        // ════════════════════════════════════════════════════════════════════

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
        /// Fast integer-based scaling that preserves hard edges (no antialiasing).
        /// Ideal for pixel art and when crisp edges are desired.
        /// </remarks>
        public static byte[] ResizeNearest(byte[] src, int sw, int sh, int dw, int dh)
            => PixelBufferOps.ResizeNearest(src, sw, sh, dw, dh);

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
        /// Produces anti-aliased results. Recommended for downscaling and general-purpose resizing.
        /// </remarks>
        public static byte[] ResizeBilinear(byte[] src, int sw, int sh, int dw, int dh)
            => PixelBufferOps.ResizeBilinear(src, sw, sh, dw, dh);

        /// <summary>
        /// Tests if two pixels in a buffer are similar within a tolerance.
        /// </summary>
        /// <param name="pix">Pixel buffer (BGRA byte array).</param>
        /// <param name="idxA">Byte index of first pixel.</param>
        /// <param name="idxB">Byte index of second pixel.</param>
        /// <param name="tolerance">Color difference tolerance.</param>
        /// <param name="useAlpha">If true, include alpha channel in comparison.</param>
        /// <returns><c>true</c> if pixels are similar; otherwise, <c>false</c>.</returns>
        public static bool PixelsSimilar(byte[] pix, int idxA, int idxB, int tolerance, bool useAlpha)
            => PixelBufferOps.PixelsSimilar(pix, idxA, idxB, tolerance, useAlpha);

        /// <summary>
        /// Crops a buffer to its minimal bounding box (removes transparent edges).
        /// </summary>
        /// <param name="src">Source buffer (BGRA byte array).</param>
        /// <param name="sw">Source width.</param>
        /// <param name="sh">Source height.</param>
        /// <returns>Tuple of (cropped buffer, width, height, offsetX, offsetY).</returns>
        public static (byte[] buf, int w, int h, int offsetX, int offsetY) CropToOpaque(byte[] src, int sw, int sh)
            => PixelBufferOps.CropToOpaque(src, sw, sh);

        // ════════════════════════════════════════════════════════════════════
        // UNO-SPECIFIC METHODS (Rect-based operations)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extracts a rectangular region from a source buffer.
        /// </summary>
        /// <param name="src">Source buffer (packed BGRA uint array).</param>
        /// <param name="w">Source buffer width in pixels.</param>
        /// <param name="h">Source buffer height in pixels.</param>
        /// <param name="r">Rectangle to extract (clamped to source bounds).</param>
        /// <returns>New buffer containing only the specified rectangle's pixels.</returns>
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

        // ════════════════════════════════════════════════════════════════════
        // UNO-SPECIFIC METHODS (Blitting)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Blits (copies) a buffer into a destination buffer at the specified position.
        /// </summary>
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

        // ════════════════════════════════════════════════════════════════════
        // UNO-SPECIFIC METHODS (Rotation)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Rotates a byte buffer using nearest-neighbor sampling.
        /// </summary>
        public static (byte[] buf, int w, int h) RotateNearest(byte[] src, int sw, int sh, double angleDeg)
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
                }
            }
            return (dst, dw, dh);
        }

        /// <summary>
        /// Rotates a pixel-art image using a hybrid RotSprite-inspired algorithm.
        /// </summary>
        public static (byte[] buf, int w, int h) RotateSpriteApprox(byte[] src, int sw, int sh, double angleDeg)
        {
            var (buf, w, h) = EPX2x(src, sw, sh);
            var rot = RotateBilinear(buf, w, h, angleDeg);
            UnpremultiplyAlpha(rot.buf, rot.w, rot.h);

            int targetW = Math.Max(1, (int)Math.Round(sw * (double)rot.w / w));
            int targetH = Math.Max(1, (int)Math.Round(sh * (double)rot.h / h));
            var dn = ResizeNearest(rot.buf, rot.w, rot.h, targetW, targetH);

            return (dn, targetW, targetH);
        }

        /// <summary>
        /// Calculates the axis-aligned bounding box dimensions for a rotated rectangle.
        /// </summary>
        public static void RotatedBounds(int w, int h, double rad, out int outW, out int outH)
        {
            double cos = Math.Abs(Math.Cos(rad));
            double sin = Math.Abs(Math.Sin(rad));
            outW = Math.Max(1, (int)Math.Ceiling(w * cos + h * sin));
            outH = Math.Max(1, (int)Math.Ceiling(w * sin + h * cos));
        }

        /// <summary>
        /// Rotates a byte buffer using bilinear interpolation with premultiplied alpha output.
        /// </summary>
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

                    bool has00 = x0 >= 0 && x0 < sw && y0 >= 0 && y0 < sh;
                    bool has10 = x1 >= 0 && x1 < sw && y0 >= 0 && y0 < sh;
                    bool has01 = x0 >= 0 && x0 < sw && y1 >= 0 && y1 < sh;
                    bool has11 = x1 >= 0 && x1 < sw && y1 >= 0 && y1 < sh;

                    if (!has00 && !has10 && !has01 && !has11)
                        continue;

                    double fx = sx - x0, fy = sy - y0;

                    double w00 = (1 - fx) * (1 - fy);
                    double w10 = fx * (1 - fy);
                    double w01 = (1 - fx) * fy;
                    double w11 = fx * fy;

                    double sumPremulB = 0.0, sumPremulG = 0.0, sumPremulR = 0.0, sumAlpha = 0.0;

                    if (has00)
                    {
                        int i00 = (y0 * sw + x0) * 4;
                        double a = src[i00 + 3] / 255.0;
                        sumAlpha += a * w00;
                        sumPremulR += (src[i00 + 2] / 255.0 * a) * w00;
                        sumPremulG += (src[i00 + 1] / 255.0 * a) * w00;
                        sumPremulB += (src[i00 + 0] / 255.0 * a) * w00;
                    }

                    if (has10)
                    {
                        int i10 = (y0 * sw + x1) * 4;
                        double a = src[i10 + 3] / 255.0;
                        sumAlpha += a * w10;
                        sumPremulR += (src[i10 + 2] / 255.0 * a) * w10;
                        sumPremulG += (src[i10 + 1] / 255.0 * a) * w10;
                        sumPremulB += (src[i10 + 0] / 255.0 * a) * w10;
                    }

                    if (has01)
                    {
                        int i01 = (y1 * sw + x0) * 4;
                        double a = src[i01 + 3] / 255.0;
                        sumAlpha += a * w01;
                        sumPremulR += (src[i01 + 2] / 255.0 * a) * w01;
                        sumPremulG += (src[i01 + 1] / 255.0 * a) * w01;
                        sumPremulB += (src[i01 + 0] / 255.0 * a) * w01;
                    }

                    if (has11)
                    {
                        int i11 = (y1 * sw + x1) * 4;
                        double a = src[i11 + 3] / 255.0;
                        sumAlpha += a * w11;
                        sumPremulR += (src[i11 + 2] / 255.0 * a) * w11;
                        sumPremulG += (src[i11 + 1] / 255.0 * a) * w11;
                        sumPremulB += (src[i11 + 0] / 255.0 * a) * w11;
                    }

                    dst[di + 0] = (byte)Math.Clamp(Math.Round(sumPremulB * 255.0), 0, 255);
                    dst[di + 1] = (byte)Math.Clamp(Math.Round(sumPremulG * 255.0), 0, 255);
                    dst[di + 2] = (byte)Math.Clamp(Math.Round(sumPremulR * 255.0), 0, 255);
                    dst[di + 3] = (byte)Math.Clamp(Math.Round(sumAlpha * 255.0), 0, 255);
                }
            }
            return (dst, dw, dh);
        }

        /// <summary>
        /// Rotates a uint buffer using nearest-neighbor sampling.
        /// </summary>
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

        // ════════════════════════════════════════════════════════════════════
        // UNO-SPECIFIC METHODS (Pixel Art Scaling)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Scales an image using repeated 2× upsampling followed by nearest-neighbor downsampling.
        /// </summary>
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
        /// Upscales an image 2× using the EPX (Scale2×/AdvMAME2x) edge-preserving algorithm.
        /// </summary>
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

        // ════════════════════════════════════════════════════════════════════
        // UNO-SPECIFIC METHODS (uint buffer scaling)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Scales a uint buffer using nearest-neighbor sampling.
        /// </summary>
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

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Converts a premultiplied alpha buffer to straight alpha in-place.
        /// </summary>
        private static void UnpremultiplyAlpha(byte[] buf, int w, int h)
        {
            int len = w * h * 4;
            for (int i = 0; i < len; i += 4)
            {
                byte a = buf[i + 3];
                if (a == 0 || a == 255) continue;

                double invAlpha = 255.0 / a;
                buf[i + 0] = (byte)Math.Clamp(Math.Round(buf[i + 0] * invAlpha), 0, 255);
                buf[i + 1] = (byte)Math.Clamp(Math.Round(buf[i + 1] * invAlpha), 0, 255);
                buf[i + 2] = (byte)Math.Clamp(Math.Round(buf[i + 2] * invAlpha), 0, 255);
            }
        }
    }
}
