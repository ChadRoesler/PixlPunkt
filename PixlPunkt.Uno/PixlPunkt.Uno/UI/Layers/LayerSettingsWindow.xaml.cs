using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Enums;
using Windows.ApplicationModel.DataTransfer;

namespace PixlPunkt.Uno.UI.Layers
{
    /// <summary>
    /// Layer settings dialog for a single raster layer.
    /// - Live-binds opacity, blend mode, visibility and lock state to the underlying layer.
    /// - Supports "Solo" visibility with a reversible snapshot for cancel/restore semantics.
    /// - Hosts an effects panel with dynamically generated UI from EffectRegistry.
    /// - Keeps a local editable copy of the name that is only committed on Apply.
    /// </summary>
    public sealed partial class LayerSettingsWindow : Window
    {
        // ────────────────────────────────────────────────────────────────────
        // FIELDS - MODEL REFERENCES
        // ────────────────────────────────────────────────────────────────────

        private readonly CanvasDocument _doc;
        private readonly RasterLayer _layer;

        // ────────────────────────────────────────────────────────────────────
        // FIELDS - UI STATE SNAPSHOTS
        // ────────────────────────────────────────────────────────────────────

        // Stores per-layer visibility while "Solo" is active so we can restore it.
        private Dictionary<RasterLayer, bool>? _visSnapshot;

        // For Cancel: remember original values to restore non-committed changes.
        private byte _origOpacity;
        private BlendMode _origBlend;
        private string _origName = string.Empty;

        // ────────────────────────────────────────────────────────────────────
        // FIELDS - DRAG AND DROP
        // ────────────────────────────────────────────────────────────────────

        private LayerEffectBase? _draggedEffect;
        private LayerEffectBase? _insertionTarget;
        private bool _insertBefore;

        // Auto-scroll during drag
        private DispatcherTimer? _dragScrollTimer;
        private double _dragScrollDirection; // -1 = up, 0 = none, 1 = down
        private const double DragScrollSpeed = 20.0; // pixels per tick
        private const double DragScrollEdgeThreshold = 30.0; // pixels from edge to trigger scroll

        // ────────────────────────────────────────────────────────────────────
        // PROPERTIES - BINDABLE
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Local editable name bound to a TextBox; only written back on Apply.
        /// </summary>
        public string EditableName { get; set; } = string.Empty;

        /// <summary>
        /// Blend mode choices for a ComboBox.
        /// </summary>
        public Array BlendModes { get; } = Enum.GetValues(typeof(BlendMode));

        /// <summary>
        /// ViewModel driving the effects list.
        /// </summary>
        public LayerSettingsViewModel ViewModel { get; }

        // ────────────────────────────────────────────────────────────────────
        // CTOR
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a layer settings window bound to the specified document and layer.
        /// </summary>
        public LayerSettingsWindow(CanvasDocument doc, RasterLayer layer)
        {
            _doc = doc;
            _layer = layer;

            // Capture "original" snapshot for Cancel semantics.
            _origOpacity = layer.Opacity;
            _origBlend = layer.Blend;
            _origName = layer.Name ?? string.Empty;

            // Local editable copy for the name box.
            EditableName = _origName;

            // Initialize VM BEFORE InitializeComponent so Binding can see it.
            ViewModel = new LayerSettingsViewModel(doc, layer);

            InitializeComponent();

            // Keep DataContext on the layer so Opacity/Blend/Vis/Lock stay live-bound.
            Root.DataContext = _layer;

            Title = $"Layer Settings - {_origName}";

            // Setup drag scroll timer (slower interval for controlled scrolling)
            _dragScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 ticks per second
            };
            _dragScrollTimer.Tick += DragScrollTimer_Tick;
        }

        // ────────────────────────────────────────────────────────────────────
        // INTERNAL HELPERS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Small helper to notify the document that something affecting composition changed.
        /// </summary>
        private void NotifyStructureChanged() => _doc.RaiseStructureChanged();

        /// <summary>
        /// Gets the ScrollViewer from the ListView.
        /// </summary>
        private ScrollViewer? GetEffectsListScrollViewer()
        {
            return FindVisualChild<ScrollViewer>(EffectsList);
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
            return default;
        }

        // ────────────────────────────────────────────────────────────────────
        // DYNAMIC EFFECT OPTIONS UI
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Handles the Loaded event for effect options ContentControl.
        /// Dynamically generates UI from the effect's registration.
        /// </summary>
        private void EffectOptionsHost_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ContentControl host)
                return;

            // Get the effect from DataContext since Content may have been replaced
            var effect = host.DataContext as LayerEffectBase;
            if (effect == null)
                return;

            // Generate dynamic UI panel from EffectRegistry
            var panel = DynamicEffectTemplateSelector.CreateEffectPanel(effect, NotifyStructureChanged);
            host.Content = panel;
        }

        // ────────────────────────────────────────────────────────────────────
        // EFFECTS PANEL - UI EVENTS
        // ────────────────────────────────────────────────────────────────────

        private void EffectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EffectsList.SelectedItem is LayerEffectBase fx)
                ViewModel.SelectedEffect = fx;
            else
                ViewModel.SelectedEffect = null;
        }

        private void EffectMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is LayerEffectBase fx)
            {
                ViewModel.MoveEffectUp(fx);
                RefreshEffectPanels();
            }
        }

        private void EffectMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is LayerEffectBase fx)
            {
                ViewModel.MoveEffectDown(fx);
                RefreshEffectPanels();
            }
        }

        /// <summary>
        /// Refreshes all effect panels after reordering to ensure correct content.
        /// </summary>
        private void RefreshEffectPanels()
        {
            // Force the ListView to re-realize its items
            var items = EffectsList.ItemsSource;
            EffectsList.ItemsSource = null;
            EffectsList.ItemsSource = items;
        }

        // ────────────────────────────────────────────────────────────────────
        // EFFECTS DRAG AND DROP
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Handles pointer pressed on the drag handle to manually initiate drag.
        /// </summary>
        private async void EffectDragHandle_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement element)
                return;

            // Find the effect from the DataContext
            var effect = FindEffectFromElement(element);
            if (effect == null)
                return;

            // Mark as handled to prevent Expander from toggling
            e.Handled = true;

            // Start the drag operation
            _draggedEffect = effect;

            // Disable ListView's built-in auto-scroll during drag - we handle it ourselves
            DisableBuiltInDragScroll();

            try
            {
                var dataPackage = new DataPackage();
                dataPackage.RequestedOperation = DataPackageOperation.Move;
                dataPackage.Properties["DraggedEffect"] = effect;

                await element.StartDragAsync(e.GetCurrentPoint(element));
            }
            catch
            {
                // Drag was cancelled or failed
                _draggedEffect = null;
                EnableBuiltInDragScroll();
            }
        }

        /// <summary>
        /// Finds the LayerEffectBase from an element in the visual tree.
        /// </summary>
        private LayerEffectBase? FindEffectFromElement(FrameworkElement element)
        {
            // Walk up the tree to find the DataContext
            DependencyObject? current = element;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is LayerEffectBase effect)
                    return effect;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void EffectsList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count > 0 && e.Items[0] is LayerEffectBase effect)
            {
                _draggedEffect = effect;
                e.Data.RequestedOperation = DataPackageOperation.Move;
                e.Data.Properties["DraggedEffect"] = effect;
            }
        }

        private void DragScrollTimer_Tick(object? sender, object e)
        {
            if (_dragScrollDirection == 0)
                return;

            var scrollViewer = GetEffectsListScrollViewer();
            if (scrollViewer == null)
                return;

            double newOffset = scrollViewer.VerticalOffset + (_dragScrollDirection * DragScrollSpeed);
            newOffset = Math.Clamp(newOffset, 0, scrollViewer.ScrollableHeight);
            scrollViewer.ChangeView(null, newOffset, null, true);
        }

        private void EffectsList_DragOver(object sender, DragEventArgs e)
        {
            if (_draggedEffect == null)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                HideInsertionIndicator();
                StopDragScroll();
                return;
            }

            e.AcceptedOperation = DataPackageOperation.Move;

            var position = e.GetPosition(EffectsList);

            // Handle auto-scroll when near edges
            double listHeight = EffectsList.ActualHeight;
            if (position.Y < DragScrollEdgeThreshold)
            {
                // Near top - scroll up
                _dragScrollDirection = -1;
                StartDragScroll();
            }
            else if (position.Y > listHeight - DragScrollEdgeThreshold)
            {
                // Near bottom - scroll down
                _dragScrollDirection = 1;
                StartDragScroll();
            }
            else
            {
                // Not near edge - stop scrolling
                StopDragScroll();
            }

            var targetEffect = GetEffectAtPosition(position);

            if (targetEffect != null && !ReferenceEquals(targetEffect, _draggedEffect))
            {
                // Determine if we should insert before or after based on drag position
                var container = EffectsList.ContainerFromItem(targetEffect) as ListViewItem;
                if (container != null)
                {
                    var transform = container.TransformToVisual(EffectsList);
                    var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                    // If dragging over top half, insert before; bottom half, insert after
                    bool insertBefore = (position.Y - bounds.Top) < (bounds.Height / 2);
                    ShowInsertionIndicator(targetEffect, insertBefore);

                    e.DragUIOverride.Caption = "Reorder";
                    e.DragUIOverride.IsCaptionVisible = true;
                }
            }
            else if (targetEffect == null && ViewModel.Effects.Count > 0)
            {
                // Dragging below all items - show indicator after last item
                var lastEffect = ViewModel.Effects[^1];
                if (!ReferenceEquals(lastEffect, _draggedEffect))
                {
                    ShowInsertionIndicator(lastEffect, insertBefore: false);
                }
            }
            else
            {
                HideInsertionIndicator();
            }
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

        private void EffectsList_Drop(object sender, DragEventArgs e)
        {
            StopDragScroll();

            if (_draggedEffect == null)
            {
                HideInsertionIndicator();
                EnableBuiltInDragScroll();
                return;
            }

            var effects = ViewModel.Effects;
            int oldIndex = effects.IndexOf(_draggedEffect);

            // If we have a valid insertion target, perform the move
            if (_insertionTarget != null && oldIndex >= 0)
            {
                int targetIndex = effects.IndexOf(_insertionTarget);

                if (targetIndex >= 0 && oldIndex != targetIndex)
                {
                    // Calculate the new index
                    int newIndex;
                    if (_insertBefore)
                    {
                        // Insert before target
                        newIndex = targetIndex;
                        if (oldIndex < targetIndex)
                        {
                            newIndex--; // Account for removal shifting indices
                        }
                    }
                    else
                    {
                        // Insert after target
                        newIndex = targetIndex;
                        if (oldIndex > targetIndex)
                        {
                            newIndex++; // Insert after
                        }
                    }

                    // Ensure newIndex is valid
                    newIndex = Math.Clamp(newIndex, 0, effects.Count - 1);

                    if (oldIndex != newIndex)
                    {
                        effects.Move(oldIndex, newIndex);
                        NotifyStructureChanged();
                        RefreshEffectPanels();
                    }
                }
            }

            // Clean up
            HideInsertionIndicator();
            _draggedEffect = null;
            EnableBuiltInDragScroll();
            e.Handled = true;
        }

        private void EffectsList_DragLeave(object sender, DragEventArgs e)
        {
            HideInsertionIndicator();
            StopDragScroll();
            // Note: Don't re-enable scroll here as drag might continue outside and come back
        }

        private LayerEffectBase? GetEffectAtPosition(Windows.Foundation.Point position)
        {
            foreach (var effect in ViewModel.Effects)
            {
                var container = EffectsList.ContainerFromItem(effect) as ListViewItem;
                if (container == null) continue;

                try
                {
                    var transform = container.TransformToVisual(EffectsList);
                    var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                    if (bounds.Contains(position))
                    {
                        return effect;
                    }
                }
                catch
                {
                    // Transform may fail if container is not in visual tree
                    continue;
                }
            }
            return null;
        }

        private void ShowInsertionIndicator(LayerEffectBase targetEffect, bool insertBefore)
        {
            _insertionTarget = targetEffect;
            _insertBefore = insertBefore;

            var container = EffectsList.ContainerFromItem(targetEffect) as ListViewItem;
            if (container == null)
            {
                HideInsertionIndicator();
                return;
            }

            try
            {
                // Get the position of the container relative to the ListView
                var transform = container.TransformToVisual(EffectsList);
                var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

                // Position indicator at top or bottom of the item
                double yPos = insertBefore ? bounds.Top : bounds.Bottom;

                InsertionIndicator.Margin = new Thickness(0, yPos - 1.5, 0, 0);
                InsertionIndicator.Visibility = Visibility.Visible;
            }
            catch
            {
                HideInsertionIndicator();
            }
        }

        private void HideInsertionIndicator()
        {
            InsertionIndicator.Visibility = Visibility.Collapsed;
            _insertionTarget = null;
        }

        /// <summary>
        /// Disables the ListView's built-in auto-scroll during drag operations.
        /// We use our own controlled scroll instead.
        /// </summary>
        private void DisableBuiltInDragScroll()
        {
            var scrollViewer = GetEffectsListScrollViewer();
            if (scrollViewer != null)
            {
                scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            }
        }

        /// <summary>
        /// Re-enables the ListView's scroll behavior after drag completes.
        /// </summary>
        private void EnableBuiltInDragScroll()
        {
            var scrollViewer = GetEffectsListScrollViewer();
            if (scrollViewer != null)
            {
                scrollViewer.VerticalScrollMode = ScrollMode.Auto;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // LIVE PREVIEW HOOKS
        // ────────────────────────────────────────────────────────────────────

        // NumberBox overload
        private void Opacity_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
            => NotifyStructureChanged();

        // Slider overload
        private void Opacity_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
            => NotifyStructureChanged();

        private void BlendCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => NotifyStructureChanged();

        private void VisBtn_Click(object sender, RoutedEventArgs e)
            => NotifyStructureChanged();

        private void LockBtn_Click(object sender, RoutedEventArgs e)
            => NotifyStructureChanged();

        // ────────────────────────────────────────────────────────────────────
        // SOLO VISIBILITY TOGGLE
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Enable Solo: hide all other layers and snapshot visibilities for restore.
        /// </summary>
        private void SoloBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (_visSnapshot != null) return;

            _visSnapshot = new Dictionary<RasterLayer, bool>(_doc.Layers.Count);
            foreach (var l in _doc.Layers)
                _visSnapshot[l] = l.Visible;

            foreach (var l in _doc.Layers)
                l.Visible = ReferenceEquals(l, _layer);

            NotifyStructureChanged();
        }

        /// <summary>
        /// Disable Solo: restore prior visibilities if the layers still exist.
        /// </summary>
        private void SoloBtn_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_visSnapshot is null) return;

            foreach (var kv in _visSnapshot)
            {
                if (_doc.Layers.Contains(kv.Key))
                    kv.Key.Visible = kv.Value;
            }

            _visSnapshot = null;
            NotifyStructureChanged();
        }

        // ────────────────────────────────────────────────────────────────────
        // APPLY / CANCEL
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies the editable name and refreshes the "original" snapshot.
        /// Opacity and blend are already live-bound; this commits name changes.
        /// </summary>
        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(_layer.Name, EditableName, StringComparison.Ordinal))
            {
                _layer.Name = EditableName;
            }

            _origOpacity = _layer.Opacity;
            _origBlend = _layer.Blend;
            _origName = _layer.Name ?? string.Empty;

            Title = $"Layer Settings - {_origName}";

            NotifyStructureChanged();
        }

        /// <summary>
        /// Cancels non-committed changes by restoring original opacity and blend.
        /// The name textbox uses a local copy, so no name rollback is required here.
        /// </summary>
        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            _layer.Opacity = _origOpacity;
            _layer.Blend = _origBlend;

            NotifyStructureChanged();
            Close();
        }
    }
}
