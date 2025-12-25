namespace PixlPunkt.PluginSdk.IO
{
    /// <summary>
    /// Registration for a file import handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Import registrations allow plugins to add support for importing custom file formats.
    /// The host application will show the format in import dialogs and call the handler
    /// when the user selects a file of that type.
    /// </para>
    /// </remarks>
    public interface IImportRegistration
    {
        /// <summary>
        /// Gets the unique identifier for this import handler.
        /// </summary>
        /// <value>
        /// A string following the convention <c>{vendor}.import.{format}</c>.
        /// For example: <c>"myplugin.import.custompalette"</c>.
        /// </value>
        string Id { get; }

        /// <summary>
        /// Gets the import category.
        /// </summary>
        ImportCategory Category { get; }

        /// <summary>
        /// Gets the file format information.
        /// </summary>
        FileFormatInfo Format { get; }

        /// <summary>
        /// Gets the priority for this handler (higher = preferred when multiple handlers match).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Checks if this handler can import the given file based on extension/content.
        /// </summary>
        /// <param name="extension">The file extension (including dot).</param>
        /// <param name="peekBytes">First few bytes of the file for magic number detection (may be null).</param>
        /// <returns>True if this handler can import the file.</returns>
        bool CanImport(string extension, byte[]? peekBytes);

        /// <summary>
        /// Imports the file and returns the result.
        /// </summary>
        /// <param name="context">The import context providing file access.</param>
        /// <returns>The import result.</returns>
        ImportResult Import(IImportContext context);

        /// <summary>
        /// Gets optional preview options to display before import.
        /// </summary>
        /// <param name="context">The import context.</param>
        /// <returns>Tool options for preview/configuration UI, or empty if no preview needed.</returns>
        IEnumerable<Settings.IToolOption> GetPreviewOptions(IImportContext context);

        /// <summary>
        /// Gets whether this import handler supports preview.
        /// </summary>
        bool SupportsPreview { get; }
    }

    /// <summary>
    /// Registration for a file export handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Export registrations allow plugins to add support for exporting to custom file formats.
    /// The host application will show the format in export dialogs and call the handler
    /// when the user chooses that format.
    /// </para>
    /// </remarks>
    public interface IExportRegistration
    {
        /// <summary>
        /// Gets the unique identifier for this export handler.
        /// </summary>
        /// <value>
        /// A string following the convention <c>{vendor}.export.{format}</c>.
        /// For example: <c>"myplugin.export.custompalette"</c>.
        /// </value>
        string Id { get; }

        /// <summary>
        /// Gets the export category.
        /// </summary>
        ExportCategory Category { get; }

        /// <summary>
        /// Gets the file format information.
        /// </summary>
        FileFormatInfo Format { get; }

        /// <summary>
        /// Gets the priority for this handler (higher = preferred in format lists).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets optional configuration options to display before export.
        /// </summary>
        /// <returns>Tool options for export configuration UI, or empty if no options needed.</returns>
        IEnumerable<Settings.IToolOption> GetExportOptions();

        /// <summary>
        /// Gets whether this export handler supports configuration options.
        /// </summary>
        bool HasOptions { get; }

        /// <summary>
        /// Exports the data to the file.
        /// </summary>
        /// <param name="context">The export context providing file access.</param>
        /// <param name="data">The data to export.</param>
        /// <returns>True if export succeeded; false otherwise.</returns>
        bool Export(IExportContext context, object data);
    }

    /// <summary>
    /// Specialized export registration for image/document formats.
    /// </summary>
    public interface IImageExportRegistration : IExportRegistration
    {
        /// <summary>
        /// Exports image data to the file.
        /// </summary>
        /// <param name="context">The export context providing file access.</param>
        /// <param name="imageData">The image data to export.</param>
        /// <returns>True if export succeeded; false otherwise.</returns>
        bool ExportImage(IExportContext context, IImageExportData imageData);
    }

    /// <summary>
    /// Specialized export registration for palette formats.
    /// </summary>
    public interface IPaletteExportRegistration : IExportRegistration
    {
        /// <summary>
        /// Exports palette data to the file.
        /// </summary>
        /// <param name="context">The export context providing file access.</param>
        /// <param name="paletteData">The palette data to export.</param>
        /// <returns>True if export succeeded; false otherwise.</returns>
        bool ExportPalette(IExportContext context, IPaletteExportData paletteData);
    }
}
