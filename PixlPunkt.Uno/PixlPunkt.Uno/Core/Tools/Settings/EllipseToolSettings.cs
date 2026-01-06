using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Constants;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Ellipse shape tool.
    /// </summary>
    public sealed class EllipseToolSettings : ToolSettingsBase, IStrokeSettings, IOpacitySettings, IDensitySettings
    {
        private BrushShape _shape = BrushShape.Circle;
        private int _strokeWidth = ToolLimits.MinStrokeWidth;
        private byte _opacity = ToolLimits.MaxOpacity;
        private byte _density = ToolLimits.MaxDensity;
        private bool _filled = false;
        private bool _circleConstrain = false;

        /// <inheritdoc/>
        public override Icon Icon => Icon.Circle;

        /// <inheritdoc/>
        public override string DisplayName => "Ellipse";

        /// <inheritdoc/>
        public override string Description => "Draw ellipses and circles";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.O);

        // ====================================================================
        // IBrushLikeSettings Implementation
        // ====================================================================

        /// <summary>
        /// Gets the brush shape for drawing ellipses (affects stroke style for custom brushes).
        /// </summary>
        public BrushShape Shape => _shape;

        /// <summary>
        /// Gets the stroke width for outlined ellipses (1-128).
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
        // Ellipse-Specific Properties
        // ====================================================================

        /// <summary>
        /// Gets the stroke width for outlined ellipses (1-128).
        /// </summary>
        public int StrokeWidth => _strokeWidth;

        /// <summary>
        /// Gets whether ellipses are drawn filled or outlined.
        /// </summary>
        public bool Filled => _filled;

        /// <summary>
        /// Gets whether to constrain to perfect circles (Shift behavior default).
        /// </summary>
        public bool CircleConstrain => _circleConstrain;

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
        /// Sets whether ellipses are drawn filled.
        /// </summary>
        public void SetFilled(bool value)
        {
            if (_filled == value) return;
            _filled = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether to constrain to circles.
        /// </summary>
        public void SetCircleConstrain(bool value)
        {
            if (_circleConstrain == value) return;
            _circleConstrain = value;
            RaiseChanged();
        }
    }
}
