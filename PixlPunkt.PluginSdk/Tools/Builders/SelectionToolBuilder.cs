using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Selection;

namespace PixlPunkt.PluginSdk.Tools.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="SelectionToolRegistration"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionToolBuilder provides a fluent API for registering selection tools
    /// that create or modify selection regions using <see cref="ISelectionTool"/>.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// var registration = ToolBuilders.SelectionTool("myplugin.select.ellipse")
    ///     .WithDisplayName("Ellipse Select")
    ///     .WithSettings(ellipseSettings)
    ///     .WithToolFactory(() => new EllipseSelectTool())
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class SelectionToolBuilder : ToolBuilderBase<SelectionToolBuilder, SelectionToolRegistration>
    {
        private Func<ISelectionTool>? _toolFactory;

        /// <summary>
        /// Initializes a new selection tool builder.
        /// </summary>
        /// <param name="id">The unique tool identifier.</param>
        public SelectionToolBuilder(string id)
            : base(id, ToolCategory.Select)
        {
        }

        /// <summary>
        /// Sets the selection tool factory.
        /// </summary>
        /// <param name="factory">Factory function that creates a new selection tool instance.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public SelectionToolBuilder WithToolFactory(Func<ISelectionTool> factory)
        {
            _toolFactory = factory;
            return this;
        }

        /// <summary>
        /// Sets the selection tool using a type parameter for convenience.
        /// </summary>
        /// <typeparam name="TTool">The tool type to create (must have parameterless constructor).</typeparam>
        /// <returns>This builder for fluent chaining.</returns>
        public SelectionToolBuilder WithToolFactory<TTool>() where TTool : ISelectionTool, new()
        {
            _toolFactory = () => new TTool();
            return this;
        }

        /// <inheritdoc/>
        public override SelectionToolRegistration Build()
        {
            if (_toolFactory == null)
                throw new InvalidOperationException("Selection tool requires a tool factory. Call WithToolFactory() before Build().");

            // Validate shortcut conflicts
            ValidateShortcut();

            return new SelectionToolRegistration(
                Id: Id,
                DisplayName: EffectiveDisplayName,
                Settings: Settings,
                SelectionToolFactory: _toolFactory
            );
        }
    }
}
