namespace PixlPunkt.Constants
{
    /// <summary>
    /// Constants for cursor export dialog and operations.
    /// </summary>
    public static class CursorExportConstants
    {
        // ====================================================================
        // DEFAULT CURSOR SIZES
        // ====================================================================

        /// <summary>Default cursor size in pixels.</summary>
        public const int DefaultCursorSize = 32;

        /// <summary>Minimum allowed cursor size in pixels.</summary>
        public const int MinCursorSize = 16;

        /// <summary>Maximum allowed cursor size in pixels.</summary>
        public const int MaxCursorSize = 256;

        /// <summary>Standard multi-resolution export sizes (largest to smallest).</summary>
        public static readonly int[] MultiResSizes = [128, 64, 48, 32];

        // ====================================================================
        // PREVIEW SIZING
        // ====================================================================

        /// <summary>Size of the actual cursor preview in pixels.</summary>
        public const int PreviewActualSize = 32;

        /// <summary>Enlargement factor for the zoomed preview.</summary>
        public const int PreviewEnlargementFactor = 10;

        /// <summary>Size of the enlarged preview in pixels.</summary>
        public const int PreviewEnlargedSize = PreviewActualSize * PreviewEnlargementFactor;

        // ====================================================================
        // GRID OVERLAY
        // ====================================================================

        /// <summary>Size of each grid cell in pixels.</summary>
        public const int GridCellSize = PreviewEnlargementFactor;

        /// <summary>Opacity of grid lines (0.0 to 1.0).</summary>
        public const double GridLineOpacity = 0.27;

        // ====================================================================
        // HOTSPOT INDICATOR STYLING
        // ====================================================================

        /// <summary>Border thickness for the hotspot indicator.</summary>
        public const int HotspotBorderThickness = 2;

        /// <summary>Corner radius for the hotspot indicator.</summary>
        public const int HotspotCornerRadius = 2;

        /// <summary>Border thickness for hover state.</summary>
        public const int HoverBorderThickness = 1;

        /// <summary>Corner radius for hover state.</summary>
        public const int HoverCornerRadius = 1;

        // ====================================================================
        // WINDOW SIZING
        // ====================================================================

        /// <summary>Default minimum window width.</summary>
        public const double DefaultMinWidth = 560;

        /// <summary>Default minimum window height.</summary>
        public const double DefaultMinHeight = 480;

        /// <summary>Maximum fraction of screen size for window.</summary>
        public const double DefaultMaxScreenFraction = 0.90;

        // ====================================================================
        // FILE SETTINGS
        // ====================================================================

        /// <summary>Default file extension for cursor files.</summary>
        public const string FileExtension = ".cur";

        /// <summary>Description for cursor file type in dialogs.</summary>
        public const string FileTypeDescription = "Cursor File";

        // ====================================================================
        // HOTSPOT DEFAULTS
        // ====================================================================

        /// <summary>Default X coordinate for cursor hotspot (top-left).</summary>
        public const int DefaultHotspotX = 0;

        /// <summary>Default Y coordinate for cursor hotspot (top-left).</summary>
        public const int DefaultHotspotY = 0;
    }
}
