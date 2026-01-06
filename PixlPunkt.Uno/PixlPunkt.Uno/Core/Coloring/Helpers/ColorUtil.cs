using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace PixlPunkt.Uno.Core.Coloring.Helpers
{
    /// <summary>
    /// Provides static utility methods for color conversion, manipulation, parsing, and interpolation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ColorUtil is the central color mathematics library for PixlPunkt, handling conversions between:
    /// - Packed BGRA (uint32) format: B[0..7], G[8..15], R[16..23], A[24..31]
    /// - WinUI Color struct
    /// - HSL (Hue, Saturation, Lightness) color space
    /// - HSV (Hue, Saturation, Value) color space
    /// - Hex strings (#RGB, #RRGGBB, #ARGB, #AARRGGBB)
    /// </para>
    /// <para>
    /// Key operations:
    /// - **Packing/Unpacking**: Convert between byte components and packed uint32
    /// - **Parsing**: Parse various hex color formats
    /// - **Color Space Conversion**: RGB ↔ HSL ↔ HSV
    /// - **Interpolation**: Lerp colors in RGB or HSL space
    /// - **Luminance/Contrast**: Calculate relative luminance and contrast ratios (WCAG)
    /// - **Merging**: Cluster similar colors within a tolerance
    /// </para>
    /// <para>
    /// All methods are thread-safe (pure functions with no shared state). Methods marked with
    /// [AggressiveInlining] are optimized for performance in hot paths.
    /// </para>
    /// </remarks>
    public static class ColorUtil
    {
        // ============================================================
        // MERGING
        // ============================================================

        /// <summary>
        /// Merges similar colors within a tolerance using simple clustering.
        /// </summary>
        /// <param name="colors">Input colors (packed BGRA).</param>
        /// <param name="tolerance">
        /// Maximum per-channel difference (0-255) to consider colors similar.
        /// Uses Chebyshev distance (max of R, G, B differences).
        /// </param>
        /// <param name="maxColors">Maximum number of output colors to retain.</param>
        /// <returns>A list of merged colors, with similar colors averaged together.</returns>
        /// <remarks>
        /// <para>
        /// Algorithm: For each input color, find the first result color within tolerance.
        /// If found, average the two colors. Otherwise, add as new color.
        /// </para>
        /// <para>
        /// This is a greedy O(n*m) algorithm where n=input count, m=output count. Not suitable
        /// for large datasets without optimization. Useful for palette quantization and color deduplication.
        /// </para>
        /// </remarks>
        public static List<uint> MergeNearColors(IReadOnlyList<uint> colors, int tolerance, int maxColors)
        {
            var result = new List<uint>();
            foreach (var c in colors)
            {
                byte cb = (byte)(c & 0xFF);
                byte cg = (byte)((c >> 8) & 0xFF);
                byte cr = (byte)((c >> 16) & 0xFF);

                bool merged = false;
                for (int i = 0; i < result.Count; i++)
                {
                    uint rcol = result[i];
                    byte rb = (byte)(rcol & 0xFF);
                    byte rg = (byte)((rcol >> 8) & 0xFF);
                    byte rr = (byte)((rcol >> 16) & 0xFF);

                    int diff = Math.Max(Math.Max(Math.Abs(cb - rb), Math.Abs(cg - rg)), Math.Abs(cr - rr));
                    if (diff <= tolerance)
                    {
                        // Cluster averaging
                        byte nb = (byte)((cb + rb) / 2);
                        byte ng = (byte)((cg + rg) / 2);
                        byte nr = (byte)((cr + rr) / 2);
                        result[i] = (uint)(0xFF << 24 | (uint)nr << 16 | (uint)ng << 8 | nb);
                        merged = true;
                        break;
                    }
                }

                if (!merged)
                {
                    result.Add(c);
                    if (result.Count >= maxColors) break;
                }
            }
            return result;
        }

        // ============================================================
        // PACK / UNPACK
        // ============================================================

        /// <summary>
        /// Packs byte components into a BGRA uint32 (preferred canonical method).
        /// </summary>
        /// <param name="b">Blue component (0-255).</param>
        /// <param name="g">Green component (0-255).</param>
        /// <param name="r">Red component (0-255).</param>
        /// <param name="a">Alpha component (0-255). Default is 255 (opaque).</param>
        /// <returns>Packed BGRA as 0xAABBGGRR.</returns>
        public static uint PackBGRA(byte b, byte g, byte r, byte a = 255)
            => (uint)(b | (g << 8) | (r << 16) | (a << 24));

        /// <summary>
        /// Converts a WinUI Color struct to packed BGRA uint32 (canonical conversion).
        /// </summary>
        /// <param name="c">The color to convert.</param>
        /// <returns>Packed BGRA as 0xAABBGGRR.</returns>
        public static uint ToBGRA(Windows.UI.Color c)
            => (uint)(c.A << 24 | c.R << 16 | c.G << 8 | c.B);

        /// <summary>
        /// Converts packed BGRA uint32 to a WinUI Color struct (canonical conversion).
        /// </summary>
        /// <param name="bgra">Packed color as 0xAABBGGRR.</param>
        /// <returns>A WinUI Color instance.</returns>
        public static Windows.UI.Color ToColor(uint bgra)
            => Windows.UI.Color.FromArgb(
                (byte)((bgra >> 24) & 0xFF),
                (byte)((bgra >> 16) & 0xFF),
                (byte)((bgra >> 8) & 0xFF),
                (byte)(bgra & 0xFF));

        /// <summary>
        /// Attempts to coerce various primitive types to a packed BGRA uint32.
        /// </summary>
        /// <param name="value">A uint, int, long, or hex string.</param>
        /// <param name="bgra">The resulting packed BGRA value.</param>
        /// <returns><c>true</c> if conversion succeeded; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Useful for deserializing colors from JSON or user input where the exact type is unknown.
        /// Hex strings should not include '#' prefix.
        /// </remarks>
        public static bool TryGetBGRA(object? value, out uint bgra)
        {
            switch (value)
            {
                case uint u: bgra = u; return true;
                case int i: bgra = unchecked((uint)i); return true;
                case long l: bgra = unchecked((uint)l); return true;
                case string s when uint.TryParse(s, NumberStyles.HexNumber, null, out var v):
                    bgra = v; return true;
                default: bgra = 0; return false;
            }
        }

        /// <summary>
        /// Sets the alpha channel of a WinUI Color to 255 (fully opaque).
        /// </summary>
        public static Windows.UI.Color MakeOpaque(Windows.UI.Color c) { c.A = 255; return c; }

        /// <summary>
        /// Sets the alpha channel of packed BGRA to 255 (fully opaque).
        /// </summary>
        public static uint MakeOpaqueBGRA(uint bgra) => bgra | 0xFF000000u;

        /// <summary>
        /// Returns a new WinUI Color with the specified alpha value.
        /// </summary>
        public static Windows.UI.Color WithAlpha(Windows.UI.Color c, byte alpha) { c.A = alpha; return c; }

        /// <summary>
        /// Strips the alpha channel from packed BGRA (sets A=0).
        /// </summary>
        public static uint StripAlpha(uint bgra) => bgra & 0x00FFFFFFu;

        /// <summary>
        /// Compares two packed BGRA values for RGB equality (ignores alpha).
        /// </summary>
        public static bool RgbEqual(uint a, uint b) => (a & 0x00FFFFFFu) == (b & 0x00FFFFFFu);

        /// <summary>
        /// Packs ARGB components into uint32 (legacy method, prefer <see cref="PackBGRA"/>).
        /// </summary>
        /// <remarks>
        /// Parameter order is A,R,G,B. Result is still BGRA-packed internally. Kept for backward compatibility.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Pack(byte a, byte r, byte g, byte b)
            => ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;

        /// <summary>
        /// Unpacks a BGRA uint32 into byte components.
        /// </summary>
        /// <param name="bgra">Packed color as 0xAABBGGRR.</param>
        /// <param name="b">Blue component output.</param>
        /// <param name="g">Green component output.</param>
        /// <param name="r">Red component output.</param>
        /// <param name="a">Alpha component output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnpackBGRA(uint bgra, out byte b, out byte g, out byte r, out byte a)
        {
            b = (byte)(bgra & 0xFF);
            g = (byte)((bgra >> 8) & 0xFF);
            r = (byte)((bgra >> 16) & 0xFF);
            a = (byte)((bgra >> 24) & 0xFF);
        }

        /// <summary>
        /// Unpacks a uint32 into ARGB components (legacy method, prefer <see cref="UnpackBGRA"/>).
        /// </summary>
        /// <remarks>
        /// Output parameter order is A,R,G,B. Kept for backward compatibility.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Unpack(uint c, out byte a, out byte r, out byte g, out byte b)
        {
            a = (byte)(c >> 24);
            r = (byte)(c >> 16);
            g = (byte)(c >> 8);
            b = (byte)c;
        }

        /// <summary>Gets the blue component from packed BGRA.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetB(uint bgra) => (byte)(bgra & 0xFF);

        /// <summary>Gets the green component from packed BGRA.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetG(uint bgra) => (byte)((bgra >> 8) & 0xFF);

        /// <summary>Gets the red component from packed BGRA.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetR(uint bgra) => (byte)((bgra >> 16) & 0xFF);

        /// <summary>Gets the alpha component from packed BGRA.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetA(uint bgra) => (byte)((bgra >> 24) & 0xFF);

        /// <summary>
        /// Returns a new packed BGRA with the alpha channel replaced.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint SetAlphaBGRA(uint bgra, byte a)
            => (bgra & 0x00FFFFFFu) | ((uint)a << 24);

        /// <summary>
        /// Creates a packed BGRA color from RGB components with opaque alpha.
        /// </summary>
        /// <param name="r">Red channel (0-255).</param>
        /// <param name="g">Green channel (0-255).</param>
        /// <param name="b">Blue channel (0-255).</param>
        /// <returns>Packed BGRA color with alpha set to 255 (fully opaque).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FromRgb(byte r, byte g, byte b)
            => (uint)(b | (g << 8) | (r << 16) | (255 << 24));

        // ============================================================
        // HEX PARSING / FORMATTING
        // ============================================================

        /// <summary>
        /// Converts a WinUI Color to a hex string (#RRGGBB or #AARRGGBB).
        /// </summary>
        /// <param name="c">The color to convert.</param>
        /// <returns>
        /// #RRGGBB if alpha is 255 (opaque); otherwise #AARRGGBB.
        /// </returns>
        public static string ToHex(Windows.UI.Color c)
            => c.A == 255
                ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        /// <summary>
        /// Parses a hex color string in various formats: #RGB, #RRGGBB, #ARGB, #AARRGGBB (with optional '#' or '0x' prefix).
        /// </summary>
        /// <param name="input">The hex string to parse.</param>
        /// <param name="color">The parsed color output.</param>
        /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// <para>
        /// Supported formats:
        /// - #RGB → #RRGGBB (each digit doubled)
        /// - #RRGGBB → opaque RGB
        /// - #ARGB → #AARRGGBB (each digit doubled)
        /// - #AARRGGBB → ARGB with alpha
        /// </para>
        /// <para>
        /// Case-insensitive. Prefix '#' or '0x' is optional.
        /// </para>
        /// </remarks>
        public static bool TryParseHex(string input, out Windows.UI.Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var s = input.Trim();
            if (s.StartsWith("#")) s = s[1..];
            else if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];

            // Expand shorthand (#RGB → #RRGGBB, #ARGB → #AARRGGBB)
            if (s.Length == 3) s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
            else if (s.Length == 4) s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}{s[3]}{s[3]}";

            byte a, r, g, b;
            if (s.Length == 6)
            {
                a = 255;
                if (!TryHexByte(s, 0, out r) ||
                    !TryHexByte(s, 2, out g) ||
                    !TryHexByte(s, 4, out b))
                    return false;
            }
            else if (s.Length == 8)
            {
                if (!TryHexByte(s, 0, out a) ||
                    !TryHexByte(s, 2, out r) ||
                    !TryHexByte(s, 4, out g) ||
                    !TryHexByte(s, 6, out b))
                    return false;
            }
            else return false;

            color = Windows.UI.Color.FromArgb(a, r, g, b);
            return true;
        }

        /// <summary>
        /// Parses an RGB-only hex string (#RGB or #RRGGBB), forcing alpha to 255.
        /// </summary>
        /// <remarks>
        /// Narrower than <see cref="TryParseHex"/>; does not accept alpha channel.
        /// Kept for performance and clarity where alpha is disallowed.
        /// </remarks>
        public static bool TryParseHexRGB(string? text, out Windows.UI.Color color)
        {
            color = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            if (string.IsNullOrWhiteSpace(text)) return false;
            var s = text.Trim();
            if (s.StartsWith("#")) s = s[1..];
            if (s.Length == 3)
                s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            if (s.Length != 6) return false;

            bool ok = byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, null, out byte r);
            if (!ok) return false;
            ok = byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, null, out byte g);
            if (!ok) return false;
            ok = byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, null, out byte b);
            if (!ok) return false;

            color = Windows.UI.Color.FromArgb(255, r, g, b);
            return true;
        }

        /// <summary>
        /// Parses an RGB-only hex string and returns packed BGRA with alpha=255.
        /// </summary>
        /// <remarks>
        /// Convenience method combining <see cref="TryParseHexRGB"/> and <see cref="ToBGRA"/>.
        /// </remarks>
        public static bool TryParseHexRGB_ToBGRA(string? text, out uint bgra)
        {
            bgra = 0xFF000000u;
            if (!TryParseHexRGB(text, out var c)) return false;
            bgra = ToBGRA(c);
            return true;
        }

        private static bool TryHexByte(string s, int start, out byte value)
        {
            value = 0;
            if (start + 2 > s.Length) return false;
            return byte.TryParse(s.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        // ============================================================
        // HSL / HSV
        // ============================================================

        /// <summary>
        /// Converts a WinUI Color to HSL color space.
        /// </summary>
        /// <param name="c">The input color.</param>
        /// <param name="h">Hue output (0-360 degrees).</param>
        /// <param name="s">Saturation output (0-1).</param>
        /// <param name="l">Lightness output (0-1).</param>
        public static void ToHSL(Windows.UI.Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;

            l = (max + min) / 2.0;
            if (Math.Abs(d) < 1e-9) { h = 0; s = 0; return; }

            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60.0;
        }

        /// <summary>
        /// Converts HSL color space to a WinUI Color.
        /// </summary>
        /// <param name="h">Hue (0-360 degrees, wraps automatically).</param>
        /// <param name="s">Saturation (0-1, clamped).</param>
        /// <param name="l">Lightness (0-1, clamped).</param>
        /// <param name="a">Alpha (0-255). Default is 255.</param>
        /// <returns>The resulting WinUI Color.</returns>
        public static Windows.UI.Color FromHSL(double h, double s, double l, byte a = 255)
        {
            h = (h % 360 + 360) % 360;
            s = Math.Clamp(s, 0, 1);
            l = Math.Clamp(l, 0, 1);

            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
            double m = l - c / 2.0;

            double r1, g1, b1;
            if (h < 60) (r1, g1, b1) = (c, x, 0);
            else if (h < 120) (r1, g1, b1) = (x, c, 0);
            else if (h < 180) (r1, g1, b1) = (0, c, x);
            else if (h < 240) (r1, g1, b1) = (0, x, c);
            else if (h < 300) (r1, g1, b1) = (x, 0, c);
            else (r1, g1, b1) = (c, 0, x);

            byte R = (byte)Math.Round((r1 + m) * 255.0);
            byte G = (byte)Math.Round((g1 + m) * 255.0);
            byte B = (byte)Math.Round((b1 + m) * 255.0);
            return Windows.UI.Color.FromArgb(a, R, G, B);
        }

        /// <summary>
        /// Converts a WinUI Color to HSV color space.
        /// </summary>
        /// <param name="c">The input color.</param>
        /// <param name="h">Hue output (0-360 degrees).</param>
        /// <param name="s">Saturation output (0-1).</param>
        /// <param name="v">Value/Brightness output (0-1).</param>
        public static void ToHSV(Windows.UI.Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;

            if (d == 0) h = 0;
            else if (max == r) h = 60.0 * ((g - b) / d % 6.0);
            else if (max == g) h = 60.0 * ((b - r) / d + 2.0);
            else h = 60.0 * ((r - g) / d + 4.0);

            if (h < 0) h += 360.0;
            v = max;
            s = max == 0 ? 0 : d / max;
        }

        /// <summary>
        /// Converts HSV color space to a WinUI Color.
        /// </summary>
        /// <param name="h">Hue (0-360 degrees, wraps automatically).</param>
        /// <param name="s">Saturation (0-1, clamped).</param>
        /// <param name="v">Value/Brightness (0-1, clamped).</param>
        /// <param name="a">Alpha (0-255). Default is 255.</param>
        /// <returns>The resulting WinUI Color.</returns>
        public static Windows.UI.Color FromHSV(double h, double s, double v, byte a = 255)
        {
            h = (h % 360 + 360) % 360;
            s = Math.Clamp(s, 0, 1);
            v = Math.Clamp(v, 0, 1);

            double c = v * s;
            double x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
            double m = v - c;

            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }

            byte R = (byte)Math.Round((r + m) * 255);
            byte G = (byte)Math.Round((g + m) * 255);
            byte B = (byte)Math.Round((b + m) * 255);
            return Windows.UI.Color.FromArgb(a, R, G, B);
        }

        // ============================================================
        // HSL INTERPOLATION
        // ============================================================

        /// <summary>
        /// Interpolates between two packed BGRA colors in HSL space using shortest hue path (legacy method).
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="LerpHSLShortest"/> for clarity. Kept for backward compatibility.
        /// </remarks>
        public static uint LerpHSL(uint a, uint b, double t)
        {
            var ca = ToColor(a);
            var cb = ToColor(b);
            ToHSL(ca, out var ha, out var sa, out var la);
            ToHSL(cb, out var hb, out var sb, out var lb);

            double dh = hb - ha;
            if (Math.Abs(dh) > 180) dh -= Math.Sign(dh) * 360;

            double h = (ha + t * dh + 360) % 360;
            double s = sa + (sb - sa) * t;
            double l = la + (lb - la) * t;
            byte alpha = (byte)Math.Round(ca.A + (cb.A - ca.A) * t);

            return ToBGRA(FromHSL(h, s, l, alpha));
        }

        /// <summary>
        /// Interpolates between two packed BGRA colors in HSL space using shortest hue path.
        /// </summary>
        /// <param name="a">Start color (packed BGRA).</param>
        /// <param name="b">End color (packed BGRA).</param>
        /// <param name="t">Interpolation factor (0-1).</param>
        /// <returns>Interpolated color (packed BGRA).</returns>
        /// <remarks>
        /// Uses <see cref="LerpHueShortest"/> to avoid hue wrapping artifacts (e.g., red to orange
        /// goes through yellows, not through blues/purples).
        /// </remarks>
        public static uint LerpHSLShortest(uint a, uint b, double t)
        {
            var ca = ToColor(a);
            var cb = ToColor(b);
            ToHSL(ca, out var ha, out var sa, out var la);
            ToHSL(cb, out var hb, out var sb, out var lb);
            var h = LerpHueShortest(ha, hb, t);
            var s = Lerp(sa, sb, t);
            var l = Lerp(la, lb, t);
            byte alpha = (byte)Math.Round(ca.A + (cb.A - ca.A) * t);
            return FromHSL_BGRA(h, s, l, alpha);
        }

        /// <summary>Clamps a value to [0, 1].</summary>
        public static double Clamp01(double v) => Math.Max(0, Math.Min(1, v));

        /// <summary>Linear interpolation between two doubles.</summary>
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>Wraps a hue value to [0, 360).</summary>
        public static double WrapHue(double h) { h %= 360; if (h < 0) h += 360; return h; }

        /// <summary>
        /// Interpolates between two hue values using the shortest angular path.
        /// </summary>
        /// <param name="h1">Start hue (0-360).</param>
        /// <param name="h2">End hue (0-360).</param>
        /// <param name="t">Interpolation factor (0-1).</param>
        /// <returns>Interpolated hue (0-360).</returns>
        /// <remarks>
        /// Handles wraparound correctly (e.g., interpolating from 350° to 10° goes through 0°, not 180°).
        /// </remarks>
        public static double LerpHueShortest(double h1, double h2, double t)
        {
            double d = ((h2 - h1 + 540) % 360) - 180;
            return (h1 + d * t + 360) % 360;
        }

        /// <summary>
        /// Convenience method to convert HSL to packed BGRA (inline).
        /// </summary>
        /// <remarks>
        /// Equivalent to <c>ToBGRA(FromHSL(h, s, l, a))</c>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FromHSL_BGRA(double h, double s, double l, byte a = 255)
            => ToBGRA(FromHSL(h, s, l, a));

        // ============================================================
        // RGB INTERPOLATION
        // ============================================================

        /// <summary>
        /// Linearly interpolates all four RGBA channels between two colors.
        /// </summary>
        /// <param name="a">Start color (BGRA).</param>
        /// <param name="b">End color (BGRA).</param>
        /// <param name="tByte">Interpolation factor (0-255), where 0 returns <paramref name="a"/> and 255 returns <paramref name="b"/>.</param>
        /// <returns>Interpolated BGRA color.</returns>
        /// <remarks>
        /// Uses floating-point interpolation with rounding for accuracy. This is NOT premultiplied alpha blending;
        /// it's a direct channel-wise lerp. For compositing, use <see cref="BlendOver"/> instead.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint LerpRGBA(uint a, uint b, byte tByte)
        {
            double t = tByte / 255.0;
            byte oa = (byte)Math.Round(GetA(a) + (GetA(b) - GetA(a)) * t);
            byte orr = (byte)Math.Round(GetR(a) + (GetR(b) - GetR(a)) * t);
            byte ogg = (byte)Math.Round(GetG(a) + (GetG(b) - GetG(a)) * t);
            byte obb = (byte)Math.Round(GetB(a) + (GetB(b) - GetB(a)) * t);
            return (uint)(oa << 24 | orr << 16 | ogg << 8 | obb);
        }

        /// <summary>
        /// Linearly interpolates RGB channels while preserving the destination alpha.
        /// </summary>
        /// <param name="dst">Destination color (BGRA).</param>
        /// <param name="fg">Foreground color (BGRA) - only RGB channels are used.</param>
        /// <param name="t">Interpolation factor (0-255).</param>
        /// <returns>Color with interpolated RGB and original destination alpha.</returns>
        /// <remarks>
        /// Uses integer math with rounding bias (+127/255) for speed. This is commonly used
        /// for opacity-based color replacement without affecting existing alpha.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint LerpRgbKeepAlpha(uint dst, uint fg, byte t)
        {
            int dr = GetR(dst), dg = GetG(dst), db = GetB(dst);
            int sr = GetR(fg), sg = GetG(fg), sb = GetB(fg);

            int rr = dr + ((sr - dr) * t + 127) / 255;
            int rg = dg + ((sg - dg) * t + 127) / 255;
            int rb = db + ((sb - db) * t + 127) / 255;

            return (dst & 0xFF000000u) | (uint)(rr << 16 | rg << 8 | rb);
        }

        // ============================================================
        // ALPHA COMPOSITING
        // ============================================================

        /// <summary>
        /// Composites a source color over a destination using Porter-Duff "over" operator with premultiplied alpha.
        /// </summary>
        /// <param name="dst">Destination color (BGRA, straight alpha).</param>
        /// <param name="src">Source color (BGRA, straight alpha).</param>
        /// <returns>Composited color (BGRA, straight alpha).</returns>
        /// <remarks>
        /// <para>
        /// Implements the standard alpha compositing formula:
        /// <code>
        /// αₒᵤₜ = αₛᵣ꜀ + αₐₛₜ(1 - αₛᵣ꜀)
        /// Cₒᵤₜ = (Cₛᵣ꜀·αₛᵣ꜀ + Cₐₛₜ·αₐₛₜ(1 - αₛᵣ꜀)) / αₒᵤₜ
        /// </code>
        /// </para>
        /// <para>
        /// Colors are converted to premultiplied alpha internally, composited, then unpremultiplied
        /// for the result. This ensures mathematically correct blending of semi-transparent layers.
        /// </para>
        /// </remarks>
        public static uint BlendOver(uint dst, uint src)
        {
            byte sa = (byte)(src >> 24);
            byte sr = (byte)(src >> 16 & 0xFF);
            byte sg = (byte)(src >> 8 & 0xFF);
            byte sb = (byte)(src & 0xFF);

            byte da = (byte)(dst >> 24);
            byte dr = (byte)(dst >> 16 & 0xFF);
            byte dg = (byte)(dst >> 8 & 0xFF);
            byte db = (byte)(dst & 0xFF);

            double Sa = sa / 255.0, Da = da / 255.0;
            double outA = Sa + Da * (1.0 - Sa);

            double sr_p = sr / 255.0 * Sa;
            double sg_p = sg / 255.0 * Sa;
            double sb_p = sb / 255.0 * Sa;

            double dr_p = dr / 255.0 * Da;
            double dg_p = dg / 255.0 * Da;
            double db_p = db / 255.0 * Da;

            double or_p = sr_p + dr_p * (1.0 - Sa);
            double og_p = sg_p + dg_p * (1.0 - Sa);
            double ob_p = sb_p + db_p * (1.0 - Sa);

            double or = outA > 0.0 ? or_p / outA : 0.0;
            double og = outA > 0.0 ? og_p / outA : 0.0;
            double ob = outA > 0.0 ? ob_p / outA : 0.0;

            byte oa = (byte)Math.Round(outA * 255.0);
            byte rr = (byte)Math.Round(Math.Clamp(or, 0.0, 1.0) * 255.0);
            byte rg = (byte)Math.Round(Math.Clamp(og, 0.0, 1.0) * 255.0);
            byte rb = (byte)Math.Round(Math.Clamp(ob, 0.0, 1.0) * 255.0);

            return (uint)(oa << 24 | rr << 16 | rg << 8 | rb);
        }

        /// <summary>
        /// Composites a semi-transparent pixel over a white background (alpha flattening).
        /// </summary>
        /// <param name="px">Source pixel (BGRA).</param>
        /// <returns>Opaque color (BGRA with alpha=255) representing the visual appearance over white.</returns>
        /// <remarks>
        /// <para>
        /// Used for export operations where transparency must be removed. Common for formats
        /// that don't support alpha channels (JPEG) or when exporting with background preservation.
        /// </para>
        /// <para>
        /// Formula: <c>C = C_src · α + 255 · (1 - α)</c>
        /// </para>
        /// </remarks>
        public static uint CompositeOverWhite(uint px)
        {
            byte a = (byte)(px >> 24);
            if (a == 255) return px;
            if (a == 0) return 0xFFFFFFFFu;

            double A = a / 255.0;
            byte b = (byte)(px & 0xFF);
            byte g = (byte)((px >> 8) & 0xFF);
            byte r = (byte)((px >> 16) & 0xFF);

            byte rw = (byte)Math.Round(r * A + 255 * (1 - A));
            byte gw = (byte)Math.Round(g * A + 255 * (1 - A));
            byte bw = (byte)Math.Round(b * A + 255 * (1 - A));

            return (uint)(0xFF << 24 | rw << 16 | gw << 8 | bw);
        }

        /// <summary>
        /// Composites a semi-transparent pixel over a specified background color.
        /// </summary>
        /// <param name="px">Source pixel (BGRA).</param>
        /// <param name="background">Background color (BGRA, alpha ignored).</param>
        /// <returns>Opaque color (BGRA with alpha=255).</returns>
        public static uint CompositeOverColor(uint px, uint background)
        {
            byte a = (byte)(px >> 24);
            if (a == 255) return px | 0xFF000000u;
            if (a == 0) return background | 0xFF000000u;

            double A = a / 255.0;
            byte pb = (byte)(px & 0xFF);
            byte pg = (byte)((px >> 8) & 0xFF);
            byte pr = (byte)((px >> 16) & 0xFF);

            byte bb = (byte)(background & 0xFF);
            byte bg = (byte)((background >> 8) & 0xFF);
            byte br = (byte)((background >> 16) & 0xFF);

            byte rOut = (byte)Math.Round(pr * A + br * (1 - A));
            byte gOut = (byte)Math.Round(pg * A + bg * (1 - A));
            byte bOut = (byte)Math.Round(pb * A + bb * (1 - A));

            return (uint)(0xFF << 24 | rOut << 16 | gOut << 8 | bOut);
        }

        // ============================================================
        // COLOR COMPARISON / DISTANCE
        // ============================================================

        /// <summary>
        /// Tests if two colors are exactly equal (all channels including alpha).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ExactRGBA(uint a, uint b) => a == b;

        /// <summary>
        /// Computes the Manhattan (L1) distance between two RGB colors.
        /// </summary>
        /// <param name="r1">Red component of first color.</param>
        /// <param name="g1">Green component of first color.</param>
        /// <param name="b1">Blue component of first color.</param>
        /// <param name="r2">Red component of second color.</param>
        /// <param name="g2">Green component of second color.</param>
        /// <param name="b2">Blue component of second color.</param>
        /// <returns>Sum of absolute channel differences (range 0-765).</returns>
        /// <remarks>
        /// Faster than Euclidean distance and sufficient for color similarity comparisons
        /// in palette extraction and clustering algorithms.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ColorDistanceManhattan(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            return Math.Abs(r1 - r2) + Math.Abs(g1 - g2) + Math.Abs(b1 - b2);
        }

        /// <summary>
        /// Computes the Manhattan (L1) distance between two packed BGRA colors (RGB only).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ColorDistanceManhattan(uint a, uint b)
        {
            return Math.Abs(GetR(a) - GetR(b)) + Math.Abs(GetG(a) - GetG(b)) + Math.Abs(GetB(a) - GetB(b));
        }

        /// <summary>
        /// Computes the Chebyshev (L∞) distance between two packed BGRA colors (max channel difference).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ColorDistanceChebyshev(uint a, uint b)
        {
            int dr = Math.Abs(GetR(a) - GetR(b));
            int dg = Math.Abs(GetG(a) - GetG(b));
            int db = Math.Abs(GetB(a) - GetB(b));
            return Math.Max(dr, Math.Max(dg, db));
        }

        /// <summary>
        /// Computes the squared Euclidean distance between two packed BGRA colors (RGB only).
        /// </summary>
        /// <remarks>
        /// Returns squared distance to avoid sqrt calculation. Compare against tolerance² for similarity testing.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ColorDistanceSquared(uint a, uint b)
        {
            int dr = GetR(a) - GetR(b);
            int dg = GetG(a) - GetG(b);
            int db = GetB(a) - GetB(b);
            return dr * dr + dg * dg + db * db;
        }

        // ============================================================
        // CONTRAST / LUMINANCE
        // ============================================================

        /// <summary>
        /// Calculates the relative luminance of a color according to WCAG 2.0 specification.
        /// </summary>
        /// <param name="bgra">Packed BGRA color.</param>
        /// <returns>Relative luminance (0-1), where 0 is black and 1 is white.</returns>
        /// <remarks>
        /// <para>
        /// Uses the WCAG formula: L = 0.2126*R + 0.7152*G + 0.0722*B, with sRGB to linear conversion.
        /// </para>
        /// <para>
        /// This is used for calculating contrast ratios and determining whether to use light or dark
        /// text over a background color.
        /// </para>
        /// </remarks>
        public static double RelativeLuminance(uint bgra)
        {
            double r = SrgbToLinear(GetR(bgra));
            double g = SrgbToLinear(GetG(bgra));
            double b = SrgbToLinear(GetB(bgra));
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        /// <summary>
        /// Determines the optimal foreground color (black or white) for maximum contrast against a background.
        /// </summary>
        /// <param name="bgra">Background color (packed BGRA).</param>
        /// <returns>White (255,255,255) or black (0,0,0) depending on which provides better contrast.</returns>
        /// <remarks>
        /// Uses WCAG contrast ratio formula: (L1 + 0.05) / (L2 + 0.05), where L1/L2 are relative luminances.
        /// Returns the color with the higher contrast ratio.
        /// </remarks>
        public static Windows.UI.Color HighContrastInk(uint bgra)
        {
            double L = RelativeLuminance(bgra);
            double cWhite = (1.0 + 0.05) / (L + 0.05);
            double cBlack = (L + 0.05) / 0.05;
            return cWhite >= cBlack
                ? Windows.UI.Color.FromArgb(255, 255, 255, 255)
                : Windows.UI.Color.FromArgb(255, 0, 0, 0);
        }

        /// <summary>
        /// Converts an sRGB component (0-255) to linear RGB (0-1).
        /// </summary>
        /// <remarks>
        /// Applies the sRGB inverse gamma curve per ITU-R BT.709.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SrgbToLinear(byte c)
        {
            double v = c / 255.0;
            return (v <= 0.03928) ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        /// <summary>
        /// Converts a linear RGB component (0-1) to sRGB (0-255).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte LinearToSrgb(double linear)
        {
            double v = linear <= 0.0031308
                ? linear * 12.92
                : 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
            return (byte)Math.Clamp(Math.Round(v * 255.0), 0, 255);
        }

        // ============================================================
        // PIXEL BUFFER I/O
        // ============================================================

        /// <summary>
        /// Reads a packed BGRA pixel from a byte array at the specified index.
        /// </summary>
        /// <param name="p">Pixel buffer (BGRA byte array).</param>
        /// <param name="idx">Byte index (must be a multiple of 4 for proper alignment).</param>
        /// <returns>Packed BGRA color as 32-bit unsigned integer.</returns>
        /// <remarks>
        /// Assumes the buffer is in BGRA format: [B, G, R, A, B, G, R, A, ...].
        /// No bounds checking is performed for performance; ensure <paramref name="idx"/> is valid.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadPixel(byte[] p, int idx)
            => (uint)(p[idx + 3] << 24 | p[idx + 2] << 16 | p[idx + 1] << 8 | p[idx + 0]);

        /// <summary>
        /// Writes a packed BGRA pixel to a byte array at the specified index.
        /// </summary>
        /// <param name="p">Pixel buffer (BGRA byte array).</param>
        /// <param name="idx">Byte index (must be a multiple of 4 for proper alignment).</param>
        /// <param name="c">Packed BGRA color to write.</param>
        /// <remarks>
        /// Writes in BGRA order: [B, G, R, A]. No bounds checking is performed for performance.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePixel(byte[] p, int idx, uint c)
        {
            p[idx + 0] = (byte)(c & 0xFF);
            p[idx + 1] = (byte)((c >> 8) & 0xFF);
            p[idx + 2] = (byte)((c >> 16) & 0xFF);
            p[idx + 3] = (byte)((c >> 24) & 0xFF);
        }

        /// <summary>
        /// Reads a BGRA pixel from a byte buffer at coordinates (x, y).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadPixelAt(byte[] src, int width, int x, int y)
        {
            int i = (y * width + x) * 4;
            return ReadPixel(src, i);
        }

        /// <summary>
        /// Writes a BGRA pixel to a byte buffer at coordinates (x, y).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePixelAt(byte[] dst, int width, int x, int y, uint c)
        {
            int i = (y * width + x) * 4;
            WritePixel(dst, i, c);
        }

        // ============================================================
        // HSV (DOUBLE-BASED FOR PERFORMANCE)
        // ============================================================

        /// <summary>
        /// Converts RGB values (0-1) to HSV color space.
        /// </summary>
        /// <param name="r">Red (0-1).</param>
        /// <param name="g">Green (0-1).</param>
        /// <param name="b">Blue (0-1).</param>
        /// <param name="h">Hue output (0-360).</param>
        /// <param name="s">Saturation output (0-1).</param>
        /// <param name="v">Value output (0-1).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
        {
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            v = max;

            if (delta < 1e-6)
            {
                h = 0;
                s = 0;
                return;
            }

            s = max <= 0 ? 0 : delta / max;

            if (max == r)
                h = 60.0 * (((g - b) / delta) % 6.0);
            else if (max == g)
                h = 60.0 * (((b - r) / delta) + 2.0);
            else
                h = 60.0 * (((r - g) / delta) + 4.0);

            if (h < 0) h += 360.0;
        }

        /// <summary>
        /// Converts HSV to RGB values (0-1).
        /// </summary>
        /// <param name="h">Hue (0-360).</param>
        /// <param name="s">Saturation (0-1).</param>
        /// <param name="v">Value (0-1).</param>
        /// <param name="r">Red output (0-1).</param>
        /// <param name="g">Green output (0-1).</param>
        /// <param name="b">Blue output (0-1).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HsvToRgb(double h, double s, double v, out double r, out double g, out double b)
        {
            if (s <= 0)
            {
                r = g = b = v;
                return;
            }

            double c = v * s;
            double hh = h / 60.0;
            double x = c * (1.0 - Math.Abs(hh % 2.0 - 1.0));

            double r1, g1, b1;
            if (hh < 1) { r1 = c; g1 = x; b1 = 0; }
            else if (hh < 2) { r1 = x; g1 = c; b1 = 0; }
            else if (hh < 3) { r1 = 0; g1 = c; b1 = x; }
            else if (hh < 4) { r1 = 0; g1 = x; b1 = c; }
            else if (hh < 5) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            double m = v - c;
            r = r1 + m;
            g = g1 + m;
            b = b1 + m;
        }
    }
}