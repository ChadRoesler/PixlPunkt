namespace PixlPunkt.Uno.Core.Effects.Builders
{
    /// <summary>
    /// Extension methods for <see cref="IEffectRegistry"/> providing fluent effect builder access.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These extension methods provide the entry points to the fluent builder API, making
    /// effect registration more readable and less error-prone than direct constructor calls.
    /// </para>
    /// <para>
    /// <strong>Before (verbose):</strong>
    /// <code>
    /// registry.Register(new BuiltInEffectRegistration(
    ///     EffectIds.DropShadow,
    ///     EffectCategory.Stylize,
    ///     "Drop Shadow",
    ///     "Adds a shadow beneath the layer...",
    ///     () => new DropShadowEffect(),
    ///     effect => effect is DropShadowEffect e ? new DropShadowEffectSettings(e) : null
    /// ));
    /// </code>
    /// </para>
    /// <para>
    /// <strong>After (fluent):</strong>
    /// <code>
    /// registry.AddEffect(EffectIds.DropShadow)
    ///     .WithCategory(EffectCategory.Stylize)
    ///     .WithDisplayName("Drop Shadow")
    ///     .WithDescription("Adds a shadow beneath the layer...")
    ///     .WithFactory(() => new DropShadowEffect())
    ///     .WithSettings(e => e is DropShadowEffect ds ? new DropShadowEffectSettings(ds) : null)
    ///     .Register();
    /// </code>
    /// </para>
    /// </remarks>
    public static class EffectBuilderExtensions
    {
        /// <summary>
        /// Starts building an effect registration.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique effect identifier.</param>
        /// <returns>A fluent builder for configuring the effect.</returns>
        /// <example>
        /// <code>
        /// registry.AddEffect("com.myplugin.effect.halftone")
        ///     .WithCategory(EffectCategory.Stylize)
        ///     .WithDisplayName("Halftone")
        ///     .WithDescription("Creates a halftone dot pattern")
        ///     .WithFactory&lt;HalftoneEffect&gt;()
        ///     .WithSettings&lt;HalftoneEffect, HalftoneSettings&gt;()
        ///     .Register();
        /// </code>
        /// </example>
        public static EffectBuilder AddEffect(this IEffectRegistry registry, string id)
            => new(registry, id);
    }
}
