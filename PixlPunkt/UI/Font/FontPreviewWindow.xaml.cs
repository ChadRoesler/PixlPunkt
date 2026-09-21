using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Document;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.Font
{
    /// <summary>
    /// Shows a font as running text rather than as a sheet of cells. Everything is laid out through
    /// <see cref="FontLayoutOps"/>, the same code the exporters use, so what is shown here and what
    /// ships cannot drift apart.
    /// </summary>
    /// <remarks>
    /// Sizes offered are multiples of the em, because those are the only ones a bitmap font renders
    /// evenly at. Offering the sizes in between would only invite the lumpy stems this editor exists
    /// to avoid, so the window says which sizes are honest instead of hiding the limit.
    /// </remarks>
    public sealed partial class FontPreviewWindow : Window
    {
        private const string DefaultSample =
            "The quick brown fox\njumps over the lazy dog\nSphinx of black quartz, judge my vow\n0123456789";

        private CanvasDocument? _document;
        private int _sizePx;
        private int _zoom = 1;
        private Color _foreground = Colors.White;
        private Color _background = Color.FromArgb(255, 24, 24, 28);

        /// <summary>
        /// False until the whole tree exists. Setting a value in XAML raises its changed event
        /// during the load, so a handler can run while controls declared further down the file are
        /// still null. The checkbox's IsChecked did exactly that to the preview canvas.
        /// </summary>
        private bool _ready;

        public FontPreviewWindow()
        {
            InitializeComponent();

            SampleBox.Text = DefaultSample;

            // Pickers live in flyouts, whose content is realised late, so neither is assumed here.
            if (ForegroundPicker is not null) ForegroundPicker.Color = _foreground;
            if (BackgroundPicker is not null) BackgroundPicker.Color = _background;

            _ready = true;
            UpdateSwatches();
        }

        /// <summary>Points the window at a font document and fills in the sizes it renders cleanly at.</summary>
        public void Bind(CanvasDocument document)
        {
            Detach();

            _document = document;
            _document.DocumentModified += OnDocumentChanged;
            _document.FontChanged += OnDocumentChanged;
            Closed += (_, __) => Detach();

            PopulateSizes();
            Redraw();
        }

        private void Detach()
        {
            if (_document is null) return;
            _document.DocumentModified -= OnDocumentChanged;
            _document.FontChanged -= OnDocumentChanged;
            _document = null;
        }

        /// <summary>
        /// Keeps the sample in step with the sheet. A preview that quietly went stale would be
        /// worse than none, since the whole reason to open it is to trust what it shows.
        /// </summary>
        private void OnDocumentChanged()
        {
            if (DispatcherQueue is null)
            {
                Redraw();
                return;
            }
            DispatcherQueue.TryEnqueue(Redraw);
        }

        private int EmHeight => _document is null ? 8 : FontMetricsOps.EmHeightOf(_document);

        /// <summary>
        /// One entry per whole multiple of the em. The label carries the multiple as well as the
        /// pixel size, because the multiple is the thing that explains why it looks right.
        /// </summary>
        private void PopulateSizes()
        {
            SizeCombo.Items.Clear();
            int em = EmHeight;

            for (int multiple = 1; multiple <= 8; multiple++)
            {
                int px = em * multiple;
                SizeCombo.Items.Add(new ComboBoxItem
                {
                    Content = multiple == 1 ? $"{px} px  (as drawn)" : $"{px} px  ({multiple}×)",
                    Tag = px,
                });
            }

            // Two up from the drawn size reads comfortably on a modern display.
            SizeCombo.SelectedIndex = Math.Min(1, SizeCombo.Items.Count - 1);
        }

        private void Size_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (SizeCombo.SelectedItem is ComboBoxItem { Tag: int px }) _sizePx = px;
            Redraw();
        }

        private void Sample_Changed(object sender, TextChangedEventArgs e) => Redraw();

        private void ResetSample_Click(object sender, RoutedEventArgs e) => SampleBox.Text = DefaultSample;

        /// <summary>
        /// Replaces the sample with every character that actually has ink. Most of the life of a
        /// font is spent part-drawn, and a standard pangram shows nothing at all until late on.
        /// </summary>
        private void UseDrawn_Click(object sender, RoutedEventArgs e)
        {
            if (_document is null || SampleBox is null) return;

            var drawn = new System.Text.StringBuilder();
            int onThisLine = 0;

            foreach (var g in FontGlyphOps.Summarize(_document))
            {
                if (!g.HasInk) continue;

                drawn.Append(char.ConvertFromUtf32(g.Codepoint));
                if (++onThisLine < 24) continue;

                drawn.Append('\n');
                onThisLine = 0;
            }

            SampleBox.Text = drawn.Length == 0 ? string.Empty : drawn.ToString();
        }

        private void Toggle_Changed(object sender, RoutedEventArgs e) => Redraw();

        private void Foreground_Changed(ColorPicker sender, ColorChangedEventArgs args)
        {
            _foreground = args.NewColor;
            UpdateSwatches();
            Redraw();
        }

        private void Background_Changed(ColorPicker sender, ColorChangedEventArgs args)
        {
            _background = args.NewColor;
            UpdateSwatches();
            Redraw();
        }

        private void UpdateSwatches()
        {
            if (ForegroundButton is not null)
                ForegroundButton.Background = new SolidColorBrush(_foreground);
            if (BackgroundButton is not null)
                BackgroundButton.Background = new SolidColorBrush(_background);
        }

        /// <summary>
        /// The canvas paints for the first time only once it exists and has been measured. Asking
        /// for a repaint from Bind alone left the window blank, because that happens before the
        /// window has been laid out and the request had nothing to land on.
        /// </summary>
        private void PreviewCanvas_Loaded(object sender, RoutedEventArgs e) => Redraw();

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e) =>
            PreviewCanvas?.Invalidate();

        // ── pan and zoom ────────────────────────────────────────────────
        // Same gestures as the main canvas: the middle button drags the view and the wheel zooms.

        private bool _panning;
        private Point _panLast;

        private void Preview_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint(PreviewHitOverlay).Properties.IsMiddleButtonPressed) return;

            _panning = true;
            _panLast = e.GetCurrentPoint(PreviewScroll).Position;
            PreviewHitOverlay.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Preview_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_panning || PreviewScroll is null) return;

            // Measured against the scroll viewer, not the canvas, so the canvas sliding under the
            // pointer cannot feed back into the next delta.
            var now = e.GetCurrentPoint(PreviewScroll).Position;
            PreviewScroll.ChangeView(
                PreviewScroll.HorizontalOffset - (now.X - _panLast.X),
                PreviewScroll.VerticalOffset - (now.Y - _panLast.Y),
                null,
                true);

            _panLast = now;
            e.Handled = true;
        }

        private void Preview_PointerReleased(object sender, PointerRoutedEventArgs e) => EndPan(e.Pointer);

        private void Preview_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndPan(null);

        private void EndPan(Pointer? pointer)
        {
            if (!_panning) return;
            _panning = false;
            if (pointer is not null) PreviewHitOverlay.ReleasePointerCapture(pointer);
        }

        /// <summary>
        /// Wheel zoom, holding whatever is under the pointer still. Zooming about the corner makes
        /// you chase the thing you were looking at, which is no way to inspect a stem.
        /// </summary>
        private void Preview_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PreviewHitOverlay);
            int notches = point.Properties.MouseWheelDelta;
            if (notches == 0 || PreviewScroll is null) return;

            e.Handled = true;

            int target = Math.Clamp(_zoom + (notches > 0 ? 1 : -1), 1, 16);
            if (target == _zoom) return;

            // Where the pointer sits in the viewport, and what font pixel is under it.
            double viewportX = point.Position.X - PreviewScroll.HorizontalOffset;
            double viewportY = point.Position.Y - PreviewScroll.VerticalOffset;
            double fontX = point.Position.X / Scale;
            double fontY = point.Position.Y / Scale;

            _zoom = target;
            Redraw();

            double newScale = Scale;
            double offsetX = fontX * newScale - viewportX;
            double offsetY = fontY * newScale - viewportY;

            // The canvas has only just been resized, so the scroll has to wait for that layout.
            DispatcherQueue.TryEnqueue(() =>
                PreviewScroll.ChangeView(Math.Max(0, offsetX), Math.Max(0, offsetY), null, true));
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom + 1);

        private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom - 1);

        /// <summary>
        /// Magnifies the view. Kept apart from the size picker on purpose: the size decides how the
        /// font is rendered, and zoom only decides how close you are looking at that rendering.
        /// Whole numbers only, so looking closer never invents a half pixel.
        /// </summary>
        private void SetZoom(int zoom)
        {
            int clamped = Math.Clamp(zoom, 1, 16);
            if (clamped == _zoom) return;
            _zoom = clamped;
            Redraw();
        }

        private void Redraw()
        {
            if (!_ready || PreviewCanvas is null) return;

            if (ZoomText is not null) ZoomText.Text = $"{_zoom}×";

            UpdateCrispnessText();
            UpdateEmptyMessage();
            ResizeCanvasToContent();
            PreviewCanvas.Invalidate();
        }

        /// <summary>
        /// Explains an empty sample. A preview that just sits blank gives no way to tell a font with
        /// nothing drawn from a sample using characters it does not have, or from a broken window.
        /// </summary>
        private void UpdateEmptyMessage()
        {
            if (EmptyMessage is null) return;

            string? message = DescribeEmptiness();
            EmptyMessage.Text = message ?? string.Empty;
            EmptyMessage.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>Null when there is something to show; otherwise why there is not.</summary>
        private string? DescribeEmptiness()
        {
            if (_document is null || !_document.FontState.HasState)
                return "No font is open.";

            var glyphs = FontGlyphOps.Summarize(_document);
            if (glyphs.Count == 0)
                return "This font has no characters mapped yet.";

            if (FontGlyphOps.UndrawnCount(glyphs) == glyphs.Count)
                return $"None of this font's {glyphs.Count} characters have been drawn yet, " +
                       "so the sample has nothing to show.";

            int placed = 0, inked = 0;
            foreach (string line in FontLayoutOps.SplitLines(SampleBox?.Text))
            {
                foreach (var p in FontLayoutOps.LayoutLine(_document, line))
                {
                    placed++;
                    if (FontMetricsOps.MeasureInk(_document, p.CellIndex) is not null) inked++;
                }
            }

            if (placed == 0)
                return "None of the characters in this sample are in the font.";

            if (inked == 0)
                return "The characters in this sample are in the font, but none of them are drawn yet.";

            return null;
        }

        private void UpdateCrispnessText()
        {
            if (_document is null || CrispnessText is null) return;

            int em = EmHeight;
            string sizes = string.Join(", ", FontMetricsOps.CleanSizes(_document));
            CrispnessText.Text =
                $"Drawn at {em} px to the em. Renders evenly at {sizes} px, and at any other whole " +
                "multiple of the em. In between those, stems come out uneven, whatever the font is built with.";
        }

        /// <summary>
        /// Sizes the canvas to the text so the scroll viewer has something to scroll. A canvas left
        /// at its natural size would clip long samples instead of letting them run.
        /// </summary>
        private void ResizeCanvasToContent()
        {
            if (_document is null || PreviewCanvas is null || SampleBox is null) return;

            var block = FontLayoutOps.MeasureBlock(_document, SampleBox.Text);
            double scale = Scale;
            int cellH = Math.Max(1, _document.TileSize.Height);

            // Room for a descender and for ink that overhangs the last advance. Never smaller than
            // the viewport, so the middle button can drag anywhere in the visible area.
            PreviewCanvas.Width = Math.Max(
                Math.Max(160, PreviewScroll?.ViewportWidth ?? 0), (block.Width + cellH * 2) * scale);
            PreviewCanvas.Height = Math.Max(
                Math.Max(120, PreviewScroll?.ViewportHeight ?? 0), (block.Height + cellH) * scale);
        }

        /// <summary>
        /// How the font is rendered: output pixels per font pixel at the chosen size. Whole numbers
        /// are what keep the stems even, which is why only multiples of the em are offered.
        /// </summary>
        private double RenderScale => _sizePx <= 0 ? 1 : _sizePx / (double)Math.Max(1, EmHeight);

        /// <summary>Render scale times zoom: what actually reaches the canvas.</summary>
        private double Scale => RenderScale * _zoom;

        private void PreviewCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(new SKColor(_background.R, _background.G, _background.B, _background.A));

            if (_document is null || !_document.FontState.HasState) return;

            var doc = _document;
            float scale = (float)Scale;
            int lineAdvance = FontLayoutOps.LineAdvance(doc);
            int baseline = doc.FontState.BaselineY;
            var em = FontMetricsOps.EmBox(doc);

            using var paint = new SKPaint { IsAntialias = false };
            if (UseTextColour?.IsChecked == true)
            {
                paint.ColorFilter = SKColorFilter.CreateBlendMode(
                    new SKColor(_foreground.R, _foreground.G, _foreground.B),
                    SKBlendMode.SrcIn);
            }

            float marginX = doc.TileSize.Height * scale;
            var lines = FontLayoutOps.SplitLines(SampleBox?.Text);

            for (int i = 0; i < lines.Length; i++)
            {
                // The em's top sits at the line's top, so the cell is drawn a little above it when
                // the font has overhang room.
                float lineTop = (i * lineAdvance) * scale - em.Y * scale;

                if (ShowBaselines?.IsChecked == true)
                    DrawBaseline(canvas, lineTop + baseline * scale, (float)PreviewCanvas.Width);

                foreach (var p in FontLayoutOps.LayoutLine(doc, lines[i]))
                    DrawGlyph(canvas, doc, p.CellIndex, marginX + p.DrawX * scale, lineTop, scale, paint);
            }
        }

        private static void DrawBaseline(SKCanvas canvas, float y, float width)
        {
            using var paint = new SKPaint
            {
                Color = new SKColor(230, 140, 90, 120),
                StrokeWidth = 1,
                IsAntialias = false,
            };
            canvas.DrawLine(0, y, width, y, paint);
        }

        /// <summary>Blits one glyph cell out of the document surface, nearest neighbour.</summary>
        private static void DrawGlyph(SKCanvas canvas, CanvasDocument doc, int cellIndex,
                                      float x, float y, float scale, SKPaint paint)
        {
            if (cellIndex < 0) return;

            var cell = FontMetricsOps.GetCellRect(doc, cellIndex);
            if (cell.Width <= 0 || cell.Height <= 0) return;

            var surf = doc.Surface;
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

            var dest = new SKRect(x, y, x + cell.Width * scale, y + cell.Height * scale);
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, cell.Width, cell.Height), dest, paint);
        }
    }
}
