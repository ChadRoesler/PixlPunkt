using System.Collections.Generic;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Compositing.Effects;

namespace PixlPunkt.Uno.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="ChromaticAberrationEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring chromatic aberration parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Offset</strong> - Radial channel offset in pixels</item>
    /// <item><strong>Strength</strong> - Effect blend strength (0 to 1)</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="ChromaticAberrationEffect"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class ChromaticAberrationEffectSettings : EffectSettingsBase
    {
        private readonly ChromaticAberrationEffect _effect;

        /// <summary>
        /// Creates settings for the specified chromatic aberration effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public ChromaticAberrationEffectSettings(ChromaticAberrationEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "Chromatic Aberration";

        /// <inheritdoc/>
        public override string Description => "Simulates lens chromatic aberration by splitting color channels.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption(
                "offset", "Offset", EffectLimits.MinRadius, EffectLimits.MaxRadius, _effect.OffsetPixels,
                v => _effect.OffsetPixels = v,
                Order: 0, Step: 0.5, Tooltip: "Radial channel offset in pixels (red shifts outward, blue inward)");

            yield return new SliderOption(
                "strength", "Strength", 0, 1, _effect.Strength,
                v => _effect.Strength = v,
                Order: 1, Step: 0.05, Tooltip: "Blend strength between original and shifted channels");
        }
    }
}
