using System;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;

namespace PixlPunkt.Uno.Core.History
{
    /// <summary>
    /// History item for layer reorder operations.
    /// </summary>
    /// <remarks>
    /// Captures the layer that was moved and its original/new positions in the root items list.
    /// Undo moves the layer back to its original position; Redo moves it to the new position.
    /// Uses layer reference to find the current position, then moves to the target position.
    /// </remarks>
    public sealed class LayerReorderItem : IHistoryItem
    {
        private readonly CanvasDocument _document;
        private readonly RasterLayer _layer;
        private readonly int _originalRootIndex;
        private readonly int _newRootIndex;
        private readonly string _layerName;

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.BranchCompare;

        /// <summary>
        /// Gets a human-readable description of the action.
        /// </summary>
        public string Description => $"Reorder Layer \"{_layerName}\"";

        /// <summary>
        /// Creates a new layer reorder history item.
        /// </summary>
        /// <param name="document">The document containing the layer.</param>
        /// <param name="layer">The layer that was moved.</param>
        /// <param name="originalRootIndex">The original index in root items (before the move).</param>
        /// <param name="newRootIndex">The new index in root items (after the move).</param>
        public LayerReorderItem(CanvasDocument document, RasterLayer layer, int originalRootIndex, int newRootIndex)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _originalRootIndex = originalRootIndex;
            _newRootIndex = newRootIndex;
            _layerName = layer.Name ?? "Layer";
        }

        /// <summary>
        /// Undoes the reorder by moving the layer back to its original position.
        /// </summary>
        public void Undo()
        {
            _document.MoveLayerByReferenceWithoutHistory(_layer, _originalRootIndex);
        }

        /// <summary>
        /// Redoes the reorder by moving the layer to its new position.
        /// </summary>
        public void Redo()
        {
            _document.MoveLayerByReferenceWithoutHistory(_layer, _newRootIndex);
        }
    }
}
