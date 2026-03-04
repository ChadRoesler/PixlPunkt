using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.Voxel.Tools
{
    /// <summary>Registry for voxel workspace tools (built-in and plugin-provided).</summary>
    public interface IVoxelToolRegistry
    {
        event Action<IVoxelToolRegistration>? ToolRegistered;
        event Action<string>? ToolUnregistered;
        event Action? ToolsChanged;

        void Register(IVoxelToolRegistration registration);
        bool Unregister(string toolId);
        IVoxelToolRegistration? GetById(string toolId);
        IEnumerable<IVoxelToolRegistration> GetAll();
        IEnumerable<string> RegisteredIds { get; }
        bool IsRegistered(string toolId);
        int Count { get; }
        void NotifyToolsChanged();
    }

    /// <summary>Default registry implementation for voxel tools.</summary>
    public sealed class VoxelToolRegistry : IVoxelToolRegistry
    {
        private static IVoxelToolRegistry _shared = new VoxelToolRegistry();
        private static readonly object _instanceLock = new();

        public static IVoxelToolRegistry Shared
        {
            get
            {
                lock (_instanceLock)
                {
                    return _shared;
                }
            }
        }

        public static void SetInstance(IVoxelToolRegistry registry)
        {
            lock (_instanceLock)
            {
                _shared = registry ?? throw new ArgumentNullException(nameof(registry));
            }
        }

        public static void ResetInstance()
        {
            lock (_instanceLock)
            {
                _shared = new VoxelToolRegistry();
            }
        }

        private readonly Dictionary<string, IVoxelToolRegistration> _registrations = new();
        private readonly object _lock = new();

        public event Action<IVoxelToolRegistration>? ToolRegistered;
        public event Action<string>? ToolUnregistered;
        public event Action? ToolsChanged;

        public void Register(IVoxelToolRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));

            lock (_lock)
            {
                if (_registrations.ContainsKey(registration.Id))
                {
                    LoggingService.Debug("Voxel tool already registered, skipping toolId={ToolId}", registration.Id);
                    return;
                }

                _registrations[registration.Id] = registration;
            }

            LoggingService.Debug("Voxel tool registered toolId={ToolId} category={Category} name={Name}",
                registration.Id, registration.Category, registration.DisplayName);
            ToolRegistered?.Invoke(registration);
        }

        public bool Unregister(string toolId)
        {
            bool removed;
            lock (_lock)
            {
                removed = _registrations.Remove(toolId);
            }

            if (removed)
            {
                LoggingService.Debug("Voxel tool unregistered toolId={ToolId}", toolId);
                ToolUnregistered?.Invoke(toolId);
            }

            return removed;
        }

        public IVoxelToolRegistration? GetById(string toolId)
        {
            lock (_lock)
            {
                return _registrations.TryGetValue(toolId, out var registration) ? registration : null;
            }
        }

        public IEnumerable<IVoxelToolRegistration> GetAll()
        {
            lock (_lock)
            {
                return new List<IVoxelToolRegistration>(_registrations.Values);
            }
        }

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

        public bool IsRegistered(string toolId)
        {
            lock (_lock)
            {
                return _registrations.ContainsKey(toolId);
            }
        }

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

        public void NotifyToolsChanged()
        {
            LoggingService.Debug("VoxelToolsChanged notification fired totalTools={Count}", Count);
            ToolsChanged?.Invoke();
        }
    }
}
