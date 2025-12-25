using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Tile;
using Windows.Graphics;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// History item for pixel changes that also propagates to all mapped tiles on undo/redo.
    /// This is used for selection operations (delete, commit) that affect mapped tile regions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="PixelChangeItem"/> which only tracks layer pixels, this item
    /// also captures affected tile states and propagates changes to all tile instances
    /// when undoing or redoing.
    /// </para>
    /// </remarks>
    public sealed class TileAwarePixelChangeItem : IHistoryItem, IRenderResult
    {
        private readonly RasterLayer _layer;
        private readonly TileSet? _tileSet;
        private readonly TileMapping? _mapping;
        private readonly string _description;
        private readonly RectInt32 _bounds;

        // Pixel changes (before/after)
        private readonly byte[] _pixelsBefore;
        private readonly byte[] _pixelsAfter;

        // Tile definition changes
        private readonly Dictionary<int, TileChange> _tileChanges = new();
        private readonly Dictionary<int, List<(int tileX, int tileY)>> _tilePositions = new();

        // Individual pixel changes (for sparse changes without bounds)
        private readonly List<(int idx, uint before, uint after)> _pixelDeltas = new();
        private bool _useSparsePixels;

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.TableCellCenterEdit;

        private class TileChange
        {
            public byte[] Before { get; set; } = [];
            public byte[] After { get; set; } = [];
        }

        // ====================================================================
        // IHistoryItem / IRenderResult SHARED PROPERTIES
        // ====================================================================

        /// <inheritdoc/>
        public string Description => _description;

        /// <inheritdoc/>
        public bool HasChanges => _pixelsBefore.Length > 0 || _pixelDeltas.Count > 0 || _tileChanges.Count > 0;

        /// <inheritdoc/>
        public bool CanPushToHistory => HasChanges;

        // ====================================================================
        // CONSTRUCTION
        // ====================================================================

        /// <summary>
        /// Creates a tile-aware pixel change item by capturing the current state of affected tiles.
        /// </summary>
        /// <param name="layer">The layer being modified.</param>
        /// <param name="tileSet">The tile set (can be null if no tiles).</param>
        /// <param name="bounds">The bounding rectangle of the change.</param>
        /// <param name="pixelsBefore">Layer pixels before the change.</param>
        /// <param name="pixelsAfter">Layer pixels after the change.</param>
        /// <param name="description">Description of the operation.</param>
        public TileAwarePixelChangeItem(
            RasterLayer layer,
            TileSet? tileSet,
            RectInt32 bounds,
            byte[] pixelsBefore,
            byte[] pixelsAfter,
            string description = "Pixel Change")
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _tileSet = tileSet;
            _mapping = layer.TileMapping;
            _bounds = bounds;
            _pixelsBefore = (byte[])pixelsBefore.Clone();
            _pixelsAfter = (byte[])pixelsAfter.Clone();
            _description = description;
            _useSparsePixels = false;

            // Capture affected tile states
            CaptureAffectedTileStates();
        }

        /// <summary>
        /// Creates a tile-aware pixel change item for sparse pixel changes (tile modifier use case).
        /// </summary>
        /// <param name="layer">The layer being modified.</param>
        /// <param name="tileSet">The tile set (can be null if no tiles).</param>
        /// <param name="description">Description of the operation.</param>
        public TileAwarePixelChangeItem(
            RasterLayer layer,
            TileSet? tileSet,
            string description = "Pixel Change")
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _tileSet = tileSet;
            _mapping = layer.TileMapping;
            _bounds = default;
            _pixelsBefore = [];
            _pixelsAfter = [];
            _description = description;
            _useSparsePixels = true;
        }

        /// <summary>
        /// Adds a single pixel change (for sparse change tracking).
        /// </summary>
        public void AddPixelChange(int byteIdx, uint before, uint after)
        {
            if (before != after)
            {
                _pixelDeltas.Add((byteIdx, before, after));
            }
        }

        /// <summary>
        /// Records a tile change with before/after states and all positions mapped to that tile.
        /// </summary>
        public void RecordTileChange(int tileId, byte[] before, byte[] after, List<(int tileX, int tileY)> positions)
        {
            _tileChanges[tileId] = new TileChange
            {
                Before = (byte[])before.Clone(),
                After = (byte[])after.Clone()
            };
            _tilePositions[tileId] = new List<(int, int)>(positions);
        }

        /// <summary>
        /// Sets pre-captured tile before states, overriding the auto-captured states.
        /// Use this when you captured tile states before the operation was performed.
        /// </summary>
        public void SetTileBeforeStates(Dictionary<int, byte[]> tileBeforeStates)
        {
            if (_tileSet == null || _mapping == null)
                return;

            int tileW = _tileSet.TileWidth;
            int tileH = _tileSet.TileHeight;

            // Find tiles that intersect the bounds
            int startTileX = Math.Max(0, _bounds.X / tileW);
            int startTileY = Math.Max(0, _bounds.Y / tileH);
            int endTileX = Math.Min(_mapping.Width - 1, (_bounds.X + _bounds.Width - 1) / tileW);
            int endTileY = Math.Min(_mapping.Height - 1, (_bounds.Y + _bounds.Height - 1) / tileH);

            var affectedTileIds = new HashSet<int>();
            for (int ty = startTileY; ty <= endTileY; ty++)
            {
                for (int tx = startTileX; tx <= endTileX; tx++)
                {
                    int tileId = _mapping.GetTileId(tx, ty);
                    if (tileId >= 0)
                    {
                        affectedTileIds.Add(tileId);
                    }
                }
            }

            _tileChanges.Clear();
            _tilePositions.Clear();

            foreach (var tileId in affectedTileIds)
            {
                // Get the pre-captured before state
                if (!tileBeforeStates.TryGetValue(tileId, out var beforePixels))
                    continue;

                // Get the current (after) state from the tile set
                var afterPixels = _tileSet.GetTilePixels(tileId);
                if (afterPixels == null)
                    continue;

                // Find all positions mapped to this tile
                var positions = new List<(int tileX, int tileY)>();
                for (int ty = 0; ty < _mapping.Height; ty++)
                {
                    for (int tx = 0; tx < _mapping.Width; tx++)
                    {
                        if (_mapping.GetTileId(tx, ty) == tileId)
                        {
                            positions.Add((tx, ty));
                        }
                    }
                }

                _tileChanges[tileId] = new TileChange
                {
                    Before = (byte[])beforePixels.Clone(),
                    After = (byte[])afterPixels.Clone()
                };
                _tilePositions[tileId] = positions;
            }
        }

        /// <summary>
        /// Captures the before/after state of tiles affected by the change.
        /// </summary>
        private void CaptureAffectedTileStates()
        {
            if (_tileSet == null || _mapping == null)
                return;

            int tileW = _tileSet.TileWidth;
            int tileH = _tileSet.TileHeight;

            // Find tiles that intersect the bounds
            int startTileX = Math.Max(0, _bounds.X / tileW);
            int startTileY = Math.Max(0, _bounds.Y / tileH);
            int endTileX = Math.Min(_mapping.Width - 1, (_bounds.X + _bounds.Width - 1) / tileW);
            int endTileY = Math.Min(_mapping.Height - 1, (_bounds.Y + _bounds.Height - 1) / tileH);

            var affectedTileIds = new HashSet<int>();
            for (int ty = startTileY; ty <= endTileY; ty++)
            {
                for (int tx = startTileX; tx <= endTileX; tx++)
                {
                    int tileId = _mapping.GetTileId(tx, ty);
                    if (tileId >= 0)
                    {
                        affectedTileIds.Add(tileId);
                    }
                }
            }

            // For each affected tile, capture before/after states
            foreach (var tileId in affectedTileIds)
            {
                var currentPixels = _tileSet.GetTilePixels(tileId);
                if (currentPixels == null)
                    continue;

                // Find all positions mapped to this tile
                var positions = new List<(int tileX, int tileY)>();
                for (int ty = 0; ty < _mapping.Height; ty++)
                {
                    for (int tx = 0; tx < _mapping.Width; tx++)
                    {
                        if (_mapping.GetTileId(tx, ty) == tileId)
                        {
                            positions.Add((tx, ty));
                        }
                    }
                }

                // Current state is "after", we need to compute "before" from pixelsBefore
                byte[] afterPixels = (byte[])currentPixels.Clone();
                byte[] beforePixels = ComputeTileStateFromLayerSnapshot(_pixelsBefore, tileId, positions);

                _tileChanges[tileId] = new TileChange
                {
                    Before = beforePixels,
                    After = afterPixels
                };
                _tilePositions[tileId] = positions;
            }
        }

        /// <summary>
        /// Computes what a tile's pixels should be based on a layer snapshot.
        /// </summary>
        private byte[] ComputeTileStateFromLayerSnapshot(byte[] layerSnapshot, int tileId, List<(int tileX, int tileY)> positions)
        {
            if (_tileSet == null)
                return [];

            int tileW = _tileSet.TileWidth;
            int tileH = _tileSet.TileHeight;
            int layerW = _layer.Surface.Width;

            // Start with current tile pixels
            var result = _tileSet.GetTilePixels(tileId);
            if (result == null)
                return [];

            result = (byte[])result.Clone();

            // For each position of this tile that intersects bounds, extract from snapshot
            foreach (var (tx, ty) in positions)
            {
                int tileDocX = tx * tileW;
                int tileDocY = ty * tileH;

                // Check if this position intersects the bounds
                if (tileDocX + tileW <= _bounds.X || tileDocX >= _bounds.X + _bounds.Width ||
                    tileDocY + tileH <= _bounds.Y || tileDocY >= _bounds.Y + _bounds.Height)
                    continue;

                // Extract pixels from snapshot
                for (int py = 0; py < tileH; py++)
                {
                    int docY = tileDocY + py;
                    if (docY < _bounds.Y || docY >= _bounds.Y + _bounds.Height)
                        continue;

                    for (int px = 0; px < tileW; px++)
                    {
                        int docX = tileDocX + px;
                        if (docX < _bounds.X || docX >= _bounds.X + _bounds.Width)
                            continue;

                        // Index into the snapshot (which is bounds-relative)
                        int snapshotX = docX - _bounds.X;
                        int snapshotY = docY - _bounds.Y;
                        int srcIdx = (snapshotY * _bounds.Width + snapshotX) * 4;
                        int dstIdx = (py * tileW + px) * 4;

                        if (srcIdx + 3 < layerSnapshot.Length && dstIdx + 3 < result.Length)
                        {
                            result[dstIdx] = layerSnapshot[srcIdx];
                            result[dstIdx + 1] = layerSnapshot[srcIdx + 1];
                            result[dstIdx + 2] = layerSnapshot[srcIdx + 2];
                            result[dstIdx + 3] = layerSnapshot[srcIdx + 3];
                        }
                    }
                }
            }

            return result;
        }

        // ====================================================================
        // NON-TILE PIXEL CHANGES (for TileModifier sparse updates)
        // ====================================================================

        /// <summary>
        /// Adds a non-tile pixel change for undo/redo.
        /// </summary>
        public void AddNonTileChange(int byteIdx, uint before, uint after)
        {
            _pixelDeltas.Add((byteIdx, before, after));
        }

        // ====================================================================
        // UNDO / REDO
        // ====================================================================

        /// <inheritdoc/>
        public void Undo()
        {
            var layerPixels = _layer.Surface.Pixels;

            var layerName = _layer.Name ?? "(layer)";
            LoggingService.Info("Undo tile-aware change on layer={Layer} desc={Desc} sparse={Sparse} tileChangeCount={TileCount}",
                layerName, _description, _useSparsePixels, _tileChanges.Count);

            if (_useSparsePixels)
            {
                foreach (var (idx, before, _) in _pixelDeltas)
                {
                    WritePixel(layerPixels, idx, before);
                }
            }
            else
            {
                WritePixelsToLayer(_pixelsBefore);
            }

            // Restore tile definitions and propagate
            if (_tileSet != null)
            {
                foreach (var (tileId, change) in _tileChanges)
                {
                    _tileSet.UpdateTilePixels(tileId, change.Before);

                    if (_tilePositions.TryGetValue(tileId, out var positions))
                    {
                        int tileW = _tileSet.TileWidth;
                        int tileH = _tileSet.TileHeight;

                        foreach (var (tx, ty) in positions)
                        {
                            int docX = tx * tileW;
                            int docY = ty * tileH;
                            WritePixelsToLayerAt(docX, docY, tileW, tileH, change.Before);
                        }
                    }

                    LoggingService.Debug("Restored tile {TileId}; mappedPositions={Positions}", tileId, _tilePositions.TryGetValue(tileId, out var p) ? p.Count : 0);
                }
            }

            _layer.UpdatePreview();
        }

        /// <inheritdoc/>
        public void Redo()
        {
            var layerPixels = _layer.Surface.Pixels;

            var layerName = _layer.Name ?? "(layer)";
            LoggingService.Info("Redo tile-aware change on layer={Layer} desc={Desc} sparse={Sparse} tileChangeCount={TileCount}",
                layerName, _description, _useSparsePixels, _tileChanges.Count);

            if (_useSparsePixels)
            {
                foreach (var (idx, _, after) in _pixelDeltas)
                {
                    WritePixel(layerPixels, idx, after);
                }
            }
            else
            {
                WritePixelsToLayer(_pixelsAfter);
            }

            // Apply tile definitions and propagate
            if (_tileSet != null)
            {
                foreach (var (tileId, change) in _tileChanges)
                {
                    _tileSet.UpdateTilePixels(tileId, change.After);

                    if (_tilePositions.TryGetValue(tileId, out var positions))
                    {
                        int tileW = _tileSet.TileWidth;
                        int tileH = _tileSet.TileHeight;

                        foreach (var (tx, ty) in positions)
                        {
                            int docX = tx * tileW;
                            int docY = ty * tileH;
                            WritePixelsToLayerAt(docX, docY, tileW, tileH, change.After);
                        }
                    }

                    LoggingService.Debug("Applied tile {TileId}; mappedPositions={Positions}", tileId, _tilePositions.TryGetValue(tileId, out var p) ? p.Count : 0);
                }
            }

            _layer.UpdatePreview();
        }

        // ====================================================================
        // HELPERS
        // ====================================================================

        private static void WritePixel(byte[] pixels, int idx, uint val)
        {
            pixels[idx + 0] = (byte)(val & 0xFF);
            pixels[idx + 1] = (byte)(val >> 8 & 0xFF);
            pixels[idx + 2] = (byte)(val >> 16 & 0xFF);
            pixels[idx + 3] = (byte)(val >> 24 & 0xFF);
        }

        private void WritePixelsToLayer(byte[] src)
        {
            WritePixelsToLayerAt(_bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, src);
        }

        private void WritePixelsToLayerAt(int x, int y, int width, int height, byte[] src)
        {
            var layerPixels = _layer.Surface.Pixels;
            int layerW = _layer.Surface.Width;
            int layerH = _layer.Surface.Height;
            int layerStride = layerW * 4;

            for (int row = 0; row < height; row++)
            {
                int dstY = y + row;
                if (dstY < 0 || dstY >= layerH) continue;

                int srcOffset = row * width * 4;
                int dstOffset = dstY * layerStride + x * 4;

                int copyWidth = width;
                if (x < 0)
                {
                    int skip = -x;
                    copyWidth -= skip;
                    srcOffset += skip * 4;
                    dstOffset = dstY * layerStride;
                }
                if (x + width > layerW)
                {
                    copyWidth = Math.Max(0, layerW - Math.Max(0, x));
                }

                if (copyWidth > 0 && srcOffset >= 0 && srcOffset + copyWidth * 4 <= src.Length &&
                    dstOffset >= 0 && dstOffset + copyWidth * 4 <= layerPixels.Length)
                {
                    Buffer.BlockCopy(src, srcOffset, layerPixels, dstOffset, copyWidth * 4);
                }
            }
        }
    }
}
