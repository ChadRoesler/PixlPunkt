using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Tile;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// History item for tile stamp operations.
    /// Tracks pixel changes and optional tile mapping assignment.
    /// Propagates changes to all mapped tile instances on undo/redo.
    /// </summary>
    public sealed class TileStampHistoryItem : IHistoryItem, IRenderResult
    {
        private readonly RasterLayer _layer;
        private TileMapping? _mapping;
        private TileSet? _tileSet;
        private readonly string _description;

        // Pixel changes (before/after for the stamped region)
        private readonly int _docX;
        private readonly int _docY;
        private readonly int _width;
        private readonly int _height;
        private readonly byte[] _pixelsBefore;
        private readonly byte[] _pixelsAfter;

        // Tile mapping changes (optional)
        private readonly int _mappingTileX;
        private readonly int _mappingTileY;
        private readonly int _mappingBefore;
        private readonly int _mappingAfter;
        private readonly bool _hasMapping;

        // Tile definition changes for propagation
        private Dictionary<int, TileChange> _tileChanges = new();
        private Dictionary<int, List<(int tileX, int tileY)>> _tilePositions = new();

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.TableCursor;

        private class TileChange
        {
            public byte[] Before { get; set; } = new byte[0];
            public byte[] After { get; set; } = new byte[0];
        }

        // ====================================================================
        // IHistoryItem / IRenderResult SHARED PROPERTIES
        // ====================================================================

        /// <inheritdoc/>
        public string Description => _description;

        /// <inheritdoc/>
        public bool HasChanges => true;

        /// <inheritdoc/>
        public bool CanPushToHistory => true;

        // ====================================================================
        // CONSTRUCTION
        // ====================================================================

        /// <summary>
        /// Creates a tile stamp history item without mapping.
        /// </summary>
        public TileStampHistoryItem(
            RasterLayer layer,
            int docX, int docY, int width, int height,
            byte[] pixelsBefore, byte[] pixelsAfter,
            string description = "Place Tile")
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _docX = docX;
            _docY = docY;
            _width = width;
            _height = height;
            _pixelsBefore = (byte[])pixelsBefore.Clone();
            _pixelsAfter = (byte[])pixelsAfter.Clone();
            _description = description;
            _hasMapping = false;
            _mapping = null;
            _tileSet = null;
            _mappingTileX = 0;
            _mappingTileY = 0;
            _mappingBefore = -1;
            _mappingAfter = -1;
        }

        /// <summary>
        /// Creates a tile stamp history item with mapping change.
        /// </summary>
        public TileStampHistoryItem(
            RasterLayer layer,
            TileMapping mapping,
            int docX, int docY, int width, int height,
            byte[] pixelsBefore, byte[] pixelsAfter,
            int mappingTileX, int mappingTileY,
            int mappingBefore, int mappingAfter,
            string description = "Place Tile")
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
            _tileSet = null;
            _docX = docX;
            _docY = docY;
            _width = width;
            _height = height;
            _pixelsBefore = (byte[])pixelsBefore.Clone();
            _pixelsAfter = (byte[])pixelsAfter.Clone();
            _mappingTileX = mappingTileX;
            _mappingTileY = mappingTileY;
            _mappingBefore = mappingBefore;
            _mappingAfter = mappingAfter;
            _hasMapping = true;
            _description = description;
        }

        /// <summary>
        /// Creates a tile stamp history item with mapping and tile propagation support.
        /// </summary>
        public TileStampHistoryItem(
            RasterLayer layer,
            TileMapping mapping,
            TileSet tileSet,
            int docX, int docY, int width, int height,
            byte[] pixelsBefore, byte[] pixelsAfter,
            int mappingTileX, int mappingTileY,
            int mappingBefore, int mappingAfter,
            string description = "Place Tile")
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
            _tileSet = tileSet ?? throw new ArgumentNullException(nameof(tileSet));
            _docX = docX;
            _docY = docY;
            _width = width;
            _height = height;
            _pixelsBefore = (byte[])pixelsBefore.Clone();
            _pixelsAfter = (byte[])pixelsAfter.Clone();
            _mappingTileX = mappingTileX;
            _mappingTileY = mappingTileY;
            _mappingBefore = mappingBefore;
            _mappingAfter = mappingAfter;
            _hasMapping = true;
            _description = description;

            // NOTE: Tile changes will be set via SetTileBeforeStates() if pre-captured states are available
        }

        /// <summary>
        /// Sets pre-captured tile before states. Call this after construction when you have 
        /// captured tile states before the operation was performed.
        /// </summary>
        public void SetTileBeforeStates(Dictionary<int, byte[]> tileBeforeStates)
        {
            if (_mapping == null || _tileSet == null)
                return;

            int tileW = _tileSet.TileWidth;
            int tileH = _tileSet.TileHeight;

            // Find tiles that intersect the stamp region
            int startTileX = Math.Max(0, _docX / tileW);
            int startTileY = Math.Max(0, _docY / tileH);
            int endTileX = Math.Min(_mapping.Width - 1, (_docX + _width - 1) / tileW);
            int endTileY = Math.Min(_mapping.Height - 1, (_docY + _height - 1) / tileH);

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
        /// Sets tile propagation context for stamps without explicit mapping changes but that
        /// may have affected tiles through BlendAndPropagate.
        /// </summary>
        public void SetTilePropagationContext(TileMapping mapping, TileSet tileSet, Dictionary<int, byte[]> tileBeforeStates,
            int docX, int docY, int width, int height)
        {
            _mapping = mapping;
            _tileSet = tileSet;

            int tileW = tileSet.TileWidth;
            int tileH = tileSet.TileHeight;

            // Find tiles that intersect the stamp region
            int startTileX = Math.Max(0, docX / tileW);
            int startTileY = Math.Max(0, docY / tileH);
            int endTileX = Math.Min(mapping.Width - 1, (docX + width - 1) / tileW);
            int endTileY = Math.Min(mapping.Height - 1, (docY + height - 1) / tileH);

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

            _tileChanges.Clear();
            _tilePositions.Clear();

            foreach (var tileId in affectedTileIds)
            {
                // Get the pre-captured before state
                if (!tileBeforeStates.TryGetValue(tileId, out var beforePixels))
                    continue;

                // Get the current (after) state from the tile set
                var afterPixels = tileSet.GetTilePixels(tileId);
                if (afterPixels == null)
                    continue;

                // Check if the tile actually changed
                bool changed = false;
                for (int i = 0; i < beforePixels.Length && !changed; i++)
                {
                    if (beforePixels[i] != afterPixels[i])
                        changed = true;
                }

                if (!changed)
                    continue;

                // Find all positions mapped to this tile
                var positions = new List<(int tileX, int tileY)>();
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

                _tileChanges[tileId] = new TileChange
                {
                    Before = (byte[])beforePixels.Clone(),
                    After = (byte[])afterPixels.Clone()
                };
                _tilePositions[tileId] = positions;
            }
        }

        /// <summary>
        /// Captures the before/after state of tiles affected by this stamp.
        /// @deprecated Use SetTileBeforeStates with pre-captured states instead.
        /// </summary>
        private void CaptureAffectedTileStates(byte[] pixelsBefore, byte[] pixelsAfter)
        {
            // This method is deprecated - tile states should be captured before the operation
            // via SetTileBeforeStates() or SetTilePropagationContext()
        }

        // ====================================================================
        // UNDO / REDO
        // ====================================================================

        /// <inheritdoc/>
        public void Undo()
        {
            var layerName = _layer.Name ?? "(layer)";
            LoggingService.Info("Undo tile stamp on layer={Layer} region={X},{Y} {W}x{H} mappingChanged={Mapping}",
                layerName, _docX, _docY, _width, _height, _hasMapping);

            // Restore mapping first (before propagating)
            if (_hasMapping && _mapping != null)
            {
                _mapping.SetTileId(_mappingTileX, _mappingTileY, _mappingBefore);
            }

            // Restore tile definitions and propagate to all instances
            if (_tileSet != null && _tileChanges.Count > 0)
            {
                foreach (var (tileId, change) in _tileChanges)
                {
                    // Update tile definition to before state
                    _tileSet.UpdateTilePixels(tileId, change.Before);

                    // Propagate to all instances
                    if (_tilePositions.TryGetValue(tileId, out var positions))
                    {
                        int tileW = _tileSet.TileWidth;
                        int tileH = _tileSet.TileHeight;

                        foreach (var (tx, ty) in positions)
                        {
                            int dstDocX = tx * tileW;
                            int dstDocY = ty * tileH;
                            WritePixelsToLayerAt(dstDocX, dstDocY, tileW, tileH, change.Before);
                        }
                    }

                    LoggingService.Debug("Reverted tile {TileId}; mappedPositions={Positions}", tileId, _tilePositions.TryGetValue(tileId, out var p) ? p.Count : 0);
                }
            }
            else
            {
                // No tile propagation - just restore pixels normally
                WritePixelsToLayer(_pixelsBefore);
            }

            _layer.UpdatePreview();
        }

        /// <inheritdoc/>
        public void Redo()
        {
            var layerName = _layer.Name ?? "(layer)";
            LoggingService.Info("Redo tile stamp on layer={Layer} region={X},{Y} {W}x{H} mappingChanged={Mapping}",
                layerName, _docX, _docY, _width, _height, _hasMapping);

            // Apply mapping first
            if (_hasMapping && _mapping != null)
            {
                _mapping.SetTileId(_mappingTileX, _mappingTileY, _mappingAfter);
            }

            // Apply tile definitions and propagate to all instances
            if (_tileSet != null && _tileChanges.Count > 0)
            {
                foreach (var (tileId, change) in _tileChanges)
                {
                    // Update tile definition to after state
                    _tileSet.UpdateTilePixels(tileId, change.After);

                    // Propagate to all instances
                    if (_tilePositions.TryGetValue(tileId, out var positions))
                    {
                        int tileW = _tileSet.TileWidth;
                        int tileH = _tileSet.TileHeight;

                        foreach (var (tx, ty) in positions)
                        {
                            int dstDocX = tx * tileW;
                            int dstDocY = ty * tileH;
                            WritePixelsToLayerAt(dstDocX, dstDocY, tileW, tileH, change.After);
                        }
                    }

                    LoggingService.Debug("Applied tile {TileId}; mappedPositions={Positions}", tileId, _tilePositions.TryGetValue(tileId, out var p) ? p.Count : 0);
                }
            }
            else
            {
                // No tile propagation - just apply pixels normally
                WritePixelsToLayer(_pixelsAfter);
            }

            _layer.UpdatePreview();
        }

        // ====================================================================
        // HELPERS
        // ====================================================================

        private void WritePixelsToLayer(byte[] src)
        {
            WritePixelsToLayerAt(_docX, _docY, _width, _height, src);
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
