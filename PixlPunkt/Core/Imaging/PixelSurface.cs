using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PixlPunkt.Core.Imaging
{
    /// <summary>
    /// Represents a 2D array of BGRA (Blue, Green, Red, Alpha) pixels stored as a contiguous byte array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PixelSurface provides the foundational pixel buffer for all image data in PixlPunkt.
    /// Each pixel is represented by 4 consecutive bytes in BGRA order (0xAABBGGRR when interpreted as uint32).
    /// </para>
    /// <para>
    /// Memory layout: [B₀, G₀, R₀, A₀, B₁, G₁, R₁, A₁, ...]
    /// Index for pixel (x,y): (y * Width + x) * 4
    /// </para>
    /// <para>
    /// This class prioritizes performance with bounds checking and provides an event for
    /// notifying subscribers of pixel changes (useful for triggering UI updates).
    /// </para>
    /// </remarks>
    public sealed class PixelSurface
    {
        /// <summary>
        /// Gets the width of the surface in pixels.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// Gets the height of the surface in pixels.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// Gets the raw pixel data in BGRA format.
        /// </summary>
        /// <value>
        /// A byte array of length (Width * Height * 4) containing pixel data in
        /// row-major order with BGRA byte ordering.
        /// </value>
        public byte[] Pixels { get; private set; }

        /// <summary>
        /// Occurs after any pixel mutation operation (<see cref="WriteBGRA"/> or <see cref="Clear"/>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This event is raised after every pixel write operation, which can be high-frequency.
        /// Subscribers should consider throttling or batching updates if performance is a concern.
        /// </para>
        /// <para>
        /// For batch operations, consider temporarily unsubscribing and resubscribing after completion.
        /// </para>
        /// </remarks>
        public event Action? PixelsChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelSurface"/> class with the specified dimensions.
        /// </summary>
        /// <param name="w">The width in pixels. Must be greater than 0.</param>
        /// <param name="h">The height in pixels. Must be greater than 0.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="w"/> or <paramref name="h"/> is less than or equal to 0.
        /// </exception>
        /// <remarks>
        /// The pixel array is allocated but not initialized, so it contains undefined data
        /// until explicitly set via <see cref="Clear"/> or <see cref="WriteBGRA"/>.
        /// </remarks>
        public PixelSurface(int w, int h)
        {
            if (w <= 0 || h <= 0)
                throw new ArgumentOutOfRangeException();

            Width = w;
            Height = h;
            Pixels = new byte[w * h * 4];
        }

        /// <summary>
        /// Calculates the byte index in the <see cref="Pixels"/> array for a given coordinate.
        /// </summary>
        /// <param name="x">The X coordinate (0-based).</param>
        /// <param name="y">The Y coordinate (0-based).</param>
        /// <returns>The starting byte index for the pixel at (x, y).</returns>
        /// <remarks>
        /// This method does not perform bounds checking. Use only when coordinates are known to be valid.
        /// Formula: (y * Width + x) * 4
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf(int x, int y) => (y * Width + x) * 4;

        /// <summary>
        /// Fills the entire surface with a single BGRA color.
        /// </summary>
        /// <param name="bgra">The color in BGRA format (0xAABBGGRR).</param>
        /// <remarks>
        /// This method efficiently fills all pixels with the specified color using
        /// optimized bulk memory operations and raises the <see cref="PixelsChanged"/> 
        /// event once after completion.
        /// </remarks>
        public void Clear(uint bgra)
        {
            // Use Span<uint> for efficient bulk fill - much faster than per-byte loop
            var pixelSpan = MemoryMarshal.Cast<byte, uint>(Pixels.AsSpan());
            pixelSpan.Fill(bgra);

            PixelsChanged?.Invoke();
        }

        /// <summary>
        /// Reads the BGRA color at the specified coordinates.
        /// </summary>
        /// <param name="x">The X coordinate (0-based).</param>
        /// <param name="y">The Y coordinate (0-based).</param>
        /// <returns>
        /// The BGRA color as a 32-bit unsigned integer (0xAABBGGRR), or 0 (transparent black)
        /// if the coordinates are out of bounds.
        /// </returns>
        /// <remarks>
        /// This method performs bounds checking. Out-of-bounds reads return 0 without throwing an exception.
        /// </remarks>
        public uint ReadBGRA(int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                return 0;

            int i = IndexOf(x, y);
            return (uint)(Pixels[i + 3] << 24 | Pixels[i + 2] << 16 | Pixels[i + 1] << 8 | Pixels[i + 0]);
        }

        /// <summary>
        /// Writes a BGRA color to the specified coordinates.
        /// </summary>
        /// <param name="x">The X coordinate (0-based).</param>
        /// <param name="y">The Y coordinate (0-based).</param>
        /// <param name="bgra">The color in BGRA format (0xAABBGGRR).</param>
        /// <remarks>
        /// <para>
        /// This method performs bounds checking. Out-of-bounds writes are silently ignored (no-op).
        /// </para>
        /// <para>
        /// The <see cref="PixelsChanged"/> event is raised after each write. For bulk operations,
        /// consider working directly with the <see cref="Pixels"/> array and manually raising the event.
        /// </para>
        /// </remarks>
        public void WriteBGRA(int x, int y, uint bgra)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                return;

            int i = IndexOf(x, y);
            Pixels[i + 0] = (byte)(bgra & 0xFF);
            Pixels[i + 1] = (byte)(bgra >> 8 & 0xFF);
            Pixels[i + 2] = (byte)(bgra >> 16 & 0xFF);
            Pixels[i + 3] = (byte)(bgra >> 24 & 0xFF);

            PixelsChanged?.Invoke();
        }

        /// <summary>
        /// Resizes the surface to new dimensions, optionally replacing pixel data.
        /// </summary>
        /// <param name="newWidth">The new width in pixels.</param>
        /// <param name="newHeight">The new height in pixels.</param>
        /// <param name="newPixels">The new pixel data array, or null to allocate a cleared buffer.</param>
        /// <remarks>
        /// This method replaces the internal pixel buffer entirely. If newPixels is provided,
        /// it must have the correct size for the new dimensions. If null, a new zeroed buffer
        /// is allocated. Use this for canvas resize operations.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if dimensions are invalid.</exception>
        /// <exception cref="ArgumentException">Thrown if pixel array size doesn't match dimensions (when provided).</exception>
        public void Resize(int newWidth, int newHeight, byte[]? newPixels)
        {
            if (newWidth <= 0 || newHeight <= 0)
                throw new ArgumentOutOfRangeException("Dimensions must be positive.");

            int expectedLength = newWidth * newHeight * 4;

            if (newPixels == null)
            {
                // Allocate new cleared buffer
                newPixels = new byte[expectedLength];
            }
            else if (newPixels.Length != expectedLength)
            {
                throw new ArgumentException($"Pixel array must be exactly {expectedLength} bytes for {newWidth}x{newHeight} surface.");
            }

            Width = newWidth;
            Height = newHeight;
            Pixels = newPixels;

            PixelsChanged?.Invoke();
        }

        /// <summary>
        /// Checks if the given coordinates are within surface bounds.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>True if (x, y) is within [0, Width) x [0, Height).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInBounds(int x, int y)
            => (uint)x < (uint)Width && (uint)y < (uint)Height;

        /// <summary>
        /// Raises the PixelsChanged event. Call after batch pixel operations.
        /// </summary>
        /// <remarks>
        /// Use this method to manually notify subscribers after performing bulk pixel
        /// modifications directly on the <see cref="Pixels"/> array.
        /// </remarks>
        public void NotifyChanged() => PixelsChanged?.Invoke();
    }
}