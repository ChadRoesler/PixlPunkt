using System;
using System.Diagnostics;
using System.Numerics;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Painting;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Selection;
using PixlPunkt.Core.Structs;
using PixlPunkt.Core.Symmetry;
using PixlPunkt.Core.Tools;
using PixlPunkt.Core.Viewport;
using PixlPunkt.UI.Helpers;
using PixlPunkt.UI.Rendering;
using Windows.Graphics;
using Windows.UI;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// Hosts a Win2D CanvasControl for rendering and interacting with a pixel-art document.
    /// 
    /// This is the core partial class containing:
    /// - Fields and properties
    /// - Construction and initialization
    /// - Document hooks and composition
    /// - Tool state binding and synchronization
    /// - Shared utility methods used by other partials
    /// 
    /// Other functionality is split into partial classes:
    /// - CanvasViewHost.Rendering.cs: Draw methods, grids, background
    /// - CanvasViewHost.Input.cs: Pointer events, wheel, keyboard
    /// - CanvasViewHost.Painting.cs: Stroke painting, commit, painters
    /// - CanvasViewHost.Shapes.cs: Shape tools, preview, modifiers
    /// - CanvasViewHost.BrushOverlay.cs: Cursor overlay, snapshot
    /// - CanvasViewHost.Viewport.cs: Zoom, pan, fit, viewport tracking
    /// - CanvasViewHost.Selection.cs: Selection tools, transforms, ants
    /// - CanvasViewHost.History.cs: Undo/redo, lift/commit
    /// </summary>
    public sealed partial class CanvasViewHost : UserControl
    {
        // ════════════════════════════════════════════════════════════════════
        // CONSTANTS
        // ════════════════════════════════════════════════════════════════════

        private const int MinBrushSize = 1;
        private const int MaxBrushSize = 64;
        private const double ZoomInFactor = 1.1;
        private const double ZoomOutFactor = 0.9;
        private const double MinZoomScale = 0.1;
        private const double MaxZoomScale = 64.0;
        private readonly PatternBackgroundService _patternService = new() { StripeBandDip = 4f, RepeatCycles = 16 };


        // ════════════════════════════════════════════════════════════════════
        // EVENTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired when a new frame of pixels is ready after a change. BGRA byte array, width, height.
        /// Useful for previews or external recording.
        /// </summary>
        public event Action<byte[], int, int>? FrameReady;

        /// <summary>
        /// Fired when the brush overlay position or size changes (for custom cursor previews).
        /// </summary>
        public event Action<Vector2, float>? BrushOverlayChanged;

        /// <summary>
        /// Fired for live FG sampling via dropper.
        /// </summary>
        public event Action<uint>? ForegroundSampledLive;

        /// <summary>
        /// Fired for live BG sampling via dropper.
        /// </summary>
        public event Action<uint>? BackgroundSampledLive;

        // ════════════════════════════════════════════════════════════════════
        // PROPERTIES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Gets the canvas document being edited.</summary>
        public CanvasDocument Document { get; }

        /// <summary>Gets the current brush opacity (0-255).</summary>
        public byte BrushOpacity => _brushOpacity;

        /// <summary>Current brush overlay snapshot (for preview/minimap).</summary>
        public BrushOverlaySnapshot CurrentBrushOverlay => _currentBrushOverlay;

        public uint ForegroundColor => _fg;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - CORE SYSTEMS
        // ════════════════════════════════════════════════════════════════════

        private readonly ZoomController _zoom = new();
        private StrokeEngine _stroke;
        private PixelSurface? _composite;
        private SelectionEngine _selectionEngine;

        /// <summary>
        /// Symmetry service for live stroke mirroring.
        /// </summary>
        private SymmetryService? _symmetryService;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - EXTERNAL STATE BINDINGS
        // ════════════════════════════════════════════════════════════════════

        private ToolState? _toolState;
        private PaletteService? _palette;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - RENDERING
        // ════════════════════════════════════════════════════════════════════

        private CanvasImageBrush? _stripeBrush;
        private bool _showPixelGrid = false;
        private bool _showTileGrid = true;
        private bool _showTileAnimationMappings = false;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - PAINTING STATE
        // ════════════════════════════════════════════════════════════════════

        private bool _isPainting;
        private uint _fg = 0xFFFF0000;
        private uint _bgColor = 0x00000000;
        private int _brushSize = 1;
        private byte _brushOpacity = 255;
        private byte _brushDensity = 255;
        private bool _hasLastDocPos;
        private int _lastDocX, _lastDocY;
        private bool _didMove;
        private bool _pendingStrokeFromOutside;
        private IStrokePainter? _activePainter;

        // Shift-line mode: hold shift after clicking to draw straight lines from origin
        private int _shiftLineOriginX;
        private int _shiftLineOriginY;
        private bool _shiftLineActive;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - NAVIGATION STATE
        // ════════════════════════════════════════════════════════════════════

        private bool _spacePan = false;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - CURSOR OVERLAY
        // ════════════════════════════════════════════════════════════════════

        private bool _hoverValid;
        private int _hoverX, _hoverY;
        private BrushOverlaySnapshot _currentBrushOverlay = BrushOverlaySnapshot.Empty;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - EXTERNAL DROPPER MODE (for color picker windows)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// When true, all canvas pointer input is routed to the external dropper callback.
        /// This allows color picker windows to sample colors from the canvas.
        /// </summary>
        private bool _externalDropperActive = false;

        /// <summary>
        /// Callback invoked when a color is sampled during external dropper mode.
        /// The parameter is the sampled BGRA color.
        /// </summary>
        private Action<uint>? _externalDropperCallback;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - SHAPE TOOL STATE
        // ════════════════════════════════════════════════════════════════════

        private bool _shapeDrag;
        private bool _shapeIsEllipse;
        private bool _shapeFilled;
        private int _sx, _sy, _ex, _ey;
        private bool _shapeShowStartPoint;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - FILL TOOL STATE
        // ════════════════════════════════════════════════════════════════════

        private bool _fillContiguous = true;
        private int _fillTolerance = 0;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - SELECTION STATE (bridges to selection subsystem)
        // ════════════════════════════════════════════════════════════════════

        private SelectionRegion _selRegion = new();
        private bool _havePreview;
        private RectInt32 _previewRect;
        private bool _selPushToTool;
        private bool _selApplyFromTool;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - STAGE (CAMERA) INTERACTION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Whether the stage is currently selected (from AnimationPanel).</summary>
        private bool _stageSelected;

        /// <summary>Whether we're currently dragging the stage.</summary>
        private bool _stageDragging;

        /// <summary>Whether we're currently resizing the stage.</summary>
        private bool _stageResizing;

        /// <summary>Which corner is being dragged for resize (0=TL, 1=TR, 2=BR, 3=BL).</summary>
        private int _stageResizeCorner;

        /// <summary>The stage position when drag started.</summary>
        private int _stageDragStartX, _stageDragStartY;

        /// <summary>The stage size when resize started.</summary>
        private int _stageDragStartW, _stageDragStartH;

        /// <summary>The pointer position when drag started.</summary>
        private int _stageDragPointerStartX, _stageDragPointerStartY;

        /// <summary>Current animation mode - stage is only shown in Canvas mode.</summary>
        private Animation.AnimationMode _animationMode = Animation.AnimationMode.Tile;

        /// <summary>
        /// Tracks whether the user has pending (unsaved) edits to the stage position.
        /// Set to true when user drags/resizes the stage, cleared when keyframe is added or frame changes.
        /// </summary>
        private bool _stagePendingEdits;

        /// <summary>
        /// The frame index where pending edits were made. Used to clear pending state when navigating away.
        /// </summary>
        private int _stagePendingEditsFrame;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - REFERENCE LAYER INTERACTION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>The currently selected reference layer for interaction.</summary>
        private ReferenceLayer? _selectedReferenceLayer;

        /// <summary>Whether we're currently dragging a reference layer.</summary>
        private bool _refLayerDragging;

        /// <summary>Whether we're currently resizing a reference layer.</summary>
        private bool _refLayerResizing;

        /// <summary>Which corner is being dragged for resize (0=TL, 1=TR, 2=BR, 3=BL).</summary>
        private int _refLayerResizeCorner;

        /// <summary>The reference layer position when drag started.</summary>
        private float _refLayerDragStartX, _refLayerDragStartY;

        /// <summary>The reference layer scale when resize started.</summary>
        private float _refLayerDragStartScale;

        /// <summary>The pointer document position when drag started.</summary>
        private float _refLayerDragPointerStartX, _refLayerDragPointerStartY;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - SUB-ROUTINE INTERACTION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>The currently selected sub-routine for interaction.</summary>
        private AnimationSubRoutine? _selectedSubRoutine;

        /// <summary>Whether we're currently dragging a sub-routine.</summary>
        private bool _subRoutineDragging;

        /// <summary>The sub-routine position when drag started (X).</summary>
        private double _subRoutineDragStartX;

        /// <summary>The sub-routine position when drag started (Y).</summary>
        private double _subRoutineDragStartY;

        /// <summary>The pointer document position when drag started (X).</summary>
        private int _subRoutineDragPointerStartX;

        /// <summary>The pointer document position when drag started (Y).</summary>
        private int _subRoutineDragPointerStartY;

        /// <summary>The normalized progress at which we're editing the sub-routine position.</summary>
        private float _subRoutineEditProgress;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - CROSSFADE / COMPOSITE SCRATCH
        // ════════════════════════════════════════════════════════════════════

        private PixelSurface? _xfPrev;
        private readonly Stopwatch _xfSw = new();

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - INPUT CURSOR
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The default cursor used for canvas painting operations.
        /// Uses the built-in Cross cursor for precise pixel targeting.
        /// </summary>
        private readonly InputCursor? _targetCursor;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - EXTERNAL MODIFICATION TRACKING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Flag to prevent refresh loops when we're the source of document changes.
        /// </summary>
        private bool _isCommittingChanges;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - LOCKED LAYER WARNING
        // ════════════════════════════════════════════════════════════════════

        private DispatcherTimer? _lockedLayerWarningTimer;

        // ════════════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Initializes a new instance of the <see cref="CanvasViewHost"/>.
        /// </summary>
        /// <param name="doc">The canvas document to display and edit.</param>
        public CanvasViewHost(CanvasDocument doc)
        {
            InitializeComponent();

            Document = doc;

            // Use built-in Cross cursor for precise pixel targeting
            _targetCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);

            // Initialize view and selection bounds
            _zoom.SetDocSize(doc.PixelWidth, doc.PixelHeight);
            _selRegion.EnsureSize(doc.PixelWidth, doc.PixelHeight);

            // Initialize stroke engine (no longer needs history stack - uses unified history)
            _stroke = new StrokeEngine(doc.TargetSurface);
            ResetStrokeForActive();
            _stroke.SetForeground(_fg);

            // Selection engine
            _selectionEngine = new SelectionEngine(activeLayerProvider: () => Document.ActiveLayer, docSizeProvider: () => (Document.PixelWidth, Document.PixelHeight),
                liftCallback: () => LiftSelectionWithHistory(),
                commitCallback: () => CommitFloatingWithHistory()
            );

            // Document hooks
            Document.ActiveLayerChanged += () =>
            {
                ResetStrokeForActive();
                CanvasView.Invalidate();
                HistoryStateChanged?.Invoke(); // allow UI to refresh CanUndo/CanRedo
            };
            Document.BeforeStructureChanged += CaptureForCrossfade;
            Document.StructureChanged += OnDocChanged;
            Document.LayersChanged += OnDocChanged;
            Document.DocumentModified += OnExternalDocumentModified;

            EnsureComposite();

            // Canvas events & selection init
            WireCanvasEvents();
            InitSelection();
            InitRulers();
            RaiseFrame();
        }

        public void UpdateTransparencyPatternForTheme(ElementTheme theme)
        {
            try
            {
                _patternService.ApplyTheme(theme);
                CanvasView?.Invalidate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update transparency pattern theme: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SHARED UTILITY METHODS (used by multiple partials)
        // ════════════════════════════════════════════════════════════════════

        private bool IsActiveLayerLocked
            => Document.ActiveLayer is RasterLayer rl && CanvasDocument.IsEffectivelyLocked(rl);

        /// <summary>
        /// Shows a warning that the active layer is locked and cannot be edited.
        /// The warning auto-dismisses after a few seconds.
        /// </summary>
        private void ShowLockedLayerWarning()
        {
            // Don't spam warnings - only show if not already visible
            if (LockedLayerWarning.IsOpen) return;

            LockedLayerWarning.IsOpen = true;

            // Auto-dismiss after 3 seconds
            _lockedLayerWarningTimer?.Stop();
            _lockedLayerWarningTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _lockedLayerWarningTimer.Tick += (_, __) =>
            {
                LockedLayerWarning.IsOpen = false;
                _lockedLayerWarningTimer.Stop();
            };
            _lockedLayerWarningTimer.Start();
        }

        private void LockedLayerWarning_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            _lockedLayerWarningTimer?.Stop();
        }

        private void EnsureComposite()
        {
            if (_composite != null &&
                _composite.Width == Document.Surface.Width &&
                _composite.Height == Document.Surface.Height)
                return;
            _composite = new PixelSurface(Document.Surface.Width, Document.Surface.Height);
        }

        private uint ReadCompositeBGRA(int x, int y)
        {
            var surf = _composite ?? Document.Surface;
            var w = surf.Width;
            var i = (y * w + x) * 4;
            var p = surf.Pixels;
            return (uint)(p[i] | (p[i + 1] << 8) | (p[i + 2] << 16) | (p[i + 3] << 24));
        }

        public void SetForeground(uint bgra)
        {
            _fg = bgra;
            _stroke.SetForeground(_fg);
            CanvasView.Invalidate();
        }

        public void ApplyBrush(BrushSettings s, uint fg)
        {
            _brushSize = s.Size;
            _stroke.SetBrushSize(_brushSize);
            _stroke.SetBrushShape(s.Shape);
            _brushOpacity = s.Opacity;
            _stroke.SetOpacity(_brushOpacity);
            _stroke.SetDensity(s.Density);
            _brushDensity = s.Density;
            uint merged = (fg & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);
            SetForeground(merged);
            CanvasView.Invalidate();
        }

        public void AdjustBrushSize(int delta)
        {
            _brushSize = _brushSize + delta;
            _stroke.SetBrushSize(_brushSize);
        }

        public void SetBrushSize(int s)
        {
            _brushSize = Math.Clamp(s, MinBrushSize, MaxBrushSize);
            _stroke.SetBrushSize(_brushSize);
            CanvasView.Invalidate();
        }

        public void SetBrushOpacity(int a)
        {
            _brushOpacity = (byte)Math.Clamp(a, 0, 255);
            _stroke.SetOpacity(_brushOpacity);
            _fg = (_fg & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);
            _stroke.SetForeground(_fg);
            CanvasView.Invalidate();
        }

        public void SetBrushShape(BrushShape shape)
        {
            _stroke.SetBrushShape(shape);
            CanvasView.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        // DOCUMENT HOOKS
        // ════════════════════════════════════════════════════════════════════

        private void OnDocChanged()
        {
            _xfSw.Restart();
            _selRegion.EnsureSize(Document.PixelWidth, Document.PixelHeight);
            UpdateActiveLayerPreview();
            UpdateViewport();
            CanvasView.Invalidate();
        }

        /// <summary>
        /// Handles external document modifications (e.g., from TileFrameEditorCanvas).
        /// Refreshes the canvas if we're not currently painting (to avoid loops).
        /// Also handles mask editing mode changes.
        /// </summary>
        private void OnExternalDocumentModified()
        {
            // Skip if we're actively painting (we're the source of changes)
            if (_isPainting) return;

            // Skip if we're committing (avoid reentry)
            if (_isCommittingChanges) return;

            // Refresh to pick up changes from external sources (e.g., TileFrameEditorCanvas)
            // This also handles mask editing mode toggling which fires StructureChanged
            CanvasView.Invalidate();
            RaiseFrame();
        }

        private void CaptureForCrossfade()
        {
            EnsureComposite();
            Document.CompositeTo(_composite!);

            // Recreate _xfPrev if dimensions changed or it doesn't exist
            if (_xfPrev == null ||
                _xfPrev.Width != _composite!.Width ||
                _xfPrev.Height != _composite!.Height)
            {
                _xfPrev = new PixelSurface(_composite!.Width, _composite!.Height);
            }

            Buffer.BlockCopy(_composite!.Pixels, 0, _xfPrev.Pixels, 0, _xfPrev.Pixels.Length);
        }

        // ════════════════════════════════════════════════════════════════════
        // STROKE / TOOLSTATE SYNCHRONIZATION
        // ════════════════════════════════════════════════════════════════════

        private void ResetStrokeForActive()
        {
            var target = Document.TargetSurface;
            _stroke = new StrokeEngine(target);
            _stroke.SetForeground(_fg);
            _stroke.SetBrushSize(_brushSize);
            _stroke.SetOpacity(_brushOpacity);
            _stroke.SetDensity(_brushDensity);
            _stroke.SetBrushShape(_toolState?.Brush.Shape ?? BrushShape.Square);
            _stroke.SetSymmetryService(_symmetryService);
        }

        private void SyncBrushPreviewFromToolSettings()
        {
            var strokeSettings = GetStrokeSettingsForCurrentTool();
            if (strokeSettings != null)
            {
                _brushSize = strokeSettings.Size;
                _brushDensity = strokeSettings is IDensitySettings ds ? ds.Density : (byte)255;
                _brushOpacity = strokeSettings is IOpacitySettings os ? os.Opacity : (byte)255;
                _stroke.SetBrushSize(strokeSettings.Size);
                _stroke.SetBrushShape(strokeSettings.Shape);
                _stroke.SetDensity(_brushDensity);
                _stroke.SetOpacity(_brushOpacity);
                // Update _fg to include the tool's opacity
                _fg = (_fg & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);
                _stroke.SetForeground(_fg);
            }
            else if (_toolState != null)
            {
                _brushSize = _toolState.Brush.Size;
                _brushDensity = _toolState.Brush.Density;
                _brushOpacity = _toolState.Brush.Opacity;
                _stroke.SetBrushSize(_toolState.Brush.Size);
                _stroke.SetBrushShape(_toolState.Brush.Shape);
                _stroke.SetDensity(_toolState.Brush.Density);
                _stroke.SetOpacity(_toolState.Brush.Opacity);
                // Update _fg to include the tool's opacity
                _fg = (_fg & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);
                _stroke.SetForeground(_fg);
            }
            CanvasView?.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        // TOOL STATE BINDING
        // ════════════════════════════════════════════════════════════════════

        public void BindToolState(ToolState toolState, PaletteService palette)
        {
            // Unhook old
            if (_toolState != null)
            {
                _toolState.BrushChanged -= OnBrushChanged;
                _toolState.ToolIdChanged -= OnToolChangedForPreview;
                _toolState.ActiveToolIdChanged -= OnEffectiveToolChangedForPreview;
                _toolState.OptionsChanged -= OnOptionsChanged;
                _toolState.SelectionCommitRequested -= CommitFloatingWithHistory;
                _toolState.SelectionCancelRequested -= CancelSelection;
                _toolState.SelectionFlipHorizontalRequested -= FlipSelectionHorizontal;
                _toolState.SelectionFlipVerticalRequested -= FlipSelectionVertical;
                _toolState.Symmetry.Changed -= OnSymmetryChanged;
            }

            if (_palette != null)
            {
                _palette.ForegroundChanged -= OnFgChanged;
                _palette.BackgroundChanged -= OnBgChanged;
            }

            // Assign new state
            _toolState = toolState;
            _palette = palette;

            // Initialize symmetry service
            _symmetryService = new SymmetryService(_toolState.Symmetry);
            _stroke.SetSymmetryService(_symmetryService);

            // Seed local flags
            _shapeFilled = _toolState.Brush.Filled;
            _shapeIsEllipse = (_toolState.CurrentToolId == ToolIds.ShapeEllipse);

            // Wire up tool-specific callbacks BEFORE hooking events
            // This ensures callbacks are available when options are rendered
            _toolState.GradientFill.OpenCustomGradientEditorCallback = OpenGradientFillCustomEditor;

            // Hook new
            _toolState.BrushChanged += OnBrushChanged;
            _toolState.ToolIdChanged += OnToolChangedForPreview;
            _toolState.ActiveToolIdChanged += OnEffectiveToolChangedForPreview;
            _toolState.OptionsChanged += OnOptionsChanged;
            _toolState.SelectionCommitRequested += CommitFloatingWithHistory;
            _toolState.SelectionCancelRequested += CancelSelection;
            _toolState.SelectionFlipHorizontalRequested += FlipSelectionHorizontal;
            _toolState.SelectionFlipVerticalRequested += FlipSelectionVertical;
            _toolState.Symmetry.Changed += OnSymmetryChanged;

            _palette.ForegroundChanged += OnFgChanged;
            _palette.BackgroundChanged += OnBgChanged;


            OnBgChanged(_palette.Background);

            ApplyBrush(_toolState.Brush, _palette.Foreground);

            // Initialize utility handlers
            InitUtilityHandlers();

            // Initialize tile handlers
            InitializeTileHandlers();

            // Initial sync of brush preview from current tool's settings
            SyncBrushPreviewFromToolSettings();
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - STATE SYNC
        // ════════════════════════════════════════════════════════════════════


        private void OnBrushChanged(BrushSettings s) => ApplyBrush(s, _fg);

        private void OnFgChanged(uint bgra)
        {
            // Preserve current opacity when FG changes
            uint merged = (bgra & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);
            SetForeground(merged);
            _toolState?.SetKnownFg(bgra);
        }

        private void OnBgChanged(uint bgra)
        {
            _bgColor = bgra; // Store for painter-based strokes

            _toolState?.SetKnownBg(bgra);
        }

        private string _lastToolId = ToolIds.Brush;
        private void OnToolChangedForPreview(string toolId)
        {
            bool wasEllipse = _shapeIsEllipse;
            _shapeIsEllipse = (toolId == ToolIds.ShapeEllipse);
            if (!wasEllipse && _shapeIsEllipse) _shapeDrag = false;

            // Commit floating selection when leaving Select tool category
            if (_toolState != null && _toolState.IsCategory(_lastToolId, ToolCategory.Select) &&
                !_toolState.IsCategory(toolId, ToolCategory.Select) && (_selState?.Floating ?? false))
                CommitFloatingWithHistory();

            _lastToolId = toolId;
        }

        /// <summary>
        /// Handles effective tool changes (including temporary overrides).
        /// Syncs brush preview from the new tool's settings.
        /// </summary>
        private void OnEffectiveToolChangedForPreview(string toolId)
        {
            // Commit floating selection when leaving Select tool category
            if (_toolState != null && _toolState.IsCategory(_lastToolId, ToolCategory.Select) &&
                !_toolState.IsCategory(toolId, ToolCategory.Select) && (_selState?.Floating ?? false))
                CommitFloatingWithHistory();

            // Sync brush preview from current tool's IBrushLikeSettings
            SyncBrushPreviewFromToolSettings();
        }

        /// <summary>
        /// Handles symmetry settings changes.
        /// Invalidates the canvas to update the symmetry overlay.
        /// </summary>
        private void OnSymmetryChanged()
        {
            // Update symmetry service with new settings
            if (_symmetryService != null && _toolState?.Symmetry != null)
            {
                _symmetryService = new SymmetryService(_toolState.Symmetry);
                _stroke.SetSymmetryService(_symmetryService);
            }

            // Redraw canvas to update symmetry overlay
            CanvasView?.Invalidate();
        }

        /// <summary>
        /// Synchronizes tool options with the stroke engine and updates selection transform state.
        /// </summary>
        private void OnOptionsChanged()
        {
            if (_toolState == null) return;

            // Sync brush preview from current tool's IBrushLikeSettings
            SyncBrushPreviewFromToolSettings();

            _fillContiguous = _toolState.FillContiguous;
            _fillTolerance = _toolState.FillTolerance;

            // Selection transforms (Canvas is authoritative; avoid reentry)
            if (_toolState.ActiveCategory != ToolCategory.Select) return;
            if (_selApplyFromTool) return;
            if (_selState == null) return;

            try
            {
                _selApplyFromTool = true;

                double wantX = _toolState.SelScalePercentX;
                double wantY = _toolState.SelScalePercentY;
                bool link = _toolState.SelScaleLink;
                ScaleMode scaleMode = _toolState.SelScaleMode;
                double angle = _toolState.RotationAngleDeg;
                RotationMode rotMode = _toolState.RotationMode;

                bool needsRedraw = false;

                // Check if any transform parameter changed that requires lifting
                bool scaleChanged = Math.Abs(wantX - _selState.ScaleX * 100.0) > 1e-6 ||
                                   Math.Abs(wantY - _selState.ScaleY * 100.0) > 1e-6 ||
                                   link != _selState.ScaleLink;
                bool rotationChanged = Math.Abs(angle - _selState.AngleDeg) > 1e-6;

                // Auto-lift selection if it's active but not floating and transform changes requested
                if (_selState.Active && !_selState.Floating && (scaleChanged || rotationChanged))
                {
                    LiftSelectionWithHistory();
                }

                // Only apply transforms if selection is now floating
                if (_selState.Floating)
                {
                    // Apply scale change
                    if (scaleChanged)
                    {
                        Selection_SetScale(wantX, wantY, link);
                        needsRedraw = true;
                    }

                    // Apply rotation angle change
                    if (rotationChanged)
                    {
                        _selState.AngleDeg = angle;
                        needsRedraw = true;
                    }
                }

                // These don't require floating - just store the mode
                if (rotMode != _selState.RotMode)
                {
                    _selState.RotMode = rotMode;
                    needsRedraw = true;
                }

                if (_selState.ScaleFilter != scaleMode)
                {
                    _selState.ScaleFilter = scaleMode;
                    needsRedraw = true;
                }

                // Redraw if any transform parameters changed
                if (needsRedraw)
                {
                    CanvasView.Invalidate();
                }
            }
            finally
            {
                _selApplyFromTool = false;
            }

            CanvasView.Invalidate();
        }

        /// <summary>
        /// Opens the gradient editor window for the GradientFill tool's custom mode.
        /// </summary>
        private void OpenGradientFillCustomEditor()
        {
            if (_toolState == null) return;

            var settings = _toolState.GradientFill;

            var win = new ColorPick.GradientWindow
            {
                GetStart = () => settings.LastKnownFg ?? 0xFF000000u,
                GetEnd = () => settings.LastKnownBg ?? 0xFFFFFFFFu,
                Commit = colors =>
                {
                    // Set the custom stops from the gradient colors
                    settings.SetCustomStopsFromColors(colors as System.Collections.Generic.IReadOnlyList<uint> ?? new System.Collections.Generic.List<uint>(colors));
                }
            };

            // Wire up external dropper mode for canvas color sampling
            win.DropperModeRequested += active =>
            {
                var mainWindow = App.PixlPunktMainWindow as PixlPunktMainWindow;
                if (active)
                {
                    mainWindow?.BeginExternalDropperMode(bgra => win.SetPickedColor(bgra));
                }
                else
                {
                    mainWindow?.EndExternalDropperMode();
                }
            };

            // Ensure dropper mode is disabled when window closes
            win.Closed += (_, __) => (App.PixlPunktMainWindow as PixlPunktMainWindow)?.EndExternalDropperMode();

            win.Activate();

            var appW = WindowHost.ApplyChrome(
                win,
                resizable: false,
                alwaysOnTop: true,
                minimizable: false,
                title: "Custom Gradient",
                owner: App.PixlPunktMainWindow);

            WindowHost.FitToContentAfterLayout(
                win,
                (FrameworkElement)win.Content,
                maxScreenFraction: 0.90,
                minLogicalWidth: 560,
                minLogicalHeight: 360);

            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }
        // --------------------------------------------------------------------
        // PUBLIC API - PREVIEW (PLACEHOLDERS)
        // --------------------------------------------------------------------

        public void PreviewPointerMoved(Vector2 worldPos, bool shift) { }
        public void PreviewPointerPressed(Vector2 worldPos, bool shift) { }
        public void PreviewPointerReleased(Vector2 worldPos, bool shift) { }

        /// <summary>
        /// Forces a redraw of the canvas. Call after external document changes.
        /// </summary>
        public void InvalidateCanvas()
        {
            // Re-sync zoom controller with potentially changed document size
            _zoom.SetDocSize(Document.PixelWidth, Document.PixelHeight);
            _selRegion.EnsureSize(Document.PixelWidth, Document.PixelHeight);

            // Ensure composite buffer matches new size
            EnsureComposite();

            // Reset stroke engine for new surface dimensions
            ResetStrokeForActive();

            // Invalidate and re-render
            CanvasView.Invalidate();
            RaiseFrame();
        }

        /// <summary>
        /// Forces a redraw of the canvas after a resize operation, adjusting the viewport
        /// to keep the original content at the same screen position.
        /// </summary>
        /// <param name="contentOffsetX">How many pixels the content was shifted right in the new canvas.</param>
        /// <param name="contentOffsetY">How many pixels the content was shifted down in the new canvas.</param>
        public void InvalidateCanvasAfterResize(int contentOffsetX, int contentOffsetY)
        {
            // Re-sync zoom controller with the new document size
            _zoom.SetDocSize(Document.PixelWidth, Document.PixelHeight);
            _selRegion.EnsureSize(Document.PixelWidth, Document.PixelHeight);

            // Ensure composite buffer matches new size
            EnsureComposite();

            // Adjust the viewport pan to compensate for content offset
            // If content shifted right by X pixels, we need to pan left by X*scale to keep it in place
            if (contentOffsetX != 0 || contentOffsetY != 0)
            {
                double panX = -contentOffsetX * _zoom.Scale;
                double panY = -contentOffsetY * _zoom.Scale;
                _zoom.PanBy(panX, panY);
            }

            // Reset stroke engine for new surface dimensions
            ResetStrokeForActive();

            // Update viewport and invalidate
            UpdateViewport();
            CanvasView.Invalidate();
            RaiseFrame();
        }

        // --------------------------------------------------------------------
        // STAGE (CAMERA) SELECTION
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets whether the stage is currently selected.
        /// </summary>
        public bool IsStageSelected => _stageSelected;

        /// <summary>
        /// Sets the stage selection state.
        /// </summary>
        /// <param name="selected">True to select the stage, false to deselect.</param>
        public void SetStageSelected(bool selected)
        {
            if (_stageSelected == selected) return;
            _stageSelected = selected;

            // Cancel any active painting when selecting stage
            if (selected && _isPainting)
            {
                _isPainting = false;
                _hasLastDocPos = false;
            }

            // Clear pending edits when deselecting stage
            if (!selected)
            {
                _stagePendingEdits = false;
            }

            CanvasView.Invalidate();
        }

        /// <summary>
        /// Clears the pending stage edits flag. Call this after adding a keyframe.
        /// </summary>
        public void ClearStagePendingEdits()
        {
            _stagePendingEdits = false;
        }

        /// <summary>
        /// Called when the animation frame changes. Clears pending edits if navigating to a different frame.
        /// </summary>
        public void OnAnimationFrameChanged(int newFrameIndex)
        {
            // If we have pending edits at a different frame, clear them
            // (user navigated away without saving)
            if (_stagePendingEdits && _stagePendingEditsFrame != newFrameIndex)
            {
                _stagePendingEdits = false;
            }

            CanvasView.Invalidate();
        }

        /// <summary>
        /// Sets the current animation mode. Stage overlay is only shown in Canvas mode.
        /// </summary>
        /// <param name="mode">The animation mode.</param>
        public void SetAnimationMode(Animation.AnimationMode mode)
        {
            if (_animationMode == mode) return;
            _animationMode = mode;

            // Deselect stage when switching to Tile mode
            if (mode == Animation.AnimationMode.Tile && _stageSelected)
            {
                _stageSelected = false;
            }

            CanvasView.Invalidate();
        }

        // --------------------------------------------------------------------
        // SUB-ROUTINE SELECTION
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets whether a sub-routine is currently selected.
        /// </summary>
        public bool IsSubRoutineSelected => _selectedSubRoutine != null;

        /// <summary>
        /// Gets the currently selected sub-routine.
        /// </summary>
        public AnimationSubRoutine? SelectedSubRoutine => _selectedSubRoutine;

        /// <summary>
        /// Sets the sub-routine selection state.
        /// </summary>
        /// <param name="subRoutine">The sub-routine to select, or null to deselect.</param>
        public void SetSelectedSubRoutine(AnimationSubRoutine? subRoutine)
        {
            if (_selectedSubRoutine == subRoutine) return;
            _selectedSubRoutine = subRoutine;

            // Cancel any active painting when selecting a sub-routine
            if (subRoutine != null && _isPainting)
            {
                _isPainting = false;
                _hasLastDocPos = false;
            }

            // Clear pending edits when deselecting
            if (subRoutine == null)
            {
                _subRoutineDragging = false;
            }

            CanvasView.Invalidate();
        }

        /// <summary>
        /// Raised when a sub-routine is selected on the canvas.
        /// </summary>
        public event Action<AnimationSubRoutine?>? SubRoutineSelected;

        // --------------------------------------------------------------------
        // EXTERNAL DROPPER MODE (for color picker windows)
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets whether external dropper mode is currently active.
        /// </summary>
        public bool IsExternalDropperActive => _externalDropperActive;

        /// <summary>
        /// Enables external dropper mode. While active, canvas pointer clicks sample colors
        /// and invoke the callback instead of performing normal tool operations.
        /// </summary>
        /// <param name="callback">Callback to invoke with the sampled BGRA color.</param>
        public void BeginExternalDropperMode(Action<uint> callback)
        {
            _externalDropperActive = true;
            _externalDropperCallback = callback;

            // Change cursor to indicate dropper mode
            if (_targetCursor != null)
            {
                // Keep using the target cursor, the eyedropper behavior is visual from the picker window
            }
        }

        /// <summary>
        /// Disables external dropper mode and resumes normal tool operations.
        /// </summary>
        public void EndExternalDropperMode()
        {
            _externalDropperActive = false;
            _externalDropperCallback = null;
        }

        /// <summary>
        /// Samples a color at the given document coordinates for external use.
        /// </summary>
        /// <param name="docX">X coordinate in document space.</param>
        /// <param name="docY">Y coordinate in document space.</param>
        /// <returns>The sampled BGRA color, or null if coordinates are out of bounds.</returns>
        public uint? SampleColorAt(int docX, int docY)
        {
            var w = Document.PixelWidth;
            var h = Document.PixelHeight;

            if (docX < 0 || docX >= w || docY < 0 || docY >= h)
                return null;

            return ReadCompositeBGRA(docX, docY);
        }

        // ════════════════════════════════════════════════════════════════════
        // INTERACTION TRACKING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Raised when the user interacts with the canvas (clicks, paints, etc.).
        /// Used to track focus for keyboard shortcut routing.
        /// </summary>
        public event Action? CanvasInteracted;

        /// <summary>
        /// Notifies that the user has interacted with the canvas.
        /// </summary>
        private void NotifyCanvasInteracted()
        {
            CanvasInteracted?.Invoke();
        }
    }
}
