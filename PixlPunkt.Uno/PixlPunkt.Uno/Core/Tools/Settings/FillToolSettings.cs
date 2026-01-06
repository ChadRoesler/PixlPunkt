using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Constants;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Fill (paint bucket) tool.
    /// </summary>
    /// <remarks>
    /// Fill tool does not implement IBrushLikeSettings as it doesn't use brush properties.
    /// It only implements IToolStrokeSync to sync tolerance and contiguous mode.
    /// </remarks>
    public sealed class FillToolSettings : ToolSettingsBase
    {
        private int _tolerance = ToolLimits.MinTolerance;
        private bool _contiguous = true;

        /// <inheritdoc/>
        public override Icon Icon => Icon.PaintBucket;

        /// <inheritdoc/>
        public override string DisplayName => "Fill";

        /// <inheritdoc/>
        public override string Description => "Fill an area with the foreground color";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.G);

        // ====================================================================
        // Fill-Specific Properties
        // ====================================================================

        /// <summary>
        /// Gets the color tolerance for fill matching (0-255).
        /// </summary>
        public int Tolerance => _tolerance;

        /// <summary>
        /// Gets whether fill uses contiguous (flood-fill) mode.
        /// </summary>
        public bool Contiguous => _contiguous;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption("tolerance", "Tolerance", ToolLimits.MinTolerance, ToolLimits.MaxTolerance, _tolerance, v => SetTolerance((int)v), Order: 0, Tooltip: "Color matching tolerance");
            yield return new ToggleOption("contiguous", "Contiguous", _contiguous, SetContiguous, Order: 1, Tooltip: "Fill only connected pixels");
        }

        /// <summary>
        /// Sets the fill tolerance (0-255).
        /// </summary>
        public void SetTolerance(int value)
        {
            value = Math.Clamp(value, ToolLimits.MinTolerance, ToolLimits.MaxTolerance);
            if (_tolerance == value) return;
            _tolerance = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether fill uses contiguous mode.
        /// </summary>
        public void SetContiguous(bool value)
        {
            if (_contiguous == value) return;
            _contiguous = value;
            RaiseChanged();
        }
    }
}
