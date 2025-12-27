using System;
using System.Collections.Generic;
using System.IO;
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
    /// <para>
    /// Implements <see cref="IDisposableHistoryItem"/> to support memory management.
    /// </para>
    /// </remarks>
    public sealed class TileAwarePixelChangeItem : IDisposableHistoryItem, IRenderResult
    {
        private readonly RasterLayer _layer;
        private readonly TileSet? _tileSet;
        private readonly TileMapping? _mapping;
        private readonly string _description;
        private readonly RectInt32 _bounds;

        // Pixel changes (before/after)
        private byte[]? _pixelsBefore;
        private byte[]? _pixelsAfter;

        // Tile definition changes
        private Dictionary<int, TileChange>? _tileChanges;
        private Dictionary<int, List<(int tileX, int tileY)>>? _tilePositions;

        // Individual pixel changes (for sparse changes without bounds)
        private List<(int idx, uint before, uint after)>? _pixelDeltas;
        private bool _useSparsePixels;

        // Offload state
        private Guid _offloadId = Guid.Empty;
        private bool _isOffloaded;
        private long _offloadedMemorySize; // Track original size for UI/stats

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
        public bool HasChanges => (_pixelsBefore?.Length ?? 0) > 0 || (_pixelDeltas?.Count ?? 0) > 0 || (_tileChanges?.Count ?? 0) > 0 || _isOffloaded;

        /// <inheritdoc/>
        public bool CanPushToHistory => HasChanges;

        // ====================================================================
        // IDisposableHistoryItem IMPLEMENTATION
        // ====================================================================

        /// <summary>
        /// Gets the estimated memory usage in bytes.
        /// </summary>
        public long EstimatedMemoryBytes
        {
            get
            {
                if (_isOffloaded) return 200; // Minimal overhead when offloaded
                
                long total = 100; // Base object overhead
                
                // Pixel arrays
                total += _pixelsBefore?.Length ?? 0;
                total += _pixelsAfter?.Length ?? 0;
                
                // Sparse pixel deltas: 12 bytes per entry (int + uint + uint)
                total += (_pixelDeltas?.Count ?? 0) * 12L;
                
                // Tile changes: each has before + after byte arrays
                if (_tileChanges != null)
                {
                    foreach (var change in _tileChanges.Values)
                    {
                        total += change.Before.Length + change.After.Length + 50; // + overhead
                    }
                }
                
                // Tile positions: each list entry is ~16 bytes
                if (_tilePositions != null)
                {
                    foreach (var positions in _tilePositions.Values)
                    {
                        total += positions.Count * 16L + 30;
                    }
                }
                
                return total;
            }
        }

        /// <inheritdoc/>
        public bool IsOffloaded => _isOffloaded;

        /// <inheritdoc/>
        public Guid OffloadId => _offloadId;

        /// <inheritdoc/>
        public bool Offload(IHistoryOffloadService offloadService)
        {
            if (_isOffloaded) return false;
            if ((_pixelsBefore?.Length ?? 0) == 0 && (_pixelDeltas?.Count ?? 0) == 0 && (_tileChanges?.Count ?? 0) == 0)
                return false;

            try
            {
                _offloadId = Guid.NewGuid();
                _offloadedMemorySize = EstimatedMemoryBytes;

                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                // Write flags and bounds
                bw.Write(_useSparsePixels);
                bw.Write(_bounds.X);
                bw.Write(_bounds.Y);
                bw.Write(_bounds.Width);
                bw.Write(_bounds.Height);

                // Write pixel arrays
                bw.Write(_pixelsBefore?.Length ?? 0);
                if (_pixelsBefore != null && _pixelsBefore.Length > 0)
                    bw.Write(_pixelsBefore);

                bw.Write(_pixelsAfter?.Length ?? 0);
                if (_pixelsAfter != null && _pixelsAfter.Length > 0)
                    bw.Write(_pixelsAfter);

                // Write sparse deltas
                bw.Write(_pixelDeltas?.Count ?? 0);
                if (_pixelDeltas != null)
                {
                    foreach (var (idx, before, after) in _pixelDeltas)
                    {
                        bw.Write(idx);
                        bw.Write(before);
                        bw.Write(after);
                    }
                }

                // Write tile changes
                bw.Write(_tileChanges?.Count ?? 0);
                if (_tileChanges != null)
                {
                    foreach (var (tileId, change) in _tileChanges)
                    {
                        bw.Write(tileId);
                        bw.Write(change.Before.Length);
                        bw.Write(change.Before);
                        bw.Write(change.After.Length);
                        bw.Write(change.After);
                        
                        // Write positions for this tile
                        var positions = _tilePositions?.GetValueOrDefault(tileId);
                        bw.Write(positions?.Count ?? 0);
                        if (positions != null)
                        {
                            foreach (var (tx, ty) in positions)
                            {
                                bw.Write(tx);
                                bw.Write(ty);
                            }
                        }
                    }
                }

                offloadService.WriteData(_offloadId, ms.ToArray());

                // Release memory
                _pixelsBefore = null;
                _pixelsAfter = null;
                _pixelDeltas?.Clear();
                _pixelDeltas = null;
                _tileChanges?.Clear();
                _tileChanges = null;
                _tilePositions?.Clear();
                _tilePositions = null;

                _isOffloaded = true;
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to offload TileAwarePixelChangeItem: {Error}", ex.Message);
                return false;
            }
        }

        /// <inheritdoc/>
        public bool Reload(IHistoryOffloadService offloadService)
        {
            if (!_isOffloaded || _offloadId == Guid.Empty)
                return true;

            try
            {
                var data = offloadService.ReadData(_offloadId);
                if (data == null)
                {
                    LoggingService.Error("Failed to reload TileAwarePixelChangeItem: data not found");
                    return false;
                }

                using var ms = new MemoryStream(data);
                using var br = new BinaryReader(ms);

                // Read flags and bounds (bounds is readonly, skip reading)
                _useSparsePixels = br.ReadBoolean();
                br.ReadInt32(); // bounds.X
                br.ReadInt32(); // bounds.Y
                br.ReadInt32(); // bounds.Width
                br.ReadInt32(); // bounds.Height

                // Read pixel arrays
                int beforeLen = br.ReadInt32();
                _pixelsBefore = beforeLen > 0 ? br.ReadBytes(beforeLen) : [];

                int afterLen = br.ReadInt32();
                _pixelsAfter = afterLen > 0 ? br.ReadBytes(afterLen) : [];

                // Read sparse deltas
                int deltaCount = br.ReadInt32();
                _pixelDeltas = new List<(int, uint, uint)>(deltaCount);
                for (int i = 0; i < deltaCount; i++)
                {
                    int idx = br.ReadInt32();
                    uint before = br.ReadUInt32();
                    uint after = br.ReadUInt32();
                    _pixelDeltas.Add((idx, before, after));
                }

                // Read tile changes
                int tileChangeCount = br.ReadInt32();
                _tileChanges = new Dictionary<int, TileChange>(tileChangeCount);
                _tilePositions = new Dictionary<int, List<(int, int)>>(tileChangeCount);

                for (int i = 0; i < tileChangeCount; i++)
                {
                    int tileId = br.ReadInt32();
                    
                    int beforeTileLen = br.ReadInt32();
                    byte[] beforeTile = br.ReadBytes(beforeTileLen);
                    int afterTileLen = br.ReadInt32();
                    byte[] afterTile = br.ReadBytes(afterTileLen);
                    
                    _tileChanges[tileId] = new TileChange { Before = beforeTile, After = afterTile };

                    int posCount = br.ReadInt32();
                    var positions = new List<(int, int)>(posCount);
                    for (int p = 0; p < posCount; p++)
                    {
                        int tx = br.ReadInt32();
                        int ty = br.ReadInt32();
                        positions.Add((tx, ty));
                    }
                    _tilePositions[tileId] = positions;
                }

                _isOffloaded = false;
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to reload TileAwarePixelChangeItem: {Error}", ex.Message);
                return false;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _pixelsBefore = null;
            _pixelsAfter = null;
            _pixelDeltas?.Clear();
            _pixelDeltas = null;
            _tileChanges?.Clear();
            _tileChanges = null;
            _tilePositions?.Clear();
            _tilePositions = null;
        }

        // ====================================================================
        // CONSTRUCTION
        // ====================================================================

        /// <summary>
        /// Creates a tile-aware pixel change item by capturing the current state of affected tiles.
        /// </summary>
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
            _tileChanges = new();
            _tilePositions = new();
            _pixelDeltas = new();

            // Capture affected tile states
            CaptureAffectedTileStates();
        }

        /// <summary>
        /// Creates a tile-aware pixel change item for sparse pixel changes (tile modifier use case).
        /// </summary>
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
            _tileChanges = new();
            _tilePositions = new();
            _pixelDeltas = new();
        }

        /// <summary>
        /// Adds a single pixel change (for sparse change tracking).
        /// </summary>
        public void AddPixelChange(int byteIdx, uint before, uint after)
        {
            if (_isOffloaded)
                throw new InvalidOperationException("Cannot modify offloaded history item");
            
            if (before != after)
            {
                _pixelDeltas!.Add((byteIdx, before, after));
            }
        }

        /// <summary>
        /// Records a tile change with before/after states and all positions mapped to that tile.
        /// </summary>
        public void RecordTileChange(int tileId, byte[] before, byte[] after, List<(int tileX, int tileY)> positions)
        {
            if (_isOffloaded)
                throw new InvalidOperationException("Cannot modify offloaded history item");
            
            _tileChanges![tileId] = new TileChange
            {
                Before = (byte[])before.Clone(),
                After = (byte[])after.Clone()
            };
            _tilePositions![tileId] = new List<(int, int)>(positions);
        }

        /// <summary>
        /// Sets pre-captured tile before states, overriding the auto-captured states.
        /// </summary>
        public void SetTileBeforeStates(Dictionary<int, byte[]> tileBeforeStates)
        {
            if (_isOffloaded)
                throw new InvalidOperationException("Cannot modify offloaded history item");
            
            if (_tileSet == null || _mapping == null)
                return;

            int tileW = _tileSet.TileWidth;
            int tileH = _tileSet.TileHeight;

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

            _tileChanges!.Clear();
            _tilePositions!.Clear();

            foreach (var tileId in affectedTileIds)
            {
                if (!tileBeforeStates.TryGetValue(tileId, out var beforePixels))
                    continue;

                var afterPixels = _tileSet.GetTilePixels(tileId);
                if (afterPixels == null)
                    continue;

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

        private void CaptureAffectedTileStates()
        {
            if (_tileSet == null || _mapping == null)
                return;

            int tileW = _tileSet.TileWidth;
            int tileH = _tileSet.TileHeight;

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

            foreach (var tileId in affectedTileIds)
            {
                var currentPixels = _tileSet.GetTilePixels(tileId);
                if (currentPixels == null)
                    continue;

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

                byte[] afterPixels = (byte[])currentPixels.Clone();
                byte[] beforePixels = ComputeTileStateFromLayerSnapshot(_pixelsBefore!, tileId, positions);

                _tileChanges![tileId] = new TileChange
                {
                    Before = beforePixels,
                    After = afterPixels
                };
                _tilePositions![tileId] = positions;
            }
        }

        private byte[] ComputeTileStateFromLayerSnapshot(byte[] layerSnapshot, int tileId, List<(int tileX, int tileY)> positions)
        {
            if (_tileSet == null)
                return [];

            int tileW = _tileSet.TileWidth;
            int tileH = _tileSet.TileHeight;

            var result = _tileSet.GetTilePixels(tileId);
            if (result == null)
                return [];

            result = (byte[])result.Clone();

            foreach (var (tx, ty) in positions)
            {
                int tileDocX = tx * tileW;
                int tileDocY = ty * tileH;

                if (tileDocX + tileW <= _bounds.X || tileDocX >= _bounds.X + _bounds.Width ||
                    tileDocY + tileH <= _bounds.Y || tileDocY >= _bounds.Y + _bounds.Height)
                    continue;

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

        /// <summary>
        /// Adds a non-tile pixel change for undo/redo.
        /// </summary>
        public void AddNonTileChange(int byteIdx, uint before, uint after)
        {
            if (_isOffloaded)
                throw new InvalidOperationException("Cannot modify offloaded history item");
            _pixelDeltas!.Add((byteIdx, before, after));
        }

        // ====================================================================
        // UNDO / REDO
        // ====================================================================

        /// <inheritdoc/>
        public void Undo()
        {
            if (_isOffloaded)
                throw new InvalidOperationException("History item must be reloaded before undo");
            
            var layerPixels = _layer.Surface.Pixels;

            var layerName = _layer.Name ?? "(layer)";
            LoggingService.Info("Undo tile-aware change on layer={Layer} desc={Desc} sparse={Sparse} tileChangeCount={TileCount}",
                layerName, _description, _useSparsePixels, _tileChanges?.Count ?? 0);

            if (_useSparsePixels)
            {
                foreach (var (idx, before, _) in _pixelDeltas!)
                {
                    WritePixel(layerPixels, idx, before);
                }
            }
            else
            {
                WritePixelsToLayer(_pixelsBefore!);
            }

            if (_tileSet != null && _tileChanges != null)
            {
                foreach (var (tileId, change) in _tileChanges)
                {
                    _tileSet.UpdateTilePixels(tileId, change.Before);

                    if (_tilePositions?.TryGetValue(tileId, out var positions) == true)
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

                    LoggingService.Debug("Restored tile {TileId}; mappedPositions={Positions}", tileId, _tilePositions?.TryGetValue(tileId, out var p) == true ? p.Count : 0);
                }
            }

            _layer.UpdatePreview();
        }

        /// <inheritdoc/>
        public void Redo()
        {
            if (_isOffloaded)
                throw new InvalidOperationException("History item must be reloaded before redo");
            
            var layerPixels = _layer.Surface.Pixels;

            var layerName = _layer.Name ?? "(layer)";
            LoggingService.Info("Redo tile-aware change on layer={Layer} desc={Desc} sparse={Sparse} tileChangeCount={TileCount}",
                layerName, _description, _useSparsePixels, _tileChanges?.Count ?? 0);

            if (_useSparsePixels)
            {
                foreach (var (idx, _, after) in _pixelDeltas!)
                {
                    WritePixel(layerPixels, idx, after);
                }
            }
            else
            {
                WritePixelsToLayer(_pixelsAfter!);
            }

            if (_tileSet != null && _tileChanges != null)
            {
                foreach (var (tileId, change) in _tileChanges)
                {
                    _tileSet.UpdateTilePixels(tileId, change.After);

                    if (_tilePositions?.TryGetValue(tileId, out var positions) == true)
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

                    LoggingService.Debug("Applied tile {TileId}; mappedPositions={Positions}", tileId, _tilePositions?.TryGetValue(tileId, out var p) == true ? p.Count : 0);
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
