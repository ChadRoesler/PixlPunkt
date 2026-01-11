using System;
using Microsoft.UI.Xaml;
using PixlPunkt.UI.Helpers;

namespace PixlPunkt.UI.Panels
{
    /// <summary>
    /// A floating window that hosts an undocked panel.
    /// </summary>
    public sealed partial class PanelWindow : Window
    {
        private readonly string _panelId;
        private readonly FrameworkElement _content;

        /// <summary>
        /// Gets the unique identifier for this panel.
        /// </summary>
        public string PanelId => _panelId;

        /// <summary>
        /// Gets the content element hosted by this window.
        /// </summary>
        public FrameworkElement PanelContent => _content;

        /// <summary>
        /// Occurs when this window is closing (either by user or programmatically).
        /// </summary>
        public event Action<PanelWindow>? WindowClosing;

        /// <summary>
        /// Creates a new floating panel window.
        /// </summary>
        /// <param name="panelId">Unique identifier for the panel (e.g., "Preview", "Palette", "Tiles", "Layers").</param>
        /// <param name="title">The title to display in the window title bar.</param>
        /// <param name="content">The UI content to host (typically a SectionCard).</param>
        public PanelWindow(string panelId, string title, FrameworkElement content)
        {
            _panelId = panelId;
            _content = content;

            InitializeComponent();

            try
            {
                // Configure window appearance
                // Use null for owner if main window is not available (shouldn't happen but be defensive)
                var owner = App.PixlPunktMainWindow;
                WindowHost.ApplyChrome(this, resizable: true, alwaysOnTop: false, title: title, owner: owner);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PanelWindow] ERROR applying chrome: {ex.Message}");
            }

            // Host the content (SectionCard handles its own header with dock button)
            if (ContentPlaceholder != null && content != null)
            {
                ContentPlaceholder.Child = content;
            }

            // Handle window close
            Closed += OnWindowClosed;
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            try
            {
                WindowClosing?.Invoke(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PanelWindow] ERROR on window closing: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes the content from this window so it can be returned to the main window.
        /// </summary>
        /// <returns>The content element that was hosted.</returns>
        public FrameworkElement DetachContent()
        {
            try
            {
                if (ContentPlaceholder != null)
                {
                    ContentPlaceholder.Child = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PanelWindow] ERROR detaching content: {ex.Message}");
            }
            return _content;
        }
    }
}
