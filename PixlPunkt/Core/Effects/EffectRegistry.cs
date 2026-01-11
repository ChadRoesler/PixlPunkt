using System;
using System.Collections.Generic;
using System.Linq;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.Effects
{
    /// <summary>
    /// Interface for the effect registry providing unified effect management.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The effect registry maintains a collection of <see cref="IEffectRegistration"/> instances
    /// that can be queried, registered, and unregistered dynamically. This enables both built-in
    /// and plugin effects to be managed through a unified API.
    /// </para>
    /// </remarks>
    public interface IEffectRegistry
    {
        /// <summary>
        /// Registers an effect with the registry.
        /// </summary>
        /// <param name="registration">The effect registration to add.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="registration"/> is null.</exception>
        /// <remarks>
        /// Registration is idempotent - registering the same effect ID multiple times has no additional effect.
        /// </remarks>
        void Register(IEffectRegistration registration);

        /// <summary>
        /// Unregisters an effect by ID.
        /// </summary>
        /// <param name="effectId">The effect ID to remove.</param>
        /// <returns><c>true</c> if the effect was found and removed; otherwise, <c>false</c>.</returns>
        bool Unregister(string effectId);

        /// <summary>
        /// Gets an effect registration by ID.
        /// </summary>
        /// <param name="effectId">The effect ID to look up.</param>
        /// <returns>The registration if found; otherwise, <c>null</c>.</returns>
        IEffectRegistration? GetById(string effectId);

        /// <summary>
        /// Gets all registered effects.
        /// </summary>
        /// <returns>A snapshot of all registered effects.</returns>
        IEnumerable<IEffectRegistration> GetAll();

        /// <summary>
        /// Gets all effects in a specific category.
        /// </summary>
        /// <param name="category">The category to filter by.</param>
        /// <returns>All effects matching the specified category.</returns>
        IEnumerable<IEffectRegistration> GetByCategory(EffectCategory category);

        /// <summary>
        /// Gets all registered effect IDs.
        /// </summary>
        /// <value>A snapshot of all registered effect IDs.</value>
        IEnumerable<string> RegisteredIds { get; }

        /// <summary>
        /// Gets the count of registered effects.
        /// </summary>
        /// <value>The number of effects currently registered.</value>
        int Count { get; }

        /// <summary>
        /// Checks if an effect is registered.
        /// </summary>
        /// <param name="effectId">The effect ID to check.</param>
        /// <returns><c>true</c> if the effect is registered; otherwise, <c>false</c>.</returns>
        bool IsRegistered(string effectId);

        /// <summary>
        /// Occurs when an effect is registered.
        /// </summary>
        /// <remarks>
        /// Subscribers receive the newly registered <see cref="IEffectRegistration"/>.
        /// </remarks>
        event Action<IEffectRegistration>? EffectRegistered;

        /// <summary>
        /// Occurs when an effect is unregistered.
        /// </summary>
        /// <remarks>
        /// Subscribers receive the effect ID that was removed.
        /// </remarks>
        event Action<string>? EffectUnregistered;

        /// <summary>
        /// Occurs when multiple effects have been added or removed in a batch operation.
        /// </summary>
        /// <remarks>
        /// Use this event to refresh UI after bulk registration (e.g., after loading all built-in effects).
        /// </remarks>
        event Action? EffectsChanged;
    }

    /// <summary>
    /// Default implementation of <see cref="IEffectRegistry"/> providing unified effect registration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="EffectRegistry"/> provides centralized effect management with:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Static Shared Property</strong>: Global access via <c>EffectRegistry.Shared</c>.</item>
    /// <item><strong>Category Queries</strong>: Filter effects by category (Stylize, Filter, Color).</item>
    /// <item><strong>Thread-Safe Registration</strong>: Uses locking for concurrent access.</item>
    /// <item><strong>Plugin Support</strong>: Events for dynamic effect registration/unregistration.</item>
    /// </list>
    /// <para>
    /// <strong>Typical Usage:</strong>
    /// </para>
    /// <code>
    /// // At application startup
    /// BuiltInEffects.RegisterAll();
    /// 
    /// // Query effects
    /// var stylizeEffects = EffectRegistry.Shared.GetByCategory(EffectCategory.Stylize);
    /// 
    /// // Create effect instance
    /// var registration = EffectRegistry.Shared.GetById(EffectIds.DropShadow);
    /// var effect = registration?.CreateInstance();
    /// </code>
    /// </remarks>
    /// <seealso cref="IEffectRegistration"/>
    /// <seealso cref="BuiltInEffects"/>
    /// <seealso cref="BuiltInEffectRegistration"/>
    public sealed class EffectRegistry : IEffectRegistry
    {
        private static IEffectRegistry _shared = new EffectRegistry();
        private static readonly object _instanceLock = new();

        /// <summary>
        /// Gets the shared global instance of the effect registry.
        /// </summary>
        /// <value>
        /// The singleton <see cref="IEffectRegistry"/> instance used throughout the application.
        /// </value>
        /// <remarks>
        /// This is the primary access point for effect registration. All components should use
        /// this shared instance to ensure consistent effect availability across the application.
        /// </remarks>
        public static IEffectRegistry Shared
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
        /// Replaces the shared instance with a custom implementation.
        /// </summary>
        /// <param name="registry">The registry instance to use as the shared instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="registry"/> is null.</exception>
        /// <remarks>
        /// This method is primarily intended for testing scenarios where a mock registry is needed.
        /// </remarks>
        public static void SetInstance(IEffectRegistry registry)
        {
            lock (_instanceLock)
            {
                _shared = registry ?? throw new ArgumentNullException(nameof(registry));
            }
        }

        /// <summary>
        /// Resets the shared instance to a new default registry.
        /// </summary>
        /// <remarks>
        /// This method clears all registrations and creates a fresh registry.
        /// Primarily used for testing cleanup.
        /// </remarks>
        public static void ResetInstance()
        {
            lock (_instanceLock)
            {
                _shared = new EffectRegistry();
            }
        }

        private readonly Dictionary<string, IEffectRegistration> _registrations = new();
        private readonly object _lock = new();

        /// <inheritdoc/>
        public event Action<IEffectRegistration>? EffectRegistered;

        /// <inheritdoc/>
        public event Action<string>? EffectUnregistered;

        /// <inheritdoc/>
        public event Action? EffectsChanged;

        // ====================================================================?
        // CORE OPERATIONS
        // ====================================================================?

        /// <inheritdoc/>
        public void Register(IEffectRegistration registration)
        {
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            lock (_lock)
            {
                // Make registration idempotent - skip if already registered
                if (_registrations.ContainsKey(registration.Id))
                {
                    LoggingService.Debug("Effect already registered, skipping effectId={EffectId}", registration.Id);
                    return;
                }

                _registrations[registration.Id] = registration;
            }

            LoggingService.Debug("Effect registered effectId={EffectId} category={Category} name={Name}",
                registration.Id, registration.Category, registration.DisplayName);

            EffectRegistered?.Invoke(registration);
        }

        /// <inheritdoc/>
        public bool Unregister(string effectId)
        {
            bool removed;

            lock (_lock)
            {
                removed = _registrations.Remove(effectId);
            }

            if (removed)
            {
                LoggingService.Debug("Effect unregistered effectId={EffectId}", effectId);
                EffectUnregistered?.Invoke(effectId);
            }

            return removed;
        }

        /// <summary>
        /// Notifies listeners that the effect set has changed.
        /// </summary>
        /// <remarks>
        /// Call this method after batch registration/unregistration operations to trigger
        /// a single UI refresh rather than multiple individual updates.
        /// </remarks>
        public void NotifyEffectsChanged()
        {
            LoggingService.Debug("EffectsChanged notification fired, totalEffects={Count}", Count);
            EffectsChanged?.Invoke();
        }

        /// <inheritdoc/>
        public IEffectRegistration? GetById(string effectId)
        {
            lock (_lock)
            {
                return _registrations.TryGetValue(effectId, out var reg) ? reg : null;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IEffectRegistration> GetAll()
        {
            lock (_lock)
            {
                return new List<IEffectRegistration>(_registrations.Values);
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
        public bool IsRegistered(string effectId)
        {
            lock (_lock)
            {
                return _registrations.ContainsKey(effectId);
            }
        }

        //////////////////////////////////////////////////////////////////
        // CATEGORY QUERIES
        //////////////////////////////////////////////////////////////////

        /// <inheritdoc/>
        public IEnumerable<IEffectRegistration> GetByCategory(EffectCategory category)
        {
            lock (_lock)
            {
                return _registrations.Values.Where(r => r.Category == category).ToList();
            }
        }
    }
}
