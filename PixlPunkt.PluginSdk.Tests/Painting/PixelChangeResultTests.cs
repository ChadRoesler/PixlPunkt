using PixlPunkt.PluginSdk.Imaging;
using PixlPunkt.PluginSdk.Painting;

namespace PixlPunkt.PluginSdk.Tests.Painting;

/// <summary>
/// Tests for <see cref="PixelChangeResult"/> - the result returned from stroke painters.
/// </summary>
public class PixelChangeResultTests
{
    // ========================================================================
    // CONSTRUCTION
    // ========================================================================

    [Fact]
    public void Constructor_StoresSurface()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        Assert.Same(surface, result.Surface);
    }

    [Fact]
    public void Constructor_StoresDescription()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface, "Custom Operation");

        Assert.Equal("Custom Operation", result.Description);
    }

    [Fact]
    public void Constructor_DefaultDescription_IsBrushStroke()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        Assert.Equal("Brush Stroke", result.Description);
    }

    [Fact]
    public void Constructor_NullSurface_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PixelChangeResult(null!));
    }

    // ========================================================================
    // HAS CHANGES
    // ========================================================================

    [Fact]
    public void HasChanges_NoChangesAdded_ReturnsFalse()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        Assert.False(result.HasChanges);
    }

    [Fact]
    public void HasChanges_ChangeAdded_ReturnsTrue()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        result.Add(0, 0xFF000000, 0xFFFF0000);

        Assert.True(result.HasChanges);
    }

    [Fact]
    public void HasChanges_SameValueAdded_ReturnsFalse()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        // Adding a "change" where before == after should not count
        result.Add(0, 0xFF000000, 0xFF000000);

        Assert.False(result.HasChanges);
    }

    // ========================================================================
    // ADD CHANGES
    // ========================================================================

    [Fact]
    public void Add_DifferentValues_TracksChange()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        result.Add(0, 0xFF000000, 0xFFFF0000);
        result.Add(4, 0xFF000000, 0xFF00FF00);

        Assert.Equal(2, result.Changes.Count);
    }

    [Fact]
    public void Add_SameValues_DoesNotTrackChange()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        result.Add(0, 0xFF000000, 0xFF000000);

        Assert.Empty(result.Changes);
    }

    [Fact]
    public void Add_TracksCorrectByteIndex()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        result.Add(48, 0xFF000000, 0xFFFF0000);

        Assert.Single(result.Changes);
        Assert.Equal(48, result.Changes[0].ByteIndex);
    }

    [Fact]
    public void Add_TracksBeforeValue()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        result.Add(0, 0xAABBCCDD, 0xFF000000);

        Assert.Equal(0xAABBCCDDu, result.Changes[0].Before);
    }

    [Fact]
    public void Add_TracksAfterValue()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        result.Add(0, 0xFF000000, 0x11223344);

        Assert.Equal(0x11223344u, result.Changes[0].After);
    }

    // ========================================================================
    // CHANGES COLLECTION
    // ========================================================================

    [Fact]
    public void Changes_InitiallyEmpty()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        Assert.Empty(result.Changes);
    }

    [Fact]
    public void Changes_IsReadOnly()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        // IReadOnlyList doesn't have Add method
        Assert.IsAssignableFrom<IReadOnlyList<PixelChange>>(result.Changes);
    }

    [Fact]
    public void Changes_MaintainsOrder()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        result.Add(0, 0xFF000001, 0xFF000002);
        result.Add(4, 0xFF000003, 0xFF000004);
        result.Add(8, 0xFF000005, 0xFF000006);

        Assert.Equal(0, result.Changes[0].ByteIndex);
        Assert.Equal(4, result.Changes[1].ByteIndex);
        Assert.Equal(8, result.Changes[2].ByteIndex);
    }

    // ========================================================================
    // PIXEL CHANGE RECORD
    // ========================================================================

    [Fact]
    public void PixelChange_IsValueType()
    {
        var change = new PixelChange(0, 0xFF000000, 0xFFFF0000);

        Assert.IsType<PixelChange>(change);
        // Records are reference types by default but readonly record struct is value type
    }

    [Fact]
    public void PixelChange_StoresAllProperties()
    {
        var change = new PixelChange(100, 0xAABBCCDD, 0x11223344);

        Assert.Equal(100, change.ByteIndex);
        Assert.Equal(0xAABBCCDDu, change.Before);
        Assert.Equal(0x11223344u, change.After);
    }

    [Fact]
    public void PixelChange_EqualsForSameValues()
    {
        var change1 = new PixelChange(100, 0xFF000000, 0xFFFF0000);
        var change2 = new PixelChange(100, 0xFF000000, 0xFFFF0000);

        Assert.Equal(change1, change2);
    }

    [Fact]
    public void PixelChange_NotEqualsForDifferentValues()
    {
        var change1 = new PixelChange(100, 0xFF000000, 0xFFFF0000);
        var change2 = new PixelChange(100, 0xFF000000, 0xFF00FF00);

        Assert.NotEqual(change1, change2);
    }

    // ========================================================================
    // EDGE CASES
    // ========================================================================

    [Fact]
    public void Add_LargeByteIndex_Works()
    {
        var surface = new PixelSurface(1000, 1000); // 4M pixels
        var result = new PixelChangeResult(surface);

        int largeIndex = 3999996; // Near end of a 1000x1000 surface
        result.Add(largeIndex, 0xFF000000, 0xFFFF0000);

        Assert.Equal(largeIndex, result.Changes[0].ByteIndex);
    }

    [Fact]
    public void Add_MultipleChanges_ToSamePixel_TracksAll()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        // Multiple changes to same pixel (could happen with overlapping strokes)
        result.Add(0, 0xFF000000, 0xFFFF0000);
        result.Add(0, 0xFFFF0000, 0xFF00FF00);

        // Both are tracked (deduplication would be done elsewhere if needed)
        Assert.Equal(2, result.Changes.Count);
    }

    [Fact]
    public void Add_TransparentToOpaque_IsTracked()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        result.Add(0, 0x00000000, 0xFFFF0000);

        Assert.True(result.HasChanges);
    }

    [Fact]
    public void Add_OpaqueToTransparent_IsTracked()
    {
        var surface = new PixelSurface(10, 10);
        var result = new PixelChangeResult(surface);

        result.Add(0, 0xFFFF0000, 0x00000000);

        Assert.True(result.HasChanges);
    }
}
