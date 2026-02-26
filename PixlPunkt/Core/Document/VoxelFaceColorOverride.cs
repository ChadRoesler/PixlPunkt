using PixlPunkt.Core.Voxel;

namespace PixlPunkt.Core.Document
{
    /// <summary>
    /// Sparse manual color override for one face of one voxel in the preview volume.
    /// </summary>
    public sealed class VoxelFaceColorOverride
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public Face Face { get; set; }

        /// <summary>
        /// Override color stored as packed BGRA (same packing used by renderer/UI swatches).
        /// </summary>
        public uint ColorBgra { get; set; }

        public VoxelFaceColorOverride()
        {
        }

        public VoxelFaceColorOverride(int x, int y, int z, Face face, uint colorBgra)
        {
            X = x;
            Y = y;
            Z = z;
            Face = face;
            ColorBgra = colorBgra;
        }
    }
}
