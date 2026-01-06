using System.Collections.Generic;
using PixlPunkt.Uno.Core.Compositing.Effects;

namespace PixlPunkt.Uno.Core.Effects.Settings
{
    /// <summary>
    /// Settings provider for <see cref="PaletteQuantizeEffect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates UI options for configuring palette quantization, which reduces
    /// the layer's colors to match a specific palette.
    /// </para>
    /// <para>
    /// The palette editor supports:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Add Color</strong> - Add individual colors to the palette</item>
    /// <item><strong>Add Ramp</strong> - Generate a gradient between two colors</item>
    /// <item><strong>Edit Color</strong> - Modify existing palette colors</item>
    /// <item><strong>Remove Color</strong> - Delete colors from the palette</item>
    /// <item><strong>Reverse</strong> - Reverse the palette order</item>
    /// <item><strong>Clear</strong> - Remove all colors from the palette</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="PaletteQuantizeEffect"/>
    /// <seealso cref="PaletteOption"/>
    /// <seealso cref="EffectSettingsBase"/>
    public sealed class PaletteQuantizeEffectSettings : EffectSettingsBase
    {
        private readonly PaletteQuantizeEffect _effect;

        /// <summary>
        /// Creates settings for the specified palette quantize effect instance.
        /// </summary>
        /// <param name="effect">The effect instance to configure.</param>
        public PaletteQuantizeEffectSettings(PaletteQuantizeEffect effect)
        {
            _effect = effect;
        }

        /// <inheritdoc/>
        public override string DisplayName => "Palette Quantize";

        /// <inheritdoc/>
        public override string Description => "Reduces colors to match a specific palette using quantization.";

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new PaletteOption(
                "colors",
                "Palette",
                _effect.Colors,
                SelectedIndex: -1,
                OnSelectionChanged: null,
                OnAddRequested: () => _effect.Colors.Add(0xFF000000),
                OnAddRampRequested: null,  // Handled by ToolOptionFactory
                OnEditRequested: null,     // Handled by ToolOptionFactory
                OnRemoveRequested: idx =>
                {
                    if (idx >= 0 && idx < _effect.Colors.Count)
                        _effect.Colors.RemoveAt(idx);
                },
                OnClearRequested: () => _effect.Colors.Clear(),
                OnReverseRequested: null,  // Handled by ToolOptionFactory
                OnMoveRequested: null,
                Order: 0,
                Tooltip: "Colors to quantize the layer to (empty = no effect)");
        }
    }
}
