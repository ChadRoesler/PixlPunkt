using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document;
using PixlPunkt.UI.CanvasHost;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace PixlPunkt.UI.Font
{
    /// <summary>
    /// Shows the glyph being worked on with a character either side, drawn at the font's real
    /// metrics. Spacing only means anything in company, so this is where you judge whether an
    /// advance is right, rather than on the sheet where every cell is the same width.
    /// </summary>
    /// <remarks>
    /// It repaints on every font change rather than on a timer, because watching the neighbours
    /// reflow while dragging an advance post is the entire point of it.
    /// </remarks>
    public sealed partial class GlyphStrip : UserControl
    {
        private CanvasDocument? _document;
        private CanvasViewHost? _host;
        private int _codepoint = -1;
        private double _zoom = 8.0;

        private static readonly SKColor BaselineColor = new(230, 140, 90, 210);
        private static readonly SKColor EmLineColor = new(170, 180, 200, 80);
        private static readonly SKColor OriginColor = new(120, 200, 255, 230);
        private static readonly SKColor AdvanceColor = new(120, 200, 255, 150);
        private static readonly SKColor AdvanceBandColor = new(120, 200, 255, 28);

        public GlyphStrip()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Points the strip at a document and the canvas host whose focus it follows. Passing null
        /// for either detaches it.
        /// </summary>
        public void Bind(CanvasDocument? document, CanvasViewHost? host)
        {
            if (_document is not null)
            {
                _document.FontChanged -= OnFontChanged;
                _document.DocumentModified -= OnFontChanged;
            }
            if (_host is not null)
            {
                _host.FontGlyphFocusedChanged -= OnGlyphFocused;
            }

            _document = document;
            _host = host;

            if (_document is not null)
            {
                _document.FontChanged += OnFontChanged;
                _document.DocumentModified += OnFontChanged;
            }
            if (_host is not null)
            {
                _host.FontGlyphFocusedChanged += OnGlyphFocused;
                _codepoint = _host.FontFocusedCodepoint;
            }
            else
            {
                _codepoint = -1;
            }

            // Opening a font with an empty strip wastes the pane. Start on the first character,
            // without moving the canvas, so there is something to look at straight away.
            if (_codepoint < 0 && HasFont)
            {
                var glyphs = FontGlyphOps.Summarize(_document!);
                if (glyphs.Count > 0) _codepoint = glyphs[0].Codepoint;
            }

            UpdateStatus();
            StripCanvas.Invalidate();
        }

        /// <summary>Whether the bound document is a font, and so whether this has anything to show.</summary>
        public bool HasFont => _document?.FontState.HasState == true;

        private void OnGlyphFocused(int codepoint) => DispatcherQueue.TryEnqueue(() =>
        {
            if (_codepoint == codepoint) return;
            _codepoint = codepoint;
            UpdateStatus();
            StripCanvas.Invalidate();
        });

        private void OnFontChanged() => DispatcherQueue.TryEnqueue(() =>
        {
            UpdateStatus();
            StripCanvas.Invalidate();
        });

        /// <summary>The character a pin box is asking for, or null when it is empty or unusable.</summary>
        private int? PinOf(TextBox box)
        {
            string text = box?.Text ?? string.Empty;
            return text.Length == 0 ? null : text[0];
        }

        private void Pin_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateStatus();
            StripCanvas.Invalidate();
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom + 2);

        private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom - 2);

        private void SetZoom(double zoom)
        {
            double clamped = Math.Clamp(zoom, 2.0, 32.0);
            if (Math.Abs(clamped - _zoom) < 0.01) return;
            _zoom = clamped;
            StripCanvas.Invalidate();
        }

        /// <summary>The sample to draw, as decided by the focused glyph and the two pin boxes.</summary>
        private string ResolveSample(out List<GlyphSummary> glyphs)
        {
            glyphs = _document is null ? new List<GlyphSummary>() : FontGlyphOps.Summarize(_document);
            if (_codepoint < 0) return string.Empty;
            return FontGlyphOps.SampleAround(glyphs, _codepoint, PinOf(LeftPin), PinOf(RightPin));
        }

        private void UpdateStatus()
        {
            if (StatusText is null) return;

            if (!HasFont)
            {
                StatusText.Text = string.Empty;
                return;
            }

            if (_codepoint < 0)
            {
                StatusText.Text = "Pick a glyph to see it beside its neighbours.";
                return;
            }

            var (originX, advance) = FontMetricsOps.ResolveMetrics(_document!, _codepoint);
            bool auto = !_document!.FontState.TryGet(_codepoint, out var g) || g.AutoFit;

            StatusText.Text =
                $"{FontGlyphOps.LabelFor(_codepoint)}   origin {originX}   advance {advance}   " +
                (auto ? "auto" : "set by hand");
        }

        // ── painting ────────────────────────────────────────────────────

        private void StripCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (sender is not SKXamlCanvas view || !HasFont) return;
            float w = (float)view.ActualWidth, h = (float)view.ActualHeight;
            if (w <= 0 || h <= 0) return;

            string sample = ResolveSample(out _);
            if (sample.Length == 0) return;

            var doc = _document!;
            var placements = FontLayoutOps.LayoutLine(doc, sample);
            if (placements.Count == 0) return;

            float scale = (float)_zoom;
            int cellH = Math.Max(1, doc.TileSize.Height);

            // Centre on the glyph being worked on, not on the sample, so the thing under scrutiny
            // stays put while its neighbours change around it.
            int focusIndex = placements.FindIndex(p => p.Codepoint == _codepoint);
            var (focusOrigin, focusAdvance) = FontMetricsOps.ResolveMetrics(doc, _codepoint);

            float centreOn = focusIndex >= 0
                ? placements[focusIndex].PenX + focusAdvance / 2f
                : FontLayoutOps.MeasureLine(doc, sample) / 2f;

            float originX = w / 2f - centreOn * scale;
            float baseY = h * 0.62f;
            float topY = baseY - doc.FontState.BaselineY * scale;

            DrawGuides(canvas, doc, w, originX, topY, baseY, scale, cellH);

            foreach (var p in placements)
                DrawGlyph(canvas, doc, p.CellIndex, originX + p.DrawX * scale, topY, scale);

            if (focusIndex >= 0)
            {
                DrawFocusMetrics(canvas, placements[focusIndex].PenX, focusOrigin, focusAdvance,
                                 originX, topY, scale, cellH);
            }
        }

        /// <summary>Baseline and the em's top and bottom, run right across so they read as lines.</summary>
        private static void DrawGuides(SKCanvas canvas, CanvasDocument doc, float w,
                                       float originX, float topY, float baseY, float scale, int cellH)
        {
            var em = FontMetricsOps.EmBox(doc);
            using var paint = new SKPaint { IsAntialias = false, StrokeWidth = 1 };

            paint.Color = EmLineColor;
            canvas.DrawLine(0, topY + em.Y * scale, w, topY + em.Y * scale, paint);
            canvas.DrawLine(0, topY + (em.Y + em.Height) * scale, w, topY + (em.Y + em.Height) * scale, paint);

            paint.Color = BaselineColor;
            canvas.DrawLine(0, baseY, w, baseY, paint);
        }

        /// <summary>Blits one cell out of the document surface at the given position.</summary>
        private static void DrawGlyph(SKCanvas canvas, CanvasDocument doc, int cellIndex,
                                      float x, float y, float scale)
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
            using var paint = new SKPaint { IsAntialias = false };
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, cell.Width, cell.Height), dest, paint);
        }

        /// <summary>
        /// The pen position and the advance of the glyph under scrutiny, as a shaded band with a
        /// post at each end. This is the same pair being dragged on the canvas, shown in context.
        /// </summary>
        private static void DrawFocusMetrics(SKCanvas canvas, int penX, int originX, int advance,
                                             float offsetX, float topY, float scale, int cellH)
        {
            float left = offsetX + penX * scale;
            float right = offsetX + (penX + advance) * scale;
            float top = topY;
            float bottom = topY + cellH * scale;

            using var fill = new SKPaint { Color = AdvanceBandColor, IsAntialias = false };
            canvas.DrawRect(new SKRect(left, top, right, bottom), fill);

            using var paint = new SKPaint { IsAntialias = false, StrokeWidth = 1 };

            paint.Color = OriginColor;
            canvas.DrawLine(left, top, left, bottom, paint);

            paint.Color = AdvanceColor;
            canvas.DrawLine(right, top, right, bottom, paint);
        }
    }
}
