using System.Collections.Generic;
using PixlPunkt.Constants;
using PixlPunkt.Core.Compositing.Effects;

namespace PixlPunkt.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="GlowBloomEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring glow/bloom parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Intensity</strong> - Glow brightness multiplier (0 to 2)</item>
    /// <item><strong>Radius</strong> - Glow blur radius</item>
    /// <item><strong>Threshold</strong> - Brightness threshold for bloom extraction (0 to 1)</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="GlowBloomEffect"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class GlowBloomEffectSettings : EffectSettingsBase
    {
        private readonly GlowBloomEffect _effect;

        /// <summary>
        /// Creates settings for the specified glow/bloom effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public GlowBloomEffectSettings(GlowBloomEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "Glow / Bloom";

        /// <inheritdoc/>
        public override string Description => "Adds a soft glow or bloom around bright areas of the layer.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption(
                "intensity", "Intensity", 0, 2, _effect.Intensity,
                v => _effect.Intensity = v,
                Order: 0, Step: 0.05, Tooltip: "Glow brightness multiplier");

            yield return new SliderOption(
                "radius", "Radius", EffectLimits.MinRadius, EffectLimits.MaxRadius, _effect.Radius,
                v => _effect.Radius = (int)v,
                Order: 1, Tooltip: "Glow blur radius in pixels");

            yield return new SliderOption(
                "threshold", "Threshold", 0, 1, _effect.Threshold,
                v => _effect.Threshold = v,
                Order: 2, Step: 0.05, Tooltip: "Brightness threshold for glow extraction (higher = only brightest pixels glow)");
        }
    }
}
