using PixlPunkt.PluginSdk.Compositing;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.Effects
{
    /// <summary>
    /// Registration record for a layer effect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Prefer using <see cref="Builders.EffectBuilders.Effect"/> for a fluent API.</strong>
    /// </para>
    /// <para>
    /// Layer effects apply visual transformations to layer pixels. The host application calls
    /// <see cref="EffectFactory"/> to create instances and <see cref="OptionsFactory"/> to generate settings UI.
    /// </para>
    /// </remarks>
    public sealed record EffectRegistration(
        string Id,
        EffectCategory Category,
        string DisplayName,
        string Description,
        Func<LayerEffectBase> EffectFactory,
        Func<LayerEffectBase, IEnumerable<IToolOption>> OptionsFactory
    ) : IEffectRegistration
    {
        /// <inheritdoc/>
        public LayerEffectBase CreateInstance()
        {
            var effect = EffectFactory();
            effect.EffectId = Id;
            return effect;
        }

        /// <inheritdoc/>
        public IEnumerable<IToolOption> GetOptions(LayerEffectBase effect)
        {
            return OptionsFactory(effect);
        }
    }
}
