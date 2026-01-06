using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Constants;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Wand (magic wand) selection tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The wand tool selects pixels by color similarity. Settings control:
    /// - Tolerance: How much color deviation is allowed (0 = exact, 255 = all colors)
    /// - Contiguous: Whether to flood-fill (connected only) or select all matching globally
    /// - UseAlpha: Whether to include alpha channel in color comparison
    /// - EightWay: Whether to use 8-way connectivity (diagonals) or 4-way (orthogonal only)
    /// </para>
    /// </remarks>
    public sealed class WandToolSettings : ToolSettingsBase
    {
        private int _tolerance = ToolLimits.MinTolerance;
        private bool _contiguous = true;
        private bool _useAlpha = true;
        private bool _eightWay = true;

        /// <inheritdoc/>
        public override Icon Icon => Icon.Wand;

        /// <inheritdoc/>
        public override string DisplayName => "Magic Wand";

        /// <inheritdoc/>
        public override string Description => "Select pixels by color similarity";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.W);

        /// <summary>
        /// Gets or sets the shared selection settings (injected by ToolState).
        /// </summary>
        public SelectionToolSettings? SelectionSettings { get; set; }

        /// <summary>
        /// Gets the color tolerance for wand selection (0-255).
        /// </summary>
        /// <remarks>
        /// 0 = exact color match only, 255 = matches all colors.
        /// Higher values select a wider range of similar colors.
        /// </remarks>
        public int Tolerance => _tolerance;

        /// <summary>
        /// Gets whether wand uses contiguous (flood-fill) mode.
        /// </summary>
        /// <remarks>
        /// When true, only connected pixels are selected (flood-fill).
        /// When false, all matching pixels in the entire image are selected.
        /// </remarks>
        public bool Contiguous => _contiguous;

        /// <summary>
        /// Gets whether alpha channel is included in color comparison.
        /// </summary>
        /// <remarks>
        /// When true, transparency differences affect color matching.
        /// When false, only RGB values are compared (ignoring alpha).
        /// </remarks>
        public bool UseAlpha => _useAlpha;

        /// <summary>
        /// Gets whether 8-way connectivity is used for contiguous selection.
        /// </summary>
        /// <remarks>
        /// When true, diagonal neighbors are considered connected (8-way).
        /// When false, only orthogonal neighbors count (4-way: up/down/left/right).
        /// Only applies when Contiguous is true.
        /// </remarks>
        public bool EightWay => _eightWay;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Tool-specific options
            yield return new SliderOption("tolerance", "Tolerance", ToolLimits.MinTolerance, ToolLimits.MaxTolerance, _tolerance, v => SetTolerance((int)v), Order: 0, Tooltip: "Color matching tolerance (0 = exact match)");
            yield return new ToggleOption("contiguous", "Contiguous", _contiguous, SetContiguous, Order: 1, Tooltip: "Select only connected pixels (flood-fill)");
            yield return new ToggleOption("useAlpha", "Use Alpha", _useAlpha, SetUseAlpha, Order: 2, Tooltip: "Include transparency in color comparison");
            yield return new ToggleOption("eightWay", "Diagonal", _eightWay, SetEightWay, Order: 3, Tooltip: "Include diagonal neighbors (8-way connectivity)");

            // Shared selection options (when selection is active)
            if (SelectionSettings is { Active: true })
            {
                yield return new SeparatorOption(Order: 100);
                foreach (var opt in SelectionSettings.GetTransformOptions(baseOrder: 101))
                    yield return opt;
            }
        }

        /// <summary>
        /// Sets the wand tolerance (0-255).
        /// </summary>
        public void SetTolerance(int value)
        {
            value = Math.Clamp(value, ToolLimits.MinTolerance, ToolLimits.MaxTolerance);
            if (_tolerance == value) return;
            _tolerance = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether wand uses contiguous (flood-fill) mode.
        /// </summary>
        public void SetContiguous(bool value)
        {
            if (_contiguous == value) return;
            _contiguous = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether alpha channel is included in color comparison.
        /// </summary>
        public void SetUseAlpha(bool value)
        {
            if (_useAlpha == value) return;
            _useAlpha = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether 8-way connectivity is used.
        /// </summary>
        public void SetEightWay(bool value)
        {
            if (_eightWay == value) return;
            _eightWay = value;
            RaiseChanged();
        }
    }
}
