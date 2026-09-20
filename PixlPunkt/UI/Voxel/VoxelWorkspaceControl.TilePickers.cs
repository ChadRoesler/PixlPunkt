using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Tile;
using PixlPunkt.Core.Voxel;
using PixlPunkt.Core.Voxel.Editing;
using PixlPunkt.Core.Voxel.Tools;
using PixlPunkt.PluginSdk.Voxel;
using PixlPunkt.UI.Controls;
using PixlPunkt.UI.Helpers;
using PixlPunkt.UI.Voxel.Tools;
using Windows.System;
using Windows.Storage.Pickers;
using Windows.UI.Core;

namespace PixlPunkt.UI.Voxel
{
    public sealed partial class VoxelWorkspaceControl
    {
        // ════════════════════════════════════════════════════════════════════
        // TILE PICKER POPULATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Populates all tile picker ComboBoxes with tiles from the document's TileSet.
        /// </summary>
        private void PopulateTilePickers(bool preserveSelection = true)
        {
            var tileSet = _document.TileSet;

            int front3Id = preserveSelection ? GetSelectedTileId(FrontTilePicker) : -1;
            int side3Id = preserveSelection ? GetSelectedTileId(SideTilePicker) : -1;
            int top3Id = preserveSelection ? GetSelectedTileId(TopTilePicker) : -1;

            int front6Id = preserveSelection ? GetSelectedTileId(FrontTilePicker6) : -1;
            int back6Id = preserveSelection ? GetSelectedTileId(BackTilePicker6) : -1;
            int left6Id = preserveSelection ? GetSelectedTileId(LeftTilePicker6) : -1;
            int right6Id = preserveSelection ? GetSelectedTileId(RightTilePicker6) : -1;
            int top6Id = preserveSelection ? GetSelectedTileId(TopTilePicker6) : -1;
            int bottom6Id = preserveSelection ? GetSelectedTileId(BottomTilePicker6) : -1;

            var tileItems = new List<TilePickerItem> { new(null, "(None)", null) };
            if (tileSet != null)
            {
                foreach (var tile in tileSet.Tiles)
                {
                    string label = $"#{tile.Id}";
                    var thumb = CreateTileThumbnail(tile);
                    tileItems.Add(new TilePickerItem(tile, label, thumb));
                }
            }

            // 3-face pickers
            bool previousSuppress = _suppressVoxelUiEvents;
            _suppressVoxelUiEvents = true;
            try
            {
                SetPickerItems(FrontTilePicker, tileItems);
                SetPickerItems(SideTilePicker, tileItems);
                SetPickerItems(TopTilePicker, tileItems);

                // 6-face pickers
                SetPickerItems(FrontTilePicker6, tileItems);
                SetPickerItems(BackTilePicker6, tileItems);
                SetPickerItems(LeftTilePicker6, tileItems);
                SetPickerItems(RightTilePicker6, tileItems);
                SetPickerItems(TopTilePicker6, tileItems);
                SetPickerItems(BottomTilePicker6, tileItems);

                if (preserveSelection)
                {
                    SetPickerSelectionByTileId(FrontTilePicker, front3Id);
                    SetPickerSelectionByTileId(SideTilePicker, side3Id);
                    SetPickerSelectionByTileId(TopTilePicker, top3Id);

                    SetPickerSelectionByTileId(FrontTilePicker6, front6Id);
                    SetPickerSelectionByTileId(BackTilePicker6, back6Id);
                    SetPickerSelectionByTileId(LeftTilePicker6, left6Id);
                    SetPickerSelectionByTileId(RightTilePicker6, right6Id);
                    SetPickerSelectionByTileId(TopTilePicker6, top6Id);
                    SetPickerSelectionByTileId(BottomTilePicker6, bottom6Id);
                }
            }
            finally
            {
                _suppressVoxelUiEvents = previousSuppress;
            }
        }

        private static void SetPickerItems(ComboBox picker, List<TilePickerItem> items)
        {
            picker.Items.Clear();
            foreach (var item in items)
                picker.Items.Add(item);
            picker.SelectedIndex = 0;
        }

        /// <summary>
        /// Gets the selected <see cref="TileDefinition"/> from a picker, or null if "(None)".
        /// </summary>
        private static TileDefinition? GetSelectedTile(ComboBox picker)
        {
            return (picker.SelectedItem as TilePickerItem)?.Tile;
        }

        private static int GetSelectedTileId(ComboBox picker)
        {
            return (picker.SelectedItem as TilePickerItem)?.Tile?.Id ?? -1;
        }

        private static void SetPickerSelectionByTileId(ComboBox picker, int tileId)
        {
            if (picker == null) return;
            if (tileId < 0)
            {
                picker.SelectedIndex = picker.Items.Count > 0 ? 0 : -1;
                return;
            }

            for (int i = 0; i < picker.Items.Count; i++)
            {
                if (picker.Items[i] is TilePickerItem item && item.Tile?.Id == tileId)
                {
                    picker.SelectedIndex = i;
                    return;
                }
            }

            picker.SelectedIndex = picker.Items.Count > 0 ? 0 : -1;
        }

        private void ApplyVoxelPreviewStateFromDocument()
        {
            var s = _document.VoxelPreviewState;
            var ws = _document.VoxelWorkspace;

            if ((s == null || !s.HasState) && (ws == null || !ws.HasState))
            {
                SyncLightingControlsFromDocument();
                return;
            }

            // Legacy preview state remains the source for the existing UI controls during the transition,
            // but we overlay any newer workspace-only fields afterward.
            if (s != null && s.HasState)
            {
                FaceModeCombo.SelectedIndex = s.FaceModeIndex is 1 ? 1 : 0;
                ColorLinkingCheckBox.IsChecked = s.ColorLinkingEnabled;
                ColorToleranceBox.Value = Math.Clamp(s.ColorTolerance, 0, 255);

                SetPickerSelectionByTileId(FrontTilePicker, s.FrontTileId3);
                SetPickerSelectionByTileId(SideTilePicker, s.SideTileId3);
                SetPickerSelectionByTileId(TopTilePicker, s.TopTileId3);

                SetPickerSelectionByTileId(FrontTilePicker6, s.FrontTileId6);
                SetPickerSelectionByTileId(BackTilePicker6, s.BackTileId6);
                SetPickerSelectionByTileId(LeftTilePicker6, s.LeftTileId6);
                SetPickerSelectionByTileId(RightTilePicker6, s.RightTileId6);
                SetPickerSelectionByTileId(TopTilePicker6, s.TopTileId6);
                SetPickerSelectionByTileId(BottomTilePicker6, s.BottomTileId6);

                OutlineCheckBox.IsChecked = s.OutlineEnabled;
                OutlineColorSwatch.Color = s.OutlineColor;
                OutlineSizeBox.Value = Math.Clamp(s.OutlineSize, 1, 16);

                PixelPreviewCheckBox.IsChecked = s.PixelPreviewEnabled;
                PixelPreviewAntialiasCheckBox.IsChecked = s.PixelPreviewAntialiasEnabled;
                PixelPreviewAaStrengthSlider.Value = Math.Clamp(s.PixelPreviewAntialiasStrength, 0f, 1f);
                PixelBaseSizeBox.Value = Math.Clamp(s.PixelBaseSize, 1, 256);
                BackdropGridCheckBox.IsChecked = s.BackdropGridEnabled;
                BackdropProjectionTilesCheckBox.IsChecked = true;
                BackdropCageScaleBox.Value = 1.6d;

                _camera.SetOrientation(s.CameraPitch, s.CameraYaw, allowSnap: true);
                _camera.SetZoomPercent(s.CameraZoomPercent);
            }

            if (ws != null && ws.HasState)
            {
                PixelPreviewAntialiasCheckBox.IsChecked = ws.PixelPreviewAntialiasEnabled;
                PixelPreviewAaStrengthSlider.Value = Math.Clamp(ws.PixelPreviewAntialiasStrength, 0f, 1f);
                SurfaceVoxelGridCheckBox.IsChecked = ws.SurfaceVoxelGridEnabled;
                BackdropGridCheckBox.IsChecked = ws.BackdropGridEnabled;
                BackdropProjectionTilesCheckBox.IsChecked = ws.BackdropProjectionTilesEnabled;
                BackdropCageScaleBox.Value = Math.Clamp(ws.BackdropCageScale, 1.05f, 4f);
                _camera.SetOrientation(ws.CameraPitch, ws.CameraYaw, allowSnap: true);
                _camera.SetZoomPercent(ws.CameraZoomPercent);
                _camera.SetPanOffset(new System.Numerics.Vector3(ws.CameraPanX, ws.CameraPanY, ws.CameraPanZ));
                ApplySidebarSectionExpandState(ws);
            }

            UpdatePixelPreviewAaStrengthLabel();
            RefreshBackdropProjectionImagesFromUi();
            SyncLightingControlsFromDocument();
        }

        private void PersistVoxelPreviewStateToDocument()
        {
            if (_suppressVoxelUiEvents) return;

            var s = _document.VoxelPreviewState;
            s.HasState = true;

            s.FaceModeIndex = FaceModeCombo?.SelectedIndex == 1 ? 1 : 0;
            s.ColorLinkingEnabled = ColorLinkingCheckBox?.IsChecked == true;
            s.ColorTolerance = Math.Clamp((int)Math.Round(ColorToleranceBox?.Value ?? 32d), 0, 255);

            s.FrontTileId3 = GetSelectedTileId(FrontTilePicker);
            s.SideTileId3 = GetSelectedTileId(SideTilePicker);
            s.TopTileId3 = GetSelectedTileId(TopTilePicker);

            s.FrontTileId6 = GetSelectedTileId(FrontTilePicker6);
            s.BackTileId6 = GetSelectedTileId(BackTilePicker6);
            s.LeftTileId6 = GetSelectedTileId(LeftTilePicker6);
            s.RightTileId6 = GetSelectedTileId(RightTilePicker6);
            s.TopTileId6 = GetSelectedTileId(TopTilePicker6);
            s.BottomTileId6 = GetSelectedTileId(BottomTilePicker6);

            s.OutlineEnabled = OutlineCheckBox?.IsChecked == true;
            s.OutlineColor = OutlineColorSwatch?.Color ?? 0xFF000000;
            s.OutlineSize = Math.Max(1, (int)Math.Round(OutlineSizeBox?.Value ?? 1d));
            s.PixelPreviewEnabled = PixelPreviewCheckBox?.IsChecked == true;
            s.PixelPreviewAntialiasEnabled = PixelPreviewAntialiasCheckBox?.IsChecked == true;
            s.PixelPreviewAntialiasStrength = Math.Clamp((float)(PixelPreviewAaStrengthSlider?.Value ?? 0.35d), 0f, 1f);
            s.PixelBaseSize = Math.Max(1, (int)Math.Round(PixelBaseSizeBox?.Value ?? 16d));
            s.BackdropGridEnabled = BackdropGridCheckBox?.IsChecked != false;

            s.CameraPitch = _camera.Pitch;
            s.CameraYaw = _camera.Yaw;
            s.CameraZoomPercent = _camera.ZoomPercent;

            var ws = _document.VoxelWorkspace;
            ws.CopyFromPreviewState(s);
            ws.HasState = true;
            ws.BackdropProjectionTilesEnabled = BackdropProjectionTilesCheckBox?.IsChecked == true;
            ws.BackdropCageScale = Math.Clamp((float)(BackdropCageScaleBox?.Value ?? 1.6d), 1.05f, 4f);
            ws.SurfaceVoxelGridEnabled = SurfaceVoxelGridCheckBox?.IsChecked == true;
            ws.CameraPanX = _camera.PanOffset.X; ws.CameraPanY = _camera.PanOffset.Y; ws.CameraPanZ = _camera.PanOffset.Z;
            ws.ToolOptionsSectionExpanded = !(LightingSectionCard?.IsMinimized ?? false);
            ws.FaceMappingSectionExpanded = !(FaceMappingSectionCard?.IsMinimized ?? false);
            ws.DisplaySectionExpanded = !(DisplaySectionCard?.IsMinimized ?? false);
            ws.VoxelEditSectionExpanded = !(VoxelEditSectionCard?.IsMinimized ?? false);
            ws.ActionsSectionExpanded = !(ActionsSectionCard?.IsMinimized ?? false);
        }

        private void ApplySidebarSectionExpandState(VoxelWorkspaceDocumentState state)
        {
            if (state == null)
                return;

            if (LightingSectionCard != null)
                LightingSectionCard.IsMinimized = !state.ToolOptionsSectionExpanded;
            if (FaceMappingSectionCard != null)
                FaceMappingSectionCard.IsMinimized = !state.FaceMappingSectionExpanded;
            if (DisplaySectionCard != null)
                DisplaySectionCard.IsMinimized = !state.DisplaySectionExpanded;
            if (VoxelEditSectionCard != null)
                VoxelEditSectionCard.IsMinimized = !state.VoxelEditSectionExpanded;
            if (ActionsSectionCard != null)
                ActionsSectionCard.IsMinimized = !state.ActionsSectionExpanded;
        }

        private void SidebarSectionCard_MinimizedChanged(SectionCard card, bool isMinimized)
        {
            if (_suppressVoxelUiEvents)
                return;

            PersistVoxelPreviewStateToDocument();
        }

        // ════════════════════════════════════════════════════════════════════
        // TILE PICKER ITEM
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wrapper for ComboBox items that displays a tile thumbnail
        /// and holds a reference to the <see cref="TileDefinition"/>.
        /// </summary>
        private sealed class TilePickerItem
        {
            public TileDefinition? Tile { get; }
            public string Label { get; }
            public WriteableBitmap? Thumbnail { get; }

            public TilePickerItem(TileDefinition? tile, string label, WriteableBitmap? thumbnail)
            {
                Tile = tile;
                Label = label;
                Thumbnail = thumbnail;
            }

            public override string ToString() => Label;
        }

        /// <summary>
        /// Creates a <see cref="WriteableBitmap"/> from a tile's BGRA pixel data.
        /// </summary>
        private static WriteableBitmap CreateTileThumbnail(TileDefinition tile)
        {
            var bmp = new WriteableBitmap(tile.Width, tile.Height);
            using var stream = bmp.PixelBuffer.AsStream();
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(tile.Pixels, 0, tile.Pixels.Length);
            return bmp;
        }
    }
}
