using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Document.Layer;
using System;

namespace PixlPunkt.Uno.UI.Layers.Controls
{
    /// <summary>
    /// Reusable MenuFlyout for ReferenceLayer context menu.
    /// Exposes events for actions so the parent can wire up handlers.
    /// </summary>
    public sealed partial class ReferenceLayerMenuFlyout : UserControl
    {
        /// <summary>Raised when "Open Settings" is clicked.</summary>
        public event EventHandler<ReferenceLayer?>? SettingsRequested;

        /// <summary>Raised when "Visible" is toggled.</summary>
        public event EventHandler<ReferenceLayer?>? VisibleToggled;

        /// <summary>Raised when "Locked" is toggled.</summary>
        public event EventHandler<ReferenceLayer?>? LockedToggled;

        /// <summary>Raised when "Fit to Canvas" is clicked.</summary>
        public event EventHandler<ReferenceLayer?>? FitToCanvasRequested;

        /// <summary>Raised when "Reset Transform" is clicked.</summary>
        public event EventHandler<ReferenceLayer?>? ResetTransformRequested;

        /// <summary>Raised when "Remove Reference" is clicked.</summary>
        public event EventHandler<ReferenceLayer?>? RemoveRequested;

        /// <summary>
        /// Gets the MenuFlyout that can be assigned to a control's ContextFlyout.
        /// </summary>
        public MenuFlyout Flyout => RefLayerMenuFlyout;

        /// <summary>
        /// The target reference layer for the context menu actions.
        /// </summary>
        public ReferenceLayer? TargetLayer { get; private set; }

        public ReferenceLayerMenuFlyout()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the flyout at the specified element for the given reference layer.
        /// </summary>
        public void ShowAt(FrameworkElement target, ReferenceLayer layer, XamlRoot xamlRoot)
        {
            TargetLayer = layer;

            // Update toggle states
            VisibleToggle.IsChecked = layer.Visible;
            LockedToggle.IsChecked = layer.Locked;

            if (RefLayerMenuFlyout.XamlRoot == null)
            {
                RefLayerMenuFlyout.XamlRoot = xamlRoot;
            }

            RefLayerMenuFlyout.ShowAt(target);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
            => SettingsRequested?.Invoke(this, TargetLayer);

        private void Visible_Toggled(object sender, RoutedEventArgs e)
        {
            if (TargetLayer != null)
            {
                TargetLayer.Visible = VisibleToggle.IsChecked == true;
            }
            VisibleToggled?.Invoke(this, TargetLayer);
        }

        private void Locked_Toggled(object sender, RoutedEventArgs e)
        {
            if (TargetLayer != null)
            {
                TargetLayer.Locked = LockedToggle.IsChecked == true;
            }
            LockedToggled?.Invoke(this, TargetLayer);
        }

        private void FitToCanvas_Click(object sender, RoutedEventArgs e)
            => FitToCanvasRequested?.Invoke(this, TargetLayer);

        private void ResetTransform_Click(object sender, RoutedEventArgs e)
            => ResetTransformRequested?.Invoke(this, TargetLayer);

        private void Remove_Click(object sender, RoutedEventArgs e)
            => RemoveRequested?.Invoke(this, TargetLayer);
    }
}
