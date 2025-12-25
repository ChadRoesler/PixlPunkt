using PixlPunkt.PluginSdk.Compositing;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.Effects.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="EffectRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EffectBuilder provides a fluent API for registering layer effects
    /// with reduced boilerplate and better discoverability.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// var registration = EffectBuilders.Effect("myplugin.effect.halftone")
    ///     .WithDisplayName("Halftone")
    ///     .WithDescription("Creates a halftone dot pattern effect")
    ///     .WithCategory(EffectCategory.Filter)
    ///     .WithFactory(() => new HalftoneEffect())
    ///     .WithOptions(effect => ((HalftoneEffect)effect).GetOptions())
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class EffectBuilder
    {
        private readonly string _id;
        private string? _displayName;
        private string _description = string.Empty;
        private EffectCategory _category = EffectCategory.Filter;
        private Func<LayerEffectBase>? _effectFactory;
        private Func<LayerEffectBase, IEnumerable<IToolOption>>? _optionsFactory;

        /// <summary>
        /// Initializes a new effect builder.
        /// </summary>
        /// <param name="id">The unique effect identifier.</param>
        public EffectBuilder(string id)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
        }

        /// <summary>
        /// Sets the human-readable display name for the effect.
        /// </summary>
        /// <param name="displayName">The display name shown in UI.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithDisplayName(string displayName)
        {
            _displayName = displayName;
            return this;
        }

        /// <summary>
        /// Sets the description of what the effect does.
        /// </summary>
        /// <param name="description">A brief description for tooltips and help.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithDescription(string description)
        {
            _description = description ?? string.Empty;
            return this;
        }

        /// <summary>
        /// Sets the category for UI grouping.
        /// </summary>
        /// <param name="category">The effect category.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithCategory(EffectCategory category)
        {
            _category = category;
            return this;
        }

        /// <summary>
        /// Sets the factory function that creates effect instances.
        /// </summary>
        /// <param name="factory">Factory function that creates a new effect instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithFactory(Func<LayerEffectBase> factory)
        {
            _effectFactory = factory;
            return this;
        }

        /// <summary>
        /// Sets the factory function using a type parameter for convenience.
        /// </summary>
        /// <typeparam name="TEffect">The effect type to create (must have parameterless constructor).</typeparam>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithFactory<TEffect>() where TEffect : LayerEffectBase, new()
        {
            _effectFactory = () => new TEffect();
            return this;
        }

        /// <summary>
        /// Sets the options factory for generating settings UI.
        /// </summary>
        /// <param name="optionsFactory">Factory function that returns options for an effect instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithOptions(Func<LayerEffectBase, IEnumerable<IToolOption>> optionsFactory)
        {
            _optionsFactory = optionsFactory;
            return this;
        }

        /// <summary>
        /// Sets the options factory using a typed delegate for convenience.
        /// </summary>
        /// <typeparam name="TEffect">The effect type for type-safe options access.</typeparam>
        /// <param name="optionsFactory">Factory function that returns options for a typed effect instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithOptions<TEffect>(Func<TEffect, IEnumerable<IToolOption>> optionsFactory)
            where TEffect : LayerEffectBase
        {
            _optionsFactory = effect => optionsFactory((TEffect)effect);
            return this;
        }

        /// <summary>
        /// Sets options to empty (effect has no configurable options).
        /// </summary>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithNoOptions()
        {
            _optionsFactory = _ => [];
            return this;
        }

        /// <summary>
        /// Builds the effect registration.
        /// </summary>
        /// <returns>The built registration.</returns>
        /// <exception cref="InvalidOperationException">Thrown if required fields are not set.</exception>
        public EffectRegistration Build()
        {
            if (_effectFactory == null)
                throw new InvalidOperationException("Effect requires a factory. Call WithFactory() before Build().");

            if (_optionsFactory == null)
                throw new InvalidOperationException("Effect requires an options factory. Call WithOptions() or WithNoOptions() before Build().");

            return new EffectRegistration(
                Id: _id,
                Category: _category,
                DisplayName: _displayName ?? ExtractNameFromId(_id),
                Description: _description,
                EffectFactory: _effectFactory,
                OptionsFactory: _optionsFactory
            );
        }

        /// <summary>
        /// Extracts a display name from an effect ID (takes last segment after dot).
        /// </summary>
        private static string ExtractNameFromId(string id)
        {
            var lastDot = id.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < id.Length - 1)
            {
                var name = id[(lastDot + 1)..];
                return char.ToUpperInvariant(name[0]) + name[1..];
            }
            return id;
        }
    }

    /// <summary>
    /// Static factory for creating effect builders with a fluent API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="EffectBuilders"/> provides the entry point to the fluent builder API
    /// for layer effects, consistent with the tool builder pattern.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// public IEnumerable&lt;IEffectRegistration&gt; GetEffectRegistrations()
    /// {
    ///     yield return EffectBuilders.Effect("myplugin.effect.halftone")
    ///         .WithDisplayName("Halftone")
    ///         .WithDescription("Creates a halftone dot pattern effect")
    ///         .WithCategory(EffectCategory.Filter)
    ///         .WithFactory&lt;HalftoneEffect&gt;()
    ///         .WithOptions&lt;HalftoneEffect&gt;(e => e.GetOptions())
    ///         .Build();
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static class EffectBuilders
    {
        /// <summary>
        /// Starts building an effect registration.
        /// </summary>
        /// <param name="id">The unique effect identifier (e.g., "myplugin.effect.halftone").</param>
        /// <returns>A fluent builder for configuring the effect.</returns>
        public static EffectBuilder Effect(string id) => new(id);
    }
}
