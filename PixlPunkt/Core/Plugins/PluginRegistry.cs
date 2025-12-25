using System;
using System.Collections.Generic;
using System.Linq;
using PixlPunkt.Core.Effects;
using PixlPunkt.Core.IO;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Settings;
using PixlPunkt.Core.Tools;
using SdkBrushToolRegistration = PixlPunkt.PluginSdk.Tools.BrushToolRegistration;
// SDK types
using SdkIToolRegistration = PixlPunkt.PluginSdk.Tools.IToolRegistration;
using SdkSelectionToolRegistration = PixlPunkt.PluginSdk.Tools.SelectionToolRegistration;
using SdkShapeToolRegistration = PixlPunkt.PluginSdk.Tools.ShapeToolRegistration;
using SdkUtilityToolRegistration = PixlPunkt.PluginSdk.Tools.UtilityToolRegistration;
using SdkTileToolRegistration = PixlPunkt.PluginSdk.Tools.TileToolRegistration;

namespace PixlPunkt.Core.Plugins
{
    /// <summary>
    /// Central registry for managing loaded plugins and their lifecycle.
    /// </summary>
    public sealed class PluginRegistry : IDisposable
    {
        private static PluginRegistry? _instance;
        private static readonly object _instanceLock = new();

        /// <summary>
        /// Gets the singleton instance of the plugin registry.
        /// </summary>
        public static PluginRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance ??= new PluginRegistry();
                    }
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, LoadedPlugin> _plugins = new();
        private readonly Dictionary<string, string> _toolToPlugin = new();
        private readonly Dictionary<string, string> _effectToPlugin = new();
        private readonly Dictionary<string, string> _importToPlugin = new();
        private readonly Dictionary<string, string> _exportToPlugin = new();
        private readonly object _lock = new();
        private readonly PluginLoader _loader;
        private bool _disposed;

        /// <summary>Raised when a plugin is successfully loaded.</summary>
        public event Action<LoadedPlugin>? PluginLoaded;

        /// <summary>Raised when a plugin is unloaded.</summary>
        public event Action<string>? PluginUnloaded;

        /// <summary>Raised when plugin loading fails.</summary>
        public event Action<string, Exception>? PluginLoadFailed;

        /// <summary>Raised when all plugins have been refreshed.</summary>
        public event Action? PluginsRefreshed;

        /// <summary>Gets the directory containing .punk plugin files.</summary>
        public string PluginsDirectory { get; }

        /// <summary>Gets the directory where plugins are extracted.</summary>
        public string ExtractedDirectory { get; }

        /// <summary>Gets the directory for plugin data storage.</summary>
        public string DataDirectory { get; }

        /// <summary>Gets the number of loaded plugins.</summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _plugins.Count;
                }
            }
        }

        /// <summary>Creates a new plugin registry with default directories.</summary>
        public PluginRegistry()
            : this(AppPaths.PluginsDirectory, AppPaths.PluginsExtractedDirectory, AppPaths.PluginsDataDirectory)
        {
        }

        /// <summary>Creates a new plugin registry with custom directories.</summary>
        public PluginRegistry(string pluginsDir, string extractedDir, string dataDir)
        {
            PluginsDirectory = pluginsDir;
            ExtractedDirectory = extractedDir;
            DataDirectory = dataDir;
            _loader = new PluginLoader(pluginsDir, extractedDir, dataDir);
        }

        /// <summary>Loads all available plugins from the plugins directory.</summary>
        public void LoadAllPlugins()
        {
            var pluginFiles = _loader.GetAvailablePlugins();
            LoggingService.Info("Found {Count} plugin files in {Dir}", pluginFiles.Length, PluginsDirectory);

            foreach (var punkFile in pluginFiles)
            {
                try
                {
                    LoadPlugin(punkFile);
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to load plugin file {FilePath}", ex, punkFile);
                    PluginLoadFailed?.Invoke(punkFile, ex);
                }
            }
        }

        /// <summary>Loads a single plugin from a .punk file.</summary>
        public LoadedPlugin LoadPlugin(string punkFilePath)
        {
            var loadedPlugin = _loader.LoadFromPackage(punkFilePath);
            string pluginId = loadedPlugin.Manifest.Id;

            lock (_lock)
            {
                if (_plugins.TryGetValue(pluginId, out var existing))
                {
                    UnloadPluginInternal(existing);
                }
                _plugins[pluginId] = loadedPlugin;
            }

            // Register tools, effects, and import/export handlers using adapters
            RegisterPluginTools(loadedPlugin);
            RegisterPluginEffects(loadedPlugin);
            RegisterPluginImportExport(loadedPlugin);

            PluginLoaded?.Invoke(loadedPlugin);
            LoggingService.Info("Plugin registered pluginId={PluginId} name={Name} files={FilePath}", pluginId, loadedPlugin.Manifest.Name, punkFilePath);
            return loadedPlugin;
        }

        /// <summary>Unloads a plugin by ID.</summary>
        public bool UnloadPlugin(string pluginId)
        {
            LoadedPlugin? plugin;

            lock (_lock)
            {
                if (!_plugins.TryGetValue(pluginId, out plugin))
                    return false;
                _plugins.Remove(pluginId);
            }

            UnloadPluginInternal(plugin);
            PluginUnloaded?.Invoke(pluginId);
            LoggingService.Info("Plugin unloaded pluginId={PluginId}", pluginId);
            return true;
        }

        /// <summary>Refreshes all plugins - unloads and reloads from disk.</summary>
        public void RefreshPlugins()
        {
            LoggingService.Info("Refreshing plugins");

            List<LoadedPlugin> toUnload;
            lock (_lock)
            {
                toUnload = _plugins.Values.ToList();
                _plugins.Clear();
                _toolToPlugin.Clear();
                _effectToPlugin.Clear();
                _importToPlugin.Clear();
                _exportToPlugin.Clear();
            }

            foreach (var plugin in toUnload)
            {
                UnloadPluginInternal(plugin);
            }

            LoadAllPlugins();

            if (ToolRegistry.Shared is ToolRegistry toolRegistry)
            {
                toolRegistry.NotifyToolsChanged();
            }
            if (EffectRegistry.Shared is EffectRegistry effectRegistry)
            {
                effectRegistry.NotifyEffectsChanged();
            }

            PluginsRefreshed?.Invoke();
            LoggingService.Info("Plugin refresh complete. {Count} plugins loaded.", Count);
        }

        /// <summary>Gets a loaded plugin by ID.</summary>
        public LoadedPlugin? GetPlugin(string pluginId)
        {
            lock (_lock)
            {
                return _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
            }
        }

        /// <summary>Gets all loaded plugins.</summary>
        public IReadOnlyList<LoadedPlugin> GetAllPlugins()
        {
            lock (_lock)
            {
                return _plugins.Values.ToList();
            }
        }

        /// <summary>Gets the plugin ID that provides a given tool.</summary>
        public string? GetPluginForTool(string toolId)
        {
            lock (_lock)
            {
                return _toolToPlugin.TryGetValue(toolId, out var pluginId) ? pluginId : null;
            }
        }

        /// <summary>Gets the plugin ID that provides a given effect.</summary>
        public string? GetPluginForEffect(string effectId)
        {
            lock (_lock)
            {
                return _effectToPlugin.TryGetValue(effectId, out var pluginId) ? pluginId : null;
            }
        }

        /// <summary>Gets the plugin ID that provides a given import handler.</summary>
        public string? GetPluginForImport(string importId)
        {
            lock (_lock)
            {
                return _importToPlugin.TryGetValue(importId, out var pluginId) ? pluginId : null;
            }
        }

        /// <summary>Gets the plugin ID that provides a given export handler.</summary>
        public string? GetPluginForExport(string exportId)
        {
            lock (_lock)
            {
                return _exportToPlugin.TryGetValue(exportId, out var pluginId) ? pluginId : null;
            }
        }

        /// <summary>Checks if a plugin is loaded.</summary>
        public bool IsLoaded(string pluginId)
        {
            lock (_lock)
            {
                return _plugins.ContainsKey(pluginId);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TOOL REGISTRATION (adapts SDK types to main project types)
        // ═══════════════════════════════════════════════════════════════════════

        private void RegisterPluginTools(LoadedPlugin loadedPlugin)
        {
            if (loadedPlugin.Instance == null) return;

            var toolRegistry = ToolRegistry.Shared;
            var sdkTools = loadedPlugin.Instance.GetToolRegistrations();

            foreach (var sdkTool in sdkTools)
            {
                var adaptedTool = AdaptToolRegistration(sdkTool);
                if (adaptedTool != null)
                {
                    toolRegistry.Register(adaptedTool);

                    lock (_lock)
                    {
                        _toolToPlugin[sdkTool.Id] = loadedPlugin.Manifest.Id;
                    }

                    LoggingService.Info("Registered tool id={ToolId} plugin={PluginId} pluginName={PluginName}", sdkTool.Id, loadedPlugin.Manifest.Id, loadedPlugin.Manifest.Name);
                }
            }
        }

        private static IToolRegistration? AdaptToolRegistration(SdkIToolRegistration sdkTool)
        {
            // Brush tools
            if (sdkTool is SdkBrushToolRegistration sdkBrush)
            {
                return new PluginBrushToolRegistration(sdkBrush);
            }

            // Shape tools
            if (sdkTool is SdkShapeToolRegistration sdkShape)
            {
                return new PluginShapeToolRegistration(sdkShape);
            }

            // Selection tools
            if (sdkTool is SdkSelectionToolRegistration sdkSelection)
            {
                return new PluginSelectionToolRegistration(sdkSelection);
            }

            // Utility tools
            if (sdkTool is SdkUtilityToolRegistration sdkUtility)
            {
                return new PluginUtilityToolRegistration(sdkUtility);
            }

            // Tile tools
            if (sdkTool is SdkTileToolRegistration sdkTile)
            {
                return new PluginTileToolRegistration(sdkTile);
            }

            LoggingService.Warning("Unsupported tool type {Type} for id={ToolId}", sdkTool.GetType().Name, sdkTool.Id);
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // EFFECT REGISTRATION (adapts SDK types to main project types)
        // ═══════════════════════════════════════════════════════════════════════

        private void RegisterPluginEffects(LoadedPlugin loadedPlugin)
        {
            if (loadedPlugin.Instance == null) return;

            var effectRegistry = EffectRegistry.Shared;
            var sdkEffects = loadedPlugin.Instance.GetEffectRegistrations();

            foreach (var sdkEffect in sdkEffects)
            {
                var adaptedEffect = new PluginEffectRegistration(sdkEffect);
                effectRegistry.Register(adaptedEffect);

                lock (_lock)
                {
                    _effectToPlugin[sdkEffect.Id] = loadedPlugin.Manifest.Id;
                }

                LoggingService.Info("Registered effect id={EffectId} plugin={PluginId} pluginName={PluginName}", sdkEffect.Id, loadedPlugin.Manifest.Id, loadedPlugin.Manifest.Name);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IMPORT/EXPORT REGISTRATION (SDK types are directly compatible)
        // ═══════════════════════════════════════════════════════════════════════

        private void RegisterPluginImportExport(LoadedPlugin loadedPlugin)
        {
            if (loadedPlugin.Instance == null) return;

            // Register import handlers
            var sdkImports = loadedPlugin.Instance.GetImportRegistrations();
            foreach (var sdkImport in sdkImports)
            {
                // SDK types are compatible via global usings - register directly
                ImportRegistry.Instance.Register(sdkImport);

                lock (_lock)
                {
                    _importToPlugin[sdkImport.Id] = loadedPlugin.Manifest.Id;
                }

                LoggingService.Info("Registered import id={ImportId} ext={Ext} plugin={PluginId} pluginName={PluginName}", sdkImport.Id, sdkImport.Format.Extension, loadedPlugin.Manifest.Id, loadedPlugin.Manifest.Name);
            }

            // Register export handlers
            var sdkExports = loadedPlugin.Instance.GetExportRegistrations();
            foreach (var sdkExport in sdkExports)
            {
                // SDK types are compatible via global usings - register directly
                ExportRegistry.Instance.Register(sdkExport);

                lock (_lock)
                {
                    _exportToPlugin[sdkExport.Id] = loadedPlugin.Manifest.Id;
                }

                LoggingService.Info("Registered export id={ExportId} ext={Ext} plugin={PluginId} pluginName={PluginName}", sdkExport.Id, sdkExport.Format.Extension, loadedPlugin.Manifest.Id, loadedPlugin.Manifest.Name);
            }
        }

        private void UnloadPluginInternal(LoadedPlugin plugin)
        {
            var toolRegistry = ToolRegistry.Shared;
            List<string> toolsToRemove;

            lock (_lock)
            {
                toolsToRemove = _toolToPlugin
                    .Where(kvp => kvp.Value == plugin.Manifest.Id)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var toolId in toolsToRemove)
                {
                    _toolToPlugin.Remove(toolId);
                }
            }

            foreach (var toolId in toolsToRemove)
            {
                toolRegistry.Unregister(toolId);
                LoggingService.Info("Unregistered tool id={ToolId} plugin={PluginId}", toolId, plugin.Manifest.Id);
            }

            var effectRegistry = EffectRegistry.Shared;
            List<string> effectsToRemove;

            lock (_lock)
            {
                effectsToRemove = _effectToPlugin
                    .Where(kvp => kvp.Value == plugin.Manifest.Id)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var effectId in effectsToRemove)
                {
                    _effectToPlugin.Remove(effectId);
                }
            }

            foreach (var effectId in effectsToRemove)
            {
                effectRegistry.Unregister(effectId);
                LoggingService.Info("Unregistered effect id={EffectId} plugin={PluginId}", effectId, plugin.Manifest.Id);
            }

            // Unregister import handlers
            List<string> importsToRemove;
            lock (_lock)
            {
                importsToRemove = _importToPlugin
                    .Where(kvp => kvp.Value == plugin.Manifest.Id)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var importId in importsToRemove)
                {
                    _importToPlugin.Remove(importId);
                }
            }

            foreach (var importId in importsToRemove)
            {
                ImportRegistry.Instance.Unregister(importId);
                LoggingService.Info("Unregistered import id={ImportId} plugin={PluginId}", importId, plugin.Manifest.Id);
            }

            // Unregister export handlers
            List<string> exportsToRemove;
            lock (_lock)
            {
                exportsToRemove = _exportToPlugin
                    .Where(kvp => kvp.Value == plugin.Manifest.Id)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var exportId in exportsToRemove)
                {
                    _exportToPlugin.Remove(exportId);
                }
            }

            foreach (var exportId in exportsToRemove)
            {
                ExportRegistry.Instance.Unregister(exportId);
                LoggingService.Info("Unregistered export id={ExportId} plugin={PluginId}", exportId, plugin.Manifest.Id);
            }

            plugin.Dispose();
            LoggingService.Info("Disposed plugin pluginId={PluginId} name={Name}", plugin.Manifest.Id, plugin.Manifest.Name);
        }

        /// <summary>Disposes the plugin registry and unloads all plugins.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            List<LoadedPlugin> plugins;
            lock (_lock)
            {
                plugins = _plugins.Values.ToList();
                _plugins.Clear();
                _toolToPlugin.Clear();
                _effectToPlugin.Clear();
                _importToPlugin.Clear();
                _exportToPlugin.Clear();
            }

            foreach (var plugin in plugins)
            {
                try
                {
                    plugin.Dispose();
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Error disposing plugin pluginId={PluginId}", ex, plugin.Manifest.Id);
                }
            }
        }
    }
}
