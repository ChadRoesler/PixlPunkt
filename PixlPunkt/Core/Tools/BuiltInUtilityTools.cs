using PixlPunkt.Core.Tools.Builders;
using PixlPunkt.Core.Tools.Utility;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Registers all built-in utility tools with the tool registry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Utility tools perform viewport or state operations without modifying pixel data.
    /// This category includes:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Pan</strong>: Scrolls the canvas viewport via <see cref="PanHandler"/></item>
    /// <item><strong>Zoom</strong>: Changes canvas magnification via <see cref="ZoomHandler"/></item>
    /// <item><strong>Dropper</strong>: Samples colors from the canvas via <see cref="DropperHandler"/></item>
    /// <item><strong>Symmetry</strong>: Toggles live stroke mirroring modes</item>
    /// </list>
    /// <para>
    /// Each utility tool provides an <see cref="IUtilityHandler"/> that encapsulates its
    /// input handling logic, following the same pattern as brush painters and shape builders.
    /// </para>
    /// </remarks>
    public static class BuiltInUtilityTools
    {
        /// <summary>
        /// Registers all built-in utility tools with the specified registry.
        /// </summary>
        /// <param name="registry">The registry to populate.</param>
        /// <param name="toolState">The ToolState instance containing settings objects.</param>
        public static void RegisterAll(IToolRegistry registry, ToolState toolState)
        {
            registry.AddUtilityTool(ToolIds.Pan)
                .WithDisplayName("Pan")
                .WithSettings(toolState.Pan)
                .WithHandler(ctx => new PanHandler(ctx))
                .Register();

            registry.AddUtilityTool(ToolIds.Zoom)
                .WithDisplayName("Zoom")
                .WithSettings(toolState.Zoom)
                .WithHandler(ctx => new ZoomHandler(ctx))
                .Register();

            registry.AddUtilityTool(ToolIds.Dropper)
                .WithDisplayName("Dropper")
                .WithSettings(toolState.Dropper)
                .WithHandler(ctx => new DropperHandler(ctx))
                .Register();

            // Symmetry tool - controls live stroke mirroring
            // Note: Symmetry doesn't need a handler because it modifies stroke behavior, not input handling
            registry.AddUtilityTool(ToolIds.Symmetry)
                .WithDisplayName("Symmetry")
                .WithSettings(toolState.SymmetryTool)
                .Register();
        }
    }
}
