using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Constants;
using PixlPunkt.PluginSdk.Settings;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Eraser tool.
    /// </summary>
    /// <remarks>
    /// This class manages its own independent brush settings (size, shape, opacity, density).
    /// Each tool maintains its own configuration so switching tools preserves individual settings.
    /// Supports both built-in shapes (Circle, Square) and custom brushes loaded from .mrk files.
    /// </remarks>
    public sealed class EraserToolSettings : ToolSettingsBase, IStrokeSettings, IOpacitySettings, IDensitySettings, ICustomBrushSettings
    {
        private BrushShape _shape = BrushShape.Circle;
        private string? _customBrushFullName;
        private int _size = 4;
        private byte _opacity = ToolLimits.MaxOpacity;
        private byte _density = ToolLimits.MaxDensity;
        private bool _eraseToTransparent = true;

        /// <inheritdoc/>
        public override Icon Icon => Icon.Eraser;

        /// <inheritdoc/>
        public override string DisplayName => "Eraser";

        /// <inheritdoc/>
        public override string Description => "Erase pixels to transparency";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.E);

        /// <summary>
        /// Gets the eraser shape (used when no custom brush is selected).
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
        /// Gets the eraser size (1-128).
        /// </summary>
        public int Size => _size;

        /// <summary>
        /// Gets the eraser opacity (0-255).
        /// </summary>
        public byte Opacity => _opacity;

        /// <summary>
        /// Gets the eraser density/hardness (0-255).
        /// </summary>
        public byte Density => _density;

        /// <summary>
        /// Gets whether eraser removes to full transparency (true) or to background color (false).
        /// </summary>
        public bool EraseToTransparent => _eraseToTransparent;

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
                Tooltip: "Select eraser shape or custom brush"
            );
            yield return new SliderOption("size", "Size", ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize, _size, v => SetSize((int)v), Order: 1);
            yield return new SliderOption("opacity", "Opacity", ToolLimits.MinOpacity, ToolLimits.MaxOpacity, _opacity, v => SetOpacity((byte)v), Order: 2);
            yield return new SliderOption("density", "Density", ToolLimits.MinDensity, ToolLimits.MaxDensity, _density, v => SetDensity((byte)v), Order: 3);
            yield return new SeparatorOption(Order: 4);
            yield return new ToggleOption("eraseToTransparent", "Erase to transparent", _eraseToTransparent, SetEraseToTransparent, Order: 5);
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
        /// Sets the eraser shape (does not clear custom brush selection).
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
        public void SetCustomBrush(string? fullName)
        {
            if (_customBrushFullName == fullName) return;
            _customBrushFullName = fullName;

            if (!string.IsNullOrEmpty(fullName))
            {
                _shape = BrushShape.Custom;
            }

            RaiseChanged();
        }

        /// <summary>
        /// Sets the eraser size (1-128).
        /// </summary>
        public void SetSize(int size)
        {
            size = Math.Clamp(size, ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize);
            if (_size == size) return;
            _size = size;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the eraser opacity (0-255).
        /// </summary>
        public void SetOpacity(byte opacity)
        {
            if (_opacity == opacity) return;
            _opacity = opacity;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the eraser density (0-255).
        /// </summary>
        public void SetDensity(byte density)
        {
            if (_density == density) return;
            _density = density;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether eraser removes to full transparency.
        /// </summary>
        public void SetEraseToTransparent(bool value)
        {
            if (_eraseToTransparent == value) return;
            _eraseToTransparent = value;
            RaiseChanged();
        }
    }
}
