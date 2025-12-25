using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Constants;
using PixlPunkt.PluginSdk.Settings;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Jumble (pixel randomization) tool.
    /// </summary>
    /// <remarks>
    /// Jumble tool uses strength and locality instead of opacity/density.
    /// It operates with hard edges and full opacity by design.
    /// Supports both built-in shapes (Circle, Square) and custom brushes loaded from .mrk files.
    /// </remarks>
    public sealed class JumbleToolSettings : ToolSettingsBase, IStrokeSettings, ICustomBrushSettings
    {
        private BrushShape _shape = BrushShape.Circle;
        private string? _customBrushFullName;
        private int _size = ToolLimits.DefaultBrushSize;
        private int _strengthPercent = 35;
        private double _falloffGamma = ToolLimits.DefaultGamma;
        private int _localityPercent = 70;
        private bool _includeTransparent = true;

        /// <inheritdoc/>
        public override Icon Icon => Icon.DataSunburst;

        /// <inheritdoc/>
        public override string DisplayName => "Jumble";

        /// <inheritdoc/>
        public override string Description => "Randomly shuffle pixels";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.J);

        // ====================================================================
        // IStrokeSettings Implementation
        // ====================================================================

        /// <summary>
        /// Gets the jumble brush shape (used when no custom brush is selected).
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
        /// Gets the jumble brush size (1-128).
        /// </summary>
        public int Size => _size;

        // ====================================================================
        // Jumble-Specific Properties
        // ====================================================================

        /// <summary>
        /// Gets the jumble strength percentage (0-100).
        /// </summary>
        public int StrengthPercent => _strengthPercent;

        /// <summary>
        /// Gets the falloff gamma curve (0.5-4.0).
        /// </summary>
        public double FalloffGamma => _falloffGamma;

        /// <summary>
        /// Gets the locality percentage determining how far pixels can move (0-100).
        /// </summary>
        public int LocalityPercent => _localityPercent;

        /// <summary>
        /// Gets whether jumble includes transparent pixels.
        /// </summary>
        public bool IncludeTransparent => _includeTransparent;

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
                Tooltip: "Select jumble brush shape or custom brush"
            );
            yield return new SliderOption("size", "Size", ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize, _size, v => SetSize((int)v), Order: 1);
            yield return new SeparatorOption(Order: 2);
            yield return new SliderOption("strength", "Strength", ToolLimits.MinStrengthPercent, ToolLimits.MaxStrengthPercent, _strengthPercent, v => SetStrengthPercent((int)v), Order: 3);
            yield return new SliderOption("falloff", "Falloff", ToolLimits.MinGamma, ToolLimits.MaxGamma, _falloffGamma, SetFalloffGamma, Order: 4, Step: 0.1);
            yield return new SliderOption("locality", "Locality", ToolLimits.MinStrengthPercent, ToolLimits.MaxStrengthPercent, _localityPercent, v => SetLocalityPercent((int)v), Order: 5);
            yield return new SeparatorOption(Order: 6);
            yield return new ToggleOption("includeTransparent", "Include transparent", _includeTransparent, SetIncludeTransparent, Order: 7);
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
        /// Sets the jumble brush shape (does not clear custom brush selection).
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
        /// Sets the jumble brush size.
        /// </summary>
        public void SetSize(int size)
        {
            size = Math.Clamp(size, ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize);
            if (_size == size) return;
            _size = size;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the jumble strength percentage.
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
            if (Math.Abs(_falloffGamma - value) < 1e-6) return;
            _falloffGamma = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the locality percentage.
        /// </summary>
        public void SetLocalityPercent(int value)
        {
            value = Math.Clamp(value, ToolLimits.MinStrengthPercent, ToolLimits.MaxStrengthPercent);
            if (_localityPercent == value) return;
            _localityPercent = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether jumble includes transparent pixels.
        /// </summary>
        public void SetIncludeTransparent(bool value)
        {
            if (_includeTransparent == value) return;
            _includeTransparent = value;
            RaiseChanged();
        }
    }
}
