using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Constants;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Gradient fill types for the gradient fill tool.
    /// </summary>
    public enum GradientType
    {
        /// <summary>Linear gradient from start to end point.</summary>
        Linear,
        /// <summary>Radial gradient expanding from center.</summary>
        Radial,
        /// <summary>Angular/conical gradient rotating around center.</summary>
        Angular,
        /// <summary>Diamond-shaped gradient from center.</summary>
        Diamond
    }

    /// <summary>
    /// Base color mode for gradient fill.
    /// </summary>
    public enum GradientColorMode
    {
        /// <summary>White to black gradient.</summary>
        WhiteToBlack,
        /// <summary>Black to white gradient.</summary>
        BlackToWhite,
        /// <summary>Foreground to background color.</summary>
        ForegroundToBackground,
        /// <summary>Background to foreground color.</summary>
        BackgroundToForeground,
        /// <summary>Custom gradient from gradient ramp editor.</summary>
        Custom
    }

    /// <summary>
    /// Dithering style for gradient fill.
    /// /// </summary>
    public enum DitherStyle
    {
        /// <summary>No dithering - smooth gradient.</summary>
        None,
        /// <summary>Bayer 2x2 ordered dithering.</summary>
        Bayer2x2,
        /// <summary>Bayer 4x4 ordered dithering.</summary>
        Bayer4x4,
        /// <summary>Bayer 8x8 ordered dithering.</summary>
        Bayer8x8,
        /// <summary>Simple 50/50 checker pattern.</summary>
        Checker,
        /// <summary>Diagonal line pattern.</summary>
        Diagonal,
        /// <summary>Crosshatch pattern.</summary>
        Crosshatch,
        /// <summary>Floyd-Steinberg error diffusion.</summary>
        FloydSteinberg,
        /// <summary>Atkinson error diffusion (lighter).</summary>
        Atkinson,
        /// <summary>Riemersma dithering (Hilbert curve based).</summary>
        Riemersma,
        /// <summary>Blue noise / stochastic dithering.</summary>
        BlueNoise
    }

    /// <summary>
    /// Settings for the Gradient Fill tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Gradient Fill tool creates gradients by dragging from a start point to an end point.
    /// Supports linear, radial, angular, and diamond gradient types with various dithering styles.
    /// </para>
    /// <para>
    /// Dithering is essential for pixel art to create smooth color transitions with limited palettes.
    /// The tool supports ordered dithering (Bayer matrices), pattern-based dithering, and error diffusion.
    /// </para>
    /// </remarks>
    public sealed class GradientFillToolSettings : ToolSettingsBase
    {
        private GradientType _gradientType = GradientType.Linear;
        private GradientColorMode _colorMode = GradientColorMode.ForegroundToBackground;
        private DitherStyle _ditherStyle = DitherStyle.Bayer4x4;
        private int _ditherStrength = 100;
        private int _ditherScale = 1;
        private bool _reverse = false;
        private byte _opacity = ToolLimits.MaxOpacity;

        // Custom gradient colors (used when ColorMode is Custom)
        private readonly List<GradientStop> _customStops = new();

        /// <inheritdoc/>
        public override Icon Icon => Icon.CalendarPattern;

        /// <inheritdoc/>
        public override string DisplayName => "Gradient Fill";

        /// <inheritdoc/>
        public override string Description => "Fill with a gradient using various dithering styles";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.G, Shift: true);

        // ====================================================================
        // GRADIENT TYPE
        // ====================================================================

        /// <summary>
        /// Gets the gradient type (Linear, Radial, Angular, Diamond).
        /// </summary>
        public GradientType GradientType => _gradientType;

        /// <summary>
        /// Sets the gradient type.
        /// </summary>
        public void SetGradientType(GradientType type)
        {
            if (_gradientType == type) return;
            _gradientType = type;
            RaiseChanged();
        }

        // ====================================================================
        // COLOR MODE
        // ====================================================================

        /// <summary>
        /// Gets the gradient color mode.
        /// </summary>
        public GradientColorMode ColorMode => _colorMode;

        /// <summary>
        /// Sets the gradient color mode.
        /// </summary>
        public void SetColorMode(GradientColorMode mode)
        {
            if (_colorMode == mode) return;
            _colorMode = mode;
            RaiseChanged();
        }

        /// <summary>
        /// Gets whether the gradient colors should be reversed.
        /// </summary>
        public bool Reverse => _reverse;

        /// <summary>
        /// Sets whether to reverse the gradient direction.
        /// </summary>
        public void SetReverse(bool reverse)
        {
            if (_reverse == reverse) return;
            _reverse = reverse;
            RaiseChanged();
        }

        // ====================================================================
        // CUSTOM GRADIENT STOPS
        // ====================================================================

        /// <summary>
        /// Gets the custom gradient stops (used when ColorMode is Custom).
        /// </summary>
        public IReadOnlyList<GradientStop> CustomStops => _customStops;

        /// <summary>
        /// Clears and sets custom gradient stops from external source.
        /// </summary>
        public void SetCustomStops(IEnumerable<GradientStop> stops)
        {
            _customStops.Clear();
            _customStops.AddRange(stops);
            RaiseChanged();
        }

        /// <summary>
        /// Creates custom stops from a list of colors (evenly distributed).
        /// </summary>
        public void SetCustomStopsFromColors(IReadOnlyList<uint> colors)
        {
            _customStops.Clear();
            if (colors.Count == 0) return;

            if (colors.Count == 1)
            {
                _customStops.Add(new GradientStop(0.0, colors[0]));
                _customStops.Add(new GradientStop(1.0, colors[0]));
            }
            else
            {
                for (int i = 0; i < colors.Count; i++)
                {
                    double position = (double)i / (colors.Count - 1);
                    _customStops.Add(new GradientStop(position, colors[i]));
                }
            }

            RaiseChanged();
        }

        // ====================================================================
        // DITHERING
        // ====================================================================

        /// <summary>
        /// Gets the dithering style.
        /// </summary>
        public DitherStyle DitherStyle => _ditherStyle;

        /// <summary>
        /// Sets the dithering style.
        /// </summary>
        public void SetDitherStyle(DitherStyle style)
        {
            if (_ditherStyle == style) return;
            _ditherStyle = style;
            RaiseChanged();
        }

        /// <summary>
        /// Gets the dither strength (0-100%).
        /// </summary>
        public int DitherStrength => _ditherStrength;

        /// <summary>
        /// Sets the dither strength (0-100%).
        /// </summary>
        public void SetDitherStrength(int strength)
        {
            strength = Math.Clamp(strength, 0, 100);
            if (_ditherStrength == strength) return;
            _ditherStrength = strength;
            RaiseChanged();
        }

        /// <summary>
        /// Gets the dither pattern scale (1-8).
        /// </summary>
        public int DitherScale => _ditherScale;

        /// <summary>
        /// Sets the dither pattern scale.
        /// </summary>
        public void SetDitherScale(int scale)
        {
            scale = Math.Clamp(scale, 1, 8);
            if (_ditherScale == scale) return;
            _ditherScale = scale;
            RaiseChanged();
        }

        // ====================================================================
        // OPACITY
        // ====================================================================

        /// <summary>
        /// Gets the gradient opacity (0-255).
        /// </summary>
        public byte Opacity => _opacity;

        /// <summary>
        /// Sets the gradient opacity.
        /// </summary>
        public void SetOpacity(byte opacity)
        {
            if (_opacity == opacity) return;
            _opacity = opacity;
            RaiseChanged();
        }

        // ====================================================================
        // LAST KNOWN COLORS (for FG/BG modes)
        // ====================================================================

        /// <summary>
        /// Gets or sets the last known foreground color.
        /// </summary>
        public uint? LastKnownFg { get; set; }

        /// <summary>
        /// Gets or sets the last known background color.
        /// </summary>
        public uint? LastKnownBg { get; set; }

        // ====================================================================
        // TOOL OPTIONS
        // ====================================================================

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Gradient type dropdown
            yield return new DropdownOption(
                "gradientType",
                "Type",
                new List<string> { "Linear", "Radial", "Angular", "Diamond" },
                (int)_gradientType,
                idx => SetGradientType((GradientType)idx),
                Order: 0,
                Tooltip: "Gradient shape type"
            );

            // Color mode dropdown
            yield return new DropdownOption(
                "colorMode",
                "Colors",
                new List<string> { "White > Black", "Black > White", "FG > BG", "BG > FG", "Custom..." },
                (int)_colorMode,
                idx =>
                {
                    var newMode = (GradientColorMode)idx;
                    SetColorMode(newMode);

                    // If switching to Custom, initialize defaults and open editor
                    if (newMode == GradientColorMode.Custom)
                    {
                        // Initialize with FG > BG as starting point if no custom stops defined
                        if (_customStops.Count == 0)
                        {
                            uint fg = LastKnownFg ?? 0xFF000000u;
                            uint bg = LastKnownBg ?? 0xFFFFFFFFu;
                            _customStops.Add(new GradientStop(0.0, fg));
                            _customStops.Add(new GradientStop(1.0, bg));
                        }

                        // Automatically open the gradient editor
                        OpenCustomGradientEditor();
                    }
                },
                Order: 1,
                Tooltip: "Gradient color source"
            );

            // Gradient preview strip - shows the current gradient visually
            yield return new GradientPreviewOption(
                "gradientPreview",
                "",
                GetPreviewStops,
                OnEditRequested: _colorMode == GradientColorMode.Custom ? OpenCustomGradientEditor : null,
                Order: 2,
                Tooltip: _colorMode == GradientColorMode.Custom ? "Click to edit custom gradient" : "Current gradient preview",
                Width: 160,
                Height: 20
            );

            // Reverse toggle
            yield return new ToggleOption(
                "reverse",
                "Reverse",
                _reverse,
                SetReverse,
                Order: 3,
                Tooltip: "Reverse gradient direction"
            );

            yield return new SeparatorOption(Order: 4);

            // Dither style dropdown
            yield return new DropdownOption(
                "ditherStyle",
                "Dither",
                new List<string>
                {
                    "None",
                    "Bayer 2×2",
                    "Bayer 4×4",
                    "Bayer 8×8",
                    "Checker",
                    "Diagonal",
                    "Crosshatch",
                    "Floyd-Steinberg",
                    "Atkinson",
                    "Riemersma",
                    "Blue Noise"
                },
                (int)_ditherStyle,
                idx => SetDitherStyle((DitherStyle)idx),
                Order: 5,
                Tooltip: "Dithering pattern for smooth pixel art gradients"
            );

            // Dither strength slider
            yield return new SliderOption(
                "ditherStrength",
                "Strength",
                0, 100,
                _ditherStrength,
                v => SetDitherStrength((int)v),
                Order: 6,
                Tooltip: "Dithering intensity (0% = solid bands, 100% = full dither)"
            );

            // Dither scale slider
            yield return new SliderOption(
                "ditherScale",
                "Scale",
                1, 8,
                _ditherScale,
                v => SetDitherScale((int)v),
                Order: 7,
                Tooltip: "Dither pattern size multiplier"
            );

            yield return new SeparatorOption(Order: 8);

            // Opacity slider
            yield return new SliderOption(
                "opacity",
                "Opacity",
                ToolLimits.MinOpacity, ToolLimits.MaxOpacity,
                _opacity,
                v => SetOpacity((byte)v),
                Order: 9,
                Tooltip: "Gradient opacity"
            );
        }

        // ====================================================================
        // GRADIENT PREVIEW HELPERS
        // ====================================================================

        /// <summary>
        /// Gets the gradient stops for the preview based on current color mode.
        /// </summary>
        private IReadOnlyList<GradientStopInfo> GetPreviewStops()
        {
            uint fg = LastKnownFg ?? 0xFF000000u;
            uint bg = LastKnownBg ?? 0xFFFFFFFFu;

            GradientStopInfo[] stops = _colorMode switch
            {
                GradientColorMode.WhiteToBlack => new GradientStopInfo[]
                {
                    new(0.0, 0xFFFFFFFF),
                    new(1.0, 0xFF000000)
                },
                GradientColorMode.BlackToWhite => new GradientStopInfo[]
                {
                    new(0.0, 0xFF000000),
                    new(1.0, 0xFFFFFFFF)
                },
                GradientColorMode.ForegroundToBackground => new GradientStopInfo[]
                {
                    new(0.0, fg),
                    new(1.0, bg)
                },
                GradientColorMode.BackgroundToForeground => new GradientStopInfo[]
                {
                    new(0.0, bg),
                    new(1.0, fg)
                },
                GradientColorMode.Custom => GetCustomPreviewStops(),
                _ => new GradientStopInfo[]
                {
                    new(0.0, fg),
                    new(1.0, bg)
                }
            };

            // Apply reverse if needed
            if (_reverse)
            {
                var reversed = new GradientStopInfo[stops.Length];
                for (int i = 0; i < stops.Length; i++)
                {
                    var orig = stops[stops.Length - 1 - i];
                    reversed[i] = new GradientStopInfo(1.0 - orig.Position, orig.Color);
                }
                return reversed;
            }

            return stops;
        }

        /// <summary>
        /// Gets custom gradient stops as preview info.
        /// </summary>
        private GradientStopInfo[] GetCustomPreviewStops()
        {
            if (_customStops.Count == 0)
            {
                // Default gradient if none set
                uint fg = LastKnownFg ?? 0xFF000000u;
                uint bg = LastKnownBg ?? 0xFFFFFFFFu;
                return new GradientStopInfo[]
                {
                    new(0.0, fg),
                    new(1.0, bg)
                };
            }

            var result = new GradientStopInfo[_customStops.Count];
            for (int i = 0; i < _customStops.Count; i++)
            {
                result[i] = new GradientStopInfo(_customStops[i].Position, _customStops[i].Color);
            }
            return result;
        }

        /// <summary>
        /// Callback invoked when user wants to edit the custom gradient.
        /// </summary>
        public Action? OpenCustomGradientEditorCallback { get; set; }

        /// <summary>
        /// Opens the custom gradient editor.
        /// </summary>
        private void OpenCustomGradientEditor()
        {
            OpenCustomGradientEditorCallback?.Invoke();
        }
    }

    /// <summary>
    /// Represents a single stop in a gradient.
    /// </summary>
    public readonly struct GradientStop
    {
        /// <summary>
        /// Position in the gradient (0.0 to 1.0).
        /// </summary>
        public double Position { get; }

        /// <summary>
        /// Color at this stop (BGRA format).
        /// </summary>
        public uint Color { get; }

        /// <summary>
        /// Creates a new gradient stop.
        /// </summary>
        public GradientStop(double position, uint color)
        {
            Position = Math.Clamp(position, 0.0, 1.0);
            Color = color;
        }
    }
}
