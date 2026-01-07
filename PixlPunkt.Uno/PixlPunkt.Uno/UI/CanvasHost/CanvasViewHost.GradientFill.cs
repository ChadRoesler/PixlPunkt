using System;
using System.Numerics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Painting.Dithering;
using PixlPunkt.Uno.Core.Painting.Painters;
using PixlPunkt.Uno.Core.Rendering;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.Core.Tools.Settings;
using Windows.Foundation;
using GradientStop = PixlPunkt.Uno.Core.Tools.Settings.GradientStop;

namespace PixlPunkt.Uno.UI.CanvasHost
{
    /// <summary>
    /// Gradient fill tool subsystem for CanvasViewHost:
    /// - Gradient drag state machine
    /// - Gradient preview rendering
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ====================================================================
        // GRADIENT FILL STATE
        // ====================================================================

        private bool _gradientDrag;
        private int _gradientStartX, _gradientStartY;
        private int _gradientEndX, _gradientEndY;
        private readonly GradientFillPainter _gradientPainter = new();

        // ====================================================================
        // GRADIENT INPUT HANDLERS
        // ====================================================================

        private void HandleGradientFillPressed(PointerPoint p, PointerRoutedEventArgs e)
        {
            if (!TryGetDocInside(p.Position, out var x, out var y)) return;

            _gradientStartX = _gradientEndX = x;
            _gradientStartY = _gradientEndY = y;
            _gradientDrag = true;

            _mainCanvas.CapturePointer(e.Pointer);
            InvalidateMainCanvas();
        }

        private void HandleGradientFillMoved(Point pos)
        {
            // Convert screen to doc and clamp to document bounds
            var docPt = _zoom.ScreenToDoc(pos);
            int x = (int)Math.Floor(docPt.X);
            int y = (int)Math.Floor(docPt.Y);

            // Clamp to document bounds
            int w = Document.Surface.Width;
            int h = Document.Surface.Height;
            x = Math.Clamp(x, 0, w - 1);
            y = Math.Clamp(y, 0, h - 1);

            // Handle Shift for axis-constrained gradients
            bool shift = IsKeyDown(Windows.System.VirtualKey.Shift);
            if (shift)
            {
                int dx = Math.Abs(x - _gradientStartX);
                int dy = Math.Abs(y - _gradientStartY);

                // Constrain to horizontal, vertical, or 45-degree diagonal
                if (dx > dy * 2)
                {
                    // Horizontal
                    y = _gradientStartY;
                }
                else if (dy > dx * 2)
                {
                    // Vertical
                    x = _gradientStartX;
                }
                else
                {
                    // 45-degree diagonal
                    int d = Math.Max(dx, dy);
                    x = _gradientStartX + (x > _gradientStartX ? d : -d);
                    y = _gradientStartY + (y > _gradientStartY ? d : -d);
                }
            }

            _gradientEndX = x;
            _gradientEndY = y;
            InvalidateMainCanvas();
        }

        private void HandleGradientFillReleased()
        {
            _gradientDrag = false;

            OnBrushMoved(new Vector2(_hoverX, _hoverY), (float)((_brushSize - 1) * 0.5));

            if (Document.ActiveLayer is not RasterLayer rl) return;
            if (_toolState == null) return;

            var settings = _toolState.GradientFill;

            // Get selection mask if any
            Func<int, int, bool>? selMask = null;
            if (_selectionEngine.HasActiveSelection)
            {
                selMask = (x, y) => _selectionEngine.Sel.Mask.Contains(x, y);
            }

            // Render the gradient
            var historyItem = _gradientPainter.Render(
                rl,
                _gradientStartX, _gradientStartY,
                _gradientEndX, _gradientEndY,
                settings,
                _fg, _bgColor,
                selMask);

            // Push to history
            if (historyItem != null && !historyItem.IsEmpty)
            {
                Document.History.Push(historyItem);
            }

            // Composite and update
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            InvalidateMainCanvas();
            HistoryStateChanged?.Invoke();
            RaiseFrame();

            _mainCanvas.ReleasePointerCaptures();
        }

        // ====================================================================
        // GRADIENT PREVIEW RENDERING
        // ====================================================================

        private void DrawGradientFillPreview(ICanvasRenderer renderer, Rect dest)
        {
            if (_toolState == null) return;

            var settings = _toolState.GradientFill;
            double scale = _zoom.Scale;

            int w = Document.Surface.Width;
            int h = Document.Surface.Height;

            // Get gradient colors
            var stops = GetPreviewGradientStops(settings);
            if (stops.Length == 0) return;

            // Calculate gradient parameters
            double dx = _gradientEndX - _gradientStartX;
            double dy = _gradientEndY - _gradientStartY;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1) length = 1;

            // Draw preview line showing gradient direction
            float startScreenX = (float)(dest.X + _gradientStartX * scale + scale / 2);
            float startScreenY = (float)(dest.Y + _gradientStartY * scale + scale / 2);
            float endScreenX = (float)(dest.X + _gradientEndX * scale + scale / 2);
            float endScreenY = (float)(dest.Y + _gradientEndY * scale + scale / 2);

            // Draw gradient line with endpoints
            var lineColor = Windows.UI.Color.FromArgb(200, 255, 255, 255);
            var dotColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            var outlineColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);

            renderer.DrawLine(startScreenX, startScreenY, endScreenX, endScreenY, lineColor, 2);

            // Start point (filled circle)
            renderer.FillEllipse(startScreenX, startScreenY, 4, 4, dotColor);
            renderer.DrawEllipse(startScreenX, startScreenY, 4, 4, outlineColor, 1);

            // End point (empty circle)
            renderer.DrawEllipse(endScreenX, endScreenY, 4, 4, dotColor, 2);

            // Draw gradient type indicator
            DrawGradientTypeIndicator(renderer, dest, settings.GradientType, length);
        }

        private void DrawGradientTypeIndicator(ICanvasRenderer renderer, Rect dest, GradientType type, double length)
        {
            if (length < 10) return; // Too small to show indicator

            float centerX = (float)(dest.X + (_gradientStartX + _gradientEndX) / 2.0 * _zoom.Scale);
            float centerY = (float)(dest.Y + (_gradientStartY + _gradientEndY) / 2.0 * _zoom.Scale);
            var indicatorColor = Windows.UI.Color.FromArgb(150, 255, 255, 255);

            switch (type)
            {
                case GradientType.Radial:
                    // Draw concentric circles
                    renderer.DrawEllipse(centerX, centerY, 15, 15, indicatorColor, 1);
                    renderer.DrawEllipse(centerX, centerY, 10, 10, indicatorColor, 1);
                    renderer.DrawEllipse(centerX, centerY, 5, 5, indicatorColor, 1);
                    break;

                case GradientType.Angular:
                    // Draw angle sweep lines
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = (float)(i * Math.PI / 4);
                        float x1 = centerX + 5 * MathF.Cos(angle);
                        float y1 = centerY + 5 * MathF.Sin(angle);
                        float x2 = centerX + 15 * MathF.Cos(angle);
                        float y2 = centerY + 15 * MathF.Sin(angle);
                        renderer.DrawLine(x1, y1, x2, y2, indicatorColor, 1);
                    }
                    break;

                case GradientType.Diamond:
                    // Draw diamond shape
                    float size = 12;
                    renderer.DrawLine(centerX, centerY - size, centerX + size, centerY, indicatorColor, 1);
                    renderer.DrawLine(centerX + size, centerY, centerX, centerY + size, indicatorColor, 1);
                    renderer.DrawLine(centerX, centerY + size, centerX - size, centerY, indicatorColor, 1);
                    renderer.DrawLine(centerX - size, centerY, centerX, centerY - size, indicatorColor, 1);
                    break;

                    // Linear has no special indicator (line is sufficient)
            }
        }

        private GradientStop[] GetPreviewGradientStops(GradientFillToolSettings settings)
        {
            return settings.ColorMode switch
            {
                GradientColorMode.WhiteToBlack => new[]
                {
                    new GradientStop(0.0, 0xFFFFFFFF),
                    new GradientStop(1.0, 0xFF000000)
                },
                GradientColorMode.BlackToWhite => new[]
                {
                    new GradientStop(0.0, 0xFF000000),
                    new GradientStop(1.0, 0xFFFFFFFF)
                },
                GradientColorMode.ForegroundToBackground => new[]
                {
                    new GradientStop(0.0, _fg),
                    new GradientStop(1.0, _bgColor)
                },
                GradientColorMode.BackgroundToForeground => new[]
                {
                    new GradientStop(0.0, _bgColor),
                    new GradientStop(1.0, _fg)
                },
                GradientColorMode.Custom when settings.CustomStops.Count > 0 =>
                    GetCustomStopsArray(settings),
                _ => new[]
                {
                    new GradientStop(0.0, _fg),
                    new GradientStop(1.0, _bgColor)
                }
            };
        }

        private static GradientStop[] GetCustomStopsArray(GradientFillToolSettings settings)
        {
            var stops = new GradientStop[settings.CustomStops.Count];
            for (int i = 0; i < settings.CustomStops.Count; i++)
            {
                stops[i] = settings.CustomStops[i];
            }
            return stops;
        }
    }
}
