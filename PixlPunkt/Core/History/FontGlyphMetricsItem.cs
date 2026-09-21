using System;
using FluentIcons.Common;
using PixlPunkt.Core.Document;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// A change to one glyph's spacing: where its origin sits in the cell, how far it advances,
    /// and whether it is still following auto-fit. One drag is one step.
    /// </summary>
    public sealed class FontGlyphMetricsItem : IHistoryItem
    {
        private readonly CanvasDocument _doc;
        private readonly int _codepoint;
        private readonly int _beforeOrigin, _beforeAdvance;
        private readonly bool _beforeAutoFit;
        private readonly int _afterOrigin, _afterAdvance;
        private readonly bool _afterAutoFit;

        public Icon HistoryIcon { get; set; } = Icon.TextT;
        public string Description { get; }

        public FontGlyphMetricsItem(
            CanvasDocument doc, int codepoint,
            int beforeOrigin, int beforeAdvance, bool beforeAutoFit,
            string description)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _codepoint = codepoint;
            _beforeOrigin = beforeOrigin;
            _beforeAdvance = beforeAdvance;
            _beforeAutoFit = beforeAutoFit;

            var g = doc.FontState.GetOrAdd(codepoint);
            _afterOrigin = g.OriginX;
            _afterAdvance = g.Advance;
            _afterAutoFit = g.AutoFit;

            Description = description;
        }

        public bool HasChange =>
            _beforeOrigin != _afterOrigin ||
            _beforeAdvance != _afterAdvance ||
            _beforeAutoFit != _afterAutoFit;

        public void Undo() => Apply(_beforeOrigin, _beforeAdvance, _beforeAutoFit);
        public void Redo() => Apply(_afterOrigin, _afterAdvance, _afterAutoFit);

        private void Apply(int originX, int advance, bool autoFit)
        {
            var g = _doc.FontState.GetOrAdd(_codepoint);
            g.OriginX = originX;
            g.Advance = advance;
            g.AutoFit = autoFit;
            _doc.RaiseFontChanged();
        }
    }
}
