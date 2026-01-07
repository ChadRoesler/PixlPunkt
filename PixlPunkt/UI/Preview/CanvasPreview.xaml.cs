using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Painting;
using PixlPunkt.Core.Structs;
using PixlPunkt.UI.CanvasHost;
using PixlPunkt.UI.Rendering;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace PixlPunkt.UI.Preview
{
    public sealed partial class CanvasPreview : UserControl
    {
        private CanvasViewHost? _host;

        // Latest composited frame
        private byte[]? _pixels;
        private int _docWidth;
        private int _docHeight;
        private readonly PatternBackgroundService _patternService = new();

        // Viewport from host (doc space)
        private Rect _viewport;

        // Local zoom inside the preview control
        private float _zoom = 1.0f;

        // Brush overlay snapshot from host - SINGLE SOURCE OF TRUTH
        private BrushOverlaySnapshot _brushOverlay = BrushOverlaySnapshot.Empty;

        // Click-drag to recenter
        private bool _dragging;

        // Empty state (no attached canvas)
        private bool _isEmpty = true;

        public string ZoomText => $"{_zoom * 100:0}%";
        public bool IsEmpty => _isEmpty;

        /// <summary>
        /// Gets the appropriate clear color based on the current theme.
        /// </summary>
        private Color GetThemeClearColor()
        {
            // Check the actual theme of the control
            var theme = ActualTheme;
            return theme == Microsoft.UI.Xaml.ElementTheme.Light
                ? Color.FromArgb(255, 249, 249, 249)  // Light theme background
                : Color.FromArgb(255, 24, 24, 24);     // Dark theme background
        }

        public CanvasPreview()
        {
            InitializeComponent();

            ZoomInButton.Click += (_, __) => AdjustZoom(1.25f);
            ZoomOutButton.Click += (_, __) => AdjustZoom(1.0f / 1.25f);
            FitButton.Click += (_, __) => ResetZoom();

            SizeChanged += (_, __) => PreviewCanvas.Invalidate();

            PreviewCanvas.PointerPressed += PreviewCanvas_PointerPressed;
            PreviewCanvas.PointerMoved += PreviewCanvas_PointerMoved;
            PreviewCanvas.PointerReleased += PreviewCanvas_PointerReleased;
            PreviewCanvas.PointerExited += PreviewCanvas_PointerExited;
            ResetToEmpty();

            // ── Stripe theme hookup ─────────────────────────────────────
            ApplyStripeColors();
            TransparencyStripeMixer.ColorsChanged += OnStripeColorsChanged;
            Unloaded += (_, __) => TransparencyStripeMixer.ColorsChanged -= OnStripeColorsChanged;
        }

        private void OnStripeColorsChanged()
        {
            ApplyStripeColors();
            _patternService.Invalidate();
            PreviewCanvas.Invalidate();
        }

        private void ApplyStripeColors()
        {
            var light = Color.FromArgb(
                255,
                TransparencyStripeMixer.LightR,
                TransparencyStripeMixer.LightG,
                TransparencyStripeMixer.LightB);

            var dark = Color.FromArgb(
                255,
                TransparencyStripeMixer.DarkR,
                TransparencyStripeMixer.DarkG,
                TransparencyStripeMixer.DarkB);

            if (_patternService.LightColor != light || _patternService.DarkColor != dark)
            {
                _patternService.LightColor = light;
                _patternService.DarkColor = dark;
            }
        }

        /// <summary>
        /// Attach this preview to a CanvasViewHost.
        /// </summary>
        public void Attach(CanvasViewHost host)
        {
            Detach();

            _host = host;

            _docWidth = host.Document.PixelWidth;
            _docHeight = host.Document.PixelHeight;
            _viewport = host.CurrentViewport;

            host.FrameReady += OnFrameReady;
            host.BrushOverlayChanged += OnBrushOverlayChanged;
            host.ViewportChanged += OnViewportChanged;

            _isEmpty = false;
            ResetZoom();

            // Ensure stripes match current theme immediately
            ApplyStripeColors();
            _patternService.Invalidate();

            PreviewCanvas.Invalidate();
        }

        /// <summary>
        /// Unhook from the current host.
        /// </summary>
        public void Detach()
        {
            if (_host != null)
            {
                _host.FrameReady -= OnFrameReady;
                _host.BrushOverlayChanged -= OnBrushOverlayChanged;
                _host.ViewportChanged -= OnViewportChanged;
                _host = null;
            }
            ResetToEmpty();
            PreviewCanvas.Invalidate();
        }

        private void ResetToEmpty()
        {
            _pixels = null;
            _docWidth = 0;
            _docHeight = 0;
            _viewport = Rect.Empty;
            _brushOverlay = BrushOverlaySnapshot.Empty;
            _zoom = 1.0f;
            _dragging = false;
            _isEmpty = true;
        }

        // Frame from host
        private void OnFrameReady(byte[] pixels, int width, int height)
        {
            if (_host == null) return;
            _pixels = pixels;
            _docWidth = width;
            _docHeight = height;
            _isEmpty = false;

            PreviewCanvas.Invalidate();
        }

        private void OnBrushOverlayChanged(Vector2 center, float radius)
        {
            // Simply grab the complete snapshot - main canvas is source of truth
            _brushOverlay = _host?.CurrentBrushOverlay ?? BrushOverlaySnapshot.Empty;
            PreviewCanvas.Invalidate();
        }

        private void OnViewportChanged(Rect rect)
        {
            _viewport = rect;
            PreviewCanvas.Invalidate();
        }

        private void AdjustZoom(float factor)
        {
            _zoom = Math.Clamp(_zoom * factor, 0.25f, 8.0f);
            PreviewCanvas.Invalidate();
        }

        private void ResetZoom()
        {
            _zoom = 1.0f;
            PreviewCanvas.Invalidate();
        }

        private bool TryGetLayout(out double offsetX, out double offsetY, out double scale)
        {
            offsetX = offsetY = scale = 0;
            if (_docWidth <= 0 || _docHeight <= 0) return false;

            double availW = PreviewCanvas.ActualWidth;
            double availH = PreviewCanvas.ActualHeight;
            if (availW <= 0 || availH <= 0) return false;

            double fitScale = Math.Min(availW / _docWidth, availH / _docHeight);
            scale = fitScale * _zoom;
            if (scale <= 0) return false;

            double canvasW = _docWidth * scale;
            double canvasH = _docHeight * scale;

            offsetX = (availW - canvasW) * 0.5;
            offsetY = (availH - canvasH) * 0.5;
            return true;
        }

        private bool TryScreenToDoc(Point screenPt, out double docX, out double docY)
        {
            docX = docY = 0;
            if (!TryGetLayout(out double ox, out double oy, out double scale)) return false;

            double localX = screenPt.X - ox;
            double localY = screenPt.Y - oy;

            double canvasW = _docWidth * scale;
            double canvasH = _docHeight * scale;
            if (localX < 0 || localY < 0 || localX > canvasW || localY > canvasH) return false;

            docX = localX / scale;
            docY = localY / scale;
            return true;
        }

        private void PreviewCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_isEmpty || _host == null) return;
            var pt = e.GetCurrentPoint(PreviewCanvas);
            if (!pt.Properties.IsLeftButtonPressed) return;
            if (TryScreenToDoc(pt.Position, out double docX, out double docY))
            {
                _dragging = true;
                PreviewCanvas.CapturePointer(e.Pointer);
                _host.CenterOnDocumentPoint(docX, docY);
            }
        }

        private void PreviewCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isEmpty || _host == null || !_dragging) return;
            var pt = e.GetCurrentPoint(PreviewCanvas);
            if (TryScreenToDoc(pt.Position, out double docX, out double docY))
                _host.CenterOnDocumentPoint(docX, docY);
        }

        private void PreviewCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                PreviewCanvas.ReleasePointerCaptures();
            }
        }

        private void PreviewCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                PreviewCanvas.ReleasePointerCaptures();
            }
        }

        private void PreviewCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;

            // Use theme-aware clear color
            var clearColor = GetThemeClearColor();
            ds.Clear(clearColor);

            if (_isEmpty) return;
            if (_pixels == null || _docWidth <= 0 || _docHeight <= 0) return;
            if (!TryGetLayout(out double ox, out double oy, out double scale)) return;

            float canvasW = (float)(_docWidth * scale);
            float canvasH = (float)(_docHeight * scale);
            float offsetX = (float)ox;
            float offsetY = (float)oy;
            var destRect = new Rect(offsetX, offsetY, canvasW, canvasH);

            double dpiScale = PreviewCanvas.XamlRoot?.RasterizationScale ?? 1.0;
            var bg = _patternService.GetSizedImage(sender.Device, dpiScale, (float)destRect.Width, (float)destRect.Height);

            float tx = (float)Math.Floor(destRect.X);
            float ty = (float)Math.Floor(destRect.Y);
            var tgt = new Rect(tx, ty, destRect.Width, destRect.Height);
            var src = new Rect(0, 0, bg.SizeInPixels.Width, bg.SizeInPixels.Height);
            ds.DrawImage(bg, tgt, src);

            // Create working buffer for compositing overlays
            byte[] workingBuf = new byte[_docWidth * _docHeight * 4];
            System.Buffer.BlockCopy(_pixels, 0, workingBuf, 0, _pixels.Length);

            // ═══════════════════════════════════════════════════════════════
            // BRUSH OVERLAYS - Trust snapshot.Visible completely
            // ═══════════════════════════════════════════════════════════════

            // 1. Shift-line preview (during shift+drag line drawing)
            if (_brushOverlay.IsShiftLineDrag && _host != null)
            {
                CompositeShiftLinePreviewIntoBuffer(workingBuf);
            }
            // 2. Brush cursor ghost (filled preview for brush tool)
            else if (_brushOverlay.Visible && _brushOverlay.FillGhost && _brushOverlay.Mask != null && _brushOverlay.Mask.Count > 0 && _host != null)
            {
                CompositeBrushGhostIntoBuffer(workingBuf);
            }

            // 3. Shape start point hover (when hovering with shape tool before drag starts)
            if (_brushOverlay.ShowShapeStartPoint && _host != null)
            {
                CompositeShapeStartPointIntoBuffer(workingBuf);
            }

            // 4. Shape preview overlay (during shape drag)
            if (_brushOverlay.IsShapeDrag && _host != null)
            {
                CompositeShapePreviewIntoBuffer(workingBuf);
            }

            // Render the composited image as a single bitmap
            using var compositeBmp = CanvasBitmap.CreateFromBytes(
                sender.Device,
                workingBuf,
                _docWidth,
                _docHeight,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96.0f);

            ds.DrawImage(
                compositeBmp,
                destRect,
                new Rect(0, 0, _docWidth, _docHeight),
                1.0f,
                CanvasImageInterpolation.NearestNeighbor);

            // 5. Brush outline (for non-brush tools - keep visible even during painting)
            // Show outline if: (1) normal hover state OR (2) outline mode with valid mask (even if Visible=false during painting)
            bool showOutline = _brushOverlay.Mask != null && _brushOverlay.Mask.Count > 0 && !_brushOverlay.FillGhost && !_brushOverlay.IsShiftLineDrag;
            if (showOutline)
            {
                DrawBrushOutline(ds, offsetX, offsetY, scale);
            }

            // Viewport rectangle
            if (_viewport.Width > 0 && _viewport.Height > 0)
            {
                float vx = offsetX + (float)(_viewport.X * scale);
                float vy = offsetY + (float)(_viewport.Y * scale);
                float vw = (float)(_viewport.Width * scale);
                float vh = (float)(_viewport.Height * scale);
                ds.DrawRectangle(vx, vy, vw, vh, Colors.DeepSkyBlue, 1.5f);
            }
        }

        /// <summary>
        /// Composites the shift-line preview into the working buffer.
        /// Shows the line being drawn from origin to current endpoint.
        /// Uses smooth stamping (stride of 1) matching PainterBase.StampLine for accurate preview.
        /// Handles both built-in shapes and custom brushes.
        /// </summary>
        private void CompositeShiftLinePreviewIntoBuffer(byte[] workingBuf)
        {
            uint fgColor = _host!.ForegroundColor;
            byte brushOpacity = _brushOverlay.BrushOpacity;

            // Check if using a custom brush - use the mask from the snapshot
            bool isCustomBrush = _brushOverlay.IsCustomBrush;
            var mask = _brushOverlay.Mask ?? GetBrushMaskOffsets(_brushOverlay.BrushSize, _brushOverlay.BrushShape);

            int x0 = _brushOverlay.ShiftLineX0;
            int y0 = _brushOverlay.ShiftLineY0;
            int x1 = _brushOverlay.ShiftLineX1;
            int y1 = _brushOverlay.ShiftLineY1;

            int dx = x1 - x0, dy = y1 - y0;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

            // Track which pixels have been drawn to prevent over-blending (max alpha wins)
            var drawnPixels = new Dictionary<(int x, int y), byte>();

            if (steps == 0)
            {
                // Single stamp at origin
                AccumulateShiftLineStamp(x0, y0, mask, isCustomBrush, brushOpacity, drawnPixels);
            }
            else
            {
                // Use stride of 1 for smooth, continuous line drawing (matches PainterBase.StampLine)
                double sx = dx / (double)steps;
                double sy = dy / (double)steps;

                double x = x0, y = y0;
                for (int i = 0; i <= steps; i++)
                {
                    AccumulateShiftLineStamp((int)Math.Round(x), (int)Math.Round(y), mask, isCustomBrush, brushOpacity, drawnPixels);
                    x += sx;
                    y += sy;
                }
            }

            // Apply all accumulated pixels to the working buffer
            foreach (var ((px, py), effA) in drawnPixels)
            {
                if ((uint)px >= (uint)_docWidth || (uint)py >= (uint)_docHeight)
                    continue;

                int idx = (py * _docWidth + px) * 4;

                uint before = (uint)(workingBuf[idx] | (workingBuf[idx + 1] << 8) |
                                    (workingBuf[idx + 2] << 16) | (workingBuf[idx + 3] << 24));
                uint srcWithAlpha = (fgColor & 0x00FFFFFFu) | ((uint)effA << 24);
                uint after = ColorUtil.BlendOver(before, srcWithAlpha);

                workingBuf[idx + 0] = (byte)(after & 0xFF);
                workingBuf[idx + 1] = (byte)((after >> 8) & 0xFF);
                workingBuf[idx + 2] = (byte)((after >> 16) & 0xFF);
                workingBuf[idx + 3] = (byte)((after >> 24) & 0xFF);
            }
        }

        /// <summary>
        /// Accumulates a brush stamp at the given point for shift-line preview.
        /// </summary>
        private void AccumulateShiftLineStamp(int cx, int cy, IReadOnlyList<(int dx, int dy)> mask, bool isCustomBrush, byte brushOpacity, Dictionary<(int x, int y), byte> drawnPixels)
        {
            byte brushDensity = _brushOverlay.BrushDensity;

            foreach (var (bx, by) in mask)
            {
                int px = cx + bx;
                int py = cy + by;

                // Compute alpha
                byte effA;
                if (isCustomBrush)
                {
                    // Custom brushes use radial density-based falloff (matches DefaultToolContextProvider)
                    effA = ComputeCustomBrushAlphaWithOpacity(bx, by, _brushOverlay.BrushSize, brushOpacity, brushDensity);
                }
                else
                {
                    effA = ComputeBrushAlphaWithOpacity(bx, by, _brushOverlay.BrushSize, _brushOverlay.BrushShape, brushOpacity, brushDensity);
                }

                if (effA == 0) continue;

                var key = (px, py);
                if (!drawnPixels.TryGetValue(key, out byte currentAlpha) || effA > currentAlpha)
                {
                    drawnPixels[key] = effA;
                }
            }
        }

        /// <summary>
        /// Composites the brush ghost (filled cursor preview for brush tool) into working buffer.
        /// Uses snapshot's brush settings for accurate alpha computation.
        /// Handles both built-in shapes and custom brushes.
        /// </summary>
        private void CompositeBrushGhostIntoBuffer(byte[] workingBuf)
        {
            uint fgColor = _host!.ForegroundColor;
            // Use snapshot's opacity instead of extracting from fgColor
            byte brushOpacity = _brushOverlay.BrushOpacity;
            byte brushDensity = _brushOverlay.BrushDensity;

            // Check if using a custom brush
            bool isCustomBrush = _brushOverlay.IsCustomBrush;

            foreach (var (dx, dy) in _brushOverlay.Mask!)
            {
                // Use exact integer hover coordinates to match main canvas perfectly
                int docX = _brushOverlay.HoverX + dx;
                int docY = _brushOverlay.HoverY + dy;

                if ((uint)docX >= (uint)_docWidth || (uint)docY >= (uint)_docHeight)
                    continue;

                // Compute per-pixel alpha
                byte effA;
                if (isCustomBrush)
                {
                    // Custom brushes use radial density-based falloff (matches DefaultToolContextProvider)
                    effA = ComputeCustomBrushAlphaWithOpacity(dx, dy, _brushOverlay.BrushSize, brushOpacity, brushDensity);
                }
                else
                {
                    // Built-in shapes use density-based alpha computation
                    effA = ComputeBrushAlphaWithOpacity(dx, dy, _brushOverlay.BrushSize, _brushOverlay.BrushShape, brushOpacity, brushDensity);
                }

                if (effA == 0) continue;

                int idx = (docY * _docWidth + docX) * 4;

                // Blend foreground color with computed alpha over existing pixel
                uint before = (uint)(workingBuf[idx] | (workingBuf[idx + 1] << 8) |
                                    (workingBuf[idx + 2] << 16) | (workingBuf[idx + 3] << 24));
                uint srcWithAlpha = (fgColor & 0x00FFFFFFu) | ((uint)effA << 24);
                uint after = ColorUtil.BlendOver(before, srcWithAlpha);

                workingBuf[idx + 0] = (byte)(after & 0xFF);
                workingBuf[idx + 1] = (byte)((after >> 8) & 0xFF);
                workingBuf[idx + 2] = (byte)((after >> 16) & 0xFF);
                workingBuf[idx + 3] = (byte)((after >> 24) & 0xFF);
            }
        }

        /// <summary>
        /// Composites the shape tool start point preview into working buffer.
        /// Shows where the brush will stamp when the shape drag begins.
        /// </summary>
        private void CompositeShapeStartPointIntoBuffer(byte[] workingBuf)
        {
            uint fgColor = _host!.ForegroundColor;
            var mask = GetBrushMaskOffsets(_brushOverlay.ShapeStrokeWidth, _brushOverlay.ShapeBrushShape);
            byte shapeOpacity = _brushOverlay.ShapeBrushOpacity;

            foreach (var (dx, dy) in mask)
            {
                int px = _brushOverlay.ShapeStartX + dx;
                int py = _brushOverlay.ShapeStartY + dy;

                if ((uint)px >= (uint)_docWidth || (uint)py >= (uint)_docHeight)
                    continue;

                // Use shape tool's opacity instead of fgColor alpha
                byte effA = ComputeBrushAlphaWithOpacity(dx, dy, _brushOverlay.ShapeStrokeWidth, _brushOverlay.ShapeBrushShape, shapeOpacity, _brushOverlay.ShapeBrushDensity);
                if (effA == 0) continue;

                int idx = (py * _docWidth + px) * 4;
                uint before = (uint)(workingBuf[idx] | (workingBuf[idx + 1] << 8) |
                                    (workingBuf[idx + 2] << 16) | (workingBuf[idx + 3] << 24));
                uint srcWithAlpha = (fgColor & 0x00FFFFFFu) | ((uint)effA << 24);
                uint after = ColorUtil.BlendOver(before, srcWithAlpha);

                workingBuf[idx + 0] = (byte)(after & 0xFF);
                workingBuf[idx + 1] = (byte)((after >> 8) & 0xFF);
                workingBuf[idx + 2] = (byte)((after >> 16) & 0xFF);
                workingBuf[idx + 3] = (byte)((after >> 24) & 0xFF);
            }
        }

        /// <summary>
        /// Draws the brush outline overlay for non-brush tools.
        /// Renders edge pixels of the mask in high-contrast orange.
        /// </summary>
        private void DrawBrushOutline(CanvasDrawingSession ds, float offsetX, float offsetY, double scale)
        {
            var set = new HashSet<(int dx, int dy)>(_brushOverlay.Mask!);
            float s = (float)scale;
            var outlineColor = Color.FromArgb(255, 255, 136, 0);

            foreach (var (dx, dy) in _brushOverlay.Mask!)
            {
                bool edge =
                    !set.Contains((dx + 1, dy)) ||
                    !set.Contains((dx - 1, dy)) ||
                    !set.Contains((dx, dy + 1)) ||
                    !set.Contains((dx, dy - 1));
                if (!edge) continue;

                // Use integer hover coordinates for pixel-perfect alignment
                int docX = _brushOverlay.HoverX + dx;
                int docY = _brushOverlay.HoverY + dy;
                float sx = offsetX + (docX * s);
                float sy = offsetY + (docY * s);
                ds.DrawRectangle(sx, sy, s, s, outlineColor, 1.0f);
            }
        }

        /// <summary>
        /// Composites the shape preview directly into the working buffer.
        /// Renders the shape being dragged with proper brush stamping and alpha blending.
        /// </summary>
        private void CompositeShapePreviewIntoBuffer(byte[] workingBuf)
        {
            int lx = Math.Min(_brushOverlay.ShapeX0, _brushOverlay.ShapeX1);
            int rx = Math.Max(_brushOverlay.ShapeX0, _brushOverlay.ShapeX1);
            int ty = Math.Min(_brushOverlay.ShapeY0, _brushOverlay.ShapeY1);
            int by = Math.Max(_brushOverlay.ShapeY0, _brushOverlay.ShapeY1);

            var pixelAlphas = new Dictionary<(int x, int y), byte>();
            uint fgColor = _host!.ForegroundColor;

            if (_brushOverlay.IsEllipse)
            {
                AccumulateEllipsePixels(lx, ty, rx, by, _brushOverlay.IsFilled, pixelAlphas);
            }
            else
            {
                if (_brushOverlay.IsFilled)
                {
                    // Filled rectangle: stamp brush at every interior point
                    var outline = new HashSet<(int x, int y)>();
                    for (int y = ty; y <= by; y++)
                        for (int x = lx; x <= rx; x++)
                            outline.Add((x, y));

                    AccumulateBrushStrokeOnPoints(outline, pixelAlphas);
                }
                else
                {
                    AccumulateRectOutlinePixels(lx, ty, rx, by, pixelAlphas);
                }
            }

            // Apply shape preview pixels with alpha blending directly into working buffer
            foreach (var (pos, alpha) in pixelAlphas)
            {
                if ((uint)pos.x >= (uint)_docWidth || (uint)pos.y >= (uint)_docHeight)
                    continue;

                uint srcWithAlpha = (fgColor & 0x00FFFFFFu) | ((uint)alpha << 24);

                int idx = (pos.y * _docWidth + pos.x) * 4;
                uint before = (uint)(workingBuf[idx] | (workingBuf[idx + 1] << 8) |
                                    (workingBuf[idx + 2] << 16) | (workingBuf[idx + 3] << 24));
                uint after = ColorUtil.BlendOver(before, srcWithAlpha);

                workingBuf[idx + 0] = (byte)(after & 0xFF);
                workingBuf[idx + 1] = (byte)((after >> 8) & 0xFF);
                workingBuf[idx + 2] = (byte)((after >> 16) & 0xFF);
                workingBuf[idx + 3] = (byte)((after >> 24) & 0xFF);
            }
        }

        private void AccumulateEllipsePixels(int x0, int y0, int x1, int y1, bool filled, Dictionary<(int x, int y), byte> pixelAlphas)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (y0 > y1) (y0, y1) = (y1, y0);

            int a = Math.Abs(x1 - x0);
            int b = Math.Abs(y1 - y0);
            int b1 = b & 1;

            long dx = 4L * (1 - a) * b * b;
            long dy = 4L * (b1 + 1) * a * a;
            long err = dx + dy + b1 * a * a;
            long e2;

            y0 += (b + 1) / 2;
            y1 = y0 - b1;

            long aa8 = 8L * a * a;
            long bb8 = 8L * b * b;

            var outline = new HashSet<(int x, int y)>();

            if (filled)
            {
                // Filled: collect all points for brush stamping
                do
                {
                    for (int x = x0; x <= x1; x++)
                        outline.Add((x, y0));

                    if (y0 != y1)
                    {
                        for (int x = x0; x <= x1; x++)
                            outline.Add((x, y1));
                    }

                    e2 = 2 * err;
                    if (e2 <= dy) { y0++; y1--; err += dy += aa8; }
                    if (e2 >= dx || 2 * err > dy) { x0++; x1--; err += dx += bb8; }
                }
                while (x0 <= x1);

                while ((y0 - y1) <= b)
                {
                    for (int x = x0 - 1; x <= x1 + 1; x++)
                    {
                        outline.Add((x, y0));
                        outline.Add((x, y1));
                    }
                    y0++; y1--;
                }
            }
            else
            {
                // Outline only
                do
                {
                    outline.Add((x1, y0)); outline.Add((x0, y0));
                    outline.Add((x0, y1)); outline.Add((x1, y1));

                    e2 = 2 * err;
                    if (e2 <= dy) { y0++; y1--; err += dy += aa8; }
                    if (e2 >= dx || 2 * err > dy) { x0++; x1--; err += dx += bb8; }
                }
                while (x0 <= x1);

                while ((y0 - y1) <= b)
                {
                    outline.Add((x0 - 1, y0)); outline.Add((x1 + 1, y0));
                    outline.Add((x0 - 1, y1)); outline.Add((x1 + 1, y1));
                    y0++; y1--;
                }
            }

            // Apply brush stroke to all collected points
            AccumulateBrushStrokeOnPoints(outline, pixelAlphas);
        }

        private void AccumulateRectOutlinePixels(int x0, int y0, int x1, int y1, Dictionary<(int x, int y), byte> pixelAlphas)
        {
            var outline = new HashSet<(int x, int y)>();

            for (int x = x0; x <= x1; x++)
            {
                outline.Add((x, y0));
                outline.Add((x, y1));
            }
            for (int y = y0 + 1; y <= y1 - 1; y++)
            {
                outline.Add((x0, y));
                if (x1 != x0) outline.Add((x1, y));
            }

            AccumulateBrushStrokeOnPoints(outline, pixelAlphas);
        }

        private void AccumulateBrushStrokeOnPoints(HashSet<(int x, int y)> points, Dictionary<(int x, int y), byte> pixelAlphas)
        {
            // Use shape-specific brush settings from snapshot
            var mask = GetBrushMaskOffsets(_brushOverlay.ShapeStrokeWidth, _brushOverlay.ShapeBrushShape);
            byte shapeOpacity = _brushOverlay.ShapeBrushOpacity;

            foreach (var (ox, oy) in points)
            {
                foreach (var (dx, dy) in mask)
                {
                    int px = ox + dx;
                    int py = oy + dy;

                    // Use shape tool's opacity instead of fgColor alpha
                    byte effA = ComputeBrushAlphaWithOpacity(dx, dy, _brushOverlay.ShapeStrokeWidth, _brushOverlay.ShapeBrushShape, shapeOpacity, _brushOverlay.ShapeBrushDensity);
                    if (effA > 0)
                    {
                        var key = (px, py);
                        // Use max alpha (matching StrokeEngine behavior)
                        if (!pixelAlphas.TryGetValue(key, out byte currentAlpha) || effA > currentAlpha)
                        {
                            pixelAlphas[key] = effA;
                        }
                    }
                }
            }
        }

        private IReadOnlyList<(int dx, int dy)> GetBrushMaskOffsets(int size, BrushShape shape)
        {
            // Use the shared BrushMaskCache singleton to ensure preview masks match main canvas exactly
            return BrushMaskCache.Shared.GetOffsets(shape, size);
        }

        /// <summary>
        /// Computes per-pixel brush alpha matching StrokeEngine.ComputePerPixelAlpha logic exactly.
        /// This MUST remain 100% synchronized with StrokeEngine's implementation.
        /// </summary>
        private byte ComputeBrushAlpha(int dx, int dy, int size, BrushShape shape, uint fgColor, byte density)
        {
            // Extract opacity from fgColor for shape tools
            byte opacity = (byte)(fgColor >> 24);
            return ComputeBrushAlphaWithOpacity(dx, dy, size, shape, opacity, density);
        }

        /// <summary>
        /// Computes per-pixel brush alpha with explicit opacity parameter.
        /// Used when opacity comes from snapshot instead of fgColor.
        /// </summary>
        private static byte ComputeBrushAlphaWithOpacity(int dx, int dy, int size, BrushShape shape, byte opacity, byte density)
        {
            if (opacity == 0) return 0;

            double Aop = opacity / 255.0;
            int sz = Math.Max(1, size);
            double r = sz / 2.0;

            // Compute distance based on shape - offsets are already relative to brush center
            double d = shape == BrushShape.Circle
                ? Math.Sqrt((double)dx * dx + (double)dy * dy)
                : Math.Max(Math.Abs(dx), Math.Abs(dy));

            if (d > r) return 0;

            // Apply density falloff (matches StrokeEngine)
            double D = density / 255.0;
            double Rhard = r * D;

            if (d <= Rhard)
                return (byte)Math.Round(255.0 * Aop);

            // Soft falloff region (smoothstep)
            double span = Math.Max(1e-6, (r - Rhard));
            double t = (d - Rhard) / span;
            double mask = 1.0 - (t * t) * (3 - 2 * t);
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }

        /// <summary>
        /// Computes per-pixel alpha for custom brushes using radial density-based falloff.
        /// Matches DefaultToolContextProvider.ComputeCustomBrushAlpha exactly.
        /// </summary>
        private static byte ComputeCustomBrushAlphaWithOpacity(int dx, int dy, int size, byte opacity, byte density)
        {
            if (opacity == 0) return 0;

            int sz = Math.Max(1, size);
            double Aop = opacity / 255.0;

            double r = sz / 2.0;

            // Use circular distance - offsets are already relative to brush center
            double d = Math.Sqrt((double)dx * dx + (double)dy * dy);

            // Apply density falloff based on distance
            double D = density / 255.0;
            double Rhard = r * D;

            // Full opacity within the hard radius
            if (d <= Rhard)
                return (byte)Math.Round(255.0 * Aop);

            // Falloff region beyond hard radius
            double span = Math.Max(1e-6, r - Rhard);
            double t = Math.Min(1.0, (d - Rhard) / span); // Clamp t to [0, 1]
            double mask = 1.0 - (t * t) * (3 - 2 * t); // Smoothstep falloff
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }
    }
}