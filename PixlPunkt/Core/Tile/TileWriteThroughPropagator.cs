using System;
using System.Collections.Generic;

namespace PixlPunkt.Core.Tile
{
    /// <summary>
    /// Applies tile write-through propagation from a painted layer region into tile definitions,
    /// then re-applies the updated tile pixels to every mapped instance on the layer.
    /// </summary>
    public static class TileWriteThroughPropagator
    {
        /// <summary>
        /// Propagates changed layer pixels into affected mapped tiles and all mapped instances.
        /// </summary>
        /// <param name="layerPixels">Layer BGRA pixel buffer.</param>
        /// <param name="layerWidth">Layer width in pixels.</param>
        /// <param name="layerHeight">Layer height in pixels.</param>
        /// <param name="mapping">Tile mapping for the layer.</param>
        /// <param name="tileSet">Document tile set.</param>
        /// <param name="affectedMinX">Affected min X (inclusive).</param>
        /// <param name="affectedMinY">Affected min Y (inclusive).</param>
        /// <param name="affectedMaxX">Affected max X (inclusive).</param>
        /// <param name="affectedMaxY">Affected max Y (inclusive).</param>
        /// <returns><c>true</c> if at least one mapped tile was affected; otherwise <c>false</c>.</returns>
        public static bool Apply(
            byte[] layerPixels,
            int layerWidth,
            int layerHeight,
            TileMapping mapping,
            TileSet tileSet,
            int affectedMinX,
            int affectedMinY,
            int affectedMaxX,
            int affectedMaxY)
        {
            ArgumentNullException.ThrowIfNull(layerPixels);
            ArgumentNullException.ThrowIfNull(mapping);
            ArgumentNullException.ThrowIfNull(tileSet);

            if (layerWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(layerWidth));
            if (layerHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(layerHeight));
            if (layerPixels.Length < layerWidth * layerHeight * 4)
                throw new ArgumentException("Layer buffer is smaller than layer dimensions.", nameof(layerPixels));
            if (affectedMaxX < affectedMinX || affectedMaxY < affectedMinY)
                return false;

            int tileW = tileSet.TileWidth;
            int tileH = tileSet.TileHeight;

            int startTileX = Math.Max(0, affectedMinX / tileW);
            int startTileY = Math.Max(0, affectedMinY / tileH);
            int endTileX = Math.Min(mapping.Width - 1, affectedMaxX / tileW);
            int endTileY = Math.Min(mapping.Height - 1, affectedMaxY / tileH);

            if (startTileX > endTileX || startTileY > endTileY)
                return false;

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

            if (affectedTileIds.Count == 0)
                return false;

            foreach (var tileId in affectedTileIds)
            {
                var sourceTilePixels = tileSet.GetTilePixels(tileId);
                if (sourceTilePixels == null)
                    continue;

                var positions = mapping.FindTilePositions(tileId);
                if (positions.Count == 0)
                    continue;

                var mergedPixels = (byte[])sourceTilePixels.Clone();

                // Read modified pixels from every affected mapped instance into the canonical tile data.
                foreach (var (tileX, tileY) in positions)
                {
                    int tileDocX = tileX * tileW;
                    int tileDocY = tileY * tileH;

                    if (!RectsIntersect(tileDocX, tileDocY, tileW, tileH, affectedMinX, affectedMinY, affectedMaxX, affectedMaxY))
                    {
                        continue;
                    }

                    int overlapMinX = Math.Max(tileDocX, affectedMinX);
                    int overlapMinY = Math.Max(tileDocY, affectedMinY);
                    int overlapMaxX = Math.Min(tileDocX + tileW - 1, affectedMaxX);
                    int overlapMaxY = Math.Min(tileDocY + tileH - 1, affectedMaxY);

                    for (int docY = overlapMinY; docY <= overlapMaxY; docY++)
                    {
                        if (docY < 0 || docY >= layerHeight)
                            continue;

                        int localY = docY - tileDocY;
                        for (int docX = overlapMinX; docX <= overlapMaxX; docX++)
                        {
                            if (docX < 0 || docX >= layerWidth)
                                continue;

                            int localX = docX - tileDocX;
                            int srcIdx = (docY * layerWidth + docX) * 4;
                            int dstIdx = (localY * tileW + localX) * 4;

                            mergedPixels[dstIdx] = layerPixels[srcIdx];
                            mergedPixels[dstIdx + 1] = layerPixels[srcIdx + 1];
                            mergedPixels[dstIdx + 2] = layerPixels[srcIdx + 2];
                            mergedPixels[dstIdx + 3] = layerPixels[srcIdx + 3];
                        }
                    }
                }

                tileSet.UpdateTilePixels(tileId, mergedPixels);

                // Apply updated tile pixels to every mapped instance.
                foreach (var (tileX, tileY) in positions)
                {
                    int tileDocX = tileX * tileW;
                    int tileDocY = tileY * tileH;
                    WriteTilePixelsToLayer(layerPixels, layerWidth, layerHeight, tileDocX, tileDocY, tileW, tileH, mergedPixels);
                }
            }

            return true;
        }

        private static bool RectsIntersect(
            int tileX,
            int tileY,
            int tileW,
            int tileH,
            int affectedMinX,
            int affectedMinY,
            int affectedMaxX,
            int affectedMaxY)
        {
            return !(tileX + tileW <= affectedMinX ||
                     tileX > affectedMaxX ||
                     tileY + tileH <= affectedMinY ||
                     tileY > affectedMaxY);
        }

        private static void WriteTilePixelsToLayer(
            byte[] layerPixels,
            int layerWidth,
            int layerHeight,
            int docX,
            int docY,
            int tileW,
            int tileH,
            byte[] tilePixels)
        {
            int layerStride = layerWidth * 4;

            for (int row = 0; row < tileH; row++)
            {
                int dstY = docY + row;
                if (dstY < 0 || dstY >= layerHeight)
                    continue;

                int srcOffset = row * tileW * 4;
                int dstOffset = dstY * layerStride + docX * 4;
                int copyWidth = tileW;

                if (docX < 0)
                {
                    int skip = -docX;
                    copyWidth -= skip;
                    srcOffset += skip * 4;
                    dstOffset = dstY * layerStride;
                }

                if (docX + tileW > layerWidth)
                {
                    copyWidth = Math.Max(0, layerWidth - Math.Max(0, docX));
                }

                if (copyWidth > 0 && dstOffset >= 0 && dstOffset + copyWidth * 4 <= layerPixels.Length)
                {
                    Buffer.BlockCopy(tilePixels, srcOffset, layerPixels, dstOffset, copyWidth * 4);
                }
            }
        }
    }
}
