using FluentIcons.Common;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.UI
{
    /// <summary>
    /// Describes a plugin-provided window that the host will create and display.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plugins cannot create WinUI Window objects directly because the SDK is platform-agnostic.
    /// Instead, plugins describe what should be in the window using this descriptor, and the
    /// host application creates and manages the actual window.
    /// </para>
    /// <para>
    /// The window content is defined using the same <see cref="IToolOption"/> system used for
    /// toolbar options, enabling a consistent declarative UI pattern.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// public PluginWindowDescriptor GetAdvancedSettingsWindow()
    /// {
    ///     return new PluginWindowDescriptor(
    ///         Title: "Advanced Settings",
    ///         GetContent: () => new IToolOption[]
    ///         {
    ///             new SliderOption("threshold", "Threshold", 0, 100, _threshold, v => _threshold = (int)v),
    ///             new ToggleOption("preview", "Live Preview", _preview, v => _preview = v),
    ///             new SeparatorOption(),
    ///             new ButtonOption("apply", "Apply", Icon.Checkmark, () => ApplySettings())
    ///         },
    ///         Width: 400,
    ///         Height: 300
    ///     );
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="Title">The window title.</param>
    /// <param name="GetContent">Factory function that returns the window content as tool options.</param>
    /// <param name="Width">Preferred window width in logical pixels. Default is 400.</param>
    /// <param name="Height">Preferred window height in logical pixels. Default is 300.</param>
    /// <param name="MinWidth">Minimum window width. Default is 300.</param>
    /// <param name="MinHeight">Minimum window height. Default is 200.</param>
    /// <param name="Resizable">Whether the window can be resized. Default is false.</param>
    /// <param name="OnOpening">Called when the window is about to open. Use for initialization.</param>
    /// <param name="OnClosing">Called when the window is about to close. Return false to cancel.</param>
    /// <param name="OnClosed">Called after the window has closed.</param>
    public sealed record PluginWindowDescriptor(
        string Title,
        Func<IEnumerable<IToolOption>> GetContent,
        double Width = 400,
        double Height = 300,
        double MinWidth = 300,
        double MinHeight = 200,
        bool Resizable = false,
        Action? OnOpening = null,
        Func<bool>? OnClosing = null,
        Action? OnClosed = null
    );

    /// <summary>
    /// Layout direction for window content sections.
    /// </summary>
    public enum WindowLayoutDirection
    {
        /// <summary>Options arranged vertically (default for dialogs).</summary>
        Vertical,

        /// <summary>Options arranged horizontally.</summary>
        Horizontal
    }

    /// <summary>
    /// Describes a section of content within a plugin window.
    /// </summary>
    /// <remarks>
    /// Use this to organize options into logical groups with headers and separators.
    /// </remarks>
    /// <param name="Header">Optional section header text.</param>
    /// <param name="Options">The options in this section.</param>
    /// <param name="Layout">Layout direction for the options. Default is vertical.</param>
    /// <param name="Collapsed">Whether the section starts collapsed. Default is false.</param>
    public sealed record WindowSection(
        string? Header,
        IEnumerable<IToolOption> Options,
        WindowLayoutDirection Layout = WindowLayoutDirection.Vertical,
        bool Collapsed = false
    );

    /// <summary>
    /// Tool option that opens a plugin-defined window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This option type renders as a button in the toolbar. When clicked, the host application
    /// creates and displays a window based on the <see cref="PluginWindowDescriptor"/>.
    /// </para>
    /// <para>
    /// Unlike Core's <c>CustomWindowOption</c> which requires a WinUI Window factory, this
    /// SDK version uses a descriptor pattern that allows the host to create the window.
    /// </para>
    /// </remarks>
    /// <param name="Id">Unique identifier for this option.</param>
    /// <param name="Label">Button text label.</param>
    /// <param name="Icon">Optional FluentUI icon for the button.</param>
    /// <param name="GetWindowDescriptor">Factory function that returns the window descriptor.</param>
    /// <param name="Order">Sort order within the toolbar.</param>
    /// <param name="Group">Optional grouping for organizing options.</param>
    /// <param name="Tooltip">Optional tooltip text.</param>
    public sealed record PluginWindowOption(
        string Id,
        string Label,
        Icon? Icon,
        Func<PluginWindowDescriptor> GetWindowDescriptor,
        int Order = 0,
        string Group = "General",
        string? Tooltip = null
    ) : IToolOption;
}
