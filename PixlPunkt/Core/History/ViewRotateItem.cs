using System;
using FluentIcons.Common;
using PixlPunkt.Core.Document;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// A non-destructive canvas rotation (the view turns; pixels are untouched). One item per
    /// drag or reset. Undoable, never dirties the document, not saved, and skipped by the
    /// timelapse because a rotated frame would change size.
    /// </summary>
    public sealed class ViewRotateItem : IViewOnlyHistoryItem
    {
        private readonly CanvasDocument _doc;
        private readonly double _before, _after;

        public Icon HistoryIcon { get; set; } = Icon.ArrowRotateClockwise;
        public string Description => Math.Abs(_after) < 1e-9 ? "Reset View Rotation" : "Rotate View";

        public ViewRotateItem(CanvasDocument doc, double beforeDeg, double afterDeg)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _before = beforeDeg; _after = afterDeg;
        }

        public bool HasChange => Math.Abs(_before - _after) > 1e-6;
        public void Undo() => _doc.SetViewRotation(_before);
        public void Redo() => _doc.SetViewRotation(_after);
    }
}
