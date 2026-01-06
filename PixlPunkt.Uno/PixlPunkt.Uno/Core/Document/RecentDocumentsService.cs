using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PixlPunkt.Uno.Core.Serialization;
using PixlPunkt.Uno.Core.Settings;

namespace PixlPunkt.Uno.Core.Document
{
    /// <summary>
    /// Simple MRU list for recently opened documents.
    /// Stored under %LocalAppData%\PixlPunkt\recent.json.
    /// </summary>
    public sealed class RecentDocumentsService
    {
        private readonly List<RecentDocumentEntry> _entries = new();

        public int MaxEntries { get; set; } = 12;

        /// <summary>Where the MRU list is persisted.</summary>
        public string StoragePath { get; }

        public IReadOnlyList<RecentDocumentEntry> Entries => _entries;

        public RecentDocumentsService(string? storagePath = null)
        {
            StoragePath = storagePath ?? Path.Combine(AppPaths.RootDirectory, "recent.json");
        }

        public void Load()
        {
            _entries.Clear();

            try
            {
                if (!File.Exists(StoragePath))
                    return;

                var json = File.ReadAllText(StoragePath);
                var data = JsonSerializer.Deserialize(json, RecentDocumentsJsonContext.Default.ListRecentDocumentEntry);
                if (data != null)
                    _entries.AddRange(data.Where(e => !string.IsNullOrWhiteSpace(e.FilePath)));

                // De-dupe + sort most-recent-first
                Normalize();
            }
            catch
            {
                // Best-effort: ignore corrupt MRU
                _entries.Clear();
            }
        }

        public void Save()
        {
            try
            {
                AppPaths.EnsureDirectoryExists(AppPaths.RootDirectory);
                Normalize();

                var json = JsonSerializer.Serialize(_entries, RecentDocumentsJsonContext.Default.ListRecentDocumentEntry);
                File.WriteAllText(StoragePath, json);
            }
            catch
            {
                // Best-effort
            }
        }

        public void Touch(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            filePath = Path.GetFullPath(filePath);

            var now = DateTime.UtcNow;
            var existing = _entries.FirstOrDefault(e => string.Equals(e.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.LastOpenedUtc = now;
            }
            else
            {
                _entries.Add(new RecentDocumentEntry
                {
                    FilePath = filePath,
                    LastOpenedUtc = now
                });
            }

            Normalize();
        }

        public void Remove(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            _entries.RemoveAll(e => string.Equals(e.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        public void PruneMissingFiles()
        {
            _entries.RemoveAll(e => string.IsNullOrWhiteSpace(e.FilePath) || !File.Exists(e.FilePath));
            Normalize();
        }

        private void Normalize()
        {
            // De-dupe by path (keep newest)
            var grouped = _entries
                .Where(e => !string.IsNullOrWhiteSpace(e.FilePath))
                .GroupBy(e => e.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.LastOpenedUtc).First())
                .OrderByDescending(e => e.LastOpenedUtc)
                .Take(Math.Max(1, MaxEntries))
                .ToList();

            _entries.Clear();
            _entries.AddRange(grouped);
        }
    }

    public sealed class RecentDocumentEntry
    {
        public string FilePath { get; set; } = string.Empty;
        public DateTime LastOpenedUtc { get; set; }
    }
}
