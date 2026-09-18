using System;
using System.Collections.Generic;
using PixlPunkt.Core.Document.Layer;
using Windows.Graphics;

namespace PixlPunkt.Core.Tile
{
    /// <summary>
    /// Tile-set bookkeeping around edits to a tile-mapped layer: which tiles a rectangle touches,
    /// snapshotting and restoring their definitions, and pushing layer pixels back through the
    /// mapping. History items use this so undo/redo restores tiles as well as layer pixels.
    /// </summary>
    public static class TileLayerPropagation
    {
        /// <summary>Tile ids whose mapped cells intersect <paramref name="bounds"/> on <paramref name="layer"/>.</summary>
        public static HashSet<int> AffectedTileIds(RasterLayer layer, TileSet tileSet, RectInt32 bounds)
        {
            var ids = new HashSet<int>();
            var mapping = layer.TileMapping;
            if (mapping == null || bounds.Width <= 0 || bounds.Height <= 0) return ids;

            int tileW = tileSet.TileWidth, tileH = tileSet.TileHeight;
            int startTileX = Math.Max(0, bounds.X / tileW);
            int startTileY = Math.Max(0, bounds.Y / tileH);
            int endTileX = Math.Min(mapping.Width - 1, (bounds.X + bounds.Width - 1) / tileW);
            int endTileY = Math.Min(mapping.Height - 1, (bounds.Y + bounds.Height - 1) / tileH);

            for (int ty = startTileY; ty <= endTileY; ty++)
                for (int tx = startTileX; tx <= endTileX; tx++)
                {
                    int id = mapping.GetTileId(tx, ty);
                    if (id >= 0) ids.Add(id);
                }
            return ids;
        }

        /// <summary>Clones the pixel definitions of the given tiles.</summary>
        public static Dictionary<int, byte[]> CaptureTileStates(TileSet tileSet, IEnumerable<int> ids)
        {
            var states = new Dictionary<int, byte[]>();
            foreach (var id in ids)
            {
                var px = tileSet.GetTilePixels(id);
                if (px != null) states[id] = (byte[])px.Clone();
            }
            return states;
        }

        /// <summary>
        /// Snapshots only the tiles that <paramref name="bounds"/> touches, or null when the layer
        /// is not tile-mapped. This is what every selection history item captures.
        /// </summary>
        public static Dictionary<int, byte[]>? CaptureAffectedTiles(RasterLayer layer, TileSet? tileSet, RectInt32 bounds)
        {
            if (tileSet == null || layer.TileMapping == null) return null;
            return CaptureTileStates(tileSet, AffectedTileIds(layer, tileSet, bounds));
        }

        /// <summary>
        /// Writes changed layer pixels in <paramref name="bounds"/> into the tiles mapped there and
        /// into every other cell that uses those tiles.
        /// </summary>
        public static void PropagateFromLayer(RasterLayer layer, TileSet? tileSet, RectInt32 bounds)
        {
            var mapping = layer.TileMapping;
            if (mapping == null || tileSet == null || bounds.Width <= 0 || bounds.Height <= 0) return;

            var surf = layer.Surface;
            TileWriteThroughPropagator.Apply(
                surf.Pixels, surf.Width, surf.Height, mapping, tileSet,
                bounds.X, bounds.Y, bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1);
        }

        /// <summary>
        /// Restores tile definitions and writes each restored tile back into every cell of the
        /// layer that maps to it.
        /// </summary>
        public static void RestoreTileStates(RasterLayer layer, TileSet? tileSet, Dictionary<int, byte[]>? states)
        {
            if (states == null || states.Count == 0 || tileSet == null) return;
            var mapping = layer.TileMapping;
            if (mapping == null) return;

            int tileW = tileSet.TileWidth, tileH = tileSet.TileHeight;
            var surf = layer.Surface;

            foreach (var (id, pixels) in states)
            {
                tileSet.UpdateTilePixels(id, pixels);

                for (int ty = 0; ty < mapping.Height; ty++)
                    for (int tx = 0; tx < mapping.Width; tx++)
                        if (mapping.GetTileId(tx, ty) == id)
                            WriteTileToLayer(surf.Pixels, surf.Width, surf.Height, tx * tileW, ty * tileH, tileW, tileH, pixels);
            }
        }

        private static void WriteTileToLayer(byte[] layerPixels, int layerW, int layerH, int docX, int docY, int tileW, int tileH, byte[] tile)
        {
            for (int py = 0; py < tileH; py++)
            {
                int y = docY + py;
                if ((uint)y >= (uint)layerH) continue;
                int copyW = Math.Min(tileW, layerW - docX);
                if (copyW <= 0) continue;
                Buffer.BlockCopy(tile, py * tileW * 4, layerPixels, (y * layerW + docX) * 4, copyW * 4);
            }
        }
    }
}
