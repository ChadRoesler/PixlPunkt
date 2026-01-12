using System;
using System.Collections.Generic;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Rendering;
using PixlPunkt.Core.Tools;
using PixlPunkt.Core.Tools.Selection;
using PixlPunkt.UI.CanvasHost.Selection;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;
using RotHandle = PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem.RotHandle;
// Alias for subsystem types
using SelDrag = PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem.SelDrag;
using SelectionState = PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem.SelectionState;
using SelHandle = PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem.SelHandle;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// Selection subsystem for CanvasViewHost:
    /// - Creates, previews, and edits selections (marquee, move, scale, rotate).
    /// - Supports floating selections with full history (lift/commit/cancel).
    /// - Renders marching ants and interactive handles (scale/rotate).
    /// - Integrates system clipboard for copy/cut/paste.
    /// - Keeps ToolState synchronized with selection presence and transforms.
    /// 
    /// This partial class delegates to specialized subsystem classes:
    /// - SelectionSubsystem: State management
    /// - SelectionHitTesting: Handle and body hit detection
    /// - SelectionRenderer: Visual rendering
    /// - SelectionTransformOps: Scale/rotate/pivot calculations
    /// - SelectionClipboard: Copy/cut/paste operations
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ═══════════════════════════════════════════════════════════════
        // SELECTION SUBSYSTEM COMPONENTS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Selection state and coordination.</summary>
        private SelectionSubsystem? _selState;

        /// <summary>Hit testing for handles and selection body.</summary>
        private SelectionHitTesting? _selHitTest;

        /// <summary>Visual rendering for selection.</summary>
        private SelectionRenderer? _selRenderer;

        /// <summary>Transform operations (scale, rotate, pivot).</summary>
        private SelectionTransformOps? _selTransform;

        /// <summary>Clipboard operations (copy, cut, paste).</summary>
        private SelectionClipboard? _selClipboard;

        // ═══════════════════════════════════════════════════════════════
        // SELECTION TOOL INSTANCES (REGISTRY-BASED)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Context for selection tool factories.</summary>
        private SelectionToolContext? _selectionToolContext;

        /// <summary>Cached selection tool instances by kind.</summary>
        private readonly Dictionary<string, ISelectionTool> _selectionTools = new();

        // ═══════════════════════════════════════════════════════════════
        // SELECTION HISTORY TRACKING
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Captured selection region before a marquee operation starts.
        /// Used to track selection changes for history.
        /// </summary>
        private Core.Selection.SelectionRegion? _selectionBeforeMarquee;

        /// <summary>
        /// Captured combine mode at the start of a marquee operation.
        /// </summary>
        private SelectionChangeItem.SelectionChangeKind _marqueeCombineKind = SelectionChangeItem.SelectionChangeKind.Create;

        // ═══════════════════════════════════════════════════════════════
        // BRIDGE PROPERTIES (for History.cs compatibility)
        // ═══════════════════════════════════════════════════════════════

        private bool _selActive { get => _selState?.Active ?? false; set { if (_selState != null) _selState.Active = value; } }
        private RectInt32 _selRect { get => _selState?.Rect ?? default; set { if (_selState != null) _selState.Rect = value; } }
        private bool _selFloating { get => _selState?.Floating ?? false; set { if (_selState != null) _selState.Floating = value; } }
        private int _selFX { get => _selState?.FloatX ?? 0; set { if (_selState != null) _selState.FloatX = value; } }
        private int _selFY { get => _selState?.FloatY ?? 0; set { if (_selState != null) _selState.FloatY = value; } }
        private byte[]? _selBuf { get => _selState?.Buffer; set { if (_selState != null) _selState.Buffer = value; } }
        private int _selBW { get => _selState?.BufferWidth ?? 0; set { if (_selState != null) _selState.BufferWidth = value; } }
        private int _selBH { get => _selState?.BufferHeight ?? 0; set { if (_selState != null) _selState.BufferHeight = value; } }
        private SelectionState _selectionState { get => _selState?.State ?? SelectionState.None; set { if (_selState != null) _selState.State = value; } }
        private double _selScaleX { get => _selState?.ScaleX ?? 1.0; set { if (_selState != null) _selState.ScaleX = value; } }
        private double _selScaleY { get => _selState?.ScaleY ?? 1.0; set { if (_selState != null) _selState.ScaleY = value; } }
        private bool _selScaleLink { get => _selState?.ScaleLink ?? false; set { if (_selState != null) _selState.ScaleLink = value; } }
        private ScaleMode _selScaleFilter { get => _selState?.ScaleFilter ?? ScaleMode.NearestNeighbor; set { if (_selState != null) _selState.ScaleFilter = value; } }
        private RotationMode _selRotMode { get => _selState?.RotMode ?? RotationMode.RotSprite; set { if (_selState != null) _selState.RotMode = value; } }
        private double _selAngleDeg { get => _selState?.AngleDeg ?? 0.0; set { if (_selState != null) _selState.AngleDeg = value; } }
        private double _selCumulativeAngleDeg { get => _selState?.CumulativeAngleDeg ?? 0.0; set { if (_selState != null) _selState.CumulativeAngleDeg = value; } }
        private int _selOrigW { get => _selState?.OrigW ?? 0; set { if (_selState != null) _selState.OrigW = value; } }
        private int _selOrigH { get => _selState?.OrigH ?? 0; set { if (_selState != null) _selState.OrigH = value; } }
        private int _selOrigCenterX { get => _selState?.OrigCenterX ?? 0; set { if (_selState != null) _selState.OrigCenterX = value; } }
        private int _selOrigCenterY { get => _selState?.OrigCenterY ?? 0; set { if (_selState != null) _selState.OrigCenterY = value; } }
        private PixelChangeItem? _selPendingCs { get => _selState?.PendingCs; set { if (_selState != null) _selState.PendingCs = value; } }
        private RectInt32 _liftRect { get => _selState?.LiftRect ?? default; set { if (_selState != null) _selState.LiftRect = value; } }

        /// <summary>Gets the currently active selection tool based on ToolState.</summary>
        private ISelectionTool? ActiveSelectionTool
        {
            get
            {
                var toolId = _toolState?.ActiveToolId;
                if (toolId == null || !_toolState!.IsSelectionToolById(toolId)) return null;
                return GetOrCreateSelectionTool(toolId);
            }
        }

        /// <summary>
        /// Gets or creates a selection tool instance for the given kind.
        /// Tools are cached after first creation.
        /// </summary>
        private ISelectionTool? GetOrCreateSelectionTool(string toolId)
        {
            if (_selectionTools.TryGetValue(toolId, out var tool))
                return tool;

            if (_toolState == null || _selectionToolContext == null)
                return null;

            var registration = _toolState.GetSelectionRegistrationById(toolId);
            if (registration == null)
                return null;

            tool = registration.CreateTool(_selectionToolContext);
            if (tool != null)
            {
                // Configure with current settings
                var settings = _toolState.GetSettingsForToolId(toolId);
                if (settings != null)
                    tool.Configure(settings);

                _selectionTools[toolId] = tool;
            }

            return tool;
        }

        // ═══════════════════════════════════════════════════════════════
        // RENDERING HOOK
        // ═══════════════════════════════════════════════════════════════

        // Hook guard so we subscribe once to CompositionTarget.Rendering
        private bool _antsRenderHooked;

        /// <summary>
        /// Rendering tick used to advance marching ants and invalidate when needed.
        /// </summary>
        private void OnAntsRendering(object? sender, object args)
        {
            if (_selState == null) return;
            bool isTransforming = _selState.Drag == SelDrag.Scale || _selState.Drag == SelDrag.Rotate || _selState.Drag == SelDrag.Move;
            if ((_selState.Active || _selState.HavePreview || (_selState.Drag == SelDrag.Marquee && (ActiveSelectionTool?.NeedsContinuousRender ?? false))) && !isTransforming)
                InvalidateMainCanvas();
        }

        // ═══════════════════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════════════════

        private bool IsSelectTool => _toolState?.ActiveCategory == ToolCategory.Select;
        public bool HasSelection => _selState?.Active ?? false;

        private InputSystemCursorShape _curShape = InputSystemCursorShape.Arrow;

        /// <summary>Gets the scaled width of the floating selection.</summary>
        private int ScaledW => _selState?.ScaledW ?? 1;

        /// <summary>Gets the scaled height of the floating selection.</summary>
        private int ScaledH => _selState?.ScaledH ?? 1;

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Subscribes the rendering hook for marching ants and initializes subsystem components.
        /// </summary>
        private void InitSelection()
        {
            // Initialize subsystem components
            _selState = new SelectionSubsystem(_selRegion);
            _selHitTest = new SelectionHitTesting(_selState);
            _selTransform = new SelectionTransformOps(_selState);
            _selRenderer = new SelectionRenderer(_selState, _selHitTest);
            _selClipboard = new SelectionClipboard(_selState);

            // Wire up dependencies for hit testing
            _selHitTest.GetDestRect = () => _zoom.GetDestRect();
            _selHitTest.GetScale = () => _zoom.Scale;

            // Wire up dependencies for transform operations
            _selTransform.RequestRedraw = () => InvalidateMainCanvas();
            _selTransform.GetToolState = () => _toolState;
            _selTransform.GetApplyFromTool = () => _selApplyFromTool;
            _selTransform.SetPushToTool = v => _selPushToTool = v;

            // Wire up dependencies for renderer
            _selRenderer.GetDestRect = () => _zoom.GetDestRect();
            _selRenderer.GetScale = () => _zoom.Scale;
            _selRenderer.GetActiveTool = () => ActiveSelectionTool;
            _selRenderer.NeedsContinuousRender = drag => drag == SelDrag.Marquee && (ActiveSelectionTool?.NeedsContinuousRender ?? false);

            // Wire up dependencies for clipboard
            _selClipboard.GetActiveLayer = () => Document.ActiveLayer as RasterLayer;
            _selClipboard.GetDocWidth = () => Document.PixelWidth;
            _selClipboard.GetDocHeight = () => Document.PixelHeight;
            _selClipboard.GetZoom = () => _zoom;
            _selClipboard.GetHoverPosition = () => (_hoverX, _hoverY, _hoverValid);
            _selClipboard.RequestRedraw = () => InvalidateMainCanvas();
            _selClipboard.CommitFloating = () => CommitFloatingWithHistory();
            _selClipboard.ApplyWithHistory = (rect, mutator) => ApplyWithHistory(rect, mutator, "Delete Selection");
            _selClipboard.SetCursor = SetCursor;
            _selClipboard.PropagateTileChanges = (bounds) => PropagateSelectionChangesToMappedTiles(bounds);
            _selClipboard.PushSelectionHistory = PushSelectionChangeToHistory;

            // Hook rendering for marching ants
            if (!_antsRenderHooked)
            {
                CompositionTarget.Rendering += OnAntsRendering;
                _antsRenderHooked = true;
            }

            // Create selection tool context
            _selectionToolContext = new SelectionToolContext
            {
                GetSelectionRegion = () => _selRegion,
                RequestRedraw = () => InvalidateMainCanvas(),
                GetDocumentSize = () => (Document.PixelWidth, Document.PixelHeight),
                GetActiveLayer = () => Document.ActiveLayer as RasterLayer,
                GetBrushOffsets = () => _stroke?.GetCurrentBrushOffsets() ?? Array.Empty<(int, int)>()
            };

            // Set tool state reference
            _selState.ToolState = _toolState;
        }

        /// <summary>
        /// Pushes a selection change item to the document history.
        /// </summary>
        private void PushSelectionChangeToHistory(SelectionChangeItem item)
        {
            if (item == null || !item.HasChanges) return;
            Document.History.Push(item);
            HistoryStateChanged?.Invoke();
        }

        /// <summary>
        /// Pushes a transform history item if the transform actually changed.
        /// Used for move and pivot operations that don't modify the buffer.
        /// </summary>
        private void PushTransformHistory(SelDrag dragType)
        {
            if (_selState == null || !_selState.DragStartSnapshot.HasValue) return;

            var beforeSnapshot = _selState.DragStartSnapshot.Value;
            var afterSnapshot = _selState.CaptureTransformSnapshot(includeBuffer: false);

            // Determine the transform kind
            var kind = dragType switch
            {
                SelDrag.Move => SelectionTransformItem.TransformKind.Move,
                SelDrag.Scale => SelectionTransformItem.TransformKind.Scale,
                SelDrag.Rotate => SelectionTransformItem.TransformKind.Rotate,
                SelDrag.Pivot => SelectionTransformItem.TransformKind.Move, // Pivot changes are treated as "move" for history
                _ => SelectionTransformItem.TransformKind.Move
            };

            var item = new SelectionTransformItem(
                kind,
                beforeSnapshot,
                afterSnapshot,
                ApplyTransformSnapshotAndRedraw
            );

            // Only push if there were actual changes
            if (item.HasChanges)
            {
                Document.History.Push(item);
                HistoryStateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Pushes a transform history item for scale/rotate operations that modify the buffer.
        /// Called AFTER BakeTransformsOnRelease() so the after snapshot captures the baked state.
        /// </summary>
        private void PushTransformHistoryWithBakedState(SelDrag dragType)
        {
            if (_selState == null || !_selState.DragStartSnapshot.HasValue) return;

            var beforeSnapshot = _selState.DragStartSnapshot.Value;
            // Capture after with buffer since we've just baked the transforms
            var afterSnapshot = _selState.CaptureTransformSnapshot(includeBuffer: true);

            var kind = dragType switch
            {
                SelDrag.Scale => SelectionTransformItem.TransformKind.Scale,
                SelDrag.Rotate => SelectionTransformItem.TransformKind.Rotate,
                _ => SelectionTransformItem.TransformKind.Scale
            };

            var item = new SelectionTransformItem(
                kind,
                beforeSnapshot,
                afterSnapshot,
                ApplyTransformSnapshotAndRedraw
            );

            if (item.HasChanges)
            {
                Document.History.Push(item);
                HistoryStateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Pushes selection change history after a marquee operation completes.
        /// </summary>
        private void PushMarqueeSelectionHistory()
        {
            if (_selectionBeforeMarquee == null) return;

            var afterRegion = _selRegion.Clone();

            // Use the captured combine mode from when the marquee started
            var kind = _marqueeCombineKind;

            // Handle edge cases
            bool hadSelectionBefore = !_selectionBeforeMarquee.IsEmpty;
            bool hasSelectionAfter = !afterRegion.IsEmpty;

            // If no selection after and it was a create/replace, that's a clear
            if (!hasSelectionAfter && kind == SelectionChangeItem.SelectionChangeKind.Create)
            {
                kind = SelectionChangeItem.SelectionChangeKind.Clear;
            }

            var item = new SelectionChangeItem(
                kind,
                _selectionBeforeMarquee,
                afterRegion,
                ApplySelectionRegionFromHistory
            );

            if (item.HasChanges)
            {
                Document.History.Push(item);
                HistoryStateChanged?.Invoke();
            }

            _selectionBeforeMarquee = null;
        }

        /// <summary>
        /// Applies a selection region from history (undo/redo).
        /// </summary>
        private void ApplySelectionRegionFromHistory(Core.Selection.SelectionRegion region)
        {
            if (_selState == null) return;

            // Copy the region data directly - this preserves all mask data, bounds, and offset
            _selRegion.CopyFrom(region);

            // Update state based on the restored region
            _selState.Rect = _selRegion.Bounds;
            _selState.Active = !_selRegion.IsEmpty;
            _selState.State = _selState.Active ? SelectionState.Armed : SelectionState.None;
            _selState.Floating = false;
            _selState.Buffer = null;

            if (_selState.Active)
            {
                _selState.OrigW = _selState.Rect.Width;
                _selState.OrigH = _selState.Rect.Height;
                _selState.OrigCenterX = _selState.Rect.X + _selState.Rect.Width / 2;
                _selState.OrigCenterY = _selState.Rect.Y + _selState.Rect.Height / 2;
                _selState.ResetPivot();
            }

            _selState.ResetTransform();
            _selState.NotifyToolState();

            // Notify tool state of selection presence change
            _toolState?.SetSelectionPresence(_selState.Active, false);

            InvalidateMainCanvas();
        }

        /// <summary>
        /// Applies a transform snapshot and triggers redraw.
        /// </summary>
        private void ApplyTransformSnapshotAndRedraw(SelectionTransformItem.TransformSnapshot snapshot)
        {
            if (_selState == null) return;

            _selState.ApplyTransformSnapshot(snapshot);

            // Rebuild the selection region from the buffer if we have one
            if (snapshot.Buffer != null && snapshot.BufferWidth > 0 && snapshot.BufferHeight > 0)
            {
                // Rebuild selection region from buffer's alpha
                int regionW = Math.Max(Document.PixelWidth, snapshot.BufferWidth + Math.Abs(snapshot.FloatX));
                int regionH = Math.Max(Document.PixelHeight, snapshot.BufferHeight + Math.Abs(snapshot.FloatY));
                _selRegion.EnsureSize(regionW, regionH);
                _selRegion.Clear();
                _selRegion.SetOffset(snapshot.FloatX, snapshot.FloatY);

                for (int y = 0; y < snapshot.BufferHeight; y++)
                    for (int x = 0; x < snapshot.BufferWidth; x++)
                        if (snapshot.Buffer[(y * snapshot.BufferWidth + x) * 4 + 3] > 0)
                            _selRegion.AddRect(CreateRect(x, y, 1, 1));
            }
            else
            {
                // No buffer - just update the offset
                _selRegion.SetOffset(snapshot.FloatX, snapshot.FloatY);
            }

            _selState.Rect = _selRegion.Bounds;

            // Sync tool state
            _toolState?.SetSelectionScale(snapshot.ScaleX * 100.0, snapshot.ScaleY * 100.0, _selState.ScaleLink);
            _toolState?.SetRotationAngle(snapshot.AngleDeg);
            _toolState?.SetSelectionPresence(_selState.Active, _selState.Floating);

            // Clear any cached preview
            _selState.PreviewBuf = null;

            Document.RaiseStructureChanged();
            InvalidateMainCanvas();
        }

        // ═══════════════════════════════════════════════════════════════
        // CURSOR MANAGEMENT
        // ═══════════════════════════════════════════════════════════════

        private void SetCursor(InputSystemCursorShape shape)
        {
            if (_curShape == shape) return;
            _curShape = shape;
            this.ProtectedCursor = InputSystemCursor.Create(shape);
        }

        private void UpdateSelectionCursor(Point viewPt)
        {
            if (_selState == null || _selHitTest == null) return;

            var shape = InputSystemCursorShape.Arrow;
            _selState.HoverHandle = SelHandle.None;

            if (IsSelectTool && _selState.Active && _selState.State == SelectionState.Armed)
            {
                // Pivot handle takes highest priority
                if (_selHitTest.HitTestPivotHandle(viewPt))
                {
                    shape = InputSystemCursorShape.Hand;
                }
                // rotation takes precedence if outside ring hit
                else if (_selHitTest.HitTestRotateHandle(viewPt) != RotHandle.None)
                {
                    shape = InputSystemCursorShape.Cross;
                }
                else
                {
                    var handle = _selHitTest.HitTestHandle(viewPt);
                    if (handle != SelHandle.None)
                    {
                        _selState.HoverHandle = handle;
                        shape = CursorForHandle(handle);
                    }
                    else
                    {
                        // Use unclamped doc coordinates so we can detect hover over off-canvas selection parts
                        var (x, y) = ViewToDoc(viewPt);
                        bool insideSelection = _selHitTest.IsInsideTransformedSelection(x, y);
                        if (insideSelection)
                            shape = InputSystemCursorShape.SizeAll;
                    }
                }
            }
            SetCursor(shape);
        }

        private static InputSystemCursorShape CursorForHandle(SelHandle h) => h switch
        {
            SelHandle.N or SelHandle.S => InputSystemCursorShape.SizeNorthSouth,
            SelHandle.E or SelHandle.W => InputSystemCursorShape.SizeWestEast,
            SelHandle.NE or SelHandle.SW => InputSystemCursorShape.SizeNortheastSouthwest,
            SelHandle.NW or SelHandle.SE => InputSystemCursorShape.SizeNorthwestSoutheast,
            _ => InputSystemCursorShape.Arrow
        };

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API - SELECTION OPERATIONS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Selects the entire document.</summary>
        public void Selection_SelectAll() => _selClipboard?.SelectAll();

        /// <summary>Inverts the current selection.</summary>
        public void Selection_InvertSelection() => _selClipboard?.InvertSelection();

        /// <summary>Deletes current selection.</summary>
        public void DeleteSelection() => _selClipboard?.Delete();

        /// <summary>Copies the current selection to clipboard.</summary>
        public void CopySelection() => _selClipboard?.Copy();

        /// <summary>Cuts the current selection.</summary>
        public void CutSelection() => _selClipboard?.Cut();

        /// <summary>Pasts from clipboard.</summary>
        public void PasteClipboard() => _selClipboard?.Paste();

        /// <summary>Cancels the active selection.</summary>
        public void CancelSelection() => _selClipboard?.Cancel();

        /// <summary>Commits the floating selection.</summary>
        public void CommitSelection() => CommitFloatingWithHistory();

        /// <summary>
        /// Nudges the selection by the specified pixel amount.
        /// If the selection is not floating, it will be lifted first.
        /// </summary>
        /// <param name="dx">Horizontal offset in pixels (positive = right).</param>
        /// <param name="dy">Vertical offset in pixels (positive = down).</param>
        public void NudgeSelection(int dx, int dy)
        {
            if (_selState == null || !_selState.Active) return;
            if (dx == 0 && dy == 0) return;

            // Lift selection if not floating
            if (!_selState.Floating)
            {
                LiftSelectionWithHistory();
            }

            // Move the floating selection
            _selState.FloatX += dx;
            _selState.FloatY += dy;
            _selState.OrigCenterX += dx;
            _selState.OrigCenterY += dy;

            // Update the selection region offset
            OffsetSelectionRegion(dx, dy);

            _selState.Dirty = true;
            _selState.Changed = true;
            InvalidateMainCanvas();
        }

        /// <summary>
        /// Applies snap-to-guide adjustments to a proposed selection move.
        /// </summary>
        /// <param name="dx">Proposed horizontal move delta.</param>
        /// <param name="dy">Proposed vertical move delta.</param>
        /// <returns>Adjusted (dx, dy) with snap applied.</returns>
        private (int dx, int dy) ApplyGuideSnap(int dx, int dy)
        {
            if (_guideService == null || !_guideService.SnapToGuides || !_guideService.GuidesVisible)
                return (dx, dy);

            if (_selState == null || !_selState.Floating)
                return (dx, dy);

            int newX = _selState.FloatX + dx;
            int newY = _selState.FloatY + dy;
            int width = ScaledW;
            int height = ScaledH;

            // Check snap for left/right edges
            int? snapLeft = _guideService.GetSnapPosition(newX, isHorizontal: false);
            int? snapRight = _guideService.GetSnapPosition(newX + width, isHorizontal: false);

            if (snapLeft.HasValue)
                dx = snapLeft.Value - _selState.FloatX;
            else if (snapRight.HasValue)
                dx = snapRight.Value - width - _selState.FloatX;

            // Check snap for top/bottom edges
            int? snapTop = _guideService.GetSnapPosition(newY, isHorizontal: true);
            int? snapBottom = _guideService.GetSnapPosition(newY + height, isHorizontal: true);

            if (snapTop.HasValue)
                dy = snapTop.Value - _selState.FloatY;
            else if (snapBottom.HasValue)
                dy = snapBottom.Value - height - _selState.FloatY;

            return (dx, dy);
        }

        /// <summary>Flips the selection horizontally.</summary>
        private void FlipSelectionHorizontal(bool useGlobalAxis)
        {
            if (_selState == null || !_selState.Floating)
            {
                // Lift the selection first if not floating
                if (_selState?.Active == true)
                    LiftSelectionWithHistory();
            }

            _selTransform?.FlipHorizontal(useGlobalAxis);
            InvalidateMainCanvas();
        }

        /// <summary>Flips the selection vertically.</summary>
        private void FlipSelectionVertical(bool useGlobalAxis)
        {
            if (_selState == null || !_selState.Floating)
            {
                // Lift the selection first if not floating
                if (_selState?.Active == true)
                    LiftSelectionWithHistory();
            }

            _selTransform?.FlipVertical(useGlobalAxis);
            InvalidateMainCanvas();
        }

        /// <summary>Sets selection scale in percent.</summary>
        public void Selection_SetScale(double percentX, double percentY, bool link) =>
            _selTransform?.SetScale(percentX, percentY, link);

        // ═══════════════════════════════════════════════════════════════
        // POINTER EVENT HANDLERS
        // ═══════════════════════════════════════════════════════════════

        private bool Selection_DoubleTapped(DoubleTappedRoutedEventArgs e)
        {
            if (!IsSelectTool || _selState == null) return false;

            var pos = e.GetPosition(_mainCanvas);
            if (!TryGetDocInside(pos, out var x, out var y)) return false;
            if (!TryGetTileRectAtDocPos(x, y, out var r)) return false;
            if (Document.ActiveLayer is not RasterLayer rl) return false;

            _selState.Drag = SelDrag.None;
            _selState.HavePreview = false;
            _mainCanvas.ReleasePointerCaptures();

            if (_selState.Floating) CommitFloatingWithHistory();

            _selRegion.EnsureSize(rl.Surface.Width, rl.Surface.Height);
            _selRegion.Clear();
            _selRegion.AddRect(r);

            _selState.Rect = _selRegion.Bounds;
            _selState.Active = true;
            _selState.Floating = false;
            _selState.Buffer = null;
            _selState.State = SelectionState.Armed;
            _selState.OrigW = r.Width;
            _selState.OrigH = r.Height;
            _selState.OrigCenterX = r.X + r.Width / 2;
            _selState.OrigCenterY = r.Y + r.Height / 2;
            _selState.ResetTransform();
            _selState.NotifyToolState();

            InvalidateMainCanvas();
            e.Handled = true;
            return true;
        }

        private bool Selection_PointerPressed(PointerRoutedEventArgs e)
        {
            if (!IsSelectTool || _selState == null || _selHitTest == null || _selTransform == null) return false;

            var pt = e.GetCurrentPoint(_mainCanvas);
            if (!pt.Properties.IsLeftButtonPressed) return false;

            var viewPos = pt.Position;
            bool shiftDown = IsShiftDown(), altDown = IsAltDown();

            if (_selState.Active && _selState.State == SelectionState.Armed && !shiftDown && !altDown)
            {
                // Pivot handle
                if (_selHitTest.HitTestPivotHandle(viewPos))
                {
                    _selState.Drag = SelDrag.Pivot;
                    if (!_selState.Floating) LiftSelectionWithHistory();
                    // Capture snapshot for potential history
                    _selState.DragStartSnapshot = _selState.CaptureTransformSnapshot();
                    _mainCanvas.CapturePointer(e.Pointer);
                    _selState.Dirty = true;
                    return true;
                }

                // Rotation handle
                var rotHandle = _selHitTest.HitTestRotateHandle(viewPos);
                if (rotHandle != RotHandle.None)
                {
                    _selState.Drag = SelDrag.Rotate;
                    var (docX, docY) = ViewToDoc(viewPos);
                    var (pivotDocX, pivotDocY) = _selTransform.GetPivotPositionDoc();
                    _selState.RotFixedPivotX = pivotDocX;
                    _selState.RotFixedPivotY = pivotDocY;
                    _selState.RotStartCenterX = _selState.OrigCenterX;
                    _selState.RotStartCenterY = _selState.OrigCenterY;
                    _selState.RotStartAngleDeg = _selState.AngleDeg;
                    _selState.RotStartPointerAngleDeg = Math.Atan2(docY - pivotDocY, docX - pivotDocX) * 180.0 / Math.PI;
                    if (!_selState.Floating) LiftSelectionWithHistory();
                    // Capture snapshot for history - include buffer since rotate bakes transforms
                    _selState.DragStartSnapshot = _selState.CaptureTransformSnapshot(includeBuffer: true);
                    _mainCanvas.CapturePointer(e.Pointer);
                    _selState.Dirty = true;
                    return true;
                }

                // Scale handle
                var scaleHandle = _selHitTest.HitTestHandle(viewPos);
                if (scaleHandle != SelHandle.None)
                {
                    _selState.Drag = SelDrag.Scale;
                    _selState.ActiveHandle = scaleHandle;
                    if (!_selState.Floating) LiftSelectionWithHistory();
                    _selState.ScaleStartFX = _selState.FloatX;
                    _selState.ScaleStartFY = _selState.FloatY;
                    _selState.ScaleStartW = ScaledW;
                    _selState.ScaleStartH = ScaledH;
                    _selState.ScaleStartScaleX = _selState.ScaleX;
                    _selState.ScaleStartScaleY = _selState.ScaleY;
                    // Capture snapshot for history - include buffer since scale bakes transforms
                    _selState.DragStartSnapshot = _selState.CaptureTransformSnapshot(includeBuffer: true);
                    _mainCanvas.CapturePointer(e.Pointer);
                    _selState.Dirty = true;
                    return true;
                }

                // Move (inside selection)
                var (mx, my) = ViewToDoc(viewPos);
                if (_selHitTest.IsInsideTransformedSelection(mx, my))
                {
                    _selState.Drag = SelDrag.Move;
                    if (!_selState.Floating) LiftSelectionWithHistory();
                    _selState.MoveStartX = mx;
                    _selState.MoveStartY = my;
                    // Capture snapshot for history
                    _selState.DragStartSnapshot = _selState.CaptureTransformSnapshot();
                    _mainCanvas.CapturePointer(e.Pointer);
                    _selState.Dirty = true;
                    return true;
                }

                // Clicked outside - deselect
                if (_selState.Floating) CommitFloatingWithHistory();
                _selState.Clear();
                _toolState?.SetSelectionPresence(false, false);
                ActiveSelectionTool?.Cancel();
                InvalidateMainCanvas();
                return true;
            }

            // Start marquee
            if (_selState.Drag == SelDrag.None)
            {
                _selState.Drag = SelDrag.Marquee;
                _selState.DragStartView = viewPos;

                // Capture selection state before marquee for history
                _selectionBeforeMarquee = _selRegion.Clone();

                // Capture the combine mode based on modifier keys
                // This must be done BEFORE calling the tool's PointerPressed which will set its own mode
                if (shiftDown)
                    _marqueeCombineKind = SelectionChangeItem.SelectionChangeKind.Add;
                else if (altDown)
                    _marqueeCombineKind = SelectionChangeItem.SelectionChangeKind.Subtract;
                else
                    _marqueeCombineKind = SelectionChangeItem.SelectionChangeKind.Create;

                // Allow starting marquee from anywhere - use unclamped doc coordinates
                var (docPosX, docPosY) = ViewToDoc(viewPos);
                var docPos = new Point(docPosX, docPosY);
                ActiveSelectionTool?.PointerPressed(docPos, e);

                _mainCanvas.CapturePointer(e.Pointer);
                return true;
            }

            return false;
        }

        private bool Selection_PointerMoved(PointerRoutedEventArgs e)
        {
            if (!IsSelectTool || _selState == null || _selHitTest == null || _selTransform == null) return false;

            var pt = e.GetCurrentPoint(_mainCanvas);
            var viewPos = pt.Position;
            UpdateSelectionCursor(viewPos);

            if (_selState.Drag == SelDrag.None) return false;

            var (docX, docY) = ViewToDoc(viewPos);

            switch (_selState.Drag)
            {
                case SelDrag.Marquee:
                    var docPos = new Point(docX, docY);
                    var tool = ActiveSelectionTool;
                    if (tool is PaintSelectionTool) UpdateHover(pt.Position);
                    return tool?.PointerMoved(docPos, e) ?? false;

                case SelDrag.Move:
                    if (_selState.Floating)
                    {
                        int dx = docX - _selState.MoveStartX;
                        int dy = docY - _selState.MoveStartY;

                        // Apply guide snapping
                        (dx, dy) = ApplyGuideSnap(dx, dy);

                        _selState.FloatX += dx;
                        _selState.FloatY += dy;
                        _selState.MoveStartX += dx;
                        _selState.MoveStartY += dy;
                        _selState.OrigCenterX += dx;
                        _selState.OrigCenterY += dy;
                        OffsetSelectionRegion(dx, dy);
                        _selState.Dirty = true;
                        _selState.Changed = true;
                        InvalidateMainCanvas();
                    }
                    return true;

                case SelDrag.Scale:
                    if (_selState.Floating)
                    {
                        _selTransform.UpdateScaleFromHandle(docX, docY);
                        _selState.Dirty = true;
                        _selState.Changed = true;
                        InvalidateMainCanvas();
                    }
                    return true;

                case SelDrag.Rotate:
                    if (_selState.Floating)
                    {
                        UpdateRotation(docX, docY);
                        _selState.Dirty = true;
                        _selState.Changed = true;
                        InvalidateMainCanvas();
                    }
                    return true;

                case SelDrag.Pivot:
                    if (_selState.Floating)
                    {
                        _selTransform.UpdatePivotFromDrag(docX, docY);
                        _selState.Dirty = true;
                    }
                    return true;
            }
            return false;
        }

        private bool Selection_PointerReleased(PointerRoutedEventArgs e)
        {
            if (!IsSelectTool || _selState == null) return false;

            if (_selState.Drag == SelDrag.Marquee)
            {
                var viewPos = e.GetCurrentPoint(_mainCanvas).Position;
                // Use unclamped doc coordinates - the tool handles clamping
                var (docX, docY) = ViewToDoc(viewPos);
                var docPos = new Point(docX, docY);
                var tool = ActiveSelectionTool;
                tool?.PointerReleased(docPos, e);

                _selState.Rect = _selRegion.Bounds;
                _selState.Active = !_selRegion.IsEmpty;
                _selState.State = _selState.Active ? SelectionState.Armed : SelectionState.None;

                if (_selState.Active)
                {
                    _selState.AngleDeg = 0.0;
                    _selState.CumulativeAngleDeg = 0.0;
                    _selState.OrigW = _selState.Rect.Width;
                    _selState.OrigH = _selState.Rect.Height;
                    _selState.OrigCenterX = _selState.Rect.X + _selState.Rect.Width / 2;
                    _selState.OrigCenterY = _selState.Rect.Y + _selState.Rect.Height / 2;
                    _selState.ResetPivot();
                }

                if (_toolState?.ActiveToolId != ToolIds.Lasso || !(tool?.HasPreview ?? false))
                    _selState.HavePreview = false;

                _toolState?.SetSelectionPresence(_selState.Active, false);

                // Push selection change to history
                PushMarqueeSelectionHistory();
            }

            // Push transform history for scale/rotate operations
            if ((_selState.Drag == SelDrag.Scale || _selState.Drag == SelDrag.Rotate) && _selState.Floating && _selState.Buffer != null)
            {
                // The DragStartSnapshot was captured at pointer press (with includeBuffer=true for scale/rotate)
                // Now bake the transforms
                BakeTransformsOnRelease();
                // Then capture the after snapshot (with baked buffer) and push history
                PushTransformHistoryWithBakedState(_selState.Drag);
            }

            // For move operations, push transform history to track position changes
            if (_selState.Drag == SelDrag.Move && _selState.Floating)
            {
                PushTransformHistory(_selState.Drag);
                _selState.Changed = true;
            }

            // Push transform history for pivot changes (no pixel changes, just UI state)
            if (_selState.Drag == SelDrag.Pivot && _selState.Floating)
            {
                PushTransformHistory(_selState.Drag);
            }

            _selState.Drag = SelDrag.None;
            _selState.ActiveHandle = SelHandle.None;
            _selState.DragStartSnapshot = null;
            _mainCanvas.ReleasePointerCaptures();
            _selState.Dirty = false;
            InvalidateMainCanvas();
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAWING
        // ═══════════════════════════════════════════════════════════════

        private void Selection_Draw(ICanvasRenderer renderer)
        {
            _selRenderer?.Draw(renderer);
        }

        // ═══════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void UpdateRotation(int docX, int docY)
        {
            if (_selState == null) return;

            double pivotDocX = _selState.RotFixedPivotX;
            double pivotDocY = _selState.RotFixedPivotY;

            double dx = docX - pivotDocX;
            double dy = docY - pivotDocY;
            double currentAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double delta = currentAngle - _selState.RotStartPointerAngleDeg;

            bool snap = IsShiftDown();
            double step = snap ? 15.0 : 1.0;
            double newAngle = Math.Round((_selState.RotStartAngleDeg + delta) / step) * step;
            _selState.AngleDeg = newAngle;

            // Orbit selection center around pivot if pivot is custom
            if (_selState.PivotCustom && (Math.Abs(_selState.PivotOffsetX) > 0.001 || Math.Abs(_selState.PivotOffsetY) > 0.001))
            {
                double totalAngleRad = (_selState.CumulativeAngleDeg + _selState.AngleDeg) * Math.PI / 180.0;
                double localCenterOffsetX = -_selState.PivotOffsetX;
                double localCenterOffsetY = -_selState.PivotOffsetY;
                double cos = Math.Cos(totalAngleRad);
                double sin = Math.Sin(totalAngleRad);
                double rotatedOffsetX = localCenterOffsetX * cos - localCenterOffsetY * sin;
                double rotatedOffsetY = localCenterOffsetX * sin + localCenterOffsetY * cos;
                _selState.OrigCenterX = (int)Math.Round(pivotDocX + rotatedOffsetX);
                _selState.OrigCenterY = (int)Math.Round(pivotDocY + rotatedOffsetY);

                int handleW = (int)Math.Round((_selState.OrigW > 0 ? _selState.OrigW : _selState.BufferWidth) * _selState.ScaleX);
                int handleH = (int)Math.Round((_selState.OrigH > 0 ? _selState.OrigH : _selState.BufferHeight) * _selState.ScaleY);
                _selState.FloatX = _selState.OrigCenterX - handleW / 2;
                _selState.FloatY = _selState.OrigCenterY - handleH / 2;
            }

            if (!_selPushToTool)
            {
                _selPushToTool = true;
                _toolState?.SetRotationAngle(_selState.AngleDeg);
                _selPushToTool = false;
            }
        }

        private void BakeTransformsOnRelease()
        {
            if (_selState == null || _selState.Buffer == null) return;

            bool hasScale = Math.Abs(_selState.ScaleX - 1.0) > 0.001 || Math.Abs(_selState.ScaleY - 1.0) > 0.001;
            bool hasRotation = Math.Abs(_selState.AngleDeg) > 0.1;
            int centerX = _selState.OrigCenterX != 0 ? _selState.OrigCenterX : (_selState.FloatX + ScaledW / 2);
            int centerY = _selState.OrigCenterY != 0 ? _selState.OrigCenterY : (_selState.FloatY + ScaledH / 2);

            if (hasScale)
            {
                var (buf, tw, th) = BuildScaledBufferForCommit(_selState.Buffer, _selState.BufferWidth, _selState.BufferHeight,
                    _selState.ScaleX, _selState.ScaleY, _selState.ScaleFilter);
                _selState.OrigW = tw;
                _selState.OrigH = th;
                _selState.Buffer = buf;
                _selState.BufferWidth = tw;
                _selState.BufferHeight = th;
                _selState.FloatX = centerX - tw / 2;
                _selState.FloatY = centerY - th / 2;
                _selState.ScaleX = 1.0;
                _selState.ScaleY = 1.0;
            }

            if (hasRotation)
            {
                _selState.CumulativeAngleDeg += _selState.AngleDeg;
                _selState.AngleDeg = 0.0;
            }

            _selState.PreviewBuf = null;

            // Rebuild selection region from the buffer
            // IMPORTANT: Use the buffer dimensions, not document dimensions, because the
            // selection may extend beyond the document bounds when scaled/dragged outside
            int regionW = Math.Max(Document.PixelWidth, _selState.BufferWidth);
            int regionH = Math.Max(Document.PixelHeight, _selState.BufferHeight);
            _selRegion.EnsureSize(regionW, regionH);
            _selRegion.Clear();
            _selRegion.SetOffset(_selState.FloatX, _selState.FloatY);

            for (int y = 0; y < _selState.BufferHeight; y++)
                for (int x = 0; x < _selState.BufferWidth; x++)
                    if (_selState.Buffer[(y * _selState.BufferWidth + x) * 4 + 3] > 0)
                        _selRegion.AddRect(CreateRect(x, y, 1, 1));

            _selState.Rect = _selRegion.Bounds;
            _toolState?.SetSelectionScale(100.0, 100.0, _selState.ScaleLink);
            _toolState?.SetRotationAngle(0.0);
            _selState.Changed = true;
        }

        private void OffsetSelectionRegion(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return;
            _selRegion.SetOffset(_selRegion.OffsetX + dx, _selRegion.OffsetY + dy);
            if (_selState != null) _selState.Rect = _selRegion.Bounds;
        }

        private void ResetPivot() => _selState?.ResetPivot();

        private (double X, double Y) GetPivotPositionDoc() => _selTransform?.GetPivotPositionDoc() ?? (0, 0);

        private (float X, float Y) GetPivotPositionView(Rect dest, double scale) =>
            _selHitTest?.GetPivotPositionView(dest, scale) ?? (0, 0);

        private bool IsInsideTransformedSelection(int docX, int docY) =>
            _selHitTest?.IsInsideTransformedSelection(docX, docY) ?? false;

        private bool IsInsideSelectionBodyInView(Point viewPt) =>
            _selHitTest?.IsInsideSelectionBodyInView(viewPt) ?? false;

        private bool IsInActiveSelection(int x, int y) =>
            _selHitTest?.IsInActiveSelection(x, y) ?? true;

        // ═══════════════════════════════════════════════════════════════
        // UTILITY METHODS
        // ═══════════════════════════════════════════════════════════════

        private static bool IsCtrlDown() =>
            (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;

        private static bool IsShiftDown() =>
            (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0;

        private static bool IsAltDown() =>
            (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) & CoreVirtualKeyStates.Down) != 0;

        private (int x, int y) ViewToDoc(Point v)
        {
            var dest = _zoom.GetDestRect();
            double s = _zoom.Scale;
            return ((int)Math.Round((v.X - dest.X) / s), (int)Math.Round((v.Y - dest.Y) / s));
        }

        private (int x, int y) ViewToDocClamped(Point v)
        {
            var dest = _zoom.GetDestRect();
            double s = _zoom.Scale;
            return (Math.Clamp((int)Math.Round((v.X - dest.X) / s), 0, Document.PixelWidth),
                    Math.Clamp((int)Math.Round((v.Y - dest.Y) / s), 0, Document.PixelHeight));
        }

        private bool TryGetTileRectAtDocPos(int x, int y, out RectInt32 tileRect)
        {
            tileRect = default;
            int tileW = Document.TileSize.Width;
            int tileH = Document.TileSize.Height;
            if (tileW <= 0 || tileH <= 0) return false;

            int tx = x / tileW;
            int ty = y / tileH;
            int rx = tx * tileW;
            int ry = ty * tileH;
            int rw = Math.Min(tileW, Document.PixelWidth - rx);
            int rh = Math.Min(tileH, Document.PixelHeight - ry);
            if (rw <= 0 || rh <= 0) return false;

            tileRect = CreateRect(rx, ry, rw, rh);
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // BUFFER TRANSFORM METHODS
        // ═══════════════════════════════════════════════════════════════

        private static (byte[] buf, int w, int h) BuildScaledBufferForCommit(byte[] src, int sw, int sh, double sx, double sy, ScaleMode filter)
        {
            int outW = Math.Max(1, (int)Math.Round(sw * sx));
            int outH = Math.Max(1, (int)Math.Round(sh * sy));
            if (outW == sw && outH == sh) return (src, sw, sh);
            return filter switch
            {
                ScaleMode.NearestNeighbor => (PixelOps.ResizeNearest(src, sw, sh, outW, outH), outW, outH),
                ScaleMode.Bilinear => (PixelOps.ResizeBilinear(src, sw, sh, outW, outH), outW, outH),
                ScaleMode.EPX => PixelOps.ScaleBy2xStepsThenNearest(src, sw, sh, outW, outH, epx: true),
                ScaleMode.Scale2x => PixelOps.ScaleBy2xStepsThenNearest(src, sw, sh, outW, outH, epx: false),
                _ => (PixelOps.ResizeNearest(src, sw, sh, outW, outH), outW, outH)
            };
        }

        private static (byte[] buf, int w, int h) BuildRotatedBufferForCommit(byte[] src, int sw, int sh, double angleDeg, RotationMode kind)
        {
            double a = angleDeg % 360.0;
            if (Math.Abs(a) < 1e-6) return (src, sw, sh);
            return kind switch
            {
                RotationMode.RotSprite => PixelOps.RotateSpriteApprox(src, sw, sh, a),
                _ => PixelOps.RotateNearest(src, sw, sh, a)
            };
        }

        private static void BlitAlphaOver(byte[] dst, int w, int h, int dx, int dy, byte[] src, int sw, int sh)
        {
            int x0 = Math.Max(0, dx), y0 = Math.Max(0, dy), x1 = Math.Min(w, dx + sw), y1 = Math.Min(h, dy + sh);
            if (x1 <= x0 || y1 <= y0) return;
            int dstStride = w * 4, srcStride = sw * 4;
            for (int y = y0; y < y1; y++)
            {
                int sy = y - dy, dstRow = y * dstStride, srcRow = sy * srcStride;
                for (int x = x0; x < x1; x++)
                {
                    int sx = x - dx, di = dstRow + x * 4, si = srcRow + sx * 4;
                    byte sb = src[si], sg = src[si + 1], sr = src[si + 2], sa = src[si + 3];
                    if (sa == 0) continue;

                    // For fully opaque source, just copy
                    if (sa == 255)
                    {
                        dst[di] = sb;
                        dst[di + 1] = sg;
                        dst[di + 2] = sr;
                        dst[di + 3] = 255;
                        continue;
                    }

                    byte db = dst[di], dg = dst[di + 1], dr = dst[di + 2], da = dst[di + 3];

                    // Porter-Duff "source over" with straight alpha source
                    // out_rgb = (src_rgb * src_a + dst_rgb * dst_a * (255 - src_a) / 255) / out_a
                    // out_a = src_a + dst_a * (255 - src_a) / 255
                    int invA = 255 - sa;
                    int outA = sa + da * invA / 255;

                    if (outA == 0)
                    {
                        dst[di] = 0;
                        dst[di + 1] = 0;
                        dst[di + 2] = 0;
                        dst[di + 3] = 0;
                    }
                    else
                    {
                        // Blend with straight alpha: result = (src * srcA + dst * dstA * (1 - srcA)) / outA
                        dst[di] = (byte)((sb * sa + db * da * invA / 255) / outA);
                        dst[di + 1] = (byte)((sg * sa + dg * da * invA / 255) / outA);
                        dst[di + 2] = (byte)((sr * sa + dr * da * invA / 255) / outA);
                        dst[di + 3] = (byte)outA;
                    }
                }
            }
        }

        private static byte[] CopyRectBytes(byte[] src, int w, int h, RectInt32 r)
        {
            int x0 = Math.Max(0, r.X), y0 = Math.Max(0, r.Y), x1 = Math.Min(w, r.X + r.Width), y1 = Math.Min(h, r.Y + r.Height);
            int rw = Math.Max(0, x1 - x0), rh = Math.Max(0, y1 - y0);
            var dst = new byte[rw * rh * 4];
            if (rw == 0 || rh == 0) return dst;
            int srcStride = w * 4, dstStride = rw * 4;
            for (int y = 0; y < rh; y++)
                System.Buffer.BlockCopy(src, (y0 + y) * srcStride + x0 * 4, dst, y * dstStride, dstStride);
            return dst;
        }

        private static void BlitBytes(byte[] dst, int w, int h, int dx, int dy, byte[] buf, int bw, int bh)
        {
            int x0 = Math.Max(0, dx), y0 = Math.Max(0, dy), x1 = Math.Min(w, dx + bw), y1 = Math.Min(h, dy + bh);
            if (x1 <= x0 || y1 <= y0) return;
            int dstStride = w * 4, srcStride = bw * 4;
            for (int y = y0; y < y1; y++)
            {
                int sy = y - dy;
                System.Buffer.BlockCopy(buf, sy * srcStride + (x0 - dx) * 4, dst, y * dstStride + x0 * 4, (x1 - x0) * 4);
            }
        }

        private static void ClearRectBytes(byte[] dst, int w, int h, RectInt32 r)
        {
            int x0 = Math.Clamp(r.X, 0, w), y0 = Math.Clamp(r.Y, 0, h);
            int x1 = Math.Clamp(r.X + r.Width, 0, w), y1 = Math.Clamp(r.Y + r.Height, 0, h);
            int dstStride = w * 4, bytes = (x1 - x0) * 4;
            for (int y = y0; y < y1; y++)
                Array.Clear(dst, y * dstStride + x0 * 4, bytes);
        }

        private static RectInt32 Normalize(RectInt32 r) => SelectionSubsystem.Normalize(r);
        private static RectInt32 ClampToSurface(RectInt32 r, int w, int h) => SelectionSubsystem.ClampToSurface(r, w, h);
        private static RectInt32 Intersect(RectInt32 a, RectInt32 b) => SelectionSubsystem.Intersect(a, b);
        private static RectInt32 UnionRect(RectInt32 a, RectInt32 b) => SelectionSubsystem.UnionRect(a, b);
    }
}
