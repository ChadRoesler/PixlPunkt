using PixlPunkt.Core.Tools.Builders;
using PixlPunkt.Core.Tools.Selection;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Registers all built-in selection tools with the tool registry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Selection tools create and manipulate pixel selections on the canvas.
    /// This category includes:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Rectangle Select</strong>: Marquee selection of rectangular regions</item>
    /// <item><strong>Magic Wand</strong>: Color-based flood selection</item>
    /// <item><strong>Lasso</strong>: Freehand polygon selection</item>
    /// <item><strong>Paint Select</strong>: Brush-based selection painting</item>
    /// </list>
    /// <para>
    /// Selection tools use <see cref="ISelectionTool"/> instances created via factory
    /// functions that receive a <see cref="SelectionToolContext"/> for dependency injection.
    /// </para>
    /// </remarks>
    public static class BuiltInSelectionTools
    {
        /// <summary>
        /// Registers all built-in selection tools with the specified registry.
        /// </summary>
        /// <param name="registry">The registry to populate.</param>
        /// <param name="toolState">The ToolState instance containing settings objects.</param>
        public static void RegisterAll(IToolRegistry registry, ToolState toolState)
        {
            registry.AddSelectionTool(ToolIds.SelectRect)
                .WithDisplayName("Rectangle Select")
                .WithSettings(toolState.SelectRect)
                .WithToolFactory(ctx => new RectSelectionTool(ctx))
                .Register();

            registry.AddSelectionTool(ToolIds.Wand)
                .WithDisplayName("Magic Wand")
                .WithSettings(toolState.Wand)
                .WithToolFactory(ctx => new WandSelectionTool(ctx))
                .Register();

            registry.AddSelectionTool(ToolIds.Lasso)
                .WithDisplayName("Lasso")
                .WithSettings(toolState.Lasso)
                .WithToolFactory(ctx => new LassoSelectionTool(ctx))
                .Register();

            registry.AddSelectionTool(ToolIds.PaintSelect)
                .WithDisplayName("Paint Selection")
                .WithSettings(toolState.PaintSelect)
                .WithToolFactory(ctx => new PaintSelectionTool(ctx))
                .Register();
        }
    }
}
