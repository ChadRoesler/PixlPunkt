using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.IO.Builders
{
    /// <summary>
    /// Fluent builder for creating import registrations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// yield return ImportBuilders.ForPalette("myplugin.import.txtpalette")
    ///     .WithFormat(".txt", "Text Palette", "Simple text-based palette format")
    ///     .WithHandler(ctx => ImportTextPalette(ctx))
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class ImportBuilder
    {
        private readonly string _id;
        private ImportCategory _category;
        private FileFormatInfo? _format;
        private int _priority = 0;
        private Func<IImportContext, ImportResult>? _importHandler;
        private Func<string, byte[]?, bool>? _canImportHandler;
        private Func<IImportContext, IEnumerable<IToolOption>>? _previewOptionsFactory;
        private bool _supportsPreview;

        /// <summary>
        /// Initializes a new import builder.
        /// </summary>
        /// <param name="id">The unique import handler identifier.</param>
        /// <param name="category">The import category.</param>
        public ImportBuilder(string id, ImportCategory category)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
            _category = category;
        }

        /// <summary>
        /// Sets the file format information.
        /// </summary>
        public ImportBuilder WithFormat(string extension, string displayName, string description, string? mimeType = null)
        {
            _format = new FileFormatInfo(extension, displayName, description, mimeType);
            return this;
        }

        /// <summary>
        /// Sets the file format information.
        /// </summary>
        public ImportBuilder WithFormat(FileFormatInfo format)
        {
            _format = format;
            return this;
        }

        /// <summary>
        /// Sets the priority (higher = preferred when multiple handlers match).
        /// </summary>
        public ImportBuilder WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        /// <summary>
        /// Sets the import handler function.
        /// </summary>
        public ImportBuilder WithHandler(Func<IImportContext, ImportResult> handler)
        {
            _importHandler = handler;
            return this;
        }

        /// <summary>
        /// Sets a custom "can import" check function.
        /// </summary>
        /// <param name="canImport">Function that checks if the handler can import based on extension and peek bytes.</param>
        public ImportBuilder WithCanImport(Func<string, byte[]?, bool> canImport)
        {
            _canImportHandler = canImport;
            return this;
        }

        /// <summary>
        /// Sets preview options factory for pre-import configuration.
        /// </summary>
        public ImportBuilder WithPreview(Func<IImportContext, IEnumerable<IToolOption>> previewOptionsFactory)
        {
            _previewOptionsFactory = previewOptionsFactory;
            _supportsPreview = true;
            return this;
        }

        /// <summary>
        /// Builds the import registration.
        /// </summary>
        public IImportRegistration Build()
        {
            if (_format == null)
                throw new InvalidOperationException("Import format is required. Call WithFormat() before Build().");
            if (_importHandler == null)
                throw new InvalidOperationException("Import handler is required. Call WithHandler() before Build().");

            return new BuiltImportRegistration(
                _id,
                _category,
                _format,
                _priority,
                _importHandler,
                _canImportHandler ?? ((ext, _) => ext.Equals(_format.Extension, StringComparison.OrdinalIgnoreCase)),
                _previewOptionsFactory ?? (_ => []),
                _supportsPreview
            );
        }

        private sealed class BuiltImportRegistration : IImportRegistration
        {
            private readonly Func<IImportContext, ImportResult> _importHandler;
            private readonly Func<string, byte[]?, bool> _canImportHandler;
            private readonly Func<IImportContext, IEnumerable<IToolOption>> _previewOptionsFactory;

            public string Id { get; }
            public ImportCategory Category { get; }
            public FileFormatInfo Format { get; }
            public int Priority { get; }
            public bool SupportsPreview { get; }

            public BuiltImportRegistration(
                string id,
                ImportCategory category,
                FileFormatInfo format,
                int priority,
                Func<IImportContext, ImportResult> importHandler,
                Func<string, byte[]?, bool> canImportHandler,
                Func<IImportContext, IEnumerable<IToolOption>> previewOptionsFactory,
                bool supportsPreview)
            {
                Id = id;
                Category = category;
                Format = format;
                Priority = priority;
                _importHandler = importHandler;
                _canImportHandler = canImportHandler;
                _previewOptionsFactory = previewOptionsFactory;
                SupportsPreview = supportsPreview;
            }

            public bool CanImport(string extension, byte[]? peekBytes) => _canImportHandler(extension, peekBytes);
            public ImportResult Import(IImportContext context) => _importHandler(context);
            public IEnumerable<IToolOption> GetPreviewOptions(IImportContext context) => _previewOptionsFactory(context);
        }
    }

    /// <summary>
    /// Fluent builder for creating export registrations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// yield return ExportBuilders.ForPalette("myplugin.export.txtpalette")
    ///     .WithFormat(".txt", "Text Palette", "Simple text-based palette format")
    ///     .WithHandler((ctx, data) => ExportTextPalette(ctx, data))
    ///     .Build();
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class ExportBuilder
    {
        private readonly string _id;
        private ExportCategory _category;
        private FileFormatInfo? _format;
        private int _priority = 0;
        private Func<IExportContext, object, bool>? _exportHandler;
        private Func<IEnumerable<IToolOption>>? _optionsFactory;
        private bool _hasOptions;

        /// <summary>
        /// Initializes a new export builder.
        /// </summary>
        /// <param name="id">The unique export handler identifier.</param>
        /// <param name="category">The export category.</param>
        public ExportBuilder(string id, ExportCategory category)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
            _category = category;
        }

        /// <summary>
        /// Sets the file format information.
        /// </summary>
        public ExportBuilder WithFormat(string extension, string displayName, string description, string? mimeType = null)
        {
            _format = new FileFormatInfo(extension, displayName, description, mimeType);
            return this;
        }

        /// <summary>
        /// Sets the file format information.
        /// </summary>
        public ExportBuilder WithFormat(FileFormatInfo format)
        {
            _format = format;
            return this;
        }

        /// <summary>
        /// Sets the priority (higher = appears earlier in format lists).
        /// </summary>
        public ExportBuilder WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        /// <summary>
        /// Sets the export handler function.
        /// </summary>
        public ExportBuilder WithHandler(Func<IExportContext, object, bool> handler)
        {
            _exportHandler = handler;
            return this;
        }

        /// <summary>
        /// Sets export options factory for pre-export configuration.
        /// </summary>
        public ExportBuilder WithOptions(Func<IEnumerable<IToolOption>> optionsFactory)
        {
            _optionsFactory = optionsFactory;
            _hasOptions = true;
            return this;
        }

        /// <summary>
        /// Builds the export registration.
        /// </summary>
        public IExportRegistration Build()
        {
            if (_format == null)
                throw new InvalidOperationException("Export format is required. Call WithFormat() before Build().");
            if (_exportHandler == null)
                throw new InvalidOperationException("Export handler is required. Call WithHandler() before Build().");

            return new BuiltExportRegistration(
                _id,
                _category,
                _format,
                _priority,
                _exportHandler,
                _optionsFactory ?? (() => []),
                _hasOptions
            );
        }

        private sealed class BuiltExportRegistration : IExportRegistration
        {
            private readonly Func<IExportContext, object, bool> _exportHandler;
            private readonly Func<IEnumerable<IToolOption>> _optionsFactory;

            public string Id { get; }
            public ExportCategory Category { get; }
            public FileFormatInfo Format { get; }
            public int Priority { get; }
            public bool HasOptions { get; }

            public BuiltExportRegistration(
                string id,
                ExportCategory category,
                FileFormatInfo format,
                int priority,
                Func<IExportContext, object, bool> exportHandler,
                Func<IEnumerable<IToolOption>> optionsFactory,
                bool hasOptions)
            {
                Id = id;
                Category = category;
                Format = format;
                Priority = priority;
                _exportHandler = exportHandler;
                _optionsFactory = optionsFactory;
                HasOptions = hasOptions;
            }

            public bool Export(IExportContext context, object data) => _exportHandler(context, data);
            public IEnumerable<IToolOption> GetExportOptions() => _optionsFactory();
        }
    }

    /// <summary>
    /// Specialized builder for palette import handlers.
    /// </summary>
    public sealed class PaletteImportBuilder
    {
        private readonly string _id;
        private FileFormatInfo? _format;
        private int _priority = 0;
        private Func<IImportContext, PaletteImportResult>? _importHandler;
        private Func<string, byte[]?, bool>? _canImportHandler;
        private Func<IImportContext, IEnumerable<IToolOption>>? _previewOptionsFactory;
        private byte[]? _magicBytes;

        public PaletteImportBuilder(string id)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
        }

        /// <summary>
        /// Sets the file format information.
        /// </summary>
        public PaletteImportBuilder WithFormat(string extension, string displayName, string description, string? mimeType = null)
        {
            _format = new FileFormatInfo(extension, displayName, description, mimeType);
            return this;
        }

        /// <summary>
        /// Sets the priority.
        /// </summary>
        public PaletteImportBuilder WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        /// <summary>
        /// Sets the import handler function.
        /// </summary>
        public PaletteImportBuilder WithHandler(Func<IImportContext, PaletteImportResult> handler)
        {
            _importHandler = handler;
            return this;
        }

        /// <summary>
        /// Sets a custom "can import" check function.
        /// </summary>
        public PaletteImportBuilder WithCanImport(Func<string, byte[]?, bool> canImport)
        {
            _canImportHandler = canImport;
            return this;
        }

        /// <summary>
        /// Sets magic bytes that identify the file format.
        /// When set, the handler will check for these bytes at the start of the file.
        /// </summary>
        public PaletteImportBuilder WithMagicBytes(byte[] magicBytes)
        {
            _magicBytes = magicBytes;
            return this;
        }

        /// <summary>
        /// Sets preview options factory.
        /// </summary>
        public PaletteImportBuilder WithPreview(Func<IImportContext, IEnumerable<IToolOption>> previewOptionsFactory)
        {
            _previewOptionsFactory = previewOptionsFactory;
            return this;
        }

        /// <summary>
        /// Builds the import registration.
        /// </summary>
        public IImportRegistration Build()
        {
            if (_format == null)
                throw new InvalidOperationException("Import format is required. Call WithFormat() before Build().");
            if (_importHandler == null)
                throw new InvalidOperationException("Import handler is required. Call WithHandler() before Build().");

            var handler = _importHandler;
            var format = _format;
            var magicBytes = _magicBytes;

            // Build the can import handler
            Func<string, byte[]?, bool> canImport = _canImportHandler ?? ((ext, peek) =>
            {
                bool extMatch = ext.Equals(format.Extension, StringComparison.OrdinalIgnoreCase);
                if (magicBytes != null && peek != null && peek.Length >= magicBytes.Length)
                {
                    return extMatch && CheckMagicBytes(peek, magicBytes);
                }
                return extMatch;
            });

            return new ImportBuilder(_id, ImportCategory.Palette)
                .WithFormat(_format)
                .WithPriority(_priority)
                .WithHandler(ctx => handler(ctx))
                .WithCanImport(canImport)
                .WithPreview(_previewOptionsFactory ?? (_ => []))
                .Build();
        }

        private static bool CheckMagicBytes(byte[] peek, byte[] magic)
        {
            for (int i = 0; i < magic.Length; i++)
            {
                if (peek[i] != magic[i]) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Specialized builder for image import handlers.
    /// </summary>
    public sealed class ImageImportBuilder
    {
        private readonly string _id;
        private FileFormatInfo? _format;
        private int _priority = 0;
        private Func<IImportContext, ImageImportResult>? _importHandler;
        private Func<string, byte[]?, bool>? _canImportHandler;
        private Func<IImportContext, IEnumerable<IToolOption>>? _previewOptionsFactory;
        private byte[]? _magicBytes;

        public ImageImportBuilder(string id)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
        }

        /// <summary>
        /// Sets the file format information.
        /// </summary>
        public ImageImportBuilder WithFormat(string extension, string displayName, string description, string? mimeType = null)
        {
            _format = new FileFormatInfo(extension, displayName, description, mimeType);
            return this;
        }

        /// <summary>
        /// Sets the priority.
        /// </summary>
        public ImageImportBuilder WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        /// <summary>
        /// Sets the import handler function.
        /// </summary>
        public ImageImportBuilder WithHandler(Func<IImportContext, ImageImportResult> handler)
        {
            _importHandler = handler;
            return this;
        }

        /// <summary>
        /// Sets a custom "can import" check function.
        /// </summary>
        public ImageImportBuilder WithCanImport(Func<string, byte[]?, bool> canImport)
        {
            _canImportHandler = canImport;
            return this;
        }

        /// <summary>
        /// Sets magic bytes that identify the file format.
        /// When set, the handler will check for these bytes at the start of the file.
        /// </summary>
        public ImageImportBuilder WithMagicBytes(byte[] magicBytes)
        {
            _magicBytes = magicBytes;
            return this;
        }

        /// <summary>
        /// Sets preview options factory.
        /// </summary>
        public ImageImportBuilder WithPreview(Func<IImportContext, IEnumerable<IToolOption>> previewOptionsFactory)
        {
            _previewOptionsFactory = previewOptionsFactory;
            return this;
        }

        /// <summary>
        /// Builds the import registration.
        /// </summary>
        public IImportRegistration Build()
        {
            if (_format == null)
                throw new InvalidOperationException("Import format is required. Call WithFormat() before Build().");
            if (_importHandler == null)
                throw new InvalidOperationException("Import handler is required. Call WithHandler() before Build().");

            var handler = _importHandler;
            var format = _format;
            var magicBytes = _magicBytes;

            // Build the can import handler
            Func<string, byte[]?, bool> canImport = _canImportHandler ?? ((ext, peek) =>
            {
                bool extMatch = ext.Equals(format.Extension, StringComparison.OrdinalIgnoreCase);
                if (magicBytes != null && peek != null && peek.Length >= magicBytes.Length)
                {
                    return extMatch && CheckMagicBytes(peek, magicBytes);
                }
                return extMatch;
            });

            return new ImportBuilder(_id, ImportCategory.Document)
                .WithFormat(_format)
                .WithPriority(_priority)
                .WithHandler(ctx => handler(ctx))
                .WithCanImport(canImport)
                .WithPreview(_previewOptionsFactory ?? (_ => []))
                .Build();
        }

        private static bool CheckMagicBytes(byte[] peek, byte[] magic)
        {
            for (int i = 0; i < magic.Length; i++)
            {
                if (peek[i] != magic[i]) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Specialized builder for palette export handlers.
    /// </summary>
    public sealed class PaletteExportBuilder
    {
        private readonly string _id;
        private FileFormatInfo? _format;
        private int _priority = 0;
        private Func<IExportContext, IPaletteExportData, bool>? _exportHandler;
        private Func<IEnumerable<IToolOption>>? _optionsFactory;
        private bool _hasOptions;

        public PaletteExportBuilder(string id)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
        }

        /// <summary>
        /// Sets the file format information.
        /// </summary>
        public PaletteExportBuilder WithFormat(string extension, string displayName, string description, string? mimeType = null)
        {
            _format = new FileFormatInfo(extension, displayName, description, mimeType);
            return this;
        }

        /// <summary>
        /// Sets the priority.
        /// </summary>
        public PaletteExportBuilder WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        /// <summary>
        /// Sets the export handler function.
        /// </summary>
        public PaletteExportBuilder WithHandler(Func<IExportContext, IPaletteExportData, bool> handler)
        {
            _exportHandler = handler;
            return this;
        }

        /// <summary>
        /// Sets export options factory.
        /// </summary>
        public PaletteExportBuilder WithOptions(Func<IEnumerable<IToolOption>> optionsFactory)
        {
            _optionsFactory = optionsFactory;
            _hasOptions = true;
            return this;
        }

        /// <summary>
        /// Builds the export registration.
        /// </summary>
        public IExportRegistration Build()
        {
            if (_format == null)
                throw new InvalidOperationException("Export format is required. Call WithFormat() before Build().");
            if (_exportHandler == null)
                throw new InvalidOperationException("Export handler is required. Call WithHandler() before Build().");

            var handler = _exportHandler;
            var builder = new ExportBuilder(_id, ExportCategory.Palette)
                .WithFormat(_format)
                .WithPriority(_priority)
                .WithHandler((ctx, data) => data is IPaletteExportData palette && handler(ctx, palette));

            if (_hasOptions && _optionsFactory != null)
                builder.WithOptions(_optionsFactory);

            return builder.Build();
        }
    }

    /// <summary>
    /// Static factory for creating import builders with a fluent API.
    /// </summary>
    public static class ImportBuilders
    {
        /// <summary>
        /// Starts building a document import registration.
        /// </summary>
        public static ImportBuilder ForDocument(string id) => new(id, ImportCategory.Document);

        /// <summary>
        /// Starts building a layer import registration.
        /// </summary>
        public static ImportBuilder ForLayer(string id) => new(id, ImportCategory.Layer);

        /// <summary>
        /// Starts building a palette import registration.
        /// </summary>
        public static PaletteImportBuilder ForPalette(string id) => new(id);

        /// <summary>
        /// Starts building an image import registration (convenience wrapper for document import).
        /// </summary>
        public static ImageImportBuilder ForImage(string id) => new(id);

        /// <summary>
        /// Starts building a brush import registration.
        /// </summary>
        public static ImportBuilder ForBrush(string id) => new(id, ImportCategory.Brush);

        /// <summary>
        /// Starts building a resource import registration.
        /// </summary>
        public static ImportBuilder ForResource(string id) => new(id, ImportCategory.Resource);
    }

    /// <summary>
    /// Static factory for creating export builders with a fluent API.
    /// </summary>
    public static class ExportBuilders
    {
        /// <summary>
        /// Starts building a document export registration.
        /// </summary>
        public static ExportBuilder ForDocument(string id) => new(id, ExportCategory.Document);

        /// <summary>
        /// Starts building a layer export registration.
        /// </summary>
        public static ExportBuilder ForLayer(string id) => new(id, ExportCategory.Layer);

        /// <summary>
        /// Starts building a palette export registration.
        /// </summary>
        public static PaletteExportBuilder ForPalette(string id) => new(id);

        /// <summary>
        /// Starts building a selection export registration.
        /// </summary>
        public static ExportBuilder ForSelection(string id) => new(id, ExportCategory.Selection);

        /// <summary>
        /// Starts building a resource export registration.
        /// </summary>
        public static ExportBuilder ForResource(string id) => new(id, ExportCategory.Resource);
    }
}
