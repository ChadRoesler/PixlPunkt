namespace PixlPunkt.Core.Enums
{
    /// <summary>
    /// Defines the placement strategies for positioning child windows relative to their parent window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Window placement controls how dialogs, color pickers, and other floating windows are
    /// initially positioned when shown. This provides consistent UX for different window types.
    /// </para>
    /// <para>
    /// Used by window management code to calculate initial window coordinates based on parent
    /// window location, display work area, or specific anchor elements.
    /// </para>
    /// </remarks>
    public enum WindowPlacement
    {
        /// <summary>
        /// Centers the window over its parent window.
        /// </summary>
        CenterOnParent,

        /// <summary>
        /// Positions the window near a specific anchor element with a +16,+16 pixel offset.
        /// </summary>
        /// <remarks>
        /// Useful for context menus or tooltips that should appear near the UI element that triggered them.
        /// </remarks>
        NearAnchor,

        /// <summary>
        /// Centers the window horizontally at the top of the parent window.
        /// </summary>
        CenterTop,

        /// <summary>
        /// Centers the window horizontally at the bottom of the parent window.
        /// </summary>
        CenterBottom,

        /// <summary>
        /// Centers the window on the current display's work area, independent of parent window location.
        /// </summary>
        /// <remarks>
        /// Falls back to centering on the primary display if no specific display can be determined.
        /// </remarks>
        CenterOnScreen
    }
}
