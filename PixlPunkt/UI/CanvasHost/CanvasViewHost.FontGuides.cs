using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Rendering;
using PixlPunkt.Core.Tools;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// The baseline and cap-height guides of a font document. They are uniform across every glyph,
    /// which is what stops a pixel font wobbling, so they are drawn once per row of cells and a
    /// drag on any one of them moves the whole font.
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        private enum FontGuideKind { None, Topline, Baseline }

        private enum FontSpacingKind { None, Origin, Advance }

        private static readonly Color BaselineColor = Color.FromArgb(225, 255, 170, 60);
        private static readonly Color ToplineColor = Color.FromArgb(170, 110, 190, 255);

        private static readonly Color OriginColor = Color.FromArgb(235, 120, 225, 140);
        private static readonly Color AdvanceColor = Color.FromArgb(235, 230, 130, 205);
        private static readonly Color OriginAutoColor = Color.FromArgb(120, 120, 225, 140);
        private static readonly Color AdvanceAutoColor = Color.FromArgb(120, 230, 130, 205);

        private static readonly Color AdvanceBandColor = Color.FromArgb(30, 150, 210, 255);
        private static readonly Color EmBoxColor = Color.FromArgb(95, 170, 180, 200);

        /// <summary>
        /// The cell whose spacing is on show. It follows the pointer and then stays there, so the
        /// posts belong to one glyph at a time instead of every glyph on the sheet at once.
        /// </summary>
        private int _fontFocusCell = -1;

        private FontSpacingKind _fontSpacingDrag = FontSpacingKind.None;
        private int _fontSpacingCodepoint;
        private int _fontSpacingBeforeOrigin, _fontSpacingBeforeAdvance;
        private bool _fontSpacingBeforeAutoFit;

        private FontGuideKind _fontGuideDrag = FontGuideKind.None;
        private int _fontGuideDragRow;
        private int _fontGuideBeforeTopline, _fontGuideBeforeBaseline;

        private bool IsFontDocument => Document.FontState.HasState;

        /// <summary>
        /// Whether the guides and spacing posts will accept a drag. Armed from the glyphs panel's
        /// metrics tool and off by default, because on a font sheet you are forever drawing right
        /// on top of the baseline and a brush must never lose a click to a guide. Borrowing the
        /// selection tool for this was the earlier arrangement; a switch of its own is clearer and
        /// leaves every drawing tool free.
        /// </summary>
        private bool FontMetricsInteractive => IsFontDocument && _fontMetricsEditing;

        private bool _fontMetricsEditing;

        /// <summary>Raised whenever metrics editing turns on or off, however that came about.</summary>
        public event Action<bool>? FontMetricsEditingChanged;

        /// <summary>
        /// Arms or disarms metrics editing. Arming stands the drawing tools down so the rail shows
        /// nothing selected and a stray click cannot paint; disarming gives them back. A drag in
        /// progress is ended first, since disarming mid-drag would strand it.
        /// </summary>
        public void SetFontMetricsEditing(bool editing)
        {
            if (_fontMetricsEditing == editing) return;
            _fontMetricsEditing = editing;

            if (!editing)
            {
                _fontGuideDrag = FontGuideKind.None;
                _fontSpacingDrag = FontSpacingKind.None;
            }

            _toolState?.SuspendTools(editing);
            InvalidateMainCanvas();
            FontMetricsEditingChanged?.Invoke(editing);
        }

        /// <summary>
        /// Picking a tool in the rail clears the suspension, so the metrics tool has to notice and
        /// disarm rather than sit armed with nothing behind it.
        /// </summary>
        private void FontMetrics_OnActiveToolChanged(string toolId)
        {
            if (!_fontMetricsEditing) return;
            if (toolId == ToolIds.None) return;

            _fontMetricsEditing = false;
            _fontGuideDrag = FontGuideKind.None;
            _fontSpacingDrag = FontSpacingKind.None;
            InvalidateMainCanvas();
            FontMetricsEditingChanged?.Invoke(false);
        }

        /// <summary>Guides read as live when they can be grabbed and as annotation when they cannot.</summary>
        private Color MetricsColor(Color c) => FontMetricsInteractive ? c : FadeOut(c);

        // ─────────────────────────────────────────────────────────────────────
        // DRAWING
        // ─────────────────────────────────────────────────────────────────────

        private void DrawFontGuides(ICanvasRenderer renderer, Rect dest)
        {
            if (!IsFontDocument) return;

            var st = Document.FontState;
            int emH = Math.Max(1, Document.TileSize.Height);
            int rows = Math.Max(1, Document.TileCounts.Height);
            float s = (float)_zoom.Scale;
            float x0 = (float)dest.X;
            float x1 = (float)(dest.X + dest.Width);

            for (int r = 0; r < rows; r++)
            {
                int cellTop = r * emH;
                DrawDashedRow(renderer, x0, x1, (float)(dest.Y + (cellTop + st.ToplineY) * s), MetricsColor(ToplineColor));
                DrawDashedRow(renderer, x0, x1, (float)(dest.Y + (cellTop + st.BaselineY) * s), MetricsColor(BaselineColor));
            }

            DrawEmBoxes(renderer, dest);
            DrawGlyphSpacing(renderer, dest);
        }

        /// <summary>
        /// The em box of each cell, drawn only when the cell is larger than it. The difference is
        /// the overhang room, and without an outline there is no way to see where the design size
        /// actually ends.
        /// </summary>
        private void DrawEmBoxes(ICanvasRenderer renderer, Rect dest)
        {
            var em = FontMetricsOps.EmBox(Document);
            int cellW = Math.Max(1, Document.TileSize.Width);
            int cellH = Math.Max(1, Document.TileSize.Height);
            if (em.X == 0 && em.Y == 0 && em.Width == cellW && em.Height == cellH) return;

            float s = (float)_zoom.Scale;
            int cols = Math.Max(1, Document.TileCounts.Width);
            int rows = Math.Max(1, Document.TileCounts.Height);
            var color = MetricsColor(EmBoxColor);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = (float)(dest.X + (c * cellW + em.X) * s);
                    float y = (float)(dest.Y + (r * cellH + em.Y) * s);
                    renderer.DrawRectangle(x, y, em.Width * s, em.Height * s, color, 1f);
                }
            }
        }

        /// <summary>The cell containing a document point, or -1 when the point is off the sheet.</summary>
        private int CellAt(int docX, int docY)
        {
            int cols = Math.Max(1, Document.TileCounts.Width);
            int rows = Math.Max(1, Document.TileCounts.Height);
            int col = (int)Math.Floor(docX / (double)Math.Max(1, Document.TileSize.Width));
            int row = (int)Math.Floor(docY / (double)Math.Max(1, Document.TileSize.Height));
            if (col < 0 || col >= cols || row < 0 || row >= rows) return -1;
            return row * cols + col;
        }

        /// <summary>
        /// Takes a click inside a glyph's cell as choosing that glyph to work on.
        /// </summary>
        /// <remarks>
        /// Deliberately a click and not a hover. Following the pointer meant the glyph under study
        /// changed on the way to the zoom button, which is the opposite of what a working view is
        /// for. The click is only noted; it is never consumed, so it still paints.
        /// </remarks>
        private void FontGuides_NotePointerPressed(PointerRoutedEventArgs e)
        {
            if (!IsFontDocument) return;
            if (!e.GetCurrentPoint(_mainCanvas).Properties.IsLeftButtonPressed) return;

            var (docX, docY) = ViewToDoc(e.GetCurrentPoint(_mainCanvas).Position);
            int cell = CellAt(docX, docY);
            if (cell < 0 || cell == _fontFocusCell) return;

            SetFontFocusCell(cell);
        }

        /// <summary>
        /// The character whose cell currently has focus, or -1. The glyph strip follows this, so
        /// moving the pointer across the sheet walks the strip along with it.
        /// </summary>
        public int FontFocusedCodepoint { get; private set; } = -1;

        /// <summary>Raised when the focused glyph changes, by pointer or by a pick in the panel.</summary>
        public event Action<int>? FontGlyphFocusedChanged;

        /// <summary>Moves the focus to a cell and tells anything that follows it.</summary>
        private void SetFontFocusCell(int cellIndex)
        {
            _fontFocusCell = cellIndex;
            InvalidateMainCanvas();

            int codepoint = Document.FontState.CodepointAtCell(cellIndex);
            if (codepoint == FontFocusedCodepoint) return;

            FontFocusedCodepoint = codepoint;
            FontGlyphFocusedChanged?.Invoke(codepoint);
        }

        /// <summary>
        /// Spacing posts for the glyph in focus and, faintly, the one either side of it. Showing
        /// every glyph's posts at once makes a sheet unreadable: you cannot tell whose advance a
        /// post belongs to, least of all when an advance runs past its own cell.
        /// </summary>
        private void DrawGlyphSpacing(ICanvasRenderer renderer, Rect dest)
        {
            var st = Document.FontState;
            if (st.Glyphs.Count == 0 || _fontFocusCell < 0) return;

            // Reshaping the font, or undoing one, can leave the focus pointing past the sheet.
            if (_fontFocusCell >= FontMetricsOps.CellCount(Document)) return;

            int cols = Math.Max(1, Document.TileCounts.Width);
            int focusCol = _fontFocusCell % cols;

            if (focusCol > 0) DrawCellSpacing(renderer, dest, _fontFocusCell - 1, focused: false);
            if (focusCol < cols - 1) DrawCellSpacing(renderer, dest, _fontFocusCell + 1, focused: false);
            DrawCellSpacing(renderer, dest, _fontFocusCell, focused: true);
        }

        private void DrawCellSpacing(ICanvasRenderer renderer, Rect dest, int cellIndex, bool focused)
        {
            var st = Document.FontState;
            int codepoint = st.CodepointAtCell(cellIndex);
            if (codepoint < 0 || !st.TryGet(codepoint, out var g)) return;

            var cell = FontMetricsOps.GetCellRect(Document, cellIndex);
            if (cell.Y >= Document.PixelHeight) return;

            var surf = Document.Surface;
            var (originX, advance) =
                FontMetricsOps.ResolveMetrics(Document, codepoint, surf.Pixels, surf.Width, surf.Height);

            float s = (float)_zoom.Scale;
            int emH = Math.Max(1, Document.TileSize.Height);
            float top = (float)(dest.Y + cell.Y * s);
            float bottom = (float)(dest.Y + (cell.Y + emH) * s);
            float ox = (float)(dest.X + (cell.X + originX) * s);
            float ax = (float)(dest.X + (cell.X + originX + advance) * s);

            // The band says "this glyph occupies this width", which two bare lines never did.
            if (focused && ax > ox)
                renderer.FillRectangle(ox, top, ax - ox, bottom - top, AdvanceBandColor);

            var originColor = MetricsColor(focused ? (g.AutoFit ? OriginAutoColor : OriginColor) : FadeOut(OriginAutoColor));
            var advanceColor = MetricsColor(focused ? (g.AutoFit ? AdvanceAutoColor : AdvanceColor) : FadeOut(AdvanceAutoColor));

            DrawDashedColumn(renderer, ox, top, bottom, originColor);
            DrawDashedColumn(renderer, ax, top, bottom, advanceColor);
        }

        private static Color FadeOut(Color c) => Color.FromArgb((byte)(c.A / 2), c.R, c.G, c.B);

        private static void DrawDashedColumn(ICanvasRenderer renderer, float x, float y0, float y1, Color color)
        {
            const float Dash = 3f, Gap = 3f;
            for (float y = y0; y < y1; y += Dash + Gap)
                renderer.DrawLine(x, y, x, Math.Min(y + Dash, y1), color, 1f);
        }

        private static void DrawDashedRow(ICanvasRenderer renderer, float x0, float x1, float y, Color color)
        {
            const float Dash = 4f, Gap = 4f;
            for (float x = x0; x < x1; x += Dash + Gap)
                renderer.DrawLine(x, y, Math.Min(x + Dash, x1), y, color, 1f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DRAGGING
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Which guide is under a document Y, and in which row of cells. Neighbouring rows are
        /// checked too, because a baseline sitting on the cell's bottom edge is also the next
        /// row's top edge.
        /// </summary>
        private (FontGuideKind Kind, int Row) HitTestFontGuide(int docY)
        {
            if (!IsFontDocument) return (FontGuideKind.None, 0);

            var st = Document.FontState;
            int emH = Math.Max(1, Document.TileSize.Height);
            int rows = Math.Max(1, Document.TileCounts.Height);
            int tolerance = Math.Max(1, (int)Math.Round(4 / Math.Max(0.0001, _zoom.Scale)));

            int centreRow = (int)Math.Floor(docY / (double)emH);
            var best = (Kind: FontGuideKind.None, Row: 0, Distance: int.MaxValue);

            for (int r = centreRow - 1; r <= centreRow + 1; r++)
            {
                if (r < 0 || r >= rows) continue;
                int cellTop = r * emH;

                int dBase = Math.Abs(docY - (cellTop + st.BaselineY));
                if (dBase <= tolerance && dBase < best.Distance)
                    best = (FontGuideKind.Baseline, r, dBase);

                int dTop = Math.Abs(docY - (cellTop + st.ToplineY));
                if (dTop <= tolerance && dTop < best.Distance)
                    best = (FontGuideKind.Topline, r, dTop);
            }

            return (best.Kind, best.Row);
        }

        private bool FontGuides_TryHandlePointerPressed(PointerRoutedEventArgs e)
        {
            if (!FontMetricsInteractive) return false;

            var pt = e.GetCurrentPoint(_mainCanvas);
            if (!pt.Properties.IsLeftButtonPressed) return false;

            var (kind, row) = HitTestFontGuide(ViewYToDocY(pt.Position.Y));
            if (kind == FontGuideKind.None) return false;

            var st = Document.FontState;
            _fontGuideDrag = kind;
            _fontGuideDragRow = row;
            _fontGuideBeforeTopline = st.ToplineY;
            _fontGuideBeforeBaseline = st.BaselineY;
            _mainCanvas.CapturePointer(e.Pointer);
            return true;
        }

        private void FontGuides_HandlePointerMoved(PointerRoutedEventArgs e)
        {
            if (_fontGuideDrag == FontGuideKind.None) return;

            var st = Document.FontState;
            int emH = Math.Max(1, Document.TileSize.Height);
            int docY = ViewYToDocY(e.GetCurrentPoint(_mainCanvas).Position.Y);
            int within = docY - _fontGuideDragRow * emH;

            bool changed = _fontGuideDrag == FontGuideKind.Baseline
                ? st.SetGuides(st.ToplineY, within, emH)
                : st.SetGuides(within, st.BaselineY, emH);

            if (changed)
            {
                Document.RaiseFontChanged();
                InvalidateMainCanvas();
            }
        }

        private void FontGuides_HandlePointerReleased(PointerRoutedEventArgs e)
        {
            if (_fontGuideDrag == FontGuideKind.None) return;
            _fontGuideDrag = FontGuideKind.None;
            _mainCanvas.ReleasePointerCaptures();

            var item = new FontGuideItem(Document, _fontGuideBeforeTopline, _fontGuideBeforeBaseline, "Move Font Guide");
            if (item.HasChange) PushHistoryItem(item);
        }

        /// <summary>
        /// Which spacing post is under a document point. Neighbouring columns are checked because
        /// an advance wider than the em puts the post outside its own cell.
        /// </summary>
        private (FontSpacingKind Kind, int Codepoint) HitTestGlyphSpacing(int docX, int docY)
        {
            if (!IsFontDocument) return (FontSpacingKind.None, 0);

            var st = Document.FontState;
            int cols = Math.Max(1, Document.TileCounts.Width);
            int rows = Math.Max(1, Document.TileCounts.Height);
            int emW = Math.Max(1, Document.TileSize.Width);
            int emH = Math.Max(1, Document.TileSize.Height);

            int row = (int)Math.Floor(docY / (double)emH);
            if (row < 0 || row >= rows) return (FontSpacingKind.None, 0);

            int centreCol = (int)Math.Floor(docX / (double)emW);
            int tolerance = Math.Max(1, (int)Math.Round(4 / Math.Max(0.0001, _zoom.Scale)));
            var best = (Kind: FontSpacingKind.None, Codepoint: 0, Distance: int.MaxValue);

            for (int col = centreCol - 1; col <= centreCol + 1; col++)
            {
                if (col < 0 || col >= cols) continue;
                int codepoint = st.CodepointAtCell(row * cols + col);
                if (codepoint < 0) continue;

                var (originX, advance) = FontMetricsOps.ResolveMetrics(Document, codepoint);
                int local = docX - col * emW;

                int dOrigin = Math.Abs(local - originX);
                if (dOrigin <= tolerance && dOrigin < best.Distance)
                    best = (FontSpacingKind.Origin, codepoint, dOrigin);

                int dAdvance = Math.Abs(local - (originX + advance));
                if (dAdvance <= tolerance && dAdvance < best.Distance)
                    best = (FontSpacingKind.Advance, codepoint, dAdvance);
            }

            return (best.Kind, best.Codepoint);
        }

        private bool FontSpacing_TryHandlePointerPressed(PointerRoutedEventArgs e)
        {
            if (!FontMetricsInteractive) return false;

            var pt = e.GetCurrentPoint(_mainCanvas);
            if (!pt.Properties.IsLeftButtonPressed) return false;

            var (docX, docY) = ViewToDoc(pt.Position);
            var (kind, codepoint) = HitTestGlyphSpacing(docX, docY);
            if (kind == FontSpacingKind.None) return false;

            var g = Document.FontState.GetOrAdd(codepoint);
            _fontSpacingBeforeOrigin = g.OriginX;
            _fontSpacingBeforeAdvance = g.Advance;
            _fontSpacingBeforeAutoFit = g.AutoFit;

            // Seed the stored values from what is on screen so the post does not jump when a
            // glyph stops following auto-fit.
            if (g.AutoFit)
            {
                var (originX, advance) = FontMetricsOps.ResolveMetrics(Document, codepoint);
                g.OriginX = originX;
                g.Advance = advance;
                g.AutoFit = false;
            }

            _fontSpacingDrag = kind;
            _fontSpacingCodepoint = codepoint;
            _mainCanvas.CapturePointer(e.Pointer);
            return true;
        }

        private void FontSpacing_HandlePointerMoved(PointerRoutedEventArgs e)
        {
            if (_fontSpacingDrag == FontSpacingKind.None) return;
            if (!Document.FontState.TryGet(_fontSpacingCodepoint, out var g) || g.CellIndex < 0) return;

            var cell = FontMetricsOps.GetCellRect(Document, g.CellIndex);
            var (docX, _) = ViewToDoc(e.GetCurrentPoint(_mainCanvas).Position);

            // Both posts stop at the edges of the glyph's own cell, overhang room included. Past
            // that the pen would move further than the cell it came from and the next glyph would
            // land on ink belonging to this one.
            int local = Math.Clamp(docX - cell.X, 0, cell.Width);

            if (_fontSpacingDrag == FontSpacingKind.Origin)
            {
                // Hold the advance post still, so dragging the origin changes the gap rather than
                // sliding the whole glyph: what a pixel artist means by "less space on the left".
                int right = g.OriginX + g.Advance;
                g.OriginX = local;
                g.Advance = Math.Max(0, right - local);
            }
            else
            {
                g.Advance = Math.Max(0, local - g.OriginX);
            }

            (g.OriginX, g.Advance) = FontMetricsOps.ClampToCell(Document, g.OriginX, g.Advance);

            Document.RaiseFontChanged();
            InvalidateMainCanvas();
        }

        private void FontSpacing_HandlePointerReleased(PointerRoutedEventArgs e)
        {
            if (_fontSpacingDrag == FontSpacingKind.None) return;
            _fontSpacingDrag = FontSpacingKind.None;
            _mainCanvas.ReleasePointerCaptures();

            var item = new FontGlyphMetricsItem(
                Document, _fontSpacingCodepoint,
                _fontSpacingBeforeOrigin, _fontSpacingBeforeAdvance, _fontSpacingBeforeAutoFit,
                "Space Glyph");
            if (item.HasChange) PushHistoryItem(item);
        }

        /// <summary>
        /// Brings a glyph's cell to the middle of the view and makes it the focused cell, so its
        /// spacing posts are the ones drawn solid. This is what the glyphs panel calls on a pick.
        /// </summary>
        public void FontFocusGlyph(int codepoint)
        {
            if (!IsFontDocument) return;
            if (!Document.FontState.TryGet(codepoint, out var g) || g.CellIndex < 0) return;

            var cell = FontMetricsOps.GetCellRect(Document, g.CellIndex);
            CenterOnDocumentPoint(cell.X + cell.Width / 2.0, cell.Y + cell.Height / 2.0);
            SetFontFocusCell(g.CellIndex);
        }

        /// <summary>
        /// Selects the first character of the font if nothing is selected yet, without moving the
        /// view. Now that focus follows clicks rather than the pointer, a freshly opened font would
        /// otherwise show no spacing posts and an empty strip until the first click.
        /// </summary>
        public void FontFocusDefaultGlyph()
        {
            if (!IsFontDocument || _fontFocusCell >= 0) return;

            var glyphs = FontGlyphOps.Summarize(Document);
            if (glyphs.Count == 0) return;

            SetFontFocusCell(glyphs[0].CellIndex);
        }

        /// <summary>Puts a glyph back on auto-fit as one undo step.</summary>
        public void FontSpacing_ResetToAuto(int codepoint)
        {
            if (!IsFontDocument) return;
            if (!Document.FontState.TryGet(codepoint, out var g) || g.AutoFit) return;

            int beforeOrigin = g.OriginX, beforeAdvance = g.Advance;
            g.AutoFit = true;
            Document.RaiseFontChanged();

            var item = new FontGlyphMetricsItem(Document, codepoint, beforeOrigin, beforeAdvance, false, "Auto-Fit Glyph");
            if (item.HasChange) PushHistoryItem(item);
            InvalidateMainCanvas();
        }

        /// <summary>Cursor feedback so the guides read as draggable. False when nothing is under the pointer.</summary>
        private bool FontGuides_TrySetHoverCursor(Point viewPos)
        {
            if (!FontMetricsInteractive) return false;
            if (_fontGuideDrag != FontGuideKind.None || _fontSpacingDrag != FontSpacingKind.None) return false;

            if (HitTestFontGuide(ViewYToDocY(viewPos.Y)).Kind != FontGuideKind.None)
            {
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
                return true;
            }

            var (docX, docY) = ViewToDoc(viewPos);
            if (HitTestGlyphSpacing(docX, docY).Kind != FontSpacingKind.None)
            {
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
                return true;
            }

            return false;
        }
    }
}
