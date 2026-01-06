using PixlPunkt.Uno.Core.Painting.Painters;
using PixlPunkt.Uno.Core.Tools.Builders;

namespace PixlPunkt.Uno.Core.Tools
{
    /// <summary>
    /// Registers all built-in brush tools with the tool registry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Brush tools paint pixels along stroke paths using <see cref="Painting.IStrokePainter"/>
    /// implementations. This category includes:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Brush</strong>: Standard painting brush</item>
    /// <item><strong>Eraser</strong>: Removes pixels (paints transparency)</item>
    /// <item><strong>Replacer</strong>: Replaces foreground color with background</item>
    /// <item><strong>Blur</strong>: Applies gaussian blur along stroke</item>
    /// <item><strong>Jumble</strong>: Randomizes/scrambles pixels</item>
    /// <item><strong>Smudge</strong>: Smears colors along stroke direction</item>
    /// <item><strong>Gradient</strong>: Cycles through palette colors for highlights/shadows</item>
    /// <item><strong>Fill</strong>: Flood fill or global color replacement</item>
    /// </list>
    /// </remarks>
    public static class BuiltInBrushTools
    {
        /// <summary>
        /// Registers all built-in brush tools with the specified registry.
        /// </summary>
        /// <param name="registry">The registry to populate.</param>
        /// <param name="toolState">The ToolState instance containing settings objects.</param>
        public static void RegisterAll(IToolRegistry registry, ToolState toolState)
        {
            registry.AddBrushTool(ToolIds.Brush)
                .WithDisplayName("Brush")
                .WithSettings(toolState.BrushTool)
                .WithPainter(() => new BrushPainter(toolState.BrushTool))
                .Register();

            registry.AddBrushTool(ToolIds.Eraser)
                .WithDisplayName("Eraser")
                .WithSettings(toolState.Eraser)
                .WithPainter(() => new EraserPainter(toolState.Eraser))
                .Register();

            registry.AddBrushTool(ToolIds.Replacer)
                .WithDisplayName("Replacer")
                .WithSettings(toolState.Replacer)
                .WithPainter(() => new ReplacerPainter(toolState.Replacer))
                .Register();

            registry.AddBrushTool(ToolIds.Blur)
                .WithDisplayName("Blur")
                .WithSettings(toolState.Blur)
                .WithPainter(() => new BlurPainter(toolState.Blur))
                .Register();

            registry.AddBrushTool(ToolIds.Jumble)
                .WithDisplayName("Jumble")
                .WithSettings(toolState.Jumble)
                .WithPainter(() => new JumblePainter(toolState.Jumble))
                .Register();

            registry.AddBrushTool(ToolIds.Smudge)
                .WithDisplayName("Smudge")
                .WithSettings(toolState.Smudge)
                .WithPainter(() => new SmudgePainter(toolState.Smudge))
                .Register();

            registry.AddBrushTool(ToolIds.Gradient)
                .WithDisplayName("Gradient")
                .WithSettings(toolState.Gradient)
                .WithPainter(() => new GradientPainter(toolState.Gradient))
                .Register();

            // Gradient Fill tool - drag-based gradient rendering with dithering
            registry.Register(new GradientFillToolRegistration(toolState.GradientFill));

            // Fill uses fluent builder - FillPainter defaults to FloodFillPainter.Shared
            registry.AddFillTool(ToolIds.Fill)
                .WithDisplayName("Fill")
                .WithSettings(toolState.Fill)
                .Register();
        }
    }
}
