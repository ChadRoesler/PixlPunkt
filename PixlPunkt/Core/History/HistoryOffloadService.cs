using System;
using System.Collections.Concurrent;
using System.IO;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Settings;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// Service that manages offloading history item data to disk for memory conservation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="HistoryOffloadService"/> provides persistent storage for history item data
    /// when memory limits are exceeded. This allows:
    /// </para>
    /// <list type="bullet">
    /// <item>Unlimited undo history without unbounded memory growth</item>
    /// <item>Full timelapse export even with memory-constrained history</item>
    /// <item>Automatic cleanup when documents are closed</item>
    /// </list>
    /// <para>
    /// Data is stored in a temporary folder specific to each document session.
    /// Files are named by GUID and automatically cleaned up on disposal.
    /// </para>
    /// </remarks>
    public sealed class HistoryOffloadService : IHistoryOffloadService, IDisposable
    {
        private readonly string _offloadDirectory;
        private readonly ConcurrentDictionary<Guid, string> _dataFiles = new();
        private bool _disposed;

        /// <summary>
        /// Gets the total size of offloaded data in bytes.
        /// </summary>
        public long TotalOffloadedBytes { get; private set; }

        /// <summary>
        /// Gets the number of offloaded items.
        /// </summary>
        public int OffloadedItemCount => _dataFiles.Count;

        /// <summary>
        /// Creates a new history offload service.
        /// </summary>
        /// <param name="documentId">Unique identifier for the document (used for folder naming).</param>
        public HistoryOffloadService(string? documentId = null)
        {
            var id = documentId ?? Guid.NewGuid().ToString("N")[..8];
            _offloadDirectory = Path.Combine(AppPaths.TempDirectory, "HistoryOffload", id);

            try
            {
                if (!Directory.Exists(_offloadDirectory))
                {
                    Directory.CreateDirectory(_offloadDirectory);
                }
                LoggingService.Debug("HistoryOffloadService initialized: {Path}", _offloadDirectory);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to create history offload directory: {Error}", ex.Message);
                throw;
            }
        }

        /// <inheritdoc/>
        public void WriteData(Guid id, byte[] data)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HistoryOffloadService));
            if (data == null || data.Length == 0) return;

            try
            {
                var filePath = Path.Combine(_offloadDirectory, $"{id:N}.dat");
                File.WriteAllBytes(filePath, data);
                _dataFiles[id] = filePath;
                TotalOffloadedBytes += data.Length;

                LoggingService.Debug("History data offloaded: {Id} size={Size}KB total={Total}MB",
                    id.ToString()[..8], data.Length / 1024, TotalOffloadedBytes / (1024 * 1024));
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to offload history data {Id}: {Error}", id, ex.Message);
                throw;
            }
        }

        /// <inheritdoc/>
        public byte[]? ReadData(Guid id)
        {
            if (_disposed) return null;

            if (!_dataFiles.TryGetValue(id, out var filePath))
            {
                LoggingService.Warning("History data not found for reload: {Id}", id.ToString()[..8]);
                return null;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    LoggingService.Warning("History data file missing: {Path}", filePath);
                    _dataFiles.TryRemove(id, out _);
                    return null;
                }

                var data = File.ReadAllBytes(filePath);
                LoggingService.Debug("History data reloaded: {Id} size={Size}KB", id.ToString()[..8], data.Length / 1024);
                return data;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to reload history data {Id}: {Error}", id, ex.Message);
                return null;
            }
        }

        /// <inheritdoc/>
        public void RemoveData(Guid id)
        {
            if (_disposed) return;

            if (_dataFiles.TryRemove(id, out var filePath))
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var info = new FileInfo(filePath);
                        TotalOffloadedBytes -= info.Length;
                        File.Delete(filePath);
                        LoggingService.Debug("History data removed: {Id}", id.ToString()[..8]);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Warning("Failed to delete offload file {Path}: {Error}", filePath, ex.Message);
                }
            }
        }

        /// <inheritdoc/>
        public string? GetDataPath(Guid id)
        {
            if (_disposed) return null;
            return _dataFiles.TryGetValue(id, out var path) && File.Exists(path) ? path : null;
        }

        /// <summary>
        /// Clears all offloaded data and removes the directory.
        /// </summary>
        public void ClearAll()
        {
            foreach (var id in _dataFiles.Keys)
            {
                RemoveData(id);
            }

            try
            {
                if (Directory.Exists(_offloadDirectory) && Directory.GetFiles(_offloadDirectory).Length == 0)
                {
                    Directory.Delete(_offloadDirectory, recursive: false);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Failed to delete offload directory: {Error}", ex.Message);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ClearAll();
            LoggingService.Debug("HistoryOffloadService disposed");
        }
    }
}
