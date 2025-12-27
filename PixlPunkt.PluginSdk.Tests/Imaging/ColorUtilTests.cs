using PixlPunkt.PluginSdk.Imaging;

namespace PixlPunkt.PluginSdk.Tests.Imaging;

/// <summary>
/// Tests for <see cref="ColorUtil"/> - platform-agnostic color utilities.
/// </summary>
public class ColorUtilTests
{
    // ========================================================================
    // PACK / UNPACK
    // ========================================================================

    [Fact]
    public void PackBgra_PacksComponentsCorrectly()
    {
        // BGRA order: B=0x11, G=0x22, R=0x33, A=0xFF
        // Result: 0xAARRGGBB = 0xFF332211
        var result = ColorUtil.PackBgra(0x11, 0x22, 0x33, 0xFF);

        Assert.Equal(0xFF332211u, result);
    }

    [Fact]
    public void PackBgra_DefaultAlphaIsOpaque()
    {
        var result = ColorUtil.PackBgra(0x00, 0x00, 0x00);

        // Alpha should be 0xFF (255)
        Assert.Equal(0xFF, ColorUtil.GetA(result));
    }

    [Fact]
    public void FromRgb_CreatesOpaqueColor()
    {
        var result = ColorUtil.FromRgb(0x33, 0x22, 0x11);

        Assert.Equal(0x33, ColorUtil.GetR(result));
        Assert.Equal(0x22, ColorUtil.GetG(result));
        Assert.Equal(0x11, ColorUtil.GetB(result));
        Assert.Equal(0xFF, ColorUtil.GetA(result));
    }

    [Fact]
    public void UnpackBgra_ExtractsComponentsCorrectly()
    {
        uint color = 0xAABBCCDD; // A=0xAA, R=0xBB, G=0xCC, B=0xDD

        ColorUtil.UnpackBgra(color, out byte b, out byte g, out byte r, out byte a);

        Assert.Equal(0xDD, b);
        Assert.Equal(0xCC, g);
        Assert.Equal(0xBB, r);
        Assert.Equal(0xAA, a);
    }

    [Fact]
    public void PackAndUnpack_RoundTrips()
    {
        byte b = 100, g = 150, r = 200, a = 250;

        var packed = ColorUtil.PackBgra(b, g, r, a);
        ColorUtil.UnpackBgra(packed, out byte b2, out byte g2, out byte r2, out byte a2);

        Assert.Equal(b, b2);
        Assert.Equal(g, g2);
        Assert.Equal(r, r2);
        Assert.Equal(a, a2);
    }

    // ========================================================================
    // CHANNEL ACCESS
    // ========================================================================

    [Fact]
    public void GetB_ExtractsBlueChannel()
    {
        uint color = 0x00000042;
        Assert.Equal(0x42, ColorUtil.GetB(color));
    }

    [Fact]
    public void GetG_ExtractsGreenChannel()
    {
        uint color = 0x00004200;
        Assert.Equal(0x42, ColorUtil.GetG(color));
    }

    [Fact]
    public void GetR_ExtractsRedChannel()
    {
        uint color = 0x00420000;
        Assert.Equal(0x42, ColorUtil.GetR(color));
    }

    [Fact]
    public void GetA_ExtractsAlphaChannel()
    {
        uint color = 0x42000000;
        Assert.Equal(0x42, ColorUtil.GetA(color));
    }

    // ========================================================================
    // ALPHA OPERATIONS
    // ========================================================================

    [Fact]
    public void MakeOpaque_SetsAlphaTo255()
    {
        uint semiTransparent = 0x80AABBCC;
        var result = ColorUtil.MakeOpaque(semiTransparent);

        Assert.Equal(0xFFAABBCCu, result);
    }

    [Fact]
    public void StripAlpha_SetsAlphaToZero()
    {
        uint opaque = 0xFFAABBCC;
        var result = ColorUtil.StripAlpha(opaque);

        Assert.Equal(0x00AABBCCu, result);
    }

    [Fact]
    public void SetAlpha_ReplacesAlphaChannel()
    {
        uint color = 0xFFAABBCC;
        var result = ColorUtil.SetAlpha(color, 0x80);

        Assert.Equal(0x80AABBCCu, result);
    }

    [Fact]
    public void RgbEqual_IgnoresAlpha()
    {
        uint a = 0xFFAABBCC;
        uint b = 0x00AABBCC;

        Assert.True(ColorUtil.RgbEqual(a, b));
    }

    [Fact]
    public void RgbEqual_ReturnsFalseForDifferentRgb()
    {
        uint a = 0xFFAABBCC;
        uint b = 0xFFAABBCD;

        Assert.False(ColorUtil.RgbEqual(a, b));
    }

    // ========================================================================
    // ALPHA COMPOSITING
    // ========================================================================

    [Fact]
    public void BlendOver_OpaqueSource_ReturnsSource()
    {
        uint dst = 0xFF112233;
        uint src = 0xFFAABBCC; // Fully opaque

        var result = ColorUtil.BlendOver(dst, src);

        Assert.Equal(src, result);
    }

    [Fact]
    public void BlendOver_TransparentSource_ReturnsDest()
    {
        uint dst = 0xFF112233;
        uint src = 0x00AABBCC; // Fully transparent

        var result = ColorUtil.BlendOver(dst, src);

        Assert.Equal(dst, result);
    }

    [Fact]
    public void BlendOver_SemiTransparent_Blends()
    {
        uint dst = 0xFF000000; // Opaque black
        uint src = 0x80FFFFFF; // 50% white

        var result = ColorUtil.BlendOver(dst, src);

        // Result should be grayish with full alpha
        Assert.Equal(0xFF, ColorUtil.GetA(result));
        Assert.True(ColorUtil.GetR(result) > 0);
        Assert.True(ColorUtil.GetR(result) < 255);
    }

    [Fact]
    public void CompositeOverColor_OpaquePixel_ReturnsPixelColor()
    {
        uint px = 0xFFAABBCC;
        uint bg = 0xFF000000;

        var result = ColorUtil.CompositeOverColor(px, bg);

        Assert.Equal(0xFFAABBCCu, result);
    }

    [Fact]
    public void CompositeOverColor_TransparentPixel_ReturnsBackground()
    {
        uint px = 0x00AABBCC;
        uint bg = 0xFF112233;

        var result = ColorUtil.CompositeOverColor(px, bg);

        Assert.Equal(0xFF112233u, result);
    }

    // ========================================================================
    // COLOR DISTANCE
    // ========================================================================

    [Fact]
    public void ColorDistanceManhattan_IdenticalColors_ReturnsZero()
    {
        uint a = 0xFFAABBCC;
        uint b = 0xFFAABBCC;

        Assert.Equal(0, ColorUtil.ColorDistanceManhattan(a, b));
    }

    [Fact]
    public void ColorDistanceManhattan_MaxDifference_Returns765()
    {
        uint black = 0xFF000000;
        uint white = 0xFFFFFFFF;

        // 255 + 255 + 255 = 765
        Assert.Equal(765, ColorUtil.ColorDistanceManhattan(black, white));
    }

    [Fact]
    public void ColorDistanceChebyshev_IdenticalColors_ReturnsZero()
    {
        uint a = 0xFFAABBCC;
        uint b = 0xFFAABBCC;

        Assert.Equal(0, ColorUtil.ColorDistanceChebyshev(a, b));
    }

    [Fact]
    public void ColorDistanceChebyshev_MaxDifference_Returns255()
    {
        uint black = 0xFF000000;
        uint white = 0xFFFFFFFF;

        Assert.Equal(255, ColorUtil.ColorDistanceChebyshev(black, white));
    }

    [Fact]
    public void ColorDistanceSquared_IdenticalColors_ReturnsZero()
    {
        uint a = 0xFFAABBCC;
        uint b = 0xFFAABBCC;

        Assert.Equal(0, ColorUtil.ColorDistanceSquared(a, b));
    }

    [Fact]
    public void ColorDistanceSquared_SymmetricDistance()
    {
        uint a = 0xFF112233;
        uint b = 0xFF445566;

        Assert.Equal(
            ColorUtil.ColorDistanceSquared(a, b),
            ColorUtil.ColorDistanceSquared(b, a)
        );
    }

    // ========================================================================
    // LUMINANCE
    // ========================================================================

    [Fact]
    public void FastLuminance_Black_ReturnsZero()
    {
        uint black = 0xFF000000;
        Assert.Equal(0, ColorUtil.FastLuminance(black));
    }

    [Fact]
    public void FastLuminance_White_ReturnsMax()
    {
        uint white = 0xFFFFFFFF;
        var lum = ColorUtil.FastLuminance(white);
        Assert.True(lum >= 254); // Approximate due to integer math
    }

    [Fact]
    public void FastLuminance_GreenBrighter_ThanBlue()
    {
        uint pureGreen = ColorUtil.FromRgb(0, 255, 0);
        uint pureBlue = ColorUtil.FromRgb(0, 0, 255);

        // Green should be perceived as brighter than blue
        Assert.True(ColorUtil.FastLuminance(pureGreen) > ColorUtil.FastLuminance(pureBlue));
    }

    [Fact]
    public void ShouldUseLightText_DarkBackground_ReturnsTrue()
    {
        uint darkBlue = ColorUtil.FromRgb(0, 0, 50);
        Assert.True(ColorUtil.ShouldUseLightText(darkBlue));
    }

    [Fact]
    public void ShouldUseLightText_LightBackground_ReturnsFalse()
    {
        uint lightYellow = ColorUtil.FromRgb(255, 255, 200);
        Assert.False(ColorUtil.ShouldUseLightText(lightYellow));
    }

    // ========================================================================
    // PIXEL BUFFER I/O
    // ========================================================================

    [Fact]
    public void ReadPixel_ReadsCorrectBgra()
    {
        byte[] pixels = [0x11, 0x22, 0x33, 0xFF]; // B, G, R, A

        var result = ColorUtil.ReadPixel(pixels, 0);

        Assert.Equal(0xFF332211u, result);
    }

    [Fact]
    public void WritePixel_WritesCorrectBytes()
    {
        byte[] pixels = new byte[4];
        uint color = 0xAABBCCDD;

        ColorUtil.WritePixel(pixels, 0, color);

        Assert.Equal(0xDD, pixels[0]); // B
        Assert.Equal(0xCC, pixels[1]); // G
        Assert.Equal(0xBB, pixels[2]); // R
        Assert.Equal(0xAA, pixels[3]); // A
    }

    [Fact]
    public void ReadAndWritePixel_RoundTrips()
    {
        byte[] pixels = new byte[4];
        uint original = 0x12345678;

        ColorUtil.WritePixel(pixels, 0, original);
        var result = ColorUtil.ReadPixel(pixels, 0);

        Assert.Equal(original, result);
    }

    [Fact]
    public void ReadPixel_WithOffset_ReadsCorrectPixel()
    {
        byte[] pixels = new byte[8];
        // Second pixel (index 4)
        pixels[4] = 0xAA;
        pixels[5] = 0xBB;
        pixels[6] = 0xCC;
        pixels[7] = 0xDD;

        var result = ColorUtil.ReadPixel(pixels, 4);

        Assert.Equal(0xDDCCBBAAu, result);
    }

    // ========================================================================
    // LERP
    // ========================================================================

    [Fact]
    public void LerpRgbKeepAlpha_AtZero_ReturnsDest()
    {
        uint dst = 0xFFAABBCC;
        uint fg = 0xFF112233;

        var result = ColorUtil.LerpRgbKeepAlpha(dst, fg, 0);

        Assert.Equal(dst, result);
    }

    [Fact]
    public void LerpRgbKeepAlpha_AtMax_ReturnsForeground()
    {
        uint dst = 0xFFAABBCC;
        uint fg = 0xFF112233;

        var result = ColorUtil.LerpRgbKeepAlpha(dst, fg, 255);

        // RGB should be very close to fg (may have rounding differences), alpha should match dst
        // Due to integer rounding in the lerp formula, allow +/- 1 tolerance
        Assert.InRange(ColorUtil.GetR(result), ColorUtil.GetR(fg) - 1, ColorUtil.GetR(fg) + 1);
        Assert.InRange(ColorUtil.GetG(result), ColorUtil.GetG(fg) - 1, ColorUtil.GetG(fg) + 1);
        Assert.InRange(ColorUtil.GetB(result), ColorUtil.GetB(fg) - 1, ColorUtil.GetB(fg) + 1);
        Assert.Equal(ColorUtil.GetA(dst), ColorUtil.GetA(result));
    }

    [Fact]
    public void LerpRgbKeepAlpha_PreservesDestinationAlpha()
    {
        uint dst = 0x80AABBCC; // 50% alpha
        uint fg = 0xFF112233; // Opaque

        var result = ColorUtil.LerpRgbKeepAlpha(dst, fg, 128);

        Assert.Equal(0x80, ColorUtil.GetA(result));
    }
}
