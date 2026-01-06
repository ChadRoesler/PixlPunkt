using PixlPunkt.Uno.Core.Tools.Settings;

namespace PixlPunkt.Uno.Core.Tools.Builders
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
    /// registry.AddBrushTool("vendor.category.name")
    ///     .WithDisplayName("My Tool")
    ///     .WithSettings(mySettings)
    ///     .WithPainter(() => new MyPainter())
    ///     .Register();
    /// </code>
    /// </para>
    /// </remarks>
    public abstract class ToolBuilderBase<TBuilder, TRegistration>
        where TBuilder : ToolBuilderBase<TBuilder, TRegistration>
        where TRegistration : IToolRegistration
    {
        /// <summary>The registry to register the built tool with.</summary>
        protected readonly IToolRegistry Registry;

        /// <summary>The unique tool identifier.</summary>
        protected readonly string Id;

        /// <summary>The tool category.</summary>
        protected readonly ToolCategory Category;

        /// <summary>Human-readable display name.</summary>
        protected string? DisplayName;

        /// <summary>Tool-specific settings object.</summary>
        protected ToolSettingsBase? Settings;

        /// <summary>
        /// Initializes a new tool builder.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        /// <param name="category">The tool category.</param>
        protected ToolBuilderBase(IToolRegistry registry, string id, ToolCategory category)
        {
            Registry = registry;
            Id = id;
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
        public TBuilder WithSettings(ToolSettingsBase? settings)
        {
            Settings = settings;
            return (TBuilder)this;
        }

        /// <summary>
        /// Builds the registration and registers it with the registry.
        /// </summary>
        /// <returns>The created registration for further reference.</returns>
        public TRegistration Register()
        {
            var registration = Build();
            Registry.Register(registration);
            return registration;
        }

        /// <summary>
        /// Builds the registration without registering it.
        /// </summary>
        /// <returns>The built registration.</returns>
        public abstract TRegistration Build();

        /// <summary>
        /// Gets the effective display name, defaulting to the Id suffix if not set.
        /// </summary>
        protected string EffectiveDisplayName => DisplayName ?? ExtractNameFromId(Id);

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
