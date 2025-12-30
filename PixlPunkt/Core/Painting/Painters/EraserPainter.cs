using PixlPunkt.Core.Tools.Settings;

namespace PixlPunkt.Core.Painting.Painters
{
    /// <summary>
    /// Eraser painting strategy - reduces pixel alpha while preserving RGB values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="EraserPainter"/> implements pixel erasure by reducing alpha:
    /// </para>
    /// <list type="bullet">
    /// <item>Subtracts effective brush alpha from existing pixel alpha.</item>
    /// <item>Preserves RGB values (doesn't change color, only transparency).</item>
    /// <item>Fully erased pixels (alpha=0) become completely transparent.</item>
    /// <item>Uses max-alpha accumulation to prevent over-erasing during continuous strokes.</item>
    /// </list>
    /// </remarks>
    public sealed class EraserPainter : PainterBase
    {
        private readonly EraserToolSettings _settings;

        /// <summary>
        /// Creates a new EraserPainter bound to the specified settings.
        /// </summary>
        /// <param name="settings">The eraser tool settings to use.</param>
        public EraserPainter(EraserToolSettings settings)
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

                // Reduce alpha by the effective brush alpha
                byte currentAlpha = (byte)(rec.before >> 24);
                int newAlpha = currentAlpha - rec.maxA;
                if (newAlpha < 0) newAlpha = 0;

                // If fully erased, set to transparent (0), otherwise preserve RGB
                rec.after = (newAlpha == 0)
                    ? 0u
                    : ((uint)newAlpha << 24) | (rec.before & 0x00FFFFFFu);

                CommitAccumRec(idx, rec);
            }
        }
    }
}
