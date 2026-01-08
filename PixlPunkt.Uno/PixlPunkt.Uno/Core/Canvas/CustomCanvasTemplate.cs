using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixlPunkt.Uno.Core.Serialization;
using PixlPunkt.Uno.Core.Settings;

namespace PixlPunkt.Uno.Core.Canvas
{
    /// <summary>
    /// Represents a custom user-defined canvas template that can be saved/loaded from JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Custom templates are stored as JSON files in %AppData%\PixlPunkt\Templates\
    /// Each template contains a name, tile dimensions, and canvas tile counts.
    /// </para>
    /// </remarks>
    public sealed class CustomCanvasTemplate
    {
        /// <summary>
        /// Gets or sets the template name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Custom Template";

        /// <summary>
        /// Gets or sets the tile width in pixels.
        /// </summary>
        [JsonPropertyName("tileWidth")]
        public int TileWidth { get; set; } = 32;

        /// <summary>
        /// Gets or sets the tile height in pixels.
        /// </summary>
        [JsonPropertyName("tileHeight")]
        public int TileHeight { get; set; } = 32;

        /// <summary>
        /// Gets or sets the number of tiles horizontally.
        /// </summary>
        [JsonPropertyName("tileCountX")]
        public int TileCountX { get; set; } = 1;

        /// <summary>
        /// Gets or sets the number of tiles vertically.
        /// </summary>
        [JsonPropertyName("tileCountY")]
        public int TileCountY { get; set; } = 1;

        /// <summary>
        /// Creates an empty custom template.
        /// </summary>
        public CustomCanvasTemplate() { }

        /// <summary>
        /// Creates a custom template with specified parameters.
        /// </summary>
        public CustomCanvasTemplate(string name, int tileWidth, int tileHeight, int tileCountX, int tileCountY)
        {
            Name = name;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileCountX = tileCountX;
            TileCountY = tileCountY;
        }

        /// <summary>
        /// Converts to a CanvasTemplate for use in dialogs.
        /// </summary>
        public CanvasTemplate ToCanvasTemplate()
        {
            return new CanvasTemplate(Name, TileWidth, TileHeight, TileCountX, TileCountY, isBuiltIn: false);
        }

        /// <summary>
        /// Creates a CustomCanvasTemplate from a CanvasTemplate.
        /// </summary>
        public static CustomCanvasTemplate FromCanvasTemplate(CanvasTemplate template)
        {
            return new CustomCanvasTemplate(
                template.Name,
                template.TileWidth,
                template.TileHeight,
                template.TileCountX,
                template.TileCountY
            );
        }

        /// <summary>
        /// Gets the total canvas width in pixels.
        /// </summary>
        [JsonIgnore]
        public int PixelWidth => TileWidth * TileCountX;

        /// <summary>
        /// Gets the total canvas height in pixels.
        /// </summary>
        [JsonIgnore]
        public int PixelHeight => TileHeight * TileCountY;

        /// <summary>
        /// Gets a display string for the template dimensions.
        /// </summary>
        [JsonIgnore]
        public string DimensionsDisplay => $"{PixelWidth}×{PixelHeight} ({TileWidth}×{TileHeight} : {TileCountX}×{TileCountY})";

        /// <summary>
        /// Gets the sanitized filename for this template.
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
    /// Handles reading and writing custom template JSON files.
    /// </summary>
    public static class CustomTemplateIO
    {
        /// <summary>
        /// Gets the custom templates directory path.
        /// </summary>
        public static string GetTemplatesDirectory() => AppPaths.TemplatesDirectory;

        /// <summary>
        /// Ensures the templates directory exists.
        /// </summary>
        public static void EnsureDirectoryExists() => AppPaths.EnsureDirectoryExists(GetTemplatesDirectory());

        /// <summary>
        /// Saves a custom template to a JSON file.
        /// </summary>
        public static string Save(CustomCanvasTemplate template)
        {
            EnsureDirectoryExists();
            var path = Path.Combine(GetTemplatesDirectory(), template.GetFileName());
            var json = JsonSerializer.Serialize(template, CanvasTemplateJsonContext.Default.CustomCanvasTemplate);
            File.WriteAllText(path, json);
            return path;
        }

        /// <summary>
        /// Loads a custom template from a JSON file.
        /// </summary>
        public static CustomCanvasTemplate? Load(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize(json, CanvasTemplateJsonContext.Default.CustomCanvasTemplate);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Enumerates all custom template files.
        /// </summary>
        public static IReadOnlyList<string> EnumerateTemplateFiles()
        {
            var dir = GetTemplatesDirectory();
            if (!Directory.Exists(dir))
                return [];

            return Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Deletes a custom template file.
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
