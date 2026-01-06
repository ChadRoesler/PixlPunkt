using FluentIcons.Common;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;

namespace PixlPunkt.Core.History
{
    public class LayerMergeDownItem : IHistoryItem
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
        public Icon HistoryIcon { get; set; } = Icon.Merge;
        public string Description { get; }

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

        public void Undo()
        {
            _doc.RaiseBeforeStructureChanged();
            _doc.InsertLayerTreeWithoutHistory(_removedTop, _parent, _insertIndex + 1);
            _doc.InsertLayerTreeWithoutHistory(_targetBelow, _parent, _insertIndex);
            _doc.RemoveLayerWithoutHistory(_merged);
            _doc.RaiseStructureChanged();
        }

        public void Redo()
        {
            _doc.RaiseBeforeStructureChanged();
            _doc.InsertLayerTreeWithoutHistory(_merged, _parent, _insertIndex);
            _doc.RemoveLayerTreeWithoutHistory(_removedTop);
            _doc.RemoveLayerTreeWithoutHistory(_targetBelow);
            _doc.RaiseStructureChanged();
        }
    }
}