using System;

namespace PixlPunkt.Uno.Core.Plugins
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

    /// <summary>
    /// Log levels for plugin messages.
    /// </summary>
    public enum PluginLogLevel
    {
        /// <summary>Detailed diagnostic information.</summary>
        Debug,

        /// <summary>General informational messages.</summary>
        Info,

        /// <summary>Potential issues that don't prevent operation.</summary>
        Warning,

        /// <summary>Errors that may affect plugin functionality.</summary>
        Error
    }

    /// <summary>
    /// Provides plugins with read-only access to the active canvas/document state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ICanvasContext"/> allows plugin tools to:
    /// </para>
    /// <list type="bullet">
    /// <item>Sample pixel colors from the active layer or composite</item>
    /// <item>Query document dimensions</item>
    /// <item>Access current foreground/background colors</item>
    /// <item>Check selection state and bounds</item>
    /// </list>
    /// <para>
    /// <strong>Thread Safety:</strong>
    /// Canvas context methods should only be called from the UI thread during tool operations.
    /// The pixel data may change between calls if the user performs other operations.
    /// </para>
    /// </remarks>
    public interface ICanvasContext
    {
        //////////////////////////////////////////////////////////////////
        // DOCUMENT INFO
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the width of the active document in pixels.
        /// </summary>
        int DocumentWidth { get; }

        /// <summary>
        /// Gets the height of the active document in pixels.
        /// </summary>
        int DocumentHeight { get; }

        /// <summary>
        /// Gets the number of layers in the active document.
        /// </summary>
        int LayerCount { get; }

        /// <summary>
        /// Gets the index of the currently active layer (0-based), or -1 if none.
        /// </summary>
        int ActiveLayerIndex { get; }

        //////////////////////////////////////////////////////////////////
        // COLORS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current foreground color in BGRA format (0xAARRGGBB).
        /// </summary>
        uint ForegroundColor { get; }

        /// <summary>
        /// Gets the current background color in BGRA format (0xAARRGGBB).
        /// </summary>
        uint BackgroundColor { get; }

        //////////////////////////////////////////////////////////////////
        // PIXEL SAMPLING
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Samples a pixel color from the active layer at the given coordinates.
        /// </summary>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns>The pixel color in BGRA format, or <c>null</c> if out of bounds.</returns>
        uint? SampleActiveLayer(int x, int y);

        /// <summary>
        /// Samples a pixel color from the composited (flattened) view.
        /// </summary>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns>The composited pixel color in BGRA format, or <c>null</c> if out of bounds.</returns>
        uint? SampleComposite(int x, int y);

        /// <summary>
        /// Samples a pixel color from a specific layer at the given coordinates.
        /// </summary>
        /// <param name="layerIndex">The layer index (0-based).</param>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns>The pixel color in BGRA format, or <c>null</c> if out of bounds or invalid layer.</returns>
        uint? SampleLayer(int layerIndex, int x, int y);

        //////////////////////////////////////////////////////////////////
        // SELECTION
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets whether there is an active selection.
        /// </summary>
        bool HasSelection { get; }

        /// <summary>
        /// Gets the bounding box of the current selection.
        /// </summary>
        /// <returns>A tuple of (X, Y, Width, Height), or <c>null</c> if no selection.</returns>
        (int X, int Y, int Width, int Height)? SelectionBounds { get; }

        /// <summary>
        /// Checks if a point is within the current selection mask.
        /// </summary>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns><c>true</c> if the point is selected; otherwise <c>false</c>.</returns>
        bool IsPointSelected(int x, int y);

        /// <summary>
        /// Gets the selection mask value at a point (0-255 for feathered selections).
        /// </summary>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns>Selection intensity (0 = not selected, 255 = fully selected).</returns>
        byte GetSelectionMask(int x, int y);
    }
}
