using System;
using System.Collections.Generic;
using System.Linq;

namespace PixlPunkt.Core.Palette
{
    /// <summary>
    /// Service for managing custom user palettes stored in LocalAppData.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CustomPaletteService provides centralized access to custom palette files (.json format).
    /// It automatically scans the palette directory on initialization and caches loaded palettes
    /// for efficient runtime access.
    /// </para>
    /// <para><strong>Storage Location:</strong></para>
    /// <para>
    /// Palettes are stored in: <c>%LocalAppData%\PixlPunkt\Palettes\*.json</c>
    /// </para>
    /// </remarks>
    public sealed class CustomPaletteService
    {
        private static readonly Lazy<CustomPaletteService> _instance = new(() => new CustomPaletteService());

        /// <summary>
        /// Gets the singleton instance of the custom palette service.
        /// </summary>
        public static CustomPaletteService Instance => _instance.Value;

        private readonly List<CustomPalette> _palettes = [];
        private bool _isInitialized;

        /// <summary>
        /// Event raised when the palette collection changes.
        /// </summary>
        public event EventHandler? PalettesChanged;

        private CustomPaletteService() { }

        /// <summary>
        /// Gets whether the service has been initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets the list of loaded custom palettes.
        /// </summary>
        public IReadOnlyList<CustomPalette> Palettes
        {
            get
            {
                EnsureInitialized();
                return _palettes;
            }
        }

        /// <summary>
        /// Gets the count of loaded palettes.
        /// </summary>
        public int Count
        {
            get
            {
                EnsureInitialized();
                return _palettes.Count;
            }
        }

        /// <summary>
        /// Initializes the service by creating the directory and loading palettes.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            CustomPaletteIO.EnsureDirectoryExists();
            RefreshPalettes();
        }

        /// <summary>
        /// Ensures the service is initialized.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!_isInitialized)
                Initialize();
        }

        /// <summary>
        /// Refreshes the palette collection by rescanning the directory.
        /// </summary>
        public void RefreshPalettes()
        {
            _palettes.Clear();

            var files = CustomPaletteIO.EnumeratePaletteFiles();
            foreach (var file in files)
            {
                var palette = CustomPaletteIO.Load(file);
                if (palette != null && !string.IsNullOrEmpty(palette.Name))
                {
                    _palettes.Add(palette);
                }
            }

            // Sort by name
            _palettes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            _isInitialized = true;
            PalettesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets a palette by name (case-insensitive).
        /// </summary>
        public CustomPalette? GetPalette(string name)
        {
            EnsureInitialized();
            return _palettes.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Saves a new custom palette.
        /// </summary>
        /// <param name="name">Palette name.</param>
        /// <param name="description">Palette description.</param>
        /// <param name="colors">BGRA colors to save.</param>
        /// <returns>The saved palette.</returns>
        public CustomPalette SavePalette(string name, string description, IEnumerable<uint> colors)
        {
            var palette = new CustomPalette(name, description, colors);
            CustomPaletteIO.Save(palette);

            // Refresh to pick up the new palette
            RefreshPalettes();

            return palette;
        }

        /// <summary>
        /// Deletes a custom palette by name.
        /// </summary>
        public bool DeletePalette(string name)
        {
            var palette = GetPalette(name);
            if (palette == null)
                return false;

            var dir = CustomPaletteIO.GetPalettesDirectory();
            var path = System.IO.Path.Combine(dir, palette.GetFileName());

            if (CustomPaletteIO.Delete(path))
            {
                RefreshPalettes();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets all palette names.
        /// </summary>
        public IReadOnlyList<string> GetPaletteNames()
        {
            EnsureInitialized();
            return _palettes.Select(p => p.Name).ToList();
        }

        /// <summary>
        /// Checks if a palette with the given name exists.
        /// </summary>
        public bool HasPalette(string name)
        {
            return GetPalette(name) != null;
        }
    }
}
