using System.Collections.Generic;
using System.Linq;
using FluentIcons.Common;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Ascii;
using PixlPunkt.Uno.Core.Compositing.Effects;

namespace PixlPunkt.Uno.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="AsciiEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring ASCII art conversion parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Glyph Set</strong> - Character set used for brightness mapping (Basic, Extended, Block, etc.)</item>
    /// <item><strong>Cell Width/Height</strong> - Size of each character cell in pixels</item>
    /// <item><strong>Selection Mode</strong> - How glyphs are chosen (Luminance Ramp vs Pattern Match)</item>
    /// <item><strong>Binarize Mode</strong> - How cells are binarized for pattern matching</item>
    /// <item><strong>Foreground/Background</strong> - Color modes for glyph rendering</item>
    /// <item><strong>Contrast</strong> - Brightness mapping curve (1.0 = linear)</item>
    /// <item><strong>Invert</strong> - Reverse brightness mapping (dark areas use bright characters)</item>
    /// <item><strong>Apply on Transparent</strong> - Include transparent pixels in calculations</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="AsciiEffect"/>
    /// <seealso cref="AsciiGlyphSets"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class AsciiEffectSettings : EffectSettingsBase
    {
        private readonly AsciiEffect _effect;

        /// <summary>
        /// Callback to open the glyph set editor window.
        /// Set by the UI layer (e.g., PixlPunktMainWindow) to provide window creation.
        /// </summary>
        public static System.Action? OpenGlyphEditorCallback { get; set; }

        /// <summary>
        /// Creates settings for the specified ASCII effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public AsciiEffectSettings(AsciiEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "ASCII";

        /// <inheritdoc/>
        public override string Description => "Converts the layer to ASCII art representation.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Build glyph set dropdown items from available sets
            var glyphSetNames = AsciiGlyphSets.All.Select(s => s.Name).ToList();
            int selectedIndex = glyphSetNames.IndexOf(_effect.GlyphSetName);
            if (selectedIndex < 0) selectedIndex = 0;

            yield return new DropdownOption(
                "glyphSet",
                "Glyph Set",
                glyphSetNames,
                selectedIndex,
                idx =>
                {
                    if (idx >= 0 && idx < glyphSetNames.Count)
                        _effect.GlyphSetName = glyphSetNames[idx];
                },
                Order: 0,
                Tooltip: "Character set used for ASCII art rendering");

            // Button to open the glyph set editor
            yield return new ButtonOption(
                "editGlyphs",
                "Edit Glyph Sets...",
                Icon.Edit,
                () => OpenGlyphEditorCallback?.Invoke(),
                Order: 1,
                Tooltip: "Create and edit custom glyph sets");

            yield return new SliderOption(
                "cellWidth", "Cell Width", EffectLimits.MinBlockSize, EffectLimits.MaxBlockSize, _effect.CellWidth,
                v => _effect.CellWidth = (int)v,
                Order: 2, Tooltip: "Width of each character cell in pixels");

            yield return new SliderOption(
                "cellHeight", "Cell Height", EffectLimits.MinBlockSize, EffectLimits.MaxBlockSize, _effect.CellHeight,
                v => _effect.CellHeight = (int)v,
                Order: 3, Tooltip: "Height of each character cell in pixels");

            // Selection Mode dropdown
            var selectionModes = new List<string> { "Luminance Ramp", "Pattern Match" };
            yield return new DropdownOption(
                "selectionMode",
                "Selection Mode",
                selectionModes,
                (int)_effect.Selection,
                idx => _effect.Selection = (AsciiEffect.GlyphSelectionMode)idx,
                Order: 4,
                Tooltip: "Luminance Ramp: classic ASCII by brightness. Pattern Match: shape-aware glyph selection.");

            // Binarize Mode dropdown (only relevant for Pattern Match)
            var binarizeModes = new List<string> { "Cell Average", "Fixed Threshold", "Bayer 4x4" };
            yield return new DropdownOption(
                "binarizeMode",
                "Binarize Mode",
                binarizeModes,
                (int)_effect.Binarize,
                idx => _effect.Binarize = (AsciiEffect.BinarizeMode)idx,
                Order: 5,
                Tooltip: "How to threshold pixels when using Pattern Match mode");

            // Fixed threshold slider (only relevant when Binarize = FixedThreshold)
            yield return new SliderOption(
                "fixedThreshold", "Fixed Threshold", 0.0, 1.0, _effect.FixedThreshold,
                v => _effect.FixedThreshold = v,
                Order: 6, Step: 0.05, Tooltip: "Threshold value for Fixed Threshold binarization (0-1)");

            // Foreground color mode dropdown
            var fgModes = new List<string> { "Average", "Dominant" };
            yield return new DropdownOption(
                "foregroundColor",
                "Foreground Color",
                fgModes,
                (int)_effect.ForegroundColor,
                idx => _effect.ForegroundColor = (AsciiEffect.ForegroundColorMode)idx,
                Order: 7,
                Tooltip: "How to determine glyph ink color: Average of cell or Dominant color");

            // Background fill mode dropdown
            var bgModes = new List<string> { "Transparent", "Secondary Dominant", "Dominant" };
            yield return new DropdownOption(
                "backgroundFill",
                "Background Fill",
                bgModes,
                (int)_effect.Background,
                idx => _effect.Background = (AsciiEffect.BackgroundFillMode)idx,
                Order: 8,
                Tooltip: "How to fill non-glyph pixels: Transparent, Secondary color, or Dominant color");

            yield return new SliderOption(
                "contrast", "Contrast", 0.1, 4.0, _effect.Contrast,
                v => _effect.Contrast = v,
                Order: 9, Step: 0.1, Tooltip: "Brightness contrast curve (1.0 = linear)");

            yield return new ToggleOption(
                "invert", "Invert", _effect.Invert,
                v => _effect.Invert = v,
                Order: 10, Tooltip: "Invert brightness mapping (dark areas use bright characters)");

            yield return new ToggleOption(
                "applyOnAlpha", "Apply on Transparent", _effect.ApplyOnAlpha,
                v => _effect.ApplyOnAlpha = v,
                Order: 11, Tooltip: "Include transparent pixels in brightness calculations");
        }
    }
}
