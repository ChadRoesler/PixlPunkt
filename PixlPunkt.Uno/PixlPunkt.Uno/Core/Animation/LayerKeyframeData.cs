using System;
using System.Collections.Generic;
using System.Linq;
using PixlPunkt.Uno.Core.Enums;

namespace PixlPunkt.Uno.Core.Animation
{
    /// <summary>
    /// Stores the state of a layer at a specific keyframe.
    /// This captures all properties that can be animated: visibility, opacity, blend mode, pixel data, effects, and mask state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LayerKeyframeData represents a snapshot of a layer's animatable properties at a specific frame.
    /// Between keyframes, the values are held (no interpolation) - Aseprite-style behavior.
    /// </para>
    /// <para>
    /// Pixel data is stored as a reference to allow efficient memory usage when frames share content.
    /// The actual pixel data is managed separately to allow deduplication.
    /// </para>
    /// <para>
    /// Effect states are stored per-effect, capturing IsEnabled and all animatable properties.
    /// Mask state (enabled/inverted/pixel data) is also captured for layers with masks.
    /// </para>
    /// </remarks>
    public sealed class LayerKeyframeData
    {
        /// <summary>
        /// Gets or sets the frame index this keyframe is at.
        /// </summary>
        public int FrameIndex { get; set; }

        /// <summary>
        /// Gets or sets whether the layer is visible at this keyframe.
        /// </summary>
        public bool Visible { get; set; } = true;

        /// <summary>
        /// Gets or sets the layer opacity at this keyframe (0-255).
        /// </summary>
        public byte Opacity { get; set; } = 255;

        /// <summary>
        /// Gets or sets the blend mode at this keyframe.
        /// </summary>
        public BlendMode BlendMode { get; set; } = BlendMode.Normal;

        /// <summary>
        /// Gets or sets the unique identifier for the pixel data snapshot.
        /// This references a stored pixel buffer in the animation's pixel data storage.
        /// -1 means no pixel data change (inherit from previous keyframe).
        /// </summary>
        public int PixelDataId { get; set; } = -1;

        /// <summary>
        /// Gets or sets whether this keyframe has pixel data changes.
        /// If false, the layer uses pixel data from the previous keyframe.
        /// </summary>
        public bool HasPixelData => PixelDataId >= 0;

        //////////////////////////////////////////////////////////////////
        // MASK STATE
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets or sets whether the layer mask is enabled at this keyframe.
        /// Only applies to layers with masks attached.
        /// </summary>
        /// <remarks>
        /// When false, the mask has no effect on the layer (fully visible).
        /// Animating this property allows for mask reveal/hide effects.
        /// </remarks>
        public bool MaskEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the layer mask is inverted at this keyframe.
        /// Only applies to layers with masks attached.
        /// </summary>
        /// <remarks>
        /// When true, white areas hide and black areas reveal (opposite of normal).
        /// Animating this property allows for mask inversion effects.
        /// </remarks>
        public bool MaskInverted { get; set; } = false;

        /// <summary>
        /// Gets or sets the unique identifier for the mask pixel data snapshot.
        /// This references a stored pixel buffer in the animation's pixel data storage.
        /// -1 means no mask pixel data change (inherit from previous keyframe or no mask).
        /// </summary>
        /// <remarks>
        /// Mask pixel data is stored separately from layer pixel data, allowing independent
        /// animation of the mask. This enables effects like animated reveals, wipes, and
        /// dynamic masking without affecting the underlying layer content.
        /// </remarks>
        public int MaskPixelDataId { get; set; } = -1;

        /// <summary>
        /// Gets whether this keyframe has mask pixel data changes.
        /// </summary>
        public bool HasMaskPixelData => MaskPixelDataId >= 0;

        /// <summary>
        /// Gets or sets the effect states at this keyframe.
        /// Each entry captures an effect's IsEnabled and property values.
        /// </summary>
        public List<EffectKeyframeData> EffectStates { get; set; } = new List<EffectKeyframeData>();

        /// <summary>
        /// Creates an empty keyframe data at frame 0.
        /// </summary>
        public LayerKeyframeData()
        {
        }

        /// <summary>
        /// Creates a keyframe data with specified values.
        /// </summary>
        public LayerKeyframeData(int frameIndex, bool visible = true, byte opacity = 255,
            BlendMode blendMode = BlendMode.Normal, int pixelDataId = -1,
            bool maskEnabled = true, bool maskInverted = false, int maskPixelDataId = -1)
        {
            FrameIndex = frameIndex;
            Visible = visible;
            Opacity = opacity;
            BlendMode = blendMode;
            PixelDataId = pixelDataId;
            MaskEnabled = maskEnabled;
            MaskInverted = maskInverted;
            MaskPixelDataId = maskPixelDataId;
        }

        /// <summary>
        /// Creates a deep copy of this keyframe data.
        /// </summary>
        public LayerKeyframeData Clone()
        {
            var clone = new LayerKeyframeData(FrameIndex, Visible, Opacity, BlendMode, PixelDataId, MaskEnabled, MaskInverted, MaskPixelDataId);

            foreach (var effectState in EffectStates)
            {
                clone.EffectStates.Add(effectState.Clone());
            }

            return clone;
        }

        /// <summary>
        /// Checks if this keyframe has the same property values as another (ignoring frame index).
        /// </summary>
        public bool HasSameValues(LayerKeyframeData other)
        {
            if (other == null) return false;
            if (Visible != other.Visible ||
                Opacity != other.Opacity ||
                BlendMode != other.BlendMode ||
                PixelDataId != other.PixelDataId ||
                MaskEnabled != other.MaskEnabled ||
                MaskInverted != other.MaskInverted ||
                MaskPixelDataId != other.MaskPixelDataId)
                return false;

            // Compare effect states
            if (EffectStates.Count != other.EffectStates.Count)
                return false;

            for (int i = 0; i < EffectStates.Count; i++)
            {
                if (!EffectStates[i].HasSameValues(other.EffectStates[i]))
                    return false;
            }

            return true;
        }
    }
}
