using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace PixlPunkt.Uno.UI.Palette.Controls
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

        public PalettePanelMenuFlyout()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the flyout at the specified element with the given XamlRoot.
        /// </summary>
        public void ShowAt(FrameworkElement target, XamlRoot xamlRoot)
        {
            // Create a fresh MenuFlyout each time to avoid XamlRoot conflicts
            var flyout = new MenuFlyout();
            flyout.XamlRoot = xamlRoot;

            var miAddFg = new MenuFlyoutItem { Text = "Add FG to palette" };
            miAddFg.Click += (s, e) => AddFgPalette?.Invoke(this, EventArgs.Empty);
            flyout.Items.Add(miAddFg);

            var miAddBg = new MenuFlyoutItem { Text = "Add BG to palette" };
            miAddBg.Click += (s, e) => AddBgPalette?.Invoke(this, EventArgs.Empty);
            flyout.Items.Add(miAddBg);

            flyout.Items.Add(new MenuFlyoutSeparator());

            var miClear = new MenuFlyoutItem { Text = "Clear palette�" };
            miClear.Click += (s, e) => ClearPalette?.Invoke(this, EventArgs.Empty);
            flyout.Items.Add(miClear);

            flyout.ShowAt(target);
        }
    }
}
