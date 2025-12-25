namespace PixlPunkt.Core.Tools
{
    // NOTE: ToolInputPattern and ToolOverlayStyle enums are defined in the SDK
    // and imported via GlobalUsings.cs for dogfooding.
    // See: PixlPunkt.PluginSdk.Enums.ToolInputPattern
    // See: PixlPunkt.PluginSdk.Enums.ToolOverlayStyle

    /// <summary>
    /// Defines tool-specific behavior hints that the UI consults for routing and display.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IToolBehavior"/> provides declarative metadata about how a tool
    /// interacts with input and renders overlays. This enables the UI layer to:
    /// </para>
    /// <list type="bullet">
    /// <item>Route pointer events without hardcoded tool ID checks</item>
    /// <item>Determine overlay visibility and style dynamically</item>
    /// <item>Support plugin tools with consistent behavior</item>
    /// </list>
    /// <para>
    /// Tool registrations implement this interface alongside <see cref="IToolRegistration"/>.
    /// </para>
    /// </remarks>
    public interface IToolBehavior
    {
        // ====================================================================
        // IDENTITY
        // ====================================================================

        /// <summary>
        /// Gets the tool ID this behavior describes.
        /// </summary>
        /// <value>
        /// The same ID as the associated <see cref="IToolRegistration.Id"/>.
        /// </value>
        string ToolId { get; }

        // ====================================================================
        // INPUT PATTERN
        // ====================================================================

        /// <summary>
        /// Gets the input interaction pattern for this tool.
        /// </summary>
        /// <value>
        /// A <see cref="ToolInputPattern"/> indicating how pointer events
        /// should be routed (click, stroke, two-point drag, etc.).
        /// </value>
        ToolInputPattern InputPattern { get; }

        /// <summary>
        /// Gets whether the tool handles right-click specially.
        /// </summary>
        /// <value>
        /// <c>true</c> if the tool has custom RMB behavior (e.g., Dropper BG sampling);
        /// <c>false</c> if RMB should trigger momentary dropper for FG sampling.
        /// </value>
        bool HandlesRightClick { get; }

        /// <summary>
        /// Gets whether the tool suppresses the RMB momentary dropper behavior.
        /// </summary>
        /// <value>
        /// <c>true</c> to disable RMB dropper (e.g., for utility tools like Pan, Zoom);
        /// <c>false</c> (default) to allow RMB dropper for quick color sampling.
        /// </value>
        /// <remarks>
        /// This is separate from <see cref="HandlesRightClick"/>. A tool may:
        /// <list type="bullet">
        /// <item>Handle RMB (HandlesRightClick=true) and NOT suppress dropper - RMB does tool action</item>
        /// <item>NOT handle RMB (HandlesRightClick=false) and suppress dropper - RMB does nothing</item>
        /// <item>Both false - RMB triggers momentary dropper (default brush tool behavior)</item>
        /// </list>
        /// Typically set to true for utility tools and selection tools where RMB dropper would be unexpected.
        /// </remarks>
        bool SuppressRmbDropper { get; }

        /// <summary>
        /// Gets whether the tool supports Shift/Ctrl modifiers for constraints.
        /// </summary>
        /// <value>
        /// <c>true</c> for shape tools (Shift = square/circle, Ctrl = center-out);
        /// <c>false</c> for most other tools.
        /// </value>
        bool SupportsModifiers { get; }

        // ====================================================================
        // OVERLAY BEHAVIOR
        // ====================================================================

        /// <summary>
        /// Gets the overlay rendering style for this tool.
        /// </summary>
        /// <value>
        /// A <see cref="ToolOverlayStyle"/> indicating how to render the
        /// brush cursor overlay when hovering.
        /// </value>
        ToolOverlayStyle OverlayStyle { get; }

        /// <summary>
        /// Gets whether the overlay remains visible during an active operation.
        /// </summary>
        /// <value>
        /// <c>true</c> for outline tools that show cursor while painting;
        /// <c>false</c> for Brush (hides overlay during stroke).
        /// </value>
        bool OverlayVisibleWhileActive { get; }

        // ====================================================================
        // ENGINE ROUTING
        // ====================================================================

        /// <summary>
        /// Gets whether the tool uses <see cref="Painting.IStrokePainter"/> for strokes.
        /// </summary>
        /// <value>
        /// <c>true</c> for painter-based tools (Brush, Eraser, Blur, etc.);
        /// <c>false</c> for tools that use specialized engine methods (Fill, Shapes).
        /// </value>
        bool UsesPainter { get; }

        /// <summary>
        /// Gets whether the tool modifies pixel data.
        /// </summary>
        /// <value>
        /// <c>true</c> for painting/editing tools;
        /// <c>false</c> for viewport-only tools (Pan, Zoom, Dropper).
        /// </value>
        bool ModifiesPixels { get; }
    }
}
