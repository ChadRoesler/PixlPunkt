using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Constants;
using PixlPunkt.PluginSdk.Settings;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Smudge tool.
    /// </summary>
    /// <remarks>
    /// Smudge tool uses strength instead of opacity to control smudge intensity.
    /// Supports both built-in shapes (Circle, Square) and custom brushes loaded from .mrk files.
    /// </remarks>
    public sealed class SmudgeToolSettings : ToolSettingsBase, IStrokeSettings, IDensitySettings, ICustomBrushSettings
    {
        private BrushShape _shape = BrushShape.Circle;
        private string? _customBrushFullName;
        private int _size = ToolLimits.DefaultBrushSize;
        private byte _density = ToolLimits.MaxDensity;
        private int _strengthPercent = 70;
        private double _falloffGamma = 1.0;
        private bool _hardEdge = false;
        private bool _blendOnTransparent = true;

        // ====================================================================
        // IStrokeSettings Implementation
        // ====================================================================

        /// <summary>
        /// Gets the smudge brush shape (used when no custom brush is selected).
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
        /// Gets the smudge brush size (1-128).
        /// </summary>
        public int Size => _size;

        // ====================================================================
        // IDensitySettings Implementation
        // ====================================================================

        /// <summary>
        /// Gets the smudge density/hardness (0-255).
        /// </summary>
        public byte Density => _density;

        // ====================================================================
        // Smudge-Specific Properties
        // ====================================================================

        /// <summary>
        /// Gets the smudge strength percentage (0-100).
        /// </summary>
        public int StrengthPercent => _strengthPercent;

        /// <summary>
        /// Gets the falloff gamma (0.2-4.0).
        /// </summary>
        public double FalloffGamma => _falloffGamma;

        /// <summary>
        /// Gets whether smudge uses hard edges.
        /// </summary>
        public bool HardEdge => _hardEdge;

        /// <summary>
        /// Gets whether smudge blends on transparent pixels.
        /// </summary>
        public bool BlendOnTransparent => _blendOnTransparent;

        /// <inheritdoc/>
        public override Icon Icon => Icon.HandDraw;

        /// <inheritdoc/>
        public override string DisplayName => "Smudge";

        /// <inheritdoc/>
        public override string Description => "Smear and blend colors";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.S, Shift: true);

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
                Tooltip: "Select smudge brush shape or custom brush"
            );
            yield return new SliderOption("size", "Size", ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize, _size, v => SetSize((int)v), Order: 1);
            yield return new SliderOption("density", "Density", ToolLimits.MinDensity, ToolLimits.MaxDensity, _density, v => SetDensity((byte)v), Order: 2);
            yield return new SeparatorOption(Order: 3);
            yield return new SliderOption("strength", "Strength", ToolLimits.MinStrengthPercent, ToolLimits.MaxStrengthPercent, _strengthPercent, v => SetStrengthPercent((int)v), Order: 4);
            yield return new SliderOption("falloff", "Falloff", ToolLimits.MinGamma, ToolLimits.MaxGamma, _falloffGamma, SetFalloffGamma, Order: 5, Step: 0.1);
            yield return new SeparatorOption(Order: 6);
            yield return new ToggleOption("hardEdge", "Hard edge", _hardEdge, SetHardEdge, Order: 7);
            yield return new ToggleOption("blendOnTransparent", "Blend on transparent", _blendOnTransparent, SetBlendOnTransparent, Order: 8);
        }

        // ====================================================================
        // Set Methods
        // ====================================================================

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
        /// Sets the smudge brush shape (does not clear custom brush selection).
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
        /// Sets the smudge brush size.
        /// </summary>
        public void SetSize(int size)
        {
            size = Math.Clamp(size, ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize);
            if (_size == size) return;
            _size = size;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the smudge density (0-255).
        /// </summary>
        public void SetDensity(byte density)
        {
            if (_density == density) return;
            _density = density;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the smudge strength percentage.
        /// </summary>
        public void SetStrengthPercent(int value)
        {
            value = Math.Clamp(value, ToolLimits.MinStrengthPercent, ToolLimits.MaxStrengthPercent);
            if (_strengthPercent == value) return;
            _strengthPercent = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the falloff gamma.
        /// </summary>
        public void SetFalloffGamma(double value)
        {
            value = Math.Clamp(value, ToolLimits.MinGamma, ToolLimits.MaxGamma);
            if (Math.Abs(_falloffGamma - value) < 0.0001) return;
            _falloffGamma = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether smudge uses hard edges.
        /// </summary>
        public void SetHardEdge(bool value)
        {
            if (_hardEdge == value) return;
            _hardEdge = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether smudge blends on transparent pixels.
        /// </summary>
        public void SetBlendOnTransparent(bool value)
        {
            if (_blendOnTransparent == value) return;
            _blendOnTransparent = value;
            RaiseChanged();
        }
    }
}
