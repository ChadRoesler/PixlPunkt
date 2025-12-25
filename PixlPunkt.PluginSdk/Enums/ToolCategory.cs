namespace PixlPunkt.PluginSdk.Enums
{
    /// <summary>
    /// Defines the categories of tools that determine engine routing and behavior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tool categories group tools by their fundamental behavior and the engine that handles them:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Utility</strong>: Viewport manipulation and state queries (Pan, Zoom, Dropper)</item>
    /// <item><strong>Select</strong>: Selection region creation and modification (Rect, Wand, Lasso, Paint)</item>
    /// <item><strong>Brush</strong>: Stroke-based pixel painting (Brush, Eraser, Blur, Smudge, Fill)</item>
    /// <item><strong>Tile</strong>: Tile-based editing operations (TileStamper, TileModifier)</item>
    /// <item><strong>Shape</strong>: Geometric primitive rendering (Rect, Ellipse, Gradient)</item>
    /// </list>
    /// <para>
    /// Each category corresponds to a specific engine or handler in the canvas system.
    /// </para>
    /// </remarks>
    public enum ToolCategory
    {
        /// <summary>
        /// Utility tools that manipulate the viewport or query state without modifying pixels.
        /// </summary>
        /// <remarks>
        /// Examples: Pan, Zoom, Color Picker (Dropper).
        /// These tools have no painter or factory - they are handled directly by the canvas.
        /// </remarks>
        Utility,

        /// <summary>
        /// Selection tools that create or modify selection regions.
        /// </summary>
        /// <remarks>
        /// Examples: Rectangle Select, Magic Wand, Lasso, Paint Select.
        /// These tools use ISelectionTool and are managed by the selection subsystem.
        /// </remarks>
        Select,

        /// <summary>
        /// Brush tools that perform stroke-based pixel painting.
        /// </summary>
        /// <remarks>
        /// Examples: Brush, Eraser, Blur, Smudge, Jumble, Fill, Replacer.
        /// These tools use IStrokePainter and are managed by the stroke engine.
        /// </remarks>
        Brush,

        /// <summary>
        /// Tile tools that perform tile-based editing operations.
        /// </summary>
        /// <remarks>
        /// <para>Examples: TileStamper, TileModifier.</para>
        /// <para>
        /// Tile tools operate on the document's tile grid and tile set:
        /// </para>
        /// <list type="bullet">
        /// <item>TileStamper: Places tiles with optional mapping, creates tiles from canvas regions</item>
        /// <item>TileModifier: Offsets, rotates, and scales content within tile boundaries</item>
        /// </list>
        /// <para>
        /// Tile tools snap to the document's tile grid and support RMB tile sampling
        /// (similar to color dropper but for tiles and their mappings).
        /// </para>
        /// </remarks>
        Tile,

        /// <summary>
        /// Shape tools that render geometric primitives.
        /// </summary>
        /// <remarks>
        /// Examples: Rectangle, Ellipse, Line, Gradient.
        /// These tools define start/end points and render geometry on pointer release.
        /// </remarks>
        Shape
    }
}
