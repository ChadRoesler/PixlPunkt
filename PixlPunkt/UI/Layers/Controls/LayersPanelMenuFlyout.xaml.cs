using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace PixlPunkt.UI.Layers.Controls
{
    /// <summary>
    /// Reusable MenuFlyout for the Layers panel background (empty area).
    /// Exposes events for actions so the parent can wire up handlers.
    /// </summary>
    public sealed partial class LayersPanelMenuFlyout : UserControl
    {
        /// <summary>Raised when "Add Layer" is clicked.</summary>
        public event EventHandler? AddLayerRequested;

        /// <summary>Raised when "Add Folder" is clicked.</summary>
        public event EventHandler? AddFolderRequested;

        /// <summary>Raised when "Add Reference Image" is clicked.</summary>
        public event EventHandler? AddReferenceLayerRequested;

        /// <summary>Raised when "Remove Selected" is clicked.</summary>
        public event EventHandler? RemoveSelectedRequested;
        /// <summary>Raised with the requested thumbnail size in pixels (0 = hidden).</summary>
        public event EventHandler<int>? PreviewSizeRequested;

        /// <summary>
        /// Gets the MenuFlyout that can be assigned to a control's ContextFlyout.
        /// </summary>
        public MenuFlyout Flyout => PanelMenuFlyout;

        public LayersPanelMenuFlyout()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the flyout at the specified element with the given XamlRoot.
        /// </summary>
        public void ShowAt(FrameworkElement target, XamlRoot xamlRoot)
        {
            if (PanelMenuFlyout.XamlRoot == null)
            {
                PanelMenuFlyout.XamlRoot = xamlRoot;
            }
            PanelMenuFlyout.ShowAt(target);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            AddLayerRequested?.Invoke(this, EventArgs.Empty);
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            AddFolderRequested?.Invoke(this, EventArgs.Empty);
        }

        private void AddReferenceLayer_Click(object sender, RoutedEventArgs e)
        {
            AddReferenceLayerRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            RemoveSelectedRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Checks the entry matching the current size (called before showing).</summary>
        public void SetPreviewSize(int size)
        {
            foreach (var item in new[] { PreviewOff, PreviewSmall, PreviewNormal, PreviewLarge })
                item.IsChecked = item.Tag is string t && int.TryParse(t, out int v) && v == size;
        }

        private void PreviewSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string t && int.TryParse(t, out int size))
            {
                SetPreviewSize(size);
                PreviewSizeRequested?.Invoke(this, size);
            }
        }
    }
}
