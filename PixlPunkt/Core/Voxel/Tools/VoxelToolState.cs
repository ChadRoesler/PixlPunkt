using PixlPunkt.Core.Tools.Settings;

namespace PixlPunkt.Core.Voxel.Tools
{
    /// <summary>Minimal state container for voxel workspace active tool and settings synchronization.</summary>
    public sealed class VoxelToolState
    {
        private readonly IVoxelToolRegistry _registry;
        private string? _activeToolId;
        private readonly HashSet<string> _wiredPluginSettings = [];

        public VoxelToolState(IVoxelToolRegistry? registry = null)
        {
            _registry = registry ?? VoxelToolRegistry.Shared;
            VoxelBuiltInTools.RegisterAll(_registry);
            _registry.ToolsChanged += OnRegistryToolsChanged;

            _activeToolId = _registry.RegisteredIds.FirstOrDefault();
        }

        public IVoxelToolRegistry Registry => _registry;

        public string? ActiveToolId => _activeToolId;

        public IVoxelToolRegistration? ActiveRegistration
            => _activeToolId != null ? _registry.GetById(_activeToolId) : null;

        public ToolSettingsBase? ActiveSettings
        {
            get
            {
                var settings = ActiveRegistration?.Settings;
                if (settings == null || _activeToolId == null) return settings;

                if (_wiredPluginSettings.Add(_activeToolId))
                {
                    settings.Changed += () => OptionsChanged?.Invoke();
                }

                return settings;
            }
        }

        public event Action<string?>? ActiveToolChanged;
        public event Action? OptionsChanged;

        public bool SetActiveTool(string toolId)
        {
            if (string.IsNullOrWhiteSpace(toolId) || !_registry.IsRegistered(toolId))
                return false;

            if (_activeToolId == toolId)
                return true;

            _activeToolId = toolId;
            ActiveToolChanged?.Invoke(_activeToolId);
            OptionsChanged?.Invoke();
            return true;
        }

        private void OnRegistryToolsChanged()
        {
            bool activeStillValid = !string.IsNullOrWhiteSpace(_activeToolId) && _registry.IsRegistered(_activeToolId!);
            if (activeStillValid)
            {
                OptionsChanged?.Invoke();
                return;
            }

            _activeToolId = _registry.RegisteredIds.FirstOrDefault();
            ActiveToolChanged?.Invoke(_activeToolId);
            OptionsChanged?.Invoke();
        }
    }
}
