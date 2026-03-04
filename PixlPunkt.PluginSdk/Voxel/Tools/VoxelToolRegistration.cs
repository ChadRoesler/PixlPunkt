using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.Voxel.Tools
{
    /// <summary>Default immutable voxel tool registration implementation.</summary>
    public sealed record VoxelToolRegistration(
        string Id,
        string DisplayName,
        VoxelToolCategory Category,
        ToolSettingsBase? Settings,
        IVoxelToolBehavior? Behavior,
        Func<IVoxelToolContext, IVoxelToolHandler> HandlerFactory) : IVoxelToolRegistration
    {
        public IVoxelToolHandler CreateHandler(IVoxelToolContext context)
            => HandlerFactory(context);
    }

    /// <summary>Default immutable voxel tool behavior implementation.</summary>
    public sealed record VoxelToolBehavior(
        string ToolId,
        VoxelToolInputPattern InputPattern,
        bool HandlesRightClick = false,
        bool SuppressRmbSample = false,
        bool ModifiesVoxelData = true,
        bool RequiresFacePick = false,
        bool RequiresVoxelPick = false) : IVoxelToolBehavior;
}
