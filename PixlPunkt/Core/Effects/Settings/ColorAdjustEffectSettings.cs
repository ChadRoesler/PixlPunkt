using System.Collections.Generic;
using PixlPunkt.Constants;
using PixlPunkt.Core.Compositing.Effects;

namespace PixlPunkt.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="ColorAdjustEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring color adjustment parameters in HSV color space:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Hue Shift</strong> - Rotates colors around the color wheel (0° to 360°)</item>
    /// <item><strong>Saturation</strong> - Color intensity multiplier (0 = grayscale, 1 = normal, 2 = oversaturated)</item>
    /// <item><strong>Brightness</strong> - Value/brightness multiplier (0 = black, 1 = normal, 2 = overbright)</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="ColorAdjustEffect"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class ColorAdjustEffectSettings : EffectSettingsBase
    {
        private readonly ColorAdjustEffect _effect;

        /// <summary>
        /// Creates settings for the specified color adjust effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public ColorAdjustEffectSettings(ColorAdjustEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "Color Adjust";

        /// <inheritdoc/>
        public override string Description => "Adjusts hue, saturation, and brightness in HSV color space.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Convert hue shift (-180 to 180) to absolute hue (0-360) for the slider
            // Then convert back when setting
            double currentHue = (_effect.HueShiftDegrees + 360) % 360;

            yield return new HueSliderOption(
                "hue", "Hue Shift", currentHue,
                v => _effect.HueShiftDegrees = v > EffectLimits.MaxHueShift ? v - 360 : v,
                Order: 0, Tooltip: "Rotate colors on the color wheel", Width: 200);

            yield return new SliderOption(
                "saturation", "Saturation", 0, 2, _effect.SaturationScale,
                v => _effect.SaturationScale = v,
                Order: 1, Step: 0.05, Tooltip: "Color intensity (0 = grayscale, 1 = normal, 2 = oversaturated)");

            yield return new SliderOption(
                "value", "Brightness", 0, 2, _effect.ValueScale,
                v => _effect.ValueScale = v,
                Order: 2, Step: 0.05, Tooltip: "Brightness multiplier (0 = black, 1 = normal)");
        }
    }
}
