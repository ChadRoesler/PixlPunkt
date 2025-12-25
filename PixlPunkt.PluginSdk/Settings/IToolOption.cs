using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.PluginSdk.Settings
{
    /// <summary>
    /// Base interface for all tool option descriptors used in dynamic UI generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IToolOption"/> provides a declarative way to describe tool settings
    /// that can be rendered as UI controls. The host application converts these 
    /// descriptors into WinUI controls at runtime.
    /// </para>
    /// <para>
    /// <strong>Available Option Types:</strong>
    /// </para>
    /// <list type="table">
    /// <listheader>
    /// <term>Option Type</term>
    /// <description>Rendered Control</description>
    /// </listheader>
    /// <item><term><see cref="Options.SliderOption"/></term><description>Slider + NumberBox</description></item>
    /// <item><term><see cref="Options.ToggleOption"/></term><description>CheckBox</description></item>
    /// <item><term><see cref="Options.ShapeOption"/></term><description>Shape toggle buttons</description></item>
    /// <item><term><see cref="Options.DropdownOption"/></term><description>ComboBox</description></item>
    /// <item><term><see cref="Options.ColorOption"/></term><description>Color swatch button</description></item>
    /// <item><term><see cref="Options.PaletteOption"/></term><description>Color palette with actions</description></item>
    /// <item><term><see cref="Options.ButtonOption"/></term><description>Button with label</description></item>
    /// <item><term><see cref="Options.SeparatorOption"/></term><description>Visual divider</description></item>
    /// <item><term><see cref="Options.LabelOption"/></term><description>Read-only text</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="ToolSettingsBase.GetOptions"/>
    public interface IToolOption
    {
        /// <summary>
        /// Gets the unique identifier for this option within its parent settings.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the display label shown in the UI.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// Gets the sort order within the toolbar.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets the optional grouping for organizing options.
        /// </summary>
        string Group { get; }

        /// <summary>
        /// Gets the optional tooltip text.
        /// </summary>
        string? Tooltip { get; }
    }
}
