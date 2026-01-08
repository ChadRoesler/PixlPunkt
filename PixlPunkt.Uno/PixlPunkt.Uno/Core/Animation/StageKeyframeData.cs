using System;

namespace PixlPunkt.Uno.Core.Animation
{
    /// <summary>
    /// Stores the state of the stage (camera) at a specific keyframe.
    /// Contains position, scale, and rotation that can be interpolated between keyframes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// StageKeyframeData represents a snapshot of the camera's transform at a specific frame.
    /// Unlike layer keyframes which hold values, stage keyframes support interpolation
    /// for smooth camera movements.
    /// </para>
    /// <para>
    /// Position is relative to the canvas origin (0,0).
    /// Scale is a multiplier (1.0 = 100%, 2.0 = 200% zoom in).
    /// Rotation is in degrees, positive = clockwise.
    /// </para>
    /// </remarks>
    public sealed class StageKeyframeData
    {
        // ====================================================================
        // FRAME INDEX
        // ====================================================================

        /// <summary>
        /// Gets or sets the frame index this keyframe is at.
        /// </summary>
        public int FrameIndex { get; set; }

        // ====================================================================
        // POSITION (PAN)
        // ====================================================================

        /// <summary>
        /// Gets or sets the X position of the stage center within the canvas.
        /// </summary>
        public float PositionX { get; set; }

        /// <summary>
        /// Gets or sets the Y position of the stage center within the canvas.
        /// </summary>
        public float PositionY { get; set; }

        // ====================================================================
        // SCALE (ZOOM)
        // ====================================================================

        /// <summary>
        /// Gets or sets the horizontal scale factor (1.0 = 100%).
        /// Values greater than 1.0 zoom in, less than 1.0 zoom out.
        /// </summary>
        public float ScaleX { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets the vertical scale factor (1.0 = 100%).
        /// Values greater than 1.0 zoom in, less than 1.0 zoom out.
        /// </summary>
        public float ScaleY { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets whether scale is uniform (ScaleX = ScaleY).
        /// </summary>
        public bool UniformScale { get; set; } = true;

        // ====================================================================
        // ROTATION
        // ====================================================================

        /// <summary>
        /// Gets or sets the rotation angle in degrees.
        /// Positive values rotate clockwise.
        /// </summary>
        public float Rotation { get; set; } = 0f;

        // ====================================================================
        // INTERPOLATION SETTINGS
        // ====================================================================

        /// <summary>
        /// Gets or sets the easing function for position interpolation to this keyframe.
        /// </summary>
        public EasingType PositionEasing { get; set; } = EasingType.Linear;

        /// <summary>
        /// Gets or sets the easing function for scale interpolation to this keyframe.
        /// </summary>
        public EasingType ScaleEasing { get; set; } = EasingType.Linear;

        /// <summary>
        /// Gets or sets the easing function for rotation interpolation to this keyframe.
        /// </summary>
        public EasingType RotationEasing { get; set; } = EasingType.Linear;

        // ====================================================================
        // CONSTRUCTORS
        // ====================================================================

        /// <summary>
        /// Creates an empty keyframe at frame 0.
        /// </summary>
        public StageKeyframeData()
        {
        }

        /// <summary>
        /// Creates a keyframe with specified values.
        /// </summary>
        public StageKeyframeData(int frameIndex, float posX, float posY, float scaleX, float scaleY, float rotation)
        {
            FrameIndex = frameIndex;
            PositionX = posX;
            PositionY = posY;
            ScaleX = scaleX;
            ScaleY = scaleY;
            Rotation = rotation;
        }

        /// <summary>
        /// Creates a keyframe with uniform scale.
        /// </summary>
        public StageKeyframeData(int frameIndex, float posX, float posY, float scale, float rotation)
            : this(frameIndex, posX, posY, scale, scale, rotation)
        {
            UniformScale = true;
        }

        // ====================================================================
        // METHODS
        // ====================================================================

        /// <summary>
        /// Creates a deep copy of this keyframe data.
        /// </summary>
        public StageKeyframeData Clone()
        {
            return new StageKeyframeData
            {
                FrameIndex = FrameIndex,
                PositionX = PositionX,
                PositionY = PositionY,
                ScaleX = ScaleX,
                ScaleY = ScaleY,
                UniformScale = UniformScale,
                Rotation = Rotation,
                PositionEasing = PositionEasing,
                ScaleEasing = ScaleEasing,
                RotationEasing = RotationEasing
            };
        }

        /// <summary>
        /// Interpolates between two keyframes.
        /// </summary>
        /// <param name="from">Starting keyframe.</param>
        /// <param name="to">Ending keyframe.</param>
        /// <param name="t">Interpolation factor (0-1).</param>
        /// <returns>Interpolated transform values.</returns>
        public static StageKeyframeData Lerp(StageKeyframeData from, StageKeyframeData to, float t)
        {
            // Apply easing to the interpolation factor
            float posT = ApplyEasing(t, to.PositionEasing);
            float scaleT = ApplyEasing(t, to.ScaleEasing);
            float rotT = ApplyEasing(t, to.RotationEasing);

            return new StageKeyframeData
            {
                FrameIndex = (int)Math.Round(from.FrameIndex + (to.FrameIndex - from.FrameIndex) * t),
                PositionX = from.PositionX + (to.PositionX - from.PositionX) * posT,
                PositionY = from.PositionY + (to.PositionY - from.PositionY) * posT,
                ScaleX = from.ScaleX + (to.ScaleX - from.ScaleX) * scaleT,
                ScaleY = from.ScaleY + (to.ScaleY - from.ScaleY) * scaleT,
                UniformScale = from.UniformScale && to.UniformScale,
                Rotation = LerpAngle(from.Rotation, to.Rotation, rotT),
                PositionEasing = to.PositionEasing,
                ScaleEasing = to.ScaleEasing,
                RotationEasing = to.RotationEasing
            };
        }

        /// <summary>
        /// Interpolates between two angles, taking the shortest path.
        /// Handles the wraparound at ≥ 180 degrees correctly.
        /// </summary>
        private static float LerpAngle(float from, float to, float t)
        {
            // Calculate the difference, wrapping to find shortest path
            float diff = to - from;
            
            // Normalize difference to range (-180, 180]
            // This handles the modulo of negative numbers correctly in C#
            while (diff > 180f) diff -= 360f;
            while (diff <= -180f) diff += 360f;
            
            return from + diff * t;
        }

        /// <summary>
        /// Applies an easing function to the interpolation factor.
        /// </summary>
        private static float ApplyEasing(float t, EasingType easing)
        {
            return easing switch
            {
                EasingType.Linear => t,
                EasingType.EaseIn => t * t,
                EasingType.EaseOut => 1 - (1 - t) * (1 - t),
                EasingType.EaseInOut => t < 0.5f ? 2 * t * t : 1 - (float)Math.Pow(-2 * t + 2, 2) / 2,
                EasingType.Step => t < 1f ? 0f : 1f,
                _ => t
            };
        }
    }

    /// <summary>
    /// Easing types for keyframe interpolation.
    /// </summary>
    public enum EasingType
    {
        /// <summary>Constant speed interpolation.</summary>
        Linear,

        /// <summary>Starts slow, accelerates.</summary>
        EaseIn,

        /// <summary>Starts fast, decelerates.</summary>
        EaseOut,

        /// <summary>Smooth acceleration and deceleration.</summary>
        EaseInOut,

        /// <summary>No interpolation, snap to target value.</summary>
        Step
    }
}
