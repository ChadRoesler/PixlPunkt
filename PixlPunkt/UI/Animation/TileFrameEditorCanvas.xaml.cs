using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Brush;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Painting;
using PixlPunkt.Core.Painting.Helpers;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Tools;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.UI.CanvasHost;
using PixlPunkt.UI.Rendering;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace PixlPunkt.UI.Animation
{
    /// <summary>
    /// A portal/viewport into the main canvas, locked to a specific tile region.
    /// Reads pixels directly from the document and paints using the same StrokeEngine.
    /// Provides a zoomed, focused editing experience for a single tile.
    /// </summary>
    public sealed partial class TileFrameEditorCanvas : UserControl
    {
        // ====================================================================
        // FIELDS - BINDINGS
        // ====================================================================

        private TileAnimationState? _state;
        private CanvasDocument? _document;
        private CanvasViewHost? _canvasHost;
        private ToolState? _toolState;
        private PaletteService? _palette;

        // Pattern background service for diagonal stripes
        private readonly PatternBackgroundService _patternService = new() { StripeBandDip = 4f, RepeatCycles = 8 };

        // ====================================================================
        // FIELDS - TILE STATE
        // ====================================================================

        private int _tileX = -1;
        private int _tileY = -1;
        private int _tileWidth;
        private int _tileHeight;

        // ====================================================================
        // FIELDS - ZOOM STATE
        // ====================================================================

        private double _zoomLevel = 1.0;
        private const double MinZoom = 0.5;
        private const double MaxZoom = 16.0;
        private const double ZoomStep = 1.2;

        // Pan offset (in screen pixels)
        private double _panOffsetX = 0;
        private double _panOffsetY = 0;

        // Pan state
        private bool _isPanning;
        private Point _panStartPoint;
        private double _panStartOffsetX;
        private double _panStartOffsetY;
        private bool _spacePan; // Space key held for panning

        // ====================================================================
        // FIELDS - PAINTING STATE
        // ====================================================================

        private bool _isPainting;
        private int _lastDocX = -1;
        private int _lastDocY = -1;
        private bool _hasLastPos;

        // Stroke engine for direct painting
        private StrokeEngine? _stroke;
        private IStrokePainter? _activePainter;
        private byte[]? _strokeBeforePixels;

        // ====================================================================
        // FIELDS - HOVER STATE
        // ====================================================================

        private int _hoverDocX = -1;
        private int _hoverDocY = -1;
        private bool _hoverValid;

        // ====================================================================
        // FIELDS - BRUSH STATE (synced from tool state)
        // ====================================================================

        private uint _foregroundColor = 0xFFFFFFFF;
        private uint _backgroundColor = 0x00000000;
        private int _brushSize = 1;
        private BrushShape _brushShape = BrushShape.Circle;
        private byte _brushOpacity = 255;
        private byte _brushDensity = 255;

        // ====================================================================
        // FIELDS - ONION SKINNING
        // ====================================================================

        private bool _onionSkinPrev1 = false;  // Show -1 frame at 50% opacity
        private bool _onionSkinPrev2 = false;  // Show -2 frame at 25% opacity
        private bool _onionSkinNext1 = false;  // Show +1 frame at 50% opacity
        private bool _onionSkinNext2 = false;  // Show +2 frame at 25% opacity

        // Onion skin opacity levels
        private const byte OnionOpacity1 = 128;  // 50% for ±1 frame
        private const byte OnionOpacity2 = 64;   // 25% for ±2 frame

        // Onion skin tint colors (to differentiate past/future)
        private static readonly Color OnionPrevTint = Color.FromArgb(255, 255, 100, 100);  // Reddish for past
        private static readonly Color OnionNextTint = Color.FromArgb(255, 100, 100, 255);  // Bluish for future

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when the tile content has been modified (for thumbnail refresh).
        /// </summary>
        public event Action? TileModified;

        /// <summary>
        /// Raised when onion skinning settings change.
        /// </summary>
        public event Action? OnionSkinChanged;

        // ====================================================================
        // PROPERTIES - ONION SKINNING
        // ====================================================================

        /// <summary>
        /// Gets or sets whether to show the previous frame (-1) at 50% opacity.
        /// </summary>
        public bool OnionSkinPrev1
        {
            get => _onionSkinPrev1;
            set
            {
                if (_onionSkinPrev1 != value)
                {
                    _onionSkinPrev1 = value;
                    OnionSkinChanged?.Invoke();
                    EditorCanvas?.Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to show the frame 2 back (-2) at 25% opacity.
        /// </summary>
        public bool OnionSkinPrev2
        {
            get => _onionSkinPrev2;
            set
            {
                if (_onionSkinPrev2 != value)
                {
                    _onionSkinPrev2 = value;
                    OnionSkinChanged?.Invoke();
                    EditorCanvas?.Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to show the next frame (+1) at 50% opacity.
        /// </summary>
        public bool OnionSkinNext1
        {
            get => _onionSkinNext1;
            set
            {
                if (_onionSkinNext1 != value)
                {
                    _onionSkinNext1 = value;
                    OnionSkinChanged?.Invoke();
                    EditorCanvas?.Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to show the frame 2 ahead (+2) at 25% opacity.
        /// </summary>
        public bool OnionSkinNext2
        {
            get => _onionSkinNext2;
            set
            {
                if (_onionSkinNext2 != value)
                {
                    _onionSkinNext2 = value;
                    OnionSkinChanged?.Invoke();
                    EditorCanvas?.Invalidate();
                }
            }
        }

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public TileFrameEditorCanvas()
        {
            this.InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyStripeColors();
            TransparencyStripeMixer.ColorsChanged += OnStripeColorsChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            TransparencyStripeMixer.ColorsChanged -= OnStripeColorsChanged;
        }

        private void OnStripeColorsChanged()
        {
            ApplyStripeColors();
            _patternService.Invalidate();
            EditorCanvas?.Invalidate();
        }

        private void ApplyStripeColors()
        {
            var light = Color.FromArgb(255,
                TransparencyStripeMixer.LightR,
                TransparencyStripeMixer.LightG,
                TransparencyStripeMixer.LightB);

            var dark = Color.FromArgb(255,
                TransparencyStripeMixer.DarkR,
                TransparencyStripeMixer.DarkG,
                TransparencyStripeMixer.DarkB);

            if (_patternService.LightColor != light || _patternService.DarkColor != dark)
            {
                _patternService.LightColor = light;
                _patternService.DarkColor = dark;
            }
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Binds the editor to animation state and document.
        /// </summary>
        public void Bind(TileAnimationState? state, CanvasDocument? document)
        {
            // Unbind previous
            if (_state != null)
            {
                _state.CurrentFrameChanged -= OnCurrentFrameChanged;
                _state.SelectedReelChanged -= OnSelectedReelChanged;
            }
            if (_document != null)
            {
                _document.DocumentModified -= OnDocumentModified;
            }

            _state = state;
            _document = document;

            // Setup stroke engine for the document
            if (_document != null)
            {
                _stroke = new StrokeEngine(_document.TargetSurface);
                SyncBrushToStrokeEngine();
            }

            // Bind new
            if (_state != null)
            {
                _state.CurrentFrameChanged += OnCurrentFrameChanged;
                _state.SelectedReelChanged += OnSelectedReelChanged;
            }
            if (_document != null)
            {
                _document.DocumentModified += OnDocumentModified;
            }

            RefreshDisplay();
        }

        /// <summary>
        /// Binds the canvas host for brush overlay sync.
        /// </summary>
        public void BindCanvasHost(CanvasViewHost? canvasHost)
        {
            _canvasHost = canvasHost;
        }

        /// <summary>
        /// Binds the tool state and palette service.
        /// </summary>
        public void BindToolState(ToolState? toolState, PaletteService? palette)
        {
            // Unbind previous
            if (_toolState != null)
            {
                _toolState.ActiveToolIdChanged -= OnToolChanged;
                _toolState.OptionsChanged -= OnToolOptionsChanged;
                _toolState.BrushChanged -= OnBrushChanged;
            }
            if (_palette != null)
            {
                _palette.ForegroundChanged -= OnForegroundChanged;
                _palette.BackgroundChanged -= OnBackgroundChanged;
            }

            _toolState = toolState;
            _palette = palette;

            // Bind new
            if (_toolState != null)
            {
                _toolState.ActiveToolIdChanged += OnToolChanged;
                _toolState.OptionsChanged += OnToolOptionsChanged;
                _toolState.BrushChanged += OnBrushChanged;
                SyncBrushFromToolState();
            }
            if (_palette != null)
            {
                _palette.ForegroundChanged += OnForegroundChanged;
                _palette.BackgroundChanged += OnBackgroundChanged;
                _foregroundColor = _palette.Foreground;
                _backgroundColor = _palette.Background;
            }
        }

        /// <summary>
        /// Sets the foreground color (kept for API compatibility).
        /// </summary>
        public void SetForegroundColor(uint bgra)
        {
            _foregroundColor = bgra;
        }

        /// <summary>
        /// Sets the background color (kept for API compatibility).
        /// </summary>
        public void SetBackgroundColor(uint bgra)
        {
            _backgroundColor = bgra;
        }

        /// <summary>
        /// Refreshes the display to reflect current state.
        /// </summary>
        public void RefreshDisplay()
        {
            UpdateTilePosition();
            EditorCanvas?.Invalidate();
        }

        // ====================================================================
        // TOOL/BRUSH STATE SYNC
        // ====================================================================

        private void OnToolChanged(string toolId)
        {
            SyncBrushFromToolState();
            EditorCanvas?.Invalidate();
        }

        private void OnToolOptionsChanged()
        {
            SyncBrushFromToolState();
            EditorCanvas?.Invalidate();
        }

        private void OnBrushChanged(BrushSettings s)
        {
            SyncBrushFromToolState();
            EditorCanvas?.Invalidate();
        }

        private void OnForegroundChanged(uint bgra)
        {
            _foregroundColor = bgra;
            SyncBrushToStrokeEngine();
        }

        private void OnBackgroundChanged(uint bgra)
        {
            _backgroundColor = bgra;
        }

        private void SyncBrushFromToolState()
        {
            if (_toolState == null) return;

            var strokeSettings = GetStrokeSettingsForCurrentTool();
            if (strokeSettings != null)
            {
                _brushSize = strokeSettings.Size;
                _brushShape = strokeSettings.Shape;
                _brushOpacity = strokeSettings is IOpacitySettings os ? os.Opacity : (byte)255;
                _brushDensity = strokeSettings is IDensitySettings ds ? ds.Density : (byte)255;
            }
            else
            {
                _brushSize = _toolState.Brush.Size;
                _brushShape = _toolState.Brush.Shape;
                _brushOpacity = _toolState.Brush.Opacity;
                _brushDensity = _toolState.Brush.Density;
            }

            SyncBrushToStrokeEngine();
        }

        private void SyncBrushToStrokeEngine()
        {
            if (_stroke == null) return;

            uint merged = (_foregroundColor & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);
            _stroke.SetForeground(merged);
            _stroke.SetBrushSize(_brushSize);
            _stroke.SetBrushShape(_brushShape);
            _stroke.SetOpacity(_brushOpacity);
            _stroke.SetDensity(_brushDensity);
        }

        // ====================================================================
        // TOOL HELPERS
        // ====================================================================

        private IStrokeSettings? GetStrokeSettingsForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;

            var brushReg = ToolRegistry.Shared.GetById<BrushToolRegistration>(toolId);
            if (brushReg != null)
                return brushReg.StrokeSettings;

            var registration = ToolRegistry.Shared.GetById(toolId);
            return registration?.Settings as IStrokeSettings;
        }

        private IStrokePainter? GetPainterForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;

            var brushReg = ToolRegistry.Shared.GetById<BrushToolRegistration>(toolId);
            if (brushReg != null)
                return brushReg.CreatePainter();

            return null;
        }

        private IReadOnlyList<(int dx, int dy)> GetCurrentBrushOffsets()
        {
            var strokeSettings = GetStrokeSettingsForCurrentTool();
            if (strokeSettings != null)
            {
                if (strokeSettings is ICustomBrushSettings custom && custom.IsCustomBrushSelected)
                {
                    var brush = BrushDefinitionService.Instance.GetBrush(custom.CustomBrushFullName!);
                    if (brush != null)
                        return BrushMaskCache.Shared.GetOffsetsForCustomBrush(brush, strokeSettings.Size);
                }
                return BrushMaskCache.Shared.GetOffsets(strokeSettings.Shape, strokeSettings.Size);
            }
            return BrushMaskCache.Shared.GetOffsets(_brushShape, _brushSize);
        }

        private bool IsPaintingToolActive()
        {
            if (_toolState == null) return true;
            return _toolState.ActiveCategory == ToolCategory.Brush;
        }

        private bool IsFillToolActive()
        {
            return _toolState?.ActiveToolId == ToolIds.Fill;
        }

        private bool IsDropperToolActive()
        {
            return _toolState?.ActiveToolId == ToolIds.Dropper;
        }

        /// <summary>
        /// Determines if the current tool should show a filled brush ghost preview.
        /// Matches CanvasViewHost logic exactly.
        /// </summary>
        private bool ShouldShowFilledBrushGhost()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;
            // Only brush tool shows filled ghost, not eraser/blur/etc.
            return toolId == ToolIds.Brush;
        }

        /// <summary>
        /// Checks if the current tool has a custom brush selected.
        /// </summary>
        private bool IsCustomBrushSelected()
        {
            var strokeSettings = GetStrokeSettingsForCurrentTool();
            return strokeSettings is ICustomBrushSettings custom && custom.IsCustomBrushSelected;
        }

        /// <summary>
        /// Computes brush alpha at offset using the stroke engine (matches CanvasViewHost exactly).
        /// </summary>
        private byte ComputeBrushAlphaAtOffset(int dx, int dy)
        {
            if (_stroke == null) return 0;

            var strokeSettings = GetStrokeSettingsForCurrentTool();

            // Check for custom brush
            if (strokeSettings is ICustomBrushSettings customBrushSettings && customBrushSettings.IsCustomBrushSelected)
            {
                // Custom brushes use radial density-based falloff
                return ComputeCustomBrushAlpha(dx, dy, _brushSize, _brushOpacity, _brushDensity);
            }

            // Built-in shapes use the stroke engine's alpha computation
            return _stroke.ComputeBrushAlphaAtOffset(dx, dy);
        }

        /// <summary>
        /// Computes per-pixel alpha for custom brushes using radial density-based falloff.
        /// Matches CanvasViewHost.ComputeCustomBrushAlpha exactly.
        /// </summary>
        private static byte ComputeCustomBrushAlpha(int dx, int dy, int size, byte opacity, byte density)
        {
            if (opacity == 0) return 0;

            int sz = Math.Max(1, size);
            double Aop = opacity / 255.0;

            double r = sz / 2.0;

            // Use circular distance - offsets are already relative to brush center
            double d = Math.Sqrt((double)dx * dx + (double)dy * dy);

            // Apply density falloff based on distance
            double D = density / 255.0;
            double Rhard = r * D;

            // Full opacity within the hard radius
            if (d <= Rhard)
                return (byte)Math.Round(255.0 * Aop);

            // Falloff region beyond hard radius
            double span = Math.Max(1e-6, r - Rhard);
            double t = Math.Min(1.0, (d - Rhard) / span);
            double mask = 1.0 - (t * t) * (3 - 2 * t);
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }

        // ====================================================================
        // EVENT HANDLERS
        // ====================================================================

        private void OnCurrentFrameChanged(int frameIndex)
        {
            DispatcherQueue.TryEnqueue(RefreshDisplay);
        }

        private void OnSelectedReelChanged(TileAnimationReel? reel)
        {
            DispatcherQueue.TryEnqueue(RefreshDisplay);
        }

        private void OnDocumentModified()
        {
            // Skip refresh if we're actively painting (we're the source)
            if (_isPainting) return;

            DispatcherQueue.TryEnqueue(() => EditorCanvas?.Invalidate());
        }

        // ====================================================================
        // RENDERING
        // ====================================================================

        private void EditorCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            ds.Antialiasing = CanvasAntialiasing.Aliased;

            float canvasW = (float)sender.ActualWidth;
            float canvasH = (float)sender.ActualHeight;

            if (canvasW <= 0 || canvasH <= 0) return;

            ds.Clear(Color.FromArgb(255, 30, 30, 30));

            //if (_document == null || _tileX < 0 || _tileY < 0 || _tileWidth <= 0 || _tileHeight <= 0)
            //{
            //    EmptyStateText.Visibility = Visibility.Visible;
            //    return;
            //}

            //EmptyStateText.Visibility = Visibility.Collapsed;

            // Calculate layout
            float padding = 8f;
            float availableW = canvasW - padding * 2;
            float availableH = canvasH - padding * 2;

            if (availableW <= 0 || availableH <= 0) return;

            float baseScale = Math.Min(availableW / _tileWidth, availableH / _tileHeight);
            float scale = baseScale * (float)_zoomLevel;

            float destW = _tileWidth * scale;
            float destH = _tileHeight * scale;

            if (destW <= 0 || destH <= 0) return;

            // Apply pan offset to destination position
            float destX = (canvasW - destW) / 2 + (float)_panOffsetX;
            float destY = (canvasH - destH) / 2 + (float)_panOffsetY;

            var destRect = new Rect(destX, destY, destW, destH);

            // Draw pattern background
            DrawPatternBackground(sender, ds, destX, destY, destW, destH);

            // Draw onion skin layers (behind current frame)
            DrawOnionSkinLayers(sender, ds, destRect);

            // Read tile pixels directly from document composite surface
            byte[]? tilePixels = ReadTilePixelsFromDocument();
            if (tilePixels == null) return;

            // If hover is valid and brush tool (not painting), composite brush ghost into tile pixels
            bool showFilledGhost = _hoverValid && !_isPainting && IsPaintingToolActive() && ShouldShowFilledBrushGhost();
            if (showFilledGhost)
            {
                CompositeBrushGhostIntoBuffer(tilePixels);
            }

            // Draw tile pixels
            using var bitmap = CanvasBitmap.CreateFromBytes(
                sender.Device,
                tilePixels,
                _tileWidth,
                _tileHeight,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96.0f);

            ds.DrawImage(
                bitmap,
                destRect,
                new Rect(0, 0, _tileWidth, _tileHeight),
                1.0f,
                CanvasImageInterpolation.NearestNeighbor);

            // Draw pixel grid if zoomed in enough
            if (scale >= 4)
            {
                DrawPixelGrid(ds, destX, destY, destW, destH, scale);
            }

            // Draw brush cursor overlay (outline for non-brush tools, always show while hovering)
            bool showOutline = _hoverValid && IsPaintingToolActive() && !ShouldShowFilledBrushGhost();
            if (showOutline)
            {
                DrawBrushOutline(ds, destX, destY, scale, tilePixels);
            }
        }

        /// <summary>
        /// Draws onion skin layers for previous and next frames.
        /// </summary>
        private void DrawOnionSkinLayers(CanvasControl sender, CanvasDrawingSession ds, Rect destRect)
        {
            var reel = _state?.SelectedReel;
            if (reel == null || reel.Frames.Count <= 1) return;

            int currentIndex = _state!.CurrentFrameIndex;

            // Draw -2 frame (furthest back, drawn first so it's behind -1)
            if (_onionSkinPrev2)
            {
                DrawOnionFrame(sender, ds, destRect, currentIndex - 2, OnionOpacity2, OnionPrevTint);
            }

            // Draw -1 frame
            if (_onionSkinPrev1)
            {
                DrawOnionFrame(sender, ds, destRect, currentIndex - 1, OnionOpacity1, OnionPrevTint);
            }

            // Draw +2 frame (furthest forward, drawn first so it's behind +1)
            if (_onionSkinNext2)
            {
                DrawOnionFrame(sender, ds, destRect, currentIndex + 2, OnionOpacity2, OnionNextTint);
            }

            // Draw +1 frame
            if (_onionSkinNext1)
            {
                DrawOnionFrame(sender, ds, destRect, currentIndex + 1, OnionOpacity1, OnionNextTint);
            }
        }

        /// <summary>
        /// Draws a single onion skin frame at the specified offset with given opacity and tint.
        /// </summary>
        private void DrawOnionFrame(CanvasControl sender, CanvasDrawingSession ds, Rect destRect,
            int frameIndex, byte opacity, Color tint)
        {
            var reel = _state?.SelectedReel;
            if (reel == null) return;

            // Handle wrapping/bounds
            int frameCount = reel.Frames.Count;
            if (frameCount == 0) return;

            // Don't wrap - just skip out-of-bounds frames
            if (frameIndex < 0 || frameIndex >= frameCount) return;

            // Don't show current frame as onion
            if (frameIndex == _state!.CurrentFrameIndex) return;

            // Get the frame's tile position
            var frame = reel.Frames[frameIndex];
            int frameTileX = frame.TileX;
            int frameTileY = frame.TileY;

            // Read pixels for this frame's tile
            byte[]? framePixels = ReadTilePixelsAt(frameTileX, frameTileY);
            if (framePixels == null) return;

            // Apply opacity and tint to pixels
            byte[] tintedPixels = ApplyOnionTint(framePixels, opacity, tint);

            // Draw the onion frame
            using var bitmap = CanvasBitmap.CreateFromBytes(
                sender.Device,
                tintedPixels,
                _tileWidth,
                _tileHeight,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96.0f);

            ds.DrawImage(
                bitmap,
                destRect,
                new Rect(0, 0, _tileWidth, _tileHeight),
                1.0f,
                CanvasImageInterpolation.NearestNeighbor);
        }

        /// <summary>
        /// Applies opacity reduction and color tint to onion skin pixels.
        /// </summary>
        private byte[] ApplyOnionTint(byte[] sourcePixels, byte opacity, Color tint)
        {
            byte[] result = new byte[sourcePixels.Length];
            float opacityFactor = opacity / 255f;

            // Tint blend factor (subtle tinting)
            const float tintStrength = 0.3f;

            for (int i = 0; i < sourcePixels.Length; i += 4)
            {
                byte b = sourcePixels[i];
                byte g = sourcePixels[i + 1];
                byte r = sourcePixels[i + 2];
                byte a = sourcePixels[i + 3];

                // Skip fully transparent pixels
                if (a == 0)
                {
                    result[i] = 0;
                    result[i + 1] = 0;
                    result[i + 2] = 0;
                    result[i + 3] = 0;
                    continue;
                }

                // Apply tint (blend toward tint color)
                float tr = r + (tint.R - r) * tintStrength;
                float tg = g + (tint.G - g) * tintStrength;
                float tb = b + (tint.B - b) * tintStrength;

                // Apply opacity reduction
                float newA = a * opacityFactor;

                result[i] = (byte)Math.Clamp(tb, 0, 255);
                result[i + 1] = (byte)Math.Clamp(tg, 0, 255);
                result[i + 2] = (byte)Math.Clamp(tr, 0, 255);
                result[i + 3] = (byte)Math.Clamp(newA, 0, 255);
            }

            return result;
        }

        /// <summary>
        /// Reads tile pixels at a specific tile position (for onion skinning).
        /// </summary>
        private byte[]? ReadTilePixelsAt(int tileX, int tileY)
        {
            if (_document == null || _tileWidth <= 0 || _tileHeight <= 0) return null;

            int docX = tileX * _tileWidth;
            int docY = tileY * _tileHeight;

            if (docX < 0 || docY < 0 ||
                docX + _tileWidth > _document.PixelWidth ||
                docY + _tileHeight > _document.PixelHeight)
            {
                return null;
            }

            var surface = _document.Surface;
            if (surface == null) return null;

            byte[] pixels = new byte[_tileWidth * _tileHeight * 4];

            for (int y = 0; y < _tileHeight; y++)
            {
                for (int x = 0; x < _tileWidth; x++)
                {
                    int srcIdx = ((docY + y) * surface.Width + (docX + x)) * 4;
                    int dstIdx = (y * _tileWidth + x) * 4;

                    if (srcIdx + 3 < surface.Pixels.Length)
                    {
                        pixels[dstIdx + 0] = surface.Pixels[srcIdx + 0];
                        pixels[dstIdx + 1] = surface.Pixels[srcIdx + 1];
                        pixels[dstIdx + 2] = surface.Pixels[srcIdx + 2];
                        pixels[dstIdx + 3] = surface.Pixels[srcIdx + 3];
                    }
                }
            }

            return pixels;
        }

        private void DrawPatternBackground(CanvasControl sender, CanvasDrawingSession ds, float x, float y, float w, float h)
        {
            if (w <= 0 || h <= 0) return;

            double dpi = sender.XamlRoot?.RasterizationScale ?? 1.0;
            var img = _patternService.GetSizedImage(sender.Device, dpi, w, h);

            var target = new Rect(x, y, w, h);
            var src = new Rect(0, 0, img.SizeInPixels.Width, img.SizeInPixels.Height);

            ds.DrawImage(img, target, src);
        }

        private void DrawPixelGrid(CanvasDrawingSession ds, float x, float y, float w, float h, float scale)
        {
            var gridColor = Color.FromArgb(50, 255, 255, 255);

            for (int px = 0; px <= _tileWidth; px++)
            {
                float lx = x + px * scale;
                ds.DrawLine(lx, y, lx, y + h, gridColor, 1);
            }

            for (int py = 0; py <= _tileHeight; py++)
            {
                float ly = y + py * scale;
                ds.DrawLine(x, ly, x + w, ly, gridColor, 1);
            }
        }

        /// <summary>
        /// Composites the brush ghost (filled cursor preview for brush tool) into the tile buffer.
        /// Uses ComputeBrushAlphaAtOffset to exactly match CanvasViewHost behavior.
        /// </summary>
        private void CompositeBrushGhostIntoBuffer(byte[] tilePixels)
        {
            int docX = _tileX * _tileWidth;
            int docY = _tileY * _tileHeight;

            // Convert hover doc coords to tile-local
            int localX = _hoverDocX - docX;
            int localY = _hoverDocY - docY;

            var offsets = GetCurrentBrushOffsets();

            foreach (var (dx, dy) in offsets)
            {
                int px = localX + dx;
                int py = localY + dy;

                if (px < 0 || px >= _tileWidth || py < 0 || py >= _tileHeight)
                    continue;

                // Use ComputeBrushAlphaAtOffset to exactly match CanvasViewHost
                byte effA = ComputeBrushAlphaAtOffset(dx, dy);
                if (effA == 0) continue;

                int idx = (py * _tileWidth + px) * 4;

                // Blend foreground color with computed alpha over existing pixel
                uint before = (uint)(tilePixels[idx] | (tilePixels[idx + 1] << 8) |
                                    (tilePixels[idx + 2] << 16) | (tilePixels[idx + 3] << 24));
                uint srcWithAlpha = (_foregroundColor & 0x00FFFFFFu) | ((uint)effA << 24);
                uint after = ColorUtil.BlendOver(before, srcWithAlpha);

                tilePixels[idx + 0] = (byte)(after & 0xFF);
                tilePixels[idx + 1] = (byte)((after >> 8) & 0xFF);
                tilePixels[idx + 2] = (byte)((after >> 16) & 0xFF);
                tilePixels[idx + 3] = (byte)((after >> 24) & 0xFF);
            }
        }

        /// <summary>
        /// Draws the brush outline overlay for non-brush tools.
        /// Uses contrasting color based on pixels under the cursor, matching CanvasViewHost exactly.
        /// </summary>
        private void DrawBrushOutline(CanvasDrawingSession ds, float destX, float destY, float scale, byte[] tilePixels)
        {
            int docX = _tileX * _tileWidth;
            int docY = _tileY * _tileHeight;

            // Convert hover doc coords to tile-local
            int localX = _hoverDocX - docX;
            int localY = _hoverDocY - docY;

            var offsets = GetCurrentBrushOffsets();
            if (offsets.Count == 0) return;

            // Build grid for edge detection (matches CanvasViewHost.DrawBrushOutline)
            StrokeUtil.BuildMaskGrid(offsets, out var grid, out int minDx, out int minDy, out int w, out int h);

            int localX0 = localX + minDx;
            int localY0 = localY + minDy;

            float sf = scale;
            float thick = 1f;

            // Horizontal edges
            for (int y = 0; y <= h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    bool edge = StrokeUtil.Occup(grid, w, h, x, y) ^ StrokeUtil.Occup(grid, w, h, x, y - 1);
                    if (!edge) { x++; continue; }

                    bool insideBelow = StrokeUtil.Occup(grid, w, h, x, y);
                    int outLocalY = localY0 + (insideBelow ? y - 1 : y);
                    int sampleLocalX = localX0 + Math.Clamp(x, 0, w - 1);
                    Color ink = SampleInkAtLocal(sampleLocalX, outLocalY, tilePixels);

                    int x0 = x++;
                    while (x < w)
                    {
                        bool e2 = StrokeUtil.Occup(grid, w, h, x, y) ^ StrokeUtil.Occup(grid, w, h, x, y - 1);
                        if (!e2) break;

                        bool insideBelow2 = StrokeUtil.Occup(grid, w, h, x, y);
                        int outLocalY2 = localY0 + (insideBelow2 ? y - 1 : y);
                        int sampleLocalX2 = localX0 + Math.Clamp(x, 0, w - 1);
                        Color ink2 = SampleInkAtLocal(sampleLocalX2, outLocalY2, tilePixels);
                        if (!ink2.Equals(ink)) break;

                        x++;
                    }

                    float sx0 = destX + (localX0 + x0) * sf;
                    float sx1 = destX + (localX0 + x) * sf;
                    float sy = destY + (localY0 + y) * sf;
                    ds.DrawLine(sx0, sy, sx1, sy, ink, thick);
                }
            }

            // Vertical edges
            for (int x = 0; x <= w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    bool edge = StrokeUtil.Occup(grid, w, h, x, y) ^ StrokeUtil.Occup(grid, w, h, x - 1, y);
                    if (!edge) { y++; continue; }

                    bool insideRight = StrokeUtil.Occup(grid, w, h, x, y);
                    int outLocalX = localX0 + (insideRight ? x - 1 : x);
                    int sampleLocalY = localY0 + Math.Clamp(y, 0, h - 1);
                    Color ink = SampleInkAtLocal(outLocalX, sampleLocalY, tilePixels);

                    int y0 = y++;
                    while (y < h)
                    {
                        bool e2 = StrokeUtil.Occup(grid, w, h, x, y) ^ StrokeUtil.Occup(grid, w, h, x - 1, y);
                        if (!e2) break;

                        bool insideRight2 = StrokeUtil.Occup(grid, w, h, x, y);
                        int outLocalX2 = localX0 + (insideRight2 ? x - 1 : x);
                        int sampleLocalY2 = localY0 + Math.Clamp(y, 0, h - 1);
                        Color ink2 = SampleInkAtLocal(outLocalX2, sampleLocalY2, tilePixels);
                        if (!ink2.Equals(ink)) break;

                        y++;
                    }

                    float sy0 = destY + (localY0 + y0) * sf;
                    float sy1 = destY + (localY0 + y) * sf;
                    float sx = destX + (localX0 + x) * sf;
                    ds.DrawLine(sx, sy0, sx, sy1, ink, thick);
                }
            }
        }

        /// <summary>
        /// Samples a contrasting ink color for outline drawing at a tile-local position.
        /// Matches CanvasViewHost.SampleInkAtDoc behavior.
        /// </summary>
        private Color SampleInkAtLocal(int localX, int localY, byte[] tilePixels)
        {
            // Clamp to tile bounds
            localX = Math.Clamp(localX, 0, _tileWidth - 1);
            localY = Math.Clamp(localY, 0, _tileHeight - 1);

            int idx = (localY * _tileWidth + localX) * 4;
            if (idx < 0 || idx + 3 >= tilePixels.Length)
                return Colors.White;

            byte b = tilePixels[idx];
            byte g = tilePixels[idx + 1];
            byte r = tilePixels[idx + 2];
            byte a = tilePixels[idx + 3];

            // Composite over white to get effective color (matches ColorUtil.CompositeOverWhite)
            uint bgra = (uint)(b | (g << 8) | (r << 16) | (a << 24));
            uint onWhite = ColorUtil.CompositeOverWhite(bgra);

            // Get high contrast ink (matches ColorUtil.HighContrastInk)
            return ColorUtil.HighContrastInk(onWhite);
        }

        private byte[]? ReadTilePixelsFromDocument()
        {
            if (_document == null || _tileX < 0 || _tileY < 0) return null;

            int docX = _tileX * _tileWidth;
            int docY = _tileY * _tileHeight;

            if (docX < 0 || docY < 0 ||
                docX + _tileWidth > _document.PixelWidth ||
                docY + _tileHeight > _document.PixelHeight)
            {
                return null;
            }

            var surface = _document.Surface;
            if (surface == null) return null;

            byte[] pixels = new byte[_tileWidth * _tileHeight * 4];

            for (int y = 0; y < _tileHeight; y++)
            {
                for (int x = 0; x < _tileWidth; x++)
                {
                    int srcIdx = ((docY + y) * surface.Width + (docX + x)) * 4;
                    int dstIdx = (y * _tileWidth + x) * 4;

                    if (srcIdx + 3 < surface.Pixels.Length)
                    {
                        pixels[dstIdx + 0] = surface.Pixels[srcIdx + 0];
                        pixels[dstIdx + 1] = surface.Pixels[srcIdx + 1];
                        pixels[dstIdx + 2] = surface.Pixels[srcIdx + 2];
                        pixels[dstIdx + 3] = surface.Pixels[srcIdx + 3];
                    }
                }
            }

            return pixels;
        }

        // ====================================================================
        // INPUT HANDLING
        // ====================================================================

        private void EditorCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_document == null || _stroke == null || _tileX < 0 || _tileY < 0)
            {
                e.Handled = true;
                return;
            }

            var point = e.GetCurrentPoint(EditorCanvas);
            var props = point.Properties;
            var (docX, docY) = ScreenToDocument(point.Position);

            // MMB or space+LMB = pan
            if (props.IsMiddleButtonPressed || (_spacePan && props.IsLeftButtonPressed))
            {
                _isPanning = true;
                _panStartPoint = point.Position;
                _panStartOffsetX = _panOffsetX;
                _panStartOffsetY = _panOffsetY;
                EditorCanvas.CapturePointer(e.Pointer);
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
                e.Handled = true;
                return;
            }

            // RMB = dropper (quick sample)
            if (props.IsRightButtonPressed)
            {
                SampleColorAt(docX, docY);
                e.Handled = true;
                return;
            }

            if (!props.IsLeftButtonPressed)
            {
                e.Handled = true;
                return;
            }

            // Dropper tool
            if (IsDropperToolActive())
            {
                SampleColorAt(docX, docY);
                e.Handled = true;
                return;
            }

            // Fill tool
            if (IsFillToolActive())
            {
                HandleFillAt(docX, docY);
                e.Handled = true;
                return;
            }

            // Painting tools
            if (!IsPaintingToolActive())
            {
                e.Handled = true;
                return;
            }

            if (_document.ActiveLayer is not RasterLayer rl)
            {
                e.Handled = true;
                return;
            }

            // Check if within tile bounds
            int tileDocX = _tileX * _tileWidth;
            int tileDocY = _tileY * _tileHeight;
            if (docX < tileDocX || docX >= tileDocX + _tileWidth ||
                docY < tileDocY || docY >= tileDocY + _tileHeight)
            {
                e.Handled = true;
                return;
            }

            // Capture before state
            CaptureBeforeState();

            // Begin stroke
            _isPainting = true;
            _hasLastPos = true;
            _lastDocX = docX;
            _lastDocY = docY;

            _activePainter = GetPainterForCurrentTool();
            if (_activePainter != null)
            {
                _stroke.BeginWithPainter(_activePainter, rl);
            }

            // Initial stamp
            var strokeSettings = GetStrokeSettingsForCurrentTool();
            if (strokeSettings != null && _activePainter != null)
            {
                uint fg = (_foregroundColor & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);
                _stroke.StampAtWithPainter(docX, docY, fg, _backgroundColor, strokeSettings);
            }

            EditorCanvas.CapturePointer(e.Pointer);

            // Live update
            _document.CompositeTo(_document.Surface);
            _document.RaiseDocumentModified();
            EditorCanvas.Invalidate();

            e.Handled = true;
        }

        private void EditorCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_document == null || _tileX < 0 || _tileY < 0)
            {
                e.Handled = true;
                return;
            }

            var point = e.GetCurrentPoint(EditorCanvas);
            var props = point.Properties;

            // Handle panning
            if (_isPanning)
            {
                double deltaX = point.Position.X - _panStartPoint.X;
                double deltaY = point.Position.Y - _panStartPoint.Y;
                _panOffsetX = _panStartOffsetX + deltaX;
                _panOffsetY = _panStartOffsetY + deltaY;
                EditorCanvas?.Invalidate();
                e.Handled = true;
                return;
            }

            var (docX, docY) = ScreenToDocument(point.Position);

            // Update hover state
            int tileDocX = _tileX * _tileWidth;
            int tileDocY = _tileY * _tileHeight;
            _hoverDocX = docX;
            _hoverDocY = docY;
            _hoverValid = docX >= tileDocX && docX < tileDocX + _tileWidth &&
                          docY >= tileDocY && docY < tileDocY + _tileHeight;

            if (_isPainting && _stroke != null && props.IsLeftButtonPressed)
            {
                if (_hoverValid && _document.ActiveLayer is RasterLayer)
                {
                    var strokeSettings = GetStrokeSettingsForCurrentTool();
                    if (strokeSettings != null && _activePainter != null)
                    {
                        uint fg = (_foregroundColor & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);

                        if (_hasLastPos && (_lastDocX != docX || _lastDocY != docY))
                        {
                            _stroke.StampLineWithPainter(_lastDocX, _lastDocY, docX, docY, fg, _backgroundColor, strokeSettings);
                        }
                        else
                        {
                            _stroke.StampAtWithPainter(docX, docY, fg, _backgroundColor, strokeSettings);
                        }

                        _lastDocX = docX;
                        _lastDocY = docY;
                        _hasLastPos = true;

                        // Live update
                        _document.CompositeTo(_document.Surface);
                        _document.RaiseDocumentModified();
                    }
                }
            }

            EditorCanvas?.Invalidate();
            e.Handled = true;
        }

        private void EditorCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // Handle pan release
            if (_isPanning)
            {
                _isPanning = false;
                EditorCanvas.ReleasePointerCapture(e.Pointer);
                ProtectedCursor = null;
                e.Handled = true;
                return;
            }

            if (_isPainting)
            {
                _isPainting = false;
                _hasLastPos = false;
                _lastDocX = -1;
                _lastDocY = -1;

                // Commit stroke
                if (_activePainter != null && _stroke?.HasActivePainterStroke == true)
                {
                    _stroke.CommitPainter("Brush Stroke");
                    _activePainter = null;
                }

                // Push to history
                PushStrokeToHistory();

                // Final update
                if (_document != null)
                {
                    _document.CompositeTo(_document.Surface);
                    _document.RaiseDocumentModified();
                }

                TileModified?.Invoke();
            }

            EditorCanvas.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        private void EditorCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _hoverValid = false;
            _hoverDocX = -1;
            _hoverDocY = -1;
            EditorCanvas?.Invalidate();
        }

        private void EditorCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(EditorCanvas);
            var delta = point.Properties.MouseWheelDelta;

            // Ctrl+Wheel = brush size
            if (IsKeyDown(Windows.System.VirtualKey.Control))
            {
                int step = delta > 0 ? 1 : -1;
                _toolState?.UpdateBrush(b => b.Size = Math.Clamp(b.Size + step, 1, 64));
                e.Handled = true;
                return;
            }

            // Get zoom pivot point (mouse position)
            double mouseX = point.Position.X;
            double mouseY = point.Position.Y;

            // Calculate current canvas center
            float canvasW = (float)EditorCanvas.ActualWidth;
            float canvasH = (float)EditorCanvas.ActualHeight;
            float padding = 8f;
            float availableW = canvasW - padding * 2;
            float availableH = canvasH - padding * 2;
            float baseScale = Math.Min(availableW / _tileWidth, availableH / _tileHeight);

            double oldZoom = _zoomLevel;

            // Normal wheel = zoom
            if (delta > 0)
            {
                _zoomLevel = Math.Min(MaxZoom, _zoomLevel * ZoomStep);
            }
            else if (delta < 0)
            {
                _zoomLevel = Math.Max(MinZoom, _zoomLevel / ZoomStep);
            }

            // Adjust pan offset to zoom toward mouse position
            double zoomRatio = _zoomLevel / oldZoom;
            double centerX = canvasW / 2 + _panOffsetX;
            double centerY = canvasH / 2 + _panOffsetY;

            // New center after zoom
            double newCenterX = mouseX + (centerX - mouseX) * zoomRatio;
            double newCenterY = mouseY + (centerY - mouseY) * zoomRatio;

            _panOffsetX = newCenterX - canvasW / 2;
            _panOffsetY = newCenterY - canvasH / 2;

            EditorCanvas.Invalidate();
            e.Handled = true;
        }

        private static bool IsKeyDown(Windows.System.VirtualKey k)
        {
            var st = InputKeyboardSource.GetKeyStateForCurrentThread(k);
            return st.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        }

        private void EditorCanvas_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Space && !_spacePan)
            {
                _spacePan = true;
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
                e.Handled = true;
            }
        }

        private void EditorCanvas_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Space)
            {
                _spacePan = false;
                if (!_isPanning)
                {
                    ProtectedCursor = null;
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// Converts screen position to document coordinates.
        /// </summary>
        private (int docX, int docY) ScreenToDocument(Point screenPos)
        {
            if (_tileWidth <= 0 || _tileHeight <= 0) return (-1, -1);

            float canvasW = (float)EditorCanvas.ActualWidth;
            float canvasH = (float)EditorCanvas.ActualHeight;

            if (canvasW <= 0 || canvasH <= 0) return (-1, -1);

            float padding = 8f;
            float availableW = canvasW - padding * 2;
            float availableH = canvasH - padding * 2;
            float baseScale = Math.Min(availableW / _tileWidth, availableH / _tileHeight);
            float scale = baseScale * (float)_zoomLevel;

            float destW = _tileWidth * scale;
            float destH = _tileHeight * scale;
            
            // Account for pan offset
            float destX = (canvasW - destW) / 2 + (float)_panOffsetX;
            float destY = (canvasH - destH) / 2 + (float)_panOffsetY;

            // Convert to tile-local coordinates
            int localX = (int)Math.Floor((screenPos.X - destX) / scale);
            int localY = (int)Math.Floor((screenPos.Y - destY) / scale);

            // Convert to document coordinates
            int docX = _tileX * _tileWidth + localX;
            int docY = _tileY * _tileHeight + localY;

            return (docX, docY);
        }

        private void UpdateTilePosition()
        {
            if (_state == null || _document == null)
            {
                _tileX = -1;
                _tileY = -1;
                _tileWidth = 0;
                _tileHeight = 0;
                return;
            }

            var (tx, ty) = _state.CurrentTilePosition;
            
            // Reset pan when tile position changes
            if (_tileX != tx || _tileY != ty)
            {
                _panOffsetX = 0;
                _panOffsetY = 0;
            }
            
            _tileX = tx;
            _tileY = ty;
            _tileWidth = _document.TileSize.Width;
            _tileHeight = _document.TileSize.Height;

            // Reset stroke engine target when document changes
            if (_stroke != null && _document.TargetSurface != null)
            {
                _stroke = new StrokeEngine(_document.TargetSurface);
                SyncBrushToStrokeEngine();
            }
        }

        // ====================================================================
        // TOOL OPERATIONS
        // ====================================================================

        private void SampleColorAt(int docX, int docY)
        {
            if (_document == null || _palette == null) return;

            if (docX < 0 || docX >= _document.PixelWidth || docY < 0 || docY >= _document.PixelHeight)
                return;

            var surface = _document.Surface;
            int idx = (docY * surface.Width + docX) * 4;
            if (idx < 0 || idx + 3 >= surface.Pixels.Length) return;

            byte b = surface.Pixels[idx];
            byte g = surface.Pixels[idx + 1];
            byte r = surface.Pixels[idx + 2];
            byte a = surface.Pixels[idx + 3];

            uint color = (uint)(b | (g << 8) | (r << 16) | (a << 24));
            _palette.SetForeground(color);
        }

        private void HandleFillAt(int docX, int docY)
        {
            if (_document?.ActiveLayer is not RasterLayer rl) return;

            CaptureBeforeState();

            var fillReg = ToolRegistry.Shared.GetById<FillToolRegistration>(ToolIds.Fill);
            if (fillReg == null) return;

            var context = new FillContext
            {
                Surface = rl.Surface,
                Color = _foregroundColor,
                Tolerance = _toolState?.FillTolerance ?? 0,
                Contiguous = _toolState?.FillContiguous ?? true,
                Description = "Fill"
            };

            var result = fillReg.EffectiveFillPainter.FillAt(rl, docX, docY, context);

            if (result is { CanPushToHistory: true } and IHistoryItem historyItem)
            {
                _document.History.Push(historyItem);
            }

            _document.CompositeTo(_document.Surface);
            _document.RaiseDocumentModified();
            EditorCanvas?.Invalidate();

            TileModified?.Invoke();
        }

        // ====================================================================
        // HISTORY
        // ====================================================================

        private void CaptureBeforeState()
        {
            if (_document?.ActiveLayer is not RasterLayer rl) return;

            int docX = _tileX * _tileWidth;
            int docY = _tileY * _tileHeight;

            _strokeBeforePixels = new byte[_tileWidth * _tileHeight * 4];
            var surface = rl.Surface;

            for (int y = 0; y < _tileHeight; y++)
            {
                for (int x = 0; x < _tileWidth; x++)
                {
                    int srcIdx = ((docY + y) * surface.Width + (docX + x)) * 4;
                    int dstIdx = (y * _tileWidth + x) * 4;

                    if (srcIdx + 3 < surface.Pixels.Length)
                    {
                        _strokeBeforePixels[dstIdx + 0] = surface.Pixels[srcIdx + 0];
                        _strokeBeforePixels[dstIdx + 1] = surface.Pixels[srcIdx + 1];
                        _strokeBeforePixels[dstIdx + 2] = surface.Pixels[srcIdx + 2];
                        _strokeBeforePixels[dstIdx + 3] = surface.Pixels[srcIdx + 3];
                    }
                }
            }
        }

        private void PushStrokeToHistory()
        {
            if (_document?.ActiveLayer is not RasterLayer rl || _strokeBeforePixels == null)
                return;

            int docX = _tileX * _tileWidth;
            int docY = _tileY * _tileHeight;

            // Get after state
            byte[] afterPixels = new byte[_tileWidth * _tileHeight * 4];
            var surface = rl.Surface;

            for (int y = 0; y < _tileHeight; y++)
            {
                for (int x = 0; x < _tileWidth; x++)
                {
                    int srcIdx = ((docY + y) * surface.Width + (docX + x)) * 4;
                    int dstIdx = (y * _tileWidth + x) * 4;

                    if (srcIdx + 3 < surface.Pixels.Length)
                    {
                        afterPixels[dstIdx + 0] = surface.Pixels[srcIdx + 0];
                        afterPixels[dstIdx + 1] = surface.Pixels[srcIdx + 1];
                        afterPixels[dstIdx + 2] = surface.Pixels[srcIdx + 2];
                        afterPixels[dstIdx + 3] = surface.Pixels[srcIdx + 3];
                    }
                }
            }

            // Check for changes
            bool hasChanges = false;
            for (int i = 0; i < _strokeBeforePixels.Length; i++)
            {
                if (_strokeBeforePixels[i] != afterPixels[i])
                {
                    hasChanges = true;
                    break;
                }
            }

            if (!hasChanges)
            {
                _strokeBeforePixels = null;
                return;
            }

            var bounds = new Windows.Graphics.RectInt32(docX, docY, _tileWidth, _tileHeight);
            var historyItem = PixelChangeItem.FromRegion(rl, bounds, _strokeBeforePixels, afterPixels, "Brush Stroke");

            if (!historyItem.IsEmpty)
            {
                _document.History.Push(historyItem);
            }

            _strokeBeforePixels = null;
        }
    }
}
