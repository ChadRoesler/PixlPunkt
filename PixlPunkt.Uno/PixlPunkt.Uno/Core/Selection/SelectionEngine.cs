using System;
using System.Collections.Generic;
using System.Numerics;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.History;
using PixlPunkt.Uno.Core.Logging;
using Windows.Foundation;
using Windows.Graphics;
using static PixlPunkt.Uno.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Uno.Core.Selection
{
    /// <summary>
    /// Manages interactive pixel-based selections with polygon boundaries, transform operations, and floating buffers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionEngine is the core controller for all selection-related operations in PixlPunkt. It coordinates:
    /// - Selection creation (rectangular marquee, magic wand integration points)
    /// - Floating selection buffers (lift pixel data from layer)
    /// - Transform operations (move, rotate, scale) with visual feedback
    /// - Hit testing for interactive handles (corners, edges, rotation handles)
    /// - Integration with undo/redo via <see cref="PushHistory"/>
    /// - Rendering callbacks for UI updates
    /// </para>
    /// <para>
    /// **Architecture**: This class is UI-framework-agnostic. It receives pointer events and coordinate
    /// transformations from the canvas host but contains no WinUI or Win2D dependencies. All rendering
    /// is delegated via callbacks.
    /// </para>
    /// <para>
    /// **Usage Flow**:
    /// 1. Create engine with layer/document providers
    /// 2. Wire up coordinate transformation functions (ViewToDoc, ViewToDocClamped)
    /// 3. Forward pointer events: PointerPressed → PointerMoved → PointerReleased
    /// 4. Call Draw() during render pass to visualize selection
    /// 5. Selection operations (LiftToFloating, CommitFloating, ClearSelection, SelectAll)
    /// </para>
    /// <para>
    /// **Transform Model**: Supports "sticky transforms" where rotation/scale state persists across
    /// multiple operations until committed, allowing incremental adjustments.
    /// </para>
    /// </remarks>
    public class SelectionEngine
    {
        // ─────────────────────────────────────────────────────────────
        // PUBLIC SURFACE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the current pixel selection state (mask, buffer, flags).
        /// </summary>
        /// <value>
        /// The <see cref="PixelSelection"/> containing mask data, floating buffer, and active/floating/armed flags.
        /// </value>
        public PixelSelection Sel { get; private set; }

        /// <summary>
        /// Gets the transform controller for move/rotate/scale operations.
        /// </summary>
        /// <value>
        /// A <see cref="SelectionTransform"/> that manages transformation matrices and handle positions.
        /// </value>
        public SelectionTransform Transform { get; private set; }

        /// <summary>
        /// Gets a value indicating whether a selection is currently active (has defined region).
        /// </summary>
        public bool HasActiveSelection => Sel.Active;

        /// <summary>
        /// Gets a value indicating whether the selection is floating (lifted from layer with transform buffer).
        /// </summary>
        public bool IsFloating => Sel.Floating;

        /// <summary>
        /// Gets or sets the callback for pushing undo history entries.
        /// </summary>
        /// <value>
        /// An action that receives a <see cref="PixelChangeItem"/> to push onto the undo stack.
        /// Set by the canvas host to integrate with document history.
        /// </value>
        public Action<PixelChangeItem>? PushHistory { get; set; }

        // Document access:
        private readonly Func<RasterLayer?> _getActiveLayer;
        private readonly Func<(int w, int h)> _getDocumentSize;

        /// <summary>
        /// Gets or sets the function to convert view coordinates to document coordinates (unbounded).
        /// </summary>
        /// <value>
        /// A function mapping <see cref="Point"/> (view space) to <see cref="Vector2"/> (document space).
        /// Set by the canvas host based on current zoom/pan.
        /// </value>
        public Func<Point, Vector2>? ViewToDoc;

        /// <summary>
        /// Gets or sets the function to convert view coordinates to document coordinates (clamped to canvas bounds).
        /// </summary>
        /// <value>
        /// A function mapping <see cref="Point"/> (view space) to <see cref="Vector2"/> (document space, clamped).
        /// Used for operations that must stay within canvas boundaries.
        /// </value>
        public Func<Point, Vector2>? ViewToDocClamped;

        /// <summary>
        /// Gets or sets the callback to trigger a canvas redraw.
        /// </summary>
        /// <value>
        /// An action invoked whenever visual state changes (selection modified, transform updated).
        /// The canvas host should respond by calling <see cref="Draw"/> during its next render pass.
        /// </value>
        public Action? RequestRedraw;

        // ─────────────────────────────────────────────────────────────
        // INTERNAL STATE
        // ─────────────────────────────────────────────────────────────

        private enum DragMode
        {
            None,
            Move,
            Rotate,
            Scale,
            Marquee,
            NewSelection
        }

        private DragMode _drag = DragMode.None;
        private SelectionTransform.ScaleHandle _activeScaleHandle =
            SelectionTransform.ScaleHandle.None;

        private Vector2 _pressDoc;     // doc coords where press began
        private Vector2 _lastDoc;      // doc coords during drag
        private Vector2 _marqueeStart; // for marquee selection

        private bool _shift;
        private bool _alt;

        /// <summary>
        /// Gets or sets the hit-test radius for transform handles in pixels.
        /// </summary>
        /// <value>
        /// Size of clickable area around handles. Default is 12 pixels.
        /// </value>
        public float HandleSizePx = 12f;

        // ─────────────────────────────────────────────────────────────
        // CONSTRUCTOR
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionEngine"/> class.
        /// </summary>
        /// <param name="activeLayerProvider">
        /// Function that returns the currently active <see cref="RasterLayer"/> or null if none.
        /// </param>
        /// <param name="docSizeProvider">
        /// Function that returns the document dimensions (width, height).
        /// </param>
        /// <param name="liftCallback">
        /// Callback invoked when selection buffer needs to be lifted from the layer.
        /// </param>
        /// <param name="commitCallback">
        /// Callback invoked when floating selection needs to be committed back to the layer.
        /// </param>
        /// <remarks>
        /// The engine is initialized with a <see cref="PixelSelection"/> and <see cref="SelectionTransform"/>
        /// sized to match the document. Callbacks are stored for deferred operations that require
        /// host coordination (lifting pixels, committing changes).
        /// </remarks>
        public SelectionEngine(
            Func<RasterLayer?> activeLayerProvider,
            Func<(int w, int h)> docSizeProvider, Action liftCallback, Action commitCallback)
        {
            _getActiveLayer = activeLayerProvider;
            _getDocumentSize = docSizeProvider;

            var (w, h) = docSizeProvider();
            Sel = new PixelSelection(w, h);
            Transform = new SelectionTransform(Sel);
            _liftCallback = liftCallback;
            _commitCallback = commitCallback;
        }

        // ─────────────────────────────────────────────────────────────
        // EXTERNAL COMMANDS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears the current selection (deselects everything).
        /// </summary>
        /// <remarks>
        /// Resets the selection to inactive state and triggers a redraw.
        /// If a floating selection exists, it is discarded.
        /// </remarks>
        public void ClearSelection()
        {
            LoggingService.Debug("Selection cleared wasFloating={WasFloating}", Sel.Floating);
            Sel.Clear();
            RequestRedraw?.Invoke();
        }

        /// <summary>
        /// Selects the entire canvas.
        /// </summary>
        /// <remarks>
        /// Creates a rectangular selection covering the full document bounds and triggers a redraw.
        /// </remarks>
        public void SelectAll()
        {
            var (w, h) = _getDocumentSize();
            Sel.SetRect(0, 0, w, h);
            LoggingService.Debug("Select all applied docSize={Width}x{Height}", w, h);
            RequestRedraw?.Invoke();
        }

        /// <summary>
        /// Lifts pixel data into a floating selection buffer with specified position.
        /// </summary>
        /// <param name="buf">BGRA pixel data (byte array).</param>
        /// <param name="w">Width of the buffer in pixels.</param>
        /// <param name="h">Height of the buffer in pixels.</param>
        /// <param name="fx">X coordinate of buffer origin in document space.</param>
        /// <param name="fy">Y coordinate of buffer origin in document space.</param>
        /// <remarks>
        /// <para>
        /// Creates a floating selection that can be transformed (moved, rotated, scaled) independently
        /// of the layer. The selection becomes active and floating, with transform state initialized
        /// to identity (scale=1, rotation=0).
        /// </para>
        /// <para>
        /// This is typically called after the user begins dragging a selection or performs a "float"
        /// command. The buffer should contain the pixels from the selection region, and the layer's
        /// corresponding pixels should be cleared or preserved depending on the operation mode.
        /// </para>
        /// </remarks>
        public void LiftToFloating(byte[] buf, int w, int h, int fx, int fy)
        {
            Sel.Active = true;
            Sel.Floating = true;

            Sel.Buffer = buf;
            Sel.BufW = w;
            Sel.BufH = h;

            Sel.Translation = new Vector2(fx, fy);
            Sel.ScaleX = Sel.ScaleY = 1;
            Sel.RotationDeg = 0;

            LoggingService.Debug("Selection lifted to floating bufferSize={Width}x{Height} position=({X},{Y})",
                w, h, fx, fy);
            RequestRedraw?.Invoke();
        }

        /// <summary>
        /// Commits the floating selection back to the layer, applying all accumulated transforms.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Applies rotation, scaling, and translation to the floating buffer, stamps it back into
        /// the active layer, rebuilds the selection mask from the transformed result, and pushes
        /// a history entry for undo/redo.
        /// </para>
        /// <para>
        /// After commit, the selection remains active with a polygon outline matching the new
        /// pixel footprint. If sticky transforms are enabled, transform state persists for
        /// subsequent operations.
        /// </para>
        /// <para>
        /// No-op if no floating selection exists or no active layer is available.
        /// </para>
        /// </remarks>
        public void CommitFloating()
        {
            if (!Sel.Floating || Sel.Buffer == null)
                return;

            var layer = _getActiveLayer();
            if (layer == null)
            {
                LoggingService.Warning("Cannot commit floating selection: no active layer");
                return;
            }

            LoggingService.Debug("Committing floating selection scale=({ScaleX},{ScaleY}) rotation={Rotation}",
                Sel.ScaleX, Sel.ScaleY, Sel.RotationDeg);
            CommitFloatingInternal(layer);
            RequestRedraw?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────
        // POINTER ENTRYPOINTS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Handles pointer press events (typically left mouse button or touch down).
        /// </summary>
        /// <param name="viewPos">The pointer position in view coordinates.</param>
        /// <param name="left">Whether this is a left button press (right button ignored).</param>
        /// <param name="shift">Whether Shift key is held (for add-to-selection).</param>
        /// <param name="alt">Whether Alt key is held (for subtract-from-selection).</param>
        /// <returns><c>true</c> if the event was handled; <c>false</c> to pass through to other tools.</returns>
        /// <remarks>
        /// <para>
        /// Determines interaction mode based on context:
        /// - Shift/Alt: Begin marquee for boolean combine (add/subtract)
        /// - Hit rotation handle: Begin rotation drag
        /// - Hit scale handle: Begin scale drag
        /// - Hit inside selection: Begin move drag (lifts selection if not already floating)
        /// - Hit outside selection: Clear if unarmed, or de-arm if armed
        /// - No selection: Begin new marquee selection
        /// </para>
        /// </remarks>
        public bool PointerPressed(Point viewPos, bool left, bool shift, bool alt)
        {
            if (!left) return false;

            if (ViewToDoc == null) return false;

            _shift = shift;
            _alt = alt;

            _pressDoc = ViewToDoc(viewPos);
            _lastDoc = _pressDoc;

            return HandlePress(viewPos);
        }

        /// <summary>
        /// Handles pointer move events during drag operations.
        /// </summary>
        /// <param name="viewPos">The pointer position in view coordinates.</param>
        /// <param name="leftIsDown">Whether the left button is currently held.</param>
        /// <returns><c>true</c> if the event was handled; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Updates the active drag operation (move, rotate, scale, or marquee) and triggers redraw.
        /// </remarks>
        public bool PointerMoved(Point viewPos, bool leftIsDown)
        {
            if (ViewToDoc == null) return false;

            _lastDoc = ViewToDoc(viewPos);

            return HandleMove(viewPos, leftIsDown);
        }

        /// <summary>
        /// Handles pointer release events (button up or touch release).
        /// </summary>
        /// <param name="viewPos">The pointer position in view coordinates.</param>
        /// <returns><c>true</c> if the event was handled; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// <para>
        /// Finalizes the drag operation:
        /// - Transform operations (move/rotate/scale): Commit history step, preserve sticky transform state
        /// - Marquee operations: Finalize selection mask from marquee rectangle
        /// - Selection becomes "armed" (ready for next interaction)
        /// </para>
        /// </remarks>
        public bool PointerReleased(Point viewPos)
        {
            var doc = _lastDoc;

            switch (_drag)
            {
                case DragMode.Move:
                case DragMode.Rotate:
                case DragMode.Scale:
                    // sticky transforms: KEEP transform state.
                    // Continuous history: commit NOW.
                    CommitStep();
                    break;

                case DragMode.Marquee:
                case DragMode.NewSelection:
                    FinalizeMarquee(doc);
                    break;
            }

            _drag = DragMode.None;
            _activeScaleHandle = SelectionTransform.ScaleHandle.None;

            // When user releases, selection becomes armed
            if (Sel.Active)
                Sel.Armed = true;

            RequestRedraw?.Invoke();
            return true;
        }

        // ─────────────────────────────────────────────────────────────
        // RENDER ENTRYPOINT
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Renders the current selection state using provided drawing callbacks.
        /// </summary>
        /// <param name="drawPoly">
        /// Callback to draw the selection polygon outline: (points, armed, floating, showHandles).
        /// </param>
        /// <param name="drawFloatingBuffer">
        /// Callback to draw the floating buffer with transform: (buffer, width, height, transformMatrix).
        /// </param>
        /// <remarks>
        /// <para>
        /// This method should be called during the canvas render pass. It delegates actual drawing
        /// to the provided callbacks, keeping the engine UI-framework-agnostic.
        /// </para>
        /// <para>
        /// The polygon outline is drawn with marching ants, and transform handles are shown when
        /// the selection is armed. The floating buffer (if present) is drawn with applied transforms.
        /// </para>
        /// </remarks>
        public void Draw(Action<List<Vector2>, bool, bool, bool> drawPoly,
                         Action<byte[], int, int, Matrix3x2> drawFloatingBuffer)
        {
            // drawPoly:    (polyPoints, armed, floating, showHandles)
            // drawFloatingBuffer: (buffer, w, h, transformMatrix)
            //
            // Implementation fills this in with polygon/buffer rendering logic
        }

        // ─────────────────────────────────────────────────────────────
        // INTERNAL: COMMIT
        // ─────────────────────────────────────────────────────────────

        private void CommitFloatingInternal(RasterLayer layer)
        {
            // Implementation:
            // 1. Transform buffer
            // 2. Stamp into layer
            // 3. Rebuild mask from buffer
            // 4. Rebuild polygon outline
            // 5. Push history deltas
            // 6. Maintain sticky transforms
        }

        // ─────────────────────────────────────────────────────────────
        // INTERNAL: PRESS / MOVE / RELEASE LOGIC
        // ─────────────────────────────────────────────────────────────

        private bool HandlePress(Point viewPos)
        {
            // [Implementation details - pointer press handling logic]
            if (ViewToDoc == null) return false;

            var doc = _pressDoc;

            bool hasSel = Sel.Active;
            bool armed = Sel.Armed;

            // SHIFT/ALT: boolean combine add/subtract
            if (_shift || _alt)
            {
                // Begin a new marquee to add/subtract
                _drag = DragMode.Marquee;
                _marqueeStart = doc;

                Sel.Active = true;
                Sel.Floating = false;
                Sel.Armed = false;

                RequestRedraw?.Invoke();
                return true;
            }

            // --- Hit-test existing selection ---
            if (hasSel)
            {
                // Grab transformed polygon
                var poly = Transform.GetTransformedPoly();

                // 1) Hit-test rotation handles first
                var rotHit = HitTestRotationHandle(viewPos, poly);
                if (rotHit)
                {
                    _drag = DragMode.Rotate;
                    Transform.BeginRotate(doc);
                    return true;
                }

                // 2) Hit-test scale handles
                var scaleHandle = HitTestScaleHandle(viewPos, poly);
                if (scaleHandle != SelectionTransform.ScaleHandle.None)
                {
                    _drag = DragMode.Scale;
                    _activeScaleHandle = scaleHandle;
                    Transform.BeginScale(scaleHandle, doc);
                    return true;
                }

                // 3) Hit-test inside polygon (move)
                if (PointInsidePolygon(doc, poly))
                {
                    _drag = DragMode.Move;
                    Transform.BeginMove(doc);

                    // If selection wasn't floating, lift it now
                    if (!Sel.Floating)
                        LiftSelectionBuffer();

                    return true;
                }

                // 4) Clicking outside selection:
                if (armed)
                {
                    // De-arm only
                    Sel.Armed = false;
                    RequestRedraw?.Invoke();
                    return true;
                }
                else
                {
                    // Clear selection
                    Sel.Clear();
                    RequestRedraw?.Invoke();
                    return true;
                }
            }

            // --- No selection, begin new marquee ---
            _drag = DragMode.NewSelection;
            _marqueeStart = doc;
            return true;
        }

        private void CommitStep()
        {
            if (!Sel.Floating || Sel.Buffer == null)
                return;

            var layer = _getActiveLayer();
            if (layer == null)
                return;

            LoggingService.Debug("Committing selection transform step");
            CommitFloatingInternal(layer);
        }

        private bool HandleMove(Point viewPos, bool leftHeld)
        {
            if (!leftHeld)
                return false;

            var doc = _lastDoc;

            switch (_drag)
            {
                case DragMode.Move:
                    Transform.UpdateMove(doc);
                    RequestRedraw?.Invoke();
                    return true;

                case DragMode.Rotate:
                    Transform.UpdateRotate(doc, snapStep: 1.0f);
                    RequestRedraw?.Invoke();
                    return true;

                case DragMode.Scale:
                    Transform.UpdateScale(doc, uniformLink: _shift);
                    RequestRedraw?.Invoke();
                    return true;

                case DragMode.Marquee:
                case DragMode.NewSelection:
                    // Live preview
                    RequestRedraw?.Invoke();
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Builds a selection mask from a rotated/transformed buffer by detecting non-transparent pixels.
        /// </summary>
        private SelectionRegion BuildMaskFromRotatedBuffer(byte[] buf, int w, int h)
        {
            var region = new SelectionRegion();
            region.EnsureSize(w, h);

            // Treat ANY pixel with alpha > 0 as "selected"
            int stride = w * 4;

            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                int x = 0;

                while (x < w)
                {
                    // Look for start of a run
                    int start = x;
                    while (x < w && buf[row + x * 4 + 3] != 0)
                        x++;

                    int end = x;

                    // A non-transparent run?
                    if (end > start)
                    {
                        region.AddRect(CreateRect(start, y, end - start, 1));
                    }

                    // Move forward to next candidate
                    while (x < w && buf[row + x * 4 + 3] == 0)
                        x++;
                }
            }

            return region;
        }

        // ─────────────────────────────────────────────────────────────
        // HIT TESTING
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Hit-tests rotation handles positioned around the selection polygon.
        /// </summary>
        private bool HitTestRotationHandle(Point v, List<Vector2> poly)
        {
            const float HANDLE_OFFSET = 20f;
            float hs = HandleSizePx;

            var center = SelectionTransform.ComputePolyCenter(poly);

            foreach (var p in poly)
            {
                // each vertex gets a rotation handle offset outward
                var dir = Vector2.Normalize(p - center);
                var handlePos = p + dir * HANDLE_OFFSET;

                if (HitPoint(v, handlePos, hs))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Hit-tests scale handles (corners and edge midpoints) on the selection polygon.
        /// </summary>
        private SelectionTransform.ScaleHandle HitTestScaleHandle(Point v, List<Vector2> poly)
        {
            float hs = HandleSizePx;

            if (poly.Count < 4)
                return SelectionTransform.ScaleHandle.None;

            // Corners
            if (HitPoint(v, poly[0], hs)) return SelectionTransform.ScaleHandle.NW;
            if (HitPoint(v, poly[1], hs)) return SelectionTransform.ScaleHandle.NE;
            if (HitPoint(v, poly[2], hs)) return SelectionTransform.ScaleHandle.SE;
            if (HitPoint(v, poly[3], hs)) return SelectionTransform.ScaleHandle.SW;

            // Edge midpoints for 8-handle mode
            static Vector2 Mid(Vector2 a, Vector2 b) => (a + b) / 2;

            if (HitPoint(v, Mid(poly[0], poly[1]), hs)) return SelectionTransform.ScaleHandle.N;
            if (HitPoint(v, Mid(poly[1], poly[2]), hs)) return SelectionTransform.ScaleHandle.E;
            if (HitPoint(v, Mid(poly[2], poly[3]), hs)) return SelectionTransform.ScaleHandle.S;
            if (HitPoint(v, Mid(poly[3], poly[0]), hs)) return SelectionTransform.ScaleHandle.W;

            return SelectionTransform.ScaleHandle.None;
        }

        private bool HitPoint(Point v, Vector2 p, float half)
        {
            return v.X >= p.X - half && v.X <= p.X + half &&
                   v.Y >= p.Y - half && v.Y <= p.Y + half;
        }

        /// <summary>
        /// Tests if a point is inside a polygon using ray-casting algorithm.
        /// </summary>
        private bool PointInsidePolygon(Vector2 pt, List<Vector2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[i];
                var b = poly[j];

                bool intersect =
                    ((a.Y > pt.Y) != (b.Y > pt.Y)) &&
                    (pt.X < (b.X - a.X) * (pt.Y - a.Y) / (b.Y - a.Y + 0.0001f) + a.X);

                if (intersect)
                    inside = !inside;
            }
            return inside;
        }

        // ─────────────────────────────────────────────────────────────
        // MARQUEE → MASK + POLY
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Finalizes a marquee selection by converting the dragged rectangle into a selection mask.
        /// </summary>
        private void FinalizeMarquee(Vector2 end)
        {
            var x0 = (int)Math.Round(_marqueeStart.X);
            var y0 = (int)Math.Round(_marqueeStart.Y);
            var x1 = (int)Math.Round(end.X);
            var y1 = (int)Math.Round(end.Y);

            int rx = Math.Min(x0, x1);
            int ry = Math.Min(y0, y1);
            int rw = Math.Abs(x1 - x0);
            int rh = Math.Abs(y1 - y0);

            if (rw < 1 || rh < 1)
            {
                Sel.Clear();
                return;
            }

            LoggingService.Debug("Marquee selection finalized rect=({X},{Y},{W},{H})", rx, ry, rw, rh);
            Sel.SetRect(rx, ry, rw, rh);
        }

        private readonly Action _liftCallback;
        private readonly Action _commitCallback;

        private void CommitTransform()
        {
            _commitCallback?.Invoke();
        }

        private void LiftSelectionBuffer()
        {
            _liftCallback?.Invoke();
        }
    }
}
