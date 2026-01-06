using System.Collections.Generic;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Compositing.Effects;

namespace PixlPunkt.Uno.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="CrtEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring CRT simulation parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Curvature</strong> - Barrel distortion strength (0 to 1)</item>
    /// <item><strong>Strength</strong> - Corner darkening intensity (0 to 1)</item>
    /// <item><strong>Color</strong> - Corner tint color</item>
    /// <item><strong>Apply on Transparent</strong> - Apply effect over transparent pixels</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="CrtEffect"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class CrtEffectSettings : EffectSettingsBase
    {
        private readonly CrtEffect _effect;

        /// <summary>
        /// Creates settings for the specified CRT effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public CrtEffectSettings(CrtEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "CRT";

        /// <inheritdoc/>
        public override string Description => "Simulates a cathode ray tube monitor with curvature and scan effects.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption(
                "curvature", "Curvature", EffectLimits.MinCurvature, EffectLimits.MaxCurvature, _effect.Curvature,
                v => _effect.Curvature = v,
                Order: 0, Step: 0.05, Tooltip: "Barrel distortion strength (0 = flat, 1 = maximum bulge)");

            yield return new SliderOption(
                "strength", "Strength", 0, 1, _effect.Strength,
                v => _effect.Strength = v,
                Order: 1, Step: 0.05, Tooltip: "Corner darkening intensity");

            yield return new ColorOption(
                "color", "Color", _effect.Color,
                v => _effect.Color = v,
                null, Order: 2, Tooltip: "Corner tint color");

            yield return new ToggleOption(
                "applyOnAlpha", "Apply on Transparent", _effect.ApplyOnAlpha,
                v => _effect.ApplyOnAlpha = v,
                Order: 3, Tooltip: "Apply effect over transparent pixels");
        }
    }
}
