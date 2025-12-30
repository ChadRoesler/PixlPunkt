using System;
using System.Collections.Generic;
using System.Text.Json;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Palette.Helpers;
using PixlPunkt.Core.Palette.Helpers.Defaults;
using PixlPunkt.Core.Serialization;
using PixlPunkt.Core.Settings;

namespace PixlPunkt.Core.Palette
{
    /// <summary>
    /// Manages the active color palette, foreground/background colors, and palette import/export.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PaletteService is the central management point for the application's color palette. It maintains:
    /// - A list of palette colors (packed BGRA uint values)
    /// - The current foreground (primary) color
    /// - The current background (secondary) color
    /// - Events for UI synchronization when any of these change
    /// </para>
    /// <para>
    /// The palette supports full CRUD operations (Add, Insert, Remove, Update, Move) with automatic
    /// change notifications. All colors are stored in BGRA format (0xAABBGGRR) for consistency with
    /// the rest of the imaging pipeline.
    /// </para>
    /// <para>
    /// Typical usage:
    /// 1. Create service with seed colors or use default palette
    /// 2. Subscribe to events for UI binding
    /// 3. Use SetForeground/SetBackground to change active colors
    /// 4. Use Add/Remove/Update methods to modify palette
    /// 5. Export/import palettes as JSON for sharing and persistence
    /// </para>
    /// </remarks>
    public sealed class PaletteService
    {
        /// <summary>
        /// Gets the read-only list of palette colors.
        /// </summary>
        /// <value>
        /// A list of packed BGRA values (0xAABBGGRR). Modifications must be done through
        /// the service methods to ensure proper event notification.
        /// </value>
        public IReadOnlyList<uint> Colors => _colors;

        /// <summary>
        /// Gets the current foreground (primary) color.
        /// </summary>
        /// <value>
        /// Packed BGRA value (0xAABBGGRR). Default is opaque black (0xFF000000).
        /// Set via <see cref="SetForeground"/>.
        /// </value>
        public uint Foreground { get; private set; } = 0xFF000000;

        /// <summary>
        /// Gets the current background (secondary) color.
        /// </summary>
        /// <value>
        /// Packed BGRA value (0xAABBGGRR). Default is opaque white (0xFFFFFFFF).
        /// Set via <see cref="SetBackground"/>.
        /// </value>
        public uint Background { get; private set; } = 0xFFFFFFFF;

        /// <summary>
        /// Occurs when the palette colors list changes (add, remove, update, move, or import).
        /// </summary>
        /// <remarks>
        /// UI controls displaying the palette should subscribe to this event to refresh their display.
        /// </remarks>
        public event Action? PaletteChanged;

        /// <summary>
        /// Occurs when the foreground color changes.
        /// </summary>
        /// <remarks>
        /// Subscribers receive the new foreground color. This allows tools and UI to update
        /// immediately when the user selects a new primary color.
        /// </remarks>
        public event Action<uint>? ForegroundChanged;

        /// <summary>
        /// Occurs when the background color changes.
        /// </summary>
        /// <remarks>
        /// Subscribers receive the new background color. This is typically used for secondary
        /// operations like fill or gradient endpoints.
        /// </remarks>
        public event Action<uint>? BackgroundChanged;

        private readonly List<uint> _colors = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteService"/> class with optional seed colors.
        /// </summary>
        /// <param name="seed">
        /// Optional initial colors to populate the palette. If null or empty, the configured
        /// default palette (from settings) or the PixlPunkt default palette is loaded.
        /// </param>
        public PaletteService(IEnumerable<uint>? seed = null)
        {
            if (seed is not null)
                _colors.AddRange(seed);

            if (_colors.Count == 0)
                LoadConfiguredDefaultPalette();
        }

        /// <summary>
        /// Loads the configured default palette from settings.
        /// Falls back to PixlPunkt Default if the configured palette is unavailable.
        /// </summary>
        /// <returns>True if the configured palette was loaded, false if fallback was used.</returns>
        public bool LoadConfiguredDefaultPalette()
        {
            var paletteName = AppSettings.Instance.DefaultPalette;
            if (string.IsNullOrEmpty(paletteName))
                paletteName = AppSettings.FallbackPaletteName;

            return LoadPaletteByName(paletteName);
        }

        /// <summary>
        /// Loads a palette by name, checking both built-in and custom palettes.
        /// Falls back to PixlPunkt Default if not found.
        /// Applies the configured default sort mode after loading.
        /// </summary>
        /// <param name="paletteName">Name of the palette to load.</param>
        /// <returns>True if the named palette was loaded, false if fallback was used.</returns>
        public bool LoadPaletteByName(string paletteName)
        {
            _colors.Clear();

            bool found = false;

            // Try built-in palettes first
            if (DefaultPalettes.ByName.TryGetValue(paletteName, out var builtIn))
            {
                _colors.AddRange(builtIn.Colors);
                LoggingService.Debug("Loaded built-in palette: {PaletteName}", paletteName);
                found = true;
            }
            else
            {
                // Try custom palettes
                CustomPaletteService.Instance.Initialize();
                var customPalette = CustomPaletteService.Instance.GetPalette(paletteName);
                if (customPalette != null)
                {
                    var colors = customPalette.GetBgraColors();
                    _colors.AddRange(colors);
                    LoggingService.Debug("Loaded custom palette: {PaletteName}", paletteName);
                    found = true;
                }
            }

            if (!found)
            {
                // Fallback to default
                LoggingService.Warning("Palette '{PaletteName}' not found, falling back to {FallbackName}",
                    paletteName, AppSettings.FallbackPaletteName);

                if (DefaultPalettes.ByName.TryGetValue(AppSettings.FallbackPaletteName, out var fallback))
                {
                    _colors.AddRange(fallback.Colors);
                }
                else
                {
                    // Ultimate fallback - hardcoded colors
                    _colors.Add(0xFF000000); // Black
                    _colors.Add(0xFFFFFFFF); // White
                }
            }

            // Apply default sort mode if configured
            ApplyDefaultSort();

            PaletteChanged?.Invoke();
            return found;
        }

        /// <summary>
        /// Applies the default sort mode from settings to the current palette.
        /// </summary>
        private void ApplyDefaultSort()
        {
            var sortMode = AppSettings.Instance.DefaultPaletteSortMode;
            if (sortMode != PaletteSortMode.Default && _colors.Count > 1)
            {
                var sorted = PaletteSorter.Sort(_colors, sortMode);
                _colors.Clear();
                _colors.AddRange(sorted);
                LoggingService.Debug("Applied default sort mode: {SortMode}", sortMode);
            }
        }

        /// <summary>
        /// Sorts the current palette colors using the specified sort mode.
        /// </summary>
        /// <param name="mode">The sorting algorithm to apply.</param>
        public void SortPalette(PaletteSortMode mode)
        {
            if (_colors.Count <= 1)
                return;

            var sorted = PaletteSorter.Sort(_colors, mode);
            _colors.Clear();
            _colors.AddRange(sorted);
            PaletteChanged?.Invoke();
        }

        /// <summary>
        /// Replaces the current palette with the default PixlPunkt palette.
        /// </summary>
        /// <remarks>
        /// Loads the "PixlPunkt Default" palette from <see cref="DefaultPalettes.ByName"/>
        /// and raises <see cref="PaletteChanged"/>.
        /// </remarks>
        public void SetDefault()
        {
            _colors.Clear();
            var defaultPalette = DefaultPalettes.ByName[AppSettings.FallbackPaletteName];
            _colors.AddRange(defaultPalette.Colors);
            PaletteChanged?.Invoke();
        }

        /// <summary>
        /// Alias for <see cref="SetDefault"/>. Resets palette to default colors.
        /// </summary>
        public void ResetToDefault() => SetDefault();

        /// <summary>
        /// Appends a color to the end of the palette.
        /// </summary>
        /// <param name="bgra">The color to add (packed BGRA, 0xAABBGGRR).</param>
        /// <remarks>
        /// Fires <see cref="PaletteChanged"/> after adding. Duplicates are allowed.
        /// </remarks>
        public void AddColor(uint bgra)
        {
            _colors.Add(bgra);
            PaletteChanged?.Invoke();
        }

        /// <summary>
        /// Inserts a color at the specified index.
        /// </summary>
        /// <param name="index">
        /// The zero-based index to insert at. Automatically clamped to [0, Count].
        /// </param>
        /// <param name="bgra">The color to insert (packed BGRA, 0xAABBGGRR).</param>
        /// <remarks>
        /// Fires <see cref="PaletteChanged"/> after insertion.
        /// </remarks>
        public void InsertColor(int index, uint bgra)
        {
            index = Math.Clamp(index, 0, _colors.Count);
            _colors.Insert(index, bgra);
            PaletteChanged?.Invoke();
        }

        /// <summary>
        /// Removes the color at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the color to remove.</param>
        /// <remarks>
        /// No-op if index is out of range. Fires <see cref="PaletteChanged"/> after removal.
        /// </remarks>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _colors.Count) return;
            _colors.RemoveAt(index);
            PaletteChanged?.Invoke();
        }

        /// <summary>
        /// Updates the color at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the color to update.</param>
        /// <param name="bgra">The new color value (packed BGRA, 0xAABBGGRR).</param>
        /// <remarks>
        /// No-op if index is out of range or the color is unchanged. Fires <see cref="PaletteChanged"/> after update.
        /// </remarks>
        public void UpdateAt(int index, uint bgra)
        {
            if (index < 0 || index >= _colors.Count) return;
            if (_colors[index] == bgra) return;
            _colors[index] = bgra;
            PaletteChanged?.Invoke();
        }

        /// <summary>
        /// Moves a color from one index to another.
        /// </summary>
        /// <param name="from">The source index (0-based).</param>
        /// <param name="to">The destination index (0-based, automatically clamped).</param>
        /// <remarks>
        /// <para>
        /// No-op if from == to, from is out of range, or list has fewer than 2 colors.
        /// The color at <paramref name="from"/> is removed and re-inserted at <paramref name="to"/>.
        /// </para>
        /// <para>
        /// Fires <see cref="PaletteChanged"/> after moving.
        /// </para>
        /// </remarks>
        public void Move(int from, int to)
        {
            if (from == to || from < 0 || from >= _colors.Count) return;
            to = Math.Clamp(to, 0, _colors.Count - 1);

            var c = _colors[from];
            _colors.RemoveAt(from);
            _colors.Insert(to, c);
            PaletteChanged?.Invoke();
        }

        /// <summary>
        /// Sets the foreground (primary) color.
        /// </summary>
        /// <param name="bgra">The new foreground color (packed BGRA, 0xAABBGGRR).</param>
        /// <remarks>
        /// No-op if the color is unchanged. Fires <see cref="ForegroundChanged"/> with the new value.
        /// This is typically the color used for drawing operations.
        /// </remarks>
        public void SetForeground(uint bgra)
        {
            if (Foreground == bgra) return;
            Foreground = bgra;
            ForegroundChanged?.Invoke(bgra);
        }

        /// <summary>
        /// Sets the background (secondary) color.
        /// </summary>
        /// <param name="bgra">The new background color (packed BGRA, 0xAABBGGRR).</param>
        /// <remarks>
        /// No-op if the color is unchanged. Fires <see cref="BackgroundChanged"/> with the new value.
        /// This is typically used for secondary operations like erasing or gradient endpoints.
        /// </remarks>
        public void SetBackground(uint bgra)
        {
            if (Background == bgra) return;
            Background = bgra;
            BackgroundChanged?.Invoke(bgra);
        }

        /// <summary>
        /// Swaps the foreground and background colors.
        /// </summary>
        /// <remarks>
        /// A common operation in painting applications (often bound to the X key).
        /// Fires both <see cref="ForegroundChanged"/> and <see cref="BackgroundChanged"/>.
        /// </remarks>
        public void Swap()
        {
            (Foreground, Background) = (Background, Foreground);
            ForegroundChanged?.Invoke(Foreground);
            BackgroundChanged?.Invoke(Background);
        }

        /// <summary>
        /// Exports the palette as a JSON string (array of uint values).
        /// </summary>
        /// <returns>A JSON string representing the palette colors.</returns>
        /// <remarks>
        /// The format is a simple JSON array: [0xFF000000, 0xFFFFFFFF, ...].
        /// Use <see cref="ImportJson"/> to load exported palettes.
        /// </remarks>
        public string ExportJson() => JsonSerializer.Serialize(_colors, PaletteColorsJsonContext.Default.ListUInt32);

        /// <summary>
        /// Imports a palette from a JSON string, replacing the current palette.
        /// </summary>
        /// <param name="json">
        /// A JSON string containing an array of uint values (packed BGRA).
        /// </param>
        /// <remarks>
        /// <para>
        /// Expected format: [0xFF000000, 0xFFFFFFFF, ...].
        /// If deserialization fails, the current palette is unchanged.
        /// </para>
        /// <para>
        /// Fires <see cref="PaletteChanged"/> if import succeeds.
        /// </para>
        /// </remarks>
        public void ImportJson(string json)
        {
            var arr = JsonSerializer.Deserialize(json, PaletteColorsJsonContext.Default.UInt32Array);
            if (arr is null) return;

            _colors.Clear();
            _colors.AddRange(arr);
            PaletteChanged?.Invoke();
        }
    }
}