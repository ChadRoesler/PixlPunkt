namespace PixlPunkt.Tests;

using FluentAssertions;
using PixlPunkt.UI.CanvasHost.Selection;
using Windows.Graphics;

/// <summary>
/// The frame the scale handles are drawn on, when the selection is not floating.
/// </summary>
/// <remarks>
/// The handles are positioned from the original size times the scale, not from the region
/// rectangle. So a stale scale left behind by an earlier transform draws handles that do not match
/// the selection, which is what made undo look as though it had only half worked.
/// </remarks>
[TestFixture]
public class SelectionFrameTests
{
    private static SelectionSubsystem WithRect(int x, int y, int w, int h)
    {
        var state = new SelectionSubsystem
        {
            Rect = new RectInt32 { X = x, Y = y, Width = w, Height = h },
        };
        state.ResetFrameToRect();
        return state;
    }

    [Test]
    public void AFreshFrameMatchesTheRegion()
    {
        var state = WithRect(4, 6, 20, 10);

        state.OrigW.Should().Be(20);
        state.OrigH.Should().Be(10);
        state.ScaleX.Should().Be(1.0);
        state.ScaleY.Should().Be(1.0);
    }

    [Test]
    public void TheFrameIsCentredOnTheRegion()
    {
        var state = WithRect(10, 20, 30, 40);

        state.OrigCenterX.Should().Be(10 + 15);
        state.OrigCenterY.Should().Be(20 + 20);
    }

    [Test]
    public void AScaleLeftBehindIsCleared()
    {
        // The reported bug: scale a selection, then make a new one and undo. Without this the
        // handles keep standing out at the old scaled size around the new region.
        var state = WithRect(0, 0, 10, 10);
        state.ScaleX = 2.5;
        state.ScaleY = 2.5;

        state.Rect = new RectInt32 { X = 0, Y = 0, Width = 8, Height = 8 };
        state.ResetFrameToRect();

        state.ScaleX.Should().Be(1.0);
        state.ScaleY.Should().Be(1.0);
        state.OrigW.Should().Be(8);
        state.OrigH.Should().Be(8);
    }

    [Test]
    public void ARotationLeftBehindIsCleared()
    {
        var state = WithRect(0, 0, 10, 10);
        state.AngleDeg = 30;
        state.CumulativeAngleDeg = 90;

        state.ResetFrameToRect();

        state.AngleDeg.Should().Be(0);
        state.CumulativeAngleDeg.Should().Be(0);
    }

    [Test]
    public void AMovedPivotIsCleared()
    {
        var state = WithRect(0, 0, 10, 10);
        state.PivotCustom = true;
        state.PivotOffsetX = 3;
        state.PivotOffsetY = -2;

        state.ResetFrameToRect();

        state.PivotCustom.Should().BeFalse();
        state.PivotOffsetX.Should().Be(0);
        state.PivotOffsetY.Should().Be(0);
    }

    [Test]
    public void TheFrameFollowsTheRegionItWasResetAgainst()
    {
        // The ordering fault: the frame is derived from Rect, so Rect has to be current first.
        var state = WithRect(0, 0, 10, 10);

        state.Rect = new RectInt32 { X = 50, Y = 60, Width = 4, Height = 6 };
        state.ResetFrameToRect();

        state.OrigW.Should().Be(4);
        state.OrigH.Should().Be(6);
        state.OrigCenterX.Should().Be(52);
        state.OrigCenterY.Should().Be(63);
    }

    [Test]
    public void AssigningNullWhenAlreadyUnliftedDoesNotResetAnything()
    {
        // This is the trap the bug came from. The reset hangs off the transition to null, and
        // assigning null when it is already null returns early, so a selection that was never
        // lifted keeps whatever scale an earlier transform left on it. Undoing a marquee takes
        // exactly that path, which is why the host has to reset the frame itself rather than
        // relying on this assignment to do it.
        var state = WithRect(0, 0, 10, 10);
        state.ScaleX = 3.0;

        state.Lifted = null;

        state.ScaleX.Should().Be(3.0, "the assignment short circuits, so nothing was cleared");

        state.ResetFrameToRect();
        state.ScaleX.Should().Be(1.0, "asking directly is what actually clears it");
    }
}
