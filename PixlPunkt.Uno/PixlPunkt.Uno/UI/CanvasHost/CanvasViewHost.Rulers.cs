using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Guides;
using PixlPunkt.Uno.Core.Rendering;
using PixlPunkt.Uno.UI.CanvasHost.Rulers;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.Uno.UI.CanvasHost
{
    /// <summary>
    /// Ruler and guide management for CanvasViewHost.
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ====================================================================
        // RULER STATE
        // ====================================================================

        private GuideService? _guideService;
        private bool _showRulers = true;
        private Guide? _dragGuide;

        private Guide? _hoveredGuide;
        private bool _isDraggingGuideOnCanvas;

        private bool _guidesLocked = false;

        private static readonly Color RulerHiddenBackground = Color.FromArgb(255, 24, 24, 24);

        private const float GuideHitThreshold = 6f;

        /// <summary>Gets or sets whether rulers are visible.</summary>
        public bool ShowRulers
        {
            get => _showRulers;
            set
            {
                if (_showRulers == value) return;
                _showRulers = value;
                UpdateRulerVisibility();
            }
        }

        /// <summary>Gets or sets whether guides are visible.</summary>
        public bool ShowGuides
        {
            get => _guideService?.GuidesVisible ?? true;
            set
            {
                if (_guideService != null)
                {
                    _guideService.GuidesVisible = value;
                    CanvasView.Invalidate();
                }
            }
        }

        /// <summary>Gets or sets whether snap-to-guides is enabled.</summary>
        public bool SnapToGuides
        {
            get => _guideService?.SnapToGuides ?? true;
            set
            {
                if (_guideService != null)
                {
                    _guideService.SnapToGuides = value;
                    UpdateSnapIndicator();
                }
            }
        }

        /// <summary>Gets or sets whether guides are locked (cannot be moved/deleted).</summary>
        public bool GuidesLocked
        {
            get => _guidesLocked;
            set
            {
                if (_guidesLocked == value) return;
                _guidesLocked = value;

                if (_guidesLocked && _hoveredGuide != null)
                {
                    _hoveredGuide.IsSelected = false;
                    _hoveredGuide = null;
                    CanvasView.Invalidate();
                }

                UpdateLockGuidesIndicator();
            }
        }

        /// <summary>Gets the guide service for this document.</summary>
        public GuideService? GuideService => _guideService;

        // ====================================================================
        // INITIALIZATION
        // ====================================================================

        private void InitRulers()
        {
            _guideService = new GuideService();
            _guideService.GuidesChanged += OnGuidesChanged;

            UpdateRulerVisibility();
            UpdateSnapIndicator();
            UpdateLockGuidesIndicator();
        }

        private void UpdateRulerVisibility()
        {
            if (RulerCorner != null)
            {
                if (_showRulers)
                {
                    if (Application.Current.Resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out var bgBrush))
                        RulerCorner.Background = bgBrush as Microsoft.UI.Xaml.Media.Brush;
                    if (Application.Current.Resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out var borderBrush))
                        RulerCorner.BorderBrush = borderBrush as Microsoft.UI.Xaml.Media.Brush;
                    RulerCorner.BorderThickness = new Microsoft.UI.Xaml.Thickness(0, 0, 1, 1);
                }
                else
                {
                    if (Application.Current.Resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out var bgBrush))
                        RulerCorner.Background = bgBrush as Microsoft.UI.Xaml.Media.Brush;
                    RulerCorner.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                }
            }

            HorizontalRulerCanvas.Invalidate();
            VerticalRulerCanvas.Invalidate();
        }

        private void UpdateSnapIndicator()
        {
            var snapIndicator = FindName("SnapIndicator") as Microsoft.UI.Xaml.Controls.Border;
            var snapIcon = FindName("SnapIcon") as FluentIcons.WinUI.FluentIcon;
            var snapText = FindName("SnapText") as Microsoft.UI.Xaml.Controls.TextBlock;

            if (snapIndicator == null || snapIcon == null || snapText == null)
                return;

            bool isEnabled = _guideService?.SnapToGuides ?? false;

            if (isEnabled)
            {
                snapIndicator.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 0, 100, 140));
                var accentFg = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 100, 220, 255));
                snapIcon.Foreground = accentFg;
                snapText.Foreground = accentFg;
            }
            else
            {
                if (Application.Current.Resources.TryGetValue("ControlFillColorDefaultBrush", out var bgBrush))
                    snapIndicator.Background = bgBrush as Microsoft.UI.Xaml.Media.Brush;
                if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var fgBrush))
                {
                    snapIcon.Foreground = fgBrush as Microsoft.UI.Xaml.Media.Brush;
                    snapText.Foreground = fgBrush as Microsoft.UI.Xaml.Media.Brush;
                }
            }
        }

        private void UpdateLockGuidesIndicator()
        {
            var lockIndicator = FindName("LockGuidesIndicator") as Microsoft.UI.Xaml.Controls.Border;
            var lockIcon = FindName("LockGuidesIcon") as FluentIcons.WinUI.FluentIcon;
            var lockText = FindName("LockGuidesText") as Microsoft.UI.Xaml.Controls.TextBlock;

            if (lockIndicator == null || lockIcon == null || lockText == null)
                return;

            if (_guidesLocked)
            {
                lockIndicator.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 140, 100, 0));
                var accentFg = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 255, 200, 100));
                lockIcon.Icon = FluentIcons.Common.Icon.LockClosed;
                lockIcon.Foreground = accentFg;
                lockText.Foreground = accentFg;
            }
            else
            {
                if (Application.Current.Resources.TryGetValue("ControlFillColorDefaultBrush", out var bgBrush))
                    lockIndicator.Background = bgBrush as Microsoft.UI.Xaml.Media.Brush;
                lockIcon.Icon = FluentIcons.Common.Icon.LockOpen;
                if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var fgBrush))
                {
                    lockIcon.Foreground = fgBrush as Microsoft.UI.Xaml.Media.Brush;
                    lockText.Foreground = fgBrush as Microsoft.UI.Xaml.Media.Brush;
                }
            }
        }

        private void SnapIndicator_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            SnapToGuides = !SnapToGuides;
            e.Handled = true;
        }

        private void LockGuidesIndicator_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            GuidesLocked = !GuidesLocked;
            e.Handled = true;
        }

        private void OnGuidesChanged()
        {
            CanvasView.Invalidate();
            HorizontalRulerCanvas.Invalidate();
            VerticalRulerCanvas.Invalidate();
        }

        // ====================================================================
        // CANVAS GUIDE INTERACTION
        // ====================================================================

        private Guide? FindGuideAtScreenPosition(Point screenPos)
        {
            if (_guideService == null || !_guideService.GuidesVisible || _guidesLocked)
                return null;

            var dest = _zoom.GetDestRect();
            float scale = (float)_zoom.Scale;

            foreach (var guide in _guideService.HorizontalGuides)
            {
                float screenY = (float)(dest.Y + guide.Position * scale);
                if (Math.Abs(screenPos.Y - screenY) <= GuideHitThreshold)
                    return guide;
            }

            foreach (var guide in _guideService.VerticalGuides)
            {
                float screenX = (float)(dest.X + guide.Position * scale);
                if (Math.Abs(screenPos.X - screenX) <= GuideHitThreshold)
                    return guide;
            }

            return null;
        }

        private bool Guide_TryHandlePointerMoved(PointerRoutedEventArgs e)
        {
            if (_guidesLocked)
                return false;

            if (_guideService == null || !_guideService.GuidesVisible)
                return false;

            if (_isDraggingGuideOnCanvas && _dragGuide != null)
            {
                var pos = e.GetCurrentPoint(CanvasView).Position;

                if (_dragGuide.IsHorizontal)
                {
                    _dragGuide.Position = ViewYToDocY(pos.Y);
                }
                else
                {
                    _dragGuide.Position = ViewXToDocX(pos.X);
                }

                CanvasView.Invalidate();
                return true;
            }

            var currentPos = e.GetCurrentPoint(CanvasView).Position;
            var newHovered = FindGuideAtScreenPosition(currentPos);

            if (newHovered != _hoveredGuide)
            {
                if (_hoveredGuide != null)
                    _hoveredGuide.IsSelected = false;

                _hoveredGuide = newHovered;

                if (_hoveredGuide != null)
                    _hoveredGuide.IsSelected = true;

                CanvasView.Invalidate();
            }

            return false;
        }

        private bool Guide_TryHandlePointerPressed(PointerRoutedEventArgs e)
        {
            if (_guidesLocked)
                return false;

            if (_guideService == null || !_guideService.GuidesVisible)
                return false;

            var pos = e.GetCurrentPoint(CanvasView).Position;
            var props = e.GetCurrentPoint(CanvasView).Properties;
            var guide = FindGuideAtScreenPosition(pos);

            if (guide == null)
                return false;

            if (props.IsRightButtonPressed)
            {
                _guideService.RemoveGuide(guide);
                _hoveredGuide = null;
                CanvasView.Invalidate();
                return true;
            }

            if (props.IsLeftButtonPressed)
            {
                _dragGuide = guide;
                _isDraggingGuideOnCanvas = true;
                CanvasView.CapturePointer(e.Pointer);
                return true;
            }

            return false;
        }

        private bool Guide_TryHandlePointerReleased(PointerRoutedEventArgs e)
        {
            if (!_isDraggingGuideOnCanvas || _dragGuide == null)
                return false;

            var pos = e.GetCurrentPoint(CanvasView).Position;

            if (_dragGuide.IsHorizontal)
            {
                int docY = ViewYToDocY(pos.Y);
                if (docY < 0 || docY > Document.PixelHeight)
                {
                    _guideService?.RemoveGuide(_dragGuide);
                }
            }
            else
            {
                int docX = ViewXToDocX(pos.X);
                if (docX < 0 || docX > Document.PixelWidth)
                {
                    _guideService?.RemoveGuide(_dragGuide);
                }
            }

            _dragGuide = null;
            _isDraggingGuideOnCanvas = false;
            CanvasView.ReleasePointerCaptures();
            CanvasView.Invalidate();
            return true;
        }

        private InputSystemCursorShape? GetGuideCursor()
        {
            if (_guidesLocked)
                return null;

            if (_isDraggingGuideOnCanvas && _dragGuide != null)
            {
                return _dragGuide.IsHorizontal
                    ? InputSystemCursorShape.SizeNorthSouth
                    : InputSystemCursorShape.SizeWestEast;
            }

            if (_hoveredGuide != null)
            {
                return _hoveredGuide.IsHorizontal
                    ? InputSystemCursorShape.SizeNorthSouth
                    : InputSystemCursorShape.SizeWestEast;
            }

            return null;
        }

        // ====================================================================
        // RULER DRAWING HELPERS
        // ====================================================================

        private void HorizontalRuler_Draw(ICanvasRenderer renderer)
        {
            var clearColor = GetThemeClearColor();

            if (!_showRulers)
            {
                renderer.Clear(clearColor);
                return;
            }

            renderer.Clear(Color.FromArgb(0, 0, 0, 0));

            // Don't draw if layout hasn't happened yet
            if (CanvasView.ActualWidth <= 0 || CanvasView.ActualHeight <= 0)
                return;

            // Get the dest rect from zoom controller (in logical pixels relative to CanvasView)
            var dest = _zoom.GetDestRect();
            int docWidth = Document.PixelWidth;
            int tileWidth = Document.TileSize.Width;
            if (tileWidth <= 0) tileWidth = 16;

            int? cursorDocX = _hoverValid ? _hoverX : null;

            // Draw using the RulerRenderer directly with SKCanvas
            if (renderer.Device is SkiaSharp.SKCanvas canvas)
            {
                // Get the canvas total matrix to determine any DPI scaling
                var matrix = canvas.TotalMatrix;
                float dpiScaleX = matrix.ScaleX;
                
                // The ruler canvas dimensions in physical pixels
                float rulerWidth = renderer.Width;
                
                // Scale the dest rect and zoom to account for DPI
                // If dpiScaleX is not 1, it means the canvas is scaled
                if (dpiScaleX > 0 && Math.Abs(dpiScaleX - 1.0f) > 0.01f)
                {
                    var scaledDest = new Rect(
                        dest.X * dpiScaleX, 
                        dest.Y * dpiScaleX, 
                        dest.Width * dpiScaleX, 
                        dest.Height * dpiScaleX);
                    RulerRenderer.DrawHorizontalRuler(canvas, scaledDest, _zoom.Scale * dpiScaleX, docWidth, tileWidth, 0, rulerWidth, cursorDocX, ActualTheme);
                }
                else
                {
                    RulerRenderer.DrawHorizontalRuler(canvas, dest, _zoom.Scale, docWidth, tileWidth, 0, rulerWidth, cursorDocX, ActualTheme);
                }
            }
        }

        private void VerticalRuler_Draw(ICanvasRenderer renderer)
        {
            var clearColor = GetThemeClearColor();

            if (!_showRulers)
            {
                renderer.Clear(clearColor);
                return;
            }

            renderer.Clear(Color.FromArgb(0, 0, 0, 0));

            // Don't draw if layout hasn't happened yet
            if (CanvasView.ActualWidth <= 0 || CanvasView.ActualHeight <= 0)
                return;

            // Get the dest rect from zoom controller (in logical pixels relative to CanvasView)
            var dest = _zoom.GetDestRect();
            int docHeight = Document.PixelHeight;
            int tileHeight = Document.TileSize.Height;
            if (tileHeight <= 0) tileHeight = 16;

            int? cursorDocY = _hoverValid ? _hoverY : null;

            // Draw using the RulerRenderer directly with SKCanvas
            if (renderer.Device is SkiaSharp.SKCanvas canvas)
            {
                // Get the canvas total matrix to determine any DPI scaling
                var matrix = canvas.TotalMatrix;
                float dpiScaleY = matrix.ScaleY;
                
                // The ruler canvas dimensions in physical pixels
                float rulerHeight = renderer.Height;
                
                // Scale the dest rect and zoom to account for DPI
                // If dpiScaleY is not 1, it means the canvas is scaled
                if (dpiScaleY > 0 && Math.Abs(dpiScaleY - 1.0f) > 0.01f)
                {
                    var scaledDest = new Rect(
                        dest.X * dpiScaleY, 
                        dest.Y * dpiScaleY, 
                        dest.Width * dpiScaleY, 
                        dest.Height * dpiScaleY);
                    RulerRenderer.DrawVerticalRuler(canvas, scaledDest, _zoom.Scale * dpiScaleY, docHeight, tileHeight, 0, rulerHeight, cursorDocY, ActualTheme);
                }
                else
                {
                    RulerRenderer.DrawVerticalRuler(canvas, dest, _zoom.Scale, docHeight, tileHeight, 0, rulerHeight, cursorDocY, ActualTheme);
                }
            }
        }

        // ====================================================================
        // HORIZONTAL RULER INTERACTION
        // ====================================================================

        private void HorizontalRuler_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_guidesLocked || _guideService == null || !_showRulers) return;

            var pos = e.GetCurrentPoint(HorizontalRulerCanvas).Position;
            int docY = ScreenYToDocYFromHorizontalRuler(pos.Y);

            _dragGuide = _guideService.FindGuideAt(docY, isHorizontal: true, threshold: (int)(4 / _zoom.Scale) + 1);

            if (_dragGuide == null)
            {
                _dragGuide = _guideService.AddHorizontalGuide(0);
            }

            HorizontalRulerCanvas.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void HorizontalRuler_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_dragGuide == null) return;

            var pos = e.GetCurrentPoint(CanvasView).Position;
            int docY = ViewYToDocY(pos.Y);

            _dragGuide.Position = docY;
            CanvasView.Invalidate();
            HorizontalRulerCanvas.Invalidate();
            e.Handled = true;
        }

        private void HorizontalRuler_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_dragGuide == null) return;

            var pos = e.GetCurrentPoint(CanvasView).Position;
            int docY = ViewYToDocY(pos.Y);

            if (docY < 0 || docY > Document.PixelHeight)
            {
                _guideService?.RemoveGuide(_dragGuide);
            }

            _dragGuide = null;
            HorizontalRulerCanvas.ReleasePointerCaptures();
            e.Handled = true;
        }

        // ====================================================================
        // VERTICAL RULER INTERACTION
        // ====================================================================

        private void VerticalRuler_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_guidesLocked || _guideService == null || !_showRulers) return;

            var pos = e.GetCurrentPoint(VerticalRulerCanvas).Position;
            int docX = ScreenXToDocXFromVerticalRuler(pos.X);

            _dragGuide = _guideService.FindGuideAt(docX, isHorizontal: false, threshold: (int)(4 / _zoom.Scale) + 1);

            if (_dragGuide == null)
            {
                _dragGuide = _guideService.AddVerticalGuide(0);
            }

            VerticalRulerCanvas.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void VerticalRuler_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_dragGuide == null) return;

            var pos = e.GetCurrentPoint(CanvasView).Position;
            int docX = ViewXToDocX(pos.X);

            _dragGuide.Position = docX;
            CanvasView.Invalidate();
            VerticalRulerCanvas.Invalidate();
            e.Handled = true;
        }

        private void VerticalRuler_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_dragGuide == null) return;

            var pos = e.GetCurrentPoint(CanvasView).Position;
            int docX = ViewXToDocX(pos.X);

            if (docX < 0 || docX > Document.PixelWidth)
            {
                _guideService?.RemoveGuide(_dragGuide);
            }

            _dragGuide = null;
            VerticalRulerCanvas.ReleasePointerCaptures();
            e.Handled = true;
        }

        // ====================================================================
        // COORDINATE CONVERSION FOR RULERS
        // ====================================================================

        private int ScreenXToDocXFromVerticalRuler(double screenX)
        {
            var dest = _zoom.GetDestRect();
            double docX = (screenX - dest.X) / _zoom.Scale;
            return (int)Math.Round(docX);
        }

        private int ScreenYToDocYFromHorizontalRuler(double screenY)
        {
            var dest = _zoom.GetDestRect();
            double docY = (screenY - dest.Y) / _zoom.Scale;
            return (int)Math.Round(docY);
        }

        private int ViewXToDocX(double viewX)
        {
            var dest = _zoom.GetDestRect();
            double docX = (viewX - dest.X) / _zoom.Scale;
            return (int)Math.Round(docX);
        }

        private int ViewYToDocY(double viewY)
        {
            var dest = _zoom.GetDestRect();
            double docY = (viewY - dest.Y) / _zoom.Scale;
            return (int)Math.Round(docY);
        }

        private int ScreenXToDocX(double screenX)
        {
            var dest = _zoom.GetDestRect();
            double docX = (screenX - dest.X) / _zoom.Scale;
            return (int)Math.Round(docX);
        }

        private int ScreenYToDocY(double screenY)
        {
            var dest = _zoom.GetDestRect();
            double docY = (screenY - dest.Y) / _zoom.Scale;
            return (int)Math.Round(docY);
        }

        // ====================================================================
        // GUIDE MANAGEMENT PUBLIC API
        // ====================================================================

        public void ClearAllGuides() => _guideService?.ClearAllGuides();

        public void AddVerticalGuide(int x) => _guideService?.AddVerticalGuide(x);

        public void AddHorizontalGuide(int y) => _guideService?.AddHorizontalGuide(y);
    }
}
