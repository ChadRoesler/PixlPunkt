using System;
using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Uno.Core.Animation;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.Imaging;
using PixlPunkt.Uno.Core.Painting;
using PixlPunkt.Uno.Core.Palette;
using PixlPunkt.Uno.Core.Rendering;
using PixlPunkt.Uno.Core.Selection;
using PixlPunkt.Uno.Core.Structs;
using PixlPunkt.Uno.Core.Symmetry;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.Core.Viewport;
using PixlPunkt.Uno.UI.Helpers;
using PixlPunkt.Uno.UI.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Windows;
#if HAS_UNO
using Uno.WinUI.Graphics2DSK;
#endif
using Windows.Graphics;
using Windows.UI;
using PixlPunkt.Uno.Core.Platform;
using Microsoft.UI.Dispatching;

namespace PixlPunkt.Uno.UI.CanvasHost
{
    /// <summary>
    /// Hosts an SKCanvasElement for rendering and interacting with a pixel-art document.
    /// Uses Uno's direct Skia integration for hardware-accelerated rendering.
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
        // FIELDS - SKCANVASELEMENT INSTANCES (Uno platforms)
        // ════════════════════════════════════════════════════════════════════

#if HAS_UNO
        /// <summary>Main canvas element using SKCanvasElement for hardware-accelerated rendering.</summary>
        private MainCanvasElement? _mainCanvasElement;

        /// <summary>Horizontal ruler element.</summary>
        private HorizontalRulerElement? _horizontalRulerElement;

        /// <summary>Vertical ruler element.</summary>
        private VerticalRulerElement? _verticalRulerElement;
#endif

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - SKXAMLCANVAS INSTANCES (WinAppSdk fallback)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Main canvas using SKXamlCanvas (WinAppSdk fallback).</summary>
        private SKXamlCanvas? _mainCanvasXaml;

        /// <summary>Horizontal ruler using SKXamlCanvas (WinAppSdk fallback).</summary>
        private SKXamlCanvas? _horizontalRulerXaml;

        /// <summary>Vertical ruler using SKXamlCanvas (WinAppSdk fallback).</summary>
        private SKXamlCanvas? _verticalRulerXaml;

        // ════════════════════════════════════════════════════════════════════
        // CANVAS ABSTRACTION HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Helper to get the main canvas as a FrameworkElement for shared operations.
        /// </summary>
        private FrameworkElement _mainCanvas =>
#if HAS_UNO
            _mainCanvasElement ?? (FrameworkElement?)_mainCanvasXaml ?? throw new InvalidOperationException("Canvas not initialized");
#else
            _mainCanvasXaml ?? throw new InvalidOperationException("Canvas not initialized");
#endif

        /// <summary>
        /// Invalidates the main canvas.
        /// </summary>
        private void InvalidateMainCanvas()
        {
#if HAS_UNO
            _mainCanvasElement?.Invalidate();
#endif
            _mainCanvasXaml?.Invalidate();
        }

        /// <summary>
        /// Invalidates the rulers.
        /// </summary>
        private void InvalidateRulers()
        {
#if HAS_UNO
            _horizontalRulerElement?.Invalidate();
            _verticalRulerElement?.Invalidate();
#endif
            _horizontalRulerXaml?.Invalidate();
            _verticalRulerXaml?.Invalidate();
        }

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
        // FIELDS - RENDERING (SKIASHARP)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Cached checkerboard pattern shader for transparency background.</summary>
        private SKShader? _checkerboardShader;

        /// <summary>Cached checkerboard pattern bitmap.</summary>
        private SKBitmap? _checkerboardBitmap;

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

        private bool _externalDropperActive = false;
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
        // FIELDS - SELECTION STATE
        // ════════════════════════════════════════════════════════════════════

        private SelectionRegion _selRegion = new();
        private bool _havePreview;
        private RectInt32 _previewRect;
        private bool _selPushToTool;
        private bool _selApplyFromTool;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - STAGE (CAMERA) INTERACTION
        // ════════════════════════════════════════════════════════════════════

        private bool _stageSelected;
        private bool _stageDragging;
        private bool _stageResizing;
        private int _stageResizeCorner;
        private int _stageDragStartX, _stageDragStartY;
        private int _stageDragStartW, _stageDragStartH;
        private int _stageDragPointerStartX, _stageDragPointerStartY;
        private Animation.AnimationMode _animationMode = Animation.AnimationMode.Tile;
        private bool _stagePendingEdits;
        private int _stagePendingEditsFrame;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - REFERENCE LAYER INTERACTION
        // ════════════════════════════════════════════════════════════════════

        private ReferenceLayer? _selectedReferenceLayer;
        private bool _refLayerDragging;
        private bool _refLayerResizing;
        private int _refLayerResizeCorner;
        private float _refLayerDragStartX, _refLayerDragStartY;
        private float _refLayerDragStartScale;
        private float _refLayerDragPointerStartX, _refLayerDragPointerStartY;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - SUB-ROUTINE INTERACTION
        // ════════════════════════════════════════════════════════════════════

        private AnimationSubRoutine? _selectedSubRoutine;
        private bool _subRoutineDragging;
        private double _subRoutineDragStartX;
        private double _subRoutineDragStartY;
        private int _subRoutineDragPointerStartX;
        private int _subRoutineDragPointerStartY;
        private float _subRoutineEditProgress;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - CROSSFADE / COMPOSITE SCRATCH
        // ════════════════════════════════════════════════════════════════════

        private PixelSurface? _xfPrev;
        private readonly Stopwatch _xfSw = new();

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - INPUT CURSOR
        // ════════════════════════════════════════════════════════════════════

        private readonly InputCursor? _targetCursor;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - EXTERNAL MODIFICATION TRACKING
        // ════════════════════════════════════════════════════════════════════

        private bool _isCommittingChanges;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - LOCKED LAYER WARNING
        // ════════════════════════════════════════════════════════════════════

        private DispatcherTimer? _lockedLayerWarningTimer;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS - SKIA RENDERING OPTIMIZATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Whether we're actively painting (mouse down + moving).
        /// Used to trigger more aggressive invalidation on Skia platforms.
        /// </summary>
        private bool _isActivePainting;

        /// <summary>
        /// Cached reference to DispatcherQueue for high-priority invalidation.
        /// </summary>
        private DispatcherQueue? _dispatcherQueue;

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

            // Cache dispatcher queue for forced invalidation
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            Document = doc;

            // Use built-in Cross cursor for precise pixel targeting
            _targetCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);

            // Create and add SKCanvasElement instances
            InitializeCanvasElements();

            // Initialize view and selection bounds
            _zoom.SetDocSize(doc.PixelWidth, doc.PixelHeight);
            _selRegion.EnsureSize(doc.PixelWidth, doc.PixelHeight);

            // Initialize stroke engine (no longer needs history stack - uses unified history)
            _stroke = new StrokeEngine(doc.TargetSurface);
            ResetStrokeForActive();
            _stroke.SetForeground(_fg);

            // Selection engine
            _selectionEngine = new SelectionEngine(
                activeLayerProvider: () => Document.ActiveLayer,
                docSizeProvider: () => (Document.PixelWidth, Document.PixelHeight),
                liftCallback: () => LiftSelectionWithHistory(),
                commitCallback: () => CommitFloatingWithHistory()
            );

            // Document hooks
            Document.ActiveLayerChanged += () =>
            {
                ResetStrokeForActive();
                InvalidateMainCanvas();
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

            // Cleanup when control is unloaded
            Unloaded += OnControlUnloaded;
        }

        /// <summary>
        /// Handles control unload to clean up resources like the render hook and timers.
        /// </summary>
        private void OnControlUnloaded(object sender, RoutedEventArgs e)
        {
            // Stop the continuous rendering hook and fallback timer to prevent memory leaks
            StopContinuousRendering();
            
            // Dispose checkerboard resources
            _checkerboardShader?.Dispose();
            _checkerboardShader = null;
            _checkerboardBitmap?.Dispose();
            _checkerboardBitmap = null;
            _checkerboardPaint?.Dispose();
            _checkerboardPaint = null;
        }

        /// <summary>
        /// Creates and initializes the SKCanvasElement instances.
        /// SKCanvasElement provides hardware-accelerated rendering via direct Skia integration.
        /// On WinAppSDK, falls back to SKXamlCanvas.
        /// </summary>
        private void InitializeCanvasElements()
        {
#if HAS_UNO
            // On Uno platforms, verify SKCanvasElement support
            if (!SKCanvasElement.IsSupportedOnCurrentPlatform())
            {
                throw new PlatformNotSupportedException(
                    "SKCanvasElement is not supported on this platform. " +
                    "Ensure you are running on a Skia-rendered target (desktop, android with SkiaRenderer, etc.).");
            }

            // Create main canvas element using SKCanvasElement for hardware-accelerated rendering
            _mainCanvasElement = new MainCanvasElement();
            _mainCanvasElement.DrawCallback = RenderMainCanvas;
            CanvasContainer.Children.Add(_mainCanvasElement);

            // Create horizontal ruler element
            _horizontalRulerElement = new HorizontalRulerElement
            {
                DrawCallback = RenderHorizontalRuler
            };
            HorizontalRulerContainer.Children.Add(_horizontalRulerElement);

            // Create vertical ruler element
            _verticalRulerElement = new VerticalRulerElement
            {
                DrawCallback = RenderVerticalRuler
            };
            VerticalRulerContainer.Children.Add(_verticalRulerElement);
#else
            // On WinAppSDK, use SKXamlCanvas as fallback
            _mainCanvasXaml = new SKXamlCanvas();
            _mainCanvasXaml.PaintSurface += MainCanvasXaml_PaintSurface;
            CanvasContainer.Children.Add(_mainCanvasXaml);

            // Create horizontal ruler using SKXamlCanvas
            _horizontalRulerXaml = new SKXamlCanvas();
            _horizontalRulerXaml.PaintSurface += HorizontalRulerXaml_PaintSurface;
            HorizontalRulerContainer.Children.Add(_horizontalRulerXaml);

            // Create vertical ruler using SKXamlCanvas
            _verticalRulerXaml = new SKXamlCanvas();
            _verticalRulerXaml.PaintSurface += VerticalRulerXaml_PaintSurface;
            VerticalRulerContainer.Children.Add(_verticalRulerXaml);
#endif
        }

#if !HAS_UNO
        /// <summary>
        /// Paint surface handler for main canvas (WinAppSDK fallback).
        /// </summary>
        private void MainCanvasXaml_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            using var renderer = new SkiaCanvasRenderer(e.Surface.Canvas, e.Info.Width, e.Info.Height);
            RenderMainCanvas(renderer);
        }

        /// <summary>
        /// Paint surface handler for horizontal ruler (WinAppSDK fallback).
        /// </summary>
        private void HorizontalRulerXaml_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            RenderHorizontalRuler(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        }

        /// <summary>
        /// Paint surface handler for vertical ruler (WinAppSDK fallback).
        /// </summary>
        private void VerticalRulerXaml_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            RenderVerticalRuler(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        }
#endif

        /// <summary>
        /// Main canvas render callback - receives ICanvasRenderer from MainCanvasElement.
        /// Delegates to CanvasView_Draw in the Rendering partial class.
        /// </summary>
        private void RenderMainCanvas(ICanvasRenderer renderer)
        {
            CanvasView_Draw(renderer);
        }

        /// <summary>
        /// Horizontal ruler render callback.
        /// </summary>
        private void RenderHorizontalRuler(SKCanvas canvas, float width, float height)
        {
            using var renderer = new SkiaCanvasRenderer(canvas, width, height);
            HorizontalRuler_Draw(renderer);
        }

        /// <summary>
        /// Vertical ruler render callback.
        /// </summary>
        private void RenderVerticalRuler(SKCanvas canvas, float width, float height)
        {
            using var renderer = new SkiaCanvasRenderer(canvas, width, height);
            VerticalRuler_Draw(renderer);
        }

        public void UpdateTransparencyPatternForTheme(ElementTheme theme)
        {
            try
            {
                _patternService.ApplyTheme(theme);
                // Invalidate checkerboard cache
                _checkerboardShader?.Dispose();
                _checkerboardShader = null;
                _checkerboardBitmap?.Dispose();
                _checkerboardBitmap = null;
                _checkerboardPaint?.Dispose();
                _checkerboardPaint = null;
                InvalidateMainCanvas();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update transparency pattern theme: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SHARED UTILITY METHODS
        // ════════════════════════════════════════════════════════════════════

        private bool IsActiveLayerLocked
            => Document.ActiveLayer is RasterLayer rl && CanvasDocument.IsEffectivelyLocked(rl);

        private void ShowLockedLayerWarning()
        {
            if (LockedLayerWarning.IsOpen) return;

            LockedLayerWarning.IsOpen = true;

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
            InvalidateMainCanvas();
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
            InvalidateMainCanvas();
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
            InvalidateMainCanvas();
        }

        public void SetBrushOpacity(int a)
        {
            _brushOpacity = (byte)Math.Clamp(a, 0, 255);
            _stroke.SetOpacity(_brushOpacity);
            _fg = (_fg & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);
            _stroke.SetForeground(_fg);
            InvalidateMainCanvas();
        }

        public void SetBrushShape(BrushShape shape)
        {
            _stroke.SetBrushShape(shape);
            InvalidateMainCanvas();
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
            InvalidateMainCanvas();
        }

        private void OnExternalDocumentModified()
        {
            // Skip if we're actively painting (we're the source of changes)
            if (_isPainting) return;

            // Skip if we're committing (avoid reentry)
            if (_isCommittingChanges) return;

            // Refresh to pick up changes from external sources (e.g., TileFrameEditorCanvas)
            // This also handles mask editing mode toggling which fires StructureChanged
            InvalidateMainCanvas();
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
            InvalidateMainCanvas();
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
            InvalidateMainCanvas();
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
                    InvalidateMainCanvas();
                }
            }
            finally
            {
                _selApplyFromTool = false;
            }

            InvalidateMainCanvas();
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
        // PUBLIC API
        // --------------------------------------------------------------------

        public void PreviewPointerMoved(Vector2 worldPos, bool shift) { }
        public void PreviewPointerPressed(Vector2 worldPos, bool shift) { }
        public void PreviewPointerReleased(Vector2 worldPos, bool shift) { }

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
            InvalidateMainCanvas();
            RaiseFrame();
        }

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
            InvalidateMainCanvas();
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

            InvalidateMainCanvas();
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

            InvalidateMainCanvas();
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

            InvalidateMainCanvas();
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

            InvalidateMainCanvas();
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
        // SKIA RENDER SYNCHRONIZATION
        // ════════════════════════════════════════════════════════════════════
        // 
        // On Skia platforms, SKXamlCanvas.Invalidate() is asynchronous and posts
        // to the dispatcher queue. During rapid painting, pointer events can flood
        // the queue faster than repaints occur, causing visual lag.
        //
        // Unlike Win2D (which has GPU-backed composition running in parallel),
        // Skia rendering competes with input events for dispatcher time.
        //
        // Our strategy: Track pending invalidations and ensure they get processed
        // by using UpdateLayout() to force a synchronous layout pass when needed.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Whether the paint invalidation system is active.
        /// </summary>
        private bool _isRenderingHooked;

        /// <summary>
        /// Counter to track idle frames for auto-stop.
        /// </summary>
        private int _idleFrameCount;

        /// <summary>
        /// Maximum idle ticks before auto-stopping.
        /// </summary>
        private const int MaxIdleFramesBeforeStop = 10;

        /// <summary>
        /// Counter to throttle forced layout passes (every N invalidations).
        /// </summary>
        private int _invalidationCounter;

        /// <summary>
        /// How often to force a synchronous layout pass (every N invalidations).
        /// Lower = more responsive but higher CPU. Higher = smoother but more lag.
        /// </summary>
        private const int ForceLayoutEveryN = 2;

        /// <summary>
        /// Starts active painting mode - enables more aggressive invalidation.
        /// </summary>
        private void StartContinuousRendering()
        {
            if (_isRenderingHooked) return;
            _isRenderingHooked = true;
            _idleFrameCount = 0;
            _invalidationCounter = 0;
        }

        /// <summary>
        /// Stops active painting mode.
        /// </summary>
        private void StopContinuousRendering()
        {
            _isRenderingHooked = false;
        }

        /// <summary>
        /// Starts the rapid invalidation for Skia platforms during painting.
        /// </summary>
        private void StartPaintInvalidationTimer()
        {
            StartContinuousRendering();
        }

        /// <summary>
        /// Stops the rapid invalidation when painting ends.
        /// </summary>
        private void StopPaintInvalidationTimer()
        {
            // Let it auto-stop after stroke commit
        }

        // ════════════════════════════════════════════════════════════════════
        // SKIA INVALIDATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Forces canvas invalidation during active painting.
        /// On Skia platforms, we periodically force a synchronous layout pass
        /// to ensure the repaint actually happens between pointer events.
        /// </summary>
        private void ForceInvalidate()
        {
            // Always queue the invalidation
            InvalidateMainCanvas();

            // During active painting, periodically force synchronous processing
            if (_isActivePainting)
            {
                _idleFrameCount = 0;
                _invalidationCounter++;

                // Every N invalidations, force a synchronous layout pass
                // This ensures the queued Invalidate() actually gets processed
                if (_invalidationCounter >= ForceLayoutEveryN)
                {
                    _invalidationCounter = 0;
                    
                    // UpdateLayout() forces XAML to process pending layout/render requests
                    // This is the key to getting synchronous-ish rendering on Skia
                    _mainCanvas.UpdateLayout();
                }
            }
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
