using FluentIcons.Common;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;

namespace PixlPunkt.Core.History
{
    public class LayerMergeDownItem : OffloadableHistoryItemBase
    {
        private readonly RasterLayer _merged;
        private readonly LayerBase _removedTop;
        private readonly RasterLayer _targetBelow;
        private readonly LayerFolder? _parent;
        private readonly int _insertIndex;
        private readonly CanvasDocument _doc;

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public override Icon HistoryIcon { get; set; } = Icon.Merge;
        public override string Description { get; }

        // Three live layers are retained between undo and redo; count their surfaces.
        protected override long PayloadBytes =>
            _merged.Surface.Pixels.Length + _targetBelow.Surface.Pixels.Length +
            (_removedTop is RasterLayer top ? top.Surface.Pixels.Length : 0);

        public LayerMergeDownItem(CanvasDocument doc, LayerBase removedTop, RasterLayer targetBelow, RasterLayer merged, int index, string desc)
        {
            _merged = merged;
            _targetBelow = targetBelow;
            _removedTop = removedTop;
            _parent = targetBelow.Parent;
            _insertIndex = index;
            _doc = doc;
            Description = desc;
        }

        public override void Undo()
        {
            _doc.InsertLayerTreeWithoutHistory(_removedTop, _parent, _insertIndex + 1);
            _doc.InsertLayerTreeWithoutHistory(_targetBelow, _parent, _insertIndex);
            _doc.RemoveLayerWithoutHistory(_merged);
            _doc.RaiseStructureChanged();
        }

        public override void Redo()
        {
            _doc.InsertLayerTreeWithoutHistory(_merged, _parent, _insertIndex);
            _doc.RemoveLayerTreeWithoutHistory(_removedTop);
            _doc.RemoveLayerTreeWithoutHistory(_targetBelow);
            _doc.RaiseStructureChanged();
        }
    }
}