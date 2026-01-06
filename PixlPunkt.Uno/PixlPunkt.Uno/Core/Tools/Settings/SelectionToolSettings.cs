using System;
using System.Collections.Generic;
using System.Linq;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Enums;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings shared by all selection tools (SelectRect, Wand, Lasso, PaintSelect).
    /// </summary>
    /// <remarks>
    /// This class represents shared selection state (scale, rotation, etc.) and is not
    /// directly selectable as a tool. Individual selection tools reference this for
    /// transform options. No shortcut is assigned since this is not a standalone tool.
    /// </remarks>
    public sealed class SelectionToolSettings : ToolSettingsBase
    {
        private static readonly IReadOnlyList<string> ScaleModeNames = Enum.GetNames<ScaleMode>().ToArray();
        private static readonly IReadOnlyList<string> RotationModeNames = Enum.GetNames<RotationMode>().ToArray();

        private double _scalePercentX = 100.0;
        private double _scalePercentY = 100.0;
        private bool _scaleLink = false;
        private ScaleMode _scaleMode = ScaleMode.NearestNeighbor;
        private double _rotationAngleDeg = 0.0;
        private RotationMode _rotationMode = RotationMode.NearestNeighbor;
        private bool _active = false;
        private bool _floating = false;
        private bool _useGlobalAxis = false;

        /// <inheritdoc/>
        public override Icon Icon => Icon.SelectObject;

        /// <inheritdoc/>
        public override string DisplayName => "Selection";

        /// <inheritdoc/>
        public override string Description => "Transform the active selection";

        // No shortcut - this is shared state, not a selectable tool

        /// <summary>
        /// Gets the selection scale X percentage.
        /// </summary>
        public double ScalePercentX => _scalePercentX;

        /// <summary>
        /// Gets the selection scale Y percentage.
        /// </summary>
        public double ScalePercentY => _scalePercentY;

        /// <summary>
        /// Gets whether selection scale X and Y are linked.
        /// </summary>
        public bool ScaleLink => _scaleLink;

        /// <summary>
        /// Gets the scale interpolation mode.
        /// </summary>
        public ScaleMode ScaleMode => _scaleMode;

        /// <summary>
        /// Gets the rotation angle in degrees.
        /// </summary>
        public double RotationAngleDeg => _rotationAngleDeg;

        /// <summary>
        /// Gets the rotation algorithm mode.
        /// </summary>
        public RotationMode RotationMode => _rotationMode;

        /// <summary>
        /// Gets whether a selection is active.
        /// </summary>
        public bool Active => _active;

        /// <summary>
        /// Gets whether a selection is floating (detached from layer).
        /// </summary>
        public bool Floating => _floating;

        /// <summary>
        /// Gets whether flip operations use global axis (true) or local/rotated axis (false).
        /// </summary>
        public bool UseGlobalAxis => _useGlobalAxis;

        /// <summary>
        /// Occurs when selection commit is requested.
        /// </summary>
        public event Action? CommitRequested;

        /// <summary>
        /// Occurs when selection cancel is requested.
        /// </summary>
        public event Action? CancelRequested;

        /// <summary>
        /// Occurs when horizontal flip is requested.
        /// </summary>
        public event Action<bool>? FlipHorizontalRequested;

        /// <summary>
        /// Occurs when vertical flip is requested.
        /// </summary>
        public event Action<bool>? FlipVerticalRequested;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Only show transform options when a selection is active
            if (!_active)
            {
                yield return new LabelOption("noSelection", "Info", "Make a selection to see transform options", Order: 0);
                yield break;
            }

            foreach (var opt in GetTransformOptions(baseOrder: 0))
                yield return opt;
        }

        /// <summary>
        /// Returns the transform options with a specified base order offset.
        /// Used by selection tools to append these options after their own.
        /// Layout: FlipH FlipV GlobalToggle | X: [link] Y: ScaleMode | Rotation: RotMode | Commit Cancel
        /// </summary>
        public IEnumerable<IToolOption> GetTransformOptions(int baseOrder)
        {
            // Flip buttons and global axis toggle
            yield return new IconButtonOption("flipH", "Flip Horizontal", Icon.FlipHorizontal, RequestFlipHorizontal, Order: baseOrder + 0, Tooltip: "Flip horizontally");
            yield return new IconButtonOption("flipV", "Flip Vertical", Icon.FlipVertical, RequestFlipVertical, Order: baseOrder + 1, Tooltip: "Flip vertically");
            yield return new IconToggleOption("globalAxis", "Global Axis", Icon.Globe, Icon.GlobeOff, _useGlobalAxis, SetUseGlobalAxis, Order: baseOrder + 2, TooltipOn: "Using global axis (canvas X/Y)", TooltipOff: "Using local axis (object X/Y)");

            yield return new SeparatorOption(Order: baseOrder + 3);

            // Scale X, Link toggle, Scale Y, Scale Mode
            // When linked, changing X updates Y and vice versa
            yield return new NumberBoxOption("scaleX", "X:", 1, 1600, _scalePercentX, v => SetScaleX(v), Order: baseOrder + 4, Suffix: "%", Width: 70);
            yield return new IconToggleOption("scaleLink", "Link Scale", Icon.LinkMultiple, Icon.LinkMultiple, _scaleLink, v => SetScaleLink(v), Order: baseOrder + 5, TooltipOn: "Scale linked (aspect ratio locked)", TooltipOff: "Scale independent");
            yield return new NumberBoxOption("scaleY", "Y:", 1, 1600, _scalePercentY, v => SetScaleY(v), Order: baseOrder + 6, Suffix: "%", Width: 70);
            yield return new DropdownOption("scaleMode", "", ScaleModeNames, (int)_scaleMode, i => SetScaleMode((ScaleMode)i), Order: baseOrder + 7, ShowLabel: false);

            yield return new SeparatorOption(Order: baseOrder + 8);

            // Rotation angle and mode
            yield return new NumberBoxOption("rotation", "Rotation:", -180, 180, _rotationAngleDeg, SetRotationAngle, Order: baseOrder + 9, Suffix: "�", Width: 70);
            yield return new DropdownOption("rotationMode", "", RotationModeNames, (int)_rotationMode, i => SetRotationMode((RotationMode)i), Order: baseOrder + 10, ShowLabel: false);

            yield return new SeparatorOption(Order: baseOrder + 11);

            // Commit and Cancel
            yield return new IconButtonOption("commit", "Commit", Icon.Checkmark, RequestCommit, Order: baseOrder + 12, Tooltip: "Apply selection");
            yield return new IconButtonOption("cancel", "Cancel", Icon.Dismiss, RequestCancel, Order: baseOrder + 13, Tooltip: "Cancel selection");
        }

        /// <summary>
        /// Sets the X scale percentage. If linked, also updates Y.
        /// </summary>
        public void SetScaleX(double px)
        {
            px = Math.Clamp(px, 1.0, 1600.0);

            if (_scaleLink)
            {
                // When linked, update both to the same value
                if (Math.Abs(px - _scalePercentX) < 1e-6 && Math.Abs(px - _scalePercentY) < 1e-6) return;
                _scalePercentX = px;
                _scalePercentY = px;
            }
            else
            {
                if (Math.Abs(px - _scalePercentX) < 1e-6) return;
                _scalePercentX = px;
            }
            RaiseChanged();
        }

        /// <summary>
        /// Sets the Y scale percentage. If linked, also updates X.
        /// </summary>
        public void SetScaleY(double py)
        {
            py = Math.Clamp(py, 1.0, 1600.0);

            if (_scaleLink)
            {
                // When linked, update both to the same value
                if (Math.Abs(py - _scalePercentX) < 1e-6 && Math.Abs(py - _scalePercentY) < 1e-6) return;
                _scalePercentX = py;
                _scalePercentY = py;
            }
            else
            {
                if (Math.Abs(py - _scalePercentY) < 1e-6) return;
                _scalePercentY = py;
            }
            RaiseChanged();
        }

        /// <summary>
        /// Sets the scale link state.
        /// When enabling link, syncs both to the higher of the two values.
        /// </summary>
        public void SetScaleLink(bool link)
        {
            if (_scaleLink == link) return;
            _scaleLink = link;

            // When enabling link, sync both to the higher value
            if (link && Math.Abs(_scalePercentX - _scalePercentY) > 1e-6)
            {
                double higher = Math.Max(_scalePercentX, _scalePercentY);
                _scalePercentX = higher;
                _scalePercentY = higher;
            }
            RaiseChanged();
        }

        /// <summary>
        /// Sets the selection scale percentages and link state.
        /// Called from canvas when handles change scale.
        /// </summary>
        public void SetScale(double px, double py, bool link)
        {
            px = Math.Clamp(px, 1.0, 1600.0);
            py = Math.Clamp(py, 1.0, 1600.0);

            if (Math.Abs(px - _scalePercentX) < 1e-6 &&
                Math.Abs(py - _scalePercentY) < 1e-6 &&
                link == _scaleLink) return;

            _scalePercentX = px;
            _scalePercentY = py;
            _scaleLink = link;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the scale interpolation mode.
        /// </summary>
        public void SetScaleMode(ScaleMode mode)
        {
            if (_scaleMode == mode) return;
            _scaleMode = mode;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the rotation angle in degrees.
        /// </summary>
        public void SetRotationAngle(double degrees)
        {
            if (Math.Abs(degrees - _rotationAngleDeg) < 1e-6) return;
            _rotationAngleDeg = degrees;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the rotation mode.
        /// </summary>
        public void SetRotationMode(RotationMode mode)
        {
            if (_rotationMode == mode) return;
            _rotationMode = mode;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the selection presence state.
        /// </summary>
        public void SetPresence(bool active, bool floating)
        {
            if (_active == active && _floating == floating) return;
            _active = active;
            _floating = floating;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether flip operations use global or local axis.
        /// </summary>
        public void SetUseGlobalAxis(bool useGlobal)
        {
            if (_useGlobalAxis == useGlobal) return;
            _useGlobalAxis = useGlobal;
            RaiseChanged();
        }

        /// <summary>
        /// Requests that the active selection be committed.
        /// </summary>
        public void RequestCommit() => CommitRequested?.Invoke();

        /// <summary>
        /// Requests that the active selection be canceled.
        /// </summary>
        public void RequestCancel() => CancelRequested?.Invoke();

        /// <summary>
        /// Requests a horizontal flip of the selection.
        /// </summary>
        public void RequestFlipHorizontal() => FlipHorizontalRequested?.Invoke(_useGlobalAxis);

        /// <summary>
        /// Requests a vertical flip of the selection.
        /// </summary>
        public void RequestFlipVertical() => FlipVerticalRequested?.Invoke(_useGlobalAxis);
    }
}
