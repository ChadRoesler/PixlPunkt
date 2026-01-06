using Microsoft.UI.Input;
using Windows.Foundation;

namespace PixlPunkt.Uno.Core.Tools.Utility
{
    /// <summary>
    /// Pan tool handler - provides viewport panning via drag operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="PanHandler"/> handles:
    /// </para>
    /// <list type="bullet">
    /// <item>Middle mouse button (MMB) drag for panning</item>
    /// <item>Left mouse button (LMB) drag when Pan tool is active</item>
    /// <item>Spacebar-held panning (via tool override system)</item>
    /// </list>
    /// <para>
    /// The handler tracks the last screen position and computes deltas for smooth panning.
    /// </para>
    /// </remarks>
    public sealed class PanHandler : IUtilityHandler
    {
        private readonly IUtilityContext _context;
        private bool _isPanning;
        private Point _lastScreenPos;

        /// <summary>
        /// Creates a new PanHandler with the specified context.
        /// </summary>
        /// <param name="context">The utility context for canvas operations.</param>
        public PanHandler(IUtilityContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public string ToolId => ToolIds.Pan;

        /// <inheritdoc/>
        public bool IsActive => _isPanning;

        /// <inheritdoc/>
        public UtilityCursorHint CursorHint => _isPanning ? UtilityCursorHint.Grabbing : UtilityCursorHint.Hand;

        /// <inheritdoc/>
        public bool PointerPressed(Point screenPos, Point docPos, PointerPointProperties props)
        {
            // Pan on LMB (when Pan tool is active) or MMB (always)
            if (props.IsLeftButtonPressed || props.IsMiddleButtonPressed)
            {
                _isPanning = true;
                _lastScreenPos = screenPos;
                _context.CapturePointer();
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool PointerMoved(Point screenPos, Point docPos, PointerPointProperties props)
        {
            if (!_isPanning)
                return false;

            double deltaX = screenPos.X - _lastScreenPos.X;
            double deltaY = screenPos.Y - _lastScreenPos.Y;

            _context.PanBy(deltaX, deltaY);
            _lastScreenPos = screenPos;
            _context.RequestRedraw();

            return true;
        }

        /// <inheritdoc/>
        public bool PointerReleased(Point screenPos, Point docPos, PointerPointProperties props)
        {
            if (!_isPanning)
                return false;

            // Check if the button that started panning was released
            if (!props.IsLeftButtonPressed && !props.IsMiddleButtonPressed)
            {
                _isPanning = false;
                _context.ReleasePointer();
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool PointerWheelChanged(Point screenPos, int delta)
        {
            // Pan tool doesn't handle wheel - let zoom handle it
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _isPanning = false;
        }
    }
}
