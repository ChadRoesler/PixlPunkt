using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Constants;
using PixlPunkt.PluginSdk.Settings;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Replacer (color replace) tool.
    /// Supports both built-in shapes (Circle, Square) and custom brushes loaded from .mrk files.
    /// </summary>
    public sealed class ReplacerToolSettings : ToolSettingsBase, IStrokeSettings, IOpacitySettings, IDensitySettings, ICustomBrushSettings
    {
        private BrushShape _shape = BrushShape.Circle;
        private string? _customBrushFullName;
        private int _size = ToolLimits.MinBrushSize;
        private byte _opacity = ToolLimits.MaxOpacity;
        private byte _density = ToolLimits.MaxDensity;
        private bool _ignoreAlpha = true;

        /// <inheritdoc/>
        public override Icon Icon => Icon.PenSync;

        /// <inheritdoc/>
        public override string DisplayName => "Replacer";

        /// <inheritdoc/>
        public override string Description => "Replace one color with another";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.R);

        // ====================================================================
        // IBrushLikeSettings Implementation
        // ====================================================================

        /// <summary>
        /// Gets the replacer shape (used when no custom brush is selected).
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
        /// Gets the replacer size (1-128).
        /// </summary>
        public int Size => _size;

        /// <summary>
        /// Gets the replacer opacity (0-255).
        /// </summary>
        public byte Opacity => _opacity;

        /// <summary>
        /// Gets the replacer density/hardness (0-255).
        /// </summary>
        public byte Density => _density;

        // ====================================================================
        // Replacer-Specific Properties
        // ====================================================================

        /// <summary>
        /// Gets whether the replacer ignores alpha channel when matching colors.
        /// </summary>
        public bool IgnoreAlpha => _ignoreAlpha;

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
                Tooltip: "Select replacer shape or custom brush"
            );
            yield return new SliderOption("size", "Size", ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize, _size, v => SetSize((int)v), Order: 1);
            yield return new SliderOption("opacity", "Opacity", ToolLimits.MinOpacity, ToolLimits.MaxOpacity, _opacity, v => SetOpacity((byte)v), Order: 2);
            yield return new SliderOption("density", "Density", ToolLimits.MinDensity, ToolLimits.MaxDensity, _density, v => SetDensity((byte)v), Order: 3);
            yield return new SeparatorOption(Order: 4);
            yield return new ToggleOption("ignoreAlpha", "Ignore alpha", _ignoreAlpha, SetIgnoreAlpha, Order: 5);
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
        /// Sets the replacer shape (does not clear custom brush selection).
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
        /// Sets the replacer size.
        /// </summary>
        public void SetSize(int size)
        {
            size = Math.Clamp(size, ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize);
            if (_size == size) return;
            _size = size;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the replacer opacity (0-255).
        /// </summary>
        public void SetOpacity(byte opacity)
        {
            if (_opacity == opacity) return;
            _opacity = opacity;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the replacer density (0-255).
        /// </summary>
        public void SetDensity(byte density)
        {
            if (_density == density) return;
            _density = density;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether the replacer ignores alpha when matching.
        /// </summary>
        public void SetIgnoreAlpha(bool value)
        {
            if (_ignoreAlpha == value) return;
            _ignoreAlpha = value;
            RaiseChanged();
        }
    }
}
