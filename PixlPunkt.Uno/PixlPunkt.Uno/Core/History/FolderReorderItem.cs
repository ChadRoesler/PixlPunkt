using System;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Logging;

namespace PixlPunkt.Uno.Core.History
{
    /// <summary>
    /// History item for folder reorder operations.
    /// </summary>
    /// <remarks>
    /// Captures the folder that was moved and its original/new positions in the root items list.
    /// Undo moves the folder back to its original position; Redo moves it to the new position.
    /// </remarks>
    public sealed class FolderReorderItem : IHistoryItem
    {
        private readonly CanvasDocument _document;
        private readonly LayerFolder _folder;
        private readonly int _originalRootIndex;
        private readonly int _newRootIndex;
        private readonly string _folderName;

        /// <summary>
        /// Gets a quick reference icon of the operation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.FolderArrowRight;

        /// <summary>
        /// Gets a human-readable description of the action.
        /// </summary>
        public string Description => $"Reorder Folder \"{_folderName}\"";

        /// <summary>
        /// Creates a new folder reorder history item.
        /// </summary>
        /// <param name="document">The document containing the folder.</param>
        /// <param name="folder">The folder that was moved.</param>
        /// <param name="originalRootIndex">The original index in root items (before the move).</param>
        /// <param name="newRootIndex">The new index in root items (after the move).</param>
        public FolderReorderItem(CanvasDocument document, LayerFolder folder, int originalRootIndex, int newRootIndex)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
            _originalRootIndex = originalRootIndex;
            _newRootIndex = newRootIndex;
            _folderName = folder.Name ?? "Folder";
        }

        /// <summary>
        /// Undoes the reorder by moving the folder back to its original position.
        /// </summary>
        public void Undo()
        {
            _document.RaiseBeforeStructureChanged();
            MoveFolderToIndex(_newRootIndex, _originalRootIndex);
            _document.RaiseStructureChanged();
            LoggingService.Info("Undo folder reorder document={Doc} folder={Folder} from={From} to={To}",
                _document.Name ?? "(doc)", _folderName, _newRootIndex, _originalRootIndex);
        }

        /// <summary>
        /// Redoes the reorder by moving the folder to its new position.
        /// </summary>
        public void Redo()
        {
            _document.RaiseBeforeStructureChanged();
            MoveFolderToIndex(_originalRootIndex, _newRootIndex);
            _document.RaiseStructureChanged();
            LoggingService.Info("Redo folder reorder document={Doc} folder={Folder} from={From} to={To}",
                _document.Name ?? "(doc)", _folderName, _originalRootIndex, _newRootIndex);
        }

        private void MoveFolderToIndex(int fromIndex, int toIndex)
        {
            // This mimics MoveLayerByReferenceWithoutHistory but for folders
            var rootItems = _document.RootItems;
            int currentIndex = -1;
            for (int i = 0; i < rootItems.Count; i++)
            {
                if (ReferenceEquals(rootItems[i], _folder))
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0) return;

            // Use the document's internal move method
            _document.MoveRootItem(_folder, toIndex);

            // Recomposite after structural change
            _document.CompositeTo(_document.Surface);
        }
    }
}
