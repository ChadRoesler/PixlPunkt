using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Constants;
using PixlPunkt.Core.Coloring.Helpers;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Gradient brush tool.
    /// </summary>
    /// <remarks>
    /// Gradient tool uses palette colors instead of opacity for color output.
    /// </remarks>
    public sealed class GradientToolSettings : ToolSettingsBase, IStrokeSettings, IDensitySettings
    {
        private readonly List<uint> _colors = new();
        private BrushShape _shape = BrushShape.Circle;
        private int _size = ToolLimits.MinBrushSize;
        private byte _density = ToolLimits.MaxDensity;
        private bool _ignoreAlpha = true;
        private bool _loop = false;
        private int _selectedIndex = -1;

        /// <inheritdoc/>
        public override Icon Icon => Icon.TapDouble;

        /// <inheritdoc/>
        public override string DisplayName => "Gradient";

        /// <inheritdoc/>
        public override string Description => "Paint with a color gradient";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.D);

        // ====================================================================
        // IStrokeSettings Implementation
        // ====================================================================

        /// <summary>
        /// Gets the gradient brush shape.
        /// </summary>
        public BrushShape Shape => _shape;

        /// <summary>
        /// Gets the gradient brush size (1-128).
        /// </summary>
        public int Size => _size;

        // ====================================================================
        // IDensitySettings Implementation
        // ====================================================================

        /// <summary>
        /// Gets the gradient brush density/hardness (0-255).
        /// </summary>
        public byte Density => _density;

        // ====================================================================
        // Gradient-Specific Properties
        // ====================================================================

        /// <summary>
        /// Gets the list of colors in the gradient as IReadOnlyList.
        /// </summary>
        public IReadOnlyList<uint> Colors => _colors;

        /// <summary>
        /// Notifies that external changes were made to the color list.
        /// Call this after directly modifying the Colors list via IList interface.
        /// </summary>
        public void NotifyColorsChanged()
        {
            _selectedIndex = _colors.Count > 0 ? _colors.Count - 1 : -1;
            if (_colors.Count > 0)
                LastPick = _colors[^1];
            RaiseChanged();
        }

        /// <summary>
        /// Gets whether gradient ignores alpha channel.
        /// </summary>
        public bool IgnoreAlpha => _ignoreAlpha;

        /// <summary>
        /// Gets whether gradient loops back to the start color.
        /// </summary>
        public bool Loop => _loop;

        /// <summary>
        /// Gets the last color picked/added to the gradient.
        /// </summary>
        public uint? LastPick { get; private set; }

        /// <summary>
        /// Gets or sets the last known foreground color for picker defaults.
        /// </summary>
        public uint? LastKnownFg { get; set; }

        /// <summary>
        /// Gets or sets the last known background color for gradient end defaults.
        /// </summary>
        public uint? LastKnownBg { get; set; }

        /// <summary>
        /// Gets the currently selected color index in the palette.
        /// </summary>
        public int SelectedIndex => _selectedIndex;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new ShapeOption("shape", "Shape", _shape, SetShape, Order: 0, ShowLabel: true);
            yield return new SliderOption("size", "Size", ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize, _size, v => SetSize((int)v), Order: 1, ShowLabel: true);
            yield return new SliderOption("density", "Density", ToolLimits.MinDensity, ToolLimits.MaxDensity, _density, v => SetDensity((byte)v), Order: 2);
            yield return new SeparatorOption(Order: 3);
            yield return new IconOption("colorIcon", Icon.Color, Order: 4);
            yield return new PaletteOption(
                Id: "palette",
                Label: "Palette",
                Colors: _colors,  // Pass the List<uint> directly - it implements IReadOnlyList<uint> and IList<uint>
                SelectedIndex: _selectedIndex,
                OnSelectionChanged: SetSelectedIndex,
                OnAddRequested: null,       // Let PaletteSwatchRow open color picker
                OnAddRampRequested: null,   // Let PaletteSwatchRow open gradient picker
                OnEditRequested: null,      // Let PaletteSwatchRow open color picker for editing
                OnRemoveRequested: RemoveAt,
                OnClearRequested: Clear,
                OnReverseRequested: Reverse,
                OnMoveRequested: Move,
                OnColorsChanged: NotifyColorsChanged,
                Order: 5,
                Tooltip: "Gradient color palette"
            );
            yield return new SeparatorOption(Order: 6);
            yield return new ToggleOption("ignoreAlpha", "Ignore alpha", _ignoreAlpha, SetIgnoreAlpha, Order: 7);
            yield return new ToggleOption("loop", "Loop", _loop, SetLoop, Order: 8, Tooltip: "Loop gradient back to start");
        }

        /// <summary>
        /// Sets the gradient brush shape.
        /// </summary>
        public void SetShape(BrushShape shape)
        {
            if (_shape == shape) return;
            _shape = shape;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the gradient brush size.
        /// </summary>
        public void SetSize(int size)
        {
            size = Math.Clamp(size, ToolLimits.MinBrushSize, ToolLimits.MaxBrushSize);
            if (_size == size) return;
            _size = size;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the gradient brush density (0-255).
        /// </summary>
        public void SetDensity(byte density)
        {
            if (_density == density) return;
            _density = density;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether gradient ignores alpha.
        /// </summary>
        public void SetIgnoreAlpha(bool value)
        {
            if (_ignoreAlpha == value) return;
            _ignoreAlpha = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets whether gradient loops.
        /// </summary>
        public void SetLoop(bool value)
        {
            if (_loop == value) return;
            _loop = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the selected color index.
        /// </summary>
        public void SetSelectedIndex(int index)
        {
            index = Math.Clamp(index, -1, _colors.Count - 1);
            if (_selectedIndex == index) return;
            _selectedIndex = index;
            RaiseChanged();
        }

        /// <summary>
        /// Clears all colors from the gradient.
        /// </summary>
        public void Clear()
        {
            _colors.Clear();
            _selectedIndex = -1;
            LastPick = null;
            RaiseChanged();
        }

        /// <summary>
        /// Adds a color to the gradient (skips if duplicate).
        /// </summary>
        public void AddColor(uint bgra)
        {
            uint rgb = 0xFF000000u | (bgra & 0x00FFFFFFu);

            foreach (var c in _colors)
                if (ColorUtil.RgbEqual(c, rgb))
                    return;

            _colors.Add(rgb);
            _selectedIndex = _colors.Count - 1;
            LastPick = rgb;
            RaiseChanged();
        }

        /// <summary>
        /// Adds multiple colors to the gradient (e.g., from a ramp generated by UI).
        /// Skips duplicates.
        /// </summary>
        public void AddColors(IEnumerable<uint> colors)
        {
            bool added = false;
            foreach (var bgra in colors)
            {
                uint rgb = 0xFF000000u | (bgra & 0x00FFFFFFu);

                bool dup = false;
                foreach (var c in _colors)
                    if (ColorUtil.RgbEqual(c, rgb)) { dup = true; break; }

                if (!dup)
                {
                    _colors.Add(rgb);
                    LastPick = rgb;
                    added = true;
                }
            }

            if (added)
            {
                _selectedIndex = _colors.Count - 1;
                RaiseChanged();
            }
        }

        /// <summary>
        /// Removes color at index.
        /// </summary>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _colors.Count) return;
            _colors.RemoveAt(index);
            LastPick = _colors.Count > 0 ? _colors[^1] : null;
            _selectedIndex = Math.Clamp(_selectedIndex, -1, _colors.Count - 1);
            RaiseChanged();
        }

        /// <summary>
        /// Replaces color at index (skips if would create duplicate).
        /// </summary>
        public void ReplaceAt(int index, uint bgra)
        {
            if (index < 0 || index >= _colors.Count) return;
            uint rgb = 0xFF000000u | (bgra & 0x00FFFFFFu);

            for (int i = 0; i < _colors.Count; i++)
                if (i != index && ColorUtil.RgbEqual(_colors[i], rgb))
                    return;

            _colors[index] = rgb;
            LastPick = rgb;
            RaiseChanged();
        }

        /// <summary>
        /// Moves a color from one index to another.
        /// </summary>
        public void Move(int from, int to)
        {
            if (from == to) return;
            if (from < 0 || from >= _colors.Count) return;
            if (to < 0 || to >= _colors.Count) return;

            // Simple swap-based move for drag operations
            uint item = _colors[from];
            _colors.RemoveAt(from);
            _colors.Insert(to, item);

            _selectedIndex = to;
            RaiseChanged();
        }

        /// <summary>
        /// Reverses the gradient color order.
        /// </summary>
        public void Reverse()
        {
            _colors.Reverse();
            if (_selectedIndex >= 0 && _colors.Count > 0)
                _selectedIndex = _colors.Count - 1 - _selectedIndex;
            RaiseChanged();
        }
    }
}
