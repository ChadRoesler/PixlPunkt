using System;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Logging;

namespace PixlPunkt.Uno.Core.History
{
    /// <summary>
    /// History item for layer add operations.
    /// </summary>
    /// <remarks>
    /// Captures the layer that was added and its position in the layer stack.
    /// Undo removes the layer; Redo re-adds it at the same position.
    /// </remarks>
    public sealed class LayerAddItem : IHistoryItem
    {
        private readonly CanvasDocument _document;
        private readonly RasterLayer _layer;
        private readonly int _insertIndex;
        private readonly string _layerName;

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.LayerDiagonalAdd;

        /// <summary>
        /// Gets a human-readable description of the action.
        /// </summary>
        public string Description => $"Add Layer \"{_layerName}\"";

        /// <summary>
        /// Creates a new layer add history item.
        /// </summary>
        /// <param name="document">The document containing the layer.</param>
        /// <param name="layer">The layer that was added.</param>
        /// <param name="insertIndex">The index where the layer was inserted.</param>
        public LayerAddItem(CanvasDocument document, RasterLayer layer, int insertIndex)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _insertIndex = insertIndex;
            _layerName = layer.Name ?? "Layer";
        }

        /// <summary>
        /// Undoes the layer addition by removing the layer.
        /// </summary>
        public void Undo()
        {
            _document.RemoveLayerWithoutHistory(_layer);
            LoggingService.Info("Undo layer add document={Doc} layer={Layer} index={Index}", _document.Name ?? "(doc)", _layerName, _insertIndex);
        }

        /// <summary>
        /// Redoes the layer addition by re-adding the layer.
        /// </summary>
        public void Redo()
        {
            _document.AddLayerWithoutHistory(_layer, _insertIndex);
            LoggingService.Info("Redo layer add document={Doc} layer={Layer} index={Index}", _document.Name ?? "(doc)", _layerName, _insertIndex);
        }
    }
}
