using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixlPunkt.Core.Settings;

namespace PixlPunkt.Core.Canvas
{
    /// <summary>
    /// Represents an advanced document template that preserves full document state
    /// including layers, tiles, and tile mappings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Advanced templates are stored as .pxpt files (PixlPunkt Template) in %AppData%\PixlPunkt\Templates\Advanced\
    /// They use the same binary format as .pxp documents but are intended to be opened as new untitled documents.
    /// </para>
    /// <para>
    /// A companion .json metadata file stores display information like name, description, and preview thumbnail.
    /// </para>
    /// </remarks>
    public sealed class AdvancedTemplate
    {
        /// <summary>
        /// Gets or sets the template display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Untitled Template";

        /// <summary>
        /// Gets or sets an optional description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the author name.
        /// </summary>
        [JsonPropertyName("author")]
        public string? Author { get; set; }

        /// <summary>
        /// Gets or sets the creation date.
        /// </summary>
        [JsonPropertyName("created")]
        public DateTime Created { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the document width in pixels.
        /// </summary>
        [JsonPropertyName("pixelWidth")]
        public int PixelWidth { get; set; }

        /// <summary>
        /// Gets or sets the document height in pixels.
        /// </summary>
        [JsonPropertyName("pixelHeight")]
        public int PixelHeight { get; set; }

        /// <summary>
        /// Gets or sets the tile width.
        /// </summary>
        [JsonPropertyName("tileWidth")]
        public int TileWidth { get; set; }

        /// <summary>
        /// Gets or sets the tile height.
        /// </summary>
        [JsonPropertyName("tileHeight")]
        public int TileHeight { get; set; }

        /// <summary>
        /// Gets or sets the number of layers.
        /// </summary>
        [JsonPropertyName("layerCount")]
        public int LayerCount { get; set; }

        /// <summary>
        /// Gets or sets the number of tiles in the tile set.
        /// </summary>
        [JsonPropertyName("tileCount")]
        public int TileCount { get; set; }

        /// <summary>
        /// Gets or sets the base64-encoded PNG preview thumbnail (optional).
        /// </summary>
        [JsonPropertyName("previewBase64")]
        public string? PreviewBase64 { get; set; }

        /// <summary>
        /// Gets the path to the .pxpt template file (not serialized).
        /// </summary>
        [JsonIgnore]
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets a display string for the template dimensions.
        /// </summary>
        [JsonIgnore]
        public string DimensionsDisplay => $"{PixelWidth}×{PixelHeight}";

        /// <summary>
        /// Gets a summary string for the template.
        /// </summary>
        [JsonIgnore]
        public string Summary
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                parts.Add(DimensionsDisplay);
                if (LayerCount > 0)
                    parts.Add($"{LayerCount} layer{(LayerCount != 1 ? "s" : "")}");
                if (TileCount > 0)
                    parts.Add($"{TileCount} tile{(TileCount != 1 ? "s" : "")}");
                return string.Join(" • ", parts);
            }
        }

        /// <summary>
        /// Gets the sanitized base filename for this template (without extension).
        /// </summary>
        public string GetBaseFileName()
        {
            var safeName = Name;
            foreach (var c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            safeName = safeName.Replace(" ", "_");
            return safeName;
        }
    }
}
