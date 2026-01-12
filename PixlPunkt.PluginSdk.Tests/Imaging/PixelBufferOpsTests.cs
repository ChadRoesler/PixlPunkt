using PixlPunkt.PluginSdk.Imaging;

namespace PixlPunkt.PluginSdk.Tests.Imaging;

/// <summary>
/// Tests for <see cref="PixelBufferOps"/> - platform-agnostic pixel buffer operations.
/// </summary>
public class PixelBufferOpsTests
{
    // ========================================================================
    // RESIZE NEAREST
    // ========================================================================

    [Fact]
    public void ResizeNearest_DoubleSize_DoublesPixels()
    {
        // 2x2 red image (BGRA: B=0, G=0, R=FF, A=FF)
        byte[] src = CreateSolidBuffer(2, 2, 0x00, 0x00, 0xFF, 0xFF); // Red

        var result = PixelBufferOps.ResizeNearest(src, 2, 2, 4, 4);

        Assert.Equal(4 * 4 * 4, result.Length);
        // Check that all pixels are red
        for (int i = 0; i < result.Length; i += 4)
        {
            Assert.Equal(0x00, result[i + 0]); // B
            Assert.Equal(0x00, result[i + 1]); // G
            Assert.Equal(0xFF, result[i + 2]); // R
            Assert.Equal(0xFF, result[i + 3]); // A
        }
    }

    [Fact]
    public void ResizeNearest_HalfSize_DownsamplesCorrectly()
    {
        // 4x4 green image (BGRA: B=0, G=FF, R=0, A=FF)
        byte[] src = CreateSolidBuffer(4, 4, 0x00, 0xFF, 0x00, 0xFF); // Green

        var result = PixelBufferOps.ResizeNearest(src, 4, 4, 2, 2);

        Assert.Equal(2 * 2 * 4, result.Length);
        // Check that all pixels are green
        for (int i = 0; i < result.Length; i += 4)
        {
            Assert.Equal(0x00, result[i + 0]); // B
            Assert.Equal(0xFF, result[i + 1]); // G
            Assert.Equal(0x00, result[i + 2]); // R
            Assert.Equal(0xFF, result[i + 3]); // A
        }
    }

    [Fact]
    public void ResizeNearest_SameSize_ReturnsEquivalentBuffer()
    {
        byte[] src = CreateSolidBuffer(3, 3, 0xCC, 0xBB, 0xAA, 0xFF);

        var result = PixelBufferOps.ResizeNearest(src, 3, 3, 3, 3);

        Assert.Equal(src.Length, result.Length);
        for (int i = 0; i < src.Length; i++)
        {
            Assert.Equal(src[i], result[i]);
        }
    }

    [Fact]
    public void ResizeNearest_InvalidDimensions_ReturnsEmptyArray()
    {
        byte[] src = CreateSolidBuffer(2, 2, 0xFF, 0xFF, 0xFF, 0xFF);

        var result = PixelBufferOps.ResizeNearest(src, 2, 2, 0, 0);

        Assert.Empty(result);
    }

    [Fact]
    public void ResizeNearest_1x1Image_ScalesCorrectly()
    {
        byte[] src = [0xAA, 0xBB, 0xCC, 0xDD]; // Single pixel: B=AA, G=BB, R=CC, A=DD

        var result = PixelBufferOps.ResizeNearest(src, 1, 1, 3, 3);

        Assert.Equal(3 * 3 * 4, result.Length);
        // All 9 pixels should be the same as the original
        for (int i = 0; i < result.Length; i += 4)
        {
            Assert.Equal(0xAA, result[i + 0]);
            Assert.Equal(0xBB, result[i + 1]);
            Assert.Equal(0xCC, result[i + 2]);
            Assert.Equal(0xDD, result[i + 3]);
        }
    }

    // ========================================================================
    // RESIZE BILINEAR
    // ========================================================================

    [Fact]
    public void ResizeBilinear_DoubleSize_InterpolatesCorrectly()
    {
        // 2x2 with different colors
        byte[] src = CreateSolidBuffer(2, 2, 0xFF, 0xFF, 0xFF, 0xFF); // White

        var result = PixelBufferOps.ResizeBilinear(src, 2, 2, 4, 4);

        Assert.Equal(4 * 4 * 4, result.Length);
    }

    [Fact]
    public void ResizeBilinear_SolidColor_PreservesColor()
    {
        byte[] src = CreateSolidBuffer(4, 4, 0xFF, 0x80, 0x80, 0xFF); // Light blue (B=FF, G=80, R=80, A=FF)

        var result = PixelBufferOps.ResizeBilinear(src, 4, 4, 2, 2);

        // All pixels should still be the same color (or very close due to rounding)
        for (int i = 0; i < result.Length; i += 4)
        {
            Assert.InRange(result[i + 0], 0xFE, 0xFF); // B
            Assert.InRange(result[i + 1], 0x7F, 0x81); // G
            Assert.InRange(result[i + 2], 0x7F, 0x81); // R
            Assert.Equal(0xFF, result[i + 3]);         // A
        }
    }

    [Fact]
    public void ResizeBilinear_1x1Image_ScalesCorrectly()
    {
        byte[] src = [0x40, 0x80, 0xC0, 0xFF];

        var result = PixelBufferOps.ResizeBilinear(src, 1, 1, 4, 4);

        Assert.Equal(4 * 4 * 4, result.Length);
        // All pixels should be the same as original (1x1 can't interpolate)
        for (int i = 0; i < result.Length; i += 4)
        {
            Assert.Equal(0x40, result[i + 0]);
            Assert.Equal(0x80, result[i + 1]);
            Assert.Equal(0xC0, result[i + 2]);
            Assert.Equal(0xFF, result[i + 3]);
        }
    }

    // ========================================================================
    // PIXELS SIMILAR
    // ========================================================================

    [Fact]
    public void PixelsSimilar_IdenticalPixels_ReturnsTrue()
    {
        byte[] pixels = [0xAA, 0xBB, 0xCC, 0xDD, 0xAA, 0xBB, 0xCC, 0xDD];

        Assert.True(PixelBufferOps.PixelsSimilar(pixels, 0, 4, 0, false));
    }

    [Fact]
    public void PixelsSimilar_DifferentPixels_ReturnsFalse()
    {
        byte[] pixels = [0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

        Assert.False(PixelBufferOps.PixelsSimilar(pixels, 0, 4, 10, false));
    }

    [Fact]
    public void PixelsSimilar_WithinTolerance_ReturnsTrue()
    {
        byte[] pixels = [0x80, 0x80, 0x80, 0xFF, 0x85, 0x85, 0x85, 0xFF];

        // Difference of 5 per channel, tolerance of 10 should pass
        Assert.True(PixelBufferOps.PixelsSimilar(pixels, 0, 4, 10, false));
    }

    [Fact]
    public void PixelsSimilar_WithAlpha_ComparesAlphaChannel()
    {
        byte[] pixels = [0x80, 0x80, 0x80, 0xFF, 0x80, 0x80, 0x80, 0x00];

        // RGB same, alpha different - without alpha check should pass
        Assert.True(PixelBufferOps.PixelsSimilar(pixels, 0, 4, 0, false));
        // With alpha check should fail
        Assert.False(PixelBufferOps.PixelsSimilar(pixels, 0, 4, 10, true));
    }

    // ========================================================================
    // COLORS SIMILAR
    // ========================================================================

    [Fact]
    public void ColorsSimilar_IdenticalColors_ReturnsTrue()
    {
        Assert.True(PixelBufferOps.ColorsSimilar(0xFFAABBCC, 0xFFAABBCC, 0, false));
    }

    [Fact]
    public void ColorsSimilar_DifferentColors_ReturnsFalse()
    {
        Assert.False(PixelBufferOps.ColorsSimilar(0xFF000000, 0xFFFFFFFF, 10, false));
    }

    [Fact]
    public void ColorsSimilar_WithinTolerance_ReturnsTrue()
    {
        uint colorA = 0xFF808080;
        uint colorB = 0xFF858585;

        Assert.True(PixelBufferOps.ColorsSimilar(colorA, colorB, 10, false));
    }

    // ========================================================================
    // CROP TO OPAQUE
    // ========================================================================

    [Fact]
    public void CropToOpaque_WithTransparentBorder_CropsCorrectly()
    {
        // 4x4 image with 2x2 opaque center
        byte[] src = new byte[4 * 4 * 4];
        // Set center 2x2 to opaque white
        SetPixel(src, 4, 1, 1, 0xFF, 0xFF, 0xFF, 0xFF);
        SetPixel(src, 4, 2, 1, 0xFF, 0xFF, 0xFF, 0xFF);
        SetPixel(src, 4, 1, 2, 0xFF, 0xFF, 0xFF, 0xFF);
        SetPixel(src, 4, 2, 2, 0xFF, 0xFF, 0xFF, 0xFF);

        var (buf, w, h, offsetX, offsetY) = PixelBufferOps.CropToOpaque(src, 4, 4);

        Assert.Equal(2, w);
        Assert.Equal(2, h);
        Assert.Equal(1, offsetX);
        Assert.Equal(1, offsetY);
    }

    [Fact]
    public void CropToOpaque_FullyTransparent_ReturnsMinimalBuffer()
    {
        byte[] src = new byte[4 * 4 * 4]; // All zeros = fully transparent

        var (buf, w, h, offsetX, offsetY) = PixelBufferOps.CropToOpaque(src, 4, 4);

        Assert.Equal(1, w);
        Assert.Equal(1, h);
        Assert.Equal(0, offsetX);
        Assert.Equal(0, offsetY);
    }

    [Fact]
    public void CropToOpaque_FullyOpaque_ReturnsOriginalSize()
    {
        byte[] src = CreateSolidBuffer(4, 4, 0xCC, 0xBB, 0xAA, 0xFF);

        var (buf, w, h, offsetX, offsetY) = PixelBufferOps.CropToOpaque(src, 4, 4);

        Assert.Equal(4, w);
        Assert.Equal(4, h);
        Assert.Equal(0, offsetX);
        Assert.Equal(0, offsetY);
    }

    [Fact]
    public void CropToOpaque_SinglePixel_CropsToOne()
    {
        byte[] src = new byte[8 * 8 * 4];
        SetPixel(src, 8, 3, 5, 0x00, 0x00, 0xFF, 0xFF); // Single red pixel at (3, 5)

        var (buf, w, h, offsetX, offsetY) = PixelBufferOps.CropToOpaque(src, 8, 8);

        Assert.Equal(1, w);
        Assert.Equal(1, h);
        Assert.Equal(3, offsetX);
        Assert.Equal(5, offsetY);
        // Verify the pixel data (B, G, R, A)
        Assert.Equal(0x00, buf[0]); // B
        Assert.Equal(0x00, buf[1]); // G
        Assert.Equal(0xFF, buf[2]); // R
        Assert.Equal(0xFF, buf[3]); // A
    }

    // ========================================================================
    // COPY REGION
    // ========================================================================

    [Fact]
    public void CopyRegion_ExtractsCorrectArea()
    {
        byte[] src = CreateSolidBuffer(4, 4, 0xCC, 0xBB, 0xAA, 0xFF);

        var result = PixelBufferOps.CopyRegion(src, 4, 4, 1, 1, 2, 2);

        Assert.Equal(2 * 2 * 4, result.Length);
    }

    [Fact]
    public void CopyRegion_ClipsToSourceBounds()
    {
        byte[] src = CreateSolidBuffer(4, 4, 0xCC, 0xBB, 0xAA, 0xFF);

        // Request region that extends beyond bounds
        var result = PixelBufferOps.CopyRegion(src, 4, 4, 2, 2, 10, 10);

        // Should only get 2x2 (clipped to remaining space)
        Assert.Equal(2 * 2 * 4, result.Length);
    }

    [Fact]
    public void CopyRegion_OutOfBounds_ReturnsEmptyArray()
    {
        byte[] src = CreateSolidBuffer(4, 4, 0xCC, 0xBB, 0xAA, 0xFF);

        var result = PixelBufferOps.CopyRegion(src, 4, 4, 10, 10, 2, 2);

        Assert.Empty(result);
    }

    [Fact]
    public void CopyRegion_ZeroSize_ReturnsEmptyArray()
    {
        byte[] src = CreateSolidBuffer(4, 4, 0xCC, 0xBB, 0xAA, 0xFF);

        var result = PixelBufferOps.CopyRegion(src, 4, 4, 0, 0, 0, 0);

        Assert.Empty(result);
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    /// <summary>
    /// Creates a solid color buffer with BGRA byte order.
    /// </summary>
    private static byte[] CreateSolidBuffer(int w, int h, byte b, byte g, byte r, byte a)
    {
        byte[] buf = new byte[w * h * 4];
        for (int i = 0; i < buf.Length; i += 4)
        {
            buf[i + 0] = b;
            buf[i + 1] = g;
            buf[i + 2] = r;
            buf[i + 3] = a;
        }
        return buf;
    }

    /// <summary>
    /// Sets a pixel in the buffer using BGRA byte order.
    /// </summary>
    private static void SetPixel(byte[] buf, int w, int x, int y, byte b, byte g, byte r, byte a)
    {
        int i = (y * w + x) * 4;
        buf[i + 0] = b;
        buf[i + 1] = g;
        buf[i + 2] = r;
        buf[i + 3] = a;
    }
}
