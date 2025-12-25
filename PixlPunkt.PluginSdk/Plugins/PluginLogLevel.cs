namespace PixlPunkt.PluginSdk.Plugins
{
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
}
