namespace PixlPunkt.Core.Enums
{
    /// <summary>
    /// Defines the supported image file formats for import and export operations.
    /// </summary>
    /// <remarks>
    /// These formats are used when saving or loading images to/from disk.
    /// Each format has different characteristics regarding compression, transparency, and metadata support.
    /// </remarks>
    public enum ImageFileFormat
    {
        /// <summary>
        /// Portable Network Graphics format with lossless compression and full alpha transparency.
        /// </summary>
        Png,

        /// <summary>
        /// JPEG format with lossy compression, typically used for photographs without transparency.
        /// </summary>
        Jpeg,

        /// <summary>
        /// Bitmap format with minimal or no compression, supporting various bit depths.
        /// </summary>
        Bmp,

        /// <summary>
        /// Tagged Image File Format supporting multiple compression schemes and metadata.
        /// </summary>
        Tiff,

        /// <summary>
        /// Windows cursor file format containing one or more images with hotspot information.
        /// </summary>
        Cur
    }
}
