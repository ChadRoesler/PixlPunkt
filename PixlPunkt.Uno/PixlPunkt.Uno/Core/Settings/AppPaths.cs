using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PixlPunkt.Uno.Core.Settings
{
    /// <summary>
    /// Centralized storage path management for all application data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All PixlPunkt data is stored in the application's local data folder.
    /// On Windows: %LocalAppData%\PixlPunkt
    /// On macOS: ~/Library/Application Support/PixlPunkt
    /// On Linux: ~/.local/share/PixlPunkt
    /// </para>
    /// </remarks>
    public static class AppPaths
    {
        /// <summary>
        /// The application name used for directory paths.
        /// </summary>
        public const string AppName = "PixlPunkt";

        private static string? _rootDirectory;
        private static bool _initialized;

        /// <summary>
        /// Gets the root application data directory.
        /// </summary>
        public static string RootDirectory
        {
            get
            {
                if (_rootDirectory == null)
                {
                    _rootDirectory = GetRootDirectory();
                }
                return _rootDirectory;
            }
        }

        private static string GetRootDirectory()
        {
            try
            {
                // Use platform-specific paths that work reliably without requiring
                // Windows.Storage.ApplicationData (which may not work on macOS/Linux)
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows: Use %LocalAppData%\PixlPunkt
                    return GetWindowsPath();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // macOS: Use ~/Library/Application Support/PixlPunkt
                    return GetMacOSPath();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Linux: Use ~/.local/share/PixlPunkt or $XDG_DATA_HOME/PixlPunkt
                    return GetLinuxPath();
                }
                else
                {
                    // Unknown OS: fall back to LocalApplicationData
                    return GetFallbackPath();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppPaths] Error determining root directory: {ex.Message}");
                return GetFallbackPath();
            }
        }

        private static string GetWindowsPath()
        {
            try
            {
                // First try to use Windows.Storage.ApplicationData for packaged apps
                try
                {
                    var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                    System.Diagnostics.Debug.WriteLine($"[AppPaths] Using ApplicationData.LocalFolder: {localFolder.Path}");
                    return localFolder.Path;
                }
                catch (InvalidOperationException)
                {
                    // App is running unpackaged
                    System.Diagnostics.Debug.WriteLine("[AppPaths] Running unpackaged on Windows");
                }
            }
            catch
            {
                // Windows.Storage not available
            }

            // Fall back to traditional Windows path
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                return Path.Combine(localAppData, AppName);
            }

            return GetFallbackPath();
        }

        private static string GetMacOSPath()
        {
            // macOS: ~/Library/Application Support/PixlPunkt
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
            {
                var appSupport = Path.Combine(home, "Library", "Application Support", AppName);
                System.Diagnostics.Debug.WriteLine($"[AppPaths] Using macOS Application Support: {appSupport}");
                return appSupport;
            }

            // Alternative: try HOME environment variable
            home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                return Path.Combine(home, "Library", "Application Support", AppName);
            }

            return GetFallbackPath();
        }

        private static string GetLinuxPath()
        {
            // Linux: Use XDG Base Directory specification
            // XDG_DATA_HOME defaults to ~/.local/share
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdgDataHome))
            {
                return Path.Combine(xdgDataHome, AppName);
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
            {
                home = Environment.GetEnvironmentVariable("HOME");
            }

            if (!string.IsNullOrEmpty(home))
            {
                var linuxPath = Path.Combine(home, ".local", "share", AppName);
                System.Diagnostics.Debug.WriteLine($"[AppPaths] Using Linux path: {linuxPath}");
                return linuxPath;
            }

            return GetFallbackPath();
        }

        private static string GetFallbackPath()
        {
            // Ultimate fallback: use LocalApplicationData folder
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    return Path.Combine(localAppData, AppName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppPaths] Error getting LocalApplicationData: {ex.Message}");
            }

            // Last resort: use temp directory
            System.Diagnostics.Debug.WriteLine("[AppPaths] WARNING: Using temp directory as fallback");
            return Path.Combine(Path.GetTempPath(), AppName);
        }

        /// <summary>
        /// Gets the path to the settings JSON file.
        /// </summary>
        public static string SettingsFilePath => Path.Combine(RootDirectory, "settings.json");

        /// <summary>
        /// Gets the auto-save backup directory.
        /// </summary>
        public static string AutoSaveDirectory => Path.Combine(RootDirectory, "AutoSave");

        /// <summary>
        /// Gets the custom brushes directory.
        /// </summary>
        public static string BrushesDirectory => Path.Combine(RootDirectory, "Brushes");

        /// <summary>
        /// Gets the custom palettes directory.
        /// </summary>
        public static string PalettesDirectory => Path.Combine(RootDirectory, "Palettes");

        /// <summary>
        /// Gets the custom templates directory.
        /// </summary>
        public static string TemplatesDirectory => Path.Combine(RootDirectory, "Templates");

        /// <summary>
        /// Gets the plugins root directory (contains .punk files).
        /// </summary>
        public static string PluginsDirectory => Path.Combine(RootDirectory, "Plugins");

        /// <summary>
        /// Gets the directory for extracted plugin assemblies.
        /// </summary>
        public static string PluginsExtractedDirectory => Path.Combine(PluginsDirectory, ".extracted");

        /// <summary>
        /// Gets the directory for plugin-specific data storage.
        /// </summary>
        public static string PluginsDataDirectory => Path.Combine(PluginsDirectory, ".data");

        /// <summary>
        /// Gets the logs directory.
        /// </summary>
        public static string LogsDirectory => Path.Combine(RootDirectory, "Logs");

        /// <summary>
        /// Gets the custom glyph sets directory for ASCII effect.
        /// </summary>
        public static string GlyphSetsDirectory => Path.Combine(RootDirectory, "GlyphSets");

        /// <summary>
        /// Gets the temporary directory for transient data like history offloading.
        /// </summary>
        public static string TempDirectory => Path.Combine(RootDirectory, "Temp");

        /// <summary>
        /// Ensures the root directory and all standard subdirectories exist.
        /// </summary>
        public static void EnsureDirectoriesExist()
        {
            if (_initialized) return;

            try
            {
                // Create root first - this is critical
                EnsureDirectoryExists(RootDirectory);

                // Then create all subdirectories
                EnsureDirectoryExists(AutoSaveDirectory);
                EnsureDirectoryExists(BrushesDirectory);
                EnsureDirectoryExists(PalettesDirectory);
                EnsureDirectoryExists(TemplatesDirectory);
                EnsureDirectoryExists(PluginsDirectory);
                EnsureDirectoryExists(LogsDirectory);
                EnsureDirectoryExists(GlyphSetsDirectory);
                EnsureDirectoryExists(TempDirectory);

                _initialized = true;
                System.Diagnostics.Debug.WriteLine($"[AppPaths] Directories initialized at: {RootDirectory}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppPaths] ERROR initializing directories: {ex.Message}");
                // Don't rethrow - let app continue with whatever directories are available
            }
        }

        /// <summary>
        /// Ensures a specific directory exists.
        /// </summary>
        /// <param name="path">The directory path to create if it doesn't exist.</param>
        public static void EnsureDirectoryExists(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    System.Diagnostics.Debug.WriteLine($"[AppPaths] Created directory: {path}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppPaths] ERROR creating directory '{path}': {ex.Message}");
                // Still try to continue - some directories may work even if others fail
            }
        }

        /// <summary>
        /// Combines the root directory with additional path segments.
        /// </summary>
        /// <param name="paths">Path segments to combine with the root directory.</param>
        /// <returns>Full path under the root directory.</returns>
        public static string Combine(params string[] paths)
        {
            var allPaths = new string[paths.Length + 1];
            allPaths[0] = RootDirectory;
            Array.Copy(paths, 0, allPaths, 1, paths.Length);
            return Path.Combine(allPaths);
        }
    }
}
