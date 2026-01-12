using System.Text.Json.Serialization;

namespace PixlPunkt.Core.Settings
{
    /// <summary>
    /// Application-level animation default settings.
    /// These are used as defaults when creating new documents or when resetting animation state.
    /// </summary>
    public sealed class AnimationSettings
    {
        // ====================================================================
        // CANVAS ANIMATION DEFAULTS
        // ====================================================================

        /// <summary>
        /// Gets or sets the default frame count for new canvas animations.
        /// </summary>
        public int DefaultFrameCount { get; set; } = 24;

        /// <summary>
        /// Gets or sets the default frames per second for canvas animations.
        /// </summary>
        public int DefaultFps { get; set; } = 12;

        /// <summary>
        /// Gets or sets whether auto-keyframe creation is enabled by default.
        /// When enabled, painting on an animated layer automatically creates a keyframe.
        /// </summary>
        public bool DefaultAutoKeyframe { get; set; } = true;

        /// <summary>
        /// Gets or sets whether canvas animations loop by default.
        /// </summary>
        public bool DefaultLoop { get; set; } = true;

        /// <summary>
        /// Gets or sets whether ping-pong playback is enabled by default.
        /// </summary>
        public bool DefaultPingPong { get; set; } = false;

        /// <summary>
        /// Gets or sets whether onion skinning is enabled by default for canvas animation.
        /// </summary>
        public bool DefaultOnionSkinEnabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the default number of onion skin frames to show before the current frame.
        /// </summary>
        public int DefaultOnionSkinFramesBefore { get; set; } = 2;

        /// <summary>
        /// Gets or sets the default number of onion skin frames to show after the current frame.
        /// </summary>
        public int DefaultOnionSkinFramesAfter { get; set; } = 1;

        /// <summary>
        /// Gets or sets the default onion skin opacity (0.0 to 1.0).
        /// </summary>
        public float DefaultOnionSkinOpacity { get; set; } = 0.3f;

        // ====================================================================
        // TILE ANIMATION DEFAULTS
        // ====================================================================

        /// <summary>
        /// Gets or sets the default frame duration in milliseconds for tile animations.
        /// </summary>
        public int DefaultTileFrameTimeMs { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether tile animations loop by default.
        /// </summary>
        public bool DefaultTileLoop { get; set; } = true;

        /// <summary>
        /// Gets or sets whether tile animations use ping-pong playback by default.
        /// </summary>
        public bool DefaultTilePingPong { get; set; } = false;

        /// <summary>
        /// Gets or sets whether onion skinning is enabled by default for tile animation.
        /// </summary>
        public bool DefaultTileOnionSkinEnabled { get; set; } = false;

        // ====================================================================
        // TIMELINE DEFAULTS
        // ====================================================================

        /// <summary>
        /// Gets or sets whether the timeline auto-scrolls to follow the playhead during playback.
        /// </summary>
        public bool AutoScrollPlayhead { get; set; } = true;

        /// <summary>
        /// Creates a new instance with default values.
        /// </summary>
        public AnimationSettings()
        {
        }

        /// <summary>
        /// Validates and clamps all settings to valid ranges.
        /// </summary>
        public void Validate()
        {
            DefaultFrameCount = System.Math.Clamp(DefaultFrameCount, 1, 9999);
            DefaultFps = System.Math.Clamp(DefaultFps, 1, 120);
            DefaultOnionSkinFramesBefore = System.Math.Clamp(DefaultOnionSkinFramesBefore, 0, 10);
            DefaultOnionSkinFramesAfter = System.Math.Clamp(DefaultOnionSkinFramesAfter, 0, 10);
            DefaultOnionSkinOpacity = System.Math.Clamp(DefaultOnionSkinOpacity, 0f, 1f);
            DefaultTileFrameTimeMs = System.Math.Clamp(DefaultTileFrameTimeMs, 10, 10000);
        }
    }
}
