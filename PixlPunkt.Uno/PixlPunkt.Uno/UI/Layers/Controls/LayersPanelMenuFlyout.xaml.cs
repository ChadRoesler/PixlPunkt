using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace PixlPunkt.Uno.UI.Layers.Controls
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
    }
}
