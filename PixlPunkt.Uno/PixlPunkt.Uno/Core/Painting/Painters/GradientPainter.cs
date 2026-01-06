using System.Collections.Generic;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Tools.Settings;

namespace PixlPunkt.Uno.Core.Painting.Painters
{
    /// <summary>
    /// Gradient painting strategy - cycles pixel colors through a palette sequence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GradientPainter"/> implements color cycling through a user-defined palette:
    /// </para>
    /// <list type="bullet">
    /// <item>Maintains a lookup table mapping each palette color to the next in sequence.</item>
    /// <item>When painting over a pixel matching a palette color, replaces it with the next color.</item>
    /// <item>Supports looping (wraps from last to first) or linear (stops at first) mode.</item>
    /// <item>Can optionally ignore non-opaque pixels (only process fully opaque).</item>
    /// </list>
    /// </remarks>
    public sealed class GradientPainter : PainterBase
    {
        private readonly GradientToolSettings _settings;

        /// <summary>Map from current RGB to next RGB in the gradient sequence.</summary>
        private readonly Dictionary<int, int> _gradMapRgbNext = new();

        /// <summary>
        /// Creates a new GradientPainter bound to the specified settings.
        /// </summary>
        /// <param name="settings">The gradient tool settings to use.</param>
        public GradientPainter(GradientToolSettings settings)
        {
            _settings = settings;
        }

        /// <inheritdoc/>
        public override bool NeedsSnapshot => false;

        /// <inheritdoc/>
        public override void Begin(RasterLayer layer, byte[]? snapshot)
        {
            base.Begin(layer, snapshot);

            // Rebuild gradient map from current settings
            RebuildGradientMap();
        }

        /// <inheritdoc/>
        public override void StampAt(int cx, int cy, StrokeContext ctx)
        {
            if (Surface == null || _gradMapRgbNext.Count == 0) return;

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

                // Extract current RGB
                int curRgb = (int)(rec.before & 0x00FFFFFFu);
                byte curAlpha = (byte)(rec.before >> 24);

                // Skip non-opaque pixels if ignoreAlpha is enabled
                if (_settings.IgnoreAlpha && curAlpha != 255)
                    continue;

                // Look up next color in gradient sequence
                if (_gradMapRgbNext.TryGetValue(curRgb, out int nextRgb))
                {
                    rec.after = (uint)((curAlpha << 24) | nextRgb);
                    CommitAccumRec(idx, rec);
                }
            }
        }

        /// <summary>
        /// Rebuilds the gradient transition map from the current palette and loop settings.
        /// </summary>
        private void RebuildGradientMap()
        {
            _gradMapRgbNext.Clear();

            var colors = _settings.Colors;
            int n = colors.Count;
            if (n == 0) return;

            if (_settings.Loop)
            {
                // Loop mode: each color maps to the previous, with wrap-around
                if (n >= 2)
                {
                    for (int i = n - 1; i >= 0; --i)
                    {
                        int j = (i - 1 + n) % n;
                        int fromRgb = (int)(colors[i] & 0x00FFFFFFu);
                        int toRgb = (int)(colors[j] & 0x00FFFFFFu);
                        if (fromRgb == toRgb) continue;
                        _gradMapRgbNext[fromRgb] = toRgb;
                    }
                }
            }
            else
            {
                // Linear mode: each color maps to the previous, no wrap
                if (n >= 2)
                {
                    for (int i = n - 1; i >= 1; --i)
                    {
                        int j = i - 1;
                        int fromRgb = (int)(colors[i] & 0x00FFFFFFu);
                        int toRgb = (int)(colors[j] & 0x00FFFFFFu);
                        if (fromRgb == toRgb) continue;
                        _gradMapRgbNext[fromRgb] = toRgb;
                    }
                }
            }
        }
    }
}
