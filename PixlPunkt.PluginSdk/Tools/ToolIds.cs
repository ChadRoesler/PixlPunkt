namespace PixlPunkt.PluginSdk.Tools
{
    /// <summary>
    /// Provides string constants for all built-in tool identifiers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tool IDs follow the convention: <c>{vendor}.{category}.{name}</c>
    /// </para>
    /// <para>
    /// Built-in tools use the <c>pixlpunkt</c> vendor prefix. Third-party plugins
    /// should use their own vendor prefix (e.g., <c>com.company.category.toolname</c>).
    /// </para>
    /// <para>
    /// These constants can be used by plugins to:
    /// </para>
    /// <list type="bullet">
    /// <item>Reference built-in tools for comparison or switching</item>
    /// <item>Follow the naming convention for new tools</item>
    /// <item>Check if a tool ID is built-in via <see cref="IsBuiltIn"/></item>
    /// </list>
    /// </remarks>
    public static class ToolIds
    {
        // ====================================================================
        // UTILITY TOOLS
        // ====================================================================

        /// <summary>Pan tool for moving the viewport.</summary>
        public const string Pan = "pixlpunkt.utility.pan";

        /// <summary>Zoom/magnifier tool for changing viewport scale.</summary>
        public const string Zoom = "pixlpunkt.utility.zoom";

        /// <summary>Color picker (dropper) tool for sampling colors.</summary>
        public const string Dropper = "pixlpunkt.utility.dropper";

        // ====================================================================
        // SELECTION TOOLS
        // ====================================================================

        /// <summary>Rectangle selection tool.</summary>
        public const string SelectRect = "pixlpunkt.select.rect";

        /// <summary>Magic wand selection tool.</summary>
        public const string Wand = "pixlpunkt.select.wand";

        /// <summary>Lasso (polygon) selection tool.</summary>
        public const string Lasso = "pixlpunkt.select.lasso";

        /// <summary>Paint selection (brush mask) tool.</summary>
        public const string PaintSelect = "pixlpunkt.select.paint";

        // ====================================================================
        // BRUSH TOOLS
        // ====================================================================

        /// <summary>Standard brush painting tool.</summary>
        public const string Brush = "pixlpunkt.brush.brush";

        /// <summary>Eraser tool.</summary>
        public const string Eraser = "pixlpunkt.brush.eraser";

        /// <summary>Flood fill tool.</summary>
        public const string Fill = "pixlpunkt.brush.fill";

        /// <summary>Blur brush tool.</summary>
        public const string Blur = "pixlpunkt.brush.blur";

        /// <summary>Smudge brush tool.</summary>
        public const string Smudge = "pixlpunkt.brush.smudge";

        /// <summary>Jumble/scatter brush tool.</summary>
        public const string Jumble = "pixlpunkt.brush.jumble";

        /// <summary>Color replacer tool.</summary>
        public const string Replacer = "pixlpunkt.brush.replacer";

        /// <summary>Gradient brush tool - cycles through palette colors for highlights/shadows.</summary>
        public const string Gradient = "pixlpunkt.brush.gradient";

        // ====================================================================
        // TILE TOOLS
        // ====================================================================

        /// <summary>Tile stamper tool - places tiles with optional mapping data.</summary>
        public const string TileStamper = "pixlpunkt.tile.stamper";

        /// <summary>Tile modifier tool - offsets, rotates, and scales tile content.</summary>
        public const string TileModifier = "pixlpunkt.tile.modifier";

        // ====================================================================
        // SHAPE TOOLS
        // ====================================================================

        /// <summary>Rectangle shape tool.</summary>
        public const string ShapeRect = "pixlpunkt.shape.rect";

        /// <summary>Ellipse shape tool.</summary>
        public const string ShapeEllipse = "pixlpunkt.shape.ellipse";

        // ====================================================================
        // HELPERS
        // ====================================================================

        /// <summary>
        /// The vendor prefix for all built-in PixlPunkt tools.
        /// </summary>
        public const string BuiltInVendor = "pixlpunkt";

        /// <summary>
        /// Checks if a tool ID is a built-in PixlPunkt tool.
        /// </summary>
        /// <param name="toolId">The tool ID to check.</param>
        /// <returns>True if the tool is built-in; false if it's a plugin tool.</returns>
        public static bool IsBuiltIn(string toolId)
            => toolId?.StartsWith(BuiltInVendor + ".") == true;
    }
}
