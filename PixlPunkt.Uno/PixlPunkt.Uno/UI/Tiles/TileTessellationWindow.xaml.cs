using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.History;
using PixlPunkt.Uno.Core.Palette;
using PixlPunkt.Uno.Core.Tile;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.UI.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace PixlPunkt.Uno.UI.Tiles
{
    /// <summary>
    /// Window for previewing tile tessellation patterns and editing tile content.
    /// Supports painting directly on the center tile with wrap-around for seamless patterns.
    /// </summary>
    public sealed partial class TileTessellationWindow : Window
    {
        // ====================================================================
        // TILE STATE
        // ====================================================================

        private TileSet? _tileSet;
        private int _tileId = -1;
        private byte[]? _originalPixels;  // For revert
        private byte[]? _workingPixels;   // Active editing buffer
        private int _tileWidth;
        private int _tileHeight;

        // ====================================================================
        // VIEW STATE
        // ====================================================================

        private int _gridSize = 3;
        private int _zoom = 2;
        private int _offsetX = 0;
        private int _offsetY = 0;
        private bool _showGrid = true;
        private bool _updatingControls = false;
        private bool _initialized = false;

        // ====================================================================
        // EXTERNAL DEPENDENCIES
        // ====================================================================

        private PaletteService? _palette;
        private ToolState? _toolState;
        private CanvasDocument? _document;

        // ====================================================================
        // PAINTING STATE
        // ====================================================================

        private bool _isPainting;
        private int _lastTileX, _lastTileY;
        private int _lastCanvasX, _lastCanvasY;  // Track canvas position for cross-tile line drawing
        private bool _hasLastPos;
        private int _hoverCanvasX, _hoverCanvasY;
        private bool _hoverValid;

        // History state - capture before pixels at stroke start
        private byte[]? _strokeBeforePixels;

        // Brush settings (synced from ToolState)
        private int _brushSize = 1;
        private BrushShape _brushShape = BrushShape.Circle;
        private byte _brushOpacity = 255;
        private byte _brushDensity = 255;
        private uint _foregroundColor = 0xFFFFFFFF;

        // ====================================================================
        // RENDERING
        // ====================================================================

        private readonly PatternBackgroundService _pattern = new();
        private SKShader? _checkerboardShader;
        private SKBitmap? _checkerboardBitmap;

        // ====================================================================
        // CONSTRUCTION
        // ====================================================================

        public TileTessellationWindow()
        {
            InitializeComponent();

            // Set initial window size using XAML properties or content sizing
            // AppWindow.Resize is not available on all Uno platforms

            if (Content is FrameworkElement root)
            {
                root.Loaded += Root_Loaded;
                root.KeyDown += Root_KeyDown;
                root.Width = 600;
                root.Height = 550;
            }

            Closed += TileTessellationWindow_Closed;
        }

        private void Root_Loaded(object sender, RoutedEventArgs e)
        {
            _initialized = true;
            ApplyStripeColors();
            TransparencyStripeMixer.ColorsChanged += OnStripeColorsChanged;

            // Make root focusable for keyboard input
            if (Content is FrameworkElement root)
            {
                root.IsTabStop = true;
                root.Focus(FocusState.Programmatic);
            }
        }

        private void TileTessellationWindow_Closed(object sender, WindowEventArgs args)
        {
            // Unhook events
            TransparencyStripeMixer.ColorsChanged -= OnStripeColorsChanged;

            if (_palette != null)
            {
                _palette.ForegroundChanged -= OnForegroundChanged;
            }

            if (_toolState != null)
            {
                _toolState.BrushChanged -= OnBrushChanged;
            }

            // Auto-commit changes on close (no history needed - already tracked)
            CommitToTileSet();

            // Dispose SkiaSharp resources
            _checkerboardShader?.Dispose();
            _checkerboardBitmap?.Dispose();
        }

        private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Handle Ctrl+Z (Undo) and Ctrl+Y (Redo)
            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (ctrl && e.Key == VirtualKey.Z)
            {
                DoUndo();
                e.Handled = true;
            }
            else if (ctrl && e.Key == VirtualKey.Y)
            {
                DoRedo();
                e.Handled = true;
            }
        }

        private void OnStripeColorsChanged()
        {
            ApplyStripeColors();
            _pattern.Invalidate();
            InvalidateCheckerboardCache();
            TessellationCanvas?.Invalidate();
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

            if (_pattern.LightColor != light || _pattern.DarkColor != dark)
            {
                _pattern.LightColor = light;
                _pattern.DarkColor = dark;
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
        // HISTORY (UNDO/REDO)
        // ====================================================================

        private void DoUndo()
        {
            if (_document?.History.CanUndo == true)
            {
                _document.History.Undo();

                // Refresh working pixels from tile set after undo
                RefreshWorkingPixelsFromTileSet();
                TessellationCanvas?.Invalidate();
            }
        }

        private void DoRedo()
        {
            if (_document?.History.CanRedo == true)
            {
                _document.History.Redo();

                // Refresh working pixels from tile set after redo
                RefreshWorkingPixelsFromTileSet();
                TessellationCanvas?.Invalidate();
            }
        }

        /// <summary>
        /// Refreshes the working pixel buffer from the tile set.
        /// Called after undo/redo to sync local state with tile set.
        /// </summary>
        private void RefreshWorkingPixelsFromTileSet()
        {
            if (_tileSet == null || _tileId < 0 || _workingPixels == null)
                return;

            var tilePixels = _tileSet.GetTilePixels(_tileId);
            if (tilePixels != null && tilePixels.Length == _workingPixels.Length)
            {
                Buffer.BlockCopy(tilePixels, 0, _workingPixels, 0, tilePixels.Length);
            }
        }

        /// <summary>
        /// Pushes a tile edit to history if there are changes.
        /// </summary>
        private void PushStrokeToHistory()
        {
            if (_document == null || _tileSet == null || _tileId < 0 || _strokeBeforePixels == null)
                return;

            var currentPixels = _tileSet.GetTilePixels(_tileId);
            if (currentPixels == null)
                return;

            var historyItem = new TileEditHistoryItem(
                _tileSet,
                _tileId,
                _strokeBeforePixels,
                currentPixels,
                "Paint Tile");

            if (historyItem.HasChanges)
            {
                _document.History.Push(historyItem);
            }

            _strokeBeforePixels = null;
        }

        // ====================================================================
        // BINDING
        // ====================================================================

        /// <summary>
        /// Binds the window to a specific tile from a tile set.
        /// </summary>
        public void BindTile(TileSet tileSet, int tileId, PaletteService? palette = null, ToolState? toolState = null, CanvasDocument? document = null)
        {
            _tileSet = tileSet;
            _tileId = tileId;
            _palette = palette;
            _toolState = toolState;
            _document = document;

            var tile = _tileSet?.GetTile(tileId);
            if (tile == null)
            {
                Close();
                return;
            }

            _tileWidth = tile.Width;
            _tileHeight = tile.Height;

            // Clone original pixels for revert
            _originalPixels = new byte[tile.Pixels.Length];
            Buffer.BlockCopy(tile.Pixels, 0, _originalPixels, 0, tile.Pixels.Length);

            // Create working buffer for live editing
            _workingPixels = new byte[tile.Pixels.Length];
            Buffer.BlockCopy(tile.Pixels, 0, _workingPixels, 0, tile.Pixels.Length);

            // Setup offset slider limits
            OffsetXSlider.Maximum = _tileWidth - 1;
            OffsetYSlider.Maximum = _tileHeight - 1;
            OffsetXBox.Maximum = _tileWidth - 1;
            OffsetYBox.Maximum = _tileHeight - 1;

            _offsetX = 0;
            _offsetY = 0;

            Title = $"Tile Tessellator - Tile {tileId} ({_tileWidth}x{_tileHeight})";

            // Hook palette events
            if (_palette != null)
            {
                _palette.ForegroundChanged += OnForegroundChanged;
                _foregroundColor = _palette.Foreground;
            }

            // Hook tool state events
            if (_toolState != null)
            {
                _toolState.BrushChanged += OnBrushChanged;
                SyncBrushFromToolState();
            }

            UpdateCanvasSize();
            TessellationCanvas?.Invalidate();
        }

        private void OnForegroundChanged(uint color)
        {
            _foregroundColor = color;
        }

        private void OnBrushChanged(BrushSettings s)
        {
            SyncBrushFromToolState();
        }

        private void SyncBrushFromToolState()
        {
            if (_toolState == null) return;

            _brushSize = _toolState.Brush.Size;
            _brushShape = _toolState.Brush.Shape;
            _brushOpacity = _toolState.Brush.Opacity;
            _brushDensity = _toolState.Brush.Density;

            TessellationCanvas?.Invalidate();
        }

        // ====================================================================
        // COORDINATE TRANSLATION
        // ====================================================================

        private (int canvasX, int canvasY) ScreenToCanvas(Point screenPos)
        {
            int canvasX = (int)Math.Floor(screenPos.X / _zoom);
            int canvasY = (int)Math.Floor(screenPos.Y / _zoom);
            return (canvasX, canvasY);
        }

        private (int tileX, int tileY) CanvasToTileLocal(int canvasX, int canvasY)
        {
            int tileX = ((canvasX - _offsetX) % _tileWidth + _tileWidth) % _tileWidth;
            int tileY = ((canvasY - _offsetY) % _tileHeight + _tileHeight) % _tileHeight;
            return (tileX, tileY);
        }

        // ====================================================================
        // TILE PIXEL PAINTING
        // ====================================================================

        private void PaintPixel(int tileX, int tileY, uint color)
        {
            if (_workingPixels == null) return;

            int x = ((tileX % _tileWidth) + _tileWidth) % _tileWidth;
            int y = ((tileY % _tileHeight) + _tileHeight) % _tileHeight;

            int idx = (y * _tileWidth + x) * 4;
            if (idx < 0 || idx + 3 >= _workingPixels.Length) return;

            byte b = (byte)(color & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte a = (byte)((color >> 24) & 0xFF);

            if (a < 255)
            {
                byte existB = _workingPixels[idx];
                byte existG = _workingPixels[idx + 1];
                byte existR = _workingPixels[idx + 2];
                byte existA = _workingPixels[idx + 3];

                float srcA = a / 255f;
                float dstA = existA / 255f;
                float outA = srcA + dstA * (1 - srcA);

                if (outA > 0)
                {
                    _workingPixels[idx] = (byte)((b * srcA + existB * dstA * (1 - srcA)) / outA);
                    _workingPixels[idx + 1] = (byte)((g * srcA + existG * dstA * (1 - srcA)) / outA);
                    _workingPixels[idx + 2] = (byte)((r * srcA + existR * dstA * (1 - srcA)) / outA);
                    _workingPixels[idx + 3] = (byte)(outA * 255);
                }
            }
            else
            {
                _workingPixels[idx] = b;
                _workingPixels[idx + 1] = g;
                _workingPixels[idx + 2] = r;
                _workingPixels[idx + 3] = a;
            }
        }

        private void StampBrush(int tileX, int tileY)
        {
            int radius = (_brushSize - 1) / 2;
            uint color = (_foregroundColor & 0x00FFFFFFu) | ((uint)_brushOpacity << 24);

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    bool inBrush = _brushShape == BrushShape.Circle
                        ? (dx * dx + dy * dy) <= (radius * radius + radius)
                        : true;

                    if (inBrush)
                    {
                        byte pixelAlpha = CalculateBrushAlpha(dx, dy, radius);
                        if (pixelAlpha > 0)
                        {
                            uint pixelColor = (color & 0x00FFFFFFu) | ((uint)pixelAlpha << 24);
                            PaintPixel(tileX + dx, tileY + dy, pixelColor);
                        }
                    }
                }
            }
        }

        private byte CalculateBrushAlpha(int dx, int dy, int radius)
        {
            if (radius == 0) return _brushOpacity;

            double dist = Math.Sqrt(dx * dx + dy * dy);
            double r = radius + 0.5;
            double density = _brushDensity / 255.0;
            double hardRadius = r * density;

            if (dist <= hardRadius)
                return _brushOpacity;

            if (dist > r)
                return 0;

            double span = Math.Max(0.001, r - hardRadius);
            double t = (dist - hardRadius) / span;
            double falloff = 1.0 - (t * t) * (3 - 2 * t);

            return (byte)Math.Round(_brushOpacity * falloff);
        }

        private void StampLineCanvas(int canvasX0, int canvasY0, int canvasX1, int canvasY1)
        {
            int dx = Math.Abs(canvasX1 - canvasX0);
            int dy = Math.Abs(canvasY1 - canvasY0);
            int sx = canvasX0 < canvasX1 ? 1 : -1;
            int sy = canvasY0 < canvasY1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                var (tileX, tileY) = CanvasToTileLocal(canvasX0, canvasY0);
                StampBrush(tileX, tileY);

                if (canvasX0 == canvasX1 && canvasY0 == canvasY1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    canvasX0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    canvasY0 += sy;
                }
            }
        }

        // ====================================================================
        // POINTER INPUT HANDLERS
        // ====================================================================

        private void TessellationCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_workingPixels == null || _tileSet == null) return;

            var pt = e.GetCurrentPoint(TessellationCanvas);
            if (!pt.Properties.IsLeftButtonPressed) return;

            var (canvasX, canvasY) = ScreenToCanvas(pt.Position);

            int canvasWidth = _tileWidth * _gridSize;
            int canvasHeight = _tileHeight * _gridSize;
            if (canvasX < 0 || canvasX >= canvasWidth || canvasY < 0 || canvasY >= canvasHeight)
                return;

            var (tileX, tileY) = CanvasToTileLocal(canvasX, canvasY);

            var currentTilePixels = _tileSet.GetTilePixels(_tileId);
            if (currentTilePixels != null)
            {
                _strokeBeforePixels = (byte[])currentTilePixels.Clone();
            }

            _isPainting = true;
            _hasLastPos = true;
            _lastTileX = tileX;
            _lastTileY = tileY;
            _lastCanvasX = canvasX;
            _lastCanvasY = canvasY;

            StampBrush(tileX, tileY);

            TessellationCanvas.CapturePointer(e.Pointer);
            TessellationCanvas.Invalidate();

            PushLiveUpdate();
        }

        private void TessellationCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(TessellationCanvas);
            var (canvasX, canvasY) = ScreenToCanvas(pt.Position);

            _hoverCanvasX = canvasX;
            _hoverCanvasY = canvasY;

            int canvasWidth = _tileWidth * _gridSize;
            int canvasHeight = _tileHeight * _gridSize;
            _hoverValid = canvasX >= 0 && canvasX < canvasWidth &&
                          canvasY >= 0 && canvasY < canvasHeight;

            if (_isPainting && _workingPixels != null && pt.Properties.IsLeftButtonPressed)
            {
                int clampedCanvasX = Math.Clamp(canvasX, 0, canvasWidth - 1);
                int clampedCanvasY = Math.Clamp(canvasY, 0, canvasHeight - 1);

                if (_hasLastPos)
                {
                    StampLineCanvas(_lastCanvasX, _lastCanvasY, clampedCanvasX, clampedCanvasY);
                }
                else
                {
                    var (tileX, tileY) = CanvasToTileLocal(clampedCanvasX, clampedCanvasY);
                    StampBrush(tileX, tileY);
                }

                _lastCanvasX = clampedCanvasX;
                _lastCanvasY = clampedCanvasY;
                var (lastTileX, lastTileY) = CanvasToTileLocal(clampedCanvasX, clampedCanvasY);
                _lastTileX = lastTileX;
                _lastTileY = lastTileY;
                _hasLastPos = true;

                PushLiveUpdate();
            }

            TessellationCanvas?.Invalidate();
        }

        private void TessellationCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isPainting)
            {
                _isPainting = false;
                _hasLastPos = false;
                TessellationCanvas.ReleasePointerCaptures();

                PushLiveUpdate();
                PushStrokeToHistory();
            }
        }

        private void TessellationCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _hoverValid = false;
            TessellationCanvas?.Invalidate();
        }

        // ====================================================================
        // COMMIT / REVERT
        // ====================================================================

        private void CommitToTileSet()
        {
            if (_tileSet == null || _tileId < 0 || _workingPixels == null) return;

            byte[] finalPixels = ApplyOffset(_workingPixels, _offsetX, _offsetY);
            _tileSet.UpdateTilePixels(_tileId, finalPixels);
        }

        private void PushLiveUpdate()
        {
            if (_tileSet == null || _tileId < 0 || _workingPixels == null)
                return;

            byte[] previewPixels = ApplyOffset(_workingPixels, _offsetX, _offsetY);
            _tileSet.UpdateTilePixels(_tileId, previewPixels);
        }

        private void RevertToOriginal()
        {
            if (_originalPixels == null || _workingPixels == null || _tileSet == null) return;

            var currentPixels = _tileSet.GetTilePixels(_tileId);
            if (currentPixels != null && _document != null)
            {
                var historyItem = new TileEditHistoryItem(
                    _tileSet,
                    _tileId,
                    currentPixels,
                    _originalPixels,
                    "Revert Tile");

                if (historyItem.HasChanges)
                {
                    _document.History.Push(historyItem);
                }
            }

            Buffer.BlockCopy(_originalPixels, 0, _workingPixels, 0, _originalPixels.Length);
            _offsetX = 0;
            _offsetY = 0;

            _updatingControls = true;
            if (OffsetXSlider != null) OffsetXSlider.Value = 0;
            if (OffsetYSlider != null) OffsetYSlider.Value = 0;
            if (OffsetXBox != null) OffsetXBox.Value = 0;
            if (OffsetYBox != null) OffsetYBox.Value = 0;
            _updatingControls = false;

            _tileSet.UpdateTilePixels(_tileId, _originalPixels);

            TessellationCanvas?.Invalidate();
        }

        // ====================================================================
        // CANVAS SIZE
        // ====================================================================

        private void UpdateCanvasSize()
        {
            if (TessellationCanvas == null) return;

            int canvasWidth = _tileWidth * _gridSize * _zoom;
            int canvasHeight = _tileHeight * _gridSize * _zoom;

            TessellationCanvas.Width = canvasWidth;
            TessellationCanvas.Height = canvasHeight;
        }

        // ====================================================================
        // RENDERING
        // ====================================================================

        private void TessellationCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            if (_workingPixels == null || _tileWidth <= 0 || _tileHeight <= 0)
                return;

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            int canvasWidth = _tileWidth * _gridSize * _zoom;
            int canvasHeight = _tileHeight * _gridSize * _zoom;

            // Draw transparency checkerboard background
            var (lightColor, darkColor) = _pattern.CurrentScheme;
            EnsureCheckerboardShader(8, lightColor, darkColor);

            if (_checkerboardShader != null)
            {
                using var bgPaint = new SKPaint { Shader = _checkerboardShader, IsAntialias = false };
                canvas.DrawRect(0, 0, canvasWidth, canvasHeight, bgPaint);
            }

            // Apply offset to get preview pixels
            byte[] previewPixels = ApplyOffset(_workingPixels, _offsetX, _offsetY);

            // Create bitmap from working pixels
            var info = new SKImageInfo(_tileWidth, _tileHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var tileBitmap = new SKBitmap(info);
            var handle = tileBitmap.GetPixels();
            Marshal.Copy(previewPixels, 0, handle, previewPixels.Length);

            // Render grid of tiles
            using var tilePaint = new SKPaint { FilterQuality = SKFilterQuality.None, IsAntialias = false };

            for (int gy = 0; gy < _gridSize; gy++)
            {
                for (int gx = 0; gx < _gridSize; gx++)
                {
                    float destX = gx * _tileWidth * _zoom;
                    float destY = gy * _tileHeight * _zoom;
                    float destW = _tileWidth * _zoom;
                    float destH = _tileHeight * _zoom;

                    var destRect = new SKRect(destX, destY, destX + destW, destY + destH);
                    var srcRect = new SKRect(0, 0, _tileWidth, _tileHeight);

                    canvas.DrawBitmap(tileBitmap, srcRect, destRect, tilePaint);
                }
            }

            // Draw grid lines if enabled
            if (_showGrid)
            {
                var gridColor = new SKColor(255, 255, 255, 128);
                using var gridPaint = new SKPaint { Color = gridColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = false };

                for (int gx = 1; gx < _gridSize; gx++)
                {
                    float x = gx * _tileWidth * _zoom;
                    canvas.DrawLine(x, 0, x, canvasHeight, gridPaint);
                }

                for (int gy = 1; gy < _gridSize; gy++)
                {
                    float y = gy * _tileHeight * _zoom;
                    canvas.DrawLine(0, y, canvasWidth, y, gridPaint);
                }

                // Highlight center tile (editable area)
                int centerTileX = _gridSize / 2;
                int centerTileY = _gridSize / 2;
                float highlightX = centerTileX * _tileWidth * _zoom;
                float highlightY = centerTileY * _tileHeight * _zoom;
                float highlightW = _tileWidth * _zoom;
                float highlightH = _tileHeight * _zoom;

                using var highlightPaint = new SKPaint { Color = new SKColor(0, 160, 255, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
                canvas.DrawRect(highlightX, highlightY, highlightW, highlightH, highlightPaint);
            }

            // Draw brush cursor overlay
            if (_hoverValid)
            {
                DrawBrushCursor(canvas);
            }
        }

        private void EnsureCheckerboardShader(int squareSize, Color lightColor, Color darkColor)
        {
            if (_checkerboardBitmap != null && _checkerboardShader != null)
                return;

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

        private void DrawBrushCursor(SKCanvas canvas)
        {
            float centerX = _hoverCanvasX * _zoom + _zoom / 2f;
            float centerY = _hoverCanvasY * _zoom + _zoom / 2f;
            float radius = _brushSize * _zoom / 2f;

            var cursorColor = new SKColor(255, 255, 255, 200);
            var shadowColor = new SKColor(0, 0, 0, 128);

            using var cursorPaint = new SKPaint { Color = cursorColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
            using var shadowPaint = new SKPaint { Color = shadowColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };

            if (_brushShape == BrushShape.Circle)
            {
                canvas.DrawOval(centerX + 1, centerY + 1, radius, radius, shadowPaint);
                canvas.DrawOval(centerX, centerY, radius, radius, cursorPaint);
            }
            else
            {
                float half = _brushSize * _zoom / 2f;
                canvas.DrawRect(centerX - half + 1, centerY - half + 1, _brushSize * _zoom, _brushSize * _zoom, shadowPaint);
                canvas.DrawRect(centerX - half, centerY - half, _brushSize * _zoom, _brushSize * _zoom, cursorPaint);
            }
        }

        private byte[] ApplyOffset(byte[] source, int offsetX, int offsetY)
        {
            var result = new byte[source.Length];
            int w = _tileWidth;
            int h = _tileHeight;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcX = ((x - offsetX) % w + w) % w;
                    int srcY = ((y - offsetY) % h + h) % h;

                    int srcIdx = (srcY * w + srcX) * 4;
                    int dstIdx = (y * w + x) * 4;

                    result[dstIdx] = source[srcIdx];
                    result[dstIdx + 1] = source[srcIdx + 1];
                    result[dstIdx + 2] = source[srcIdx + 2];
                    result[dstIdx + 3] = source[srcIdx + 3];
                }
            }

            return result;
        }

        // ====================================================================
        // EVENT HANDLERS
        // ====================================================================

        private void GridSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridSizeCombo == null || !_initialized) return;
            _gridSize = GridSizeCombo.SelectedIndex + 2;
            UpdateCanvasSize();
            TessellationCanvas?.Invalidate();
        }

        private void ShowGridToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowGridToggle == null || !_initialized) return;
            _showGrid = ShowGridToggle.IsChecked == true;
            TessellationCanvas?.Invalidate();
        }

        private void ZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (ZoomSlider == null || !_initialized) return;
            _zoom = (int)Math.Round(ZoomSlider.Value);
            if (ZoomLabel != null)
                ZoomLabel.Text = $"{_zoom}x";
            UpdateCanvasSize();
            TessellationCanvas?.Invalidate();
        }

        private void OffsetXSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (OffsetXSlider == null || OffsetXBox == null || !_initialized) return;
            if (_updatingControls) return;
            _offsetX = (int)OffsetXSlider.Value;

            _updatingControls = true;
            OffsetXBox.Value = _offsetX;
            _updatingControls = false;

            TessellationCanvas?.Invalidate();
            PushLiveUpdate();
        }

        private void OffsetYSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (OffsetYSlider == null || OffsetYBox == null || !_initialized) return;
            if (_updatingControls) return;
            _offsetY = (int)OffsetYSlider.Value;

            _updatingControls = true;
            OffsetYBox.Value = _offsetY;
            _updatingControls = false;

            TessellationCanvas?.Invalidate();
            PushLiveUpdate();
        }

        private void OffsetXBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (OffsetXSlider == null || !_initialized) return;
            if (_updatingControls || double.IsNaN(args.NewValue)) return;
            _offsetX = (int)args.NewValue;

            _updatingControls = true;
            OffsetXSlider.Value = _offsetX;
            _updatingControls = false;

            TessellationCanvas?.Invalidate();
            PushLiveUpdate();
        }

        private void OffsetYBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (OffsetYSlider == null || !_initialized) return;
            if (_updatingControls || double.IsNaN(args.NewValue)) return;
            _offsetY = (int)args.NewValue;

            _updatingControls = true;
            OffsetYSlider.Value = _offsetY;
            _updatingControls = false;

            TessellationCanvas?.Invalidate();
            PushLiveUpdate();
        }

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            RevertToOriginal();
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            CommitToTileSet();

            if (_workingPixels != null && _originalPixels != null)
            {
                byte[] finalPixels = ApplyOffset(_workingPixels, _offsetX, _offsetY);
                Buffer.BlockCopy(finalPixels, 0, _originalPixels, 0, finalPixels.Length);
                Buffer.BlockCopy(finalPixels, 0, _workingPixels, 0, finalPixels.Length);
            }

            _offsetX = 0;
            _offsetY = 0;

            _updatingControls = true;
            if (OffsetXSlider != null) OffsetXSlider.Value = 0;
            if (OffsetYSlider != null) OffsetYSlider.Value = 0;
            if (OffsetXBox != null) OffsetXBox.Value = 0;
            if (OffsetYBox != null) OffsetYBox.Value = 0;
            _updatingControls = false;

            TessellationCanvas?.Invalidate();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
