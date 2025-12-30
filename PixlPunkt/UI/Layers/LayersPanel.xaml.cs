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
using PixlPunkt.Core.Logging;
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

            // Wire up flyout events
            WireFlyoutEvents();

            UpdateUiEnabled();
        }

        // ────────────────────────────────────────────────────────────────────
        // FLYOUT EVENT WIRING
        // ────────────────────────────────────────────────────────────────────

        public void WireFlyoutEvents()
        {
            // Panel menu flyout (empty area right-click)
            PanelMenuFlyout.AddLayerRequested += (s, e) => Add_Click(s, new RoutedEventArgs());
            PanelMenuFlyout.AddFolderRequested += (s, e) => AddFolder_Click(s, new RoutedEventArgs());
            PanelMenuFlyout.AddReferenceLayerRequested += (s, e) => AddReferenceLayer_Click(s, new RoutedEventArgs());
            PanelMenuFlyout.RemoveSelectedRequested += (s, e) => Remove_Click(s, new RoutedEventArgs());

            // Raster layer flyout
            RasterLayerMenuFlyout.SettingsRequested += OnRasterSettings;
            RasterLayerMenuFlyout.CopyEffectsRequested += OnCopyEffects;
            RasterLayerMenuFlyout.PasteEffectsRequested += OnPasteEffects;
            RasterLayerMenuFlyout.AddMaskRequested += OnAddMask;
            RasterLayerMenuFlyout.DeleteMaskRequested += OnDeleteMask;
            RasterLayerMenuFlyout.ApplyMaskRequested += OnApplyMask;
            RasterLayerMenuFlyout.VisibleToggled += OnLayerVisibleToggled;
            RasterLayerMenuFlyout.LockedToggled += OnLayerLockedToggled;
            RasterLayerMenuFlyout.DuplicateRequested += OnDuplicateLayer;
            RasterLayerMenuFlyout.MergeDownRequested += OnMergeDown;
            RasterLayerMenuFlyout.RemoveRequested += OnRemoveRasterLayer;

            // Folder flyout
            FolderMenuFlyout.VisibleToggled += OnFolderVisibleToggled;
            FolderMenuFlyout.LockedToggled += OnFolderLockedToggled;
            FolderMenuFlyout.DuplicateRequested += OnDuplicateFolder;
            FolderMenuFlyout.FlattenFolderRequested += OnFlattenFolder;
            FolderMenuFlyout.RemoveRequested += OnRemoveFolder;

            // Reference layer flyout
            RefLayerMenuFlyout.SettingsRequested += OnRefLayerSettings;
            RefLayerMenuFlyout.VisibleToggled += OnRefLayerVisibleToggled;
            RefLayerMenuFlyout.LockedToggled += OnRefLayerLockedToggled;
            RefLayerMenuFlyout.FitToCanvasRequested += OnFitToCanvas;
            RefLayerMenuFlyout.ResetTransformRequested += OnResetTransform;
            RefLayerMenuFlyout.RemoveRequested += OnRemoveRefLayer;
        }

        // ────────────────────────────────────────────────────────────────────
        // RASTER LAYER FLYOUT HANDLERS
        // ────────────────────────────────────────────────────────────────────

        private void OnRasterSettings(object? sender, RasterLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;

                var win = new LayerSettingsWindow(_doc, layer);
                win.Activate();
                var appW = WindowHost.ApplyChrome(win, resizable: true, alwaysOnTop: true, minimizable: false,
                    title: $"Layer Settings - {layer.Name}", owner: App.PixlPunktMainWindow);
                WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.90,
                    minLogicalWidth: 100, minLogicalHeight: 100);
                WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnRasterSettings error: {ex.Message}");
            }
        }

        private void OnCopyEffects(object? sender, RasterLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;

                if (layer.Effects.Count == 0)
                {
                    _effectsClipboard = null;
                    return;
                }

                _effectsClipboard = [];
                foreach (var fx in layer.Effects)
                {
                    var cloned = EffectCloner.Clone(fx);
                    if (cloned != null)
                        _effectsClipboard.Add(cloned);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnCopyEffects error: {ex.Message}");
            }
        }

        private void OnPasteEffects(object? sender, RasterLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;
                if (_effectsClipboard == null || _effectsClipboard.Count == 0) return;

                layer.Effects.Clear();

                foreach (var fx in _effectsClipboard)
                {
                    var cloned = EffectCloner.Clone(fx);
                    if (cloned != null)
                        layer.Effects.Add(cloned);
                }

                _doc.RaiseStructureChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnPasteEffects error: {ex.Message}");
            }
        }

        private void OnAddMask(object? sender, RasterLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;
                if (layer.HasMask) return;

                layer.CreateMask();
                _doc.RaiseStructureChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnAddMask error: {ex.Message}");
            }
        }

        private void OnDeleteMask(object? sender, RasterLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;
                if (!layer.HasMask) return;

                layer.RemoveMask();
                _doc.RaiseStructureChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnDeleteMask error: {ex.Message}");
            }
        }

        private void OnApplyMask(object? sender, RasterLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;
                if (!layer.HasMask) return;

                layer.ApplyMask();
                _doc.RaiseStructureChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnApplyMask error: {ex.Message}");
            }
        }

        private void OnLayerVisibleToggled(object? sender, RasterLayer? layer)
        {
            _doc?.RaiseStructureChanged();
        }

        private void OnLayerLockedToggled(object? sender, RasterLayer? layer)
        {
            _doc?.RaiseStructureChanged();
        }

        private void OnDuplicateLayer(object? sender, RasterLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;

                _doc.DuplicateLayerTree(layer);
                RebuildFromDoc();
                SelectFromDoc();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnDuplicateLayer error: {ex.Message}");
            }
        }

        private void OnMergeDown(object? sender, RasterLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;

                if (_doc.MergeDown(layer))
                {
                    _doc.CompositeTo(_doc.Surface);
                    RebuildFromDoc();
                    SelectFromDoc();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnMergeDown error: {ex.Message}");
            }
        }

        private void OnRemoveRasterLayer(object? sender, RasterLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;

                var rasters = _doc.GetAllRasterLayers();
                int rasterIndex = rasters.IndexOf(layer);
                if (rasterIndex >= 0)
                    _doc.RemoveLayer(rasterIndex);

                RebuildFromDoc();
                SelectFromDoc();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnRemoveRasterLayer error: {ex.Message}");
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // FOLDER FLYOUT HANDLERS
        // ────────────────────────────────────────────────────────────────────

        private void OnFolderVisibleToggled(object? sender, LayerFolder? folder)
        {
            _doc?.RaiseStructureChanged();
        }

        private void OnFolderLockedToggled(object? sender, LayerFolder? folder)
        {
            _doc?.RaiseStructureChanged();
        }

        private void OnDuplicateFolder(object? sender, LayerFolder? folder)
        {
            try
            {
                if (_doc == null || folder == null) return;

                _doc.DuplicateLayerTree(folder);
                RebuildFromDoc();
                SelectFromDoc();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnDuplicateFolder error: {ex.Message}");
            }
        }

        private void OnFlattenFolder(object? sender, LayerFolder? folder)
        {
            try
            {
                if (_doc == null || folder == null) return;

                if (_doc.FlattenFolderVisible(folder))
                {
                    _doc.CompositeTo(_doc.Surface);
                    RebuildFromDoc();
                    SelectFromDoc();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnFlattenFolder error: {ex.Message}");
            }
        }

        private void OnRemoveFolder(object? sender, LayerFolder? folder)
        {
            try
            {
                if (_doc == null || folder == null) return;

                _doc.RemoveItem(folder);
                RebuildFromDoc();
                SelectFromDoc();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnRemoveFolder error: {ex.Message}");
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // REFERENCE LAYER FLYOUT HANDLERS
        // ────────────────────────────────────────────────────────────────────

        private void OnRefLayerSettings(object? sender, ReferenceLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;

                var win = new ReferenceLayerSettingsWindow(_doc, layer);
                win.Activate();
                var appW = WindowHost.ApplyChrome(win, resizable: true, alwaysOnTop: true, minimizable: false,
                    title: $"Reference Layer - {layer.Name}", owner: App.PixlPunktMainWindow);
                WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.90,
                    minLogicalWidth: 300, minLogicalHeight: 200);
                WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnRefLayerSettings error: {ex.Message}");
            }
        }

        private void OnRefLayerVisibleToggled(object? sender, ReferenceLayer? layer)
        {
            _doc?.RaiseStructureChanged();
        }

        private void OnRefLayerLockedToggled(object? sender, ReferenceLayer? layer)
        {
            _doc?.RaiseStructureChanged();
        }

        private void OnFitToCanvas(object? sender, ReferenceLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;

                layer.FitToCanvas(_doc.PixelWidth, _doc.PixelHeight, 0.05f);
                _doc.RaiseStructureChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnFitToCanvas error: {ex.Message}");
            }
        }

        private void OnResetTransform(object? sender, ReferenceLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;

                layer.ResetTransform();
                _doc.RaiseStructureChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnResetTransform error: {ex.Message}");
            }
        }

        private void OnRemoveRefLayer(object? sender, ReferenceLayer? layer)
        {
            try
            {
                if (_doc == null || layer == null) return;

                _doc.RemoveItem(layer);
                RebuildFromDoc();
                SelectFromDoc();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayersPanel] OnRemoveRefLayer error: {ex.Message}");
            }
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

        // NOTE: LayersList_ContextRequested is no longer needed since we use reusable flyout controls
        // that handle their own XamlRoot. Keeping a minimal version for safety.
        private void LayersList_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
        {
            // Context menus are now handled via Item_RightTapped and LayersList_RightTapped
            // using the reusable flyout controls
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

        private void FolderChevron_Click(object sender, RoutedEventArgs e) => _doc?.RaiseStructureChanged();
        private void Vis_Click(object sender, RoutedEventArgs e) => _doc?.RaiseStructureChanged();
        private void Lock_Click(object sender, RoutedEventArgs e) => _doc?.RaiseStructureChanged();

        private void Item_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (_doc is null) { e.Handled = true; return; }
            
            try
            {
                // Get the element that was right-tapped
                var element = sender as FrameworkElement;
                if (element == null) { e.Handled = true; return; }
                
                // Get the layer from DataContext
                var layer = element.DataContext as LayerBase;
                if (layer == null) { e.Handled = true; return; }

                // Select the layer
                LayersList.SelectedItem = layer;

                // Get XamlRoot from element (works correctly in both docked and undocked scenarios)
                var xamlRoot = element.XamlRoot ?? this.XamlRoot;
                if (xamlRoot == null) { e.Handled = true; return; }

                // Show the appropriate flyout based on layer type
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

        /// <summary>
        /// Handles right-click on the ListView itself (empty area).
        /// </summary>
        private void LayersList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (_doc is null) { e.Handled = true; return; }

            try
            {
                // Only show panel menu if clicking on empty area (not on an item)
                var element = e.OriginalSource as FrameworkElement;
                
                // Check if we clicked on a layer item (has LayerBase in DataContext)
                while (element != null)
                {
                    if (element.DataContext is LayerBase)
                    {
                        // Clicked on a layer item - don't show panel menu
                        return;
                    }
                    element = element.Parent as FrameworkElement;
                }

                // Clicked on empty area - show panel context menu
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

        private void UpdateUiEnabled()
        {
            bool hasDoc = _doc != null;
            if (AddBtn != null) AddBtn.IsEnabled = hasDoc;
            if (AddFolderBtnTop != null) AddFolderBtnTop.IsEnabled = hasDoc;
            if (RemoveBtn != null) RemoveBtn.IsEnabled = hasDoc && LayersList.SelectedIndex >= 0;
            if (AddRefLayerBtn != null) AddRefLayerBtn.IsEnabled = hasDoc;
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

        // Drag-and-drop event handlers (stubs, real logic should be present in the file)
        private void LayersList_DragItemsStarting(object sender, DragItemsStartingEventArgs e) { /* ... */ }
        private void LayersList_DragOver(object sender, DragEventArgs e) { /* ... */ }
        private void LayersList_Drop(object sender, DragEventArgs e) { /* ... */ }
        private void LayersList_DragLeave(object sender, DragEventArgs e) { /* ... */ }

        // Restore missing event handler stubs for XAML
        private void LayerName_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) { /* ... */ }
        private void LayerNameEdit_LostFocus(object sender, RoutedEventArgs e) { /* ... */ }
        private void LayerNameEdit_KeyDown(object sender, KeyRoutedEventArgs e) { /* ... */ }
        private void Settings_Click(object sender, RoutedEventArgs e) { /* ... */ }
        private void MaskEnabled_Click(object sender, RoutedEventArgs e) { /* ... */ }
        private void MaskInvert_Click(object sender, RoutedEventArgs e) { /* ... */ }
        private void MaskPreview_PointerPressed(object sender, PointerRoutedEventArgs e) { /* ... */ }
        private void LayerPreview_PointerPressed(object sender, PointerRoutedEventArgs e) { /* ... */ }
        private void RefLayerSettings_Click(object sender, RoutedEventArgs e) { /* ... */ }
        private void RootDropZone_DragOver(object sender, DragEventArgs e) { /* ... */ }
        private void RootDropZone_Drop(object sender, DragEventArgs e) { /* ... */ }
        private void RootDropZone_DragLeave(object sender, DragEventArgs e) { /* ... */ }
    }
}
