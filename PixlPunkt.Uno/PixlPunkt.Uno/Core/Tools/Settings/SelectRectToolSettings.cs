using System.Collections.Generic;
using FluentIcons.Common;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Rectangle Selection tool.
    /// </summary>
    public sealed class SelectRectToolSettings : ToolSettingsBase
    {
        private bool _squareConstrain = false;

        /// <inheritdoc/>
        public override Icon Icon => Icon.BorderNone;

        /// <inheritdoc/>
        public override string DisplayName => "Select Rectangle";

        /// <inheritdoc/>
        public override string Description => "Select a rectangular region";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.M);

        /// <summary>
        /// Gets or sets the shared selection settings (injected by ToolState).
        /// </summary>
        public SelectionToolSettings? SelectionSettings { get; set; }

        /// <summary>
        /// Gets whether to constrain to perfect squares (Shift behavior default).
        /// </summary>
        public bool SquareConstrain => _squareConstrain;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Shared selection options (when selection is active)
            if (SelectionSettings is { Active: true })
            {
                foreach (var opt in SelectionSettings.GetTransformOptions(baseOrder: 0))
                    yield return opt;
            }
        }

        /// <summary>
        /// Sets whether to constrain to squares.
        /// </summary>
        public void SetSquareConstrain(bool value)
        {
            if (_squareConstrain == value) return;
            _squareConstrain = value;
            RaiseChanged();
        }
    }
}
