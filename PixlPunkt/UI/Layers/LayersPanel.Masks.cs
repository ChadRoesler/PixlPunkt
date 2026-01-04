using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Compositing.Effects;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.UI.Helpers;
using System.Collections.Generic;

namespace PixlPunkt.UI.Layers
{
    /// <summary>
    /// LayersPanel partial: Layer mask operations and effects clipboard.
    /// </summary>
    public sealed partial class LayersPanel
    {
        // --------------------------------------------------------------------
        // COPY/PASTE EFFECTS
        // --------------------------------------------------------------------

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

        // --------------------------------------------------------------------
        // LAYER MASK OPERATIONS
        // --------------------------------------------------------------------

        private void AddMask_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            var item = ItemFromSender(sender);
            if (item is not RasterLayer raster) return;

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

        // --------------------------------------------------------------------
        // MASK EDITING MODE
        // --------------------------------------------------------------------

        private void LayerPreview_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_doc == null) return;
            if ((sender as FrameworkElement)?.DataContext is not RasterLayer layer) return;

            layer.IsEditingMask = false;

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

            layer.IsEditingMask = true;

            var rasters = _doc.GetAllRasterLayers();
            int rasterIndex = rasters.IndexOf(layer);
            if (rasterIndex >= 0)
                _doc.SetActiveLayer(rasterIndex);

            _doc.RaiseStructureChanged();
            e.Handled = true;
        }

        // --------------------------------------------------------------------
        // MASK TOGGLE CONTROLS
        // --------------------------------------------------------------------

        private void MaskEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            _doc.RaiseStructureChanged();
        }

        private void MaskInvert_Click(object sender, RoutedEventArgs e)
        {
            if (_doc == null) return;
            _doc.RaiseStructureChanged();
        }

        // --------------------------------------------------------------------
        // REFERENCE LAYER OPERATIONS
        // --------------------------------------------------------------------

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
