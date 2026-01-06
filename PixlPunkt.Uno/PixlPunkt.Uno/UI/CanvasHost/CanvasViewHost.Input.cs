using System;
using System.Linq;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Animation;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.Core.Tools.Utility;
using Windows.Foundation;

namespace PixlPunkt.Uno.UI.CanvasHost
{
    /// <summary>
    /// Input handling subsystem for CanvasViewHost:
    /// - Pointer event handlers (pressed, moved, released)
    /// - Mouse wheel handling
    /// - Keyboard state helpers
    /// - Cursor management
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ════════════════════════════════════════════════════════════════════
        // SYMMETRY AXIS INTERACTION STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Whether we're currently dragging a symmetry axis line.</summary>
        private bool _symmetryAxisDragging;

        /// <summary>Which axis component is being dragged: 'x' for horizontal axis, 'y' for vertical, 'c' for center point.</summary>
        private char _symmetryAxisDragType;

        // ════════════════════════════════════════════════════════════════════
        // CANVAS EVENT WIRING
        // ════════════════════════════════════════════════════════════════════

        private void WireCanvasEvents()
        {
            CanvasView.Loaded += (_, __) => DoFit();

            CanvasView.SizeChanged += (_, __) =>
            {
                _zoom.SetViewportSize(CanvasView.ActualWidth, CanvasView.ActualHeight);
                UpdateViewport();
                ZoomLevel.Text = ZoomLevelText;
                CanvasView.Invalidate();
            };

            // SkiaSharp PaintSurface is wired in XAML via PaintSurface="CanvasView_PaintSurface"
            // No need to wire it here since we use the event handler directly

            CanvasView.PointerExited += CanvasView_PointerExited;
            CanvasView.PointerEntered += CanvasView_PointerEntered;

            CanvasView.PointerCaptureLost += CanvasView_PointerCaptureLost;
            CanvasView.PointerCanceled += CanvasView_PointerCanceled;

            CanvasView.DoubleTapped += CanvasView_DoubleTapped;
        }

        private void CommitIfPaintingLost()
        {
            if (_isPainting)
            {
                _isPainting = false;
                _hasLastDocPos = false;
                // Note: Don't reset _shiftLineActive - we want the origin to persist for shift-click
                CommitStroke();
            }
            _pendingStrokeFromOutside = false;

            // Reset utility handlers
            GetUtilityHandler(ToolIds.Pan)?.Reset();
            GetUtilityHandler(ToolIds.Dropper)?.Reset();

            // Reset symmetry drag
            _symmetryAxisDragging = false;
        }

        private void CanvasView_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_targetCursor != null)
            {
                ProtectedCursor = _targetCursor;
            }
        }

        private void CanvasView_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => CommitIfPaintingLost();
        private void CanvasView_PointerCanceled(object sender, PointerRoutedEventArgs e) => CommitIfPaintingLost();

        private void CanvasView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (Selection_DoubleTapped(e)) return;
        }

        // ════════════════════════════════════════════════════════════════════
        // UTILITY HANDLER HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets a utility handler by tool ID from the handlers dictionary.
        /// </summary>
        private IUtilityHandler? GetUtilityHandler(string toolId)
        {
            return _utilityHandlers.TryGetValue(toolId, out var handler) ? handler : null;
        }

        // ════════════════════════════════════════════════════════════════════
        // SYMMETRY AXIS INTERACTION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Hit test radius for symmetry axis lines (in screen pixels).</summary>
        private const float SymmetryAxisHitRadius = 8f;

        /// <summary>Hit test radius for symmetry center point (in screen pixels).</summary>
        private const float SymmetryCenterHitRadius = 12f;

        /// <summary>
        /// Gets the cursor for symmetry axis interaction based on pointer position.
        /// Returns resize cursor when hovering over draggable axis.
        /// Only allows interaction when Symmetry tool is active.
        /// </summary>
        private InputSystemCursorShape? GetSymmetryAxisCursor(Point screenPos)
        {
            if (_toolState?.Symmetry == null || !_toolState.Symmetry.Enabled || !_toolState.Symmetry.ShowAxisLines)
                return null;

            // Only allow axis dragging when Symmetry tool is active
            if (_toolState?.ActiveToolId != ToolIds.Symmetry)
                return null;

            var hitType = HitTestSymmetryAxis(screenPos);
            return hitType switch
            {
                'x' => InputSystemCursorShape.SizeWestEast,     // Vertical axis line - drag left/right
                'y' => InputSystemCursorShape.SizeNorthSouth,  // Horizontal axis line - drag up/down  
                'c' => InputSystemCursorShape.SizeAll,         // Center point - drag any direction
                _ => null
            };
        }

        /// <summary>
        /// Hit tests for symmetry axis interaction.
        /// Returns 'x' for horizontal axis (vertical line), 'y' for vertical axis (horizontal line),
        /// 'c' for center point, or '\0' for no hit.
        /// </summary>
        private char HitTestSymmetryAxis(Point screenPos)
        {
            var settings = _toolState?.Symmetry;
            if (settings == null || !settings.Enabled || !settings.ShowAxisLines)
                return '\0';

            var dest = _zoom.GetDestRect();
            float scale = (float)_zoom.Scale;

            // Get axis positions in document space
            int docWidth = Document.PixelWidth;
            int docHeight = Document.PixelHeight;

            // Canvas-wide symmetry
            double axisX = settings.AxisX * docWidth;
            double axisY = settings.AxisY * docHeight;
            double lineStartX = 0;
            double lineStartY = 0;
            double lineEndX = docWidth;
            double lineEndY = docHeight;

            // Convert to screen coordinates
            float screenAxisX = (float)(dest.X + axisX * scale);
            float screenAxisY = (float)(dest.Y + axisY * scale);

            // For radial modes, check center point first
            if (settings.IsRadialMode)
            {
                float dx = (float)screenPos.X - screenAxisX;
                float dy = (float)screenPos.Y - screenAxisY;
                if (dx * dx + dy * dy <= SymmetryCenterHitRadius * SymmetryCenterHitRadius)
                    return 'c';
            }

            // For horizontal/both modes, check vertical line (X axis)
            if (settings.Mode == SymmetryMode.Horizontal || settings.Mode == SymmetryMode.Both)
            {
                float screenLineStartY = (float)(dest.Y + lineStartY * scale);
                float screenLineEndY = (float)(dest.Y + lineEndY * scale);

                // Check if near the vertical axis line
                if (Math.Abs(screenPos.X - screenAxisX) <= SymmetryAxisHitRadius &&
                    screenPos.Y >= screenLineStartY - SymmetryAxisHitRadius &&
                    screenPos.Y <= screenLineEndY + SymmetryAxisHitRadius)
                {
                    return 'x';
                }
            }

            // For vertical/both modes, check horizontal line (Y axis)
            if (settings.Mode == SymmetryMode.Vertical || settings.Mode == SymmetryMode.Both)
            {
                float screenLineStartX = (float)(dest.X + lineStartX * scale);
                float screenLineEndX = (float)(dest.X + lineEndX * scale);

                // Check if near the horizontal axis line
                if (Math.Abs(screenPos.Y - screenAxisY) <= SymmetryAxisHitRadius &&
                    screenPos.X >= screenLineStartX - SymmetryAxisHitRadius &&
                    screenPos.X <= screenLineEndX + SymmetryAxisHitRadius)
                {
                    return 'y';
                }
            }

            return '\0';
        }

        /// <summary>
        /// Handles pointer pressed for symmetry axis dragging.
        /// Returns true if the event was handled.
        /// Only allows interaction when Symmetry tool is active.
        /// </summary>
        private bool Symmetry_TryHandlePointerPressed(PointerRoutedEventArgs e)
        {
            var settings = _toolState?.Symmetry;
            if (settings == null || !settings.Enabled || !settings.ShowAxisLines)
                return false;

            // Only allow axis dragging when Symmetry tool is active
            if (_toolState?.ActiveToolId != ToolIds.Symmetry)
                return false;

            var pt = e.GetCurrentPoint(CanvasView);
            if (!pt.Properties.IsLeftButtonPressed) return false;

            var hitType = HitTestSymmetryAxis(pt.Position);
            if (hitType == '\0') return false;

            _symmetryAxisDragging = true;
            _symmetryAxisDragType = hitType;
            CanvasView.CapturePointer(e.Pointer);
            return true;
        }

        /// <summary>
        /// Handles pointer moved for symmetry axis dragging.
        /// Returns true if the event was handled.
        /// </summary>
        private bool Symmetry_TryHandlePointerMoved(PointerRoutedEventArgs e)
        {
            if (!_symmetryAxisDragging) return false;

            var settings = _toolState?.Symmetry;
            if (settings == null) return false;

            var pt = e.GetCurrentPoint(CanvasView);
            var docPos = _zoom.ScreenToDoc(pt.Position);

            int docWidth = Document.PixelWidth;
            int docHeight = Document.PixelHeight;

            // Canvas-wide symmetry
            switch (_symmetryAxisDragType)
            {
                case 'x': // Vertical line (horizontal position)
                    settings.AxisX = Math.Clamp(docPos.X / docWidth, 0.0, 1.0);
                    break;
                case 'y': // Horizontal line (vertical position)
                    settings.AxisY = Math.Clamp(docPos.Y / docHeight, 0.0, 1.0);
                    break;
                case 'c': // Center point
                    settings.SetAxisPosition(docPos.X / docWidth, docPos.Y / docHeight);
                    break;
            }

            CanvasView.Invalidate();
            return true;
        }

        /// <summary>
        /// Handles pointer released for symmetry axis dragging.
        /// Returns true if the event was handled.
        /// </summary>
        private bool Symmetry_TryHandlePointerReleased(PointerRoutedEventArgs e)
        {
            if (!_symmetryAxisDragging) return false;

            _symmetryAxisDragging = false;
            CanvasView.ReleasePointerCaptures();
            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        // INPUT - POINTER PRESSED
        // ════════════════════════════════════════════════════════════════════

        private void CanvasView_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Notify that the canvas was interacted with (for keyboard shortcut routing)
            NotifyCanvasInteracted();

            // ════════════════════════════════════════════════════════════════════
            // EXTERNAL DROPPER MODE - intercepts all pointer input for color picker windows
            // ════════════════════════════════════════════════════════════════════
            if (_externalDropperActive && _externalDropperCallback != null)
            {
                var extPt = e.GetCurrentPoint(CanvasView);
                var extDocPos = ScreenToDocPoint(extPt.Position);

                int extX = (int)extDocPos.X;
                int extY = (int)extDocPos.Y;

                var (extW, extH) = (Document.PixelWidth, Document.PixelHeight);
                if (extX >= 0 && extX < extW && extY >= 0 && extY < extH)
                {
                    uint sampled = ReadCompositeBGRA(extX, extY);
                    _externalDropperCallback(sampled);
                }

                e.Handled = true;
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // SYMMETRY AXIS INTERACTION - drag axis lines (only when Symmetry tool is active)
            // ════════════════════════════════════════════════════════════════════
            if (Symmetry_TryHandlePointerPressed(e))
            {
                e.Handled = true;
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // GUIDE INTERACTION - click to drag guides, right-click to delete
            // ════════════════════════════════════════════════════════════════════
            if (Guide_TryHandlePointerPressed(e))
            {
                e.Handled = true;
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // STAGE (CAMERA) INTERACTION - drag stage when selected
            // ════════════════════════════════════════════════════════════════════
            if (Stage_TryHandlePointerPressed(e))
            {
                e.Handled = true;
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // SUB-ROUTINE INTERACTION - select and drag sub-routines on canvas
            // ════════════════════════════════════════════════════════════════════
            if (SubRoutine_TryHandlePointerPressed(e))
            {
                e.Handled = true;
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // REFERENCE LAYER INTERACTION - drag/resize reference layers
            // ════════════════════════════════════════════════════════════════════
            if (RefLayer_TryHandlePointerPressed(e))
            {
                e.Handled = true;
                return;
            }

            if (Selection_PointerPressed(e)) return;

            var p = e.GetCurrentPoint(CanvasView);
            var props = p.Properties;
            var screenPos = p.Position;
            var docPos = ScreenToDocPoint(screenPos);

            UpdateHover(screenPos);

            var dropperHandler = GetUtilityHandler(ToolIds.Dropper);
            var panHandler = GetUtilityHandler(ToolIds.Pan);
            var zoomHandler = GetUtilityHandler(ToolIds.Zoom);

            // RMB handling - momentary dropper (FG by default, BG with Alt)
            // Only fire for brush and shape tools that don't suppress it
            if (props.IsRightButtonPressed)
            {
                // Check if active tool suppresses RMB dropper
                var behavior = _toolState?.Registry.GetBehavior(_toolState.ActiveToolId);
                bool suppressRmbDropper = behavior?.SuppressRmbDropper ?? false;

                // Update dropper's Alt state for FG vs BG sampling
                if (dropperHandler is DropperHandler dropper)
                {
                    dropper.IsAltHeld = IsKeyDown(Windows.System.VirtualKey.Menu);
                }

                if (_toolState?.IsDropper == true)
                {
                    // Dropper tool active - use dropper directly (handles its own RMB)
                    if (dropperHandler?.PointerPressed(screenPos, docPos, props) == true)
                    {
                        CanvasView.CapturePointer(e.Pointer);
                        return;
                    }
                }
                else if (!suppressRmbDropper)
                {
                    // Tool allows RMB dropper = momentary dropper for quick color sampling
                    _toolState?.BeginOverrideById(ToolIds.Dropper);
                    if (dropperHandler?.PointerPressed(screenPos, docPos, props) == true)
                    {
                        CanvasView.CapturePointer(e.Pointer);
                        return;
                    }
                }
                // else: Tool suppresses RMB dropper - do nothing with RMB
            }

            // Pan tool (LMB)
            if (_toolState?.IsPan == true && props.IsLeftButtonPressed)
            {
                if (panHandler?.PointerPressed(screenPos, docPos, props) == true)
                {
                    CanvasView.CapturePointer(e.Pointer);
                    return;
                }
            }

            // MMB or space-pan = always pan
            if (props.IsMiddleButtonPressed || _spacePan)
            {
                if (panHandler?.PointerPressed(screenPos, docPos, props) == true)
                {
                    CanvasView.CapturePointer(e.Pointer);
                    return;
                }
            }

            // Zoom tool (LMB/RMB)
            if (_toolState?.IsMagnifier == true)
            {
                if (zoomHandler?.PointerPressed(screenPos, docPos, props) == true)
                {
                    return;
                }
            }

            // Dropper tool (LMB)
            if (_toolState?.IsDropper == true && props.IsLeftButtonPressed)
            {
                if (dropperHandler?.PointerPressed(screenPos, docPos, props) == true)
                {
                    CanvasView.CapturePointer(e.Pointer);
                    return;
                }
            }

            // Active utility tool handler (for plugins like Info Tool)
            var activeHandler = GetActiveUtilityHandler();
            if (activeHandler != null && _toolState?.IsActiveUtilityTool == true)
            {
                if (activeHandler.PointerPressed(screenPos, docPos, props))
                {
                    CanvasView.CapturePointer(e.Pointer);
                    return;
                }
            }

            // Tile tools (LMB/RMB)
            if (_toolState?.IsActiveTileTool == true)
            {
                if (IsActiveLayerLocked)
                {
                    ShowLockedLayerWarning();
                    return;
                }
                if (HandleTilePressed(e)) return;
            }

            // Jumble (LMB)
            if (_toolState?.ActiveToolId == ToolIds.Jumble && props.IsLeftButtonPressed)
            {
                if (IsActiveLayerLocked)
                {
                    ShowLockedLayerWarning();
                    return;
                }
                HandleJumblePressed(p, e);
                return;
            }

            // Shape tools (built-in Rect/Ellipse and plugin shape tools)
            if (_toolState?.IsActiveShapeTool == true && props.IsLeftButtonPressed)
            {
                if (IsActiveLayerLocked)
                {
                    ShowLockedLayerWarning();
                    return;
                }
                HandleShapePressed(p, e);
                return;
            }

            // Fill
            if (_toolState?.IsFill == true && props.IsLeftButtonPressed)
            {
                if (IsActiveLayerLocked)
                {
                    ShowLockedLayerWarning();
                    return;
                }
                HandleFillPressed(p);
                return;
            }

            // Gradient Fill (drag-based)
            if (_toolState?.IsGradientFill == true && props.IsLeftButtonPressed)
            {
                if (IsActiveLayerLocked)
                {
                    ShowLockedLayerWarning();
                    return;
                }
                HandleGradientFillPressed(p, e);
                return;
            }

            // Normal painting (LMB)
            if (props.IsLeftButtonPressed &&
                (_toolState?.IsDropper != true) &&
                (_toolState?.ActiveCategory == ToolCategory.Brush))
            {
                if (IsActiveLayerLocked)
                {
                    ShowLockedLayerWarning();
                    return;
                }
                HandlePaintingPressed(p, e);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // INPUT - POINTER MOVED
        // ════════════════════════════════════════════════════════════════════

        private void CanvasView_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // External dropper mode - still update hover for cursor overlay
            if (_externalDropperActive)
            {
                var extPt = e.GetCurrentPoint(CanvasView);
                UpdateHover(extPt.Position);
                return;
            }

            var pt = e.GetCurrentPoint(CanvasView);
            var screenPos = pt.Position;

            // ════════════════════════════════════════════════════════════════════
            // SYMMETRY AXIS DRAGGING
            // ════════════════════════════════════════════════════════════════════
            if (Symmetry_TryHandlePointerMoved(e))
            {
                var symCursor = GetSymmetryAxisCursor(screenPos);
                ProtectedCursor = symCursor.HasValue
                    ? InputSystemCursor.Create(symCursor.Value)
                    : _targetCursor;
                e.Handled = true;
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // CURSOR PRIORITY SYSTEM
            // Each subsystem returns a cursor if it wants to override, or null.
            // First non-null cursor wins. Default is _targetCursor (Cross).
            // ════════════════════════════════════════════════════════════════════

            InputCursor? desiredCursor = null;

            // 1. Symmetry axis hover (when symmetry is enabled)
            var symAxisCursor = GetSymmetryAxisCursor(screenPos);
            if (symAxisCursor.HasValue)
            {
                ProtectedCursor = InputSystemCursor.Create(symAxisCursor.Value);
                UpdateHover(screenPos);
                return;
            }

            // 2. Guide interaction (highest priority when dragging)
            Guide_TryHandlePointerMoved(e);
            if (_isDraggingGuideOnCanvas)
            {
                var guideCursor = GetGuideCursor();
                if (guideCursor.HasValue)
                    desiredCursor = InputSystemCursor.Create(guideCursor.Value);
                
                ProtectedCursor = desiredCursor ?? _targetCursor;
                e.Handled = true;
                return;
            }

            // 3. Stage (camera) interaction
            if (Stage_TryHandlePointerMoved(e))
            {
                var stageCursor = GetStageCursor(e);
                if (stageCursor.HasValue)
                    desiredCursor = InputSystemCursor.Create(stageCursor.Value);
                
                ProtectedCursor = desiredCursor ?? _targetCursor;
                e.Handled = true;
                return;
            }

            // 3.5. Sub-routine interaction
            if (SubRoutine_TryHandlePointerMoved(e))
            {
                var subRoutineCursor = GetSubRoutineCursor(e);
                if (subRoutineCursor.HasValue)
                    desiredCursor = InputSystemCursor.Create(subRoutineCursor.Value);
                
                ProtectedCursor = desiredCursor ?? _targetCursor;
                e.Handled = true;
                return;
            }

            // 4. Reference layer interaction
            if (RefLayer_TryHandlePointerMoved(e))
            {
                // RefLayer handler sets its own cursor during active manipulation
                e.Handled = true;
                return;
            }

            // 5. Selection tool interaction
            if (Selection_PointerMoved(e))
            {
                // Selection handler manages its own cursor via UpdateSelectionCursor
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // HOVER CURSOR DETECTION (when not actively manipulating)
            // Check each subsystem for hover state, in priority order.
            // ════════════════════════════════════════════════════════════════════

            // Check guide hover
            var guideCursorHover = GetGuideCursor();
            if (guideCursorHover.HasValue)
            {
                ProtectedCursor = InputSystemCursor.Create(guideCursorHover.Value);
                UpdateHover(screenPos);
                return;
            }

            // Check stage hover (when stage is selected)
            var stageCursorHover = GetStageCursor(e);
            if (stageCursorHover.HasValue)
            {
                ProtectedCursor = InputSystemCursor.Create(stageCursorHover.Value);
                UpdateHover(screenPos);
                return;
            }

            // Check sub-routine hover (when in canvas animation mode)
            var subRoutineCursorHover = GetSubRoutineCursor(e);
            if (subRoutineCursorHover.HasValue)
            {
                ProtectedCursor = InputSystemCursor.Create(subRoutineCursorHover.Value);
                UpdateHover(screenPos);
                return;
            }

            // Check reference layer hover
            var refCursorHover = GetRefLayerCursor(screenPos);
            if (refCursorHover.HasValue)
            {
                ProtectedCursor = InputSystemCursor.Create(refCursorHover.Value);
                UpdateHover(screenPos);
                return;
            }

            // Check selection hover (for armed selections)
            if (IsSelectTool && _selState != null && _selState.Active && _selState.State == Selection.SelectionSubsystem.SelectionState.Armed)
            {
                // UpdateSelectionCursor handles its own cursor setting
                // But we need to call it to update hover state
                UpdateSelectionCursor(screenPos);
                // If selection set a non-Arrow cursor, we're done
                if (_curShape != InputSystemCursorShape.Arrow)
                {
                    UpdateHover(screenPos);
                    return;
                }
            }

            // ════════════════════════════════════════════════════════════════════
            // DEFAULT: Use target cursor (Cross) for normal canvas operations
            // ════════════════════════════════════════════════════════════════════
            ProtectedCursor = _targetCursor;

            UpdateHover(screenPos);

            var props = pt.Properties;
            var docPos = ScreenToDocPoint(screenPos);

            var panHandler = GetUtilityHandler(ToolIds.Pan);
            var dropperHandler = GetUtilityHandler(ToolIds.Dropper);

            // Pan handler (MMB drag, space-pan, or Pan tool)
            if (panHandler?.IsActive == true)
            {
                panHandler.PointerMoved(screenPos, docPos, props);
                return;
            }

            // Dropper handler (FG/BG sampling)
            if (dropperHandler?.IsActive == true)
            {
                dropperHandler.PointerMoved(screenPos, docPos, props);
                return;
            }

            // Active utility tool handler (for plugins like Info Tool - hover sampling)
            var activeHandler = GetActiveUtilityHandler();
            if (activeHandler != null && _toolState?.IsActiveUtilityTool == true)
            {
                // Always call PointerMoved for utility tools (they may need hover events)
                activeHandler.PointerMoved(screenPos, docPos, props);
                // Don't return - allow other processing to continue unless handler is active
                if (activeHandler.IsActive)
                    return;
            }

            // Tile tools - always update for hover preview
            if (_toolState?.IsActiveTileTool == true)
            {
                HandleTileMoved(e);
                // Don't return early - allow normal painting to be skipped if tile tool is active
                if (_activeTileHandler?.IsActive == true)
                    return;
            }

            // Jumble painting
            if (_toolState?.ActiveToolId == ToolIds.Jumble && (_pendingStrokeFromOutside || _isPainting))
            {
                HandleJumbleMoved(screenPos);
                return;
            }

            // Shape dragging
            if (_shapeDrag)
            {
                HandleShapeMoved(screenPos);
                return;
            }

            // Gradient fill dragging
            if (_gradientDrag)
            {
                HandleGradientFillMoved(screenPos);
                return;
            }

            // Normal painting
            if (_pendingStrokeFromOutside || _isPainting)
            {
                HandlePaintingMoved(screenPos);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // INPUT - POINTER RELEASED
        // ════════════════════════════════════════════════════════════════════

        private void CanvasView_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // ════════════════════════════════════════════════════════════════════
            // SYMMETRY AXIS INTERACTION - release axis drag
            // ════════════════════════════════════════════════════════════════════
            if (Symmetry_TryHandlePointerReleased(e))
            {
                e.Handled = true;
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // GUIDE INTERACTION - release guide drag
            // ════════════════════════════════════════════════════════════════════
            if (Guide_TryHandlePointerReleased(e))
            {
                e.Handled = true;
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // STAGE (CAMERA) INTERACTION - release stage drag
            // ════════════════════════════════════════════════════════════════════
            if (Stage_TryHandlePointerReleased(e))
            {
                e.Handled = true;
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // SUB-ROUTINE INTERACTION - release sub-routine drag
            // ════════════════════════════════════════════════════════════════════
            if (SubRoutine_TryHandlePointerReleased(e))
            {
                e.Handled = true;
                return;
            }

            // ════════════════════════════════════════════════════════════════════
            // REFERENCE LAYER INTERACTION - release reference image drag
            // ════════════════════════════════════════════════════════════════════
            if (RefLayer_TryHandlePointerReleased(e))
            {
                e.Handled = true;
                return;
            }

            if (Selection_PointerReleased(e)) return;

            var pt = e.GetCurrentPoint(CanvasView);
            var props = pt.Properties;
            var screenPos = pt.Position;
            var docPos = ScreenToDocPoint(screenPos);

            var panHandler = GetUtilityHandler(ToolIds.Pan);
            var dropperHandler = GetUtilityHandler(ToolIds.Dropper);

            // Pan handler release
            if (panHandler?.IsActive == true)
            {
                panHandler.PointerReleased(screenPos, docPos, props);
                return;
            }

            // Dropper handler release (and end override if RMB momentary dropper)
            if (dropperHandler?.IsActive == true)
            {
                bool wasRmbOverride = _toolState?.OverrideToolId == ToolIds.Dropper;
                dropperHandler.PointerReleased(screenPos, docPos, props);
                if (wasRmbOverride && !props.IsRightButtonPressed)
                {
                    _toolState?.EndOverride();
                }
                return;
            }

            // Active utility tool handler (for plugins like Info Tool)
            var activeHandler = GetActiveUtilityHandler();
            if (activeHandler?.IsActive == true)
            {
                activeHandler.PointerReleased(screenPos, docPos, props);
                return;
            }

            // Tile tools
            if (_activeTileHandler != null)
            {
                HandleTileReleased(e);
                return;
            }

            // End Jumble painting
            if (_toolState?.ActiveToolId == ToolIds.Jumble)
            {
                if (_isPainting)
                {
                    HandlePaintingReleased(); // Handle shift-line commit
                    _isPainting = false;
                    _hasLastDocPos = false;
                    CommitStroke();
                    CanvasView.ReleasePointerCaptures();
                    _pendingStrokeFromOutside = false;
                    return;
                }
                if (_pendingStrokeFromOutside)
                {
                    _pendingStrokeFromOutside = false;
                    CanvasView.ReleasePointerCaptures();
                    return;
                }
            }

            // End shape dragging
            if (_shapeDrag)
            {
                HandleShapeReleased();
                return;
            }

            // End gradient fill dragging
            if (_gradientDrag)
            {
                HandleGradientFillReleased();
                return;
            }

            // End normal painting
            if (_isPainting)
            {
                HandlePaintingReleased(); // Handle shift-line commit
                _isPainting = false;
                _hasLastDocPos = false;
                CommitStroke();
                CanvasView.ReleasePointerCaptures();
            }

            if (_pendingStrokeFromOutside)
            {
                _pendingStrokeFromOutside = false;
                CanvasView.ReleasePointerCaptures();
            }
        }

        private void CanvasView_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ProtectedCursor = null;

            if (_isPainting) _hasLastDocPos = false;
            _pendingStrokeFromOutside = false;

            // Reset handlers on exit
            GetUtilityHandler(ToolIds.Pan)?.Reset();

            _hoverValid = false;

            // Clear cursor coordinates display
            CursorXText.Text = "--";
            CursorYText.Text = "--";

            OnBrushMoved(System.Numerics.Vector2.Zero, 0);
            CanvasView.Invalidate();
        }

        private void CanvasView_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var cp = e.GetCurrentPoint(CanvasView);
            var screenPos = cp.Position;
            UpdateHover(screenPos);

            // Ctrl+Wheel = brush size adjustment (but not for Pan, Zoom, Dropper)
            if (IsKeyDown(Windows.System.VirtualKey.Control))
            {
                // Skip brush size shortcuts for utility tools without brushes
                bool isUtilityWithoutBrush = _toolState?.ActiveToolId == ToolIds.Pan ||
                                             _toolState?.ActiveToolId == ToolIds.Zoom ||
                                             _toolState?.ActiveToolId == ToolIds.Dropper;

                if (!isUtilityWithoutBrush)
                {
                    var step = cp.Properties.MouseWheelDelta / 120;
                    if (IsKeyDown(Windows.System.VirtualKey.Shift))
                        step *= 5;
                    _toolState?.UpdateBrush(b => b.Size = Math.Clamp(b.Size + step, 1, 128));
                    e.Handled = true;
                    return;
                }
            }

            // Delegate to zoom handler
            int delta = cp.Properties.MouseWheelDelta;
            GetUtilityHandler(ToolIds.Zoom)?.PointerWheelChanged(screenPos, delta);
        }

        // ════════════════════════════════════════════════════════════════════
        // KEYBOARD HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static bool IsKeyDown(Windows.System.VirtualKey k)
        {
            var st = InputKeyboardSource.GetKeyStateForCurrentThread(k);
            return st.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        }

        // ════════════════════════════════════════════════════════════════════
        // COORDINATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        private bool TryGetDocInside(Point screenPt, out int x, out int y)
        {
            var dest = _zoom.GetDestRect();
            if (!dest.Contains(screenPt))
            {
                x = y = 0;
                return false;
            }
            return _zoom.ScreenToDocClamped(screenPt, out x, out y);
        }

        private bool TryGetDocWithBrushOverlap(Point screenPt, out int x, out int y)
        {
            var docPt = _zoom.ScreenToDoc(screenPt);
            int cx = (int)Math.Floor(docPt.X);
            int cy = (int)Math.Floor(docPt.Y);

            x = cx;
            y = cy;

            int w = Document.Surface.Width;
            int h = Document.Surface.Height;

            var mask = _stroke.GetCurrentBrushOffsets();
            if (mask == null || mask.Count == 0)
            {
                return (uint)cx < (uint)w && (uint)cy < (uint)h;
            }

            foreach (var (dx, dy) in mask)
            {
                int px = cx + dx;
                int py = cy + dy;
                if ((uint)px < (uint)w && (uint)py < (uint)h)
                    return true;
            }

            return false;
        }

        private void UpdateHover(Point screenPt)
        {
            var docPt = _zoom.ScreenToDoc(screenPt);
            int cx = (int)Math.Floor(docPt.X);
            int cy = (int)Math.Floor(docPt.Y);

            _hoverX = cx;
            _hoverY = cy;

            int w = Document.Surface.Width;
            int h = Document.Surface.Height;

            // External dropper mode uses simple bounds check (1x1 pixel cursor)
            if (_externalDropperActive)
            {
                _hoverValid = cx >= 0 && cx < w && cy >= 0 && cy < h;
            }
            else
            {
                // Normal mode checks if brush mask intersects canvas
                bool intersects = false;

                var mask = _stroke.GetCurrentBrushOffsets();

                foreach (var (dx, dy) in mask)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if ((uint)px < (uint)w && (uint)py < (uint)h)
                    {
                        intersects = true;
                        break;
                    }
                }

                _hoverValid = intersects;
            }

            // Show shape start point for any shape tool (built-in or plugin)
            bool isShapeTool = _toolState?.IsActiveShapeTool == true;
            _shapeShowStartPoint = isShapeTool && _hoverValid && !_shapeDrag;

            // Update cursor coordinates display
            UpdateCursorCoordinatesDisplay(cx, cy);

            OnBrushMoved(new System.Numerics.Vector2(cx, cy), (float)((_brushSize - 1) * 0.5));
            CanvasView.Invalidate();
        }

        /// <summary>
        /// Updates the cursor coordinates display in the bottom bar.
        /// Shows coordinates when inside canvas bounds, "--" when outside.
        /// </summary>
        private void UpdateCursorCoordinatesDisplay(int x, int y)
        {
            int w = Document.Surface.Width;
            int h = Document.Surface.Height;

            bool insideCanvas = x >= 0 && x < w && y >= 0 && y < h;

            if (insideCanvas)
            {
                CursorXText.Text = x.ToString();
                CursorYText.Text = y.ToString();
            }
            else
            {
                CursorXText.Text = "--";
                CursorYText.Text = "--";
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SAMPLING HELPERS
        // ════════════════════════════════════════════════════════════════════

        private void SampleAt(int x, int y)
        {
            uint px = ReadCompositeBGRA(x, y);
            uint merged = ((uint)_brushOpacity << 24) | (px & 0x00FFFFFFu);
            SetForeground(merged);
            ForegroundSampledLive?.Invoke(merged);
        }

        private void SampleAtBg(int x, int y)
        {
            uint px = ReadCompositeBGRA(x, y);
            uint merged = 0xFF000000u | (px & 0x00FFFFFFu);
            BackgroundSampledLive?.Invoke(merged);
        }

        // ════════════════════════════════════════════════════════════════════
        // STAGE (CAMERA) INTERACTION
        // ════════════════════════════════════════════════════════════════════

        private const float StageHandleSize = 8f;
        private const float StageHandleHitRadius = 12f; // Slightly larger for easier clicking

        /// <summary>
        /// Gets the appropriate cursor for stage interaction based on pointer position.
        /// Returns resize cursor when hovering over corners, move cursor when inside stage.
        /// </summary>
        private InputSystemCursorShape? GetStageCursor(PointerRoutedEventArgs e)
        {
            if (!_stageSelected) return null;

            var animState = Document.CanvasAnimationState;
            if (animState == null || !animState.Stage.Enabled) return null;

            var pt = e.GetCurrentPoint(CanvasView);
            var docPos = ScreenToDocPoint(pt.Position);
            int docX = (int)docPos.X;
            int docY = (int)docPos.Y;

            var stage = animState.Stage;

            // Check corner handles first
            int corner = GetStageCornerAtPoint(docX, docY);
            if (corner >= 0)
            {
                // Use diagonal resize cursors for corners
                return corner switch
                {
                    0 or 2 => InputSystemCursorShape.SizeNorthwestSoutheast, // TL or BR
                    1 or 3 => InputSystemCursorShape.SizeNortheastSouthwest, // TR or BL
                    _ => null
                };
            }

            // Check if inside stage area (for move cursor)
            if (docX >= stage.StageX && docX < stage.StageX + stage.StageWidth &&
                docY >= stage.StageY && docY < stage.StageY + stage.StageHeight)
            {
                return InputSystemCursorShape.SizeAll; // Move cursor
            }

            return null;
        }

        /// <summary>
        /// Checks if a point is near a stage corner handle.
        /// Returns corner index (0=TL, 1=TR, 2=BR, 3=BL) or -1 if not near any corner.
        /// </summary>
        private int GetStageCornerAtPoint(int docX, int docY)
        {
            var animState = Document.CanvasAnimationState;
            if (animState == null || !animState.Stage.Enabled) return -1;

            var stage = animState.Stage;
            float hitRadius = StageHandleHitRadius / (float)_zoom.Scale;

            // Corner positions
            var corners = new (int x, int y)[]
            {
                (stage.StageX, stage.StageY),                                    // TL
                (stage.StageX + stage.StageWidth, stage.StageY),                 // TR
                (stage.StageX + stage.StageWidth, stage.StageY + stage.StageHeight), // BR
                (stage.StageX, stage.StageY + stage.StageHeight)                 // BL
            };

            for (int i = 0; i < 4; i++)
            {
                float dx = docX - corners[i].x;
                float dy = docY - corners[i].y;
                if (dx * dx + dy * dy <= hitRadius * hitRadius)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Handles pointer pressed for stage dragging and resizing.
        /// Returns true if the event was handled.
        /// </summary>
        private bool Stage_TryHandlePointerPressed(PointerRoutedEventArgs e)
        {
            if (!_stageSelected) return false;

            var animState = Document.CanvasAnimationState;
            if (animState == null || !animState.Stage.Enabled) return false;

            var pt = e.GetCurrentPoint(CanvasView);
            if (!pt.Properties.IsLeftButtonPressed) return false;

            var screenPos = pt.Position;
            var docPos = ScreenToDocPoint(screenPos);
            int docX = (int)docPos.X;
            int docY = (int)docPos.Y;

            var stage = animState.Stage;

            // Check if clicking on a corner handle first (for resize)
            int corner = GetStageCornerAtPoint(docX, docY);
            if (corner >= 0)
            {
                _stageResizing = true;
                _stageResizeCorner = corner;
                _stageDragStartX = stage.StageX;
                _stageDragStartY = stage.StageY;
                _stageDragStartW = stage.StageWidth;
                _stageDragStartH = stage.StageHeight;
                _stageDragPointerStartX = docX;
                _stageDragPointerStartY = docY;

                CanvasView.CapturePointer(e.Pointer);
                return true;
            }

            // Check if click is inside the stage area (for move)
            if (docX >= stage.StageX && docX < stage.StageX + stage.StageWidth &&
                docY >= stage.StageY && docY < stage.StageY + stage.StageHeight)
            {
                // Start dragging
                _stageDragging = true;
                _stageDragStartX = stage.StageX;
                _stageDragStartY = stage.StageY;
                _stageDragPointerStartX = docX;
                _stageDragPointerStartY = docY;

                CanvasView.CapturePointer(e.Pointer);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles pointer moved for stage dragging and resizing.
        /// Returns true if the event was handled.
        /// </summary>
        private bool Stage_TryHandlePointerMoved(PointerRoutedEventArgs e)
        {
            if (!_stageDragging && !_stageResizing) return false;

            var animState = Document.CanvasAnimationState;
            if (animState == null) return false;

            var pt = e.GetCurrentPoint(CanvasView);
            var screenPos = pt.Position;
            var docPos = ScreenToDocPoint(screenPos);
            int docX = (int)docPos.X;
            int docY = (int)docPos.Y;

            var stage = animState.Stage;
            int canvasW = Document.PixelWidth;
            int canvasH = Document.PixelHeight;

            if (_stageResizing)
            {
                // Calculate delta from start
                int deltaX = docX - _stageDragPointerStartX;
                int deltaY = docY - _stageDragPointerStartY;

                // Calculate new size based on which corner is being dragged
                // Ratio-locked: use the larger delta to maintain aspect ratio
                float aspectRatio = (float)_stageDragStartW / _stageDragStartH;

                int newW, newH, newX, newY;

                switch (_stageResizeCorner)
                {
                    case 0: // TL - resize from top-left
                        // Moving TL up/left makes it bigger
                        deltaX = -deltaX;
                        deltaY = -deltaY;
                        // Use the larger delta, adjusted for aspect ratio
                        if (Math.Abs(deltaX) > Math.Abs(deltaY * aspectRatio))
                        {
                            newW = Math.Max(4, _stageDragStartW + deltaX);
                            newH = (int)(newW / aspectRatio);
                        }
                        else
                        {
                            newH = Math.Max(4, _stageDragStartH + deltaY);
                            newW = (int)(newH * aspectRatio);
                        }
                        newX = _stageDragStartX + _stageDragStartW - newW;
                        newY = _stageDragStartY + _stageDragStartH - newH;
                        break;

                    case 1: // TR - resize from top-right
                        deltaY = -deltaY;
                        // Use the larger delta, adjusted for aspect ratio
                        if (Math.Abs(deltaX) > Math.Abs(deltaY * aspectRatio))
                        {
                            newW = Math.Max(4, _stageDragStartW + deltaX);
                            newH = (int)(newW / aspectRatio);
                        }
                        else
                        {
                            newH = Math.Max(4, _stageDragStartH + deltaY);
                            newW = (int)(newH * aspectRatio);
                        }
                        newX = _stageDragStartX;
                        newY = _stageDragStartY + _stageDragStartH - newH;
                        break;

                    case 2: // BR - resize from bottom-right (most intuitive)
                        // Use the larger delta, adjusted for aspect ratio
                        if (Math.Abs(deltaX) > Math.Abs(deltaY * aspectRatio))
                        {
                            newW = Math.Max(4, _stageDragStartW + deltaX);
                            newH = (int)(newW / aspectRatio);
                        }
                        else
                        {
                            newH = Math.Max(4, _stageDragStartH + deltaY);
                            newW = (int)(newH * aspectRatio);
                        }
                        newX = _stageDragStartX;
                        newY = _stageDragStartY;
                        break;

                    case 3: // BL - resize from bottom-left
                        deltaX = -deltaX;
                        // Use the larger delta, adjusted for aspect ratio
                        if (Math.Abs(deltaX) > Math.Abs(deltaY * aspectRatio))
                        {
                            newW = Math.Max(4, _stageDragStartW + deltaX);
                            newH = (int)(newW / aspectRatio);
                        }
                        else
                        {
                            newH = Math.Max(4, _stageDragStartH + deltaY);
                            newW = (int)(newH * aspectRatio);
                        }
                        newX = _stageDragStartX + _stageDragStartW - newW;
                        newY = _stageDragStartY;
                        break;

                    default:
                        return true;
                }

                // Clamp to canvas bounds - stage must stay within canvas
                // First, ensure position is not negative
                if (newX < 0)
                {
                    int overflow = -newX;
                    newX = 0;
                    newW -= overflow;
                    newH = (int)(newW / aspectRatio);
                }
                if (newY < 0)
                {
                    int overflow = -newY;
                    newY = 0;
                    newH -= overflow;
                    newW = (int)(newH * aspectRatio);
                }

                // Then, ensure it doesn't exceed canvas bounds
                if (newX + newW > canvasW)
                {
                    newW = canvasW - newX;
                    newH = (int)(newW / aspectRatio);
                }
                if (newY + newH > canvasH)
                {
                    newH = canvasH - newY;
                    newW = (int)(newH * aspectRatio);
                }

                // Final clamp to ensure minimum size
                newW = Math.Max(4, newW);
                newH = Math.Max(4, newH);

                // Re-check bounds after ratio adjustment
                newX = Math.Clamp(newX, 0, canvasW - newW);
                newY = Math.Clamp(newY, 0, canvasH - newH);

                stage.StageX = newX;
                stage.StageY = newY;
                stage.StageWidth = newW;
                stage.StageHeight = newH;

                CanvasView.Invalidate();
                return true;
            }

            if (_stageDragging)
            {
                // Calculate offset from drag start
                int deltaX = docX - _stageDragPointerStartX;
                int deltaY = docY - _stageDragPointerStartY;

                int newX = _stageDragStartX + deltaX;
                int newY = _stageDragStartY + deltaY;

                // Apply bounds constraint if not in Free mode
                if (stage.BoundsMode == Core.Animation.StageBoundsMode.Constrained)
                {
                    newX = Math.Clamp(newX, 0, canvasW - stage.StageWidth);
                    newY = Math.Clamp(newY, 0, canvasH - stage.StageHeight);
                }
                else if (stage.BoundsMode == Core.Animation.StageBoundsMode.CenterLocked)
                {
                    // Center locked - don't allow movement
                    newX = stage.StageX;
                    newY = stage.StageY;
                }

                stage.StageX = newX;
                stage.StageY = newY;

                CanvasView.Invalidate();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles pointer released for stage dragging and resizing.
        /// Returns true if the event was handled.
        /// </summary>
        private bool Stage_TryHandlePointerReleased(PointerRoutedEventArgs e)
        {
            if (!_stageDragging && !_stageResizing) return false;

            // Mark that we have pending edits at this frame
            var animState = Document.CanvasAnimationState;
            if (animState != null)
            {
                _stagePendingEdits = true;
                _stagePendingEditsFrame = animState.CurrentFrameIndex;
            }

            _stageDragging = false;
            _stageResizing = false;
            CanvasView.ReleasePointerCaptures();
            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        // SUB-ROUTINE INTERACTION
        // ════════════════════════════════════════════════════════════════════

        private const float SubRoutineHandleHitRadius = 12f;

        /// <summary>
        /// Gets the appropriate cursor for sub-routine interaction based on pointer position.
        /// Returns move cursor only when hovering over the SELECTED sub-routine.
        /// </summary>
        private InputSystemCursorShape? GetSubRoutineCursor(PointerRoutedEventArgs e)
        {
            // Only show cursor when a sub-routine is selected
            if (_selectedSubRoutine == null)
                return null;

            if (_animationMode != Animation.AnimationMode.Canvas)
                return null;

            var animState = Document.CanvasAnimationState;
            if (animState == null)
                return null;

            var pt = e.GetCurrentPoint(CanvasView);
            var docPos = ScreenToDocPoint(pt.Position);
            int docX = (int)docPos.X;
            int docY = (int)docPos.Y;

            // Check if hovering over the SELECTED sub-routine
            var hitSubRoutine = HitTestSubRoutine(docX, docY, animState.CurrentFrameIndex);
            if (hitSubRoutine != null && hitSubRoutine == _selectedSubRoutine)
            {
                return InputSystemCursorShape.SizeAll; // Move cursor
            }

            return null;
        }

        /// <summary>
        /// Hit tests for sub-routines at the given document position.
        /// Returns the sub-routine if found, null otherwise.
        /// </summary>
        private Core.Animation.AnimationSubRoutine? HitTestSubRoutine(int docX, int docY, int frameIndex)
        {
            var animState = Document.CanvasAnimationState;
            if (animState == null)
                return null;

            // Update sub-routine state for current frame
            animState.SubRoutineState.UpdateForFrame(frameIndex);

            // Check each active sub-routine (in reverse order so topmost is hit first)
            var renderInfos = animState.SubRoutineState.GetRenderInfo(frameIndex).ToList();
            for (int i = renderInfos.Count - 1; i >= 0; i--)
            {
                var renderInfo = renderInfos[i];
                if (renderInfo.FramePixels == null)
                    continue;

                double posX = renderInfo.PositionX;
                double posY = renderInfo.PositionY;
                float scale = renderInfo.Scale;

                int w = (int)(renderInfo.FrameWidth * scale);
                int h = (int)(renderInfo.FrameHeight * scale);

                // Simple bounding box hit test
                if (docX >= posX && docX < posX + w &&
                    docY >= posY && docY < posY + h)
                {
                    return renderInfo.SubRoutine;
                }
            }

            return null;
        }

        /// <summary>
        /// Handles pointer pressed for sub-routine dragging (only when already selected).
        /// Returns true if the event was handled.
        /// </summary>
        private bool SubRoutine_TryHandlePointerPressed(PointerRoutedEventArgs e)
        {
            // Only allow manipulation if a sub-routine is already selected (via track header)
            if (_selectedSubRoutine == null)
                return false;

            if (_animationMode != Animation.AnimationMode.Canvas)
                return false;

            var animState = Document.CanvasAnimationState;
            if (animState == null)
                return false;

            var pt = e.GetCurrentPoint(CanvasView);
            if (!pt.Properties.IsLeftButtonPressed)
                return false;

            var screenPos = pt.Position;
            var docPos = ScreenToDocPoint(screenPos);
            int docX = (int)docPos.X;
            int docY = (int)docPos.Y;

            // Hit test for sub-routine - but only start drag if it's the SELECTED sub-routine
            var hitSubRoutine = HitTestSubRoutine(docX, docY, animState.CurrentFrameIndex);

            if (hitSubRoutine != null && hitSubRoutine == _selectedSubRoutine)
            {
                // Get the current position for this sub-routine at this frame
                float progress = _selectedSubRoutine.GetNormalizedProgress(animState.CurrentFrameIndex);
                var (posX, posY) = _selectedSubRoutine.InterpolatePosition(progress);

                // Start dragging
                _subRoutineDragging = true;
                _subRoutineDragStartX = posX;
                _subRoutineDragStartY = posY;
                _subRoutineDragPointerStartX = docX;
                _subRoutineDragPointerStartY = docY;
                _subRoutineEditProgress = progress;

                CanvasView.CapturePointer(e.Pointer);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles pointer moved for sub-routine dragging.
        /// Returns true if the event was handled.
        /// </summary>
        private bool SubRoutine_TryHandlePointerMoved(PointerRoutedEventArgs e)
        {
            if (!_subRoutineDragging || _selectedSubRoutine == null)
                return false;

            var pt = e.GetCurrentPoint(CanvasView);
            var screenPos = pt.Position;
            var docPos = ScreenToDocPoint(screenPos);
            int docX = (int)docPos.X;
            int docY = (int)docPos.Y;

            // Calculate delta from drag start
            int deltaX = docX - _subRoutineDragPointerStartX;
            int deltaY = docY - _subRoutineDragPointerStartY;

            // Calculate new position
            double newX = _subRoutineDragStartX + deltaX;
            double newY = _subRoutineDragStartY + deltaY;

            // Update the position keyframe for the current progress
            // If there's no keyframe at this progress, add one
            if (_selectedSubRoutine.PositionKeyframes.Count == 0)
            {
                // No keyframes yet - add start and end keyframes
                _selectedSubRoutine.PositionKeyframes[0f] = (newX, newY);
                _selectedSubRoutine.PositionKeyframes[1f] = (newX, newY);
            }
            else
            {
                // Find the closest keyframe to edit, or add a new one at current progress
                float closestKey = -1f;
                float minDist = float.MaxValue;

                foreach (var key in _selectedSubRoutine.PositionKeyframes.Keys)
                {
                    float dist = Math.Abs(key - _subRoutineEditProgress);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestKey = key;
                    }
                }

                // If we're close to an existing keyframe (within 5%), edit it
                // Otherwise, add/update a keyframe at the current progress
                if (minDist < 0.05f)
                {
                    _selectedSubRoutine.PositionKeyframes[closestKey] = (newX, newY);
                }
                else
                {
                    // Add a new keyframe at the current progress
                    _selectedSubRoutine.PositionKeyframes[_subRoutineEditProgress] = (newX, newY);
                }
            }

            CanvasView.Invalidate();
            return true;
        }

        /// <summary>
        /// Handles pointer released for sub-routine dragging.
        /// Returns true if the event was handled.
        /// </summary>
        private bool SubRoutine_TryHandlePointerReleased(PointerRoutedEventArgs e)
        {
            if (!_subRoutineDragging)
                return false;

            _subRoutineDragging = false;
            CanvasView.ReleasePointerCaptures();

            // Notify that the selected sub-routine has changed (for timeline refresh)
            if (_selectedSubRoutine != null)
            {
                var animState = Document.CanvasAnimationState;
                // Trigger the SubRoutineChanged event by invoking property changed
                // The SubRoutineChanged event will fire automatically when properties change
            }

            CanvasView.Invalidate();
            return true;
        }
    }
}
