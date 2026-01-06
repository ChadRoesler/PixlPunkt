using System;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Logging;

namespace PixlPunkt.Uno.Core.History
{
    /// <summary>
    /// History item for reference layer add operations.
    /// </summary>
    public sealed class ReferenceLayerAddItem : IHistoryItem
    {
        private readonly CanvasDocument _document;
        private readonly ReferenceLayer _layer;
        private readonly int _insertIndex;
        private readonly string _layerName;

        /// <summary>
        /// Gets a quick reference icon of the operation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.ImageSparkle;

        /// <summary>
        /// Gets a human-readable description of the action.
        /// </summary>
        public string Description => $"Add Reference \"{_layerName}\"";

        /// <summary>
        /// Creates a new reference layer add history item.
        /// </summary>
        /// <param name="document">The document containing the layer.</param>
        /// <param name="layer">The reference layer that was added.</param>
        /// <param name="insertIndex">The index where the layer was inserted.</param>
        public ReferenceLayerAddItem(CanvasDocument document, ReferenceLayer layer, int insertIndex)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _insertIndex = insertIndex;
            _layerName = layer.Name ?? "Reference";
        }

        /// <inheritdoc/>
        public void Undo()
        {
            _document.RemoveReferenceLayerWithoutHistory(_layer);
            LoggingService.Info("Undo reference layer add document={Doc} layer={Layer} index={Index}",
                _document.Name ?? "(doc)", _layerName, _insertIndex);
        }

        /// <inheritdoc/>
        public void Redo()
        {
            _document.AddReferenceLayerWithoutHistory(_layer, _insertIndex);
            LoggingService.Info("Redo reference layer add document={Doc} layer={Layer} index={Index}",
                _document.Name ?? "(doc)", _layerName, _insertIndex);
        }
    }
}
