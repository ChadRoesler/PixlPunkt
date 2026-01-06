using System;
using PixlPunkt.Uno.Core.Painting;

namespace PixlPunkt.Uno.Core.Tools.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="ShapeToolRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ShapeToolBuilder provides a fluent API for registering shape-category tools
    /// that draw geometric primitives. Supports both shape-based tools (Rectangle, Ellipse)
    /// and line-based tools (Gradient) that use stroke painters.
    /// </para>
    /// <para>
    /// <strong>Shape Tool Example:</strong>
    /// <code>
    /// registry.AddShapeTool(ToolIds.ShapeRect)
    ///     .WithDisplayName("Rectangle")
    ///     .WithSettings(toolState.Rect)
    ///     .WithShapeBuilder(new RectangleShapeBuilder())
    ///     .Register();
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Line Tool Example (Gradient):</strong>
    /// <code>
    /// registry.AddShapeTool(ToolIds.Gradient)
    ///     .WithDisplayName("Gradient")
    ///     .WithSettings(toolState.Gradient)
    ///     .WithPainter(() => new GradientPainter())
    ///     .Register();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class ShapeToolBuilder : ToolBuilderBase<ShapeToolBuilder, ShapeToolRegistration>
    {
        private IShapeBuilder? _shapeBuilder;
        private IShapeRenderer? _renderer;
        private Func<IStrokePainter>? _painterFactory;

        /// <summary>
        /// Initializes a new shape tool builder.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        public ShapeToolBuilder(IToolRegistry registry, string id)
            : base(registry, id, ToolCategory.Shape)
        {
        }

        /// <summary>
        /// Sets the shape builder for geometry generation.
        /// </summary>
        /// <param name="builder">The shape builder instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public ShapeToolBuilder WithShapeBuilder(IShapeBuilder builder)
        {
            _shapeBuilder = builder;
            return this;
        }

        /// <summary>
        /// Sets the shape builder using a type parameter for convenience.
        /// </summary>
        /// <typeparam name="TBuilder">The builder type (must have parameterless constructor).</typeparam>
        /// <returns>This builder for fluent chaining.</returns>
        public ShapeToolBuilder WithShapeBuilder<TBuilder>() where TBuilder : IShapeBuilder, new()
        {
            _shapeBuilder = new TBuilder();
            return this;
        }

        /// <summary>
        /// Sets a custom shape renderer (optional - uses default if not specified).
        /// </summary>
        /// <param name="renderer">The renderer instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public ShapeToolBuilder WithRenderer(IShapeRenderer renderer)
        {
            _renderer = renderer;
            return this;
        }

        /// <summary>
        /// Sets the painter factory for line-based shape tools (e.g., Gradient).
        /// </summary>
        /// <param name="factory">Factory function that creates a new painter instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public ShapeToolBuilder WithPainter(Func<IStrokePainter> factory)
        {
            _painterFactory = factory;
            return this;
        }

        /// <inheritdoc/>
        public override ShapeToolRegistration Build()
        {
            return new ShapeToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                ShapeBuilder: _shapeBuilder,
                Renderer: _renderer,
                PainterFactory: _painterFactory
            );
        }
    }
}
