using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Imaging;
using Windows.Foundation;
using Windows.Storage;

namespace PixlPunkt.Uno.UI.CanvasHost
{
    /// <summary>
    /// Reference layer interaction subsystem for CanvasViewHost:
    /// - Selection, dragging, resizing, rotating reference layers
    /// - Cursor management for reference layer handles
    /// - Hit testing and manipulation
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        /// <summary>Hit radius for reference layer corner handles (in screen pixels).</summary>
        private const float RefLayerHandleHitRadius = 14f;

        /// <summary>Distance from corner for rotation handle (in screen pixels).</summary>
        private const float RefLayerRotationHandleOffset = 20f;

        /// <summary>Whether we're currently rotating a reference layer.</summary>
        private bool _refLayerRotating;

        /// <summary>The reference layer rotation when rotation started.</summary>
        private float _refLayerDragStartRotation;

        /// <summary>The angle from center to pointer when rotation started.</summary>
        private float _refLayerRotationStartAngle;

        /// <summary>Whether we're resizing via an edge handle (vs corner).</summary>
        private bool _refLayerEdgeResizing;

        /// <summary>Which edge is being dragged (0=Top, 1=Right, 2=Bottom, 3=Left).</summary>
        private int _refLayerResizeEdge;

        ///////////////////////////////////////////////////////////////////////
        // REFERENCE LAYER INTERACTION - HIT TESTING
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the appropriate cursor for reference layer interaction based on pointer position.
        /// Returns resize cursor when hovering over corners or edges, rotate cursor when near rotation handles,
        /// move cursor when inside layer.
        /// </summary>
        private InputSystemCursorShape? GetRefLayerCursor(Point screenPos)
        {
            if (_selectedReferenceLayer == null)
                return null;

            var refLayer = _selectedReferenceLayer;
            if (refLayer.Locked || !refLayer.Visible)
                return null;

            var docPos = ScreenToDocPoint(screenPos);
            float docX = (float)docPos.X;
            float docY = (float)docPos.Y;
            float handleRadius = RefLayerHandleHitRadius / (float)_zoom.Scale;
            float rotationOffset = RefLayerRotationHandleOffset / (float)_zoom.Scale;

            // Check rotation handles first (outside corners)
            int rotationHandle = HitTestRotationHandle(refLayer, docX, docY, handleRadius, rotationOffset);
            if (rotationHandle >= 0)
            {
                // Custom rotation cursor not available, use cross or hand
                return InputSystemCursorShape.Hand;
            }

            // Check corner handles for resize
            int corner = refLayer.HitTestCorner(docX, docY, handleRadius);
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

            // Check edge handles for resize (new!)
            int edge = refLayer.HitTestEdge(docX, docY, handleRadius);
            if (edge >= 0)
            {
                // Use horizontal/vertical resize cursors for edges
                // Account for rotation when determining cursor
                float rotation = refLayer.Rotation;
                bool isVerticalEdge = edge == 0 || edge == 2; // Top or Bottom
                
                // Adjust for rotation - swap cursor type if rotated ~90 degrees
                float normalizedRotation = Math.Abs(rotation % 180);
                bool swapCursor = normalizedRotation > 45 && normalizedRotation < 135;
                
                if (swapCursor)
                    isVerticalEdge = !isVerticalEdge;
                
                return isVerticalEdge 
                    ? InputSystemCursorShape.SizeNorthSouth 
                    : InputSystemCursorShape.SizeWestEast;
            }

            // Check if inside layer bounds (for move cursor)
            if (refLayer.HitTest(docX, docY))
            {
                return InputSystemCursorShape.SizeAll; // Move cursor
            }

            return null;
        }

        /// <summary>
        /// Hit tests rotation handles (positioned outside corners).
        /// </summary>
        /// <returns>Corner index (0-3) if near a rotation handle, or -1.</returns>
        private int HitTestRotationHandle(ReferenceLayer refLayer, float docX, float docY, float handleRadius, float rotationOffset)
        {
            var corners = refLayer.GetCorners();
            float cx = refLayer.CenterX;
            float cy = refLayer.CenterY;

            for (int i = 0; i < 4; i++)
            {
                // Calculate rotation handle position (extended outward from corner)
                float cornerX = corners[i].x;
                float cornerY = corners[i].y;

                // Direction from center to corner
                float dirX = cornerX - cx;
                float dirY = cornerY - cy;
                float len = MathF.Sqrt(dirX * dirX + dirY * dirY);
                if (len < 0.001f) continue;

                // Normalize and extend
                dirX /= len;
                dirY /= len;
                float handleX = cornerX + dirX * rotationOffset;
                float handleY = cornerY + dirY * rotationOffset;

                // Check distance
                float dx = docX - handleX;
                float dy = docY - handleY;
                if (dx * dx + dy * dy <= handleRadius * handleRadius)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Hit tests all reference layers in the document.
        /// Locked layers are completely ignored and cannot be selected by clicking.
        /// </summary>
        private ReferenceLayer? HitTestReferenceLayers(float docX, float docY)
        {
            if (Document == null) return null;

            // Test in reverse order (topmost first)
            var refLayers = Document.GetAllReferenceLayers();
            for (int i = refLayers.Count - 1; i >= 0; i--)
            {
                var layer = refLayers[i];
                // Skip locked layers entirely - they should be completely ignored for interaction
                if (layer.IsEffectivelyLocked())
                    continue;
                    
                if (layer.IsEffectivelyVisible() && layer.HitTest(docX, docY))
                {
                    return layer;
                }
            }
            return null;
        }

        ///////////////////////////////////////////////////////////////////////
        // REFERENCE LAYER INTERACTION - POINTER HANDLERS
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Handles pointer pressed for reference layer dragging, resizing, and rotating.
        /// Returns true if the event was handled.
        /// </summary>
        private bool RefLayer_TryHandlePointerPressed(PointerRoutedEventArgs e)
        {
            if (Document == null) return false;

            var pt = e.GetCurrentPoint(_mainCanvas);
            if (!pt.Properties.IsLeftButtonPressed)
                return false;

            var screenPos = pt.Position;
            var docPos = ScreenToDocPoint(screenPos);
            float docX = (float)docPos.X;
            float docY = (float)docPos.Y;
            float handleRadius = RefLayerHandleHitRadius / (float)_zoom.Scale;
            float rotationOffset = RefLayerRotationHandleOffset / (float)_zoom.Scale;

            // First check if clicking on the selected layer's handles (only if not locked)
            if (_selectedReferenceLayer != null && !_selectedReferenceLayer.IsEffectivelyLocked() && _selectedReferenceLayer.IsEffectivelyVisible())
            {
                // Check rotation handles first (outside corners)
                int rotationHandle = HitTestRotationHandle(_selectedReferenceLayer, docX, docY, handleRadius, rotationOffset);
                if (rotationHandle >= 0)
                {
                    _refLayerRotating = true;
                    _refLayerDragStartRotation = _selectedReferenceLayer.Rotation;

                    // Calculate initial angle from center to pointer
                    float cx = _selectedReferenceLayer.CenterX;
                    float cy = _selectedReferenceLayer.CenterY;
                    _refLayerRotationStartAngle = MathF.Atan2(docY - cy, docX - cx) * 180f / MathF.PI;

                    _mainCanvas.CapturePointer(e.Pointer);
                    return true;
                }

                // Check corner handles (for resize)
                int corner = _selectedReferenceLayer.HitTestCorner(docX, docY, handleRadius);
                if (corner >= 0)
                {
                    _refLayerResizing = true;
                    _refLayerEdgeResizing = false;
                    _refLayerResizeCorner = corner;
                    _refLayerDragStartX = _selectedReferenceLayer.PositionX;
                    _refLayerDragStartY = _selectedReferenceLayer.PositionY;
                    _refLayerDragStartScale = _selectedReferenceLayer.Scale;
                    _refLayerDragPointerStartX = docX;
                    _refLayerDragPointerStartY = docY;

                    _mainCanvas.CapturePointer(e.Pointer);
                    return true;
                }

                // Check edge handles (for resize when corners are off-screen)
                int edge = _selectedReferenceLayer.HitTestEdge(docX, docY, handleRadius);
                if (edge >= 0)
                {
                    _refLayerResizing = true;
                    _refLayerEdgeResizing = true;
                    _refLayerResizeEdge = edge;
                    _refLayerDragStartX = _selectedReferenceLayer.PositionX;
                    _refLayerDragStartY = _selectedReferenceLayer.PositionY;
                    _refLayerDragStartScale = _selectedReferenceLayer.Scale;
                    _refLayerDragPointerStartX = docX;
                    _refLayerDragPointerStartY = docY;

                    _mainCanvas.CapturePointer(e.Pointer);
                    return true;
                }

                // Check if click is inside the layer (for move)
                if (_selectedReferenceLayer.HitTest(docX, docY))
                {
                    _refLayerDragging = true;
                    _refLayerDragStartX = _selectedReferenceLayer.PositionX;
                    _refLayerDragStartY = _selectedReferenceLayer.PositionY;
                    _refLayerDragPointerStartX = docX;
                    _refLayerDragPointerStartY = docY;

                    _mainCanvas.CapturePointer(e.Pointer);
                    return true;
                }
            }

            // Check if clicking on any unlocked reference layer to select it
            // (HitTestReferenceLayers already skips locked layers)
            var hitLayer = HitTestReferenceLayers(docX, docY);
            if (hitLayer != null && hitLayer != _selectedReferenceLayer)
            {
                _selectedReferenceLayer = hitLayer;

                // Start dragging (we know it's not locked because HitTestReferenceLayers skips locked layers)
                _refLayerDragging = true;
                _refLayerDragStartX = hitLayer.PositionX;
                _refLayerDragStartY = hitLayer.PositionY;
                _refLayerDragPointerStartX = docX;
                _refLayerDragPointerStartY = docY;

                _mainCanvas.CapturePointer(e.Pointer);
                InvalidateMainCanvas();
                return true;
            }

            // If clicking on the currently selected layer but it's now locked, deselect it
            if (_selectedReferenceLayer != null && _selectedReferenceLayer.IsEffectivelyLocked())
            {
                // Check if clicking inside the locked layer's bounds
                if (_selectedReferenceLayer.HitTest(docX, docY))
                {
                    // Deselect the locked layer and let the click pass through
                    _selectedReferenceLayer = null;
                    InvalidateMainCanvas();
                    // Don't return true - let other handlers process this click
                    return false;
                }
            }

            // Clicking elsewhere deselects
            if (_selectedReferenceLayer != null)
            {
                // Only deselect if clicking inside canvas area but outside any reference layer
                bool insideCanvas = docX >= 0 && docX < Document.PixelWidth &&
                                   docY >= 0 && docY < Document.PixelHeight;
                if (insideCanvas)
                {
                    _selectedReferenceLayer = null;
                    InvalidateMainCanvas();
                    // Don't return true - let other handlers process this click
                }
            }

            return false;
        }

        /// <summary>
        /// Handles pointer moved for reference layer dragging, resizing, and rotating.
        /// Returns true if the event was handled.
        /// </summary>
        private bool RefLayer_TryHandlePointerMoved(PointerRoutedEventArgs e)
        {
            if (Document == null) return false;

            // Handle active manipulation only - hover cursor is handled by Input.cs
            if (!_refLayerDragging && !_refLayerResizing && !_refLayerRotating)
                return false;

            if (_selectedReferenceLayer == null)
                return false;

            var pt = e.GetCurrentPoint(_mainCanvas);
            var screenPos = pt.Position;
            var docPos = ScreenToDocPoint(screenPos);
            float docX = (float)docPos.X;
            float docY = (float)docPos.Y;

            // Keep cursor updated during active manipulation
            var refCursor = GetRefLayerCursor(screenPos);
            if (refCursor.HasValue)
            {
                ProtectedCursor = InputSystemCursor.Create(refCursor.Value);
            }

            if (_refLayerRotating)
            {
                // Calculate current angle from center to pointer
                float cx = _selectedReferenceLayer.CenterX;
                float cy = _selectedReferenceLayer.CenterY;
                float currentAngle = MathF.Atan2(docY - cy, docX - cx) * 180f / MathF.PI;

                // Calculate rotation delta
                float deltaAngle = currentAngle - _refLayerRotationStartAngle;

                // Apply rotation (with optional snapping when Shift is held)
                float newRotation = _refLayerDragStartRotation + deltaAngle;

                // Snap to 15-degree increments when Shift is held
                if (IsKeyDown(Windows.System.VirtualKey.Shift))
                {
                    newRotation = MathF.Round(newRotation / 15f) * 15f;
                }

                _selectedReferenceLayer.Rotation = newRotation;

                InvalidateMainCanvas();
                return true;
            }

            if (_refLayerResizing)
            {
                if (_refLayerEdgeResizing)
                {
                    // Edge handle resizing - scale uniformly based on distance from center
                    float cx = _selectedReferenceLayer.CenterX;
                    float cy = _selectedReferenceLayer.CenterY;
                    
                    // Calculate distances from center to pointer positions
                    float startDistX = _refLayerDragPointerStartX - cx;
                    float startDistY = _refLayerDragPointerStartY - cy;
                    float startDist = MathF.Sqrt(startDistX * startDistX + startDistY * startDistY);
                    
                    float currentDistX = docX - cx;
                    float currentDistY = docY - cy;
                    float currentDist = MathF.Sqrt(currentDistX * currentDistX + currentDistY * currentDistY);
                    
                    if (startDist > 0.01f)
                    {
                        float scaleFactor = currentDist / startDist;
                        float newScale = _refLayerDragStartScale * scaleFactor;
                        newScale = Math.Clamp(newScale, 0.01f, 100f);
                        
                        // Keep center fixed while scaling
                        float oldCenterX = _refLayerDragStartX + _selectedReferenceLayer.ImageWidth * _refLayerDragStartScale / 2f;
                        float oldCenterY = _refLayerDragStartY + _selectedReferenceLayer.ImageHeight * _refLayerDragStartScale / 2f;
                        
                        float newHalfW = _selectedReferenceLayer.ImageWidth * newScale / 2f;
                        float newHalfH = _selectedReferenceLayer.ImageHeight * newScale / 2f;
                        
                        _selectedReferenceLayer.Scale = newScale;
                        _selectedReferenceLayer.PositionX = oldCenterX - newHalfW;
                        _selectedReferenceLayer.PositionY = oldCenterY - newHalfH;
                    }
                }
                else
                {
                    // Corner handle resizing - existing code
                    // For rotated objects, we need to:
                    // 1. Calculate scale based on distance from the ORIGINAL anchor point
                    // 2. Keep the anchor corner fixed in world space
                    
                    float rotation = _selectedReferenceLayer.Rotation;
                    float radians = rotation * MathF.PI / 180f;
                    float cos = MathF.Cos(radians);
                    float sin = MathF.Sin(radians);
                    
                    // Calculate the ORIGINAL center and half-sizes from saved start state
                    float oldHalfW = _selectedReferenceLayer.ImageWidth * _refLayerDragStartScale / 2f;
                    float oldHalfH = _selectedReferenceLayer.ImageHeight * _refLayerDragStartScale / 2f;
                    float oldCenterX = _refLayerDragStartX + oldHalfW;
                    float oldCenterY = _refLayerDragStartY + oldHalfH;
                    
                    // Determine anchor point in local space (opposite corner from dragged corner)
                    float anchorLocalX, anchorLocalY;
                    switch (_refLayerResizeCorner)
                    {
                        case 0: // TL dragged, anchor BR
                            anchorLocalX = oldHalfW;
                            anchorLocalY = oldHalfH;
                            break;
                        case 1: // TR dragged, anchor BL
                            anchorLocalX = -oldHalfW;
                            anchorLocalY = oldHalfH;
                            break;
                        case 2: // BR dragged, anchor TL
                            anchorLocalX = -oldHalfW;
                            anchorLocalY = -oldHalfH;
                            break;
                        case 3: // BL dragged, anchor TR
                            anchorLocalX = oldHalfW;
                            anchorLocalY = -oldHalfH;
                            break;
                        default:
                            anchorLocalX = 0;
                            anchorLocalY = 0;
                            break;
                    }
                    
                    // Transform anchor from local to world space
                    float anchorWorldX = oldCenterX + anchorLocalX * cos - anchorLocalY * sin;
                    float anchorWorldY = oldCenterY + anchorLocalX * sin + anchorLocalY * cos;
                    
                    // Calculate distance from anchor point to pointer positions
                    // This determines the scale factor
                    float startDistX = _refLayerDragPointerStartX - anchorWorldX;
                    float startDistY = _refLayerDragPointerStartY - anchorWorldY;
                    float startDist = MathF.Sqrt(startDistX * startDistX + startDistY * startDistY);
                    
                    float currentDistX = docX - anchorWorldX;
                    float currentDistY = docY - anchorWorldY;
                    float currentDist = MathF.Sqrt(currentDistX * currentDistX + currentDistY * currentDistY);
                    
                    if (startDist > 0.01f)
                    {
                        float scaleFactor = currentDist / startDist;
                        float newScale = _refLayerDragStartScale * scaleFactor;
                        newScale = Math.Clamp(newScale, 0.01f, 100f);
                        
                        // Calculate the new half-sizes
                        float newHalfW = _selectedReferenceLayer.ImageWidth * newScale / 2f;
                        float newHalfH = _selectedReferenceLayer.ImageHeight * newScale / 2f;
                        
                        // Calculate the new anchor position in local space (scaled)
                        float newAnchorLocalX, newAnchorLocalY;
                        switch (_refLayerResizeCorner)
                        {
                            case 0: // TL dragged, anchor BR
                                newAnchorLocalX = newHalfW;
                                newAnchorLocalY = newHalfH;
                                break;
                            case 1: // TR dragged, anchor BL
                                newAnchorLocalX = -newHalfW;
                                newAnchorLocalY = newHalfH;
                                break;
                            case 2: // BR dragged, anchor TL
                                newAnchorLocalX = -newHalfW;
                                newAnchorLocalY = -newHalfH;
                                break;
                            case 3: // BL dragged, anchor TR
                                newAnchorLocalX = newHalfW;
                                newAnchorLocalY = -newHalfH;
                                break;
                            default:
                                newAnchorLocalX = 0;
                                newAnchorLocalY = 0;
                                break;
                        }
                        
                        // Calculate the new center position to keep the anchor in place
                        // anchorWorld = newCenter + rotate(newAnchorLocal)
                        // newCenter = anchorWorld - rotate(newAnchorLocal)
                        float newCenterX = anchorWorldX - (newAnchorLocalX * cos - newAnchorLocalY * sin);
                        float newCenterY = anchorWorldY - (newAnchorLocalX * sin + newAnchorLocalY * cos);
                        
                        // Convert center to top-left position
                        float newPosX = newCenterX - newHalfW;
                        float newPosY = newCenterY - newHalfH;
                        
                        // Apply the changes
                        _selectedReferenceLayer.Scale = newScale;
                        _selectedReferenceLayer.PositionX = newPosX;
                        _selectedReferenceLayer.PositionY = newPosY;
                    }
                }

                InvalidateMainCanvas();
                return true;
            }

            if (_refLayerDragging)
            {
                // Calculate offset from drag start
                float deltaX = docX - _refLayerDragPointerStartX;
                float deltaY = docY - _refLayerDragPointerStartY;

                _selectedReferenceLayer.PositionX = _refLayerDragStartX + deltaX;
                _selectedReferenceLayer.PositionY = _refLayerDragStartY + deltaY;

                InvalidateMainCanvas();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles pointer released for reference layer dragging, resizing, and rotating.
        /// Returns true if the event was handled.
        /// </summary>
        private bool RefLayer_TryHandlePointerReleased(PointerRoutedEventArgs e)
        {
            if (!_refLayerDragging && !_refLayerResizing && !_refLayerRotating)
                return false;

            _refLayerDragging = false;
            _refLayerResizing = false;
            _refLayerRotating = false;
            _mainCanvas.ReleasePointerCaptures();

            // Restore default cursor
            ProtectedCursor = _targetCursor;

            return true;
        }

        ///////////////////////////////////////////////////////////////////////
        // REFERENCE LAYER PUBLIC API
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens a file picker to add a reference layer.
        /// </summary>
        public async Task AddReferenceImageAsync()
        {
            if (Document == null) return;

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".gif");
            picker.FileTypeFilter.Add(".webp");

            // Initialize the picker with the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.PixlPunktMainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            // Load the image using SkiaSharp (cross-platform)
            try
            {
                using var stream = await file.OpenStreamForReadAsync();
                var (pixels, width, height) = SkiaImageEncoder.DecodeFromStream(stream);

                var name = Path.GetFileNameWithoutExtension(file.Name);
                var refLayer = Document.AddReferenceLayer(name, pixels, width, height, file.Path);

                // Select the new layer
                _selectedReferenceLayer = refLayer;
                InvalidateMainCanvas();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load reference image: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes the currently selected reference layer.
        /// </summary>
        public void DeleteSelectedReferenceLayer()
        {
            if (Document == null || _selectedReferenceLayer == null) return;

            Document.RemoveReferenceLayer(_selectedReferenceLayer);
            _selectedReferenceLayer = null;
            InvalidateMainCanvas();
        }

        /// <summary>
        /// Resets the transform of the selected reference layer.
        /// </summary>
        public void ResetSelectedReferenceLayerTransform()
        {
            if (_selectedReferenceLayer == null) return;

            _selectedReferenceLayer.ResetTransform();
            _selectedReferenceLayer.FitToCanvas(Document.PixelWidth, Document.PixelHeight);
            InvalidateMainCanvas();
        }

        /// <summary>
        /// Toggles the lock state of the selected reference layer.
        /// </summary>
        public void ToggleSelectedReferenceLayerLock()
        {
            if (_selectedReferenceLayer == null) return;

            _selectedReferenceLayer.Locked = !_selectedReferenceLayer.Locked;
            InvalidateMainCanvas();
        }

        /// <summary>
        /// Toggles the visibility of the selected reference layer.
        /// </summary>
        public void ToggleSelectedReferenceLayerVisibility()
        {
            if (_selectedReferenceLayer == null) return;

            _selectedReferenceLayer.Visible = !_selectedReferenceLayer.Visible;
            InvalidateMainCanvas();
        }
    }
}
