using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using PixlPunkt.Core.Logging;
using SdkICanvasContext = PixlPunkt.PluginSdk.Plugins.ICanvasContext;
// Use SDK types for plugin interface
using SdkIPlugin = PixlPunkt.PluginSdk.Plugins.IPlugin;
using SdkIPluginContext = PixlPunkt.PluginSdk.Plugins.IPluginContext;
using SdkPluginLogLevel = PixlPunkt.PluginSdk.Plugins.PluginLogLevel;

namespace PixlPunkt.Core.Plugins
{
    public sealed class PluginLoader
    {
        public const int CurrentApiVersion = 2;
        public const string PluginExtension = ".punk";
        public const string ManifestFileName = "manifest.json";

        private readonly string _pluginsDirectory;
        private readonly string _extractedDirectory;
        private readonly string _dataDirectory;

        public static Func<SdkICanvasContext?>? CanvasContextProvider { get; set; }

        public PluginLoader(string pluginsDirectory, string extractedDirectory, string dataDirectory)
        {
            _pluginsDirectory = pluginsDirectory ?? throw new ArgumentNullException(nameof(pluginsDirectory));
            _extractedDirectory = extractedDirectory ?? throw new ArgumentNullException(nameof(extractedDirectory));
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));

            Directory.CreateDirectory(_pluginsDirectory);
            Directory.CreateDirectory(_extractedDirectory);
            Directory.CreateDirectory(_dataDirectory);
        }

        public LoadedPlugin LoadFromPackage(string punkFilePath)
        {
            if (!File.Exists(punkFilePath))
                throw new PluginLoadException($"Plugin file not found: {punkFilePath}");

            if (!punkFilePath.EndsWith(PluginExtension, StringComparison.OrdinalIgnoreCase))
                throw new PluginLoadException($"Invalid plugin file extension. Expected {PluginExtension}");

            string pluginName = Path.GetFileNameWithoutExtension(punkFilePath);
            string extractPath = Path.Combine(_extractedDirectory, pluginName);

            try
            {
                LoggingService.Info("Extracting plugin package {FilePath}", punkFilePath);
                ExtractPlugin(punkFilePath, extractPath);

                var loaded = LoadFromDirectory(extractPath);
                LoggingService.Info("Plugin package loaded pluginId={PluginId} file={FilePath}", loaded.Manifest.Id, punkFilePath);
                return loaded;
            }
            catch (PluginLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to load plugin from package {FilePath}", ex);
                throw new PluginLoadException($"Failed to load plugin: {ex.Message}", ex);
            }
        }

        public LoadedPlugin LoadFromDirectory(string pluginDirectory)
        {
            string manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
            if (!File.Exists(manifestPath))
                throw new PluginLoadException($"Manifest not found: {manifestPath}");

            PluginManifest manifest;
            try
            {
                string manifestJson = File.ReadAllText(manifestPath);
                manifest = PluginManifest.Parse(manifestJson);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Invalid plugin manifest path={ManifestPath}", ex);
                throw new PluginLoadException($"Invalid manifest: {ex.Message}", ex);
            }

            var errors = manifest.Validate();
            if (errors.Count > 0)
            {
                LoggingService.Warning("Plugin manifest validation failed pluginId={PluginId} errors={Errors}", manifest.Id, string.Join("; ", errors));
                throw new PluginLoadException($"Invalid manifest: {string.Join("; ", errors)}");
            }

            if (manifest.MinApiVersion > CurrentApiVersion)
            {
                LoggingService.Error("Plugin requires newer API version pluginId={PluginId} required={RequiredApi} host={HostApi}", manifest.Id, manifest.MinApiVersion, CurrentApiVersion);
                throw new PluginLoadException(
                    $"Plugin requires API version {manifest.MinApiVersion}, but host only supports version {CurrentApiVersion}");
            }

            string assemblyPath = Path.Combine(pluginDirectory, manifest.EntryPoint);
            if (!File.Exists(assemblyPath))
            {
                LoggingService.Error("Entry point assembly not found pluginId={PluginId} entryPoint={EntryPoint}", manifest.Id, manifest.EntryPoint);
                throw new PluginLoadException($"Entry point assembly not found: {manifest.EntryPoint}");
            }

            var loadContext = new PluginLoadContext(assemblyPath, manifest.Id);

            Assembly assembly;
            try
            {
                assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            }
            catch (Exception ex)
            {
                loadContext.Unload();
                LoggingService.Error("Failed to load assembly pluginId={PluginId} path={Path}", ex);
                throw new PluginLoadException($"Failed to load assembly: {ex.Message}", ex);
            }

            Type? pluginType = assembly.GetType(manifest.PluginClass);
            if (pluginType == null)
            {
                loadContext.Unload();
                LoggingService.Error("Plugin class not found pluginId={PluginId} class={Class}", manifest.Id, manifest.PluginClass);
                throw new PluginLoadException($"Plugin class not found: {manifest.PluginClass}");
            }

            if (!typeof(SdkIPlugin).IsAssignableFrom(pluginType))
            {
                loadContext.Unload();
                LoggingService.Error("Plugin class does not implement IPlugin pluginId={PluginId} class={Class}", manifest.Id, manifest.PluginClass);
                throw new PluginLoadException($"Plugin class does not implement IPlugin: {manifest.PluginClass}");
            }

            SdkIPlugin instance;
            try
            {
                instance = (SdkIPlugin)(Activator.CreateInstance(pluginType)
                    ?? throw new InvalidOperationException("Failed to create plugin instance"));
            }
            catch (Exception ex)
            {
                loadContext.Unload();
                LoggingService.Error("Failed to instantiate plugin pluginId={PluginId}", ex);
                throw new PluginLoadException($"Failed to instantiate plugin: {ex.Message}", ex);
            }

            var loadedPlugin = new LoadedPlugin(manifest, loadContext, instance, pluginDirectory);

            try
            {
                string pluginDataDir = Path.Combine(_dataDirectory, manifest.Id);
                Directory.CreateDirectory(pluginDataDir);

                var context = new PluginContextImpl(pluginDirectory, pluginDataDir, manifest.Id);

                LoggingService.Info("Initializing plugin pluginId={PluginId} name={Name} dir={Dir}", manifest.Id, manifest.Name, pluginDirectory);
                instance.Initialize(context);
                LoggingService.Info("Plugin initialized pluginId={PluginId} name={Name}", manifest.Id, manifest.Name);
            }
            catch (Exception ex)
            {
                loadedPlugin.Dispose();
                LoggingService.Error("Plugin initialization failed pluginId={PluginId}", ex);
                throw new PluginLoadException($"Plugin initialization failed: {ex.Message}", ex);
            }

            LoggingService.Info("Plugin loaded pluginId={PluginId}", manifest.Id);
            return loadedPlugin;
        }

        private static void ExtractPlugin(string punkFilePath, string extractPath)
        {
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, recursive: true);
            }

            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(punkFilePath, extractPath);
        }

        public string[] GetAvailablePlugins()
        {
            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
                return Array.Empty<string>();
            }
            return Directory.GetFiles(_pluginsDirectory, $"*{PluginExtension}");
        }

        private sealed class PluginContextImpl : SdkIPluginContext
        {
            private static readonly string _hostVersion = GetHostVersion();

            public string HostVersion => _hostVersion;
            public int ApiVersion => CurrentApiVersion;
            public string PluginDirectory { get; }
            public string DataDirectory { get; }
            public string PluginId { get; }

            public SdkICanvasContext? Canvas => CanvasContextProvider?.Invoke();

            public PluginContextImpl(string pluginDir, string dataDir, string pluginId)
            {
                PluginDirectory = pluginDir;
                DataDirectory = dataDir;
                PluginId = pluginId;
            }

            private static string GetHostVersion()
            {
                var assembly = typeof(PluginLoader).Assembly;
                var version = assembly.GetName().Version;
                return version?.ToString(3) ?? "1.0.0";
            }

            public void Log(SdkPluginLogLevel level, string message)
            {
                switch (level)
                {
                    case SdkPluginLogLevel.Debug:
                        LoggingService.Debug("[Plugin:{PluginId}] {Message}", PluginId, message);
                        break;
                    case SdkPluginLogLevel.Info:
                        LoggingService.Info("[Plugin:{PluginId}] {Message}", PluginId, message);
                        break;
                    case SdkPluginLogLevel.Warning:
                        LoggingService.Warning("[Plugin:{PluginId}] {Message}", PluginId, message);
                        break;
                    case SdkPluginLogLevel.Error:
                        LoggingService.Error("[Plugin:{PluginId}] {Message}", message);
                        break;
                    default:
                        LoggingService.Debug("[Plugin:{PluginId}] {Message}", PluginId, message);
                        break;
                }
            }

            public void LogError(string message, Exception exception)
            {
                LoggingService.Error($"[Plugin:{PluginId}] {message}", exception);
            }
        }
    }

    public sealed class PluginLoadException : Exception
    {
        public PluginLoadException(string message) : base(message) { }
        public PluginLoadException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
