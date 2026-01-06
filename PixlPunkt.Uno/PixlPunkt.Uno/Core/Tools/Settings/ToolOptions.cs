using System;
using FluentIcons.Common;
using Microsoft.UI.Xaml;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Custom window option for opening a tool-specific configuration window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Renders as a button that opens a custom WinUI window using <see cref="UI.Helpers.WindowHost"/>.
    /// Use this as an "escape hatch" for complex configuration that can't be expressed as 
    /// simple toolbar controls.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> This option type is Core-only because it requires 
    /// <see cref="Microsoft.UI.Xaml.Window"/> which is not available in the SDK.
    /// Plugin developers should use <see cref="ButtonOption"/> with custom dialog handling instead.
    /// </para>
    /// </remarks>
    /// <param name="Id">Unique identifier for this option.</param>
    /// <param name="Label">Button text label.</param>
    /// <param name="Icon">Optional FluentUI icon for the button.</param>
    /// <param name="CreateWindow">Factory function that creates the window instance.</param>
    /// <param name="Order">Sort order within the toolbar.</param>
    /// <param name="Group">Optional grouping for organizing options.</param>
    /// <param name="Tooltip">Optional tooltip text.</param>
    /// <param name="WindowTitle">Title for the window. If null, uses Label.</param>
    /// <param name="Resizable">Whether the window can be resized. Default is false.</param>
    /// <param name="Minimizable">Whether the window can be minimized. Default is false.</param>
    /// <param name="MinWidth">Minimum window width in logical pixels. Default is 400.</param>
    /// <param name="MinHeight">Minimum window height in logical pixels. Default is 300.</param>
    /// <param name="MaxScreenFraction">Maximum fraction of screen to occupy (0.0-1.0). Default is 0.90.</param>
    public sealed partial record CustomWindowOption(
        string Id,
        string Label,
        Icon? Icon,
        Func<Window> CreateWindow,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null,
        string? WindowTitle = null,
        bool Resizable = false,
        bool Minimizable = false,
        double MinWidth = 400,
        double MinHeight = 300,
        double MaxScreenFraction = 0.90
    ) : IToolOption;
}
