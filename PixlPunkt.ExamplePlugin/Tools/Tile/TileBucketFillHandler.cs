using System;
using System.Collections.Generic;
using PixlPunkt.PluginSdk.Tile;

namespace PixlPunkt.ExamplePlugin.Tools.Tile
{
    /// <summary>
    /// Example tile tool that performs a flood fill over the active layer's tile mapping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Controls:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>LMB</strong>: Flood fill the clicked mapping region with the selected tile.</item>
    /// <item><strong>RMB</strong>: Sample tile under cursor and select it in the tile panel.</item>
    /// <item><strong>Shift</strong>: Temporarily enable "Fill Empty Only".</item>
    /// <item><strong>Ctrl</strong>: Temporarily enable diagonal (8-way) fill.</item>
    /// </list>
    /// </remarks>
    public sealed class TileBucketFillHandler : ITileHandler
    {
        private readonly ITileContext _ctx;
        private readonly TileBucketFillSettings _settings;

        private int _hoverTileX = -1;
        private int _hoverTileY = -1;

        public TileBucketFillHandler(ITileContext ctx, TileBucketFillSettings settings)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public string ToolId => "pixlpunkt.example.tile.bucketfill";

        public bool IsActive { get; private set; }

        public TileCursorHint CursorHint => TileCursorHint.Stamp;

        public bool PointerPressed(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            UpdateHover(docX, docY);

            // RMB = tile dropper
            if (isRightButton)
            {
                var (tileId, _, _) = _ctx.SampleTileAt(docX, docY);
                if (tileId >= 0)
                {
                    _ctx.SetSelectedTile(tileId);
                }
                _ctx.Invalidate();
                return true;
            }

            // LMB = bucket fill
            if (!isLeftButton)
                return false;

            // Get tile coordinates
            var (seedX, seedY) = _ctx.DocToTile(docX, docY);

            // Bounds check
            if (seedX < 0 || seedY < 0 || seedX >= _ctx.TileCountX || seedY >= _ctx.TileCountY)
                return true;

            // Get the selected tile ID to fill with
            int fillId = _settings.EraseMode ? -1 : _ctx.SelectedTileId;

            // Get the current tile mapping at the clicked position
            int targetId = _ctx.GetMappedTileId(seedX, seedY);

            // Settings
            bool diagonal = _settings.UseDiagonalFill || isCtrlHeld;
            bool emptyOnly = _settings.FillEmptyOnly || isShiftHeld;

            // "Fill empty only" means we only act when starting on empty (-1)
            if (emptyOnly && targetId != -1)
                return true;

            // If the fill would not change anything, bail early
            if (targetId == fillId)
                return true;

            // If not erasing and no tile selected, we can't fill
            if (!_settings.EraseMode && fillId < 0)
                return true;

            IsActive = true;

            try
            {
                // Compute the flood fill region
                var region = FloodCollect(seedX, seedY, targetId, diagonal, _settings.MaxTiles);

                if (region.Count == 0)
                {
                    IsActive = false;
                    return true;
                }

                // Begin history transaction
                string desc = _settings.EraseMode ? "Tile Bucket Erase" : "Tile Bucket Fill";
                _ctx.BeginHistoryTransaction(desc);

                try
                {
                    // Apply the fill
                    foreach (var (x, y) in region)
                    {
                        if (_settings.EraseMode || fillId == -1)
                        {
                            _ctx.ClearTileMapping(x, y);
                        }
                        else
                        {
                            _ctx.SetTileMapping(x, y, fillId);

                            // Also write the tile pixels to the layer
                            var pixels = _ctx.GetTilePixels(fillId);
                            if (pixels != null)
                            {
                                var (docTileX, docTileY, w, h) = _ctx.GetTileRect(x, y);
                                _ctx.WriteLayerRect(docTileX, docTileY, w, h, pixels);
                            }
                        }
                    }

                    _ctx.CommitHistoryTransaction();
                }
                catch
                {
                    _ctx.CancelHistoryTransaction();
                    throw;
                }

                _ctx.Invalidate();
                return true;
            }
            finally
            {
                IsActive = false;
            }
        }

        public bool PointerMoved(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            if (UpdateHover(docX, docY))
            {
                _ctx.Invalidate();
            }
            return false;
        }

        public bool PointerReleased(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            return false;
        }

        public void Reset()
        {
            IsActive = false;
            _hoverTileX = -1;
            _hoverTileY = -1;
        }

        public TileOverlayPreview? GetOverlayPreview()
        {
            if (_hoverTileX < 0 || _hoverTileY < 0)
                return null;

            int tileId = _settings.EraseMode ? -1 : _ctx.SelectedTileId;

            return new TileOverlayPreview(
                TileX: _hoverTileX,
                TileY: _hoverTileY,
                TileId: tileId,
                ShowGhost: tileId >= 0,
                ShowOutline: true,
                SnapToGrid: true);
        }

        private bool UpdateHover(int docX, int docY)
        {
            var (tx, ty) = _ctx.DocToTile(docX, docY);

            if (tx < 0 || ty < 0 || tx >= _ctx.TileCountX || ty >= _ctx.TileCountY)
            {
                bool changed = _hoverTileX != -1 || _hoverTileY != -1;
                _hoverTileX = -1;
                _hoverTileY = -1;
                return changed;
            }

            bool diff = tx != _hoverTileX || ty != _hoverTileY;
            _hoverTileX = tx;
            _hoverTileY = ty;
            return diff;
        }

        /// <summary>
        /// Collects all tile positions that should be filled using flood fill algorithm.
        /// </summary>
        private List<(int x, int y)> FloodCollect(int seedX, int seedY, int targetId, bool diagonal, int maxTiles)
        {
            int w = _ctx.TileCountX;
            int h = _ctx.TileCountY;

            var result = new List<(int x, int y)>();
            var queue = new Queue<(int x, int y)>();
            var visited = new HashSet<(int x, int y)>();

            // Start with the seed position
            queue.Enqueue((seedX, seedY));
            visited.Add((seedX, seedY));

            while (queue.Count > 0 && result.Count < maxTiles)
            {
                var (x, y) = queue.Dequeue();

                // Check if this position has the target mapping
                if (_ctx.GetMappedTileId(x, y) != targetId)
                    continue;

                result.Add((x, y));

                // Add neighbors
                // 4-way neighbors
                TryEnqueue(x + 1, y);
                TryEnqueue(x - 1, y);
                TryEnqueue(x, y + 1);
                TryEnqueue(x, y - 1);

                // 8-way diagonal neighbors
                if (diagonal)
                {
                    TryEnqueue(x + 1, y + 1);
                    TryEnqueue(x + 1, y - 1);
                    TryEnqueue(x - 1, y + 1);
                    TryEnqueue(x - 1, y - 1);
                }
            }

            return result;

            void TryEnqueue(int nx, int ny)
            {
                if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                    return;

                var pos = (nx, ny);
                if (visited.Contains(pos))
                    return;

                visited.Add(pos);

                // Only enqueue if it has the same target mapping
                if (_ctx.GetMappedTileId(nx, ny) == targetId)
                {
                    queue.Enqueue(pos);
                }
            }
        }
    }
}
