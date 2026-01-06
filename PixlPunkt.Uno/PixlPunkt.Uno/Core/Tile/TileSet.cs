using System;
using System.Collections.Generic;
using System.Linq;
using PixlPunkt.Uno.Core.Logging;

namespace PixlPunkt.Uno.Core.Tile
{
    /// <summary>
    /// Manages a collection of tiles for a document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tile set stores all unique tiles that can be placed on the canvas.
    /// Each tile has a fixed size determined at tile set creation time.
    /// </para>
    /// <para>
    /// Tiles are identified by unique integer IDs. When a tile is deleted,
    /// its ID is not reused to maintain mapping consistency.
    /// </para>
    /// </remarks>
    public sealed class TileSet
    {
        private readonly Dictionary<int, TileDefinition> _tiles = new();
        private int _nextId = 1;

        /// <summary>
        /// Gets the tile width in pixels.
        /// </summary>
        public int TileWidth { get; }

        /// <summary>
        /// Gets the tile height in pixels.
        /// </summary>
        public int TileHeight { get; }

        /// <summary>
        /// Gets the number of tiles in the set.
        /// </summary>
        public int Count => _tiles.Count;

        /// <summary>
        /// Gets all tile IDs in the set.
        /// </summary>
        public IEnumerable<int> TileIds => _tiles.Keys.OrderBy(id => id);

        /// <summary>
        /// Gets all tiles in the set, ordered by ID.
        /// </summary>
        public IEnumerable<TileDefinition> Tiles => _tiles.Values.OrderBy(t => t.Id);

        /// <summary>
        /// Occurs when a tile is added.
        /// </summary>
        public event Action<TileDefinition>? TileAdded;

        /// <summary>
        /// Occurs when a tile is removed.
        /// </summary>
        public event Action<int>? TileRemoved;

        /// <summary>
        /// Occurs when a tile's pixels are updated.
        /// </summary>
        public event Action<TileDefinition>? TileUpdated;

        /// <summary>
        /// Occurs when the tile set is cleared.
        /// </summary>
        public event Action? TileSetCleared;

        /// <summary>
        /// Creates a new tile set with the specified tile dimensions.
        /// </summary>
        /// <param name="tileWidth">Width of each tile in pixels.</param>
        /// <param name="tileHeight">Height of each tile in pixels.</param>
        public TileSet(int tileWidth, int tileHeight)
        {
            if (tileWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(tileWidth), "Tile width must be positive.");
            if (tileHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(tileHeight), "Tile height must be positive.");

            TileWidth = tileWidth;
            TileHeight = tileHeight;

            LoggingService.Debug("TileSet created tileSize={Width}x{Height}", tileWidth, tileHeight);
        }

        /// <summary>
        /// Gets a tile by its ID.
        /// </summary>
        /// <param name="tileId">The tile ID to look up.</param>
        /// <returns>The tile, or null if not found.</returns>
        public TileDefinition? GetTile(int tileId)
        {
            return _tiles.TryGetValue(tileId, out var tile) ? tile : null;
        }

        /// <summary>
        /// Checks if a tile exists with the given ID.
        /// </summary>
        /// <param name="tileId">The tile ID to check.</param>
        /// <returns>True if the tile exists.</returns>
        public bool ContainsTile(int tileId)
        {
            return _tiles.ContainsKey(tileId);
        }

        /// <summary>
        /// Adds a new empty tile to the set.
        /// </summary>
        /// <returns>The ID of the newly created tile.</returns>
        public int AddEmptyTile()
        {
            int id = _nextId++;
            var tile = new TileDefinition(id, TileWidth, TileHeight);
            _tiles[id] = tile;

            LoggingService.Debug("Tile added (empty) tileId={TileId} totalTiles={Count}", id, _tiles.Count);
            TileAdded?.Invoke(tile);
            return id;
        }

        /// <summary>
        /// Adds a new tile with the specified pixel data.
        /// </summary>
        /// <param name="pixels">BGRA pixel data (must match tile dimensions).</param>
        /// <returns>The ID of the newly created tile.</returns>
        public int AddTile(byte[] pixels)
        {
            int id = _nextId++;
            var tile = new TileDefinition(id, TileWidth, TileHeight, pixels);
            _tiles[id] = tile;

            LoggingService.Debug("Tile added tileId={TileId} totalTiles={Count}", id, _tiles.Count);
            TileAdded?.Invoke(tile);
            return id;
        }

        /// <summary>
        /// Adds an existing tile definition to the set.
        /// </summary>
        /// <param name="tile">The tile to add.</param>
        /// <remarks>
        /// The tile's ID will be used. Updates _nextId if necessary.
        /// </remarks>
        internal void AddTileInternal(TileDefinition tile)
        {
            if (tile.Width != TileWidth || tile.Height != TileHeight)
                throw new ArgumentException("Tile dimensions must match tile set dimensions.");

            _tiles[tile.Id] = tile;
            if (tile.Id >= _nextId)
                _nextId = tile.Id + 1;

            LoggingService.Debug("Tile added (internal) tileId={TileId} totalTiles={Count}", tile.Id, _tiles.Count);
            TileAdded?.Invoke(tile);
        }

        /// <summary>
        /// Removes a tile from the set.
        /// </summary>
        /// <param name="tileId">The tile ID to remove.</param>
        /// <returns>True if the tile was removed; false if not found.</returns>
        public bool RemoveTile(int tileId)
        {
            if (_tiles.Remove(tileId))
            {
                LoggingService.Debug("Tile removed tileId={TileId} remainingTiles={Count}", tileId, _tiles.Count);
                TileRemoved?.Invoke(tileId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Duplicates an existing tile.
        /// </summary>
        /// <param name="tileId">The tile ID to duplicate.</param>
        /// <returns>The ID of the new tile, or -1 if source not found.</returns>
        public int DuplicateTile(int tileId)
        {
            if (!_tiles.TryGetValue(tileId, out var source))
                return -1;

            int newId = _nextId++;
            var clone = source.Clone(newId);
            _tiles[newId] = clone;

            LoggingService.Debug("Tile duplicated sourceId={SourceId} newId={NewId}", tileId, newId);
            TileAdded?.Invoke(clone);
            return newId;
        }

        /// <summary>
        /// Updates the pixel data of an existing tile.
        /// </summary>
        /// <param name="tileId">The tile ID to update.</param>
        /// <param name="pixels">New BGRA pixel data.</param>
        /// <returns>True if updated; false if tile not found.</returns>
        public bool UpdateTilePixels(int tileId, byte[] pixels)
        {
            if (!_tiles.TryGetValue(tileId, out var tile))
                return false;

            tile.SetPixels(pixels);
            TileUpdated?.Invoke(tile);
            return true;
        }

        /// <summary>
        /// Clears all tiles from the set.
        /// </summary>
        public void Clear()
        {
            int previousCount = _tiles.Count;
            _tiles.Clear();
            _nextId = 1;

            LoggingService.Debug("TileSet cleared previousCount={PreviousCount}", previousCount);
            TileSetCleared?.Invoke();
        }

        /// <summary>
        /// Gets the pixel data for a tile.
        /// </summary>
        /// <param name="tileId">The tile ID.</param>
        /// <returns>BGRA pixel data, or null if not found.</returns>
        public byte[]? GetTilePixels(int tileId)
        {
            return _tiles.TryGetValue(tileId, out var tile) ? tile.Pixels : null;
        }

        /// <summary>
        /// Finds duplicate tiles in the set.
        /// </summary>
        /// <returns>Groups of tile IDs that have identical pixel data.</returns>
        public IEnumerable<IGrouping<int, int>> FindDuplicates()
        {
            // Group tiles by their pixel data hash
            return _tiles.Values
                .GroupBy(t => ComputePixelHash(t.Pixels), t => t.Id)
                .Where(g => g.Count() > 1);
        }

        /// <summary>
        /// Computes a simple hash of pixel data for duplicate detection.
        /// </summary>
        private static int ComputePixelHash(byte[] pixels)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    hash = hash * 31 + pixels[i];
                    hash = hash * 31 + pixels[i + 1];
                    hash = hash * 31 + pixels[i + 2];
                    hash = hash * 31 + pixels[i + 3];
                }
                return hash;
            }
        }

        /// <summary>
        /// Removes unused tiles that are not referenced in any mapping.
        /// </summary>
        /// <param name="usedTileIds">Set of tile IDs that are in use.</param>
        /// <returns>Number of tiles removed.</returns>
        public int RemoveUnusedTiles(ISet<int> usedTileIds)
        {
            var unused = _tiles.Keys.Where(id => !usedTileIds.Contains(id)).ToList();
            foreach (var id in unused)
            {
                _tiles.Remove(id);
                TileRemoved?.Invoke(id);
            }

            if (unused.Count > 0)
            {
                LoggingService.Info("Removed unused tiles count={RemovedCount} remainingTiles={Count}", unused.Count, _tiles.Count);
            }

            return unused.Count;
        }
    }
}
