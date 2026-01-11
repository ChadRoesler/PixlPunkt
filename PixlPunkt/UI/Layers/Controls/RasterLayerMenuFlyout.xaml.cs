using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document.Layer;
using System;

namespace PixlPunkt.UI.Layers.Controls
{
    /// <summary>
    /// Reusable MenuFlyout for RasterLayer context menu.
    /// Exposes events for actions so the parent can wire up handlers.
    /// </summary>
    public sealed partial class RasterLayerMenuFlyout : UserControl
    {
        /// <summary>Raised when "Open Settings" is clicked.</summary>
        public event EventHandler<RasterLayer?>? SettingsRequested;

        /// <summary>Raised when "Copy Effects" is clicked.</summary>
        public event EventHandler<RasterLayer?>? CopyEffectsRequested;

        /// <summary>Raised when "Paste Effects" is clicked.</summary>
        public event EventHandler<RasterLayer?>? PasteEffectsRequested;

        /// <summary>Raised when "Add Mask" is clicked.</summary>
        public event EventHandler<RasterLayer?>? AddMaskRequested;

        /// <summary>Raised when "Delete Mask" is clicked.</summary>
        public event EventHandler<RasterLayer?>? DeleteMaskRequested;

        /// <summary>Raised when "Apply Mask" is clicked.</summary>
        public event EventHandler<RasterLayer?>? ApplyMaskRequested;

        /// <summary>Raised when "Visible" is toggled.</summary>
        public event EventHandler<RasterLayer?>? VisibleToggled;

        /// <summary>Raised when "Locked" is toggled.</summary>
        public event EventHandler<RasterLayer?>? LockedToggled;

        /// <summary>Raised when "Duplicate Layer" is clicked.</summary>
        public event EventHandler<RasterLayer?>? DuplicateRequested;

        /// <summary>Raised when "Merge Down" is clicked.</summary>
        public event EventHandler<RasterLayer?>? MergeDownRequested;

        /// <summary>Raised when "Remove Layer" is clicked.</summary>
        public event EventHandler<RasterLayer?>? RemoveRequested;

        /// <summary>
        /// Gets the MenuFlyout that can be assigned to a control's ContextFlyout.
        /// </summary>
        public MenuFlyout Flyout => LayerMenuFlyout;

        /// <summary>
        /// The target layer for the context menu actions.
        /// </summary>
        public RasterLayer? TargetLayer { get; private set; }

        public RasterLayerMenuFlyout()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the flyout at the specified element for the given layer.
        /// </summary>
        public void ShowAt(FrameworkElement target, RasterLayer layer, XamlRoot xamlRoot)
        {
            TargetLayer = layer;

            // Update toggle states
            VisibleToggle.IsChecked = layer.Visible;
            LockedToggle.IsChecked = layer.Locked;

            if (LayerMenuFlyout.XamlRoot == null)
            {
                LayerMenuFlyout.XamlRoot = xamlRoot;
            }

            LayerMenuFlyout.ShowAt(target);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
            => SettingsRequested?.Invoke(this, TargetLayer);

        private void CopyEffects_Click(object sender, RoutedEventArgs e)
            => CopyEffectsRequested?.Invoke(this, TargetLayer);

        private void PasteEffects_Click(object sender, RoutedEventArgs e)
            => PasteEffectsRequested?.Invoke(this, TargetLayer);

        private void AddMask_Click(object sender, RoutedEventArgs e)
            => AddMaskRequested?.Invoke(this, TargetLayer);

        private void DeleteMask_Click(object sender, RoutedEventArgs e)
            => DeleteMaskRequested?.Invoke(this, TargetLayer);

        private void ApplyMask_Click(object sender, RoutedEventArgs e)
            => ApplyMaskRequested?.Invoke(this, TargetLayer);

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

        private void Duplicate_Click(object sender, RoutedEventArgs e)
            => DuplicateRequested?.Invoke(this, TargetLayer);

        private void MergeDown_Click(object sender, RoutedEventArgs e)
            => MergeDownRequested?.Invoke(this, TargetLayer);

        private void Remove_Click(object sender, RoutedEventArgs e)
            => RemoveRequested?.Invoke(this, TargetLayer);
    }
}
