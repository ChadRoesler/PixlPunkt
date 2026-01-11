using System;
using System.Collections.Generic;
using PixlPunkt.Core.Effects.Settings;

namespace PixlPunkt.Core.Effects.Builders
{
    /// <summary>
    /// Fluent builder for creating <see cref="IEffectRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EffectBuilder provides a fluent API for registering layer effects with
    /// reduced boilerplate. This is the recommended approach for plugin developers.
    /// </para>
    /// <para>
    /// <strong>Plugin Usage Example:</strong>
    /// <code>
    /// public IEnumerable&lt;IEffectRegistration&gt; GetEffectRegistrations()
    /// {
    ///     yield return EffectBuilder.Create("com.myplugin.effect.halftone")
    ///         .WithCategory(EffectCategory.Stylize)
    ///         .WithDisplayName("Halftone")
    ///         .WithDescription("Creates a halftone dot pattern effect")
    ///         .WithFactory(() => new HalftoneEffect())
    ///         .WithSettings(e => e is HalftoneEffect h ? new HalftoneSettings(h) : null)
    ///         .Build();
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Direct Registration Example:</strong>
    /// <code>
    /// registry.AddEffect("com.myplugin.effect.halftone")
    ///     .WithCategory(EffectCategory.Stylize)
    ///     .WithDisplayName("Halftone")
    ///     .WithDescription("Creates a halftone dot pattern effect")
    ///     .WithFactory(() => new HalftoneEffect())
    ///     .WithSettings(e => e is HalftoneEffect h ? new HalftoneSettings(h) : null)
    ///     .Register();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class EffectBuilder
    {
        private readonly IEffectRegistry? _registry;
        private readonly string _id;
        private EffectCategory _category = EffectCategory.Filter;
        private string? _displayName;
        private string _description = string.Empty;
        private Func<LayerEffectBase>? _factory;
        private Func<LayerEffectBase, EffectSettingsBase?>? _settingsFactory;

        /// <summary>
        /// Creates a new effect builder with the specified ID.
        /// </summary>
        /// <param name="id">The unique effect ID (e.g., "com.vendor.effect.name").</param>
        /// <returns>A new fluent builder instance.</returns>
        public static EffectBuilder Create(string id) => new(null, id);

        /// <summary>
        /// Creates a new effect builder associated with a registry.
        /// </summary>
        /// <param name="registry">The registry to register with (optional).</param>
        /// <param name="id">The unique effect ID.</param>
        public EffectBuilder(IEffectRegistry? registry, string id)
        {
            _registry = registry;
            _id = id ?? throw new ArgumentNullException(nameof(id));
        }

        /// <summary>
        /// Sets the effect category for UI grouping.
        /// </summary>
        /// <param name="category">The effect category.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithCategory(EffectCategory category)
        {
            _category = category;
            return this;
        }

        /// <summary>
        /// Sets the display name shown in the UI.
        /// </summary>
        /// <param name="displayName">The human-readable display name.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithDisplayName(string displayName)
        {
            _displayName = displayName;
            return this;
        }

        /// <summary>
        /// Sets the description for the effect.
        /// </summary>
        /// <param name="description">Brief description of what the effect does.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithDescription(string description)
        {
            _description = description ?? string.Empty;
            return this;
        }

        /// <summary>
        /// Sets the factory function for creating effect instances.
        /// </summary>
        /// <param name="factory">Factory that creates a new effect instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithFactory(Func<LayerEffectBase> factory)
        {
            _factory = factory;
            return this;
        }

        /// <summary>
        /// Sets the factory using a generic type parameter (for effects with parameterless constructors).
        /// </summary>
        /// <typeparam name="TEffect">The effect type to create.</typeparam>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithFactory<TEffect>() where TEffect : LayerEffectBase, new()
        {
            _factory = () => new TEffect();
            return this;
        }

        /// <summary>
        /// Sets the settings factory for creating settings UI options.
        /// </summary>
        /// <param name="settingsFactory">Factory that creates settings for an effect instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithSettings(Func<LayerEffectBase, EffectSettingsBase?> settingsFactory)
        {
            _settingsFactory = settingsFactory;
            return this;
        }

        /// <summary>
        /// Sets up settings using strongly-typed effect and settings types.
        /// </summary>
        /// <typeparam name="TEffect">The effect type.</typeparam>
        /// <typeparam name="TSettings">The settings type.</typeparam>
        /// <returns>This builder for fluent chaining.</returns>
        public EffectBuilder WithSettings<TEffect, TSettings>()
            where TEffect : LayerEffectBase
            where TSettings : EffectSettingsBase
        {
            _settingsFactory = effect => effect is TEffect e
                ? (EffectSettingsBase?)Activator.CreateInstance(typeof(TSettings), e)
                : null;
            return this;
        }

        /// <summary>
        /// Builds the registration without registering it.
        /// </summary>
        /// <returns>The built effect registration.</returns>
        /// <exception cref="InvalidOperationException">Thrown if required properties are not set.</exception>
        public IEffectRegistration Build()
        {
            if (_factory == null)
                throw new InvalidOperationException($"Effect factory is required for {_id}");

            return new BuiltEffectRegistration(
                _id,
                _category,
                _displayName ?? ExtractNameFromId(_id),
                _description,
                _factory,
                _settingsFactory ?? (_ => null)
            );
        }

        /// <summary>
        /// Builds and registers the effect with the associated registry.
        /// </summary>
        /// <returns>The built effect registration.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no registry is associated.</exception>
        public IEffectRegistration Register()
        {
            if (_registry == null)
                throw new InvalidOperationException("No registry associated with this builder. Use Build() instead or create builder via registry extension.");

            var registration = Build();
            _registry.Register(registration);
            return registration;
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
                // Capitalize first letter
                return char.ToUpperInvariant(name[0]) + name[1..];
            }
            return id;
        }

        /// <summary>
        /// Internal registration class used by the builder.
        /// </summary>
        private sealed class BuiltEffectRegistration : IEffectRegistration
        {
            private readonly Func<LayerEffectBase> _factory;
            private readonly Func<LayerEffectBase, EffectSettingsBase?> _settingsFactory;

            public string Id { get; }
            public EffectCategory Category { get; }
            public string DisplayName { get; }
            public string Description { get; }

            public BuiltEffectRegistration(
                string id,
                EffectCategory category,
                string displayName,
                string description,
                Func<LayerEffectBase> factory,
                Func<LayerEffectBase, EffectSettingsBase?> settingsFactory)
            {
                Id = id;
                Category = category;
                DisplayName = displayName;
                Description = description;
                _factory = factory;
                _settingsFactory = settingsFactory;
            }

            public LayerEffectBase CreateInstance()
            {
                var effect = _factory();
                effect.EffectId = Id;
                return effect;
            }

            public IEnumerable<IToolOption> GetOptions(LayerEffectBase effect)
            {
                var settings = _settingsFactory(effect);
                return settings?.GetOptions() ?? [];
            }
        }
    }
}
