using System;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Logging;

namespace PixlPunkt.Uno.Core.History
{
    /// <summary>
    /// History item for layer remove operations.
    /// </summary>
    /// <remarks>
    /// Captures the layer that was removed and its position in the layer stack.
    /// Undo re-adds the layer at its original position; Redo removes it again.
    /// The layer's pixel data is preserved in memory for restoration.
    /// </remarks>
    public sealed class LayerRemoveItem : IHistoryItem
    {
        private readonly CanvasDocument _document;
        private readonly RasterLayer _layer;
        private readonly int _originalIndex;
        private readonly string _layerName;

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.DeleteLines;

        /// <summary>
        /// Gets a human-readable description of the action.
        /// </summary>
        public string Description => $"Remove Layer \"{_layerName}\"";

        /// <summary>
        /// Creates a new layer remove history item.
        /// </summary>
        /// <param name="document">The document containing the layer.</param>
        /// <param name="layer">The layer that was removed.</param>
        /// <param name="originalIndex">The index where the layer was before removal.</param>
        public LayerRemoveItem(CanvasDocument document, RasterLayer layer, int originalIndex)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _originalIndex = originalIndex;
            _layerName = layer.Name ?? "Layer";
        }

        /// <summary>
        /// Undoes the layer removal by re-adding the layer.
        /// </summary>
        public void Undo()
        {
            _document.AddLayerWithoutHistory(_layer, _originalIndex);
            LoggingService.Info("Undo layer remove document={Doc} layer={Layer} index={Index}", _document.Name ?? "(doc)", _layerName, _originalIndex);
        }

        /// <summary>
        /// Redoes the layer removal by removing the layer again.
        /// </summary>
        public void Redo()
        {
            // Pass allowRemoveLast=true since the original remove was allowed
            _document.RemoveLayerWithoutHistory(_layer, allowRemoveLast: true);
            LoggingService.Info("Redo layer remove document={Doc} layer={Layer} index={Index}", _document.Name ?? "(doc)", _layerName, _originalIndex);
        }
    }
}
