using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Utility;

namespace PixlPunkt.PluginSdk.Tools.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="UtilityToolRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UtilityToolBuilder provides a fluent API for registering utility tools
    /// that manipulate the viewport or query state using <see cref="IUtilityHandler"/>.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// var registration = ToolBuilders.UtilityTool("myplugin.utility.info")
    ///     .WithDisplayName("Info Tool")
    ///     .WithSettings(infoSettings)
    ///     .WithHandler(ctx => new InfoToolHandler(ctx, infoSettings))
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class UtilityToolBuilder : ToolBuilderBase<UtilityToolBuilder, UtilityToolRegistration>
    {
        private Func<IUtilityContext, IUtilityHandler>? _handlerFactory;

        /// <summary>
        /// Initializes a new utility tool builder.
        /// </summary>
        /// <param name="id">The unique tool identifier.</param>
        public UtilityToolBuilder(string id)
            : base(id, ToolCategory.Utility)
        {
        }

        /// <summary>
        /// Sets the utility handler factory.
        /// </summary>
        /// <param name="factory">Factory function that creates a new handler given a context.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UtilityToolBuilder WithHandler(Func<IUtilityContext, IUtilityHandler> factory)
        {
            _handlerFactory = factory;
            return this;
        }

        /// <inheritdoc/>
        public override UtilityToolRegistration Build()
        {
            if (_handlerFactory == null)
                throw new InvalidOperationException("Utility tool requires a handler. Call WithHandler() before Build().");

            // Validate shortcut conflicts
            ValidateShortcut();

            return new UtilityToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                UtilityHandlerFactory: _handlerFactory
            );
        }
    }
}
