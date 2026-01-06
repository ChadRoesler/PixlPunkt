using Microsoft.UI.Input;
using PixlPunkt.Uno.Core.Painting;
using PixlPunkt.Uno.Core.Tools.Selection;
using Windows.Foundation;

namespace PixlPunkt.Uno.Core.Tools.Utility
{
    /// <summary>
    /// Strategy interface for utility tool operations (Pan, Zoom, Dropper, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Utility handlers encapsulate the input handling and state machine logic for
    /// non-painting tools. This brings utility tools into the same pattern as:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="IStrokePainter"/> for brush tools</item>
    /// <item><see cref="IShapeBuilder"/> for shape tools</item>
    /// <item><see cref="ISelectionTool"/> for selection tools</item>
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
        /// <param name="screenPos">Position in screen/view coordinates.</param>
        /// <param name="docPos">Position in document pixel coordinates.</param>
        /// <param name="props">Pointer button properties.</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerPressed(Point screenPos, Point docPos, PointerPointProperties props);

        /// <summary>
        /// Handles pointer move events.
        /// </summary>
        /// <param name="screenPos">Position in screen/view coordinates.</param>
        /// <param name="docPos">Position in document pixel coordinates.</param>
        /// <param name="props">Pointer button properties.</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerMoved(Point screenPos, Point docPos, PointerPointProperties props);

        /// <summary>
        /// Handles pointer release events.
        /// </summary>
        /// <param name="screenPos">Position in screen/view coordinates.</param>
        /// <param name="docPos">Position in document pixel coordinates.</param>
        /// <param name="props">Pointer button properties.</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerReleased(Point screenPos, Point docPos, PointerPointProperties props);

        /// <summary>
        /// Handles mouse wheel events.
        /// </summary>
        /// <param name="screenPos">Position in screen/view coordinates.</param>
        /// <param name="delta">Wheel delta (positive = up/zoom in, negative = down/zoom out).</param>
        /// <returns>True if the handler consumed the event; false to allow fallthrough.</returns>
        bool PointerWheelChanged(Point screenPos, int delta);

        /// <summary>
        /// Resets the handler state (called when tool is deactivated or operation cancelled).
        /// </summary>
        void Reset();
    }

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
}
