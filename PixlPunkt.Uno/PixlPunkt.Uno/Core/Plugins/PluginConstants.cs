namespace PixlPunkt.Uno.Core.Plugins
{
    /// <summary>
    /// Constants for the plugin system.
    /// </summary>
    public static class PluginConstants
    {
        /// <summary>
        /// The file extension for plugin packages.
        /// </summary>
        public const string PluginExtension = ".punk";

        /// <summary>
        /// The manifest filename within plugin packages.
        /// </summary>
        public const string ManifestFileName = "manifest.json";

        /// <summary>
        /// The current plugin API version.
        /// </summary>
        public const int CurrentApiVersion = 1;

        /// <summary>
        /// The application name used for directory paths.
        /// </summary>
        public const string AppName = "PixlPunkt";

        /// <summary>
        /// The plugins subdirectory name.
        /// </summary>
        public const string PluginsDirectoryName = "Plugins";

        /// <summary>
        /// The extracted plugins subdirectory name.
        /// </summary>
        public const string ExtractedDirectoryName = ".extracted";

        /// <summary>
        /// The plugin data subdirectory name.
        /// </summary>
        public const string DataDirectoryName = ".data";

        /// <summary>
        /// MIME type for .punk files.
        /// </summary>
        public const string PluginMimeType = "application/x-pixlpunkt-plugin";

        /// <summary>
        /// File type description for .punk files.
        /// </summary>
        public const string PluginFileTypeDescription = "PixlPunkt Plugin";
    }
}
