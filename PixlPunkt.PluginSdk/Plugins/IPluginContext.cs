namespace PixlPunkt.PluginSdk.Plugins
{
    /// <summary>
    /// Context interface providing plugins access to host services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IPluginContext"/> is passed to plugins during initialization and provides
    /// a safe, versioned API surface for plugins to interact with the host application.
    /// </para>
    /// <para>
    /// <strong>Design Rationale:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>Plugins should not access host internals directly</item>
    /// <item>This interface defines the stable API contract between host and plugins</item>
    /// <item>New capabilities can be added in future versions without breaking existing plugins</item>
    /// </list>
    /// </remarks>
    public interface IPluginContext
    {
        /// <summary>
        /// Gets the host application version.
        /// </summary>
        /// <value>
        /// The PixlPunkt version string (e.g., "1.0.0").
        /// </value>
        /// <remarks>
        /// Plugins can use this to check compatibility or enable version-specific features.
        /// </remarks>
        string HostVersion { get; }

        /// <summary>
        /// Gets the plugin API version supported by the host.
        /// </summary>
        /// <value>
        /// The API version number. Higher versions support more features.
        /// </value>
        /// <remarks>
        /// <para>
        /// API versions:
        /// </para>
        /// <list type="bullet">
        /// <item><strong>1</strong>: Initial plugin API (tools only)</item>
        /// <item><strong>2</strong>: Added <see cref="Canvas"/> for pixel sampling and selection access</item>
        /// </list>
        /// </remarks>
        int ApiVersion { get; }

        /// <summary>
        /// Gets the directory where the plugin is installed.
        /// </summary>
        /// <value>
        /// The full path to the plugin's extracted directory.
        /// </value>
        /// <remarks>
        /// Use this to load plugin-specific assets or configuration files.
        /// </remarks>
        string PluginDirectory { get; }

        /// <summary>
        /// Gets the directory for plugin-specific persistent data.
        /// </summary>
        /// <value>
        /// A writable directory where the plugin can store settings and cache data.
        /// </value>
        /// <remarks>
        /// This directory is preserved across plugin updates and app restarts.
        /// </remarks>
        string DataDirectory { get; }

        /// <summary>
        /// Gets the canvas context for the active document (API version 2+).
        /// </summary>
        /// <value>
        /// An <see cref="ICanvasContext"/> for sampling pixels and querying document state,
        /// or <c>null</c> if no document is open.
        /// </value>
        /// <remarks>
        /// <para>
        /// The canvas context provides read-only access to:
        /// </para>
        /// <list type="bullet">
        /// <item>Document dimensions and layer information</item>
        /// <item>Current foreground/background colors</item>
        /// <item>Pixel sampling from layers or composite</item>
        /// <item>Selection state and mask data</item>
        /// </list>
        /// </remarks>
        ICanvasContext? Canvas { get; }

        /// <summary>
        /// Logs a message to the host's log system.
        /// </summary>
        /// <param name="level">The log level (Debug, Info, Warning, Error).</param>
        /// <param name="message">The message to log.</param>
        void Log(PluginLogLevel level, string message);

        /// <summary>
        /// Logs an exception to the host's log system.
        /// </summary>
        /// <param name="message">A description of what was happening when the error occurred.</param>
        /// <param name="exception">The exception that was thrown.</param>
        void LogError(string message, Exception exception);
    }
}
