using System;
using FluentIcons.Common;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// History item for moving a layer between folders (or to/from root).
    /// </summary>
    /// <remarks>
    /// Captures the layer that was moved, its original parent and index, and its new parent and index.
    /// Undo moves the layer back to its original parent; Redo moves it to the new parent.
    /// </remarks>
    public sealed class LayerMoveToFolderItem : IHistoryItem
    {
        private readonly CanvasDocument _document;
        private readonly LayerBase _layer;
        private readonly LayerFolder? _originalParent;
        private readonly int _originalIndex;
        private readonly LayerFolder? _newParent;
        private readonly int _newIndex;
        private readonly string _layerName;

        /// <summary>
        /// Gets a quick reference icon of the operation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.FolderArrowRight;

        /// <summary>
        /// Gets a human-readable description of the action.
        /// </summary>
        public string Description => _newParent == null 
            ? $"Move \"{_layerName}\" to root" 
            : $"Move \"{_layerName}\" to folder \"{_newParent.Name}\"";

        /// <summary>
        /// Creates a new layer move to folder history item.
        /// </summary>
        /// <param name="document">The document containing the layer.</param>
        /// <param name="layer">The layer that was moved.</param>
        /// <param name="originalParent">The original parent folder (null if was at root).</param>
        /// <param name="originalIndex">The original index within the parent (or root items).</param>
        /// <param name="newParent">The new parent folder (null if moved to root).</param>
        /// <param name="newIndex">The new index within the parent (or root items).</param>
        public LayerMoveToFolderItem(
            CanvasDocument document, 
            LayerBase layer, 
            LayerFolder? originalParent, 
            int originalIndex,
            LayerFolder? newParent,
            int newIndex)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _originalParent = originalParent;
            _originalIndex = originalIndex;
            _newParent = newParent;
            _newIndex = newIndex;
            _layerName = layer.Name ?? "Layer";
        }

        /// <summary>
        /// Undoes the move by moving the layer back to its original parent and position.
        /// </summary>
        public void Undo()
        {
            _document.MoveLayerToFolderWithoutHistory(_layer, _originalParent, _originalIndex);
        }

        /// <summary>
        /// Redoes the move by moving the layer to its new parent and position.
        /// </summary>
        public void Redo()
        {
            _document.MoveLayerToFolderWithoutHistory(_layer, _newParent, _newIndex);
        }
    }
}
