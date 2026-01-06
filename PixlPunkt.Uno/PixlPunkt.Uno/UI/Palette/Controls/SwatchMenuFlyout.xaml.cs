using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PixlPunkt.Uno.UI.Palette.Controls
{
    /// <summary>
    /// Reusable MenuFlyout for individual swatch items.
    /// Exposes events for actions so the parent can wire up handlers.
    /// </summary>
    public sealed partial class SwatchMenuFlyout : UserControl
    {
        public event EventHandler<Border?>? RemoveSwatch;
        public event EventHandler<Border?>? SetAsForeground;
        public event EventHandler<Border?>? SetAsBackground;
        public event EventHandler<Border?>? EditSwatch;

        public Border? TargetSwatch { get; private set; }

        public SwatchMenuFlyout()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the flyout at the specified element for the given swatch.
        /// </summary>
        public void ShowAt(FrameworkElement target, Border swatch, XamlRoot xamlRoot)
        {
            TargetSwatch = swatch;

            // Create a fresh MenuFlyout each time to avoid XamlRoot conflicts
            var flyout = new MenuFlyout();
            flyout.XamlRoot = xamlRoot;

            var miEdit = new MenuFlyoutItem { Text = "Edit" };
            miEdit.Click += (s, e) => EditSwatch?.Invoke(this, TargetSwatch);
            flyout.Items.Add(miEdit);

            var miRemove = new MenuFlyoutItem { Text = "Remove" };
            miRemove.Click += (s, e) => RemoveSwatch?.Invoke(this, TargetSwatch);
            flyout.Items.Add(miRemove);

            flyout.Items.Add(new MenuFlyoutSeparator());

            var miFg = new MenuFlyoutItem { Text = "Set as FG" };
            miFg.Click += (s, e) => SetAsForeground?.Invoke(this, TargetSwatch);
            flyout.Items.Add(miFg);

            var miBg = new MenuFlyoutItem { Text = "Set as BG" };
            miBg.Click += (s, e) => SetAsBackground?.Invoke(this, TargetSwatch);
            flyout.Items.Add(miBg);

            flyout.ShowAt(target);
        }
    }
}
