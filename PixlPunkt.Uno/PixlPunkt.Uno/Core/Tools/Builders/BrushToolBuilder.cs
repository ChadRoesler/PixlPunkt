using System;
using PixlPunkt.Uno.Core.Painting;

namespace PixlPunkt.Uno.Core.Tools.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="BrushToolRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BrushToolBuilder provides a fluent API for registering brush-category tools
    /// that paint strokes on the canvas using <see cref="IStrokePainter"/>.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// registry.AddBrushTool(ToolIds.Brush)
    ///     .WithDisplayName("Brush")
    ///     .WithSettings(toolState.BrushTool)
    ///     .WithPainter(() => new BrushPainter(toolState.BrushTool))
    ///     .Register();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class BrushToolBuilder : ToolBuilderBase<BrushToolBuilder, BrushToolRegistration>
    {
        private Func<IStrokePainter>? _painterFactory;

        /// <summary>
        /// Initializes a new brush tool builder.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        public BrushToolBuilder(IToolRegistry registry, string id)
            : base(registry, id, ToolCategory.Brush)
        {
        }

        /// <summary>
        /// Sets the painter factory for creating stroke painters.
        /// </summary>
        /// <param name="factory">Factory function that creates a new painter instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public BrushToolBuilder WithPainter(Func<IStrokePainter> factory)
        {
            _painterFactory = factory;
            return this;
        }

        /// <summary>
        /// Sets the painter factory using a type parameter for convenience.
        /// </summary>
        /// <typeparam name="TPainter">The painter type to create (must have parameterless constructor).</typeparam>
        /// <returns>This builder for fluent chaining.</returns>
        public BrushToolBuilder WithPainter<TPainter>() where TPainter : IStrokePainter, new()
        {
            _painterFactory = () => new TPainter();
            return this;
        }

        /// <inheritdoc/>
        public override BrushToolRegistration Build()
        {
            return new BrushToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                PainterFactory: _painterFactory
            );
        }
    }
}
