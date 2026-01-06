using PixlPunkt.Uno.Core.Painting;
using PixlPunkt.Uno.Core.Tools.Settings;

namespace PixlPunkt.Uno.Core.Tools
{
    /// <summary>
    /// Registration record for fill-category tools that perform flood fill or global color replacement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fill tools replace colors at a clicked point using either contiguous flood fill
    /// or global color replacement. The architecture uses <see cref="IFillPainter"/> for
    /// plugin extensibility:
    /// </para>
    /// <list type="bullet">
    /// <item>Custom fills: Provide an <see cref="IFillPainter"/> for pattern/gradient fills</item>
    /// <item>Default: Uses <see cref="FloodFillPainter"/> for standard flood/global fill</item>
    /// </list>
    /// </remarks>
    /// <param name="Id">Unique string identifier (e.g., "pixlpunkt.utility.fill").</param>
    /// <param name="DisplayName">Human-readable name for UI display.</param>
    /// <param name="Settings">Tool-specific settings object.</param>
    /// <param name="FillPainter">Fill painter implementation (null = use default).</param>
    public sealed partial record FillToolRegistration(
        string Id,
        string DisplayName,
        ToolSettingsBase? Settings,
        IFillPainter? FillPainter = null
    ) : IToolRegistration, IToolBehavior
    {
        /// <summary>
        /// Gets the tool category - Utility for fill tools.
        /// </summary>
        public ToolCategory Category => ToolCategory.Utility;

        /// <summary>
        /// Gets the effective fill painter, falling back to default if none specified.
        /// </summary>
        public IFillPainter EffectiveFillPainter => FillPainter ?? FloodFillPainter.Shared;

        // ====================================================================
        // IToolBehavior IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        string IToolBehavior.ToolId => Id;

        /// <inheritdoc/>
        public ToolInputPattern InputPattern => ToolInputPattern.Click;

        /// <inheritdoc/>
        public bool HandlesRightClick => false;

        /// <inheritdoc/>
        public bool SuppressRmbDropper => false; // Fill tool allows RMB dropper for color picking

        /// <inheritdoc/>
        public bool SupportsModifiers => false;

        /// <inheritdoc/>
        public ToolOverlayStyle OverlayStyle => ToolOverlayStyle.None;

        /// <inheritdoc/>
        public bool OverlayVisibleWhileActive => false;

        /// <inheritdoc/>
        public bool UsesPainter => false;

        /// <inheritdoc/>
        public bool ModifiesPixels => true;
    }
}
