using System.Collections.Generic;
using PixlPunkt.Constants;
using PixlPunkt.Core.Compositing.Effects;

namespace PixlPunkt.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="GrainEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring film grain parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Amount</strong> - Noise intensity (0 to 100%)</item>
    /// <item><strong>Monochrome</strong> - Use grayscale noise instead of colored</item>
    /// <item><strong>Seed</strong> - Random seed for noise pattern</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="GrainEffect"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class GrainEffectSettings : EffectSettingsBase
    {
        private readonly GrainEffect _effect;

        /// <summary>
        /// Creates settings for the specified grain effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public GrainEffectSettings(GrainEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "Grain";

        /// <inheritdoc/>
        public override string Description => "Adds film grain or noise texture to the layer.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption(
                "amount", "Amount", 0, 1, _effect.Amount,
                v => _effect.Amount = v,
                Order: 0, Step: 0.05, Tooltip: "Noise intensity (0 = no grain, 1 = maximum)");

            yield return new ToggleOption(
                "monochrome", "Monochrome", _effect.Monochrome,
                v => _effect.Monochrome = v,
                Order: 1, Tooltip: "Use grayscale noise instead of colored noise");

            yield return new SliderOption(
                "seed", "Seed", 1, EffectLimits.MaxIntensityPercent, _effect.Seed,
                v => _effect.Seed = (int)v,
                Order: 2, Tooltip: "Random seed for noise pattern generation");
        }
    }
}
