using System;
using FluentIcons.Common;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Tools.Settings;

namespace PixlPunkt.Core.Painting.Painters
{
    /// <summary>
    /// Smudge painting strategy - pulls colors along the stroke direction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SmudgePainter"/> creates a color-smearing effect by sampling behind the stroke:
    /// </para>
    /// <list type="bullet">
    /// <item>Maintains 4 logical float buffers (R, G, B, A) for smooth progressive blending.</item>
    /// <item>Samples source color from one pixel back along the stroke direction.</item>
    /// <item>Blends source into destination using strength-weighted radial falloff.</item>
    /// <item>Supports soft mode (smooth gradients) and hard-edge mode (pixel-art style).</item>
    /// <item>Handles transparency smearing (wedge growth) when enabled.</item>
    /// </list>
    /// <para>
    /// Smudge requires snapshot for baseline comparison but primarily operates on logical buffers
    /// to maintain smooth progression across multiple dabs.
    /// </para>
    /// </remarks>
    public sealed class SmudgePainter : PainterBase
    {
        private readonly SmudgeToolSettings _settings;

        /// <summary>Logical alpha channel buffer (0..1 float).</summary>
        private float[]? _alphaLogic;
        /// <summary>Logical red channel buffer (0..1 float).</summary>
        private float[]? _rLogic;
        /// <summary>Logical green channel buffer (0..1 float).</summary>
        private float[]? _gLogic;
        /// <summary>Logical blue channel buffer (0..1 float).</summary>
        private float[]? _bLogic;

        /// <summary>Tracks if stroke has a previous position for direction calculation.</summary>
        private bool _hasLast;
        /// <summary>Previous stamp X coordinate.</summary>
        private int _lastX;
        /// <summary>Previous stamp Y coordinate.</summary>
        private int _lastY;

        /// <summary>Logical alpha threshold below which pixels are "empty" (8/255).</summary>
        private const float ALPHA_EMPTY = 8f / 255f;
        /// <summary>Threshold for hard-edge alpha binarization (76/255).</summary>
        private const float HARD_ALPHA_THRESHOLD = 76f / 255f;
        /// <summary>Max channel difference for hard-edge color snapping.</summary>
        private const int HARD_COLOR_TOLERANCE = 24;
        /// <summary>Minimum strength for color adoption in hard mode.</summary>
        private const float HARD_STRENGTH_CUTOFF = 0.02f;

        /// <summary>
        /// Creates a new SmudgePainter bound to the specified settings.
        /// </summary>
        /// <param name="settings">The smudge tool settings to use.</param>
        public SmudgePainter(SmudgeToolSettings settings)
        {
            _settings = settings;
        }

        /// <inheritdoc/>
        public override bool NeedsSnapshot => true;

        /// <inheritdoc/>
        public override void Begin(RasterLayer layer, byte[]? snapshot)
        {
            base.Begin(layer, snapshot);

            int total = Surface!.Width * Surface.Height;

            // Allocate logical buffers
            _alphaLogic = new float[total];
            _rLogic = new float[total];
            _gLogic = new float[total];
            _bLogic = new float[total];

            // Seed from current surface
            var pixels = Surface.Pixels;
            for (int i = 0, idx = 0; idx < total; idx++, i += 4)
            {
                _bLogic[idx] = pixels[i + 0] / 255f;
                _gLogic[idx] = pixels[i + 1] / 255f;
                _rLogic[idx] = pixels[i + 2] / 255f;
                _alphaLogic[idx] = pixels[i + 3] / 255f;
            }

            _hasLast = false;
        }

        /// <inheritdoc/>
        public override void StampAt(int cx, int cy, StrokeContext ctx)
        {
            if (Surface == null) return;

            // First stamp: just record position
            if (!_hasLast)
            {
                _hasLast = true;
                _lastX = cx;
                _lastY = cy;
                return;
            }

            int dxStroke = cx - _lastX;
            int dyStroke = cy - _lastY;
            _lastX = cx;
            _lastY = cy;

            double len = Math.Sqrt(dxStroke * dxStroke + dyStroke * dyStroke);
            if (len < 0.5) return; // No significant motion

            double ux = dxStroke / len;
            double uy = dyStroke / len;

            SmudgeDab(cx, cy, ux, uy, ctx);
        }

        /// <inheritdoc/>
        public override void StampLine(int x0, int y0, int x1, int y1, StrokeContext ctx)
        {
            // Smudge needs continuous direction tracking, so we just call StampAt repeatedly
            int dx = x1 - x0, dy = y1 - y0;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

            if (steps == 0)
            {
                StampAt(x0, y0, ctx);
                return;
            }

            int stride = Math.Max(1, ctx.BrushSize / 3);
            double sx = dx / (double)steps, sy = dy / (double)steps;

            double x = x0, y = y0;
            for (int i = 0; i <= steps; i += stride)
            {
                StampAt((int)Math.Round(x), (int)Math.Round(y), ctx);
                x += sx * stride;
                y += sy * stride;
            }
        }

        /// <inheritdoc/>
        public override IRenderResult? End(string description = "Smudge", Icon icon = Icon.History)
        {
            if (Layer == null)
            {
                CleanupBuffers();
                return null;
            }

            // Use the passed icon, but fall back to HandDraw if the default History icon is passed
            var effectiveIcon = icon == Icon.History ? Icon.HandDraw : icon;
            var item = new PixelChangeItem(Layer, description, effectiveIcon);

            // Diff snapshot vs current surface
            if (Snapshot != null && Surface != null)
            {
                int total = Surface.Width * Surface.Height;
                var current = Surface.Pixels;

                for (int p = 0; p < total; p++)
                {
                    int idx4 = p * 4;
                    uint before = ReadPixel(Snapshot, idx4);
                    uint after = ReadPixel(current, idx4);

                    if (before != after)
                        item.Add(idx4, before, after);
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
            _alphaLogic = null;
            _rLogic = null;
            _gLogic = null;
            _bLogic = null;
            _hasLast = false;
            Layer = null;
            Surface = null;
            Snapshot = null;
            Accum.Clear();
            Touched.Clear();
        }

        /// <summary>
        /// Performs a single smudge dab at the specified position with stroke direction.
        /// </summary>
        private void SmudgeDab(int cx, int cy, double ux, double uy, StrokeContext ctx)
        {
            if (Surface == null || _alphaLogic == null) return;

            var pixels = Surface.Pixels;
            int w = Surface.Width, h = Surface.Height;

            // Get brush footprint and compute max radius for falloff
            var offsets = ctx.BrushOffsets;
            if (offsets == null || offsets.Count == 0) return;

            double maxR = 1.0;
            foreach (var (ox, oy) in offsets)
            {
                double d = Math.Sqrt(ox * ox + oy * oy);
                if (d > maxR) maxR = d;
            }

            float baseStrength = _settings.StrengthPercent / 100f;
            double falloffGamma = _settings.FalloffGamma;
            bool hardEdge = _settings.HardEdge;
            bool blendTransparent = _settings.BlendOnTransparent;

            const double pullStep = 1.0;

            foreach (var (ox, oy) in offsets)
            {
                int dx = cx + ox, dy = cy + oy;
                if ((uint)dx >= (uint)w || (uint)dy >= (uint)h) continue;

                // Check selection mask - skip pixels outside selection
                if (!ctx.IsInSelection(dx, dy)) continue;

                // Radial falloff
                double dist = Math.Sqrt(ox * ox + oy * oy);
                double radial = 1.0 - dist / (maxR + 1e-6);
                if (radial <= 0.0) continue;

                if (falloffGamma > 0.0)
                {
                    double exp = 1.0 + falloffGamma * 3.0;
                    radial = Math.Pow(radial, exp);
                }

                // Brush mask alpha
                float mask = ctx.ComputeAlphaAtOffset(ox, oy) / 255f;
                if (mask <= 0.0f) continue;

                float t = baseStrength * mask * (float)radial;
                if (t <= 0.001f) continue;

                // Sample source pixel (one step back along stroke)
                double sxF = dx - ux * pullStep;
                double syF = dy - uy * pullStep;
                int sx = (int)Math.Round(sxF);
                int sy = (int)Math.Round(syF);
                if ((uint)sx >= (uint)w || (uint)sy >= (uint)h) continue;

                int dstLin = dy * w + dx;
                int srcLin = sy * w + sx;
                int dstIdx4 = dstLin * 4;

                // Current logical values
                float dstLogicR = _rLogic![dstLin];
                float dstLogicG = _gLogic![dstLin];
                float dstLogicB = _bLogic![dstLin];
                float dstLogicA = _alphaLogic[dstLin];

                float srcLogicR = _rLogic[srcLin];
                float srcLogicG = _gLogic[srcLin];
                float srcLogicB = _bLogic[srcLin];
                float srcLogicA = _alphaLogic[srcLin];

                bool dstEmpty = dstLogicA <= ALPHA_EMPTY;
                bool srcEmpty = srcLogicA <= ALPHA_EMPTY;

                // Handle transparency
                if (!blendTransparent && (srcEmpty || dstEmpty))
                    continue;

                // Visible colors for hard-edge comparison
                byte origDstR = pixels[dstIdx4 + 2];
                byte origDstG = pixels[dstIdx4 + 1];
                byte origDstB = pixels[dstIdx4 + 0];
                byte origSrcR = pixels[srcLin * 4 + 2];
                byte origSrcG = pixels[srcLin * 4 + 1];
                byte origSrcB = pixels[srcLin * 4 + 0];

                if (blendTransparent)
                {
                    if (srcEmpty && dstEmpty)
                        continue;
                    else if (srcEmpty && !dstEmpty)
                    {
                        // Outward smear
                        srcLogicR = dstLogicR;
                        srcLogicG = dstLogicG;
                        srcLogicB = dstLogicB;
                        srcLogicA = dstLogicA;
                        origSrcR = origDstR;
                        origSrcG = origDstG;
                        origSrcB = origDstB;
                    }
                    else if (!srcEmpty && dstEmpty)
                    {
                        // Pull solid into empty
                        dstLogicR = srcLogicR;
                        dstLogicG = srcLogicG;
                        dstLogicB = srcLogicB;
                        origDstR = origSrcR;
                        origDstG = origSrcG;
                        origDstB = origSrcB;
                    }
                }

                // Soft logical progression
                float newLogicR = Math.Clamp(dstLogicR + t * (srcLogicR - dstLogicR), 0f, 1f);
                float newLogicG = Math.Clamp(dstLogicG + t * (srcLogicG - dstLogicG), 0f, 1f);
                float newLogicB = Math.Clamp(dstLogicB + t * (srcLogicB - dstLogicB), 0f, 1f);
                float newLogicA = Math.Clamp(dstLogicA + t * (srcLogicA - dstLogicA), 0f, 1f);

                // Update logical state
                _rLogic[dstLin] = newLogicR;
                _gLogic[dstLin] = newLogicG;
                _bLogic[dstLin] = newLogicB;
                _alphaLogic[dstLin] = newLogicA;

                // Compute visible output
                byte softR = (byte)Math.Round(newLogicR * 255f);
                byte softG = (byte)Math.Round(newLogicG * 255f);
                byte softB = (byte)Math.Round(newLogicB * 255f);

                byte outR, outG, outB, outA;

                if (hardEdge)
                {
                    // Hard-edge snapping
                    int diffSrc = Math.Max(Math.Max(Math.Abs(softR - origSrcR), Math.Abs(softG - origSrcG)), Math.Abs(softB - origSrcB));
                    int diffDst = Math.Max(Math.Max(Math.Abs(softR - origDstR), Math.Abs(softG - origDstG)), Math.Abs(softB - origDstB));

                    bool canAdopt = t >= HARD_STRENGTH_CUTOFF && !srcEmpty;

                    if (canAdopt && diffSrc + HARD_COLOR_TOLERANCE < diffDst)
                    {
                        outR = origSrcR; outG = origSrcG; outB = origSrcB;
                    }
                    else if (canAdopt && diffDst + HARD_COLOR_TOLERANCE < diffSrc)
                    {
                        outR = origDstR; outG = origDstG; outB = origDstB;
                    }
                    else
                    {
                        outR = origDstR; outG = origDstG; outB = origDstB;
                    }

                    // Alpha quantization
                    if (newLogicA <= ALPHA_EMPTY) outA = 0;
                    else if (newLogicA >= HARD_ALPHA_THRESHOLD) outA = 255;
                    else outA = 0;

                    // Keep opaque if both sides solid
                    if (srcLogicA >= HARD_ALPHA_THRESHOLD && dstLogicA >= HARD_ALPHA_THRESHOLD)
                        outA = 255;
                }
                else
                {
                    // Soft mode
                    outR = softR;
                    outG = softG;
                    outB = softB;
                    outA = (byte)Math.Round(newLogicA * 255f);
                }

                // Write visible result
                pixels[dstIdx4 + 2] = outR;
                pixels[dstIdx4 + 1] = outG;
                pixels[dstIdx4 + 0] = outB;
                pixels[dstIdx4 + 3] = outA;
            }
        }
    }
}
