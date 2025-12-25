using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.PluginSdk.Effects.Settings
{
    /// <summary>
    /// Base class for effect-specific settings that provides option discovery for dynamic UI generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Effect settings classes define the configurable parameters for layer effects.
    /// Each effect type has a corresponding settings class that exposes its parameters
    /// through the <see cref="GetOptions"/> method for dynamic UI generation.
    /// </para>
    /// <para>
    /// Unlike tool settings (which use a separate settings object), effect settings are
    /// typically bound directly to the effect instance. The settings class holds a reference
    /// to the effect and its <see cref="GetOptions"/> method returns <see cref="IToolOption"/>
    /// descriptors that read from and write to the effect's properties.
    /// </para>
    /// <para>
    /// <strong>Implementation Pattern:</strong>
    /// </para>
    /// <code>
    /// public sealed class MyEffectSettings : EffectSettingsBase
    /// {
    ///     private readonly MyEffect _effect;
    /// 
    ///     public MyEffectSettings(MyEffect effect) =&gt; _effect = effect;
    /// 
    ///     public override string DisplayName =&gt; "My Effect";
    ///     public override string Description =&gt; "Does something cool.";
    /// 
    ///     public override IEnumerable&lt;IToolOption&gt; GetOptions()
    ///     {
    ///         yield return new SliderOption(
    ///             "amount", "Amount", 0, 100, _effect.Amount,
    ///             v =&gt; _effect.Amount = (int)v,
    ///             Order: 0, Tooltip: "Effect intensity");
    ///     }
    /// }
    /// </code>
    /// </remarks>
    /// <seealso cref="IToolOption"/>
    public abstract class EffectSettingsBase
    {
        /// <summary>
        /// Gets the display name for this effect shown in UI.
        /// </summary>
        /// <value>
        /// A human-readable name for the effect (e.g., "Drop Shadow", "Color Adjust").
        /// </value>
        public abstract string DisplayName { get; }

        /// <summary>
        /// Gets a brief description of what this effect does.
        /// </summary>
        /// <value>
        /// A short description for tooltips or help text.
        /// Default is an empty string if not overridden.
        /// </value>
        public virtual string Description => string.Empty;

        /// <summary>
        /// Returns the list of options to display in the settings panel for this effect.
        /// </summary>
        /// <returns>
        /// An enumerable of <see cref="IToolOption"/> descriptors that the UI uses to
        /// generate controls (sliders, checkboxes, color pickers, etc.) for configuring
        /// the effect's parameters.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Options are typically created with callbacks that directly modify the effect's
        /// properties. The <see cref="IToolOption.Order"/> property determines the display
        /// order in the UI.
        /// </para>
        /// <para>
        /// Common option types include:
        /// </para>
        /// <list type="bullet">
        /// <item><see cref="SliderOption"/> - Numeric values with min/max range</item>
        /// <item><see cref="ToggleOption"/> - Boolean on/off settings</item>
        /// <item><see cref="ColorOption"/> - Color picker with swatch</item>
        /// <item><see cref="DropdownOption"/> - Selection from a list</item>
        /// <item><see cref="PaletteOption"/> - Color palette editor</item>
        /// </list>
        /// </remarks>
        public abstract IEnumerable<IToolOption> GetOptions();
    }
}
