using System;

namespace PixlPunkt.Core.Tile
{
    /// <summary>
    /// Represents a single tile in the tile set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tile stores a fixed-size pixel region that can be placed on the canvas
    /// and referenced by <see cref="TileMapping"/> for tile-based editing.
    /// </para>
    /// <para>
    /// Tiles are immutable in dimensions but their pixel data can be updated.
    /// Each tile has a unique ID within its <see cref="TileSet"/>.
    /// </para>
    /// </remarks>
    public sealed class TileDefinition
    {
        /// <summary>
        /// Gets the unique identifier for this tile within its tile set.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the tile width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the tile height in pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets or sets the BGRA pixel data for this tile.
        /// </summary>
        /// <remarks>
        /// Array length is always Width * Height * 4 bytes.
        /// </remarks>
        public byte[] Pixels { get; private set; }

        /// <summary>
        /// Gets or sets the optional thumbnail for preview display.
        /// </summary>
        /// <remarks>
        /// May be null if thumbnail hasn't been generated yet.
        /// Typically a scaled-down version of the pixels for UI display.
        /// </remarks>
        public byte[]? Thumbnail { get; set; }

        /// <summary>
        /// Gets or sets the optional name/label for this tile.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Creates a new tile with the specified dimensions and pixel data.
        /// </summary>
        /// <param name="id">Unique tile identifier.</param>
        /// <param name="width">Tile width in pixels.</param>
        /// <param name="height">Tile height in pixels.</param>
        /// <param name="pixels">BGRA pixel data (must be width * height * 4 bytes).</param>
        /// <exception cref="ArgumentOutOfRangeException">If dimensions are invalid.</exception>
        /// <exception cref="ArgumentException">If pixel array size doesn't match dimensions.</exception>
        public TileDefinition(int id, int width, int height, byte[] pixels)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Tile width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Tile height must be positive.");

            int expectedSize = width * height * 4;
            if (pixels == null || pixels.Length != expectedSize)
                throw new ArgumentException($"Pixel array must be exactly {expectedSize} bytes for {width}x{height} tile.", nameof(pixels));

            Id = id;
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        /// <summary>
        /// Creates a new empty (transparent) tile with the specified dimensions.
        /// </summary>
        /// <param name="id">Unique tile identifier.</param>
        /// <param name="width">Tile width in pixels.</param>
        /// <param name="height">Tile height in pixels.</param>
        public TileDefinition(int id, int width, int height)
            : this(id, width, height, new byte[width * height * 4])
        {
        }

        /// <summary>
        /// Updates the pixel data for this tile.
        /// </summary>
        /// <param name="pixels">New BGRA pixel data (must match tile dimensions).</param>
        /// <exception cref="ArgumentException">If pixel array size doesn't match dimensions.</exception>
        public void SetPixels(byte[] pixels)
        {
            int expectedSize = Width * Height * 4;
            if (pixels == null || pixels.Length != expectedSize)
                throw new ArgumentException($"Pixel array must be exactly {expectedSize} bytes.", nameof(pixels));

            Pixels = pixels;
            Thumbnail = null; // Invalidate thumbnail
        }

        /// <summary>
        /// Creates a deep copy of this tile with a new ID.
        /// </summary>
        /// <param name="newId">The ID for the cloned tile.</param>
        /// <returns>A new tile with copied pixel data.</returns>
        public TileDefinition Clone(int newId)
        {
            var clonedPixels = new byte[Pixels.Length];
            Buffer.BlockCopy(Pixels, 0, clonedPixels, 0, Pixels.Length);

            return new TileDefinition(newId, Width, Height, clonedPixels)
            {
                Name = Name != null ? $"{Name} (copy)" : null
            };
        }

        /// <summary>
        /// Checks if this tile is empty (all pixels are fully transparent).
        /// </summary>
        /// <returns>True if all pixels have alpha = 0.</returns>
        public bool IsEmpty()
        {
            for (int i = 3; i < Pixels.Length; i += 4)
            {
                if (Pixels[i] != 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets a pixel color at the specified position within the tile.
        /// </summary>
        /// <param name="x">X coordinate (0 to Width-1).</param>
        /// <param name="y">Y coordinate (0 to Height-1).</param>
        /// <returns>BGRA color value.</returns>
        public uint GetPixel(int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                return 0;

            int i = (y * Width + x) * 4;
            return (uint)(Pixels[i] | (Pixels[i + 1] << 8) | (Pixels[i + 2] << 16) | (Pixels[i + 3] << 24));
        }

        /// <summary>
        /// Sets a pixel color at the specified position within the tile.
        /// </summary>
        /// <param name="x">X coordinate (0 to Width-1).</param>
        /// <param name="y">Y coordinate (0 to Height-1).</param>
        /// <param name="bgra">BGRA color value.</param>
        public void SetPixel(int x, int y, uint bgra)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                return;

            int i = (y * Width + x) * 4;
            Pixels[i] = (byte)(bgra & 0xFF);
            Pixels[i + 1] = (byte)((bgra >> 8) & 0xFF);
            Pixels[i + 2] = (byte)((bgra >> 16) & 0xFF);
            Pixels[i + 3] = (byte)((bgra >> 24) & 0xFF);
        }
    }
}
