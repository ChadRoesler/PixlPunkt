using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Imaging;
using PixlPunkt.PluginSdk.Painting;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Tools;

namespace PixlPunkt.PluginSdk.Tests.Tools;

/// <summary>
/// Tests for tool registration record types.
/// </summary>
public class ToolRegistrationTests
{
    // ========================================================================
    // TEST DOUBLES
    // ========================================================================

    private class TestPainter : IStrokePainter
    {
        public bool NeedsSnapshot => false;
        public void Begin(PixelSurface surface, byte[]? snapshot) { }
        public void StampAt(int cx, int cy, StrokeContext context) { }
        public void StampLine(int x0, int y0, int x1, int y1, StrokeContext context) { }
        public IRenderResult? End(string description = "Brush Stroke") => null;
    }

    private class TestSettings : ToolSettingsBase
    {
        public int TestValue { get; set; } = 42;
    }

    // ========================================================================
    // BRUSH TOOL REGISTRATION
    // ========================================================================

    [Fact]
    public void BrushToolRegistration_HasCorrectCategory()
    {
        var reg = new BrushToolRegistration(
            Id: "test.brush",
            DisplayName: "Test Brush",
            Settings: null,
            PainterFactory: () => new TestPainter()
        );

        Assert.Equal(ToolCategory.Brush, reg.Category);
    }

    [Fact]
    public void BrushToolRegistration_HasPainter_WhenFactoryProvided()
    {
        var reg = new BrushToolRegistration(
            Id: "test.brush",
            DisplayName: "Test Brush",
            Settings: null,
            PainterFactory: () => new TestPainter()
        );

        Assert.True(reg.HasPainter);
    }

    [Fact]
    public void BrushToolRegistration_CreatePainter_ReturnsNewInstance()
    {
        var reg = new BrushToolRegistration(
            Id: "test.brush",
            DisplayName: "Test Brush",
            Settings: null,
            PainterFactory: () => new TestPainter()
        );

        var painter1 = reg.CreatePainter();
        var painter2 = reg.CreatePainter();

        Assert.NotNull(painter1);
        Assert.NotNull(painter2);
        Assert.NotSame(painter1, painter2);
    }

    [Fact]
    public void BrushToolRegistration_StoresSettings()
    {
        var settings = new TestSettings { TestValue = 99 };

        var reg = new BrushToolRegistration(
            Id: "test.brush",
            DisplayName: "Test Brush",
            Settings: settings,
            PainterFactory: () => new TestPainter()
        );

        Assert.Same(settings, reg.Settings);
    }

    // ========================================================================
    // SHAPE TOOL REGISTRATION
    // ========================================================================

    [Fact]
    public void ShapeToolRegistration_HasCorrectCategory()
    {
        var reg = new ShapeToolRegistration(
            Id: "test.shape",
            DisplayName: "Test Shape",
            Settings: null
        );

        Assert.Equal(ToolCategory.Shape, reg.Category);
    }

    [Fact]
    public void ShapeToolRegistration_HasShapeBuilder_WhenFactoryProvided()
    {
        var reg = new ShapeToolRegistration(
            Id: "test.shape",
            DisplayName: "Test Shape",
            Settings: null,
            ShapeBuilderFactory: null
        );

        Assert.False(reg.HasShapeBuilder);
    }

    // ========================================================================
    // SELECTION TOOL REGISTRATION
    // ========================================================================

    [Fact]
    public void SelectionToolRegistration_HasCorrectCategory()
    {
        var reg = new SelectionToolRegistration(
            Id: "test.select",
            DisplayName: "Test Select",
            Settings: null
        );

        Assert.Equal(ToolCategory.Select, reg.Category);
    }

    [Fact]
    public void SelectionToolRegistration_HasSelectionTool_WhenFactoryProvided()
    {
        var reg = new SelectionToolRegistration(
            Id: "test.select",
            DisplayName: "Test Select",
            Settings: null,
            SelectionToolFactory: null
        );

        Assert.False(reg.HasSelectionTool);
    }

    // ========================================================================
    // UTILITY TOOL REGISTRATION
    // ========================================================================

    [Fact]
    public void UtilityToolRegistration_HasCorrectCategory()
    {
        var reg = new UtilityToolRegistration(
            Id: "test.utility",
            DisplayName: "Test Utility",
            Settings: null
        );

        Assert.Equal(ToolCategory.Utility, reg.Category);
    }

    [Fact]
    public void UtilityToolRegistration_HasUtilityHandler_WhenFactoryProvided()
    {
        var reg = new UtilityToolRegistration(
            Id: "test.utility",
            DisplayName: "Test Utility",
            Settings: null,
            UtilityHandlerFactory: null
        );

        Assert.False(reg.HasUtilityHandler);
    }

    // ========================================================================
    // TILE TOOL REGISTRATION
    // ========================================================================

    [Fact]
    public void TileToolRegistration_HasCorrectCategory()
    {
        var reg = new TileToolRegistration(
            Id: "test.tile",
            DisplayName: "Test Tile",
            Settings: null
        );

        Assert.Equal(ToolCategory.Tile, reg.Category);
    }

    [Fact]
    public void TileToolRegistration_HasTileHandler_WhenFactoryProvided()
    {
        var reg = new TileToolRegistration(
            Id: "test.tile",
            DisplayName: "Test Tile",
            Settings: null,
            TileHandlerFactory: null
        );

        Assert.False(reg.HasTileHandler);
    }

    // ========================================================================
    // RECORD EQUALITY
    // ========================================================================

    [Fact]
    public void BrushToolRegistration_RecordEquality_ComparesCorrectly()
    {
        Func<IStrokePainter> factory = () => new TestPainter();

        var reg1 = new BrushToolRegistration("id", "name", null, factory);
        var reg2 = new BrushToolRegistration("id", "name", null, factory);

        Assert.Equal(reg1, reg2);
    }

    [Fact]
    public void BrushToolRegistration_RecordEquality_DifferentIds()
    {
        Func<IStrokePainter> factory = () => new TestPainter();

        var reg1 = new BrushToolRegistration("id1", "name", null, factory);
        var reg2 = new BrushToolRegistration("id2", "name", null, factory);

        Assert.NotEqual(reg1, reg2);
    }

    // ========================================================================
    // RECORD WITH EXPRESSION
    // ========================================================================

    [Fact]
    public void BrushToolRegistration_WithExpression_CreatesModifiedCopy()
    {
        var original = new BrushToolRegistration(
            Id: "test.brush",
            DisplayName: "Original",
            Settings: null,
            PainterFactory: () => new TestPainter()
        );

        var modified = original with { DisplayName = "Modified" };

        Assert.Equal("Original", original.DisplayName);
        Assert.Equal("Modified", modified.DisplayName);
        Assert.Equal(original.Id, modified.Id);
    }

    // ========================================================================
    // INTERFACE IMPLEMENTATION
    // ========================================================================

    [Fact]
    public void AllRegistrations_ImplementIToolRegistration()
    {
        Assert.IsAssignableFrom<IToolRegistration>(new BrushToolRegistration("a", "b", null, () => new TestPainter()));
        Assert.IsAssignableFrom<IToolRegistration>(new ShapeToolRegistration("a", "b", null));
        Assert.IsAssignableFrom<IToolRegistration>(new SelectionToolRegistration("a", "b", null));
        Assert.IsAssignableFrom<IToolRegistration>(new UtilityToolRegistration("a", "b", null));
        Assert.IsAssignableFrom<IToolRegistration>(new TileToolRegistration("a", "b", null));
    }

    [Fact]
    public void IToolRegistration_ProvidesIdCategoryDisplayName()
    {
        IToolRegistration reg = new BrushToolRegistration(
            Id: "test.id",
            DisplayName: "Test Display",
            Settings: null,
            PainterFactory: () => new TestPainter()
        );

        Assert.Equal("test.id", reg.Id);
        Assert.Equal("Test Display", reg.DisplayName);
        Assert.Equal(ToolCategory.Brush, reg.Category);
    }
}
