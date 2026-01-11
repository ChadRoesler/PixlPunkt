using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixlPunkt.Core.Plugins;

namespace PixlPunkt.Core.IO
{
    /// <summary>
    /// Registry for import handlers, including both built-in and plugin-provided handlers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ImportRegistry maintains a collection of <see cref="IImportRegistration"/> handlers
    /// that can import various file formats. Handlers are matched by extension and priority.
    /// </para>
    /// <para>
    /// <strong>Usage:</strong>
    /// <code>
    /// // Register a handler
    /// ImportRegistry.Instance.Register(myHandler);
    /// 
    /// // Find handlers for a file
    /// var handlers = ImportRegistry.Instance.FindHandlers(".png", ImportCategory.Document);
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class ImportRegistry
    {
        private static ImportRegistry? _instance;
        private readonly List<IImportRegistration> _handlers = [];
        private readonly object _lock = new();

        /// <summary>
        /// Gets the singleton instance of the import registry.
        /// </summary>
        public static ImportRegistry Instance => _instance ??= new ImportRegistry();

        private ImportRegistry() { }

        /// <summary>
        /// Registers an import handler.
        /// </summary>
        /// <param name="handler">The import registration to add.</param>
        public void Register(IImportRegistration handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            lock (_lock)
            {
                // Remove any existing handler with same ID
                _handlers.RemoveAll(h => h.Id == handler.Id);
                _handlers.Add(handler);
            }
        }

        /// <summary>
        /// Unregisters an import handler by ID.
        /// </summary>
        /// <param name="handlerId">The ID of the handler to remove.</param>
        /// <returns>True if a handler was removed; false otherwise.</returns>
        public bool Unregister(string handlerId)
        {
            lock (_lock)
            {
                return _handlers.RemoveAll(h => h.Id == handlerId) > 0;
            }
        }

        /// <summary>
        /// Finds all handlers that can import the given file extension.
        /// </summary>
        /// <param name="extension">The file extension (including dot).</param>
        /// <param name="category">Optional category filter.</param>
        /// <param name="peekBytes">Optional first bytes of file for magic number detection.</param>
        /// <returns>Handlers sorted by priority (highest first).</returns>
        public IReadOnlyList<IImportRegistration> FindHandlers(
            string extension,
            ImportCategory? category = null,
            byte[]? peekBytes = null)
        {
            lock (_lock)
            {
                return _handlers
                    .Where(h => (category == null || h.Category == category) && h.CanImport(extension, peekBytes))
                    .OrderByDescending(h => h.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all registered handlers.
        /// </summary>
        public IReadOnlyList<IImportRegistration> GetAllHandlers()
        {
            lock (_lock)
            {
                return [.. _handlers];
            }
        }

        /// <summary>
        /// Gets handlers for a specific category.
        /// </summary>
        public IReadOnlyList<IImportRegistration> GetHandlersByCategory(ImportCategory category)
        {
            lock (_lock)
            {
                return _handlers
                    .Where(h => h.Category == category)
                    .OrderByDescending(h => h.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all supported extensions for a category.
        /// </summary>
        public IReadOnlyList<string> GetSupportedExtensions(ImportCategory category)
        {
            lock (_lock)
            {
                return _handlers
                    .Where(h => h.Category == category)
                    .Select(h => h.Format.Extension)
                    .Distinct()
                    .OrderBy(e => e)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets handlers that come from plugins only (not built-in handlers).
        /// </summary>
        /// <param name="category">Optional category filter.</param>
        /// <returns>Handlers provided by plugins, sorted by priority.</returns>
        public IReadOnlyList<IImportRegistration> GetPluginHandlers(ImportCategory? category = null)
        {
            lock (_lock)
            {
                return _handlers
                    .Where(h => (category == null || h.Category == category) &&
                                PluginRegistry.Instance.GetPluginForImport(h.Id) != null)
                    .OrderByDescending(h => h.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Checks if there are any plugin-provided handlers for a category.
        /// </summary>
        public bool HasPluginHandlers(ImportCategory? category = null)
        {
            lock (_lock)
            {
                return _handlers.Any(h =>
                    (category == null || h.Category == category) &&
                    PluginRegistry.Instance.GetPluginForImport(h.Id) != null);
            }
        }

        /// <summary>
        /// Clears all registered handlers.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _handlers.Clear();
            }
        }
    }

    /// <summary>
    /// Registry for export handlers, including both built-in and plugin-provided handlers.
    /// </summary>
    public sealed class ExportRegistry
    {
        private static ExportRegistry? _instance;
        private readonly List<IExportRegistration> _handlers = [];
        private readonly object _lock = new();

        /// <summary>
        /// Gets the singleton instance of the export registry.
        /// </summary>
        public static ExportRegistry Instance => _instance ??= new ExportRegistry();

        private ExportRegistry() { }

        /// <summary>
        /// Registers an export handler.
        /// </summary>
        public void Register(IExportRegistration handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            lock (_lock)
            {
                _handlers.RemoveAll(h => h.Id == handler.Id);
                _handlers.Add(handler);
            }
        }

        /// <summary>
        /// Unregisters an export handler by ID.
        /// </summary>
        public bool Unregister(string handlerId)
        {
            lock (_lock)
            {
                return _handlers.RemoveAll(h => h.Id == handlerId) > 0;
            }
        }

        /// <summary>
        /// Finds handlers that can export to the given extension.
        /// </summary>
        public IReadOnlyList<IExportRegistration> FindHandlers(
            string extension,
            ExportCategory? category = null)
        {
            lock (_lock)
            {
                return _handlers
                    .Where(h => (category == null || h.Category == category) &&
                                h.Format.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(h => h.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all registered handlers.
        /// </summary>
        public IReadOnlyList<IExportRegistration> GetAllHandlers()
        {
            lock (_lock)
            {
                return [.. _handlers];
            }
        }

        /// <summary>
        /// Gets handlers for a specific category.
        /// </summary>
        public IReadOnlyList<IExportRegistration> GetHandlersByCategory(ExportCategory category)
        {
            lock (_lock)
            {
                return _handlers
                    .Where(h => h.Category == category)
                    .OrderByDescending(h => h.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all supported extensions for a category.
        /// </summary>
        public IReadOnlyList<string> GetSupportedExtensions(ExportCategory category)
        {
            lock (_lock)
            {
                return _handlers
                    .Where(h => h.Category == category)
                    .Select(h => h.Format.Extension)
                    .Distinct()
                    .OrderBy(e => e)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets handlers that come from plugins only (not built-in handlers).
        /// </summary>
        /// <param name="category">Optional category filter.</param>
        /// <returns>Handlers provided by plugins, sorted by priority.</returns>
        public IReadOnlyList<IExportRegistration> GetPluginHandlers(ExportCategory? category = null)
        {
            lock (_lock)
            {
                return _handlers
                    .Where(h => (category == null || h.Category == category) &&
                                PluginRegistry.Instance.GetPluginForExport(h.Id) != null)
                    .OrderByDescending(h => h.Priority)
                    .ToList();
            }
        }

        /// <summary>
        /// Checks if there are any plugin-provided handlers for a category.
        /// </summary>
        public bool HasPluginHandlers(ExportCategory? category = null)
        {
            lock (_lock)
            {
                return _handlers.Any(h =>
                    (category == null || h.Category == category) &&
                    PluginRegistry.Instance.GetPluginForExport(h.Id) != null);
            }
        }

        /// <summary>
        /// Clears all registered handlers.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _handlers.Clear();
            }
        }
    }

    /// <summary>
    /// File-based implementation of <see cref="IImportContext"/>.
    /// </summary>
    public sealed class FileImportContext : IImportContext
    {
        private readonly string _filePath;
        private readonly Action<double, string?>? _progressCallback;

        /// <summary>
        /// Creates a new file import context.
        /// </summary>
        /// <param name="filePath">Full path to the file being imported.</param>
        /// <param name="progressCallback">Optional callback for progress reporting.</param>
        public FileImportContext(string filePath, Action<double, string?>? progressCallback = null)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _progressCallback = progressCallback;
        }

        /// <inheritdoc/>
        public string FileName => Path.GetFileName(_filePath);

        /// <inheritdoc/>
        public string Extension => Path.GetExtension(_filePath);

        /// <inheritdoc/>
        public byte[] ReadAllBytes() => File.ReadAllBytes(_filePath);

        /// <inheritdoc/>
        public string ReadAllText() => File.ReadAllText(_filePath);

        /// <inheritdoc/>
        public Stream OpenRead() => File.OpenRead(_filePath);

        /// <inheritdoc/>
        public void ReportProgress(double progress, string? message = null)
        {
            _progressCallback?.Invoke(Math.Clamp(progress, 0.0, 1.0), message);
        }
    }

    /// <summary>
    /// File-based implementation of <see cref="IExportContext"/>.
    /// </summary>
    public sealed class FileExportContext : IExportContext
    {
        private readonly string _filePath;
        private readonly Action<double, string?>? _progressCallback;

        /// <summary>
        /// Creates a new file export context.
        /// </summary>
        /// <param name="filePath">Full path to the target file.</param>
        /// <param name="progressCallback">Optional callback for progress reporting.</param>
        public FileExportContext(string filePath, Action<double, string?>? progressCallback = null)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _progressCallback = progressCallback;

            // Ensure directory exists
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        /// <inheritdoc/>
        public string FileName => Path.GetFileName(_filePath);

        /// <inheritdoc/>
        public void WriteAllBytes(byte[] data) => File.WriteAllBytes(_filePath, data);

        /// <inheritdoc/>
        public void WriteAllText(string text) => File.WriteAllText(_filePath, text);

        /// <inheritdoc/>
        public Stream OpenWrite() => File.Create(_filePath);

        /// <inheritdoc/>
        public void ReportProgress(double progress, string? message = null)
        {
            _progressCallback?.Invoke(Math.Clamp(progress, 0.0, 1.0), message);
        }
    }

    /// <summary>
    /// Memory-based implementation of <see cref="IImportContext"/> for testing or clipboard imports.
    /// </summary>
    public sealed class MemoryImportContext : IImportContext
    {
        private readonly byte[] _data;
        private readonly string _fileName;
        private readonly string _extension;

        /// <summary>
        /// Creates a new memory import context.
        /// </summary>
        public MemoryImportContext(byte[] data, string fileName, string extension)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _fileName = fileName;
            _extension = extension;
        }

        /// <inheritdoc/>
        public string FileName => _fileName;

        /// <inheritdoc/>
        public string Extension => _extension;

        /// <inheritdoc/>
        public byte[] ReadAllBytes() => _data;

        /// <inheritdoc/>
        public string ReadAllText() => System.Text.Encoding.UTF8.GetString(_data);

        /// <inheritdoc/>
        public Stream OpenRead() => new MemoryStream(_data, writable: false);

        /// <inheritdoc/>
        public void ReportProgress(double progress, string? message = null) { }
    }

    /// <summary>
    /// Memory-based implementation of <see cref="IExportContext"/> for clipboard exports.
    /// </summary>
    public sealed class MemoryExportContext : IExportContext
    {
        private readonly MemoryStream _stream = new();
        private byte[]? _data;

        /// <summary>
        /// Creates a new memory export context.
        /// </summary>
        public MemoryExportContext(string fileName)
        {
            FileName = fileName;
        }

        /// <inheritdoc/>
        public string FileName { get; }

        /// <summary>
        /// Gets the exported data after export completes.
        /// </summary>
        public byte[] GetData()
        {
            if (_data != null) return _data;
            return _stream.ToArray();
        }

        /// <inheritdoc/>
        public void WriteAllBytes(byte[] data) => _data = data;

        /// <inheritdoc/>
        public void WriteAllText(string text) => _data = System.Text.Encoding.UTF8.GetBytes(text);

        /// <inheritdoc/>
        public Stream OpenWrite() => _stream;

        /// <inheritdoc/>
        public void ReportProgress(double progress, string? message = null) { }
    }

    /// <summary>
    /// Implementation of <see cref="IPaletteExportData"/> for palette exports.
    /// </summary>
    public sealed class PaletteExportData : IPaletteExportData
    {
        /// <inheritdoc/>
        public IReadOnlyList<uint> Colors { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <summary>
        /// Creates palette export data.
        /// </summary>
        public PaletteExportData(IReadOnlyList<uint> colors, string name)
        {
            Colors = colors ?? throw new ArgumentNullException(nameof(colors));
            Name = name ?? "Untitled Palette";
        }
    }

    /// <summary>
    /// Implementation of <see cref="IImageExportData"/> for image exports.
    /// </summary>
    public sealed class ImageExportData : IImageExportData
    {
        /// <inheritdoc/>
        public uint[] Pixels { get; }

        /// <inheritdoc/>
        public int Width { get; }

        /// <inheritdoc/>
        public int Height { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <summary>
        /// Creates image export data.
        /// </summary>
        public ImageExportData(uint[] pixels, int width, int height, string name)
        {
            Pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
            Width = width;
            Height = height;
            Name = name ?? "Untitled";
        }
    }
}
