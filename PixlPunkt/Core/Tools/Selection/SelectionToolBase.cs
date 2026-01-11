using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Rendering;
using PixlPunkt.Core.Tools.Settings;
using Windows.Foundation;

namespace PixlPunkt.Core.Tools.Selection
{
    /// <summary>
    /// Base class for selection tools providing common selection mode management and modifier key handling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionToolBase provides the foundation for all selection tools (Rect, Wand, Brush, Lasso) by:
    /// - Managing selection combination modes (Replace, Add, Subtract)
    /// - Detecting modifier keys (Shift for add, Alt for subtract)
    /// - Coordinating with SelectionRegion for building selection masks
    /// - Providing lifecycle hooks for derived tools
    /// </para>
    /// </remarks>
    public abstract class SelectionToolBase : ISelectionTool
    {
        /// <summary>
        /// Defines how new selections combine with existing selections.
        /// </summary>
        protected enum SelectionCombineMode
        {
            /// <summary>New selection replaces existing (default, no modifiers).</summary>
            Replace,
            /// <summary>New selection unions with existing (Shift held).</summary>
            Add,
            /// <summary>New selection subtracts from existing (Alt held).</summary>
            Subtract
        }

        /// <summary>
        /// Gets the current selection combination mode based on modifier keys.
        /// </summary>
        protected SelectionCombineMode CombineMode { get; private set; } = SelectionCombineMode.Replace;

        /// <summary>
        /// Gets a value indicating whether the tool is currently active (drag in progress).
        /// </summary>
        /// <inheritdoc/>
        bool ISelectionTool.IsActive => IsActiveInternal;

        /// <inheritdoc/>
        public virtual bool NeedsContinuousRender => false;

        /// <summary>
        /// Gets a value indicating whether the tool is currently active (drag in progress).
        /// </summary>
        protected bool IsActiveInternal { get; private set; }

        // ====================================================================
        // ISelectionTool IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        public abstract string Id { get; }

        /// <inheritdoc/>
        public abstract bool HasPreview { get; }

        /// <summary>
        /// Handles pointer press events and determines selection mode from modifiers.
        /// </summary>
        public virtual bool PointerPressed(Point docPos, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(null);
            if (!pt.Properties.IsLeftButtonPressed)
                return false;

            // Determine combine mode from modifiers
            bool shift = (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Shift) != 0;
            bool alt = (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Menu) != 0;

            if (shift)
                CombineMode = SelectionCombineMode.Add;
            else if (alt)
                CombineMode = SelectionCombineMode.Subtract;
            else
                CombineMode = SelectionCombineMode.Replace;

            IsActiveInternal = true;
            return OnPressed(docPos, e);
        }

        /// <summary>
        /// Handles pointer move events during active drag.
        /// </summary>
        public virtual bool PointerMoved(Point docPos, PointerRoutedEventArgs e)
        {
            if (!IsActiveInternal)
                return false;

            return OnMoved(docPos, e);
        }

        /// <summary>
        /// Handles pointer release events to finalize selection operation.
        /// </summary>
        public virtual bool PointerReleased(Point docPos, PointerRoutedEventArgs e)
        {
            if (!IsActiveInternal)
                return false;

            bool handled = OnReleased(docPos, e);
            IsActiveInternal = false;
            CombineMode = SelectionCombineMode.Replace;
            return handled;
        }

        /// <summary>
        /// Called when tool is activated/selected.
        /// </summary>
        public virtual void Activate()
        {
        }

        /// <summary>
        /// Called when tool is deactivated/unselected.
        /// </summary>
        public virtual void Deactivate()
        {
            IsActiveInternal = false;
            CombineMode = SelectionCombineMode.Replace;
        }

        /// <inheritdoc/>
        public virtual void Cancel()
        {
            IsActiveInternal = false;
        }

        /// <inheritdoc/>
        public virtual void Configure(ToolSettingsBase settings)
        {
        }

        /// <inheritdoc/>
        public virtual void DrawPreview(ICanvasRenderer renderer, Rect destRect, double scale, float antsPhase)
        {
        }

        // ====================================================================
        // ABSTRACT LIFECYCLE HOOKS
        // ====================================================================

        /// <summary>
        /// Called when pointer is pressed. Derived tools begin their selection operation here.
        /// </summary>
        protected abstract bool OnPressed(Point docPos, PointerRoutedEventArgs e);

        /// <summary>
        /// Called during pointer move. Derived tools update their selection preview here.
        /// </summary>
        protected abstract bool OnMoved(Point docPos, PointerRoutedEventArgs e);

        /// <summary>
        /// Called when pointer is released. Derived tools finalize their selection here.
        /// </summary>
        protected abstract bool OnReleased(Point docPos, PointerRoutedEventArgs e);
    }
}
