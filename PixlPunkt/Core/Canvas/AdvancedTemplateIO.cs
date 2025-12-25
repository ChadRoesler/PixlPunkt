using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Serialization;
using PixlPunkt.Core.Settings;

namespace PixlPunkt.Core.Canvas
{
    /// <summary>
    /// Handles reading and writing advanced template files (.pxpt).
    /// </summary>
    public static class AdvancedTemplateIO
    {
        /// <summary>
        /// File extension for advanced template files.
        /// </summary>
        public const string TemplateExtension = ".pxpt";

        /// <summary>
        /// File extension for metadata files.
        /// </summary>
        public const string MetadataExtension = ".json";

        /// <summary>
        /// Gets the advanced templates directory path.
        /// </summary>
        public static string GetTemplatesDirectory()
        {
            return Path.Combine(AppPaths.TemplatesDirectory, "Advanced");
        }

        /// <summary>
        /// Ensures the advanced templates directory exists.
        /// </summary>
        public static void EnsureDirectoryExists()
        {
            var dir = GetTemplatesDirectory();
            AppPaths.EnsureDirectoryExists(dir);
        }

        /// <summary>
        /// Saves a document as an advanced template.
        /// </summary>
        public static string SaveAsTemplate(
            CanvasDocument doc,
            string templateName,
            string? description = null,
            string? author = null,
            byte[]? previewPngBytes = null)
        {
            EnsureDirectoryExists();

            // Create metadata
            var metadata = new AdvancedTemplate
            {
                Name = templateName,
                Description = description,
                Author = author,
                Created = DateTime.Now,
                PixelWidth = doc.PixelWidth,
                PixelHeight = doc.PixelHeight,
                TileWidth = doc.TileSize.Width,
                TileHeight = doc.TileSize.Height,
                LayerCount = doc.Layers.Count,
                TileCount = doc.TileSet?.Count ?? 0,
                PreviewBase64 = previewPngBytes != null ? Convert.ToBase64String(previewPngBytes) : null
            };

            var baseFileName = metadata.GetBaseFileName();
            var dir = GetTemplatesDirectory();

            // Ensure unique filename
            var templatePath = Path.Combine(dir, baseFileName + TemplateExtension);
            var metadataPath = Path.Combine(dir, baseFileName + MetadataExtension);
            int counter = 1;
            while (File.Exists(templatePath) || File.Exists(metadataPath))
            {
                var uniqueName = $"{baseFileName}_{counter}";
                templatePath = Path.Combine(dir, uniqueName + TemplateExtension);
                metadataPath = Path.Combine(dir, uniqueName + MetadataExtension);
                counter++;
            }

            // Save template using DocumentIO (same format as .pxp)
            DocumentIO.Save(doc, templatePath);

            // Save metadata using source-generated serializer
            var json = JsonSerializer.Serialize(metadata, CanvasTemplateJsonContext.Default.AdvancedTemplate);
            File.WriteAllText(metadataPath, json);

            LoggingService.Info("Saved advanced template: {TemplateName} -> {Path}", templateName, templatePath);

            return templatePath;
        }

        /// <summary>
        /// Loads an advanced template as a new untitled document.
        /// </summary>
        public static CanvasDocument LoadAsNewDocument(string templatePath)
        {
            // Load using DocumentIO
            var doc = DocumentIO.Load(templatePath);

            // Get the template name from metadata if available
            var metadataPath = Path.ChangeExtension(templatePath, MetadataExtension);
            string newName = "Untitled";

            if (File.Exists(metadataPath))
            {
                try
                {
                    var json = File.ReadAllText(metadataPath);
                    var metadata = JsonSerializer.Deserialize(json, CanvasTemplateJsonContext.Default.AdvancedTemplate);
                    if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Name))
                    {
                        newName = $"New {metadata.Name}";
                    }
                }
                catch
                {
                    // Ignore metadata errors
                }
            }

            // Set as new untitled document
            doc.Name = newName;

            // Clear history so it starts fresh
            doc.History.Clear();
            doc.MarkSaved();

            LoggingService.Info("Loaded template as new document: {Path} -> {Name}", templatePath, newName);

            return doc;
        }

        /// <summary>
        /// Loads template metadata from a .pxpt file.
        /// </summary>
        public static AdvancedTemplate? LoadMetadata(string templatePath)
        {
            var metadataPath = Path.ChangeExtension(templatePath, MetadataExtension);

            if (!File.Exists(metadataPath))
            {
                // Create minimal metadata from template file name
                if (File.Exists(templatePath))
                {
                    return new AdvancedTemplate
                    {
                        Name = Path.GetFileNameWithoutExtension(templatePath),
                        FilePath = templatePath
                    };
                }
                return null;
            }

            try
            {
                var json = File.ReadAllText(metadataPath);
                var metadata = JsonSerializer.Deserialize(json, CanvasTemplateJsonContext.Default.AdvancedTemplate);
                if (metadata != null)
                {
                    metadata.FilePath = templatePath;
                }
                return metadata;
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Failed to load template metadata: {Path} - {Error}", metadataPath, ex.Message);
                return new AdvancedTemplate
                {
                    Name = Path.GetFileNameWithoutExtension(templatePath),
                    FilePath = templatePath
                };
            }
        }

        /// <summary>
        /// Enumerates all advanced template files with their metadata.
        /// </summary>
        public static IReadOnlyList<AdvancedTemplate> EnumerateTemplates()
        {
            var result = new List<AdvancedTemplate>();
            var dir = GetTemplatesDirectory();

            if (!Directory.Exists(dir))
                return result;

            foreach (var templatePath in Directory.GetFiles(dir, $"*{TemplateExtension}", SearchOption.TopDirectoryOnly))
            {
                var metadata = LoadMetadata(templatePath);
                if (metadata != null)
                {
                    result.Add(metadata);
                }
            }

            // Sort by name
            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            return result;
        }

        /// <summary>
        /// Deletes an advanced template and its metadata.
        /// </summary>
        public static bool Delete(string templatePath)
        {
            try
            {
                var metadataPath = Path.ChangeExtension(templatePath, MetadataExtension);

                if (File.Exists(templatePath))
                    File.Delete(templatePath);

                if (File.Exists(metadataPath))
                    File.Delete(metadataPath);

                LoggingService.Info("Deleted advanced template: {Path}", templatePath);
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to delete template: {Path}", ex, templatePath);
                return false;
            }
        }
    }
}
