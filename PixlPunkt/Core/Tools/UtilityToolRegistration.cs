using System;
using PixlPunkt.Core.Painting;
using PixlPunkt.Core.Tools.Selection;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.Core.Tools.Utility;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Registration record for utility-category tools that don't paint pixels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Utility tools perform viewport or state operations without modifying pixel data:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Pan</strong>: Scrolls the canvas viewport.</item>
    /// <item><strong>Zoom</strong>: Changes canvas magnification.</item>
    /// <item><strong>Dropper</strong>: Samples colors from the canvas.</item>
    /// </list>
    /// <para>
    /// Each utility tool provides an <see cref="IUtilityHandler"/> that encapsulates
    /// its input handling logic, following the same pattern as:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="IStrokePainter"/> for brush tools</item>
    /// <item><see cref="IShapeBuilder"/> for shape tools</item>
    /// <item><see cref="ISelectionTool"/> for selection tools</item>
    /// </list>
    /// </remarks>
    /// <param name="Id">Unique string identifier (e.g., "pixlpunkt.utility.pan").</param>
    /// <param name="DisplayName">Human-readable name for UI display.</param>
    /// <param name="Settings">Tool-specific settings object.</param>
    /// <param name="HandlerFactory">Factory function to create the utility handler.</param>
    public sealed partial record UtilityToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        Func<IUtilityContext, IUtilityHandler>? HandlerFactory = null
    ) : IUtilityToolRegistration, IToolBehavior
    {
        /// <summary>
        /// Gets the tool category - always Utility for utility tools.
        /// </summary>
        public ToolCategory Category => ToolCategory.Utility;

        /// <summary>
        /// Gets whether this tool has a handler implementation.
        /// </summary>
        public bool HasHandler => HandlerFactory != null;

        /// <summary>
        /// Creates a new handler instance for this tool.
        /// </summary>
        /// <param name="context">The utility context for canvas operations.</param>
        /// <returns>A new handler instance, or null if no factory is registered.</returns>
        public IUtilityHandler? CreateHandler(IUtilityContext context)
            => HandlerFactory?.Invoke(context);

        // ====================================================================
        // IToolBehavior IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        string IToolBehavior.ToolId => Id;

        /// <inheritdoc/>
        public ToolInputPattern InputPattern => Id switch
        {
            ToolIds.Dropper => ToolInputPattern.Click,
            ToolIds.Pan => ToolInputPattern.Stroke, // Drag-based panning
            ToolIds.Zoom => ToolInputPattern.Click,
            _ => ToolInputPattern.None
        };

        /// <inheritdoc/>
        public bool HandlesRightClick => Id == ToolIds.Dropper; // Dropper RMB = BG sample

        /// <inheritdoc/>
        public bool SuppressRmbDropper => true; // Utility tools suppress RMB dropper (Pan, Zoom don't need it, Dropper handles RMB itself)

        /// <inheritdoc/>
        public bool SupportsModifiers => Id == ToolIds.Zoom; // Alt+click = zoom out

        /// <inheritdoc/>
        public ToolOverlayStyle OverlayStyle => ToolOverlayStyle.None;

        /// <inheritdoc/>
        public bool OverlayVisibleWhileActive => false;

        /// <inheritdoc/>
        public bool UsesPainter => false;

        /// <inheritdoc/>
        public bool ModifiesPixels => false;
    }
}
