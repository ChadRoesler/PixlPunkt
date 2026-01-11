using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Painting.Painters;
using PixlPunkt.Core.Tools.Settings;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Registration for the Gradient Fill tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Gradient Fill tool is a drag-based tool (like shape tools) that fills
    /// an area with a gradient. The user drags from a start point to an end point
    /// to define the gradient direction and extent.
    /// </para>
    /// </remarks>
    public sealed class GradientFillToolRegistration : IToolRegistration
    {
        private readonly GradientFillToolSettings _settings;
        private readonly GradientFillPainter _painter;

        /// <summary>
        /// Creates a new gradient fill tool registration.
        /// </summary>
        /// <param name="settings">The tool settings.</param>
        public GradientFillToolRegistration(GradientFillToolSettings settings)
        {
            _settings = settings;
            _painter = new GradientFillPainter();
        }

        /// <inheritdoc/>
        public string Id => ToolIds.GradientFill;

        /// <inheritdoc/>
        public string DisplayName => "Gradient Fill";

        /// <inheritdoc/>
        public ToolCategory Category => ToolCategory.Brush;

        /// <inheritdoc/>
        public ToolSettingsBase? Settings => _settings;

        /// <summary>
        /// Gets the gradient fill painter.
        /// </summary>
        public GradientFillPainter Painter => _painter;
    }
}
