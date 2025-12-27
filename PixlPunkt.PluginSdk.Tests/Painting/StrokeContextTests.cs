using PixlPunkt.PluginSdk.Imaging;
using PixlPunkt.PluginSdk.Painting;

namespace PixlPunkt.PluginSdk.Tests.Painting;

/// <summary>
/// Tests for <see cref="StrokeContext"/> - the context passed to painters.
/// </summary>
public class StrokeContextTests
{
    // ========================================================================
    // BOUNDS CHECKING
    // ========================================================================

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(9, 9, true)]
    [InlineData(5, 5, true)]
    [InlineData(-1, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(10, 0, false)]
    [InlineData(0, 10, false)]
    public void IsInBounds_ReturnsCorrectResult(int x, int y, bool expected)
    {
        var surface = new PixelSurface(10, 10);
        var ctx = CreateContext(surface);

        Assert.Equal(expected, ctx.IsInBounds(x, y));
    }

    // ========================================================================
    // INDEX CALCULATION
    // ========================================================================

    [Fact]
    public void IndexOf_DelegatesToSurface()
    {
        var surface = new PixelSurface(10, 10);
        var ctx = CreateContext(surface);

        Assert.Equal(surface.IndexOf(5, 5), ctx.IndexOf(5, 5));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 4)]
    [InlineData(0, 1, 40)] // 10 * 4
    [InlineData(5, 5, 220)] // (5 * 10 + 5) * 4
    public void IndexOf_CalculatesCorrectByteIndex(int x, int y, int expected)
    {
        var surface = new PixelSurface(10, 10);
        var ctx = CreateContext(surface);

        Assert.Equal(expected, ctx.IndexOf(x, y));
    }

    // ========================================================================
    // SELECTION MASKING
    // ========================================================================

    [Fact]
    public void IsInSelection_NoMask_ReturnsTrue()
    {
        var surface = new PixelSurface(10, 10);
        var ctx = CreateContext(surface, selectionMask: null);

        Assert.True(ctx.IsInSelection(0, 0));
        Assert.True(ctx.IsInSelection(5, 5));
        Assert.True(ctx.IsInSelection(9, 9));
    }

    [Fact]
    public void IsInSelection_WithMask_ReturnsTrue_WhenMaskReturnsTrue()
    {
        var surface = new PixelSurface(10, 10);
        // Mask that includes only x >= 5
        var ctx = CreateContext(surface, selectionMask: (x, y) => x >= 5);

        Assert.False(ctx.IsInSelection(4, 5));
        Assert.True(ctx.IsInSelection(5, 5));
        Assert.True(ctx.IsInSelection(9, 5));
    }

    [Fact]
    public void IsInSelection_WithMask_ReturnsFalse_WhenMaskReturnsFalse()
    {
        var surface = new PixelSurface(10, 10);
        // Mask that excludes everything
        var ctx = CreateContext(surface, selectionMask: (_, _) => false);

        Assert.False(ctx.IsInSelection(0, 0));
        Assert.False(ctx.IsInSelection(5, 5));
    }

    // ========================================================================
    // ALPHA COMPUTATION DELEGATE
    // ========================================================================

    [Fact]
    public void ComputeAlphaAtOffset_DelegateIsCalled()
    {
        var surface = new PixelSurface(10, 10);
        bool wasCalled = false;

        var ctx = new StrokeContext
        {
            Surface = surface,
            ForegroundColor = 0xFFFF0000,
            BrushSize = 10,
            BrushShape = BrushShape.Circle,
            BrushDensity = 255,
            BrushOpacity = 255,
            BrushOffsets = [(0, 0)],
            ComputeAlphaAtOffset = (dx, dy) =>
            {
                wasCalled = true;
                return 255;
            }
        };

        ctx.ComputeAlphaAtOffset(0, 0);
        Assert.True(wasCalled);
    }

    [Fact]
    public void ComputeAlphaAtOffset_ReceivesCorrectParameters()
    {
        var surface = new PixelSurface(10, 10);
        int receivedDx = -1, receivedDy = -1;

        var ctx = new StrokeContext
        {
            Surface = surface,
            ForegroundColor = 0xFFFF0000,
            BrushSize = 10,
            BrushShape = BrushShape.Circle,
            BrushDensity = 255,
            BrushOpacity = 255,
            BrushOffsets = [(0, 0)],
            ComputeAlphaAtOffset = (dx, dy) =>
            {
                receivedDx = dx;
                receivedDy = dy;
                return 128;
            }
        };

        ctx.ComputeAlphaAtOffset(3, 7);

        Assert.Equal(3, receivedDx);
        Assert.Equal(7, receivedDy);
    }

    // ========================================================================
    // BRUSH OFFSETS
    // ========================================================================

    [Fact]
    public void BrushOffsets_DefaultsToEmpty()
    {
        var surface = new PixelSurface(10, 10);
        var ctx = new StrokeContext
        {
            Surface = surface,
            ForegroundColor = 0xFFFF0000,
            ComputeAlphaAtOffset = (_, _) => 255
        };

        Assert.Empty(ctx.BrushOffsets);
    }

    [Fact]
    public void BrushOffsets_ContainsProvided()
    {
        var surface = new PixelSurface(10, 10);
        var offsets = new List<(int, int)> { (-1, 0), (0, 0), (1, 0), (0, -1), (0, 1) };

        var ctx = new StrokeContext
        {
            Surface = surface,
            ForegroundColor = 0xFFFF0000,
            BrushOffsets = offsets,
            ComputeAlphaAtOffset = (_, _) => 255
        };

        Assert.Equal(5, ctx.BrushOffsets.Count);
        Assert.Contains((0, 0), ctx.BrushOffsets);
        Assert.Contains((-1, 0), ctx.BrushOffsets);
    }

    // ========================================================================
    // SNAPSHOT HANDLING
    // ========================================================================

    [Fact]
    public void Snapshot_CanBeNull()
    {
        var surface = new PixelSurface(10, 10);
        var ctx = CreateContext(surface);

        Assert.Null(ctx.Snapshot);
    }

    [Fact]
    public void Snapshot_CanBeProvided()
    {
        var surface = new PixelSurface(10, 10);
        var snapshot = new byte[10 * 10 * 4];
        snapshot[0] = 0xAB; // Mark it

        var ctx = new StrokeContext
        {
            Surface = surface,
            ForegroundColor = 0xFFFF0000,
            BrushOffsets = [],
            ComputeAlphaAtOffset = (_, _) => 255,
            Snapshot = snapshot
        };

        Assert.NotNull(ctx.Snapshot);
        Assert.Equal(0xAB, ctx.Snapshot[0]);
    }

    // ========================================================================
    // CUSTOM BRUSH
    // ========================================================================

    [Fact]
    public void IsCustomBrush_DefaultsFalse()
    {
        var surface = new PixelSurface(10, 10);
        var ctx = CreateContext(surface);

        Assert.False(ctx.IsCustomBrush);
    }

    [Fact]
    public void CustomBrushFullName_DefaultsNull()
    {
        var surface = new PixelSurface(10, 10);
        var ctx = CreateContext(surface);

        Assert.Null(ctx.CustomBrushFullName);
    }

    [Fact]
    public void CustomBrush_CanBeConfigured()
    {
        var surface = new PixelSurface(10, 10);

        var ctx = new StrokeContext
        {
            Surface = surface,
            ForegroundColor = 0xFFFF0000,
            BrushOffsets = [],
            ComputeAlphaAtOffset = (_, _) => 255,
            IsCustomBrush = true,
            CustomBrushFullName = "author.brushname"
        };

        Assert.True(ctx.IsCustomBrush);
        Assert.Equal("author.brushname", ctx.CustomBrushFullName);
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    private static StrokeContext CreateContext(
        PixelSurface surface,
        Func<int, int, bool>? selectionMask = null)
    {
        return new StrokeContext
        {
            Surface = surface,
            ForegroundColor = 0xFFFF0000,
            BackgroundColor = 0xFF000000,
            BrushSize = 10,
            BrushShape = BrushShape.Circle,
            BrushDensity = 255,
            BrushOpacity = 255,
            IsCustomBrush = false,
            BrushOffsets = [(0, 0)],
            ComputeAlphaAtOffset = (_, _) => 255,
            SelectionMask = selectionMask
        };
    }
}
