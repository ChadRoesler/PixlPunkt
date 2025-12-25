using System.Runtime.CompilerServices;

namespace PixlPunkt.PluginSdk.Imaging
{
    /// <summary>
    /// Platform-agnostic color utility methods for plugin development.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ColorUtil"/> provides common color manipulation operations without
    /// platform-specific dependencies (no WinUI Color struct). All operations use
    /// packed BGRA uint32 format for maximum portability.
    /// </para>
    /// <para>
    /// <strong>BGRA Format:</strong>
    /// Colors are packed as 32-bit unsigned integers: <c>0xAARRGGBB</c>
    /// <br/>- Bits 0-7: Blue
    /// <br/>- Bits 8-15: Green
    /// <br/>- Bits 16-23: Red
    /// <br/>- Bits 24-31: Alpha
    /// </para>
    /// <para>
    /// <strong>Categories:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Pack/Unpack:</strong> Convert between byte components and packed uint32</item>
    /// <item><strong>Channel Access:</strong> Extract individual color channels</item>
    /// <item><strong>Alpha Operations:</strong> Modify alpha, strip alpha, blend</item>
    /// <item><strong>Interpolation:</strong> Linear interpolation in RGB and HSL space</item>
    /// <item><strong>Color Distance:</strong> Similarity metrics for color comparison</item>
    /// <item><strong>Luminance:</strong> Brightness calculations for contrast decisions</item>
    /// </list>
    /// </remarks>
    public static class ColorUtil
    {
        // ====================================================================
        // PACK / UNPACK
        // ====================================================================

        /// <summary>
        /// Packs byte components into a BGRA uint32.
        /// </summary>
        /// <param name="b">Blue component (0-255).</param>
        /// <param name="g">Green component (0-255).</param>
        /// <param name="r">Red component (0-255).</param>
        /// <param name="a">Alpha component (0-255). Default is 255 (opaque).</param>
        /// <returns>Packed BGRA as 0xAARRGGBB.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackBgra(byte b, byte g, byte r, byte a = 255)
            => (uint)(b | (g << 8) | (r << 16) | (a << 24));

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

        /// <summary>
        /// Unpacks a BGRA uint32 into byte components.
        /// </summary>
        /// <param name="bgra">Packed color as 0xAARRGGBB.</param>
        /// <param name="b">Blue component output.</param>
        /// <param name="g">Green component output.</param>
        /// <param name="r">Red component output.</param>
        /// <param name="a">Alpha component output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnpackBgra(uint bgra, out byte b, out byte g, out byte r, out byte a)
        {
            b = (byte)(bgra & 0xFF);
            g = (byte)((bgra >> 8) & 0xFF);
            r = (byte)((bgra >> 16) & 0xFF);
            a = (byte)((bgra >> 24) & 0xFF);
        }

        // ====================================================================
        // CHANNEL ACCESS
        // ====================================================================

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

        // ====================================================================
        // ALPHA OPERATIONS
        // ====================================================================

        /// <summary>
        /// Sets the alpha channel of packed BGRA to 255 (fully opaque).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MakeOpaque(uint bgra) => bgra | 0xFF000000u;

        /// <summary>
        /// Strips the alpha channel from packed BGRA (sets A=0).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint StripAlpha(uint bgra) => bgra & 0x00FFFFFFu;

        /// <summary>
        /// Returns a new packed BGRA with the alpha channel replaced.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint SetAlpha(uint bgra, byte a)
            => (bgra & 0x00FFFFFFu) | ((uint)a << 24);

        /// <summary>
        /// Compares two packed BGRA values for RGB equality (ignores alpha).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RgbEqual(uint a, uint b) => (a & 0x00FFFFFFu) == (b & 0x00FFFFFFu);

        // ====================================================================
        // ALPHA COMPOSITING
        // ====================================================================

        /// <summary>
        /// Composites a source color over a destination using Porter-Duff "over" operator.
        /// </summary>
        /// <param name="dst">Destination color (BGRA, straight alpha).</param>
        /// <param name="src">Source color (BGRA, straight alpha).</param>
        /// <returns>Composited color (BGRA, straight alpha).</returns>
        /// <remarks>
        /// Implements the standard alpha compositing formula for proper blending
        /// of semi-transparent colors.
        /// </remarks>
        public static uint BlendOver(uint dst, uint src)
        {
            byte sa = (byte)(src >> 24);
            if (sa == 0) return dst;
            if (sa == 255) return src;

            byte sr = (byte)(src >> 16);
            byte sg = (byte)(src >> 8);
            byte sb = (byte)src;

            byte da = (byte)(dst >> 24);
            byte dr = (byte)(dst >> 16);
            byte dg = (byte)(dst >> 8);
            byte db = (byte)dst;

            int invA = 255 - sa;
            byte outA = (byte)Math.Min(255, sa + (da * invA + 127) / 255);

            if (outA == 0) return 0;

            int divisor = Math.Max(1, (int)outA);
            byte outR = (byte)((sr * sa + dr * da * invA / 255 + 127) / divisor * outA / 255);
            byte outG = (byte)((sg * sa + dg * da * invA / 255 + 127) / divisor * outA / 255);
            byte outB = (byte)((sb * sa + db * da * invA / 255 + 127) / divisor * outA / 255);

            return (uint)((outA << 24) | (outR << 16) | (outG << 8) | outB);
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

            double af = a / 255.0;
            byte pb = (byte)(px & 0xFF);
            byte pg = (byte)((px >> 8) & 0xFF);
            byte pr = (byte)((px >> 16) & 0xFF);

            byte bb = (byte)(background & 0xFF);
            byte bg = (byte)((background >> 8) & 0xFF);
            byte br = (byte)((background >> 16) & 0xFF);

            byte rOut = (byte)Math.Round(pr * af + br * (1 - af));
            byte gOut = (byte)Math.Round(pg * af + bg * (1 - af));
            byte bOut = (byte)Math.Round(pb * af + bb * (1 - af));

            return (uint)(0xFF000000u | ((uint)rOut << 16) | ((uint)gOut << 8) | bOut);
        }

        // ====================================================================
        // RGB INTERPOLATION
        // ====================================================================

        /// <summary>
        /// Linearly interpolates RGB channels while preserving the destination alpha.
        /// </summary>
        /// <param name="dst">Destination color (BGRA).</param>
        /// <param name="fg">Foreground color (BGRA) - only RGB channels are used.</param>
        /// <param name="t">Interpolation factor (0-255).</param>
        /// <returns>Color with interpolated RGB and original destination alpha.</returns>
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

        // ====================================================================
        // COLOR DISTANCE
        // ====================================================================

        /// <summary>
        /// Computes the Manhattan (L1) distance between two packed BGRA colors (RGB only).
        /// </summary>
        /// <remarks>
        /// Sum of absolute channel differences. Range: 0-765.
        /// Fast metric suitable for color similarity comparisons.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ColorDistanceManhattan(uint a, uint b)
        {
            return Math.Abs(GetR(a) - GetR(b)) + Math.Abs(GetG(a) - GetG(b)) + Math.Abs(GetB(a) - GetB(b));
        }

        /// <summary>
        /// Computes the Chebyshev (L?) distance between two packed BGRA colors.
        /// </summary>
        /// <remarks>
        /// Maximum channel difference. Range: 0-255.
        /// Useful for tolerance-based color matching.
        /// </remarks>
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
        /// Returns squared distance to avoid sqrt. Compare against tolerance² for similarity testing.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ColorDistanceSquared(uint a, uint b)
        {
            int dr = GetR(a) - GetR(b);
            int dg = GetG(a) - GetG(b);
            int db = GetB(a) - GetB(b);
            return dr * dr + dg * dg + db * db;
        }

        // ====================================================================
        // LUMINANCE
        // ====================================================================

        /// <summary>
        /// Calculates a fast approximate luminance (perceived brightness) of a color.
        /// </summary>
        /// <param name="bgra">Packed BGRA color.</param>
        /// <returns>Luminance value (0-255).</returns>
        /// <remarks>
        /// Uses integer approximation of standard luminance weights:
        /// L ? (77*R + 150*G + 29*B) / 256
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte FastLuminance(uint bgra)
        {
            int r = GetR(bgra);
            int g = GetG(bgra);
            int b = GetB(bgra);
            return (byte)((77 * r + 150 * g + 29 * b) >> 8);
        }

        /// <summary>
        /// Determines whether to use light or dark text over a background color.
        /// </summary>
        /// <param name="bgra">Background color (packed BGRA).</param>
        /// <returns>True if light (white) text should be used; false for dark (black) text.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldUseLightText(uint bgra)
        {
            return FastLuminance(bgra) < 128;
        }

        // ====================================================================
        // PIXEL BUFFER I/O
        // ====================================================================

        /// <summary>
        /// Reads a packed BGRA pixel from a byte array at the specified index.
        /// </summary>
        /// <param name="pixels">Pixel buffer (BGRA byte array).</param>
        /// <param name="idx">Byte index (must be a multiple of 4).</param>
        /// <returns>Packed BGRA color.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadPixel(byte[] pixels, int idx)
            => (uint)(pixels[idx + 3] << 24 | pixels[idx + 2] << 16 | pixels[idx + 1] << 8 | pixels[idx + 0]);

        /// <summary>
        /// Writes a packed BGRA pixel to a byte array at the specified index.
        /// </summary>
        /// <param name="pixels">Pixel buffer (BGRA byte array).</param>
        /// <param name="idx">Byte index (must be a multiple of 4).</param>
        /// <param name="color">Packed BGRA color to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePixel(byte[] pixels, int idx, uint color)
        {
            pixels[idx + 0] = (byte)(color & 0xFF);
            pixels[idx + 1] = (byte)((color >> 8) & 0xFF);
            pixels[idx + 2] = (byte)((color >> 16) & 0xFF);
            pixels[idx + 3] = (byte)((color >> 24) & 0xFF);
        }
    }
}
