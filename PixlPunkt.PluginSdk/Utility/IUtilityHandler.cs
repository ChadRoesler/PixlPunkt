namespace PixlPunkt.PluginSdk.Utility
{
    /// <summary>
    /// Cursor hint for utility tools.
    /// </summary>
    public enum UtilityCursorHint
    {
        /// <summary>Default cursor (tool-specific or application default).</summary>
        Default,

        /// <summary>Open hand cursor for pan tool.</summary>
        Hand,

        /// <summary>Closed/grabbing hand cursor during active pan.</summary>
        Grabbing,

        /// <summary>Magnifying glass for zoom tool.</summary>
        ZoomIn,

        /// <summary>Magnifying glass with minus for zoom out.</summary>
        ZoomOut,

        /// <summary>Eyedropper/pipette cursor for color sampling.</summary>
        Eyedropper,

        /// <summary>Crosshair for precise positioning.</summary>
        Crosshair
    }

    /// <summary>
    /// Strategy interface for utility tool operations (Pan, Zoom, Dropper, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Utility handlers encapsulate the input handling and state machine logic for
    /// non-painting tools. This brings utility tools into the same pattern as:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="Painting.IStrokePainter"/> for brush tools</item>
    /// <item><see cref="Shapes.IShapeBuilder"/> for shape tools</item>
    /// <item><see cref="Selection.ISelectionTool"/> for selection tools</item>
    /// </list>
    /// <para>
    /// Each utility handler manages its own state and responds to pointer events
    /// through the <see cref="IUtilityContext"/> provided during construction.
    /// </para>
    /// </remarks>
    public interface IUtilityHandler
    {
        /// <summary>
        /// Gets the unique tool identifier this handler implements.
        /// </summary>
        string ToolId { get; }

        /// <summary>
        /// Gets whether this handler is currently in an active operation (e.g., panning, sampling).
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Gets the cursor style hint for this tool.
        /// </summary>
        UtilityCursorHint CursorHint { get; }

        /// <summary>
        /// Handles pointer press events.
        /// </summary>
        /// <param name="screenX">X position in screen/view coordinates.</param>
        /// <param name="screenY">Y position in screen/view coordinates.</param>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <param name="isLeftButton">True if left button pressed.</param>
        /// <param name="isRightButton">True if right button pressed.</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerPressed(double screenX, double screenY, double docX, double docY, bool isLeftButton, bool isRightButton);

        /// <summary>
        /// Handles pointer move events.
        /// </summary>
        /// <param name="screenX">X position in screen/view coordinates.</param>
        /// <param name="screenY">Y position in screen/view coordinates.</param>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <param name="isLeftButton">True if left button held.</param>
        /// <param name="isRightButton">True if right button held.</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerMoved(double screenX, double screenY, double docX, double docY, bool isLeftButton, bool isRightButton);

        /// <summary>
        /// Handles pointer release events.
        /// </summary>
        /// <param name="screenX">X position in screen/view coordinates.</param>
        /// <param name="screenY">Y position in screen/view coordinates.</param>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <param name="isLeftButton">True if left button released.</param>
        /// <param name="isRightButton">True if right button released.</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerReleased(double screenX, double screenY, double docX, double docY, bool isLeftButton, bool isRightButton);

        /// <summary>
        /// Handles mouse wheel events.
        /// </summary>
        /// <param name="screenX">X position in screen/view coordinates.</param>
        /// <param name="screenY">Y position in screen/view coordinates.</param>
        /// <param name="delta">Wheel delta (positive = up/zoom in, negative = down/zoom out).</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerWheelChanged(double screenX, double screenY, int delta);

        /// <summary>
        /// Resets the handler state (called when tool is deactivated or operation cancelled).
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Context interface providing host services to utility handlers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Utility handlers receive this context at construction time to interact with the host
    /// application. It provides access to viewport manipulation and color sampling.
    /// </para>
    /// </remarks>
    public interface IUtilityContext
    {
        // ====================================================================
        // VIEWPORT
        // ====================================================================

        /// <summary>
        /// Gets or sets the current zoom scale factor.
        /// </summary>
        double Zoom { get; set; }

        /// <summary>
        /// Gets the minimum allowed zoom level.
        /// </summary>
        double MinZoom { get; }

        /// <summary>
        /// Gets the maximum allowed zoom level.
        /// </summary>
        double MaxZoom { get; }

        /// <summary>
        /// Gets or sets the horizontal scroll offset.
        /// </summary>
        double ScrollX { get; set; }

        /// <summary>
        /// Gets or sets the vertical scroll offset.
        /// </summary>
        double ScrollY { get; set; }

        /// <summary>
        /// Pans the viewport by the specified delta.
        /// </summary>
        /// <param name="deltaX">Horizontal pan amount in screen pixels.</param>
        /// <param name="deltaY">Vertical pan amount in screen pixels.</param>
        void Pan(double deltaX, double deltaY);

        /// <summary>
        /// Zooms the viewport centered on a screen position.
        /// </summary>
        /// <param name="factor">Zoom factor (greater than 1 = zoom in, less than 1 = zoom out).</param>
        /// <param name="centerX">Center X in screen coordinates.</param>
        /// <param name="centerY">Center Y in screen coordinates.</param>
        void ZoomAt(double factor, double centerX, double centerY);

        // ====================================================================
        // COLOR SAMPLING
        // ====================================================================

        /// <summary>
        /// Samples the color at a document position.
        /// </summary>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <returns>The BGRA color at the position, or null if out of bounds.</returns>
        uint? SampleColor(int docX, int docY);

        /// <summary>
        /// Sets the foreground color.
        /// </summary>
        /// <param name="bgra">The BGRA color value.</param>
        void SetForegroundColor(uint bgra);

        /// <summary>
        /// Sets the background color.
        /// </summary>
        /// <param name="bgra">The BGRA color value.</param>
        void SetBackgroundColor(uint bgra);

        // ====================================================================
        // CLIPBOARD
        // ====================================================================

        /// <summary>
        /// Copies text to the system clipboard.
        /// </summary>
        /// <param name="text">The text to copy.</param>
        /// <returns>True if successful; false if the operation failed.</returns>
        bool CopyToClipboard(string text);

        // ====================================================================
        // REFRESH
        // ====================================================================

        /// <summary>
        /// Requests a canvas redraw.
        /// </summary>
        void Invalidate();
    }
}
