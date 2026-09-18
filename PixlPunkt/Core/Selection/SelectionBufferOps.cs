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
