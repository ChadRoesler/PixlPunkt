using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Enums;
using System;

namespace PixlPunkt.UI.Layers
{
    /// <summary>
    /// LayersPanel partial: Visual tree helpers and UI utility methods.
    /// </summary>
    public sealed partial class LayersPanel
    {
        // --------------------------------------------------------------------
        // AUTO-SCROLL DURING DRAG
        // --------------------------------------------------------------------

        private void DragScrollTimer_Tick(object? sender, object e)
        {
            if (_dragScrollDirection == 0)
                return;

            var scrollViewer = GetLayersListScrollViewer();
            if (scrollViewer == null)
                return;

            double newOffset = scrollViewer.VerticalOffset + (_dragScrollDirection * DragScrollSpeed);
            newOffset = Math.Clamp(newOffset, 0, scrollViewer.ScrollableHeight);
            scrollViewer.ChangeView(null, newOffset, null, true);
        }

        private void StartDragScroll()
        {
            if (_dragScrollTimer != null && !_dragScrollTimer.IsEnabled)
            {
                _dragScrollTimer.Start();
            }
        }

        private void StopDragScroll()
        {
            _dragScrollDirection = 0;
            _dragScrollTimer?.Stop();
        }

        /// <summary>
        /// Gets the ScrollViewer from the ListView.
        /// </summary>
        private ScrollViewer? GetLayersListScrollViewer()
        {
            return FindVisualChild<ScrollViewer>(LayersList);
        }

        // --------------------------------------------------------------------
        // VISUAL TREE HELPERS
        // --------------------------------------------------------------------

        /// <summary>
        /// Finds a visual child of the specified type.
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var cur = start;
            while (cur != null && cur is not T)
                cur = VisualTreeHelper.GetParent(cur);
            return cur as T;
        }

        private static T? FindDescendantByName<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root is FrameworkElement fe && fe.Name == name && fe is T typed) return typed;
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var r = FindDescendantByName<T>(child, name);
                if (r != null) return r;
            }
            return null;
        }

        private static T? FindVisualDescendant<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent is T element && element.Name == name)
                return element;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindVisualDescendant<T>(child, name);
                if (result != null) return result;
            }

            return null;
        }

        // --------------------------------------------------------------------
        // NAME EDITING HELPERS
        // --------------------------------------------------------------------

        private static void SetEditing(FrameworkElement anyCellChild, bool editing)
        {
            var cell = FindAncestor<Grid>(anyCellChild);
            if (cell == null) return;

            var view = FindDescendantByName<TextBlock>(cell, "NameView");
            var edit = FindDescendantByName<TextBox>(cell, "NameEdit");
            if (view == null || edit == null) return;

            if (editing)
            {
                edit.Tag = edit.Text;
                view.Visibility = Visibility.Collapsed;
                edit.Visibility = Visibility.Visible;
                edit.Focus(FocusState.Programmatic);
                edit.SelectAll();
            }
            else
            {
                view.Visibility = Visibility.Visible;
                edit.Visibility = Visibility.Collapsed;
            }
        }

        // --------------------------------------------------------------------
        // UI ENABLE STATE
        // --------------------------------------------------------------------

        private void UpdateUiEnabled()
        {
            bool hasDoc = _doc != null;
            if (AddBtn != null) AddBtn.IsEnabled = hasDoc;
            if (AddFolderBtnTop != null) AddFolderBtnTop.IsEnabled = hasDoc;
            if (RemoveBtn != null) RemoveBtn.IsEnabled = hasDoc && LayersList.SelectedIndex >= 0;
            if (AddRefLayerBtn != null) AddRefLayerBtn.IsEnabled = hasDoc;
        }

        /// <summary>
        /// Updates the active layer indicator for all visible layer items.
        /// </summary>
        private void UpdateActiveLayerIndicators()
        {
            var activeLayer = _doc?.ActiveLayer;

            foreach (var item in _uiLayers)
            {
                var container = LayersList.ContainerFromItem(item) as ListViewItem;
                if (container == null) continue;

                // Only RasterLayers have the active indicator
                if (item is Core.Document.Layer.RasterLayer)
                {
                    var indicator = FindVisualDescendant<Border>(container, "ActiveIndicator");
                    if (indicator != null)
                    {
                        indicator.Visibility = ReferenceEquals(item, activeLayer)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }
                }
            }
        }
    }
}
