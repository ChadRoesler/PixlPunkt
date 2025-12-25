using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.Selection
{
    /// <summary>
    /// Represents the mode for combining selection operations.
    /// </summary>
    public enum SelectionMode
    {
        /// <summary>Replace existing selection with new selection.</summary>
        Replace,

        /// <summary>Add new selection to existing selection (union).</summary>
        Add,

        /// <summary>Subtract new selection from existing selection.</summary>
        Subtract,

        /// <summary>Keep only the intersection of existing and new selection.</summary>
        Intersect
    }

    /// <summary>
    /// Strategy interface for selection tool operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each selection tool implements this interface to handle its unique selection logic.
    /// The host application manages pointer event routing and rendering integration.
    /// </para>
    /// <para>
    /// <strong>Lifecycle:</strong>
    /// <list type="number">
    /// <item><see cref="Configure"/> is called when the tool is activated</item>
    /// <item>Pointer events are routed through <see cref="PointerPressed"/>, <see cref="PointerMoved"/>, <see cref="PointerReleased"/></item>
    /// <item><see cref="Deactivate"/> is called when switching to another tool</item>
    /// </list>
    /// </para>
    /// </remarks>
    public interface ISelectionTool
    {
        /// <summary>
        /// Gets the unique string identifier for this selection tool.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets whether the tool has active preview geometry to render.
        /// </summary>
        bool HasPreview { get; }

        /// <summary>
        /// Gets a value indicating whether the tool is currently in an active drag operation.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Gets a value indicating whether the tool requires continuous rendering during active operations.
        /// </summary>
        bool NeedsContinuousRender { get; }

        /// <summary>
        /// Handles pointer press events to start a selection operation.
        /// </summary>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <param name="shift">True if Shift key is held.</param>
        /// <param name="ctrl">True if Ctrl key is held.</param>
        /// <param name="alt">True if Alt key is held.</param>
        /// <returns>True if the tool handled the event; false otherwise.</returns>
        bool PointerPressed(double docX, double docY, bool shift, bool ctrl, bool alt);

        /// <summary>
        /// Handles pointer move events during an active selection operation.
        /// </summary>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <param name="shift">True if Shift key is held.</param>
        /// <param name="ctrl">True if Ctrl key is held.</param>
        /// <param name="alt">True if Alt key is held.</param>
        /// <returns>True if the tool handled the event; false otherwise.</returns>
        bool PointerMoved(double docX, double docY, bool shift, bool ctrl, bool alt);

        /// <summary>
        /// Handles pointer release events to finalize a selection operation.
        /// </summary>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <param name="shift">True if Shift key is held.</param>
        /// <param name="ctrl">True if Ctrl key is held.</param>
        /// <param name="alt">True if Alt key is held.</param>
        /// <returns>True if the tool handled the event; false otherwise.</returns>
        bool PointerReleased(double docX, double docY, bool shift, bool ctrl, bool alt);

        /// <summary>
        /// Called when the tool is deactivated (user switches to another tool).
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Cancels any in-progress tool operation without committing.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Configures the tool with current settings.
        /// </summary>
        /// <param name="settings">The settings object for this tool type.</param>
        void Configure(ToolSettingsBase settings);

        /// <summary>
        /// Gets the preview points for rendering the selection preview.
        /// </summary>
        /// <returns>Collection of (x, y) points defining the preview outline, or null if no preview.</returns>
        IReadOnlyList<(double x, double y)>? GetPreviewPoints();
    }

    /// <summary>
    /// Abstract base class for selection tools providing common functionality.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extend this class to create custom selection tools. It provides:
    /// </para>
    /// <list type="bullet">
    /// <item>Default implementations for lifecycle methods</item>
    /// <item>Common state tracking (IsActive, HasPreview)</item>
    /// <item>Preview point storage</item>
    /// </list>
    /// </remarks>
    public abstract class SelectionToolBase : ISelectionTool
    {
        /// <summary>
        /// Preview points for rendering.
        /// </summary>
        protected readonly List<(double x, double y)> PreviewPoints = new();

        /// <inheritdoc/>
        public abstract string Id { get; }

        /// <inheritdoc/>
        public virtual bool HasPreview => PreviewPoints.Count > 0;

        /// <inheritdoc/>
        public bool IsActive { get; protected set; }

        /// <inheritdoc/>
        public virtual bool NeedsContinuousRender => false;

        /// <inheritdoc/>
        public abstract bool PointerPressed(double docX, double docY, bool shift, bool ctrl, bool alt);

        /// <inheritdoc/>
        public abstract bool PointerMoved(double docX, double docY, bool shift, bool ctrl, bool alt);

        /// <inheritdoc/>
        public abstract bool PointerReleased(double docX, double docY, bool shift, bool ctrl, bool alt);

        /// <inheritdoc/>
        public virtual void Deactivate()
        {
            Cancel();
        }

        /// <inheritdoc/>
        public virtual void Cancel()
        {
            IsActive = false;
            PreviewPoints.Clear();
        }

        /// <inheritdoc/>
        public virtual void Configure(ToolSettingsBase settings)
        {
            // Override in derived classes if settings are needed
        }

        /// <inheritdoc/>
        public IReadOnlyList<(double x, double y)>? GetPreviewPoints()
        {
            return HasPreview ? PreviewPoints : null;
        }
    }
}
