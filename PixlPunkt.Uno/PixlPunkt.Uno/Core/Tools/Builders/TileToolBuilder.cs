using System;
using PixlPunkt.PluginSdk.Tile;

namespace PixlPunkt.Uno.Core.Tools.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="TileToolRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TileToolBuilder provides a fluent API for registering tile-category tools
    /// that perform tile-based editing operations using <see cref="ITileHandler"/>.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// registry.AddTileTool(ToolIds.TileStamper)
    ///     .WithDisplayName("Tile Stamper")
    ///     .WithSettings(toolState.TileStamper)
    ///     .WithHandler(ctx => new TileStamperHandler(ctx, toolState.TileStamper))
    ///     .Register();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class TileToolBuilder : ToolBuilderBase<TileToolBuilder, TileToolRegistration>
    {
        private Func<ITileContext, ITileHandler>? _handlerFactory;

        /// <summary>
        /// Initializes a new tile tool builder.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        public TileToolBuilder(IToolRegistry registry, string id)
            : base(registry, id, ToolCategory.Tile)
        {
        }

        /// <summary>
        /// Sets the tile handler factory.
        /// </summary>
        /// <param name="factory">Factory function that creates a new handler given a context.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public TileToolBuilder WithHandler(Func<ITileContext, ITileHandler> factory)
        {
            _handlerFactory = factory;
            return this;
        }

        /// <inheritdoc/>
        public override TileToolRegistration Build()
        {
            return new TileToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                HandlerFactory: _handlerFactory
            );
        }
    }
}
