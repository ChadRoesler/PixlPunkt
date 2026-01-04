using Microsoft.UI.Xaml;
using PixlPunkt.Core.Compositing.Effects;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Logging;
using PixlPunkt.UI.Helpers;
using System;
using System.Collections.Generic;

namespace PixlPunkt.UI.Layers
{
    /// <summary>
    /// LayersPanel partial: Flyout event wiring and handlers for raster layers, folders, and reference layers.
    /// </summary>
    public sealed partial class LayersPanel
    {
        // --------------------------------------------------------------------
        // FLYOUT EVENT WIRING
        // --------------------------------------------------------------------

        public void WireFlyoutEvents()
        {
            // Panel menu flyout (empty area right-click)
            PanelMenuFlyout.AddLayerRequested += (s, e) => Add_Click(s, new RoutedEventArgs());
            PanelMenuFlyout.AddFolderRequested += (s, e) => AddFolder_Click(s, new RoutedEventArgs());
            PanelMenuFlyout.AddReferenceLayerRequested += (s, e) => AddReferenceLayer_Click(s, new RoutedEventArgs());
            PanelMenuFlyout.RemoveSelectedRequested += (s, e) => Remove_Click(s, new RoutedEventArgs());

            // Raster layer flyout
            LayerMenuFlyout.SettingsRequested += OnRasterSettings;
            LayerMenuFlyout.CopyEffectsRequested += OnCopyEffects;
            LayerMenuFlyout.PasteEffectsRequested += OnPasteEffects;
            LayerMenuFlyout.AddMaskRequested += OnAddMask;
            LayerMenuFlyout.DeleteMaskRequested += OnDeleteMask;
            LayerMenuFlyout.ApplyMaskRequested += OnApplyMask;
            LayerMenuFlyout.VisibleToggled += OnLayerVisibleToggled;
            LayerMenuFlyout.LockedToggled += OnLayerLockedToggled;
            LayerMenuFlyout.DuplicateRequested += OnDuplicateLayer;
            LayerMenuFlyout.MergeDownRequested += OnMergeDown;
            LayerMenuFlyout.RemoveRequested += OnRemoveRasterLayer;

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

        // --------------------------------------------------------------------
        // RASTER LAYER FLYOUT HANDLERS
        // --------------------------------------------------------------------

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

        // --------------------------------------------------------------------
        // FOLDER FLYOUT HANDLERS
        // --------------------------------------------------------------------

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

        // --------------------------------------------------------------------
        // REFERENCE LAYER FLYOUT HANDLERS
        // --------------------------------------------------------------------

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
    }
}
