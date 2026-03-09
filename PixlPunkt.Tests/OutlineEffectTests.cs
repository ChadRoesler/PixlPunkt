namespace PixlPunkt.Tests;

using PixlPunkt.Core.Compositing.Effects;

/// <summary>
/// Tests for <see cref="OutlineEffect"/> BFS distance transform algorithm.
/// Verifies correctness of outline generation, edge cases, and the snapshot overload.
/// </summary>
[TestFixture]
public class OutlineEffectTests
{
    private const uint Transparent = 0x00000000;
    private const uint OpaqueWhite = 0xFFFFFFFF;
    private const uint OpaqueBlack = 0xFF000000;
    private const uint OpaqueRed = 0xFFFF0000;

    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    private static uint[] CreateBuffer(int w, int h, uint fill = Transparent)
    {
        var buf = new uint[w * h];
        Array.Fill(buf, fill);
        return buf;
    }

    private static void SetPixel(uint[] buf, int w, int x, int y, uint color)
        => buf[y * w + x] = color;

    private static uint GetPixel(uint[] buf, int w, int x, int y)
        => buf[y * w + x];

    private static bool IsOpaque(uint c) => (c >> 24) != 0;

    private static OutlineEffect CreateEffect(int thickness = 1, uint color = OpaqueBlack)
        => new() { Thickness = thickness, Color = color, IsEnabled = true };

    private static void ApplyEffect(OutlineEffect effect, uint[] pixels, int w, int h)
    {
        Span<uint> span = pixels.AsSpan();
        effect.Apply(span, w, h);
    }

    // ════════════════════════════════════════════════════════════════════
    // BASIC BEHAVIOR
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Apply_AllTransparent_RemainsTransparent()
    {
        var buf = CreateBuffer(8, 8);
        var effect = CreateEffect(thickness: 3);

        ApplyEffect(effect, buf, 8, 8);

        buf.Should().OnlyContain(p => p == Transparent);
    }

    [Test]
    public void Apply_AllOpaque_PreservesAllPixels()
    {
        var buf = CreateBuffer(8, 8, OpaqueWhite);
        var original = buf.ToArray();
        var effect = CreateEffect(thickness: 3);

        ApplyEffect(effect, buf, 8, 8);

        buf.Should().Equal(original);
    }

    [Test]
    public void Apply_SinglePixelCenter_CreatesOutline()
    {
        // 5x5 canvas with single opaque pixel at center (2,2)
        int w = 5, h = 5;
        var buf = CreateBuffer(w, h);
        SetPixel(buf, w, 2, 2, OpaqueWhite);

        var effect = CreateEffect(thickness: 1);
        ApplyEffect(effect, buf, w, h);

        // Center pixel preserved
        GetPixel(buf, w, 2, 2).Should().Be(OpaqueWhite);

        // Direct 8-connected neighbors should be outline
        GetPixel(buf, w, 1, 1).Should().Be(OpaqueBlack,
            "diagonal neighbor should be outlined at thickness=1");
        GetPixel(buf, w, 2, 1).Should().Be(OpaqueBlack,
            "top neighbor should be outlined");
        GetPixel(buf, w, 3, 2).Should().Be(OpaqueBlack,
            "right neighbor should be outlined");
        GetPixel(buf, w, 2, 3).Should().Be(OpaqueBlack,
            "bottom neighbor should be outlined");
        GetPixel(buf, w, 1, 2).Should().Be(OpaqueBlack,
            "left neighbor should be outlined");

        // Corners of canvas should remain transparent
        GetPixel(buf, w, 0, 0).Should().Be(Transparent);
        GetPixel(buf, w, 4, 4).Should().Be(Transparent);
    }

    [Test]
    public void Apply_SinglePixelCorner_CreatesPartialOutline()
    {
        // Opaque pixel at (0,0) — outline only in available directions
        int w = 4, h = 4;
        var buf = CreateBuffer(w, h);
        SetPixel(buf, w, 0, 0, OpaqueWhite);

        var effect = CreateEffect(thickness: 1);
        ApplyEffect(effect, buf, w, h);

        GetPixel(buf, w, 0, 0).Should().Be(OpaqueWhite, "source pixel preserved");
        GetPixel(buf, w, 1, 0).Should().Be(OpaqueBlack, "right neighbor outlined");
        GetPixel(buf, w, 0, 1).Should().Be(OpaqueBlack, "bottom neighbor outlined");
        GetPixel(buf, w, 1, 1).Should().Be(OpaqueBlack, "diagonal neighbor outlined");
    }

    // ════════════════════════════════════════════════════════════════════
    // THICKNESS BEHAVIOR
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Apply_Thickness2_ExpandsFurther()
    {
        // 7x7 canvas with single pixel center (3,3)
        int w = 7, h = 7;
        var buf = CreateBuffer(w, h);
        SetPixel(buf, w, 3, 3, OpaqueWhite);

        var effect = CreateEffect(thickness: 2);
        ApplyEffect(effect, buf, w, h);

        // Center pixel preserved
        GetPixel(buf, w, 3, 3).Should().Be(OpaqueWhite);

        // Distance 1 neighbors = outline
        GetPixel(buf, w, 3, 2).Should().Be(OpaqueBlack, "distance 1");

        // Distance 2 neighbors = still outline
        GetPixel(buf, w, 3, 1).Should().Be(OpaqueBlack, "distance 2 should be outlined at thickness=2");

        // Distance 3 neighbors = not outlined
        GetPixel(buf, w, 3, 0).Should().Be(Transparent, "distance 3 should not be outlined at thickness=2");
    }

    [Test]
    public void Apply_Thickness1And2_OutlineGrows()
    {
        int w = 9, h = 9;

        // Place a 3x3 block in center
        var buf1 = CreateBuffer(w, h);
        var buf2 = CreateBuffer(w, h);
        for (int y = 3; y <= 5; y++)
        for (int x = 3; x <= 5; x++)
        {
            SetPixel(buf1, w, x, y, OpaqueWhite);
            SetPixel(buf2, w, x, y, OpaqueWhite);
        }

        ApplyEffect(CreateEffect(thickness: 1), buf1, w, h);
        ApplyEffect(CreateEffect(thickness: 2), buf2, w, h);

        // Count outlined pixels (non-transparent, non-white)
        int count1 = buf1.Count(p => p == OpaqueBlack);
        int count2 = buf2.Count(p => p == OpaqueBlack);

        count2.Should().BeGreaterThan(count1,
            "thickness=2 should produce more outline pixels than thickness=1");
    }

    // ════════════════════════════════════════════════════════════════════
    // OUTLINE COLOR
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Apply_CustomColor_UsesSpecifiedColor()
    {
        int w = 5, h = 5;
        var buf = CreateBuffer(w, h);
        SetPixel(buf, w, 2, 2, OpaqueWhite);

        var effect = CreateEffect(thickness: 1, color: OpaqueRed);
        ApplyEffect(effect, buf, w, h);

        GetPixel(buf, w, 2, 1).Should().Be(OpaqueRed, "outline should use custom color");
    }

    // ════════════════════════════════════════════════════════════════════
    // DISABLED EFFECT
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Apply_Disabled_DoesNothing()
    {
        int w = 5, h = 5;
        var buf = CreateBuffer(w, h);
        SetPixel(buf, w, 2, 2, OpaqueWhite);
        var original = buf.ToArray();

        var effect = CreateEffect(thickness: 2);
        effect.IsEnabled = false;
        ApplyEffect(effect, buf, w, h);

        buf.Should().Equal(original);
    }

    // ════════════════════════════════════════════════════════════════════
    // SNAPSHOT OVERLOAD PARITY
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Apply_SnapshotOverload_ProducesSameResult()
    {
        int w = 8, h = 8;

        // Build a shape: L-shape
        var buf1 = CreateBuffer(w, h);
        var buf2 = CreateBuffer(w, h);
        for (int y = 1; y <= 5; y++) { SetPixel(buf1, w, 2, y, OpaqueWhite); SetPixel(buf2, w, 2, y, OpaqueWhite); }
        for (int x = 2; x <= 5; x++) { SetPixel(buf1, w, x, 5, OpaqueWhite); SetPixel(buf2, w, x, 5, OpaqueWhite); }

        var effect = CreateEffect(thickness: 2, color: OpaqueRed);

        // Single-arg Apply (uses ToArray internally)
        Span<uint> span1 = buf1.AsSpan();
        effect.Apply(span1, w, h);

        // Snapshot overload
        ReadOnlySpan<uint> snapshot = buf2.ToArray().AsSpan();
        Span<uint> span2 = buf2.AsSpan();
        effect.Apply(span2, snapshot, w, h);

        buf1.Should().Equal(buf2, "both Apply overloads should produce identical results");
    }

    // ════════════════════════════════════════════════════════════════════
    // EDGE CASES
    // ════════════════════════════════════════════════════════════════════

    [Test]
    public void Apply_1x1Canvas_OpaquePixel_Preserved()
    {
        var buf = new uint[] { OpaqueWhite };
        var effect = CreateEffect(thickness: 3);
        ApplyEffect(effect, buf, 1, 1);

        buf[0].Should().Be(OpaqueWhite);
    }

    [Test]
    public void Apply_1x1Canvas_TransparentPixel_RemainsTransparent()
    {
        var buf = new uint[] { Transparent };
        var effect = CreateEffect(thickness: 3);
        ApplyEffect(effect, buf, 1, 1);

        buf[0].Should().Be(Transparent);
    }

    [Test]
    public void Apply_SemiTransparentPixel_TreatedAsOpaque()
    {
        // Alpha > 0 should be treated as "opaque" for outline detection
        int w = 5, h = 5;
        uint semiTransparent = 0x80FF0000; // 50% alpha red
        var buf = CreateBuffer(w, h);
        SetPixel(buf, w, 2, 2, semiTransparent);

        var effect = CreateEffect(thickness: 1);
        ApplyEffect(effect, buf, w, h);

        // Center preserved
        GetPixel(buf, w, 2, 2).Should().Be(semiTransparent);

        // Neighbors should be outlined
        GetPixel(buf, w, 2, 1).Should().Be(OpaqueBlack);
    }

    [Test]
    public void Apply_FullRow_OutlinesAboveAndBelow()
    {
        // Full row of opaque pixels at y=2 in a 6x5 canvas
        int w = 6, h = 5;
        var buf = CreateBuffer(w, h);
        for (int x = 0; x < w; x++)
            SetPixel(buf, w, x, 2, OpaqueWhite);

        var effect = CreateEffect(thickness: 1);
        ApplyEffect(effect, buf, w, h);

        // Row above (y=1) should be all outline
        for (int x = 0; x < w; x++)
            GetPixel(buf, w, x, 1).Should().Be(OpaqueBlack, $"pixel ({x},1) should be outlined");

        // Row below (y=3) should be all outline
        for (int x = 0; x < w; x++)
            GetPixel(buf, w, x, 3).Should().Be(OpaqueBlack, $"pixel ({x},3) should be outlined");

        // Original row preserved
        for (int x = 0; x < w; x++)
            GetPixel(buf, w, x, 2).Should().Be(OpaqueWhite);
    }

    [Test]
    public void Apply_NeedsSnapshot_ReturnsTrue()
    {
        var effect = new OutlineEffect();
        effect.NeedsSnapshot.Should().BeTrue();
    }
}
