namespace PixlPunkt.PluginSdk.Enums
{
    /// <summary>
    /// Input interaction patterns for tools.
    /// </summary>
    /// <remarks>
    /// Determines how the UI routes pointer events to the tool.
    /// </remarks>
    public enum ToolInputPattern
    {
        /// <summary>
        /// No direct input handling. Tool is activated via other means
        /// (e.g., Pan uses space-pan, Zoom uses mouse wheel).
        /// </summary>
        None,

        /// <summary>
        /// Single click triggers the action (e.g., Fill, Dropper).
        /// No drag interaction.
        /// </summary>
        Click,

        /// <summary>
        /// Continuous stroke from press to release (e.g., Brush, Eraser, Blur).
        /// Stamps along the path as the pointer moves.
        /// </summary>
        Stroke,

        /// <summary>
        /// Two-point drag defines start and end positions (e.g., Rect, Ellipse, Gradient).
        /// Preview updates during drag, commits on release.
        /// </summary>
        TwoPoint,

        /// <summary>
        /// Complex multi-step interaction requiring custom handling
        /// (e.g., Lasso polygon vertices, Selection transforms).
        /// </summary>
        Custom
    }

    /// <summary>
    /// Overlay rendering styles for cursor/brush preview.
    /// </summary>
    /// <remarks>
    /// Determines how the brush footprint is visualized when hovering.
    /// </remarks>
    public enum ToolOverlayStyle
    {
        /// <summary>
        /// No brush overlay shown (e.g., Fill, Selection tools, Dropper).
        /// </summary>
        None,

        /// <summary>
        /// Filled ghost showing blended result preview.
        /// Used only by Brush tool for live color preview.
        /// </summary>
        FilledGhost,

        /// <summary>
        /// Contrast outline showing brush boundary.
        /// Used by Eraser, Blur, Smudge, Jumble, Replacer, etc.
        /// </summary>
        Outline,

        /// <summary>
        /// Shape-specific preview during drag operation.
        /// Shows the shape being drawn (Rect, Ellipse) or start point when hovering.
        /// </summary>
        ShapePreview,

        /// <summary>
        /// Tile boundary outline showing the tile grid cell.
        /// Used by TileStamper and TileModifier for tile-aligned operations.
        /// May include a ghosted tile image preview when stamping.
        /// </summary>
        TileBoundary
    }
}
