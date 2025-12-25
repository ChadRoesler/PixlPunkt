namespace PixlPunkt.PluginSdk.Tools.Builders
{
    /// <summary>
    /// Static factory for creating tool builders with a fluent API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ToolBuilders"/> provides the entry points to the fluent builder API,
    /// making tool registration more readable and consistent with the host application pattern.
    /// </para>
    /// <para>
    /// <strong>Example Usage:</strong>
    /// <code>
    /// public IEnumerable&lt;IToolRegistration&gt; GetToolRegistrations()
    /// {
    ///     yield return ToolBuilders.BrushTool("myplugin.brush.sparkle")
    ///         .WithDisplayName("Sparkle Brush")
    ///         .WithSettings(_sparkleSettings)
    ///         .WithPainter(() => new SparklePainter(_sparkleSettings))
    ///         .Build();
    ///         
    ///     yield return ToolBuilders.TileTool("myplugin.tile.stamper")
    ///         .WithDisplayName("Tile Stamper")
    ///         .WithSettings(_stamperSettings)
    ///         .WithHandler(ctx => new TileStamperHandler(ctx, _stamperSettings))
    ///         .Build();
    ///         
    ///     yield return ToolBuilders.ShapeTool("myplugin.shape.star")
    ///         .WithDisplayName("Star")
    ///         .WithSettings(_starSettings)
    ///         .WithShapeBuilder(() => new StarShapeBuilder(_starSettings))
    ///         .Build();
    ///         
    ///     yield return ToolBuilders.SelectionTool("myplugin.select.ellipse")
    ///         .WithDisplayName("Ellipse Select")
    ///         .WithSettings(_ellipseSettings)
    ///         .WithToolFactory(() => new EllipseSelectTool())
    ///         .Build();
    ///         
    ///     yield return ToolBuilders.UtilityTool("myplugin.utility.info")
    ///         .WithDisplayName("Info Tool")
    ///         .WithSettings(_infoSettings)
    ///         .WithHandler(ctx => new InfoToolHandler(ctx, _infoSettings))
    ///         .Build();
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static class ToolBuilders
    {
        /// <summary>
        /// Starts building a brush tool registration.
        /// </summary>
        /// <param name="id">The unique tool identifier (e.g., "myplugin.brush.sparkle").</param>
        /// <returns>A fluent builder for configuring the brush tool.</returns>
        /// <example>
        /// <code>
        /// ToolBuilders.BrushTool("myplugin.brush.sparkle")
        ///     .WithDisplayName("Sparkle Brush")
        ///     .WithSettings(sparkleSettings)
        ///     .WithPainter(() => new SparklePainter(sparkleSettings))
        ///     .Build();
        /// </code>
        /// </example>
        public static BrushToolBuilder BrushTool(string id) => new(id);

        /// <summary>
        /// Starts building a tile tool registration.
        /// </summary>
        /// <param name="id">The unique tool identifier (e.g., "myplugin.tile.stamper").</param>
        /// <returns>A fluent builder for configuring the tile tool.</returns>
        /// <example>
        /// <code>
        /// ToolBuilders.TileTool("myplugin.tile.stamper")
        ///     .WithDisplayName("Tile Stamper")
        ///     .WithSettings(stamperSettings)
        ///     .WithHandler(ctx => new TileStamperHandler(ctx, stamperSettings))
        ///     .Build();
        /// </code>
        /// </example>
        public static TileToolBuilder TileTool(string id) => new(id);

        /// <summary>
        /// Starts building a shape tool registration.
        /// </summary>
        /// <param name="id">The unique tool identifier (e.g., "myplugin.shape.star").</param>
        /// <returns>A fluent builder for configuring the shape tool.</returns>
        /// <example>
        /// <code>
        /// ToolBuilders.ShapeTool("myplugin.shape.star")
        ///     .WithDisplayName("Star")
        ///     .WithSettings(starSettings)
        ///     .WithShapeBuilder(() => new StarShapeBuilder(starSettings))
        ///     .Build();
        /// </code>
        /// </example>
        public static ShapeToolBuilder ShapeTool(string id) => new(id);

        /// <summary>
        /// Starts building a selection tool registration.
        /// </summary>
        /// <param name="id">The unique tool identifier (e.g., "myplugin.select.ellipse").</param>
        /// <returns>A fluent builder for configuring the selection tool.</returns>
        /// <example>
        /// <code>
        /// ToolBuilders.SelectionTool("myplugin.select.ellipse")
        ///     .WithDisplayName("Ellipse Select")
        ///     .WithSettings(ellipseSettings)
        ///     .WithToolFactory(() => new EllipseSelectTool())
        ///     .Build();
        /// </code>
        /// </example>
        public static SelectionToolBuilder SelectionTool(string id) => new(id);

        /// <summary>
        /// Starts building a utility tool registration.
        /// </summary>
        /// <param name="id">The unique tool identifier (e.g., "myplugin.utility.info").</param>
        /// <returns>A fluent builder for configuring the utility tool.</returns>
        /// <example>
        /// <code>
        /// ToolBuilders.UtilityTool("myplugin.utility.info")
        ///     .WithDisplayName("Info Tool")
        ///     .WithSettings(infoSettings)
        ///     .WithHandler(ctx => new InfoToolHandler(ctx, infoSettings))
        ///     .Build();
        /// </code>
        /// </example>
        public static UtilityToolBuilder UtilityTool(string id) => new(id);
    }
}
