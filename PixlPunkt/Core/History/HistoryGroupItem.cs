using System;
using System.Collections.Generic;
using FluentIcons.Common;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// An item that can absorb the item pushed immediately after it, so a burst of small
    /// operations (arrow-key nudges, repeated scale drags) becomes one undo step.
    /// </summary>
    public interface ICoalescingHistoryItem : IHistoryItem
    {
        /// <summary>
        /// Try to fold <paramref name="next"/> into this item. Return true if this item now
        /// represents both and <paramref name="next"/> should not be pushed.
        /// </summary>
        bool TryAbsorb(IHistoryItem next);
    }

    /// <summary>
    /// A history item that changes only how the document is viewed (a view flip), never its
    /// contents. It sits on the stack so it undoes and shows up in a timelapse, but it does not
    /// make the document dirty and never needs a pixel or structure refresh.
    /// </summary>
    public interface IViewOnlyHistoryItem : IHistoryItem { }

    /// <summary>
    /// Several history items that undo and redo as one user-level step, e.g. "Delete Selection"
    /// = clear the pixels + clear the mask. Created by
    /// <see cref="UnifiedHistoryStack.BeginGroup"/> / <see cref="UnifiedHistoryStack.EndGroup"/>.
    /// </summary>
    public sealed class HistoryGroupItem : IDisposableHistoryItem
    {
        private readonly List<IHistoryItem> _items = new();

        public Icon HistoryIcon { get; set; } = Icon.History;
        public string Description { get; }
        public IReadOnlyList<IHistoryItem> Items => _items;
        public int Count => _items.Count;

        public HistoryGroupItem(string description)
        {
            Description = description;
        }

        internal void Add(IHistoryItem item)
        {
            if (_items.Count > 0 && _items[^1] is ICoalescingHistoryItem c && c.TryAbsorb(item))
                return;
            _items.Add(item);
            if (_items.Count == 1) HistoryIcon = item.HistoryIcon;
        }

        public void Undo()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
                _items[i].Undo();
        }

        public void Redo()
        {
            for (int i = 0; i < _items.Count; i++)
                _items[i].Redo();
        }

        // ── IDisposableHistoryItem: delegate to the children that participate ──

        public long EstimatedMemoryBytes
        {
            get
            {
                long total = 64;
                foreach (var it in _items)
                    if (it is IDisposableHistoryItem d) total += d.EstimatedMemoryBytes;
                return total;
            }
        }

        public bool IsOffloaded
        {
            get
            {
                bool any = false;
                foreach (var it in _items)
                {
                    if (it is not IDisposableHistoryItem d) continue;
                    any = true;
                    if (!d.IsOffloaded) return false;
                }
                return any;
            }
        }

        public Guid OffloadId => Guid.Empty;

        public bool Offload(IHistoryOffloadService offloadService)
        {
            bool any = false;
            foreach (var it in _items)
                if (it is IDisposableHistoryItem d && !d.IsOffloaded && d.Offload(offloadService)) any = true;
            return any;
        }

        public bool Reload(IHistoryOffloadService offloadService)
        {
            bool ok = true;
            foreach (var it in _items)
                if (it is IDisposableHistoryItem d && d.IsOffloaded && !d.Reload(offloadService)) ok = false;
            return ok;
        }

        public void Dispose()
        {
            foreach (var it in _items)
                if (it is IDisposableHistoryItem d) d.Dispose();
        }
    }
}
