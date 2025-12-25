using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Compositing.Effects;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.UI.Converters;
using PixlPunkt.UI.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace PixlPunkt.UI.Layers
{
    public sealed partial class LayersPanel : UserControl
    {
        private CanvasDocument? _doc;
        private LayerBase? _pendingRemovedItem;
        private int _pendingRemovedIndex = -1;

        private readonly ObservableCollection<LayerBase> _uiLayers = [];

        private bool _suppressDocRefresh;
        private bool _suppressCollectionMove;
        private bool _suppressSelectionChanged;

        public event Action<int>? SelectionChangedUiIndex;
        private readonly Dictionary<RasterLayer, List<LayerEffectBase>> _fxMuteSnapshots = [];

        // Effects clipboard for Copy/Paste Effects
        private List<LayerEffectBase>? _effectsClipboard;

        // Drag-and-drop state
        private LayerBase? _draggedItem;
        private ListViewItem? _lastHighlightedItem;

        // Insertion indicator state (SOURCE OF TRUTH)
        private LayerBase? _insertionTarget;
        private LayerBase? _insertionVisualAnchor;
        private bool _insertBefore; // true = insert before target, false = insert after

        // Hovered folder (middle-zone drop)
        private LayerFolder? _hoverFolderTarget;

        // Deferred rebuild flag - set when rebuild is needed but skipped due to active drag
        private bool _needsRebuildAfterDrag;

        // Auto-scroll during drag
        private DispatcherTimer? _dragScrollTimer;
        private double _dragScrollDirection; // -1 = up, 0 = none, 1 = down
        private const double DragScrollSpeed = 8.0; // pixels per tick (slow and controlled)
        private const double DragScrollEdgeThreshold = 50.0; // pixels from edge to trigger scroll

        public LayersPanel()
        {
            InitializeComponent();

            if (!Resources.ContainsKey("BooleanToVisibilityConverter"))
                Resources["BooleanToVisibilityConverter"] = new BoolToVisibilityConverter();
            if (!Resources.ContainsKey("InverseBooleanToVisibilityConverter"))
                Resources["InverseBooleanToVisibilityConverter"] = new InverseBoolToVisibilityConverter();

            LayersList.ItemsSource = _uiLayers;
            _uiLayers.CollectionChanged += UiLayers_CollectionChanged;

            // Disable built-in reordering - we handle everything custom
            LayersList.CanDragItems = true;
            LayersList.AllowDrop = true;
            LayersList.CanReorderItems = false; // Disable - we handle all reordering
            LayersList.DragItemsStarting += LayersList_DragItemsStarting;
            LayersList.DragOver += LayersList_DragOver;
            LayersList.Drop += LayersList_Drop;
            LayersList.DragLeave += LayersList_DragLeave;

            // Setup drag scroll timer (slower interval for controlled scrolling)
            _dragScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 ticks per second
            };
            _dragScrollTimer.Tick += DragScrollTimer_Tick;

            UpdateUiEnabled();
        }

        // ────────────────────────────────────────────────────────────────────
        // AUTO-SCROLL DURING DRAG
        // ────────────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────────────
        // FX MUTE
        // ────────────────────────────────────────────────────────────────────

        private void FxMuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn)
                return;

            if (btn.DataContext is not RasterLayer layer)
                return;

            if (btn.IsChecked == true)
            {
                var snapshot = new List<LayerEffectBase>();

                foreach (var fx in layer.Effects)
                {
                    if (fx.IsEnabled)
                    {
                        snapshot.Add(fx);
                        fx.IsEnabled = false;
                    }
                }

                if (snapshot.Count == 0)
                {
                    btn.IsChecked = false;
                    return;
                }

                _fxMuteSnapshots[layer] = snapshot;
            }
            else
            {
                if (_fxMuteSnapshots.TryGetValue(layer, out var snapshot))
                {
                    foreach (var fx in snapshot)
                    {
                        if (layer.Effects.Contains(fx))
                            fx.IsEnabled = true;
                    }

                    _fxMuteSnapshots.Remove(layer);
                }
            }

            _doc?.RaiseStructureChanged();
        }

        // ────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Forces all layer previews to refresh. Call after canvas resize operations.
        /// </summary>
        public void RefreshAllPreviews()
        {
            ForcePreviewRefreshAll();
        }

        public void Bind(CanvasDocument? doc)
        {
            if (_doc != null)
            {
                _doc.LayersChanged -= OnDocLayersChanged;
                _doc.ActiveLayerChanged -= OnDocActiveLayerChanged;
                _doc.StructureChanged -= OnDocStructureChanged;
            }

            _doc = doc;

            if (_doc is null)
            {
                RebuildFromDoc();
                SelectFromDoc();
                ForcePreviewRefreshAll();
                UpdateUiEnabled();
                return;
            }

            _doc.LayersChanged += OnDocLayersChanged;
            _doc.ActiveLayerChanged += OnDocActiveLayerChanged;
            _doc.StructureChanged += OnDocStructureChanged;

            RebuildFromDoc();
            SelectFromDoc();
            ForcePreviewRefreshAll();
            UpdateUiEnabled();
        }

        private void OnDocStructureChanged()
        {
            if (_doc?.ActiveLayer is RasterLayer rl)
                rl.UpdatePreview();
        }

        private void ForcePreviewRefreshAll()
        {
            if (_doc == null) return;
            foreach (var layer in _doc.GetAllRasterLayers())
                layer.UpdatePreview();
        }

        private void RebuildFromDoc()
        {
            // CRITICAL FIX: NEVER rebuild during drag operations!
            // Any collection changes will cause ListView to abort the drag
            if (_draggedItem != null)
            {
                _needsRebuildAfterDrag = true;
                return;
            }

            if (_doc is null)
            {
                _uiLayers.Clear();
                UpdateUiEnabled();
                return;
            }

            _suppressCollectionMove = true;

            _uiLayers.Clear();

            // Get root items in REVERSE order (top-to-bottom for UI)
            var rootItems = _doc.RootItems;

            for (int i = rootItems.Count - 1; i >= 0; i--)
            {
                var item = rootItems[i];

                if (item is LayerFolder folder)
                {
                    // Add folder first
                    _uiLayers.Add(folder);

                    // Then add its children if expanded
                    if (folder.IsExpanded)
                        AddFolderChildrenToUI(folder);
                }
                else
                {
                    _uiLayers.Add(item);
                }
            }

            _suppressCollectionMove = false;

            ForcePreviewRefreshAll();
            UpdateUiEnabled();
        }

        private void AddFolderChildrenToUI(LayerFolder folder)
        {
            var children = folder.Children;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                var child = children[i];

                if (child is LayerFolder childFolder)
                {
                    _uiLayers.Add(childFolder);

                    if (childFolder.IsExpanded)
                        AddFolderChildrenToUI(childFolder);
                }
                else
                {
                    _uiLayers.Add(child);
                }
            }
        }

        private void SelectFromDoc()
        {
            if (_doc is null)
            {
                LayersList.SelectedIndex = -1;
                UpdateActiveLayerIndicators();
                UpdateUiEnabled();
                return;
            }

            var rasters = _doc.GetAllRasterLayers();
            if (rasters.Count == 0)
            {
                LayersList.SelectedIndex = -1;
                UpdateActiveLayerIndicators();
                UpdateUiEnabled();
                return;
            }

            var activeLayer = _doc.ActiveLayer;
            int uiIndex = _uiLayers.IndexOf(activeLayer);

            _suppressSelectionChanged = true;
            try { LayersList.SelectedIndex = uiIndex; }
            finally { _suppressSelectionChanged = false; }

            UpdateActiveLayerIndicators();
            UpdateUiEnabled();
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
                if (item is RasterLayer)
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

        private void UiLayers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_doc is null || _suppressCollectionMove) return;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Move:
                    {
                        // Don't allow reordering if we're dragging (Drop handler owns it)
                        if (_draggedItem != null)
                            break;

                        _doc.RaiseStructureChanged();
                        _doc.CompositeTo(_doc.Surface);
                        SelectFromDoc();
                        _pendingRemovedItem = null;
                        _pendingRemovedIndex = -1;
                        break;
                    }

                case NotifyCollectionChangedAction.Remove:
                    {
                        if (e.OldItems?.Count == 1)
                        {
                            _pendingRemovedItem = (LayerBase)e.OldItems[0]!;
                            _pendingRemovedIndex = e.OldStartingIndex;
                        }
                        break;
                    }

                case NotifyCollectionChangedAction.Add:
                    {
                        if (_pendingRemovedItem != null &&
                            e.NewItems?.Count == 1 &&
                            ReferenceEquals(e.NewItems[0], _pendingRemovedItem))
                        {
                            _doc.RaiseStructureChanged();
                            _doc.CompositeTo(_doc.Surface);
                            SelectFromDoc();
                        }

                        _pendingRemovedItem = null;
                        _pendingRemovedIndex = -1;
                        break;
                    }

                default:
                    _pendingRemovedItem = null;
                    _pendingRemovedIndex = -1;
                    break;
            }
        }

        private void OnDocLayersChanged()
        {
            if (_suppressDocRefresh) return;

            if (_draggedItem != null)
            {
                _needsRebuildAfterDrag = true;
                return;
            }

            RebuildFromDoc();
            SelectFromDoc();
            UpdateUiEnabled();
        }

        private void OnDocActiveLayerChanged()
        {
            SelectFromDoc();
            if (_doc?.ActiveLayer is RasterLayer rl)
                rl.UpdatePreview();
        }

        private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_doc is null) return;
            if (_suppressSelectionChanged) return;

            int ui = LayersList.SelectedIndex;
            if (ui < 0)
            {
                UpdateUiEnabled();
                return;
            }

            var selectedItem = _uiLayers[ui];

            if (selectedItem is RasterLayer raster)
            {
                var rasters = _doc.GetAllRasterLayers();
                int rasterIndex = rasters.IndexOf(raster);
                if (rasterIndex >= 0)
                    _doc.SetActiveLayer(rasterIndex);
            }

            UpdateActiveLayerIndicators();
            UpdateUiEnabled();
        }

        private void LayersList_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
        {
            if (_doc is null)
                e.Handled = true;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) return;
            _doc.AddLayer();
            RebuildFromDoc();
            SelectFromDoc();
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) return;
            _doc.AddFolder();
            RebuildFromDoc();
            SelectFromDoc();
        }

        private async void AddReferenceLayer_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) return;

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".gif");

            // Initialize the picker with the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.PixlPunktMainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            try
            {
                using var stream = await file.OpenReadAsync();
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);

                var transform = new Windows.Graphics.Imaging.BitmapTransform();
                var pixelData = await decoder.GetPixelDataAsync(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    transform,
                    Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation,
                    Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);

                var pixels = pixelData.DetachPixelData();
                int width = (int)decoder.PixelWidth;
                int height = (int)decoder.PixelHeight;

                var name = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                _doc.AddReferenceLayer(name, pixels, width, height, file.Path);

                RebuildFromDoc();
                SelectFromDoc();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load reference image: {ex.Message}");
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) return;
            int ui = LayersList.SelectedIndex;
            if (ui < 0) return;

            var item = _uiLayers[ui];

            if (item is RasterLayer)
            {
                var rasters = _doc.GetAllRasterLayers();
                int rasterIndex = rasters.IndexOf((RasterLayer)item);
                if (rasterIndex >= 0)
                    _doc.RemoveLayer(rasterIndex);
            }
            else
            {
                _doc.RemoveItem(item);
            }

            RebuildFromDoc();
            SelectFromDoc();
        }

        private void FolderChevron_Click(object sender, RoutedEventArgs e)
        {
            if (_draggedItem == null)
            {
                RebuildFromDoc();
            }
            else
            {
                _needsRebuildAfterDrag = true;
            }
        }

        private void Vis_Click(object sender, RoutedEventArgs e) => _doc?.RaiseStructureChanged();
        private void Lock_Click(object sender, RoutedEventArgs e) => _doc?.RaiseStructureChanged();

        private void Item_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (_doc is null) { e.Handled = true; return; }
            if ((sender as FrameworkElement)?.DataContext is LayerBase layer)
                LayersList.SelectedItem = layer;
        }

        private LayerBase? ItemFromSender(object sender)
        {
            if (sender is FrameworkElement fe)
            {
                if (fe.DataContext is LayerBase item) return item;
                if (fe.Parent is MenuFlyout mf &&
                    mf.Target is FrameworkElement target &&
                    target.DataContext is LayerBase item2)
                    return item2;
            }
            return LayersList.SelectedItem as LayerBase;
        }

        private void ItemFlyout_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) return;
            var item = ItemFromSender(sender);
            if (item is null) return;

            if (item is RasterLayer raster)
            {
                var rasters = _doc.GetAllRasterLayers();
                int rasterIndex = rasters.IndexOf(raster);
                if (rasterIndex >= 0)
                    _doc.RemoveLayer(rasterIndex);
            }
            else
            {
                _doc.RemoveItem(item);
            }

            RebuildFromDoc();
            SelectFromDoc();
        }

        private void ItemFlyout_Duplicate_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) return;
            var item = ItemFromSender(sender);
            if (item is null) return;

            _doc.DuplicateLayerTree(item);
            RebuildFromDoc();
            SelectFromDoc();
        }

        private void ItemFlyout_Vis_Toggled(object sender, RoutedEventArgs e) => _doc?.RaiseStructureChanged();
        private void ItemFlyout_Lock_Toggled(object sender, RoutedEventArgs e) => _doc?.RaiseStructureChanged();

        private void UpdateUiEnabled()
        {
            bool hasDoc = _doc != null;
            if (AddBtn != null) AddBtn.IsEnabled = hasDoc;
            if (AddFolderBtnTop != null) AddFolderBtnTop.IsEnabled = hasDoc;
            if (RemoveBtn != null) RemoveBtn.IsEnabled = hasDoc && LayersList.SelectedIndex >= 0;
            if (AddRefLayerBtn != null) AddRefLayerBtn.IsEnabled = hasDoc;
        }

        // ────────────────────────────────────────────────────────────────────
        // NAME EDITING HELPERS
        // ────────────────────────────────────────────────────────────────────

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

        private void LayerName_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                SetEditing(fe, true);
                e.Handled = true;
            }
        }

        private void LayerNameEdit_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;

            if (e.Key == VirtualKey.Enter)
            {
                SetEditing(tb, false);

                if (_draggedItem == null)
                    RebuildFromDoc();
                else
                    _needsRebuildAfterDrag = true;

                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                if (tb.Tag is string original) tb.Text = original;
                SetEditing(tb, false);
                e.Handled = true;
            }
        }

        private void LayerNameEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                SetEditing(tb, false);

                if (_draggedItem == null)
                    RebuildFromDoc();
                else
                    _needsRebuildAfterDrag = true;
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) return;
            if ((sender as FrameworkElement)?.DataContext is not RasterLayer layer) return;

            var win = new LayerSettingsWindow(_doc, layer);
            win.Activate();
            var appW = WindowHost.ApplyChrome(win, resizable: true, alwaysOnTop: true, minimizable: false,
                title: $"Layer Settings - {layer.Name}", owner: App.PixlPunktMainWindow);
            WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.90,
                minLogicalWidth: 100, minLogicalHeight: 100);
            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }

        // ────────────────────────────────────────────────────────────────────
        // DRAG + DROP
        // ────────────────────────────────────────────────────────────────────

        private void LayersList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count > 0 && e.Items[0] is LayerBase item)
            {
                _draggedItem = item;
                e.Data.RequestedOperation = DataPackageOperation.Move;
                e.Data.Properties.Add("DraggedLayer", item);

                DisableInteractiveElementsDuringDrag();
                DisableBuiltInDragScroll();
            }
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

            // 3) Hovering over an item in a folder that isn't our parent => move into that folder at indicated position
            if (targetItem.Parent is LayerFolder targetParentFolder &&
                _draggedItem.Parent != targetParentFolder &&
                CanMoveIntoFolder(_draggedItem, targetParentFolder))
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
                e.DragUIOverride.Caption = $"Move into {targetParentFolder.Name}";
                e.DragUIOverride.IsCaptionVisible = true;
                return;
            }

            // 4) Pulling out to root (hovering root item)
            if (_draggedItem.Parent != null && targetItem.Parent == null)
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
                e.DragUIOverride.Caption = $"Move '{_draggedItem.Name}' to root";
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

            bool handled = false;

            // If we have an insertion target, that is the ONLY target we use.
            // Otherwise, we can fall back to hover-folder (middle zone) or hit-test.
            var position = e.GetPosition(LayersList);
            var targetItem = snapTarget ?? GetItemAtPosition(position);
            bool insertBefore = snapBefore;

            // If we are dropping ON a folder row and we don't have an insertion target,
            // decide right here: edge-zone = reorder, middle-zone = move-into folder.
            if (!handled && snapTarget == null && targetItem is LayerFolder folderHit && CanMoveIntoFolder(_draggedItem, folderHit))
            {
                var container = LayersList.ContainerFromItem(folderHit) as ListViewItem;
                if (container != null)
                {
                    var t = container.TransformToVisual(LayersList);
                    var bounds = t.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                    double edgeThreshold = bounds.Height * 0.25; // you can shrink this later if you want
                    double relY = position.Y - bounds.Top;

                    bool inTopEdge = relY < edgeThreshold;
                    bool inBottomEdge = relY > (bounds.Height - edgeThreshold);

                    // Only treat as reorder if it's the SAME parent group (root->root or same folder)
                    bool sameParent = _draggedItem.Parent == folderHit.Parent;

                    if (sameParent && (inTopEdge || inBottomEdge))
                    {
                        // Edge-zone reorder: behave like the insertion indicator would
                        targetItem = folderHit;
                        insertBefore = inTopEdge;
                        // let your existing reorder logic run (step 3)
                    }
                    else
                    {
                        // Middle-zone: MOVE INTO folder
                        _doc.MoveLayerToFolder(_draggedItem, folderHit);
                        if (!folderHit.IsExpanded)
                            folderHit.IsExpanded = true;

                        handled = true;
                    }
                }
                else
                {
                    // If we can't resolve bounds, default to "move into folder"
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

            // 2) Moving INTO a folder at an indicated position (line shown over a child)
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
                            _doc.MoveRootItem(_draggedItem, to);
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

                    _doc.MoveLayerToFolder(_draggedItem, null); // moves to root (appends)

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
                                _doc.MoveRootItem(_draggedItem, desiredInternal);
                        }
                    }

                    handled = true;
                }
                else
                {
                    // Dropped in empty space -> move to root (default append/top)
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

        // ────────────────────────────────────────────────────────────────────
        // DRAG VISUALS
        // ────────────────────────────────────────────────────────────────────

        private void HighlightFolder(LayerFolder folder)
        {
            _hoverFolderTarget = folder;

            var container = LayersList.ContainerFromItem(folder) as ListViewItem;
            if (container == null) return;

            var dropBorder = FindVisualDescendant<Border>(container, "DropTargetBorder");
            if (dropBorder == null) return;

            // Clear previous highlight only (do not touch insertion indicator)
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

        private void ShowInsertionIndicator(LayerBase targetItem, bool insertBefore, LayerBase? visualAnchor = null)
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
            indicator.Margin = new Thickness(0, yPos - 1.5, 0, 0);
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

        private LayerBase? GetItemAtPosition(Windows.Foundation.Point position)
        {
            try
            {
                foreach (var item in _uiLayers)
                {
                    var container = LayersList.ContainerFromItem(item) as ListViewItem;
                    if (container == null) continue;

                    var transform = container.TransformToVisual(LayersList);
                    var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                    if (bounds.Contains(position))
                        return item;
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
                // Children are stored bottom-to-top internally; index 0 is the visual bottom
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

        private void DisableInteractiveElementsDuringDrag()
        {
            foreach (var item in _uiLayers)
            {
                var container = LayersList.ContainerFromItem(item) as ListViewItem;
                if (container == null) continue;

                if (item is LayerFolder)
                {
                    var chevronButton = FindVisualDescendant<ToggleButton>(container, "FolderIconButton");
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
                    var chevronButton = FindVisualDescendant<ToggleButton>(container, "FolderIconButton");
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

        // ────────────────────────────────────────────────────────────────────
        // COPY/PASTE EFFECTS
        // ────────────────────────────────────────────────────────────────────

        private void CopyEffects_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) return;

            var layer = ItemFromSender(sender);
            if (layer is not RasterLayer rasterLayer) return;

            if (rasterLayer.Effects.Count == 0)
            {
                _effectsClipboard = null;
                return;
            }

            _effectsClipboard = [];
            foreach (var fx in rasterLayer.Effects)
            {
                var cloned = EffectCloner.Clone(fx);
                if (cloned != null)
                    _effectsClipboard.Add(cloned);
            }
        }

        private void PasteEffects_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) return;
            if (_effectsClipboard == null || _effectsClipboard.Count == 0) return;

            var layer = ItemFromSender(sender);
            if (layer is not RasterLayer rasterLayer) return;

            rasterLayer.Effects.Clear();

            foreach (var fx in _effectsClipboard)
            {
                var cloned = EffectCloner.Clone(fx);
                if (cloned != null)
                    rasterLayer.Effects.Add(cloned);
            }

            _doc.RaiseStructureChanged();
        }

        private void ItemFlyout_FlattenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            var item = ItemFromSender(sender);
            if (item is not LayerFolder folder) return;

            if (_doc.FlattenFolderVisible(folder))
            {
                _doc.CompositeTo(_doc.Surface);
                RebuildFromDoc();
                SelectFromDoc();
            }
        }

        private void ItemFlyout_MergeDown_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            var item = ItemFromSender(sender);
            if (item is not RasterLayer raster) return;

            if (_doc.MergeDown(raster))
            {
                _doc.CompositeTo(_doc.Surface);
                RebuildFromDoc();
                SelectFromDoc();
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // LAYER MASK OPERATIONS
        // ────────────────────────────────────────────────────────────────────

        private void AddMask_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            var item = ItemFromSender(sender);
            if (item is not RasterLayer raster) return;

            // Only add mask if layer doesn't already have one
            if (raster.HasMask) return;

            raster.CreateMask();
            _doc.RaiseStructureChanged();
        }

        private void DeleteMask_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            var item = ItemFromSender(sender);
            if (item is not RasterLayer raster) return;

            if (!raster.HasMask) return;

            raster.RemoveMask();
            _doc.RaiseStructureChanged();
        }

        private void ApplyMask_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            var item = ItemFromSender(sender);
            if (item is not RasterLayer raster) return;

            if (!raster.HasMask) return;

            raster.ApplyMask();
            _doc.RaiseStructureChanged();
        }

        // ────────────────────────────────────────────────────────────────────
        // MASK EDITING MODE
        // ────────────────────────────────────────────────────────────────────

        private void LayerPreview_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_doc == null) return;
            if ((sender as FrameworkElement)?.DataContext is not RasterLayer layer) return;

            // Switch to layer editing mode
            layer.IsEditingMask = false;

            // Also select this layer as active
            var rasters = _doc.GetAllRasterLayers();
            int rasterIndex = rasters.IndexOf(layer);
            if (rasterIndex >= 0)
                _doc.SetActiveLayer(rasterIndex);

            _doc.RaiseStructureChanged();
            e.Handled = true;
        }

        private void MaskPreview_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_doc == null) return;
            if ((sender as FrameworkElement)?.DataContext is not RasterLayer layer) return;

            if (!layer.HasMask) return;

            // Switch to mask editing mode
            layer.IsEditingMask = true;

            // Also select this layer as active
            var rasters = _doc.GetAllRasterLayers();
            int rasterIndex = rasters.IndexOf(layer);
            if (rasterIndex >= 0)
                _doc.SetActiveLayer(rasterIndex);

            _doc.RaiseStructureChanged();
            e.Handled = true;
        }

        // ────────────────────────────────────────────────────────────────────
        // MASK TOGGLE CONTROLS
        // ────────────────────────────────────────────────────────────────────

        private void MaskEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            // The two-way binding already updated Mask.IsEnabled
            // Just trigger recomposite
            _doc.RaiseStructureChanged();
        }

        private void MaskInvert_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            // The two-way binding already updated Mask.IsInverted
            // Just trigger recomposite
            _doc.RaiseStructureChanged();
        }

        // ────────────────────────────────────────────────────────────────────
        // REFERENCE LAYER OPERATIONS
        // ────────────────────────────────────────────────────────────────────

        private void RefLayer_FitToCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            var item = ItemFromSender(sender);
            if (item is not ReferenceLayer refLayer) return;

            refLayer.FitToCanvas(_doc.PixelWidth, _doc.PixelHeight, 0.05f);
            _doc.RaiseStructureChanged();
        }

        private void RefLayer_ResetTransform_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            var item = ItemFromSender(sender);
            if (item is not ReferenceLayer refLayer) return;

            refLayer.ResetTransform();
            _doc.RaiseStructureChanged();
        }

        private void RefLayerSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            var item = ItemFromSender(sender);
            if (item is not ReferenceLayer refLayer) return;

            var win = new ReferenceLayerSettingsWindow(_doc, refLayer);
            win.Activate();
            var appW = WindowHost.ApplyChrome(win, resizable: true, alwaysOnTop: true, minimizable: false,
                title: $"Reference Layer - {refLayer.Name}", owner: App.PixlPunktMainWindow);
            WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.90,
                minLogicalWidth: 300, minLogicalHeight: 200);
            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }
    }
}
