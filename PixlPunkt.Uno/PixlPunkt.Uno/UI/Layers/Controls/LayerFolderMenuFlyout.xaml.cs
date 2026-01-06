using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Document.Layer;
using System;

namespace PixlPunkt.Uno.UI.Layers.Controls
{
    /// <summary>
    /// Reusable MenuFlyout for LayerFolder context menu.
    /// Exposes events for actions so the parent can wire up handlers.
    /// </summary>
    public sealed partial class LayerFolderMenuFlyout : UserControl
    {
        /// <summary>Raised when "Visible" is toggled.</summary>
        public event EventHandler<LayerFolder?>? VisibleToggled;

        /// <summary>Raised when "Locked" is toggled.</summary>
        public event EventHandler<LayerFolder?>? LockedToggled;

        /// <summary>Raised when "Duplicate Folder" is clicked.</summary>
        public event EventHandler<LayerFolder?>? DuplicateRequested;

        /// <summary>Raised when "Flatten Folder" is clicked.</summary>
        public event EventHandler<LayerFolder?>? FlattenFolderRequested;

        /// <summary>Raised when "Remove Folder" is clicked.</summary>
        public event EventHandler<LayerFolder?>? RemoveRequested;

        /// <summary>
        /// Gets the MenuFlyout that can be assigned to a control's ContextFlyout.
        /// </summary>
        public MenuFlyout Flyout => FolderMenuFlyout;

        /// <summary>
        /// The target folder for the context menu actions.
        /// </summary>
        public LayerFolder? TargetFolder { get; private set; }

        public LayerFolderMenuFlyout()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the flyout at the specified element for the given folder.
        /// </summary>
        public void ShowAt(FrameworkElement target, LayerFolder folder, XamlRoot xamlRoot)
        {
            TargetFolder = folder;

            // Update toggle states
            VisibleToggle.IsChecked = folder.Visible;
            LockedToggle.IsChecked = folder.Locked;

            if (FolderMenuFlyout.XamlRoot == null)
            {
                FolderMenuFlyout.XamlRoot = xamlRoot;
            }

            FolderMenuFlyout.ShowAt(target);
        }

        private void Visible_Toggled(object sender, RoutedEventArgs e)
        {
            if (TargetFolder != null)
            {
                TargetFolder.Visible = VisibleToggle.IsChecked == true;
            }
            VisibleToggled?.Invoke(this, TargetFolder);
        }

        private void Locked_Toggled(object sender, RoutedEventArgs e)
        {
            if (TargetFolder != null)
            {
                TargetFolder.Locked = LockedToggle.IsChecked == true;
            }
            LockedToggled?.Invoke(this, TargetFolder);
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
            => DuplicateRequested?.Invoke(this, TargetFolder);

        private void FlattenFolder_Click(object sender, RoutedEventArgs e)
            => FlattenFolderRequested?.Invoke(this, TargetFolder);

        private void Remove_Click(object sender, RoutedEventArgs e)
            => RemoveRequested?.Invoke(this, TargetFolder);
    }
}
