using System;
using System.Diagnostics;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Selection;
using PixlPunkt.Core.Tools;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.UI.CanvasHost.Selection
{
    /// <summary>
    /// View-side selection state for <c>CanvasViewHost</c>: the marquee phase, drag/hover state,
    /// marching-ants animation, preview caches, and the tool-state bridge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <em>floating</em> selection itself is document state (<see cref="FloatingSelection"/>,
    /// owned by <c>CanvasDocument.Floating</c>). <see cref="Lifted"/> is the view's reference to
    /// that same object, and every floating/transform property here forwards to it while a
    /// selection is lifted. When nothing is lifted, the transform properties fall back to local
    /// values that describe the handle frame of the armed (non-floating) marquee.
    /// </para>
    /// <para>
    /// That means there is exactly one copy of the floating state, and history items, the save
    /// path and the view all read the same object.
    /// </para>
    /// </remarks>
    public sealed class SelectionSubsystem
    {
        // ════════════════════════════════════════════════════════════════════
        // ENUMS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Selection state machine states.</summary>
        public enum SelectionState
        {
            /// <summary>No selection exists.</summary>
            None,
            /// <summary>Selection exists and can be transformed.</summary>
            Armed
        }

        /// <summary>Scale handle positions.</summary>
        public enum SelHandle { None, NW, N, NE, E, SE, S, SW, W }

        /// <summary>Current drag operation type.</summary>
        public enum SelDrag { None, Marquee, Move, Scale, Rotate, Pivot }

        /// <summary>Rotation handle positions.</summary>
        public enum RotHandle { None, RNW, RN, RNE, RE, RSE, RS, RSW, RW }

        /// <summary>Pivot snap positions.</summary>
        public enum PivotSnap { None, Center, NW, N, NE, E, SE, S, SW, W }

        // ════════════════════════════════════════════════════════════════════
        // CONSTANTS
        // ════════════════════════════════════════════════════════════════════

        public const float HANDLE_DRAW_SIZE = 10f;
        public const float HANDLE_HIT_PAD = 14f;
        public const float PIVOT_HIT_RADIUS = 12f;
        public const float PIVOT_DRAW_SIZE = 8f;
        public const float ROT_HANDLE_OFFSET = 22f;
        public const float ROT_HANDLE_RADIUS = 8f;

        public const float ANTS_THICKNESS = 2.0f;
        public const float ANTS_ON = 4.0f;
        public const float ANTS_OFF = 4.0f;
        public const float ANTS_SPEED = 30f;

        // ════════════════════════════════════════════════════════════════════
        // MARQUEE STATE (view-owned)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets or sets whether a selection is active (marquee or floating).</summary>
        public bool Active { get; set; }

        /// <summary>Gets or sets the selection bounding rectangle (a cached copy of <c>Region.Bounds</c>).</summary>
        public RectInt32 Rect { get; set; }

        /// <summary>Gets or sets the selection state machine state.</summary>
        public SelectionState State { get; set; } = SelectionState.None;

        /// <summary>Gets the selection region mask. This is the document's mask (<c>CanvasDocument.Selection</c>).</summary>
        public SelectionRegion Region { get; }

        /// <summary>Gets or sets the tool state for synchronization.</summary>
        public ToolState? ToolState { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // FLOATING SELECTION (document-owned; forwarded)
        // ════════════════════════════════════════════════════════════════════

        private FloatingSelection? _lifted;

        /// <summary>
        /// The document's floating selection, or null. Set by the host whenever
        /// <c>CanvasDocument.SelectionChanged</c> fires. When a selection becomes lifted, the
        /// view's current scale filter / rotation mode / link settings are carried onto it.
        /// </summary>
        public FloatingSelection? Lifted
        {
            get => _lifted;
            set
            {
                if (ReferenceEquals(_lifted, value)) return;
                bool becameLifted = value != null && _lifted == null;
                _lifted = value;
                if (becameLifted)
                {
                    value!.ScaleFilter = _scaleFilter;
                    value.RotMode = _rotMode;
                    value.ScaleLink = _scaleLink;
                }
                if (value == null)
                {
                    // Back to an armed marquee (or nothing): the handle frame is the region bounds.
                    _scaleX = _scaleY = 1.0;
                    _angleDeg = _cumulativeAngleDeg = 0.0;
                    _origW = Rect.Width; _origH = Rect.Height;
                    _origCenterX = Rect.X + Rect.Width / 2; _origCenterY = Rect.Y + Rect.Height / 2;
                    _pivotOffsetX = _pivotOffsetY = 0; _pivotCustom = false;
                    _regionNonRectangular = false; _bufferFlipped = false;
                    PreviewBuf = null;
                }
            }
        }

        /// <summary>Gets whether the selection is floating (lifted from its layer).</summary>
        public bool Floating => _lifted != null;

        /// <summary>The floating buffer (null when not floating). Setting it while floating replaces the lifted pixels.</summary>
        public byte[]? Buffer
        {
            get => _lifted?.Pixels;
            set { if (_lifted != null && value != null) _lifted.Pixels = value; }
        }
        public int BufferWidth { get => _lifted?.Width ?? 0; set { if (_lifted != null) _lifted.Width = value; } }
        public int BufferHeight { get => _lifted?.Height ?? 0; set { if (_lifted != null) _lifted.Height = value; } }
        public int FloatX { get => _lifted?.X ?? 0; set { if (_lifted != null) _lifted.X = value; } }
        public int FloatY { get => _lifted?.Y ?? 0; set { if (_lifted != null) _lifted.Y = value; } }

        // Transform frame: forwards while lifted, local for the armed marquee otherwise.
        private double _scaleX = 1.0, _scaleY = 1.0;
        private bool _scaleLink;
        private ScaleMode _scaleFilter = ScaleMode.NearestNeighbor;
        private RotationMode _rotMode = RotationMode.RotSprite;
        private double _angleDeg, _cumulativeAngleDeg;
        private int _origW, _origH, _origCenterX, _origCenterY;
        private double _pivotOffsetX, _pivotOffsetY;
        private bool _pivotCustom;
        private bool _regionNonRectangular, _bufferFlipped;

        public double ScaleX { get => _lifted?.ScaleX ?? _scaleX; set { if (_lifted != null) _lifted.ScaleX = value; else _scaleX = value; } }
        public double ScaleY { get => _lifted?.ScaleY ?? _scaleY; set { if (_lifted != null) _lifted.ScaleY = value; else _scaleY = value; } }
        public bool ScaleLink { get => _lifted?.ScaleLink ?? _scaleLink; set { if (_lifted != null) _lifted.ScaleLink = value; _scaleLink = value; } }
        public ScaleMode ScaleFilter { get => _lifted?.ScaleFilter ?? _scaleFilter; set { if (_lifted != null) _lifted.ScaleFilter = value; _scaleFilter = value; } }
        public RotationMode RotMode { get => _lifted?.RotMode ?? _rotMode; set { if (_lifted != null) _lifted.RotMode = value; _rotMode = value; } }
        public double AngleDeg { get => _lifted?.AngleDeg ?? _angleDeg; set { if (_lifted != null) _lifted.AngleDeg = value; else _angleDeg = value; } }
        public double CumulativeAngleDeg { get => _lifted?.CumulativeAngleDeg ?? _cumulativeAngleDeg; set { if (_lifted != null) _lifted.CumulativeAngleDeg = value; else _cumulativeAngleDeg = value; } }
        public int OrigW { get => _lifted?.OrigW ?? _origW; set { if (_lifted != null) _lifted.OrigW = value; else _origW = value; } }
        public int OrigH { get => _lifted?.OrigH ?? _origH; set { if (_lifted != null) _lifted.OrigH = value; else _origH = value; } }
        public int OrigCenterX { get => _lifted?.OrigCenterX ?? _origCenterX; set { if (_lifted != null) _lifted.OrigCenterX = value; else _origCenterX = value; } }
        public int OrigCenterY { get => _lifted?.OrigCenterY ?? _origCenterY; set { if (_lifted != null) _lifted.OrigCenterY = value; else _origCenterY = value; } }
        public double PivotOffsetX { get => _lifted?.PivotOffsetX ?? _pivotOffsetX; set { if (_lifted != null) _lifted.PivotOffsetX = value; else _pivotOffsetX = value; } }
        public double PivotOffsetY { get => _lifted?.PivotOffsetY ?? _pivotOffsetY; set { if (_lifted != null) _lifted.PivotOffsetY = value; else _pivotOffsetY = value; } }
        public bool PivotCustom { get => _lifted?.PivotCustom ?? _pivotCustom; set { if (_lifted != null) _lifted.PivotCustom = value; else _pivotCustom = value; } }
        public bool RegionNonRectangular { get => _lifted?.RegionNonRectangular ?? _regionNonRectangular; set { if (_lifted != null) _lifted.RegionNonRectangular = value; else _regionNonRectangular = value; } }
        public bool BufferFlipped { get => _lifted?.BufferFlipped ?? _bufferFlipped; set { if (_lifted != null) _lifted.BufferFlipped = value; else _bufferFlipped = value; } }

        /// <summary>Gets the scaled width.</summary>
        public int ScaledW => Math.Max(1, (int)Math.Round(BufferWidth * ScaleX));
        /// <summary>Gets the scaled height.</summary>
        public int ScaledH => Math.Max(1, (int)Math.Round(BufferHeight * ScaleY));

        // ════════════════════════════════════════════════════════════════════
        // DRAG STATE
        // ════════════════════════════════════════════════════════════════════

        public SelDrag Drag { get; set; }
        public Windows.Foundation.Point DragStartView { get; set; }
        public int MoveStartX { get; set; }
        public int MoveStartY { get; set; }
        public SelHandle HoverHandle { get; set; }
        public SelHandle ActiveHandle { get; set; }

        // Scale drag state
        public int ScaleStartFX { get; set; }
        public int ScaleStartFY { get; set; }
        public int ScaleStartW { get; set; }
        public int ScaleStartH { get; set; }
        public double ScaleStartScaleX { get; set; }
        public double ScaleStartScaleY { get; set; }

        // Rotation drag state
        public int RotStartCenterX { get; set; }
        public int RotStartCenterY { get; set; }
        public double RotStartAngleDeg { get; set; }
        public double RotStartPointerAngleDeg { get; set; }
        public double RotFixedPivotX { get; set; }
        public double RotFixedPivotY { get; set; }

        /// <summary>Snapshot of transform state at the start of a drag operation.</summary>
        public SelectionTransformItem.TransformSnapshot? DragStartSnapshot { get; set; }

        /// <summary>Captures the current floating transform (default when nothing is lifted).</summary>
        public SelectionTransformItem.TransformSnapshot CaptureTransformSnapshot(bool includeBuffer = false) =>
            _lifted == null ? default : SelectionTransformItem.Capture(_lifted, includeBuffer);

        // ════════════════════════════════════════════════════════════════════
        // MARCHING ANTS / PREVIEW STATE
        // ════════════════════════════════════════════════════════════════════

        public float AntsPhase { get; set; }
        private readonly Stopwatch _antsTimer = Stopwatch.StartNew();

        public bool HavePreview { get; set; }
        public byte[]? PreviewBuf { get; set; }
        public int PreviewW { get; set; }
        public int PreviewH { get; set; }
        public double PreviewScaleX { get; set; }
        public double PreviewScaleY { get; set; }
        public double PreviewAngle { get; set; }
        public ScaleMode PreviewScaleFilter { get; set; }
        public RotationMode PreviewRotMode { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // CONSTRUCTORS
        // ════════════════════════════════════════════════════════════════════

        public SelectionSubsystem() : this(new SelectionRegion()) { }

        public SelectionSubsystem(SelectionRegion region)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
        }

        // ════════════════════════════════════════════════════════════════════
        // STATE MANAGEMENT
        // ════════════════════════════════════════════════════════════════════

        public void ResetPivot()
        {
            PivotOffsetX = 0;
            PivotOffsetY = 0;
            PivotCustom = false;
        }

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
        /// Clears the view's marquee state. Does not touch the document's floating selection:
        /// callers commit or cancel through <c>FloatingSelectionOps</c> first.
        /// </summary>
        public void Clear()
        {
            Active = false;
            State = SelectionState.None;
            Drag = SelDrag.None;
            HavePreview = false;
            PreviewBuf = null;
            Region.Clear();
            Rect = CreateRect(0, 0, 0, 0);
            _origW = _origH = _origCenterX = _origCenterY = 0;
            ResetTransform();
        }

        public void AdvanceAnts()
        {
            float elapsed = (float)_antsTimer.Elapsed.TotalSeconds;
            _antsTimer.Restart();
            float period = ANTS_ON + ANTS_OFF;
            AntsPhase = (AntsPhase + ANTS_SPEED * elapsed) % period;
        }

        public void NotifyToolState()
        {
            ToolState?.SetSelectionPresence(Active, Floating);
            ToolState?.SetSelectionScale(ScaleX * 100.0, ScaleY * 100.0, ScaleLink);
            ToolState?.SetSelectionScaleMode(ScaleFilter);
            ToolState?.SetRotationAngle(AngleDeg);
        }

        // ════════════════════════════════════════════════════════════════════
        // PIVOT GEOMETRY (single home; hit-testing, rendering and transforms all use these)
        // ════════════════════════════════════════════════════════════════════

        public (double X, double Y) GetPivotPositionDoc()
        {
            double centerX = OrigCenterX;
            double centerY = OrigCenterY;
            if (!PivotCustom || (PivotOffsetX == 0 && PivotOffsetY == 0))
                return (centerX, centerY);

            double radians = CumulativeAngleDeg * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double globalOffsetX = PivotOffsetX * cos - PivotOffsetY * sin;
            double globalOffsetY = PivotOffsetX * sin + PivotOffsetY * cos;
            return (centerX + globalOffsetX, centerY + globalOffsetY);
        }

        public (float X, float Y) GetPivotPositionView(Windows.Foundation.Rect dest, double scale)
        {
            if (Drag == SelDrag.Rotate)
            {
                return ((float)(RotFixedPivotX * scale + dest.X),
                        (float)(RotFixedPivotY * scale + dest.Y));
            }
            var (docX, docY) = GetPivotPositionDoc();
            return ((float)(dest.X + docX * scale), (float)(dest.Y + docY * scale));
        }

        // ════════════════════════════════════════════════════════════════════
        // UTILITY METHODS
        // ════════════════════════════════════════════════════════════════════

        public static RectInt32 Normalize(RectInt32 r) =>
            CreateRect(r.Width >= 0 ? r.X : r.X + r.Width,
                r.Height >= 0 ? r.Y : r.Y + r.Height,
                Math.Abs(r.Width),
                Math.Abs(r.Height));

        public static RectInt32 ClampToSurface(RectInt32 r, int w, int h)
        {
            int x0 = Math.Clamp(r.X, 0, w);
            int y0 = Math.Clamp(r.Y, 0, h);
            int x1 = Math.Clamp(r.X + r.Width, 0, w);
            int y1 = Math.Clamp(r.Y + r.Height, 0, h);
            return CreateRect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }

        public static RectInt32 Intersect(RectInt32 a, RectInt32 b)
        {
            int x0 = Math.Max(a.X, b.X);
            int y0 = Math.Max(a.Y, b.Y);
            int x1 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y1 = Math.Min(a.Y + a.Height, b.Y + b.Height);
            return (x1 > x0 && y1 > y0) ? CreateRect(x0, y0, x1 - x0, y1 - y0) : CreateRect(0, 0, 0, 0);
        }

        public static RectInt32 UnionRect(RectInt32 a, RectInt32 b)
        {
            int x0 = Math.Min(a.X, b.X);
            int y0 = Math.Min(a.Y, b.Y);
            int x1 = Math.Max(a.X + a.Width, b.X + b.Width);
            int y1 = Math.Max(a.Y + a.Height, b.Y + b.Height);
            return CreateRect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }

        public static (float x, float y) RotateAround(float px, float py, float cx, float cy, float radians)
        {
            float dx = px - cx, dy = py - cy;
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            return (cx + dx * c - dy * s, cy + dx * s + dy * c);
        }
    }
}
