using FluentIcons.Common;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.ExamplePlugin.Tools.Utility
{
    /// <summary>
    /// Settings for the Info utility tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This demonstrates creating custom settings for a utility tool.
    /// The Info tool displays hex color values for pixels under the cursor.
    /// </para>
    /// </remarks>
    public sealed class InfoToolSettings : ToolSettingsBase
    {
        public override Icon Icon => Icon.SearchInfo;
        private bool _continuousSample = true;
        private bool _sampleOnHover = true;
        private bool _showHexValues = true;
        private string _hexOutput = "#------";
        private string _positionOutput = "X:-- Y:--";

        /// <inheritdoc/>
        public override string DisplayName => "Info Tool";

        /// <inheritdoc/>
        public override string Description => "Displays pixel color and position information.";

        /// <summary>
        /// Gets or sets whether to continuously sample while dragging.
        /// </summary>
        public bool ContinuousSample
        {
            get => _continuousSample;
            set
            {
                if (_continuousSample != value)
                {
                    _continuousSample = value;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to sample colors on hover (without clicking).
        /// </summary>
        public bool SampleOnHover
        {
            get => _sampleOnHover;
            set
            {
                if (_sampleOnHover != value)
                {
                    _sampleOnHover = value;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to show hex color values in output.
        /// </summary>
        public bool ShowHexValues
        {
            get => _showHexValues;
            set
            {
                if (_showHexValues != value)
                {
                    _showHexValues = value;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current hex output value displayed in the settings panel.
        /// </summary>
        public string HexOutput
        {
            get => _hexOutput;
            set
            {
                if (_hexOutput != value)
                {
                    _hexOutput = value;
                    // No RaiseChanged() - DynamicLabelOption polls the getter directly
                }
            }
        }

        /// <summary>
        /// Gets or sets the current position output value displayed in the settings panel.
        /// </summary>
        public string PositionOutput
        {
            get => _positionOutput;
            set
            {
                if (_positionOutput != value)
                {
                    _positionOutput = value;
                    // No RaiseChanged() - DynamicLabelOption polls the getter directly
                }
            }
        }

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Output display labels at the top - using DynamicLabelOption for live updates
            yield return new DynamicLabelOption(
                "positionLabel",
                "Position",
                () => _positionOutput,
                Order: 0,
                Tooltip: "Current cursor position in document coordinates"
            );

            yield return new DynamicLabelOption(
                "hexLabel",
                "Color",
                () => _hexOutput,
                Order: 1,
                Tooltip: "Hex color value at cursor position (click to copy)",
                MonospacedValue: true
            );

            yield return new SeparatorOption(Order: 2);

            // Settings toggles
            yield return new ToggleOption(
                "sampleOnHover",
                "Sample on Hover",
                _sampleOnHover,
                v => SampleOnHover = v,
                Order: 3,
                Tooltip: "Sample colors when hovering (without clicking)"
            );

            yield return new ToggleOption(
                "continuousSample",
                "Continuous Sample",
                _continuousSample,
                v => ContinuousSample = v,
                Order: 4,
                Tooltip: "Sample colors continuously while dragging"
            );

            yield return new ToggleOption(
                "showHex",
                "Include Alpha",
                _showHexValues,
                v => ShowHexValues = v,
                Order: 5,
                Tooltip: "Include alpha channel in hex output"
            );
        }
    }
}
