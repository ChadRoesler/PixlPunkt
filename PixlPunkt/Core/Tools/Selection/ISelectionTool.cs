using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Rendering;
using PixlPunkt.Core.Tools.Settings;
using Windows.Foundation;

namespace PixlPunkt.Core.Tools.Selection
{
    /// <summary>
    /// Strategy interface for selection tool operations.
    /// Each selection tool implements this interface to handle its unique selection logic.
    /// </summary>
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
        /// <param name="docPos">Position in document pixel coordinates.</param>
        /// <param name="e">Pointer event arguments for modifier key detection.</param>
        /// <returns>True if the tool handled the event; false otherwise.</returns>
        bool PointerPressed(Point docPos, PointerRoutedEventArgs e);

        /// <summary>
        /// Handles pointer move events during an active selection operation.
        /// </summary>
        /// <param name="docPos">Position in document pixel coordinates.</param>
        /// <param name="e">Pointer event arguments.</param>
        /// <returns>True if the tool handled the event; false otherwise.</returns>
        bool PointerMoved(Point docPos, PointerRoutedEventArgs e);

        /// <summary>
        /// Handles pointer release events to finalize a selection operation.
        /// </summary>
        /// <param name="docPos">Position in document pixel coordinates.</param>
        /// <param name="e">Pointer event arguments.</param>
        /// <returns>True if the tool handled the event; false otherwise.</returns>
        bool PointerReleased(Point docPos, PointerRoutedEventArgs e);

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
        /// Draws tool-specific preview geometry (marquee, lasso path, etc.).
        /// </summary>
        /// <param name="renderer">Canvas renderer for rendering.</param>
        /// <param name="destRect">Destination rectangle in view coordinates.</param>
        /// <param name="scale">Current zoom scale factor.</param>
        /// <param name="antsPhase">Current marching ants animation phase (0 to period).</param>
        void DrawPreview(ICanvasRenderer renderer, Rect destRect, double scale, float antsPhase);
    }
}
