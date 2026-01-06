using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.History;
using PixlPunkt.Uno.Core.Imaging;
using PixlPunkt.Uno.Core.Painting.Dithering;
using PixlPunkt.Uno.Core.Tools.Settings;
using Windows.Graphics;
using static PixlPunkt.Uno.Core.Helpers.GraphicsStructHelper;
using GradientStop = PixlPunkt.Uno.Core.Tools.Settings.GradientStop;

namespace PixlPunkt.Uno.Core.Painting.Painters
{
    /// <summary>
    /// Renders gradients with various types and dithering styles.
    /// </summary>
    public sealed class GradientFillPainter
    {
        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Renders a gradient on the specified layer.
        /// </summary>
        /// <param name="layer">Target layer.</param>
        /// <param name="startX">Start point X (document coordinates).</param>
        /// <param name="startY">Start point Y (document coordinates).</param>
        /// <param name="endX">End point X (document coordinates).</param>
        /// <param name="endY">End point Y (document coordinates).</param>
        /// <param name="settings">Gradient fill settings.</param>
        /// <param name="foreground">Current foreground color (BGRA).</param>
        /// <param name="background">Current background color (BGRA).</param>
        /// <param name="selectionMask">Optional selection mask (returns true for selected pixels).</param>
        /// <returns>History item for undo support.</returns>
        public PixelChangeItem? Render(
            RasterLayer layer,
            int startX, int startY,
            int endX, int endY,
            GradientFillToolSettings settings,
            uint foreground, uint background,
            Func<int, int, bool>? selectionMask = null)
        {
            if (layer == null) return null;

            var surface = layer.Surface;
            int width = surface.Width;
            int height = surface.Height;
            var pixels = surface.Pixels;

            // Capture before state for undo
            byte[] before = (byte[])pixels.Clone();

            // Get gradient colors
            var stops = GetGradientStops(settings, foreground, background);
            if (stops.Length == 0) return null;

            // Calculate gradient parameters
            double dx = endX - startX;
            double dy = endY - startY;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length < 1) length = 1; // Prevent division by zero

            // Determine bounds to render (selection or full canvas)
            int minX = 0, minY = 0, maxX = width - 1, maxY = height - 1;

            // For error diffusion, we need to process in scanline order
            // and can't easily restrict to selection, so we process all
            // but only write to selected pixels
            bool useErrorDiffusion =
                settings.DitherStyle == DitherStyle.FloydSteinberg ||
                settings.DitherStyle == DitherStyle.Atkinson ||
                settings.DitherStyle == DitherStyle.Riemersma;

            if (useErrorDiffusion)
            {
                RenderWithErrorDiffusion(
                    pixels, width, height,
                    startX, startY, endX, endY,
                    settings, stops, selectionMask);
            }
            else
            {
                // Standard ordered/pattern dithering
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        // Check selection mask
                        if (selectionMask != null && !selectionMask(x, y))
                            continue;

                        // Calculate gradient position (t value 0-1)
                        double t = CalculateGradientT(
                            x, y, startX, startY, endX, endY,
                            settings.GradientType, length, dx, dy);

                        // Reverse if needed
                        if (settings.Reverse)
                            t = 1.0 - t;

                        // Sample gradient with dithering
                        uint color = DitherPatterns.SampleGradient(
                            stops, t, x, y,
                            settings.DitherStyle,
                            settings.DitherStrength,
                            settings.DitherScale);

                        // Apply opacity
                        if (settings.Opacity < 255)
                        {
                            color = ApplyOpacity(color, settings.Opacity);
                        }

                        // Blend with existing pixel
                        int idx = (y * width + x) * 4;
                        uint existing = ReadPixel(pixels, idx);
                        uint blended = BlendOver(existing, color);
                        WritePixel(pixels, idx, blended);
                    }
                }
            }

            // Create history item from before/after comparison
            byte[] after = (byte[])pixels.Clone();
            var rect = CreateRect(0, 0, width, height);
            return PixelChangeItem.FromRegion(layer, rect, before, after, "Gradient Fill", Icon.CalendarPattern);
        }

        // ====================================================================
        // GRADIENT STOPS
        // ====================================================================

        private static GradientStop[] GetGradientStops(
            GradientFillToolSettings settings,
            uint foreground, uint background)
        {
            return settings.ColorMode switch
            {
                GradientColorMode.WhiteToBlack => new[]
                {
                    new GradientStop(0.0, 0xFFFFFFFF), // White
                    new GradientStop(1.0, 0xFF000000)  // Black
                },
                GradientColorMode.BlackToWhite => new[]
                {
                    new GradientStop(0.0, 0xFF000000), // Black
                    new GradientStop(1.0, 0xFFFFFFFF)  // White
                },
                GradientColorMode.ForegroundToBackground => new[]
                {
                    new GradientStop(0.0, foreground),
                    new GradientStop(1.0, background)
                },
                GradientColorMode.BackgroundToForeground => new[]
                {
                    new GradientStop(0.0, background),
                    new GradientStop(1.0, foreground)
                },
                GradientColorMode.Custom => GetCustomStops(settings),
                _ => new[]
                {
                    new GradientStop(0.0, foreground),
                    new GradientStop(1.0, background)
                }
            };
        }

        private static GradientStop[] GetCustomStops(GradientFillToolSettings settings)
        {
            if (settings.CustomStops.Count == 0)
            {
                // Default to white-to-black if no custom stops
                return new[]
                {
                    new GradientStop(0.0, 0xFFFFFFFF),
                    new GradientStop(1.0, 0xFF000000)
                };
            }

            var stops = new GradientStop[settings.CustomStops.Count];
            for (int i = 0; i < settings.CustomStops.Count; i++)
            {
                stops[i] = settings.CustomStops[i];
            }
            return stops;
        }

        // ====================================================================
        // GRADIENT T CALCULATION
        // ====================================================================

        /// <summary>
        /// Calculates the gradient interpolation factor (t) for a pixel position.
        /// </summary>
        private static double CalculateGradientT(
            int x, int y,
            int startX, int startY,
            int endX, int endY,
            GradientType type,
            double length, double dx, double dy)
        {
            return type switch
            {
                GradientType.Linear => CalculateLinearT(x, y, startX, startY, dx, dy, length),
                GradientType.Radial => CalculateRadialT(x, y, startX, startY, length),
                GradientType.Angular => CalculateAngularT(x, y, startX, startY, dx, dy),
                GradientType.Diamond => CalculateDiamondT(x, y, startX, startY, length, dx, dy),
                _ => CalculateLinearT(x, y, startX, startY, dx, dy, length)
            };
        }

        /// <summary>
        /// Linear gradient: projection onto gradient line.
        /// </summary>
        private static double CalculateLinearT(
            int x, int y,
            int startX, int startY,
            double dx, double dy, double length)
        {
            // Vector from start to pixel
            double px = x - startX;
            double py = y - startY;

            // Project onto gradient direction
            double dot = (px * dx + py * dy) / (length * length);
            return Math.Clamp(dot, 0, 1);
        }

        /// <summary>
        /// Radial gradient: distance from center.
        /// </summary>
        private static double CalculateRadialT(
            int x, int y,
            int centerX, int centerY,
            double radius)
        {
            double dx = x - centerX;
            double dy = y - centerY;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            return Math.Clamp(dist / radius, 0, 1);
        }

        /// <summary>
        /// Angular/conical gradient: angle around center.
        /// </summary>
        private static double CalculateAngularT(
            int x, int y,
            int centerX, int centerY,
            double gradDx, double gradDy)
        {
            double px = x - centerX;
            double py = y - centerY;

            // Calculate angle of gradient line
            double gradAngle = Math.Atan2(gradDy, gradDx);

            // Calculate angle to pixel
            double pixelAngle = Math.Atan2(py, px);

            // Difference in angles, normalized to 0-1
            double angleDiff = pixelAngle - gradAngle;

            // Normalize to 0-2?
            while (angleDiff < 0) angleDiff += 2 * Math.PI;
            while (angleDiff >= 2 * Math.PI) angleDiff -= 2 * Math.PI;

            return angleDiff / (2 * Math.PI);
        }

        /// <summary>
        /// Diamond gradient: Manhattan distance scaled by gradient length.
        /// </summary>
        private static double CalculateDiamondT(
            int x, int y,
            int centerX, int centerY,
            double length, double dx, double dy)
        {
            // Rotate pixel coordinates to align with gradient direction
            double angle = Math.Atan2(dy, dx);
            double cos = Math.Cos(-angle);
            double sin = Math.Sin(-angle);

            double px = x - centerX;
            double py = y - centerY;

            double rotX = px * cos - py * sin;
            double rotY = px * sin + py * cos;

            // Diamond distance (L1 norm / Manhattan)
            double dist = Math.Abs(rotX) + Math.Abs(rotY);
            return Math.Clamp(dist / length, 0, 1);
        }

        // ====================================================================
        // ERROR DIFFUSION RENDERING
        // ====================================================================

        private static void RenderWithErrorDiffusion(
            byte[] pixels, int width, int height,
            int startX, int startY, int endX, int endY,
            GradientFillToolSettings settings,
            GradientStop[] stops,
            Func<int, int, bool>? selectionMask)
        {
            double dx = endX - startX;
            double dy = endY - startY;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1) length = 1;

            // Build gradient value buffer
            var gradientValues = new double[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double t = CalculateGradientT(
                        x, y, startX, startY, endX, endY,
                        settings.GradientType, length, dx, dy);

                    if (settings.Reverse)
                        t = 1.0 - t;

                    gradientValues[y * width + x] = t;
                }
            }

            // Apply multi-color error diffusion - uses full palette!
            uint[] ditheredColors = settings.DitherStyle switch
            {
                DitherStyle.FloydSteinberg => DitherPatterns.ApplyFloydSteinbergMultiColor(gradientValues, stops, width, height),
                DitherStyle.Atkinson => DitherPatterns.ApplyAtkinsonMultiColor(gradientValues, stops, width, height),
                DitherStyle.Riemersma => DitherPatterns.ApplyRiemersmaMultiColor(gradientValues, stops, width, height),
                _ => DitherPatterns.ApplyFloydSteinbergMultiColor(gradientValues, stops, width, height)
            };

            // Apply dithered colors to pixels
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (selectionMask != null && !selectionMask(x, y))
                        continue;

                    uint color = ditheredColors[y * width + x];

                    // Apply opacity
                    if (settings.Opacity < 255)
                    {
                        color = ApplyOpacity(color, settings.Opacity);
                    }

                    // Blend with existing
                    int idx = (y * width + x) * 4;
                    uint existing = ReadPixel(pixels, idx);
                    uint blended = BlendOver(existing, color);
                    WritePixel(pixels, idx, blended);
                }
            }
        }

        /// <summary>
        /// Smooth gradient sampling (no dithering).
        /// </summary>
        private static uint SampleGradientSmooth(GradientStop[] stops, double t)
        {
            if (stops.Length == 0) return 0xFF000000;
            if (stops.Length == 1) return stops[0].Color;

            t = Math.Clamp(t, 0, 1);

            // Find surrounding stops
            int idx = 0;
            for (int i = 0; i < stops.Length - 1; i++)
            {
                if (t >= stops[i].Position && t <= stops[i + 1].Position)
                {
                    idx = i;
                    break;
                }
                idx = i;
            }

            if (idx >= stops.Length - 1)
                return stops[^1].Color;

            var s1 = stops[idx];
            var s2 = stops[idx + 1];

            double range = s2.Position - s1.Position;
            double localT = range > 0.0001 ? (t - s1.Position) / range : 0;

            return LerpColor(s1.Color, s2.Color, localT);
        }

        // ====================================================================
        // PIXEL HELPERS
        // ====================================================================

        private static uint ReadPixel(byte[] pixels, int idx)
        {
            return (uint)(pixels[idx] | (pixels[idx + 1] << 8) | (pixels[idx + 2] << 16) | (pixels[idx + 3] << 24));
        }

        private static void WritePixel(byte[] pixels, int idx, uint color)
        {
            pixels[idx] = (byte)(color & 0xFF);
            pixels[idx + 1] = (byte)((color >> 8) & 0xFF);
            pixels[idx + 2] = (byte)((color >> 16) & 0xFF);
            pixels[idx + 3] = (byte)((color >> 24) & 0xFF);
        }

        private static uint ApplyOpacity(uint color, byte opacity)
        {
            byte a = (byte)((color >> 24) & 0xFF);
            a = (byte)((a * opacity) / 255);
            return (color & 0x00FFFFFF) | ((uint)a << 24);
        }

        private static uint LerpColor(uint c1, uint c2, double t)
        {
            byte b1 = (byte)(c1 & 0xFF);
            byte g1 = (byte)((c1 >> 8) & 0xFF);
            byte r1 = (byte)((c1 >> 16) & 0xFF);
            byte a1 = (byte)((c1 >> 24) & 0xFF);

            byte b2 = (byte)(c2 & 0xFF);
            byte g2 = (byte)((c2 >> 8) & 0xFF);
            byte r2 = (byte)((c2 >> 16) & 0xFF);
            byte a2 = (byte)((c2 >> 24) & 0xFF);

            byte b = (byte)(b1 + (b2 - b1) * t);
            byte g = (byte)(g1 + (g2 - g1) * t);
            byte r = (byte)(r1 + (r2 - r1) * t);
            byte a = (byte)(a1 + (a2 - a1) * t);

            return (uint)(b | (g << 8) | (r << 16) | (a << 24));
        }

        private static uint BlendOver(uint dst, uint src)
        {
            byte sa = (byte)((src >> 24) & 0xFF);
            if (sa == 0) return dst;
            if (sa == 255) return src;

            byte da = (byte)((dst >> 24) & 0xFF);

            byte sr = (byte)((src >> 16) & 0xFF);
            byte sg = (byte)((src >> 8) & 0xFF);
            byte sb = (byte)(src & 0xFF);

            byte dr = (byte)((dst >> 16) & 0xFF);
            byte dg = (byte)((dst >> 8) & 0xFF);
            byte db = (byte)(dst & 0xFF);

            float srcA = sa / 255f;
            float dstA = da / 255f;
            float outA = srcA + dstA * (1 - srcA);

            if (outA <= 0) return 0;

            byte outR = (byte)((sr * srcA + dr * dstA * (1 - srcA)) / outA);
            byte outG = (byte)((sg * srcA + dg * dstA * (1 - srcA)) / outA);
            byte outB = (byte)((sb * srcA + db * dstA * (1 - srcA)) / outA);
            byte outAByte = (byte)(outA * 255);

            return (uint)(outB | (outG << 8) | (outR << 16) | (outAByte << 24));
        }
    }
}
