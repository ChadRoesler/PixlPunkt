using System.Collections.Generic;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Compositing.Effects;

namespace PixlPunkt.Uno.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="VignetteEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring vignette parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Strength</strong> - Maximum darkening at corners (0 to 1)</item>
    /// <item><strong>Radius</strong> - Inner radius where darkening begins (0 to max)</item>
    /// <item><strong>Softness</strong> - Feather width of transition zone (0 to 1)</item>
    /// <item><strong>Color</strong> - Vignette tint color</item>
    /// <item><strong>Apply on Transparent</strong> - Apply vignette over transparent pixels</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="VignetteEffect"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class VignetteEffectSettings : EffectSettingsBase
    {
        private readonly VignetteEffect _effect;

        /// <summary>
        /// Creates settings for the specified vignette effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public VignetteEffectSettings(VignetteEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "Vignette";

        /// <inheritdoc/>
        public override string Description => "Darkens the edges of the layer to draw focus to the center.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption(
                "strength", "Strength", 0, 1, _effect.Strength,
                v => _effect.Strength = v,
                Order: 0, Step: 0.05, Tooltip: "Maximum darkening at corners (0 = none, 1 = full tint)");

            yield return new SliderOption(
                "radius", "Radius", EffectLimits.MinVignetteRadius, EffectLimits.MaxVignetteRadius, _effect.Radius,
                v => _effect.Radius = v,
                Order: 1, Step: 0.05, Tooltip: "Inner radius where darkening begins (0 = from center, 1 = only corners)");

            yield return new SliderOption(
                "softness", "Softness", EffectLimits.MinVignetteSoftness, EffectLimits.MaxVignetteSoftness, _effect.Softness,
                v => _effect.Softness = v,
                Order: 2, Step: 0.05, Tooltip: "Feather width of transition zone");

            yield return new ColorOption(
                "color", "Color", _effect.Color,
                v => _effect.Color = v,
                null, Order: 3, Tooltip: "Vignette tint color");

            yield return new ToggleOption(
                "applyOnAlpha", "Apply on Transparent", _effect.ApplyOnAlpha,
                v => _effect.ApplyOnAlpha = v,
                Order: 4, Tooltip: "Apply vignette over transparent pixels");
        }
    }
}
