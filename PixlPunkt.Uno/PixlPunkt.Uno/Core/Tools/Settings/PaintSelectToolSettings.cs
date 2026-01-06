using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Constants;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings specific to the Paint Selection tool.
    /// </summary>
    /// <remarks>
    /// Paint Selection tool uses hard edges and full opacity by design
    /// since it's painting a binary selection mask, not color.
    /// </remarks>
    public sealed class PaintSelectToolSettings : ToolSettingsBase, IStrokeSettings
    {
        private BrushShape _shape = BrushShape.Circle;
        private int _size = ToolLimits.DefaultBrushSize;

        /// <inheritdoc/>
        public override Icon Icon => Icon.ColorLine;

        /// <inheritdoc/>
        public override string DisplayName => "Paint Select";

        /// <inheritdoc/>
        public override string Description => "Paint to add or remove from selection";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.P, Shift: true);

        /// <summary>
        /// Gets or sets the shared selection settings (injected by ToolState).
        /// </summary>
        public SelectionToolSettings? SelectionSettings { get; set; }

        // ====================================================================
        // IStrokeSettings Implementation
        // ====================================================================

        /// <summary>
        /// Gets the paint selection brush shape.
        /// </summary>
        public BrushShape Shape => _shape;

        /// <summary>
        /// Gets the paint selection brush size (1-128).
        /// </summary>
        public int Size => _size;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Tool-specific options
            yield return new ShapeOption("shape", "Shape", _shape, SetShape, Order: 0);
            yield return new SliderOption("size", "Size", ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize, _size, v => SetSize((int)v), Order: 1);

            // Shared selection options (when selection is active)
            if (SelectionSettings is { Active: true })
            {
                yield return new SeparatorOption(Order: 100);
                foreach (var opt in SelectionSettings.GetTransformOptions(baseOrder: 101))
                    yield return opt;
            }
        }

        /// <summary>
        /// Sets the paint selection brush shape.
        /// </summary>
        public void SetShape(BrushShape shape)
        {
            if (_shape == shape) return;
            _shape = shape;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the paint selection brush size.
        /// </summary>
        public void SetSize(int size)
        {
            size = Math.Clamp(size, ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize);
            if (_size == size) return;
            _size = size;
            RaiseChanged();
        }
    }
}
