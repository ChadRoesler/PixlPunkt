using System;
using PixlPunkt.Uno.Core.Tools.Selection;
using PixlPunkt.Uno.Core.Tools.Settings;

namespace PixlPunkt.Uno.Core.Tools
{
    /// <summary>
    /// Registration record for a selection tool, containing all information needed to instantiate and use the tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SelectionToolRegistration"/> encapsulates:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Id</strong>: Unique string identifier (e.g., "pixlpunkt.select.wand").</item>
    /// <item><strong>DisplayName</strong>: Human-readable name shown in UI.</item>
    /// <item><strong>Settings</strong>: The tool's configuration object (e.g., WandToolSettings).</item>
    /// <item><strong>ToolFactory</strong>: Factory function to create the selection tool instance.</item>
    /// </list>
    /// </remarks>
    /// <param name="Id">Unique string identifier following vendor.category.name convention.</param>
    /// <param name="DisplayName">Human-readable name for UI display.</param>
    /// <param name="Settings">The tool's settings object (may be null for tools without settings).</param>
    /// <param name="ToolFactory">
    /// Factory function that creates an <see cref="ISelectionTool"/> for this tool.
    /// Receives <see cref="SelectionToolContext"/> for dependency injection.
    /// </param>
    public sealed partial record SelectionToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        Func<SelectionToolContext, ISelectionTool> ToolFactory
    ) : ISelectionToolRegistration, IToolBehavior
    {
        /// <summary>
        /// Gets the tool category - always Select for selection tools.
        /// </summary>
        public ToolCategory Category => ToolCategory.Select;

        /// <summary>
        /// Gets whether this registration has a valid tool factory.
        /// </summary>
        public bool HasTool => ToolFactory != null;

        /// <summary>
        /// Creates a new selection tool instance using the provided context.
        /// </summary>
        /// <param name="context">Context providing dependencies for the tool.</param>
        /// <returns>A new selection tool instance, or null if no factory is defined.</returns>
        public ISelectionTool? CreateTool(SelectionToolContext context)
            => ToolFactory?.Invoke(context);

        // ====================================================================
        // IToolBehavior IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        string IToolBehavior.ToolId => Id;

        /// <inheritdoc/>
        public ToolInputPattern InputPattern => Id switch
        {
            ToolIds.Wand => ToolInputPattern.Click,
            ToolIds.Lasso => ToolInputPattern.Custom, // Multi-click polygon
            _ => ToolInputPattern.Custom // SelectRect, PaintSelect have complex handling
        };

        /// <inheritdoc/>
        public bool HandlesRightClick => false; // Selection tools don't handle RMB specially

        /// <inheritdoc/>
        public bool SuppressRmbDropper => true; // Selection tools suppress RMB dropper

        /// <inheritdoc/>
        public bool SupportsModifiers => true; // Shift = add, Alt = subtract

        /// <inheritdoc/>
        public ToolOverlayStyle OverlayStyle => Id == ToolIds.PaintSelect
            ? ToolOverlayStyle.Outline // PaintSelect shows brush outline
            : ToolOverlayStyle.None;   // Other selection tools have custom marching ants

        /// <inheritdoc/>
        public bool OverlayVisibleWhileActive => Id == ToolIds.PaintSelect;

        /// <inheritdoc/>
        public bool UsesPainter => false; // Selection tools don't use stroke painters

        /// <inheritdoc/>
        public bool ModifiesPixels => false; // Selection modifies region, not pixels directly
    }
}
