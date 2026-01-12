using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using PixlPunkt.Core.Canvas;
using PixlPunkt.Core.Compositing.Effects;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Plugins;
using PixlPunkt.Core.Session;
using PixlPunkt.Core.Settings;

namespace PixlPunkt.Core.Serialization
{
    /// <summary>
    /// Source-generated JSON serializer context for session state types.
    /// Required for .NET trimming/AOT compatibility.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(SessionState))]
    [JsonSerializable(typeof(SessionDocument))]
    [JsonSerializable(typeof(List<SessionDocument>))]
    public partial class SessionJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Source-generated JSON serializer context for shortcut settings types.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(ShortcutSettings))]
    [JsonSerializable(typeof(ShortcutBinding))]
    [JsonSerializable(typeof(Dictionary<string, ShortcutBinding>))]
    [JsonSerializable(typeof(HashSet<string>))]
    public partial class ShortcutSettingsJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Source-generated JSON serializer context for plugin manifest types.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(PluginManifest))]
    [JsonSerializable(typeof(ToolDeclaration))]
    [JsonSerializable(typeof(List<ToolDeclaration>))]
    [JsonSerializable(typeof(List<string>))]
    public partial class PluginManifestJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Source-generated JSON serializer context for canvas template types.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(CustomCanvasTemplate))]
    [JsonSerializable(typeof(AdvancedTemplate))]
    public partial class CanvasTemplateJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Source-generated JSON serializer context for application settings.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(AnimationSettings))]
    [JsonSerializable(typeof(ExportSettings))]
    public partial class AppSettingsJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Source-generated JSON serializer context for layer effect serialization.
    /// Includes all built-in effect types for .pxp file storage.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(AsciiEffect))]
    [JsonSerializable(typeof(ChromaticAberrationEffect))]
    [JsonSerializable(typeof(ColorAdjustEffect))]
    [JsonSerializable(typeof(CrtEffect))]
    [JsonSerializable(typeof(DropShadowEffect))]
    [JsonSerializable(typeof(GlowBloomEffect))]
    [JsonSerializable(typeof(GrainEffect))]
    [JsonSerializable(typeof(OrphanedEffect))]
    [JsonSerializable(typeof(OutlineEffect))]
    [JsonSerializable(typeof(PaletteQuantizeEffect))]
    [JsonSerializable(typeof(PixelateEffect))]
    [JsonSerializable(typeof(ScanLinesEffect))]
    [JsonSerializable(typeof(VignetteEffect))]
    public partial class EffectJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Source-generated JSON serializer context for ASCII glyph set loading.
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(AsciiGlyphSetJson))]
    public partial class AsciiGlyphSetJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// JSON model for external ASCII glyph set files (*.asciifont.json).
    /// </summary>
    public sealed class AsciiGlyphSetJson
    {
        public string? Name { get; set; }
        public string Ramp { get; set; } = string.Empty;
        public int GlyphWidth { get; set; } = 8;
        public int GlyphHeight { get; set; } = 8;
        public List<string>? Bitmaps { get; set; }
    }

    /// <summary>
    /// Source-generated JSON serializer context for recent documents service.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(RecentDocumentEntry))]
    [JsonSerializable(typeof(List<RecentDocumentEntry>))]
    public partial class RecentDocumentsJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Source-generated JSON serializer context for custom palette serialization.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(CustomPalette))]
    public partial class CustomPaletteJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Source-generated JSON serializer context for palette color arrays.
    /// Used by PaletteService import/export.
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(uint[]))]
    [JsonSerializable(typeof(List<uint>))]
    public partial class PaletteColorsJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Source-generated JSON serializer context for palette export JSON data.
    /// Used by BuiltInIOHandlers for JSON palette export.
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(PaletteExportJsonModel))]
    public partial class PaletteExportJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// JSON data model for palette export.
    /// Named differently from PaletteExportData to avoid conflict.
    /// </summary>
    public sealed class PaletteExportJsonModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IReadOnlyList<uint>? Colors { get; set; }
    }
}
