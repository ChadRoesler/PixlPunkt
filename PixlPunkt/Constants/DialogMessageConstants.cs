namespace PixlPunkt.Constants
{
    /// <summary>
    /// Localized string constants for dialog messages and UI text.
    /// </summary>
    public static class DialogMessages
    {
        // ════════════════════════════════════════════════════════════════════
        // DIALOG TITLES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Title for import failure dialogs.</summary>
        public const string ImportFailedTitle = "Import failed";

        /// <summary>Title for open failure dialogs.</summary>
        public const string OpenFailedTitle = "Open failed";

        /// <summary>Title for save failure dialogs.</summary>
        public const string SaveFailedTitle = "Save failed";

        /// <summary>Title for palette export success dialogs.</summary>
        public const string PaletteExportedTitle = "Palette exported";

        /// <summary>Title for color picker dialogs.</summary>
        public const string PickColorTitle = "Pick color";

        /// <summary>Title for background color dialogs.</summary>
        public const string BackgroundColorTitle = "Background Color";

        /// <summary>Prefix for import apply dialogs.</summary>
        public const string ApplyImportPrefix = "Apply Import: ";

        /// <summary>Prefix for preset apply dialogs.</summary>
        public const string ApplyPresetPrefix = "Apply preset: ";

        /// <summary>Title for palette import dialogs.</summary>
        public const string ImportPaletteTitle = "Import palette (paste JSON)";

        /// <summary>Title for add palette color dialogs.</summary>
        public const string AddPaletteColorTitle = "Add Palette Color";

        // ════════════════════════════════════════════════════════════════════
        // ERROR MESSAGES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Error when no active layer is available for import.</summary>
        public const string NoActiveLayerMessage = "No active layer or document to import from.";

        /// <summary>Error when no active document is available.</summary>
        public const string NoActiveDocumentMessage = "No active document to import from.";

        /// <summary>Error when image file cannot be read.</summary>
        public const string CouldNotReadImageFile = "Could not read image file.";

        /// <summary>Error when layer cannot be read.</summary>
        public const string CouldNotReadLayer = "Could not read layer.";

        /// <summary>Error when document cannot be read.</summary>
        public const string CouldNotReadDocument = "Could not read document.";

        /// <summary>Error when palette JSON is invalid.</summary>
        public const string InvalidPaletteJson = "Invalid palette JSON.";

        /// <summary>Error when document cannot be saved.</summary>
        public const string CouldNotSaveDocument = "Could not save current document.";

        /// <summary>Format string for file open errors.</summary>
        public const string OpenFileErrorFormat = "Could not open file.\n{0}";

        // ════════════════════════════════════════════════════════════════════
        // SUCCESS MESSAGES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Subtitle for successful palette export.</summary>
        public const string PaletteExportedSubtitle = "JSON copied to clipboard";

        // ════════════════════════════════════════════════════════════════════
        // BUTTON TEXT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Text for Add button.</summary>
        public const string ButtonAdd = "Add";

        /// <summary>Text for Replace button.</summary>
        public const string ButtonReplace = "Replace";

        /// <summary>Text for Cancel button.</summary>
        public const string ButtonCancel = "Cancel";

        /// <summary>Text for Import button.</summary>
        public const string ButtonImport = "Import";

        /// <summary>Text for OK button.</summary>
        public const string ButtonOK = "OK";

        /// <summary>Text for Primary button.</summary>
        public const string ButtonPrimary = "Primary";

        // ════════════════════════════════════════════════════════════════════
        // TOOLTIPS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Tooltip for open in new window button.</summary>
        public const string OpenInNewWindowTooltip = "Open in new window";

        // ════════════════════════════════════════════════════════════════════
        // LABELS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Label for merge near colors option.</summary>
        public const string MergeNearColorsLabel = "Merge near colors";

        /// <summary>Label for tolerance setting.</summary>
        public const string ToleranceLabel = "Tolerance: ";

        /// <summary>Label for background color setting.</summary>
        public const string BackgroundColorLabel = "Background Color:";

        /// <summary>Label for pixel scale setting.</summary>
        public const string PixelScaleLabel = "Pixel scale:";

        /// <summary>Label for format selection.</summary>
        public const string FormatLabel = "Format:";

        // ════════════════════════════════════════════════════════════════════
        // MENU ITEMS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Menu item text for opening in new window.</summary>
        public const string OpenInNewWindowMenu = "Open in New Window";

        /// <summary>Menu item text for duplicating in new window.</summary>
        public const string DuplicateInNewWindowMenu = "Duplicate in New Window";

        /// <summary>Menu item text for exporting layers as separate files.</summary>
        public const string LayersAsSeparateFiles = "Layers as separate files";
    }
}
