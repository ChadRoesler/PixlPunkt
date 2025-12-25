namespace PixlPunkt.PluginSdk.Plugins
{
    /// <summary>
    /// Specifies the unique identifier for a plugin assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute should follow the convention <c>{vendor}.{name}</c>.
    /// For example: <c>"com.example.sparkletools"</c>.
    /// </para>
    /// <para>
    /// If not specified, the assembly name is used as the plugin ID.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [assembly: PluginId("com.mycompany.mytools")]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class PluginIdAttribute : Attribute
    {
        /// <summary>
        /// Gets the plugin identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginIdAttribute"/> class.
        /// </summary>
        /// <param name="id">The unique plugin identifier.</param>
        public PluginIdAttribute(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }
    }

    /// <summary>
    /// Specifies the display name for a plugin assembly.
    /// </summary>
    /// <remarks>
    /// This name is shown in the plugin manager UI.
    /// If not specified, the assembly title or name is used.
    /// </remarks>
    /// <example>
    /// <code>
    /// [assembly: PluginDisplayName("My Custom Tools")]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class PluginDisplayNameAttribute : Attribute
    {
        /// <summary>
        /// Gets the display name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginDisplayNameAttribute"/> class.
        /// </summary>
        /// <param name="displayName">The human-readable display name.</param>
        public PluginDisplayNameAttribute(string displayName)
        {
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        }
    }

    /// <summary>
    /// Specifies the author or organization for a plugin assembly.
    /// </summary>
    /// <remarks>
    /// This is shown in the plugin manager UI and plugin details.
    /// If not specified, the assembly company attribute is used.
    /// </remarks>
    /// <example>
    /// <code>
    /// [assembly: PluginAuthor("My Company")]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class PluginAuthorAttribute : Attribute
    {
        /// <summary>
        /// Gets the author name.
        /// </summary>
        public string Author { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginAuthorAttribute"/> class.
        /// </summary>
        /// <param name="author">The author or organization name.</param>
        public PluginAuthorAttribute(string author)
        {
            Author = author ?? throw new ArgumentNullException(nameof(author));
        }
    }

    /// <summary>
    /// Specifies a description for a plugin assembly.
    /// </summary>
    /// <remarks>
    /// This description is shown in the plugin manager UI.
    /// If not specified, the assembly description attribute is used.
    /// </remarks>
    /// <example>
    /// <code>
    /// [assembly: PluginDescription("Awesome custom tools for PixlPunkt")]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class PluginDescriptionAttribute : Attribute
    {
        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginDescriptionAttribute"/> class.
        /// </summary>
        /// <param name="description">The plugin description.</param>
        public PluginDescriptionAttribute(string description)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }
    }

    /// <summary>
    /// Specifies the minimum required API version for a plugin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PixlPunkt will refuse to load plugins that require a higher API version
    /// than the host supports. This ensures forward compatibility.
    /// </para>
    /// <para>
    /// Current API versions:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>1</strong>: Initial plugin API (tools only)</item>
    /// <item><strong>2</strong>: Added canvas context for pixel sampling</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// [assembly: PluginMinApiVersion(2)]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class PluginMinApiVersionAttribute : Attribute
    {
        /// <summary>
        /// Gets the minimum required API version.
        /// </summary>
        public int MinApiVersion { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginMinApiVersionAttribute"/> class.
        /// </summary>
        /// <param name="minApiVersion">The minimum required API version.</param>
        public PluginMinApiVersionAttribute(int minApiVersion)
        {
            MinApiVersion = minApiVersion;
        }
    }
}
