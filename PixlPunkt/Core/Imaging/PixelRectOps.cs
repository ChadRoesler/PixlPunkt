using System;
using Windows.Graphics;

namespace PixlPunkt.Core.Imaging
{
    /// <summary>
    /// Rectangle-level BGRA buffer operations shared by the selection subsystem.
    /// </summary>
    public static class PixelRectOps
    {
        /// <summary>Copies the pixels of <paramref name="r"/> (clamped to the surface) into a new tightly packed buffer.</summary>
        public static byte[] CopyRect(byte[] src, int w, int h, RectInt32 r)
        {
            int x0 = Math.Max(0, r.X), y0 = Math.Max(0, r.Y), x1 = Math.Min(w, r.X + r.Width), y1 = Math.Min(h, r.Y + r.Height);
            int rw = Math.Max(0, x1 - x0), rh = Math.Max(0, y1 - y0);
            var dst = new byte[rw * rh * 4];
            if (rw == 0 || rh == 0) return dst;
            int srcStride = w * 4, dstStride = rw * 4;
            for (int y = 0; y < rh; y++)
                Buffer.BlockCopy(src, (y0 + y) * srcStride + x0 * 4, dst, y * dstStride, dstStride);
            return dst;
        }

        /// <summary>Copies a packed buffer into the surface at (dx, dy), clipping to the surface. No blending.</summary>
        public static void Blit(byte[] dst, int w, int h, int dx, int dy, byte[] buf, int bw, int bh)
        {
            int x0 = Math.Max(0, dx), y0 = Math.Max(0, dy), x1 = Math.Min(w, dx + bw), y1 = Math.Min(h, dy + bh);
            if (x1 <= x0 || y1 <= y0) return;
            int dstStride = w * 4, srcStride = bw * 4;
            for (int y = y0; y < y1; y++)
            {
                int sy = y - dy;
                Buffer.BlockCopy(buf, sy * srcStride + (x0 - dx) * 4, dst, y * dstStride + x0 * 4, (x1 - x0) * 4);
            }
        }

        /// <summary>
        /// Tight bounds of every pixel with alpha &gt; 0, or null when the buffer is fully
        /// transparent. Used by "auto crop" exports.
        /// </summary>
        public static RectInt32? OpaqueBounds(byte[] src, int w, int h)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    if (src[row + x * 4 + 3] == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) return null;
            return new RectInt32 { X = minX, Y = minY, Width = maxX - minX + 1, Height = maxY - minY + 1 };
        }

        /// <summary>Zeroes the pixels of <paramref name="r"/> (clamped to the surface).</summary>
        public static void ClearRect(byte[] dst, int w, int h, RectInt32 r)
        {
            int x0 = Math.Clamp(r.X, 0, w), y0 = Math.Clamp(r.Y, 0, h);
            int x1 = Math.Clamp(r.X + r.Width, 0, w), y1 = Math.Clamp(r.Y + r.Height, 0, h);
            int dstStride = w * 4, bytes = (x1 - x0) * 4;
            for (int y = y0; y < y1; y++)
                Array.Clear(dst, y * dstStride + x0 * 4, bytes);
        }

        /// <summary>Porter-Duff source-over of a straight-alpha packed buffer onto the surface at (dx, dy).</summary>
        public static void BlitAlphaOver(byte[] dst, int w, int h, int dx, int dy, byte[] src, int sw, int sh)
        {
            int x0 = Math.Max(0, dx), y0 = Math.Max(0, dy), x1 = Math.Min(w, dx + sw), y1 = Math.Min(h, dy + sh);
            if (x1 <= x0 || y1 <= y0) return;
            int dstStride = w * 4, srcStride = sw * 4;
            for (int y = y0; y < y1; y++)
            {
                int sy = y - dy, dstRow = y * dstStride, srcRow = sy * srcStride;
                for (int x = x0; x < x1; x++)
                {
                    int sx = x - dx, di = dstRow + x * 4, si = srcRow + sx * 4;
                    byte sb = src[si], sg = src[si + 1], sr = src[si + 2], sa = src[si + 3];
                    if (sa == 0) continue;
                    if (sa == 255)
                    {
                        dst[di] = sb; dst[di + 1] = sg; dst[di + 2] = sr; dst[di + 3] = 255;
                        continue;
                    }
                    byte db = dst[di], dg = dst[di + 1], dr = dst[di + 2], da = dst[di + 3];
                    int invA = 255 - sa;
                    int outA = sa + da * invA / 255;
                    if (outA == 0)
                    {
                        dst[di] = 0; dst[di + 1] = 0; dst[di + 2] = 0; dst[di + 3] = 0;
                    }
                    else
                    {
                        dst[di] = (byte)((sb * sa + db * da * invA / 255) / outA);
                        dst[di + 1] = (byte)((sg * sa + dg * da * invA / 255) / outA);
                        dst[di + 2] = (byte)((sr * sa + dr * da * invA / 255) / outA);
                        dst[di + 3] = (byte)outA;
                    }
                }
            }
        }
    }
}
