using System;
using System.IO;
using FluentIcons.Common;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Selection;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// A change to a floating selection's transform (move, scale, rotate, pivot). Applies to
    /// <see cref="CanvasDocument.Floating"/>; the lift item below it in the stack guarantees one
    /// exists whenever this item is undone or redone.
    /// </summary>
    public sealed class SelectionTransformItem : OffloadableHistoryItemBase, ICoalescingHistoryItem
    {
        public override Icon HistoryIcon { get; set; } = Icon.CopySelect;

        public enum TransformKind { Move, Scale, Rotate }

        /// <summary>The transform state of a floating selection, plus its pixels when a bake changed them.</summary>
        public readonly struct TransformSnapshot
        {
            public readonly int FloatX, FloatY;
            public readonly double ScaleX, ScaleY;
            public readonly double AngleDeg, CumulativeAngleDeg;
            public readonly int OrigCenterX, OrigCenterY, OrigW, OrigH;
            public readonly double PivotOffsetX, PivotOffsetY;
            public readonly bool PivotCustom;
            public readonly bool BufferFlipped;
            public readonly byte[]? Buffer;
            public readonly int BufferWidth, BufferHeight;

            public TransformSnapshot(
                int floatX, int floatY, double scaleX, double scaleY,
                double angleDeg, double cumulativeAngleDeg,
                int origCenterX, int origCenterY, int origW, int origH,
                double pivotOffsetX, double pivotOffsetY, bool pivotCustom, bool bufferFlipped,
                byte[]? buffer = null, int bufferWidth = 0, int bufferHeight = 0)
            {
                FloatX = floatX; FloatY = floatY; ScaleX = scaleX; ScaleY = scaleY;
                AngleDeg = angleDeg; CumulativeAngleDeg = cumulativeAngleDeg;
                OrigCenterX = origCenterX; OrigCenterY = origCenterY; OrigW = origW; OrigH = origH;
                PivotOffsetX = pivotOffsetX; PivotOffsetY = pivotOffsetY; PivotCustom = pivotCustom; BufferFlipped = bufferFlipped;
                Buffer = buffer; BufferWidth = bufferWidth; BufferHeight = bufferHeight;
            }

            public TransformSnapshot WithBuffer(byte[]? buffer) => new(
                FloatX, FloatY, ScaleX, ScaleY, AngleDeg, CumulativeAngleDeg,
                OrigCenterX, OrigCenterY, OrigW, OrigH, PivotOffsetX, PivotOffsetY, PivotCustom, BufferFlipped,
                buffer, BufferWidth, BufferHeight);
        }

        /// <summary>Captures the transform of <paramref name="f"/>; include the buffer when the operation bakes pixels (scale/rotate).</summary>
        public static TransformSnapshot Capture(FloatingSelection f, bool includeBuffer) => new(
            f.X, f.Y, f.ScaleX, f.ScaleY, f.AngleDeg, f.CumulativeAngleDeg,
            f.OrigCenterX, f.OrigCenterY, f.OrigW, f.OrigH,
            f.PivotOffsetX, f.PivotOffsetY, f.PivotCustom, f.BufferFlipped,
            includeBuffer ? (byte[])f.Pixels.Clone() : null, f.Width, f.Height);

        /// <summary>Applies a snapshot to <paramref name="f"/> (restoring the buffer if the snapshot carries one).</summary>
        public static void ApplyTo(FloatingSelection f, in TransformSnapshot s)
        {
            f.X = s.FloatX; f.Y = s.FloatY;
            f.ScaleX = s.ScaleX; f.ScaleY = s.ScaleY;
            f.AngleDeg = s.AngleDeg; f.CumulativeAngleDeg = s.CumulativeAngleDeg;
            f.OrigCenterX = s.OrigCenterX; f.OrigCenterY = s.OrigCenterY; f.OrigW = s.OrigW; f.OrigH = s.OrigH;
            f.PivotOffsetX = s.PivotOffsetX; f.PivotOffsetY = s.PivotOffsetY; f.PivotCustom = s.PivotCustom;
            f.BufferFlipped = s.BufferFlipped;
            if (s.Buffer != null)
            {
                f.Pixels = (byte[])s.Buffer.Clone();
                f.Width = s.BufferWidth;
                f.Height = s.BufferHeight;
            }
        }

        private readonly CanvasDocument _doc;
        private readonly TransformKind _kind;
        private TransformSnapshot _before;
        private TransformSnapshot _after;

        public override string Description { get; }
        public TransformKind Kind => _kind;

        public SelectionTransformItem(CanvasDocument doc, TransformKind kind, TransformSnapshot before, TransformSnapshot after)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _kind = kind;
            _before = before;
            _after = after;
            Description = kind switch
            {
                TransformKind.Move => "Move Selection",
                TransformKind.Scale => "Scale Selection",
                TransformKind.Rotate => "Rotate Selection",
                _ => "Transform Selection"
            };
        }

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
            _before.BufferHeight != _after.BufferHeight ||
            _before.BufferFlipped != _after.BufferFlipped ||
            Math.Abs(_before.PivotOffsetX - _after.PivotOffsetX) > 0.001 ||
            Math.Abs(_before.PivotOffsetY - _after.PivotOffsetY) > 0.001;

        public override void Undo() => Apply(_before);
        public override void Redo() => Apply(_after);

        /// <summary>
        /// A transform of the same kind on the same document pushed right after this one is the
        /// continuation of the same gesture (nudge, nudge, nudge): keep this item's <c>before</c>
        /// and take the newcomer's <c>after</c>.
        /// </summary>
        public bool TryAbsorb(IHistoryItem next)
        {
            if (next is not SelectionTransformItem n || !ReferenceEquals(n._doc, _doc) || n._kind != _kind || IsOffloaded)
                return false;
            _after = n._after;
            return true;
        }

        private void Apply(in TransformSnapshot s)
        {
            var f = _doc.Floating;
            if (f == null)
            {
                LoggingService.Warning("SelectionTransformItem applied with no floating selection; the lift item should sit below it");
                return;
            }
            ApplyTo(f, s);
            SelectionRegionBuilders.RebuildFromFloating(_doc.Selection, f, _doc.PixelWidth, _doc.PixelHeight);
            _doc.RaiseSelectionChanged();
        }

        // ── memory budget ──────────────────────────────────────────────

        protected override long PayloadBytes => (_before.Buffer?.Length ?? 0) + (_after.Buffer?.Length ?? 0);

        protected override byte[]? SerializePayload()
        {
            if (_before.Buffer == null && _after.Buffer == null) return null;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            HistoryBlobs.Write(bw, _before.Buffer);
            HistoryBlobs.Write(bw, _after.Buffer);
            return ms.ToArray();
        }

        protected override void DeserializePayload(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            _before = _before.WithBuffer(HistoryBlobs.Read(br));
            _after = _after.WithBuffer(HistoryBlobs.Read(br));
        }

        protected override void ReleasePayload()
        {
            _before = _before.WithBuffer(null);
            _after = _after.WithBuffer(null);
        }
    }
}
