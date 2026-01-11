using System.Runtime.CompilerServices;

namespace PixlPunkt.Core.Structs
{
    /// <summary>
    /// Lightweight BGRA pixel struct for clearer pack/unpack operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This struct provides efficient byte-level access to BGRA color components with
    /// optimized methods for conversion to/from uint32 and byte arrays.
    /// </para>
    /// <para>
    /// Memory layout matches Windows DIB format: Blue, Green, Red, Alpha (little-endian).
    /// As uint32: 0xAARRGGBB.
    /// </para>
    /// </remarks>
    public readonly struct Bgra
    {
        /// <summary>Blue component (0-255).</summary>
        public readonly byte B;

        /// <summary>Green component (0-255).</summary>
        public readonly byte G;

        /// <summary>Red component (0-255).</summary>
        public readonly byte R;

        /// <summary>Alpha component (0-255), where 255 is fully opaque.</summary>
        public readonly byte A;

        /// <summary>
        /// Creates a new BGRA color from individual components.
        /// </summary>
        /// <param name="b">Blue component (0-255).</param>
        /// <param name="g">Green component (0-255).</param>
        /// <param name="r">Red component (0-255).</param>
        /// <param name="a">Alpha component (0-255). Default is 255 (opaque).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgra(byte b, byte g, byte r, byte a = 255)
        {
            B = b; G = g; R = r; A = a;
        }

        /// <summary>
        /// Creates a BGRA color from a packed uint32 value.
        /// </summary>
        /// <param name="bgra">Packed color in 0xAARRGGBB format.</param>
        /// <returns>A new Bgra struct with unpacked components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bgra FromUInt(uint bgra)
            => new((byte)(bgra & 0xFF), (byte)((bgra >> 8) & 0xFF), (byte)((bgra >> 16) & 0xFF), (byte)((bgra >> 24) & 0xFF));

        /// <summary>
        /// Packs this color into a uint32 value.
        /// </summary>
        /// <returns>Packed color in 0xAARRGGBB format.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ToUInt()
            => (uint)(A << 24 | R << 16 | G << 8 | B);

        /// <summary>
        /// Creates a BGRA color from a byte array at the specified index.
        /// </summary>
        /// <param name="src">Source byte array containing BGRA data.</param>
        /// <param name="idx4">Starting byte index (must be multiple of 4).</param>
        /// <returns>A new Bgra struct with components from the array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bgra FromBytes(byte[] src, int idx4)
            => new(src[idx4 + 0], src[idx4 + 1], src[idx4 + 2], src[idx4 + 3]);

        /// <summary>
        /// Writes this color's components to a byte array at the specified index.
        /// </summary>
        /// <param name="dst">Destination byte array.</param>
        /// <param name="idx4">Starting byte index (must be multiple of 4).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(byte[] dst, int idx4)
        {
            dst[idx4 + 0] = B;
            dst[idx4 + 1] = G;
            dst[idx4 + 2] = R;
            dst[idx4 + 3] = A;
        }

        /// <summary>
        /// Reads a packed uint32 color directly from a byte array.
        /// </summary>
        /// <param name="src">Source byte array containing BGRA data.</param>
        /// <param name="idx4">Starting byte index (must be multiple of 4).</param>
        /// <returns>Packed color in 0xAARRGGBB format.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUIntFromBytes(byte[] src, int idx4)
            => (uint)(src[idx4 + 3] << 24 | src[idx4 + 2] << 16 | src[idx4 + 1] << 8 | src[idx4 + 0]);

        /// <summary>
        /// Writes a packed uint32 color directly to a byte array.
        /// </summary>
        /// <param name="dst">Destination byte array.</param>
        /// <param name="idx4">Starting byte index (must be multiple of 4).</param>
        /// <param name="bgra">Packed color in 0xAARRGGBB format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUIntToBytes(byte[] dst, int idx4, uint bgra)
        {
            dst[idx4 + 0] = (byte)(bgra & 0xFF);
            dst[idx4 + 1] = (byte)((bgra >> 8) & 0xFF);
            dst[idx4 + 2] = (byte)((bgra >> 16) & 0xFF);
            dst[idx4 + 3] = (byte)((bgra >> 24) & 0xFF);
        }
    }
}
