namespace PixlPunkt.PluginSdk.Voxel.Tools.Builders
{
    /// <summary>Entry points for voxel tool builders.</summary>
    public static class VoxelToolBuilders
    {
        public static FaceVoxelToolBuilder FaceTool(string id) => new(id);
        public static EditVoxelToolBuilder EditTool(string id) => new(id);
        public static UtilityVoxelToolBuilder UtilityTool(string id) => new(id);
    }
}
