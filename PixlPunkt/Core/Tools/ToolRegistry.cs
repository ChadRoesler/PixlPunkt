using System;
using System.Collections.Generic;
using System.Linq;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Default implementation of <see cref="IToolRegistry"/> providing unified tool registration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ToolRegistry"/> provides centralized tool management with:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Static Shared Property</strong>: Easy global access via <c>ToolRegistry.Shared</c>.</item>
    /// <item><strong>Category Queries</strong>: Type-safe access to tools by category.</item>
    /// <item><strong>Thread-Safe Registration</strong>: Uses locking for concurrent access.</item>
    /// <item><strong>String ID Lookup</strong>: Primary lookup via string IDs.</item>
    /// <item><strong>Plugin Support</strong>: Events for dynamic tool registration/unregistration.</item>
    /// </list>
    /// </remarks>
    public sealed class ToolRegistry : IToolRegistry
    {
        private static IToolRegistry _shared = new ToolRegistry();
        private static readonly object _instanceLock = new();

        /// <summary>
        /// Gets the shared global instance of the tool registry.
        /// </summary>
        public static IToolRegistry Shared
        {
            get
            {
                lock (_instanceLock)
                {
                    return _shared;
                }
            }
        }

        /// <summary>
        /// Replaces the shared instance (primarily for testing).
        /// </summary>
        /// <param name="registry">The new registry instance to use as shared.</param>
        public static void SetInstance(IToolRegistry registry)
        {
            lock (_instanceLock)
            {
                _shared = registry ?? throw new ArgumentNullException(nameof(registry));
            }
        }

        /// <summary>
        /// Resets the shared instance to a new default registry (for testing cleanup).
        /// </summary>
        public static void ResetInstance()
        {
            lock (_instanceLock)
            {
                _shared = new ToolRegistry();
            }
        }

        private readonly Dictionary<string, IToolRegistration> _registrations = new();
        private readonly object _lock = new();

        /// <inheritdoc/>
        public event Action<IToolRegistration>? ToolRegistered;

        /// <inheritdoc/>
        public event Action<string>? ToolUnregistered;

        /// <summary>
        /// Raised when multiple tools have been added or removed in a batch operation.
        /// UI components should listen to this for efficient refresh.
        /// </summary>
        public event Action? ToolsChanged;

        // ====================================================================
        // CORE OPERATIONS
        // ====================================================================

        /// <inheritdoc/>
        public void Register(IToolRegistration registration)
        {
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            lock (_lock)
            {
                // Make registration idempotent - skip if already registered
                if (_registrations.ContainsKey(registration.Id))
                {
                    LoggingService.Debug("Tool already registered, skipping toolId={ToolId}", registration.Id);
                    return;
                }

                _registrations[registration.Id] = registration;
            }

            LoggingService.Debug("Tool registered toolId={ToolId} category={Category} name={Name}",
                registration.Id, registration.Category, registration.DisplayName);

            ToolRegistered?.Invoke(registration);
        }

        /// <inheritdoc/>
        public bool Unregister(string toolId)
        {
            bool removed;

            lock (_lock)
            {
                removed = _registrations.Remove(toolId);
            }

            if (removed)
            {
                LoggingService.Debug("Tool unregistered toolId={ToolId}", toolId);
                ToolUnregistered?.Invoke(toolId);
            }

            return removed;
        }

        /// <summary>
        /// Notifies listeners that the tool set has changed.
        /// Call this after batch registration/unregistration operations.
        /// </summary>
        public void NotifyToolsChanged()
        {
            LoggingService.Debug("ToolsChanged notification fired, totalTools={Count}", Count);
            ToolsChanged?.Invoke();
        }

        /// <inheritdoc/>
        public IToolRegistration? GetById(string toolId)
        {
            lock (_lock)
            {
                return _registrations.TryGetValue(toolId, out var reg) ? reg : null;
            }
        }

        /// <inheritdoc/>
        public T? GetById<T>(string toolId) where T : class, IToolRegistration
        {
            lock (_lock)
            {
                return _registrations.TryGetValue(toolId, out var reg) ? reg as T : null;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IToolRegistration> GetAll()
        {
            lock (_lock)
            {
                return new List<IToolRegistration>(_registrations.Values);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<string> RegisteredIds
        {
            get
            {
                lock (_lock)
                {
                    return new List<string>(_registrations.Keys);
                }
            }
        }

        /// <inheritdoc/>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _registrations.Count;
                }
            }
        }

        /// <inheritdoc/>
        public bool IsRegistered(string toolId)
        {
            lock (_lock)
            {
                return _registrations.ContainsKey(toolId);
            }
        }

        // ====================================================================
        // BEHAVIOR QUERIES
        // ====================================================================

        /// <inheritdoc/>
        public IToolBehavior? GetBehavior(string toolId)
        {
            lock (_lock)
            {
                return _registrations.TryGetValue(toolId, out var reg) ? reg as IToolBehavior : null;
            }
        }

        // ====================================================================
        // CATEGORY-SPECIFIC QUERIES
        // ====================================================================

        /// <inheritdoc/>
        public IEnumerable<BrushToolRegistration> GetBrushTools()
        {
            lock (_lock)
            {
                return _registrations.Values.OfType<BrushToolRegistration>().ToList();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<TileToolRegistration> GetTileTools()
        {
            lock (_lock)
            {
                return _registrations.Values.OfType<TileToolRegistration>().ToList();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<ShapeToolRegistration> GetShapeTools()
        {
            lock (_lock)
            {
                return _registrations.Values.OfType<ShapeToolRegistration>().ToList();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<SelectionToolRegistration> GetSelectionTools()
        {
            lock (_lock)
            {
                return _registrations.Values.OfType<SelectionToolRegistration>().ToList();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<UtilityToolRegistration> GetUtilityTools()
        {
            lock (_lock)
            {
                return _registrations.Values.OfType<UtilityToolRegistration>().ToList();
            }
        }

        // ====================================================================
        // CATEGORY HELPERS
        // ====================================================================

        /// <summary>
        /// Gets the category for a tool by ID.
        /// </summary>
        /// <param name="toolId">The tool ID to categorize.</param>
        /// <returns>The tool category, or Utility if not found.</returns>
        public ToolCategory GetCategory(string toolId)
        {
            lock (_lock)
            {
                return _registrations.TryGetValue(toolId, out var reg) ? reg.Category : ToolCategory.Utility;
            }
        }

        /// <summary>
        /// Gets all tools in a specific category.
        /// </summary>
        /// <param name="category">The category to filter by.</param>
        /// <returns>Enumerable of registrations in that category.</returns>
        public IEnumerable<IToolRegistration> GetByCategory(ToolCategory category)
        {
            lock (_lock)
            {
                return _registrations.Values.Where(r => r.Category == category).ToList();
            }
        }
    }
}
