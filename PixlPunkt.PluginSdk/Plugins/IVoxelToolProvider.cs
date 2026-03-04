using PixlPunkt.PluginSdk.Voxel.Tools;

namespace PixlPunkt.PluginSdk.Plugins
{
    /// <summary>
    /// Optional plugin interface for providing voxel tools.
    /// </summary>
    /// <remarks>
    /// This interface is additive: plugins can continue implementing only <see cref="IPlugin"/>
    /// and remain fully compatible. Implement this interface when your plugin also exposes tools
    /// for the voxel workspace.
    /// </remarks>
    public interface IVoxelToolProvider
    {
        /// <summary>
        /// Returns voxel tool registrations provided by this plugin.
        /// </summary>
        IEnumerable<IVoxelToolRegistration> GetVoxelToolRegistrations();
    }
}
