namespace PixlPunkt.Uno.Core.Tools.Builders
{
    /// <summary>
    /// Extension methods for <see cref="IToolRegistry"/> providing fluent tool builder access.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These extension methods provide the entry points to the fluent builder API, making
    /// tool registration more readable and less error-prone than direct constructor calls.
    /// </para>
    /// <para>
    /// <strong>Before (verbose):</strong>
    /// <code>
    /// registry.Register(new BrushToolRegistration(
    ///     Id: ToolIds.Brush,
    ///     DisplayName: "Brush",
    ///     Settings: toolState.BrushTool,
    ///     PainterFactory: () => new BrushPainter(toolState.BrushTool)
    /// ));
    /// </code>
    /// </para>
    /// <para>
    /// <strong>After (fluent):</strong>
    /// <code>
    /// registry.AddBrushTool(ToolIds.Brush)
    ///     .WithDisplayName("Brush")
    ///     .WithSettings(toolState.BrushTool)
    ///     .WithPainter(() => new BrushPainter(toolState.BrushTool))
    ///     .Register();
    /// </code>
    /// </para>
    /// </remarks>
    public static class ToolBuilderExtensions
    {
        /// <summary>
        /// Starts building a brush tool registration.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        /// <returns>A fluent builder for configuring the brush tool.</returns>
        /// <example>
        /// <code>
        /// registry.AddBrushTool("myplugin.brush.custom")
        ///     .WithDisplayName("Custom Brush")
        ///     .WithSettings(mySettings)
        ///     .WithPainter(() => new CustomBrushPainter())
        ///     .Register();
        /// </code>
        /// </example>
        public static BrushToolBuilder AddBrushTool(this IToolRegistry registry, string id)
            => new(registry, id);

        /// <summary>
        /// Starts building a tile tool registration.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        /// <returns>A fluent builder for configuring the tile tool.</returns>
        /// <example>
        /// <code>
        /// registry.AddTileTool(ToolIds.TileStamper)
        ///     .WithDisplayName("Tile Stamper")
        ///     .WithSettings(toolState.TileStamper)
        ///     .WithHandler(ctx => new TileStamperHandler(ctx, toolState.TileStamper))
        ///     .Register();
        /// </code>
        /// </example>
        public static TileToolBuilder AddTileTool(this IToolRegistry registry, string id)
            => new(registry, id);

        /// <summary>
        /// Starts building a shape tool registration.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        /// <returns>A fluent builder for configuring the shape tool.</returns>
        /// <example>
        /// <code>
        /// registry.AddShapeTool("myplugin.shape.star")
        ///     .WithDisplayName("Star")
        ///     .WithSettings(mySettings)
        ///     .WithShapeBuilder(new StarShapeBuilder())
        ///     .Register();
        /// </code>
        /// </example>
        public static ShapeToolBuilder AddShapeTool(this IToolRegistry registry, string id)
            => new(registry, id);

        /// <summary>
        /// Starts building a selection tool registration.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        /// <returns>A fluent builder for configuring the selection tool.</returns>
        /// <example>
        /// <code>
        /// registry.AddSelectionTool("myplugin.select.fuzzy")
        ///     .WithDisplayName("Fuzzy Select")
        ///     .WithSettings(mySettings)
        ///     .WithToolFactory(ctx => new FuzzySelectionTool(ctx))
        ///     .Register();
        /// </code>
        /// </example>
        public static SelectionToolBuilder AddSelectionTool(this IToolRegistry registry, string id)
            => new(registry, id);

        /// <summary>
        /// Starts building a utility tool registration.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        /// <returns>A fluent builder for configuring the utility tool.</returns>
        /// <example>
        /// <code>
        /// registry.AddUtilityTool("myplugin.utility.measure")
        ///     .WithDisplayName("Measure")
        ///     .WithSettings(mySettings)
        ///     .WithHandler(ctx => new MeasureHandler(ctx))
        ///     .Register();
        /// </code>
        /// </example>
        public static UtilityToolBuilder AddUtilityTool(this IToolRegistry registry, string id)
            => new(registry, id);

        /// <summary>
        /// Starts building a fill tool registration.
        /// </summary>
        /// <param name="registry">The registry to register with.</param>
        /// <param name="id">The unique tool identifier.</param>
        /// <returns>A fluent builder for configuring the fill tool.</returns>
        /// <example>
        /// <code>
        /// registry.AddFillTool(ToolIds.Fill)
        ///     .WithDisplayName("Fill")
        ///     .WithSettings(toolState.Fill)
        ///     .Register();
        /// </code>
        /// </example>
        public static FillToolBuilder AddFillTool(this IToolRegistry registry, string id)
            => new(registry, id);
    }
}
