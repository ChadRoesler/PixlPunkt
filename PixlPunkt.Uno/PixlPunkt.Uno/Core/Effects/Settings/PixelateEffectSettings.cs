using System.Collections.Generic;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Compositing.Effects;

namespace PixlPunkt.Uno.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="PixelateEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring pixelate/mosaic parameters:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Block Size</strong> - Size of each pixel block</item>
    /// </list>
    /// <para>
    /// The effect averages all pixels within each block and fills the block with
    /// the average color, creating a chunky pixelated appearance.
    /// </para>
    /// </remarks>
    /// <seealso cref="PixelateEffect"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class PixelateEffectSettings : EffectSettingsBase
    {
        private readonly PixelateEffect _effect;

        /// <summary>
        /// Creates settings for the specified pixelate effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public PixelateEffectSettings(PixelateEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "Pixelate";

        /// <inheritdoc/>
        public override string Description => "Reduces resolution to create a pixelated mosaic effect.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new SliderOption(
                "blockSize", "Block Size", EffectLimits.MinBlockSize, EffectLimits.MaxBlockSize, _effect.BlockSize,
                v => _effect.BlockSize = (int)v,
                Order: 0, Tooltip: "Size of each pixel block (1 = no effect, higher = more pixelated)");
        }
    }
}
