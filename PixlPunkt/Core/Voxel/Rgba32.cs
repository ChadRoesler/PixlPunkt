using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PixlPunkt.Core.Structs;

namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// Lightweight RGBA pixel struct for the voxel pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses RGBA byte order internally for clarity in the voxel/3D pipeline.
    /// Convert to/from <see cref="Bgra"/> when interfacing with
    /// <see cref="Tile.TileDefinition"/> or <see cref="Imaging.PixelSurface"/>.
    /// </para>
    /// <para>
    /// Memory layout: Red, Green, Blue, Alpha (4 bytes, sequential).
    /// </para>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Rgba32(byte R, byte G, byte B, byte A)
    {
        /// <summary>Fully transparent pixel (all components zero).</summary>
        public static readonly Rgba32 Transparent = new(0, 0, 0, 0);

        /// <summary>
        /// Creates a fully opaque RGBA color.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rgba32 Opaque(byte r, byte g, byte b) => new(r, g, b, 255);

        /// <summary>
        /// Reads one pixel from a BGRA byte buffer (e.g. <see cref="Tile.TileDefinition.Pixels"/>).
        /// </summary>
        /// <param name="bgra">Source BGRA byte array.</param>
        /// <param name="offset">Byte offset (must be a multiple of 4).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rgba32 FromBgraBytes(byte[] bgra, int offset)
            => new(bgra[offset + 2], bgra[offset + 1], bgra[offset], bgra[offset + 3]);

        /// <summary>
        /// Writes this pixel into a BGRA byte buffer.
        /// </summary>
        /// <param name="bgra">Destination BGRA byte array.</param>
        /// <param name="offset">Byte offset (must be a multiple of 4).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgraBytes(byte[] bgra, int offset)
        {
            bgra[offset]     = B;
            bgra[offset + 1] = G;
            bgra[offset + 2] = R;
            bgra[offset + 3] = A;
        }

        /// <summary>
        /// Converts from the existing <see cref="Bgra"/> struct.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rgba32 FromBgra(Bgra c) => new(c.R, c.G, c.B, c.A);

        /// <summary>
        /// Converts to the existing <see cref="Bgra"/> struct.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgra ToBgra() => new(B, G, R, A);

        /// <summary>
        /// Reads one pixel from an RGBA byte buffer at the specified offset.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rgba32 FromRgbaBytes(byte[] rgba, int offset)
            => new(rgba[offset], rgba[offset + 1], rgba[offset + 2], rgba[offset + 3]);

        /// <summary>
        /// Writes this pixel into an RGBA byte buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgbaBytes(byte[] rgba, int offset)
        {
            rgba[offset]     = R;
            rgba[offset + 1] = G;
            rgba[offset + 2] = B;
            rgba[offset + 3] = A;
        }
    }
}
