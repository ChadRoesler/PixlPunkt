using PixlPunkt.PluginSdk.Effects;
using PixlPunkt.PluginSdk.IO;
using PixlPunkt.PluginSdk.Tools;

namespace PixlPunkt.PluginSdk.Plugins
{
    /// <summary>
    /// Core interface that all PixlPunkt plugins must implement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IPlugin"/> defines the contract for plugin lifecycle and tool/effect registration.
    /// Plugins are loaded from <c>.punk</c> files (ZIP archives containing assemblies and metadata).
    /// </para>
    /// <para>
    /// <strong>Plugin Lifecycle:</strong>
    /// </para>
    /// <list type="number">
    /// <item>Plugin assembly is loaded into an isolated AssemblyLoadContext</item>
    /// <item><see cref="Initialize"/> is called with the host context</item>
    /// <item><see cref="GetToolRegistrations"/> is called to discover tools</item>
    /// <item><see cref="GetEffectRegistrations"/> is called to discover effects</item>
    /// <item><see cref="GetImportRegistrations"/> is called to discover import handlers</item>
    /// <item><see cref="GetExportRegistrations"/> is called to discover export handlers</item>
    /// <item>All registrations are added to their respective global registries</item>
    /// <item><see cref="Shutdown"/> is called when the plugin is unloaded</item>
    /// </list>
    /// <para>
    /// <strong>Implementation Guidelines:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>Plugins should be stateless where possible</item>
    /// <item>Use <see cref="IPluginContext"/> for host services, not static references</item>
    /// <item>Clean up all resources in <see cref="Shutdown"/></item>
    /// <item>All IDs should use your vendor prefix (e.g., "com.mycompany.brush.sparkle")</item>
    /// </list>
    /// </remarks>
    public interface IPlugin
    {
        /// <summary>
        /// Gets the unique identifier for this plugin.
        /// </summary>
        /// <value>
        /// A string following the convention <c>{vendor}.{name}</c>.
        /// For example: <c>"com.example.sparkletools"</c>.
        /// </value>
        /// <remarks>
        /// This ID must match the <c>id</c> field in the plugin's manifest.json.
        /// </remarks>
        string Id { get; }

        /// <summary>
        /// Gets the display name for this plugin.
        /// </summary>
        /// <value>
        /// A human-readable name for display in the plugin manager UI.
        /// </value>
        string DisplayName { get; }

        /// <summary>
        /// Gets the plugin version.
        /// </summary>
        /// <value>
        /// A semantic version string (e.g., "1.0.0", "2.1.3-beta").
        /// </value>
        string Version { get; }

        /// <summary>
        /// Gets the plugin author or organization.
        /// </summary>
        /// <value>
        /// The author name or organization responsible for the plugin.
        /// </value>
        string Author { get; }

        /// <summary>
        /// Gets a brief description of what this plugin provides.
        /// </summary>
        /// <value>
        /// A short description for display in the plugin manager.
        /// </value>
        string Description { get; }

        /// <summary>
        /// Initializes the plugin with the host context.
        /// </summary>
        /// <param name="context">The plugin context providing access to host services.</param>
        /// <remarks>
        /// <para>
        /// Called once after the plugin assembly is loaded. Use this to:
        /// </para>
        /// <list type="bullet">
        /// <item>Store the context reference for later use</item>
        /// <item>Load any plugin-specific resources</item>
        /// <item>Perform one-time initialization</item>
        /// </list>
        /// <para>
        /// Do NOT register tools or effects here - use the Get*Registrations methods instead.
        /// </para>
        /// </remarks>
        void Initialize(IPluginContext context);

        /// <summary>
        /// Returns all tool registrations provided by this plugin.
        /// </summary>
        /// <returns>
        /// An enumerable of tool registrations to add to the tool registry.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Called after <see cref="Initialize"/> to discover the plugin's tools.
        /// Each registration should include:
        /// </para>
        /// <list type="bullet">
        /// <item>A unique tool ID with your vendor prefix</item>
        /// <item>A settings object (or null for simple tools)</item>
        /// <item>The appropriate factory/builder for the tool category</item>
        /// </list>
        /// </remarks>
        IEnumerable<IToolRegistration> GetToolRegistrations();

        /// <summary>
        /// Returns all effect registrations provided by this plugin.
        /// </summary>
        /// <returns>
        /// An enumerable of effect registrations to add to the effect registry.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Called after <see cref="Initialize"/> to discover the plugin's layer effects.
        /// Each registration should include:
        /// </para>
        /// <list type="bullet">
        /// <item>A unique effect ID with your vendor prefix (e.g., "com.mycompany.effect.halftone")</item>
        /// <item>A factory method to create effect instances</item>
        /// <item>Options provider for settings UI generation</item>
        /// </list>
        /// <para>
        /// Return an empty enumerable if your plugin doesn't provide any effects.
        /// </para>
        /// </remarks>
        IEnumerable<IEffectRegistration> GetEffectRegistrations();

        /// <summary>
        /// Returns all import handler registrations provided by this plugin.
        /// </summary>
        /// <returns>
        /// An enumerable of import registrations to add to the import registry.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Called after <see cref="Initialize"/> to discover the plugin's import handlers.
        /// Use <see cref="IO.Builders.ImportBuilders"/> to create registrations:
        /// </para>
        /// <code>
        /// public IEnumerable&lt;IImportRegistration&gt; GetImportRegistrations()
        /// {
        ///     yield return ImportBuilders.ForPalette("myplugin.import.txtpalette")
        ///         .WithFormat(".txtpal", "Text Palette", "Simple text-based palette")
        ///         .WithHandler(ctx => ImportTextPalette(ctx))
        ///         .Build();
        /// }
        /// </code>
        /// <para>
        /// Return an empty enumerable if your plugin doesn't provide any import handlers.
        /// </para>
        /// </remarks>
        IEnumerable<IImportRegistration> GetImportRegistrations();

        /// <summary>
        /// Returns all export handler registrations provided by this plugin.
        /// </summary>
        /// <returns>
        /// An enumerable of export registrations to add to the export registry.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Called after <see cref="Initialize"/> to discover the plugin's export handlers.
        /// Use <see cref="IO.Builders.ExportBuilders"/> to create registrations:
        /// </para>
        /// <code>
        /// public IEnumerable&lt;IExportRegistration&gt; GetExportRegistrations()
        /// {
        ///     yield return ExportBuilders.ForPalette("myplugin.export.txtpalette")
        ///         .WithFormat(".txtpal", "Text Palette", "Simple text-based palette")
        ///         .WithHandler((ctx, data) => ExportTextPalette(ctx, data))
        ///         .Build();
        /// }
        /// </code>
        /// <para>
        /// Return an empty enumerable if your plugin doesn't provide any export handlers.
        /// </para>
        /// </remarks>
        IEnumerable<IExportRegistration> GetExportRegistrations();

        /// <summary>
        /// Shuts down the plugin and releases resources.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Called before the plugin is unloaded. Use this to:
        /// </para>
        /// <list type="bullet">
        /// <item>Dispose of any managed resources</item>
        /// <item>Save plugin state if needed</item>
        /// <item>Unsubscribe from any events</item>
        /// </list>
        /// <para>
        /// After this method returns, the plugin's tools and effects will be unregistered
        /// and the assembly load context may be unloaded.
        /// </para>
        /// </remarks>
        void Shutdown();
    }
}
