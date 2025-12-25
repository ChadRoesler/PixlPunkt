namespace PixlPunkt.PluginSdk.IO
{
    /// <summary>
    /// Describes a file format supported for import or export.
    /// </summary>
    /// <param name="Extension">The file extension including the dot (e.g., ".myformat").</param>
    /// <param name="DisplayName">Human-readable format name (e.g., "My Custom Format").</param>
    /// <param name="Description">Brief description of the format.</param>
    /// <param name="MimeType">Optional MIME type (e.g., "image/x-myformat").</param>
    public sealed record FileFormatInfo(
        string Extension,
        string DisplayName,
        string Description,
        string? MimeType = null
    )
    {
        /// <summary>
        /// Gets the extension without the leading dot.
        /// </summary>
        public string ExtensionNoDot => Extension.TrimStart('.');

        /// <summary>
        /// Gets a file filter string for dialogs (e.g., "*.myformat").
        /// </summary>
        public string FileFilter => $"*{Extension}";
    }

    /// <summary>
    /// Category of import operation.
    /// </summary>
    public enum ImportCategory
    {
        /// <summary>Import as a new document/image.</summary>
        Document,

        /// <summary>Import as a new layer in the current document.</summary>
        Layer,

        /// <summary>Import a color palette.</summary>
        Palette,

        /// <summary>Import brushes or brush packs.</summary>
        Brush,

        /// <summary>Import other resources (effects presets, etc.).</summary>
        Resource
    }

    /// <summary>
    /// Category of export operation.
    /// </summary>
    public enum ExportCategory
    {
        /// <summary>Export the entire document/image.</summary>
        Document,

        /// <summary>Export a single layer.</summary>
        Layer,

        /// <summary>Export a color palette.</summary>
        Palette,

        /// <summary>Export selection only.</summary>
        Selection,

        /// <summary>Export other resources.</summary>
        Resource
    }

    /// <summary>
    /// Result of an import operation.
    /// </summary>
    public abstract class ImportResult
    {
        /// <summary>
        /// Gets whether the import was successful.
        /// </summary>
        public abstract bool Success { get; }

        /// <summary>
        /// Gets an error message if the import failed.
        /// </summary>
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Result of importing image data.
    /// </summary>
    public sealed class ImageImportResult : ImportResult
    {
        /// <inheritdoc/>
        public override bool Success => Pixels != null && Width > 0 && Height > 0;

        /// <summary>
        /// Gets the imported pixel data in BGRA format.
        /// </summary>
        public uint[]? Pixels { get; init; }

        /// <summary>
        /// Gets the image width.
        /// </summary>
        public int Width { get; init; }

        /// <summary>
        /// Gets the image height.
        /// </summary>
        public int Height { get; init; }

        /// <summary>
        /// Gets an optional suggested name for the imported content.
        /// </summary>
        public string? SuggestedName { get; init; }
    }

    /// <summary>
    /// Result of importing palette data.
    /// </summary>
    public sealed class PaletteImportResult : ImportResult
    {
        /// <inheritdoc/>
        public override bool Success => Colors != null && Colors.Count > 0;

        /// <summary>
        /// Gets the imported colors in BGRA format.
        /// </summary>
        public IReadOnlyList<uint>? Colors { get; init; }

        /// <summary>
        /// Gets the palette name.
        /// </summary>
        public string? PaletteName { get; init; }
    }

    /// <summary>
    /// Context provided to import handlers.
    /// </summary>
    public interface IImportContext
    {
        /// <summary>
        /// Reads all bytes from the import source.
        /// </summary>
        byte[] ReadAllBytes();

        /// <summary>
        /// Reads all text from the import source using UTF-8 encoding.
        /// </summary>
        string ReadAllText();

        /// <summary>
        /// Opens a stream for reading the import source.
        /// </summary>
        Stream OpenRead();

        /// <summary>
        /// Gets the file name being imported (without path).
        /// </summary>
        string FileName { get; }

        /// <summary>
        /// Gets the file extension being imported (including dot).
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Reports progress during import (0.0 to 1.0).
        /// </summary>
        void ReportProgress(double progress, string? message = null);
    }

    /// <summary>
    /// Context provided to export handlers.
    /// </summary>
    public interface IExportContext
    {
        /// <summary>
        /// Writes all bytes to the export destination.
        /// </summary>
        void WriteAllBytes(byte[] data);

        /// <summary>
        /// Writes all text to the export destination using UTF-8 encoding.
        /// </summary>
        void WriteAllText(string text);

        /// <summary>
        /// Opens a stream for writing to the export destination.
        /// </summary>
        Stream OpenWrite();

        /// <summary>
        /// Gets the target file name (without path).
        /// </summary>
        string FileName { get; }

        /// <summary>
        /// Reports progress during export (0.0 to 1.0).
        /// </summary>
        void ReportProgress(double progress, string? message = null);
    }

    /// <summary>
    /// Data provided for document/layer export.
    /// </summary>
    public interface IImageExportData
    {
        /// <summary>
        /// Gets the pixel data in BGRA format.
        /// </summary>
        uint[] Pixels { get; }

        /// <summary>
        /// Gets the image width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the image height.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the document/layer name.
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// Data provided for palette export.
    /// </summary>
    public interface IPaletteExportData
    {
        /// <summary>
        /// Gets the colors in BGRA format.
        /// </summary>
        IReadOnlyList<uint> Colors { get; }

        /// <summary>
        /// Gets the palette name.
        /// </summary>
        string Name { get; }
    }
}
