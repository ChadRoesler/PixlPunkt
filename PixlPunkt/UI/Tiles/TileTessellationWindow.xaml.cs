using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Tile;
using PixlPunkt.Core.Tools;
using PixlPunkt.UI.Rendering;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.System;
using Windows.UI;

namespace PixlPunkt.UI.Tiles
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

        // ====================================================================
        // CONSTRUCTION
        // ====================================================================

        public TileTessellationWindow()
        {
            InitializeComponent();

            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(600, 550));

            if (Content is FrameworkElement root)
            {
                root.Loaded += Root_Loaded;
                root.KeyDown += Root_KeyDown;
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
                // Apply reverse offset to get working pixels
                // (tile set stores the "committed" state, working pixels have offset applied)
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

            // Get the current tile state (after the stroke)
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
        /// <param name="tileSet">The tile set containing the tile.</param>
        /// <param name="tileId">The ID of the tile to edit.</param>
        /// <param name="palette">The palette service for colors.</param>
        /// <param name="toolState">The tool state for brush settings.</param>
        /// <param name="document">The document for history support.</param>
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

        /// <summary>
        /// Converts screen position to canvas coordinates.
        /// </summary>
        private (int canvasX, int canvasY) ScreenToCanvas(Point screenPos)
        {
            int canvasX = (int)Math.Floor(screenPos.X / _zoom);
            int canvasY = (int)Math.Floor(screenPos.Y / _zoom);
            return (canvasX, canvasY);
        }

        /// <summary>
        /// Converts canvas coordinates to tile-local coordinates with wrap-around.
        /// </summary>
        private (int tileX, int tileY) CanvasToTileLocal(int canvasX, int canvasY)
        {
            // Apply offset and wrap to tile coordinates
            int tileX = ((canvasX - _offsetX) % _tileWidth + _tileWidth) % _tileWidth;
            int tileY = ((canvasY - _offsetY) % _tileHeight + _tileHeight) % _tileHeight;
            return (tileX, tileY);
        }

        /// <summary>
        /// Gets which tile cell (grid position) contains the canvas coordinates.
        /// </summary>
        private (int gridX, int gridY) CanvasToGridCell(int canvasX, int canvasY)
        {
            int gridX = canvasX / _tileWidth;
            int gridY = canvasY / _tileHeight;
            return (gridX, gridY);
        }

        // ====================================================================
        // TILE PIXEL PAINTING
        // ====================================================================

        /// <summary>
        /// Paints a single pixel at tile-local coordinates with wrap-around.
        /// </summary>
        private void PaintPixel(int tileX, int tileY, uint color)
        {
            if (_workingPixels == null) return;

            // Wrap coordinates
            int x = ((tileX % _tileWidth) + _tileWidth) % _tileWidth;
            int y = ((tileY % _tileHeight) + _tileHeight) % _tileHeight;

            int idx = (y * _tileWidth + x) * 4;
            if (idx < 0 || idx + 3 >= _workingPixels.Length) return;

            // Extract BGRA components
            byte b = (byte)(color & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte a = (byte)((color >> 24) & 0xFF);

            // Alpha blend with existing pixel
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

        /// <summary>
        /// Stamps the brush at the given tile-local coordinates.
        /// </summary>
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
                        : true; // Square

                    if (inBrush)
                    {
                        // Calculate alpha falloff based on density
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

        /// <summary>
        /// Calculates brush alpha at offset based on density falloff.
        /// </summary>
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
            double falloff = 1.0 - (t * t) * (3 - 2 * t); // Smoothstep

            return (byte)Math.Round(_brushOpacity * falloff);
        }

        /// <summary>
        /// Draws a line between two tile-local positions using Bresenham.
        /// </summary>
        private void StampLine(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                StampBrush(x0, y0);

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        /// <summary>
        /// Draws a line between two canvas positions using Bresenham.
        /// Each point is converted to tile-local coordinates with wrap-around,
        /// allowing seamless drawing across tile boundaries.
        /// </summary>
        private void StampLineCanvas(int canvasX0, int canvasY0, int canvasX1, int canvasY1)
        {
            int dx = Math.Abs(canvasX1 - canvasX0);
            int dy = Math.Abs(canvasY1 - canvasY0);
            int sx = canvasX0 < canvasX1 ? 1 : -1;
            int sy = canvasY0 < canvasY1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // Convert canvas position to tile-local coordinates (with wrap)
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

            // Validate canvas position is within bounds
            int canvasWidth = _tileWidth * _gridSize;
            int canvasHeight = _tileHeight * _gridSize;
            if (canvasX < 0 || canvasX >= canvasWidth || canvasY < 0 || canvasY >= canvasHeight)
                return;

            var (tileX, tileY) = CanvasToTileLocal(canvasX, canvasY);

            // Capture before state for history at stroke start
            var currentTilePixels = _tileSet.GetTilePixels(_tileId);
            if (currentTilePixels != null)
            {
                _strokeBeforePixels = (byte[])currentTilePixels.Clone();
            }

            _isPainting = true;
            _hasLastPos = true;
            _lastTileX = tileX;
            _lastTileY = tileY;
            // Also track the last canvas position for proper line interpolation
            _lastCanvasX = canvasX;
            _lastCanvasY = canvasY;

            StampBrush(tileX, tileY);

            TessellationCanvas.CapturePointer(e.Pointer);
            TessellationCanvas.Invalidate();

            // Live update - push changes immediately to TileSet for real-time preview
            PushLiveUpdate();
        }

        private void TessellationCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(TessellationCanvas);
            var (canvasX, canvasY) = ScreenToCanvas(pt.Position);

            // Update hover position for cursor preview
            _hoverCanvasX = canvasX;
            _hoverCanvasY = canvasY;

            // Validate hover is within canvas bounds
            int canvasWidth = _tileWidth * _gridSize;
            int canvasHeight = _tileHeight * _gridSize;
            _hoverValid = canvasX >= 0 && canvasX < canvasWidth &&
                          canvasY >= 0 && canvasY < canvasHeight;

            if (_isPainting && _workingPixels != null && pt.Properties.IsLeftButtonPressed)
            {
                // Clamp canvas coordinates to valid range for line drawing
                int clampedCanvasX = Math.Clamp(canvasX, 0, canvasWidth - 1);
                int clampedCanvasY = Math.Clamp(canvasY, 0, canvasHeight - 1);

                if (_hasLastPos)
                {
                    // Draw line in canvas coordinates to handle tile boundary crossings properly
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

                // Live update - push changes during drag for real-time preview
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

                // Final live update to ensure tile is in sync
                PushLiveUpdate();

                // Push to history now that stroke is complete
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

        /// <summary>
        /// Commits the working pixels to the TileSet.
        /// </summary>
        private void CommitToTileSet()
        {
            if (_tileSet == null || _tileId < 0 || _workingPixels == null) return;

            // Apply current offset before committing
            byte[] finalPixels = ApplyOffset(_workingPixels, _offsetX, _offsetY);
            _tileSet.UpdateTilePixels(_tileId, finalPixels);
        }

        /// <summary>
        /// Pushes the current working pixels to the TileSet for live preview.
        /// This triggers TileSetChanged event which updates TilePanel and main canvas.
        /// </summary>
        private void PushLiveUpdate()
        {
            if (_tileSet == null || _tileId < 0 || _workingPixels == null)
                return;

            // Apply current offset before pushing
            byte[] previewPixels = ApplyOffset(_workingPixels, _offsetX, _offsetY);
            _tileSet.UpdateTilePixels(_tileId, previewPixels);
        }

        /// <summary>
        /// Reverts to the original tile pixels.
        /// </summary>
        private void RevertToOriginal()
        {
            if (_originalPixels == null || _workingPixels == null || _tileSet == null) return;

            // Capture current state before revert for history
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

            // Push revert to tile set
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

        private void TessellationCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_workingPixels == null || _tileWidth <= 0 || _tileHeight <= 0)
                return;

            var ds = args.DrawingSession;
            ds.Antialiasing = CanvasAntialiasing.Aliased;

            int canvasWidth = _tileWidth * _gridSize * _zoom;
            int canvasHeight = _tileHeight * _gridSize * _zoom;

            // Draw transparency stripe background
            double dpi = sender.XamlRoot?.RasterizationScale ?? 1.0;
            var bgImg = _pattern.GetSizedImage(sender.Device, dpi, canvasWidth, canvasHeight);
            var bgSrc = new Rect(0, 0, bgImg.SizeInPixels.Width, bgImg.SizeInPixels.Height);
            var bgDst = new Rect(0, 0, canvasWidth, canvasHeight);
            ds.DrawImage(bgImg, bgDst, bgSrc);

            // Apply offset to get preview pixels
            byte[] previewPixels = ApplyOffset(_workingPixels, _offsetX, _offsetY);

            // Create bitmap from working pixels
            using var tileBitmap = CanvasBitmap.CreateFromBytes(
                sender.Device,
                previewPixels,
                _tileWidth,
                _tileHeight,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96.0f);

            // Render grid of tiles
            for (int gy = 0; gy < _gridSize; gy++)
            {
                for (int gx = 0; gx < _gridSize; gx++)
                {
                    float destX = gx * _tileWidth * _zoom;
                    float destY = gy * _tileHeight * _zoom;
                    float destW = _tileWidth * _zoom;
                    float destH = _tileHeight * _zoom;

                    var destRect = new Rect(destX, destY, destW, destH);
                    var srcRect = new Rect(0, 0, _tileWidth, _tileHeight);

                    ds.DrawImage(
                        tileBitmap,
                        destRect,
                        srcRect,
                        1.0f,
                        CanvasImageInterpolation.NearestNeighbor);
                }
            }

            // Draw grid lines if enabled
            if (_showGrid)
            {
                var gridColor = Color.FromArgb(128, 255, 255, 255);

                for (int gx = 1; gx < _gridSize; gx++)
                {
                    float x = gx * _tileWidth * _zoom;
                    ds.DrawLine(x, 0, x, canvasHeight, gridColor, 1f);
                }

                for (int gy = 1; gy < _gridSize; gy++)
                {
                    float y = gy * _tileHeight * _zoom;
                    ds.DrawLine(0, y, canvasWidth, y, gridColor, 1f);
                }

                // Highlight center tile (editable area)
                int centerTileX = _gridSize / 2;
                int centerTileY = _gridSize / 2;
                float highlightX = centerTileX * _tileWidth * _zoom;
                float highlightY = centerTileY * _tileHeight * _zoom;
                float highlightW = _tileWidth * _zoom;
                float highlightH = _tileHeight * _zoom;

                var highlightColor = Color.FromArgb(200, 0, 160, 255);
                ds.DrawRectangle(highlightX, highlightY, highlightW, highlightH, highlightColor, 2f);
            }

            // Draw brush cursor overlay
            if (_hoverValid)
            {
                DrawBrushCursor(ds);
            }
        }

        /// <summary>
        /// Draws the brush cursor preview at the hover position.
        /// </summary>
        private void DrawBrushCursor(CanvasDrawingSession ds)
        {
            float centerX = _hoverCanvasX * _zoom + _zoom / 2f;
            float centerY = _hoverCanvasY * _zoom + _zoom / 2f;
            float radius = _brushSize * _zoom / 2f;

            var cursorColor = Color.FromArgb(200, 255, 255, 255);
            var shadowColor = Color.FromArgb(128, 0, 0, 0);

            if (_brushShape == BrushShape.Circle)
            {
                // Shadow
                ds.DrawEllipse(centerX + 1, centerY + 1, radius, radius, shadowColor, 1f);
                // Cursor
                ds.DrawEllipse(centerX, centerY, radius, radius, cursorColor, 1f);
            }
            else
            {
                float half = _brushSize * _zoom / 2f;
                // Shadow
                ds.DrawRectangle(centerX - half + 1, centerY - half + 1, _brushSize * _zoom, _brushSize * _zoom, shadowColor, 1f);
                // Cursor
                ds.DrawRectangle(centerX - half, centerY - half, _brushSize * _zoom, _brushSize * _zoom, cursorColor, 1f);
            }
        }

        /// <summary>
        /// Applies pixel offset with wrap-around.
        /// </summary>
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

            // Live update for offset changes
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

            // Live update for offset changes
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

            // Live update for offset changes
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

            // Live update for offset changes
            PushLiveUpdate();
        }

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            RevertToOriginal();
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            CommitToTileSet();

            // Update original to new state so revert goes to this point
            if (_workingPixels != null && _originalPixels != null)
            {
                byte[] finalPixels = ApplyOffset(_workingPixels, _offsetX, _offsetY);
                Buffer.BlockCopy(finalPixels, 0, _originalPixels, 0, finalPixels.Length);
                Buffer.BlockCopy(finalPixels, 0, _workingPixels, 0, finalPixels.Length);
            }

            // Reset offset to 0 (offset is now baked in)
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
