using System;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Logging;

namespace PixlPunkt.Uno.Core.History
{
    /// <summary>
    /// History item for reference layer remove operations.
    /// </summary>
    public sealed class ReferenceLayerRemoveItem : IHistoryItem
    {
        private readonly CanvasDocument _document;
        private readonly ReferenceLayer _layer;
        private readonly int _removeIndex;
        private readonly string _layerName;

        /// <summary>
        /// Gets a quick reference icon of the operation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.ImageSparkle;

        /// <summary>
        /// Gets a human-readable description of the action.
        /// </summary>
        public string Description => $"Remove Reference \"{_layerName}\"";

        /// <summary>
        /// Creates a new reference layer remove history item.
        /// </summary>
        /// <param name="document">The document containing the layer.</param>
        /// <param name="layer">The reference layer that was removed.</param>
        /// <param name="removeIndex">The index where the layer was removed from.</param>
        public ReferenceLayerRemoveItem(CanvasDocument document, ReferenceLayer layer, int removeIndex)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _removeIndex = removeIndex;
            _layerName = layer.Name ?? "Reference";
        }

        /// <inheritdoc/>
        public void Undo()
        {
            _document.AddReferenceLayerWithoutHistory(_layer, _removeIndex);
            LoggingService.Info("Undo reference layer remove document={Doc} layer={Layer} index={Index}",
                _document.Name ?? "(doc)", _layerName, _removeIndex);
        }

        /// <inheritdoc/>
        public void Redo()
        {
            _document.RemoveReferenceLayerWithoutHistory(_layer);
            LoggingService.Info("Redo reference layer remove document={Doc} layer={Layer} index={Index}",
                _document.Name ?? "(doc)", _layerName, _removeIndex);
        }
    }
}
