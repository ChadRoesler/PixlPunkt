using System;
using System.Collections.Generic;
using PixlPunkt.Core.Tile;

namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// Builds orthographic voxel input images from tile mappings.
    /// </summary>
    /// <remarks>
    /// Produces a tile-cell image where each mapped tile becomes one pixel.
    /// This keeps voxel generation aligned to the document's tile grid and
    /// avoids exploding volume size when tiles are larger than 1x1 pixels.
    /// </remarks>
    public static class TileMappingOrthoImageBuilder
    {
        /// <summary>
        /// Builds an <see cref="ImageData"/> from a <see cref="TileMapping"/> at tile-cell resolution.
        /// </summary>
        /// <param name="mapping">Tile mapping to convert.</param>
        /// <param name="tileSet">Tile set used to resolve tile IDs.</param>
        /// <returns>
        /// An RGBA image with dimensions <c>mapping.Width × mapping.Height</c>.
        /// Empty mapping cells are transparent. Mapped cells use a representative
        /// color computed from the referenced tile's opaque pixels.
        /// </returns>
        public static ImageData BuildTileCellImage(TileMapping mapping, TileSet tileSet)
        {
            ArgumentNullException.ThrowIfNull(mapping);
            ArgumentNullException.ThrowIfNull(tileSet);

            var image = new ImageData(mapping.Width, mapping.Height);
            var colorCache = new Dictionary<int, Rgba32>();

            for (int y = 0; y < mapping.Height; y++)
            {
                for (int x = 0; x < mapping.Width; x++)
                {
                    int tileId = mapping.GetTileId(x, y);
                    if (tileId < 0)
                        continue;

                    if (!colorCache.TryGetValue(tileId, out var color))
                    {
                        color = GetRepresentativeTileColor(tileSet.GetTile(tileId));
                        colorCache[tileId] = color;
                    }

                    if (color.A == 0)
                        continue;

                    image.SetPixel(x, y, color);
                }
            }

            return image;
        }

        private static Rgba32 GetRepresentativeTileColor(TileDefinition? tile)
        {
            if (tile == null || tile.Pixels == null || tile.Pixels.Length == 0)
                return default;

            long sumR = 0;
            long sumG = 0;
            long sumB = 0;
            long sumA = 0;

            var pixels = tile.Pixels; // BGRA
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte a = pixels[i + 3];
                if (a == 0)
                    continue;

                sumB += pixels[i + 0] * a;
                sumG += pixels[i + 1] * a;
                sumR += pixels[i + 2] * a;
                sumA += a;
            }

            if (sumA <= 0)
                return default;

            byte r = (byte)Math.Clamp((int)((sumR + (sumA / 2)) / sumA), 0, 255);
            byte g = (byte)Math.Clamp((int)((sumG + (sumA / 2)) / sumA), 0, 255);
            byte b = (byte)Math.Clamp((int)((sumB + (sumA / 2)) / sumA), 0, 255);

            // Treat mapped tiles as fully solid for silhouette + color-linking.
            return new Rgba32(r, g, b, 255);
        }
    }
}
