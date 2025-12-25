namespace PixlPunkt.Constants
{
    /// <summary>
    /// Constants for image export dialog and operations.
    /// </summary>
    public static class ImageExportConstants
    {
        /// <summary>Supported image export formats.</summary>
        public static readonly string[] SupportedFormats = ["png", "gif", "bmp", "jpeg", "tiff"];

        /// <summary>Display names for supported export formats.</summary>
        public static readonly string[] FormatDisplayNames = ["PNG", "GIF", "BMP", "JPEG", "TIFF"];

        /// <summary>Default export format.</summary>
        public const string DefaultFormat = "png";

        /// <summary>Default export scale factor.</summary>
        public const int DefaultScale = 1;

        /// <summary>Minimum export scale factor.</summary>
        public const int MinScale = 1;

        /// <summary>Maximum export scale factor.</summary>
        public const int MaxScale = 16;

        /// <summary>Maximum width for export preview.</summary>
        public const int PreviewMaxWidth = 320;

        /// <summary>Maximum height for export preview.</summary>
        public const int PreviewMaxHeight = 240;

        /// <summary>Formats that require a background (do not support alpha).</summary>
        public static readonly string[] FormatsRequiringBackground = ["bmp", "jpeg"];

        /// <summary>Default background color (red component) for non-alpha formats.</summary>
        public static readonly byte DefaultBgR = 255;

        /// <summary>Default background color (green component) for non-alpha formats.</summary>
        public static readonly byte DefaultBgG = 255;

        /// <summary>Default background color (blue component) for non-alpha formats.</summary>
        public static readonly byte DefaultBgB = 255;

        /// <summary>Default background color (alpha component) for non-alpha formats.</summary>
        public static readonly byte DefaultBgA = 255;

        /// <summary>Grey background color (red component) for preview.</summary>
        public static readonly byte GreyR = 170;

        /// <summary>Grey background color (green component) for preview.</summary>
        public static readonly byte GreyG = 170;

        /// <summary>Grey background color (blue component) for preview.</summary>
        public static readonly byte GreyB = 170;

        /// <summary>File type choices for file picker dialogs.</summary>
        public static readonly (string description, string extension)[] FileTypeChoices =
        [
            ("PNG Image", ".png"),
            ("GIF Image", ".gif"),
            ("BMP Image", ".bmp"),
            ("JPEG Image", ".jpg"),
            ("TIFF Image", ".tiff")
        ];
    }
}
