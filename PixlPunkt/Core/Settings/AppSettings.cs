using System;
using System.IO;
using System.Text.Json;
using PixlPunkt.Core.Serialization;

namespace PixlPunkt.Core.Settings
{
    /// <summary>
    /// Theme choice for the application stripe/accent.
    /// </summary>
    public enum StripeThemeChoice { System = 0, Light = 1, Dark = 2 }

    /// <summary>
    /// Theme choice for the overall application UI.
    /// </summary>
    public enum AppThemeChoice { System = 0, Light = 1, Dark = 2 }

    /// <summary>
    /// Application-wide settings persisted to JSON file.
    /// </summary>
    public sealed class AppSettings
    {
        private static AppSettings? _instance;

        /// <summary>
        /// Gets the singleton settings instance.
        /// </summary>
        public static AppSettings Instance => _instance ??= Load();

        /// <summary>
        /// Gets the path to the settings JSON file.
        /// </summary>
        public static string SettingsFilePath => AppPaths.SettingsFilePath;

        /// <summary>
        /// The default palette name used when no custom palette is selected or the selected palette is unavailable.
        /// </summary>
        public const string FallbackPaletteName = "PixlPunkt Default";

        // ====================================================================
        // SETTINGS PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets or sets the default storage folder path for documents.
        /// </summary>
        public string StorageFolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the auto-backup interval in minutes (minimum 4).
        /// </summary>
        public int AutoBackupMinutes { get; set; } = 10;

        /// <summary>
        /// Gets or sets the maximum number of backup files to keep per document.
        /// </summary>
        public int MaxBackupCount { get; set; } = 10;

        /// <summary>
        /// Gets or sets the overall application theme choice (affects all UI).
        /// </summary>
        public AppThemeChoice AppTheme { get; set; } = AppThemeChoice.System;

        /// <summary>
        /// Gets or sets the stripe/accent theme choice (transparency pattern only).
        /// </summary>
        public StripeThemeChoice StripeTheme { get; set; } = StripeThemeChoice.System;

        /// <summary>
        /// Gets or sets the palette swatch size in pixels.
        /// </summary>
        public int PaletteSwatchSize { get; set; } = 16;

        /// <summary>
        /// Gets or sets the name of the default palette to load on startup.
        /// </summary>
        /// <remarks>
        /// Can be a built-in palette name (e.g., "PixlPunkt Default", "Game Boy (DMG)") 
        /// or a custom palette name. If the palette is not found, falls back to "PixlPunkt Default".
        /// </remarks>
        public string DefaultPalette { get; set; } = FallbackPaletteName;

        /// <summary>
        /// Gets or sets the tile swatch size in pixels for the tile panel.
        /// </summary>
        public int TileSwatchSize { get; set; } = 48;

        /// <summary>
        /// Gets or sets the path to a default tile set file (.pxpt) to load for new documents.
        /// </summary>
        /// <remarks>
        /// If the file doesn't exist or fails to load, a warning is shown and no tile set is loaded.
        /// Empty string means no default tile set.
        /// </remarks>
        public string DefaultTileSetPath { get; set; } = string.Empty;

        /// <summary>
        /// Minimum log level for application logging (Serilog level name).
        /// </summary>
        public string? LogLevel { get; set; } = "Information";

        // ====================================================================
        // LOAD / SAVE
        // ====================================================================

        /// <summary>
        /// Loads settings from the JSON file, or returns defaults if not found.
        /// </summary>
        /// <returns>The loaded or default settings instance.</returns>
        public static AppSettings Load()
        {
            try
            {
                var path = SettingsFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
                    if (loaded != null)
                    {
                        // Validate/clamp values
                        loaded.AutoBackupMinutes = Math.Max(4, loaded.AutoBackupMinutes);
                        loaded.MaxBackupCount = Math.Max(1, loaded.MaxBackupCount);
                        loaded.PaletteSwatchSize = Math.Clamp(loaded.PaletteSwatchSize, 8, 64);
                        loaded.TileSwatchSize = Math.Clamp(loaded.TileSwatchSize, 16, 128);
                        if (string.IsNullOrEmpty(loaded.LogLevel)) loaded.LogLevel = "Information";
                        if (string.IsNullOrEmpty(loaded.DefaultPalette)) loaded.DefaultPalette = FallbackPaletteName;
                        return loaded;
                    }
                }
            }
            catch
            {
                // Fall through to defaults
            }

            // Return defaults and save them
            var settings = new AppSettings();
            settings.Save();
            return settings;
        }

        /// <summary>
        /// Saves current settings to the JSON file.
        /// </summary>
        public void Save()
        {
            try
            {
                var path = SettingsFilePath;
                AppPaths.EnsureDirectoryExists(AppPaths.RootDirectory);

                var json = JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings);
                File.WriteAllText(path, json);
            }
            catch
            {
                // Best-effort save - don't crash the app
            }
        }

        /// <summary>
        /// Reloads settings from disk, updating the singleton instance.
        /// </summary>
        public static void Reload()
        {
            _instance = null;
            _ = Instance; // Force reload
        }
    }
}
