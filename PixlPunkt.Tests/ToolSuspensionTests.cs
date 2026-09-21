namespace PixlPunkt.Tests;

using System.Collections.Generic;
using FluentAssertions;
using PixlPunkt.Core.Tools;

/// <summary>
/// The font metrics tool lives in the glyphs panel rather than the tool rail, so while it is armed
/// the drawing tools have to stand down: the rail shows nothing selected and no tool takes input.
/// These cover that handover in both directions.
/// </summary>
[TestFixture]
public class ToolSuspensionTests
{
    private static ToolState NewToolState() => new(ToolRegistry.Shared);

    [Test]
    public void SuspendingTools_ReportsNoActiveTool()
    {
        var tools = NewToolState();
        tools.SetById(ToolIds.Brush);

        tools.SuspendTools(true);

        tools.ToolsSuspended.Should().BeTrue();
        tools.ActiveToolId.Should().Be(ToolIds.None);
        tools.IsBrush.Should().BeFalse("nothing should answer to a tool test while suspended");
        tools.IsActiveBrushTool.Should().BeFalse();
        tools.IsActiveSelectTool.Should().BeFalse();
    }

    [Test]
    public void TheChosenToolIsRemembered_AndComesBackOnRelease()
    {
        var tools = NewToolState();
        tools.SetById(ToolIds.Eraser);

        tools.SuspendTools(true);
        tools.SuspendTools(false);

        tools.CurrentToolId.Should().Be(ToolIds.Eraser);
        tools.ActiveToolId.Should().Be(ToolIds.Eraser, "suspending never changed which tool was chosen");
    }

    [Test]
    public void NoToolIsRegistered_SoTheRailFindsNothingToCheck()
    {
        // The rail checks the button whose id matches the active tool. "None" must match no button,
        // which is exactly what leaving it unregistered guarantees.
        ToolRegistry.Shared.IsRegistered(ToolIds.None).Should().BeFalse();
    }

    [Test]
    public void PickingAToolInTheRail_TakesTheCanvasBack()
    {
        var tools = NewToolState();
        tools.SetById(ToolIds.Brush);
        tools.SuspendTools(true);

        tools.SetById(ToolIds.Fill);

        tools.ToolsSuspended.Should().BeFalse("reaching for a tool means letting go of the metrics tool");
        tools.ActiveToolId.Should().Be(ToolIds.Fill);
    }

    [Test]
    public void ReChoosingTheSameTool_AlsoTakesTheCanvasBack()
    {
        // Clicking the already-chosen tool is how someone escapes the metrics tool without
        // switching away from the brush they were using, so it has to count.
        var tools = NewToolState();
        tools.SetById(ToolIds.Brush);
        tools.SuspendTools(true);

        var seen = new List<string>();
        tools.ActiveToolIdChanged += seen.Add;

        tools.SetById(ToolIds.Brush);

        tools.ToolsSuspended.Should().BeFalse();
        tools.ActiveToolId.Should().Be(ToolIds.Brush);
        seen.Should().ContainSingle(id => id == ToolIds.Brush,
            "the rail has to be told, or its buttons stay blank");
    }

    [Test]
    public void SuspendingTwice_RaisesNothingTheSecondTime()
    {
        var tools = NewToolState();
        int raised = 0;
        tools.ActiveToolIdChanged += _ => raised++;

        tools.SuspendTools(true);
        tools.SuspendTools(true);

        raised.Should().Be(1);
    }
}
