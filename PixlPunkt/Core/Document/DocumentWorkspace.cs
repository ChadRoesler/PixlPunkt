using System;
using System.Collections.ObjectModel;

namespace PixlPunkt.Core.Document
{
    /// <summary>
    /// Manages a collection of open canvas documents and tracks the currently active document.
    /// </summary>
    /// <remarks>
    /// The workspace maintains an observable collection of documents for UI binding
    /// and provides events when the active document changes. This is the central
    /// coordination point for managing multiple open canvases in the application.
    /// </remarks>
    public sealed class DocumentWorkspace
    {
        /// <summary>
        /// Gets the collection of all open documents in the workspace.
        /// </summary>
        /// <value>
        /// An observable collection that can be bound to UI controls to display
        /// the list of open documents.
        /// </value>
        public ObservableCollection<CanvasDocument> Documents { get; } = new();

        /// <summary>
        /// Gets the currently active document in the workspace, or null if no document is active.
        /// </summary>
        /// <value>
        /// The document that is currently being edited, or null if the workspace is empty
        /// or no document is selected.
        /// </value>
        public CanvasDocument? ActiveDocument { get; private set; }

        /// <summary>
        /// Occurs when the active document changes.
        /// </summary>
        /// <remarks>
        /// This event is raised whenever <see cref="SetActive"/> is called with a different
        /// document, or when <see cref="Close"/> removes the active document.
        /// </remarks>
        public event Action? ActiveDocumentChanged;

        /// <summary>
        /// Adds a document to the workspace and sets it as the active document.
        /// </summary>
        /// <param name="doc">The document to add to the workspace.</param>
        /// <remarks>
        /// The document is added to the <see cref="Documents"/> collection and automatically
        /// becomes the active document, triggering the <see cref="ActiveDocumentChanged"/> event.
        /// </remarks>
        public void Add(CanvasDocument doc)
        {
            Documents.Add(doc);
            SetActive(doc);
        }

        /// <summary>
        /// Sets the specified document as the active document.
        /// </summary>
        /// <param name="doc">The document to make active.</param>
        /// <remarks>
        /// If the specified document is already active, no action is taken. Otherwise,
        /// the active document is updated and the <see cref="ActiveDocumentChanged"/> event is raised.
        /// The document must already be in the <see cref="Documents"/> collection.
        /// </remarks>
        public void SetActive(CanvasDocument doc)
        {
            if (ActiveDocument == doc) return;
            ActiveDocument = doc;
            ActiveDocumentChanged?.Invoke();
        }

        /// <summary>
        /// Closes and removes the specified document from the workspace.
        /// </summary>
        /// <param name="doc">The document to close and remove.</param>
        /// <remarks>
        /// If the closed document was the active document, the active document is set to null.
        /// The <see cref="ActiveDocumentChanged"/> event is raised after removal. The caller
        /// is responsible for prompting the user to save unsaved changes before calling this method.
        /// </remarks>
        public void Close(CanvasDocument doc)
        {
            if (ActiveDocument == doc)
                ActiveDocument = null;

            Documents.Remove(doc);
            ActiveDocumentChanged?.Invoke();
        }
    }
}