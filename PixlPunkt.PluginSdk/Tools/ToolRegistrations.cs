using PixlPunkt.PluginSdk.Enums;
using PixlPunkt.PluginSdk.Painting;
using PixlPunkt.PluginSdk.Selection;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Shapes;
using PixlPunkt.PluginSdk.Tile;
using PixlPunkt.PluginSdk.Utility;

namespace PixlPunkt.PluginSdk.Tools
{
    /// <summary>
    /// Registration record for a brush/stroke-based tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Prefer using <see cref="Builders.ToolBuilders.BrushTool"/> for a fluent API.</strong>
    /// </para>
    /// <para>
    /// Brush tools perform stroke-based pixel painting using <see cref="IStrokePainter"/>
    /// implementations. Examples: Brush, Eraser, Blur, Smudge, etc.
    /// </para>
    /// </remarks>
    public sealed record BrushToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        Func<IStrokePainter> PainterFactory
    ) : IToolRegistration
    {
        /// <inheritdoc/>
        public ToolCategory Category => ToolCategory.Brush;

        /// <summary>
        /// Gets whether this tool uses a stroke painter.
        /// </summary>
        public bool HasPainter => PainterFactory != null;

        /// <summary>
        /// Creates a new painter instance for this tool.
        /// </summary>
        public IStrokePainter? CreatePainter() => PainterFactory?.Invoke();
    }

    /// <summary>
    /// Registration record for a shape tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Prefer using <see cref="Builders.ToolBuilders.ShapeTool"/> for a fluent API.</strong>
    /// </para>
    /// <para>
    /// Shape tools render geometric primitives using <see cref="IShapeBuilder"/>.
    /// Examples: Rectangle, Ellipse, Line, Star, etc.
    /// </para>
    /// </remarks>
    public sealed record ShapeToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        Func<IShapeBuilder>? ShapeBuilderFactory = null
    ) : IToolRegistration
    {
        /// <inheritdoc/>
        public ToolCategory Category => ToolCategory.Shape;

        /// <summary>
        /// Gets whether this tool has a shape builder.
        /// </summary>
        public bool HasShapeBuilder => ShapeBuilderFactory != null;

        /// <summary>
        /// Creates a new shape builder instance for this tool.
        /// </summary>
        public IShapeBuilder? CreateShapeBuilder() => ShapeBuilderFactory?.Invoke();
    }

    /// <summary>
    /// Registration record for a selection tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Prefer using <see cref="Builders.ToolBuilders.SelectionTool"/> for a fluent API.</strong>
    /// </para>
    /// <para>
    /// Selection tools create or modify selection regions using <see cref="ISelectionTool"/>.
    /// Examples: Rectangle Select, Ellipse Select, Lasso, Magic Wand, etc.
    /// </para>
    /// </remarks>
    public sealed record SelectionToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        Func<ISelectionTool>? SelectionToolFactory = null
    ) : IToolRegistration
    {
        /// <inheritdoc/>
        public ToolCategory Category => ToolCategory.Select;

        /// <summary>
        /// Gets whether this tool has a selection tool implementation.
        /// </summary>
        public bool HasSelectionTool => SelectionToolFactory != null;

        /// <summary>
        /// Creates a new selection tool instance.
        /// </summary>
        public ISelectionTool? CreateSelectionTool() => SelectionToolFactory?.Invoke();
    }

    /// <summary>
    /// Registration record for a utility tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Prefer using <see cref="Builders.ToolBuilders.UtilityTool"/> for a fluent API.</strong>
    /// </para>
    /// <para>
    /// Utility tools manipulate the viewport or query state using <see cref="IUtilityHandler"/>.
    /// Examples: Pan, Zoom, Color Picker, Info tool, etc.
    /// </para>
    /// </remarks>
    public sealed record UtilityToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        Func<IUtilityContext, IUtilityHandler>? UtilityHandlerFactory = null
    ) : IToolRegistration
    {
        /// <inheritdoc/>
        public ToolCategory Category => ToolCategory.Utility;

        /// <summary>
        /// Gets whether this tool has a utility handler implementation.
        /// </summary>
        public bool HasUtilityHandler => UtilityHandlerFactory != null;

        /// <summary>
        /// Creates a new utility handler instance.
        /// </summary>
        public IUtilityHandler? CreateUtilityHandler(IUtilityContext context) => UtilityHandlerFactory?.Invoke(context);
    }

    /// <summary>
    /// Registration record for a tile tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Prefer using fluent builder API for a cleaner registration experience.</strong>
    /// </para>
    /// <para>
    /// Tile tools perform tile-based editing operations using <see cref="ITileHandler"/>.
    /// Examples: TileStamper (place tiles), TileModifier (offset/rotate/scale tile content).
    /// </para>
    /// <para><strong>Universal Behavior:</strong></para>
    /// <para>
    /// All tile tools support RMB tile sampling. When the user right-clicks, the tile
    /// under the cursor and its mapping are captured for subsequent operations.
    /// </para>
    /// </remarks>
    public sealed record TileToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        Func<ITileContext, ITileHandler>? TileHandlerFactory = null
    ) : IToolRegistration
    {
        /// <inheritdoc/>
        public ToolCategory Category => ToolCategory.Tile;

        /// <summary>
        /// Gets whether this tool has a tile handler implementation.
        /// </summary>
        public bool HasTileHandler => TileHandlerFactory != null;

        /// <summary>
        /// Creates a new tile handler instance.
        /// </summary>
        /// <param name="context">The tile context for host services.</param>
        /// <returns>A new tile handler instance, or null if no factory provided.</returns>
        public ITileHandler? CreateTileHandler(ITileContext context) => TileHandlerFactory?.Invoke(context);
    }
}
