using PixlPunkt.Uno.Core.Tools.Builders;
using PixlPunkt.Uno.Core.Tools.Shapes;

namespace PixlPunkt.Uno.Core.Tools
{
    /// <summary>
    /// Registers all built-in shape tools with the tool registry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shape tools draw geometric primitives on the canvas. This category includes:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Rectangle</strong>: Draws axis-aligned rectangles (filled or outlined) via <see cref="RectangleShapeBuilder"/></item>
    /// <item><strong>Ellipse</strong>: Draws ellipses and circles (filled or outlined) via <see cref="EllipseShapeBuilder"/></item>
    /// </list>
    /// <para>
    /// Shape tools use <see cref="IShapeBuilder"/> for geometry generation and
    /// <see cref="Painting.IShapeRenderer"/> for pixel application. The default renderer
    /// (<see cref="Painting.BrushStrokeShapeRenderer"/>) provides brush-stroked rendering with
    /// proper alpha blending and density falloff.
    /// </para>
    /// </remarks>
    public static class BuiltInShapeTools
    {
        /// <summary>
        /// Registers all built-in shape tools with the specified registry.
        /// </summary>
        /// <param name="registry">The registry to populate.</param>
        /// <param name="toolState">The ToolState instance containing settings objects.</param>
        public static void RegisterAll(IToolRegistry registry, ToolState toolState)
        {
            registry.AddShapeTool(ToolIds.ShapeRect)
                .WithDisplayName("Rectangle")
                .WithSettings(toolState.Rect)
                .WithShapeBuilder(new RectangleShapeBuilder())
                .Register();

            registry.AddShapeTool(ToolIds.ShapeEllipse)
                .WithDisplayName("Ellipse")
                .WithSettings(toolState.Ellipse)
                .WithShapeBuilder(new EllipseShapeBuilder())
                .Register();
        }
    }
}
