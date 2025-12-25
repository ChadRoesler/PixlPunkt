using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.Tools.Builders
{
    /// <summary>
    /// Base class for fluent tool builders providing common configuration methods.
    /// </summary>
    /// <typeparam name="TBuilder">The concrete builder type for fluent chaining.</typeparam>
    /// <typeparam name="TRegistration">The registration type produced by this builder.</typeparam>
    /// <remarks>
    /// <para>
    /// ToolBuilderBase provides a fluent API for constructing tool registrations with
    /// reduced boilerplate. Common configuration (Id, DisplayName, Settings) is handled
    /// by the base class, while category-specific configuration is added by derived builders.
    /// </para>
    /// <para>
    /// <strong>Usage Pattern:</strong>
    /// <code>
    /// ToolBuilders.BrushTool("myplugin.brush.sparkle")
    ///     .WithDisplayName("Sparkle Brush")
    ///     .WithSettings(mySettings)
    ///     .WithPainter(() => new SparklePainter())
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public abstract class ToolBuilderBase<TBuilder, TRegistration>
        where TBuilder : ToolBuilderBase<TBuilder, TRegistration>
        where TRegistration : IToolRegistration
    {
        /// <summary>The unique tool identifier.</summary>
        protected readonly string Id;

        /// <summary>The tool category.</summary>
        protected readonly ToolCategory Category;

        /// <summary>Human-readable display name.</summary>
        protected string? DisplayName;

        /// <summary>Tool-specific settings object.</summary>
        protected ToolSettingsBase? Settings;

        /// <summary>Whether shortcut conflicts should be ignored.</summary>
        protected bool AllowShortcutConflicts;

        /// <summary>
        /// Initializes a new tool builder.
        /// </summary>
        /// <param name="id">The unique tool identifier.</param>
        /// <param name="category">The tool category.</param>
        protected ToolBuilderBase(string id, ToolCategory category)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Category = category;
        }

        /// <summary>
        /// Sets the human-readable display name for the tool.
        /// </summary>
        /// <param name="displayName">The display name shown in UI.</param>
        /// <returns>This builder for fluent chaining.</returns>
        public TBuilder WithDisplayName(string displayName)
        {
            DisplayName = displayName;
            return (TBuilder)this;
        }

        /// <summary>
        /// Sets the tool settings object.
        /// </summary>
        /// <param name="settings">The settings object containing tool configuration.</param>
        /// <returns>This builder for fluent chaining.</returns>
        /// <remarks>
        /// <para>
        /// If the settings define a <see cref="ToolSettingsBase.Shortcut"/> that conflicts
        /// with a built-in tool, a warning will be shown at runtime unless:
        /// </para>
        /// <list type="bullet">
        /// <item>The settings class has <see cref="AllowShortcutOverrideAttribute"/></item>
        /// <item><see cref="WithAllowShortcutConflict"/> was called on the builder</item>
        /// </list>
        /// </remarks>
        public TBuilder WithSettings(ToolSettingsBase? settings)
        {
            Settings = settings;
            return (TBuilder)this;
        }

        /// <summary>
        /// Allows this tool's shortcut to conflict with built-in tools without warning.
        /// </summary>
        /// <returns>This builder for fluent chaining.</returns>
        /// <remarks>
        /// <para>
        /// Call this method when you intentionally want your tool to use a shortcut
        /// that conflicts with a built-in PixlPunkt tool. This suppresses the
        /// conflict warning shown to users at startup.
        /// </para>
        /// <para>
        /// <strong>Example:</strong>
        /// <code>
        /// ToolBuilders.BrushTool("myplugin.brush.custom")
        ///     .WithDisplayName("Custom Brush")
        ///     .WithSettings(customSettings) // Has shortcut 'B' like built-in Brush
        ///     .WithAllowShortcutConflict()  // Suppress warning
        ///     .WithPainter(() => new CustomPainter())
        ///     .Build();
        /// </code>
        /// </para>
        /// </remarks>
        public TBuilder WithAllowShortcutConflict()
        {
            AllowShortcutConflicts = true;
            return (TBuilder)this;
        }

        /// <summary>
        /// Builds the registration.
        /// </summary>
        /// <returns>The built registration.</returns>
        public abstract TRegistration Build();

        /// <summary>
        /// Gets the effective display name, defaulting to the Id suffix if not set.
        /// </summary>
        protected string EffectiveDisplayName => DisplayName ?? ExtractNameFromId(Id);

        /// <summary>
        /// Validates the shortcut and emits a warning if it conflicts with built-in tools.
        /// </summary>
        protected void ValidateShortcut()
        {
            if (Settings?.Shortcut == null)
                return;

            // Check if override is explicitly allowed
            if (AllowShortcutConflicts)
                return;

            // Check for AllowShortcutOverrideAttribute on the settings class
            var settingsType = Settings.GetType();
            if (settingsType.GetCustomAttributes(typeof(AllowShortcutOverrideAttribute), false).Length > 0)
                return;

            // Check for conflict with built-in shortcuts
            if (BuiltInShortcuts.ConflictsWithBuiltIn(Settings.Shortcut))
            {
                var conflictingTool = BuiltInShortcuts.GetConflictingToolName(Settings.Shortcut);
                System.Diagnostics.Debug.WriteLine(
                    $"[PixlPunkt.PluginSdk] Warning: Tool '{Id}' has shortcut '{Settings.Shortcut}' " +
                    $"which conflicts with built-in tool '{conflictingTool}'. " +
                    $"Consider using a different shortcut or call WithAllowShortcutConflict() to suppress this warning.");
            }
        }

        /// <summary>
        /// Extracts a display name from a tool ID (takes last segment after dot).
        /// </summary>
        private static string ExtractNameFromId(string id)
        {
            var lastDot = id.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < id.Length - 1)
            {
                var name = id[(lastDot + 1)..];
                // Capitalize first letter
                return char.ToUpperInvariant(name[0]) + name[1..];
            }
            return id;
        }
    }
}
