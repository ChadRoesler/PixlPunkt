using System;
using PixlPunkt.Uno.Core.Painting;
using PixlPunkt.Uno.Core.Tools.Settings;

namespace PixlPunkt.Uno.Core.Tools
{
    /// <summary>
    /// Registration record for shape-category tools that draw geometric primitives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shape tools create geometric forms like rectangles, ellipses, and custom shapes.
    /// The architecture separates geometry generation (<see cref="IShapeBuilder"/>) from
    /// pixel rendering (<see cref="IShapeRenderer"/>), enabling plugin extensibility:
    /// </para>
    /// <list type="bullet">
    /// <item>Custom shapes: Provide an <see cref="IShapeBuilder"/> with default renderer</item>
    /// <item>Custom rendering: Provide an <see cref="IShapeRenderer"/> for special effects</item>
    /// <item>Full control: Provide both builder and renderer</item>
    /// </list>
    /// <para>
    /// For line-based tools like Gradient, use <see cref="IStrokePainter"/> instead.
    /// </para>
    /// </remarks>
    /// <param name="Id">Unique string identifier (e.g., "pixlpunkt.shape.rect").</param>
    /// <param name="DisplayName">Human-readable name for UI display.</param>
    /// <param name="Settings">Tool-specific settings object.</param>
    /// <param name="ShapeBuilder">Shape builder for geometry generation.</param>
    /// <param name="Renderer">Shape renderer for pixel application (null = use default).</param>
    /// <param name="PainterFactory">Factory to create painter for line-based shapes (e.g., Gradient).</param>
    public sealed partial record ShapeToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        IShapeBuilder? ShapeBuilder,
        IShapeRenderer? Renderer = null,
        Func<IStrokePainter>? PainterFactory = null
    ) : IShapeToolRegistration, IToolBehavior
    {
        /// <summary>
        /// Gets the tool category - always Shape for shape tools.
        /// </summary>
        public ToolCategory Category => ToolCategory.Shape;

        /// <summary>
        /// Gets whether this tool uses a stroke painter (e.g., Gradient).
        /// </summary>
        public bool HasPainter => PainterFactory != null;

        /// <summary>
        /// Gets whether this tool uses a shape builder (e.g., Rectangle, Ellipse).
        /// </summary>
        public bool HasShapeBuilder => ShapeBuilder != null;

        /// <summary>
        /// Gets the shape renderer for this tool, falling back to the default if none specified.
        /// </summary>
        public IShapeRenderer EffectiveRenderer => Renderer ?? BrushStrokeShapeRenderer.Shared;

        /// <summary>
        /// Creates a new painter instance for this tool.
        /// </summary>
        /// <returns>A new painter instance, or null if this shape uses builder rendering.</returns>
        public IStrokePainter? CreatePainter() => PainterFactory?.Invoke();

        // ====================================================================
        // IToolBehavior IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        string IToolBehavior.ToolId => Id;

        /// <inheritdoc/>
        public ToolInputPattern InputPattern => ToolInputPattern.TwoPoint;

        /// <inheritdoc/>
        public bool HandlesRightClick => false; // Shape tools use RMB for momentary dropper

        /// <inheritdoc/>
        public bool SuppressRmbDropper => false; // Shape tools allow RMB dropper

        /// <inheritdoc/>
        public bool SupportsModifiers => true; // Shift = square/circle, Ctrl = center-out

        /// <inheritdoc/>
        public ToolOverlayStyle OverlayStyle => ToolOverlayStyle.ShapePreview;

        /// <inheritdoc/>
        public bool OverlayVisibleWhileActive => true; // Shape preview visible during drag

        /// <inheritdoc/>
        public bool UsesPainter => PainterFactory != null;

        /// <inheritdoc/>
        public bool ModifiesPixels => true;
    }
}
