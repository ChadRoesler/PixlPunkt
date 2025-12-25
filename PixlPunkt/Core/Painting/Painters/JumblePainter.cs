using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Painting.Helpers;
using PixlPunkt.Core.Tools.Settings;

namespace PixlPunkt.Core.Painting.Painters
{
    /// <summary>
    /// Jumble painting strategy - randomly swaps pixel colors within the brush footprint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="JumblePainter"/> creates a pixel-shuffling effect by randomly exchanging colors:
    /// </para>
    /// <list type="bullet">
    /// <item>Collects candidate pixels within brush radius weighted by radial falloff.</item>
    /// <item>Pairs pixels using locality-biased random selection (nearby pixels more likely).</item>
    /// <item>Swaps colors between paired pixels, recording changes for undo.</item>
    /// <item>Strength controls number of swap events per stamp.</item>
    /// </list>
    /// <para>
    /// Unlike other painters, JumblePainter tracks its own before/after state dictionaries
    /// instead of using the standard accumulation system, since swaps affect pairs of pixels.
    /// </para>
    /// </remarks>
    public sealed class JumblePainter : PainterBase
    {
        private readonly JumbleToolSettings _settings;
        private readonly Random _rng = new();

        /// <summary>Original pixel values before jumble swaps (by byte index).</summary>
        private Dictionary<int, uint>? _jumbleBefore;

        /// <summary>Modified pixel values after jumble swaps (by byte index).</summary>
        private Dictionary<int, uint>? _jumbleAfter;

        /// <summary>
        /// Creates a new JumblePainter bound to the specified settings.
        /// </summary>
        /// <param name="settings">The jumble tool settings to use.</param>
        public JumblePainter(JumbleToolSettings settings)
        {
            _settings = settings;
        }

        /// <inheritdoc/>
        public override bool NeedsSnapshot => true;

        /// <inheritdoc/>
        public override void Begin(RasterLayer layer, byte[]? snapshot)
        {
            base.Begin(layer, snapshot);
            _jumbleBefore = new Dictionary<int, uint>();
            _jumbleAfter = new Dictionary<int, uint>();
        }

        /// <inheritdoc/>
        public override void StampAt(int cx, int cy, StrokeContext ctx)
        {
            if (Surface == null) return;
            JumbleAt(cx, cy, ctx);
        }

        /// <inheritdoc/>
        public override void StampLine(int x0, int y0, int x1, int y1, StrokeContext ctx)
        {
            // Use same stride-based approach as base but call JumbleAt
            int dx = x1 - x0, dy = y1 - y0;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

            if (steps == 0)
            {
                JumbleAt(x0, y0, ctx);
                return;
            }

            int stride = Math.Max(1, ctx.BrushSize / 3);
            double sx = dx / (double)steps, sy = dy / (double)steps;

            double x = x0, y = y0;
            for (int i = 0; i <= steps; i += stride)
            {
                JumbleAt((int)Math.Round(x), (int)Math.Round(y), ctx);
                x += sx * stride;
                y += sy * stride;
            }
        }

        /// <inheritdoc/>
        public override IRenderResult? End(string description = "Jumble", Icon icon = Icon.History)
        {
            if (Layer == null)
            {
                CleanupBuffers();
                return null;
            }

            var item = new PixelChangeItem(Layer, description, icon);

            // Collect jumble changes
            if (_jumbleAfter != null && _jumbleAfter.Count > 0)
            {
                foreach (var (idx, after) in _jumbleAfter)
                {
                    uint before = (_jumbleBefore != null && _jumbleBefore.TryGetValue(idx, out var b))
                        ? b
                        : (Snapshot != null ? ReadPixel(Snapshot, idx) : 0u);

                    if (before != after)
                        item.Add(idx, before, after);
                }
            }

            CleanupBuffers();

            return item.IsEmpty ? null : item;
        }

        /// <summary>
        /// Cleans up all per-stroke buffers.
        /// </summary>
        private void CleanupBuffers()
        {
            _jumbleBefore = null;
            _jumbleAfter = null;
            Accum.Clear();
            Touched.Clear();
            Layer = null;
            Surface = null;
            Snapshot = null;
        }

        /// <summary>
        /// Performs localized jumble (random pair swapping) within brush radius.
        /// </summary>
        private void JumbleAt(int cx, int cy, StrokeContext ctx)
        {
            if (Surface == null) return;

            int W = Surface.Width, H = Surface.Height;
            int sz = Math.Max(1, ctx.BrushSize);

            // Use consistent radius calculation that matches BrushMaskCache offsets
            int R = StrokeUtil.BrushRadius(sz);
            if (R <= 0) R = 1; // Ensure at least some radius for size 1-2

            int x0 = Math.Max(0, cx - R), y0 = Math.Max(0, cy - R);
            int x1 = Math.Min(W - 1, cx + R), y1 = Math.Min(H - 1, cy + R);

            // Collect candidates with radial weights
            var idx = new List<int>();
            var posX = new List<int>();
            var posY = new List<int>();
            var wRad = new List<double>();

            double falloffGamma = _settings.FalloffGamma;
            bool includeTransparent = _settings.IncludeTransparent;

            for (int y = y0; y <= y1; y++)
            {
                int dy = y - cy;
                for (int x = x0; x <= x1; x++)
                {
                    int dx = x - cx;
                    double w = StrokeUtil.RadialFalloffWeight(dx, dy, R + 1, falloffGamma);
                    if (w <= 0) continue;

                    // Check selection mask - skip pixels outside selection
                    if (!ctx.IsInSelection(x, y)) continue;

                    int i4 = (y * W + x) * 4;
                    uint live = ReadPixel(Surface.Pixels, i4);

                    if (!includeTransparent && (live >> 24) == 0)
                        continue;

                    idx.Add(i4);
                    posX.Add(x);
                    posY.Add(y);
                    wRad.Add(w);
                    CaptureBeforeOnce(i4);
                }
            }

            int n = idx.Count;
            if (n < 2) return;

            // Build CDF for weighted random selection
            var cdf = StrokeUtil.BuildCdf(wRad, out double total);
            if (total <= 0) return;

            // Determine number of swap events based on strength
            float strength = _settings.StrengthPercent / 100f;
            int events = StrokeUtil.EventsPerIteration(n, strength);
            var used = new HashSet<int>();
            double locality = _settings.LocalityPercent / 100.0;

            for (int e = 0; e < events; e++)
            {
                // Select source pixel
                int iSrc;
                int tries = 0;
                do
                {
                    iSrc = StrokeUtil.SampleIndex(_rng, cdf, total);
                    if (iSrc < 0) return;
                } while (used.Contains(iSrc) && ++tries < 12);

                if (tries >= 12) break;

                // Find swap partner using locality-biased selection
                const int partnerAttempts = 10;
                int iDst = -1;
                for (int k = 0; k < partnerAttempts; k++)
                {
                    int cand = StrokeUtil.SampleIndex(_rng, cdf, total);
                    if (cand < 0 || cand == iSrc || used.Contains(cand)) continue;

                    double pairDx = posX[cand] - posX[iSrc];
                    double pairDy = posY[cand] - posY[iSrc];
                    double dist = Math.Sqrt(pairDx * pairDx + pairDy * pairDy);

                    double accept = StrokeUtil.LocalityAcceptance(dist, R + 1, locality);

                    if (_rng.NextDouble() < accept)
                    {
                        iDst = cand;
                        break;
                    }
                }
                if (iDst < 0) continue;

                // Perform swap
                int ia = idx[iSrc];
                int ib = idx[iDst];

                uint ca = ReadPixel(Surface.Pixels, ia);
                uint cb = ReadPixel(Surface.Pixels, ib);

                if (ca == cb)
                {
                    used.Add(iSrc);
                    used.Add(iDst);
                    continue;
                }

                WritePixel(Surface.Pixels, ia, cb);
                WritePixel(Surface.Pixels, ib, ca);

                _jumbleAfter![ia] = cb;
                _jumbleAfter[ib] = ca;

                used.Add(iSrc);
                used.Add(iDst);
            }
        }

        /// <summary>
        /// Captures original pixel value once for jumble history tracking.
        /// </summary>
        private void CaptureBeforeOnce(int idx4)
        {
            if (_jumbleBefore == null) return;
            if (!_jumbleBefore.ContainsKey(idx4))
                _jumbleBefore[idx4] = ReadPixel(Surface!.Pixels, idx4);
        }
    }
}
