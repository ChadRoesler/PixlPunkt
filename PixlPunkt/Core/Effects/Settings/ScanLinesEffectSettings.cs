using System.Collections.Generic;
using PixlPunkt.Constants;
using PixlPunkt.Core.Compositing.Effects;

namespace PixlPunkt.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="ScanLinesEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring scan line parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Intensity</strong> - Line darkness/opacity (0 to 1)</item>
    /// <item><strong>Thickness</strong> - Line height in pixels</item>
    /// <item><strong>Spacing</strong> - Gap between lines in pixels</item>
    /// <item><strong>Color</strong> - Line tint color</item>
    /// <item><strong>Apply on Transparent</strong> - Draw lines over transparent pixels</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="ScanLinesEffect"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class ScanLinesEffectSettings : EffectSettingsBase
    {
        private readonly ScanLinesEffect _effect;

        /// <summary>
        /// Creates settings for the specified scan lines effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public ScanLinesEffectSettings(ScanLinesEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "Scan Lines";

        /// <inheritdoc/>
        public override string Description => "Adds horizontal scan lines to simulate CRT monitor appearance.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption(
                "intensity", "Intensity", 0, 1, _effect.Intensity,
                v => _effect.Intensity = v,
                Order: 0, Step: 0.05, Tooltip: "Line darkness (0 = invisible, 1 = fully opaque)");

            yield return new SliderOption(
                "thickness", "Thickness", EffectLimits.MinLineSpacing, EffectLimits.MaxBlockSize, _effect.LineThickness,
                v => _effect.LineThickness = (int)v,
                Order: 1, Tooltip: "Line height in pixels");

            yield return new SliderOption(
                "spacing", "Spacing", 0, EffectLimits.MaxBlockSize, _effect.LineSpacing,
                v => _effect.LineSpacing = (int)v,
                Order: 2, Tooltip: "Gap between lines in pixels");

            yield return new ColorOption(
                "color", "Color", _effect.Color,
                v => _effect.Color = v,
                null, Order: 3, Tooltip: "Line tint color");

            yield return new ToggleOption(
                "applyOnAlpha", "Apply on Transparent", _effect.ApplyOnAlpha,
                v => _effect.ApplyOnAlpha = v,
                Order: 4, Tooltip: "Draw lines over transparent pixels");
        }
    }
}
