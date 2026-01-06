using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixlPunkt.Uno.Core.Serialization;

namespace PixlPunkt.Uno.Core.Plugins
{
    /// <summary>
    /// Represents the manifest.json metadata for a plugin package.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The manifest is stored in the root of a <c>.punk</c> file and describes:
    /// </para>
    /// <list type="bullet">
    /// <item>Plugin identity (id, name, version, author)</item>
    /// <item>Entry point assembly</item>
    /// <item>Tool declarations</item>
    /// <item>Compatibility requirements</item>
    /// </list>
    /// <para>
    /// <strong>Example manifest.json:</strong>
    /// </para>
    /// <code>
    /// {
    ///   "id": "com.example.sparkletools",
    ///   "name": "Sparkle Tools",
    ///   "version": "1.0.0",
    ///   "author": "Example Inc.",
    ///   "description": "Adds sparkle and glitter brush effects",
    ///   "minApiVersion": 1,
    ///   "entryPoint": "SparkleTools.dll",
    ///   "pluginClass": "SparkleTools.SparklePlugin",
    ///   "tools": [
    ///     {
    ///       "id": "com.example.brush.sparkle",
    ///       "category": "Brush",
    ///       "name": "Sparkle Brush"
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
    public sealed class PluginManifest
    {
        /// <summary>
        /// Gets or sets the unique plugin identifier.
        /// </summary>
        /// <value>
        /// A string following <c>{vendor}.{name}</c> convention.
        /// </value>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable plugin name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plugin version (semantic versioning recommended).
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the plugin author or organization.
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plugin description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the minimum host API version required.
        /// </summary>
        /// <value>
        /// The minimum API version number (default: 1).
        /// </value>
        [JsonPropertyName("minApiVersion")]
        public int MinApiVersion { get; set; } = 1;

        /// <summary>
        /// Gets or sets the entry point assembly filename.
        /// </summary>
        /// <value>
        /// The DLL filename relative to the plugin root (e.g., "MyPlugin.dll").
        /// </value>
        [JsonPropertyName("entryPoint")]
        public string EntryPoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the fully qualified name of the plugin class.
        /// </summary>
        /// <value>
        /// The type name including namespace (e.g., "MyPlugin.MyPluginClass").
        /// </value>
        [JsonPropertyName("pluginClass")]
        public string PluginClass { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of tool declarations.
        /// </summary>
        /// <remarks>
        /// Tool declarations are informational and used for display purposes.
        /// Actual tool registration happens via <see cref="IPlugin.GetToolRegistrations"/>.
        /// </remarks>
        [JsonPropertyName("tools")]
        public List<ToolDeclaration> Tools { get; set; } = new();

        /// <summary>
        /// Gets or sets optional plugin tags for categorization.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Gets or sets the plugin homepage URL.
        /// </summary>
        [JsonPropertyName("homepage")]
        public string? Homepage { get; set; }

        /// <summary>
        /// Gets or sets the plugin icon filename (relative to plugin root).
        /// </summary>
        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        /// <summary>
        /// Parses a manifest from JSON string.
        /// </summary>
        /// <param name="json">The JSON string to parse.</param>
        /// <returns>A parsed <see cref="PluginManifest"/> instance.</returns>
        /// <exception cref="JsonException">If the JSON is invalid.</exception>
        public static PluginManifest Parse(string json)
        {
            return JsonSerializer.Deserialize(json, PluginManifestJsonContext.Default.PluginManifest)
                ?? throw new JsonException("Failed to parse plugin manifest");
        }

        /// <summary>
        /// Serializes the manifest to JSON.
        /// </summary>
        /// <returns>A JSON string representation of the manifest.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, PluginManifestJsonContext.Default.PluginManifest);
        }

        /// <summary>
        /// Validates the manifest has required fields.
        /// </summary>
        /// <returns>A list of validation errors, empty if valid.</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Id))
                errors.Add("Plugin 'id' is required");

            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("Plugin 'name' is required");

            if (string.IsNullOrWhiteSpace(EntryPoint))
                errors.Add("Plugin 'entryPoint' is required");

            if (string.IsNullOrWhiteSpace(PluginClass))
                errors.Add("Plugin 'pluginClass' is required");

            if (!Id.Contains('.'))
                errors.Add("Plugin 'id' should follow vendor.name convention");

            return errors;
        }
    }

    /// <summary>
    /// Declares a tool provided by the plugin (informational).
    /// </summary>
    public sealed class ToolDeclaration
    {
        /// <summary>
        /// Gets or sets the tool identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tool category (Brush, Shape, Select, Utility).
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tool display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional tool description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
