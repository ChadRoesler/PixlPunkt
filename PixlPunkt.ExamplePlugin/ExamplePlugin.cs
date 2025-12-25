using System.Globalization;
using System.Text;
using PixlPunkt.ExamplePlugin.Effects;
using PixlPunkt.ExamplePlugin.Tools;
using PixlPunkt.ExamplePlugin.Tools.Selection;
using PixlPunkt.ExamplePlugin.Tools.Shapes;
using PixlPunkt.ExamplePlugin.Tools.Tile;
using PixlPunkt.ExamplePlugin.Tools.Utility;
using PixlPunkt.PluginSdk.Effects;
using PixlPunkt.PluginSdk.Effects.Builders;
using PixlPunkt.PluginSdk.IO;
using PixlPunkt.PluginSdk.IO.Builders;
using PixlPunkt.PluginSdk.Plugins;
using PixlPunkt.PluginSdk.Tools;
using PixlPunkt.PluginSdk.Tools.Builders;

// Assembly-level plugin attributes (alternative to csproj properties)
[assembly: PluginId("pixlpunkt.example")]
[assembly: PluginDisplayName("Example Plugin")]
[assembly: PluginAuthor("PixlPunkt Team")]
[assembly: PluginDescription("Example plugin demonstrating the SDK with custom tools and effects")]
[assembly: PluginMinApiVersion(1)]

namespace PixlPunkt.ExamplePlugin
{
    /// <summary>
    /// Example plugin demonstrating the PixlPunkt Plugin SDK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This plugin serves as a reference implementation for plugin developers.
    /// It demonstrates:
    /// </para>
    /// <list type="bullet">
    /// <item>Implementing <see cref="IPlugin"/> interface</item>
    /// <item>Registering custom brush tools (Sparkle Brush) using fluent builders</item>
    /// <item>Registering custom shape tools (Star) using fluent builders</item>
    /// <item>Registering custom selection tools (Ellipse Select) using fluent builders</item>
    /// <item>Registering custom utility tools (Info Tool) using fluent builders</item>
    /// <item>Registering custom layer effects (Halftone) using fluent builders</item>
    /// <item>Registering custom import/export handlers for file formats</item>
    /// <item>Using plugin context for logging</item>
    /// </list>
    /// </remarks>
    public sealed class ExamplePlugin : IPlugin
    {
        private IPluginContext? _context;

        // Shared settings instances for tools
        private readonly SparkleSettings _sparkleSettings = new();
        private readonly StarShapeSettings _starShapeSettings = new();
        private readonly EllipseSelectSettings _ellipseSelectSettings = new();
        private readonly InfoToolSettings _infoToolSettings = new();
        private readonly TileBucketFillSettings _tileBucketFillSettings = new();

        /// <inheritdoc/>
        public string Id => "pixlpunkt.example";

        /// <inheritdoc/>
        public string DisplayName => "Example Plugin";

        /// <inheritdoc/>
        public string Version => "1.0.0";

        /// <inheritdoc/>
        public string Author => "PixlPunkt Team";

        /// <inheritdoc/>
        public string Description => "Example plugin demonstrating the SDK with custom tools and effects";

        /// <inheritdoc/>
        public void Initialize(IPluginContext context)
        {
            _context = context;
            _context.Log(PluginLogLevel.Info, $"Example plugin v{Version} initialized!");
            _context.Log(PluginLogLevel.Debug, $"Plugin directory: {context.PluginDirectory}");
            _context.Log(PluginLogLevel.Debug, $"Data directory: {context.DataDirectory}");
            _context.Log(PluginLogLevel.Debug, $"Host version: {context.HostVersion}, API version: {context.ApiVersion}");
        }

        /// <inheritdoc/>
        public IEnumerable<IToolRegistration> GetToolRegistrations()
        {
            _context?.Log(PluginLogLevel.Debug, "Registering example tools...");

            // ====================================================================
            // BRUSH TOOLS
            // ====================================================================

            yield return ToolBuilders.BrushTool("pixlpunkt.example.brush.sparkle")
                .WithDisplayName("Sparkle Brush")
                .WithSettings(_sparkleSettings)
                .WithPainter(() => new SparklePainter(_sparkleSettings))
                .Build();

            // ====================================================================
            // SHAPE TOOLS
            // ====================================================================

            yield return ToolBuilders.ShapeTool("pixlpunkt.example.shape.star")
                .WithDisplayName("Star")
                .WithSettings(_starShapeSettings)
                .WithShapeBuilder(() => new StarShapeBuilder(_starShapeSettings))
                .Build();

            // ====================================================================
            // SELECTION TOOLS
            // ====================================================================

            yield return ToolBuilders.SelectionTool("pixlpunkt.example.select.ellipse")
                .WithDisplayName("Ellipse Select")
                .WithSettings(_ellipseSelectSettings)
                .WithToolFactory(() => new EllipseSelectTool())
                .Build();

            // ====================================================================
            // UTILITY TOOLS
            // ====================================================================

            yield return ToolBuilders.UtilityTool("pixlpunkt.example.utility.info")
                .WithDisplayName("Info Tool")
                .WithSettings(_infoToolSettings)
                .WithHandler(ctx => new InfoToolHandler(ctx, _infoToolSettings))
                .Build();



            // ====================================================================
            // TILE TOOLS
            // ====================================================================

            yield return ToolBuilders.TileTool("pixlpunkt.example.tile.bucketfill")
                .WithDisplayName("Tile Bucket Fill")
                .WithSettings(_tileBucketFillSettings)
                .WithHandler(ctx => new TileBucketFillHandler(ctx, _tileBucketFillSettings))
                .Build();

            _context?.Log(PluginLogLevel.Info, "Registered 5 tools: Sparkle Brush, Star, Ellipse Select, Info Tool, Tile Bucket Fill");
        }

        /// <inheritdoc/>
        public IEnumerable<IEffectRegistration> GetEffectRegistrations()
        {
            _context?.Log(PluginLogLevel.Debug, "Registering example effects...");

            // ====================================================================
            // LAYER EFFECTS
            // ====================================================================

            yield return EffectBuilders.Effect("pixlpunkt.example.effect.halftone")
                .WithDisplayName("Halftone")
                .WithDescription("Creates a halftone dot pattern effect")
                .WithCategory(EffectCategory.Filter)
                .WithFactory<HalftoneEffect>()
                .WithOptions<HalftoneEffect>(e => e.GetOptions())
                .Build();

            _context?.Log(PluginLogLevel.Info, "Registered 1 effect: Halftone");
        }

        /// <inheritdoc/>
        public IEnumerable<IImportRegistration> GetImportRegistrations()
        {
            _context?.Log(PluginLogLevel.Debug, "Registering example import handlers...");

            // ====================================================================
            // PALETTE IMPORT - Simple text format
            // Each line is a hex color: #RRGGBB or #AARRGGBB
            // ====================================================================

            yield return ImportBuilders.ForPalette("pixlpunkt.example.import.txtpalette")
                .WithFormat(".txtpal", "Text Palette", "Simple text-based color palette (one hex color per line)")
                .WithHandler(ImportTextPalette)
                .Build();

            _context?.Log(PluginLogLevel.Info, "Registered 1 import handler: Text Palette");
        }

        /// <inheritdoc/>
        public IEnumerable<IExportRegistration> GetExportRegistrations()
        {
            _context?.Log(PluginLogLevel.Debug, "Registering example export handlers...");

            // ====================================================================
            // PALETTE EXPORT - Simple text format
            // ====================================================================

            yield return ExportBuilders.ForPalette("pixlpunkt.example.export.txtpalette")
                .WithFormat(".txtpal", "Text Palette", "Simple text-based color palette (one hex color per line)")
                .WithHandler(ExportTextPalette)
                .Build();

            _context?.Log(PluginLogLevel.Info, "Registered 1 export handler: Text Palette");
        }

        /// <summary>
        /// Imports a text palette file.
        /// </summary>
        private PaletteImportResult ImportTextPalette(IImportContext context)
        {
            try
            {
                var text = context.ReadAllText();
                var lines = text.Split('\n', '\r')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("//") && !l.StartsWith("#comment", StringComparison.OrdinalIgnoreCase));

                var colors = new List<uint>();
                foreach (var line in lines)
                {
                    if (TryParseHexColor(line, out uint color))
                    {
                        colors.Add(color);
                    }
                }

                if (colors.Count == 0)
                {
                    return new PaletteImportResult
                    {
                        ErrorMessage = "No valid colors found in file"
                    };
                }

                // Extract palette name from filename
                var name = Path.GetFileNameWithoutExtension(context.FileName);

                _context?.Log(PluginLogLevel.Info, $"Imported {colors.Count} colors from '{context.FileName}'");

                return new PaletteImportResult
                {
                    Colors = colors,
                    PaletteName = name
                };
            }
            catch (Exception ex)
            {
                _context?.Log(PluginLogLevel.Error, $"Failed to import palette: {ex.Message}");
                return new PaletteImportResult
                {
                    ErrorMessage = $"Import failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Exports a palette to text format.
        /// </summary>
        private bool ExportTextPalette(IExportContext context, IPaletteExportData data)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"// {data.Name}");
                sb.AppendLine($"// Exported from PixlPunkt");
                sb.AppendLine($"// {data.Colors.Count} colors");
                sb.AppendLine();

                foreach (var color in data.Colors)
                {
                    // Format as #AARRGGBB (or #RRGGBB if fully opaque)
                    byte a = (byte)(color >> 24);
                    byte r = (byte)(color >> 16);
                    byte g = (byte)(color >> 8);
                    byte b = (byte)color;

                    if (a == 255)
                    {
                        sb.AppendLine($"#{r:X2}{g:X2}{b:X2}");
                    }
                    else
                    {
                        sb.AppendLine($"#{a:X2}{r:X2}{g:X2}{b:X2}");
                    }
                }

                context.WriteAllText(sb.ToString());

                _context?.Log(PluginLogLevel.Info, $"Exported {data.Colors.Count} colors to '{context.FileName}'");
                return true;
            }
            catch (Exception ex)
            {
                _context?.Log(PluginLogLevel.Error, $"Failed to export palette: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts to parse a hex color string.
        /// </summary>
        private static bool TryParseHexColor(string input, out uint color)
        {
            color = 0;

            // Remove # prefix if present
            var hex = input.TrimStart('#');

            // Try to parse as hex
            if (hex.Length == 6)
            {
                // #RRGGBB -> AARRGGBB with alpha = 255
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                {
                    color = 0xFF000000 | rgb;
                    return true;
                }
            }
            else if (hex.Length == 8)
            {
                // #AARRGGBB
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out color))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            _context?.Log(PluginLogLevel.Info, "Example plugin shutting down");
            _context = null;
        }
    }
}
