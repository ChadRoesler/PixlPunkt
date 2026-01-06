using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Animation;
using PixlPunkt.Uno.Core.Brush;
using PixlPunkt.Uno.Core.Coloring.Helpers;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.History;
using PixlPunkt.Uno.Core.Painting;
using PixlPunkt.Uno.Core.Painting.Helpers;
using PixlPunkt.Uno.Core.Palette;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.Uno.UI.CanvasHost;
using PixlPunkt.Uno.UI.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;
using Windows.UI;
using static PixlPunkt.Uno.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Uno.UI.Animation
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

        // Cached checkerboard pattern for SkiaSharp
        private SKShader? _checkerboardShader;
        private SKBitmap? _checkerboardBitmap;

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

        private bool _onionSkinPrev1 = false;
        private bool _onionSkinPrev2 = false;
        private bool _onionSkinNext1 = false;
        private bool _onionSkinNext2 = false;

        private const byte OnionOpacity1 = 128;
        private const byte OnionOpacity2 = 64;

        private static readonly Color OnionPrevTint = Color.FromArgb(255, 255, 100, 100);
        private static readonly Color OnionNextTint = Color.FromArgb(255, 100, 100, 255);

        // ====================================================================
        // EVENTS
        // ====================================================================

        public event Action? TileModified;
        public event Action? OnionSkinChanged;

        // ====================================================================
        // PROPERTIES - ONION SKINNING
        // ====================================================================

        public bool OnionSkinPrev1
        {
            get => _onionSkinPrev1;
            set { if (_onionSkinPrev1 != value) { _onionSkinPrev1 = value; OnionSkinChanged?.Invoke(); EditorCanvas?.Invalidate(); } }
        }

        public bool OnionSkinPrev2
        {
            get => _onionSkinPrev2;
            set { if (_onionSkinPrev2 != value) { _onionSkinPrev2 = value; OnionSkinChanged?.Invoke(); EditorCanvas?.Invalidate(); } }
        }

        public bool OnionSkinNext1
        {
            get => _onionSkinNext1;
            set { if (_onionSkinNext1 != value) { _onionSkinNext1 = value; OnionSkinChanged?.Invoke(); EditorCanvas?.Invalidate(); } }
        }

        public bool OnionSkinNext2
        {
            get => _onionSkinNext2;
            set { if (_onionSkinNext2 != value) { _onionSkinNext2 = value; OnionSkinChanged?.Invoke(); EditorCanvas?.Invalidate(); } }
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
            _checkerboardShader?.Dispose();
            _checkerboardBitmap?.Dispose();
        }

        private void OnStripeColorsChanged()
        {
            ApplyStripeColors();
            _patternService.Invalidate();
            InvalidateCheckerboardCache();
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

        private void InvalidateCheckerboardCache()
        {
            _checkerboardShader?.Dispose();
            _checkerboardShader = null;
            _checkerboardBitmap?.Dispose();
            _checkerboardBitmap = null;
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        public void Bind(TileAnimationState? state, CanvasDocument? document)
        {
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

            if (_document != null)
            {
                _stroke = new StrokeEngine(_document.TargetSurface);
                SyncBrushToStrokeEngine();
            }

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

        public void BindCanvasHost(CanvasViewHost? canvasHost) => _canvasHost = canvasHost;

        public void BindToolState(ToolState? toolState, PaletteService? palette)
        {
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

        public void SetForegroundColor(uint bgra) => _foregroundColor = bgra;
        public void SetBackgroundColor(uint bgra) => _backgroundColor = bgra;

        public void RefreshDisplay()
        {
            UpdateTilePosition();
            EditorCanvas?.Invalidate();
        }

        // ====================================================================
        // TOOL/BRUSH STATE SYNC (unchanged)
        // ====================================================================

        private void OnToolChanged(string toolId) { SyncBrushFromToolState(); EditorCanvas?.Invalidate(); }
        private void OnToolOptionsChanged() { SyncBrushFromToolState(); EditorCanvas?.Invalidate(); }
        private void OnBrushChanged(BrushSettings s) { SyncBrushFromToolState(); EditorCanvas?.Invalidate(); }
        private void OnForegroundChanged(uint bgra) { _foregroundColor = bgra; SyncBrushToStrokeEngine(); }
        private void OnBackgroundChanged(uint bgra) { _backgroundColor = bgra; }

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
        // TOOL HELPERS (unchanged)
        // ====================================================================

        private IStrokeSettings? GetStrokeSettingsForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;
            var brushReg = ToolRegistry.Shared.GetById<BrushToolRegistration>(toolId);
            if (brushReg != null) return brushReg.StrokeSettings;
            var registration = ToolRegistry.Shared.GetById(toolId);
            return registration?.Settings as IStrokeSettings;
        }

        private IStrokePainter? GetPainterForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;
            var brushReg = ToolRegistry.Shared.GetById<BrushToolRegistration>(toolId);
            return brushReg?.CreatePainter();
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

        private bool IsPaintingToolActive() => _toolState == null || _toolState.ActiveCategory == ToolCategory.Brush;
        private bool IsFillToolActive() => _toolState?.ActiveToolId == ToolIds.Fill;
        private bool IsDropperToolActive() => _toolState?.ActiveToolId == ToolIds.Dropper;
        private bool ShouldShowFilledBrushGhost() => (_toolState?.ActiveToolId ?? ToolIds.Brush) == ToolIds.Brush;

        private byte ComputeBrushAlphaAtOffset(int dx, int dy)
        {
            if (_stroke == null) return 0;
            var strokeSettings = GetStrokeSettingsForCurrentTool();
            if (strokeSettings is ICustomBrushSettings custom && custom.IsCustomBrushSelected)
                return ComputeCustomBrushAlpha(dx, dy, _brushSize, _brushOpacity, _brushDensity);
            return _stroke.ComputeBrushAlphaAtOffset(dx, dy);
        }

        private static byte ComputeCustomBrushAlpha(int dx, int dy, int size, byte opacity, byte density)
        {
            if (opacity == 0) return 0;
            int sz = Math.Max(1, size);
            double Aop = opacity / 255.0;
            double r = sz / 2.0;
            double d = Math.Sqrt((double)dx * dx + (double)dy * dy);
            double D = density / 255.0;
            double Rhard = r * D;
            if (d <= Rhard) return (byte)Math.Round(255.0 * Aop);
            double span = Math.Max(1e-6, r - Rhard);
            double t = Math.Min(1.0, (d - Rhard) / span);
            double mask = 1.0 - (t * t) * (3 - 2 * t);
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }

        // ====================================================================
        // EVENT HANDLERS
        // ====================================================================

        private void OnCurrentFrameChanged(int frameIndex) => DispatcherQueue.TryEnqueue(RefreshDisplay);
        private void OnSelectedReelChanged(TileAnimationReel? reel) => DispatcherQueue.TryEnqueue(RefreshDisplay);

        private void OnDocumentModified()
        {
            if (_isPainting) return;
            DispatcherQueue.TryEnqueue(() => EditorCanvas?.Invalidate());
        }

        // ====================================================================
        // RENDERING (SKIASHARP)
        // ====================================================================

        private void EditorCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;

            float canvasW = (float)EditorCanvas.ActualWidth;
            float canvasH = (float)EditorCanvas.ActualHeight;

            if (canvasW <= 0 || canvasH <= 0) return;

            canvas.Clear(new SKColor(30, 30, 30));

            if (_document == null || _tileX < 0 || _tileY < 0 || _tileWidth <= 0 || _tileHeight <= 0)
                return;

            float padding = 8f;
            float availableW = canvasW - padding * 2;
            float availableH = canvasH - padding * 2;

            if (availableW <= 0 || availableH <= 0) return;

            float baseScale = Math.Min(availableW / _tileWidth, availableH / _tileHeight);
            float scale = baseScale * (float)_zoomLevel;

            float destW = _tileWidth * scale;
            float destH = _tileHeight * scale;

            if (destW <= 0 || destH <= 0) return;

            float destX = (canvasW - destW) / 2 + (float)_panOffsetX;
            float destY = (canvasH - destH) / 2 + (float)_panOffsetY;

            var destRect = new SKRect(destX, destY, destX + destW, destY + destH);

            // Draw pattern background
            DrawPatternBackground(canvas, destRect);

            // Draw onion skin layers
            DrawOnionSkinLayers(canvas, destRect);

            // Read tile pixels
            byte[]? tilePixels = ReadTilePixelsFromDocument();
            if (tilePixels == null) return;

            // Composite brush ghost if hovering with brush tool
            bool showFilledGhost = _hoverValid && !_isPainting && IsPaintingToolActive() && ShouldShowFilledBrushGhost();
            if (showFilledGhost)
                CompositeBrushGhostIntoBuffer(tilePixels);

            // Draw tile pixels
            using var bitmap = new SKBitmap(_tileWidth, _tileHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            System.Runtime.InteropServices.Marshal.Copy(tilePixels, 0, bitmap.GetPixels(), tilePixels.Length);

            using var paint = new SKPaint { FilterQuality = SKFilterQuality.None, IsAntialias = false };
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, _tileWidth, _tileHeight), destRect, paint);

            // Draw pixel grid if zoomed in
            if (scale >= 4)
                DrawPixelGrid(canvas, destX, destY, destW, destH, scale);

            // Draw brush outline for non-brush tools
            bool showOutline = _hoverValid && IsPaintingToolActive() && !ShouldShowFilledBrushGhost();
            if (showOutline)
                DrawBrushOutline(canvas, destX, destY, scale, tilePixels);
        }

        private void DrawPatternBackground(SKCanvas canvas, SKRect destRect)
        {
            var (lightColor, darkColor) = _patternService.CurrentScheme;

            int squareSize = 4;
            EnsureCheckerboardShader(squareSize, lightColor, darkColor);

            if (_checkerboardShader != null)
            {
                using var paint = new SKPaint { Shader = _checkerboardShader, IsAntialias = false };
                canvas.DrawRect(destRect, paint);
            }
            else
            {
                canvas.DrawRect(destRect, new SKPaint { Color = new SKColor(lightColor.R, lightColor.G, lightColor.B) });
            }
        }

        private void EnsureCheckerboardShader(int squareSize, Color lightColor, Color darkColor)
        {
            if (_checkerboardBitmap != null && _checkerboardShader != null) return;

            _checkerboardShader?.Dispose();
            _checkerboardBitmap?.Dispose();

            int tileSize = squareSize * 2;
            _checkerboardBitmap = new SKBitmap(tileSize, tileSize, SKColorType.Bgra8888, SKAlphaType.Premul);

            var skLight = new SKColor(lightColor.R, lightColor.G, lightColor.B, lightColor.A);
            var skDark = new SKColor(darkColor.R, darkColor.G, darkColor.B, darkColor.A);

            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    int cx = x / squareSize;
                    int cy = y / squareSize;
                    bool isLight = ((cx + cy) & 1) == 0;
                    _checkerboardBitmap.SetPixel(x, y, isLight ? skLight : skDark);
                }
            }

            using var image = SKImage.FromBitmap(_checkerboardBitmap);
            _checkerboardShader = image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        }

        private void DrawOnionSkinLayers(SKCanvas canvas, SKRect destRect)
        {
            var reel = _state?.SelectedReel;
            if (reel == null || reel.Frames.Count <= 1) return;

            int currentIndex = _state!.CurrentFrameIndex;

            if (_onionSkinPrev2) DrawOnionFrame(canvas, destRect, currentIndex - 2, OnionOpacity2, OnionPrevTint);
            if (_onionSkinPrev1) DrawOnionFrame(canvas, destRect, currentIndex - 1, OnionOpacity1, OnionPrevTint);
            if (_onionSkinNext2) DrawOnionFrame(canvas, destRect, currentIndex + 2, OnionOpacity2, OnionNextTint);
            if (_onionSkinNext1) DrawOnionFrame(canvas, destRect, currentIndex + 1, OnionOpacity1, OnionNextTint);
        }

        private void DrawOnionFrame(SKCanvas canvas, SKRect destRect, int frameIndex, byte opacity, Color tint)
        {
            var reel = _state?.SelectedReel;
            if (reel == null) return;

            int frameCount = reel.Frames.Count;
            if (frameCount == 0 || frameIndex < 0 || frameIndex >= frameCount) return;
            if (frameIndex == _state!.CurrentFrameIndex) return;

            var frame = reel.Frames[frameIndex];
            byte[]? framePixels = ReadTilePixelsAt(frame.TileX, frame.TileY);
            if (framePixels == null) return;

            byte[] tintedPixels = ApplyOnionTint(framePixels, opacity, tint);

            using var bitmap = new SKBitmap(_tileWidth, _tileHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            System.Runtime.InteropServices.Marshal.Copy(tintedPixels, 0, bitmap.GetPixels(), tintedPixels.Length);

            using var paint = new SKPaint { FilterQuality = SKFilterQuality.None, IsAntialias = false };
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, _tileWidth, _tileHeight), destRect, paint);
        }

        private byte[] ApplyOnionTint(byte[] sourcePixels, byte opacity, Color tint)
        {
            byte[] result = new byte[sourcePixels.Length];
            float opacityFactor = opacity / 255f;
            const float tintStrength = 0.3f;

            for (int i = 0; i < sourcePixels.Length; i += 4)
            {
                byte b = sourcePixels[i], g = sourcePixels[i + 1], r = sourcePixels[i + 2], a = sourcePixels[i + 3];
                if (a == 0) { result[i] = result[i + 1] = result[i + 2] = result[i + 3] = 0; continue; }

                float tr = r + (tint.R - r) * tintStrength;
                float tg = g + (tint.G - g) * tintStrength;
                float tb = b + (tint.B - b) * tintStrength;
                float newA = a * opacityFactor;

                result[i] = (byte)Math.Clamp(tb, 0, 255);
                result[i + 1] = (byte)Math.Clamp(tg, 0, 255);
                result[i + 2] = (byte)Math.Clamp(tr, 0, 255);
                result[i + 3] = (byte)Math.Clamp(newA, 0, 255);
            }
            return result;
        }

        private byte[]? ReadTilePixelsAt(int tileX, int tileY)
        {
            if (_document == null || _tileWidth <= 0 || _tileHeight <= 0) return null;

            int docX = tileX * _tileWidth;
            int docY = tileY * _tileHeight;

            if (docX < 0 || docY < 0 || docX + _tileWidth > _document.PixelWidth || docY + _tileHeight > _document.PixelHeight)
                return null;

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
                        pixels[dstIdx] = surface.Pixels[srcIdx];
                        pixels[dstIdx + 1] = surface.Pixels[srcIdx + 1];
                        pixels[dstIdx + 2] = surface.Pixels[srcIdx + 2];
                        pixels[dstIdx + 3] = surface.Pixels[srcIdx + 3];
                    }
                }
            }
            return pixels;
        }

        private void DrawPixelGrid(SKCanvas canvas, float x, float y, float w, float h, float scale)
        {
            using var gridPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = new SKColor(255, 255, 255, 50),
                StrokeWidth = 1,
                IsAntialias = false
            };

            for (int px = 0; px <= _tileWidth; px++)
            {
                float lx = x + px * scale;
                canvas.DrawLine(lx, y, lx, y + h, gridPaint);
            }

            for (int py = 0; py <= _tileHeight; py++)
            {
                float ly = y + py * scale;
                canvas.DrawLine(x, ly, x + w, ly, gridPaint);
            }
        }

        private void CompositeBrushGhostIntoBuffer(byte[] tilePixels)
        {
            int docX = _tileX * _tileWidth;
            int docY = _tileY * _tileHeight;
            int localX = _hoverDocX - docX;
            int localY = _hoverDocY - docY;

            var offsets = GetCurrentBrushOffsets();

            foreach (var (dx, dy) in offsets)
            {
                int px = localX + dx;
                int py = localY + dy;

                if (px < 0 || px >= _tileWidth || py < 0 || py >= _tileHeight) continue;

                byte effA = ComputeBrushAlphaAtOffset(dx, dy);
                if (effA == 0) continue;

                int idx = (py * _tileWidth + px) * 4;
                uint before = (uint)(tilePixels[idx] | (tilePixels[idx + 1] << 8) |
                                    (tilePixels[idx + 2] << 16) | (tilePixels[idx + 3] << 24));
                uint srcWithAlpha = (_foregroundColor & 0x00FFFFFFu) | ((uint)effA << 24);
                uint after = ColorUtil.BlendOver(before, srcWithAlpha);

                tilePixels[idx] = (byte)(after & 0xFF);
                tilePixels[idx + 1] = (byte)((after >> 8) & 0xFF);
                tilePixels[idx + 2] = (byte)((after >> 16) & 0xFF);
                tilePixels[idx + 3] = (byte)((after >> 24) & 0xFF);
            }
        }

        private void DrawBrushOutline(SKCanvas canvas, float destX, float destY, float scale, byte[] tilePixels)
        {
            int docX = _tileX * _tileWidth;
            int docY = _tileY * _tileHeight;
            int localX = _hoverDocX - docX;
            int localY = _hoverDocY - docY;

            var offsets = GetCurrentBrushOffsets();
            if (offsets.Count == 0) return;

            StrokeUtil.BuildMaskGrid(offsets, out var grid, out int minDx, out int minDy, out int w, out int h);

            int localX0 = localX + minDx;
            int localY0 = localY + minDy;

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
                    var ink = SampleInkAtLocal(sampleLocalX, outLocalY, tilePixels);

                    int x0 = x++;
                    while (x < w)
                    {
                        bool e2 = StrokeUtil.Occup(grid, w, h, x, y) ^ StrokeUtil.Occup(grid, w, h, x, y - 1);
                        if (!e2) break;
                        bool insideBelow2 = StrokeUtil.Occup(grid, w, h, x, y);
                        int outLocalY2 = localY0 + (insideBelow2 ? y - 1 : y);
                        int sampleLocalX2 = localX0 + Math.Clamp(x, 0, w - 1);
                        var ink2 = SampleInkAtLocal(sampleLocalX2, outLocalY2, tilePixels);
                        if (!ink2.Equals(ink)) break;
                        x++;
                    }

                    float sx0 = destX + (localX0 + x0) * scale;
                    float sx1 = destX + (localX0 + x) * scale;
                    float sy = destY + (localY0 + y) * scale;
                    using var paint = new SKPaint { Color = new SKColor(ink.R, ink.G, ink.B, ink.A), StrokeWidth = 1, IsAntialias = false };
                    canvas.DrawLine(sx0, sy, sx1, sy, paint);
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
                    var ink = SampleInkAtLocal(outLocalX, sampleLocalY, tilePixels);

                    int y0 = y++;
                    while (y < h)
                    {
                        bool e2 = StrokeUtil.Occup(grid, w, h, x, y) ^ StrokeUtil.Occup(grid, w, h, x - 1, y);
                        if (!e2) break;
                        bool insideRight2 = StrokeUtil.Occup(grid, w, h, x, y);
                        int outLocalX2 = localX0 + (insideRight2 ? x - 1 : x);
                        int sampleLocalY2 = localY0 + Math.Clamp(y, 0, h - 1);
                        var ink2 = SampleInkAtLocal(outLocalX2, sampleLocalY2, tilePixels);
                        if (!ink2.Equals(ink)) break;
                        y++;
                    }

                    float sy0 = destY + (localY0 + y0) * scale;
                    float sy1 = destY + (localY0 + y) * scale;
                    float sx = destX + (localX0 + x) * scale;
                    using var paint = new SKPaint { Color = new SKColor(ink.R, ink.G, ink.B, ink.A), StrokeWidth = 1, IsAntialias = false };
                    canvas.DrawLine(sx, sy0, sx, sy1, paint);
                }
            }
        }

        private Color SampleInkAtLocal(int localX, int localY, byte[] tilePixels)
        {
            localX = Math.Clamp(localX, 0, _tileWidth - 1);
            localY = Math.Clamp(localY, 0, _tileHeight - 1);

            int idx = (localY * _tileWidth + localX) * 4;
            if (idx < 0 || idx + 3 >= tilePixels.Length) return Colors.White;

            byte b = tilePixels[idx], g = tilePixels[idx + 1], r = tilePixels[idx + 2], a = tilePixels[idx + 3];
            uint bgra = (uint)(b | (g << 8) | (r << 16) | (a << 24));
            uint onWhite = ColorUtil.CompositeOverWhite(bgra);
            return ColorUtil.HighContrastInk(onWhite);
        }

        private byte[]? ReadTilePixelsFromDocument()
        {
            if (_document == null || _tileX < 0 || _tileY < 0) return null;

            int docX = _tileX * _tileWidth;
            int docY = _tileY * _tileHeight;

            if (docX < 0 || docY < 0 || docX + _tileWidth > _document.PixelWidth || docY + _tileHeight > _document.PixelHeight)
                return null;

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
                        pixels[dstIdx] = surface.Pixels[srcIdx];
                        pixels[dstIdx + 1] = surface.Pixels[srcIdx + 1];
                        pixels[dstIdx + 2] = surface.Pixels[srcIdx + 2];
                        pixels[dstIdx + 3] = surface.Pixels[srcIdx + 3];
                    }
                }
            }
            return pixels;
        }

        // ====================================================================
        // INPUT HANDLING (unchanged except EditorCanvas references)
        // ====================================================================

        private void EditorCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_document == null || _stroke == null || _tileX < 0 || _tileY < 0) { e.Handled = true; return; }

            var point = e.GetCurrentPoint(EditorCanvas);
            var props = point.Properties;
            var (docX, docY) = ScreenToDocument(point.Position);

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

            if (props.IsRightButtonPressed) { SampleColorAt(docX, docY); e.Handled = true; return; }
            if (!props.IsLeftButtonPressed) { e.Handled = true; return; }
            if (IsDropperToolActive()) { SampleColorAt(docX, docY); e.Handled = true; return; }
            if (IsFillToolActive()) { HandleFillAt(docX, docY); e.Handled = true; return; }
            if (!IsPaintingToolActive()) { e.Handled = true; return; }
            if (_document.ActiveLayer is not RasterLayer rl) { e.Handled = true; return; }

            int tileDocX = _tileX * _tileWidth;
            int tileDocY = _tileY * _tileHeight;
            if (docX < tileDocX || docX >= tileDocX + _tileWidth || docY < tileDocY || docY >= tileDocY + _tileHeight)
            { e.Handled = true; return; }

            CaptureBeforeState();
            _isPainting = true;
            _hasLastPos = true;
            _lastDocX = docX;
            _lastDocY = docY;

            _activePainter = GetPainterForCurrentTool();
            if (_activePainter != null) _stroke.BeginWithPainter(_activePainter, rl);

            var strokeSettings = GetStrokeSettingsForCurrentTool();
            if (strokeSettings != null && _activePainter != null)
            {
                uint fg = (_foregroundColor & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);
                _stroke.StampAtWithPainter(docX, docY, fg, _backgroundColor, strokeSettings);
            }

            EditorCanvas.CapturePointer(e.Pointer);
            _document.CompositeTo(_document.Surface);
            _document.RaiseDocumentModified();
            EditorCanvas.Invalidate();
            e.Handled = true;
        }

        private void EditorCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_document == null || _tileX < 0 || _tileY < 0) { e.Handled = true; return; }

            var point = e.GetCurrentPoint(EditorCanvas);
            var props = point.Properties;

            if (_isPanning)
            {
                _panOffsetX = _panStartOffsetX + point.Position.X - _panStartPoint.X;
                _panOffsetY = _panStartOffsetY + point.Position.Y - _panStartPoint.Y;
                EditorCanvas?.Invalidate();
                e.Handled = true;
                return;
            }

            var (docX, docY) = ScreenToDocument(point.Position);
            int tileDocX = _tileX * _tileWidth;
            int tileDocY = _tileY * _tileHeight;
            _hoverDocX = docX;
            _hoverDocY = docY;
            _hoverValid = docX >= tileDocX && docX < tileDocX + _tileWidth && docY >= tileDocY && docY < tileDocY + _tileHeight;

            if (_isPainting && _stroke != null && props.IsLeftButtonPressed)
            {
                if (_hoverValid && _document.ActiveLayer is RasterLayer)
                {
                    var strokeSettings = GetStrokeSettingsForCurrentTool();
                    if (strokeSettings != null && _activePainter != null)
                    {
                        uint fg = (_foregroundColor & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);
                        if (_hasLastPos && (_lastDocX != docX || _lastDocY != docY))
                            _stroke.StampLineWithPainter(_lastDocX, _lastDocY, docX, docY, fg, _backgroundColor, strokeSettings);
                        else
                            _stroke.StampAtWithPainter(docX, docY, fg, _backgroundColor, strokeSettings);

                        _lastDocX = docX; _lastDocY = docY; _hasLastPos = true;
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
            if (_isPanning) { _isPanning = false; EditorCanvas.ReleasePointerCapture(e.Pointer); ProtectedCursor = null; e.Handled = true; return; }

            if (_isPainting)
            {
                _isPainting = false; _hasLastPos = false; _lastDocX = -1; _lastDocY = -1;
                if (_activePainter != null && _stroke?.HasActivePainterStroke == true) { _stroke.CommitPainter("Brush Stroke"); _activePainter = null; }
                PushStrokeToHistory();
                if (_document != null) { _document.CompositeTo(_document.Surface); _document.RaiseDocumentModified(); }
                TileModified?.Invoke();
            }

            EditorCanvas.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        private void EditorCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _hoverValid = false; _hoverDocX = -1; _hoverDocY = -1;
            EditorCanvas?.Invalidate();
        }

        private void EditorCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(EditorCanvas);
            var delta = point.Properties.MouseWheelDelta;

            if (IsKeyDown(Windows.System.VirtualKey.Control))
            {
                int step = delta > 0 ? 1 : -1;
                _toolState?.UpdateBrush(b => b.Size = Math.Clamp(b.Size + step, 1, 64));
                e.Handled = true;
                return;
            }

            float canvasW = (float)EditorCanvas.ActualWidth;
            float canvasH = (float)EditorCanvas.ActualHeight;
            double oldZoom = _zoomLevel;

            _zoomLevel = delta > 0 ? Math.Min(MaxZoom, _zoomLevel * ZoomStep) : Math.Max(MinZoom, _zoomLevel / ZoomStep);

            double zoomRatio = _zoomLevel / oldZoom;
            double centerX = canvasW / 2 + _panOffsetX;
            double centerY = canvasH / 2 + _panOffsetY;
            double newCenterX = point.Position.X + (centerX - point.Position.X) * zoomRatio;
            double newCenterY = point.Position.Y + (centerY - point.Position.Y) * zoomRatio;
            _panOffsetX = newCenterX - canvasW / 2;
            _panOffsetY = newCenterY - canvasH / 2;

            EditorCanvas.Invalidate();
            e.Handled = true;
        }

        private static bool IsKeyDown(Windows.System.VirtualKey k) =>
            InputKeyboardSource.GetKeyStateForCurrentThread(k).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        private void EditorCanvas_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Space && !_spacePan)
            { _spacePan = true; ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll); e.Handled = true; }
        }

        private void EditorCanvas_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Space) { _spacePan = false; if (!_isPanning) ProtectedCursor = null; e.Handled = true; }
        }

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
            float destX = (canvasW - destW) / 2 + (float)_panOffsetX;
            float destY = (canvasH - destH) / 2 + (float)_panOffsetY;

            int localX = (int)Math.Floor((screenPos.X - destX) / scale);
            int localY = (int)Math.Floor((screenPos.Y - destY) / scale);

            return (_tileX * _tileWidth + localX, _tileY * _tileHeight + localY);
        }

        private void UpdateTilePosition()
        {
            if (_state == null || _document == null)
            { _tileX = -1; _tileY = -1; _tileWidth = 0; _tileHeight = 0; return; }

            var (tx, ty) = _state.CurrentTilePosition;
            if (_tileX != tx || _tileY != ty) { _panOffsetX = 0; _panOffsetY = 0; }

            _tileX = tx; _tileY = ty;
            _tileWidth = _document.TileSize.Width;
            _tileHeight = _document.TileSize.Height;

            if (_stroke != null && _document.TargetSurface != null)
            { _stroke = new StrokeEngine(_document.TargetSurface); SyncBrushToStrokeEngine(); }
        }

        // ====================================================================
        // TOOL OPERATIONS
        // ====================================================================

        private void SampleColorAt(int docX, int docY)
        {
            if (_document == null || _palette == null) return;
            if (docX < 0 || docX >= _document.PixelWidth || docY < 0 || docY >= _document.PixelHeight) return;

            var surface = _document.Surface;
            int idx = (docY * surface.Width + docX) * 4;
            if (idx < 0 || idx + 3 >= surface.Pixels.Length) return;

            uint color = (uint)(surface.Pixels[idx] | (surface.Pixels[idx + 1] << 8) | (surface.Pixels[idx + 2] << 16) | (surface.Pixels[idx + 3] << 24));
            _palette.SetForeground(color);
        }

        private void HandleFillAt(int docX, int docY)
        {
            if (_document?.ActiveLayer is not RasterLayer rl) return;
            CaptureBeforeState();

            var fillReg = ToolRegistry.Shared.GetById<FillToolRegistration>(ToolIds.Fill);
            if (fillReg == null) return;

            var context = new FillContext { Surface = rl.Surface, Color = _foregroundColor, Tolerance = _toolState?.FillTolerance ?? 0, Contiguous = _toolState?.FillContiguous ?? true, Description = "Fill" };
            var result = fillReg.EffectiveFillPainter.FillAt(rl, docX, docY, context);

            if (result is { CanPushToHistory: true } and IHistoryItem historyItem)
                _document.History.Push(historyItem);

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
                        _strokeBeforePixels[dstIdx] = surface.Pixels[srcIdx];
                        _strokeBeforePixels[dstIdx + 1] = surface.Pixels[srcIdx + 1];
                        _strokeBeforePixels[dstIdx + 2] = surface.Pixels[srcIdx + 2];
                        _strokeBeforePixels[dstIdx + 3] = surface.Pixels[srcIdx + 3];
                    }
                }
            }
        }

        private void PushStrokeToHistory()
        {
            if (_document?.ActiveLayer is not RasterLayer rl || _strokeBeforePixels == null) return;

            int docX = _tileX * _tileWidth;
            int docY = _tileY * _tileHeight;
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
                        afterPixels[dstIdx] = surface.Pixels[srcIdx];
                        afterPixels[dstIdx + 1] = surface.Pixels[srcIdx + 1];
                        afterPixels[dstIdx + 2] = surface.Pixels[srcIdx + 2];
                        afterPixels[dstIdx + 3] = surface.Pixels[srcIdx + 3];
                    }
                }
            }

            bool hasChanges = false;
            for (int i = 0; i < _strokeBeforePixels.Length; i++)
                if (_strokeBeforePixels[i] != afterPixels[i]) { hasChanges = true; break; }

            if (!hasChanges) { _strokeBeforePixels = null; return; }

            var bounds = CreateRect(docX, docY, _tileWidth, _tileHeight);
            var historyItem = PixelChangeItem.FromRegion(rl, bounds, _strokeBeforePixels, afterPixels, "Brush Stroke");

            if (!historyItem.IsEmpty) _document.History.Push(historyItem);
            _strokeBeforePixels = null;
        }
    }
}
