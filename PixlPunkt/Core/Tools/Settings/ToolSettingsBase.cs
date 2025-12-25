using System;
using System.Collections.Generic;
using FluentIcons.Common;
using SdkVirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Base class for tool-specific settings with change notification and dynamic UI generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ToolSettingsBase"/> provides the foundation for all tool settings classes with:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Identity</strong>: Icon, display name, description, and keyboard shortcut</item>
    /// <item><strong>Change Notification</strong>: <see cref="Changed"/> event for settings updates</item>
    /// <item><strong>Dynamic UI</strong>: <see cref="GetOptions"/> for toolbar control generation</item>
    /// </list>
    /// <para>
    /// Keyboard shortcuts use SDK's <see cref="KeyBinding"/> type which supports matching against
    /// both SDK <see cref="SdkVirtualKey"/> and integer key codes (for platform interop).
    /// </para>
    /// </remarks>
    /// <seealso cref="IToolOption"/>
    /// <seealso cref="IStrokeSettings"/>
    /// <seealso cref="IOpacitySettings"/>
    /// <seealso cref="IDensitySettings"/>
    public abstract class ToolSettingsBase
    {
        /// <summary>
        /// Gets the icon representing this tool in the UI.
        /// </summary>
        /// <value>
        /// A <see cref="FluentIcons.Common.Icon"/> value. Default is <see cref="Icon.Apps"/>.
        /// </value>
        /// <remarks>
        /// Override in derived classes to provide a tool-specific icon that appears
        /// in the tool rail and toolbar.
        /// </remarks>
        public virtual Icon Icon => Icon.Apps;

        /// <summary>
        /// Gets the display name for this tool.
        /// </summary>
        /// <value>
        /// A human-readable name shown in tooltips and labels. Default is "Tool".
        /// </value>
        public virtual string DisplayName => "Tool";

        /// <summary>
        /// Gets a brief description of what this tool does.
        /// </summary>
        /// <value>
        /// A short description for tooltips and help text. Default is empty string.
        /// </value>
        public virtual string Description => string.Empty;

        /// <summary>
        /// Gets the keyboard shortcut to activate this tool.
        /// </summary>
        /// <value>
        /// A <see cref="KeyBinding"/> for the shortcut, or <c>null</c> if no shortcut is assigned.
        /// Use SDK's <see cref="SdkVirtualKey"/> enum for the key parameter.
        /// </value>
        public virtual KeyBinding? Shortcut => null;

        /// <summary>
        /// Gets a formatted tooltip string including name, shortcut, and description.
        /// </summary>
        /// <value>
        /// A multi-line string suitable for tooltip display, formatted as:
        /// <c>"Tool Name (Shortcut)\nDescription"</c>
        /// </value>
        public string TooltipText
        {
            get
            {
                var name = DisplayName;
                if (Shortcut is not null)
                    name += $" ({Shortcut})";
                if (!string.IsNullOrEmpty(Description))
                    name += $"\n{Description}";
                return name;
            }
        }

        /// <summary>
        /// Occurs when any setting in this tool configuration changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Subscribe to this event to respond to settings changes (e.g., update brush preview,
        /// refresh UI, persist settings).
        /// </para>
        /// <para>
        /// Derived classes should call <see cref="RaiseChanged"/> in property setters.
        /// </para>
        /// </remarks>
        public event Action? Changed;

        /// <summary>
        /// Raises the <see cref="Changed"/> event to notify listeners of a setting change.
        /// </summary>
        /// <remarks>
        /// Call this method in property setters after updating the backing field:
        /// <code>
        /// private int _size = 8;
        /// public int Size
        /// {
        ///     get => _size;
        ///     set { _size = value; RaiseChanged(); }
        /// }
        /// </code>
        /// </remarks>
        protected void RaiseChanged() => Changed?.Invoke();

        /// <summary>
        /// Returns the list of options to display in the toolbar for this tool.
        /// </summary>
        /// <returns>
        /// An enumerable of <see cref="IToolOption"/> descriptors that the
        /// <see cref="UI.Tools.ToolOptionFactory"/> converts to WinUI controls.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Override in derived classes to expose tool settings to the dynamic toolbar.
        /// The options are ordered by their <see cref="IToolOption.Order"/> property.
        /// </para>
        /// <para>
        /// <strong>Example:</strong>
        /// </para>
        /// <code>
        /// public override IEnumerable&lt;IToolOption&gt; GetOptions()
        /// {
        ///     yield return new SliderOption("size", "Size", 1, 128, Size,
        ///         v => Size = (int)v, Order: 0, Tooltip: "Brush size in pixels");
        ///     yield return new ShapeOption("shape", "Shape", Shape,
        ///         v => Shape = v, Order: 1);
        /// }
        /// </code>
        /// </remarks>
        public virtual IEnumerable<IToolOption> GetOptions()
        {
            yield break; // Default: no options
        }
    }
}
