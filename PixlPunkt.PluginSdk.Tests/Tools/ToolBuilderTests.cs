using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Imaging;
using PixlPunkt.PluginSdk.Painting;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Tools;
using PixlPunkt.PluginSdk.Tools.Builders;

namespace PixlPunkt.PluginSdk.Tests.Tools;

/// <summary>
/// Tests for <see cref="BrushToolBuilder"/> and tool registration builders.
/// </summary>
public class ToolBuilderTests
{
    // ========================================================================
    // TEST DOUBLES
    // ========================================================================

    private class TestPainter : IStrokePainter
    {
        public bool NeedsSnapshot => false;
        public int BeginCallCount { get; private set; }
        public int StampCallCount { get; private set; }

        public void Begin(PixelSurface surface, byte[]? snapshot)
        {
            BeginCallCount++;
        }

        public void StampAt(int cx, int cy, StrokeContext context)
        {
            StampCallCount++;
        }

        public void StampLine(int x0, int y0, int x1, int y1, StrokeContext context)
        {
            StampCallCount++;
        }

        public IRenderResult? End(string description = "Brush Stroke")
        {
            return null;
        }
    }

    private class TestSettings : ToolSettingsBase
    {
        public int BrushSize { get; set; } = 10;
    }

    // ========================================================================
    // BUILDER CREATION
    // ========================================================================

    [Fact]
    public void BrushTool_CreatesBuilder()
    {
        var builder = ToolBuilders.BrushTool("myplugin.brush.test");
        Assert.NotNull(builder);
    }

    [Fact]
    public void BrushTool_NullId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ToolBuilders.BrushTool(null!));
    }

    // ========================================================================
    // FLUENT API - DISPLAY NAME
    // ========================================================================

    [Fact]
    public void WithDisplayName_SetsDisplayName()
    {
        var reg = ToolBuilders.BrushTool("test.brush")
            .WithDisplayName("My Custom Brush")
            .WithPainter<TestPainter>()
            .Build();

        Assert.Equal("My Custom Brush", reg.DisplayName);
    }

    [Fact]
    public void DisplayName_DefaultsToIdSuffix()
    {
        var reg = ToolBuilders.BrushTool("myplugin.brush.sparkle")
            .WithPainter<TestPainter>()
            .Build();

        Assert.Equal("Sparkle", reg.DisplayName);
    }

    [Fact]
    public void DisplayName_CapitalizesFirstLetter()
    {
        var reg = ToolBuilders.BrushTool("myplugin.brush.glow")
            .WithPainter<TestPainter>()
            .Build();

        Assert.Equal("Glow", reg.DisplayName);
    }

    // ========================================================================
    // FLUENT API - SETTINGS
    // ========================================================================

    [Fact]
    public void WithSettings_SetsSettings()
    {
        var settings = new TestSettings { BrushSize = 25 };

        var reg = ToolBuilders.BrushTool("test.brush")
            .WithSettings(settings)
            .WithPainter<TestPainter>()
            .Build();

        Assert.Same(settings, reg.Settings);
    }

    [Fact]
    public void WithSettings_Null_Allowed()
    {
        var reg = ToolBuilders.BrushTool("test.brush")
            .WithSettings(null)
            .WithPainter<TestPainter>()
            .Build();

        Assert.Null(reg.Settings);
    }

    // ========================================================================
    // PAINTER CONFIGURATION
    // ========================================================================

    [Fact]
    public void WithPainter_Lambda_CreatesPainter()
    {
        var testPainter = new TestPainter();

        var reg = ToolBuilders.BrushTool("test.brush")
            .WithPainter(() => testPainter)
            .Build();

        var created = reg.PainterFactory();
        Assert.Same(testPainter, created);
    }

    [Fact]
    public void WithPainter_Generic_CreatesPainter()
    {
        var reg = ToolBuilders.BrushTool("test.brush")
            .WithPainter<TestPainter>()
            .Build();

        var created = reg.PainterFactory();
        Assert.IsType<TestPainter>(created);
    }

    [Fact]
    public void WithPainter_Generic_CreatesNewInstanceEachCall()
    {
        var reg = ToolBuilders.BrushTool("test.brush")
            .WithPainter<TestPainter>()
            .Build();

        var painter1 = reg.PainterFactory();
        var painter2 = reg.PainterFactory();

        Assert.NotSame(painter1, painter2);
    }

    // ========================================================================
    // BUILD VALIDATION
    // ========================================================================

    [Fact]
    public void Build_WithoutPainter_ThrowsInvalidOperation()
    {
        var builder = ToolBuilders.BrushTool("test.brush")
            .WithDisplayName("Test");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithPainter_Succeeds()
    {
        var reg = ToolBuilders.BrushTool("test.brush")
            .WithPainter<TestPainter>()
            .Build();

        Assert.NotNull(reg);
        Assert.Equal("test.brush", reg.Id);
    }

    // ========================================================================
    // SHORTCUT CONFLICT
    // ========================================================================

    [Fact]
    public void WithAllowShortcutConflict_DoesNotThrow()
    {
        // Just verify it doesn't throw - actual conflict detection is internal
        var reg = ToolBuilders.BrushTool("test.brush")
            .WithAllowShortcutConflict()
            .WithPainter<TestPainter>()
            .Build();

        Assert.NotNull(reg);
    }

    // ========================================================================
    // FLUENT CHAINING
    // ========================================================================

    [Fact]
    public void FluentChaining_AllMethodsReturnBuilder()
    {
        var settings = new TestSettings();

        var reg = ToolBuilders.BrushTool("myplugin.brush.complete")
            .WithDisplayName("Complete Brush")
            .WithSettings(settings)
            .WithAllowShortcutConflict()
            .WithPainter<TestPainter>()
            .Build();

        Assert.Equal("myplugin.brush.complete", reg.Id);
        Assert.Equal("Complete Brush", reg.DisplayName);
        Assert.Same(settings, reg.Settings);
    }

    // ========================================================================
    // REGISTRATION PROPERTIES
    // ========================================================================

    [Fact]
    public void Registration_HasCorrectId()
    {
        var reg = ToolBuilders.BrushTool("myplugin.brush.test")
            .WithPainter<TestPainter>()
            .Build();

        Assert.Equal("myplugin.brush.test", reg.Id);
    }

    [Fact]
    public void Registration_PainterFactory_IsCallable()
    {
        var reg = ToolBuilders.BrushTool("test.brush")
            .WithPainter<TestPainter>()
            .Build();

        var painter = reg.PainterFactory();

        Assert.NotNull(painter);
        Assert.IsType<TestPainter>(painter);
    }

    // ========================================================================
    // EDGE CASES
    // ========================================================================

    [Fact]
    public void IdWithoutDot_UsesFullIdAsDisplayName()
    {
        var reg = ToolBuilders.BrushTool("simplebrush")
            .WithPainter<TestPainter>()
            .Build();

        Assert.Equal("simplebrush", reg.DisplayName);
    }

    [Fact]
    public void IdWithTrailingDot_HandlesGracefully()
    {
        var reg = ToolBuilders.BrushTool("test.")
            .WithPainter<TestPainter>()
            .Build();

        // Should not throw, display name extraction should handle edge cases
        Assert.NotNull(reg.DisplayName);
    }

    [Fact]
    public void EmptyId_Allowed()
    {
        // Empty string is allowed but probably not useful
        var reg = ToolBuilders.BrushTool("")
            .WithPainter<TestPainter>()
            .Build();

        Assert.Equal("", reg.Id);
    }
}
