using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PixlPunkt.UI.Palette.Controls
{
    public sealed partial class SwatchMenuFlyout : UserControl
    {
        public event EventHandler<Border?>? RemoveSwatch;
        public event EventHandler<Border?>? SetAsForeground;
        public event EventHandler<Border?>? SetAsBackground;
        public event EventHandler<Border?>? EditSwatch;

        public Border? TargetSwatch { get; private set; }

        public MenuFlyout Flyout => SwatchBorderMenuFlyout;
        public SwatchMenuFlyout()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the flyout at the specified element for the given layer.
        /// </summary>
        public void ShowAt(FrameworkElement target, Border swatch, XamlRoot xamlRoot)
        {
            TargetSwatch = swatch;

            if (SwatchBorderMenuFlyout.XamlRoot == null)
            {
                SwatchBorderMenuFlyout.XamlRoot = xamlRoot;
            }

            SwatchBorderMenuFlyout.ShowAt(target);
        }

        private void SwatchRemove_Click(object sender, RoutedEventArgs e)
        {
            RemoveSwatch?.Invoke(this, TargetSwatch);
        }
        private void SwatchSetFg_Click(object sender, RoutedEventArgs e)
        {
            SetAsForeground?.Invoke(this, TargetSwatch);
        }
        private void SwatchSetBg_Click(object sender, RoutedEventArgs e)
        {
            SetAsBackground?.Invoke(this, TargetSwatch);
        }
        private void SwatchEdit_Click(object sender, RoutedEventArgs e)
        {
            EditSwatch?.Invoke(this, TargetSwatch);
        }
    }
}
