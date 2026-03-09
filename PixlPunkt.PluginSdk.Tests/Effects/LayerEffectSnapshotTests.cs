using PixlPunkt.PluginSdk.Compositing;

namespace PixlPunkt.PluginSdk.Tests.Effects;

/// <summary>
/// Tests for the <see cref="LayerEffectBase"/> snapshot contract:
/// <see cref="LayerEffectBase.NeedsSnapshot"/> and the dual Apply overloads.
/// </summary>
public class LayerEffectSnapshotTests
{
    // ════════════════════════════════════════════════════════════════════
    // TEST DOUBLES
    // ════════════════════════════════════════════════════════════════════

    /// <summary>Simple in-place effect that doesn't need a snapshot.</summary>
    private sealed class InPlaceEffect : LayerEffectBase
    {
        public override string DisplayName => "In-Place";
        public int ApplyCallCount { get; private set; }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            ApplyCallCount++;
            // Invert alpha of each pixel
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] ^= 0xFF000000;
        }
    }

    /// <summary>Effect that needs a snapshot to sample neighbors.</summary>
    private sealed class SnapshotEffect : LayerEffectBase
    {
        public override string DisplayName => "Snapshot";
        public override bool NeedsSnapshot => true;

        public int SingleArgCallCount { get; private set; }
        public int SnapshotArgCallCount { get; private set; }
        public ReadOnlySpan<uint> LastSnapshot => _lastSnapshot;
        private uint[] _lastSnapshot = [];

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            SingleArgCallCount++;
        }

        public override void Apply(Span<uint> pixels, ReadOnlySpan<uint> snapshot, int width, int height)
        {
            SnapshotArgCallCount++;
            _lastSnapshot = snapshot.ToArray();
            // Write inverted snapshot data to pixels
            for (int i = 0; i < pixels.Length && i < snapshot.Length; i++)
                pixels[i] = snapshot[i] ^ 0xFF000000;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // NeedsSnapshot DEFAULTS
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void NeedsSnapshot_DefaultIsFalse()
    {
        var effect = new InPlaceEffect();
        Assert.False(effect.NeedsSnapshot);
    }

    [Fact]
    public void NeedsSnapshot_CanBeOverriddenToTrue()
    {
        var effect = new SnapshotEffect();
        Assert.True(effect.NeedsSnapshot);
    }

    // ════════════════════════════════════════════════════════════════════
    // DEFAULT SNAPSHOT OVERLOAD DELEGATION
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Apply_SnapshotOverload_DefaultDelegatesToSingleArg()
    {
        // InPlaceEffect doesn't override the snapshot overload,
        // so the base class should delegate to the single-arg Apply.
        var effect = new InPlaceEffect();
        var pixels = new uint[] { 0xFF000000, 0x00FFFFFF };
        var snapshot = new uint[] { 0xFF000000, 0x00FFFFFF };

        Span<uint> span = pixels.AsSpan();
        ReadOnlySpan<uint> snap = snapshot.AsSpan();
        effect.Apply(span, snap, 2, 1);

        Assert.Equal(1, effect.ApplyCallCount);
        // Verify the single-arg Apply actually ran (inverted alpha)
        Assert.Equal(0x00000000u, pixels[0]);
        Assert.Equal(0xFFFFFFFFu, pixels[1]);
    }

    // ════════════════════════════════════════════════════════════════════
    // SNAPSHOT OVERLOAD WHEN OVERRIDDEN
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Apply_SnapshotOverload_WhenOverridden_CallsOverride()
    {
        var effect = new SnapshotEffect();
        var pixels = new uint[] { 0xFFAABBCC, 0xFF112233 };
        var snapshot = new uint[] { 0xFFAABBCC, 0xFF112233 };

        Span<uint> span = pixels.AsSpan();
        ReadOnlySpan<uint> snap = snapshot.AsSpan();
        effect.Apply(span, snap, 2, 1);

        Assert.Equal(1, effect.SnapshotArgCallCount);
        Assert.Equal(0, effect.SingleArgCallCount);
    }

    [Fact]
    public void Apply_SnapshotOverload_ReceivesCorrectSnapshotData()
    {
        var effect = new SnapshotEffect();
        uint[] original = [0xFF112233, 0xFFAABBCC, 0xFF445566];
        var pixels = original.ToArray();
        var snapshot = original.ToArray();

        Span<uint> span = pixels.AsSpan();
        ReadOnlySpan<uint> snap = snapshot.AsSpan();
        effect.Apply(span, snap, 3, 1);

        // Verify the snapshot data matched the original
        var received = effect.LastSnapshot.ToArray();
        Assert.Equal(original, received);
    }

    [Fact]
    public void Apply_SnapshotOverload_CanWriteToPixelsFromSnapshot()
    {
        var effect = new SnapshotEffect();
        var pixels = new uint[] { 0xFF000000, 0xFF000000 };
        var snapshot = new uint[] { 0xFF000000, 0xFF000000 };

        Span<uint> span = pixels.AsSpan();
        ReadOnlySpan<uint> snap = snapshot.AsSpan();
        effect.Apply(span, snap, 2, 1);

        // SnapshotEffect writes snapshot ^ 0xFF000000
        Assert.Equal(0x00000000u, pixels[0]);
        Assert.Equal(0x00000000u, pixels[1]);
    }

    // ════════════════════════════════════════════════════════════════════
    // SINGLE-ARG Apply STILL WORKS
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Apply_SingleArg_StillFunctional()
    {
        var effect = new InPlaceEffect();
        var pixels = new uint[] { 0xFF000000 };

        Span<uint> span = pixels.AsSpan();
        effect.Apply(span, 1, 1);

        Assert.Equal(1, effect.ApplyCallCount);
        Assert.Equal(0x00000000u, pixels[0]);
    }
}
