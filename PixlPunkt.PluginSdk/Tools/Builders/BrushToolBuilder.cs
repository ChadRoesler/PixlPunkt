using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Painting;

namespace PixlPunkt.PluginSdk.Tools.Builders
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
    /// var registration = ToolBuilders.BrushTool("myplugin.brush.sparkle")
    ///     .WithDisplayName("Sparkle Brush")
    ///     .WithSettings(sparkleSettings)
    ///     .WithPainter(() => new SparklePainter(sparkleSettings))
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class BrushToolBuilder : ToolBuilderBase<BrushToolBuilder, BrushToolRegistration>
    {
        private Func<IStrokePainter>? _painterFactory;

        /// <summary>
        /// Initializes a new brush tool builder.
        /// </summary>
        /// <param name="id">The unique tool identifier.</param>
        public BrushToolBuilder(string id)
            : base(id, ToolCategory.Brush)
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
            if (_painterFactory == null)
                throw new InvalidOperationException("Brush tool requires a painter. Call WithPainter() before Build().");

            // Validate shortcut conflicts
            ValidateShortcut();

            return new BrushToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                PainterFactory: _painterFactory
            );
        }
    }
}
