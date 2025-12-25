using PixlPunkt.PluginSdk.Compositing;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.Effects
{
    /// <summary>
    /// Interface for effect registrations in the unified effect registry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IEffectRegistration"/> provides a common contract for registering layer effects.
    /// Both built-in and plugin effects implement this interface.
    /// </para>
    /// <para>
    /// Each registration provides:
    /// </para>
    /// <list type="bullet">
    /// <item>A unique string ID for identification</item>
    /// <item>Display metadata (name, description)</item>
    /// <item>A factory method to create effect instances</item>
    /// <item>Settings options for dynamic UI generation</item>
    /// </list>
    /// </remarks>
    public interface IEffectRegistration
    {
        /// <summary>
        /// Gets the unique identifier for this effect.
        /// </summary>
        /// <value>
        /// A string following the convention <c>{vendor}.effect.{name}</c>.
        /// For example: <c>"pixlpunkt.effect.dropshadow"</c> or <c>"com.plugin.effect.halftone"</c>.
        /// </value>
        string Id { get; }

        /// <summary>
        /// Gets the category for UI grouping.
        /// </summary>
        EffectCategory Category { get; }

        /// <summary>
        /// Gets the human-readable display name for UI.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets a brief description of what this effect does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Creates a new instance of this effect.
        /// </summary>
        /// <returns>A new <see cref="LayerEffectBase"/> instance configured with default settings.</returns>
        LayerEffectBase CreateInstance();

        /// <summary>
        /// Gets the settings options for this effect for dynamic UI generation.
        /// </summary>
        /// <param name="effect">The effect instance to get options for.</param>
        /// <returns>Enumerable of <see cref="IToolOption"/> descriptors for the effect's settings.</returns>
        IEnumerable<IToolOption> GetOptions(LayerEffectBase effect);
    }
}
