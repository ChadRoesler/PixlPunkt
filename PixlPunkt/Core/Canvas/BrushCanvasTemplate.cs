using PixlPunkt.Constants;

namespace PixlPunkt.Core.Canvas
{
    /// <summary>
    /// Specialized canvas template for creating custom brushes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Creates a 16x16 canvas for drawing custom brush shapes.
    /// Users draw the brush shape directly on the canvas, and the export process
    /// converts it to a 1-bit mask.
    /// </para>
    /// <para><strong>Layer Structure:</strong></para>
    /// <para>
    /// A single default layer for the 16x16 brush mask.
    /// </para>
    /// </remarks>
    public class BrushCanvasTemplate : CanvasTemplate
    {
        /// <summary>
        /// Creates a new brush canvas template.
        /// </summary>
        public BrushCanvasTemplate()
            : base(
                name: "Custom Brush",
                tileWidth: BrushExportConstants.CanvasSize,
                tileHeight: BrushExportConstants.CanvasSize,
                tileCountX: 1,
                tileCountY: 1,
                isBuiltIn: true)
        {
        }

        /// <summary>
        /// Creates a new brush canvas template with a custom name.
        /// </summary>
        /// <param name="brushName">Name for the brush/document.</param>
        public BrushCanvasTemplate(string brushName)
            : base(
                name: brushName,
                tileWidth: BrushExportConstants.CanvasSize,
                tileHeight: BrushExportConstants.CanvasSize,
                tileCountX: 1,
                tileCountY: 1,
                isBuiltIn: false)
        {
        }
    }
}
