namespace PixlPunkt.Constants
{
    /// <summary>
    /// Constants for custom brush export dialog and operations.
    /// </summary>
    public static class BrushExportConstants
    {
        //////////////////////////////////////////////////////////////////
        // BRUSH SIZE
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Standard brush mask size (16x16 pixels).
        /// All custom brushes use a single 16x16 mask that is scaled at runtime.
        /// </summary>
        public const int MaskSize = 16;

        /// <summary>
        /// Canvas size for brush creation (matches mask size).
        /// </summary>
        public const int CanvasSize = MaskSize;

        //////////////////////////////////////////////////////////////////
        // FILE FORMAT
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// File extension for custom brush files.
        /// </summary>
        public const string FileExtension = ".mrk";

        /// <summary>
        /// File type description for save dialogs.
        /// </summary>
        public const string FileTypeDescription = "PixlPunkt Brush";

        /// <summary>
        /// Current file format version.
        /// Version 3: Single 16x16 mask (no multi-tier).
        /// </summary>
        public const ushort FileFormatVersion = 3;

        //////////////////////////////////////////////////////////////////
        // ICON PREVIEW
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Icon preview size in pixels (scaled from 16x16 mask).
        /// </summary>
        public const int IconSize = 32;

        /// <summary>
        /// Icon preview display size in dialog (enlarged for visibility).
        /// </summary>
        public const int IconDisplaySize = 96;

        /// <summary>
        /// Icon outline stroke thickness.
        /// </summary>
        public const float IconOutlineThickness = 1.5f;

        //////////////////////////////////////////////////////////////////
        // PIVOT GRID
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Pivot grid cell size in dialog.
        /// </summary>
        public const int PivotCellSize = 24;

        /// <summary>
        /// Pivot positions as (X, Y) normalized coordinates.
        /// </summary>
        public static readonly (float x, float y, string name)[] PivotPositions =
        [
            (0.0f, 0.0f, "Top-Left"),
            (0.5f, 0.0f, "Top-Center"),
            (1.0f, 0.0f, "Top-Right"),
            (0.0f, 0.5f, "Middle-Left"),
            (0.5f, 0.5f, "Center"),
            (1.0f, 0.5f, "Middle-Right"),
            (0.0f, 1.0f, "Bottom-Left"),
            (0.5f, 1.0f, "Bottom-Center"),
            (1.0f, 1.0f, "Bottom-Right")
        ];

        //////////////////////////////////////////////////////////////////
        // WINDOW SIZING
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Default minimum dialog width.
        /// </summary>
        public const double DefaultMinWidth = 400;

        /// <summary>
        /// Default minimum dialog height.
        /// </summary>
        public const double DefaultMinHeight = 480;

        /// <summary>
        /// Default maximum screen fraction for dialog.
        /// </summary>
        public const double DefaultMaxScreenFraction = 0.90;

        //////////////////////////////////////////////////////////////////
        // DEFAULTS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Default brush name.
        /// </summary>
        public const string DefaultBrushName = "CustomBrush";

        /// <summary>
        /// Default author name.
        /// </summary>
        public const string DefaultAuthor = "Custom";

        /// <summary>
        /// Default pivot X (center).
        /// </summary>
        public const float DefaultPivotX = 0.5f;

        /// <summary>
        /// Default pivot Y (center).
        /// </summary>
        public const float DefaultPivotY = 0.5f;

        //////////////////////////////////////////////////////////////////
        // CUSTOM ICON NAMING
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Prefix for custom brush icon names.
        /// </summary>
        public const string IconNamePrefix = "Brush_";

        /// <summary>
        /// Gets the icon name for a custom brush with author.
        /// </summary>
        public static string GetIconName(string author, string brushName)
        {
            var safeAuthor = SanitizeForIdentifier(author);
            var safeName = SanitizeForIdentifier(brushName);
            return $"{IconNamePrefix}{safeAuthor}_{safeName}";
        }

        /// <summary>
        /// Gets the icon name for a custom brush (uses "Custom" as author).
        /// </summary>
        public static string GetIconName(string brushName)
        {
            return GetIconName(DefaultAuthor, brushName);
        }

        /// <summary>
        /// Gets the suggested filename for a brush file.
        /// </summary>
        public static string GetFileName(string author, string brushName)
        {
            var safeAuthor = SanitizeForFileName(author);
            var safeName = SanitizeForFileName(brushName);
            return $"{safeAuthor}.{safeName}{FileExtension}";
        }

        private static string SanitizeForIdentifier(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "Unknown";

            return input
                .Replace(" ", "")
                .Replace("-", "_")
                .Replace(".", "_");
        }

        private static string SanitizeForFileName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "Unknown";

            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');

            return input.Replace(" ", "");
        }
    }
}
