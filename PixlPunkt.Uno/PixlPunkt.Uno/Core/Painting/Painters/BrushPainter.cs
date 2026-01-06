using PixlPunkt.Uno.Core.Coloring.Helpers;
using PixlPunkt.Uno.Core.Tools.Settings;

namespace PixlPunkt.Uno.Core.Painting.Painters
{
    /// <summary>
    /// Standard brush painting strategy - blends foreground color over existing pixels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BrushPainter"/> implements the classic paint brush behavior:
    /// </para>
    /// <list type="bullet">
    /// <item>Blends foreground color over existing pixels using Porter-Duff "source over".</item>
    /// <item>Respects brush opacity, density, and shape from <see cref="StrokeContext"/>.</item>
    /// <item>Uses max-alpha accumulation to prevent over-blending during continuous strokes.</item>
    /// <item>Optimizes hard-opaque brushes by skipping already-touched pixels.</item>
    /// <item>Respects selection mask when active (only paints inside selection).</item>
    /// </list>
    /// </remarks>
    public sealed class BrushPainter : PainterBase
    {
        private readonly BrushToolSettings _settings;

        /// <summary>
        /// Creates a new BrushPainter bound to the specified settings.
        /// </summary>
        /// <param name="settings">The brush tool settings to use.</param>
        public BrushPainter(BrushToolSettings settings)
        {
            _settings = settings;
        }

        /// <inheritdoc/>
        public override bool NeedsSnapshot => false;

        /// <inheritdoc/>
        public override void StampAt(int cx, int cy, StrokeContext ctx)
        {
            // Use the painter's Surface for both bounds checking and pixel operations
            // to ensure consistency. The ctx.Surface should be the same, but we use
            // our Surface directly for all operations to guarantee correctness.
            if (Surface == null) return;

            int surfaceWidth = Surface.Width;
            int surfaceHeight = Surface.Height;

            bool isHardOpaque = ctx.BrushDensity == 255 && ctx.BrushOpacity == 255;

            foreach (var (dx, dy) in ctx.BrushOffsets)
            {
                byte effA = ctx.ComputeAlphaAtOffset(dx, dy);
                if (effA == 0) continue;

                int x = cx + dx, y = cy + dy;
                
                // Use painter's surface dimensions for bounds checking
                if ((uint)x >= (uint)surfaceWidth || (uint)y >= (uint)surfaceHeight) continue;

                // Check selection mask - skip pixels outside selection
                if (!ctx.IsInSelection(x, y)) continue;

                // Compute index using painter's surface dimensions
                int idx = (y * surfaceWidth + x) * 4;

                // Skip already-painted pixels for hard opaque brushes
                if (isHardOpaque && !Touched.Add(idx))
                    continue;

                uint before = ReadPixel(Surface.Pixels, idx);
                var rec = GetOrCreateAccumRec(idx, before);

                // Only apply if this stamp has higher alpha than previous
                if (effA <= rec.maxA)
                    continue;

                rec.maxA = effA;

                // Blend foreground over existing pixel
                uint src = (ctx.ForegroundColor & 0x00FFFFFFu) | ((uint)rec.maxA << 24);
                rec.after = ColorUtil.BlendOver(rec.before, src);

                CommitAccumRec(idx, rec);
            }
        }
    }
}
