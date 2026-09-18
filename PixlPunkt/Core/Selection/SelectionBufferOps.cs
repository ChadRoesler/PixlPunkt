using System;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Imaging;

namespace PixlPunkt.Core.Selection
{
    /// <summary>
    /// Scale and rotate operations on a floating selection's pixel buffer. Used identically by
    /// the on-screen preview and by the commit path so both produce the same pixels.
    /// </summary>
    public static class SelectionBufferOps
    {
        public static (byte[] buf, int w, int h) BuildScaled(byte[] src, int sw, int sh, double sx, double sy, ScaleMode filter)
        {
            int outW = Math.Max(1, (int)Math.Round(sw * sx));
            int outH = Math.Max(1, (int)Math.Round(sh * sy));
            if (outW == sw && outH == sh) return (src, sw, sh);
            return filter switch
            {
                ScaleMode.NearestNeighbor => (PixelOps.ResizeNearest(src, sw, sh, outW, outH), outW, outH),
                ScaleMode.Bilinear => (PixelOps.ResizeBilinear(src, sw, sh, outW, outH), outW, outH),
                ScaleMode.EPX => PixelOps.ScaleBy2xStepsThenNearest(src, sw, sh, outW, outH, epx: true),
                ScaleMode.Scale2x => PixelOps.ScaleBy2xStepsThenNearest(src, sw, sh, outW, outH, epx: false),
                _ => (PixelOps.ResizeNearest(src, sw, sh, outW, outH), outW, outH)
            };
        }

        /// <summary>
        /// Scales and rotates a 1-byte-per-pixel selection mask with the same geometry as the
        /// pixels: nearest-neighbour for the scale, and the pixels' own rotation method for the
        /// rotation so the outline shows what that method will produce. The mask rides the pixel
        /// resamplers as alpha; any coverage counts as selected.
        /// </summary>
        public static (byte[] mask, int w, int h) BuildTransformedMask(
            byte[] mask, int w, int h, double sx, double sy, double angleDeg, RotationMode rotMode)
        {
            int n = w * h;
            var rgba = new byte[n * 4];
            for (int i = 0; i < n; i++)
                if (mask[i] != 0) { int o = i * 4; rgba[o] = rgba[o + 1] = rgba[o + 2] = rgba[o + 3] = 255; }

            var (scaled, sw, sh) = BuildScaled(rgba, w, h, sx, sy, ScaleMode.NearestNeighbor);
            var (rotated, rw, rh) = BuildRotated(scaled, sw, sh, angleDeg, rotMode);

            int m = rw * rh;
            var outMask = new byte[m];
            for (int i = 0; i < m; i++)
                outMask[i] = rotated[i * 4 + 3] != 0 ? (byte)1 : (byte)0;
            return (outMask, rw, rh);
        }

        public static (byte[] buf, int w, int h) BuildRotated(byte[] src, int sw, int sh, double angleDeg, RotationMode kind)
        {
            double a = angleDeg % 360.0;
            if (Math.Abs(a) < 1e-6) return (src, sw, sh);
            return kind switch
            {
                RotationMode.RotSprite => PixelOps.RotateSpriteApprox(src, sw, sh, a),
                _ => PixelOps.RotateNearest(src, sw, sh, a)
            };
        }
    }
}
