using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Tools.Settings;

namespace PixlPunkt.Core.Painting.Painters
{
    /// <summary>
    /// Replacer painting strategy - conditionally replaces pixels matching a target color.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ReplacerPainter"/> implements color replacement:
    /// </para>
    /// <list type="bullet">
    /// <item>Only affects pixels that match the target (background) color.</item>
    /// <item>Can optionally ignore alpha when matching (RGB-only comparison).</item>
    /// <item>Interpolates between original and foreground based on brush alpha for soft transitions.</item>
    /// <item>Uses max-alpha accumulation to prevent over-replacement during continuous strokes.</item>
    /// </list>
    /// </remarks>
    public sealed class ReplacerPainter : PainterBase
    {
        private readonly ReplacerToolSettings _settings;

        /// <summary>
        /// Creates a new ReplacerPainter bound to the specified settings.
        /// </summary>
        /// <param name="settings">The replacer tool settings to use.</param>
        public ReplacerPainter(ReplacerToolSettings settings)
        {
            _settings = settings;
        }

        /// <inheritdoc/>
        public override bool NeedsSnapshot => false;

        /// <inheritdoc/>
        public override void StampAt(int cx, int cy, StrokeContext ctx)
        {
            // Use the painter's Surface for both bounds checking and pixel operations
            // to ensure consistency with symmetry support.
            if (Surface == null) return;

            int surfaceWidth = Surface.Width;
            int surfaceHeight = Surface.Height;

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

                uint before = ReadPixel(Surface.Pixels, idx);
                var rec = GetOrCreateAccumRec(idx, before);

                // Only apply if this stamp has higher alpha than previous
                if (effA <= rec.maxA)
                    continue;

                rec.maxA = effA;

                // Only replace if pixel matches target color
                if (ReplaceMatch(rec.before, ctx.BackgroundColor))
                {
                    // Interpolate from original to foreground based on alpha
                    rec.after = ColorUtil.LerpRgbKeepAlpha(rec.before, ctx.ForegroundColor, rec.maxA);
                    CommitAccumRec(idx, rec);
                }
            }
        }

        /// <summary>
        /// Determines if the current pixel matches the target color for replacement.
        /// </summary>
        /// <param name="current">Current pixel color (BGRA).</param>
        /// <param name="target">Target color to match (BGRA).</param>
        /// <returns>True if the pixel should be replaced.</returns>
        private bool ReplaceMatch(uint current, uint target)
        {
            // Check RGB match
            if (!ColorUtil.RgbEqual(current, target))
                return false;

            // If ignoring alpha, any alpha value matches
            if (_settings.IgnoreAlpha)
                return true;

            // Otherwise require fully opaque pixel
            return (current >> 24) == 255;
        }
    }
}
