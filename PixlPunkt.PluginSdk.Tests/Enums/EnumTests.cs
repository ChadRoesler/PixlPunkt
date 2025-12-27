using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Imaging;
using PixlPunkt.PluginSdk.Effects;

namespace PixlPunkt.PluginSdk.Tests.Enums;

/// <summary>
/// Tests for SDK enums to verify they exist and have expected values.
/// </summary>
public class EnumTests
{
    // ========================================================================
    // TOOL CATEGORY
    // ========================================================================

    [Fact]
    public void ToolCategory_HasExpectedValues()
    {
        // Verify all expected categories exist
        Assert.True(Enum.IsDefined(typeof(ToolCategory), ToolCategory.Utility));
        Assert.True(Enum.IsDefined(typeof(ToolCategory), ToolCategory.Select));
        Assert.True(Enum.IsDefined(typeof(ToolCategory), ToolCategory.Brush));
        Assert.True(Enum.IsDefined(typeof(ToolCategory), ToolCategory.Tile));
        Assert.True(Enum.IsDefined(typeof(ToolCategory), ToolCategory.Shape));
    }

    [Fact]
    public void ToolCategory_HasFiveValues()
    {
        var values = Enum.GetValues<ToolCategory>();
        Assert.Equal(5, values.Length);
    }

    [Theory]
    [InlineData(ToolCategory.Utility)]
    [InlineData(ToolCategory.Select)]
    [InlineData(ToolCategory.Brush)]
    [InlineData(ToolCategory.Tile)]
    [InlineData(ToolCategory.Shape)]
    public void ToolCategory_CanBeParsed(ToolCategory expected)
    {
        string name = expected.ToString();
        Assert.True(Enum.TryParse<ToolCategory>(name, out var parsed));
        Assert.Equal(expected, parsed);
    }

    // ========================================================================
    // TOOL INPUT PATTERN
    // ========================================================================

    [Fact]
    public void ToolInputPattern_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(ToolInputPattern), ToolInputPattern.None));
        Assert.True(Enum.IsDefined(typeof(ToolInputPattern), ToolInputPattern.Click));
        Assert.True(Enum.IsDefined(typeof(ToolInputPattern), ToolInputPattern.Stroke));
        Assert.True(Enum.IsDefined(typeof(ToolInputPattern), ToolInputPattern.TwoPoint));
        Assert.True(Enum.IsDefined(typeof(ToolInputPattern), ToolInputPattern.Custom));
    }

    [Fact]
    public void ToolInputPattern_HasFiveValues()
    {
        var values = Enum.GetValues<ToolInputPattern>();
        Assert.Equal(5, values.Length);
    }

    // ========================================================================
    // TOOL OVERLAY STYLE
    // ========================================================================

    [Fact]
    public void ToolOverlayStyle_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(ToolOverlayStyle), ToolOverlayStyle.None));
        Assert.True(Enum.IsDefined(typeof(ToolOverlayStyle), ToolOverlayStyle.FilledGhost));
        Assert.True(Enum.IsDefined(typeof(ToolOverlayStyle), ToolOverlayStyle.Outline));
        Assert.True(Enum.IsDefined(typeof(ToolOverlayStyle), ToolOverlayStyle.ShapePreview));
        Assert.True(Enum.IsDefined(typeof(ToolOverlayStyle), ToolOverlayStyle.TileBoundary));
    }

    [Fact]
    public void ToolOverlayStyle_HasFiveValues()
    {
        var values = Enum.GetValues<ToolOverlayStyle>();
        Assert.Equal(5, values.Length);
    }

    // ========================================================================
    // BRUSH SHAPE
    // ========================================================================

    [Fact]
    public void BrushShape_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(BrushShape), BrushShape.Square));
        Assert.True(Enum.IsDefined(typeof(BrushShape), BrushShape.Circle));
        Assert.True(Enum.IsDefined(typeof(BrushShape), BrushShape.Custom));
    }

    [Fact]
    public void BrushShape_HasThreeValues()
    {
        var values = Enum.GetValues<BrushShape>();
        Assert.Equal(3, values.Length);
    }

    // ========================================================================
    // EFFECT CATEGORY
    // ========================================================================

    [Fact]
    public void EffectCategory_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(EffectCategory), EffectCategory.Filter));
        Assert.True(Enum.IsDefined(typeof(EffectCategory), EffectCategory.Stylize));
        Assert.True(Enum.IsDefined(typeof(EffectCategory), EffectCategory.Color));
    }

    [Fact]
    public void EffectCategory_HasThreeValues()
    {
        var values = Enum.GetValues<EffectCategory>();
        Assert.Equal(3, values.Length);
    }

    [Theory]
    [InlineData(EffectCategory.Filter)]
    [InlineData(EffectCategory.Stylize)]
    [InlineData(EffectCategory.Color)]
    public void EffectCategory_CanBeParsed(EffectCategory expected)
    {
        string name = expected.ToString();
        Assert.True(Enum.TryParse<EffectCategory>(name, out var parsed));
        Assert.Equal(expected, parsed);
    }
}
