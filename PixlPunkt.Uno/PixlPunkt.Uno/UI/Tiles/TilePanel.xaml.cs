using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Palette;
using PixlPunkt.Uno.Core.Tile;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.UI.Dialogs;
using PixlPunkt.Uno.UI.Helpers;
using PixlPunkt.Uno.UI.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.UI;

namespace PixlPunkt.Uno.UI.Tiles
{
    /// <summary>
    /// Tile panel for managing the document's tile set.
    /// </summary>
    public sealed partial class TilePanel : UserControl, INotifyPropertyChanged
    {
        private CanvasDocument? _document;
        private ToolState? _toolState;
        private PaletteService? _palette;
        private int _selectedTileId = -1;
        private double _zoomLevel = 3.0;
        private readonly PatternBackgroundService _pattern = new();

        // Cached checkerboard pattern for SkiaSharp
        private SKShader? _checkerboardShader;
        private SKBitmap? _checkerboardBitmap;

        /// <summary>
        /// Provider for accessing all open documents (set by main window).
        /// </summary>
        public Func<IEnumerable<CanvasDocument>>? OpenDocumentsProvider { get; set; }

        /// <summary>
        /// Observable collection of tile IDs for the grid.
        /// </summary>
        public ObservableCollection<int> TileIds { get; } = new();

        /// <summary>
        /// Gets the cell size for tile display (tile size * zoom).
        /// </summary>
        public double TileCellSize
        {
            get
            {
                int baseTileSize = _document?.TileSize.Width ?? 16;
                return Math.Max(32, baseTileSize * _zoomLevel);
            }
        }

        /// <summary>
        /// Gets or sets the selected tile ID.
        /// </summary>
        public int SelectedTileId
        {
            get => _selectedTileId;
            set
            {
                if (_selectedTileId == value) return;
                _selectedTileId = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTileId)));

                // Update tool state
                _toolState?.TileStamper.SetSelectedTileId(value);

                // Refresh selection visuals
                RefreshSelectionRings();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Occurs when the tile tessellator should be opened.
        /// </summary>
        public event Action<int>? TessellatorRequested;

        public TilePanel()
        {
            InitializeComponent();
            TileGrid.ItemsSource = TileIds;

            // Subscribe to stripe color changes
            Loaded += (_, __) =>
            {
                ApplyStripeColors();
                TransparencyStripeMixer.ColorsChanged += OnStripeColorsChanged;
            };
            Unloaded += (_, __) =>
            {
                TransparencyStripeMixer.ColorsChanged -= OnStripeColorsChanged;
                _checkerboardShader?.Dispose();
                _checkerboardBitmap?.Dispose();
            };
        }

        private void OnStripeColorsChanged()
        {
            ApplyStripeColors();
            _pattern.Invalidate();
            InvalidateCheckerboardCache();
            RefreshAllTileVisuals();
        }

        private void ApplyStripeColors()
        {
            var light = Color.FromArgb(255, TransparencyStripeMixer.LightR, TransparencyStripeMixer.LightG, TransparencyStripeMixer.LightB);
            var dark = Color.FromArgb(255, TransparencyStripeMixer.DarkR, TransparencyStripeMixer.DarkG, TransparencyStripeMixer.DarkB);

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

        /// <summary>
        /// Binds the panel to a document and tool state.
        /// </summary>
        public void Bind(CanvasDocument? document, ToolState? toolState, PaletteService? palette = null)
        {
            // Unbind previous
            if (_document != null)
            {
                _document.TileSetChanged -= OnTileSetChanged;
            }

            _document = document;
            _toolState = toolState;
            _palette = palette;

            // Bind new
            if (_document != null)
            {
                _document.TileSetChanged += OnTileSetChanged;
            }

            RefreshTileList();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TileCellSize)));
        }

        /// <summary>
        /// Public method to refresh tiles externally.
        /// </summary>
        public void RefreshTiles()
        {
            RefreshTileList();
            RefreshAllTileVisuals();
        }

        /// <summary>
        /// Refreshes the tile list from the document's tile set.
        /// </summary>
        public void RefreshTileList()
        {
            TileIds.Clear();

            if (_document?.TileSet == null) return;

            foreach (var tileId in _document.TileSet.TileIds)
                TileIds.Add(tileId);

            // Select first tile if none selected
            if (_selectedTileId < 0 && TileIds.Count > 0)
                SelectedTileId = TileIds[0];
        }

        private void OnTileSetChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshTileList();
                RefreshAllTileVisuals();
            });
        }

        //////////////////////////////////////////////////////////////////
        // TOOLBAR BUTTON HANDLERS
        //////////////////////////////////////////////////////////////////

        private void NewEmptyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_document?.TileSet == null) return;
            int newId = _document.TileSet.AddEmptyTile();
            SelectedTileId = newId;
        }

        private void DuplicateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_document?.TileSet == null || _selectedTileId < 0) return;
            int newId = _document.TileSet.DuplicateTile(_selectedTileId);
            if (newId >= 0) SelectedTileId = newId;
        }

        private async void ImportTilesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_document?.TileSet == null) return;

            var allDocs = OpenDocumentsProvider?.Invoke() ?? Enumerable.Empty<CanvasDocument>();
            var sourceDocs = allDocs.Where(d => d != _document && d.TileSet != null && d.TileSet.Count > 0).ToList();

            if (sourceDocs.Count == 0)
            {
                await new ContentDialog { XamlRoot = XamlRoot, Title = "No Source Documents", Content = "There are no other open documents with tiles to import from.", CloseButtonText = "OK" }.ShowAsync();
                return;
            }

            var dialog = new ImportTilesDialog(_document, allDocs) { XamlRoot = XamlRoot };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var selectedTiles = dialog.GetSelectedTiles();
            if (selectedTiles.Count == 0) return;

            int importedCount = 0, skippedCount = 0;
            var targetTileSet = _document.TileSet;
            var sourceTileSet = dialog.SourceTileSet;
            bool needsScale = sourceTileSet != null && (sourceTileSet.TileWidth != targetTileSet.TileWidth || sourceTileSet.TileHeight != targetTileSet.TileHeight);

            foreach (var tile in selectedTiles)
            {
                byte[] pixelsToImport = tile.Pixels;
                if (needsScale)
                    pixelsToImport = ScaleTilePixels(tile.Pixels, tile.Width, tile.Height, targetTileSet.TileWidth, targetTileSet.TileHeight);

                if (dialog.SkipDuplicates && IsDuplicateTile(pixelsToImport, targetTileSet)) { skippedCount++; continue; }

                targetTileSet.AddTile(pixelsToImport);
                importedCount++;
            }

            if (importedCount > 0 && TileIds.Count > 0)
                SelectedTileId = TileIds[TileIds.Count - 1];

            if (skippedCount > 0)
                await new ContentDialog { XamlRoot = XamlRoot, Title = "Import Complete", Content = $"Imported {importedCount} tile(s).\nSkipped {skippedCount} duplicate tile(s).", CloseButtonText = "OK" }.ShowAsync();
        }

        private static byte[] ScaleTilePixels(byte[] source, int srcW, int srcH, int dstW, int dstH)
        {
            var result = new byte[dstW * dstH * 4];
            for (int y = 0; y < dstH; y++)
            {
                int srcY = y * srcH / dstH;
                for (int x = 0; x < dstW; x++)
                {
                    int srcX = x * srcW / dstW;
                    int srcIdx = (srcY * srcW + srcX) * 4;
                    int dstIdx = (y * dstW + x) * 4;
                    result[dstIdx] = source[srcIdx];
                    result[dstIdx + 1] = source[srcIdx + 1];
                    result[dstIdx + 2] = source[srcIdx + 2];
                    result[dstIdx + 3] = source[srcIdx + 3];
                }
            }
            return result;
        }

        private static bool IsDuplicateTile(byte[] pixels, TileSet tileSet)
        {
            foreach (var existingTile in tileSet.Tiles)
            {
                if (existingTile.Pixels.Length != pixels.Length) continue;
                bool match = true;
                for (int i = 0; i < pixels.Length; i++)
                    if (existingTile.Pixels[i] != pixels[i]) { match = false; break; }
                if (match) return true;
            }
            return false;
        }

        private void TessellatorBtn_Click(object sender, RoutedEventArgs e) => OpenTessellationWindow();

        private void OpenTessellationWindow()
        {
            if (_document?.TileSet == null || _selectedTileId < 0) return;
            var tile = _document.TileSet.GetTile(_selectedTileId);
            if (tile == null) return;

            var win = new TileTessellationWindow();
            win.BindTile(_document.TileSet, _selectedTileId, _palette, _toolState, _document);
            win.Activate();

            var appW = WindowHost.ApplyChrome(win, resizable: true, alwaysOnTop: false, minimizable: true,
                title: $"Tile Tessellator - Tile {_selectedTileId}", owner: App.PixlPunktMainWindow);
            WindowHost.Place(appW, Core.Enums.WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel = Math.Min(8.0, _zoomLevel * 1.25);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TileCellSize)));
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel = Math.Max(1.0, _zoomLevel / 1.25);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TileCellSize)));
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_document?.TileSet == null || _selectedTileId < 0) return;
            int deletedId = _selectedTileId;
            bool removed = _document.TileSet.RemoveTile(deletedId);
            if (removed) SelectedTileId = TileIds.Count > 0 ? TileIds[0] : -1;
        }

        //////////////////////////////////////////////////////////////////
        // TILE CELL EVENT HANDLERS
        //////////////////////////////////////////////////////////////////

        private void TileCell_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid grid || grid.DataContext is not int tileId) return;
            UpdateSelectionRing(grid, tileId);
            var canvas = grid.FindName("TileCanvas") as SKXamlCanvas;
            canvas?.Invalidate();
        }

        private void TileCell_Unloaded(object sender, RoutedEventArgs e) { }

        private void TileCell_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not int tileId) return;
            SelectedTileId = tileId;
        }

        private void TileCell_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not int tileId) return;
            _selectedTileId = tileId;
            OpenTessellationWindow();
        }

        //////////////////////////////////////////////////////////////////
        // SKIA SHARP TILE RENDERING
        //////////////////////////////////////////////////////////////////

        private void TileCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            if (sender is not SKXamlCanvas skCanvas) return;

            var canvas = e.Surface.Canvas;
            float w = (float)skCanvas.ActualWidth;
            float h = (float)skCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // Get tile ID from parent grid's DataContext
            int? tileId = null;
            if (skCanvas.Parent is Grid grid && grid.DataContext is int id)
                tileId = id;

            // Draw transparency pattern background
            DrawPatternBackground(canvas, w, h);

            if (tileId == null || _document?.TileSet == null) return;

            var tile = _document.TileSet.GetTile(tileId.Value);
            if (tile?.Pixels == null || tile.Pixels.Length == 0) return;

            // Create bitmap from tile pixels
            using var bitmap = new SKBitmap(tile.Width, tile.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            System.Runtime.InteropServices.Marshal.Copy(tile.Pixels, 0, bitmap.GetPixels(), tile.Pixels.Length);

            // Calculate destination rect (centered, scaled uniformly)
            float tileW = tile.Width;
            float tileH = tile.Height;
            float scale = Math.Min(w / tileW, h / tileH);
            float destW = tileW * scale;
            float destH = tileH * scale;
            float destX = (w - destW) / 2;
            float destY = (h - destH) / 2;

            var destRect = new SKRect(destX, destY, destX + destW, destY + destH);
            var srcRect = new SKRect(0, 0, tileW, tileH);

            using var paint = new SKPaint { FilterQuality = SKFilterQuality.None, IsAntialias = false };
            canvas.DrawBitmap(bitmap, srcRect, destRect, paint);
        }

        private void DrawPatternBackground(SKCanvas canvas, float w, float h)
        {
            var (lightColor, darkColor) = _pattern.CurrentScheme;

            int squareSize = 4;
            EnsureCheckerboardShader(squareSize, lightColor, darkColor);

            if (_checkerboardShader != null)
            {
                using var paint = new SKPaint { Shader = _checkerboardShader, IsAntialias = false };
                canvas.DrawRect(0, 0, w, h, paint);
            }
            else
            {
                canvas.Clear(new SKColor(lightColor.R, lightColor.G, lightColor.B, lightColor.A));
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

        //////////////////////////////////////////////////////////////////
        // VISUAL HELPERS
        //////////////////////////////////////////////////////////////////

        private void UpdateSelectionRing(Grid grid, int tileId)
        {
            var ring = grid.FindName("SelectionRing") as Border;
            if (ring != null)
                ring.Visibility = tileId == _selectedTileId ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshSelectionRings()
        {
            // Walk through visible items and update selection visuals
            foreach (var item in TileGrid.Items)
            {
                if (item is int tileId)
                {
                    var container = TileGrid.ContainerFromItem(item);
                    if (container is ContentPresenter presenter &&
                        VisualTreeHelper.GetChildrenCount(presenter) > 0 &&
                        VisualTreeHelper.GetChild(presenter, 0) is Grid grid)
                    {
                        UpdateSelectionRing(grid, tileId);
                    }
                }
            }
        }

        /// <summary>
        /// Refreshes visuals for all tile cells.
        /// </summary>
        private void RefreshAllTileVisuals()
        {
            foreach (var item in TileGrid.Items)
            {
                if (item is int tileId)
                {
                    var container = TileGrid.ContainerFromItem(item);
                    if (container is ContentPresenter presenter &&
                        VisualTreeHelper.GetChildrenCount(presenter) > 0 &&
                        VisualTreeHelper.GetChild(presenter, 0) is Grid grid)
                    {
                        UpdateSelectionRing(grid, tileId);
                        var canvas = grid.FindName("TileCanvas") as SKXamlCanvas;
                        canvas?.Invalidate();
                    }
                }
            }
        }
    }
}
