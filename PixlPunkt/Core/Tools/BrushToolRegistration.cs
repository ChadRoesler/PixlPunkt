using System;
using PixlPunkt.Core.Painting;
using PixlPunkt.Core.Tools.Settings;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Registration record for brush-category tools that paint strokes on the canvas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Brush tools use <see cref="IStrokePainter"/> instances to apply pixel operations
    /// along stroke paths. This includes standard brushes, erasers, blur, smudge, jumble,
    /// replacer, and fill tools.
    /// </para>
    /// <para>
    /// Some brush tools (like Fill) don't use continuous stroke painting and have a null
    /// <see cref="PainterFactory"/>. These tools use specialized methods in StrokeEngine.
    /// </para>
    /// </remarks>
    /// <param name="Id">Unique string identifier (e.g., "pixlpunkt.brush.brush").</param>
    /// <param name="DisplayName">Human-readable name for UI display.</param>
    /// <param name="Settings">Tool-specific settings object.</param>
    /// <param name="PainterFactory">Factory to create stroke painter, or null for non-stroke tools like Fill.</param>
    public sealed partial record BrushToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        Func<IStrokePainter>? PainterFactory
    ) : IToolRegistration, IToolBehavior
    {
        /// <summary>
        /// Gets the tool category - always Brush for brush tools.
        /// </summary>
        public ToolCategory Category => ToolCategory.Brush;

        /// <summary>
        /// Gets whether this tool has a stroke painter.
        /// </summary>
        public bool HasPainter => PainterFactory != null;

        /// <summary>
        /// Gets the settings as <see cref="IStrokeSettings"/> if applicable.
        /// </summary>
        public IStrokeSettings? StrokeSettings => Settings as IStrokeSettings;

        /// <summary>
        /// Creates a new painter instance for this tool.
        /// </summary>
        /// <returns>A new painter instance, or null if this tool doesn't use stroke painting.</returns>
        public IStrokePainter? CreatePainter() => PainterFactory?.Invoke();

        // ====================================================================
        // IToolBehavior IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        string IToolBehavior.ToolId => Id;

        /// <inheritdoc/>
        public ToolInputPattern InputPattern => Id == ToolIds.Fill
            ? ToolInputPattern.Click
            : ToolInputPattern.Stroke;

        /// <inheritdoc/>
        public bool HandlesRightClick => false; // All brush tools use RMB for momentary dropper

        /// <inheritdoc/>
        public bool SuppressRmbDropper => false; // Brush tools allow RMB dropper

        /// <inheritdoc/>
        public bool SupportsModifiers => false; // Brush tools don't use Shift/Ctrl constraints

        /// <inheritdoc/>
        public ToolOverlayStyle OverlayStyle => Id switch
        {
            ToolIds.Fill => ToolOverlayStyle.None,
            ToolIds.Brush => ToolOverlayStyle.FilledGhost,
            _ => ToolOverlayStyle.Outline // Eraser, Blur, Smudge, Jumble, Replacer
        };

        /// <inheritdoc/>
        public bool OverlayVisibleWhileActive => Id != ToolIds.Brush; // Outline tools stay visible while painting

        /// <inheritdoc/>
        public bool UsesPainter => PainterFactory != null;

        /// <inheritdoc/>
        public bool ModifiesPixels => true;
    }
}
