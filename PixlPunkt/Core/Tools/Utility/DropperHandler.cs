using Microsoft.UI.Input;
using Windows.Foundation;

namespace PixlPunkt.Core.Tools.Utility
{
    /// <summary>
    /// Dropper tool handler - samples colors from the canvas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DropperHandler"/> handles:
    /// </para>
    /// <list type="bullet">
    /// <item>Left click/drag to sample foreground color</item>
    /// <item>Right click/drag to sample foreground color (momentary dropper from other tools)</item>
    /// <item>Alt + Right click/drag to sample background color</item>
    /// <item>Live preview during drag (fires sampling events)</item>
    /// </list>
    /// <para>
    /// The dropper preserves the current brush opacity when setting the foreground color,
    /// merging only the RGB portion of the sampled color.
    /// </para>
    /// </remarks>
    public sealed class DropperHandler : IUtilityHandler
    {
        private readonly IUtilityContext _context;
        private readonly byte _getCurrentOpacity;
        private bool _isSamplingFg;
        private bool _isSamplingBg;

        /// <summary>
        /// Creates a new DropperHandler with the specified context.
        /// </summary>
        /// <param name="context">The utility context for canvas operations.</param>
        /// <param name="getCurrentOpacity">Current brush opacity (0-255) to preserve when sampling FG.</param>
        public DropperHandler(IUtilityContext context, byte getCurrentOpacity = 255)
        {
            _context = context;
            _getCurrentOpacity = getCurrentOpacity;
        }

        /// <inheritdoc/>
        public string ToolId => ToolIds.Dropper;

        /// <inheritdoc/>
        public bool IsActive => _isSamplingFg || _isSamplingBg;

        /// <inheritdoc/>
        public UtilityCursorHint CursorHint => UtilityCursorHint.Eyedropper;

        /// <summary>
        /// Gets or sets the current brush opacity to preserve when sampling foreground.
        /// </summary>
        public byte CurrentOpacity { get; set; } = 255;

        /// <summary>
        /// Gets or sets whether Alt key is held (for BG sampling with RMB).
        /// </summary>
        public bool IsAltHeld { get; set; }

        /// <inheritdoc/>
        public bool PointerPressed(Point screenPos, Point docPos, PointerPointProperties props)
        {
            int x = (int)docPos.X;
            int y = (int)docPos.Y;

            var (w, h) = _context.DocumentSize;
            if (x < 0 || y < 0 || x >= w || y >= h)
                return false;

            if (props.IsLeftButtonPressed)
            {
                // LMB always samples FG
                _isSamplingFg = true;
                SampleForeground(x, y);
                _context.CapturePointer();
                return true;
            }

            if (props.IsRightButtonPressed)
            {
                // RMB: Alt held = BG, otherwise = FG
                if (IsAltHeld)
                {
                    _isSamplingBg = true;
                    SampleBackground(x, y);
                }
                else
                {
                    _isSamplingFg = true;
                    SampleForeground(x, y);
                }
                _context.CapturePointer();
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool PointerMoved(Point screenPos, Point docPos, PointerPointProperties props)
        {
            if (!_isSamplingFg && !_isSamplingBg)
                return false;

            int x = (int)docPos.X;
            int y = (int)docPos.Y;

            var (w, h) = _context.DocumentSize;
            if (x < 0 || y < 0 || x >= w || y >= h)
                return true; // Still consuming but not sampling

            if (_isSamplingFg)
                SampleForeground(x, y);
            else if (_isSamplingBg)
                SampleBackground(x, y);

            return true;
        }

        /// <inheritdoc/>
        public bool PointerReleased(Point screenPos, Point docPos, PointerPointProperties props)
        {
            if (_isSamplingFg && !props.IsLeftButtonPressed && !props.IsRightButtonPressed)
            {
                _isSamplingFg = false;
                _context.ReleasePointer();
                return true;
            }

            if (_isSamplingBg && !props.IsRightButtonPressed)
            {
                _isSamplingBg = false;
                _context.ReleasePointer();
                return true;
            }

            // Handle RMB release when sampling FG via RMB
            if (_isSamplingFg && !props.IsRightButtonPressed && !props.IsLeftButtonPressed)
            {
                _isSamplingFg = false;
                _context.ReleasePointer();
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool PointerWheelChanged(Point screenPos, int delta)
        {
            // Dropper doesn't handle wheel
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _isSamplingFg = false;
            _isSamplingBg = false;
        }

        /// <summary>
        /// Samples a color and sets it as foreground, preserving current opacity.
        /// </summary>
        private void SampleForeground(int x, int y)
        {
            uint sampled = _context.SampleColorAt(x, y);

            // Merge sampled RGB with current opacity
            uint merged = (sampled & 0x00FFFFFFu) | ((uint)CurrentOpacity << 24);

            _context.SetForegroundColor(merged);
        }

        /// <summary>
        /// Samples a color and sets it as background (always opaque).
        /// </summary>
        private void SampleBackground(int x, int y)
        {
            uint sampled = _context.SampleColorAt(x, y);

            // Background is always fully opaque
            uint merged = 0xFF000000u | (sampled & 0x00FFFFFFu);

            _context.SetBackgroundColor(merged);
        }
    }
}
