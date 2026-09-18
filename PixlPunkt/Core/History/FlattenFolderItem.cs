using System;
using System.Collections.Generic;
using System.Text;
using FluentIcons.Common;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;

namespace PixlPunkt.Core.History
{
    public class FlattenFolderItem : OffloadableHistoryItemBase
    {
        private readonly LayerFolder _original;
        private readonly RasterLayer _flattened;
        private readonly LayerFolder? _parent;
        private readonly int _insertIndex;
        private readonly CanvasDocument _doc;
        public override string Description { get; }

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public override Icon HistoryIcon { get; set; } = Icon.FolderList;

        // The original folder subtree and the flattened result are both retained live.
        protected override long PayloadBytes
        {
            get
            {
                long total = _flattened.Surface.Pixels.Length;
                foreach (var node in _original.FlattenDepthFirst())
                    if (node is RasterLayer rl) total += rl.Surface.Pixels.Length;
                return total;
            }
        }

        public FlattenFolderItem(CanvasDocument doc, LayerFolder original, RasterLayer flattened, int index, string description)
        {
            _original = original;
            _flattened = flattened;
            _parent = original.Parent;
            _insertIndex = index;
            _doc = doc;
            Description = description;
        }

        public override void Undo()
        {

            _doc.InsertLayerTreeWithoutHistory(_original, _parent, _insertIndex);
            _doc.RemoveLayerWithoutHistory(_flattened);
            _doc.RaiseStructureChanged();

        }

        public override void Redo()
        {
            _doc.InsertLayerTreeWithoutHistory(_flattened, _parent, _insertIndex);
            _doc.RemoveLayerTreeWithoutHistory(_original);
            _doc.RaiseStructureChanged();
        }
    }
}
