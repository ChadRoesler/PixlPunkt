using System;

namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// 3D voxel volume with per-voxel occupancy and per-face color data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stores a cubic grid of <see cref="Size"/>³ voxels. Each voxel has:
    /// </para>
    /// <list type="bullet">
    /// <item>An occupancy flag (0 = empty, 1 = solid) in <see cref="Occupancy"/>.</item>
    /// <item>Six <see cref="Rgba32"/> face colors in <see cref="FaceColors"/>,
    /// one for each <see cref="Face"/> direction.</item>
    /// </list>
    /// <para>
    /// Indexing follows row-major 3D order: <c>x + Size * (y + Size * z)</c>.
    /// Face colors are stored at <c>voxelIndex * 6 + (int)face</c>.
    /// </para>
    /// <para>
    /// <strong>Memory usage:</strong> For a 64×64×64 volume, occupancy is ~256 KB
    /// and face colors are ~6 MB (6 × 4 bytes × 64³). A hard cap of
    /// <see cref="MaxSize"/> prevents runaway allocation.
    /// </para>
    /// </remarks>
    public sealed class VoxelVolume
    {
        /// <summary>
        /// Maximum allowed volume size (per axis) to prevent excessive memory usage.
        /// A 128³ volume uses ~48 MB for face colors.
        /// </summary>
        public const int MaxSize = 128;

        /// <summary>
        /// Gets the size of the volume along each axis.
        /// The volume is always cubic: <c>Size × Size × Size</c>.
        /// </summary>
        public readonly int Size;

        /// <summary>
        /// Occupancy grid. Length is <c>Size³</c>.
        /// A value of 0 means empty; 1 means the voxel is solid.
        /// </summary>
        public readonly byte[] Occupancy;

        /// <summary>
        /// Per-face color data. Length is <c>Size³ × 6</c>.
        /// Indexed by <c>voxelIndex * 6 + (int)face</c>.
        /// </summary>
        public readonly Rgba32[] FaceColors;

        /// <summary>
        /// Creates a new empty voxel volume of the specified size.
        /// </summary>
        /// <param name="size">
        /// Size along each axis. Must be between 1 and <see cref="MaxSize"/> (inclusive).
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <paramref name="size"/> is less than 1 or greater than <see cref="MaxSize"/>.
        /// </exception>
        public VoxelVolume(int size)
        {
            if (size < 1 || size > MaxSize)
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    $"Volume size must be between 1 and {MaxSize}, got {size}.");

            Size = size;
            Occupancy = new byte[size * size * size];
            FaceColors = new Rgba32[size * size * size * 6];
        }

        /// <summary>
        /// Computes the flat array index for a voxel at (x, y, z).
        /// </summary>
        /// <param name="x">X coordinate (0 to Size−1).</param>
        /// <param name="y">Y coordinate (0 to Size−1).</param>
        /// <param name="z">Z coordinate (0 to Size−1).</param>
        /// <returns>Flat index into <see cref="Occupancy"/>.</returns>
        public int Index(int x, int y, int z) => x + Size * (y + Size * z);

        /// <summary>
        /// Computes the index into <see cref="FaceColors"/> for a specific face
        /// of the voxel at the given flat index.
        /// </summary>
        /// <param name="voxelIndex">Flat voxel index from <see cref="Index"/>.</param>
        /// <param name="face">Which face to address.</param>
        /// <returns>Index into <see cref="FaceColors"/>.</returns>
        public int FaceIndex(int voxelIndex, Face face) => voxelIndex * 6 + (int)face;

        /// <summary>
        /// Returns whether the voxel at (x, y, z) is occupied.
        /// </summary>
        public bool IsOccupied(int x, int y, int z) => Occupancy[Index(x, y, z)] != 0;

        /// <summary>
        /// Sets a voxel as occupied and assigns colors for all six faces.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="z">Z coordinate.</param>
        /// <param name="front">Color for the front face (+Z).</param>
        /// <param name="back">Color for the back face (−Z).</param>
        /// <param name="left">Color for the left face (−X).</param>
        /// <param name="right">Color for the right face (+X).</param>
        /// <param name="top">Color for the top face (+Y).</param>
        /// <param name="bottom">Color for the bottom face (−Y).</param>
        public void SetVoxel(int x, int y, int z,
            Rgba32 front, Rgba32 back,
            Rgba32 left, Rgba32 right,
            Rgba32 top, Rgba32 bottom)
        {
            int idx = Index(x, y, z);
            Occupancy[idx] = 1;
            FaceColors[FaceIndex(idx, Face.Front)]  = front;
            FaceColors[FaceIndex(idx, Face.Back)]   = back;
            FaceColors[FaceIndex(idx, Face.Left)]   = left;
            FaceColors[FaceIndex(idx, Face.Right)]  = right;
            FaceColors[FaceIndex(idx, Face.Top)]    = top;
            FaceColors[FaceIndex(idx, Face.Bottom)] = bottom;
        }

        /// <summary>
        /// Sets a voxel as occupied with the same color on all six faces.
        /// </summary>
        public void SetVoxel(int x, int y, int z, Rgba32 color)
            => SetVoxel(x, y, z, color, color, color, color, color, color);

        /// <summary>
        /// Clears a voxel, marking it as unoccupied.
        /// Face colors are left as-is (they are ignored for unoccupied voxels).
        /// </summary>
        public void ClearVoxel(int x, int y, int z)
        {
            Occupancy[Index(x, y, z)] = 0;
        }

        /// <summary>
        /// Gets the face color for a specific face of the voxel at (x, y, z).
        /// </summary>
        public Rgba32 GetFaceColor(int x, int y, int z, Face face)
        {
            int idx = Index(x, y, z);
            return FaceColors[FaceIndex(idx, face)];
        }

        /// <summary>
        /// Sets the face color for a specific face of the voxel at (x, y, z).
        /// The voxel occupancy is not changed.
        /// </summary>
        public void SetFaceColor(int x, int y, int z, Face face, Rgba32 color)
        {
            int idx = Index(x, y, z);
            FaceColors[FaceIndex(idx, face)] = color;
        }

        /// <summary>
        /// Gets the total number of occupied voxels in the volume.
        /// </summary>
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Occupancy.Length; i++)
                {
                    if (Occupancy[i] != 0) count++;
                }
                return count;
            }
        }
    }
}
