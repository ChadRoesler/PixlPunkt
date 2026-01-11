using System;
using System.Collections.Generic;
using System.Linq;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Represents copied keyframe data for clipboard operations.
    /// Stores keyframe data with relative frame offsets for paste operations.
    /// </summary>
    public sealed class KeyframeClipboard
    {
        // ====================================================================
        // STATIC INSTANCE (APPLICATION-WIDE CLIPBOARD)
        // ====================================================================

        /// <summary>
        /// Gets the application-wide keyframe clipboard.
        /// </summary>
        public static KeyframeClipboard Instance { get; } = new();

        // ====================================================================
        // PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets whether the clipboard contains any keyframes.
        /// </summary>
        public bool HasContent => _entries.Count > 0;

        /// <summary>
        /// Gets the number of keyframes in the clipboard.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Gets whether the clipboard contains keyframes from a single layer.
        /// </summary>
        public bool IsSingleLayer => _layerIds.Count == 1;

        /// <summary>
        /// Gets whether the clipboard contains keyframes from multiple layers.
        /// </summary>
        public bool IsMultiLayer => _layerIds.Count > 1;

        /// <summary>
        /// Gets the frame span of the copied keyframes.
        /// </summary>
        public int FrameSpan => HasContent ? _entries.Max(e => e.RelativeFrame) + 1 : 0;

        // ====================================================================
        // FIELDS
        // ====================================================================

        private readonly List<KeyframeClipboardEntry> _entries = [];
        private readonly HashSet<Guid> _layerIds = [];
        private int _baseFrame;

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when clipboard content changes.
        /// </summary>
        public event Action? ContentChanged;

        // ====================================================================
        // COPY OPERATIONS
        // ====================================================================

        /// <summary>
        /// Clears the clipboard.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _layerIds.Clear();
            _baseFrame = 0;
            ContentChanged?.Invoke();
        }

        /// <summary>
        /// Copies a single keyframe to the clipboard.
        /// </summary>
        /// <param name="layerId">The layer ID the keyframe belongs to.</param>
        /// <param name="keyframe">The keyframe data to copy.</param>
        public void CopyKeyframe(Guid layerId, LayerKeyframeData keyframe)
        {
            Clear();
            _baseFrame = keyframe.FrameIndex;
            _layerIds.Add(layerId);
            _entries.Add(new KeyframeClipboardEntry
            {
                LayerId = layerId,
                RelativeFrame = 0,
                KeyframeData = keyframe.Clone()
            });
            ContentChanged?.Invoke();
        }

        /// <summary>
        /// Copies multiple keyframes from a single layer to the clipboard.
        /// </summary>
        /// <param name="layerId">The layer ID the keyframes belong to.</param>
        /// <param name="keyframes">The keyframes to copy.</param>
        public void CopyKeyframes(Guid layerId, IEnumerable<LayerKeyframeData> keyframes)
        {
            var keyframeList = keyframes.OrderBy(k => k.FrameIndex).ToList();
            if (keyframeList.Count == 0) return;

            Clear();
            _baseFrame = keyframeList[0].FrameIndex;
            _layerIds.Add(layerId);

            foreach (var kf in keyframeList)
            {
                _entries.Add(new KeyframeClipboardEntry
                {
                    LayerId = layerId,
                    RelativeFrame = kf.FrameIndex - _baseFrame,
                    KeyframeData = kf.Clone()
                });
            }
            ContentChanged?.Invoke();
        }

        /// <summary>
        /// Copies keyframes from multiple layers to the clipboard.
        /// </summary>
        /// <param name="layerKeyframes">Dictionary of layer IDs to their keyframes.</param>
        public void CopyKeyframes(Dictionary<Guid, List<LayerKeyframeData>> layerKeyframes)
        {
            Clear();

            // Find the minimum frame index across all keyframes
            int minFrame = int.MaxValue;
            foreach (var kvp in layerKeyframes)
            {
                foreach (var kf in kvp.Value)
                {
                    minFrame = Math.Min(minFrame, kf.FrameIndex);
                }
            }

            if (minFrame == int.MaxValue) return;

            _baseFrame = minFrame;

            foreach (var kvp in layerKeyframes)
            {
                _layerIds.Add(kvp.Key);
                foreach (var kf in kvp.Value.OrderBy(k => k.FrameIndex))
                {
                    _entries.Add(new KeyframeClipboardEntry
                    {
                        LayerId = kvp.Key,
                        RelativeFrame = kf.FrameIndex - _baseFrame,
                        KeyframeData = kf.Clone()
                    });
                }
            }
            ContentChanged?.Invoke();
        }

        // ====================================================================
        // PASTE OPERATIONS
        // ====================================================================

        /// <summary>
        /// Gets the keyframes to paste at a target frame.
        /// Returns cloned keyframes with adjusted frame indices.
        /// </summary>
        /// <param name="targetFrame">The target frame to paste at.</param>
        /// <returns>Dictionary of layer IDs to keyframes.</returns>
        public Dictionary<Guid, List<LayerKeyframeData>> GetKeyframesToPaste(int targetFrame)
        {
            var result = new Dictionary<Guid, List<LayerKeyframeData>>();

            foreach (var entry in _entries)
            {
                if (!result.ContainsKey(entry.LayerId))
                {
                    result[entry.LayerId] = [];
                }

                var clonedKf = entry.KeyframeData.Clone();
                clonedKf.FrameIndex = targetFrame + entry.RelativeFrame;
                result[entry.LayerId].Add(clonedKf);
            }

            return result;
        }

        /// <summary>
        /// Gets keyframes to paste for a single layer at a target frame.
        /// If the clipboard contains keyframes from a different layer, they are remapped.
        /// </summary>
        /// <param name="targetLayerId">The target layer ID.</param>
        /// <param name="targetFrame">The target frame to paste at.</param>
        /// <returns>List of keyframes for the target layer.</returns>
        public List<LayerKeyframeData> GetKeyframesToPasteForLayer(Guid targetLayerId, int targetFrame)
        {
            var result = new List<LayerKeyframeData>();

            foreach (var entry in _entries)
            {
                var clonedKf = entry.KeyframeData.Clone();
                clonedKf.FrameIndex = targetFrame + entry.RelativeFrame;
                result.Add(clonedKf);
            }

            return result;
        }

        /// <summary>
        /// Gets the entries in the clipboard.
        /// </summary>
        public IReadOnlyList<KeyframeClipboardEntry> GetEntries() => _entries.AsReadOnly();
    }

    /// <summary>
    /// Represents a single keyframe entry in the clipboard.
    /// </summary>
    public sealed class KeyframeClipboardEntry
    {
        /// <summary>
        /// The layer ID this keyframe originally came from.
        /// </summary>
        public Guid LayerId { get; init; }

        /// <summary>
        /// The relative frame offset from the base frame.
        /// 0 = first keyframe in the copied set.
        /// </summary>
        public int RelativeFrame { get; init; }

        /// <summary>
        /// The cloned keyframe data.
        /// </summary>
        public required LayerKeyframeData KeyframeData { get; init; }
    }
}
