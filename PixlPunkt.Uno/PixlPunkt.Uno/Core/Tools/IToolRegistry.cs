using System;
using System.Collections.Generic;

namespace PixlPunkt.Uno.Core.Tools
{
    /// <summary>
    /// Interface for unified tool registration and discovery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IToolRegistry"/> provides a single registry for all tool types:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="BrushToolRegistration"/> - Brush tools with stroke painters</item>
    /// <item><see cref="TileToolRegistration"/> - Tile tools for tile-based editing</item>
    /// <item><see cref="ShapeToolRegistration"/> - Shape tools for geometric primitives</item>
    /// <item><see cref="SelectionToolRegistration"/> - Selection tools with interactive UI</item>
    /// <item><see cref="UtilityToolRegistration"/> - Utility tools (pan, zoom, dropper)</item>
    /// </list>
    /// <para>
    /// All registration types implement <see cref="IToolRegistration"/> for unified lookup,
    /// with category-specific queries available for type-safe access.
    /// </para>
    /// </remarks>
    public interface IToolRegistry
    {
        // ====================================================================
        // CORE OPERATIONS
        // ====================================================================

        /// <summary>
        /// Registers a new tool.
        /// </summary>
        /// <param name="registration">The tool registration.</param>
        /// <exception cref="ArgumentException">Thrown if a tool with the same ID is already registered.</exception>
        void Register(IToolRegistration registration);

        /// <summary>
        /// Unregisters a tool by string ID.
        /// </summary>
        /// <param name="toolId">The tool ID to unregister.</param>
        /// <returns>True if the tool was found and removed; false if not found.</returns>
        bool Unregister(string toolId);

        /// <summary>
        /// Gets a tool registration by string ID.
        /// </summary>
        /// <param name="toolId">The tool ID to look up.</param>
        /// <returns>The registration if found; null otherwise.</returns>
        IToolRegistration? GetById(string toolId);

        /// <summary>
        /// Gets a tool registration by string ID with type casting.
        /// </summary>
        /// <typeparam name="T">The expected registration type.</typeparam>
        /// <param name="toolId">The tool ID to look up.</param>
        /// <returns>The registration cast to T if found and compatible; null otherwise.</returns>
        T? GetById<T>(string toolId) where T : class, IToolRegistration;

        /// <summary>
        /// Gets all registered tools.
        /// </summary>
        /// <returns>Enumerable of all tool registrations.</returns>
        IEnumerable<IToolRegistration> GetAll();

        /// <summary>
        /// Gets all registered tool string IDs.
        /// </summary>
        IEnumerable<string> RegisteredIds { get; }

        /// <summary>
        /// Gets the count of registered tools.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Checks if a tool ID is registered.
        /// </summary>
        /// <param name="toolId">The tool ID to check.</param>
        /// <returns>True if registered; false otherwise.</returns>
        bool IsRegistered(string toolId);

        // ====================================================================
        // BEHAVIOR QUERIES
        // ====================================================================

        /// <summary>
        /// Gets the behavior descriptor for a tool.
        /// </summary>
        /// <param name="toolId">The tool ID to look up.</param>
        /// <returns>
        /// The <see cref="IToolBehavior"/> for the tool if registered and the
        /// registration implements the interface; null otherwise.
        /// </returns>
        IToolBehavior? GetBehavior(string toolId);

        // ====================================================================
        // CATEGORY-SPECIFIC QUERIES
        // ====================================================================

        /// <summary>
        /// Gets all brush tool registrations.
        /// </summary>
        IEnumerable<BrushToolRegistration> GetBrushTools();

        /// <summary>
        /// Gets all tile tool registrations.
        /// </summary>
        IEnumerable<TileToolRegistration> GetTileTools();

        /// <summary>
        /// Gets all shape tool registrations.
        /// </summary>
        IEnumerable<ShapeToolRegistration> GetShapeTools();

        /// <summary>
        /// Gets all selection tool registrations.
        /// </summary>
        IEnumerable<SelectionToolRegistration> GetSelectionTools();

        /// <summary>
        /// Gets all utility tool registrations.
        /// </summary>
        IEnumerable<UtilityToolRegistration> GetUtilityTools();

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when a new tool is registered.
        /// </summary>
        event Action<IToolRegistration>? ToolRegistered;

        /// <summary>
        /// Raised when a tool is unregistered.
        /// </summary>
        event Action<string>? ToolUnregistered;
    }
}
