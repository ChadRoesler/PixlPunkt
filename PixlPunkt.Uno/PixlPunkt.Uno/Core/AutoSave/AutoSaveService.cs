using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Settings;

namespace PixlPunkt.Uno.Core.AutoSave
{
    /// <summary>
    /// Service that automatically saves open documents at configurable intervals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AutoSaveService monitors registered documents and saves them periodically to a backup location.
    /// Each save creates a timestamped file (e.g., "MyCanvas_2024-01-15-14-30-00.pxp") to preserve
    /// version history and prevent data loss.
    /// </para>
    /// <para><strong>Configuration:</strong></para>
    /// <list type="bullet">
    /// <item>Save interval: Configured via <see cref="AppSettings.AutoBackupMinutes"/></item>
    /// <item>Save location: Configured via <see cref="AppSettings.StorageFolderPath"/></item>
    /// </list>
    /// </remarks>
    public sealed class AutoSaveService : IDisposable
    {
        private readonly object _lock = new();
        private readonly HashSet<CanvasDocument> _documents = [];
        private readonly Dictionary<CanvasDocument, DateTime> _lastSaveTimes = [];
        private Timer? _timer;
        private bool _disposed;

        /// <summary>
        /// Gets or sets the save interval in minutes. Default is from AppSettings.
        /// </summary>
        public int IntervalMinutes { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of backups to retain per document.
        /// </summary>
        public int MaxBackupCount { get; set; }

        /// <summary>
        /// Gets or sets the folder path where auto-saves are stored.
        /// </summary>
        public string SaveFolderPath { get; set; }

        /// <summary>
        /// Gets whether auto-save is enabled (has a valid save path).
        /// </summary>
        public bool IsEnabled => !string.IsNullOrWhiteSpace(SaveFolderPath);

        /// <summary>
        /// Occurs when an auto-save operation completes (success or failure).
        /// </summary>
        public event Action<CanvasDocument, bool, string?>? AutoSaveCompleted;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoSaveService"/> class.
        /// </summary>
        public AutoSaveService()
        {
            var settings = AppSettings.Instance;
            IntervalMinutes = Math.Max(1, settings.AutoBackupMinutes);
            MaxBackupCount = Math.Max(1, settings.MaxBackupCount);
            SaveFolderPath = GetEffectiveSavePath(settings.StorageFolderPath);

            Debug.WriteLine($"[AutoSave] Initialized: Interval={IntervalMinutes}min, MaxBackups={MaxBackupCount}, Path={SaveFolderPath}");
        }

        /// <summary>
        /// Gets the effective save path, falling back to default if not configured.
        /// </summary>
        private static string GetEffectiveSavePath(string? configuredPath)
        {
            // If a path is configured and exists (or can be created), use it
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                try
                {
                    if (!Directory.Exists(configuredPath))
                        Directory.CreateDirectory(configuredPath);
                    return configuredPath;
                }
                catch
                {
                    Debug.WriteLine($"[AutoSave] Could not use configured path: {configuredPath}");
                }
            }

            // Default: use centralized AppPaths location
            var defaultPath = AppPaths.AutoSaveDirectory;

            try
            {
                AppPaths.EnsureDirectoryExists(defaultPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoSave] Could not create default path: {ex.Message}");
            }

            return defaultPath;
        }

        /// <summary>
        /// Starts the auto-save timer.
        /// </summary>
        public void Start()
        {
            if (_disposed) return;

            lock (_lock)
            {
                _timer?.Dispose();

                // Start checking after 30 seconds, then every minute
                // This gives the app time to initialize while still catching early saves
                var initialDelay = TimeSpan.FromSeconds(30);
                var checkInterval = TimeSpan.FromMinutes(1);
                _timer = new Timer(OnTimerTick, null, initialDelay, checkInterval);

                Debug.WriteLine($"[AutoSave] Started: checking every {checkInterval.TotalMinutes}min, saving every {IntervalMinutes}min to: {SaveFolderPath}");
            }
        }

        /// <summary>
        /// Stops the auto-save timer.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
                Debug.WriteLine("[AutoSave] Stopped");
            }
        }

        /// <summary>
        /// Registers a document for auto-saving.
        /// </summary>
        /// <param name="doc">The document to register.</param>
        public void RegisterDocument(CanvasDocument doc)
        {
            if (doc == null) return;

            lock (_lock)
            {
                if (_documents.Add(doc))
                {
                    // Set last save time to now - this means first auto-save will happen after IntervalMinutes
                    _lastSaveTimes[doc] = DateTime.Now;
                    Debug.WriteLine($"[AutoSave] Registered document: {doc.Name} (total: {_documents.Count})");
                }
            }
        }

        /// <summary>
        /// Unregisters a document from auto-saving.
        /// </summary>
        /// <param name="doc">The document to unregister.</param>
        public void UnregisterDocument(CanvasDocument doc)
        {
            if (doc == null) return;

            lock (_lock)
            {
                _documents.Remove(doc);
                _lastSaveTimes.Remove(doc);
                Debug.WriteLine($"[AutoSave] Unregistered document: {doc.Name} (remaining: {_documents.Count})");
            }
        }

        /// <summary>
        /// Refreshes settings from AppSettings (call after settings change).
        /// </summary>
        public void RefreshSettings()
        {
            var settings = AppSettings.Instance;
            IntervalMinutes = Math.Max(1, settings.AutoBackupMinutes);
            MaxBackupCount = Math.Max(1, settings.MaxBackupCount);
            SaveFolderPath = GetEffectiveSavePath(settings.StorageFolderPath);
            Debug.WriteLine($"[AutoSave] Settings refreshed: {IntervalMinutes}min, MaxBackups={MaxBackupCount}, path: {SaveFolderPath}");
        }

        /// <summary>
        /// Forces an immediate save of all registered documents.
        /// </summary>
        public void SaveAllNow()
        {
            List<CanvasDocument> docsToSave;
            lock (_lock)
            {
                docsToSave = [.. _documents];
                Debug.WriteLine($"[AutoSave] SaveAllNow called for {docsToSave.Count} documents");
            }

            foreach (var doc in docsToSave)
            {
                SaveDocument(doc);
            }
        }

        /// <summary>
        /// Timer callback - checks and saves documents that have exceeded their interval.
        /// </summary>
        private void OnTimerTick(object? state)
        {
            if (_disposed) return;

            // Reload settings in case they changed
            var settings = AppSettings.Instance;
            IntervalMinutes = Math.Max(1, settings.AutoBackupMinutes);
            MaxBackupCount = Math.Max(1, settings.MaxBackupCount);
            SaveFolderPath = GetEffectiveSavePath(settings.StorageFolderPath);

            List<CanvasDocument> docsToSave = [];

            lock (_lock)
            {
                var now = DateTime.Now;
                var interval = TimeSpan.FromMinutes(IntervalMinutes);

                Debug.WriteLine($"[AutoSave] Timer tick: checking {_documents.Count} documents (interval: {IntervalMinutes}min, maxBackups: {MaxBackupCount})");

                foreach (var doc in _documents)
                {
                    if (_lastSaveTimes.TryGetValue(doc, out var lastSave))
                    {
                        var elapsed = now - lastSave;
                        if (elapsed >= interval)
                        {
                            Debug.WriteLine($"[AutoSave] Document '{doc.Name}' needs save (elapsed: {elapsed.TotalMinutes:F1}min)");
                            docsToSave.Add(doc);
                        }
                        else
                        {
                            Debug.WriteLine($"[AutoSave] Document '{doc.Name}' not due yet (elapsed: {elapsed.TotalMinutes:F1}min, needs: {IntervalMinutes}min)");
                        }
                    }
                    else
                    {
                        // No last save time recorded, save now
                        Debug.WriteLine($"[AutoSave] Document '{doc.Name}' has no last save time, saving now");
                        docsToSave.Add(doc);
                    }
                }
            }

            // Save outside of lock to avoid blocking registrations
            foreach (var doc in docsToSave)
            {
                SaveDocument(doc);
            }
        }

        /// <summary>
        /// Saves a single document to the auto-save location with timestamp.
        /// </summary>
        private void SaveDocument(CanvasDocument doc)
        {
            try
            {
                // Ensure save directory exists
                if (!Directory.Exists(SaveFolderPath))
                {
                    Directory.CreateDirectory(SaveFolderPath);
                    Debug.WriteLine($"[AutoSave] Created directory: {SaveFolderPath}");
                }

                // Generate timestamped filename: CanvasName_yyyy-MM-dd-HH-mm-ss.pxp
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                var safeName = SanitizeFileName(doc.Name ?? "Untitled");
                var fileName = $"{safeName}_{timestamp}.pxp";
                var filePath = Path.Combine(SaveFolderPath, fileName);

                Debug.WriteLine($"[AutoSave] Saving '{doc.Name}' to: {filePath}");

                // Save using DocumentIO
                DocumentIO.Save(doc, filePath);

                // Update last save time
                lock (_lock)
                {
                    _lastSaveTimes[doc] = DateTime.Now;
                }

                Debug.WriteLine($"[AutoSave] Successfully saved: {fileName}");
                AutoSaveCompleted?.Invoke(doc, true, filePath);

                // Prune old backups for this document
                PruneOldBackups(safeName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoSave] FAILED to save '{doc.Name}': {ex.Message}");
                Debug.WriteLine($"[AutoSave] Exception: {ex}");
                AutoSaveCompleted?.Invoke(doc, false, ex.Message);
            }
        }

        /// <summary>
        /// Sanitizes a filename by removing invalid characters.
        /// </summary>
        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Untitled";

            var invalid = Path.GetInvalidFileNameChars();
            var result = input;
            foreach (var c in invalid)
            {
                result = result.Replace(c, '_');
            }
            return result.Trim();
        }

        /// <summary>
        /// Deletes old backups for a document, keeping only the most recent MaxBackupCount files.
        /// </summary>
        /// <param name="sanitizedDocName">The sanitized document name (without timestamp).</param>
        private void PruneOldBackups(string sanitizedDocName)
        {
            try
            {
                if (!Directory.Exists(SaveFolderPath)) return;
                if (MaxBackupCount <= 0) return;

                // Find all backup files for this document
                // Pattern: {sanitizedDocName}_*.pxp
                var searchPattern = $"{sanitizedDocName}_*.pxp";
                var backupFiles = Directory.GetFiles(SaveFolderPath, searchPattern)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                Debug.WriteLine($"[AutoSave] Found {backupFiles.Count} backups for '{sanitizedDocName}', keeping {MaxBackupCount}");

                // Delete files beyond the retention limit
                if (backupFiles.Count > MaxBackupCount)
                {
                    var filesToDelete = backupFiles.Skip(MaxBackupCount).ToList();
                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            file.Delete();
                            Debug.WriteLine($"[AutoSave] Deleted old backup: {file.Name}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[AutoSave] Failed to delete old backup '{file.Name}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoSave] Failed to prune old backups: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the most recent auto-save file path for a document, if one exists.
        /// </summary>
        /// <param name="doc">The document to find the auto-save for.</param>
        /// <returns>The path to the most recent auto-save file, or null if none exists.</returns>
        public string? GetAutoSavePath(CanvasDocument doc)
        {
            try
            {
                if (doc == null || !Directory.Exists(SaveFolderPath))
                    return null;

                var safeName = SanitizeFileName(doc.Name ?? "Untitled");
                var searchPattern = $"{safeName}_*.pxp";
                var backupFiles = Directory.GetFiles(SaveFolderPath, searchPattern)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();

                return backupFiles?.FullName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Disposes the auto-save service and stops the timer.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
                _documents.Clear();
                _lastSaveTimes.Clear();
            }

            Debug.WriteLine("[AutoSave] Disposed");
        }
    }
}
