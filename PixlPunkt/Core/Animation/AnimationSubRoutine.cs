using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

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
        // METHODS
        // ====================================================================

        /// <summary>
        /// Clears all settings and keyframes.
        /// </summary>
        public void Clear()
        {
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
        /// <returns>Normalized progress, or 0.0 if frame is outside range.</returns>
        public float GetNormalizedProgress(int frameIndex)
        {
            if (!IsFrameInRange(frameIndex)) return 0f;

            int relativeFrame = frameIndex - StartFrame;
            return DurationFrames > 0 ? relativeFrame / (float)DurationFrames : 0f;
        }

        /// <summary>
        /// Interpolates a position value based on the normalized progress.
        /// </summary>
        public (double X, double Y) InterpolatePosition(float normalizedProgress)
        {
            if (PositionKeyframes.Count == 0)
                return (0, 0);

            if (PositionKeyframes.Count == 1)
                return PositionKeyframes.Values.First();

            return InterpolateKeyframe(PositionKeyframes, normalizedProgress, PositionInterpolation,
                (a, b, t) => (
                    a.X + (b.X - a.X) * t,
                    a.Y + (b.Y - a.Y) * t
                ));
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
