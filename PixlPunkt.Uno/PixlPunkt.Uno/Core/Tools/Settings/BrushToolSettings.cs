using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Constants;
using PixlPunkt.PluginSdk.Settings;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Brush tool.
    /// </summary>
    /// <remarks>
    /// This class manages its own independent brush settings (size, shape, opacity, density).
    /// Each tool maintains its own configuration so switching tools preserves individual settings.
    /// Supports both built-in shapes (Circle, Square) and custom brushes loaded from .mrk files.
    /// </remarks>
    public sealed class BrushToolSettings : ToolSettingsBase, IStrokeSettings, IOpacitySettings, IDensitySettings, ICustomBrushSettings
    {
        private BrushShape _shape = BrushShape.Circle;
        private string? _customBrushFullName;
        private int _size = ToolLimits.MinBrushSize;
        private byte _opacity = ToolLimits.MaxOpacity;
        private byte _density = ToolLimits.MaxDensity;
        private bool _pixelPerfect;

        /// <inheritdoc/>
        public override Icon Icon => Icon.Edit;

        /// <inheritdoc/>
        public override string DisplayName => "Brush";

        /// <inheritdoc/>
        public override string Description => "Paint with the foreground color";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.B);

        /// <summary>
        /// Gets the brush shape (used when no custom brush is selected).
        /// </summary>
        public BrushShape Shape => _shape;

        /// <summary>
        /// Gets the full name of the selected custom brush (author.brushname), or null if using built-in shape.
        /// </summary>
        public string? CustomBrushFullName => _customBrushFullName;

        /// <summary>
        /// Gets whether a custom brush is currently selected.
        /// </summary>
        public bool IsCustomBrushSelected => !string.IsNullOrEmpty(_customBrushFullName);

        /// <summary>
        /// Gets the brush size (1-128).
        /// </summary>
        public int Size => _size;

        /// <summary>
        /// Gets the brush opacity (0-255).
        /// </summary>
        public byte Opacity => _opacity;

        /// <summary>
        /// Gets the brush density/hardness (0-255).
        /// </summary>
        public byte Density => _density;

        /// <summary>
        /// Gets whether pixel-perfect mode is enabled.
        /// </summary>
        /// <remarks>
        /// When enabled for 1px brushes, diagonal movements are filtered to produce
        /// only orthogonal (horizontal/vertical) lines, eliminating "stair-stepping"
        /// artifacts common in pixel art.
        /// </remarks>
        public bool PixelPerfect => _pixelPerfect;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new CustomBrushOption(
                "brush",
                "Brush",
                _customBrushFullName,
                _shape,
                SetCustomBrush,
                SetShapeAndClearCustomBrush,
                Order: 0,
                Tooltip: "Select brush shape or custom brush"
            );
            yield return new SliderOption("size", "Size", ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize, _size, v => SetSize((int)v), Order: 1);
            yield return new SliderOption("opacity", "Opacity", ToolLimits.MinOpacity, ToolLimits.MaxOpacity, _opacity, v => SetOpacity((byte)v), Order: 2);
            yield return new SliderOption("density", "Density", ToolLimits.MinDensity, ToolLimits.MaxDensity, _density, v => SetDensity((byte)v), Order: 3);
            yield return new ToggleOption("pixelPerfect", "Pixel Perfect", _pixelPerfect, SetPixelPerfect, Order: 4, Tooltip: "Eliminate diagonal stair-stepping for 1px brushes");
        }

        /// <summary>
        /// Sets the brush shape and clears any custom brush selection.
        /// </summary>
        public void SetShapeAndClearCustomBrush(BrushShape shape)
        {
            bool changed = _shape != shape || _customBrushFullName != null;
            _shape = shape;
            _customBrushFullName = null;
            if (changed) RaiseChanged();
        }

        /// <summary>
        /// Sets the brush shape (does not clear custom brush selection).
        /// </summary>
        public void SetShape(BrushShape shape)
        {
            if (_shape == shape) return;
            _shape = shape;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the custom brush by full name (author.brushname).
        /// </summary>
        /// <param name="fullName">The full brush name, or null to clear custom brush selection.</param>
        public void SetCustomBrush(string? fullName)
        {
            if (_customBrushFullName == fullName) return;
            _customBrushFullName = fullName;

            // When selecting a custom brush, set shape to Custom
            if (!string.IsNullOrEmpty(fullName))
            {
                _shape = BrushShape.Custom;
            }

            RaiseChanged();
        }

        /// <summary>
        /// Clears the custom brush selection and reverts to built-in shape.
        /// </summary>
        public void ClearCustomBrush()
        {
            if (_customBrushFullName == null) return;
            _customBrushFullName = null;
            if (_shape == BrushShape.Custom)
            {
                _shape = BrushShape.Circle;
            }
            RaiseChanged();
        }

        /// <summary>
        /// Sets the brush size (1-128).
        /// </summary>
        public void SetSize(int size)
        {
            size = System.Math.Clamp(size, ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize);
            if (_size == size) return;
            _size = size;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the brush opacity (0-255).
        /// </summary>
        public void SetOpacity(byte opacity)
        {
            if (_opacity == opacity) return;
            _opacity = opacity;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the brush density (0-255).
        /// </summary>
        public void SetDensity(byte density)
        {
            if (_density == density) return;
            _density = density;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether pixel-perfect mode is enabled.
        /// </summary>
        public void SetPixelPerfect(bool enabled)
        {
            if (_pixelPerfect == enabled) return;
            _pixelPerfect = enabled;
            RaiseChanged();
        }
    }
}
