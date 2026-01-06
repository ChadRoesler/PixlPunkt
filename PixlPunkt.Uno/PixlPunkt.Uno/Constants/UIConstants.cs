namespace PixlPunkt.Uno.Constants
{
    /// <summary>
    /// UI sizing and spacing constants for consistent visual appearance.
    /// </summary>
    public static class UIConstants
    {
        // ════════════════════════════════════════════════════════════════════
        // PALETTE SWATCHES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Width of color swatches in pixels.</summary>
        public const int SwatchWidth = 20;

        /// <summary>Height of color swatches in pixels.</summary>
        public const int SwatchHeight = 20;

        /// <summary>Margin around color swatches in pixels.</summary>
        public const int SwatchMargin = 2;

        /// <summary>Corner radius for color swatches.</summary>
        public const int SwatchCornerRadius = 4;

        /// <summary>Border thickness for color swatches.</summary>
        public const int SwatchBorderThickness = 1;

        /// <summary>Maximum number of swatches per row.</summary>
        public const int MaxSwatchesPerRow = 16;

        /// <summary>Vertical spacing between swatch rows.</summary>
        public const int SwatchGridRowSpacing = 4;

        /// <summary>Horizontal spacing between swatch columns.</summary>
        public const int SwatchGridColumnSpacing = 8;

        // ════════════════════════════════════════════════════════════════════
        // PALETTE PREVIEW
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Maximum height of palette preview area.</summary>
        public const int PalettePreviewMaxHeight = 240;

        /// <summary>Number of rows visible before scrolling.</summary>
        public const int PalettePreviewRowsBeforeScroll = 6;

        // ════════════════════════════════════════════════════════════════════
        // TAB HEADERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Size of the tear-off button in tab headers.</summary>
        public const int TearButtonSize = 24;

        /// <summary>Padding for the tear-off button.</summary>
        public const int TearButtonPadding = 0;

        /// <summary>Column spacing in tab headers.</summary>
        public const int TabHeaderColumnSpacing = 8;

        // ════════════════════════════════════════════════════════════════════
        // DIALOG SIZING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Minimum width for JSON import text boxes.</summary>
        public const int JsonImportTextBoxMinWidth = 360;

        /// <summary>Minimum height for JSON import text boxes.</summary>
        public const int JsonImportTextBoxMinHeight = 160;

        // ════════════════════════════════════════════════════════════════════
        // TOLERANCE SLIDER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Width of tolerance sliders.</summary>
        public const int ToleranceSliderWidth = 180;

        /// <summary>Font size multiplier for tolerance slider labels.</summary>
        public const double ToleranceSliderFontSizeMultiplier = 0.6;

        /// <summary>Minimum width multiplier for tolerance slider labels.</summary>
        public const double ToleranceSliderMinWidthMultiplier = 15.0;

        // ════════════════════════════════════════════════════════════════════
        // STACK PANELS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Standard spacing for stack panels.</summary>
        public const int StandardStackPanelSpacing = 8;

        /// <summary>Spacing for merge row layouts.</summary>
        public const int MergeRowSpacing = 8;
    }
}
