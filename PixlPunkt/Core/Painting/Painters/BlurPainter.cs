using System;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Tools.Settings;

namespace PixlPunkt.Core.Painting.Painters
{
    /// <summary>
    /// Blur painting strategy - softens pixels by sampling a weighted 3x3 kernel from the snapshot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BlurPainter"/> implements a localized blur effect:
    /// </para>
    /// <list type="bullet">
    /// <item>Samples a 3x3 weighted Gaussian-like kernel from the stroke-start snapshot.</item>
    /// <item>Blends the blurred result with the original pixel based on brush alpha.</item>
    /// <item>Uses max-alpha accumulation to control blur intensity during continuous strokes.</item>
    /// <item>Requires snapshot to prevent feedback loops during iterative blurring.</item>
    /// </list>
    /// </remarks>
    public sealed class BlurPainter : PainterBase
    {
        private readonly BlurToolSettings _settings;

        /// <summary>Gaussian-like 3x3 kernel weights.</summary>
        private static readonly int[] KernelWeights = [1, 2, 1, 2, 4, 2, 1, 2, 1];

        /// <summary>
        /// Creates a new BlurPainter bound to the specified settings.
        /// </summary>
        /// <param name="settings">The blur tool settings to use.</param>
        public BlurPainter(BlurToolSettings settings)
        {
            _settings = settings;
        }

        /// <inheritdoc/>
        public override bool NeedsSnapshot => true;

        /// <inheritdoc/>
        public override void StampAt(int cx, int cy, StrokeContext ctx)
        {
            if (Surface == null || Snapshot == null) return;

            foreach (var (dx, dy) in ctx.BrushOffsets)
            {
                byte effA = ctx.ComputeAlphaAtOffset(dx, dy);
                if (effA == 0) continue;

                int x = cx + dx, y = cy + dy;
                if (!ctx.IsInBounds(x, y)) continue;

                // Check selection mask - skip pixels outside selection
                if (!ctx.IsInSelection(x, y)) continue;

                int idx = ctx.IndexOf(x, y);

                uint before = ReadPixel(Surface.Pixels, idx);
                var rec = GetOrCreateAccumRec(idx, before);

                // Only apply if this stamp has higher alpha than previous
                if (effA <= rec.maxA)
                    continue;

                rec.maxA = effA;

                // Sample blurred color from snapshot
                uint blurred = SampleBlurredFromSnapshot(x, y, ctx);

                // Interpolate from original to blurred based on effective alpha
                rec.after = ColorUtil.LerpRGBA(rec.before, blurred, rec.maxA);

                CommitAccumRec(idx, rec);
            }
        }

        /// <summary>
        /// Samples a 3x3 weighted blur kernel from the snapshot.
        /// </summary>
        /// <param name="x">Center X coordinate.</param>
        /// <param name="y">Center Y coordinate.</param>
        /// <param name="ctx">Stroke context for bounds checking.</param>
        /// <returns>Blurred BGRA pixel value.</returns>
        private uint SampleBlurredFromSnapshot(int x, int y, StrokeContext ctx)
        {
            double sumA = 0, sumR = 0, sumG = 0, sumB = 0, sumW = 0;

            int idx = 0;
            for (int j = -1; j <= 1; j++)
            {
                int yy = y + j;
                if (!IsYInBounds(yy, ctx))
                {
                    idx += 3;
                    continue;
                }

                for (int i = -1; i <= 1; i++)
                {
                    int xx = x + i;
                    int ww = KernelWeights[idx++];

                    if (!IsXInBounds(xx, ctx))
                        continue;

                    int sampleIdx = (yy * ctx.Surface.Width + xx) * 4;
                    uint c = ReadPixel(Snapshot!, sampleIdx);

                    double a = (c >> 24) / 255.0;
                    double r = ((c >> 16) & 0xFF) / 255.0;
                    double g = ((c >> 8) & 0xFF) / 255.0;
                    double b = (c & 0xFF) / 255.0;

                    // Premultiplied alpha accumulation
                    sumA += a * ww;
                    sumR += r * a * ww;
                    sumG += g * a * ww;
                    sumB += b * a * ww;
                    sumW += ww;
                }
            }

            // Compute output with unpremultiplication
            double outA = (sumW > 0) ? (sumA / sumW) : 0.0;
            double outR = outA > 0 ? (sumR / sumA) : 0.0;
            double outG = outA > 0 ? (sumG / sumA) : 0.0;
            double outB = outA > 0 ? (sumB / sumA) : 0.0;

            byte oa = (byte)Math.Round(outA * 255.0);
            byte rr = (byte)Math.Round(Math.Clamp(outR, 0.0, 1.0) * 255.0);
            byte rg = (byte)Math.Round(Math.Clamp(outG, 0.0, 1.0) * 255.0);
            byte rb = (byte)Math.Round(Math.Clamp(outB, 0.0, 1.0) * 255.0);

            return (uint)(oa << 24 | rr << 16 | rg << 8 | rb);
        }

        private static bool IsXInBounds(int x, StrokeContext ctx)
            => (uint)x < (uint)ctx.Surface.Width;

        private static bool IsYInBounds(int y, StrokeContext ctx)
            => (uint)y < (uint)ctx.Surface.Height;
    }
}
