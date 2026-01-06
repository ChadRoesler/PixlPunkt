using System.Collections.Generic;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Compositing.Effects;

namespace PixlPunkt.Uno.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="OutlineEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring outline parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Thickness</strong> - Outline width in pixels</item>
    /// <item><strong>Color</strong> - Outline tint color</item>
    /// <item><strong>Outside Only</strong> - Only draw outline outside the shape</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="OutlineEffect"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class OutlineEffectSettings : EffectSettingsBase
    {
        private readonly OutlineEffect _effect;

        /// <summary>
        /// Creates settings for the specified outline effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public OutlineEffectSettings(OutlineEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "Outline";

        /// <inheritdoc/>
        public override string Description => "Draws a colored outline around the opaque regions of the layer.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption(
                "thickness", "Thickness", EffectLimits.MinThickness, EffectLimits.MaxThickness, _effect.Thickness,
                v => _effect.Thickness = (int)v,
                Order: 0, Tooltip: "Outline width in pixels");

            yield return new ColorOption(
                "color", "Color", _effect.Color,
                v => _effect.Color = v,
                null, Order: 1, Tooltip: "Outline color");

            yield return new ToggleOption(
                "outsideOnly", "Outside Only", _effect.OutsideOnly,
                v => _effect.OutsideOnly = v,
                Order: 2, Tooltip: "Only draw outline outside the shape");
        }
    }
}
