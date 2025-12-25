namespace PixlPunkt.PluginSdk.Tools
{
    /// <summary>
    /// Indicates that a tool's shortcut is intentionally overriding a built-in shortcut.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Apply this attribute to a tool settings class when you intentionally want to use
    /// a shortcut that conflicts with a built-in PixlPunkt tool. Without this attribute,
    /// a warning will be displayed to users at startup when shortcut conflicts are detected.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// </para>
    /// <code>
    /// [AllowShortcutOverride("Intentionally replacing the Brush tool shortcut for this workflow")]
    /// public sealed class MyToolSettings : ToolSettingsBase
    /// {
    ///     public override KeyBinding? Shortcut => new(VirtualKey.B); // Same as Brush
    /// }
    /// </code>
    /// <para>
    /// <strong>When to use:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>Your plugin provides a specialized replacement for a built-in tool</item>
    /// <item>Your plugin is designed for a specific workflow that doesn't need the built-in tool</item>
    /// <item>Users have requested this behavior and understand the implications</item>
    /// </list>
    /// <para>
    /// <strong>Best Practice:</strong>
    /// Consider using different shortcuts (with modifiers) instead of overriding built-in tools.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AllowShortcutOverrideAttribute : Attribute
    {
        /// <summary>
        /// Gets the reason for allowing the override.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="AllowShortcutOverrideAttribute"/>.
        /// </summary>
        public AllowShortcutOverrideAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="AllowShortcutOverrideAttribute"/> with a reason.
        /// </summary>
        /// <param name="reason">The reason for allowing the shortcut override.</param>
        public AllowShortcutOverrideAttribute(string reason)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// Marks a tool shortcut as requiring validation at build time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute is used internally by the SDK to trigger build-time warnings
    /// when a plugin's shortcut conflicts with a built-in tool shortcut.
    /// </para>
    /// <para>
    /// Plugin developers do not need to apply this attribute directly - it is
    /// automatically considered during the plugin build process.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ValidateShortcutAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether to generate a build error (vs warning) on conflict.
        /// </summary>
        /// <value>
        /// When true, generates a build error. When false (default), generates a warning.
        /// </value>
        public bool TreatAsError { get; set; }
    }
}
