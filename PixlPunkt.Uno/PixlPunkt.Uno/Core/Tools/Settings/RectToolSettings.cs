using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Constants;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Rectangle shape tool.
    /// </summary>
    public sealed class RectToolSettings : ToolSettingsBase, IStrokeSettings, IOpacitySettings, IDensitySettings
    {
        private BrushShape _shape = BrushShape.Circle;
        private int _strokeWidth = ToolLimits.MinStrokeWidth;
        private byte _opacity = ToolLimits.MaxOpacity;
        private byte _density = ToolLimits.MaxDensity;
        private bool _filled = false;
        private bool _squareConstrain = false;

        /// <inheritdoc/>
        public override Icon Icon => Icon.Square;

        /// <inheritdoc/>
        public override string DisplayName => "Rectangle";

        /// <inheritdoc/>
        public override string Description => "Draw rectangles and squares";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.U, Ctrl: true);

        // ====================================================================
        // IBrushLikeSettings Implementation
        // ====================================================================

        /// <summary>
        /// Gets the brush shape for drawing rectangles (affects stroke style for custom brushes).
        /// </summary>
        public BrushShape Shape => _shape;

        /// <summary>
        /// Gets the stroke width for outlined rectangles (1-128).
        /// Maps to IBrushLikeSettings.Size for unified brush handling.
        /// </summary>
        public int Size => _strokeWidth;

        /// <summary>
        /// Gets the opacity (0-255).
        /// </summary>
        public byte Opacity => _opacity;

        /// <summary>
        /// Gets the density/hardness (0-255).
        /// </summary>
        public byte Density => _density;

        // ====================================================================
        // Rect-Specific Properties
        // ====================================================================

        /// <summary>
        /// Gets the stroke width for outlined rectangles (1-128).
        /// </summary>
        public int StrokeWidth => _strokeWidth;

        /// <summary>
        /// Gets whether rectangles are drawn filled or outlined.
        /// </summary>
        public bool Filled => _filled;

        /// <summary>
        /// Gets whether to constrain to perfect squares (Shift behavior default).
        /// </summary>
        public bool SquareConstrain => _squareConstrain;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new ShapeOption("shape", "Shape", _shape, SetShape, Order: 0);
            yield return new SliderOption("strokeWidth", "Stroke", ToolLimits.MinStrokeWidth, ToolLimits.MaxStrokeWidth, _strokeWidth, v => SetStrokeWidth((int)v), Order: 1);
            yield return new SliderOption("opacity", "Opacity", ToolLimits.MinOpacity, ToolLimits.MaxOpacity, _opacity, v => SetOpacity((byte)v), Order: 2);
            yield return new SliderOption("density", "Density", ToolLimits.MinDensity, ToolLimits.MaxDensity, _density, v => SetDensity((byte)v), Order: 3);
            yield return new SeparatorOption(Order: 4);
            yield return new ToggleOption("filled", "Filled", _filled, SetFilled, Order: 5);
        }

        /// <summary>
        /// Sets the brush shape.
        /// </summary>
        public void SetShape(BrushShape shape)
        {
            if (_shape == shape) return;
            _shape = shape;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the stroke width.
        /// </summary>
        public void SetStrokeWidth(int width)
        {
            width = Math.Clamp(width, ToolLimits.MinStrokeWidth, ToolLimits.MaxStrokeWidth);
            if (_strokeWidth == width) return;
            _strokeWidth = width;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the opacity (0-255).
        /// </summary>
        public void SetOpacity(byte opacity)
        {
            if (_opacity == opacity) return;
            _opacity = opacity;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the density (0-255).
        /// </summary>
        public void SetDensity(byte density)
        {
            if (_density == density) return;
            _density = density;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether rectangles are drawn filled.
        /// </summary>
        public void SetFilled(bool value)
        {
            if (_filled == value) return;
            _filled = value;
            RaiseChanged();
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
