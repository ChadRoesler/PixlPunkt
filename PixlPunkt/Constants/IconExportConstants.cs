namespace PixlPunkt.Constants
{
    /// <summary>
    /// Constants for icon export dialog and operations.
    /// </summary>
    public static class IconExportConstants
    {
        // ====================================================================
        // ICON SIZES
        // ====================================================================

        /// <summary>Standard icon sizes (largest to smallest for directory order).</summary>
        public static readonly int[] StandardSizes = [256, 128, 64, 48, 32, 16];

        /// <summary>Sizes to show in preview (excludes very large sizes).</summary>
        public static readonly int[] PreviewSizes = [64, 48, 32, 16];

        // ====================================================================
        // PREVIEW SIZING
        // ====================================================================

        /// <summary>Maximum visual size for large icons in preview.</summary>
        public const int PreviewMaxSize = 48;

        /// <summary>Maximum height of the preview scroll area.</summary>
        public const double PreviewScrollMaxHeight = 200.0;

        /// <summary>Maximum display size for icons in preview (64x64 shown at full size).</summary>
        public const int PreviewMaxDisplaySize = 64;

        // ====================================================================
        // WINDOW SIZING
        // ====================================================================

        /// <summary>Default minimum window width.</summary>
        public const double DefaultMinWidth = 640;

        /// <summary>Default minimum window height.</summary>
        public const double DefaultMinHeight = 520;

        /// <summary>Maximum fraction of screen size for window.</summary>
        public const double DefaultMaxScreenFraction = 0.90;

        // ====================================================================
        // FILE SETTINGS
        // ====================================================================

        /// <summary>Default file extension for icon files.</summary>
        public const string FileExtension = ".ico";

        /// <summary>Description for icon file type in dialogs.</summary>
        public const string FileTypeDescription = "Icon File";

        // ====================================================================
        // PREVIEW LABEL FORMATTING
        // ====================================================================

        /// <summary>Font size for preview labels.</summary>
        public const int PreviewLabelFontSize = 11;

        /// <summary>Opacity for preview labels.</summary>
        public const double PreviewLabelOpacity = 0.8;

        /// <summary>Width of preview boxes.</summary>
        public const int PreviewBoxWidth = 80;

        // ====================================================================
        // DEFAULTS
        // ====================================================================

        /// <summary>Default scale mode for icon resizing.</summary>
        public const string DefaultScaleMode = "Bilinear";

        // ====================================================================
        // BACKGROUND COLORS
        // ====================================================================

        /// <summary>Grey background red component.</summary>
        public static readonly byte GreyR = 170;

        /// <summary>Grey background green component.</summary>
        public static readonly byte GreyG = 170;

        /// <summary>Grey background blue component.</summary>
        public static readonly byte GreyB = 170;
    }
}
