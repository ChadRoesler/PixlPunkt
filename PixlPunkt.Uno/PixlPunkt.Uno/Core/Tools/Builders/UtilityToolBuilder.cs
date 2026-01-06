using System;
using PixlPunkt.Uno.Core.Tools.Utility;

namespace PixlPunkt.Uno.Core.Tools.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="UtilityToolRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UtilityToolBuilder provides a fluent API for registering utility-category tools
    /// that perform viewport or state operations without modifying pixel data.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// registry.AddUtilityTool(ToolIds.Pan)
    ///     .WithDisplayName("Pan")
    ///     .WithSettings(toolState.Pan)
    ///     .WithHandler(ctx => new PanHandler(ctx))
    ///     .Register();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class UtilityToolBuilder : ToolBuilderBase<UtilityToolBuilder, UtilityToolRegistration>
    {
        private Func<IUtilityContext, IUtilityHandler>? _handlerFactory;

        /// <summary>
        /// Initializes a new utility tool builder.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        public UtilityToolBuilder(IToolRegistry registry, string id)
            : base(registry, id, ToolCategory.Utility)
        {
        }

        /// <summary>
        /// Sets the handler factory for creating utility handler instances.
        /// </summary>
        /// <param name="factory">Factory function that creates a new handler instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public UtilityToolBuilder WithHandler(Func<IUtilityContext, IUtilityHandler> factory)
        {
            _handlerFactory = factory;
            return this;
        }

        /// <summary>
        /// Sets the handler factory using a type parameter for handlers with context-only constructor.
        /// </summary>
        /// <typeparam name="THandler">The handler type (must have constructor accepting IUtilityContext).</typeparam>
        /// <returns>This builder for fluent chaining.</returns>
        public UtilityToolBuilder WithHandler<THandler>() where THandler : IUtilityHandler
        {
            _handlerFactory = ctx => (IUtilityHandler)Activator.CreateInstance(typeof(THandler), ctx)!;
            return this;
        }

        /// <inheritdoc/>
        public override UtilityToolRegistration Build()
        {
            return new UtilityToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                HandlerFactory: _handlerFactory
            );
        }
    }
}
