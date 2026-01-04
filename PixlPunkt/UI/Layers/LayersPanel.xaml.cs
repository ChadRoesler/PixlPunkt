using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Compositing.Effects;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Logging;
using PixlPunkt.UI.Converters;
using PixlPunkt.UI.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Windows.System;

namespace PixlPunkt.UI.Layers
{
    /// <summary>
    /// Main partial class for LayersPanel containing fields, constructor, document binding, and core event handlers.
    /// Other functionality is split into partial files:
    /// - LayersPanel.Flyouts.cs: Flyout event wiring and handlers
    /// - LayersPanel.DragDrop.cs: Drag and drop logic
    /// - LayersPanel.RootDropZone.cs: Root drop zone for moving items out of folders
    /// - LayersPanel.Helpers.cs: Visual tree helpers and UI utilities
    /// - LayersPanel.Masks.cs: Layer mask operations
    /// </summary>
    public sealed partial class LayersPanel : UserControl
    {
        // ====================================================================
        // FIELDS
        // ====================================================================

        private CanvasDocument? _doc;
        private LayerBase? _pendingRemovedItem;
        private int _pendingRemovedIndex = -1;

        private readonly ObservableCollection<object> _uiLayers = [];

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
        private bool _insertBefore;

        // Hovered folder (middle-zone drop)
        private LayerFolder? _hoverFolderTarget;

        // Deferred rebuild flag
        private bool _needsRebuildAfterDrag;

        // Auto-scroll during drag
        private DispatcherTimer? _dragScrollTimer;
        private double _dragScrollDirection;
        private const double DragScrollSpeed = 8.0;
        private const double DragScrollEdgeThreshold = 50.0;

        // Root drop zone border reference
        private Border? _rootDropZoneBorder;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

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
            LayersList.CanReorderItems = false;
            LayersList.DragItemsStarting += LayersList_DragItemsStarting;
            LayersList.DragOver += LayersList_DragOver;
            LayersList.Drop += LayersList_Drop;
            LayersList.DragLeave += LayersList_DragLeave;

            // Setup drag scroll timer
            _dragScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _dragScrollTimer.Tick += DragScrollTimer_Tick;

            // Wire up flyout events
            WireFlyoutEvents();

            UpdateUiEnabled();
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

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

            // Defensive cleanup: reset any stuck drag state when switching documents
            if (_draggedItem != null)
            {
                _draggedItem = null;
                _needsRebuildAfterDrag = false;
                ClearDragVisuals();
                StopDragScroll();
                EnableInteractiveElementsAfterDrag();
                HideRootDropZone();
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

        // ====================================================================
        // DOCUMENT EVENT HANDLERS
        // ====================================================================

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
            // CRITICAL: NEVER rebuild during drag operations!
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
                    _uiLayers.Add(folder);

                    if (folder.IsExpanded)
                        AddFolderChildrenToUI(folder);
                }
                else
                {
                    _uiLayers.Add(item);
                }
            }

            // Add the root drop zone footer item
            _uiLayers.Add(RootDropZoneFooterItem.Instance);

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

        private void UiLayers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_doc is null || _suppressCollectionMove) return;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Move:
                    {
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

        // ====================================================================
        // UI EVENT HANDLERS
        // ====================================================================

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

            // Prevent selecting the footer item
            if (selectedItem is RootDropZoneFooterItem)
            {
                _suppressSelectionChanged = true;
                LayersList.SelectedIndex = -1;
                _suppressSelectionChanged = false;
                UpdateUiEnabled();
                return;
            }

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

            if (item is RootDropZoneFooterItem) return;

            if (item is RasterLayer)
            {
                var rasters = _doc.GetAllRasterLayers();
                int rasterIndex = rasters.IndexOf((RasterLayer)item);
                if (rasterIndex >= 0)
                    _doc.RemoveLayer(rasterIndex);
            }
            else if (item is LayerBase layerBase)
            {
                _doc.RemoveItem(layerBase);
            }

            RebuildFromDoc();
            SelectFromDoc();
        }

        private void FolderChevron_Click(object sender, RoutedEventArgs e) => _doc?.RaiseStructureChanged();
        private void Vis_Click(object sender, RoutedEventArgs e) => _doc?.RaiseStructureChanged();
        private void Lock_Click(object sender, RoutedEventArgs e) => _doc?.RaiseStructureChanged();

        // ====================================================================
        // FX MUTE
        // ====================================================================

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

        // ====================================================================
        // CONTEXT MENU / RIGHT-CLICK
        // ====================================================================

        private void Item_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (_doc is null) { e.Handled = true; return; }

            try
            {
                var element = sender as FrameworkElement;
                if (element == null) { e.Handled = true; return; }

                var layer = element.DataContext as LayerBase;
                if (layer == null) { e.Handled = true; return; }

                LayersList.SelectedItem = layer;

                var xamlRoot = element.XamlRoot ?? this.XamlRoot;
                if (xamlRoot == null) { e.Handled = true; return; }

                if (layer is RasterLayer rasterLayer)
                {
                    var flyout = new Controls.RasterLayerMenuFlyout();
                    flyout.SettingsRequested += OnRasterSettings;
                    flyout.CopyEffectsRequested += OnCopyEffects;
                    flyout.PasteEffectsRequested += OnPasteEffects;
                    flyout.AddMaskRequested += OnAddMask;
                    flyout.DeleteMaskRequested += OnDeleteMask;
                    flyout.ApplyMaskRequested += OnApplyMask;
                    flyout.VisibleToggled += OnLayerVisibleToggled;
                    flyout.LockedToggled += OnLayerLockedToggled;
                    flyout.DuplicateRequested += OnDuplicateLayer;
                    flyout.MergeDownRequested += OnMergeDown;
                    flyout.RemoveRequested += OnRemoveRasterLayer;
                    flyout.ShowAt(element, rasterLayer, xamlRoot);
                }
                else if (layer is LayerFolder folder)
                {
                    var flyout = new Controls.LayerFolderMenuFlyout();
                    flyout.VisibleToggled += OnFolderVisibleToggled;
                    flyout.LockedToggled += OnFolderLockedToggled;
                    flyout.DuplicateRequested += OnDuplicateFolder;
                    flyout.FlattenFolderRequested += OnFlattenFolder;
                    flyout.RemoveRequested += OnRemoveFolder;
                    flyout.ShowAt(element, folder, xamlRoot);
                }
                else if (layer is ReferenceLayer refLayer)
                {
                    var flyout = new Controls.ReferenceLayerMenuFlyout();
                    flyout.SettingsRequested += OnRefLayerSettings;
                    flyout.VisibleToggled += OnRefLayerVisibleToggled;
                    flyout.LockedToggled += OnRefLayerLockedToggled;
                    flyout.FitToCanvasRequested += OnFitToCanvas;
                    flyout.ResetTransformRequested += OnResetTransform;
                    flyout.RemoveRequested += OnRemoveRefLayer;
                    flyout.ShowAt(element, refLayer, xamlRoot);
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                LoggingService.Debug($"[LayersPanel] Item_RightTapped error: {ex.Message}");
                e.Handled = true;
            }
        }

        private void LayersList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (_doc is null) { e.Handled = true; return; }

            try
            {
                var element = e.OriginalSource as FrameworkElement;

                while (element != null)
                {
                    if (element.DataContext is LayerBase)
                    {
                        return;
                    }
                    element = element.Parent as FrameworkElement;
                }

                var xamlRoot = LayersList.XamlRoot ?? this.XamlRoot;
                if (xamlRoot != null)
                {
                    PanelMenuFlyout.ShowAt(LayersList, xamlRoot);
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                LoggingService.Debug($"[LayersPanel] LayersList_RightTapped error: {ex.Message}");
                e.Handled = true;
            }
        }

        private LayerBase? ItemFromSender(object sender)
        {
            try
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
            catch
            {
                return LayersList.SelectedItem as LayerBase;
            }
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

        // ====================================================================
        // NAME EDITING
        // ====================================================================

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
            WindowHost.Place(appW, Core.Enums.WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }
    }
}
