using System.Collections.Generic;
using PixlPunkt.Constants;
using PixlPunkt.Core.Compositing.Effects;

namespace PixlPunkt.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="DropShadowEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring drop shadow parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Offset X/Y</strong> - Shadow displacement from the layer</item>
    /// <item><strong>Opacity</strong> - Shadow transparency (0 to 1)</item>
    /// <item><strong>Blur</strong> - Shadow blur radius</item>
    /// <item><strong>Color</strong> - Shadow tint color</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="DropShadowEffect"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class DropShadowEffectSettings : EffectSettingsBase
    {
        private readonly DropShadowEffect _effect;

        /// <summary>
        /// Creates settings for the specified drop shadow effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public DropShadowEffectSettings(DropShadowEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "Drop Shadow";

        /// <inheritdoc/>
        public override string Description => "Adds a shadow beneath the layer with configurable offset and blur.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption(
                "offsetX", "Offset X", EffectLimits.MinOffset, EffectLimits.MaxOffset, _effect.OffsetX,
                v => _effect.OffsetX = (int)v,
                Order: 0, Tooltip: "Horizontal shadow offset in pixels");

            yield return new SliderOption(
                "offsetY", "Offset Y", EffectLimits.MinOffset, EffectLimits.MaxOffset, _effect.OffsetY,
                v => _effect.OffsetY = (int)v,
                Order: 1, Tooltip: "Vertical shadow offset in pixels");

            yield return new SliderOption(
                "opacity", "Opacity", 0, 1, _effect.Opacity,
                v => _effect.Opacity = v,
                Order: 2, Step: 0.05, Tooltip: "Shadow opacity (0 = invisible, 1 = fully opaque)");

            yield return new SliderOption(
                "blur", "Blur", EffectLimits.MinRadius, EffectLimits.MaxRadius, _effect.BlurRadius,
                v => _effect.BlurRadius = (int)v,
                Order: 3, Tooltip: "Shadow blur radius in pixels");

            yield return new ColorOption(
                "color", "Color", _effect.Color,
                v => _effect.Color = v,
                null, Order: 4, Tooltip: "Shadow tint color");
        }
    }
}
