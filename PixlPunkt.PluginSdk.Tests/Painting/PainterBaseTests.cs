using PixlPunkt.PluginSdk.Imaging;
using PixlPunkt.PluginSdk.Painting;

namespace PixlPunkt.PluginSdk.Tests.Painting;

/// <summary>
/// Tests for <see cref="PainterBase"/> - the abstract base class for stroke painters.
/// </summary>
public class PainterBaseTests
{
    // ========================================================================
    // TEST DOUBLE - MINIMAL IMPLEMENTATION
    // ========================================================================

    /// <summary>
    /// Simple test implementation of PainterBase that tracks method calls.
    /// </summary>
    private class TestPainter : PainterBase
    {
        public override bool NeedsSnapshot => false;

        public List<(int x, int y)> StampedPositions { get; } = new();

        public uint PaintColor { get; set; } = 0xFFFF0000; // Red by default

        public override void StampAt(int cx, int cy, StrokeContext context)
        {
            StampedPositions.Add((cx, cy));

            if (Surface == null) return;
            if (!context.IsInBounds(cx, cy)) return;

            int idx = Surface.IndexOf(cx, cy);
            if (idx < 0 || idx + 3 >= Surface.Pixels.Length) return;

            uint current = ReadPixel(Surface.Pixels, idx);
            var rec = GetOrCreateAccumRec(idx, current);
            rec.after = PaintColor;
            CommitAccumRec(idx, rec);
        }
    }

    /// <summary>
    /// Painter that requires a snapshot (like blur/smudge effects).
    /// </summary>
    private class SnapshotPainter : PainterBase
    {
        public override bool NeedsSnapshot => true;
        public byte[]? ReceivedSnapshot { get; private set; }

        public override void Begin(PixelSurface surface, byte[]? snapshot)
        {
            base.Begin(surface, snapshot);
            ReceivedSnapshot = snapshot;
        }

        public override void StampAt(int cx, int cy, StrokeContext context) { }
    }

    // ========================================================================
    // BEGIN / END LIFECYCLE
    // ========================================================================

    [Fact]
    public void Begin_ClearsAccumulationState()
    {
        var painter = new TestPainter();
        var surface = new PixelSurface(10, 10);
        var context = CreateContext(surface);

        // First stroke
        painter.Begin(surface, null);
        painter.StampAt(5, 5, context);
        painter.End();

        // Second stroke - should start fresh
        painter.Begin(surface, null);
        var result = painter.End() as PixelChangeResult;

        // If no stamps were made, result should be null or empty
        Assert.True(result == null || !result.HasChanges);
    }

    [Fact]
    public void Begin_StoresSnapshot()
    {
        var painter = new SnapshotPainter();
        var surface = new PixelSurface(10, 10);
        var snapshot = new byte[10 * 10 * 4];

        painter.Begin(surface, snapshot);

        Assert.Same(snapshot, painter.ReceivedSnapshot);
    }

    [Fact]
    public void End_ClearsState()
    {
        var painter = new TestPainter();
        var surface = new PixelSurface(10, 10);
        var context = CreateContext(surface);

        painter.Begin(surface, null);
        painter.StampAt(5, 5, context);
        painter.End();

        // Starting a new stroke should have clean state
        painter.StampedPositions.Clear();
        painter.Begin(surface, null);

        // This should not fail even though we're starting fresh
        painter.StampAt(3, 3, context);
        var result = painter.End() as PixelChangeResult;

        Assert.NotNull(result);
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void End_WithoutBegin_ReturnsNull()
    {
        var painter = new TestPainter();

        var result = painter.End();

        Assert.Null(result);
    }

    // ========================================================================
    // STAMP LINE
    // ========================================================================

    [Fact]
    public void StampLine_SinglePoint_StampsOnce()
    {
        var painter = new TestPainter();
        var surface = new PixelSurface(10, 10);
        var context = CreateContext(surface);

        painter.Begin(surface, null);
        painter.StampLine(5, 5, 5, 5, context);
        painter.End();

        Assert.Single(painter.StampedPositions);
        Assert.Equal((5, 5), painter.StampedPositions[0]);
    }

    [Fact]
    public void StampLine_Horizontal_StampsAllPixels()
    {
        var painter = new TestPainter();
        var surface = new PixelSurface(10, 10);
        var context = CreateContext(surface);

        painter.Begin(surface, null);
        painter.StampLine(0, 5, 5, 5, context);
        painter.End();

        // Should stamp at each pixel from (0,5) to (5,5)
        Assert.Equal(6, painter.StampedPositions.Count);
        Assert.Contains((0, 5), painter.StampedPositions);
        Assert.Contains((5, 5), painter.StampedPositions);
    }

    [Fact]
    public void StampLine_Vertical_StampsAllPixels()
    {
        var painter = new TestPainter();
        var surface = new PixelSurface(10, 10);
        var context = CreateContext(surface);

        painter.Begin(surface, null);
        painter.StampLine(5, 0, 5, 5, context);
        painter.End();

        // Should stamp at each pixel from (5,0) to (5,5)
        Assert.Equal(6, painter.StampedPositions.Count);
        Assert.Contains((5, 0), painter.StampedPositions);
        Assert.Contains((5, 5), painter.StampedPositions);
    }

    [Fact]
    public void StampLine_Diagonal_StampsReasonableCount()
    {
        var painter = new TestPainter();
        var surface = new PixelSurface(20, 20);
        var context = CreateContext(surface);

        painter.Begin(surface, null);
        painter.StampLine(0, 0, 10, 10, context);
        painter.End();

        // Diagonal of 10 pixels should stamp 11 times (inclusive)
        Assert.Equal(11, painter.StampedPositions.Count);
        Assert.Contains((0, 0), painter.StampedPositions);
        Assert.Contains((10, 10), painter.StampedPositions);
    }

    // ========================================================================
    // PIXEL CHANGE TRACKING
    // ========================================================================

    [Fact]
    public void End_ReturnsPixelChangeResult()
    {
        var painter = new TestPainter();
        var surface = new PixelSurface(10, 10);
        surface.Clear(0xFF000000); // Black
        var context = CreateContext(surface);

        painter.PaintColor = 0xFFFF0000; // Red
        painter.Begin(surface, null);
        painter.StampAt(5, 5, context);
        var result = painter.End() as PixelChangeResult;

        Assert.NotNull(result);
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void End_NoChanges_ReturnsNull()
    {
        var painter = new TestPainter();
        var surface = new PixelSurface(10, 10);
        surface.Clear(0xFFFF0000); // Red
        var context = CreateContext(surface);

        painter.PaintColor = 0xFFFF0000; // Same red - no change
        painter.Begin(surface, null);
        painter.StampAt(5, 5, context);
        var result = painter.End();

        // Same color = no change = null result
        Assert.Null(result);
    }

    [Fact]
    public void Accumulation_TracksOriginalBeforeValue()
    {
        var painter = new TestPainter();
        var surface = new PixelSurface(10, 10);
        surface.Clear(0xFF000000); // Black
        var context = CreateContext(surface);

        painter.Begin(surface, null);

        // First stamp makes it red
        painter.PaintColor = 0xFFFF0000;
        painter.StampAt(5, 5, context);

        // Second stamp changes to blue
        painter.PaintColor = 0xFF0000FF;
        painter.StampAt(5, 5, context);

        var result = painter.End() as PixelChangeResult;

        // The "before" should still be the original black, not the intermediate red
        Assert.NotNull(result);
        Assert.True(result.HasChanges);
    }

    // ========================================================================
    // HELPER PIXEL METHODS
    // ========================================================================

    [Fact]
    public void ReadPixel_ParsesBgraCorrectly()
    {
        byte[] pixels = [0xAA, 0xBB, 0xCC, 0xDD]; // B, G, R, A

        // Use reflection to test protected static method
        var method = typeof(PainterBase).GetMethod("ReadPixel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (uint)method!.Invoke(null, [pixels, 0])!;

        // Expected: 0xDDCCBBAA (A=DD, R=CC, G=BB, B=AA)
        Assert.Equal(0xDDCCBBAAu, result);
    }

    [Fact]
    public void WritePixel_WritesBgraCorrectly()
    {
        byte[] pixels = new byte[4];
        uint color = 0xAABBCCDD;

        var method = typeof(PainterBase).GetMethod("WritePixel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method!.Invoke(null, [pixels, 0, color]);

        Assert.Equal(0xDD, pixels[0]); // B
        Assert.Equal(0xCC, pixels[1]); // G
        Assert.Equal(0xBB, pixels[2]); // R
        Assert.Equal(0xAA, pixels[3]); // A
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    private static StrokeContext CreateContext(PixelSurface surface)
    {
        return new StrokeContext
        {
            Surface = surface,
            ForegroundColor = 0xFFFF0000,
            BackgroundColor = 0xFF000000,
            BrushSize = 1,
            BrushShape = BrushShape.Circle,
            BrushDensity = 255,
            BrushOpacity = 255,
            IsCustomBrush = false,
            BrushOffsets = [(0, 0)],
            ComputeAlphaAtOffset = (_, _) => 255
        };
    }
}
