using System;
using FluentIcons.Common;
using PixlPunkt.Core.Document;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// A move of the baseline or cap-height guide. Both are uniform across every glyph, so one
    /// drag changes the whole font and undoes as one step.
    /// </summary>
    public sealed class FontGuideItem : IHistoryItem
    {
        private readonly CanvasDocument _doc;
        private readonly int _beforeTopline, _beforeBaseline;
        private readonly int _afterTopline, _afterBaseline;

        public Icon HistoryIcon { get; set; } = Icon.TextBaseline;
        public string Description { get; }

        public FontGuideItem(CanvasDocument doc, int beforeTopline, int beforeBaseline, string description)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _beforeTopline = beforeTopline;
            _beforeBaseline = beforeBaseline;
            _afterTopline = doc.FontState.ToplineY;
            _afterBaseline = doc.FontState.BaselineY;
            Description = description;
        }

        /// <summary>False when the drag ended where it started, so nothing needs pushing.</summary>
        public bool HasChange => _beforeTopline != _afterTopline || _beforeBaseline != _afterBaseline;

        public void Undo() => Apply(_beforeTopline, _beforeBaseline);
        public void Redo() => Apply(_afterTopline, _afterBaseline);

        private void Apply(int topline, int baseline)
        {
            _doc.FontState.SetGuides(topline, baseline, _doc.TileSize.Height);
            _doc.RaiseFontChanged();
        }
    }
}
