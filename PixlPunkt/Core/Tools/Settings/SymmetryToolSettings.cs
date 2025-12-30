using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Symmetry;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Symmetry utility tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The symmetry tool controls live stroke mirroring during painting operations.
    /// When active, all brush strokes are automatically mirrored across the configured axes.
    /// </para>
    /// <para>
    /// Supports horizontal/vertical axis mirroring, both axes combined, and radial (mandala)
    /// symmetry with configurable segment counts.
    /// </para>
    /// </remarks>
    public sealed class SymmetryToolSettings : ToolSettingsBase
    {
        private readonly SymmetrySettings _settings;

        /// <summary>
        /// Creates a new SymmetryToolSettings instance.
        /// </summary>
        /// <param name="settings">The shared symmetry settings to control.</param>
        public SymmetryToolSettings(SymmetrySettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Forward settings changes to tool settings changed event
            _settings.Changed += () => RaiseChanged();
        }

        /// <inheritdoc/>
        public override Icon Icon => Icon.FlipHorizontal;

        /// <inheritdoc/>
        public override string DisplayName => "Symmetry";

        /// <inheritdoc/>
        public override string Description => "Toggle live symmetry drawing mode";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.Y);

        /// <summary>
        /// Gets the underlying symmetry settings.
        /// </summary>
        public SymmetrySettings Settings => _settings;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Toggle on/off
            yield return new IconToggleOption(
                "enabled",
                "Enabled",
                Icon.FlipHorizontal,
                Icon.DismissCircle,
                _settings.Enabled,
                v => _settings.Enabled = v,
                Order: 0,
                Tooltip: "Toggle symmetry drawing",
                TooltipOn: "Symmetry is ON",
                TooltipOff: "Symmetry is OFF"
            );

            // Mode dropdown - simplified to 5 modes
            var modeNames = new List<string>
            {
                "Horizontal",
                "Vertical",
                "Both Axes",
                "Radial",
                "Kaleidoscope"
            };

            int selectedModeIndex = _settings.Mode switch
            {
                SymmetryMode.Horizontal => 0,
                SymmetryMode.Vertical => 1,
                SymmetryMode.Both => 2,
                SymmetryMode.Radial => 3,
                SymmetryMode.Kaleidoscope => 4,
                _ => 0
            };

            yield return new DropdownOption(
                "mode",
                "Mode",
                modeNames,
                selectedModeIndex,
                index => SetModeFromIndex(index),
                Order: 1,
                Tooltip: "Select symmetry mode"
            );

            // Radial segments slider (only for Radial and Kaleidoscope modes)
            if (_settings.Mode == SymmetryMode.Radial || _settings.Mode == SymmetryMode.Kaleidoscope)
            {
                yield return new SliderOption(
                    "segments",
                    "Segments",
                    2,
                    16,
                    _settings.RadialSegments,
                    v => _settings.RadialSegments = (int)v,
                    Order: 2,
                    Tooltip: "Number of radial segments (2-16)"
                );
            }

            yield return new SeparatorOption(Order: 3);

            // Show axis lines toggle
            yield return new ToggleOption(
                "showAxis",
                "Show Axis",
                _settings.ShowAxisLines,
                v => _settings.ShowAxisLines = v,
                Order: 4,
                Tooltip: "Show symmetry axis lines on canvas (drag to move when Symmetry tool is active)"
            );

            // Center axis button
            yield return new ButtonOption(
                "centerAxis",
                "Center Axis",
                Icon.AlignCenterVertical,
                () => _settings.CenterAxis(),
                Order: 5,
                Tooltip: "Center the symmetry axis on the canvas"
            );
        }

        /// <summary>
        /// Sets the symmetry mode from a dropdown index.
        /// </summary>
        private void SetModeFromIndex(int index)
        {
            var mode = index switch
            {
                0 => SymmetryMode.Horizontal,
                1 => SymmetryMode.Vertical,
                2 => SymmetryMode.Both,
                3 => SymmetryMode.Radial,
                4 => SymmetryMode.Kaleidoscope,
                _ => SymmetryMode.Horizontal
            };

            _settings.Mode = mode;
        }

        /// <summary>
        /// Toggles symmetry on/off.
        /// </summary>
        public void Toggle() => _settings.Toggle();

        /// <summary>
        /// Cycles through symmetry modes.
        /// </summary>
        public void CycleMode() => _settings.CycleMode();
    }
}
