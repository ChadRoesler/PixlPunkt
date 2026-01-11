using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.Tile
{
    /// <summary>
    /// Binary serializer for PixlPunkt tile format (.pxpt).
    /// </summary>
    /// <remarks>
    /// <para>
    /// TileIO provides save/load functionality for the PixlPunkt tile file format (.pxpt),
    /// which stores tiles and their mappings independently from the main document.
    /// This allows tiles to be exported and imported between documents.
    /// </para>
    /// <para><strong>File Format Structure (Version 1):</strong></para>
    /// <list type="number">
    /// <item><strong>Header</strong>:
    ///   <list type="bullet">
    ///   <item>Magic number: 'PXPT' (0x54505850) for format identification</item>
    ///   <item>Version: Int32 = 1</item>
    ///   </list>
    /// </item>
    /// <item><strong>Tile Dimensions</strong>:
    ///   <list type="bullet">
    ///   <item>Tile width (Int32)</item>
    ///   <item>Tile height (Int32)</item>
    ///   </list>
    /// </item>
    /// <item><strong>Tile Data</strong>:
    ///   <list type="bullet">
    ///   <item>Tile count (Int32)</item>
    ///   <item>For each tile: ID (Int32), pixel data length (Int32), pixel data (bytes)</item>
    ///   </list>
    /// </item>
    /// <item><strong>Mapping Data</strong>:
    ///   <list type="bullet">
    ///   <item>Mapping count (Int32) - number of layers with mappings</item>
    ///   <item>For each mapping: layer name (string), width (Int32), height (Int32), sparse entries</item>
    ///   </list>
    /// </item>
    /// </list>
    /// </remarks>
    public static class TileIO
    {
        /// <summary>
        /// Magic number identifying PixlPunkt tile format: 'PXPT' in ASCII (0x54505850).
        /// </summary>
        private const int Magic = 0x54505850;

        /// <summary>
        /// Current file format version.
        /// </summary>
        private const int CurrentVersion = 1;

        /// <summary>
        /// File extension for PixlPunkt tile files.
        /// </summary>
        public const string FileExtension = ".pxpt";

        /// <summary>
        /// Display name for file picker dialogs.
        /// </summary>
        public const string FileTypeDisplayName = "PixlPunkt Tiles";

        //////////////////////////////////////////////////////////////////
        // DATA CLASSES
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Represents exported tile data including tiles and mappings.
        /// </summary>
        public sealed class TileExportData
        {
            /// <summary>Tile width in pixels.</summary>
            public int TileWidth { get; set; }

            /// <summary>Tile height in pixels.</summary>
            public int TileHeight { get; set; }

            /// <summary>List of tiles with their IDs and pixel data.</summary>
            public List<TileData> Tiles { get; set; } = new();

            /// <summary>Layer mappings (layer name to mapping data).</summary>
            public List<LayerMappingData> LayerMappings { get; set; } = new();
        }

        /// <summary>
        /// Represents a single tile's data.
        /// </summary>
        public sealed class TileData
        {
            /// <summary>Original tile ID.</summary>
            public int Id { get; set; }

            /// <summary>BGRA pixel data.</summary>
            public byte[] Pixels { get; set; } = Array.Empty<byte>();
        }

        /// <summary>
        /// Represents tile mapping data for a single layer.
        /// </summary>
        public sealed class LayerMappingData
        {
            /// <summary>Layer name for identification.</summary>
            public string LayerName { get; set; } = string.Empty;

            /// <summary>Mapping grid width (tile columns).</summary>
            public int Width { get; set; }

            /// <summary>Mapping grid height (tile rows).</summary>
            public int Height { get; set; }

            /// <summary>Sparse mapping entries (x, y, tileId).</summary>
            public List<(int X, int Y, int TileId)> Entries { get; set; } = new();
        }

        //////////////////////////////////////////////////////////////////
        // SAVE
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves tile data to the specified file path.
        /// </summary>
        /// <param name="data">The tile export data to save.</param>
        /// <param name="filePath">The target file path (.pxpt extension recommended).</param>
        public static void Save(TileExportData data, string filePath)
        {
            try
            {
                LoggingService.Info("Saving tile export to {FilePath} (tiles={TileCount}, mappings={MappingsCount})", filePath, data.Tiles.Count, data.LayerMappings.Count);
                using var fs = File.Create(filePath);
                Save(data, fs);
                LoggingService.Info($"Saved tile export to {filePath} (tiles={data.Tiles.Count}, mappings={data.LayerMappings.Count})");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to save tile file: {filePath}", ex);
                throw;
            }
        }

        /// <summary>
        /// Saves tile data to the specified stream.
        /// </summary>
        /// <param name="data">The tile export data to save.</param>
        /// <param name="stream">The target stream (must support writing).</param>
        public static void Save(TileExportData data, Stream stream)
        {
            using var bw = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            // Header
            bw.Write(Magic);
            bw.Write(CurrentVersion);

            // Tile dimensions
            bw.Write(data.TileWidth);
            bw.Write(data.TileHeight);

            // Tile data
            bw.Write(data.Tiles.Count);
            foreach (var tile in data.Tiles)
            {
                bw.Write(tile.Id);
                bw.Write(tile.Pixels.Length);
                bw.Write(tile.Pixels);
            }

            // Layer mappings
            bw.Write(data.LayerMappings.Count);
            foreach (var mapping in data.LayerMappings)
            {
                bw.Write(mapping.LayerName);
                bw.Write(mapping.Width);
                bw.Write(mapping.Height);

                // Write sparse entries
                bw.Write(mapping.Entries.Count);
                foreach (var (x, y, tileId) in mapping.Entries)
                {
                    bw.Write(x);
                    bw.Write(y);
                    bw.Write(tileId);
                }
            }

            bw.Flush();
        }

        //////////////////////////////////////////////////////////////////
        // LOAD
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads tile data from the specified file path.
        /// </summary>
        /// <param name="filePath">The .pxpt file to load.</param>
        /// <returns>The loaded tile export data.</returns>
        public static TileExportData Load(string filePath)
        {
            try
            {
                LoggingService.Info("Loading tile file {FilePath}", filePath);
                using var fs = File.OpenRead(filePath);
                var data = Load(fs);
                LoggingService.Info($"Loaded tile file {filePath} (tiles={data.Tiles.Count}, mappings={data.LayerMappings.Count})");
                return data;
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to load tile file: {filePath}", ex);
                throw;
            }
        }

        /// <summary>
        /// Loads tile data from the specified stream.
        /// </summary>
        /// <param name="stream">The stream containing .pxpt tile data.</param>
        /// <returns>The loaded tile export data.</returns>
        public static TileExportData Load(Stream stream)
        {
            using var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var magic = br.ReadInt32();
            if (magic != Magic)
            {
                LoggingService.Warning($"Tile file has invalid magic: {magic}");
                throw new InvalidDataException("Not a PixlPunkt tile (.pxpt) file.");
            }

            var version = br.ReadInt32();
            if (version < 1 || version > CurrentVersion)
            {
                LoggingService.Warning($"Tile file has unsupported version: {version}");
                throw new InvalidDataException($"Unsupported tile file version: {version}");
            }

            var data = new TileExportData
            {
                TileWidth = br.ReadInt32(),
                TileHeight = br.ReadInt32()
            };

            // Read tiles
            int tileCount = br.ReadInt32();
            for (int i = 0; i < tileCount; i++)
            {
                var tile = new TileData
                {
                    Id = br.ReadInt32()
                };
                int pixelLength = br.ReadInt32();
                tile.Pixels = br.ReadBytes(pixelLength);
                data.Tiles.Add(tile);
            }

            // Read layer mappings
            int mappingCount = br.ReadInt32();
            for (int i = 0; i < mappingCount; i++)
            {
                var mapping = new LayerMappingData
                {
                    LayerName = br.ReadString(),
                    Width = br.ReadInt32(),
                    Height = br.ReadInt32()
                };

                int entryCount = br.ReadInt32();
                for (int j = 0; j < entryCount; j++)
                {
                    int x = br.ReadInt32();
                    int y = br.ReadInt32();
                    int tileId = br.ReadInt32();
                    mapping.Entries.Add((x, y, tileId));
                }

                data.LayerMappings.Add(mapping);
            }

            return data;
        }

        //////////////////////////////////////////////////////////////////
        // HELPER METHODS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates export data from a TileSet and layer mappings.
        /// </summary>
        /// <param name="tileSet">The tile set to export.</param>
        /// <param name="layerMappings">Dictionary of layer names to their tile mappings.</param>
        /// <returns>Export data ready to be saved.</returns>
        public static TileExportData CreateExportData(
            TileSet tileSet,
            IEnumerable<(string layerName, TileMapping? mapping)> layerMappings)
        {
            var data = new TileExportData
            {
                TileWidth = tileSet.TileWidth,
                TileHeight = tileSet.TileHeight
            };

            // Export all tiles
            foreach (var tile in tileSet.Tiles)
            {
                data.Tiles.Add(new TileData
                {
                    Id = tile.Id,
                    Pixels = (byte[])tile.Pixels.Clone()
                });
            }

            // Export layer mappings
            foreach (var (layerName, mapping) in layerMappings)
            {
                if (mapping == null) continue;

                var mappingData = new LayerMappingData
                {
                    LayerName = layerName,
                    Width = mapping.Width,
                    Height = mapping.Height
                };

                // Get sparse entries
                for (int y = 0; y < mapping.Height; y++)
                {
                    for (int x = 0; x < mapping.Width; x++)
                    {
                        int tileId = mapping.GetTileId(x, y);
                        if (tileId >= 0)
                        {
                            mappingData.Entries.Add((x, y, tileId));
                        }
                    }
                }

                if (mappingData.Entries.Count > 0)
                {
                    data.LayerMappings.Add(mappingData);
                }
            }

            return data;
        }

        /// <summary>
        /// Applies imported tile data to a document in "Add" mode.
        /// Tiles are added with new IDs, mappings are adjusted accordingly.
        /// </summary>
        /// <param name="data">The imported tile data.</param>
        /// <param name="tileSet">The target tile set to add tiles to.</param>
        /// <returns>Mapping from old tile IDs to new tile IDs.</returns>
        public static Dictionary<int, int> AddTilesToSet(TileExportData data, TileSet tileSet)
        {
            var idMapping = new Dictionary<int, int>();

            try
            {
                // Validate tile dimensions
                if (data.TileWidth != tileSet.TileWidth || data.TileHeight != tileSet.TileHeight)
                {
                    var msg = $"Tile dimensions mismatch. Expected {tileSet.TileWidth}x{tileSet.TileHeight}, got {data.TileWidth}x{data.TileHeight}.";
                    LoggingService.Error(msg);
                    throw new InvalidOperationException(msg);
                }

                // Add each tile with a new ID
                foreach (var tileData in data.Tiles)
                {
                    int newId = tileSet.AddTile(tileData.Pixels);
                    idMapping[tileData.Id] = newId;
                }

                LoggingService.Info($"Added {data.Tiles.Count} tiles to tileset (newIds={idMapping.Count})");
                return idMapping;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to add tiles to set", ex);
                throw;
            }
        }

        /// <summary>
        /// Replaces all tiles in a tile set with imported data.
        /// </summary>
        /// <param name="data">The imported tile data.</param>
        /// <param name="tileSet">The target tile set to replace.</param>
        public static void ReplaceTileSet(TileExportData data, TileSet tileSet)
        {
            try
            {
                // Validate tile dimensions match
                if (data.TileWidth != tileSet.TileWidth || data.TileHeight != tileSet.TileHeight)
                {
                    var msg = $"Tile dimensions mismatch. Expected {tileSet.TileWidth}x{tileSet.TileHeight}, got {data.TileWidth}x{data.TileHeight}.";
                    LoggingService.Error(msg);
                    throw new InvalidOperationException(msg);
                }

                // Clear existing tiles
                tileSet.Clear();

                // Add tiles with their original IDs
                foreach (var tileData in data.Tiles)
                {
                    var tile = new TileDefinition(tileData.Id, data.TileWidth, data.TileHeight, tileData.Pixels);
                    tileSet.AddTileInternal(tile);
                }

                LoggingService.Info($"Replaced tileset with {data.Tiles.Count} tiles");
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to replace tileset", ex);
                throw;
            }
        }
    }
}
