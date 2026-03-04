namespace PixlPunkt.PluginSdk.Voxel.Tools.Builders
{
    /// <summary>Builder for voxel utility tools (lighting, debug, view helpers).</summary>
    public sealed class UtilityVoxelToolBuilder : VoxelToolBuilderBase<UtilityVoxelToolBuilder>
    {
        public UtilityVoxelToolBuilder(string id) : base(id, VoxelToolCategory.Utility)
        {
        }

        public override VoxelToolRegistration Build()
        {
            var behavior = new VoxelToolBehavior(
                ToolId: Id,
                InputPattern: VoxelToolInputPattern.Utility,
                HandlesRightClick: true,
                SuppressRmbSample: true,
                ModifiesVoxelData: false,
                RequiresFacePick: false,
                RequiresVoxelPick: false);

            return BuildCore(behavior);
        }
    }
}
