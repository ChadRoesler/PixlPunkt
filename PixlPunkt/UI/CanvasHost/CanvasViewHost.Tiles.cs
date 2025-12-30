using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Tile;
using PixlPunkt.Core.Tools;
using PixlPunkt.Core.Tools.Tile;
using PixlPunkt.PluginSdk.Tile;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// Tile tool subsystem for CanvasViewHost:
    /// - Tile handler management
    /// - ITileContext implementation
    /// - Tile overlay preview state
    /// - Tile mapping propagation after painting
    /// - Live update when tiles are modified externally (e.g., tessellation window)
    /// </summary>
    public sealed partial class CanvasViewHost : ITileContext, ITileAnimationContext, ITileModifierHistoryContext
    {
        // ====================================================================
        // TILE HANDLER STATE
        // ====================================================================

        private readonly Dictionary<string, ITileHandler> _tileHandlers = new();
        private ITileHandler? _activeTileHandler;
        private TileOverlayPreview? _currentTileOverlay;
        private int _selectedTileId = -1;
        private bool _tileSetHooked = false;

        // ====================================================================
        // TILE HANDLER INITIALIZATION
        // ====================================================================

        /// <summary>
        /// Initializes built-in tile handlers and hooks TileSet events.
        /// </summary>
        private void InitializeTileHandlers()
        {
            if (_toolState == null) return;

            // Create Tile Stamper handler
            var stamperHandler = new TileStamperHandler(this, _toolState.TileStamper);
            _tileHandlers[ToolIds.TileStamper] = stamperHandler;

            // Create Tile Modifier handler
            var modifierHandler = new TileModifierHandler(this, _toolState.TileModifier);
            _tileHandlers[ToolIds.TileModifier] = modifierHandler;

            // Create Tile Animation handler
            var animationHandler = new TileAnimationHandler(
                this,
                _toolState.TileAnimation,
                () => Document?.TileAnimationState);
            _tileHandlers[ToolIds.TileAnimation] = animationHandler;

            // Hook TileSet events for live updates
            HookTileSetEvents();
        }

        /// <summary>
        /// Hooks the TileSet events for live updates when tiles are modified externally.
        /// </summary>
        private void HookTileSetEvents()
        {
            if (_tileSetHooked || Document?.TileSet == null) return;

            Document.TileSet.TileUpdated += OnTileUpdated;
            _tileSetHooked = true;
        }

        /// <summary>
        /// Unhooks TileSet events (call on disposal).
        /// </summary>
        private void UnhookTileSetEvents()
        {
            if (!_tileSetHooked || Document?.TileSet == null) return;

            Document.TileSet.TileUpdated -= OnTileUpdated;
            _tileSetHooked = false;
        }

        /// <summary>
        /// Called when a tile's pixels are updated (e.g., from tessellation window).
        /// Refreshes all mapped instances of this tile on all layers.
        /// </summary>
        private void OnTileUpdated(TileDefinition tile)
        {
            RefreshMappedTilesForId(tile.Id);
        }

        /// <summary>
        /// Refreshes all canvas positions that have a mapping to the specified tile ID.
        /// </summary>
        public void RefreshMappedTilesForId(int tileId)
        {
            var tileSet = Document?.TileSet;
            if (tileSet == null) return;

            var tilePixels = tileSet.GetTilePixels(tileId);
            if (tilePixels == null) return;

            int tileW = tileSet.TileWidth;
            int tileH = tileSet.TileHeight;

            bool anyUpdated = false;

            // Update all layers that have mappings for this tile
            foreach (var layer in Document!.GetAllRasterLayers())
            {
                var mapping = layer.TileMapping;
                if (mapping == null) continue;

                for (int ty = 0; ty < mapping.Height; ty++)
                {
                    for (int tx = 0; tx < mapping.Width; tx++)
                    {
                        if (mapping.GetTileId(tx, ty) == tileId)
                        {
                            int docX = tx * tileW;
                            int docY = ty * tileH;
                            WriteLayerRectForLayer(layer, docX, docY, tileW, tileH, tilePixels);
                            anyUpdated = true;
                        }
                    }
                }
            }

            if (anyUpdated)
            {
                // Refresh display
                Document.CompositeTo(Document.Surface);
                UpdateActiveLayerPreview();
                CanvasView.Invalidate();
            }
        }

        /// <summary>
        /// Writes pixels to a specific layer's rectangle without triggering display refresh.
        /// </summary>
        private void WriteLayerRectForLayer(RasterLayer layer, int x, int y, int width, int height, byte[] pixels)
        {
            var dstPixels = layer.Surface.Pixels;
            int dstStride = layer.Surface.Width * 4;

            for (int row = 0; row < height; row++)
            {
                int dstY = y + row;
                if (dstY < 0 || dstY >= layer.Surface.Height) continue;

                int srcOffset = row * width * 4;
                int dstOffset = dstY * dstStride + x * 4;

                int copyWidth = width;

                // Clamp to layer bounds
                if (x < 0)
                {
                    int startX = -x;
                    copyWidth -= startX;
                    srcOffset = row * width * 4 + startX * 4;
                    dstOffset = dstY * dstStride;
                }
                if (x + width > layer.Surface.Width)
                {
                    copyWidth = Math.Max(0, layer.Surface.Width - Math.Max(0, x));
                }

                if (copyWidth > 0 && dstOffset >= 0 && dstOffset + copyWidth * 4 <= dstPixels.Length)
                {
                    Buffer.BlockCopy(pixels, srcOffset, dstPixels, dstOffset, copyWidth * 4);
                }
            }
        }

        // ====================================================================
        // TILE HANDLER LOOKUP
        // ====================================================================

        /// <summary>
        /// Gets the tile handler for a specific tool ID.
        /// Checks built-in handlers first, then plugin handlers.
        /// </summary>
        private ITileHandler? GetTileHandler(string toolId)
        {
            // Check built-in handlers first
            if (_tileHandlers.TryGetValue(toolId, out var handler))
                return handler;

            // Check if it's a plugin tile tool and create/cache the handler
            var registration = ToolRegistry.Shared.GetById(toolId);
            if (registration is Core.Plugins.PluginTileToolRegistration pluginTileReg)
            {
                // Create handler if not cached
                if (!_tileHandlers.ContainsKey(toolId))
                {
                    var pluginHandler = pluginTileReg.CreateHandler(this);
                    if (pluginHandler != null)
                    {
                        _tileHandlers[toolId] = pluginHandler;
                        Core.Logging.LoggingService.Debug("Created plugin tile handler for tool: {ToolId}", toolId);
                        return pluginHandler;
                    }
                }
            }

            // Also check for Core ITileToolRegistration (in case of other adapters)
            if (registration is ITileToolRegistration tileReg && tileReg.HasHandler)
            {
                if (!_tileHandlers.ContainsKey(toolId))
                {
                    var tileHandler = tileReg.CreateHandler(this);
                    if (tileHandler != null)
                    {
                        _tileHandlers[toolId] = tileHandler;
                        Core.Logging.LoggingService.Debug("Created tile handler for tool: {ToolId}", toolId);
                        return tileHandler;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the tile handler for the current active tile tool.
        /// </summary>
        private ITileHandler? GetActiveTileHandler()
        {
            if (_toolState == null) return null;
            return GetTileHandler(_toolState.ActiveToolId);
        }

        // ====================================================================
        // TILE INPUT HANDLERS
        // ====================================================================

        private bool HandleTilePressed(PointerRoutedEventArgs e)
        {
            if (_toolState?.IsActiveTileTool != true)
            {
                Core.Logging.LoggingService.Debug("HandleTilePressed: Not an active tile tool. ActiveToolId={ToolId}", _toolState?.ActiveToolId ?? "null");
                return false;
            }

            var handler = GetActiveTileHandler();
            if (handler == null)
            {
                Core.Logging.LoggingService.Debug("HandleTilePressed: No handler found for tool {ToolId}", _toolState.ActiveToolId);
                return false;
            }

            Core.Logging.LoggingService.Debug("HandleTilePressed: Got handler {HandlerType} for tool {ToolId}", handler.GetType().Name, _toolState.ActiveToolId);

            var pt = e.GetCurrentPoint(CanvasView);
            var props = pt.Properties;
            var screenPos = pt.Position;
            var docPos = ScreenToDocPoint(screenPos);

            bool isLeft = props.IsLeftButtonPressed;
            bool isRight = props.IsRightButtonPressed;
            bool isShift = IsKeyDown(Windows.System.VirtualKey.Shift);
            bool isCtrl = IsKeyDown(Windows.System.VirtualKey.Control);
            bool isAlt = IsKeyDown(Windows.System.VirtualKey.Menu);

            int docX = (int)Math.Floor(docPos.X);
            int docY = (int)Math.Floor(docPos.Y);

            Core.Logging.LoggingService.Debug("HandleTilePressed: docX={DocX}, docY={DocY}, isLeft={IsLeft}, isRight={IsRight}", docX, docY, isLeft, isRight);

            // Handle Alt+LMB for tile stamper - remove mapping
            if (isAlt && isLeft && handler is TileStamperHandler stamper)
            {
                var (tileX, tileY) = ((ITileContext)this).DocToTile(docX, docY);
                int currentMapping = ((ITileContext)this).GetMappedTileId(tileX, tileY);
                if (currentMapping >= 0)
                {
                    ((ITileContext)this).BeginHistoryTransaction($"Remove tile mapping at ({tileX}, {tileY})");
                    ((ITileContext)this).ClearTileMapping(tileX, tileY);
                    ((ITileContext)this).CommitHistoryTransaction();
                    return true;
                }
                return false;
            }

            var result = handler.PointerPressed(screenPos.X, screenPos.Y, docX, docY,
                isLeft, isRight, isShift, isCtrl);

            Core.Logging.LoggingService.Debug("HandleTilePressed: handler.PointerPressed returned {Result}", result);

            if (result)
            {
                _activeTileHandler = handler;
                CanvasView.CapturePointer(e.Pointer);
                UpdateTileOverlay();
                return true;
            }

            return false;
        }

        private bool HandleTileMoved(PointerRoutedEventArgs e)
        {
            if (_toolState?.IsActiveTileTool != true)
                return false;

            var handler = GetActiveTileHandler();
            if (handler == null)
                return false;

            var pt = e.GetCurrentPoint(CanvasView);
            var props = pt.Properties;
            var screenPos = pt.Position;
            var docPos = ScreenToDocPoint(screenPos);

            bool isLeft = props.IsLeftButtonPressed;
            bool isRight = props.IsRightButtonPressed;
            bool isShift = IsKeyDown(Windows.System.VirtualKey.Shift);
            bool isCtrl = IsKeyDown(Windows.System.VirtualKey.Control);

            int docX = (int)Math.Floor(docPos.X);
            int docY = (int)Math.Floor(docPos.Y);

            handler.PointerMoved(screenPos.X, screenPos.Y, docX, docY,
                isLeft, isRight, isShift, isCtrl);

            UpdateTileOverlay();
            return _activeTileHandler?.IsActive == true;
        }

        private bool HandleTileReleased(PointerRoutedEventArgs e)
        {
            if (_activeTileHandler == null)
                return false;

            var pt = e.GetCurrentPoint(CanvasView);
            var props = pt.Properties;
            var screenPos = pt.Position;
            var docPos = ScreenToDocPoint(screenPos);

            bool isLeft = props.IsLeftButtonPressed;
            bool isRight = props.IsRightButtonPressed;
            bool isShift = IsKeyDown(Windows.System.VirtualKey.Shift);
            bool isCtrl = IsKeyDown(Windows.System.VirtualKey.Control);

            int docX = (int)Math.Floor(docPos.X);
            int docY = (int)Math.Floor(docPos.Y);

            _activeTileHandler.PointerReleased(screenPos.X, screenPos.Y, docX, docY,
                isLeft, isRight, isShift, isCtrl);

            _activeTileHandler = null;
            CanvasView.ReleasePointerCaptures();
            UpdateTileOverlay();
            return true;
        }

        /// <summary>
        /// Updates the current tile overlay preview from the active handler.
        /// </summary>
        private void UpdateTileOverlay()
        {
            var handler = GetActiveTileHandler();
            _currentTileOverlay = handler?.GetOverlayPreview();
            CanvasView.Invalidate();
        }

        // ====================================================================
        // TILE MAPPING PROPAGATION
        // ====================================================================

        /// <summary>
        /// Creates a tile-mapped history item for strokes that affect mapped tiles.
        /// Returns null if no tiles are affected (stroke is on unmapped area).
        /// </summary>
        /// <param name="minX">Minimum X of affected area.</param>
        /// <param name="minY">Minimum Y of affected area.</param>
        /// <param name="maxX">Maximum X of affected area.</param>
        /// <param name="maxY">Maximum Y of affected area.</param>
        /// <param name="description">Description for the history item.</param>
        /// <returns>A TileMappedPixelChangeItem if tiles are affected, null otherwise.</returns>
        private Core.History.TileMappedPixelChangeItem? CreateTileMappedHistoryItem(int minX, int minY, int maxX, int maxY, string description)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return null;

            var mapping = rl.TileMapping;
            if (mapping == null)
                return null;

            var tileSet = Document.TileSet;
            if (tileSet == null)
                return null;

            int tileW = tileSet.TileWidth;
            int tileH = tileSet.TileHeight;

            // Find all tiles that intersect the affected area
            int startTileX = Math.Max(0, minX / tileW);
            int startTileY = Math.Max(0, minY / tileH);
            int endTileX = Math.Min(mapping.Width - 1, maxX / tileW);
            int endTileY = Math.Min(mapping.Height - 1, maxY / tileH);

            // Group tile positions by tile ID to handle cross-tile painting
            var affectedTileIds = new HashSet<int>();

            for (int ty = startTileY; ty <= endTileY; ty++)
            {
                for (int tx = startTileX; tx <= endTileX; tx++)
                {
                    int tileId = mapping.GetTileId(tx, ty);
                    if (tileId >= 0)
                    {
                        affectedTileIds.Add(tileId);
                    }
                }
            }

            if (affectedTileIds.Count == 0)
                return null; // No tiles affected, use regular history

            // Create a tile-aware history item
            var tileMappedItem = new Core.History.TileMappedPixelChangeItem(rl, tileSet, description);

            // For each affected tile ID, capture before state, extract changes, and record
            foreach (var tileId in affectedTileIds)
            {
                // Get the current tile pixels BEFORE we extract from canvas
                byte[]? beforePixels = tileSet.GetTilePixels(tileId);
                if (beforePixels == null)
                    continue;

                // Clone the before state
                byte[] beforeClone = (byte[])beforePixels.Clone();

                // Start with the existing tile definition
                byte[] mergedPixels = (byte[])beforePixels.Clone();

                // Find all positions mapped to this tile and extract painted pixels
                var affectedPositions = new List<(int tx, int ty)>();
                for (int ty = startTileY; ty <= endTileY; ty++)
                {
                    for (int tx = startTileX; tx <= endTileX; tx++)
                    {
                        if (mapping.GetTileId(tx, ty) == tileId)
                        {
                            affectedPositions.Add((tx, ty));
                        }
                    }
                }

                // For each affected position, extract the painted pixels into the tile
                foreach (var (tx, ty) in affectedPositions)
                {
                    int tileDocX = tx * tileW;
                    int tileDocY = ty * tileH;

                    // Read the current layer pixels for this tile region
                    byte[] layerTilePixels = ReadLayerRectInternal(tileDocX, tileDocY, tileW, tileH);

                    // Merge the layer pixels into the tile definition
                    // (pixels painted in this region override the tile definition)
                    for (int py = tileDocY; py < tileDocY + tileH; py++)
                    {
                        for (int px = tileDocX; px < tileDocX + tileW; px++)
                        {
                            // Check if this pixel is within the affected stroke area
                            if (px < minX || px > maxX || py < minY || py > maxY)
                                continue;

                            int localX = px - tileDocX;
                            int localY = py - tileDocY;

                            int idx = (localY * tileW + localX) * 4;

                            // Copy from layer to merged tile
                            mergedPixels[idx] = layerTilePixels[idx];
                            mergedPixels[idx + 1] = layerTilePixels[idx + 1];
                            mergedPixels[idx + 2] = layerTilePixels[idx + 2];
                            mergedPixels[idx + 3] = layerTilePixels[idx + 3];
                        }
                    }
                }

                // Collect ALL positions mapped to this tile (not just affected ones)
                var allPositions = new List<(int tileX, int tileY)>();
                for (int ty = 0; ty < mapping.Height; ty++)
                {
                    for (int tx = 0; tx < mapping.Width; tx++)
                    {
                        if (mapping.GetTileId(tx, ty) == tileId)
                        {
                            allPositions.Add((tx, ty));
                        }
                    }
                }

                // Record this tile change in the history item
                tileMappedItem.RecordTileChange(tileId, beforeClone, mergedPixels, allPositions);

                // Update the tile definition in the tile set
                tileSet.UpdateTilePixels(tileId, mergedPixels);

                // Propagate to all mapped instances (including original)
                foreach (var (tx, ty) in allPositions)
                {
                    int dstDocX = tx * tileW;
                    int dstDocY = ty * tileH;
                    WriteLayerRectWithoutRefresh(dstDocX, dstDocY, tileW, tileH, mergedPixels);
                }
            }

            return tileMappedItem;
        }

        /// <summary>
        /// Reads pixels from a layer rectangle (internal version that doesn't go through ITileContext).
        /// </summary>
        private byte[] ReadLayerRectInternal(int x, int y, int width, int height)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return new byte[width * height * 4];

            var result = new byte[width * height * 4];
            var srcPixels = rl.Surface.Pixels;
            int srcStride = rl.Surface.Width * 4;

            for (int row = 0; row < height; row++)
            {
                int srcY = y + row;
                if (srcY < 0 || srcY >= rl.Surface.Height) continue;

                int dstOffset = row * width * 4;
                int srcOffset = srcY * srcStride + x * 4;

                int copyWidth = width;
                if (x < 0)
                {
                    int skip = -x;
                    copyWidth -= skip;
                    dstOffset += skip * 4;
                    srcOffset = srcY * srcStride;
                }
                if (x + width > rl.Surface.Width)
                {
                    copyWidth = Math.Max(0, rl.Surface.Width - Math.Max(0, x));
                }

                if (copyWidth > 0 && srcOffset >= 0 && srcOffset + copyWidth * 4 <= srcPixels.Length)
                {
                    Buffer.BlockCopy(srcPixels, srcOffset, result, dstOffset, copyWidth * 4);
                }
            }

            return result;
        }

        /// <summary>
        /// Writes pixels to a layer rectangle without triggering display refresh.
        /// Used for batch tile propagation.
        /// </summary>
        private void WriteLayerRectWithoutRefresh(int x, int y, int width, int height, byte[] pixels)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            var dstPixels = rl.Surface.Pixels;
            int dstStride = rl.Surface.Width * 4;

            for (int row = 0; row < height; row++)
            {
                int dstY = y + row;
                if (dstY < 0 || dstY >= rl.Surface.Height) continue;

                int srcOffset = row * width * 4;
                int dstOffset = dstY * dstStride + x * 4;

                int copyWidth = width;
                int startX = 0;

                // Clamp to layer bounds
                if (x < 0)
                {
                    startX = -x;
                    copyWidth -= startX;
                    srcOffset = row * width * 4 + startX * 4;
                    dstOffset = dstY * dstStride;
                }
                if (x + width > rl.Surface.Width)
                {
                    copyWidth = Math.Max(0, rl.Surface.Width - Math.Max(0, x));
                }

                if (copyWidth > 0 && dstOffset >= 0 && dstOffset + copyWidth * 4 <= dstPixels.Length)
                {
                    Buffer.BlockCopy(pixels, srcOffset, dstPixels, dstOffset, copyWidth * 4);
                }
            }
        }

        // ====================================================================
        // ITILECONTEXT IMPLEMENTATION - DOCUMENT GEOMETRY
        // ====================================================================

        int ITileContext.DocumentWidth => Document?.PixelWidth ?? 0;
        int ITileContext.DocumentHeight => Document?.PixelHeight ?? 0;
        int ITileContext.TileWidth => Document?.TileSet?.TileWidth ?? 16;
        int ITileContext.TileHeight => Document?.TileSet?.TileHeight ?? 16;

        int ITileContext.TileCountX
        {
            get
            {
                var tileSet = Document?.TileSet;
                if (tileSet == null) return 0;
                return (Document!.PixelWidth + tileSet.TileWidth - 1) / tileSet.TileWidth;
            }
        }

        int ITileContext.TileCountY
        {
            get
            {
                var tileSet = Document?.TileSet;
                if (tileSet == null) return 0;
                return (Document!.PixelHeight + tileSet.TileHeight - 1) / tileSet.TileHeight;
            }
        }

        // ====================================================================
        // ITILECONTEXT IMPLEMENTATION - COORDINATE CONVERSION
        // ====================================================================

        (int tileX, int tileY) ITileContext.DocToTile(int docX, int docY)
        {
            var tileSet = Document?.TileSet;
            if (tileSet == null) return (0, 0);

            int tileX = docX / tileSet.TileWidth;
            int tileY = docY / tileSet.TileHeight;
            return (tileX, tileY);
        }

        (int docX, int docY) ITileContext.TileToDoc(int tileX, int tileY)
        {
            var tileSet = Document?.TileSet;
            if (tileSet == null) return (0, 0);

            int docX = tileX * tileSet.TileWidth;
            int docY = tileY * tileSet.TileHeight;
            return (docX, docY);
        }

        (int x, int y, int width, int height) ITileContext.GetTileRect(int tileX, int tileY)
        {
            var tileSet = Document?.TileSet;
            if (tileSet == null) return (0, 0, 16, 16);

            int docX = tileX * tileSet.TileWidth;
            int docY = tileY * tileSet.TileHeight;
            return (docX, docY, tileSet.TileWidth, tileSet.TileHeight);
        }

        // ====================================================================
        // ITILECONTEXT IMPLEMENTATION - TILESET ACCESS
        // ====================================================================

        int ITileContext.SelectedTileId => _selectedTileId;
        int ITileContext.TileCount => Document?.TileSet?.Count ?? 0;

        IEnumerable<int> ITileContext.GetAllTileIds()
        {
            return Document?.TileSet?.TileIds ?? Array.Empty<int>();
        }

        int ITileContext.GetTileIdAtIndex(int index)
        {
            var tileSet = Document?.TileSet;
            if (tileSet == null) return -1;

            var ids = tileSet.TileIds.ToList();
            if (index < 0 || index >= ids.Count) return -1;
            return ids[index];
        }

        byte[]? ITileContext.GetTilePixels(int tileId)
        {
            return Document?.TileSet?.GetTilePixels(tileId);
        }

        int ITileContext.CreateTile(byte[] pixels)
        {
            return Document?.TileSet?.AddTile(pixels) ?? -1;
        }

        bool ITileContext.DeleteTile(int tileId)
        {
            return Document?.TileSet?.RemoveTile(tileId) ?? false;
        }

        int ITileContext.DuplicateTile(int tileId)
        {
            return Document?.TileSet?.DuplicateTile(tileId) ?? -1;
        }

        bool ITileContext.UpdateTilePixels(int tileId, byte[] pixels)
        {
            return Document?.TileSet?.UpdateTilePixels(tileId, pixels) ?? false;
        }

        // ====================================================================
        // ITILECONTEXT IMPLEMENTATION - TILE MAPPING
        // ====================================================================

        int ITileContext.GetMappedTileId(int tileX, int tileY)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return -1;

            var mapping = rl.TileMapping;
            if (mapping == null)
                return -1;

            return mapping.GetTileId(tileX, tileY);
        }

        void ITileContext.SetTileMapping(int tileX, int tileY, int tileId)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            var mapping = rl.GetOrCreateTileMapping(
                ((ITileContext)this).TileCountX,
                ((ITileContext)this).TileCountY);

            // If in a transaction, record mapping change for history
            if (_tileHistoryDescription != null)
            {
                _tileHistoryHasMapping = true;
                _tileHistoryMappingTileX = tileX;
                _tileHistoryMappingTileY = tileY;
                _tileHistoryMappingBefore = mapping.GetTileId(tileX, tileY);
                _tileHistoryMappingAfter = tileId;
            }

            mapping.SetTileId(tileX, tileY, tileId);
        }

        void ITileContext.ClearTileMapping(int tileX, int tileY)
        {
            ((ITileContext)this).SetTileMapping(tileX, tileY, -1);
        }

        // ====================================================================
        // ITILECONTEXT IMPLEMENTATION - LAYER PIXEL ACCESS
        // ====================================================================

        byte[] ITileContext.GetActiveLayerPixels()
        {
            if (Document?.ActiveLayer is RasterLayer rl)
                return rl.Surface.Pixels;
            return Array.Empty<byte>();
        }

        byte[] ITileContext.ReadLayerRect(int x, int y, int width, int height)
        {
            return ReadLayerRectInternal(x, y, width, height);
        }

        void ITileContext.WriteLayerRect(int x, int y, int width, int height, byte[] pixels)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            // If in a transaction, capture the before state (only on first write)
            if (_tileHistoryDescription != null && _tileHistoryPixelsBefore == null)
            {
                _tileHistoryDocX = x;
                _tileHistoryDocY = y;
                _tileHistoryWidth = width;
                _tileHistoryHeight = height;
                _tileHistoryPixelsBefore = ReadLayerRectInternal(x, y, width, height);
            }

            var dstPixels = rl.Surface.Pixels;
            int dstStride = rl.Surface.Width * 4;

            for (int row = 0; row < height; row++)
            {
                int dstY = y + row;
                if (dstY < 0 || dstY >= rl.Surface.Height) continue;

                int srcOffset = row * width * 4;
                int dstOffset = dstY * dstStride + x * 4;

                int copyWidth = width;
                int startX = 0;

                // Clamp to layer bounds
                if (x < 0)
                {
                    startX = -x;
                    copyWidth -= startX;
                    srcOffset = row * width * 4 + startX * 4;
                    dstOffset = dstY * dstStride;
                }
                if (x + width > rl.Surface.Width)
                {
                    copyWidth = Math.Max(0, rl.Surface.Width - Math.Max(0, x));
                }

                if (copyWidth > 0 && dstOffset >= 0 && dstOffset + copyWidth * 4 <= dstPixels.Length)
                {
                    Buffer.BlockCopy(pixels, srcOffset, dstPixels, dstOffset, copyWidth * 4);
                }
            }

            // Refresh display
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            CanvasView.Invalidate();
        }

        void ITileContext.BlendLayerRect(int x, int y, int width, int height, byte[] pixels)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            // If in a transaction, capture the before state (only on first write)
            if (_tileHistoryDescription != null && _tileHistoryPixelsBefore == null)
            {
                _tileHistoryDocX = x;
                _tileHistoryDocY = y;
                _tileHistoryWidth = width;
                _tileHistoryHeight = height;
                _tileHistoryPixelsBefore = ReadLayerRectInternal(x, y, width, height);
            }

            var dstPixels = rl.Surface.Pixels;
            int dstW = rl.Surface.Width;
            int dstH = rl.Surface.Height;

            for (int row = 0; row < height; row++)
            {
                int dstY = y + row;
                if (dstY < 0 || dstY >= dstH) continue;

                for (int col = 0; col < width; col++)
                {
                    int dstX = x + col;
                    if (dstX < 0 || dstX >= dstW) continue;

                    int srcIdx = (row * width + col) * 4;
                    int dstIdx = (dstY * dstW + dstX) * 4;

                    // Source pixel (BGRA)
                    byte srcB = pixels[srcIdx];
                    byte srcG = pixels[srcIdx + 1];
                    byte srcR = pixels[srcIdx + 2];
                    byte srcA = pixels[srcIdx + 3];

                    if (srcA == 0)
                        continue; // Fully transparent, skip

                    if (srcA == 255)
                    {
                        // Fully opaque, just copy
                        dstPixels[dstIdx] = srcB;
                        dstPixels[dstIdx + 1] = srcG;
                        dstPixels[dstIdx + 2] = srcR;
                        dstPixels[dstIdx + 3] = srcA;
                    }
                    else
                    {
                        // Alpha blend: dst = src + dst * (1 - srcA)
                        byte dstB = dstPixels[dstIdx];
                        byte dstG = dstPixels[dstIdx + 1];
                        byte dstR = dstPixels[dstIdx + 2];
                        byte dstA = dstPixels[dstIdx + 3];

                        float sa = srcA / 255f;
                        float da = dstA / 255f;
                        float outA = sa + da * (1 - sa);

                        if (outA > 0)
                        {
                            dstPixels[dstIdx] = (byte)((srcB * sa + dstB * da * (1 - sa)) / outA);
                            dstPixels[dstIdx + 1] = (byte)((srcG * sa + dstG * da * (1 - sa)) / outA);
                            dstPixels[dstIdx + 2] = (byte)((srcR * sa + dstR * da * (1 - sa)) / outA);
                            dstPixels[dstIdx + 3] = (byte)(outA * 255);
                        }
                    }
                }
            }

            // Refresh display
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            CanvasView.Invalidate();
        }

        void ITileContext.BlendAndPropagateTiles(int x, int y, int width, int height, byte[] pixels)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            var mapping = rl.TileMapping;
            var tileSet = Document.TileSet;

            // If in a transaction, capture the before state (only on first write)
            if (_tileHistoryDescription != null && _tileHistoryPixelsBefore == null)
            {
                _tileHistoryDocX = x;
                _tileHistoryDocY = y;
                _tileHistoryWidth = width;
                _tileHistoryHeight = height;
                _tileHistoryPixelsBefore = ReadLayerRectInternal(x, y, width, height);
            }

            var dstPixels = rl.Surface.Pixels;
            int dstW = rl.Surface.Width;
            int dstH = rl.Surface.Height;

            // First, blend the pixels onto the layer
            for (int row = 0; row < height; row++)
            {
                int dstY = y + row;
                if (dstY < 0 || dstY >= dstH) continue;

                for (int col = 0; col < width; col++)
                {
                    int dstX = x + col;
                    if (dstX < 0 || dstX >= dstW) continue;

                    int srcIdx = (row * width + col) * 4;
                    int dstIdx = (dstY * dstW + dstX) * 4;

                    // Source pixel (BGRA)
                    byte srcB = pixels[srcIdx];
                    byte srcG = pixels[srcIdx + 1];
                    byte srcR = pixels[srcIdx + 2];
                    byte srcA = pixels[srcIdx + 3];

                    if (srcA == 0)
                        continue; // Fully transparent, skip

                    if (srcA == 255)
                    {
                        // Fully opaque, just copy
                        dstPixels[dstIdx] = srcB;
                        dstPixels[dstIdx + 1] = srcG;
                        dstPixels[dstIdx + 2] = srcR;
                        dstPixels[dstIdx + 3] = srcA;
                    }
                    else
                    {
                        // Alpha blend: dst = src + dst * (1 - srcA)
                        byte dstB = dstPixels[dstIdx];
                        byte dstG = dstPixels[dstIdx + 1];
                        byte dstR = dstPixels[dstIdx + 2];
                        byte dstA = dstPixels[dstIdx + 3];

                        float sa = srcA / 255f;
                        float da = dstA / 255f;
                        float outA = sa + da * (1 - sa);

                        if (outA > 0)
                        {
                            dstPixels[dstIdx] = (byte)((srcB * sa + dstB * da * (1 - sa)) / outA);
                            dstPixels[dstIdx + 1] = (byte)((srcG * sa + dstG * da * (1 - sa)) / outA);
                            dstPixels[dstIdx + 2] = (byte)((srcR * sa + dstR * da * (1 - sa)) / outA);
                            dstPixels[dstIdx + 3] = (byte)(outA * 255);
                        }
                    }
                }
            }

            // Now propagate to any mapped tiles that overlap this region
            if (mapping != null && tileSet != null)
            {
                int tileW = tileSet.TileWidth;
                int tileH = tileSet.TileHeight;

                // Find tiles that intersect the blended area
                int startTileX = Math.Max(0, x / tileW);
                int startTileY = Math.Max(0, y / tileH);
                int endTileX = Math.Min(mapping.Width - 1, (x + width - 1) / tileW);
                int endTileY = Math.Min(mapping.Height - 1, (y + height - 1) / tileH);

                // Collect affected tile IDs
                var affectedTileIds = new HashSet<int>();
                for (int ty = startTileY; ty <= endTileY; ty++)
                {
                    for (int tx = startTileX; tx <= endTileX; tx++)
                    {
                        int tileId = mapping.GetTileId(tx, ty);
                        if (tileId >= 0)
                        {
                            affectedTileIds.Add(tileId);
                        }
                    }
                }

                // For each affected tile, extract changes and propagate
                foreach (var tileId in affectedTileIds)
                {
                    // Find all positions with this tile
                    var positions = new List<(int tx, int ty)>();
                    for (int ty = 0; ty < mapping.Height; ty++)
                    {
                        for (int tx = 0; tx < mapping.Width; tx++)
                        {
                            if (mapping.GetTileId(tx, ty) == tileId)
                            {
                                positions.Add((tx, ty));
                            }
                        }
                    }

                    // Merge changes from all affected positions into the tile definition
                    byte[] mergedPixels = (byte[])tileSet.GetTilePixels(tileId)!.Clone();

                    foreach (var (tx, ty) in positions)
                    {
                        int tileDocX = tx * tileW;
                        int tileDocY = ty * tileH;

                        // Check if this position intersects the bounds
                        if (tileDocX + tileW <= x || tileDocX >= x + width ||
                            tileDocY + tileH <= y || tileDocY >= y + height)
                            continue;

                        // Extract the layer pixels at this tile position
                        byte[] layerPixels = rl.Surface.Pixels;
                        int layerW = rl.Surface.Width;

                        for (int py = 0; py < tileH; py++)
                        {
                            int docY = tileDocY + py;
                            if (docY < y || docY >= y + height || docY >= dstH)
                                continue;

                            for (int px = 0; px < tileW; px++)
                            {
                                int docX = tileDocX + px;
                                if (docX < x || docX >= x + width || docX >= dstW)
                                    continue;

                                int srcIdx = (docY * dstW + docX) * 4;
                                int dstIdx2 = (py * tileW + px) * 4;

                                mergedPixels[dstIdx2] = dstPixels[srcIdx];
                                mergedPixels[dstIdx2 + 1] = dstPixels[srcIdx + 1];
                                mergedPixels[dstIdx2 + 2] = dstPixels[srcIdx + 2];
                                mergedPixels[dstIdx2 + 3] = dstPixels[srcIdx + 3];
                            }
                        }
                    }

                    // Update the tile definition
                    tileSet.UpdateTilePixels(tileId, mergedPixels);

                    // Propagate to all instances
                    foreach (var (tx, ty) in positions)
                    {
                        int dstDocX = tx * tileW;
                        int dstDocY = ty * tileH;
                        WriteLayerRectWithoutRefresh(dstDocX, dstDocY, tileW, tileH, mergedPixels);
                    }
                }
            }

            // Refresh display
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            CanvasView.Invalidate();
        }

        void ITileContext.ClearLayerRect(int x, int y, int width, int height)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            // If in a transaction, capture the before state (only on first write)
            if (_tileHistoryDescription != null && _tileHistoryPixelsBefore == null)
            {
                _tileHistoryDocX = x;
                _tileHistoryDocY = y;
                _tileHistoryWidth = width;
                _tileHistoryHeight = height;
                _tileHistoryPixelsBefore = ReadLayerRectInternal(x, y, width, height);
            }

            var dstPixels = rl.Surface.Pixels;
            int dstStride = rl.Surface.Width * 4;

            for (int row = 0; row < height; row++)
            {
                int dstY = y + row;
                if (dstY < 0 || dstY >= rl.Surface.Height) continue;

                int dstOffset = dstY * dstStride + Math.Max(0, x) * 4;
                int clearWidth = Math.Min(width, rl.Surface.Width - Math.Max(0, x));

                if (clearWidth > 0 && dstOffset >= 0 && dstOffset + clearWidth * 4 <= dstPixels.Length)
                {
                    Array.Clear(dstPixels, dstOffset, clearWidth * 4);
                }
            }

            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            CanvasView.Invalidate();
        }

        // ====================================================================
        // ITILECONTEXT IMPLEMENTATION - TILE SAMPLING
        // ====================================================================

        (int tileId, int tileX, int tileY) ITileContext.SampleTileAt(int docX, int docY)
        {
            var ctx = (ITileContext)this;
            var (tileX, tileY) = ctx.DocToTile(docX, docY);

            int tileId = ctx.GetMappedTileId(tileX, tileY);
            return (tileId, tileX, tileY);
        }

        void ITileContext.SetSelectedTile(int tileId)
        {
            _selectedTileId = tileId;
            TileSelected?.Invoke(tileId);
        }

        /// <summary>
        /// Occurs when a tile is selected (e.g., via tile dropper).
        /// </summary>
        public event Action<int>? TileSelected;

        // ====================================================================
        // TILE STAMP HISTORY TRANSACTION STATE
        // ====================================================================

        private string? _tileHistoryDescription;
        private int _tileHistoryDocX;
        private int _tileHistoryDocY;
        private int _tileHistoryWidth;
        private int _tileHistoryHeight;
        private byte[]? _tileHistoryPixelsBefore;
        private int _tileHistoryMappingTileX;
        private int _tileHistoryMappingTileY;
        private int _tileHistoryMappingBefore;
        private int _tileHistoryMappingAfter;
        private bool _tileHistoryHasMapping;

        // Tile definition before states (captured at transaction start)
        private Dictionary<int, byte[]>? _tileHistoryTileBeforeStates;

        // ====================================================================
        // ITILECONTEXT IMPLEMENTATION - HISTORY & REFRESH
        // ====================================================================

        void ITileContext.BeginHistoryTransaction(string description)
        {
            _tileHistoryDescription = description;
            _tileHistoryPixelsBefore = null;
            _tileHistoryHasMapping = false;
            _tileHistoryMappingBefore = -1;
            _tileHistoryMappingAfter = -1;

            // Capture tile definition before states at transaction start
            _tileHistoryTileBeforeStates = null;
            if (Document?.ActiveLayer is RasterLayer rl && rl.TileMapping != null && Document.TileSet != null)
            {
                _tileHistoryTileBeforeStates = new Dictionary<int, byte[]>();
                foreach (var tileId in Document.TileSet.TileIds)
                {
                    var pixels = Document.TileSet.GetTilePixels(tileId);
                    if (pixels != null)
                    {
                        _tileHistoryTileBeforeStates[tileId] = (byte[])pixels.Clone();
                    }
                }
            }
        }

        void ITileContext.CommitHistoryTransaction()
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
            {
                _tileHistoryDescription = null;
                _tileHistoryTileBeforeStates = null;
                return;
            }

            // If we have captured pixel changes, create history item
            if (_tileHistoryPixelsBefore != null && _tileHistoryDescription != null)
            {
                // Read the current (after) state
                var pixelsAfter = ReadLayerRectInternal(_tileHistoryDocX, _tileHistoryDocY, _tileHistoryWidth, _tileHistoryHeight);

                Core.History.IHistoryItem historyItem;

                if (_tileHistoryHasMapping && rl.TileMapping != null && Document.TileSet != null && _tileHistoryTileBeforeStates != null)
                {
                    // Create a TileStampHistoryItem with pre-captured tile states
                    var stampItem = new Core.History.TileStampHistoryItem(
                        rl,
                        rl.TileMapping,
                        Document.TileSet,
                        _tileHistoryDocX, _tileHistoryDocY, _tileHistoryWidth, _tileHistoryHeight,
                        _tileHistoryPixelsBefore, pixelsAfter,
                        _tileHistoryMappingTileX, _tileHistoryMappingTileY,
                        _tileHistoryMappingBefore, _tileHistoryMappingAfter,
                        _tileHistoryDescription);

                    // Override tile before states with our pre-captured states
                    stampItem.SetTileBeforeStates(_tileHistoryTileBeforeStates);
                    historyItem = stampItem;
                }
                else if (rl.TileMapping != null && Document.TileSet != null && _tileHistoryTileBeforeStates != null)
                {
                    // No mapping change but may have tile changes from BlendAndPropagate
                    var stampItem = new Core.History.TileStampHistoryItem(
                        rl,
                        _tileHistoryDocX, _tileHistoryDocY, _tileHistoryWidth, _tileHistoryHeight,
                        _tileHistoryPixelsBefore, pixelsAfter,
                        _tileHistoryDescription);

                    // Add tile propagation support
                    stampItem.SetTilePropagationContext(rl.TileMapping, Document.TileSet, _tileHistoryTileBeforeStates,
                        _tileHistoryDocX, _tileHistoryDocY, _tileHistoryWidth, _tileHistoryHeight);
                    historyItem = stampItem;
                }
                else
                {
                    historyItem = new Core.History.TileStampHistoryItem(
                        rl,
                        _tileHistoryDocX, _tileHistoryDocY, _tileHistoryWidth, _tileHistoryHeight,
                        _tileHistoryPixelsBefore, pixelsAfter,
                        _tileHistoryDescription);
                }

                Document.History.Push(historyItem);
            }

            // Clean up transaction state
            _tileHistoryDescription = null;
            _tileHistoryPixelsBefore = null;
            _tileHistoryTileBeforeStates = null;

            // Refresh display
            RaiseFrame();
            HistoryStateChanged?.Invoke();
        }

        void ITileContext.CancelHistoryTransaction()
        {
            // Revert any pixel changes if we have the before state
            if (_tileHistoryPixelsBefore != null && Document?.ActiveLayer is RasterLayer rl)
            {
                WriteLayerRectWithoutRefresh(_tileHistoryDocX, _tileHistoryDocY, _tileHistoryWidth, _tileHistoryHeight, _tileHistoryPixelsBefore);

                // Revert mapping if it was changed
                if (_tileHistoryHasMapping && rl.TileMapping != null)
                {
                    rl.TileMapping.SetTileId(_tileHistoryMappingTileX, _tileHistoryMappingTileY, _tileHistoryMappingBefore);
                }

                // Revert tile definitions if captured
                if (_tileHistoryTileBeforeStates != null && Document.TileSet != null)
                {
                    foreach (var (tileId, beforePixels) in _tileHistoryTileBeforeStates)
                    {
                        Document.TileSet.UpdateTilePixels(tileId, beforePixels);
                    }
                }
            }

            // Clean up transaction state
            _tileHistoryDescription = null;
            _tileHistoryPixelsBefore = null;
            _tileHistoryTileBeforeStates = null;
        }

        void ITileContext.Invalidate()
        {
            CanvasView.Invalidate();
        }

        void ITileContext.CapturePointer()
        {
            // Capture is handled in HandleTilePressed
        }

        void ITileContext.ReleasePointer()
        {
            CanvasView.ReleasePointerCaptures();
        }

        // ────────────────────────────────────────────────────────────────────
        // LIVE TILE PROPAGATION DURING PAINTING
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tracks tile states before painting starts for undo purposes.
        /// </summary>
        private Dictionary<int, byte[]>? _liveTileBeforeStates;

        /// <summary>
        /// Tracks the layer snapshot before painting starts (for non-tile pixel undo).
        /// </summary>
        private byte[]? _liveLayerBeforeSnapshot;

        /// <summary>
        /// Tracks the bounding box of all painting during this stroke.
        /// </summary>
        private int _liveStrokeMinX, _liveStrokeMinY, _liveStrokeMaxX, _liveStrokeMaxY;
        private bool _liveStrokeHasBounds;

        /// <summary>
        /// Called when a paint stroke begins to capture tile states for live propagation.
        /// </summary>
        internal void BeginLiveTilePropagation()
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            // Capture entire layer snapshot for non-tile undo
            _liveLayerBeforeSnapshot = (byte[])rl.Surface.Pixels.Clone();
            _liveStrokeHasBounds = false;

            var mapping = rl.TileMapping;
            var tileSet = Document.TileSet;
            if (mapping == null || tileSet == null)
            {
                _liveTileBeforeStates = null;
                return;
            }

            // Capture the "before" state of all tiles (we don't know which will be affected yet)
            _liveTileBeforeStates = new Dictionary<int, byte[]>();
            foreach (var tileId in tileSet.TileIds)
            {
                var pixels = tileSet.GetTilePixels(tileId);
                if (pixels != null)
                {
                    _liveTileBeforeStates[tileId] = (byte[])pixels.Clone();
                }
            }
        }

        /// <summary>
        /// Called during painting to propagate changes to all mapped tiles in real-time.
        /// This should be called after each stamp/line operation.
        /// </summary>
        internal void PropagateLiveTileChanges(int affectedMinX, int affectedMinY, int affectedMaxX, int affectedMaxY)
        {
            // Update stroke bounds
            if (!_liveStrokeHasBounds)
            {
                _liveStrokeMinX = affectedMinX;
                _liveStrokeMinY = affectedMinY;
                _liveStrokeMaxX = affectedMaxX;
                _liveStrokeMaxY = affectedMaxY;
                _liveStrokeHasBounds = true;
            }
            else
            {
                _liveStrokeMinX = Math.Min(_liveStrokeMinX, affectedMinX);
                _liveStrokeMinY = Math.Min(_liveStrokeMinY, affectedMinY);
                _liveStrokeMaxX = Math.Max(_liveStrokeMaxX, affectedMaxX);
                _liveStrokeMaxY = Math.Max(_liveStrokeMaxY, affectedMaxY);
            }

            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            var mapping = rl.TileMapping;
            var tileSet = Document.TileSet;
            if (mapping == null || tileSet == null)
                return;

            int tileW = tileSet.TileWidth;
            int tileH = tileSet.TileHeight;

            // Find tiles that intersect the affected area
            int startTileX = Math.Max(0, affectedMinX / tileW);
            int startTileY = Math.Max(0, affectedMinY / tileH);
            int endTileX = Math.Min(mapping.Width - 1, affectedMaxX / tileW);
            int endTileY = Math.Min(mapping.Height - 1, affectedMaxY / tileH);

            // Collect affected tile IDs
            var affectedTileIds = new HashSet<int>();
            for (int ty = startTileY; ty <= endTileY; ty++)
            {
                for (int tx = startTileX; tx <= endTileX; tx++)
                {
                    int tileId = mapping.GetTileId(tx, ty);
                    if (tileId >= 0)
                    {
                        affectedTileIds.Add(tileId);
                    }
                }
            }

            if (affectedTileIds.Count == 0)
                return;

            // For each affected tile, extract current state and propagate
            foreach (var tileId in affectedTileIds)
            {
                // Find all positions with this tile
                var positions = new List<(int tx, int ty)>();
                for (int ty = 0; ty < mapping.Height; ty++)
                {
                    for (int tx = 0; tx < mapping.Width; tx++)
                    {
                        if (mapping.GetTileId(tx, ty) == tileId)
                        {
                            positions.Add((tx, ty));
                        }
                    }
                }

                if (positions.Count <= 1)
                    continue; // No propagation needed for single instance

                // Merge changes from all affected positions into the tile definition
                byte[] mergedPixels = (byte[])tileSet.GetTilePixels(tileId)!.Clone();

                // For each position of this tile that was in the affected area, extract pixels
                foreach (var (tx, ty) in positions)
                {
                    int tileDocX = tx * tileW;
                    int tileDocY = ty * tileH;

                    // Check if this position intersects the affected area
                    if (tileDocX + tileW <= affectedMinX || tileDocX > affectedMaxX ||
                        tileDocY + tileH <= affectedMinY || tileDocY > affectedMaxY)
                        continue;

                    // Extract the layer pixels at this tile position
                    byte[] layerPixels = rl.Surface.Pixels;
                    int layerW = rl.Surface.Width;

                    for (int py = 0; py < tileH; py++)
                    {
                        int docY = tileDocY + py;
                        if (docY < affectedMinY || docY > affectedMaxY)
                            continue;

                        for (int px = 0; px < tileW; px++)
                        {
                            int docX = tileDocX + px;
                            if (docX < affectedMinX || docX > affectedMaxX)
                                continue;

                            int srcIdx = (docY * layerW + docX) * 4;
                            int dstIdx = (py * tileW + px) * 4;

                            mergedPixels[dstIdx] = layerPixels[srcIdx];
                            mergedPixels[dstIdx + 1] = layerPixels[srcIdx + 1];
                            mergedPixels[dstIdx + 2] = layerPixels[srcIdx + 2];
                            mergedPixels[dstIdx + 3] = layerPixels[srcIdx + 3];
                        }
                    }
                }

                // Update the tile definition
                tileSet.UpdateTilePixels(tileId, mergedPixels);

                // Propagate to all instances
                foreach (var (tx, ty) in positions)
                {
                    int dstDocX = tx * tileW;
                    int dstDocY = ty * tileH;
                    WriteLayerRectWithoutRefresh(dstDocX, dstDocY, tileW, tileH, mergedPixels);
                }
            }

            // Refresh display
            Document.CompositeTo(Document.Surface);
            CanvasView.Invalidate();
        }

        /// <summary>
        /// Called when a paint stroke ends to finalize tile propagation and create history.
        /// Returns a TileMappedPixelChangeItem if tiles were affected, null otherwise.
        /// </summary>
        internal TileMappedPixelChangeItem? EndLiveTilePropagation(string description)
        {
            if (Document?.ActiveLayer is not RasterLayer rl || _liveLayerBeforeSnapshot == null)
            {
                _liveTileBeforeStates = null;
                _liveLayerBeforeSnapshot = null;
                return null;
            }

            var mapping = rl.TileMapping;
            var tileSet = Document.TileSet;

            // If there's no tile mapping, return null to use the painter's result instead
            // This ensures symmetry strokes (and all non-tile painting) use the painter's Accum dictionary
            if (mapping == null || tileSet == null || _liveTileBeforeStates == null)
            {
                _liveTileBeforeStates = null;
                _liveLayerBeforeSnapshot = null;
                return null;
            }

            // Check if ANY tiles were actually affected during this stroke
            bool anyTilesAffected = false;
            if (_liveStrokeHasBounds)
            {
                int tileW = tileSet.TileWidth;
                int tileH = tileSet.TileHeight;

                int startTileX = Math.Max(0, _liveStrokeMinX / tileW);
                int startTileY = Math.Max(0, _liveStrokeMinY / tileH);
                int endTileX = Math.Min(mapping.Width - 1, _liveStrokeMaxX / tileW);
                int endTileY = Math.Min(mapping.Height - 1, _liveStrokeMaxY / tileH);

                for (int ty = startTileY; ty <= endTileY && !anyTilesAffected; ty++)
                {
                    for (int tx = startTileX; tx <= endTileX && !anyTilesAffected; tx++)
                    {
                        if (mapping.GetTileId(tx, ty) >= 0)
                        {
                            anyTilesAffected = true;
                        }
                    }
                }
            }

            // If no tiles were affected, return null to use the painter's result
            if (!anyTilesAffected)
            {
                _liveTileBeforeStates = null;
                _liveLayerBeforeSnapshot = null;
                return null;
            }

            // Create tile-mapped history item (only when tiles are affected)
            var tileMappedItem = new TileMappedPixelChangeItem(rl, tileSet, description);
            bool hasTileChanges = false;
            bool hasNonTileChanges = false;

            // Build a set of all pixels covered by mapped tiles
            var mappedPixelSet = new HashSet<int>();
            int tileWidth = tileSet.TileWidth;
            int tileHeight = tileSet.TileHeight;
            int layerW = rl.Surface.Width;

            for (int ty = 0; ty < mapping.Height; ty++)
            {
                for (int tx = 0; tx < mapping.Width; tx++)
                {
                    if (mapping.GetTileId(tx, ty) >= 0)
                    {
                        int tileDocX = tx * tileWidth;
                        int tileDocY = ty * tileHeight;

                        for (int py = 0; py < tileHeight; py++)
                        {
                            int docY = tileDocY + py;
                            if (docY >= rl.Surface.Height) continue;

                            for (int px = 0; px < tileWidth; px++)
                            {
                                int docX = tileDocX + px;
                                if (docX >= layerW) continue;

                                int idx = (docY * layerW + docX) * 4;
                                mappedPixelSet.Add(idx);
                            }
                        }
                    }
                }
            }

            // Find non-tile pixels that changed
            if (_liveStrokeHasBounds)
            {
                var layerPixels = rl.Surface.Pixels;

                // Clamp bounds to layer
                int minX = Math.Max(0, _liveStrokeMinX);
                int minY = Math.Max(0, _liveStrokeMinY);
                int maxX = Math.Min(rl.Surface.Width - 1, _liveStrokeMaxX);
                int maxY = Math.Min(rl.Surface.Height - 1, _liveStrokeMaxY);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int idx = (y * layerW + x) * 4;

                        // Skip if this pixel is inside a mapped tile
                        if (mappedPixelSet.Contains(idx))
                            continue;

                        // Check if pixel changed
                        uint before = (uint)(_liveLayerBeforeSnapshot[idx] |
                                             (_liveLayerBeforeSnapshot[idx + 1] << 8) |
                                             (_liveLayerBeforeSnapshot[idx + 2] << 16) |
                                             (_liveLayerBeforeSnapshot[idx + 3] << 24));

                        uint after = (uint)(layerPixels[idx] |
                                            (layerPixels[idx + 1] << 8) |
                                            (layerPixels[idx + 2] << 16) |
                                            (layerPixels[idx + 3] << 24));

                        if (before != after)
                        {
                            tileMappedItem.AddNonTileChange(idx, before, after);
                            hasNonTileChanges = true;
                        }
                    }
                }
            }

            // Find tiles that changed
            foreach (var (tileId, beforePixels) in _liveTileBeforeStates)
            {
                var afterPixels = tileSet.GetTilePixels(tileId);
                if (afterPixels == null)
                    continue;

                // Check if tile changed
                bool changed = false;
                for (int i = 0; i < beforePixels.Length && !changed; i++)
                {
                    if (beforePixels[i] != afterPixels[i])
                        changed = true;
                }

                if (!changed)
                    continue;

                // Collect all positions mapped to this tile
                var allPositions = new List<(int tileX, int tileY)>();
                for (int ty = 0; ty < mapping.Height; ty++)
                {
                    for (int tx = 0; tx < mapping.Width; tx++)
                    {
                        if (mapping.GetTileId(tx, ty) == tileId)
                        {
                            allPositions.Add((tx, ty));
                        }
                    }
                }

                tileMappedItem.RecordTileChange(tileId, beforePixels, (byte[])afterPixels.Clone(), allPositions);
                hasTileChanges = true;
            }

            _liveTileBeforeStates = null;
            _liveLayerBeforeSnapshot = null;

            return (hasTileChanges || hasNonTileChanges) ? tileMappedItem : null;
        }

        // ====================================================================
        // ITILEANIMATIONCONTEXT IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        object? ITileAnimationContext.GetAnimationStateObject()
        {
            return Document?.TileAnimationState;
        }

        // ====================================================================
        // ITILEMODIFIERHISTORYCONTEXT IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        void ITileModifierHistoryContext.PushTileModifierHistory(
            int activeTileId,
            List<(int tx, int ty)>? mappedPositions,
            Dictionary<int, byte[]>? tileBeforeStates,
            byte[]? layerBeforeSnapshot,
            string description)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            var tileSet = Document.TileSet;
            if (tileSet == null || tileBeforeStates == null || layerBeforeSnapshot == null)
                return;

            // Create the tile-mapped history item
            var historyItem = new TileMappedPixelChangeItem(rl, tileSet, description);

            // Record changes for each tile that was modified
            foreach (var (tileId, beforePixels) in tileBeforeStates)
            {
                var afterPixels = tileSet.GetTilePixels(tileId);
                if (afterPixels == null)
                    continue;

                // Check if tile actually changed
                bool changed = false;
                for (int i = 0; i < beforePixels.Length && !changed; i++)
                {
                    if (beforePixels[i] != afterPixels[i])
                        changed = true;
                }

                if (!changed)
                    continue;

                // Find all positions mapped to this tile
                var mapping = rl.TileMapping;
                var allPositions = new List<(int tileX, int tileY)>();
                if (mapping != null)
                {
                    for (int ty = 0; ty < mapping.Height; ty++)
                    {
                        for (int tx = 0; tx < mapping.Width; tx++)
                        {
                            if (mapping.GetTileId(tx, ty) == tileId)
                            {
                                allPositions.Add((tx, ty));
                            }
                        }
                    }
                }

                historyItem.RecordTileChange(tileId, beforePixels, (byte[])afterPixels.Clone(), allPositions);
            }

            // Record layer pixel changes for non-mapped areas
            var currentLayerPixels = rl.Surface.Pixels;
            int layerW = rl.Surface.Width;

            for (int i = 0; i < layerBeforeSnapshot.Length; i += 4)
            {
                uint before = (uint)(layerBeforeSnapshot[i] |
                                     (layerBeforeSnapshot[i + 1] << 8) |
                                     (layerBeforeSnapshot[i + 2] << 16) |
                                     (layerBeforeSnapshot[i + 3] << 24));

                uint after = (uint)(currentLayerPixels[i] |
                                    (currentLayerPixels[i + 1] << 8) |
                                    (currentLayerPixels[i + 2] << 16) |
                                    (currentLayerPixels[i + 3] << 24));

                if (before != after)
                {
                    historyItem.AddNonTileChange(i, before, after);
                }
            }

            Document.History.Push(historyItem);
            HistoryStateChanged?.Invoke();
        }

        // ────────────────────────────────────────────────────────────────────
        // SELECTION TILE PROPAGATION
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Propagates selection changes (delete, commit, lift) to all mapped tile instances.
        /// This ensures that when a selection operation modifies a mapped tile region,
        /// all instances of that tile are updated to match.
        /// </summary>
        /// <param name="bounds">The document-space bounds of the affected area.</param>
        private void PropagateSelectionChangesToMappedTiles(Windows.Graphics.RectInt32 bounds)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            var mapping = rl.TileMapping;
            var tileSet = Document.TileSet;
            if (mapping == null || tileSet == null)
                return;

            int tileW = tileSet.TileWidth;
            int tileH = tileSet.TileHeight;

            // Find tiles that intersect the bounds
            int startTileX = Math.Max(0, bounds.X / tileW);
            int startTileY = Math.Max(0, bounds.Y / tileH);
            int endTileX = Math.Min(mapping.Width - 1, (bounds.X + bounds.Width - 1) / tileW);
            int endTileY = Math.Min(mapping.Height - 1, (bounds.Y + bounds.Height - 1) / tileH);

            // Collect affected tile IDs
            var affectedTileIds = new HashSet<int>();
            for (int ty = startTileY; ty <= endTileY; ty++)
            {
                for (int tx = startTileX; tx <= endTileX; tx++)
                {
                    int tileId = mapping.GetTileId(tx, ty);
                    if (tileId >= 0)
                    {
                        affectedTileIds.Add(tileId);
                    }
                }
            }

            if (affectedTileIds.Count == 0)
                return;

            // For each affected tile, extract current state from layer and propagate
            foreach (var tileId in affectedTileIds)
            {
                // Find all positions with this tile
                var positions = new List<(int tx, int ty)>();
                for (int ty = 0; ty < mapping.Height; ty++)
                {
                    for (int tx = 0; tx < mapping.Width; tx++)
                    {
                        if (mapping.GetTileId(tx, ty) == tileId)
                        {
                            positions.Add((tx, ty));
                        }
                    }
                }

                if (positions.Count <= 1)
                    continue; // No propagation needed for single instance

                // Merge changes from all affected positions into the tile definition
                byte[] mergedPixels = (byte[])tileSet.GetTilePixels(tileId)!.Clone();
                var layerPixels = rl.Surface.Pixels;
                int layerW = rl.Surface.Width;

                // For each position of this tile that was in the affected area, extract pixels
                foreach (var (tx, ty) in positions)
                {
                    int tileDocX = tx * tileW;
                    int tileDocY = ty * tileH;

                    // Check if this position intersects the bounds
                    if (tileDocX + tileW <= bounds.X || tileDocX >= bounds.X + bounds.Width ||
                        tileDocY + tileH <= bounds.Y || tileDocY >= bounds.Y + bounds.Height)
                        continue;

                    // Extract the layer pixels at this tile position
                    for (int py = 0; py < tileH; py++)
                    {
                        int docY = tileDocY + py;
                        if (docY < bounds.Y || docY >= bounds.Y + bounds.Height || docY >= layerPixels.Length / 4)
                            continue;

                        for (int px = 0; px < tileW; px++)
                        {
                            int docX = tileDocX + px;
                            if (docX < bounds.X || docX >= bounds.X + bounds.Width || docX >= layerW)
                                continue;

                            int srcIdx = (docY * layerW + docX) * 4;
                            int dstIdx = (py * tileW + px) * 4;

                            mergedPixels[dstIdx] = layerPixels[srcIdx];
                            mergedPixels[dstIdx + 1] = layerPixels[srcIdx + 1];
                            mergedPixels[dstIdx + 2] = layerPixels[srcIdx + 2];
                            mergedPixels[dstIdx + 3] = layerPixels[srcIdx + 3];
                        }
                    }
                }

                // Update the tile definition
                tileSet.UpdateTilePixels(tileId, mergedPixels);

                // Propagate to all instances on the canvas
                foreach (var (tx, ty) in positions)
                {
                    int dstDocX = tx * tileW;
                    int dstDocY = ty * tileH;
                    WriteLayerRectWithoutRefresh(dstDocX, dstDocY, tileW, tileH, mergedPixels);
                }
            }

            // Refresh display
            Document.CompositeTo(Document.Surface);
            CanvasView.Invalidate();
        }
    }
}
