using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.Core.Tools.Selection;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;
using SS = PixlPunkt.Uno.UI.CanvasHost.Selection.SelectionSubsystem;

namespace PixlPunkt.Uno.UI.CanvasHost.Selection
{
    /// <summary>
    /// Handles pointer input events for selection operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionInputHandler processes pointer pressed, moved, and released events to enable:
    /// - Marquee selection creation
    /// - Selection movement (dragging)
    /// - Scale handle manipulation
    /// - Rotation handle manipulation
    /// - Pivot point adjustment
    /// </para>
    /// <para>
    /// <strong>Coordinate Flow</strong>: Input arrives in view coordinates and is converted to
    /// document coordinates via the provided ViewToDoc callback.
    /// </para>
    /// </remarks>
    public sealed class SelectionInputHandler
    {
        private readonly SelectionSubsystem _state;
        private readonly SelectionHitTesting _hitTest;
        private readonly SelectionTransformOps _transform;

        // External callbacks
        private Func<Point, (int x, int y)>? _viewToDoc;
        private Func<Point, (int x, int y)>? _viewToDocClamped;
        private Func<Point, int, int, bool>? _tryGetDocInside;
        private Func<int, int, RectInt32, bool>? _tryGetTileRect;
        private Action? _requestRedraw;
        private Action? _liftSelection;
        private Action? _commitFloating;
        private Func<UIElement>? _getCanvas;
        private Func<ISelectionTool?>? _getActiveTool;
        private Func<ToolState?>? _getToolState;
        private Func<RasterLayer?>? _getActiveLayer;
        private Action<InputSystemCursorShape>? _setCursor;
        private Func<int, int, bool>? _isInsideTransformedSel;

        /// <summary>
        /// Gets or sets the function to convert view to document coordinates.
        /// </summary>
        public Func<Point, (int x, int y)>? ViewToDoc
        {
            get => _viewToDoc;
            set => _viewToDoc = value;
        }

        /// <summary>
        /// Gets or sets the function to convert view to document coordinates (clamped).
        /// </summary>
        public Func<Point, (int x, int y)>? ViewToDocClamped
        {
            get => _viewToDocClamped;
            set => _viewToDocClamped = value;
        }

        /// <summary>
        /// Gets or sets the function to check if a view point is inside the document.
        /// </summary>
        public Func<Point, int, int, bool>? TryGetDocInside
        {
            get => _tryGetDocInside;
            set => _tryGetDocInside = value;
        }

        /// <summary>
        /// Gets or sets the function to get tile rectangle at document position.
        /// </summary>
        public Func<int, int, RectInt32, bool>? TryGetTileRect
        {
            get => _tryGetTileRect;
            set => _tryGetTileRect = value;
        }

        /// <summary>
        /// Gets or sets the action to request a canvas redraw.
        /// </summary>
        public Action? RequestRedraw
        {
            get => _requestRedraw;
            set => _requestRedraw = value;
        }

        /// <summary>
        /// Gets or sets the action to lift selection to floating.
        /// </summary>
        public Action? LiftSelection
        {
            get => _liftSelection;
            set => _liftSelection = value;
        }

        /// <summary>
        /// Gets or sets the action to commit floating selection.
        /// </summary>
        public Action? CommitFloating
        {
            get => _commitFloating;
            set => _commitFloating = value;
        }

        /// <summary>
        /// Gets or sets the function to get the canvas control.
        /// </summary>
        public Func<UIElement>? GetCanvas
        {
            get => _getCanvas;
            set => _getCanvas = value;
        }

        /// <summary>
        /// Gets or sets the function to get the active selection tool.
        /// </summary>
        public Func<ISelectionTool?>? GetActiveTool
        {
            get => _getActiveTool;
            set => _getActiveTool = value;
        }

        /// <summary>
        /// Gets or sets the function to get the tool state.
        /// </summary>
        public Func<ToolState?>? GetToolState
        {
            get => _getToolState;
            set => _getToolState = value;
        }

        /// <summary>
        /// Gets or sets the function to get the active layer.
        /// </summary>
        public Func<RasterLayer?>? GetActiveLayer
        {
            get => _getActiveLayer;
            set => _getActiveLayer = value;
        }

        /// <summary>
        /// Gets or sets the action to set the cursor shape.
        /// </summary>
        public Action<InputSystemCursorShape>? SetCursor
        {
            get => _setCursor;
            set => _setCursor = value;
        }

        /// <summary>
        /// Gets or sets the function to check if a point is inside the transformed selection.
        /// </summary>
        public Func<int, int, bool>? IsInsideTransformedSelection
        {
            get => _isInsideTransformedSel;
            set => _isInsideTransformedSel = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionInputHandler"/> class.
        /// </summary>
        /// <param name="state">The selection subsystem state.</param>
        /// <param name="hitTest">The hit testing handler.</param>
        /// <param name="transform">The transform operations handler.</param>
        public SelectionInputHandler(
            SelectionSubsystem state,
            SelectionHitTesting hitTest,
            SelectionTransformOps transform)
        {
            _state = state;
            _hitTest = hitTest;
            _transform = transform;
        }

        /// <summary>
        /// Handles double-tap to select tile under pointer.
        /// </summary>
        /// <param name="e">The double-tapped event args.</param>
        /// <param name="isSelectTool">Whether a selection tool is active.</param>
        /// <returns>True if the event was handled.</returns>
        public bool HandleDoubleTapped(DoubleTappedRoutedEventArgs e, bool isSelectTool)
        {
            if (!isSelectTool) return false;

            var canvas = _getCanvas?.Invoke();
            if (canvas == null) return false;

            var pos = e.GetPosition(canvas);

            // Try to get document position
            int x = 0, y = 0;
            bool inside = _tryGetDocInside?.Invoke(pos, x, y) ?? false;
            if (!inside) return false;

            // Get tile rect at position
            var tileRect = new RectInt32();
            bool hasTile = _tryGetTileRect?.Invoke(x, y, tileRect) ?? false;
            if (!hasTile) return false;

            var rl = _getActiveLayer?.Invoke();
            if (rl == null) return false;

            // Kill any in-progress drag
            _state.Drag = SS.SelDrag.None;
            _state.HavePreview = false;

            if (_state.Floating)
                _commitFloating?.Invoke();

            _state.Region.EnsureSize(rl.Surface.Width, rl.Surface.Height);
            _state.Region.Clear();
            _state.Region.AddRect(tileRect);

            _state.Rect = _state.Region.Bounds;
            _state.Active = true;
            _state.Floating = false;
            _state.Buffer = null;
            _state.State = SS.SelectionState.Armed;
            _state.OrigW = tileRect.Width;
            _state.OrigH = tileRect.Height;
            _state.OrigCenterX = tileRect.X + tileRect.Width / 2;
            _state.OrigCenterY = tileRect.Y + tileRect.Height / 2;

            _state.ResetTransform();
            _state.NotifyToolState();

            _requestRedraw?.Invoke();
            e.Handled = true;
            return true;
        }

        /// <summary>
        /// Handles pointer pressed events.
        /// </summary>
        /// <param name="e">The pointer event args.</param>
        /// <param name="isSelectTool">Whether a selection tool is active.</param>
        /// <returns>True if the event was handled.</returns>
        public bool HandlePointerPressed(PointerRoutedEventArgs e, bool isSelectTool)
        {
            if (!isSelectTool) return false;

            var canvas = _getCanvas?.Invoke();
            if (canvas == null) return false;

            var pt = e.GetCurrentPoint(canvas);
            if (!pt.Properties.IsLeftButtonPressed) return false;

            var viewPos = pt.Position;
            bool shiftDown = IsShiftDown(), altDown = IsAltDown();

            if (_state.Active && _state.State == SS.SelectionState.Armed && !shiftDown && !altDown)
            {
                // Check pivot handle first
                if (_hitTest.HitTestPivotHandle(viewPos))
                {
                    _state.Drag = SS.SelDrag.Pivot;
                    if (!_state.Floating) _liftSelection?.Invoke();
                    _state.Dirty = true;
                    return true;
                }

                // Check rotation handles
                var rotHandle = _hitTest.HitTestRotateHandle(viewPos);
                if (rotHandle != SS.RotHandle.None)
                {
                    _state.Drag = SS.SelDrag.Rotate;
                    var (docX, docY) = _viewToDoc?.Invoke(viewPos) ?? (0, 0);
                    var (pivotDocX, pivotDocY) = _transform.GetPivotPositionDoc();
                    _state.RotFixedPivotX = pivotDocX;
                    _state.RotFixedPivotY = pivotDocY;
                    _state.RotStartCenterX = _state.OrigCenterX;
                    _state.RotStartCenterY = _state.OrigCenterY;
                    _state.RotStartAngleDeg = _state.AngleDeg;
                    _state.RotStartPointerAngleDeg = Math.Atan2(docY - pivotDocY, docX - pivotDocX) * 180.0 / Math.PI;
                    if (!_state.Floating) _liftSelection?.Invoke();
                    _state.Dirty = true;
                    return true;
                }

                // Check scale handles
                var scaleHandle = _hitTest.HitTestHandle(viewPos);
                if (scaleHandle != SS.SelHandle.None)
                {
                    _state.Drag = SS.SelDrag.Scale;
                    _state.ActiveHandle = scaleHandle;
                    if (!_state.Floating) _liftSelection?.Invoke();
                    _state.ScaleStartFX = _state.FloatX;
                    _state.ScaleStartFY = _state.FloatY;
                    _state.ScaleStartW = _state.ScaledW;
                    _state.ScaleStartH = _state.ScaledH;
                    _state.ScaleStartScaleX = _state.ScaleX;
                    _state.ScaleStartScaleY = _state.ScaleY;
                    _state.Dirty = true;
                    return true;
                }

                // Move (inside selection)
                var (mx, my) = _viewToDoc?.Invoke(viewPos) ?? (0, 0);
                if (_isInsideTransformedSel?.Invoke(mx, my) ?? false)
                {
                    _state.Drag = SS.SelDrag.Move;
                    if (!_state.Floating) _liftSelection?.Invoke();
                    _state.MoveStartX = mx;
                    _state.MoveStartY = my;
                    _state.Dirty = true;
                    return true;
                }

                // Clicked outside - deselect
                if (_state.Floating)
                    _commitFloating?.Invoke();

                _state.Clear();
                _state.ToolState?.SetSelectionPresence(false, false);
                _getActiveTool?.Invoke()?.Cancel();
                _requestRedraw?.Invoke();
                return true;
            }

            // Start marquee selection
            if (_state.Drag == SS.SelDrag.None)
            {
                _state.Drag = SS.SelDrag.Marquee;
                _state.DragStartView = viewPos;

                if (_tryGetDocInside != null)
                {
                    int docPosX = 0, docPosY = 0;
                    if (_tryGetDocInside(viewPos, docPosX, docPosY))
                    {
                        var docPos = new Point(docPosX, docPosY);
                        _getActiveTool?.Invoke()?.PointerPressed(docPos, e);
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles pointer moved events.
        /// </summary>
        /// <param name="e">The pointer event args.</param>
        /// <param name="isSelectTool">Whether a selection tool is active.</param>
        /// <returns>True if the event was handled.</returns>
        public bool HandlePointerMoved(PointerRoutedEventArgs e, bool isSelectTool)
        {
            if (!isSelectTool) return false;

            var canvas = _getCanvas?.Invoke();
            if (canvas == null) return false;

            var pt = e.GetCurrentPoint(canvas);
            var viewPos = pt.Position;

            UpdateSelectionCursor(viewPos, isSelectTool);

            if (_state.Drag == SS.SelDrag.None) return false;

            var (docX, docY) = _viewToDoc?.Invoke(viewPos) ?? (0, 0);

            switch (_state.Drag)
            {
                case SS.SelDrag.Marquee:
                    var docPos = new Point(docX, docY);
                    var tool = _getActiveTool?.Invoke();
                    return tool?.PointerMoved(docPos, e) ?? false;

                case SS.SelDrag.Move:
                    if (_state.Floating)
                    {
                        int dx = docX - _state.MoveStartX, dy = docY - _state.MoveStartY;
                        _state.FloatX += dx;
                        _state.FloatY += dy;
                        _state.MoveStartX = docX;
                        _state.MoveStartY = docY;
                        _state.OrigCenterX += dx;
                        _state.OrigCenterY += dy;
                        OffsetSelectionRegion(dx, dy);
                        _state.Dirty = true;
                        _state.Changed = true;
                        _requestRedraw?.Invoke();
                    }
                    return true;

                case SS.SelDrag.Scale:
                    if (_state.Floating)
                    {
                        _transform.UpdateScaleFromHandle(docX, docY);
                        _state.Dirty = true;
                        _state.Changed = true;
                        _requestRedraw?.Invoke();
                    }
                    return true;

                case SS.SelDrag.Rotate:
                    if (_state.Floating)
                    {
                        UpdateRotation(docX, docY);
                        _state.Dirty = true;
                        _state.Changed = true;
                        _requestRedraw?.Invoke();
                    }
                    return true;

                case SS.SelDrag.Pivot:
                    if (_state.Floating)
                    {
                        _transform.UpdatePivotFromDrag(docX, docY);
                        _state.Dirty = true;
                    }
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Handles pointer released events.
        /// </summary>
        /// <param name="e">The pointer event args.</param>
        /// <param name="isSelectTool">Whether a selection tool is active.</param>
        /// <returns>True if the event was handled.</returns>
        public bool HandlePointerReleased(PointerRoutedEventArgs e, bool isSelectTool)
        {
            if (!isSelectTool) return false;

            var canvas = _getCanvas?.Invoke();
            if (canvas == null) return false;

            if (_state.Drag == SS.SelDrag.Marquee)
            {
                var viewPos = e.GetCurrentPoint(canvas).Position;
                var (docX, docY) = _viewToDoc?.Invoke(viewPos) ?? (0, 0);
                var docPos = new Point(docX, docY);
                var tool = _getActiveTool?.Invoke();
                tool?.PointerReleased(docPos, e);

                _state.Rect = _state.Region.Bounds;
                _state.Active = !_state.Region.IsEmpty;
                _state.State = _state.Active ? SS.SelectionState.Armed : SS.SelectionState.None;

                if (_state.Active)
                {
                    _state.AngleDeg = 0.0;
                    _state.CumulativeAngleDeg = 0.0;
                    _state.OrigW = _state.Rect.Width;
                    _state.OrigH = _state.Rect.Height;
                    _state.OrigCenterX = _state.Rect.X + _state.Rect.Width / 2;
                    _state.OrigCenterY = _state.Rect.Y + _state.Rect.Height / 2;
                    _state.ResetPivot();
                }

                var toolState = _getToolState?.Invoke();
                if (toolState?.ActiveToolId != ToolIds.Lasso || !(tool?.HasPreview ?? false))
                    _state.HavePreview = false;

                _state.ToolState?.SetSelectionPresence(_state.Active, false);
            }

            if ((_state.Drag == SS.SelDrag.Scale || _state.Drag == SS.SelDrag.Rotate) && _state.Floating && _state.Buffer != null)
            {
                BakeTransformsOnRelease();
            }

            if (_state.Drag == SS.SelDrag.Move && _state.Floating)
                _state.Changed = true;

            _state.Drag = SS.SelDrag.None;
            _state.ActiveHandle = SS.SelHandle.None;
            _state.Dirty = false;
            _requestRedraw?.Invoke();
            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        private void UpdateSelectionCursor(Point viewPt, bool isSelectTool)
        {
            var shape = InputSystemCursorShape.Arrow;
            _state.HoverHandle = SS.SelHandle.None;

            if (isSelectTool && _state.Active && _state.State == SS.SelectionState.Armed)
            {
                if (_hitTest.HitTestPivotHandle(viewPt))
                {
                    shape = InputSystemCursorShape.Hand;
                }
                else if (_hitTest.HitTestRotateHandle(viewPt) != SS.RotHandle.None)
                {
                    shape = InputSystemCursorShape.Cross;
                }
                else
                {
                    var handle = _hitTest.HitTestHandle(viewPt);
                    if (handle != SS.SelHandle.None)
                    {
                        _state.HoverHandle = handle;
                        shape = CursorForHandle(handle);
                    }
                    else
                    {
                        var (x, y) = _viewToDoc?.Invoke(viewPt) ?? (0, 0);
                        if (_isInsideTransformedSel?.Invoke(x, y) ?? false)
                            shape = InputSystemCursorShape.SizeAll;
                    }
                }
            }

            _setCursor?.Invoke(shape);
        }

        private static InputSystemCursorShape CursorForHandle(SS.SelHandle h) => h switch
        {
            SS.SelHandle.N or SS.SelHandle.S => InputSystemCursorShape.SizeNorthSouth,
            SS.SelHandle.E or SS.SelHandle.W => InputSystemCursorShape.SizeWestEast,
            SS.SelHandle.NE or SS.SelHandle.SW => InputSystemCursorShape.SizeNortheastSouthwest,
            SS.SelHandle.NW or SS.SelHandle.SE => InputSystemCursorShape.SizeNorthwestSoutheast,
            _ => InputSystemCursorShape.Arrow
        };

        private void UpdateRotation(int docX, int docY)
        {
            double pivotDocX = _state.RotFixedPivotX;
            double pivotDocY = _state.RotFixedPivotY;

            double dx = docX - pivotDocX;
            double dy = docY - pivotDocY;
            double currentAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double delta = currentAngle - _state.RotStartPointerAngleDeg;

            bool snap = IsShiftDown();
            double step = snap ? 15.0 : 1.0;
            double newAngle = Math.Round((_state.RotStartAngleDeg + delta) / step) * step;
            _state.AngleDeg = newAngle;

            // Orbit selection center around pivot if pivot is custom
            if (_state.PivotCustom && (Math.Abs(_state.PivotOffsetX) > 0.001 || Math.Abs(_state.PivotOffsetY) > 0.001))
            {
                double totalAngleRad = (_state.CumulativeAngleDeg + _state.AngleDeg) * Math.PI / 180.0;
                double localCenterOffsetX = -_state.PivotOffsetX;
                double localCenterOffsetY = -_state.PivotOffsetY;
                double cos = Math.Cos(totalAngleRad);
                double sin = Math.Sin(totalAngleRad);
                double rotatedOffsetX = localCenterOffsetX * cos - localCenterOffsetY * sin;
                double rotatedOffsetY = localCenterOffsetX * sin + localCenterOffsetY * cos;
                _state.OrigCenterX = (int)Math.Round(pivotDocX + rotatedOffsetX);
                _state.OrigCenterY = (int)Math.Round(pivotDocY + rotatedOffsetY);

                int handleW = (int)Math.Round((_state.OrigW > 0 ? _state.OrigW : _state.BufferWidth) * _state.ScaleX);
                int handleH = (int)Math.Round((_state.OrigH > 0 ? _state.OrigH : _state.BufferHeight) * _state.ScaleY);
                _state.FloatX = _state.OrigCenterX - handleW / 2;
                _state.FloatY = _state.OrigCenterY - handleH / 2;
            }

            _state.ToolState?.SetRotationAngle(_state.AngleDeg);
        }

        private void BakeTransformsOnRelease()
        {
            // Bake scale and rotation into buffer after drag release
            bool hasScale = Math.Abs(_state.ScaleX - 1.0) > 0.001 || Math.Abs(_state.ScaleY - 1.0) > 0.001;
            bool hasRotation = Math.Abs(_state.AngleDeg) > 0.1;

            if (hasScale)
            {
                _state.ScaleX = 1.0;
                _state.ScaleY = 1.0;
            }

            if (hasRotation)
            {
                _state.CumulativeAngleDeg += _state.AngleDeg;
                _state.AngleDeg = 0.0;
            }

            _state.PreviewBuf = null;
            _state.ToolState?.SetSelectionScale(100.0, 100.0, _state.ScaleLink);
            _state.ToolState?.SetRotationAngle(0.0);
            _state.Changed = true;
        }

        private void OffsetSelectionRegion(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return;
            _state.Region.SetOffset(_state.Region.OffsetX + dx, _state.Region.OffsetY + dy);
            _state.Rect = _state.Region.Bounds;
        }

        private static bool IsShiftDown() =>
            (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0;

        private static bool IsAltDown() =>
            (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & CoreVirtualKeyStates.Down) != 0;

        private static bool IsCtrlDown() =>
            (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;
    }
}