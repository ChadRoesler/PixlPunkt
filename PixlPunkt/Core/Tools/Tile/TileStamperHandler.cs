using System;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Tile;

namespace PixlPunkt.Core.Tools.Tile
{
    /// <summary>
    /// Handler for the Tile Stamper tool.
    /// </summary>
    /// <remarks>
    /// <para><strong>Actions:</strong></para>
    /// <list type="bullet">
    /// <item>LMB: Place tile with mapping (if snap enabled)</item>
    /// <item>Shift + LMB: Place tile without mapping</item>
    /// <item>Ctrl + LMB: Create new tile from area under stamp</item>
    /// <item>RMB: Sample tile and mapping (tile dropper)</item>
    /// </list>
    /// <para><strong>Snap Behavior:</strong></para>
    /// <list type="bullet">
    /// <item>Snap ON: Tile aligns to grid, mapping is written</item>
    /// <item>Snap OFF: Tile is centered on cursor, no mapping written</item>
    /// </list>
    /// </remarks>
    public sealed class TileStamperHandler : ITileHandler
    {
        private readonly ITileContext _context;
        private readonly TileStamperToolSettings _settings;

        private bool _isActive;
        private int _hoverTileX;
        private int _hoverTileY;
        private int _hoverDocX;
        private int _hoverDocY;
        private int _hoverPixelOffsetX;
        private int _hoverPixelOffsetY;

        /// <inheritdoc/>
        public string ToolId => ToolIds.TileStamper;

        /// <inheritdoc/>
        public bool IsActive => _isActive;

        /// <inheritdoc/>
        public TileCursorHint CursorHint => _isActive ? TileCursorHint.Stamp : TileCursorHint.Default;

        /// <summary>
        /// Creates a new Tile Stamper handler.
        /// </summary>
        /// <param name="context">The tile context for host services.</param>
        /// <param name="settings">The stamper tool settings.</param>
        public TileStamperHandler(ITileContext context, TileStamperToolSettings settings)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc/>
        public bool PointerPressed(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            // Check for Alt key
            bool isAltHeld = IsAltKeyDown();

            // RMB: Sample tile (tile dropper)
            if (isRightButton)
            {
                SampleTileAt(docX, docY);
                return true;
            }

            if (!isLeftButton)
                return false;

            var (tileX, tileY) = _context.DocToTile(docX, docY);

            // Alt + LMB: Remove tile mapping (leave pixels, clear mapping)
            if (isAltHeld)
            {
                RemoveTileMapping(tileX, tileY);
                return true;
            }

            // Ctrl + LMB: Create new tile from canvas area
            if (isCtrlHeld)
            {
                CreateTileFromCanvas(tileX, tileY);
                return true;
            }

            // LMB or Shift + LMB: Place tile
            int selectedTileId = _settings.SelectedTileId;
            if (selectedTileId < 0)
                return false;

            if (_settings.SnapToGrid)
            {
                // Snap mode: place at grid position, write mapping unless shift held
                bool writeMapping = !isShiftHeld;
                PlaceTileAtGrid(tileX, tileY, selectedTileId, writeMapping);
            }
            else
            {
                // Free placement: center tile on cursor, no mapping
                PlaceTileCenteredOnCursor(docX, docY, selectedTileId);
            }
            return true;
        }

        /// <summary>
        /// Checks if the Alt key is currently pressed.
        /// </summary>
        private static bool IsAltKeyDown()
        {
            return (Windows.UI.Core.CoreWindow.GetForCurrentThread()?.GetKeyState(Windows.System.VirtualKey.Menu)
                    & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        }

        /// <inheritdoc/>
        public bool PointerMoved(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            _hoverDocX = docX;
            _hoverDocY = docY;

            // Update hover position for overlay
            var (tileX, tileY) = _context.DocToTile(docX, docY);
            _hoverTileX = tileX;
            _hoverTileY = tileY;

            if (!_settings.SnapToGrid)
            {
                // Free placement: calculate offset to center tile on cursor
                int tileW = _context.TileWidth;
                int tileH = _context.TileHeight;

                // The preview should show the tile centered on the cursor
                // PixelOffset is relative to the tile grid position
                var (tileDocX, tileDocY) = _context.TileToDoc(tileX, tileY);

                // Center offset: cursor position relative to tile top-left, minus half tile size
                _hoverPixelOffsetX = (docX - tileDocX) - (tileW / 2);
                _hoverPixelOffsetY = (docY - tileDocY) - (tileH / 2);
            }
            else
            {
                _hoverPixelOffsetX = 0;
                _hoverPixelOffsetY = 0;
            }

            _context.Invalidate();
            return false;
        }

        /// <inheritdoc/>
        public bool PointerReleased(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            _isActive = false;
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _isActive = false;
        }

        /// <inheritdoc/>
        public TileOverlayPreview? GetOverlayPreview()
        {
            int selectedTileId = _settings.SelectedTileId;

            return new TileOverlayPreview(
                TileX: _hoverTileX,
                TileY: _hoverTileY,
                TileId: selectedTileId,
                ShowGhost: selectedTileId >= 0,
                ShowOutline: true,
                SnapToGrid: _settings.SnapToGrid,
                PixelOffsetX: _hoverPixelOffsetX,
                PixelOffsetY: _hoverPixelOffsetY
            );
        }

        /// <summary>
        /// Samples the tile at a document position (RMB tile dropper).
        /// </summary>
        private void SampleTileAt(int docX, int docY)
        {
            var (tileId, tileX, tileY) = _context.SampleTileAt(docX, docY);

            if (tileId >= 0)
            {
                _settings.SetSelectedTileId(tileId);
                _context.SetSelectedTile(tileId);
            }
        }

        /// <summary>
        /// Places a tile at the specified grid position (snap-to-grid mode).
        /// </summary>
        private void PlaceTileAtGrid(int tileX, int tileY, int tileId, bool writeMapping)
        {
            // Validate tile exists
            var pixels = _context.GetTilePixels(tileId);
            if (pixels == null)
                return;

            // Get destination rectangle (aligned to grid)
            var (docX, docY, width, height) = _context.GetTileRect(tileX, tileY);

            _context.BeginHistoryTransaction($"Place tile at ({tileX}, {tileY})");
            try
            {
                // Write tile pixels to layer
                _context.WriteLayerRect(docX, docY, width, height, pixels);

                // Write mapping if enabled
                if (writeMapping)
                {
                    _context.SetTileMapping(tileX, tileY, tileId);
                }

                _context.CommitHistoryTransaction();
            }
            catch
            {
                _context.CancelHistoryTransaction();
                throw;
            }

            _context.Invalidate();
        }

        /// <summary>
        /// Places a tile centered on the cursor position (free placement mode).
        /// No tile mapping is written in this mode.
        /// The tile is blended with existing pixels (not replaced), like a brush stamp.
        /// If the stamp overlaps mapped tiles, changes are propagated to all instances.
        /// </summary>
        private void PlaceTileCenteredOnCursor(int cursorDocX, int cursorDocY, int tileId)
        {
            // Validate tile exists
            var pixels = _context.GetTilePixels(tileId);
            if (pixels == null)
                return;

            int tileW = _context.TileWidth;
            int tileH = _context.TileHeight;

            // Center the tile on the cursor
            int docX = cursorDocX - (tileW / 2);
            int docY = cursorDocY - (tileH / 2);

            _context.BeginHistoryTransaction($"Stamp tile at ({docX}, {docY})");
            try
            {
                // Blend tile pixels with layer AND propagate to mapped tiles
                _context.BlendAndPropagateTiles(docX, docY, tileW, tileH, pixels);

                _context.CommitHistoryTransaction();
            }
            catch
            {
                _context.CancelHistoryTransaction();
                throw;
            }

            _context.Invalidate();
        }

        /// <summary>
        /// Creates a new tile from the canvas area under the current position.
        /// </summary>
        private void CreateTileFromCanvas(int tileX, int tileY)
        {
            var (docX, docY, width, height) = _context.GetTileRect(tileX, tileY);

            // Read pixels from layer
            var pixels = _context.ReadLayerRect(docX, docY, width, height);

            _context.BeginHistoryTransaction("Create tile from canvas");
            try
            {
                // Create new tile
                int newTileId = _context.CreateTile(pixels);

                // Select the new tile
                _settings.SetSelectedTileId(newTileId);
                _context.SetSelectedTile(newTileId);

                // Optionally set mapping (only in snap mode)
                if (_settings.SnapToGrid)
                {
                    _context.SetTileMapping(tileX, tileY, newTileId);
                }

                _context.CommitHistoryTransaction();
            }
            catch
            {
                _context.CancelHistoryTransaction();
                throw;
            }

            _context.Invalidate();
        }

        /// <summary>
        /// Removes tile mapping at the specified grid position, leaving the pixels unchanged.
        /// </summary>
        private void RemoveTileMapping(int tileX, int tileY)
        {
            int currentMapping = _context.GetMappedTileId(tileX, tileY);
            if (currentMapping < 0)
                return; // No mapping to remove

            _context.BeginHistoryTransaction($"Remove tile mapping at ({tileX}, {tileY})");
            try
            {
                // Clear the mapping (no pixel changes)
                _context.ClearTileMapping(tileX, tileY);

                _context.CommitHistoryTransaction();
            }
            catch
            {
                _context.CancelHistoryTransaction();
                throw;
            }

            _context.Invalidate();
        }
    }
}
