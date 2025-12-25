using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Shapes;

namespace PixlPunkt.PluginSdk.Tools.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="ShapeToolRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ShapeToolBuilder provides a fluent API for registering shape tools
    /// that render geometric primitives using <see cref="IShapeBuilder"/>.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// var registration = ToolBuilders.ShapeTool("myplugin.shape.star")
    ///     .WithDisplayName("Star")
    ///     .WithSettings(starSettings)
    ///     .WithShapeBuilder(() => new StarShapeBuilder(starSettings))
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class ShapeToolBuilder : ToolBuilderBase<ShapeToolBuilder, ShapeToolRegistration>
    {
        private Func<IShapeBuilder>? _shapeBuilderFactory;

        /// <summary>
        /// Initializes a new shape tool builder.
        /// </summary>
        /// <param name="id">The unique tool identifier.</param>
        public ShapeToolBuilder(string id)
            : base(id, ToolCategory.Shape)
        {
        }

        /// <summary>
        /// Sets the shape builder factory.
        /// </summary>
        /// <param name="factory">Factory function that creates a new shape builder instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public ShapeToolBuilder WithShapeBuilder(Func<IShapeBuilder> factory)
        {
            _shapeBuilderFactory = factory;
            return this;
        }

        /// <summary>
        /// Sets the shape builder using an existing instance.
        /// </summary>
        /// <param name="builder">The shape builder instance to use.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public ShapeToolBuilder WithShapeBuilder(IShapeBuilder builder)
        {
            _shapeBuilderFactory = () => builder;
            return this;
        }

        /// <summary>
        /// Sets the shape builder using a type parameter for convenience.
        /// </summary>
        /// <typeparam name="TBuilder">The builder type to create (must have parameterless constructor).</typeparam>
        /// <returns>This builder for fluent chaining.</returns>
        public ShapeToolBuilder WithShapeBuilder<TBuilder>() where TBuilder : IShapeBuilder, new()
        {
            _shapeBuilderFactory = () => new TBuilder();
            return this;
        }

        /// <inheritdoc/>
        public override ShapeToolRegistration Build()
        {
            if (_shapeBuilderFactory == null)
                throw new InvalidOperationException("Shape tool requires a shape builder. Call WithShapeBuilder() before Build().");

            // Validate shortcut conflicts
            ValidateShortcut();

            return new ShapeToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                ShapeBuilderFactory: _shapeBuilderFactory
            );
        }
    }
}
