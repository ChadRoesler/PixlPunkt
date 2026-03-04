namespace PixlPunkt.Core.Document
{
    /// <summary>
    /// Persisted voxel workspace/editor UI and viewport state.
    /// </summary>
    /// <remarks>
    /// This is intentionally separate from <see cref="VoxelModelDocumentState"/> so camera/view/tool
    /// preferences can evolve independently from canonical voxel model data.
    /// </remarks>
    public sealed class VoxelWorkspaceDocumentState
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
        public bool PixelPreviewAntialiasEnabled { get; set; }
        public float PixelPreviewAntialiasStrength { get; set; } = 0.35f;
        public int PixelBaseSize { get; set; } = 16;
        public bool BackdropGridEnabled { get; set; } = true;
        public bool BackdropProjectionTilesEnabled { get; set; } = true;
        public float BackdropCageScale { get; set; } = 1.6f;
        public bool SurfaceVoxelGridEnabled { get; set; }

        // Sidebar section expand/collapse state
        public bool ToolOptionsSectionExpanded { get; set; } = true;
        public bool FaceMappingSectionExpanded { get; set; } = true;
        public bool DisplaySectionExpanded { get; set; } = true;
        public bool VoxelEditSectionExpanded { get; set; } = true;
        public bool ActionsSectionExpanded { get; set; } = true;

        // In-tab workspace layout
        public bool VoxelPaneVisible { get; set; }
        public double VoxelPaneWidth { get; set; } = 560d;

        // Camera state
        public float CameraPitch { get; set; } = 0.5235988f; // 30 deg
        public float CameraYaw { get; set; } = 3.9269907f;   // 225 deg
        public float CameraZoomPercent { get; set; } = 100f;

        // Lighting preview utility state (Phase 5 uses these; persisted early for compatibility)
        public bool LightingEnabled { get; set; }
        public float LightPosX { get; set; } = 32f;
        public float LightPosY { get; set; } = 48f;
        public float LightPosZ { get; set; } = 32f;
        public uint LightColorBgra { get; set; } = 0xFFFFFFFF;
        public uint ShadowColorBgra { get; set; } = 0xC0000000;
        public float LightShadowStrength { get; set; } = 1f;
        public float LightIntensity { get; set; } = 1f;
        public float LightFalloff { get; set; } = 0.05f;
        public bool LightCastShadows { get; set; }

        public void CopyFromPreviewState(VoxelPreviewDocumentState preview)
        {
            if (preview == null)
                return;

            HasState = preview.HasState;

            FaceModeIndex = preview.FaceModeIndex;
            ColorLinkingEnabled = preview.ColorLinkingEnabled;
            ColorTolerance = preview.ColorTolerance;

            FrontTileId3 = preview.FrontTileId3;
            SideTileId3 = preview.SideTileId3;
            TopTileId3 = preview.TopTileId3;

            FrontTileId6 = preview.FrontTileId6;
            BackTileId6 = preview.BackTileId6;
            LeftTileId6 = preview.LeftTileId6;
            RightTileId6 = preview.RightTileId6;
            TopTileId6 = preview.TopTileId6;
            BottomTileId6 = preview.BottomTileId6;

            OutlineEnabled = preview.OutlineEnabled;
            OutlineColor = preview.OutlineColor;
            OutlineSize = preview.OutlineSize;
            PixelPreviewEnabled = preview.PixelPreviewEnabled;
            PixelPreviewAntialiasEnabled = preview.PixelPreviewAntialiasEnabled;
            PixelPreviewAntialiasStrength = preview.PixelPreviewAntialiasStrength;
            PixelBaseSize = preview.PixelBaseSize;
            BackdropGridEnabled = preview.BackdropGridEnabled;

            CameraPitch = preview.CameraPitch;
            CameraYaw = preview.CameraYaw;
            CameraZoomPercent = preview.CameraZoomPercent;
        }

        public void ApplyToPreviewState(VoxelPreviewDocumentState preview)
        {
            if (preview == null)
                return;

            preview.HasState = HasState;

            preview.FaceModeIndex = FaceModeIndex;
            preview.ColorLinkingEnabled = ColorLinkingEnabled;
            preview.ColorTolerance = ColorTolerance;

            preview.FrontTileId3 = FrontTileId3;
            preview.SideTileId3 = SideTileId3;
            preview.TopTileId3 = TopTileId3;

            preview.FrontTileId6 = FrontTileId6;
            preview.BackTileId6 = BackTileId6;
            preview.LeftTileId6 = LeftTileId6;
            preview.RightTileId6 = RightTileId6;
            preview.TopTileId6 = TopTileId6;
            preview.BottomTileId6 = BottomTileId6;

            preview.OutlineEnabled = OutlineEnabled;
            preview.OutlineColor = OutlineColor;
            preview.OutlineSize = OutlineSize;
            preview.PixelPreviewEnabled = PixelPreviewEnabled;
            preview.PixelPreviewAntialiasEnabled = PixelPreviewAntialiasEnabled;
            preview.PixelPreviewAntialiasStrength = PixelPreviewAntialiasStrength;
            preview.PixelBaseSize = PixelBaseSize;
            preview.BackdropGridEnabled = BackdropGridEnabled;

            preview.CameraPitch = CameraPitch;
            preview.CameraYaw = CameraYaw;
            preview.CameraZoomPercent = CameraZoomPercent;
        }
    }
}
