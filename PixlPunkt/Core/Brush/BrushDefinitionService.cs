using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixlPunkt.Constants;

namespace PixlPunkt.Core.Brush
{
    /// <summary>
    /// Service for managing custom brush definitions loaded from the user's AppData directory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BrushDefinitionService provides centralized access to custom brush files (.mrk format).
    /// It automatically scans the brush directory on initialization and caches loaded templates
    /// for efficient runtime access.
    /// </para>
    /// <para><strong>Brush Identification:</strong></para>
    /// <para>
    /// Brushes are identified by their full name in "author.brushname" format. This prevents
    /// conflicts between brushes from different authors.
    /// </para>
    /// <para><strong>Usage:</strong></para>
    /// <code>
    /// // Initialize on app startup
    /// BrushDefinitionService.Instance.Initialize();
    /// 
    /// // Get available brush names for UI
    /// var names = BrushDefinitionService.Instance.GetBrushNames();
    /// 
    /// // Get a specific brush by full name
    /// var brush = BrushDefinitionService.Instance.GetBrush("MyAuthor.MyBrush");
    /// 
    /// // Refresh to load newly added brushes
    /// BrushDefinitionService.Instance.RefreshBrushes();
    /// </code>
    /// <para><strong>Storage Location:</strong></para>
    /// <para>
    /// Brushes are stored in: <c>%AppData%/PixlPunkt/Brushes/*.mrk</c>
    /// </para>
    /// </remarks>
    public sealed class BrushDefinitionService
    {
        private static readonly Lazy<BrushDefinitionService> _instance = new(() => new BrushDefinitionService());

        /// <summary>
        /// Gets the singleton instance of the brush definition service.
        /// </summary>
        public static BrushDefinitionService Instance => _instance.Value;

        // Keyed by FullName (author.brushname)
        private readonly Dictionary<string, BrushTemplate> _brushes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _brushFullNames = new();
        private readonly List<string> _brushDisplayNames = new();
        private bool _isInitialized;

        /// <summary>
        /// Event raised when the brush collection changes (brush added, removed, or refreshed).
        /// </summary>
        public event EventHandler? BrushesChanged;

        private BrushDefinitionService()
        {
            // Constructor doesn't auto-initialize - call Initialize() explicitly
        }

        /// <summary>
        /// Gets whether the service has been initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets the list of available brush full names (author.brushname).
        /// </summary>
        public IReadOnlyList<string> BrushFullNames
        {
            get
            {
                EnsureInitialized();
                return _brushFullNames;
            }
        }

        /// <summary>
        /// Gets the list of brush display names (brushname only, for UI).
        /// </summary>
        public IReadOnlyList<string> BrushDisplayNames
        {
            get
            {
                EnsureInitialized();
                return _brushDisplayNames;
            }
        }

        /// <summary>
        /// Gets the count of loaded brushes.
        /// </summary>
        public int Count
        {
            get
            {
                EnsureInitialized();
                return _brushes.Count;
            }
        }

        /// <summary>
        /// Initializes the brush service by creating the brush directory and loading brushes.
        /// Call this once during application startup.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            // Ensure brush directory exists
            var dir = BrushMarkIO.GetBrushDirectory();
            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch
                {
                    // Ignore directory creation failures
                }
            }

            // Load all brushes
            RefreshBrushes();
        }

        /// <summary>
        /// Ensures the service is initialized.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Refreshes the brush collection by rescanning the brush directory.
        /// Use this to pick up newly added brush files without restarting.
        /// </summary>
        public void RefreshBrushes()
        {
            _brushes.Clear();
            _brushFullNames.Clear();
            _brushDisplayNames.Clear();
            CustomBrushIcons.Instance.ClearCache();

            var files = BrushMarkIO.EnumerateBrushFiles();

            foreach (var file in files)
            {
                try
                {
                    var brush = BrushMarkIO.Load(file);
                    System.Diagnostics.Debug.WriteLine(file);
                    if (brush != null && !string.IsNullOrEmpty(brush.Name))
                    {
                        // Use full name (author.brushname) as key
                        var fullName = GetUniqueFullName(brush.FullName);

                        // Update brush if name changed
                        if (fullName != brush.FullName)
                        {
                            var (author, name) = BrushTemplate.ParseFullName(fullName);
                            brush.Author = author;
                            brush.Name = name;
                        }

                        _brushes[fullName] = brush;
                        _brushFullNames.Add(fullName);
                        _brushDisplayNames.Add(brush.DisplayName);

                        // Register icon
                        if (brush.IconData != null && brush.IconData.Length > 0)
                        {
                            CustomBrushIcons.Instance.RegisterIcon(fullName, brush.IconData);
                        }
                        else
                        {
                            // Generate outline icon if not stored
                            CustomBrushIcons.Instance.GetIcon(brush);
                        }
                    }
                }
                catch
                {
                    // Skip invalid brush files
                }
            }

            // Sort by display name for UI
            var sortedPairs = _brushFullNames
                .Select((fn, idx) => (fullName: fn, displayName: _brushDisplayNames[idx]))
                .OrderBy(p => p.displayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _brushFullNames.Clear();
            _brushDisplayNames.Clear();
            foreach (var (fullName, displayName) in sortedPairs)
            {
                _brushFullNames.Add(fullName);
                _brushDisplayNames.Add(displayName);
            }

            _isInitialized = true;

            BrushesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets a unique full name, adding a suffix if necessary.
        /// </summary>
        private string GetUniqueFullName(string baseName)
        {
            if (!_brushes.ContainsKey(baseName))
                return baseName;

            int suffix = 2;
            while (_brushes.ContainsKey($"{baseName}_{suffix}"))
                suffix++;

            return $"{baseName}_{suffix}";
        }

        /// <summary>
        /// Gets a brush template by full name (author.brushname).
        /// </summary>
        /// <param name="fullName">The full brush name (case-insensitive).</param>
        /// <returns>The brush template, or null if not found.</returns>
        public BrushTemplate? GetBrush(string fullName)
        {
            EnsureInitialized();
            return _brushes.TryGetValue(fullName, out var brush) ? brush : null;
        }

        /// <summary>
        /// Gets a brush template by author and name.
        /// </summary>
        /// <param name="author">The brush author.</param>
        /// <param name="name">The brush name.</param>
        /// <returns>The brush template, or null if not found.</returns>
        public BrushTemplate? GetBrush(string author, string name)
        {
            return GetBrush($"{author}.{name}");
        }

        /// <summary>
        /// Gets all brushes by a specific author.
        /// </summary>
        /// <param name="author">The author name (case-insensitive).</param>
        /// <returns>List of brushes by that author.</returns>
        public IReadOnlyList<BrushTemplate> GetBrushesByAuthor(string author)
        {
            EnsureInitialized();
            return _brushes.Values
                .Where(b => b.Author.Equals(author, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Gets all loaded brush templates.
        /// </summary>
        /// <returns>Enumerable of all brush templates.</returns>
        public IEnumerable<BrushTemplate> GetAllBrushes()
        {
            EnsureInitialized();
            return _brushes.Values;
        }

        /// <summary>
        /// Gets brush full names for display in UI.
        /// </summary>
        /// <returns>List of brush full names sorted by display name.</returns>
        public IReadOnlyList<string> GetBrushNames()
        {
            EnsureInitialized();
            return _brushFullNames;
        }

        /// <summary>
        /// Gets all unique author names.
        /// </summary>
        /// <returns>List of author names sorted alphabetically.</returns>
        public IReadOnlyList<string> GetAuthors()
        {
            EnsureInitialized();
            return _brushes.Values
                .Select(b => b.Author)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a)
                .ToList();
        }

        /// <summary>
        /// Gets the icon name for a brush (Brush_Author_BrushName format).
        /// </summary>
        /// <param name="fullName">The full brush name (author.brushname).</param>
        /// <returns>Icon name in format "Brush_Author_BrushName".</returns>
        public string GetIconName(string fullName)
        {
            var (author, name) = BrushTemplate.ParseFullName(fullName);
            return BrushExportConstants.GetIconName(author, name);
        }

        /// <summary>
        /// Gets the icon data for a brush.
        /// </summary>
        /// <param name="fullName">The full brush name (author.brushname).</param>
        /// <returns>32x32 BGRA icon data, or null if not found.</returns>
        public byte[]? GetBrushIcon(string fullName)
        {
            var brush = GetBrush(fullName);
            if (brush == null) return null;
            return CustomBrushIcons.Instance.GetIcon(brush);
        }

        /// <summary>
        /// Adds a new brush to the collection (does not save to file).
        /// </summary>
        /// <param name="brush">The brush template to add.</param>
        public void AddBrush(BrushTemplate brush)
        {
            if (brush == null || string.IsNullOrEmpty(brush.Name))
                return;

            var fullName = GetUniqueFullName(brush.FullName);

            // Update brush if name changed
            if (fullName != brush.FullName)
            {
                var (author, name) = BrushTemplate.ParseFullName(fullName);
                brush.Author = author;
                brush.Name = name;
            }

            _brushes[fullName] = brush;

            if (!_brushFullNames.Contains(fullName, StringComparer.OrdinalIgnoreCase))
            {
                _brushFullNames.Add(fullName);
                _brushDisplayNames.Add(brush.DisplayName);

                // Re-sort
                var sortedPairs = _brushFullNames
                    .Select((fn, idx) => (fullName: fn, displayName: _brushDisplayNames[idx]))
                    .OrderBy(p => p.displayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _brushFullNames.Clear();
                _brushDisplayNames.Clear();
                foreach (var (fn, displayName) in sortedPairs)
                {
                    _brushFullNames.Add(fn);
                    _brushDisplayNames.Add(displayName);
                }
            }

            // Register icon
            CustomBrushIcons.Instance.GetIcon(brush);

            BrushesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Removes a brush from the collection (does not delete file).
        /// </summary>
        /// <param name="fullName">The full brush name to remove.</param>
        /// <returns>True if removed, false if not found.</returns>
        public bool RemoveBrush(string fullName)
        {
            if (_brushes.Remove(fullName, out var brush))
            {
                var idx = _brushFullNames.FindIndex(fn => fn.Equals(fullName, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    _brushFullNames.RemoveAt(idx);
                    _brushDisplayNames.RemoveAt(idx);
                }
                BrushesChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Saves a brush to the brush directory.
        /// </summary>
        /// <param name="brush">The brush to save.</param>
        /// <returns>The full path where the brush was saved.</returns>
        public string SaveBrush(BrushTemplate brush)
        {
            if (brush == null || string.IsNullOrEmpty(brush.Name))
                throw new ArgumentException("Brush must have a valid name.");

            var dir = BrushMarkIO.GetBrushDirectory();
            var fileName = BrushExportConstants.GetFileName(brush.Author, brush.Name);
            var path = Path.Combine(dir, fileName);

            BrushMarkIO.Save(brush, path);

            // Add to collection if not already present
            if (!_brushes.ContainsKey(brush.FullName))
            {
                AddBrush(brush);
            }

            return path;
        }

        /// <summary>
        /// Deletes a brush file from disk.
        /// </summary>
        /// <param name="fullName">The full brush name to delete.</param>
        /// <returns>True if deleted, false if not found.</returns>
        public bool DeleteBrushFile(string fullName)
        {
            var brush = GetBrush(fullName);
            if (brush == null) return false;

            var dir = BrushMarkIO.GetBrushDirectory();
            var fileName = BrushExportConstants.GetFileName(brush.Author, brush.Name);
            var path = Path.Combine(dir, fileName);

            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    RemoveBrush(fullName);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a brush with the given full name exists.
        /// </summary>
        /// <param name="fullName">The full brush name to check.</param>
        /// <returns>True if brush exists, false otherwise.</returns>
        public bool HasBrush(string fullName)
        {
            EnsureInitialized();
            return _brushes.ContainsKey(fullName);
        }

        /// <summary>
        /// Checks if a brush with the given author and name exists.
        /// </summary>
        /// <param name="author">The brush author.</param>
        /// <param name="name">The brush name.</param>
        /// <returns>True if brush exists, false otherwise.</returns>
        public bool HasBrush(string author, string name)
        {
            return HasBrush($"{author}.{name}");
        }
    }
}
