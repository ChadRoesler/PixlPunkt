using PixlPunkt.Core.Tools.Utility;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Interface for utility tool registrations, enabling both built-in and plugin utility tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface allows the canvas host to work with utility tools without knowing
    /// whether they are built-in (<see cref="UtilityToolRegistration"/>) or from plugins
    /// (<see cref="Plugins.PluginUtilityToolRegistration"/>).
    /// </para>
    /// </remarks>
    public interface IUtilityToolRegistration : IToolRegistration
    {
        /// <summary>
        /// Gets whether this tool has a handler implementation.
        /// </summary>
        bool HasHandler { get; }

        /// <summary>
        /// Creates a new handler instance for this tool.
        /// </summary>
        /// <param name="context">The utility context for canvas operations.</param>
        /// <returns>A new handler instance, or null if no factory is registered.</returns>
        IUtilityHandler? CreateHandler(IUtilityContext context);
    }
}
