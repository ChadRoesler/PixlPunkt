using System.Collections.Generic;
using PixlPunkt.Core.Voxel;

namespace PixlPunkt.Core.Document
{
    /// <summary>
    /// Persisted voxel preview UI + camera state stored inside the .pxp document.
    /// </summary>
    public sealed class VoxelPreviewDocumentState
    {
        public bool HasState { get; set; }

        // Build/source controls
        public int FaceModeIndex { get; set; } = 0; // 0=3-face, 1=6-face
        public bool ColorLinkingEnabled { get; set; }
        public int ColorTolerance { get; set; } = 32;

        // Tile picker selections (-1 = none)
        public int FrontTileId3 { get; set; } = -1;
        public int SideTileId3 { get; set; } = -1;
        public int TopTileId3 { get; set; } = -1;

        public int FrontTileId6 { get; set; } = -1;
        public int BackTileId6 { get; set; } = -1;
        public int LeftTileId6 { get; set; } = -1;
        public int RightTileId6 { get; set; } = -1;
        public int TopTileId6 { get; set; } = -1;
        public int BottomTileId6 { get; set; } = -1;

        // Render controls
        public bool OutlineEnabled { get; set; }
        public uint OutlineColor { get; set; } = 0xFF000000;
        public int OutlineSize { get; set; } = 1;
        public bool PixelPreviewEnabled { get; set; }
        public int PixelBaseSize { get; set; } = 16;
        public bool BackdropGridEnabled { get; set; } = true;

        // Camera state
        public float CameraPitch { get; set; } = 0.5235988f; // 30 deg
        public float CameraYaw { get; set; } = 3.9269907f;   // 225 deg
        public float CameraZoomPercent { get; set; } = 100f;

        // Manual per-face color overrides (sparse)
        public List<VoxelFaceColorOverride> FaceColorOverrides { get; } = [];

        public bool TryGetFaceColorOverride(int x, int y, int z, Face face, out uint colorBgra)
        {
            for (int i = 0; i < FaceColorOverrides.Count; i++)
            {
                var o = FaceColorOverrides[i];
                if (o.X == x && o.Y == y && o.Z == z && o.Face == face)
                {
                    colorBgra = o.ColorBgra;
                    return true;
                }
            }

            colorBgra = 0;
            return false;
        }

        public void SetFaceColorOverride(int x, int y, int z, Face face, uint colorBgra)
        {
            for (int i = 0; i < FaceColorOverrides.Count; i++)
            {
                var o = FaceColorOverrides[i];
                if (o.X == x && o.Y == y && o.Z == z && o.Face == face)
                {
                    o.ColorBgra = colorBgra;
                    return;
                }
            }

            FaceColorOverrides.Add(new VoxelFaceColorOverride(x, y, z, face, colorBgra));
        }

        public bool RemoveFaceColorOverride(int x, int y, int z, Face face)
        {
            for (int i = 0; i < FaceColorOverrides.Count; i++)
            {
                var o = FaceColorOverrides[i];
                if (o.X == x && o.Y == y && o.Z == z && o.Face == face)
                {
                    FaceColorOverrides.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public void ClearFaceColorOverrides()
        {
            FaceColorOverrides.Clear();
        }
    }
}
