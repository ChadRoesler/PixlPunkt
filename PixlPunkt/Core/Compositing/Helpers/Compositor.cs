using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Imaging;

namespace PixlPunkt.Core.Compositing.Helpers
{
    /// <summary>
    /// Provides static methods for compositing multiple raster layers with blend modes, opacity, and effects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Compositor is responsible for rendering the final image by blending all visible layers from
    /// bottom to top, applying their blend modes, opacity, and effects. It uses a scratch buffer to
    /// apply effects without modifying the original layer data.
    /// </para>
    /// <para>
    /// Supported blend modes include: Normal, Multiply, Screen, Overlay, HardLight, Add, Subtract,
    /// Difference, Darken, Lighten, and Invert. All blending is performed in normalized float space
    /// (0.0-1.0) with proper alpha compositing.
    /// </para>
    /// <para>
    /// The compositor is stateless except for a reusable scratch buffer for performance optimization.
    /// It's safe to call from multiple threads if operating on different destination surfaces.
    /// </para>
    /// </remarks>
    public static class Compositor
    {
        /// <summary>
        /// Reusable scratch surface for applying effects without mutating layer data.
        /// Automatically resized as needed to match canvas dimensions.
        /// </summary>
        private static PixelSurface? _fxScratch;

        /// <summary>
        /// Precomputed lookup table for byte to normalized float conversion (0-255 → 0.0-1.0).
        /// </summary>
        private static readonly float[] ByteToFloat = new float[256];

        /// <summary>
        /// Static constructor to initialize lookup tables.
        /// </summary>
        static Compositor()
        {
            for (int i = 0; i < 256; i++)
            {
                ByteToFloat[i] = i / 255f;
            }
        }

        /// <summary>
        /// Composites multiple raster layers into a destination surface from bottom to top.
        /// </summary>
        /// <param name="layers">
        /// The layers to composite, in bottom-to-top order. Invisible layers are automatically skipped.
        /// </param>
        /// <param name="dest">
        /// The destination surface to render into. Must not be null. The surface is cleared
        /// to transparent black before compositing begins.
        /// </param>
        /// <remarks>
        /// <para>
        /// Algorithm:
        /// 1. Clear destination to transparent (0,0,0,0)
        /// 2. For each visible layer (bottom to top):
        ///    a. If layer has no enabled effects, blit directly to destination
        ///    b. If layer has effects, copy to scratch buffer, apply effects in order, then blit
        /// 3. Blitting respects layer opacity, blend mode, and layer mask
        /// </para>
        /// <para>
        /// Layers are skipped if they are null, invisible, have null surfaces, or have zero opacity.
        /// </para>
        /// <para>
        /// Effects are applied in the order they appear in <see cref="RasterLayer.Effects"/>,
        /// but the list is reversed before application (so first effect in list is applied first).
        /// </para>
        /// </remarks>
        public static void CompositeLinear(IEnumerable<RasterLayer> layers, PixelSurface dest)
        {
            if (dest == null) return;

            var dp = dest.Pixels;
            Array.Clear(dp, 0, dp.Length);

            if (layers == null) return;

            int canvasW = dest.Width;
            int canvasH = dest.Height;

            foreach (var l in layers)
            {
                if (l == null || !l.Visible || l.Surface == null || l.Opacity <= 0)
                    continue;

                var layerSurf = l.Surface;
                int w = Math.Min(layerSurf.Width, canvasW);
                int h = Math.Min(layerSurf.Height, canvasH);

                // IMPORTANT: Take a snapshot of the effects collection to avoid
                // "Collection was modified" exceptions if another thread modifies it
                List<LayerEffectBase>? enabledEffects = null;
                var effects = l.Effects;
                if (effects != null)
                {
                    lock (effects)
                    {
                        enabledEffects = effects.Where(e => e.IsEnabled).ToList();
                    }
                }

                // If no enabled effects, just blit the layer as-is
                if (enabledEffects == null || enabledEffects.Count == 0)
                {
                    Blit(layerSurf, dest, l.Opacity, l.Blend, l.Mask);
                    continue;
                }
                enabledEffects.Reverse();
                // Ensure scratch surface exists and is at least canvas-sized
                if (_fxScratch == null || _fxScratch.Width != canvasW || _fxScratch.Height != canvasH)
                {
                    _fxScratch = new PixelSurface(canvasW, canvasH);
                }

                // Copy layer pixels into scratch (only overlapping region)
                var srcBytes = layerSurf.Pixels;
                var fxBytes = _fxScratch.Pixels;

                int rowBytes = w * 4;
                for (int y = 0; y < h; y++)
                {
                    int srcIndex = layerSurf.IndexOf(0, y);
                    int dstIndex = _fxScratch.IndexOf(0, y);
                    Buffer.BlockCopy(srcBytes, srcIndex, fxBytes, dstIndex, rowBytes);
                }

                // Treat scratch buffer as uint[] for effect math
                var fxSpan = MemoryMarshal.Cast<byte, uint>(fxBytes.AsSpan());

                // Run all enabled effects in order, in-place
                foreach (var fx in enabledEffects)
                {
                    fx.Apply(fxSpan, canvasW, canvasH);
                }

                // Now blend the scratch surface into dest
                Blit(_fxScratch, dest, l.Opacity, l.Blend, l.Mask);
            }
        }

        /// <summary>
        /// Blits (blends) a source surface into a destination surface with the specified opacity, blend mode, and optional mask.
        /// </summary>
        /// <param name="src">Source surface to blend from.</param>
        /// <param name="dst">Destination surface to blend into.</param>
        /// <param name="opacity">Global opacity multiplier (0-255). 255 = fully opaque, 0 = fully transparent.</param>
        /// <param name="mode">The blend mode to use when combining colors.</param>
        /// <param name="mask">Optional layer mask. If provided, pixel alpha is multiplied by mask value.</param>
        internal static void Blit(PixelSurface src, PixelSurface dst, byte opacity, BlendMode mode, LayerMask? mask = null)
        {
            int w = Math.Min(src.Width, dst.Width);
            int h = Math.Min(src.Height, dst.Height);

            var sp = src.Pixels;
            var dp = dst.Pixels;

            float layerOp = ByteToFloat[opacity];

            // Fast path for Normal blend mode (most common)
            if (mode == BlendMode.Normal)
            {
                BlitNormal(sp, dp, w, h, src.Width, dst.Width, layerOp, mask);
                return;
            }

            for (int y = 0; y < h; y++)
            {
                int si = src.IndexOf(0, y);
                int di = dst.IndexOf(0, y);
                for (int x = 0; x < w; x++)
                {
                    byte sb = sp[si + 0], sg = sp[si + 1], sr = sp[si + 2], sa = sp[si + 3];
                    byte db = dp[di + 0], dg = dp[di + 1], dr = dp[di + 2], da = dp[di + 3];

                    // Apply mask if present
                    float maskMult = 1.0f;
                    if (mask != null && mask.IsEnabled)
                    {
                        byte maskValue = mask.GetEffectiveMaskValue(x, y);
                        maskMult = ByteToFloat[maskValue];
                    }

                    float sAf = ByteToFloat[sa] * layerOp * maskMult;
                    if (sAf <= 0f)
                    {
                        si += 4; di += 4; continue;
                    }

                    float dAf = ByteToFloat[da];
                    float outAf = sAf + dAf * (1f - sAf);

                    // Use lookup table for byte->float conversion
                    float srN = ByteToFloat[sr], sgN = ByteToFloat[sg], sbN = ByteToFloat[sb];
                    float drN = ByteToFloat[dr], dgN = ByteToFloat[dg], dbN = ByteToFloat[db];

                    float fr, fg, fb;
                    switch (mode)
                    {
                        case BlendMode.Multiply:
                            fr = srN * drN; fg = sgN * dgN; fb = sbN * dbN;
                            break;
                        case BlendMode.Add:
                            fr = MathF.Min(1f, srN + drN);
                            fg = MathF.Min(1f, sgN + dgN);
                            fb = MathF.Min(1f, sbN + dbN);
                            break;
                        case BlendMode.Subtract:
                            fr = MathF.Max(0f, drN - srN);
                            fg = MathF.Max(0f, dgN - sgN);
                            fb = MathF.Max(0f, dbN - sbN);
                            break;
                        case BlendMode.Difference:
                            fr = MathF.Abs(drN - srN);
                            fg = MathF.Abs(dgN - sgN);
                            fb = MathF.Abs(dbN - sbN);
                            break;
                        case BlendMode.Darken:
                            fr = MathF.Min(srN, drN);
                            fg = MathF.Min(sgN, dgN);
                            fb = MathF.Min(sbN, dbN);
                            break;
                        case BlendMode.Lighten:
                            fr = MathF.Max(srN, drN);
                            fg = MathF.Max(sgN, dgN);
                            fb = MathF.Max(sbN, dbN);
                            break;
                        case BlendMode.Screen:
                            fr = 1f - (1f - srN) * (1f - drN);
                            fg = 1f - (1f - sgN) * (1f - dgN);
                            fb = 1f - (1f - sbN) * (1f - dbN);
                            break;
                        case BlendMode.Overlay:
                            fr = drN < 0.5f ? (2f * srN * drN) : (1f - 2f * (1f - srN) * (1f - drN));
                            fg = dgN < 0.5f ? (2f * sgN * dgN) : (1f - 2f * (1f - sgN) * (1f - dgN));
                            fb = dbN < 0.5f ? (2f * sbN * dbN) : (1f - 2f * (1f - sbN) * (1f - dbN));
                            break;
                        case BlendMode.HardLight:
                            fr = srN < 0.5f ? (2f * drN * srN) : (1f - 2f * (1f - drN) * (1f - srN));
                            fg = sgN < 0.5f ? (2f * dgN * sgN) : (1f - 2f * (1f - dgN) * (1f - sgN));
                            fb = sbN < 0.5f ? (2f * dbN * sbN) : (1f - 2f * (1f - dbN) * (1f - sbN));
                            break;
                        case BlendMode.Invert:
                            fr = 1f - drN;
                            fg = 1f - dgN;
                            fb = 1f - dbN;
                            break;
                        default: // Normal - handled by fast path above, but included for safety
                            fr = srN; fg = sgN; fb = sbN;
                            break;
                    }

                    float orN = (1f - sAf) * drN + sAf * fr;
                    float ogN = (1f - sAf) * dgN + sAf * fg;
                    float obN = (1f - sAf) * dbN + sAf * fb;

                    dp[di + 2] = (byte)Math.Clamp((int)(orN * 255f + 0.5f), 0, 255);
                    dp[di + 1] = (byte)Math.Clamp((int)(ogN * 255f + 0.5f), 0, 255);
                    dp[di + 0] = (byte)Math.Clamp((int)(obN * 255f + 0.5f), 0, 255);
                    dp[di + 3] = (byte)Math.Clamp((int)(outAf * 255f + 0.5f), 0, 255);

                    si += 4; di += 4;
                }
            }
        }

        /// <summary>
        /// Optimized blit for Normal blend mode (most common case).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BlitNormal(byte[] sp, byte[] dp, int w, int h, int srcWidth, int dstWidth, float layerOp, LayerMask? mask = null)
        {
            bool hasMask = mask != null && mask.IsEnabled;

            for (int y = 0; y < h; y++)
            {
                int si = y * srcWidth * 4;
                int di = y * dstWidth * 4;
                for (int x = 0; x < w; x++)
                {
                    byte sb = sp[si + 0], sg = sp[si + 1], sr = sp[si + 2], sa = sp[si + 3];

                    // Apply mask if present
                    float maskMult = 1.0f;
                    if (hasMask)
                    {
                        byte maskValue = mask!.GetEffectiveMaskValue(x, y);
                        maskMult = ByteToFloat[maskValue];
                    }

                    float sAf = ByteToFloat[sa] * layerOp * maskMult;
                    if (sAf <= 0f)
                    {
                        si += 4; di += 4; continue;
                    }

                    byte db = dp[di + 0], dg = dp[di + 1], dr = dp[di + 2], da = dp[di + 3];
                    float dAf = ByteToFloat[da];
                    float outAf = sAf + dAf * (1f - sAf);

                    float srN = ByteToFloat[sr], sgN = ByteToFloat[sg], sbN = ByteToFloat[sb];
                    float drN = ByteToFloat[dr], dgN = ByteToFloat[dg], dbN = ByteToFloat[db];

                    float inv = 1f - sAf;
                    float orN = inv * drN + sAf * srN;
                    float ogN = inv * dgN + sAf * sgN;
                    float obN = inv * dbN + sAf * sbN;

                    dp[di + 2] = (byte)(orN * 255f + 0.5f);
                    dp[di + 1] = (byte)(ogN * 255f + 0.5f);
                    dp[di + 0] = (byte)(obN * 255f + 0.5f);
                    dp[di + 3] = (byte)(outAf * 255f + 0.5f);

                    si += 4; di += 4;
                }
            }
        }
    }
}
