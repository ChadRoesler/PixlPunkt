using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.Tools
{
    /// <summary>
    /// Base interface for all tool registrations in the unified tool registry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IToolRegistration"/> provides a common interface for all tool types,
    /// enabling a unified registry that can store utility, selection, brush, and shape tools.
    /// </para>
    /// <para>
    /// The <see cref="Category"/> property determines which engine handles the tool at runtime.
    /// </para>
    /// </remarks>
    public interface IToolRegistration
    {
        /// <summary>
        /// Gets the unique identifier for the tool.
        /// </summary>
        /// <value>
        /// A string following the convention <c>{vendor}.{category}.{name}</c>.
        /// For example: <c>"pixlpunkt.brush.brush"</c> or <c>"com.plugin.select.smartedge"</c>.
        /// </value>
        /// <remarks>
        /// Use <see cref="ToolIds"/> constants for built-in tools to avoid magic strings.
        /// Plugin tools should use their vendor namespace prefix.
        /// </remarks>
        string Id { get; }

        /// <summary>
        /// Gets the category that determines engine routing.
        /// </summary>
        /// <value>
        /// A <see cref="ToolCategory"/> value indicating which system handles this tool:
        /// Utility (no engine), Select (selection engine), Brush (stroke engine), or Shape (shape engine).
        /// </value>
        ToolCategory Category { get; }

        /// <summary>
        /// Gets the human-readable display name for UI.
        /// </summary>
        /// <value>
        /// A localized string suitable for display in menus, tooltips, and tool panels.
        /// </value>
        string DisplayName { get; }

        /// <summary>
        /// Gets the tool-specific settings object.
        /// </summary>
        /// <value>
        /// The settings instance for this tool, or <c>null</c> for tools without configurable settings
        /// (e.g., Pan, Zoom, Dropper).
        /// </value>
        /// <remarks>
        /// Settings are typically subclasses of <see cref="ToolSettingsBase"/> and may implement
        /// additional interfaces like IBrushLikeSettings for brush-style tools.
        /// </remarks>
        ToolSettingsBase? Settings { get; }
    }
}
