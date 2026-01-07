using System;
using PixlPunkt.Core.Tools.Settings;

namespace PixlPunkt.Core.Painting.Dithering
{
    /// <summary>
    /// Provides dithering patterns and algorithms for gradient rendering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dithering creates the illusion of more colors by using patterns of available colors.
    /// This is essential for pixel art where limited palettes require smooth transitions.
    /// </para>
    /// <para>
    /// Supported algorithms:
    /// - Ordered dithering (Bayer matrices): Creates regular patterns, good for retro aesthetics
    /// - Pattern-based: Checker, diagonal, crosshatch for stylized looks
    /// - Error diffusion (Floyd-Steinberg, Atkinson): Distributes quantization error for smoother results
    /// - Blue noise: Random but visually pleasing distribution
    /// </para>
    /// </remarks>
    public static class DitherPatterns
    {
        // ====================================================================
        // BAYER MATRICES (ORDERED DITHERING)
        // ====================================================================

        /// <summary>
        /// Bayer 2x2 ordered dither matrix (normalized to 0-1).
        /// </summary>
        private static readonly double[,] Bayer2x2 =
        {
            { 0.0 / 4.0, 2.0 / 4.0 },
            { 3.0 / 4.0, 1.0 / 4.0 }
        };

        /// <summary>
        /// Bayer 4x4 ordered dither matrix (normalized to 0-1).
        /// </summary>
        private static readonly double[,] Bayer4x4 =
        {
            {  0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0 },
            { 12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0 },
            {  3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0 },
            { 15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0 }
        };

        /// <summary>
        /// Bayer 8x8 ordered dither matrix (normalized to 0-1).
        /// </summary>
        private static readonly double[,] Bayer8x8 =
        {
            {  0.0 / 64.0, 32.0 / 64.0,  8.0 / 64.0, 40.0 / 64.0,  2.0 / 64.0, 34.0 / 64.0, 10.0 / 64.0, 42.0 / 64.0 },
            { 48.0 / 64.0, 16.0 / 64.0, 56.0 / 64.0, 24.0 / 64.0, 50.0 / 64.0, 18.0 / 64.0, 58.0 / 64.0, 26.0 / 64.0 },
            { 12.0 / 64.0, 44.0 / 64.0,  4.0 / 64.0, 36.0 / 64.0, 14.0 / 64.0, 46.0 / 64.0,  6.0 / 64.0, 38.0 / 64.0 },
            { 60.0 / 64.0, 28.0 / 64.0, 52.0 / 64.0, 20.0 / 64.0, 62.0 / 64.0, 30.0 / 64.0, 54.0 / 64.0, 22.0 / 64.0 },
            {  3.0 / 64.0, 35.0 / 64.0, 11.0 / 64.0, 43.0 / 64.0,  1.0 / 64.0, 33.0 / 64.0,  9.0 / 64.0, 41.0 / 64.0 },
            { 51.0 / 64.0, 19.0 / 64.0, 59.0 / 64.0, 27.0 / 64.0, 49.0 / 64.0, 17.0 / 64.0, 57.0 / 64.0, 25.0 / 64.0 },
            { 15.0 / 64.0, 47.0 / 64.0,  7.0 / 64.0, 39.0 / 64.0, 13.0 / 64.0, 45.0 / 64.0,  5.0 / 64.0, 37.0 / 64.0 },
            { 63.0 / 64.0, 31.0 / 64.0, 55.0 / 64.0, 23.0 / 64.0, 61.0 / 64.0, 29.0 / 64.0, 53.0 / 64.0, 21.0 / 64.0 }
        };

        // ====================================================================
        // BLUE NOISE TEXTURE (16x16)
        // ====================================================================

        /// <summary>
        /// Pre-computed blue noise pattern (16x16, normalized to 0-1).
        /// Blue noise has a more random but visually pleasing distribution than white noise.
        /// </summary>
        private static readonly double[,] BlueNoise16x16 = GenerateBlueNoise(16);

        /// <summary>
        /// Generates a simple blue noise approximation using void-and-cluster method.
        /// </summary>
        private static double[,] GenerateBlueNoise(int size)
        {
            // Pre-computed blue noise values for deterministic results
            // This is a simplified approximation - real blue noise would use void-and-cluster
            var noise = new double[size, size];
            var random = new Random(42); // Fixed seed for reproducibility

            // Initialize with uniform random
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    noise[x, y] = random.NextDouble();

            // Apply simple high-pass filter to push towards blue noise characteristics
            var filtered = new double[size, size];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        double sum = 0;
                        double weight = 0;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = (x + dx + size) % size;
                                int ny = (y + dy + size) % size;
                                double w = (dx == 0 && dy == 0) ? 4.0 : -0.5;
                                sum += noise[nx, ny] * w;
                                weight += Math.Abs(w);
                            }
                        }

                        filtered[x, y] = Math.Clamp(sum / weight, 0, 1);
                    }
                }

                // Swap buffers
                (noise, filtered) = (filtered, noise);
            }

            return noise;
        }

        // ====================================================================
        // DITHER THRESHOLD CALCULATION
        // ====================================================================

        /// <summary>
        /// Gets the dither threshold for a pixel position based on the selected style.
        /// </summary>
        /// <param name="x">Pixel X coordinate.</param>
        /// <param name="y">Pixel Y coordinate.</param>
        /// <param name="style">The dithering style.</param>
        /// <param name="scale">Pattern scale multiplier (1-8).</param>
        /// <returns>Threshold value from 0.0 to 1.0.</returns>
        public static double GetThreshold(int x, int y, DitherStyle style, int scale)
        {
            // Apply scale (divide coordinates to enlarge pattern)
            int sx = x / Math.Max(1, scale);
            int sy = y / Math.Max(1, scale);

            return style switch
            {
                DitherStyle.None => 0.5,
                DitherStyle.Bayer2x2 => Bayer2x2[sx % 2, sy % 2],
                DitherStyle.Bayer4x4 => Bayer4x4[sx % 4, sy % 4],
                DitherStyle.Bayer8x8 => Bayer8x8[sx % 8, sy % 8],
                DitherStyle.Checker => GetCheckerThreshold(sx, sy),
                DitherStyle.Diagonal => GetDiagonalThreshold(sx, sy),
                DitherStyle.Crosshatch => GetCrosshatchThreshold(sx, sy),
                DitherStyle.BlueNoise => BlueNoise16x16[sx % 16, sy % 16],
                // Error diffusion handled separately
                DitherStyle.FloydSteinberg => 0.5,
                DitherStyle.Atkinson => 0.5,
                _ => 0.5
            };
        }

        /// <summary>
        /// Simple 50/50 checker pattern.
        /// </summary>
        private static double GetCheckerThreshold(int x, int y)
        {
            return ((x + y) % 2 == 0) ? 0.25 : 0.75;
        }

        /// <summary>
        /// Diagonal line pattern.
        /// </summary>
        private static double GetDiagonalThreshold(int x, int y)
        {
            int diag = (x + y) % 4;
            return diag switch
            {
                0 => 0.0,
                1 => 0.5,
                2 => 0.25,
                3 => 0.75,
                _ => 0.5
            };
        }

        /// <summary>
        /// Crosshatch pattern.
        /// </summary>
        private static double GetCrosshatchThreshold(int x, int y)
        {
            bool h = (y % 4) < 2;
            bool v = (x % 4) < 2;

            if (h && v) return 0.0;
            if (h || v) return 0.5;
            return 1.0;
        }

        // ====================================================================
        // ERROR DIFFUSION
        // ====================================================================

        /// <summary>
        /// Applies Floyd-Steinberg error diffusion to a gradient buffer.
        /// </summary>
        /// <param name="gradient">Gradient values (0-1) for each pixel, row-major.</param>
        /// <param name="width">Buffer width.</param>
        /// <param name="height">Buffer height.</param>
        /// <param name="levels">Number of output levels (typically 2 for binary dither).</param>
        /// <returns>Quantized values for each pixel.</returns>
        public static double[] ApplyFloydSteinberg(double[] gradient, int width, int height, int levels = 2)
        {
            var result = new double[gradient.Length];
            var errors = new double[gradient.Length];
            Array.Copy(gradient, result, gradient.Length);

            double levelStep = 1.0 / (levels - 1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    double oldVal = result[idx] + errors[idx];
                    double newVal = Math.Round(oldVal * (levels - 1)) * levelStep;
                    result[idx] = Math.Clamp(newVal, 0, 1);

                    double error = oldVal - newVal;

                    // Distribute error to neighboring pixels
                    // Floyd-Steinberg kernel:
                    //     * 7/16
                    // 3/16 5/16 1/16

                    if (x + 1 < width)
                        errors[idx + 1] += error * 7.0 / 16.0;
                    if (y + 1 < height)
                    {
                        if (x > 0)
                            errors[idx + width - 1] += error * 3.0 / 16.0;
                        errors[idx + width] += error * 5.0 / 16.0;
                        if (x + 1 < width)
                            errors[idx + width + 1] += error * 1.0 / 16.0;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Applies Atkinson error diffusion (lighter than Floyd-Steinberg).
        /// </summary>
        /// <param name="gradient">Gradient values (0-1) for each pixel, row-major.</param>
        /// <param name="width">Buffer width.</param>
        /// <param name="height">Buffer height.</param>
        /// <param name="levels">Number of output levels.</param>
        /// <returns>Quantized values for each pixel.</returns>
        public static double[] ApplyAtkinson(double[] gradient, int width, int height, int levels = 2)
        {
            var result = new double[gradient.Length];
            var errors = new double[gradient.Length];
            Array.Copy(gradient, result, gradient.Length);

            double levelStep = 1.0 / (levels - 1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    double oldVal = result[idx] + errors[idx];
                    double newVal = Math.Round(oldVal * (levels - 1)) * levelStep;
                    result[idx] = Math.Clamp(newVal, 0, 1);

                    // Atkinson only distributes 6/8 (75%) of error, making lighter dither
                    double error = (oldVal - newVal) / 8.0;

                    // Atkinson kernel:
                    //     * 1 1
                    //   1 1 1
                    //     1

                    if (x + 1 < width)
                        errors[idx + 1] += error;
                    if (x + 2 < width)
                        errors[idx + 2] += error;

                    if (y + 1 < height)
                    {
                        if (x > 0)
                            errors[idx + width - 1] += error;
                        errors[idx + width] += error;
                        if (x + 1 < width)
                            errors[idx + width + 1] += error;
                    }

                    if (y + 2 < height)
                        errors[idx + 2 * width] += error;
                }
            }

            return result;
        }

        // ====================================================================
        // MULTI-COLOR ERROR DIFFUSION
        // ====================================================================

        /// <summary>
        /// Applies Floyd-Steinberg error diffusion with a multi-color palette.
        /// Instead of binary dithering, quantizes to the nearest color in the gradient stops.
        /// </summary>
        /// <param name="gradient">Gradient t-values (0-1) for each pixel, row-major.</param>
        /// <param name="stops">All gradient color stops to choose from.</param>
        /// <param name="width">Buffer width.</param>
        /// <param name="height">Buffer height.</param>
        /// <returns>Quantized colors (BGRA) for each pixel.</returns>
        public static uint[] ApplyFloydSteinbergMultiColor(double[] gradient, GradientStop[] stops, int width, int height)
        {
            var result = new uint[gradient.Length];
            var errorR = new double[gradient.Length];
            var errorG = new double[gradient.Length];
            var errorB = new double[gradient.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    double t = gradient[idx];

                    // Get the ideal smooth color at this position
                    uint idealColor = SampleGradientSmooth(stops, t);

                    // Add accumulated error to get target color
                    int targetR = (int)((idealColor >> 16) & 0xFF) + (int)Math.Round(errorR[idx]);
                    int targetG = (int)((idealColor >> 8) & 0xFF) + (int)Math.Round(errorG[idx]);
                    int targetB = (int)(idealColor & 0xFF) + (int)Math.Round(errorB[idx]);

                    targetR = Math.Clamp(targetR, 0, 255);
                    targetG = Math.Clamp(targetG, 0, 255);
                    targetB = Math.Clamp(targetB, 0, 255);

                    // Find the nearest color in the palette
                    uint quantizedColor = FindNearestColor(stops, (byte)targetR, (byte)targetG, (byte)targetB);
                    result[idx] = quantizedColor;

                    // Calculate error
                    int qR = (int)((quantizedColor >> 16) & 0xFF);
                    int qG = (int)((quantizedColor >> 8) & 0xFF);
                    int qB = (int)(quantizedColor & 0xFF);

                    double errR = targetR - qR;
                    double errG = targetG - qG;
                    double errB = targetB - qB;

                    // Floyd-Steinberg error distribution:
                    //     * 7/16
                    // 3/16 5/16 1/16
                    if (x + 1 < width)
                    {
                        errorR[idx + 1] += errR * 7.0 / 16.0;
                        errorG[idx + 1] += errG * 7.0 / 16.0;
                        errorB[idx + 1] += errB * 7.0 / 16.0;
                    }
                    if (y + 1 < height)
                    {
                        if (x > 0)
                        {
                            errorR[idx + width - 1] += errR * 3.0 / 16.0;
                            errorG[idx + width - 1] += errG * 3.0 / 16.0;
                            errorB[idx + width - 1] += errB * 3.0 / 16.0;
                        }
                        errorR[idx + width] += errR * 5.0 / 16.0;
                        errorG[idx + width] += errG * 5.0 / 16.0;
                        errorB[idx + width] += errB * 5.0 / 16.0;
                        if (x + 1 < width)
                        {
                            errorR[idx + width + 1] += errR * 1.0 / 16.0;
                            errorG[idx + width + 1] += errG * 1.0 / 16.0;
                            errorB[idx + width + 1] += errB * 1.0 / 16.0;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Applies Atkinson error diffusion with a multi-color palette.
        /// Atkinson only distributes 75% of error, creating a lighter dither effect.
        /// </summary>
        public static uint[] ApplyAtkinsonMultiColor(double[] gradient, GradientStop[] stops, int width, int height)
        {
            var result = new uint[gradient.Length];
            var errorR = new double[gradient.Length];
            var errorG = new double[gradient.Length];
            var errorB = new double[gradient.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    double t = gradient[idx];

                    // Get the ideal smooth color at this position
                    uint idealColor = SampleGradientSmooth(stops, t);

                    // Add accumulated error to get target color
                    int targetR = (int)((idealColor >> 16) & 0xFF) + (int)Math.Round(errorR[idx]);
                    int targetG = (int)((idealColor >> 8) & 0xFF) + (int)Math.Round(errorG[idx]);
                    int targetB = (int)(idealColor & 0xFF) + (int)Math.Round(errorB[idx]);

                    targetR = Math.Clamp(targetR, 0, 255);
                    targetG = Math.Clamp(targetG, 0, 255);
                    targetB = Math.Clamp(targetB, 0, 255);

                    // Find the nearest color in the palette
                    uint quantizedColor = FindNearestColor(stops, (byte)targetR, (byte)targetG, (byte)targetB);
                    result[idx] = quantizedColor;

                    // Calculate error (Atkinson only distributes 6/8 = 75%)
                    int qR = (int)((quantizedColor >> 16) & 0xFF);
                    int qG = (int)((quantizedColor >> 8) & 0xFF);
                    int qB = (int)(quantizedColor & 0xFF);

                    double errR = (targetR - qR) / 8.0;
                    double errG = (targetG - qG) / 8.0;
                    double errB = (targetB - qB) / 8.0;

                    // Atkinson kernel:
                    //     * 1 1
                    //   1 1 1
                    //     1
                    if (x + 1 < width)
                    {
                        errorR[idx + 1] += errR;
                        errorG[idx + 1] += errG;
                        errorB[idx + 1] += errB;
                    }
                    if (x + 2 < width)
                    {
                        errorR[idx + 2] += errR;
                        errorG[idx + 2] += errG;
                        errorB[idx + 2] += errB;
                    }
                    if (y + 1 < height)
                    {
                        if (x > 0)
                        {
                            errorR[idx + width - 1] += errR;
                            errorG[idx + width - 1] += errG;
                            errorB[idx + width - 1] += errB;
                        }
                        errorR[idx + width] += errR;
                        errorG[idx + width] += errG;
                        errorB[idx + width] += errB;
                        if (x + 1 < width)
                        {
                            errorR[idx + width + 1] += errR;
                            errorG[idx + width + 1] += errG;
                            errorB[idx + width + 1] += errB;
                        }
                    }
                    if (y + 2 < height)
                    {
                        errorR[idx + 2 * width] += errR;
                        errorG[idx + 2 * width] += errG;
                        errorB[idx + 2 * width] += errB;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Finds the nearest color in the gradient stops to the target RGB.
        /// </summary>
        private static uint FindNearestColor(GradientStop[] stops, byte targetR, byte targetG, byte targetB)
        {
            uint bestColor = stops[0].Color;
            int bestDist = int.MaxValue;

            foreach (var stop in stops)
            {
                byte r = (byte)((stop.Color >> 16) & 0xFF);
                byte g = (byte)((stop.Color >> 8) & 0xFF);
                byte b = (byte)(stop.Color & 0xFF);

                // Euclidean distance squared (no need for sqrt for comparison)
                int dr = targetR - r;
                int dg = targetG - g;
                int db = targetB - b;
                int dist = dr * dr + dg * dg + db * db;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestColor = stop.Color;
                }
            }

            return bestColor;
        }

        /// <summary>
        /// Interpolates between two colors using dithering.
        /// </summary>
        /// <param name="color1">First color (BGRA).</param>
        /// <param name="color2">Second color (BGRA).</param>
        /// <param name="t">Interpolation factor (0-1).</param>
        /// <param name="x">Pixel X coordinate.</param>
        /// <param name="y">Pixel Y coordinate.</param>
        /// <param name="style">Dithering style.</param>
        /// <param name="strength">Dither strength (0-100).</param>
        /// <param name="scale">Pattern scale.</param>
        /// <returns>Dithered color (BGRA).</returns>
        public static uint DitheredLerp(uint color1, uint color2, double t, int x, int y,
            DitherStyle style, int strength, int scale)
        {
            if (style == DitherStyle.None || strength == 0)
            {
                // No dithering - smooth interpolation
                return LerpColor(color1, color2, t);
            }

            double threshold = GetThreshold(x, y, style, scale);
            double strengthFactor = strength / 100.0;

            // Blend between smooth and dithered based on strength
            // At strength 100%, fully dithered; at 0%, fully smooth
            double ditherT = t + (threshold - 0.5) * strengthFactor;
            ditherT = Math.Clamp(ditherT, 0, 1);

            // Round to create hard edge (dithered) vs smooth
            // The threshold determines which color to pick
            if (strengthFactor >= 1.0)
            {
                // Full dither - binary choice
                return ditherT >= 0.5 ? color2 : color1;
            }
            else
            {
                // Partial dither - blend smooth and dithered
                uint smoothColor = LerpColor(color1, color2, t);
                uint ditherColor = ditherT >= 0.5 ? color2 : color1;
                return LerpColor(smoothColor, ditherColor, strengthFactor);
            }
        }

        /// <summary>
        /// Linear interpolation between two colors.
        /// </summary>
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

        /// <summary>
        /// Samples a multi-stop gradient at a position with dithering.
        /// </summary>
        /// <param name="stops">Gradient stops (sorted by position).</param>
        /// <param name="t">Position in gradient (0-1).</param>
        /// <param name="x">Pixel X coordinate.</param>
        /// <param name="y">Pixel Y coordinate.</param>
        /// <param name="style">Dithering style.</param>
        /// <param name="strength">Dither strength (0-100).</param>
        /// <param name="scale">Pattern scale.</param>
        /// <returns>Dithered color (BGRA).</returns>
        public static uint SampleGradient(GradientStop[] stops, double t, int x, int y,
            DitherStyle style, int strength, int scale)
        {
            if (stops == null || stops.Length == 0)
                return 0xFF000000; // Black

            if (stops.Length == 1)
                return stops[0].Color;

            // Find the two stops surrounding t
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

            var stop1 = stops[idx];
            var stop2 = stops[idx + 1];

            // Calculate local t between these two stops
            double range = stop2.Position - stop1.Position;
            double localT = range > 0.0001 ? (t - stop1.Position) / range : 0;

            return DitheredLerp(stop1.Color, stop2.Color, localT, x, y, style, strength, scale);
        }

        /// <summary>
        /// Samples a gradient smoothly (linear interpolation between stops).
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
        // RIEMERSMA DITHERING (HILBERT CURVE)
        // ====================================================================

        /// <summary>
        /// Applies Riemersma dithering using a Hilbert curve traversal.
        /// This creates a more organic, less directional dither than scanline-based methods.
        /// </summary>
        /// <param name="gradient">Gradient t-values (0-1) for each pixel, row-major.</param>
        /// <param name="stops">All gradient color stops to choose from.</param>
        /// <param name="width">Buffer width.</param>
        /// <param name="height">Buffer height.</param>
        /// <returns>Quantized colors (BGRA) for each pixel.</returns>
        public static uint[] ApplyRiemersmaMultiColor(double[] gradient, GradientStop[] stops, int width, int height)
        {
            var result = new uint[gradient.Length];

            // Initialize with smooth colors first
            for (int i = 0; i < gradient.Length; i++)
            {
                result[i] = SampleGradientSmooth(stops, gradient[i]);
            }

            // Riemersma uses a weighted history of errors with exponential decay
            const int HistorySize = 16;
            var historyR = new double[HistorySize];
            var historyG = new double[HistorySize];
            var historyB = new double[HistorySize];
            int historyIdx = 0;

            // Weights for the error history (exponential decay)
            var weights = new double[HistorySize];
            double totalWeight = 0;
            for (int i = 0; i < HistorySize; i++)
            {
                weights[i] = Math.Pow(0.5, i);
                totalWeight += weights[i];
            }
            // Normalize weights
            for (int i = 0; i < HistorySize; i++)
            {
                weights[i] /= totalWeight;
            }

            // Traverse using Hilbert curve
            int size = Math.Max(width, height);
            int order = (int)Math.Ceiling(Math.Log2(size));
            if (order < 1) order = 1;

            // Process each point along the Hilbert curve
            int maxPoints = 1 << (order * 2);
            for (int d = 0; d < maxPoints; d++)
            {
                // Convert Hilbert curve index to x,y coordinates
                HilbertIndexToXY(d, order, out int hx, out int hy);

                // Skip if outside image bounds
                if (hx >= width || hy >= height) continue;

                int idx = hy * width + hx;
                double t = gradient[idx];

                // Get the ideal smooth color
                uint idealColor = SampleGradientSmooth(stops, t);

                // Calculate weighted error from history
                double accErrorR = 0, accErrorG = 0, accErrorB = 0;
                for (int i = 0; i < HistorySize; i++)
                {
                    int hi = (historyIdx - i - 1 + HistorySize) % HistorySize;
                    accErrorR += historyR[hi] * weights[i];
                    accErrorG += historyG[hi] * weights[i];
                    accErrorB += historyB[hi] * weights[i];
                }

                // Add error to ideal color
                int targetR = (int)((idealColor >> 16) & 0xFF) + (int)Math.Round(accErrorR);
                int targetG = (int)((idealColor >> 8) & 0xFF) + (int)Math.Round(accErrorG);
                int targetB = (int)(idealColor & 0xFF) + (int)Math.Round(accErrorB);

                targetR = Math.Clamp(targetR, 0, 255);
                targetG = Math.Clamp(targetG, 0, 255);
                targetB = Math.Clamp(targetB, 0, 255);

                // Find nearest color in palette
                uint quantizedColor = FindNearestColor(stops, (byte)targetR, (byte)targetG, (byte)targetB);
                result[idx] = quantizedColor;

                // Calculate and store error in history
                int qR = (int)((quantizedColor >> 16) & 0xFF);
                int qG = (int)((quantizedColor >> 8) & 0xFF);
                int qB = (int)(quantizedColor & 0xFF);

                historyR[historyIdx] = targetR - qR;
                historyG[historyIdx] = targetG - qG;
                historyB[historyIdx] = targetB - qB;
                historyIdx = (historyIdx + 1) % HistorySize;
            }

            return result;
        }

        /// <summary>
        /// Converts a Hilbert curve index to x,y coordinates.
        /// </summary>
        private static void HilbertIndexToXY(int d, int order, out int x, out int y)
        {
            x = 0;
            y = 0;
            int rx, ry, s;
            int t = d;

            for (s = 1; s < (1 << order); s *= 2)
            {
                rx = 1 & (t / 2);
                ry = 1 & (t ^ rx);

                // Rotate
                if (ry == 0)
                {
                    if (rx == 1)
                    {
                        x = s - 1 - x;
                        y = s - 1 - y;
                    }
                    // Swap x and y
                    (x, y) = (y, x);
                }

                x += s * rx;
                y += s * ry;
                t /= 4;
            }
        }
    }
}
