using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PixlPunkt.Uno.Core.Animation
{
    /// <summary>
    /// Manages keyframes for the stage (camera) animation track.
    /// Unlike layer tracks which hold values between keyframes, stage tracks interpolate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// StageAnimationTrack provides smooth camera movements by interpolating between keyframes.
    /// This allows for panning, zooming, and rotating effects during animation playback.
    /// </para>
    /// <para>
    /// The track supports multiple easing functions per property (position, scale, rotation)
    /// for fine-grained control over camera motion.
    /// </para>
    /// </remarks>
    public sealed class StageAnimationTrack : INotifyPropertyChanged
    {
        // ====================================================================
        // IDENTITY
        // ====================================================================

        /// <summary>
        /// Gets or sets the unique identifier for this track.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = "Stage";

        /// <summary>
        /// Gets or sets the display name of this track.
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        // ====================================================================
        // KEYFRAMES
        // ====================================================================

        /// <summary>
        /// Gets the collection of keyframes for this track, sorted by frame index.
        /// </summary>
        public ObservableCollection<StageKeyframeData> Keyframes { get; } = [];

        /// <summary>
        /// Raised when the keyframes collection changes.
        /// </summary>
        public event Action? KeyframesChanged;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public StageAnimationTrack()
        {
            Keyframes.CollectionChanged += (_, __) => KeyframesChanged?.Invoke();
        }

        // ====================================================================
        // KEYFRAME MANAGEMENT
        // ====================================================================

        /// <summary>
        /// Gets the keyframe at a specific frame index, or null if none exists.
        /// </summary>
        public StageKeyframeData? GetKeyframeAt(int frameIndex)
        {
            return Keyframes.FirstOrDefault(k => k.FrameIndex == frameIndex);
        }

        /// <summary>
        /// Checks if there is a keyframe at the specified frame.
        /// </summary>
        public bool HasKeyframeAt(int frameIndex)
        {
            return Keyframes.Any(k => k.FrameIndex == frameIndex);
        }

        /// <summary>
        /// Gets the interpolated transform state at a specific frame.
        /// Unlike layer tracks, this interpolates between keyframes for smooth motion.
        /// </summary>
        /// <param name="frameIndex">The frame to query.</param>
        /// <returns>The interpolated keyframe data, or null if no keyframes exist.</returns>
        public StageKeyframeData? GetInterpolatedStateAt(int frameIndex)
        {
            if (Keyframes.Count == 0)
                return null;

            // Find surrounding keyframes
            StageKeyframeData? before = null;
            StageKeyframeData? after = null;

            foreach (var kf in Keyframes.OrderBy(k => k.FrameIndex))
            {
                if (kf.FrameIndex <= frameIndex)
                {
                    before = kf;
                }
                if (kf.FrameIndex >= frameIndex && after == null)
                {
                    after = kf;
                }
            }

            // If exact keyframe exists, return it
            if (before != null && before.FrameIndex == frameIndex)
                return before.Clone();

            // If only before exists (past last keyframe), hold that value
            if (after == null && before != null)
                return before.Clone();

            // If only after exists (before first keyframe), hold that value
            if (before == null && after != null)
                return after.Clone();

            // Interpolate between before and after
            if (before != null && after != null)
            {
                int range = after.FrameIndex - before.FrameIndex;
                if (range <= 0) return before.Clone();

                float t = (float)(frameIndex - before.FrameIndex) / range;
                return StageKeyframeData.Lerp(before, after, t);
            }

            return null;
        }

        /// <summary>
        /// Gets all frame indices that have keyframes.
        /// </summary>
        public IEnumerable<int> GetKeyframeIndices()
        {
            return Keyframes.Select(k => k.FrameIndex).OrderBy(i => i);
        }

        /// <summary>
        /// Adds or updates a keyframe at the specified frame.
        /// </summary>
        /// <param name="keyframe">The keyframe data to set.</param>
        public void SetKeyframe(StageKeyframeData keyframe)
        {
            // Remove existing keyframe at this frame if any
            var existing = GetKeyframeAt(keyframe.FrameIndex);
            if (existing != null)
            {
                Keyframes.Remove(existing);
            }

            // Insert in sorted order
            int insertIndex = 0;
            for (int i = 0; i < Keyframes.Count; i++)
            {
                if (Keyframes[i].FrameIndex > keyframe.FrameIndex)
                {
                    insertIndex = i;
                    break;
                }
                insertIndex = i + 1;
            }

            Keyframes.Insert(insertIndex, keyframe);
        }

        /// <summary>
        /// Removes the keyframe at the specified frame.
        /// </summary>
        /// <returns>True if a keyframe was removed.</returns>
        public bool RemoveKeyframeAt(int frameIndex)
        {
            var kf = GetKeyframeAt(frameIndex);
            if (kf != null)
            {
                Keyframes.Remove(kf);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all keyframes from this track.
        /// </summary>
        public void ClearKeyframes()
        {
            Keyframes.Clear();
        }

        /// <summary>
        /// Gets the next keyframe after the specified frame, or null if none.
        /// </summary>
        public StageKeyframeData? GetNextKeyframe(int afterFrame)
        {
            return Keyframes
                .Where(k => k.FrameIndex > afterFrame)
                .OrderBy(k => k.FrameIndex)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the previous keyframe before the specified frame, or null if none.
        /// </summary>
        public StageKeyframeData? GetPreviousKeyframe(int beforeFrame)
        {
            return Keyframes
                .Where(k => k.FrameIndex < beforeFrame)
                .OrderByDescending(k => k.FrameIndex)
                .FirstOrDefault();
        }

        /// <summary>
        /// Creates a deep copy of this track.
        /// </summary>
        public StageAnimationTrack Clone()
        {
            var clone = new StageAnimationTrack
            {
                Id = Guid.NewGuid(),
                Name = Name
            };

            foreach (var kf in Keyframes)
            {
                clone.Keyframes.Add(kf.Clone());
            }

            return clone;
        }

        /// <summary>
        /// Creates an initial keyframe capturing the current stage settings.
        /// </summary>
        /// <param name="settings">The stage settings to capture.</param>
        /// <param name="frameIndex">The frame index for the keyframe.</param>
        public void CaptureKeyframe(StageSettings settings, int frameIndex)
        {
            // Calculate scale from the ratio of output size to stage size
            // Scale > 1 means we're zooming in (source area smaller than output)
            // Scale < 1 means we're zooming out (source area larger than output)
            float scaleX = settings.StageWidth > 0 ? (float)settings.OutputWidth / settings.StageWidth : 1.0f;
            float scaleY = settings.StageHeight > 0 ? (float)settings.OutputHeight / settings.StageHeight : 1.0f;

            var keyframe = new StageKeyframeData(
                frameIndex,
                settings.StageX + settings.StageWidth / 2f,
                settings.StageY + settings.StageHeight / 2f,
                scaleX,
                scaleY,
                0f    // Default rotation
            );

            SetKeyframe(keyframe);
        }

        // ====================================================================
        // INotifyPropertyChanged
        // ====================================================================

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
