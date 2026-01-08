using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Enums;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Tile Modifier tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Tile Modifier edits tile content in place with offset, rotate, and scale operations.
    /// </para>
    /// <para><strong>Actions:</strong></para>
    /// <list type="bullet">
    /// <item>LMB drag: Offset tile content (tessellating wrap)</item>
    /// <item>Ctrl + LMB drag: Rotate tile content within bounds</item>
    /// <item>Shift + LMB drag: Scale tile content in drag direction</item>
    /// <item>RMB: Sample tile and mapping (tile dropper)</item>
    /// </list>
    /// <para>
    /// On release, content outside tile bounds is clipped. Only pixels within
    /// the tile boundary are preserved.
    /// </para>
    /// </remarks>
    public sealed class TileModifierToolSettings : ToolSettingsBase
    {
        private bool _wrapContent = true;
        private bool _constrainRotation = false;
        private RotationMode _rotationMode = RotationMode.NearestNeighbor;
        private ScaleMode _scaleMode = ScaleMode.NearestNeighbor;

        private static readonly string[] RotationModeNames = Enum.GetNames<RotationMode>();
        private static readonly string[] ScaleModeNames = Enum.GetNames<ScaleMode>();

        /// <inheritdoc/>
        public override Icon Icon => Icon.TableEdit;

        /// <inheritdoc/>
        public override string DisplayName => "Tile Modifier";

        /// <inheritdoc/>
        public override string Description => "Offset, rotate, and scale tile content";

        /// <summary>
        /// Gets the default keyboard shortcut for the Tile Modifier tool (Ctrl+T).
        /// </summary>
        public override KeyBinding? Shortcut => new(VirtualKey.T, Ctrl: true);

        //////////////////////////////////////////////////////////////////
        // Tile Modifier Properties
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets whether offset operations wrap content around tile edges (tessellation).
        /// </summary>
        public bool WrapContent => _wrapContent;

        /// <summary>
        /// Gets whether rotation is constrained to 15-degree increments.
        /// </summary>
        public bool ConstrainRotation => _constrainRotation;

        /// <summary>
        /// Gets the rotation interpolation mode.
        /// </summary>
        public RotationMode RotationMode => _rotationMode;

        /// <summary>
        /// Gets the scale interpolation mode.
        /// </summary>
        public ScaleMode ScaleMode => _scaleMode;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new ToggleOption(
                "wrapContent",
                "Wrap Content",
                _wrapContent,
                SetWrapContent,
                Order: 0,
                Tooltip: "Wrap content around tile edges during offset"
            );

            yield return new ToggleOption(
                "constrainRotation",
                "Constrain Rotation",
                _constrainRotation,
                SetConstrainRotation,
                Order: 1,
                Tooltip: "Snap rotation to 15° increments"
            );

            yield return new DropdownOption(
                "rotationMode",
                "Rotation Quality",
                RotationModeNames,
                (int)_rotationMode,
                idx => SetRotationMode((RotationMode)idx),
                Order: 2,
                Tooltip: "Interpolation mode for rotation"
            );

            yield return new DropdownOption(
                "scaleMode",
                "Scale Quality",
                ScaleModeNames,
                (int)_scaleMode,
                idx => SetScaleMode((ScaleMode)idx),
                Order: 3,
                Tooltip: "Interpolation mode for scaling"
            );
        }

        /// <summary>
        /// Sets whether offset operations wrap content.
        /// </summary>
        public void SetWrapContent(bool value)
        {
            if (_wrapContent == value) return;
            _wrapContent = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether rotation is constrained to 15-degree increments.
        /// </summary>
        public void SetConstrainRotation(bool value)
        {
            if (_constrainRotation == value) return;
            _constrainRotation = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the rotation interpolation mode.
        /// </summary>
        public void SetRotationMode(RotationMode value)
        {
            if (_rotationMode == value) return;
            _rotationMode = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the scale interpolation mode.
        /// </summary>
        public void SetScaleMode(ScaleMode value)
        {
            if (_scaleMode == value) return;
            _scaleMode = value;
            RaiseChanged();
        }
    }
}
