using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Tile;

namespace PixlPunkt.PluginSdk.Tools.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="TileToolRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TileToolBuilder provides a fluent API for registering tile tools
    /// that perform tile-based editing operations using <see cref="ITileHandler"/>.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// var registration = ToolBuilders.TileTool("myplugin.tile.stamper")
    ///     .WithDisplayName("Tile Stamper")
    ///     .WithSettings(stamperSettings)
    ///     .WithHandler(ctx => new TileStamperHandler(ctx, stamperSettings))
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class TileToolBuilder : ToolBuilderBase<TileToolBuilder, TileToolRegistration>
    {
        private Func<ITileContext, ITileHandler>? _handlerFactory;

        /// <summary>
        /// Initializes a new tile tool builder.
        /// </summary>
        /// <param name="id">The unique tool identifier.</param>
        public TileToolBuilder(string id)
            : base(id, ToolCategory.Tile)
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
            if (_handlerFactory == null)
                throw new InvalidOperationException("Tile tool requires a handler. Call WithHandler() before Build().");

            // Validate shortcut conflicts
            ValidateShortcut();

            return new TileToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                TileHandlerFactory: _handlerFactory
            );
        }
    }
}
