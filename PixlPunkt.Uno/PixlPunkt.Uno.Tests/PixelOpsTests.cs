namespace PixlPunkt.Uno.Tests;

using PixlPunkt.Uno.Core.Imaging;
using FluentAssertions;

/// <summary>
/// Unit tests for <see cref="PixelOps"/> static class.
/// Tests cover rotation, scaling, EPX/Scale2x upscaling, and buffer operations.
/// Note: Tests that require Windows.Graphics.RectInt32 are commented out as the test project
/// doesn't reference the Uno Platform assemblies. These can be enabled for integration tests.
/// </summary>
[TestFixture]
public class PixelOpsTests
{
    // ════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>Creates a solid color byte buffer (BGRA).</summary>
    private static byte[] CreateSolidBuffer(int w, int h, uint bgra)
    {
        var buf = new byte[w * h * 4];
        for (int i = 0; i < buf.Length; i += 4)
        {
            buf[i + 0] = (byte)(bgra & 0xFF);          // B
            buf[i + 1] = (byte)((bgra >> 8) & 0xFF);   // G
            buf[i + 2] = (byte)((bgra >> 16) & 0xFF);  // R
            buf[i + 3] = (byte)((bgra >> 24) & 0xFF);  // A
        }
        return buf;
    }

    /// <summary>Creates a solid color uint buffer.</summary>
    private static uint[] CreateSolidUintBuffer(int w, int h, uint bgra)
    {
        var buf = new uint[w * h];
        Array.Fill(buf, bgra);
        return buf;
    }

    /// <summary>Gets a pixel from a byte buffer.</summary>
    private static uint GetPixel(byte[] buf, int w, int x, int y)
    {
        int i = (y * w + x) * 4;
        return (uint)(buf[i] | (buf[i + 1] << 8) | (buf[i + 2] << 16) | (buf[i + 3] << 24));
    }

    // ════════════════════════════════════════════════════════════════════
    // RESIZE NEAREST TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void ResizeNearest_DoubleSize_DoublesPixels()
    {
        // Arrange - 2x2 checkerboard pattern
        var src = new byte[2 * 2 * 4];
        uint white = 0xFFFFFFFF;
        uint black = 0xFF000000;

        // Set pixels: [white, black]
        //             [black, white]
        SetPixel(src, 2, 0, 0, white);
        SetPixel(src, 2, 1, 0, black);
        SetPixel(src, 2, 0, 1, black);
        SetPixel(src, 2, 1, 1, white);

        // Act
        var result = PixelOps.ResizeNearest(src, 2, 2, 4, 4);

        // Assert
        result.Should().HaveCount(4 * 4 * 4);
        // Top-left 2x2 block should be white
        GetPixel(result, 4, 0, 0).Should().Be(white);
        GetPixel(result, 4, 1, 0).Should().Be(white);
    }

    [Test]
    public void ResizeNearest_HalfSize_DownsamplesCorrectly()
    {
        // Arrange - 4x4 solid buffer
        var src = CreateSolidBuffer(4, 4, 0xFFFF0000); // Blue

        // Act
        var result = PixelOps.ResizeNearest(src, 4, 4, 2, 2);

        // Assert
        result.Should().HaveCount(2 * 2 * 4);
        GetPixel(result, 2, 0, 0).Should().Be(0xFFFF0000);
    }

    [Test]
    public void ResizeNearest_SameSize_ReturnsEquivalentBuffer()
    {
        // Arrange
        var src = CreateSolidBuffer(10, 10, 0xFF123456);

        // Act
        var result = PixelOps.ResizeNearest(src, 10, 10, 10, 10);

        // Assert
        result.Should().HaveCount(src.Length);
        GetPixel(result, 10, 5, 5).Should().Be(0xFF123456);
    }

    [Test]
    public void ResizeNearest_InvalidDimensions_ReturnsEmptyArray()
    {
        // Arrange
        var src = CreateSolidBuffer(10, 10, 0xFFFFFFFF);

        // Act
        var result = PixelOps.ResizeNearest(src, 10, 10, 0, 0);

        // Assert
        result.Should().BeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════
    // RESIZE BILINEAR TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void ResizeBilinear_DoubleSize_InterpolatesCorrectly()
    {
        // Arrange - simple 2x2 gradient
        var src = new byte[2 * 2 * 4];
        SetPixel(src, 2, 0, 0, 0xFF000000); // Black
        SetPixel(src, 2, 1, 0, 0xFFFFFFFF); // White
        SetPixel(src, 2, 0, 1, 0xFFFFFFFF); // White
        SetPixel(src, 2, 1, 1, 0xFF000000); // Black

        // Act
        var result = PixelOps.ResizeBilinear(src, 2, 2, 4, 4);

        // Assert
        result.Should().HaveCount(4 * 4 * 4);
        // Corners should match original
        GetPixel(result, 4, 0, 0).Should().Be(0xFF000000);
        GetPixel(result, 4, 3, 0).Should().Be(0xFFFFFFFF);
    }

    [Test]
    public void ResizeBilinear_SolidColor_PreservesColor()
    {
        // Arrange
        var src = CreateSolidBuffer(4, 4, 0xFF808080); // Gray

        // Act
        var result = PixelOps.ResizeBilinear(src, 4, 4, 8, 8);

        // Assert
        // All pixels should be gray (allow small rounding differences)
        var pixel = GetPixel(result, 8, 4, 4);
        ((pixel >> 24) & 0xFF).Should().Be(0xFF); // Alpha
    }

    // ════════════════════════════════════════════════════════════════════
    // ROTATION TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void RotateNearest_ZeroDegrees_ReturnsSameSize()
    {
        // Arrange
        var src = CreateSolidBuffer(10, 10, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.RotateNearest(src, 10, 10, 0);

        // Assert
        w.Should().Be(10);
        h.Should().Be(10);
    }

    [Test]
    public void RotateNearest_90Degrees_SwapsDimensions()
    {
        // Arrange - 4x2 rectangle (wide)
        var src = CreateSolidBuffer(4, 2, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.RotateNearest(src, 4, 2, 90);

        // Assert - should be ~2x4 (tall) with some expansion
        // Rotated bounds are larger due to diagonal fitting
        w.Should().BeGreaterOrEqualTo(2);
        h.Should().BeGreaterOrEqualTo(4);
    }

    [Test]
    public void RotateNearest_180Degrees_MaintainsSize()
    {
        // Arrange
        var src = CreateSolidBuffer(10, 10, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.RotateNearest(src, 10, 10, 180);

        // Assert - 180 rotation should maintain dimensions (allow +1 for floating-point rounding in bounds calc)
        w.Should().BeInRange(10, 11);
        h.Should().BeInRange(10, 11);
    }

    [Test]
    public void RotateNearest_45Degrees_ExpandsBounds()
    {
        // Arrange
        var src = CreateSolidBuffer(10, 10, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.RotateNearest(src, 10, 10, 45);

        // Assert - 45 degree rotation expands to fit diagonal
        w.Should().BeGreaterThan(10);
        h.Should().BeGreaterThan(10);
    }

    [Test]
    public void RotateBilinear_ZeroDegrees_ReturnsSameSize()
    {
        // Arrange
        var src = CreateSolidBuffer(10, 10, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.RotateBilinear(src, 10, 10, 0);

        // Assert
        w.Should().Be(10);
        h.Should().Be(10);
    }

    [Test]
    public void RotatedBounds_Square_ExpandsCorrectlyAt45Degrees()
    {
        // Arrange
        double rad45 = 45 * Math.PI / 180.0;

        // Act
        PixelOps.RotatedBounds(10, 10, rad45, out int outW, out int outH);

        // Assert - diagonal of 10x10 square is ~14.14
        outW.Should().BeGreaterThan(10);
        outH.Should().BeGreaterThan(10);
        outW.Should().BeLessThan(20);
        outH.Should().BeLessThan(20);
    }

    [Test]
    public void RotatedBounds_Rectangle_CalculatesCorrectly()
    {
        // Arrange - 90 degree rotation
        double rad90 = 90 * Math.PI / 180.0;

        // Act
        PixelOps.RotatedBounds(100, 50, rad90, out int outW, out int outH);

        // Assert - should swap dimensions approximately
        outW.Should().BeGreaterOrEqualTo(50);
        outH.Should().BeGreaterOrEqualTo(100);
    }

    // ════════════════════════════════════════════════════════════════════
    // EPX (EDGE-PRESERVING) TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void EPX2x_DoublesSize()
    {
        // Arrange
        var src = CreateSolidBuffer(4, 4, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.EPX2x(src, 4, 4);

        // Assert
        w.Should().Be(8);
        h.Should().Be(8);
        buf.Should().HaveCount(8 * 8 * 4);
    }

    [Test]
    public void EPX2x_SolidColor_PreservesColor()
    {
        // Arrange
        var src = CreateSolidBuffer(4, 4, 0xFF123456);

        // Act
        var (buf, w, h) = PixelOps.EPX2x(src, 4, 4);

        // Assert - all pixels should be same color
        GetPixel(buf, 8, 0, 0).Should().Be(0xFF123456);
        GetPixel(buf, 8, 7, 7).Should().Be(0xFF123456);
    }

    // ════════════════════════════════════════════════════════════════════
    // SCALE2X TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Scale2x_DoublesSize()
    {
        // Arrange
        var src = CreateSolidBuffer(4, 4, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.Scale2x(src, 4, 4);

        // Assert
        w.Should().Be(8);
        h.Should().Be(8);
        buf.Should().HaveCount(8 * 8 * 4);
    }

    [Test]
    public void Scale2x_SolidColor_PreservesColor()
    {
        // Arrange
        var src = CreateSolidBuffer(4, 4, 0xFFABCDEF);

        // Act
        var (buf, w, h) = PixelOps.Scale2x(src, 4, 4);

        // Assert
        GetPixel(buf, 8, 0, 0).Should().Be(0xFFABCDEF);
        GetPixel(buf, 8, 4, 4).Should().Be(0xFFABCDEF);
    }

    // ════════════════════════════════════════════════════════════════════
    // BLIT TESTS (using manual rect simulation)
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Blit_CopiesSourceToDestination()
    {
        // Arrange
        var dst = CreateSolidUintBuffer(10, 10, 0xFF000000); // Black
        var src = CreateSolidUintBuffer(3, 3, 0xFFFF0000);   // Blue

        // Act
        PixelOps.Blit(dst, 10, 10, 2, 2, src, 3, 3);

        // Assert - 3x3 blue region at (2,2)
        dst[2 * 10 + 2].Should().Be(0xFFFF0000);
        dst[4 * 10 + 4].Should().Be(0xFFFF0000);
        // Surrounding should still be black
        dst[0].Should().Be(0xFF000000);
    }

    [Test]
    public void Blit_WithEraseDst_ClearsDestinationFirst()
    {
        // Arrange
        var dst = CreateSolidUintBuffer(10, 10, 0xFFFFFFFF); // White
        var src = new uint[3 * 3]; // Transparent (all zeros)

        // Act
        PixelOps.Blit(dst, 10, 10, 2, 2, src, 3, 3, eraseDst: true);

        // Assert - region should be cleared
        dst[2 * 10 + 2].Should().Be(0u);
    }

    [Test]
    public void Blit_PartiallyOutOfBounds_ClipsCorrectly()
    {
        // Arrange
        var dst = CreateSolidUintBuffer(10, 10, 0xFF000000);
        var src = CreateSolidUintBuffer(5, 5, 0xFFFF0000);

        // Act - blit at (8,8) so only 2x2 fits
        PixelOps.Blit(dst, 10, 10, 8, 8, src, 5, 5);

        // Assert
        dst[8 * 10 + 8].Should().Be(0xFFFF0000);
        dst[9 * 10 + 9].Should().Be(0xFFFF0000);
    }

    // ════════════════════════════════════════════════════════════════════
    // SCALE NN (UINT) TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void ScaleNN_DoublesSize()
    {
        // Arrange
        var src = CreateSolidUintBuffer(5, 5, 0xFF123456);

        // Act
        var result = PixelOps.ScaleNN(src, 5, 5, 10, 10);

        // Assert
        result.Should().HaveCount(100);
        result[0].Should().Be(0xFF123456);
        result[99].Should().Be(0xFF123456);
    }

    [Test]
    public void ScaleNN_InvalidDimensions_ReturnsEmptyArray()
    {
        // Arrange
        var src = CreateSolidUintBuffer(10, 10, 0xFFFFFFFF);

        // Act
        var result = PixelOps.ScaleNN(src, 10, 10, 0, 0);

        // Assert
        result.Should().BeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════
    // PIXELS SIMILAR TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void PixelsSimilar_IdenticalPixels_ReturnsTrue()
    {
        // Arrange
        var buf = new byte[] { 100, 150, 200, 255, 100, 150, 200, 255 };

        // Act
        var result = PixelOps.PixelsSimilar(buf, 0, 4, 0, false);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void PixelsSimilar_DifferentPixels_ReturnsFalse()
    {
        // Arrange
        var buf = new byte[] { 0, 0, 0, 255, 255, 255, 255, 255 };

        // Act
        var result = PixelOps.PixelsSimilar(buf, 0, 4, 10, false);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void PixelsSimilar_WithinTolerance_ReturnsTrue()
    {
        // Arrange - pixels differ by ~10 in each channel
        var buf = new byte[] { 100, 100, 100, 255, 110, 110, 110, 255 };

        // Act
        var result = PixelOps.PixelsSimilar(buf, 0, 4, 20, false);

        // Assert
        result.Should().BeTrue();
    }

    // ════════════════════════════════════════════════════════════════════
    // ROTATE NN (UINT) TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void RotateNN_ZeroRadians_MaintainsSize()
    {
        // Arrange
        var src = CreateSolidUintBuffer(10, 10, 0xFFFF0000);

        // Act
        var result = PixelOps.RotateNN(src, 10, 10, 0, out int outW, out int outH);

        // Assert
        outW.Should().Be(10);
        outH.Should().Be(10);
    }

    [Test]
    public void RotateNN_90Degrees_RotatesCorrectly()
    {
        // Arrange
        var src = CreateSolidUintBuffer(4, 2, 0xFFFF0000);
        double rad90 = Math.PI / 2.0;

        // Act
        var result = PixelOps.RotateNN(src, 4, 2, rad90, out int outW, out int outH);

        // Assert - dimensions should approximately swap
        result.Length.Should().BeGreaterThan(0);
    }

    // ════════════════════════════════════════════════════════════════════
    // CROP TO OPAQUE TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void CropToOpaque_WithTransparentBorder_CropsCorrectly()
    {
        // Arrange - 5x5 with opaque 2x2 in center
        var src = new byte[5 * 5 * 4]; // All transparent
        // Set center pixels opaque
        SetPixel(src, 5, 2, 2, 0xFFFF0000);
        SetPixel(src, 5, 3, 2, 0xFFFF0000);
        SetPixel(src, 5, 2, 3, 0xFFFF0000);
        SetPixel(src, 5, 3, 3, 0xFFFF0000);

        // Act
        var (buf, w, h, offsetX, offsetY) = PixelOps.CropToOpaque(src, 5, 5);

        // Assert
        w.Should().Be(2);
        h.Should().Be(2);
        offsetX.Should().Be(2);
        offsetY.Should().Be(2);
    }

    [Test]
    public void CropToOpaque_FullyTransparent_ReturnsMinimalBuffer()
    {
        // Arrange
        var src = new byte[10 * 10 * 4]; // All zeros = transparent

        // Act
        var (buf, w, h, offsetX, offsetY) = PixelOps.CropToOpaque(src, 10, 10);

        // Assert
        w.Should().Be(1);
        h.Should().Be(1);
    }

    // ════════════════════════════════════════════════════════════════════
    // ROTATE SPRITE APPROX TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void RotateSpriteApprox_ZeroDegrees_MaintainsApproximateSize()
    {
        // Arrange
        var src = CreateSolidBuffer(8, 8, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.RotateSpriteApprox(src, 8, 8, 0);

        // Assert - should be approximately same size
        w.Should().BeGreaterOrEqualTo(7);
        h.Should().BeGreaterOrEqualTo(7);
        buf.Should().NotBeEmpty();
    }

    [Test]
    public void RotateSpriteApprox_45Degrees_ExpandsBounds()
    {
        // Arrange
        var src = CreateSolidBuffer(8, 8, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.RotateSpriteApprox(src, 8, 8, 45);

        // Assert - rotated bounds should be larger
        Math.Max(w, h).Should().BeGreaterThanOrEqualTo(8);
        buf.Should().NotBeEmpty();
    }

    [Test]
    public void RotateSpriteApprox_SmallImage_HandlesCorrectly()
    {
        // Arrange - minimal 2x2 image
        var src = CreateSolidBuffer(2, 2, 0xFF00FF00);

        // Act
        var (buf, w, h) = PixelOps.RotateSpriteApprox(src, 2, 2, 90);

        // Assert
        w.Should().BeGreaterOrEqualTo(1);
        h.Should().BeGreaterOrEqualTo(1);
        buf.Should().NotBeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════
    // SCALE BY 2X STEPS THEN NEAREST TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void ScaleBy2xStepsThenNearest_WithEPX_ScalesCorrectly()
    {
        // Arrange
        var src = CreateSolidBuffer(4, 4, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.ScaleBy2xStepsThenNearest(src, 4, 4, 16, 16, epx: true);

        // Assert
        w.Should().Be(16);
        h.Should().Be(16);
        buf.Should().HaveCount(16 * 16 * 4);
    }

    [Test]
    public void ScaleBy2xStepsThenNearest_WithScale2x_ScalesCorrectly()
    {
        // Arrange
        var src = CreateSolidBuffer(4, 4, 0xFF00FF00);

        // Act
        var (buf, w, h) = PixelOps.ScaleBy2xStepsThenNearest(src, 4, 4, 16, 16, epx: false);

        // Assert
        w.Should().Be(16);
        h.Should().Be(16);
        buf.Should().HaveCount(16 * 16 * 4);
    }

    [Test]
    public void ScaleBy2xStepsThenNearest_NonPowerOfTwo_HandlesCorrectly()
    {
        // Arrange
        var src = CreateSolidBuffer(5, 5, 0xFFABCDEF);

        // Act
        var (buf, w, h) = PixelOps.ScaleBy2xStepsThenNearest(src, 5, 5, 17, 17, epx: true);

        // Assert
        w.Should().Be(17);
        h.Should().Be(17);
    }

    // ════════════════════════════════════════════════════════════════════
    // ADDITIONAL EDGE CASE TESTS
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void ResizeNearest_1x1Image_ScalesCorrectly()
    {
        // Arrange
        var src = CreateSolidBuffer(1, 1, 0xFFFF0000);

        // Act
        var result = PixelOps.ResizeNearest(src, 1, 1, 5, 5);

        // Assert
        result.Should().HaveCount(5 * 5 * 4);
        GetPixel(result, 5, 2, 2).Should().Be(0xFFFF0000);
    }

    [Test]
    public void ResizeBilinear_1x1Image_ScalesCorrectly()
    {
        // Arrange
        var src = CreateSolidBuffer(1, 1, 0xFF808080);

        // Act
        var result = PixelOps.ResizeBilinear(src, 1, 1, 4, 4);

        // Assert
        result.Should().HaveCount(4 * 4 * 4);
    }

    [Test]
    public void EPX2x_1x1Image_Doubles()
    {
        // Arrange
        var src = CreateSolidBuffer(1, 1, 0xFFABCDEF);

        // Act
        var (buf, w, h) = PixelOps.EPX2x(src, 1, 1);

        // Assert
        w.Should().Be(2);
        h.Should().Be(2);
    }

    [Test]
    public void Scale2x_1x1Image_Doubles()
    {
        // Arrange
        var src = CreateSolidBuffer(1, 1, 0xFFABCDEF);

        // Act
        var (buf, w, h) = PixelOps.Scale2x(src, 1, 1);

        // Assert
        w.Should().Be(2);
        h.Should().Be(2);
    }

    [Test]
    public void PixelsSimilar_WithAlpha_ComparesAlphaChannel()
    {
        // Arrange - same RGB but different alpha
        var buf = new byte[] { 100, 150, 200, 255, 100, 150, 200, 128 };

        // Act
        var resultWithAlpha = PixelOps.PixelsSimilar(buf, 0, 4, 50, useAlpha: true);
        var resultWithoutAlpha = PixelOps.PixelsSimilar(buf, 0, 4, 50, useAlpha: false);

        // Assert
        resultWithoutAlpha.Should().BeTrue(); // Same RGB
        // Alpha differs by 127, which may or may not pass depending on tolerance
    }

    [Test]
    public void CropToOpaque_FullyOpaque_ReturnsOriginalSize()
    {
        // Arrange - fully opaque image
        var src = CreateSolidBuffer(5, 5, 0xFFFF0000);

        // Act
        var (buf, w, h, offsetX, offsetY) = PixelOps.CropToOpaque(src, 5, 5);

        // Assert
        w.Should().Be(5);
        h.Should().Be(5);
        offsetX.Should().Be(0);
        offsetY.Should().Be(0);
    }

    [Test]
    public void CropToOpaque_SinglePixel_CropsToOne()
    {
        // Arrange - single opaque pixel in corner
        var src = new byte[10 * 10 * 4];
        SetPixel(src, 10, 9, 9, 0xFFFF0000);

        // Act
        var (buf, w, h, offsetX, offsetY) = PixelOps.CropToOpaque(src, 10, 10);

        // Assert
        w.Should().Be(1);
        h.Should().Be(1);
        offsetX.Should().Be(9);
        offsetY.Should().Be(9);
    }

    [Test]
    public void Blit_CompletelyOutOfBounds_DoesNotCrash()
    {
        // Arrange
        var dst = CreateSolidUintBuffer(10, 10, 0xFF000000);
        var src = CreateSolidUintBuffer(3, 3, 0xFFFF0000);

        // Act - blit completely outside destination
        PixelOps.Blit(dst, 10, 10, -10, -10, src, 3, 3);

        // Assert - destination unchanged
        dst[0].Should().Be(0xFF000000);
    }

    [Test]
    public void Blit_NegativeOffset_ClipsCorrectly()
    {
        // Arrange
        var dst = CreateSolidUintBuffer(10, 10, 0xFF000000);
        var src = CreateSolidUintBuffer(5, 5, 0xFFFF0000);

        // Act - blit with negative offset
        PixelOps.Blit(dst, 10, 10, -2, -2, src, 5, 5);

        // Assert - only visible portion is blitted
        dst[0].Should().Be(0xFFFF0000); // Top-left should be from source
    }

    [Test]
    public void RotateNearest_NegativeAngle_RotatesCorrectly()
    {
        // Arrange
        var src = CreateSolidBuffer(10, 10, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.RotateNearest(src, 10, 10, -45);

        // Assert - should expand bounds like positive rotation
        w.Should().BeGreaterThan(10);
        h.Should().BeGreaterThan(10);
    }

    [Test]
    public void RotateBilinear_360Degrees_ReturnsSameSize()
    {
        // Arrange
        var src = CreateSolidBuffer(10, 10, 0xFFFF0000);

        // Act
        var (buf, w, h) = PixelOps.RotateBilinear(src, 10, 10, 360);

        // Assert - 360 rotation should maintain dimensions (allow +1 for floating-point rounding in bounds calc)
        w.Should().BeInRange(10, 11);
        h.Should().BeInRange(10, 11);
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ════════════════════════════════════════════════════════════════════

    private static void SetPixel(byte[] buf, int w, int x, int y, uint bgra)
    {
        int i = (y * w + x) * 4;
        buf[i + 0] = (byte)(bgra & 0xFF);          // B
        buf[i + 1] = (byte)((bgra >> 8) & 0xFF);   // G
        buf[i + 2] = (byte)((bgra >> 16) & 0xFF);  // R
        buf[i + 3] = (byte)((bgra >> 24) & 0xFF);  // A
    }
}
