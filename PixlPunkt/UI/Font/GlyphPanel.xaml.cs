using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Document;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.UI;

namespace PixlPunkt.UI.Font
{
    /// <summary>
    /// The character set of a font document: every mapped glyph in codepoint order, which ones are
    /// still blank, and which have had their spacing pinned by hand. Selecting one focuses its cell
    /// on the canvas, so this is how you navigate a font rather than hunting the sheet by eye.
    /// </summary>
    /// <remarks>
    /// Cells are drawn immediate-mode into a Skia canvas from the document surface, the same way the
    /// tile panel does it, because the glyph sheet is the document rather than a separate image.
    /// </remarks>
    public sealed partial class GlyphPanel : UserControl, INotifyPropertyChanged
    {
        private CanvasDocument? _document;
        private double _zoomLevel = 3.0;
        private int _selectedCodepoint = -1;
        private readonly Dictionary<int, GlyphSummary> _summaries = new();

        // Both painting and dragging a spacing post raise a change per pointer move, so refreshes
        // are collapsed onto a timer rather than run on each one.
        private DispatcherQueueTimer? _refreshTimer;
        private bool _refreshPending;

        public GlyphPanel()
        {
            InitializeComponent();
            GlyphGrid.ItemsSource = Codepoints;

            Unloaded += (_, __) => _refreshTimer?.Stop();
        }

        /// <summary>Mapped characters in codepoint order. The item DataContext is the codepoint.</summary>
        public ObservableCollection<int> Codepoints { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Raised when a glyph is picked, so the canvas can bring that cell into view.</summary>
        public event Action<int>? GlyphSelected;

        /// <summary>Raised when the panel is asked to hand a glyph's spacing back to auto-fit.</summary>
        public event Action<int>? ResetSpacingRequested;

        /// <summary>
        /// Raised when the metrics tool is switched on or off. While it is off the guides and
        /// spacing posts on the canvas are inert, so they cannot steal a click meant for a brush.
        /// </summary>
        public event Action<bool>? MetricsEditingChanged;

        /// <summary>Whether the metrics tool is currently armed.</summary>
        public bool IsMetricsEditing => MetricsToolButton?.IsChecked == true;

        /// <summary>Whether the bound document is a font at all; the panel is pointless otherwise.</summary>
        public bool HasFont => _document?.FontState.HasState == true;

        public double GlyphCellSize
        {
            get
            {
                int baseSize = _document?.TileSize.Width ?? 16;
                return Math.Max(28, baseSize * _zoomLevel);
            }
        }

        public int SelectedCodepoint
        {
            get => _selectedCodepoint;
            set
            {
                if (_selectedCodepoint == value) return;
                _selectedCodepoint = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCodepoint)));
                RefreshSelectionRings();
                if (value >= 0) GlyphSelected?.Invoke(value);
            }
        }

        /// <summary>
        /// Points the panel at a document, or at nothing. The main window pushes this in on every
        /// tab change, so the panel never reaches for the active document itself.
        /// </summary>
        public void Bind(CanvasDocument? document)
        {
            if (_document is not null)
            {
                _document.FontChanged -= OnFontChanged;
                _document.DocumentModified -= OnDocumentModified;
            }

            _document = document;

            if (_document is not null)
            {
                _document.FontChanged += OnFontChanged;
                _document.DocumentModified += OnDocumentModified;
            }

            _selectedCodepoint = -1;
            RefreshGlyphList();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GlyphCellSize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFont)));
        }

        /// <summary>
        /// Re-reads the font and brings the panel in line with it.
        /// </summary>
        /// <remarks>
        /// The item list is replaced only when the set of characters genuinely changed. Dragging a
        /// spacing post raises a font change on every pointer move, and clearing the collection
        /// there would tear down and rebuild a container per glyph each time, which stalls the
        /// canvas mid-drag. Metrics moves therefore touch nothing but the cells' own chrome.
        /// </remarks>
        public void RefreshGlyphList()
        {
            if (StatusText is null) return;

            if (_document is null || !_document.FontState.HasState)
            {
                _summaries.Clear();
                if (Codepoints.Count > 0) Codepoints.Clear();
                StatusText.Text = "Not a font document.";
                return;
            }

            var glyphs = FontGlyphOps.Summarize(_document);

            _summaries.Clear();
            foreach (var g in glyphs)
                _summaries[g.Codepoint] = g;

            if (!MatchesCurrentList(glyphs))
            {
                Codepoints.Clear();
                foreach (var g in glyphs)
                    Codepoints.Add(g.Codepoint);
            }

            int undrawn = FontGlyphOps.UndrawnCount(glyphs);
            StatusText.Text = undrawn == 0
                ? $"{glyphs.Count} glyphs, all drawn."
                : $"{glyphs.Count} glyphs, {undrawn} still blank.";

            RefreshAllCells();
        }

        /// <summary>Whether the displayed characters already are the font's, in the same order.</summary>
        private bool MatchesCurrentList(List<GlyphSummary> glyphs)
        {
            if (Codepoints.Count != glyphs.Count) return false;
            for (int i = 0; i < glyphs.Count; i++)
                if (Codepoints[i] != glyphs[i].Codepoint) return false;
            return true;
        }

        private void OnFontChanged() => DispatcherQueue.TryEnqueue(QueueRefresh);

        private void OnDocumentModified() => DispatcherQueue.TryEnqueue(QueueRefresh);

        /// <summary>
        /// Asks for a redraw soon rather than now. Repeated calls inside the window collapse into
        /// one, which is what keeps painting smooth with a hundred cells on screen.
        /// </summary>
        private void QueueRefresh()
        {
            _refreshPending = true;

            if (_refreshTimer is null)
            {
                var queue = DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
                if (queue is null)
                {
                    RefreshGlyphList();
                    _refreshPending = false;
                    return;
                }

                _refreshTimer = queue.CreateTimer();
                _refreshTimer.Interval = TimeSpan.FromMilliseconds(120);
                _refreshTimer.IsRepeating = true;
                _refreshTimer.Tick += (_, __) =>
                {
                    if (!_refreshPending)
                    {
                        _refreshTimer.Stop();
                        return;
                    }

                    _refreshPending = false;
                    RefreshGlyphList();
                };
            }

            _refreshTimer.Start();
        }

        // ── painting ────────────────────────────────────────────────────

        private void GlyphCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            if (sender is not SKXamlCanvas skCanvas) return;

            var canvas = e.Surface.Canvas;
            float w = (float)skCanvas.ActualWidth;
            float h = (float)skCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // No transparency checkerboard here. The cell sits on a solid panel colour from XAML,
            // because a chequered square next to a one-character label makes the label unreadable.
            canvas.Clear(SKColors.Transparent);

            // The cell's DataContext is the codepoint. It normally reaches the canvas by
            // inheritance, but the tile panel reads it off the parent, so fall back that way too.
            if (ResolveCodepoint(skCanvas) is not { } cp || _document is null) return;
            if (!_summaries.TryGetValue(cp, out var summary)) return;

            var cell = FontMetricsOps.GetCellRect(_document, summary.CellIndex);
            if (cell.Width <= 0 || cell.Height <= 0) return;

            float scale = Math.Min(w / cell.Width, h / cell.Height);
            float destW = cell.Width * scale, destH = cell.Height * scale;
            float destX = (w - destW) / 2, destY = (h - destH) / 2;

            DrawCellPixels(canvas, cell, new SKRect(destX, destY, destX + destW, destY + destH));
            DrawBaseline(canvas, cell, destX, destY, destW, scale);
        }

        /// <summary>The codepoint a cell stands for, from the canvas or whatever encloses it.</summary>
        private static int? ResolveCodepoint(FrameworkElement element)
        {
            if (element.DataContext is int direct) return direct;

            for (var parent = element.Parent as FrameworkElement; parent is not null;
                 parent = parent.Parent as FrameworkElement)
            {
                if (parent.DataContext is int inherited) return inherited;
            }

            return null;
        }

        /// <summary>Copies the cell out of the document surface and blits it, nearest-neighbour.</summary>
        private void DrawCellPixels(SKCanvas canvas, Windows.Graphics.RectInt32 cell, SKRect dest)
        {
            var surf = _document!.Surface;
            int stride = cell.Width * 4;
            var buffer = new byte[stride * cell.Height];

            for (int row = 0; row < cell.Height; row++)
            {
                int srcY = cell.Y + row;
                if (srcY < 0 || srcY >= surf.Height) continue;
                int srcOffset = (srcY * surf.Width + cell.X) * 4;
                if (srcOffset < 0 || srcOffset + stride > surf.Pixels.Length) continue;
                Buffer.BlockCopy(surf.Pixels, srcOffset, buffer, row * stride, stride);
            }

            using var bitmap = new SKBitmap(cell.Width, cell.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            Marshal.Copy(buffer, 0, bitmap.GetPixels(), buffer.Length);

            using var paint = new SKPaint { IsAntialias = false };
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, cell.Width, cell.Height), dest, paint);
        }

        /// <summary>
        /// A hairline where the baseline falls. Without it a thumbnail says nothing about whether a
        /// glyph sits on the line, which is most of what you are checking when you scan the set.
        /// </summary>
        private void DrawBaseline(SKCanvas canvas, Windows.Graphics.RectInt32 cell,
                                  float destX, float destY, float destW, float scale)
        {
            int baseline = _document!.FontState.BaselineY;
            if (baseline <= 0 || baseline >= cell.Height) return;

            float y = destY + baseline * scale;
            using var paint = new SKPaint
            {
                Color = new SKColor(255, 140, 90, 110),
                StrokeWidth = 1,
                IsAntialias = false,
            };
            canvas.DrawLine(destX, y, destX + destW, y, paint);
        }

        // ── cell containers ─────────────────────────────────────────────

        private void GlyphCell_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid grid || grid.DataContext is not int cp) return;
            UpdateCellChrome(grid, cp);
            (grid.FindName("GlyphCanvas") as SKXamlCanvas)?.Invalidate();
        }

        private void GlyphCell_Unloaded(object sender, RoutedEventArgs e) { }

        private void GlyphCell_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not int cp) return;
            SelectedCodepoint = cp;
        }

        /// <summary>
        /// Label, selection ring and the dimming that marks a blank glyph. A pinned glyph gets a dot
        /// after its label, since "why is this one spaced oddly" is otherwise invisible here.
        /// </summary>
        private void UpdateCellChrome(Grid grid, int codepoint)
        {
            if (grid.FindName("SelectionRing") is Border ring)
                ring.Visibility = codepoint == _selectedCodepoint ? Visibility.Visible : Visibility.Collapsed;

            bool known = _summaries.TryGetValue(codepoint, out var summary);

            if (grid.FindName("GlyphLabel") is TextBlock label)
            {
                string mark = known && !summary.AutoFit ? " ·" : string.Empty;
                label.Text = FontGlyphOps.LabelFor(codepoint) + mark;
                label.Opacity = known && summary.HasInk ? 1.0 : 0.4;
            }

            // Apostrophe, backtick and full stop are barely distinguishable at any size, so the
            // codepoint is one hover away rather than something to squint at.
            string tip = $"{FontGlyphOps.LabelFor(codepoint)}  U+{codepoint:X4}";
            if (known && !summary.AutoFit) tip += "  (spacing set by hand)";
            ToolTipService.SetToolTip(grid, tip);
        }

        private void RefreshSelectionRings() => ForEachCell(UpdateCellChrome);

        private void RefreshAllCells() => ForEachCell((grid, cp) =>
        {
            UpdateCellChrome(grid, cp);
            (grid.FindName("GlyphCanvas") as SKXamlCanvas)?.Invalidate();
        });

        /// <summary>Walks realised containers. ItemsControl wraps each item in a ContentPresenter.</summary>
        private void ForEachCell(Action<Grid, int> action)
        {
            foreach (var item in GlyphGrid.Items)
            {
                if (item is not int cp) continue;
                if (GlyphGrid.ContainerFromItem(item) is ContentPresenter presenter &&
                    VisualTreeHelper.GetChildrenCount(presenter) > 0 &&
                    VisualTreeHelper.GetChild(presenter, 0) is Grid grid)
                {
                    action(grid, cp);
                }
            }
        }

        // ── toolbar ─────────────────────────────────────────────────────

        private void JumpBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_document is null || Codepoints.Count == 0) return;

            var glyphs = new List<GlyphSummary>(Codepoints.Count);
            foreach (int cp in Codepoints)
                if (_summaries.TryGetValue(cp, out var s)) glyphs.Add(s);

            int index = FontGlyphOps.FindJumpIndex(glyphs, JumpBox?.Text);
            if (index < 0 || index >= glyphs.Count) return;

            SelectedCodepoint = glyphs[index].Codepoint;
            ScrollSelectionIntoView();
        }

        private void ScrollSelectionIntoView()
        {
            if (GlyphScroll.Content is not UIElement content) return;
            if (GlyphGrid.ContainerFromItem(_selectedCodepoint) is not FrameworkElement container) return;

            var point = container.TransformToVisual(content).TransformPoint(new Windows.Foundation.Point(0, 0));
            GlyphScroll.ChangeView(null, point.Y, null);
        }

        private bool _suppressMetricsEvent;

        private void MetricsTool_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressMetricsEvent) return;
            MetricsEditingChanged?.Invoke(IsMetricsEditing);
        }

        /// <summary>
        /// Reflects the canvas's own view of the metrics tool without echoing it back. Picking a
        /// drawing tool disarms metrics editing from that side, and the button has to follow.
        /// </summary>
        public void SetMetricsEditing(bool editing)
        {
            if (MetricsToolButton is null || IsMetricsEditing == editing) return;

            _suppressMetricsEvent = true;
            MetricsToolButton.IsChecked = editing;
            _suppressMetricsEvent = false;
        }

        private void ResetSpacing_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCodepoint < 0) return;
            ResetSpacingRequested?.Invoke(_selectedCodepoint);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoomLevel + 1);

        private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoomLevel - 1);

        private void SetZoom(double level)
        {
            double clamped = Math.Clamp(level, 1.0, 10.0);
            if (Math.Abs(clamped - _zoomLevel) < 0.01) return;
            _zoomLevel = clamped;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GlyphCellSize)));
        }
    }
}
