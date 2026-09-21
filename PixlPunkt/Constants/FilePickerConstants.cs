namespace PixlPunkt.Constants
{
    /// <summary>
    /// File extension constants for supported file formats.
    /// </summary>
    public static class FileExtensions
    {
        // ════════════════════════════════════════════════════════════════════
        // NATIVE FORMAT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Native PixlPunkt document extension.</summary>
        public const string PixlPunktDocument = ".pxp";

        /// <summary>
        /// Pixel font documents. The same container as <see cref="PixlPunktDocument"/> with the
        /// font section filled in, so either extension loads either file; this one exists so the
        /// shell, the Open dialog and a folder listing can tell a font from a sprite sheet.
        /// </summary>
        public const string PixlPunktFont = ".pxpf";

        // ════════════════════════════════════════════════════════════════════
        // IMPORT FORMATS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Pyxel Edit project file extension.</summary>
        public const string PyxelEdit = ".pyxel";

        /// <summary>Windows icon file extension.</summary>
        public const string WindowsIcon = ".ico";

        /// <summary>Aseprite file extension.</summary>
        public const string Aseprite = ".ase";

        // ════════════════════════════════════════════════════════════════════
        // IMAGE FORMATS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>PNG image file extension.</summary>
        public const string Png = ".png";

        /// <summary>BMP image file extension.</summary>
        public const string Bmp = ".bmp";

        /// <summary>JPG image file extension.</summary>
        public const string Jpg = ".jpg";

        /// <summary>JPEG image file extension (alternate).</summary>
        public const string Jpeg = ".jpeg";

        /// <summary>GIF image file extension.</summary>
        public const string Gif = ".gif";

        /// <summary>TIFF image file extension.</summary>
        public const string Tiff = ".tiff";

        // ════════════════════════════════════════════════════════════════════
        // FILE TYPE DESCRIPTIONS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Description for PixlPunkt document type.</summary>
        public const string PixlPunktDocumentDescription = "PixlPunkt document";
        public const string PixlPunktFontDescription = "PixlPunkt font";
    }
}
