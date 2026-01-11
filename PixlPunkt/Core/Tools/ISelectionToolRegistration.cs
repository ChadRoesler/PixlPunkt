using PixlPunkt.Core.Tools.Selection;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Interface for selection tool registrations, enabling both built-in and plugin selection tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface allows the canvas host to work with selection tools without knowing
    /// whether they are built-in (<see cref="SelectionToolRegistration"/>) or from plugins
    /// (<see cref="Plugins.PluginSelectionToolRegistration"/>).
    /// </para>
    /// </remarks>
    public interface ISelectionToolRegistration : IToolRegistration
    {
        /// <summary>
        /// Gets whether this registration has a valid tool factory.
        /// </summary>
        bool HasTool { get; }

        /// <summary>
        /// Creates a new selection tool instance using the provided context.
        /// </summary>
        /// <param name="context">Context providing dependencies for the tool.</param>
        /// <returns>A new selection tool instance, or null if no factory is defined.</returns>
        ISelectionTool? CreateTool(SelectionToolContext context);
    }
}
