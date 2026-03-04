using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Voxel;

namespace PixlPunkt.Core.Voxel.Tools
{
    /// <summary>
    /// Registration point for built-in voxel tools.
    /// </summary>
    /// <remarks>
    /// Phase 0 scaffolding only: built-in voxel tool registrations are added in later phases.
    /// </remarks>
    public static class VoxelBuiltInTools
    {
        private static bool _registered;
        private static readonly VoxelLightingToolSettings LightingToolSettings = new();

        public static void RegisterAll(IVoxelToolRegistry registry)
        {
            if (_registered || registry == null)
                return;

            registry.Register(new BuiltInRegistration(VoxelToolIds.FacePaint, "Face Paint", VoxelToolCategory.Face,
                behavior: new BuiltInBehavior(VoxelToolIds.FacePaint, VoxelToolInputPattern.Stroke, modifiesVoxelData: true, requiresFacePick: true)));
            registry.Register(new BuiltInRegistration(VoxelToolIds.FaceDropper, "Face Dropper", VoxelToolCategory.Face,
                behavior: new BuiltInBehavior(VoxelToolIds.FaceDropper, VoxelToolInputPattern.Click, requiresFacePick: true)));
            registry.Register(new BuiltInRegistration(VoxelToolIds.FaceEraseOverride, "Face Erase", VoxelToolCategory.Face,
                behavior: new BuiltInBehavior(VoxelToolIds.FaceEraseOverride, VoxelToolInputPattern.Click, modifiesVoxelData: true, requiresFacePick: true)));
            registry.Register(new BuiltInRegistration(VoxelToolIds.VoxelCreate, "Voxel Create", VoxelToolCategory.Edit,
                behavior: new BuiltInBehavior(VoxelToolIds.VoxelCreate, VoxelToolInputPattern.Click, modifiesVoxelData: true, requiresFacePick: true)));
            registry.Register(new BuiltInRegistration(VoxelToolIds.VoxelDelete, "Voxel Delete", VoxelToolCategory.Edit,
                behavior: new BuiltInBehavior(VoxelToolIds.VoxelDelete, VoxelToolInputPattern.Click, modifiesVoxelData: true, requiresVoxelPick: true)));
            registry.Register(new BuiltInRegistration(VoxelToolIds.VoxelSelect, "Voxel Select", VoxelToolCategory.Edit,
                behavior: new BuiltInBehavior(VoxelToolIds.VoxelSelect, VoxelToolInputPattern.Click, requiresVoxelPick: true)));
            registry.Register(new BuiltInRegistration(VoxelToolIds.VoxelMove, "Voxel Move", VoxelToolCategory.Edit,
                behavior: new BuiltInBehavior(VoxelToolIds.VoxelMove, VoxelToolInputPattern.SelectionTransform, modifiesVoxelData: true, requiresVoxelPick: true)));
            registry.Register(new BuiltInRegistration(VoxelToolIds.Lighting, "Lighting", VoxelToolCategory.Utility,
                settings: LightingToolSettings,
                behavior: new BuiltInBehavior(VoxelToolIds.Lighting, VoxelToolInputPattern.Utility)));

            _registered = true;
        }

        private sealed class BuiltInRegistration : IVoxelToolRegistration
        {
            private readonly ToolSettingsBase? _settings;

            public BuiltInRegistration(
                string id,
                string displayName,
                VoxelToolCategory category,
                ToolSettingsBase? settings = null,
                IVoxelToolBehavior? behavior = null)
            {
                Id = id;
                DisplayName = displayName;
                Category = category;
                _settings = settings;
                Behavior = behavior ?? new BuiltInBehavior(id, category == VoxelToolCategory.Utility ? VoxelToolInputPattern.Utility : VoxelToolInputPattern.Click);
            }

            public string Id { get; }
            public string DisplayName { get; }
            public VoxelToolCategory Category { get; }
            public ToolSettingsBase? Settings => _settings;
            public IVoxelToolBehavior? Behavior { get; }
            public IVoxelToolHandler CreateHandler(IVoxelToolContext context) => NoOpHandler.Instance;
        }

        private sealed class BuiltInBehavior : IVoxelToolBehavior
        {
            public BuiltInBehavior(
                string toolId,
                VoxelToolInputPattern pattern,
                bool modifiesVoxelData = false,
                bool requiresFacePick = false,
                bool requiresVoxelPick = false)
            {
                ToolId = toolId;
                InputPattern = pattern;
                ModifiesVoxelData = modifiesVoxelData;
                RequiresFacePick = requiresFacePick;
                RequiresVoxelPick = requiresVoxelPick;
            }

            public string ToolId { get; }
            public VoxelToolInputPattern InputPattern { get; }
            public bool HandlesRightClick => false;
            public bool SuppressRmbSample => false;
            public bool ModifiesVoxelData { get; }
            public bool RequiresFacePick { get; }
            public bool RequiresVoxelPick { get; }
        }

        private sealed class NoOpHandler : IVoxelToolHandler
        {
            public static readonly NoOpHandler Instance = new();
            public bool PointerPressed(VoxelPointerEvent e) => false;
            public bool PointerMoved(VoxelPointerEvent e) => false;
            public bool PointerReleased(VoxelPointerEvent e) => false;
            public void Cancel() { }
        }
    }
}
