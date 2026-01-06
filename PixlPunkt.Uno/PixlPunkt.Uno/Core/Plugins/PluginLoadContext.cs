using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using PixlPunkt.Uno.Core.Logging;
// Use SDK types for plugin interface
using SdkIPlugin = PixlPunkt.PluginSdk.Plugins.IPlugin;

namespace PixlPunkt.Uno.Core.Plugins
{
    /// <summary>
    /// Isolated assembly load context for plugin assemblies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="PluginLoadContext"/> provides assembly isolation for plugins:
    /// </para>
    /// <list type="bullet">
    /// <item>Each plugin loads into its own context</item>
    /// <item>Plugin assemblies don't conflict with host or other plugins</item>
    /// <item>Supports unloading via <see cref="AssemblyLoadContext.Unload"/></item>
    /// </list>
    /// <para>
    /// <strong>Resolution Strategy:</strong>
    /// </para>
    /// <list type="number">
    /// <item>Try to load from the plugin directory first</item>
    /// <item>Fall back to the default context for shared assemblies</item>
    /// </list>
    /// </remarks>
    public sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _pluginDirectory;

        /// <summary>
        /// Gets the plugin directory path.
        /// </summary>
        public string PluginDirectory => _pluginDirectory;

        /// <summary>
        /// Creates a new plugin load context.
        /// </summary>
        /// <param name="pluginPath">Full path to the plugin's main assembly (.dll).</param>
        /// <param name="pluginName">Name for the load context (for debugging).</param>
        public PluginLoadContext(string pluginPath, string pluginName)
            : base(name: pluginName, isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
            _pluginDirectory = Path.GetDirectoryName(pluginPath) ?? string.Empty;
        }

        /// <summary>
        /// Loads an assembly by name.
        /// </summary>
        /// <param name="assemblyName">The assembly name to load.</param>
        /// <returns>The loaded assembly, or null to fall back to default context.</returns>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Try to resolve from plugin directory first
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            // Check if it's in the plugin directory directly
            string localPath = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(localPath))
            {
                return LoadFromAssemblyPath(localPath);
            }

            // Fall back to default context for shared assemblies (e.g., PixlPunkt.PluginSdk)
            return null;
        }

        /// <summary>
        /// Loads a native library by name.
        /// </summary>
        /// <param name="unmanagedDllName">The native library name.</param>
        /// <returns>Handle to the loaded library, or IntPtr.Zero to fall back.</returns>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Wrapper for managing a loaded plugin instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="LoadedPlugin"/> bundles together:
    /// </para>
    /// <list type="bullet">
    /// <item>The plugin manifest (metadata)</item>
    /// <item>The load context (for unloading)</item>
    /// <item>The plugin instance (implementing SDK <see cref="SdkIPlugin"/>)</item>
    /// <item>Weak reference for tracking unload completion</item>
    /// </list>
    /// </remarks>
    public sealed class LoadedPlugin : IDisposable
    {
        private PluginLoadContext? _loadContext;
        private WeakReference? _contextRef;
        private bool _disposed;

        /// <summary>
        /// Gets the plugin manifest.
        /// </summary>
        public PluginManifest Manifest { get; }

        /// <summary>
        /// Gets the plugin instance (SDK IPlugin interface).
        /// </summary>
        public SdkIPlugin? Instance { get; private set; }

        /// <summary>
        /// Gets whether the plugin is currently loaded.
        /// </summary>
        public bool IsLoaded => Instance != null && _loadContext != null;

        /// <summary>
        /// Gets the plugin directory path.
        /// </summary>
        public string PluginDirectory { get; }

        /// <summary>
        /// Gets the timestamp when the plugin was loaded.
        /// </summary>
        public DateTime LoadedAt { get; }

        /// <summary>
        /// Creates a new loaded plugin wrapper.
        /// </summary>
        /// <param name="manifest">The plugin manifest.</param>
        /// <param name="loadContext">The assembly load context.</param>
        /// <param name="instance">The plugin instance (SDK IPlugin).</param>
        /// <param name="pluginDirectory">The plugin directory path.</param>
        public LoadedPlugin(
            PluginManifest manifest,
            PluginLoadContext loadContext,
            SdkIPlugin instance,
            string pluginDirectory)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
            LoadedAt = DateTime.UtcNow;

            // Keep weak reference for tracking unload
            _contextRef = new WeakReference(_loadContext);
        }

        /// <summary>
        /// Unloads the plugin and its assembly context.
        /// </summary>
        public void Unload()
        {
            if (_disposed) return;

            // Shutdown the plugin first
            try
            {
                if (Instance != null)
                {
                    LoggingService.Info("Shutting down plugin pluginId={PluginId} name={Name}", Manifest.Id, Manifest.Name);
                    Instance.Shutdown();
                    LoggingService.Info("Plugin shutdown complete pluginId={PluginId} name={Name}", Manifest.Id, Manifest.Name);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Error("Error during plugin shutdown pluginId={PluginId} name={Name}", ex, Manifest.Id, Manifest.Name);
            }

            // Clear instance reference
            Instance = null;

            // Unload the assembly context
            try
            {
                _loadContext?.Unload();
                _loadContext = null;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Error unloading plugin assembly context pluginId={PluginId}", ex, Manifest.Id);
            }

            // Force GC to help unload complete
            for (int i = 0; i < 10 && _contextRef?.IsAlive == true; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            LoggingService.Info("Plugin unloaded pluginId={PluginId} name={Name}", Manifest.Id, Manifest.Name);
        }

        /// <summary>
        /// Disposes the loaded plugin.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Unload();
        }
    }
}
