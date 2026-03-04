using System;
using PixlPunkt.Core.Voxel;

namespace PixlPunkt.Core.Document
{
    /// <summary>
    /// Canonical voxel model payload persisted with the document.
    /// </summary>
    /// <remarks>
    /// Stores voxel occupancy and per-face colors in a dimension-aware layout.
    /// Internally, the current renderer/editor can still convert to/from <see cref="VoxelVolume"/>
    /// when the model is cubic and within the legacy size cap.
    /// </remarks>
    public sealed class VoxelModelDocumentState
    {
        public bool HasModel { get; set; }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Depth { get; private set; }

        /// <summary>
        /// Sparse/derived source provenance for rebuild flows.
        /// </summary>
        public VoxelModelSourceKind SourceKind { get; set; } = VoxelModelSourceKind.None;

        /// <summary>
        /// Indicates the model was manually edited after generation and may no longer match source projections.
        /// </summary>
        public bool DirtyFromSource { get; set; }

        /// <summary>
        /// Optional UTC timestamp of the last generation/import step.
        /// </summary>
        public long LastGeneratedUtcTicks { get; set; }

        /// <summary>
        /// Occupancy payload in row-major x + Width * (y + Height * z) order.
        /// </summary>
        public byte[] Occupancy { get; private set; } = Array.Empty<byte>();

        /// <summary>
        /// Packed BGRA per-face colors. Indexed as voxelIndex * 6 + face.
        /// </summary>
        public uint[] FaceColorsBgra { get; private set; } = Array.Empty<uint>();

        public int VoxelCount => Width > 0 && Height > 0 && Depth > 0 ? Width * Height * Depth : 0;

        public bool IsStorageValid
        {
            get
            {
                if (!HasModel)
                    return Occupancy.Length == 0 && FaceColorsBgra.Length == 0;

                if (Width <= 0 || Height <= 0 || Depth <= 0)
                    return false;

                long voxelCount = (long)Width * Height * Depth;
                if (voxelCount > int.MaxValue)
                    return false;

                long faceColorCount = voxelCount * 6L;
                if (faceColorCount > int.MaxValue)
                    return false;

                return Occupancy.Length == (int)voxelCount &&
                       FaceColorsBgra.Length == (int)faceColorCount;
            }
        }

        public void Clear()
        {
            HasModel = false;
            Width = 0;
            Height = 0;
            Depth = 0;
            SourceKind = VoxelModelSourceKind.None;
            DirtyFromSource = false;
            LastGeneratedUtcTicks = 0;
            Occupancy = Array.Empty<byte>();
            FaceColorsBgra = Array.Empty<uint>();
        }

        public void Initialize(int width, int height, int depth)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
            if (depth < 1) throw new ArgumentOutOfRangeException(nameof(depth));

            long voxelCount = (long)width * height * depth;
            long faceColorCount = voxelCount * 6L;
            if (voxelCount > int.MaxValue || faceColorCount > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(width), "Voxel model dimensions are too large.");

            HasModel = true;
            Width = width;
            Height = height;
            Depth = depth;
            Occupancy = new byte[(int)voxelCount];
            FaceColorsBgra = new uint[(int)faceColorCount];
        }

        public void SetStorage(
            int width,
            int height,
            int depth,
            byte[] occupancy,
            uint[] faceColorsBgra,
            bool hasModel = true)
        {
            if (occupancy == null) throw new ArgumentNullException(nameof(occupancy));
            if (faceColorsBgra == null) throw new ArgumentNullException(nameof(faceColorsBgra));

            if (!hasModel)
            {
                Clear();
                return;
            }

            Width = width;
            Height = height;
            Depth = depth;
            HasModel = true;
            Occupancy = occupancy;
            FaceColorsBgra = faceColorsBgra;
        }

        public VoxelModelDocumentState Clone()
        {
            var copy = new VoxelModelDocumentState
            {
                HasModel = HasModel,
                SourceKind = SourceKind,
                DirtyFromSource = DirtyFromSource,
                LastGeneratedUtcTicks = LastGeneratedUtcTicks,
            };

            copy.Width = Width;
            copy.Height = Height;
            copy.Depth = Depth;
            copy.Occupancy = (byte[])Occupancy.Clone();
            copy.FaceColorsBgra = (uint[])FaceColorsBgra.Clone();
            return copy;
        }

        public void CopyFrom(VoxelModelDocumentState other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            HasModel = other.HasModel;
            Width = other.Width;
            Height = other.Height;
            Depth = other.Depth;
            SourceKind = other.SourceKind;
            DirtyFromSource = other.DirtyFromSource;
            LastGeneratedUtcTicks = other.LastGeneratedUtcTicks;
            Occupancy = (byte[])other.Occupancy.Clone();
            FaceColorsBgra = (uint[])other.FaceColorsBgra.Clone();
        }

        public bool IsInBounds(int x, int y, int z)
            => (uint)x < (uint)Width && (uint)y < (uint)Height && (uint)z < (uint)Depth;

        public int Index(int x, int y, int z)
            => x + Width * (y + Height * z);

        public int FaceIndex(int voxelIndex, Face face)
            => voxelIndex * 6 + (int)face;

        public bool IsOccupied(int x, int y, int z)
        {
            if (!HasModel || !IsInBounds(x, y, z) || !IsStorageValid) return false;
            return Occupancy[Index(x, y, z)] != 0;
        }

        public void SetOccupied(int x, int y, int z, bool occupied)
        {
            if (!HasModel || !IsInBounds(x, y, z) || !IsStorageValid) return;
            Occupancy[Index(x, y, z)] = occupied ? (byte)1 : (byte)0;
        }

        public uint GetFaceColorBgra(int x, int y, int z, Face face)
        {
            if (!HasModel || !IsInBounds(x, y, z) || !IsStorageValid) return 0;
            int vi = Index(x, y, z);
            return FaceColorsBgra[FaceIndex(vi, face)];
        }

        public void SetFaceColorBgra(int x, int y, int z, Face face, uint colorBgra)
        {
            if (!HasModel || !IsInBounds(x, y, z) || !IsStorageValid) return;
            int vi = Index(x, y, z);
            FaceColorsBgra[FaceIndex(vi, face)] = colorBgra;
        }

        public void SetFromVoxelVolume(VoxelVolume volume)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));

            int size = volume.Size;
            Initialize(size, size, size);
            SourceKind = SourceKind == VoxelModelSourceKind.None ? VoxelModelSourceKind.TileOrthoGenerated : SourceKind;
            LastGeneratedUtcTicks = DateTime.UtcNow.Ticks;

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int vi = Index(x, y, z);
                        Occupancy[vi] = volume.IsOccupied(x, y, z) ? (byte)1 : (byte)0;

                        if (Occupancy[vi] == 0)
                            continue;

                        for (int f = 0; f < 6; f++)
                        {
                            var face = (Face)f;
                            FaceColorsBgra[FaceIndex(vi, face)] = PackBgra(volume.GetFaceColor(x, y, z, face));
                        }
                    }
                }
            }
        }

        public bool TryCreateVoxelVolume(out VoxelVolume? volume)
        {
            volume = null;

            if (!HasModel || !IsStorageValid)
                return false;

            if (Width != Height || Height != Depth)
                return false;

            if (Width < 1 || Width > VoxelVolume.MaxSize)
                return false;

            var result = new VoxelVolume(Width);

            for (int z = 0; z < Depth; z++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int vi = Index(x, y, z);
                        if (Occupancy[vi] == 0)
                            continue;

                        result.Occupancy[result.Index(x, y, z)] = 1;
                        for (int f = 0; f < 6; f++)
                        {
                            var face = (Face)f;
                            result.SetFaceColor(x, y, z, face, UnpackBgra(FaceColorsBgra[FaceIndex(vi, face)]));
                        }
                    }
                }
            }

            volume = result;
            return true;
        }

        private static uint PackBgra(Rgba32 c)
            => (uint)(c.B | (c.G << 8) | (c.R << 16) | (c.A << 24));

        private static Rgba32 UnpackBgra(uint bgra)
            => new(
                (byte)((bgra >> 16) & 0xFF),
                (byte)((bgra >> 8) & 0xFF),
                (byte)(bgra & 0xFF),
                (byte)((bgra >> 24) & 0xFF));
    }

    public enum VoxelModelSourceKind
    {
        None = 0,
        TileOrthoGenerated = 1,
        Manual = 2,
        Hybrid = 3,
    }
}
