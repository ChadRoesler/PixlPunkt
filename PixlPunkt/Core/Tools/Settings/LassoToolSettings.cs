using System;
using System.Collections.Generic;
using FluentIcons.Common;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Settings specific to the Lasso (polygon) selection tool.
    /// </summary>
    public sealed class LassoToolSettings : ToolSettingsBase
    {
        private bool _autoClose = true;
        private double _closeDistance = 10.0;

        /// <inheritdoc/>
        public override Icon Icon => Icon.Lasso;

        /// <inheritdoc/>
        public override string DisplayName => "Lasso";

        /// <inheritdoc/>
        public override string Description => "Draw a freeform selection";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.L);

        /// <summary>
        /// Gets or sets the shared selection settings (injected by ToolState).
        /// </summary>
        public SelectionToolSettings? SelectionSettings { get; set; }

        /// <summary>
        /// Gets whether the lasso automatically closes when clicking near the first point.
        /// </summary>
        public bool AutoClose => _autoClose;

        /// <summary>
        /// Gets the distance threshold (in doc pixels) for auto-closing the polygon.
        /// </summary>
        public double CloseDistance => _closeDistance;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Tool-specific options
            yield return new ToggleOption("autoClose", "Auto close", _autoClose, SetAutoClose, Order: 0, Tooltip: "Automatically close polygon when clicking near start");
            yield return new SliderOption("closeDistance", "Close distance", 1, 50, _closeDistance, SetCloseDistance, Order: 1, Tooltip: "Distance threshold for auto-close");

            // Shared selection options (when selection is active)
            if (SelectionSettings is { Active: true })
            {
                yield return new SeparatorOption(Order: 100);
                foreach (var opt in SelectionSettings.GetTransformOptions(baseOrder: 101))
                    yield return opt;
            }
        }

        /// <summary>
        /// Sets whether lasso auto-closes.
        /// </summary>
        public void SetAutoClose(bool value)
        {
            if (_autoClose == value) return;
            _autoClose = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the close distance threshold.
        /// </summary>
        public void SetCloseDistance(double value)
        {
            if (value < 1.0) value = 1.0;
            if (value > 50.0) value = 50.0;
            if (Math.Abs(_closeDistance - value) < 0.001) return;
            _closeDistance = value;
            RaiseChanged();
        }
    }
}
