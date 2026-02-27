namespace PixlPunkt.PluginSdk.Voxel.Tools.Builders
{
    /// <summary>Builder for voxel editing tools (create/delete/select/move).</summary>
    public sealed class EditVoxelToolBuilder : VoxelToolBuilderBase<EditVoxelToolBuilder>
    {
        public EditVoxelToolBuilder(string id) : base(id, VoxelToolCategory.Edit)
        {
        }

        public override VoxelToolRegistration Build()
        {
            var behavior = new VoxelToolBehavior(
                ToolId: Id,
                InputPattern: VoxelToolInputPattern.Click,
                HandlesRightClick: false,
                SuppressRmbSample: false,
                ModifiesVoxelData: true,
                RequiresFacePick: false,
                RequiresVoxelPick: true);

            return BuildCore(behavior);
        }
    }
}
