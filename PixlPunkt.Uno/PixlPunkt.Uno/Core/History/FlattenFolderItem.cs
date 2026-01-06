using System;
using System.Collections.Generic;
using System.Text;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;

namespace PixlPunkt.Uno.Core.History
{
    public class FlattenFolderItem : IHistoryItem
    {
        private readonly LayerFolder _original;
        private readonly RasterLayer _flattened;
        private readonly LayerFolder? _parent;
        private readonly int _insertIndex;
        private readonly CanvasDocument _doc;
        public string Description { get; }

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.FolderList;

        public FlattenFolderItem(CanvasDocument doc, LayerFolder original, RasterLayer flattened, int index, string description)
        {
            _original = original;
            _flattened = flattened;
            _parent = original.Parent;
            _insertIndex = index;
            _doc = doc;
            Description = description;
        }

        public void Undo()
        {
            _doc.RaiseBeforeStructureChanged();

            _doc.InsertLayerTreeWithoutHistory(_original, _parent, _insertIndex);
            _doc.RemoveLayerWithoutHistory(_flattened);
            _doc.RaiseStructureChanged();

        }

        public void Redo()
        {
            _doc.RaiseBeforeStructureChanged();
            _doc.InsertLayerTreeWithoutHistory(_flattened, _parent, _insertIndex);
            _doc.RemoveLayerTreeWithoutHistory(_original);
            _doc.RaiseStructureChanged();
        }
    }
}
