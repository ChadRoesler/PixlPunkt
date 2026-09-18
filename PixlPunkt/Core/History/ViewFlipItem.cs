using System;
using FluentIcons.Common;
using PixlPunkt.Core.Document;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// A non-destructive canvas flip (the view is mirrored; pixels are untouched), like
    /// Krita's mirror view. Undoable and recorded for the timelapse; does not dirty the document
    /// and is not saved with it.
    /// </summary>
    public sealed class ViewFlipItem : IViewOnlyHistoryItem
    {
        private readonly CanvasDocument _doc;
        private readonly bool _horizontal;

        public Icon HistoryIcon { get; set; }
        public string Description => _horizontal ? "Flip View Horizontally" : "Flip View Vertically";

        public ViewFlipItem(CanvasDocument doc, bool horizontal)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _horizontal = horizontal;
            HistoryIcon = horizontal ? Icon.FlipHorizontal : Icon.FlipVertical;
        }

        // A flip is its own inverse.
        public void Undo() => _doc.ToggleViewFlip(_horizontal);
        public void Redo() => _doc.ToggleViewFlip(_horizontal);
    }
}
