namespace PixlPunkt.Uno.Core.Compositing.Serialization
{
    /// <summary>
    /// Represents a serialized layer effect for storage in .pxp files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This structure captures all metadata needed to serialize/deserialize effects,
    /// including support for plugin effects that may not be available at load time.
    /// </para>
    /// <para>
    /// <strong>Binary Format (per effect):</strong>
    /// </para>
    /// <list type="number">
    /// <item>EffectId (string) - Unique effect identifier</item>
    /// <item>PluginId (string) - Plugin ID or empty for built-in</item>
    /// <item>DisplayName (string) - Human-readable name for warnings</item>
    /// <item>IsEnabled (bool) - Effect enabled state</item>
    /// <item>DataLength (int) - Length of property data</item>
    /// <item>Data (byte[]) - Serialized effect properties</item>
    /// </list>
    /// </remarks>
    public readonly struct SerializedEffect
    {
        /// <summary>
        /// Gets the unique effect identifier (e.g., "pixlpunkt.effect.ascii").
        /// </summary>
        public string EffectId { get; init; }

        /// <summary>
        /// Gets the plugin ID if this is a plugin effect, or empty/null for built-in effects.
        /// </summary>
        public string? PluginId { get; init; }

        /// <summary>
        /// Gets the human-readable display name for UI and warning messages.
        /// </summary>
        public string DisplayName { get; init; }

        /// <summary>
        /// Gets whether the effect was enabled when saved.
        /// </summary>
        public bool IsEnabled { get; init; }

        /// <summary>
        /// Gets the serialized property data (JSON or binary blob).
        /// </summary>
        public byte[] Data { get; init; }

        /// <summary>
        /// Gets whether this effect came from a plugin (vs built-in).
        /// </summary>
        public bool IsFromPlugin => !string.IsNullOrEmpty(PluginId);

        /// <summary>
        /// Creates a SerializedEffect from a live effect instance.
        /// </summary>
        /// <param name="effect">The effect to serialize.</param>
        /// <param name="pluginId">The plugin ID if this is a plugin effect.</param>
        /// <param name="data">The serialized property data.</param>
        /// <returns>A new SerializedEffect instance.</returns>
        public static SerializedEffect FromEffect(LayerEffectBase effect, string? pluginId, byte[] data)
        {
            return new SerializedEffect
            {
                EffectId = effect.EffectId ?? string.Empty,
                PluginId = pluginId,
                DisplayName = effect.DisplayName,
                IsEnabled = effect.IsEnabled,
                Data = data
            };
        }
    }
}
