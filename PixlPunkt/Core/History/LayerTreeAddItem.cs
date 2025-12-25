using FluentIcons.Common;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;

namespace PixlPunkt.Core.History
{
    internal sealed class LayerTreeAddItem : IHistoryItem
    {
        private readonly CanvasDocument _doc;
        private readonly LayerBase _tree;
        private readonly LayerFolder? _parent;
        private readonly int _index;

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.LayerDiagonalAdd;
        public string Description { get; }

        public LayerTreeAddItem(CanvasDocument doc, LayerBase tree, LayerFolder? parent, int index, string desc)
        {
            _doc = doc;
            _tree = tree;
            _parent = parent;
            _index = index;
            Description = desc;
        }

        public void Undo()
        {
            _doc.RaiseBeforeStructureChanged();
            _doc.RemoveLayerTreeWithoutHistory(_tree);
            _doc.RaiseStructureChanged();
        }

        public void Redo()
        {
            _doc.RaiseBeforeStructureChanged();
            _doc.InsertLayerTreeWithoutHistory(_tree, _parent, _index);
            _doc.RaiseStructureChanged();
        }
    }
}
