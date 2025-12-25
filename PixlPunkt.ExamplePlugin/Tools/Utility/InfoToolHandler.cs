using PixlPunkt.PluginSdk.Utility;

namespace PixlPunkt.ExamplePlugin.Tools.Utility
{
    /// <summary>
    /// A utility handler that displays information about the pixel under the cursor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an example implementation of <see cref="IUtilityHandler"/> that demonstrates:
    /// </para>
    /// <list type="bullet">
    /// <item>Implementing viewport-only tools</item>
    /// <item>Using the utility context for color sampling</item>
    /// <item>Providing cursor hints</item>
    /// <item>Displaying hex color values in the settings panel</item>
    /// </list>
    /// <para>
    /// The info tool samples colors and displays the hex value in the toolbar.
    /// When hovering with "Sample on Hover" enabled, the hex value updates live.
    /// Left-clicking copies the hex value to the clipboard.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> This tool does NOT handle RMB since RMB is universally
    /// reserved for the momentary dropper tool across all tools.
    /// </para>
    /// </remarks>
    public sealed class InfoToolHandler : IUtilityHandler
    {
        private readonly IUtilityContext _context;
        private readonly InfoToolSettings _settings;

        private double _lastDocX, _lastDocY;
        private uint? _lastSampledColor;

        /// <summary>
        /// Creates a new info tool handler.
        /// </summary>
        /// <param name="context">The utility context for canvas operations.</param>
        /// <param name="settings">The tool settings.</param>
        public InfoToolHandler(IUtilityContext context, InfoToolSettings settings)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc/>
        public string ToolId => "pixlpunkt.example.utility.info";

        /// <inheritdoc/>
        public bool IsActive { get; private set; }

        /// <inheritdoc/>
        public UtilityCursorHint CursorHint => UtilityCursorHint.Crosshair;

        /// <summary>
        /// Gets the last sampled document X coordinate.
        /// </summary>
        public double LastDocX => _lastDocX;

        /// <summary>
        /// Gets the last sampled document Y coordinate.
        /// </summary>
        public double LastDocY => _lastDocY;

        /// <summary>
        /// Gets the last sampled color (BGRA format), or null if no valid sample.
        /// </summary>
        public uint? LastSampledColor => _lastSampledColor;

        /// <inheritdoc/>
        public bool PointerPressed(double screenX, double screenY, double docX, double docY, bool isLeftButton, bool isRightButton)
        {
            // Only handle LMB - RMB is reserved for the universal momentary dropper
            if (!isLeftButton) return false;

            IsActive = true;
            SampleAt(docX, docY);

            // Copy hex value to clipboard on LMB click
            if (_lastSampledColor.HasValue)
            {
                string hex = FormatHex(_lastSampledColor.Value, _settings.ShowHexValues);
                _context.CopyToClipboard(hex);
            }

            // If continuous sampling is enabled, we'll keep sampling on move while dragging
            if (_settings.ContinuousSample)
            {
                return true;
            }

            // Single sample mode - we're done after press
            IsActive = false;
            return true;
        }

        /// <inheritdoc/>
        public bool PointerMoved(double screenX, double screenY, double docX, double docY, bool isLeftButton, bool isRightButton)
        {
            // Always track position for display
            _lastDocX = docX;
            _lastDocY = docY;

            // Update position in settings panel
            _settings.PositionOutput = $"X:{(int)docX} Y:{(int)docY}";

            // In continuous mode while LMB is held, sample while dragging
            if (IsActive && _settings.ContinuousSample && isLeftButton)
            {
                SampleAt(docX, docY);
                return true;
            }

            // Sample on hover if enabled (regardless of button state)
            if (_settings.SampleOnHover)
            {
                SampleAt(docX, docY);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool PointerReleased(double screenX, double screenY, double docX, double docY, bool isLeftButton, bool isRightButton)
        {
            if (!IsActive) return false;

            SampleAt(docX, docY);
            IsActive = false;
            return true;
        }

        /// <inheritdoc/>
        public bool PointerWheelChanged(double screenX, double screenY, int delta)
        {
            // Info tool doesn't handle wheel events
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            IsActive = false;
            _lastSampledColor = null;
            _settings.HexOutput = "#------";
            _settings.PositionOutput = "X:-- Y:--";
        }

        /// <summary>
        /// Samples the color at the specified document position and updates the settings display.
        /// </summary>
        private void SampleAt(double docX, double docY)
        {
            _lastDocX = docX;
            _lastDocY = docY;

            int x = (int)Math.Round(docX);
            int y = (int)Math.Round(docY);

            _lastSampledColor = _context.SampleColor(x, y);

            // Update the settings panel display
            if (_lastSampledColor.HasValue)
            {
                _settings.HexOutput = FormatHex(_lastSampledColor.Value, _settings.ShowHexValues);
            }
            else
            {
                _settings.HexOutput = "#------";
            }
        }

        /// <summary>
        /// Formats a BGRA color as a hex string.
        /// </summary>
        /// <param name="bgra">The BGRA color value.</param>
        /// <param name="includeAlpha">If true, includes alpha channel in output.</param>
        /// <returns>Hex string in format "#RRGGBB" or "#AARRGGBB".</returns>
        private static string FormatHex(uint bgra, bool includeAlpha)
        {
            byte b = (byte)(bgra & 0xFF);
            byte g = (byte)((bgra >> 8) & 0xFF);
            byte r = (byte)((bgra >> 16) & 0xFF);
            byte a = (byte)((bgra >> 24) & 0xFF);

            return includeAlpha
                ? $"#{a:X2}{r:X2}{g:X2}{b:X2}"
                : $"#{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>
        /// Gets formatted color info string for the last sampled color.
        /// </summary>
        public string GetColorInfo()
        {
            if (_lastSampledColor == null)
                return "No color sampled";

            uint color = _lastSampledColor.Value;
            byte b = (byte)(color & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte r = (byte)((color >> 16) & 0xFF);
            byte a = (byte)((color >> 24) & 0xFF);

            return _settings.ShowHexValues
                ? $"R:{r} G:{g} B:{b} A:{a} | #{r:X2}{g:X2}{b:X2}"
                : $"R:{r} G:{g} B:{b} A:{a}";
        }

        /// <summary>
        /// Gets formatted position info string.
        /// </summary>
        public string GetPositionInfo()
        {
            return $"X:{(int)_lastDocX} Y:{(int)_lastDocY}";
        }
    }
}
