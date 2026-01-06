using System;
using PixlPunkt.Core.Painting;
using PixlPunkt.Core.Tools.Selection;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.Core.Tools.Utility;
using PixlPunkt.PluginSdk.Tile;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Interface for tile tool registrations.
    /// </summary>
    public interface ITileToolRegistration : IToolRegistration
    {
        /// <summary>
        /// Gets whether this tool has a handler implementation.
        /// </summary>
        bool HasHandler { get; }

        /// <summary>
        /// Creates a new handler instance for this tool.
        /// </summary>
        /// <param name="context">The tile context for canvas operations.</param>
        /// <returns>A new handler instance, or null if no factory is registered.</returns>
        ITileHandler? CreateHandler(ITileContext context);
    }

    /// <summary>
    /// Registration record for tile-category tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tile tools perform tile-based editing operations:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>TileStamper</strong>: Places tiles with optional mapping data.</item>
    /// <item><strong>TileModifier</strong>: Offsets, rotates, and scales tile content.</item>
    /// </list>
    /// <para>
    /// Each tile tool provides an <see cref="ITileHandler"/> that encapsulates
    /// its input handling logic, following the same pattern as:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="IStrokePainter"/> for brush tools</item>
    /// <item><see cref="IShapeBuilder"/> for shape tools</item>
    /// <item><see cref="ISelectionTool"/> for selection tools</item>
    /// <item><see cref="IUtilityHandler"/> for utility tools</item>
    /// </list>
    /// <para><strong>Universal Behavior:</strong></para>
    /// <para>
    /// All tile tools support RMB tile sampling. When the user right-clicks, the tile
    /// under the cursor and its mapping are captured for subsequent operations.
    /// </para>
    /// </remarks>
    /// <param name="Id">Unique string identifier (e.g., "pixlpunkt.tile.stamper").</param>
    /// <param name="DisplayName">Human-readable name for UI display.</param>
    /// <param name="Settings">Tool-specific settings object.</param>
    /// <param name="HandlerFactory">Factory function to create the tile handler.</param>
    public sealed partial record TileToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        Func<ITileContext, ITileHandler>? HandlerFactory = null
    ) : ITileToolRegistration, IToolBehavior
    {
        /// <summary>
        /// Gets the tool category - always Tile for tile tools.
        /// </summary>
        public ToolCategory Category => ToolCategory.Tile;

        /// <summary>
        /// Gets whether this tool has a handler implementation.
        /// </summary>
        public bool HasHandler => HandlerFactory != null;

        /// <summary>
        /// Creates a new handler instance for this tool.
        /// </summary>
        /// <param name="context">The tile context for canvas operations.</param>
        /// <returns>A new handler instance, or null if no factory is registered.</returns>
        public ITileHandler? CreateHandler(ITileContext context)
            => HandlerFactory?.Invoke(context);

        //////////////////////////////////////////////////////////////////
        // IToolBehavior IMPLEMENTATION
        //////////////////////////////////////////////////////////////////

        /// <inheritdoc/>
        string IToolBehavior.ToolId => Id;

        /// <inheritdoc/>
        public ToolInputPattern InputPattern => Id switch
        {
            ToolIds.TileStamper => ToolInputPattern.Click, // Click to place tiles
            ToolIds.TileModifier => ToolInputPattern.Stroke, // Drag to modify
            ToolIds.TileAnimation => ToolInputPattern.Stroke, // Drag to select frame range
            _ => ToolInputPattern.Click
        };

        /// <inheritdoc/>
        public bool HandlesRightClick => true; // All tile tools handle RMB for tile sampling

        /// <inheritdoc/>
        public bool SuppressRmbDropper => true; // Tile tools use RMB for tile sampling, not color dropper

        /// <inheritdoc/>
        public bool SupportsModifiers => true; // Ctrl+click = create tile, Shift+click = no mapping

        /// <inheritdoc/>
        public ToolOverlayStyle OverlayStyle => ToolOverlayStyle.TileBoundary;

        /// <inheritdoc/>
        public bool OverlayVisibleWhileActive => true;

        /// <inheritdoc/>
        public bool UsesPainter => false;

        /// <inheritdoc/>
        public bool ModifiesPixels => true;
    }
}
