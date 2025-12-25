using System;
using PixlPunkt.Core.Tools.Selection;

namespace PixlPunkt.Core.Tools.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="SelectionToolRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionToolBuilder provides a fluent API for registering selection-category tools
    /// that create and modify pixel selections using <see cref="ISelectionTool"/>.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// registry.AddSelectionTool(ToolIds.SelectRect)
    ///     .WithDisplayName("Rectangle Select")
    ///     .WithSettings(toolState.SelectRect)
    ///     .WithToolFactory(ctx => new RectSelectionTool(ctx))
    ///     .Register();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class SelectionToolBuilder : ToolBuilderBase<SelectionToolBuilder, SelectionToolRegistration>
    {
        private Func<SelectionToolContext, ISelectionTool>? _toolFactory;

        /// <summary>
        /// Initializes a new selection tool builder.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        public SelectionToolBuilder(IToolRegistry registry, string id)
            : base(registry, id, ToolCategory.Select)
        {
        }

        /// <summary>
        /// Sets the tool factory for creating selection tool instances.
        /// </summary>
        /// <param name="factory">Factory function that creates a new tool instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public SelectionToolBuilder WithToolFactory(Func<SelectionToolContext, ISelectionTool> factory)
        {
            _toolFactory = factory;
            return this;
        }

        /// <summary>
        /// Sets the tool factory using a type parameter for tools with context-only constructor.
        /// </summary>
        /// <typeparam name="TTool">The tool type (must have constructor accepting SelectionToolContext).</typeparam>
        /// <returns>This builder for fluent chaining.</returns>
        public SelectionToolBuilder WithTool<TTool>() where TTool : ISelectionTool
        {
            _toolFactory = ctx => (ISelectionTool)Activator.CreateInstance(typeof(TTool), ctx)!;
            return this;
        }

        /// <inheritdoc/>
        public override SelectionToolRegistration Build()
        {
            return new SelectionToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                ToolFactory: _toolFactory ?? (ctx => throw new InvalidOperationException($"No tool factory set for {Id}"))
            );
        }
    }
}
