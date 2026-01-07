using System;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace PixlPunkt.Uno.UI.CanvasHost
{
    /// <summary>
    /// Viewport subsystem for CanvasViewHost:
    /// - Zoom in/out/actual
    /// - Pan navigation
    /// - Fit to screen
    /// - Viewport tracking and events
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ════════════════════════════════════════════════════════════════════
        // PUBLIC API - VIEW NAVIGATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Fits the document to the viewport with padding.</summary>
        public void Fit() => DoFit();

        /// <summary>
        /// Recenters the view so that the given document-space point appears near
        /// the center of the _mainCanvas.
        /// </summary>
        public void CenterOnDocumentPoint(double docX, double docY)
        {
            var screenPt = _zoom.DocToScreen(new Point(docX, docY));

            double centerX = _mainCanvas.ActualWidth * 0.5;
            double centerY = _mainCanvas.ActualHeight * 0.5;

            double dx = centerX - screenPt.X;
            double dy = centerY - screenPt.Y;

            _zoom.PanBy(dx, dy);
            UpdateViewport();
            InvalidateMainCanvas();
        }

        /// <summary>Resets the zoom to 1:1 (actual size).</summary>
        public void CanvasActualSize()
        {
            _zoom.Actual();
            UpdateViewport();
            ZoomLevel.Text = ZoomLevelText;
            InvalidateMainCanvas();
        }

        /// <summary>Toggles the pixel grid overlay.</summary>
        public void TogglePixelGrid()
        {
            _showPixelGrid = !_showPixelGrid;
            InvalidateMainCanvas();
        }

        /// <summary>Toggles the tile grid overlay.</summary>
        public void ToggleTileGrid()
        {
            _showTileGrid = !_showTileGrid;
            InvalidateMainCanvas();
        }

        /// <summary>Gets or sets whether tile mapping numbers are displayed on the canvas.</summary>
        public bool ShowTileMappings
        {
            get => _showTileMappings;
            set
            {
                if (_showTileMappings == value) return;
                _showTileMappings = value;
                InvalidateMainCanvas();
            }
        }
        private bool _showTileMappings = false;

        /// <summary>Enables space-bar panning mode.</summary>
        public void BeginSpacePan() => _spacePan = true;

        /// <summary>Disables space-bar panning mode.</summary>
        public void EndSpacePan() => _spacePan = false;

        // ════════════════════════════════════════════════════════════════════
        // ZOOM CONTROLS
        // ════════════════════════════════════════════════════════════════════

        public string ZoomLevelText => $"{_zoom.Scale * 100:0}%";

        public void ZoomIn()
        {
            _zoom.ZoomAt(_zoom.Offset, ZoomInFactor, MinZoomScale, MaxZoomScale);
            UpdateViewport();
            ZoomLevel.Text = ZoomLevelText;
            InvalidateMainCanvas();
        }

        public void ZoomOut()
        {
            _zoom.ZoomAt(_zoom.Offset, ZoomOutFactor, MinZoomScale, MaxZoomScale);
            UpdateViewport();
            ZoomLevel.Text = ZoomLevelText;
            InvalidateMainCanvas();
        }

        public void ZoomActual()
        {
        }

        // ════════════════════════════════════════════════════════════════════
        // BUTTON HANDLERS
        // ════════════════════════════════════════════════════════════════════

        public void FitToScreenBtn_Click(object sender, RoutedEventArgs e)
        {
            DoFit();
        }

        public void ActualSizeBtn_Click(object sender, RoutedEventArgs e)
        {
            CanvasActualSize();
        }

        public void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            ZoomOut();
        }

        public void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            ZoomIn();
        }

        // ════════════════════════════════════════════════════════════════════
        // VIEW FITTING
        // ════════════════════════════════════════════════════════════════════

        private void DoFit()
        {
            _zoom.SetViewportSize(_mainCanvas.ActualWidth, _mainCanvas.ActualHeight);
            _zoom.Fit(24);
            ZoomLevel.Text = ZoomLevelText;
            UpdateViewport();
            InvalidateMainCanvas();
        }

        // ════════════════════════════════════════════════════════════════════
        // VIEWPORT TRACKING
        // ════════════════════════════════════════════════════════════════════

        private Rect _currentViewport;

        /// <summary>
        /// Fired when the main view's visible document region changes (pan/zoom/resize).
        /// Rect is in document pixel coordinates.
        /// </summary>
        public event Action<Rect>? ViewportChanged;

        /// <summary>
        /// Current visible document rectangle in document pixel coordinates.
        /// </summary>
        public Rect CurrentViewport => _currentViewport;

        /// <summary>
        /// Updates the current visible document rectangle and raises ViewportChanged if it changed.
        /// Also invalidates rulers to keep them in sync.
        /// </summary>
        private void UpdateViewport()
        {
            if (Document.PixelWidth <= 0 || Document.PixelHeight <= 0 ||
                _mainCanvas.ActualWidth <= 0 || _mainCanvas.ActualHeight <= 0)
                return;

            // Get the document rectangle in screen coordinates
            var docScreen = _zoom.GetDestRect();
            // Get the view rectangle in screen coordinates
            var viewScreen = new Rect(0, 0, _mainCanvas.ActualWidth, _mainCanvas.ActualHeight);
            // Find the intersection (visible portion of document in screen space)
            var intersect = RectHelper.Intersect(docScreen, viewScreen);

            if (intersect.Width <= 0 || intersect.Height <= 0)
            {
                var empty = new Rect(0, 0, 0, 0);
                if (!empty.Equals(_currentViewport))
                {
                    _currentViewport = empty;
                    ViewportChanged?.Invoke(_currentViewport);
                    // Invalidate rulers when viewport changes
                    InvalidateRulers();
                }
                return;
            }

            // Convert screen coordinates back to document coordinates
            var topLeft = _zoom.ScreenToDoc(new Point(intersect.X, intersect.Y));
            var bottomRight = _zoom.ScreenToDoc(new Point(intersect.X + intersect.Width, intersect.Y + intersect.Height));

            // Clamp to document bounds
            double left = Math.Clamp(topLeft.X, 0, Document.PixelWidth);
            double top = Math.Clamp(topLeft.Y, 0, Document.PixelHeight);
            double right = Math.Clamp(bottomRight.X, 0, Document.PixelWidth);
            double bottom = Math.Clamp(bottomRight.Y, 0, Document.PixelHeight);

            var rect = new Rect(
                x: left,
                y: top,
                width: Math.Max(0, right - left),
                height: Math.Max(0, bottom - top));

            if (rect.Equals(_currentViewport))
                return;

            _currentViewport = rect;
            ViewportChanged?.Invoke(rect);
            
            // Invalidate rulers when viewport changes (pan/zoom)
            InvalidateRulers();
        }
    }
}
