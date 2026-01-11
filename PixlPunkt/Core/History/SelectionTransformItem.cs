using System;
using FluentIcons.Common;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// History item that captures a selection transform operation (move, scale, or rotate).
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionTransformItem allows undo/redo of individual transform operations on floating selections
    /// without committing the selection back to the layer. This enables granular undo for:
    /// - Move operations (translation changes)
    /// - Scale operations (scale factor changes)
    /// - Rotate operations (rotation angle changes)
    /// </para>
    /// <para>
    /// For scale and rotate operations, the buffer state is also captured because these operations
    /// "bake" the transform into the buffer on release. Without capturing the buffer, undo would
    /// apply the old transform parameters to the already-transformed buffer.
    /// </para>
    /// </remarks>
    public sealed class SelectionTransformItem : IHistoryItem
    {
        /// <summary>
        /// Gets a quick reference icon of the operation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.CopySelect;

        /// <summary>
        /// Describes what kind of transform this item represents.
        /// </summary>
        public enum TransformKind
        {
            /// <summary>Translation/position change.</summary>
            Move,
            /// <summary>Scale factor change.</summary>
            Scale,
            /// <summary>Rotation angle change.</summary>
            Rotate
        }

        /// <summary>
        /// Snapshot of selection transform state.
        /// </summary>
        public readonly struct TransformSnapshot
        {
            public readonly int FloatX;
            public readonly int FloatY;
            public readonly double ScaleX;
            public readonly double ScaleY;
            public readonly double AngleDeg;
            public readonly double CumulativeAngleDeg;
            public readonly int OrigCenterX;
            public readonly int OrigCenterY;
            public readonly int OrigW;
            public readonly int OrigH;
            public readonly double PivotOffsetX;
            public readonly double PivotOffsetY;
            public readonly bool PivotCustom;

            // Buffer state for scale/rotate operations
            public readonly byte[]? Buffer;
            public readonly int BufferWidth;
            public readonly int BufferHeight;

            public TransformSnapshot(
                int floatX, int floatY,
                double scaleX, double scaleY,
                double angleDeg, double cumulativeAngleDeg,
                int origCenterX, int origCenterY,
                int origW, int origH,
                double pivotOffsetX, double pivotOffsetY,
                bool pivotCustom,
                byte[]? buffer = null,
                int bufferWidth = 0,
                int bufferHeight = 0)
            {
                FloatX = floatX;
                FloatY = floatY;
                ScaleX = scaleX;
                ScaleY = scaleY;
                AngleDeg = angleDeg;
                CumulativeAngleDeg = cumulativeAngleDeg;
                OrigCenterX = origCenterX;
                OrigCenterY = origCenterY;
                OrigW = origW;
                OrigH = origH;
                PivotOffsetX = pivotOffsetX;
                PivotOffsetY = pivotOffsetY;
                PivotCustom = pivotCustom;
                Buffer = buffer;
                BufferWidth = bufferWidth;
                BufferHeight = bufferHeight;
            }
        }

        private readonly TransformKind _kind;
        private readonly TransformSnapshot _before;
        private readonly TransformSnapshot _after;
        private readonly Action<TransformSnapshot> _applySnapshot;

        /// <inheritdoc/>
        public string Description { get; }

        /// <summary>
        /// Gets the kind of transform this item represents.
        /// </summary>
        public TransformKind Kind => _kind;

        /// <summary>
        /// Creates a new selection transform history item.
        /// </summary>
        /// <param name="kind">The type of transform operation.</param>
        /// <param name="before">State before the transform.</param>
        /// <param name="after">State after the transform.</param>
        /// <param name="applySnapshot">Callback to apply a snapshot to the selection state.</param>
        public SelectionTransformItem(
            TransformKind kind,
            TransformSnapshot before,
            TransformSnapshot after,
            Action<TransformSnapshot> applySnapshot)
        {
            _kind = kind;
            _before = before;
            _after = after;
            _applySnapshot = applySnapshot ?? throw new ArgumentNullException(nameof(applySnapshot));

            Description = kind switch
            {
                TransformKind.Move => "Move Selection",
                TransformKind.Scale => "Scale Selection",
                TransformKind.Rotate => "Rotate Selection",
                _ => "Transform Selection"
            };
        }

        /// <inheritdoc/>
        public void Undo()
        {
            _applySnapshot(_before);
        }

        /// <inheritdoc/>
        public void Redo()
        {
            _applySnapshot(_after);
        }

        /// <summary>
        /// Checks if this transform actually changed anything (before != after).
        /// </summary>
        public bool HasChanges =>
            _before.FloatX != _after.FloatX ||
            _before.FloatY != _after.FloatY ||
            Math.Abs(_before.ScaleX - _after.ScaleX) > 0.001 ||
            Math.Abs(_before.ScaleY - _after.ScaleY) > 0.001 ||
            Math.Abs(_before.AngleDeg - _after.AngleDeg) > 0.1 ||
            Math.Abs(_before.CumulativeAngleDeg - _after.CumulativeAngleDeg) > 0.1 ||
            _before.OrigCenterX != _after.OrigCenterX ||
            _before.OrigCenterY != _after.OrigCenterY ||
            _before.OrigW != _after.OrigW ||
            _before.OrigH != _after.OrigH ||
            _before.BufferWidth != _after.BufferWidth ||
            _before.BufferHeight != _after.BufferHeight;
    }
}
