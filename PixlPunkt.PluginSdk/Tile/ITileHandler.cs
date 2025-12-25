namespace PixlPunkt.PluginSdk.Tile
{
    /// <summary>
    /// Cursor hint for tile tools.
    /// </summary>
    public enum TileCursorHint
    {
        /// <summary>Default cursor (tool-specific or application default).</summary>
        Default,

        /// <summary>Tile stamp cursor for placing tiles.</summary>
        Stamp,

        /// <summary>Tile create cursor for creating new tiles from canvas.</summary>
        Create,

        /// <summary>Tile offset cursor for shifting tile content.</summary>
        Offset,

        /// <summary>Tile rotate cursor for rotating tile content.</summary>
        Rotate,

        /// <summary>Tile scale cursor for scaling tile content.</summary>
        Scale,

        /// <summary>Tile picker cursor for sampling tiles (RMB dropper).</summary>
        Picker,

        /// <summary>Crosshair for precise positioning.</summary>
        Crosshair
    }

    /// <summary>
    /// Strategy interface for tile tool operations (TileStamper, TileModifier, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tile handlers encapsulate the input handling and state machine logic for
    /// tile-based editing tools. This brings tile tools into the same pattern as:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="Painting.IStrokePainter"/> for brush tools</item>
    /// <item><see cref="Shapes.IShapeBuilder"/> for shape tools</item>
    /// <item><see cref="Selection.ISelectionTool"/> for selection tools</item>
    /// <item><see cref="Utility.IUtilityHandler"/> for utility tools</item>
    /// </list>
    /// <para>
    /// Each tile handler manages its own state and responds to pointer events
    /// through the <see cref="ITileContext"/> provided during construction.
    /// </para>
    /// <para><strong>Universal Behavior:</strong></para>
    /// <para>
    /// All tile tools support RMB tile sampling (like color dropper but for tiles).
    /// When the user right-clicks, the tile under the cursor and its mapping are
    /// captured for subsequent stamping operations.
    /// </para>
    /// </remarks>
    public interface ITileHandler
    {
        /// <summary>
        /// Gets the unique tool identifier this handler implements.
        /// </summary>
        string ToolId { get; }

        /// <summary>
        /// Gets whether this handler is currently in an active operation.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Gets the cursor style hint for this tool.
        /// </summary>
        TileCursorHint CursorHint { get; }

        /// <summary>
        /// Handles pointer press events.
        /// </summary>
        /// <param name="screenX">X position in screen/view coordinates.</param>
        /// <param name="screenY">Y position in screen/view coordinates.</param>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <param name="isLeftButton">True if left button pressed.</param>
        /// <param name="isRightButton">True if right button pressed.</param>
        /// <param name="isShiftHeld">True if Shift modifier is held.</param>
        /// <param name="isCtrlHeld">True if Ctrl modifier is held.</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerPressed(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld);

        /// <summary>
        /// Handles pointer move events.
        /// </summary>
        /// <param name="screenX">X position in screen/view coordinates.</param>
        /// <param name="screenY">Y position in screen/view coordinates.</param>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <param name="isLeftButton">True if left button held.</param>
        /// <param name="isRightButton">True if right button held.</param>
        /// <param name="isShiftHeld">True if Shift modifier is held.</param>
        /// <param name="isCtrlHeld">True if Ctrl modifier is held.</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerMoved(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld);

        /// <summary>
        /// Handles pointer release events.
        /// </summary>
        /// <param name="screenX">X position in screen/view coordinates.</param>
        /// <param name="screenY">Y position in screen/view coordinates.</param>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <param name="isLeftButton">True if left button released.</param>
        /// <param name="isRightButton">True if right button released.</param>
        /// <param name="isShiftHeld">True if Shift modifier is held.</param>
        /// <param name="isCtrlHeld">True if Ctrl modifier is held.</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerReleased(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld);

        /// <summary>
        /// Resets the handler state (called when tool is deactivated or operation cancelled).
        /// </summary>
        void Reset();

        /// <summary>
        /// Gets the preview information for overlay rendering.
        /// </summary>
        /// <returns>Preview state for the tile overlay, or null if no preview should be shown.</returns>
        TileOverlayPreview? GetOverlayPreview();
    }

    /// <summary>
    /// Preview information for tile tool overlay rendering.
    /// </summary>
    /// <param name="TileX">Tile grid X coordinate (in tiles, not pixels).</param>
    /// <param name="TileY">Tile grid Y coordinate (in tiles, not pixels).</param>
    /// <param name="TileId">ID of the tile to preview, or -1 for empty/outline only.</param>
    /// <param name="ShowGhost">True to show a ghosted preview of the tile image.</param>
    /// <param name="ShowOutline">True to show the tile boundary outline.</param>
    /// <param name="SnapToGrid">True if snapped to tile grid; false for free positioning.</param>
    /// <param name="PixelOffsetX">Pixel offset within tile (for free positioning).</param>
    /// <param name="PixelOffsetY">Pixel offset within tile (for free positioning).</param>
    /// <param name="AnimationSelection">Optional animation frame selection range (for animation tool).</param>
    public readonly record struct TileOverlayPreview(
        int TileX,
        int TileY,
        int TileId,
        bool ShowGhost,
        bool ShowOutline,
        bool SnapToGrid,
        int PixelOffsetX = 0,
        int PixelOffsetY = 0,
        AnimationSelectionOverlay? AnimationSelection = null
    );

    /// <summary>
    /// Represents an animation frame selection range overlay.
    /// Used by the Tile Animation tool to show start/end frame selection.
    /// </summary>
    /// <param name="StartTileX">Start tile X coordinate (blue border).</param>
    /// <param name="StartTileY">Start tile Y coordinate.</param>
    /// <param name="EndTileX">End tile X coordinate (orange border).</param>
    /// <param name="EndTileY">End tile Y coordinate.</param>
    /// <param name="IsDragging">True if currently dragging to select frames.</param>
    public readonly record struct AnimationSelectionOverlay(
        int StartTileX,
        int StartTileY,
        int EndTileX,
        int EndTileY,
        bool IsDragging
    );
}
