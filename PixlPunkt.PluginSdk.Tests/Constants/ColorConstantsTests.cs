using PixlPunkt.PluginSdk.Constants;

namespace PixlPunkt.PluginSdk.Tests.Constants;

/// <summary>
/// Tests for <see cref="ColorConstants"/> - constants for color manipulation.
/// </summary>
public class ColorConstantsTests
{
    // ========================================================================
    // MASK TESTS
    // ========================================================================

    [Fact]
    public void RgbMask_ExtractsRgbChannels()
    {
        uint color = 0xAABBCCDD; // A=AA, R=BB, G=CC, B=DD
        uint rgb = color & ColorConstants.RgbMask;

        Assert.Equal(0x00BBCCDDu, rgb);
    }

    [Fact]
    public void RgbMask_StripsAlpha()
    {
        uint opaqueColor = 0xFFFF0000;
        uint transparentColor = 0x00FF0000;

        Assert.Equal(opaqueColor & ColorConstants.RgbMask, transparentColor & ColorConstants.RgbMask);
    }

    [Fact]
    public void AlphaMask_ExtractsAlphaChannel()
    {
        uint color = 0xAABBCCDD;
        uint alpha = color & ColorConstants.AlphaMask;

        Assert.Equal(0xAA000000u, alpha);
    }

    [Fact]
    public void RgbMask_Alias_IsSameValue()
    {
        Assert.Equal(ColorConstants.RgbMask, ColorConstants.RGBMask);
    }

    // ========================================================================
    // SHIFT TESTS
    // ========================================================================

    [Fact]
    public void AlphaShift_ExtractsAlphaByte()
    {
        uint color = 0xABCDEF12;
        byte alpha = (byte)(color >> ColorConstants.AlphaShift);

        Assert.Equal(0xAB, alpha);
    }

    [Fact]
    public void RedShift_ExtractsRedByte()
    {
        uint color = 0xABCDEF12;
        byte red = (byte)(color >> ColorConstants.RedShift);

        Assert.Equal(0xCD, red);
    }

    [Fact]
    public void GreenShift_ExtractsGreenByte()
    {
        uint color = 0xABCDEF12;
        byte green = (byte)(color >> ColorConstants.GreenShift);

        Assert.Equal(0xEF, green);
    }

    [Fact]
    public void BlueShift_IsZero()
    {
        Assert.Equal(0, ColorConstants.BlueShift);
    }

    [Fact]
    public void BlueChannel_NoShiftNeeded()
    {
        uint color = 0xABCDEF12;
        byte blue = (byte)(color >> ColorConstants.BlueShift);

        Assert.Equal(0x12, blue);
    }

    // ========================================================================
    // BYTE RANGE TESTS
    // ========================================================================

    [Fact]
    public void MinByte_IsZero()
    {
        Assert.Equal(0, ColorConstants.MinByte);
    }

    [Fact]
    public void MaxByte_Is255()
    {
        Assert.Equal(255, ColorConstants.MaxByte);
    }

    [Fact]
    public void MaxByteValue_Is255()
    {
        Assert.Equal(255, ColorConstants.MaxByteValue);
    }

    [Fact]
    public void HalfByteValue_Is127()
    {
        Assert.Equal(127, ColorConstants.HalfByteValue);
    }

    // ========================================================================
    // ALPHA VALUE TESTS
    // ========================================================================

    [Fact]
    public void FullAlpha_Is255()
    {
        Assert.Equal(255, ColorConstants.FullAlpha);
    }

    [Fact]
    public void TransparentAlpha_IsZero()
    {
        Assert.Equal(0, ColorConstants.TransparentAlpha);
    }

    [Fact]
    public void OpaqueAlphaMask_SetsFullAlpha()
    {
        uint rgb = 0x00FF0000; // Red with no alpha
        uint opaque = rgb | ColorConstants.OpaqueAlphaMask;

        Assert.Equal(0xFFFF0000u, opaque);
    }

    // ========================================================================
    // COMMON COLOR TESTS
    // ========================================================================

    [Fact]
    public void TransparentBlack_IsZero()
    {
        Assert.Equal(0x00000000u, ColorConstants.TransparentBlack);
    }

    [Fact]
    public void OpaqueBlack_HasFullAlpha()
    {
        Assert.Equal(0xFF000000u, ColorConstants.OpaqueBlack);
    }

    [Fact]
    public void OpaqueWhite_IsAllBits()
    {
        Assert.Equal(0xFFFFFFFFu, ColorConstants.OpaqueWhite);
    }

    // ========================================================================
    // PIXEL FORMAT TESTS
    // ========================================================================

    [Fact]
    public void BytesPerPixel_Is4()
    {
        Assert.Equal(4, ColorConstants.BytesPerPixel);
    }

    [Fact]
    public void BitsPerPixel_Is32()
    {
        Assert.Equal(32, ColorConstants.BitsPerPixel);
    }

    // ========================================================================
    // USAGE PATTERN TESTS
    // ========================================================================

    [Fact]
    public void ExtractAndRepack_RoundTrips()
    {
        uint original = 0xAABBCCDD;

        byte a = (byte)(original >> ColorConstants.AlphaShift);
        byte r = (byte)(original >> ColorConstants.RedShift);
        byte g = (byte)(original >> ColorConstants.GreenShift);
        byte b = (byte)(original >> ColorConstants.BlueShift);

        uint repacked = (uint)(
            (a << ColorConstants.AlphaShift) |
            (r << ColorConstants.RedShift) |
            (g << ColorConstants.GreenShift) |
            (b << ColorConstants.BlueShift)
        );

        Assert.Equal(original, repacked);
    }

    [Fact]
    public void SetOpacity_WorksCorrectly()
    {
        uint transparent = 0x00FF0000; // Transparent red
        uint opaque = (transparent & ColorConstants.RgbMask) | ColorConstants.OpaqueAlphaMask;

        Assert.Equal(0xFFFF0000u, opaque);
    }

    [Fact]
    public void ClearOpacity_WorksCorrectly()
    {
        uint opaque = 0xFFFF0000; // Opaque red
        uint transparent = opaque & ColorConstants.RgbMask;

        Assert.Equal(0x00FF0000u, transparent);
    }
}
