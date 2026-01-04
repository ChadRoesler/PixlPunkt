using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Logging;
using System;

namespace PixlPunkt.UI.Layers
{
    /// <summary>
    /// LayersPanel partial: Root drop zone for moving items out of folders to root level.
    /// </summary>
    public sealed partial class LayersPanel
    {
        // --------------------------------------------------------------------
        // ROOT DROP ZONE (for moving items out of folders to root level)
        // --------------------------------------------------------------------

        /// <summary>
        /// Shows the root drop zone if the dragged item is inside a folder.
        /// </summary>
        private void ShowRootDropZoneIfNeeded()
        {
            if (_draggedItem?.Parent != null)
            {
                LoggingService.Debug("ShowRootDropZoneIfNeeded: Showing drop zone for layer '{LayerName}' from folder '{FolderName}'",
                    _draggedItem.Name,
                    _draggedItem.Parent.Name);

                var highlight = this.FindName("RootDropZoneHighlight") as Border;
                if (highlight != null)
                {
                    highlight.Opacity = 1;
                }

                var footerContainer = LayersList.ContainerFromItem(RootDropZoneFooterItem.Instance) as ListViewItem;
                if (footerContainer != null)
                {
                    footerContainer.IsEnabled = true;

                    _rootDropZoneBorder = FindVisualChild<Border>(footerContainer);
                    LoggingService.Debug("ShowRootDropZoneIfNeeded: Footer item enabled, root border reference obtained: {BorderFound}",
                        _rootDropZoneBorder != null ? "yes" : "no");
                }
            }
        }

        /// <summary>
        /// Hides the root drop zone highlight and text.
        /// </summary>
        private void HideRootDropZone()
        {
            LoggingService.Debug("HideRootDropZone: Hiding drop zone");

            var highlight = this.FindName("RootDropZoneHighlight") as Border;
            if (highlight != null)
            {
                highlight.Opacity = 0;
            }

            if (_rootDropZoneBorder != null)
            {
                try
                {
                    _rootDropZoneBorder.Background = new SolidColorBrush(Colors.Transparent);
                    _rootDropZoneBorder.BorderBrush = new SolidColorBrush(Colors.Transparent);
                    _rootDropZoneBorder.BorderThickness = new Thickness(2);
                    LoggingService.Debug("HideRootDropZone: Footer border colors reset to transparent using stored reference");
                }
                catch (Exception ex)
                {
                    LoggingService.Error("HideRootDropZone: Exception during color reset with stored reference", ex);
                }
                finally
                {
                    _rootDropZoneBorder = null;
                }
            }
            else
            {
                LoggingService.Debug("HideRootDropZone: No stored reference to footer border (will be reset in DragLeave)");
            }

            var footerContainer = LayersList.ContainerFromItem(RootDropZoneFooterItem.Instance) as ListViewItem;
            if (footerContainer != null)
            {
                footerContainer.IsEnabled = false;
                LoggingService.Debug("HideRootDropZone: Footer item disabled");
            }
        }

        private void RootDropZone_DragOver(object sender, DragEventArgs e)
        {
            if (_draggedItem == null || _draggedItem.Parent == null)
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
                return;
            }

            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            e.DragUIOverride.Caption = $"Release to move '{_draggedItem.Name}' to root level";
            e.DragUIOverride.IsCaptionVisible = true;

            if (sender is Border rootDropZone)
            {
                try
                {
                    var accentColor = (Windows.UI.Color)Application.Current.Resources["SystemAccentColor"];
                    var accentLight = (Windows.UI.Color)Application.Current.Resources["SystemAccentColor"];

                    rootDropZone.Background = new SolidColorBrush(accentLight);
                    rootDropZone.BorderBrush = new SolidColorBrush(accentColor);
                    rootDropZone.BorderThickness = new Thickness(2);
                }
                catch (Exception ex)
                {
                    LoggingService.Warning("Failed to apply accent colors to RootDropZone: {Exception}", ex.Message);
                    rootDropZone.Background = new SolidColorBrush(Microsoft.UI.Colors.LightBlue);
                    rootDropZone.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                    rootDropZone.BorderThickness = new Thickness(2);
                }

                var highlight = FindVisualDescendant<Border>(rootDropZone, "RootDropZoneHighlight");
                if (highlight != null)
                {
                    highlight.Opacity = 1;
                }
            }

            ClearDragVisuals();
            e.Handled = true;
        }

        private void RootDropZone_Drop(object sender, DragEventArgs e)
        {
            StopDragScroll();

            if (_draggedItem == null || _doc == null || _draggedItem.Parent == null)
            {
                LoggingService.Warning("RootDropZone_Drop: Invalid state - draggedItem={Item}, doc={Doc}, parent={Parent}",
                    _draggedItem?.Name ?? "null",
                    _doc != null ? "valid" : "null",
                    _draggedItem?.Parent != null ? "valid" : "null");
                return;
            }

            try
            {
                LoggingService.Info("RootDropZone_Drop: Moving layer '{LayerName}' from '{SourceParent}' to root level",
                    _draggedItem.Name,
                    _draggedItem.Parent.Name);

                _doc.MoveLayerToFolder(_draggedItem, null);

                _doc.CompositeTo(_doc.Surface);

                LoggingService.Info("RootDropZone_Drop: Successfully moved '{LayerName}' to root level", _draggedItem.Name);

                if (sender is Border rootDropZone)
                {
                    rootDropZone.Background = new SolidColorBrush(Colors.Transparent);
                    rootDropZone.BorderBrush = new SolidColorBrush(Colors.Transparent);
                    rootDropZone.BorderThickness = new Thickness(2);
                    LoggingService.Debug("RootDropZone_Drop: Footer border reset to transparent");

                    var highlight = FindVisualDescendant<Border>(rootDropZone, "RootDropZoneHighlight");
                    if (highlight != null)
                    {
                        highlight.Opacity = 0;
                        LoggingService.Debug("RootDropZone_Drop: Highlight opacity set to 0");
                    }
                    
                    var footerContainer = LayersList.ContainerFromItem(RootDropZoneFooterItem.Instance) as ListViewItem;
                    if (footerContainer != null)
                    {
                        footerContainer.IsEnabled = false;
                        LoggingService.Debug("RootDropZone_Drop: Footer item disabled");
                    }
                }

                ClearDragVisuals();

                _draggedItem = null;

                EnableInteractiveElementsAfterDrag();

                RebuildFromDoc();
                SelectFromDoc();

                if (_needsRebuildAfterDrag)
                {
                    RebuildFromDoc();
                    _needsRebuildAfterDrag = false;
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                LoggingService.Error("RootDropZone_Drop: Failed to move layer to root", ex);
                _draggedItem = null;
                EnableInteractiveElementsAfterDrag();
                e.Handled = true;
            }
        }

        private void RootDropZone_DragLeave(object sender, DragEventArgs e)
        {
            try
            {
                if (sender is Border rootDropZone)
                {
                    rootDropZone.Background = new SolidColorBrush(Colors.Transparent);
                    rootDropZone.BorderBrush = new SolidColorBrush(Colors.Transparent);
                    rootDropZone.BorderThickness = new Thickness(2);
                    LoggingService.Debug("RootDropZone_DragLeave: Footer border reset to transparent");

                    var highlight = FindVisualDescendant<Border>(rootDropZone, "RootDropZoneHighlight");
                    if (highlight != null)
                    {
                        highlight.Opacity = 0;
                        LoggingService.Debug("RootDropZone_DragLeave: Highlight opacity set to 0");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warning("RootDropZone_DragLeave: Failed to reset drop zone appearance: {Exception}", ex.Message);
            }
        }
    }
}
