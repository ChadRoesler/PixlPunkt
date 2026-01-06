using PixlPunkt.Uno.Core.Painting;

namespace PixlPunkt.Uno.Core.Tools.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="FillToolRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FillToolBuilder provides a fluent API for registering fill-category tools
    /// that perform flood fill or global color replacement using <see cref="IFillPainter"/>.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// registry.AddFillTool(ToolIds.Fill)
    ///     .WithDisplayName("Fill")
    ///     .WithSettings(toolState.Fill)
    ///     .WithFillPainter(FloodFillPainter.Shared)
    ///     .Register();
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Plugin Usage:</strong>
    /// <code>
    /// registry.AddFillTool("com.myplugin.fill.pattern")
    ///     .WithDisplayName("Pattern Fill")
    ///     .WithSettings(mySettings)
    ///     .WithFillPainter(new PatternFillPainter())
    ///     .Register();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class FillToolBuilder : ToolBuilderBase<FillToolBuilder, FillToolRegistration>
    {
        private IFillPainter? _fillPainter;

        /// <summary>
        /// Initializes a new fill tool builder.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        public FillToolBuilder(IToolRegistry registry, string id)
            : base(registry, id, ToolCategory.Utility)
        {
        }

        /// <summary>
        /// Sets the fill painter implementation.
        /// </summary>
        /// <param name="fillPainter">The fill painter instance (null = use default FloodFillPainter).</param>
        /// <returns>This builder for fluent chaining.</returns>
        public FillToolBuilder WithFillPainter(IFillPainter? fillPainter)
        {
            _fillPainter = fillPainter;
            return this;
        }

        /// <summary>
        /// Sets the fill painter using a type parameter for convenience.
        /// </summary>
        /// <typeparam name="TPainter">The fill painter type to create (must have parameterless constructor).</typeparam>
        /// <returns>This builder for fluent chaining.</returns>
        public FillToolBuilder WithFillPainter<TPainter>() where TPainter : IFillPainter, new()
        {
            _fillPainter = new TPainter();
            return this;
        }

        /// <inheritdoc/>
        public override FillToolRegistration Build()
        {
            return new FillToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                FillPainter: _fillPainter
            );
        }
    }
}
