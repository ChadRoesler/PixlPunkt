using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Tools.Builders;
using PixlPunkt.Core.Tools.Tile;
using PixlPunkt.PluginSdk.Tile;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Registers all built-in tile tools with the tool registry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tile tools perform tile-based editing operations on the canvas.
    /// This category includes:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>TileStamper</strong>: Places tiles with optional mapping via <see cref="TileStamperHandler"/></item>
    /// <item><strong>TileModifier</strong>: Offsets, rotates, and scales tile content via <see cref="TileModifierHandler"/></item>
    /// <item><strong>TileAnimation</strong>: Paints tile selections for animation reels via <see cref="TileAnimationHandler"/></item>
    /// </list>
    /// <para>
    /// Each tile tool provides an <see cref="ITileHandler"/> that encapsulates its
    /// input handling logic, following the same pattern as brush painters and shape builders.
    /// </para>
    /// <para><strong>Universal Behavior:</strong></para>
    /// <para>
    /// All tile tools support RMB tile sampling. When the user right-clicks, the tile
    /// under the cursor and its mapping are captured for subsequent operations.
    /// </para>
    /// </remarks>
    public static class BuiltInTileTools
    {
        /// <summary>
        /// Registers all built-in tile tools with the specified registry.
        /// </summary>
        /// <param name="registry">The registry to populate.</param>
        /// <param name="toolState">The ToolState instance containing settings objects.</param>
        public static void RegisterAll(IToolRegistry registry, ToolState toolState)
        {
            registry.AddTileTool(ToolIds.TileStamper)
                .WithDisplayName("Tile Stamper")
                .WithSettings(toolState.TileStamper)
                .WithHandler(ctx => new TileStamperHandler(ctx, toolState.TileStamper))
                .Register();

            registry.AddTileTool(ToolIds.TileModifier)
                .WithDisplayName("Tile Modifier")
                .WithSettings(toolState.TileModifier)
                .WithHandler(ctx => new TileModifierHandler(ctx, toolState.TileModifier))
                .Register();

            registry.AddTileTool(ToolIds.TileAnimation)
                .WithDisplayName("Tile Animation")
                .WithSettings(toolState.TileAnimation)
                .WithHandler(ctx => new TileAnimationHandler(
                    ctx,
                    toolState.TileAnimation,
                    () => (ctx as ITileAnimationContext)?.GetAnimationStateObject() as TileAnimationState))
                .Register();
        }
    }
}
