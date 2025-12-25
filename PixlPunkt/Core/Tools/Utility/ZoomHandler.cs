using Microsoft.UI.Input;
using Windows.Foundation;

namespace PixlPunkt.Core.Tools.Utility
{
    /// <summary>
    /// Zoom tool handler - provides viewport zooming via click and wheel operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ZoomHandler"/> handles:
    /// </para>
    /// <list type="bullet">
    /// <item>Left click to zoom in at cursor position</item>
    /// <item>Right click (or Alt+Left click) to zoom out at cursor position</item>
    /// <item>Mouse wheel to zoom in/out at cursor position</item>
    /// </list>
    /// </remarks>
    public sealed class ZoomHandler : IUtilityHandler
    {
        private readonly IUtilityContext _context;

        /// <summary>Default zoom in factor per click.</summary>
        private const double ZoomInFactor = 2.0;

        /// <summary>Default zoom out factor per click.</summary>
        private const double ZoomOutFactor = 0.5;

        /// <summary>Zoom factor per wheel notch.</summary>
        private const double WheelZoomIn = 1.1;

        /// <summary>Zoom factor per wheel notch (out).</summary>
        private const double WheelZoomOut = 0.9;

        /// <summary>
        /// Creates a new ZoomHandler with the specified context.
        /// </summary>
        /// <param name="context">The utility context for canvas operations.</param>
        public ZoomHandler(IUtilityContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public string ToolId => ToolIds.Zoom;

        /// <inheritdoc/>
        public bool IsActive => false; // Zoom is instant, not a drag operation

        /// <inheritdoc/>
        public UtilityCursorHint CursorHint => UtilityCursorHint.ZoomIn;

        /// <inheritdoc/>
        public bool PointerPressed(Point screenPos, Point docPos, PointerPointProperties props)
        {
            if (props.IsLeftButtonPressed)
            {
                // Alt+LMB or RMB = zoom out, LMB alone = zoom in
                bool zoomOut = props.IsRightButtonPressed || IsAltDown();
                double factor = zoomOut ? ZoomOutFactor : ZoomInFactor;

                _context.ZoomAt(screenPos, factor);
                _context.RequestRedraw();
                return true;
            }

            if (props.IsRightButtonPressed)
            {
                // RMB alone = zoom out
                _context.ZoomAt(screenPos, ZoomOutFactor);
                _context.RequestRedraw();
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool PointerMoved(Point screenPos, Point docPos, PointerPointProperties props)
        {
            // Zoom tool doesn't have drag behavior
            return false;
        }

        /// <inheritdoc/>
        public bool PointerReleased(Point screenPos, Point docPos, PointerPointProperties props)
        {
            // Nothing to release for zoom
            return false;
        }

        /// <inheritdoc/>
        public bool PointerWheelChanged(Point screenPos, int delta)
        {
            double factor = delta > 0 ? WheelZoomIn : WheelZoomOut;
            _context.ZoomAt(screenPos, factor);
            _context.RequestRedraw();
            return true;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            // Nothing to reset for zoom
        }

        private static bool IsAltDown()
        {
            var state = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            return state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        }
    }
}
