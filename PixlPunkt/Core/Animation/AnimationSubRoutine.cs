using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Represents a sub-routine animation embedded within a canvas animation.
    /// Allows tile animations to be imported and played as nested animations with transform interpolation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Animation sub-routines enable complex animations where a tile animation (like a walking character)
    /// can be embedded in a canvas animation with additional transform effects (position, scale, rotation)
    /// applied over the duration of the sub-routine.
    /// </para>
    /// <para>
    /// <strong>Example Use Case:</strong> A character walks across the screen using a tile animation
    /// while the position is interpolated from left to right, creating the effect of movement across the canvas.
    /// </para>
    /// </remarks>
    public sealed class AnimationSubRoutine : INotifyPropertyChanged
    {
        // ====================================================================
        // IDENTITY
        // ====================================================================

        /// <summary>
        /// Gets the unique identifier for this sub-routine.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        // ====================================================================
        // FILE REFERENCE
        // ====================================================================

        private string _reelFilePath = string.Empty;

        /// <summary>
        /// Gets or sets the path to the tile animation reel file (.pxpr).
        /// </summary>
        /// <remarks>
        /// Can be absolute or relative to the project file.
        /// Empty string indicates no reel loaded.
        /// </remarks>
        public string ReelFilePath
        {
            get => _reelFilePath;
            set => SetProperty(ref _reelFilePath, value ?? string.Empty);
        }

        /// <summary>
        /// Gets whether a tile animation reel is loaded.
        /// </summary>
        public bool HasReel => !string.IsNullOrEmpty(_reelFilePath);

        /// <summary>
        /// Gets the reel file name (without path) for display.
        /// </summary>
        public string DisplayName => HasReel
            ? System.IO.Path.GetFileNameWithoutExtension(_reelFilePath)
            : "(No reel)";

        // ====================================================================
        // LOADED REEL DATA
        // ====================================================================

        /// <summary>
        /// Gets the loaded tile animation reel, or null if not loaded.
        /// </summary>
        [JsonIgnore]
        public TileAnimationReel? LoadedReel { get; private set; }

        /// <summary>
        /// Gets the pre-rendered frames from the loaded reel.
        /// Key = frame index, Value = pixel data (BGRA bytes).
        /// </summary>
        [JsonIgnore]
        public Dictionary<int, byte[]> RenderedFrames { get; } = new();

        /// <summary>
        /// Gets the width of the rendered frames in pixels.
        /// </summary>
        [JsonIgnore]
        public int FrameWidth { get; private set; }

        /// <summary>
        /// Gets the height of the rendered frames in pixels.
        /// </summary>
        [JsonIgnore]
        public int FrameHeight { get; private set; }

        /// <summary>
        /// Gets whether the reel is loaded and frames are rendered.
        /// </summary>
        [JsonIgnore]
        public bool IsLoaded => LoadedReel != null && RenderedFrames.Count > 0;

        /// <summary>
        /// Raised when the reel is loaded or unloaded.
        /// </summary>
        public event Action<bool>? ReelLoadedChanged;

        // ====================================================================
        // TIMING / POSITION
        // ====================================================================

        private int _startFrame;

        /// <summary>
        /// Gets or sets the start frame in the canvas animation timeline where this sub-routine begins.
        /// </summary>
        public int StartFrame
        {
            get => _startFrame;
            set => SetProperty(ref _startFrame, Math.Max(0, value));
        }

        private int _durationFrames = 1;

        /// <summary>
        /// Gets or sets the duration in frames for how long this sub-routine plays.
        /// </summary>
        /// <remarks>
        /// If the tile animation reel is shorter than this duration, it will loop or stop
        /// depending on the reel's settings. If longer, only the portion within this duration is played.
        /// </remarks>
        public int DurationFrames
        {
            get => _durationFrames;
            set => SetProperty(ref _durationFrames, Math.Max(1, value));
        }

        /// <summary>
        /// Gets the end frame (exclusive) where this sub-routine ends.
        /// </summary>
        public int EndFrame => StartFrame + DurationFrames;

        // ====================================================================
        // TRANSFORM KEYFRAMES
        // ====================================================================

        /// <summary>
        /// Gets or sets the position keyframes for interpolation over the duration.
        /// Key = normalized frame position (0.0 to 1.0), Value = position in pixels (X, Y).
        /// </summary>
        /// <remarks>
        /// At frame 0 (StartFrame), position is interpolated from the first keyframe.
        /// At frame DurationFrames (EndFrame), position is interpolated to the last keyframe.
        /// </remarks>
        public SortedDictionary<float, (double X, double Y)> PositionKeyframes { get; } = new();

        /// <summary>
        /// Gets or sets the scale keyframes for interpolation over the duration.
        /// Key = normalized frame position (0.0 to 1.0), Value = scale factor (1.0 = 100%).
        /// </summary>
        public SortedDictionary<float, float> ScaleKeyframes { get; } = new();

        /// <summary>
        /// Gets or sets the rotation keyframes for interpolation over the duration.
        /// Key = normalized frame position (0.0 to 1.0), Value = rotation in degrees.
        /// </summary>
        public SortedDictionary<float, float> RotationKeyframes { get; } = new();

        // ====================================================================
        // INTERPOLATION MODE
        // ====================================================================

        private InterpolationMode _positionInterpolation = InterpolationMode.Linear;

        /// <summary>
        /// Gets or sets the interpolation mode for position keyframes.
        /// </summary>
        public InterpolationMode PositionInterpolation
        {
            get => _positionInterpolation;
            set => SetProperty(ref _positionInterpolation, value);
        }

        private InterpolationMode _scaleInterpolation = InterpolationMode.Linear;

        /// <summary>
        /// Gets or sets the interpolation mode for scale keyframes.
        /// </summary>
        public InterpolationMode ScaleInterpolation
        {
            get => _scaleInterpolation;
            set => SetProperty(ref _scaleInterpolation, value);
        }

        private InterpolationMode _rotationInterpolation = InterpolationMode.Linear;

        /// <summary>
        /// Gets or sets the interpolation mode for rotation keyframes.
        /// </summary>
        public InterpolationMode RotationInterpolation
        {
            get => _rotationInterpolation;
            set => SetProperty(ref _rotationInterpolation, value);
        }

        // ====================================================================
        // Z-ORDER / RENDERING ORDER
        // ====================================================================

        private int _zOrder;

        /// <summary>
        /// Gets or sets the Z-order for this sub-routine relative to layers.
        /// Lower values render first (behind), higher values render last (in front).
        /// A value of 0 places the sub-routine at the bottom of the layer stack.
        /// Use negative values to place behind all layers.
        /// </summary>
        /// <remarks>
        /// The Z-order determines where this sub-routine appears in the timeline
        /// and in what order it's composited during rendering. Sub-routines can
        /// be interleaved with layers to create foreground/background effects.
        /// </remarks>
        public int ZOrder
        {
            get => _zOrder;
            set => SetProperty(ref _zOrder, value);
        }

        // ====================================================================
        // ENABLED STATE
        // ====================================================================

        private bool _isEnabled = true;

        /// <summary>
        /// Gets or sets whether this sub-routine is active and should be played.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        // ====================================================================
        // REEL LOADING METHODS
        // ====================================================================

        /// <summary>
        /// Loads the tile animation reel from the file path.
        /// If the reel has embedded pixel data (v2 format), uses that directly.
        /// Otherwise, pre-renders frames from the provided document.
        /// </summary>
        /// <param name="document">The document to use for rendering frames (only needed for v1 format without embedded pixels).</param>
        /// <returns>True if the reel was loaded successfully.</returns>
        public bool LoadReel(Document.CanvasDocument? document)
        {
            if (string.IsNullOrEmpty(_reelFilePath))
                return false;

            // Load the reel file
            var reel = TileAnimationReelIO.Load(_reelFilePath);
            if (reel == null || reel.FrameCount == 0)
                return false;

            LoadedReel = reel;

            // Check if reel has embedded pixel data (v2 format)
            if (reel.HasEmbeddedPixels)
            {
                // Use embedded pixels directly
                LoadFramesFromEmbeddedData();
            }
            else if (document != null)
            {
                // Fall back to rendering from document (v1 format)
                RenderFramesFromDocument(document);
            }
            else
            {
                // No embedded pixels and no document - can't load frames
                Core.Logging.LoggingService.Warning(
                    "Reel '{Name}' has no embedded pixels and no document provided for rendering", 
                    reel.Name);
                return false;
            }

            ReelLoadedChanged?.Invoke(true);
            return RenderedFrames.Count > 0;
        }

        /// <summary>
        /// Loads frames from the embedded pixel data in the reel (v2 format).
        /// </summary>
        private void LoadFramesFromEmbeddedData()
        {
            if (LoadedReel == null)
                return;

            RenderedFrames.Clear();

            FrameWidth = LoadedReel.FrameWidth;
            FrameHeight = LoadedReel.FrameHeight;

            for (int i = 0; i < LoadedReel.FrameCount; i++)
            {
                var frame = LoadedReel.Frames[i];
                if (frame.EmbeddedPixels != null)
                {
                    RenderedFrames[i] = frame.EmbeddedPixels;
                }
            }

            Core.Logging.LoggingService.Debug(
                "Loaded {FrameCount} frames from embedded pixel data ({Width}x{Height})",
                RenderedFrames.Count, FrameWidth, FrameHeight);
        }

        /// <summary>
        /// Pre-renders all frames from the tile animation reel using the document's tile data.
        /// Used for v1 format reels that don't have embedded pixel data.
        /// </summary>
        /// <param name="document">The document containing the tile pixel data.</param>
        public void RenderFramesFromDocument(Document.CanvasDocument document)
        {
            if (LoadedReel == null || document == null)
                return;

            RenderedFrames.Clear();

            int tileWidth = document.TileSize.Width;
            int tileHeight = document.TileSize.Height;

            FrameWidth = tileWidth;
            FrameHeight = tileHeight;

            // Composite the document to get the full pixel data
            var composite = new Imaging.PixelSurface(document.PixelWidth, document.PixelHeight);
            document.CompositeTo(composite);

            // Extract each frame's pixel data from the tile positions
            for (int i = 0; i < LoadedReel.FrameCount; i++)
            {
                var frame = LoadedReel.Frames[i];
                int srcX = frame.TileX * tileWidth;
                int srcY = frame.TileY * tileHeight;

                byte[] framePixels = new byte[tileWidth * tileHeight * 4];

                for (int y = 0; y < tileHeight; y++)
                {
                    for (int x = 0; x < tileWidth; x++)
                    {
                        int dstIdx = (y * tileWidth + x) * 4;
                        int sx = srcX + x;
                        int sy = srcY + y;

                        if (sx >= 0 && sx < composite.Width && sy >= 0 && sy < composite.Height)
                        {
                            int srcIdx = (sy * composite.Width + sx) * 4;
                            framePixels[dstIdx + 0] = composite.Pixels[srcIdx + 0];
                            framePixels[dstIdx + 1] = composite.Pixels[srcIdx + 1];
                            framePixels[dstIdx + 2] = composite.Pixels[srcIdx + 2];
                            framePixels[dstIdx + 3] = composite.Pixels[srcIdx + 3];
                        }
                        else
                        {
                            // Transparent if outside bounds
                            framePixels[dstIdx + 0] = 0;
                            framePixels[dstIdx + 1] = 0;
                            framePixels[dstIdx + 2] = 0;
                            framePixels[dstIdx + 3] = 0;
                        }
                    }
                }

                RenderedFrames[i] = framePixels;
            }
        }

        /// <summary>
        /// Unloads the reel and clears all rendered frames.
        /// </summary>
        public void UnloadReel()
        {
            LoadedReel = null;
            RenderedFrames.Clear();
            FrameWidth = 0;
            FrameHeight = 0;
            ReelLoadedChanged?.Invoke(false);
        }

        /// <summary>
        /// Gets the tile animation frame index for a given canvas animation frame.
        /// The animation loops automatically when the duration extends beyond the reel's frame count.
        /// </summary>
        /// <param name="canvasFrame">The canvas animation frame index.</param>
        /// <returns>The tile animation frame index to display, or -1 if none.</returns>
        public int GetTileFrameIndex(int canvasFrame)
        {
            if (LoadedReel == null || !IsFrameInRange(canvasFrame))
                return -1;

            int reelFrameCount = LoadedReel.FrameCount;
            if (reelFrameCount == 0)
                return -1;

            // Calculate relative frame within this sub-routine's duration
            int relativeFrame = canvasFrame - StartFrame;

            // The animation should loop based on the reel's frame count
            // So if you have a 4-frame walk cycle and extend to 8 frames, 
            // it plays frames 0,1,2,3,0,1,2,3
            int tileFrame = relativeFrame % reelFrameCount;

            // Handle ping-pong mode if enabled
            if (LoadedReel.PingPong && reelFrameCount > 1)
            {
                // In ping-pong mode, one full cycle is: 0,1,2,3,2,1 (for 4 frames)
                // That's (frameCount - 1) * 2 positions per cycle
                int cycleLength = (reelFrameCount - 1) * 2;
                int posInCycle = relativeFrame % cycleLength;
                
                if (posInCycle < reelFrameCount)
                {
                    // Forward direction: 0,1,2,3
                    tileFrame = posInCycle;
                }
                else
                {
                    // Reverse direction: 2,1 (skip first and last)
                    tileFrame = cycleLength - posInCycle;
                }
            }

            return Math.Clamp(tileFrame, 0, reelFrameCount - 1);
        }

        /// <summary>
        /// Gets the pixel data for the current frame at the given canvas frame index.
        /// </summary>
        /// <param name="canvasFrame">The canvas animation frame index.</param>
        /// <returns>The pixel data (BGRA), or null if not available.</returns>
        public byte[]? GetFramePixels(int canvasFrame)
        {
            int tileFrame = GetTileFrameIndex(canvasFrame);
            if (tileFrame < 0 || !RenderedFrames.TryGetValue(tileFrame, out var pixels))
                return null;

            return pixels;
        }

        // ====================================================================
        // OTHER METHODS
        // ====================================================================

        /// <summary>
        /// Clears all settings and keyframes.
        /// </summary>
        public void Clear()
        {
            UnloadReel();
            ReelFilePath = string.Empty;
            StartFrame = 0;
            DurationFrames = 1;
            PositionKeyframes.Clear();
            ScaleKeyframes.Clear();
            RotationKeyframes.Clear();
            PositionInterpolation = InterpolationMode.Linear;
            ScaleInterpolation = InterpolationMode.Linear;
            RotationInterpolation = InterpolationMode.Linear;
            IsEnabled = true;
        }

        /// <summary>
        /// Creates a deep copy of the sub-routine.
        /// </summary>
        public AnimationSubRoutine Clone()
        {
            var clone = new AnimationSubRoutine
            {
                ReelFilePath = ReelFilePath,
                StartFrame = StartFrame,
                DurationFrames = DurationFrames,
                PositionInterpolation = PositionInterpolation,
                ScaleInterpolation = ScaleInterpolation,
                RotationInterpolation = RotationInterpolation,
                ZOrder = ZOrder,
                IsEnabled = IsEnabled
            };

            // Deep copy keyframes
            foreach (var kvp in PositionKeyframes)
                clone.PositionKeyframes[kvp.Key] = kvp.Value;

            foreach (var kvp in ScaleKeyframes)
                clone.ScaleKeyframes[kvp.Key] = kvp.Value;

            foreach (var kvp in RotationKeyframes)
                clone.RotationKeyframes[kvp.Key] = kvp.Value;

            return clone;
        }

        /// <summary>
        /// Checks if a frame index falls within the active range of this sub-routine.
        /// </summary>
        /// <param name="frameIndex">The frame index in the canvas animation timeline.</param>
        /// <returns>True if the frame is within the sub-routine's active range and enabled.</returns>
        public bool IsFrameInRange(int frameIndex)
        {
            if (!IsEnabled) return false;
            return frameIndex >= StartFrame && frameIndex < EndFrame;
        }

        /// <summary>
        /// Gets the normalized progress (0.0 to 1.0) for a given frame within this sub-routine.
        /// </summary>
        /// <param name="frameIndex">The frame index in the canvas animation timeline.</param>
        /// <returns>Normalized progress, or 0.0 if frame is outside range or duration is invalid.</returns>
        public float GetNormalizedProgress(int frameIndex)
        {
            if (!IsFrameInRange(frameIndex)) return 0f;
            
            // Guard against division by zero (DurationFrames should always be >= 1, but be defensive)
            if (DurationFrames <= 0) return 0f;

            int relativeFrame = frameIndex - StartFrame;
            return relativeFrame / (float)DurationFrames;
        }

        /// <summary>
        /// Interpolates a position value based on the normalized progress.
        /// Returns pixel-snapped values (rounded to nearest integer) to avoid sub-pixel rendering.
        /// </summary>
        public (double X, double Y) InterpolatePosition(float normalizedProgress)
        {
            if (PositionKeyframes.Count == 0)
                return (0, 0);

            if (PositionKeyframes.Count == 1)
            {
                var pos = PositionKeyframes.Values.First();
                return (Math.Round(pos.X), Math.Round(pos.Y));
            }

            var interpolated = InterpolateKeyframe(PositionKeyframes, normalizedProgress, PositionInterpolation,
                (a, b, t) => (
                    a.X + (b.X - a.X) * t,
                    a.Y + (b.Y - a.Y) * t
                ));

            // Snap to whole pixels to avoid sub-pixel rendering artifacts
            return (Math.Round(interpolated.X), Math.Round(interpolated.Y));
        }

        /// <summary>
        /// Interpolates a scale value based on the normalized progress.
        /// </summary>
        public float InterpolateScale(float normalizedProgress)
        {
            if (ScaleKeyframes.Count == 0)
                return 1.0f;

            if (ScaleKeyframes.Count == 1)
                return ScaleKeyframes.Values.First();

            return InterpolateKeyframe(ScaleKeyframes, normalizedProgress, ScaleInterpolation,
                (a, b, t) => a + (b - a) * t);
        }

        /// <summary>
        /// Interpolates a rotation value based on the normalized progress.
        /// </summary>
        public float InterpolateRotation(float normalizedProgress)
        {
            if (RotationKeyframes.Count == 0)
                return 0.0f;

            if (RotationKeyframes.Count == 1)
                return RotationKeyframes.Values.First();

            return InterpolateKeyframe(RotationKeyframes, normalizedProgress, RotationInterpolation,
                (a, b, t) => a + (b - a) * t);
        }

        /// <summary>
        /// Generic keyframe interpolation helper.
        /// </summary>
        private TValue InterpolateKeyframe<TValue>(
            SortedDictionary<float, TValue> keyframes,
            float normalizedProgress,
            InterpolationMode mode,
            Func<TValue, TValue, float, TValue> lerp)
        {
            if (keyframes.Count == 0)
                throw new InvalidOperationException("Cannot interpolate with no keyframes.");

            if (normalizedProgress <= 0f)
                return keyframes.Values.First();

            if (normalizedProgress >= 1f)
                return keyframes.Values.Last();

            // Find surrounding keyframes
            var keys = keyframes.Keys.ToList();
            int index = keys.BinarySearch(normalizedProgress);

            if (index >= 0)
                return keyframes[keys[index]]; // Exact match

            // index is negative: ~index is the insertion point
            index = ~index;
            if (index == 0)
                return keyframes[keys[0]];
            if (index >= keys.Count)
                return keyframes[keys[keys.Count - 1]];

            float beforeKey = keys[index - 1];
            float afterKey = keys[index];
            TValue beforeValue = keyframes[beforeKey];
            TValue afterValue = keyframes[afterKey];

            // Compute local time (0 to 1) within this keyframe pair
            float localTime = (normalizedProgress - beforeKey) / (afterKey - beforeKey);
            localTime = Math.Clamp(localTime, 0f, 1f);

            // Apply interpolation curve
            localTime = ApplyInterpolationCurve(localTime, mode);

            return lerp(beforeValue, afterValue, localTime);
        }

        /// <summary>
        /// Applies interpolation curve to the local time value.
        /// </summary>
        private static float ApplyInterpolationCurve(float t, InterpolationMode mode)
        {
            return mode switch
            {
                InterpolationMode.Linear => t,
                InterpolationMode.EaseInQuad => t * t,
                InterpolationMode.EaseOutQuad => 1 - (1 - t) * (1 - t),
                InterpolationMode.EaseInOutQuad => t < 0.5f
                    ? 2 * t * t
                    : 1 - (float)Math.Pow(-2 * t + 2, 2) / 2,
                InterpolationMode.EaseInCubic => t * t * t,
                InterpolationMode.EaseOutCubic => 1 - (float)Math.Pow(1 - t, 3),
                InterpolationMode.EaseInOutCubic => t < 0.5f
                    ? 4 * t * t * t
                    : 1 - (float)Math.Pow(-2 * t + 2, 3) / 2,
                _ => t
            };
        }

        // ====================================================================
        // INOTIFYPROPERTYCHANGED
        // ====================================================================

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Interpolation modes for keyframe animation.
    /// </summary>
    public enum InterpolationMode
    {
        /// <summary>Constant linear interpolation.</summary>
        Linear,

        /// <summary>Quadratic ease-in (slow start).</summary>
        EaseInQuad,

        /// <summary>Quadratic ease-out (slow end).</summary>
        EaseOutQuad,

        /// <summary>Quadratic ease-in-out (slow at both ends).</summary>
        EaseInOutQuad,

        /// <summary>Cubic ease-in (slow start).</summary>
        EaseInCubic,

        /// <summary>Cubic ease-out (slow end).</summary>
        EaseOutCubic,

        /// <summary>Cubic ease-in-out (slow at both ends).</summary>
        EaseInOutCubic
    }
}
