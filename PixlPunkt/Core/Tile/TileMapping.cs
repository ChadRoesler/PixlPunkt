using System;
using System.Collections.Generic;

namespace PixlPunkt.Core.Tile
{
    /// <summary>
    /// Stores tile mappings for a layer - which tile is placed at each grid position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each layer can have its own tile mapping, allowing different tile arrangements
    /// per layer. The mapping is a sparse grid where -1 indicates no tile at that position.
    /// </para>
    /// <para>
    /// The mapping grid dimensions match the document's tile counts, not pixel dimensions.
    /// </para>
    /// </remarks>
    public sealed class TileMapping
    {
        private readonly int[,] _grid;

        /// <summary>
        /// Gets the number of tile columns in the mapping.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the number of tile rows in the mapping.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Occurs when a mapping is changed.
        /// </summary>
        public event Action<int, int, int>? MappingChanged;

        /// <summary>
        /// Occurs when the mapping is cleared.
        /// </summary>
        public event Action? MappingCleared;

        /// <summary>
        /// Creates a new tile mapping with the specified dimensions.
        /// </summary>
        /// <param name="width">Number of tile columns.</param>
        /// <param name="height">Number of tile rows.</param>
        public TileMapping(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

            Width = width;
            Height = height;
            _grid = new int[width, height];

            // Initialize all positions to -1 (no tile)
            Clear();
        }

        /// <summary>
        /// Gets the tile ID at the specified grid position.
        /// </summary>
        /// <param name="tileX">Tile grid X coordinate.</param>
        /// <param name="tileY">Tile grid Y coordinate.</param>
        /// <returns>The tile ID, or -1 if no tile is mapped or position is out of bounds.</returns>
        public int GetTileId(int tileX, int tileY)
        {
            if ((uint)tileX >= (uint)Width || (uint)tileY >= (uint)Height)
                return -1;

            return _grid[tileX, tileY];
        }

        /// <summary>
        /// Sets the tile mapping at the specified grid position.
        /// </summary>
        /// <param name="tileX">Tile grid X coordinate.</param>
        /// <param name="tileY">Tile grid Y coordinate.</param>
        /// <param name="tileId">The tile ID to map, or -1 to clear.</param>
        public void SetTileId(int tileX, int tileY, int tileId)
        {
            if ((uint)tileX >= (uint)Width || (uint)tileY >= (uint)Height)
                return;

            int oldId = _grid[tileX, tileY];
            if (oldId == tileId)
                return;

            _grid[tileX, tileY] = tileId;
            MappingChanged?.Invoke(tileX, tileY, tileId);
        }

        /// <summary>
        /// Clears the tile mapping at the specified grid position.
        /// </summary>
        /// <param name="tileX">Tile grid X coordinate.</param>
        /// <param name="tileY">Tile grid Y coordinate.</param>
        public void ClearTile(int tileX, int tileY)
        {
            SetTileId(tileX, tileY, -1);
        }

        /// <summary>
        /// Clears all tile mappings.
        /// </summary>
        public void Clear()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    _grid[x, y] = -1;
                }
            }
            MappingCleared?.Invoke();
        }

        /// <summary>
        /// Checks if any tiles are mapped.
        /// </summary>
        /// <returns>True if at least one position has a tile mapped.</returns>
        public bool HasAnyMappings()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (_grid[x, y] >= 0)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets all tile IDs that are used in this mapping.
        /// </summary>
        /// <returns>Set of unique tile IDs (excluding -1).</returns>
        public HashSet<int> GetUsedTileIds()
        {
            var used = new HashSet<int>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int id = _grid[x, y];
                    if (id >= 0)
                        used.Add(id);
                }
            }
            return used;
        }

        /// <summary>
        /// Finds all grid positions where a specific tile is mapped.
        /// </summary>
        /// <param name="tileId">The tile ID to search for.</param>
        /// <returns>List of (tileX, tileY) positions.</returns>
        public List<(int tileX, int tileY)> FindTilePositions(int tileId)
        {
            var positions = new List<(int, int)>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (_grid[x, y] == tileId)
                        positions.Add((x, y));
                }
            }
            return positions;
        }

        /// <summary>
        /// Replaces all occurrences of one tile ID with another.
        /// </summary>
        /// <param name="oldTileId">The tile ID to replace.</param>
        /// <param name="newTileId">The replacement tile ID.</param>
        /// <returns>Number of positions updated.</returns>
        public int ReplaceTileId(int oldTileId, int newTileId)
        {
            int count = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (_grid[x, y] == oldTileId)
                    {
                        _grid[x, y] = newTileId;
                        MappingChanged?.Invoke(x, y, newTileId);
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Removes all mappings for a specific tile ID.
        /// </summary>
        /// <param name="tileId">The tile ID to remove.</param>
        /// <returns>Number of positions cleared.</returns>
        public int RemoveTileId(int tileId)
        {
            return ReplaceTileId(tileId, -1);
        }

        /// <summary>
        /// Resizes the mapping grid, preserving existing mappings where possible.
        /// </summary>
        /// <param name="newWidth">New number of tile columns.</param>
        /// <param name="newHeight">New number of tile rows.</param>
        /// <returns>A new TileMapping with the resized dimensions.</returns>
        public TileMapping Resize(int newWidth, int newHeight)
        {
            var newMapping = new TileMapping(newWidth, newHeight);

            int copyWidth = Math.Min(Width, newWidth);
            int copyHeight = Math.Min(Height, newHeight);

            for (int x = 0; x < copyWidth; x++)
            {
                for (int y = 0; y < copyHeight; y++)
                {
                    newMapping._grid[x, y] = _grid[x, y];
                }
            }

            return newMapping;
        }

        /// <summary>
        /// Creates a deep copy of this mapping.
        /// </summary>
        /// <returns>A new TileMapping with copied data.</returns>
        public TileMapping Clone()
        {
            var clone = new TileMapping(Width, Height);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    clone._grid[x, y] = _grid[x, y];
                }
            }
            return clone;
        }

        /// <summary>
        /// Gets the raw grid data for serialization.
        /// </summary>
        /// <returns>Flattened array of tile IDs in row-major order.</returns>
        internal int[] ToArray()
        {
            var arr = new int[Width * Height];
            int i = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    arr[i++] = _grid[x, y];
                }
            }
            return arr;
        }

        /// <summary>
        /// Loads grid data from a flattened array.
        /// </summary>
        /// <param name="data">Flattened array of tile IDs in row-major order.</param>
        internal void FromArray(int[] data)
        {
            if (data.Length != Width * Height)
                throw new ArgumentException("Array length must match grid dimensions.");

            int i = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    _grid[x, y] = data[i++];
                }
            }
        }
    }
}
