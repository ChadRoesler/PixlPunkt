using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Tile;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// History item for pixel changes that affect tile-mapped regions.
    /// Tracks changes to both the canvas and the tile definition, and properly
    /// undoes/redoes across all instances of a mapped tile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When painting on a mapped tile:
    /// 1. The original stroke changes the layer pixels at one location
    /// 2. These changes are extracted and applied to the tile definition
    /// 3. The tile definition change propagates to all other mapped instances
    /// </para>
    /// <para>
    /// This history item tracks:
    /// - The original pixel changes (from the user's stroke) for non-tile areas
    /// - The tile definition before/after states
    /// - All tile positions that need to be updated on undo/redo
    /// </para>
    /// </remarks>
    public sealed class TileMappedPixelChangeItem : IHistoryItem, IRenderResult
    {
        private readonly RasterLayer _layer;
        private readonly TileSet _tileSet;
        private readonly string _description;

        // Non-tile pixel changes (pixels outside any mapped tile region)
        private readonly List<int> _nonTileIndices = new();
        private readonly List<uint> _nonTileBefore = new();
        private readonly List<uint> _nonTileAfter = new();

        // Tile definition changes
        private readonly Dictionary<int, TileChange> _tileChanges = new();

        // All positions mapped to each tile (for propagation during undo/redo)
        private readonly Dictionary<int, List<(int tileX, int tileY)>> _tilePositions = new();

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.Map;

        /// <summary>
        /// Represents the before/after state of a tile definition.
        /// </summary>
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

        /// <summary>
        /// Gets whether this change item has any actual pixel changes.
        /// </summary>
        public bool IsEmpty => _nonTileIndices.Count == 0 && _tileChanges.Count == 0;

        /// <inheritdoc/>
        public bool HasChanges => !IsEmpty;

        /// <inheritdoc/>
        public bool CanPushToHistory => HasChanges;

        // ====================================================================
        // CONSTRUCTION
        // ====================================================================

        /// <summary>
        /// Creates a new tile-mapped pixel change item.
        /// </summary>
        /// <param name="layer">The layer being modified.</param>
        /// <param name="tileSet">The tile set containing tile definitions.</param>
        /// <param name="description">Description of the operation.</param>
        public TileMappedPixelChangeItem(RasterLayer layer, TileSet tileSet, string description = "Brush Stroke")
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _tileSet = tileSet ?? throw new ArgumentNullException(nameof(tileSet));
            _description = description;
        }

        /// <summary>
        /// Adds a single non-tile pixel change (pixel outside any mapped tile).
        /// </summary>
        public void AddNonTileChange(int byteIndex, uint beforeValue, uint afterValue)
        {
            _nonTileIndices.Add(byteIndex);
            _nonTileBefore.Add(beforeValue);
            _nonTileAfter.Add(afterValue);
        }

        /// <summary>
        /// Records a tile definition change and all positions mapped to that tile.
        /// </summary>
        /// <param name="tileId">The tile ID being modified.</param>
        /// <param name="beforePixels">Tile pixels before the change.</param>
        /// <param name="afterPixels">Tile pixels after the change.</param>
        /// <param name="positions">All (tileX, tileY) positions mapped to this tile.</param>
        public void RecordTileChange(int tileId, byte[] beforePixels, byte[] afterPixels, List<(int tileX, int tileY)> positions)
        {
            _tileChanges[tileId] = new TileChange
            {
                Before = (byte[])beforePixels.Clone(),
                After = (byte[])afterPixels.Clone()
            };
            _tilePositions[tileId] = new List<(int, int)>(positions);

            LoggingService.Debug("Recorded tile change tileId={TileId} mappedPositions={PositionsCount}", tileId, positions?.Count ?? 0);
        }

        // ====================================================================
        // UNDO / REDO
        // ====================================================================

        /// <summary>
        /// Undoes all changes - both non-tile pixels and all tile propagations.
        /// </summary>
        public void Undo()
        {
            var layerPixels = _layer.Surface.Pixels;
            int layerW = _layer.Surface.Width;

            var docName = _layer.Name ?? "(layer)";
            LoggingService.Info("Undo TileMapped change on layer={Layer} desc={Desc} nonTileCount={NonTile} tileChangeCount={TileCount}",
                docName, _description, _nonTileIndices.Count, _tileChanges.Count);

            // Restore non-tile pixel changes first
            for (int i = 0; i < _nonTileIndices.Count; i++)
            {
                int idx = _nonTileIndices[i];
                uint val = _nonTileBefore[i];
                WritePixel(layerPixels, idx, val);
            }

            // Restore tile definitions to their "before" state
            foreach (var (tileId, change) in _tileChanges)
            {
                // Update tile set
                _tileSet.UpdateTilePixels(tileId, change.Before);

                // Update all canvas positions mapped to this tile
                if (_tilePositions.TryGetValue(tileId, out var positions))
                {
                    int tileW = _tileSet.TileWidth;
                    int tileH = _tileSet.TileHeight;

                    foreach (var (tx, ty) in positions)
                    {
                        int docX = tx * tileW;
                        int docY = ty * tileH;
                        WritePixelsToLayer(layerPixels, layerW, _layer.Surface.Height, docX, docY, tileW, tileH, change.Before);
                    }
                }

                LoggingService.Debug("Restored tile {TileId} to before state; mappedPositions={Positions}", tileId, _tilePositions.TryGetValue(tileId, out var p) ? p.Count : 0);
            }

            _layer.UpdatePreview();
        }

        /// <summary>
        /// Redoes all changes - both non-tile pixels and all tile propagations.
        /// </summary>
        public void Redo()
        {
            var layerPixels = _layer.Surface.Pixels;
            int layerW = _layer.Surface.Width;

            var docName = _layer.Name ?? "(layer)";
            LoggingService.Info("Redo TileMapped change on layer={Layer} desc={Desc} nonTileCount={NonTile} tileChangeCount={TileCount}",
                docName, _description, _nonTileIndices.Count, _tileChanges.Count);

            // Apply non-tile pixel changes first
            for (int i = 0; i < _nonTileIndices.Count; i++)
            {
                int idx = _nonTileIndices[i];
                uint val = _nonTileAfter[i];
                WritePixel(layerPixels, idx, val);
            }

            // Update tile definitions to their "after" state
            foreach (var (tileId, change) in _tileChanges)
            {
                // Update tile set
                _tileSet.UpdateTilePixels(tileId, change.After);

                // Update all canvas positions mapped to this tile
                if (_tilePositions.TryGetValue(tileId, out var positions))
                {
                    int tileW = _tileSet.TileWidth;
                    int tileH = _tileSet.TileHeight;

                    foreach (var (tx, ty) in positions)
                    {
                        int docX = tx * tileW;
                        int docY = ty * tileH;
                        WritePixelsToLayer(layerPixels, layerW, _layer.Surface.Height, docX, docY, tileW, tileH, change.After);
                    }
                }

                LoggingService.Debug("Applied tile {TileId} after state; mappedPositions={Positions}", tileId, _tilePositions.TryGetValue(tileId, out var p) ? p.Count : 0);
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

        private static void WritePixelsToLayer(byte[] layerPixels, int layerW, int layerH, int x, int y, int w, int h, byte[] src)
        {
            int layerStride = layerW * 4;

            for (int row = 0; row < h; row++)
            {
                int dstY = y + row;
                if (dstY < 0 || dstY >= layerH) continue;

                int srcOffset = row * w * 4;
                int dstOffset = dstY * layerStride + x * 4;

                int copyWidth = w;
                if (x < 0)
                {
                    int skip = -x;
                    copyWidth -= skip;
                    srcOffset += skip * 4;
                    dstOffset = dstY * layerStride;
                }
                if (x + w > layerW)
                {
                    copyWidth = Math.Max(0, layerW - Math.Max(0, x));
                }

                if (copyWidth > 0 && dstOffset >= 0 && dstOffset + copyWidth * 4 <= layerPixels.Length)
                {
                    Buffer.BlockCopy(src, srcOffset, layerPixels, dstOffset, copyWidth * 4);
                }
            }
        }
    }
}
