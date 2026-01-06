using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PixlPunkt.Uno.Core.Animation
{
    /// <summary>
    /// Represents an animation track for a single layer or folder.
    /// Contains keyframes that define the layer's state at specific frames.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each layer/folder in the document has a corresponding CanvasAnimationTrack that stores
    /// its keyframes. Keyframes are sparse - only frames with explicit changes are stored.
    /// Between keyframes, values are held (no interpolation).
    /// </para>
    /// <para>
    /// The track is identified by a LayerId that corresponds to a layer's GUID or index
    /// in the document structure.
    /// </para>
    /// </remarks>
    public sealed class CanvasAnimationTrack : INotifyPropertyChanged
    {
        // ====================================================================
        // IDENTITY
        // ====================================================================

        /// <summary>
        /// Gets or sets the unique identifier for this track.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the layer identifier this track is associated with.
        /// This corresponds to a layer's unique ID in the document.
        /// </summary>
        public Guid LayerId { get; set; }

        private string _layerName = "Layer";

        /// <summary>
        /// Gets or sets the display name of the associated layer.
        /// Cached for UI display without requiring document lookup.
        /// </summary>
        public string LayerName
        {
            get => _layerName;
            set
            {
                if (_layerName != value)
                {
                    _layerName = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isFolder;

        /// <summary>
        /// Gets or sets whether this track is for a folder (vs. a raster layer).
        /// </summary>
        public bool IsFolder
        {
            get => _isFolder;
            set
            {
                if (_isFolder != value)
                {
                    _isFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _depth;

        /// <summary>
        /// Gets or sets the nesting depth of this track (for indentation in UI).
        /// </summary>
        public int Depth
        {
            get => _depth;
            set
            {
                if (_depth != value)
                {
                    _depth = value;
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
        public ObservableCollection<LayerKeyframeData> Keyframes { get; } = [];

        /// <summary>
        /// Raised when the keyframes collection changes.
        /// </summary>
        public event Action? KeyframesChanged;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        /// <summary>
        /// Creates a new animation track.
        /// </summary>
        public CanvasAnimationTrack()
        {
            Keyframes.CollectionChanged += (_, __) => KeyframesChanged?.Invoke();
        }

        /// <summary>
        /// Creates a new animation track for a specific layer.
        /// </summary>
        public CanvasAnimationTrack(Guid layerId, string layerName, bool isFolder = false, int depth = 0)
            : this()
        {
            LayerId = layerId;
            LayerName = layerName;
            IsFolder = isFolder;
            Depth = depth;
        }

        // ====================================================================
        // KEYFRAME MANAGEMENT
        // ====================================================================

        /// <summary>
        /// Gets the keyframe at a specific frame index, or null if none exists.
        /// </summary>
        public LayerKeyframeData? GetKeyframeAt(int frameIndex)
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
        /// Gets the effective state at a frame (from the most recent keyframe at or before this frame).
        /// </summary>
        /// <param name="frameIndex">The frame to query.</param>
        /// <returns>The keyframe data, or null if no keyframes exist before this frame.</returns>
        public LayerKeyframeData? GetEffectiveStateAt(int frameIndex)
        {
            // Find the most recent keyframe at or before this frame
            LayerKeyframeData? result = null;
            foreach (var kf in Keyframes)
            {
                if (kf.FrameIndex <= frameIndex)
                {
                    if (result == null || kf.FrameIndex > result.FrameIndex)
                    {
                        result = kf;
                    }
                }
            }
            return result;
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
        public void SetKeyframe(LayerKeyframeData keyframe)
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
        public LayerKeyframeData? GetNextKeyframe(int afterFrame)
        {
            return Keyframes
                .Where(k => k.FrameIndex > afterFrame)
                .OrderBy(k => k.FrameIndex)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the previous keyframe before the specified frame, or null if none.
        /// </summary>
        public LayerKeyframeData? GetPreviousKeyframe(int beforeFrame)
        {
            return Keyframes
                .Where(k => k.FrameIndex < beforeFrame)
                .OrderByDescending(k => k.FrameIndex)
                .FirstOrDefault();
        }

        /// <summary>
        /// Creates a deep copy of this track.
        /// </summary>
        public CanvasAnimationTrack Clone()
        {
            var clone = new CanvasAnimationTrack
            {
                Id = Guid.NewGuid(),
                LayerId = LayerId,
                LayerName = LayerName,
                IsFolder = IsFolder,
                Depth = Depth
            };

            foreach (var kf in Keyframes)
            {
                clone.Keyframes.Add(kf.Clone());
            }

            return clone;
        }

        // ====================================================================
        // KEYFRAME MOVEMENT
        // ====================================================================

        /// <summary>
        /// Moves a keyframe from one frame index to another.
        /// </summary>
        /// <param name="fromFrameIndex">Current frame index of the keyframe.</param>
        /// <param name="toFrameIndex">Target frame index.</param>
        /// <returns>True if the keyframe was moved successfully.</returns>
        public bool MoveKeyframe(int fromFrameIndex, int toFrameIndex)
        {
            if (fromFrameIndex == toFrameIndex) return true;

            var keyframe = GetKeyframeAt(fromFrameIndex);
            if (keyframe == null) return false;

            // Remove existing keyframe at target if any
            RemoveKeyframeAt(toFrameIndex);

            // Remove the keyframe from its current position
            Keyframes.Remove(keyframe);

            // Update frame index and re-insert
            keyframe.FrameIndex = toFrameIndex;
            SetKeyframe(keyframe);

            return true;
        }

        /// <summary>
        /// Moves all keyframes by a specified offset.
        /// Keyframes that would move to negative indices are clamped to 0.
        /// </summary>
        /// <param name="offset">The number of frames to shift (positive = right, negative = left).</param>
        public void MoveAllKeyframes(int offset)
        {
            if (offset == 0 || Keyframes.Count == 0) return;

            // Collect all keyframes and their new positions
            var moves = Keyframes.Select(kf => (kf, newIndex: Math.Max(0, kf.FrameIndex + offset))).ToList();

            // Clear and re-add with new positions
            Keyframes.Clear();
            foreach (var (kf, newIndex) in moves)
            {
                kf.FrameIndex = newIndex;
                SetKeyframe(kf);
            }
        }

        /// <summary>
        /// Moves a range of keyframes by a specified offset.
        /// </summary>
        /// <param name="startFrame">Start of the frame range (inclusive).</param>
        /// <param name="endFrame">End of the frame range (inclusive).</param>
        /// <param name="offset">The number of frames to shift.</param>
        public void MoveKeyframeRange(int startFrame, int endFrame, int offset)
        {
            if (offset == 0) return;

            var keyframesToMove = Keyframes
                .Where(kf => kf.FrameIndex >= startFrame && kf.FrameIndex <= endFrame)
                .ToList();

            foreach (var kf in keyframesToMove)
            {
                Keyframes.Remove(kf);
            }

            foreach (var kf in keyframesToMove)
            {
                kf.FrameIndex = Math.Max(0, kf.FrameIndex + offset);
                SetKeyframe(kf);
            }
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
