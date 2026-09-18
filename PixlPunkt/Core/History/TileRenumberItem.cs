using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Core.Document;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// Tile ids renumbered 1..N. Holds the forward map and its inverse; both directions go
    /// through <see cref="CanvasDocument.ApplyTileIdMap"/> so the tile set, every layer mapping
    /// and the voxel side-tile references move together. Because this is an ordinary history
    /// item, older items that remember tile ids stay valid: undoing past this point restores
    /// the ids they were recorded against.
    /// </summary>
    public sealed class TileRenumberItem : IHistoryItem
    {
        private readonly CanvasDocument _doc;
        private readonly Dictionary<int, int> _forward;
        private readonly Dictionary<int, int> _inverse;

        public Icon HistoryIcon { get; set; } = Icon.TableCursor;
        public string Description => "Renumber Tiles";

        public TileRenumberItem(CanvasDocument doc, IReadOnlyDictionary<int, int> forward)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _forward = new Dictionary<int, int>(forward);
            _inverse = new Dictionary<int, int>(_forward.Count);
            foreach (var (oldId, newId) in _forward) _inverse[newId] = oldId;
        }

        public void Undo() => _doc.ApplyTileIdMap(_inverse);
        public void Redo() => _doc.ApplyTileIdMap(_forward);
    }
}
