using System;
using FluentIcons.Common;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Selection;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// A change to the selection mask (create, add, subtract, select all, invert, clear).
    /// Applies directly to <see cref="CanvasDocument.Selection"/>. Masks are stored run-length
    /// encoded and the item takes part in the history memory budget.
    /// </summary>
    public sealed class SelectionChangeItem : OffloadableHistoryItemBase
    {
        public enum SelectionChangeKind { Create, Add, Subtract, SelectAll, Invert, Clear }

        private readonly CanvasDocument _doc;
        private readonly SelectionChangeKind _kind;
        private byte[] _before;
        private byte[] _after;
        private readonly bool _hasChanges;

        public override Icon HistoryIcon { get; set; } = Icon.SelectAllOn;
        public override string Description { get; }
        public SelectionChangeKind Kind => _kind;

        /// <summary>Whether before and after differ; callers skip pushing when false.</summary>
        public bool HasChanges => _hasChanges;

        public SelectionChangeItem(CanvasDocument doc, SelectionChangeKind kind, SelectionRegion beforeRegion, SelectionRegion afterRegion)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            ArgumentNullException.ThrowIfNull(beforeRegion);
            ArgumentNullException.ThrowIfNull(afterRegion);

            _kind = kind;
            _hasChanges = !RegionsEqual(beforeRegion, afterRegion);
            _before = SelectionRegionCodec.Encode(beforeRegion);
            _after = SelectionRegionCodec.Encode(afterRegion);

            Description = kind switch
            {
                SelectionChangeKind.Create => "Create Selection",
                SelectionChangeKind.Add => "Add to Selection",
                SelectionChangeKind.Subtract => "Subtract from Selection",
                SelectionChangeKind.SelectAll => "Select All",
                SelectionChangeKind.Invert => "Invert Selection",
                SelectionChangeKind.Clear => "Clear Selection",
                _ => "Selection Change"
            };
            HistoryIcon = kind switch
            {
                SelectionChangeKind.Add => Icon.AddSquareMultiple,
                SelectionChangeKind.Subtract => Icon.SubtractSquareMultiple,
                SelectionChangeKind.Invert => Icon.ArrowSwap,
                SelectionChangeKind.Clear => Icon.SelectAllOff,
                _ => Icon.SelectAllOn
            };
        }

        public override void Undo()
        {
            SelectionRegionCodec.Decode(_before, _doc.Selection);
            _doc.RaiseSelectionChanged();
            LoggingService.Info("Undo selection change kind={Kind}", _kind);
        }

        public override void Redo()
        {
            SelectionRegionCodec.Decode(_after, _doc.Selection);
            _doc.RaiseSelectionChanged();
            LoggingService.Info("Redo selection change kind={Kind}", _kind);
        }

        private static bool RegionsEqual(SelectionRegion a, SelectionRegion b)
        {
            if (a.IsEmpty && b.IsEmpty) return true;
            if (a.IsEmpty != b.IsEmpty) return false;

            var ba = a.Bounds; var bb = b.Bounds;
            if (ba.X != bb.X || ba.Y != bb.Y || ba.Width != bb.Width || ba.Height != bb.Height)
                return false;

            for (int y = ba.Y; y < ba.Y + ba.Height; y++)
                for (int x = ba.X; x < ba.X + ba.Width; x++)
                    if (a.Contains(x, y) != b.Contains(x, y))
                        return false;
            return true;
        }

        // ── memory budget ──────────────────────────────────────────────

        protected override long PayloadBytes => _before.Length + _after.Length;

        protected override byte[]? SerializePayload()
        {
            using var ms = new System.IO.MemoryStream();
            using var bw = new System.IO.BinaryWriter(ms);
            HistoryBlobs.Write(bw, _before);
            HistoryBlobs.Write(bw, _after);
            return ms.ToArray();
        }

        protected override void DeserializePayload(byte[] data)
        {
            using var ms = new System.IO.MemoryStream(data);
            using var br = new System.IO.BinaryReader(ms);
            _before = HistoryBlobs.Read(br) ?? Array.Empty<byte>();
            _after = HistoryBlobs.Read(br) ?? Array.Empty<byte>();
        }

        protected override void ReleasePayload()
        {
            _before = Array.Empty<byte>();
            _after = Array.Empty<byte>();
        }
    }
}
