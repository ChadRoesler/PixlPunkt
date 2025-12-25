using PixlPunkt.PluginSdk.Selection;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.ExamplePlugin.Tools.Selection
{
    /// <summary>
    /// An ellipse/circle selection tool demonstrating the selection tool SDK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an example implementation of <see cref="SelectionToolBase"/> that demonstrates:
    /// </para>
    /// <list type="bullet">
    /// <item>Creating custom selection tools</item>
    /// <item>Generating preview geometry for marching ants</item>
    /// <item>Handling modifier keys (Shift for circle, Ctrl for center-out)</item>
    /// </list>
    /// <para>
    /// <strong>Note:</strong> This tool only provides the preview outline. The host application
    /// is responsible for applying the selection to the actual selection region.
    /// </para>
    /// </remarks>
    public sealed class EllipseSelectTool : SelectionToolBase
    {
        private EllipseSelectSettings? _settings;
        private double _startX, _startY;
        private double _endX, _endY;
        private bool _shiftHeld, _ctrlHeld;

        /// <inheritdoc/>
        public override string Id => "pixlpunkt.example.select.ellipse";

        /// <inheritdoc/>
        public override bool PointerPressed(double docX, double docY, bool shift, bool ctrl, bool alt)
        {
            IsActive = true;
            _startX = docX;
            _startY = docY;
            _endX = docX;
            _endY = docY;
            _shiftHeld = shift;
            _ctrlHeld = ctrl;

            UpdatePreview();
            return true;
        }

        /// <inheritdoc/>
        public override bool PointerMoved(double docX, double docY, bool shift, bool ctrl, bool alt)
        {
            if (!IsActive) return false;

            _endX = docX;
            _endY = docY;
            _shiftHeld = shift;
            _ctrlHeld = ctrl;

            UpdatePreview();
            return true;
        }

        /// <inheritdoc/>
        public override bool PointerReleased(double docX, double docY, bool shift, bool ctrl, bool alt)
        {
            if (!IsActive) return false;

            _endX = docX;
            _endY = docY;
            _shiftHeld = shift;
            _ctrlHeld = ctrl;

            UpdatePreview();
            IsActive = false;
            return true;
        }

        /// <inheritdoc/>
        public override void Configure(ToolSettingsBase settings)
        {
            if (settings is EllipseSelectSettings ellipseSettings)
            {
                _settings = ellipseSettings;
            }
        }

        /// <summary>
        /// Updates the preview points to form an ellipse outline.
        /// </summary>
        private void UpdatePreview()
        {
            PreviewPoints.Clear();

            // Calculate bounds with modifiers
            var (x0, y0, x1, y1) = ApplyModifiers(_startX, _startY, _endX, _endY, _shiftHeld, _ctrlHeld);

            // Don't generate preview for degenerate shapes
            double width = Math.Abs(x1 - x0);
            double height = Math.Abs(y1 - y0);
            if (width < 1 || height < 1) return;

            // Calculate ellipse parameters
            double centerX = (x0 + x1) / 2.0;
            double centerY = (y0 + y1) / 2.0;
            double radiusX = width / 2.0;
            double radiusY = height / 2.0;

            // Generate ellipse outline points
            // Use more points for larger ellipses
            int segments = Math.Max(32, (int)(Math.Max(radiusX, radiusY) * 2));
            double angleStep = 2 * Math.PI / segments;

            for (int i = 0; i < segments; i++)
            {
                double angle = i * angleStep;
                double px = centerX + radiusX * Math.Cos(angle);
                double py = centerY + radiusY * Math.Sin(angle);
                PreviewPoints.Add((px, py));
            }

            // Close the ellipse by connecting back to start
            if (PreviewPoints.Count > 0)
            {
                PreviewPoints.Add(PreviewPoints[0]);
            }
        }

        /// <summary>
        /// Applies modifier key transformations to the selection bounds.
        /// </summary>
        private static (double x0, double y0, double x1, double y1) ApplyModifiers(
            double startX, double startY, double endX, double endY, bool shift, bool ctrl)
        {
            double x0 = startX, y0 = startY, x1 = endX, y1 = endY;

            // Shift = constrain to circle (equal width/height)
            if (shift)
            {
                double dx = x1 - x0;
                double dy = y1 - y0;
                double maxDim = Math.Max(Math.Abs(dx), Math.Abs(dy));

                x1 = x0 + (dx >= 0 ? maxDim : -maxDim);
                y1 = y0 + (dy >= 0 ? maxDim : -maxDim);
            }

            // Ctrl = draw from center outward
            if (ctrl)
            {
                double dx = x1 - x0;
                double dy = y1 - y0;
                x0 = startX - dx;
                y0 = startY - dy;
                x1 = startX + dx;
                y1 = startY + dy;
            }

            return (Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
        }
    }
}
