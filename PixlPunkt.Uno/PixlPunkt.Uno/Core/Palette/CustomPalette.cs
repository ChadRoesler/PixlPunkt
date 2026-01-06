using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixlPunkt.Uno.Core.Serialization;
using PixlPunkt.Uno.Core.Settings;

namespace PixlPunkt.Uno.Core.Palette
{
    /// <summary>
    /// Represents a custom user-defined color palette that can be saved/loaded from JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Custom palettes are stored as JSON files in %LocalAppData%\PixlPunkt\Palettes\
    /// Each palette contains a name, description, and ordered list of colors in RRGGBB hex format.
    /// </para>
    /// </remarks>
    public sealed class CustomPalette
    {
        /// <summary>
        /// Gets or sets the palette name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Custom Palette";

        /// <summary>
        /// Gets or sets the palette description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the list of colors in RRGGBB hex format (without # prefix).
        /// </summary>
        [JsonPropertyName("colors")]
        public List<string> Colors { get; set; } = [];

        /// <summary>
        /// Gets a display string showing color count.
        /// </summary>
        [JsonIgnore]
        public string ColorCountDisplay => $"{Colors.Count} color{(Colors.Count == 1 ? "" : "s")}";

        /// <summary>
        /// Gets a display string for the palette info.
        /// </summary>
        [JsonIgnore]
        public string InfoDisplay => string.IsNullOrWhiteSpace(Description)
            ? ColorCountDisplay
            : $"{ColorCountDisplay} - {Description}";

        /// <summary>
        /// Creates an empty custom palette.
        /// </summary>
        public CustomPalette() { }

        /// <summary>
        /// Creates a custom palette with the specified name and colors.
        /// </summary>
        public CustomPalette(string name, string description, IEnumerable<uint> bgraColors)
        {
            Name = name;
            Description = description;
            Colors = [];
            foreach (var bgra in bgraColors)
            {
                Colors.Add(BgraToHex(bgra));
            }
        }

        /// <summary>
        /// Converts BGRA packed color to RRGGBB hex string.
        /// </summary>
        private static string BgraToHex(uint bgra)
        {
            byte b = (byte)(bgra & 0xFF);
            byte g = (byte)((bgra >> 8) & 0xFF);
            byte r = (byte)((bgra >> 16) & 0xFF);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>
        /// Converts RRGGBB hex string to BGRA packed color.
        /// </summary>
        private static uint HexToBgra(string hex)
        {
            hex = hex.Trim().TrimStart('#');
            if (hex.Length != 6)
                throw new FormatException($"Invalid hex color: {hex}");

            byte r = byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            return (uint)(b | (g << 8) | (r << 16) | (255 << 24));
        }

        /// <summary>
        /// Gets the colors as BGRA packed values.
        /// </summary>
        public uint[] GetBgraColors()
        {
            var result = new uint[Colors.Count];
            for (int i = 0; i < Colors.Count; i++)
            {
                try
                {
                    result[i] = HexToBgra(Colors[i]);
                }
                catch
                {
                    result[i] = 0xFF000000; // Default to black on parse error
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the sanitized filename for this palette.
        /// </summary>
        public string GetFileName()
        {
            var safeName = Name;
            foreach (var c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            safeName = safeName.Replace(" ", "_");
            return $"{safeName}.json";
        }
    }

    /// <summary>
    /// Handles reading and writing custom palette JSON files.
    /// </summary>
    public static class CustomPaletteIO
    {
        /// <summary>
        /// Gets the custom palettes directory path.
        /// </summary>
        public static string GetPalettesDirectory() => AppPaths.PalettesDirectory;

        /// <summary>
        /// Ensures the palettes directory exists.
        /// </summary>
        public static void EnsureDirectoryExists() => AppPaths.EnsureDirectoryExists(GetPalettesDirectory());

        /// <summary>
        /// Saves a custom palette to a JSON file.
        /// </summary>
        public static string Save(CustomPalette palette)
        {
            EnsureDirectoryExists();
            var path = Path.Combine(GetPalettesDirectory(), palette.GetFileName());
            var json = JsonSerializer.Serialize(palette, CustomPaletteJsonContext.Default.CustomPalette);
            File.WriteAllText(path, json);
            return path;
        }

        /// <summary>
        /// Loads a custom palette from a JSON file.
        /// </summary>
        public static CustomPalette? Load(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize(json, CustomPaletteJsonContext.Default.CustomPalette);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Enumerates all custom palette files.
        /// </summary>
        public static IReadOnlyList<string> EnumeratePaletteFiles()
        {
            var dir = GetPalettesDirectory();
            if (!Directory.Exists(dir))
                return [];

            return Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Deletes a custom palette file.
        /// </summary>
        public static bool Delete(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
