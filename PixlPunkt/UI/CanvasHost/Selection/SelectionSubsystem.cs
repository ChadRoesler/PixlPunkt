using System;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Selection;
using PixlPunkt.Core.Tools;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.UI.CanvasHost.Selection
{
    /// <summary>
    /// Encapsulates all selection state and coordinates selection subsystem components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionSubsystem is the central coordinator for selection functionality in CanvasViewHost.
    /// It manages:
    /// - Selection state (active, floating, armed)
    /// - Floating buffer data and transform parameters
    /// - Pivot point configuration for rotation
    /// - Coordination between input handling, hit testing, rendering, and clipboard operations
    /// </para>
    /// <para>
    /// <strong>Architecture</strong>: This class owns the selection state and provides access to
    /// specialized handlers (input, hit testing, rendering, clipboard) that operate on that state.
    /// </para>
    /// </remarks>
    public sealed class SelectionSubsystem
    {
        // ════════════════════════════════════════════════════════════════════
        // ENUMS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Selection state machine states.
        /// </summary>
        public enum SelectionState
        {
            /// <summary>No selection exists.</summary>
            None,
            /// <summary>Selection exists and can be transformed.</summary>
            Armed
        }

        /// <summary>
        /// Scale handle positions.
        /// </summary>
        public enum SelHandle { None, NW, N, NE, E, SE, S, SW, W }

        /// <summary>
        /// Current drag operation type.
        /// </summary>
        public enum SelDrag { None, Marquee, Move, Scale, Rotate, Pivot }

        /// <summary>
        /// Rotation handle positions.
        /// </summary>
        public enum RotHandle { None, RNW, RN, RNE, RE, RSE, RS, RSW, RW }

        /// <summary>
        /// Pivot snap positions.
        /// </summary>
        public enum PivotSnap { None, Center, NW, N, NE, E, SE, S, SW, W }

        /// <summary>
        /// Selection combine modes.
        /// </summary>
        public enum SelCombine { Replace, Add, Subtract }

        // ════════════════════════════════════════════════════════════════════
        // CONSTANTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Visual size of scale handles.</summary>
        public const float HANDLE_DRAW_SIZE = 10f;
        /// <summary>Hit detection padding for scale handles.</summary>
        public const float HANDLE_HIT_PAD = 14f;
        /// <summary>Hit radius for pivot handle.</summary>
        public const float PIVOT_HIT_RADIUS = 12f;
        /// <summary>Visual size of pivot indicator.</summary>
        public const float PIVOT_DRAW_SIZE = 8f;
        /// <summary>Distance of rotation handles from selection edge.</summary>
        public const float ROT_HANDLE_OFFSET = 22f;
        /// <summary>Hit radius for rotation handles.</summary>
        public const float ROT_HANDLE_RADIUS = 8f;

        // Marching ants parameters
        /// <summary>Marching ants line thickness.</summary>
        public const float ANTS_THICKNESS = 2.0f;
        /// <summary>Marching ants dash length.</summary>
        public const float ANTS_ON = 4.0f;
        /// <summary>Marching ants gap length.</summary>
        public const float ANTS_OFF = 4.0f;
        /// <summary>Marching ants animation speed (pixels per frame).</summary>
        public const float ANTS_SPEED = 0.5f;

        // ════════════════════════════════════════════════════════════════════
        // SELECTION STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets whether a selection is active.</summary>
        public bool Active { get; set; }

        /// <summary>Gets or sets the selection bounding rectangle.</summary>
        public RectInt32 Rect { get; set; }

        /// <summary>Gets or sets whether the selection is floating (lifted from layer).</summary>
        public bool Floating { get; set; }

        /// <summary>Gets or sets the floating buffer X position.</summary>
        public int FloatX { get; set; }

        /// <summary>Gets or sets the floating buffer Y position.</summary>
        public int FloatY { get; set; }

        /// <summary>Gets or sets the floating buffer pixel data.</summary>
        public byte[]? Buffer { get; set; }

        /// <summary>Gets or sets the floating buffer width.</summary>
        public int BufferWidth { get; set; }

        /// <summary>Gets or sets the floating buffer height.</summary>
        public int BufferHeight { get; set; }

        /// <summary>Gets or sets the selection state machine state.</summary>
        public SelectionState State { get; set; } = SelectionState.None;

        /// <summary>Gets or sets whether the selection has been modified.</summary>
        public bool Dirty { get; set; }

        /// <summary>Gets or sets whether the selection has changed (for conditional commit).</summary>
        public bool Changed { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // DRAG STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets the current drag operation type.</summary>
        public SelDrag Drag { get; set; }

        /// <summary>Gets or sets the drag start position in view space.</summary>
        public Windows.Foundation.Point DragStartView { get; set; }

        /// <summary>Gets or sets the selection rect at drag start.</summary>
        public RectInt32 DragStartRect { get; set; }

        /// <summary>Gets or sets the move start X position.</summary>
        public int MoveStartX { get; set; }

        /// <summary>Gets or sets the move start Y position.</summary>
        public int MoveStartY { get; set; }

        /// <summary>Gets or sets the hovered scale handle.</summary>
        public SelHandle HoverHandle { get; set; }

        /// <summary>Gets or sets the active scale handle during drag.</summary>
        public SelHandle ActiveHandle { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // TRANSFORM STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets the X scale factor.</summary>
        public double ScaleX { get; set; } = 1.0;

        /// <summary>Gets or sets the Y scale factor.</summary>
        public double ScaleY { get; set; } = 1.0;

        /// <summary>Gets or sets whether scale is linked (maintain aspect ratio).</summary>
        public bool ScaleLink { get; set; }

        /// <summary>Gets or sets the scale interpolation filter.</summary>
        public ScaleMode ScaleFilter { get; set; } = ScaleMode.NearestNeighbor;

        /// <summary>Gets or sets the rotation mode.</summary>
        public RotationMode RotMode { get; set; } = RotationMode.RotSprite;

        /// <summary>Gets or sets the current drag rotation angle (degrees).</summary>
        public double AngleDeg { get; set; }

        /// <summary>Gets or sets the cumulative rotation angle (degrees).</summary>
        public double CumulativeAngleDeg { get; set; }

        // Scale drag state
        /// <summary>Gets or sets the floating X at scale start.</summary>
        public int ScaleStartFX { get; set; }
        /// <summary>Gets or sets the floating Y at scale start.</summary>
        public int ScaleStartFY { get; set; }
        /// <summary>Gets or sets the width at scale start.</summary>
        public int ScaleStartW { get; set; }
        /// <summary>Gets or sets the height at scale start.</summary>
        public int ScaleStartH { get; set; }
        /// <summary>Gets or sets the X scale at scale start.</summary>
        public double ScaleStartScaleX { get; set; }
        /// <summary>Gets or sets the Y scale at scale start.</summary>
        public double ScaleStartScaleY { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // ORIGINAL DIMENSIONS (for handle positioning after rotation)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets the original buffer width before transforms.</summary>
        public int OrigW { get; set; }

        /// <summary>Gets or sets the original buffer height before transforms.</summary>
        public int OrigH { get; set; }

        /// <summary>Gets or sets the original center X position.</summary>
        public int OrigCenterX { get; set; }

        /// <summary>Gets or sets the original center Y position.</summary>
        public int OrigCenterY { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // PIVOT STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets the pivot X offset from center in local space.</summary>
        public double PivotOffsetX { get; set; }

        /// <summary>Gets or sets the pivot Y offset from center in local space.</summary>
        public double PivotOffsetY { get; set; }

        /// <summary>Gets or sets whether the pivot has been customized.</summary>
        public bool PivotCustom { get; set; }

        /// <summary>Gets or sets the snap position the pivot is snapped to.</summary>
        public PivotSnap PivotSnappedTo { get; set; } = PivotSnap.Center;

        // ════════════════════════════════════════════════════════════════════
        // ROTATION DRAG STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets the rotation start center X.</summary>
        public int RotStartCenterX { get; set; }

        /// <summary>Gets or sets the rotation start center Y.</summary>
        public int RotStartCenterY { get; set; }

        /// <summary>Gets or sets the rotation start angle.</summary>
        public double RotStartAngleDeg { get; set; }

        /// <summary>Gets or sets the rotation start pointer angle.</summary>
        public double RotStartPointerAngleDeg { get; set; }

        /// <summary>Gets or sets the fixed pivot X during rotation.</summary>
        public double RotFixedPivotX { get; set; }

        /// <summary>Gets or sets the fixed pivot Y during rotation.</summary>
        public double RotFixedPivotY { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // MARCHING ANTS STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets the marching ants animation phase.</summary>
        public float AntsPhase { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // HISTORY STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets the pending pixel change item for history.</summary>
        public PixelChangeItem? PendingCs { get; set; }

        /// <summary>Gets or sets the lift rectangle for history tracking.</summary>
        public RectInt32 LiftRect { get; set; }

        /// <summary>Gets or sets whether selection was lifted from document.</summary>
        public bool LiftedFromDoc { get; set; }

        /// <summary>Gets or sets the source rectangle for lifted selection.</summary>
        public RectInt32 SourceRect { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // PREVIEW STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets whether a preview is active.</summary>
        public bool HavePreview { get; set; }

        /// <summary>Gets or sets the preview rectangle.</summary>
        public RectInt32 PreviewRect { get; set; }

        /// <summary>Gets or sets the preview buffer.</summary>
        public byte[]? PreviewBuf { get; set; }

        /// <summary>Gets or sets the preview buffer width.</summary>
        public int PreviewW { get; set; }

        /// <summary>Gets or sets the preview buffer height.</summary>
        public int PreviewH { get; set; }

        /// <summary>Gets or sets the preview scale X.</summary>
        public double PreviewScaleX { get; set; }

        /// <summary>Gets or sets the preview scale Y.</summary>
        public double PreviewScaleY { get; set; }

        /// <summary>Gets or sets the preview angle.</summary>
        public double PreviewAngle { get; set; }

        /// <summary>Gets or sets the preview scale filter.</summary>
        public ScaleMode PreviewScaleFilter { get; set; }

        /// <summary>Gets or sets the preview rotation mode.</summary>
        public RotationMode PreviewRotMode { get; set; }

        /// <summary>Gets or sets whether the buffer has been flipped (requires buffer-based ant drawing).</summary>
        public bool BufferFlipped { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // COMBINE MODE STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets the selection combine mode.</summary>
        public SelCombine CombineMode { get; set; } = SelCombine.Replace;

        /// <summary>Gets or sets whether there was a selection before the current operation.</summary>
        public bool HadSelBefore { get; set; }

        /// <summary>Gets or sets the selection rect before the current operation.</summary>
        public RectInt32 BeforeRect { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // EXTERNAL DEPENDENCIES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets the selection region mask.</summary>
        public SelectionRegion Region { get; }

        /// <summary>Gets or sets the tool state for synchronization.</summary>
        public ToolState? ToolState { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // COMPUTED PROPERTIES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets the scaled width.</summary>
        public int ScaledW => Math.Max(1, (int)Math.Round(BufferWidth * ScaleX));

        /// <summary>Gets the scaled height.</summary>
        public int ScaledH => Math.Max(1, (int)Math.Round(BufferHeight * ScaleY));

        // ════════════════════════════════════════════════════════════════════
        // TRANSFORM HISTORY TRACKING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Snapshot of transform state at the start of a drag operation.</summary>
        public SelectionTransformItem.TransformSnapshot? DragStartSnapshot { get; set; }

        /// <summary>
        /// Captures the current transform state as a snapshot.
        /// </summary>
        /// <param name="includeBuffer">Whether to include the buffer data (for scale/rotate operations).</param>
        public SelectionTransformItem.TransformSnapshot CaptureTransformSnapshot(bool includeBuffer = false)
        {
            byte[]? bufferCopy = null;
            if (includeBuffer && Buffer != null)
            {
                bufferCopy = (byte[])Buffer.Clone();
            }

            return new SelectionTransformItem.TransformSnapshot(
                floatX: FloatX,
                floatY: FloatY,
                scaleX: ScaleX,
                scaleY: ScaleY,
                angleDeg: AngleDeg,
                cumulativeAngleDeg: CumulativeAngleDeg,
                origCenterX: OrigCenterX,
                origCenterY: OrigCenterY,
                origW: OrigW,
                origH: OrigH,
                pivotOffsetX: PivotOffsetX,
                pivotOffsetY: PivotOffsetY,
                pivotCustom: PivotCustom,
                buffer: bufferCopy,
                bufferWidth: BufferWidth,
                bufferHeight: BufferHeight
            );
        }

        /// <summary>
        /// Applies a transform snapshot to restore selection state.
        /// </summary>
        public void ApplyTransformSnapshot(SelectionTransformItem.TransformSnapshot snapshot)
        {
            FloatX = snapshot.FloatX;
            FloatY = snapshot.FloatY;
            ScaleX = snapshot.ScaleX;
            ScaleY = snapshot.ScaleY;
            AngleDeg = snapshot.AngleDeg;
            CumulativeAngleDeg = snapshot.CumulativeAngleDeg;
            OrigCenterX = snapshot.OrigCenterX;
            OrigCenterY = snapshot.OrigCenterY;
            OrigW = snapshot.OrigW;
            OrigH = snapshot.OrigH;
            PivotOffsetX = snapshot.PivotOffsetX;
            PivotOffsetY = snapshot.PivotOffsetY;
            PivotCustom = snapshot.PivotCustom;

            // Restore buffer if present in snapshot
            if (snapshot.Buffer != null)
            {
                Buffer = (byte[])snapshot.Buffer.Clone();
                BufferWidth = snapshot.BufferWidth;
                BufferHeight = snapshot.BufferHeight;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionSubsystem"/> class.
        /// </summary>
        public SelectionSubsystem()
        {
            Region = new SelectionRegion();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionSubsystem"/> class with an existing region.
        /// </summary>
        /// <param name="region">The selection region to use.</param>
        public SelectionSubsystem(SelectionRegion region)
        {
            Region = region;
        }

        // ════════════════════════════════════════════════════════════════════
        // STATE MANAGEMENT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Resets the pivot to the center of the selection.
        /// </summary>
        public void ResetPivot()
        {
            PivotOffsetX = 0;
            PivotOffsetY = 0;
            PivotCustom = false;
            PivotSnappedTo = PivotSnap.Center;
        }

        /// <summary>
        /// Resets all transform state to defaults.
        /// </summary>
        public void ResetTransform()
        {
            ScaleX = 1.0;
            ScaleY = 1.0;
            ScaleLink = false;
            ScaleFilter = ScaleMode.NearestNeighbor;
            AngleDeg = 0.0;
            CumulativeAngleDeg = 0.0;
            ResetPivot();
        }

        /// <summary>
        /// Clears all selection state.
        /// </summary>
        public void Clear()
        {
            Active = false;
            Floating = false;
            Buffer = null;
            BufferWidth = 0;
            BufferHeight = 0;
            FloatX = 0;
            FloatY = 0;
            OrigW = 0;
            OrigH = 0;
            OrigCenterX = 0;
            OrigCenterY = 0;
            State = SelectionState.None;
            Drag = SelDrag.None;
            HavePreview = false;
            PreviewBuf = null;
            BufferFlipped = false;
            Region.Clear();
            Rect = CreateRect(0, 0, 0, 0);
            ResetTransform();
        }

        /// <summary>
        /// Updates the marching ants animation phase.
        /// </summary>
        public void AdvanceAnts()
        {
            AntsPhase += ANTS_SPEED;
            if (AntsPhase >= ANTS_ON + ANTS_OFF)
                AntsPhase -= ANTS_ON + ANTS_OFF;
        }

        /// <summary>
        /// Notifies the tool state of selection presence changes.
        /// </summary>
        public void NotifyToolState()
        {
            ToolState?.SetSelectionPresence(Active, Floating);
            ToolState?.SetSelectionScale(ScaleX * 100.0, ScaleY * 100.0, ScaleLink);
            ToolState?.SetSelectionScaleMode(ScaleFilter);
            ToolState?.SetRotationAngle(AngleDeg);
        }

        // ════════════════════════════════════════════════════════════════════
        // UTILITY METHODS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Normalizes a rectangle to have positive width and height.
        /// </summary>
        public static RectInt32 Normalize(RectInt32 r) =>
            CreateRect(r.Width >= 0 ? r.X : r.X + r.Width,
                r.Height >= 0 ? r.Y : r.Y + r.Height,
                Math.Abs(r.Width),
                Math.Abs(r.Height));

        /// <summary>
        /// Clamps a rectangle to surface bounds.
        /// </summary>
        public static RectInt32 ClampToSurface(RectInt32 r, int w, int h)
        {
            int x0 = Math.Clamp(r.X, 0, w);
            int y0 = Math.Clamp(r.Y, 0, h);
            int x1 = Math.Clamp(r.X + r.Width, 0, w);
            int y1 = Math.Clamp(r.Y + r.Height, 0, h);
            return CreateRect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }

        /// <summary>
        /// Computes the intersection of two rectangles.
        /// </summary>
        public static RectInt32 Intersect(RectInt32 a, RectInt32 b)
        {
            int x0 = Math.Max(a.X, b.X);
            int y0 = Math.Max(a.Y, b.Y);
            int x1 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y1 = Math.Min(a.Y + a.Height, b.Y + b.Height);
            return (x1 > x0 && y1 > y0) ? CreateRect(x0, y0, x1 - x0, y1 - y0) : CreateRect(0, 0, 0, 0);
        }

        /// <summary>
        /// Computes the union of two rectangles.
        /// </summary>
        public static RectInt32 UnionRect(RectInt32 a, RectInt32 b)
        {
            int x0 = Math.Min(a.X, b.X);
            int y0 = Math.Min(a.Y, b.Y);
            int x1 = Math.Max(a.X + a.Width, b.X + b.Width);
            int y1 = Math.Max(a.Y + a.Height, b.Y + b.Height);
            return CreateRect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }

        /// <summary>
        /// Rotates a point around a center.
        /// </summary>
        public static (float x, float y) RotateAround(float px, float py, float cx, float cy, float radians)
        {
            float dx = px - cx, dy = py - cy;
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            return (cx + dx * c - dy * s, cy + dx * s + dy * c);
        }
    }
}
