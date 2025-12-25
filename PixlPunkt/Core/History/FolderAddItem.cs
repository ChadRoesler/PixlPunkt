using System;
using FluentIcons.Common;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// History item for folder add operations.
    /// </summary>
    /// <remarks>
    /// Captures the folder that was added and its position in the layer stack.
    /// Undo removes the folder; Redo re-adds it at the same position.
    /// </remarks>
    public sealed class FolderAddItem : IHistoryItem
    {
        private readonly CanvasDocument _document;
        private readonly LayerFolder _folder;
        private readonly LayerFolder? _parent;
        private readonly int _insertIndex;
        private readonly string _folderName;

        /// <summary>
        /// Gets a quick reference icon of the operation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.FolderAdd;

        /// <summary>
        /// Gets a human-readable description of the action.
        /// </summary>
        public string Description => $"Add Folder \"{_folderName}\"";

        /// <summary>
        /// Creates a new folder add history item.
        /// </summary>
        /// <param name="document">The document containing the folder.</param>
        /// <param name="folder">The folder that was added.</param>
        /// <param name="parent">The parent folder (null if at root level).</param>
        /// <param name="insertIndex">The index where the folder was inserted.</param>
        public FolderAddItem(CanvasDocument document, LayerFolder folder, LayerFolder? parent, int insertIndex)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
            _parent = parent;
            _insertIndex = insertIndex;
            _folderName = folder.Name ?? "Folder";
        }

        /// <summary>
        /// Undoes the folder addition by removing the folder.
        /// </summary>
        public void Undo()
        {
            _document.RaiseBeforeStructureChanged();
            _document.RemoveLayerTreeWithoutHistory(_folder);
            _document.RaiseStructureChanged();
            LoggingService.Info("Undo folder add document={Doc} folder={Folder} index={Index}",
                _document.Name ?? "(doc)", _folderName, _insertIndex);
        }

        /// <summary>
        /// Redoes the folder addition by re-adding the folder.
        /// </summary>
        public void Redo()
        {
            _document.RaiseBeforeStructureChanged();
            _document.InsertLayerTreeWithoutHistory(_folder, _parent, _insertIndex);
            _document.RaiseStructureChanged();
            LoggingService.Info("Redo folder add document={Doc} folder={Folder} index={Index}",
                _document.Name ?? "(doc)", _folderName, _insertIndex);
        }
    }
}
