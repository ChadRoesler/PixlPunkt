using System;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Guides;
using PixlPunkt.UI.CanvasHost.Rulers;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// Ruler and guide management for CanvasViewHost.
    /// - Draws horizontal and vertical rulers with tile-based tick marks
    /// - Supports guide creation by dragging from rulers
    /// - Supports guide manipulation on canvas (move with LMB, delete with RMB)
    /// - Manages guide visibility and snap settings
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ====================================================================
        // RULER STATE
        // ====================================================================

        private GuideService? _guideService;
        private bool _showRulers = true;
        private Guide? _dragGuide;

        // Guide hover state for canvas interaction
        private Guide? _hoveredGuide;
        private bool _isDraggingGuideOnCanvas;

        // Guide lock state - prevents interaction when locked
        private bool _guidesLocked = false;

        // Background color for non-ruler area (matches canvas outside fill)
        private static readonly Color RulerHiddenBackground = Color.FromArgb(255, 24, 24, 24);

        // Guide hit threshold in screen pixels
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

                // Clear any hover state when locking
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

            // Wire up ruler draw events
            HorizontalRulerCanvas.Draw += HorizontalRulerCanvas_Draw;
            VerticalRulerCanvas.Draw += VerticalRulerCanvas_Draw;

            UpdateRulerVisibility();
            UpdateSnapIndicator();
            UpdateLockGuidesIndicator();
        }

        private void UpdateRulerVisibility()
        {
            // Update corner background based on ruler visibility
            if (RulerCorner != null)
            {
                if (_showRulers)
                {
                    // Use theme-aware resources for ruler corner
                    if (Application.Current.Resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out var bgBrush))
                        RulerCorner.Background = bgBrush as Microsoft.UI.Xaml.Media.Brush;
                    if (Application.Current.Resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out var borderBrush))
                        RulerCorner.BorderBrush = borderBrush as Microsoft.UI.Xaml.Media.Brush;
                    RulerCorner.BorderThickness = new Microsoft.UI.Xaml.Thickness(0, 0, 1, 1);
                }
                else
                {
                    // Use theme-aware background when hidden
                    if (Application.Current.Resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out var bgBrush))
                        RulerCorner.Background = bgBrush as Microsoft.UI.Xaml.Media.Brush;
                    RulerCorner.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                }
            }

            // The rulers are always in the layout but we can skip drawing
            HorizontalRulerCanvas.Invalidate();
            VerticalRulerCanvas.Invalidate();
        }

        /// <summary>
        /// Updates the snap indicator visual state.
        /// </summary>
        private void UpdateSnapIndicator()
        {
            // Find controls by name since they're defined in XAML
            var snapIndicator = FindName("SnapIndicator") as Microsoft.UI.Xaml.Controls.Border;
            var snapIcon = FindName("SnapIcon") as FluentIcons.WinUI.FluentIcon;
            var snapText = FindName("SnapText") as Microsoft.UI.Xaml.Controls.TextBlock;

            if (snapIndicator == null || snapIcon == null || snapText == null)
                return;

            bool isEnabled = _guideService?.SnapToGuides ?? false;

            if (isEnabled)
            {
                // Active state - use accent color
                snapIndicator.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 0, 100, 140));
                var accentFg = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 100, 220, 255));
                snapIcon.Foreground = accentFg;
                snapText.Foreground = accentFg;
            }
            else
            {
                // Inactive state - use theme resource (will adapt to light/dark)
                if (Application.Current.Resources.TryGetValue("ControlFillColorDefaultBrush", out var bgBrush))
                    snapIndicator.Background = bgBrush as Microsoft.UI.Xaml.Media.Brush;
                if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var fgBrush))
                {
                    snapIcon.Foreground = fgBrush as Microsoft.UI.Xaml.Media.Brush;
                    snapText.Foreground = fgBrush as Microsoft.UI.Xaml.Media.Brush;
                }
            }
        }

        /// <summary>
        /// Updates the lock guides indicator visual state.
        /// </summary>
        private void UpdateLockGuidesIndicator()
        {
            var lockIndicator = FindName("LockGuidesIndicator") as Microsoft.UI.Xaml.Controls.Border;
            var lockIcon = FindName("LockGuidesIcon") as FluentIcons.WinUI.FluentIcon;
            var lockText = FindName("LockGuidesText") as Microsoft.UI.Xaml.Controls.TextBlock;

            if (lockIndicator == null || lockIcon == null || lockText == null)
                return;

            if (_guidesLocked)
            {
                // Locked state - orange/yellow accent
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
                // Unlocked state - use theme resource (will adapt to light/dark)
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

        /// <summary>
        /// Handles clicking the snap indicator to toggle snap-to-guides.
        /// </summary>
        private void SnapIndicator_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            SnapToGuides = !SnapToGuides;
            e.Handled = true;
        }

        /// <summary>
        /// Handles clicking the lock guides indicator to toggle guide locking.
        /// </summary>
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
        // CANVAS GUIDE INTERACTION (move/delete guides on canvas)
        // ====================================================================

        /// <summary>
        /// Finds a guide at the given screen position on the canvas.
        /// </summary>
        private Guide? FindGuideAtScreenPosition(Point screenPos)
        {
            if (_guideService == null || !_guideService.GuidesVisible || _guidesLocked)
                return null;

            var dest = _zoom.GetDestRect();
            float scale = (float)_zoom.Scale;

            // Check horizontal guides
            foreach (var guide in _guideService.HorizontalGuides)
            {
                float screenY = (float)(dest.Y + guide.Position * scale);
                if (Math.Abs(screenPos.Y - screenY) <= GuideHitThreshold)
                    return guide;
            }

            // Check vertical guides
            foreach (var guide in _guideService.VerticalGuides)
            {
                float screenX = (float)(dest.X + guide.Position * scale);
                if (Math.Abs(screenPos.X - screenX) <= GuideHitThreshold)
                    return guide;
            }

            return null;
        }

        /// <summary>
        /// Handles guide hover detection during pointer move on canvas.
        /// Call this from the main pointer move handler.
        /// Returns true if guide interaction consumed the event.
        /// </summary>
        private bool Guide_TryHandlePointerMoved(PointerRoutedEventArgs e)
        {
            // Skip all guide interaction if locked
            if (_guidesLocked)
                return false;

            if (_guideService == null || !_guideService.GuidesVisible)
                return false;

            // If we're actively dragging a guide, update its position
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

            // Check for hover (only when not dragging something else)
            var currentPos = e.GetCurrentPoint(CanvasView).Position;
            var newHovered = FindGuideAtScreenPosition(currentPos);

            if (newHovered != _hoveredGuide)
            {
                // Update selection state for visual feedback
                if (_hoveredGuide != null)
                    _hoveredGuide.IsSelected = false;

                _hoveredGuide = newHovered;

                if (_hoveredGuide != null)
                    _hoveredGuide.IsSelected = true;

                CanvasView.Invalidate();
            }

            return false;
        }

        /// <summary>
        /// Handles guide interaction on pointer pressed.
        /// Returns true if a guide was clicked and the event was handled.
        /// </summary>
        private bool Guide_TryHandlePointerPressed(PointerRoutedEventArgs e)
        {
            // Skip all guide interaction if locked
            if (_guidesLocked)
                return false;

            if (_guideService == null || !_guideService.GuidesVisible)
                return false;

            var pos = e.GetCurrentPoint(CanvasView).Position;
            var props = e.GetCurrentPoint(CanvasView).Properties;
            var guide = FindGuideAtScreenPosition(pos);

            if (guide == null)
                return false;

            // Right-click to delete
            if (props.IsRightButtonPressed)
            {
                _guideService.RemoveGuide(guide);
                _hoveredGuide = null;
                CanvasView.Invalidate();
                return true;
            }

            // Left-click to start dragging
            if (props.IsLeftButtonPressed)
            {
                _dragGuide = guide;
                _isDraggingGuideOnCanvas = true;
                CanvasView.CapturePointer(e.Pointer);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles guide interaction on pointer released.
        /// Returns true if a guide drag was completed.
        /// </summary>
        private bool Guide_TryHandlePointerReleased(PointerRoutedEventArgs e)
        {
            if (!_isDraggingGuideOnCanvas || _dragGuide == null)
                return false;

            var pos = e.GetCurrentPoint(CanvasView).Position;

            // Check if guide was dragged off canvas - delete it
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

        /// <summary>
        /// Gets the appropriate cursor for guide interaction.
        /// Returns null if no guide-specific cursor should be shown.
        /// </summary>
        private InputSystemCursorShape? GetGuideCursor()
        {
            // No guide cursor when guides are locked
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
        // RULER DRAWING
        // ====================================================================

        private void HorizontalRulerCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;

            // When rulers are hidden, show the dark background color
            if (!_showRulers)
            {
                var clearColor = GetThemeClearColor();
                ds.Clear(clearColor);
                return;
            }

            ds.Clear(Microsoft.UI.Colors.Transparent);

            var dest = _zoom.GetDestRect();
            int docWidth = Document.PixelWidth;
            int tileWidth = Document.TileSize.Width;
            if (tileWidth <= 0) tileWidth = 16; // Default fallback

            // Get cursor position for highlight
            int? cursorDocX = _hoverValid ? _hoverX : null;

            // Draw ruler marks - pass the actual canvas dest position for proper alignment
            float rulerWidth = (float)sender.ActualWidth;
            RulerRenderer.DrawHorizontalRuler(ds, dest, _zoom.Scale, docWidth, tileWidth, 0, rulerWidth, cursorDocX, ActualTheme);
        }

        private void VerticalRulerCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;

            // When rulers are hidden, show the dark background color
            if (!_showRulers)
            {
                var clearColor = GetThemeClearColor();
                ds.Clear(clearColor);
                return;
            }

            ds.Clear(Microsoft.UI.Colors.Transparent);

            var dest = _zoom.GetDestRect();
            int docHeight = Document.PixelHeight;
            int tileHeight = Document.TileSize.Height;
            if (tileHeight <= 0) tileHeight = 16; // Default fallback

            // Get cursor position for highlight
            int? cursorDocY = _hoverValid ? _hoverY : null;

            // Draw ruler marks - pass the actual canvas dest position for proper alignment
            float rulerHeight = (float)sender.ActualHeight;
            RulerRenderer.DrawVerticalRuler(ds, dest, _zoom.Scale, docHeight, tileHeight, 0, rulerHeight, cursorDocY, ActualTheme);
        }

        // ====================================================================
        // HORIZONTAL RULER INTERACTION (creates horizontal guides)
        // ====================================================================

        private void HorizontalRuler_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_guideService == null || !_showRulers) return;

            var pos = e.GetCurrentPoint(HorizontalRulerCanvas).Position;
            int docY = ScreenYToDocYFromHorizontalRuler(pos.Y);

            // Check if clicking on existing guide
            _dragGuide = _guideService.FindGuideAt(docY, isHorizontal: true, threshold: (int)(4 / _zoom.Scale) + 1);

            if (_dragGuide == null)
            {
                // Create new horizontal guide (dragged from top ruler)
                _dragGuide = _guideService.AddHorizontalGuide(0); // Start at top, will update on move
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

            // If dragged off canvas (back into ruler area or past bottom), delete the guide
            if (docY < 0 || docY > Document.PixelHeight)
            {
                _guideService?.RemoveGuide(_dragGuide);
            }

            _dragGuide = null;
            HorizontalRulerCanvas.ReleasePointerCaptures();
            e.Handled = true;
        }

        // ====================================================================
        // VERTICAL RULER INTERACTION (creates vertical guides)
        // ====================================================================

        private void VerticalRuler_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_guideService == null || !_showRulers) return;

            var pos = e.GetCurrentPoint(VerticalRulerCanvas).Position;
            int docX = ScreenXToDocXFromVerticalRuler(pos.X);

            // Check if clicking on existing guide
            _dragGuide = _guideService.FindGuideAt(docX, isHorizontal: false, threshold: (int)(4 / _zoom.Scale) + 1);

            if (_dragGuide == null)
            {
                // Create new vertical guide (dragged from left ruler)
                _dragGuide = _guideService.AddVerticalGuide(0); // Start at left, will update on move
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

            // If dragged off canvas (back into ruler area or past right), delete the guide
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

        /// <summary>
        /// Converts screen X from vertical ruler to document X coordinate.
        /// </summary>
        private int ScreenXToDocXFromVerticalRuler(double screenX)
        {
            var dest = _zoom.GetDestRect();
            double docX = (screenX - dest.X) / _zoom.Scale;
            return (int)Math.Round(docX);
        }

        /// <summary>
        /// Converts screen Y from horizontal ruler to document Y coordinate.
        /// </summary>
        private int ScreenYToDocYFromHorizontalRuler(double screenY)
        {
            var dest = _zoom.GetDestRect();
            double docY = (screenY - dest.Y) / _zoom.Scale;
            return (int)Math.Round(docY);
        }

        /// <summary>
        /// Converts view X to document X coordinate (for main canvas).
        /// </summary>
        private int ViewXToDocX(double viewX)
        {
            var dest = _zoom.GetDestRect();
            double docX = (viewX - dest.X) / _zoom.Scale;
            return (int)Math.Round(docX);
        }

        /// <summary>
        /// Converts view Y to document Y coordinate (for main canvas).
        /// </summary>
        private int ViewYToDocY(double viewY)
        {
            var dest = _zoom.GetDestRect();
            double docY = (viewY - dest.Y) / _zoom.Scale;
            return (int)Math.Round(docY);
        }

        /// <summary>
        /// Converts ruler screen X to document X coordinate.
        /// </summary>
        private int ScreenXToDocX(double screenX)
        {
            var dest = _zoom.GetDestRect();
            double docX = (screenX - dest.X) / _zoom.Scale;
            return (int)Math.Round(docX);
        }

        /// <summary>
        /// Converts ruler screen Y to document Y coordinate.
        /// </summary>
        private int ScreenYToDocY(double screenY)
        {
            var dest = _zoom.GetDestRect();
            double docY = (screenY - dest.Y) / _zoom.Scale;
            return (int)Math.Round(docY);
        }

        // ====================================================================
        // GUIDE MANAGEMENT PUBLIC API
        // ====================================================================

        /// <summary>
        /// Clears all guides.
        /// </summary>
        public void ClearAllGuides() => _guideService?.ClearAllGuides();

        /// <summary>
        /// Adds a vertical guide at the specified document X position.
        /// </summary>
        public void AddVerticalGuide(int x) => _guideService?.AddVerticalGuide(x);

        /// <summary>
        /// Adds a horizontal guide at the specified document Y position.
        /// </summary>
        public void AddHorizontalGuide(int y) => _guideService?.AddHorizontalGuide(y);
    }
}
