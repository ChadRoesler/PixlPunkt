using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace PixlPunkt.UI.Palette.Controls
{
    /// <summary>
    /// Reusable MenuFlyout for the Palette panel background (empty area).
    /// Exposes events for actions so the parent can wire up handlers.
    /// </summary>
    public sealed partial class PalettePanelMenuFlyout : UserControl
    {
        /// <summary>Raised when "Add Fg to Palette" is clicked.</summary>
        public event EventHandler? AddFgPalette;
        /// <summary>Raised when "Add Bg to Palette" is clicked.</summary>
        public event EventHandler? AddBgPalette;
        /// <summary>Raised when "Clear Palette" is clicked.</summary>
        public event EventHandler? ClearPalette;

        /// <summary>
        /// Gets the MenuFlyout that can be assigned to a control's ContextFlyout.
        /// </summary>
        public MenuFlyout Flyout => PanelMenuFlyout;
        public PalettePanelMenuFlyout()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the flyout at the specified element with the given XamlRoot.
        /// </summary>
        public void ShowAt(FrameworkElement target, XamlRoot xamlRoot)
        {
            if (PanelMenuFlyout.XamlRoot != xamlRoot)
                PanelMenuFlyout.XamlRoot = xamlRoot;
            PanelMenuFlyout.ShowAt(target);
        }

        private void AddFgPalette_Click(object sender, RoutedEventArgs e)
        {
            AddFgPalette?.Invoke(this, EventArgs.Empty);
        }

        private void AddBgPalette_Click(object sender, RoutedEventArgs e)
        {
            AddBgPalette?.Invoke(this, EventArgs.Empty);
        }

        private void ClearPalette_Click(object sender, RoutedEventArgs e)
        {
            ClearPalette?.Invoke(this, EventArgs.Empty);
        }
    }
}
