using System;
using PixlPunkt.Core.Tile;

namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// Lightweight RGBA image wrapper for the voxel pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stores pixel data in RGBA byte order (4 bytes per pixel, row-major).
    /// Coordinates use column/row addressing where (0, 0) is the top-left corner.
    /// </para>
    /// <para>
    /// Use <see cref="FromTile"/> to convert from a <see cref="TileDefinition"/>
    /// (which uses BGRA byte order) into the RGBA format used by the voxel builder.
    /// </para>
    /// </remarks>
    public sealed class ImageData
    {
        /// <summary>Image width in pixels.</summary>
        public readonly int Width;

        /// <summary>Image height in pixels.</summary>
        public readonly int Height;

        /// <summary>
        /// RGBA byte array. Length is always <c>Width * Height * 4</c>.
        /// </summary>
        public readonly byte[] Rgba;

        /// <summary>
        /// Creates a new image from raw RGBA pixel data.
        /// </summary>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="rgba">RGBA byte array (must be exactly <c>width * height * 4</c> bytes).</param>
        /// <exception cref="ArgumentNullException">If <paramref name="rgba"/> is null.</exception>
        /// <exception cref="ArgumentException">If buffer length doesn't match dimensions.</exception>
        public ImageData(int width, int height, byte[] rgba)
        {
            Width = width;
            Height = height;
            Rgba = rgba ?? throw new ArgumentNullException(nameof(rgba));

            if (rgba.Length != width * height * 4)
                throw new ArgumentException(
                    $"RGBA buffer size mismatch: expected {width * height * 4} bytes " +
                    $"for {width}×{height}, got {rgba.Length}.");
        }

        /// <summary>
        /// Creates an empty (transparent) image with the specified dimensions.
        /// </summary>
        public ImageData(int width, int height)
            : this(width, height, new byte[width * height * 4])
        {
        }

        /// <summary>
        /// Creates an <see cref="ImageData"/> from a BGRA <see cref="TileDefinition"/>,
        /// converting pixel data to RGBA byte order.
        /// </summary>
        /// <param name="tile">Source tile with BGRA pixel data.</param>
        /// <returns>A new image containing the tile's pixels in RGBA order.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="tile"/> is null.</exception>
        public static ImageData FromTile(TileDefinition tile)
        {
            ArgumentNullException.ThrowIfNull(tile);

            int length = tile.Width * tile.Height * 4;
            var rgba = new byte[length];

            for (int i = 0; i < length; i += 4)
            {
                rgba[i]     = tile.Pixels[i + 2]; // R ← BGRA[2]
                rgba[i + 1] = tile.Pixels[i + 1]; // G ← BGRA[1]
                rgba[i + 2] = tile.Pixels[i];     // B ← BGRA[0]
                rgba[i + 3] = tile.Pixels[i + 3]; // A ← BGRA[3]
            }

            return new ImageData(tile.Width, tile.Height, rgba);
        }

        /// <summary>
        /// Converts this RGBA image to a BGRA byte array suitable for
        /// <see cref="TileDefinition"/> or <see cref="Imaging.PixelSurface"/>.
        /// </summary>
        /// <returns>A new BGRA byte array.</returns>
        public byte[] ToBgraBytes()
        {
            var bgra = new byte[Rgba.Length];

            for (int i = 0; i < Rgba.Length; i += 4)
            {
                bgra[i]     = Rgba[i + 2]; // B ← RGBA[2]
                bgra[i + 1] = Rgba[i + 1]; // G ← RGBA[1]
                bgra[i + 2] = Rgba[i];     // R ← RGBA[0]
                bgra[i + 3] = Rgba[i + 3]; // A ← RGBA[3]
            }

            return bgra;
        }

        /// <summary>
        /// Gets the color at the specified pixel position.
        /// </summary>
        /// <param name="col">Column (X coordinate, 0-based).</param>
        /// <param name="row">Row (Y coordinate, 0-based).</param>
        /// <returns>RGBA color at the position.</returns>
        public Rgba32 GetPixel(int col, int row)
        {
            int i = (col + row * Width) * 4;
            return new Rgba32(Rgba[i], Rgba[i + 1], Rgba[i + 2], Rgba[i + 3]);
        }

        /// <summary>
        /// Sets the color at the specified pixel position.
        /// </summary>
        /// <param name="col">Column (X coordinate, 0-based).</param>
        /// <param name="row">Row (Y coordinate, 0-based).</param>
        /// <param name="c">RGBA color to write.</param>
        public void SetPixel(int col, int row, Rgba32 c)
        {
            int i = (col + row * Width) * 4;
            Rgba[i] = c.R;
            Rgba[i + 1] = c.G;
            Rgba[i + 2] = c.B;
            Rgba[i + 3] = c.A;
        }
    }
}
