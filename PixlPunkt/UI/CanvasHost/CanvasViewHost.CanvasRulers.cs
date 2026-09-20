using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Rendering;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// Rulers that live on the canvas itself, along the document's top and left edges, so they
    /// turn with a rotated view. The strip rulers in the layout are axis-aligned and can only
    /// describe an unrotated view, so while the view is rotated they go blank and these take
    /// over, including dragging a guide out of them. They are drawn in canvas-element space
    /// before the view transform, and pointer positions arrive in that same space, so the
    /// geometry needs no rotation maths of its own.
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        private const float CanvasRulerThickness = 20f;

        private static readonly Color CanvasRulerBgLight = Color.FromArgb(235, 245, 245, 245);
        private static readonly Color CanvasRulerBgDark = Color.FromArgb(235, 45, 45, 45);
        private static readonly Color CanvasRulerTickLight = Color.FromArgb(255, 80, 80, 80);
        private static readonly Color CanvasRulerTickDark = Color.FromArgb(255, 180, 180, 180);
        private static readonly Color CanvasRulerTextLight = Color.FromArgb(255, 60, 60, 60);
        private static readonly Color CanvasRulerTextDark = Color.FromArgb(255, 200, 200, 200);
        private static readonly Color CanvasRulerCursor = Color.FromArgb(100, 0, 120, 215);

        /// <summary>True while the view is rotated (or a rotate drag is in progress).</summary>
        private bool IsViewRotated => Math.Abs(Document.ViewRotationDeg) > 1e-9 || _viewRotateActive;

        /// <summary>The on-canvas rulers replace the strips while the view is rotated.</summary>
        private bool UseCanvasRulers => _showRulers && IsViewRotated;

        private Rect CanvasRulerTopBand(Rect dest) =>
            new(dest.X - CanvasRulerThickness, dest.Y - CanvasRulerThickness, dest.Width + CanvasRulerThickness, CanvasRulerThickness);

        private Rect CanvasRulerLeftBand(Rect dest) =>
            new(dest.X - CanvasRulerThickness, dest.Y, CanvasRulerThickness, dest.Height);

        // ─────────────────────────────────────────────────────────────────────
        // DRAWING
        // ─────────────────────────────────────────────────────────────────────

        private void DrawCanvasRulers(ICanvasRenderer renderer, Rect dest)
        {
            if (!UseCanvasRulers) return;

            bool dark = ActualTheme == ElementTheme.Dark;
            var bg = dark ? CanvasRulerBgDark : CanvasRulerBgLight;
            var tick = dark ? CanvasRulerTickDark : CanvasRulerTickLight;
            var text = dark ? CanvasRulerTextDark : CanvasRulerTextLight;
            float s = (float)_zoom.Scale;
            float t = CanvasRulerThickness;

            int tileW = Math.Max(1, Document.TileSize.Width);
            int tileH = Math.Max(1, Document.TileSize.Height);
            int majorX = CanvasRulerMajorInterval(_zoom.Scale, tileW);
            int majorY = CanvasRulerMajorInterval(_zoom.Scale, tileH);
            int minorX = Math.Max(1, majorX / 4);
            int minorY = Math.Max(1, majorY / 4);

            using var textFormat = renderer.CreateTextFormat("Segoe UI", 9f);

            // Top band (X axis)
            var top = CanvasRulerTopBand(dest);
            renderer.FillRectangle(top, bg);
            renderer.PushClip(top);
            if (_hoverValid && _hoverX >= 0 && _hoverX < Document.PixelWidth)
                renderer.FillRectangle((float)dest.X + _hoverX * s, (float)top.Y, s, t, CanvasRulerCursor);
            for (int x = 0; x <= Document.PixelWidth; x += minorX)
            {
                float sx = (float)dest.X + x * s;
                bool major = x % majorX == 0, atTile = x % tileW == 0;
                float h = major ? t * 0.6f : (atTile ? t * 0.4f : t * 0.25f);
                renderer.DrawLine(sx, (float)dest.Y - h, sx, (float)dest.Y, tick, 1f);
                if (major && _zoom.Scale >= 0.5)
                    renderer.DrawText(x.ToString(), sx + 2, (float)top.Y + 2, text, textFormat);
            }
            renderer.DrawLine((float)top.X, (float)dest.Y - 1, (float)(top.X + top.Width), (float)dest.Y - 1, tick, 1f);
            renderer.PopClip();

            // Left band (Y axis)
            var left = CanvasRulerLeftBand(dest);
            renderer.FillRectangle(left, bg);
            renderer.PushClip(left);
            if (_hoverValid && _hoverY >= 0 && _hoverY < Document.PixelHeight)
                renderer.FillRectangle((float)left.X, (float)dest.Y + _hoverY * s, t, s, CanvasRulerCursor);
            for (int y = 0; y <= Document.PixelHeight; y += minorY)
            {
                float sy = (float)dest.Y + y * s;
                bool major = y % majorY == 0, atTile = y % tileH == 0;
                float w = major ? t * 0.6f : (atTile ? t * 0.4f : t * 0.25f);
                renderer.DrawLine((float)dest.X - w, sy, (float)dest.X, sy, tick, 1f);
                if (major && _zoom.Scale >= 1.0)
                    renderer.DrawText(y.ToString(), (float)left.X + 2, sy + 1, text, textFormat);
            }
            renderer.DrawLine((float)dest.X - 1, (float)left.Y, (float)dest.X - 1, (float)(left.Y + left.Height), tick, 1f);
            renderer.PopClip();

            // Guide markers on the bands
            if (_guideService != null && _guideService.GuidesVisible)
            {
                var guideColor = Color.FromArgb(255, 0, 180, 255);
                foreach (var g in _guideService.HorizontalGuides)
                {
                    float sy = (float)(dest.Y + g.Position * s);
                    renderer.FillRectangle((float)left.X, sy - 1, t, 3, guideColor);
                }
                foreach (var g in _guideService.VerticalGuides)
                {
                    float sx = (float)(dest.X + g.Position * s);
                    renderer.FillRectangle(sx - 1, (float)top.Y, 3, t, guideColor);
                }
            }
        }

        private static int CanvasRulerMajorInterval(double scale, int tileSize)
        {
            double pixelsPerTile = tileSize * scale;
            if (pixelsPerTile >= 100) return tileSize;
            if (pixelsPerTile >= 50) return tileSize * 2;
            if (pixelsPerTile >= 25) return tileSize * 4;
            if (pixelsPerTile >= 12) return tileSize * 8;
            return tileSize * 16;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GUIDE DRAG FROM THE BANDS (same rules as the strip rulers)
        // ─────────────────────────────────────────────────────────────────────

        private bool _canvasRulerDragActive;

        private bool CanvasRulers_TryHandlePointerPressed(PointerRoutedEventArgs e)
        {
            if (!UseCanvasRulers || _guidesLocked || _guideService == null) return false;

            var pt = e.GetCurrentPoint(_mainCanvas);
            if (!pt.Properties.IsLeftButtonPressed) return false;
            var pos = pt.Position;
            var dest = _zoom.GetDestRect();

            if (CanvasRulerTopBand(dest).Contains(pos))
            {
                // Pull a horizontal guide down out of the top band.
                int docY = ViewYToDocY(pos.Y);
                _dragGuide = _guideService.FindGuideAt(docY, isHorizontal: true, threshold: (int)(4 / _zoom.Scale) + 1)
                             ?? _guideService.AddHorizontalGuide(0);
            }
            else if (CanvasRulerLeftBand(dest).Contains(pos))
            {
                int docX = ViewXToDocX(pos.X);
                _dragGuide = _guideService.FindGuideAt(docX, isHorizontal: false, threshold: (int)(4 / _zoom.Scale) + 1)
                             ?? _guideService.AddVerticalGuide(0);
            }
            else
            {
                return false;
            }

            _canvasRulerDragActive = true;
            _mainCanvas.CapturePointer(e.Pointer);
            return true;
        }

        private void CanvasRulers_HandlePointerMoved(PointerRoutedEventArgs e)
        {
            if (_dragGuide == null) { _canvasRulerDragActive = false; return; }
            var pos = e.GetCurrentPoint(_mainCanvas).Position;
            _dragGuide.Position = _dragGuide.IsHorizontal ? ViewYToDocY(pos.Y) : ViewXToDocX(pos.X);
            InvalidateMainCanvas();
        }

        private void CanvasRulers_HandlePointerReleased(PointerRoutedEventArgs e)
        {
            _canvasRulerDragActive = false;
            if (_dragGuide == null) return;

            var pos = e.GetCurrentPoint(_mainCanvas).Position;
            if (_dragGuide.IsHorizontal)
            {
                int docY = ViewYToDocY(pos.Y);
                if (docY < 0 || docY > Document.PixelHeight) _guideService?.RemoveGuide(_dragGuide);
            }
            else
            {
                int docX = ViewXToDocX(pos.X);
                if (docX < 0 || docX > Document.PixelWidth) _guideService?.RemoveGuide(_dragGuide);
            }

            _dragGuide = null;
            _mainCanvas.ReleasePointerCaptures();
            InvalidateMainCanvas();
        }
    }
}
