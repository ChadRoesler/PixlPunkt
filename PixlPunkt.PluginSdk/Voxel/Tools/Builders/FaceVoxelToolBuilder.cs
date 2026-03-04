namespace PixlPunkt.PluginSdk.Voxel.Tools.Builders
{
    /// <summary>Builder for face-oriented voxel tools (paint/sample/erase).</summary>
    public sealed class FaceVoxelToolBuilder : VoxelToolBuilderBase<FaceVoxelToolBuilder>
    {
        public FaceVoxelToolBuilder(string id) : base(id, VoxelToolCategory.Face)
        {
        }

        public override VoxelToolRegistration Build()
        {
            var behavior = new VoxelToolBehavior(
                ToolId: Id,
                InputPattern: VoxelToolInputPattern.Stroke,
                HandlesRightClick: false,
                SuppressRmbSample: false,
                ModifiesVoxelData: true,
                RequiresFacePick: true,
                RequiresVoxelPick: false);

            return BuildCore(behavior);
        }
    }
}
