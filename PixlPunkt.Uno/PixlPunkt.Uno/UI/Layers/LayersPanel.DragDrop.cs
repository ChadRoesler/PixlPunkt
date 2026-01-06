using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Logging;
using System;
using Windows.ApplicationModel.DataTransfer;

namespace PixlPunkt.Uno.UI.Layers
{
    /// <summary>
    /// LayersPanel partial: Drag-and-drop logic, insertion indicators, folder highlighting, and auto-scroll.
    /// </summary>
    public sealed partial class LayersPanel
    {
        // --------------------------------------------------------------------
        // DRAG + DROP
        // --------------------------------------------------------------------

        private void LayersList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count > 0 && e.Items[0] is LayerBase item)
            {
                _draggedItem = item;
                e.Data.RequestedOperation = DataPackageOperation.Move;
                e.Data.Properties.Add("DraggedLayer", item);

                DisableInteractiveElementsDuringDrag();
                DisableBuiltInDragScroll();

                // Show root drop zone if the dragged item is inside a folder
                ShowRootDropZoneIfNeeded();
            }
            // Footer item cannot be dragged - simply don't initiate drag
        }

        private void LayersList_DragOver(object sender, DragEventArgs e)
        {
            if (_draggedItem == null)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                ClearDragVisuals();
                StopDragScroll();
                return;
            }

            var position = e.GetPosition(LayersList);

            // Auto-scroll
            double listHeight = LayersList.ActualHeight;
            if (position.Y < DragScrollEdgeThreshold)
            {
                _dragScrollDirection = -1;
                StartDragScroll();
            }
            else if (position.Y > listHeight - DragScrollEdgeThreshold)
            {
                _dragScrollDirection = 1;
                StartDragScroll();
            }
            else
            {
                StopDragScroll();
            }

            // Clear folder highlight only; insertion indicator remains until updated
            ClearFolderHighlightOnly();

            var targetItem = GetItemAtPosition(position);

            // Empty-space: if below group tail, show insertion at tail
            if (targetItem == null)
            {
                var tail = TryGetGroupTailForDragged();
                if (tail.HasValue)
                {
                    var (logicalTail, visualTail) = tail.Value;

                    var tailContainer = LayersList.ContainerFromItem(visualTail) as ListViewItem;
                    if (tailContainer != null)
                    {
                        var t = tailContainer.TransformToVisual(LayersList);
                        var bounds = t.TransformBounds(new Windows.Foundation.Rect(0, 0, tailContainer.ActualWidth, tailContainer.ActualHeight));

                        if (position.Y > bounds.Bottom)
                        {
                            ShowInsertionIndicator(logicalTail, insertBefore: false, visualAnchor: visualTail);

                            e.AcceptedOperation = DataPackageOperation.Move;
                            e.DragUIOverride.Caption = "Reorder";
                            e.DragUIOverride.IsCaptionVisible = true;
                            return;
                        }
                    }
                }

                HideInsertionIndicator();
                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.Caption = "Reorder";
                e.DragUIOverride.IsCaptionVisible = true;
                return;
            }

            // 1) Folder hit: edge zones reorder; middle zone move into folder
            if (targetItem is LayerFolder targetFolder)
            {
                var container = LayersList.ContainerFromItem(targetFolder) as ListViewItem;
                if (container != null)
                {
                    var transform = container.TransformToVisual(LayersList);
                    var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                    double edgeThreshold = bounds.Height * 0.25;
                    double relativeY = position.Y - bounds.Top;

                    bool inTopEdge = relativeY < edgeThreshold;
                    bool inBottomEdge = relativeY > (bounds.Height - edgeThreshold);

                    if (inTopEdge || inBottomEdge)
                    {
                        // edge-zone reorder (same parent only)
                        if ((inTopEdge || inBottomEdge) && _draggedItem.Parent == targetFolder.Parent)
                        {
                            bool insertBefore = inTopEdge;
                            ShowInsertionIndicator(targetFolder, insertBefore);

                            e.AcceptedOperation = DataPackageOperation.Move;
                            e.DragUIOverride.Caption = "Reorder";
                            e.DragUIOverride.IsCaptionVisible = true;
                            return;
                        }
                    }
                }

                // middle-zone move into folder
                if (CanMoveIntoFolder(_draggedItem, targetFolder))
                {
                    _hoverFolderTarget = targetFolder;
                    HighlightFolder(targetFolder);

                    HideInsertionIndicator();
                    e.AcceptedOperation = DataPackageOperation.Move;
                    e.DragUIOverride.Caption = $"Move into {targetFolder.Name}";
                    e.DragUIOverride.IsCaptionVisible = true;
                    e.DragUIOverride.IsGlyphVisible = true;
                    return;
                }
            }

            // 2) Target item exists: if same parent -> reorder with insertion line
            if (_draggedItem.Parent == targetItem.Parent)
            {
                var container = LayersList.ContainerFromItem(targetItem) as ListViewItem;
                if (container != null)
                {
                    var t = container.TransformToVisual(LayersList);
                    var bounds = t.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                    bool insertBefore = (position.Y - bounds.Top) < (bounds.Height / 2);
                    ShowInsertionIndicator(targetItem, insertBefore);
                }

                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.Caption = "Reorder";
                e.DragUIOverride.IsCaptionVisible = true;
                return;
            }

            // 3) Pulling out to root (hovering root item)
            // IMPORTANT: Skip this if the target IS the dragged item's parent folder
            if (_draggedItem.Parent != null && targetItem.Parent == null &&
                !ReferenceEquals(targetItem, _draggedItem.Parent))
            {
                var container = LayersList.ContainerFromItem(targetItem) as ListViewItem;
                if (container != null)
                {
                    var t = container.TransformToVisual(LayersList);
                    var bounds = t.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                    bool insertBefore = (position.Y - bounds.Top) < (bounds.Height / 2);
                    ShowInsertionIndicator(targetItem, insertBefore, moveToRoot: true);
                }

                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.Caption = $"Move '{_draggedItem.Name}' to root";
                e.DragUIOverride.IsCaptionVisible = true;
                return;
            }

            // 4) Dragging out of a folder by hovering over the parent folder itself
            if (_draggedItem.Parent != null && ReferenceEquals(targetItem, _draggedItem.Parent) &&
                targetItem is LayerFolder parentFolder)
            {
                var container = LayersList.ContainerFromItem(parentFolder) as ListViewItem;
                if (container != null)
                {
                    var transform = container.TransformToVisual(LayersList);
                    var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                    double edgeThreshold = bounds.Height * 0.25;
                    double relativeY = position.Y - bounds.Top;

                    bool inTopEdge = relativeY < edgeThreshold;
                    bool inBottomEdge = relativeY > (bounds.Height - edgeThreshold);

                    if (inTopEdge || inBottomEdge)
                    {
                        bool insertBefore = inTopEdge;
                        ShowInsertionIndicator(parentFolder, insertBefore, moveToRoot: true);

                        e.AcceptedOperation = DataPackageOperation.Move;
                        e.DragUIOverride.Caption = $"Move '{_draggedItem.Name}' to root";
                        e.DragUIOverride.IsCaptionVisible = true;
                        return;
                    }
                }

                // Middle zone of parent folder: no special action
                HideInsertionIndicator();
                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.Caption = "Drag to edge to move to root";
                e.DragUIOverride.IsCaptionVisible = true;
                return;
            }

            HideInsertionIndicator();
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.Caption = "Reorder";
            e.DragUIOverride.IsCaptionVisible = true;
        }

        private void LayersList_Drop(object sender, DragEventArgs e)
        {
            StopDragScroll();

            if (_draggedItem == null || _doc == null)
                return;

            // SNAPSHOT the indicator truth BEFORE clearing visuals
            var snapTarget = _insertionTarget;
            var snapBefore = _insertBefore;
            var snapHoverFolder = _hoverFolderTarget;

            // Now clear visuals
            ClearDragVisuals();
            HideRootDropZone();

            bool handled = false;

            var position = e.GetPosition(LayersList);
            var targetItem = snapTarget ?? GetItemAtPosition(position);
            bool insertBefore = snapBefore;

            // If we are dropping ON a folder row and we don't have an insertion target,
            // decide: edge-zone = reorder, middle-zone = move-into folder.
            if (!handled && snapTarget == null && targetItem is LayerFolder folderHit && CanMoveIntoFolder(_draggedItem, folderHit))
            {
                var container = LayersList.ContainerFromItem(folderHit) as ListViewItem;
                if (container != null)
                {
                    var t = container.TransformToVisual(LayersList);
                    var bounds = t.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                    double edgeThreshold = bounds.Height * 0.25;
                    double relY = position.Y - bounds.Top;

                    bool inTopEdge = relY < edgeThreshold;
                    bool inBottomEdge = relY > (bounds.Height - edgeThreshold);

                    bool sameParent = _draggedItem.Parent == folderHit.Parent;

                    if (sameParent && (inTopEdge || inBottomEdge))
                    {
                        targetItem = folderHit;
                        insertBefore = inTopEdge;
                    }
                    else
                    {
                        _doc.MoveLayerToFolder(_draggedItem, folderHit);
                        if (!folderHit.IsExpanded)
                            folderHit.IsExpanded = true;

                        handled = true;
                    }
                }
                else
                {
                    _doc.MoveLayerToFolder(_draggedItem, folderHit);
                    if (!folderHit.IsExpanded)
                        folderHit.IsExpanded = true;

                    handled = true;
                }
            }

            // 1) Middle-zone hover folder with no insertion line => move into folder
            if (!handled && snapTarget == null && snapHoverFolder != null && CanMoveIntoFolder(_draggedItem, snapHoverFolder))
            {
                _doc.MoveLayerToFolder(_draggedItem, snapHoverFolder);
                if (!snapHoverFolder.IsExpanded)
                    snapHoverFolder.IsExpanded = true;

                handled = true;
            }

            // 2) Moving INTO a folder at an indicated position
            if (!handled && targetItem != null && targetItem.Parent is LayerFolder destFolder &&
                _draggedItem.Parent != destFolder && CanMoveIntoFolder(_draggedItem, destFolder))
            {
                int destCountBefore = destFolder.Children.Count;
                int targetInternal = destFolder.IndexOfChild(targetItem);
                int desiredInternal = ComputeInsertInternalIndexIntoGroup(destCountBefore, targetInternal, insertBefore);

                _doc.MoveLayerToFolder(_draggedItem, destFolder);

                if (!destFolder.IsExpanded)
                    destFolder.IsExpanded = true;

                int fromInternal = destFolder.IndexOfChild(_draggedItem);
                if (fromInternal >= 0)
                {
                    desiredInternal = Math.Clamp(desiredInternal, 0, destFolder.Children.Count - 1);
                    if (fromInternal != desiredInternal)
                        destFolder.MoveChild(fromInternal, desiredInternal);
                }

                handled = true;
            }

            // 3) Reorder within SAME parent (folder OR root) using UI->internal mapping
            if (!handled && targetItem != null && _draggedItem.Parent == targetItem.Parent)
            {
                if (_draggedItem.Parent is LayerFolder folder)
                {
                    int count = folder.Children.Count;
                    int from = folder.IndexOfChild(_draggedItem);
                    int tgt = folder.IndexOfChild(targetItem);

                    if (from >= 0 && tgt >= 0)
                    {
                        int to = ComputeNewInternalIndexFromUiOrder(count, from, tgt, insertBefore);
                        if (from != to)
                            folder.MoveChild(from, to);
                        handled = true;
                    }
                }
                else
                {
                    var root = _doc.RootItems;
                    int count = root.Count;
                    int from = -1;
                    for (int i = 0; i < count; i++)
                    {
                        if (ReferenceEquals(root[i], _draggedItem))
                        {
                            from = i;
                            break;
                        }
                    }

                    int tgt = -1;
                    for (int i = 0; i < root.Count; i++)
                    {
                        if (ReferenceEquals(root[i], targetItem))
                        {
                            tgt = i;
                            break;
                        }
                    }

                    if (from >= 0 && tgt >= 0)
                    {
                        int to = ComputeNewInternalIndexFromUiOrder(count, from, tgt, insertBefore);
                        if (from != to)
                            _doc.MoveRootItemWithHistory(_draggedItem, to);
                        handled = true;
                    }
                }
            }

            // 4) Pulling OUT to root relative to an indicated root item position
            if (!handled && _draggedItem.Parent != null && (targetItem == null || targetItem.Parent == null))
            {
                var root = _doc.RootItems;
                int destCountBefore = root.Count;

                if (targetItem != null)
                {
                    int targetInternal = -1;
                    for (int i = 0; i < root.Count; i++)
                    {
                        if (ReferenceEquals(root[i], targetItem))
                        {
                            targetInternal = i;
                            break;
                        }
                    }

                    _doc.MoveLayerToFolder(_draggedItem, null);

                    if (targetInternal >= 0)
                    {
                        int desiredInternal = ComputeInsertInternalIndexIntoGroup(destCountBefore, targetInternal, insertBefore);

                        int from = -1;
                        for (int i = 0; i < root.Count; i++)
                        {
                            if (ReferenceEquals(root[i], _draggedItem))
                            {
                                from = i;
                                break;
                            }
                        }

                        if (from >= 0)
                        {
                            desiredInternal = Math.Clamp(desiredInternal, 0, _doc.RootItems.Count - 1);
                            if (from != desiredInternal)
                                _doc.MoveRootItemWithHistory(_draggedItem, desiredInternal);
                        }
                    }

                    handled = true;
                }
                else
                {
                    _doc.MoveLayerToFolder(_draggedItem, null);
                    handled = true;
                }
            }

            if (handled)
            {
                _doc.CompositeTo(_doc.Surface);
                RebuildFromDoc();
                SelectFromDoc();
            }

            _draggedItem = null;

            EnableInteractiveElementsAfterDrag();

            if (_needsRebuildAfterDrag)
            {
                RebuildFromDoc();
                _needsRebuildAfterDrag = false;
            }

            e.Handled = true;
        }

        private void LayersList_DragLeave(object sender, DragEventArgs e)
        {
            ClearDragVisuals();
            StopDragScroll();
        }

        // --------------------------------------------------------------------
        // DRAG VISUALS
        // --------------------------------------------------------------------

        private void HighlightFolder(LayerFolder folder)
        {
            _hoverFolderTarget = folder;

            var container = LayersList.ContainerFromItem(folder) as ListViewItem;
            if (container == null) return;

            var dropBorder = FindVisualDescendant<Border>(container, "DropTargetBorder");
            if (dropBorder == null) return;

            ClearFolderHighlightOnly();

            try
            {
                var accentColor = (Windows.UI.Color)Application.Current.Resources["SystemAccentColor"];
                var accentLight = (Windows.UI.Color)Application.Current.Resources["SystemAccentColorLight3"];

                dropBorder.BorderBrush = new SolidColorBrush(accentColor);
                dropBorder.BorderThickness = new Thickness(2);
                dropBorder.Background = new SolidColorBrush(accentLight);
                dropBorder.Opacity = 0.3;
            }
            catch
            {
                dropBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                dropBorder.BorderThickness = new Thickness(2);
                dropBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.LightBlue);
                dropBorder.Opacity = 0.3;
            }

            _lastHighlightedItem = container;
        }

        private void ClearFolderHighlightOnly()
        {
            if (_lastHighlightedItem != null)
            {
                var dropBorder = FindVisualDescendant<Border>(_lastHighlightedItem, "DropTargetBorder");
                if (dropBorder != null)
                {
                    dropBorder.BorderThickness = new Thickness(0);
                    dropBorder.Opacity = 0;
                }
                _lastHighlightedItem = null;
            }

            _hoverFolderTarget = null;
        }

        private void ClearDragVisuals()
        {
            ClearFolderHighlightOnly();
            HideInsertionIndicator();
        }

        private void ShowInsertionIndicator(LayerBase targetItem, bool insertBefore, LayerBase? visualAnchor = null, bool moveToRoot = false)
        {
            _insertionTarget = targetItem;
            _insertBefore = insertBefore;
            _insertionVisualAnchor = visualAnchor ?? targetItem;

            var container = LayersList.ContainerFromItem(_insertionVisualAnchor) as ListViewItem;
            if (container == null)
            {
                HideInsertionIndicator();
                return;
            }

            var layerBox = this.FindName("LayerBox") as Border;
            if (layerBox == null) return;

            var indicator = FindVisualDescendant<Border>(layerBox, "InsertionIndicator");
            if (indicator == null) return;

            var transform = container.TransformToVisual(LayersList);
            var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

            double yPos = insertBefore ? bounds.Top : bounds.Bottom;

            double leftIndent = 0;
            if (!moveToRoot && _draggedItem != null)
            {
                int targetDepth = 0;

                if (targetItem.Parent != null)
                {
                    targetDepth = targetItem.Depth;
                }
                else if (_draggedItem.Parent != null && ReferenceEquals(targetItem, _draggedItem.Parent))
                {
                    targetDepth = 0;
                }
                else
                {
                    targetDepth = 0;
                }

                leftIndent = targetDepth * 16 + 8;
            }
            else
            {
                leftIndent = 8;
            }

            indicator.Margin = new Thickness(leftIndent, yPos - 1.5, 8, 0);
            indicator.Visibility = Visibility.Visible;
        }

        private void HideInsertionIndicator()
        {
            var layerBox = this.FindName("LayerBox") as Border;
            if (layerBox != null)
            {
                var indicator = FindVisualDescendant<Border>(layerBox, "InsertionIndicator");
                if (indicator != null)
                    indicator.Visibility = Visibility.Collapsed;
            }

            _insertionTarget = null;
            _insertionVisualAnchor = null;
        }

        // --------------------------------------------------------------------
        // DRAG POSITION / FOLDER HELPERS
        // --------------------------------------------------------------------

        private LayerBase? GetItemAtPosition(Windows.Foundation.Point position)
        {
            try
            {
                foreach (var item in _uiLayers)
                {
                    if (item is RootDropZoneFooterItem)
                        continue;

                    var container = LayersList.ContainerFromItem(item) as ListViewItem;
                    if (container == null) continue;

                    var transform = container.TransformToVisual(LayersList);
                    var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                    if (bounds.Contains(position) && item is LayerBase layerBase)
                        return layerBase;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private bool CanMoveIntoFolder(LayerBase source, LayerFolder target)
        {
            if (ReferenceEquals(source, target))
                return false;

            if (source is LayerFolder sourceFolder)
            {
                var current = target.Parent;
                while (current != null)
                {
                    if (ReferenceEquals(current, sourceFolder))
                        return false;
                    current = current.Parent;
                }
            }

            return true;
        }

        private static LayerBase GetBottomMostVisibleDescendant(LayerBase item)
        {
            while (item is LayerFolder f && f.IsExpanded && f.Children.Count > 0)
            {
                item = f.Children[0];
            }
            return item;
        }

        private (LayerBase logicalTail, LayerBase visualTail)? TryGetGroupTailForDragged()
        {
            if (_doc == null || _draggedItem == null) return null;

            if (_draggedItem.Parent == null)
            {
                if (_doc.RootItems.Count == 0) return null;

                var logicalTail = _doc.RootItems[0];
                var visualTail = GetBottomMostVisibleDescendant(logicalTail);
                return (logicalTail, visualTail);
            }

            if (_draggedItem.Parent is LayerFolder pf)
            {
                if (pf.Children.Count == 0) return null;

                var logicalTail = pf.Children[0];
                var visualTail = GetBottomMostVisibleDescendant(logicalTail);
                return (logicalTail, visualTail);
            }

            return null;
        }

        // --------------------------------------------------------------------
        // INDEX COMPUTATIONS (UI <-> Internal mapping)
        // --------------------------------------------------------------------

        private static int ComputeNewInternalIndexFromUiOrder(int count, int draggedInternal, int targetInternal, bool insertBefore)
        {
            int draggedUi = (count - 1) - draggedInternal;
            int targetUi = (count - 1) - targetInternal;

            int insertUi = insertBefore ? targetUi : (targetUi + 1);

            if (draggedUi < insertUi)
                insertUi--;

            insertUi = Math.Clamp(insertUi, 0, count - 1);
            return (count - 1) - insertUi;
        }

        private static int ComputeInsertInternalIndexIntoGroup(int destCountBefore, int targetInternal, bool insertBefore)
        {
            int targetUi = (destCountBefore - 1) - targetInternal;
            int insertUi = insertBefore ? targetUi : (targetUi + 1);

            int desiredInternal = destCountBefore - insertUi;
            return Math.Clamp(desiredInternal, 0, destCountBefore);
        }

        // --------------------------------------------------------------------
        // DRAG INTERACTION STATE
        // --------------------------------------------------------------------

        private void DisableInteractiveElementsDuringDrag()
        {
            foreach (var item in _uiLayers)
            {
                var container = LayersList.ContainerFromItem(item) as ListViewItem;
                if (container == null) continue;

                if (item is LayerFolder)
                {
                    var chevronButton = FindVisualDescendant<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton>(container, "FolderIconButton");
                    if (chevronButton != null)
                        chevronButton.IsHitTestVisible = false;
                }
            }
        }

        private void EnableInteractiveElementsAfterDrag()
        {
            foreach (var item in _uiLayers)
            {
                var container = LayersList.ContainerFromItem(item) as ListViewItem;
                if (container == null) continue;

                if (item is LayerFolder)
                {
                    var chevronButton = FindVisualDescendant<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton>(container, "FolderIconButton");
                    if (chevronButton != null)
                        chevronButton.IsHitTestVisible = true;
                }
            }

            EnableBuiltInDragScroll();
        }

        private void DisableBuiltInDragScroll()
        {
            var scrollViewer = GetLayersListScrollViewer();
            if (scrollViewer != null)
                scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
        }

        private void EnableBuiltInDragScroll()
        {
            var scrollViewer = GetLayersListScrollViewer();
            if (scrollViewer != null)
                scrollViewer.VerticalScrollMode = ScrollMode.Auto;
        }
    }
}
