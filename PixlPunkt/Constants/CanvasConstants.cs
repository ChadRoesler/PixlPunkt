namespace PixlPunkt.Constants
{
    /// <summary>
    /// Constants for canvas dimensions, tiles, and export settings.
    /// </summary>
    public static class CanvasConstants
    {
        // ════════════════════════════════════════════════════════════════════
        // DEFAULT CANVAS DIMENSIONS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Default canvas width in pixels.</summary>
        public const int DefaultWidth = 64;

        /// <summary>Default canvas height in pixels.</summary>
        public const int DefaultHeight = 64;

        /// <summary>Default tile width in pixels.</summary>
        public const int DefaultTileWidth = 8;

        /// <summary>Default tile height in pixels.</summary>
        public const int DefaultTileHeight = 8;

        /// <summary>Default number of tiles horizontally.</summary>
        public const int DefaultTileCountX = 8;

        /// <summary>Default number of tiles vertically.</summary>
        public const int DefaultTileCountY = 8;

        // ════════════════════════════════════════════════════════════════════
        // IMAGE EXPORT SETTINGS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Default DPI for exported images.</summary>
        public const double ExportDpi = 96.0;

        /// <summary>Horizontal DPI for exported images.</summary>
        public const double ExportDpiX = 96.0;

        /// <summary>Vertical DPI for exported images.</summary>
        public const double ExportDpiY = 96.0;

        // ════════════════════════════════════════════════════════════════════
        // CANVAS SIZE LIMITS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Minimum allowed canvas dimension in pixels.</summary>
        public const int MinCanvasDimension = 1;

        /// <summary>Maximum allowed canvas dimension in pixels.</summary>
        public const int MaxCanvasDimension = 4096;

        // ════════════════════════════════════════════════════════════════════
        // TILE SIZE LIMITS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Minimum allowed tile size in pixels.</summary>
        public const int MinTileSize = 1;

        /// <summary>Maximum allowed tile size in pixels.</summary>
        public const int MaxTileSize = 64;
    }
}
