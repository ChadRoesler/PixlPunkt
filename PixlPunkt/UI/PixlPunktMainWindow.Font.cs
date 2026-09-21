using PixlPunkt.UI.CanvasHost;
using Microsoft.UI.Xaml;

namespace PixlPunkt.UI
{
    /// <summary>
    /// Font-specific pieces of the main window. A font document wants a different bottom pane from
    /// an image one: an animation timeline says nothing about a typeface, while seeing a letter
    /// beside its neighbours is how spacing is judged.
    /// </summary>
    public sealed partial class PixlPunktMainWindow
    {
        /// <summary>
        /// Swaps the bottom pane to suit the document, and points the glyph strip at it. Passing
        /// null puts the animation panel back, which is the right resting state with nothing open.
        /// </summary>
        private void UpdateBottomPaneForDocument(CanvasViewHost? host)
        {
            bool isFont = host?.Document.FontState.HasState == true;

            if (AnimationPanel is not null)
                AnimationPanel.Visibility = isFont ? Visibility.Collapsed : Visibility.Visible;

            if (GlyphStrip is null) return;

            GlyphStrip.Visibility = isFont ? Visibility.Visible : Visibility.Collapsed;
            GlyphStrip.Bind(isFont ? host!.Document : null, isFont ? host : null);
        }
    }
}
