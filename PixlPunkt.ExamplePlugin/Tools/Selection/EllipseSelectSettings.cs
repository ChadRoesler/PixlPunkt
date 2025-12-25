using FluentIcons.Common;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.ExamplePlugin.Tools.Selection
{
    /// <summary>
    /// Settings for the Ellipse selection tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This demonstrates creating custom settings for a selection tool.
    /// Currently minimal, but could be extended with options like:
    /// </para>
    /// <list type="bullet">
    /// <item>Feathering radius for soft edges</item>
    /// <item>Anti-aliasing toggle</item>
    /// <item>Fixed aspect ratio presets</item>
    /// </list>
    /// </remarks>
    public sealed class EllipseSelectSettings : ToolSettingsBase
    {
        private bool _antiAlias = true;
        private int _featherRadius = 0;

        public override Icon Icon => Icon.CircleHint;

        /// <inheritdoc/>
        public override string DisplayName => "Ellipse Select";

        /// <inheritdoc/>
        public override string Description => "Creates elliptical or circular selections.";

        /// <summary>
        /// Gets or sets whether to anti-alias the selection edge.
        /// </summary>
        public bool AntiAlias
        {
            get => _antiAlias;
            set
            {
                if (_antiAlias != value)
                {
                    _antiAlias = value;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the feather radius for soft selection edges (0-50 pixels).
        /// </summary>
        public int FeatherRadius
        {
            get => _featherRadius;
            set
            {
                int clamped = Math.Clamp(value, 0, 50);
                if (_featherRadius != clamped)
                {
                    _featherRadius = clamped;
                    RaiseChanged();
                }
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new ToggleOption(
                "antiAlias",
                "Anti-Alias",
                _antiAlias,
                v => AntiAlias = v,
                Order: 0,
                Tooltip: "Smooth selection edges with anti-aliasing"
            );

            yield return new SliderOption(
                "featherRadius",
                "Feather",
                0, 50, _featherRadius,
                v => FeatherRadius = (int)v,
                Order: 1,
                Step: 1,
                Tooltip: "Soft edge radius in pixels (0 = hard edge)"
            );
        }
    }
}
