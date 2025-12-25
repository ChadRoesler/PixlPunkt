using System;

namespace PixlPunkt.Core.Compositing.Effects
{
    /// <summary>
    /// Represents a layer effect from a plugin that is not currently loaded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When loading a .pxp file that contains effects from plugins that aren't installed
    /// or loaded, those effects are preserved as <see cref="OrphanedEffect"/> instances.
    /// This allows:
    /// </para>
    /// <list type="bullet">
    /// <item>The document to load without errors</item>
    /// <item>The user to see which effects are unavailable</item>
    /// <item>The effect data to be preserved when re-saving</item>
    /// <item>The effect to become active if the plugin is installed later</item>
    /// </list>
    /// <para>
    /// Orphaned effects are displayed in the UI with a warning indicator and are
    /// automatically disabled (they have no Apply implementation).
    /// </para>
    /// </remarks>
    public sealed class OrphanedEffect : LayerEffectBase
    {
        /// <summary>
        /// Gets the original effect ID from the saved file.
        /// </summary>
        public string OriginalEffectId { get; }

        /// <summary>
        /// Gets the plugin ID that provided this effect.
        /// </summary>
        public string OriginalPluginId { get; }

        /// <summary>
        /// Gets the original display name from the saved file.
        /// </summary>
        public string OriginalDisplayName { get; }

        /// <summary>
        /// Gets the preserved serialized data for this effect.
        /// </summary>
        /// <remarks>
        /// This data is kept intact so that re-saving the document preserves
        /// the original effect configuration, allowing it to be restored if
        /// the plugin is installed later.
        /// </remarks>
        public byte[] PreservedData { get; }

        /// <inheritdoc/>
        public override string DisplayName => $"? {OriginalDisplayName}";

        /// <summary>
        /// Gets a message describing why this effect is unavailable.
        /// </summary>
        public string UnavailableReason => $"Plugin '{OriginalPluginId}' is not installed";

        /// <summary>
        /// Gets whether this effect is orphaned (from a missing plugin).
        /// </summary>
        public bool IsOrphaned => true;

        /// <summary>
        /// Creates a new orphaned effect from saved data.
        /// </summary>
        /// <param name="effectId">The original effect ID.</param>
        /// <param name="pluginId">The plugin ID that provided this effect.</param>
        /// <param name="displayName">The original display name.</param>
        /// <param name="isEnabled">Whether the effect was enabled when saved.</param>
        /// <param name="data">The preserved serialized property data.</param>
        public OrphanedEffect(
            string effectId,
            string pluginId,
            string displayName,
            bool isEnabled,
            byte[] data)
        {
            OriginalEffectId = effectId ?? throw new ArgumentNullException(nameof(effectId));
            OriginalPluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
            OriginalDisplayName = displayName ?? "Unknown Effect";
            PreservedData = data ?? Array.Empty<byte>();

            // Set the effect ID so it can be identified
            EffectId = effectId;

            // Orphaned effects are always disabled - they can't be applied
            IsEnabled = false;

            // Store the original enabled state in case plugin is loaded later
            _wasOriginallyEnabled = isEnabled;
        }

        private readonly bool _wasOriginallyEnabled;

        /// <summary>
        /// Gets whether this effect was enabled in the original saved file.
        /// </summary>
        public bool WasOriginallyEnabled => _wasOriginallyEnabled;

        /// <inheritdoc/>
        public override void Apply(Span<uint> pixels, int width, int height)
        {
            // No-op - orphaned effects cannot be applied
            // The effect is always disabled, but even if someone forces IsEnabled = true,
            // we don't do anything because we don't have the implementation
        }
    }
}
