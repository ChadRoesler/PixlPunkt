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
        // BUILD VOXEL
        // ════════════════════════════════════════════════════════════════════

        private void TilePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();

            // Auto-rebuild on tile change if we already have a volume
            if (_lastVolume != null)
                BuildAndRender();
        }

        private void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            BuildAndRender();
        }

        private void ReloadTilesButton_Click(object sender, RoutedEventArgs e)
        {
            ReloadTilesFromDocument(rebuildModel: false);
        }

        private void ReloadTilesAndBuildButton_Click(object sender, RoutedEventArgs e)
        {
            ReloadTilesFromDocument(rebuildModel: true);
        }

        private void ReloadTilesFromDocument(bool rebuildModel)
        {
            try
            {
                PopulateTilePickers(preserveSelection: true);
                _lastKnownTileSetCount = _document.TileSet?.Count ?? 0;
                PersistVoxelPreviewStateToDocument();
                RefreshBackdropProjectionImagesFromUi();
                _cachedCardinalPixelPreviewImages = null;
                _pixelPreviewSpriteCache = null;

                if (rebuildModel)
                {
                    BuildAndRender();
                }
                else
                {
                    RenderViewport();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Voxel tile reload failed: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Builds the voxel volume from selected tiles and renders the viewport.
        /// </summary>
        private void BuildAndRender()
        {
            try
            {
                bool isSixFace = FaceModeCombo.SelectedIndex == 1;

                ImageData? frontImg = null, sideImg = null, topImg = null;
                ImageData? backOverrideImg = null, rightOverrideImg = null, bottomOverrideImg = null;

                bool usingMappedLayers = TryGetMappedViewImages(
                    isSixFace,
                    out frontImg, out sideImg, out topImg,
                    out backOverrideImg, out rightOverrideImg, out bottomOverrideImg);

                if (!usingMappedLayers)
                {
                    if (isSixFace)
                    {
                        // 6-face mode: build from front/side/top, assign individual faces
                        frontImg = TileToImage(GetSelectedTile(FrontTilePicker6));
                        sideImg = TileToImage(GetSelectedTile(LeftTilePicker6));
                        topImg = TileToImage(GetSelectedTile(TopTilePicker6));

                        backOverrideImg = TileToImage(GetSelectedTile(BackTilePicker6));
                        rightOverrideImg = TileToImage(GetSelectedTile(RightTilePicker6));
                        bottomOverrideImg = TileToImage(GetSelectedTile(BottomTilePicker6));
                    }
                    else
                    {
                        // 3-face mode: front/side/top (mirrored to back/right/bottom)
                        frontImg = TileToImage(GetSelectedTile(FrontTilePicker));
                        sideImg = TileToImage(GetSelectedTile(SideTilePicker));
                        topImg = TileToImage(GetSelectedTile(TopTilePicker));
                    }
                }

                if (frontImg == null && sideImg == null && topImg == null)
                {
                    _lastVolume = null;
                    _backdropFrontProjectionImage = null;
                    _backdropBackProjectionImage = null;
                    _backdropLeftProjectionImage = null;
                    _backdropRightProjectionImage = null;
                    _backdropTopProjectionImage = null;
                    _backdropBottomProjectionImage = null;
                    RefreshOccupiedBoundsCache();
                    _document.VoxelModel.Clear();
                    _editEngine.Selection.Clear();
                    _cachedCardinalPixelPreviewImages = null;
                    _pixelPreviewSpriteCache = null;
                    SetExportButtonsEnabled(false);
                    UpdateVoxelSelectionStatusText();
                    RenderViewport();
                    return;
                }

                var fallback = Rgba32.Opaque(128, 128, 128);
                var volume = OrthoVoxelBuilder.BuildFromOrtho(
                    frontImg, sideImg, topImg, fallback,
                    colorTolerance: GetColorTolerance());

                // For 6-face mode, override face colors for back/right/bottom
                // if distinct tiles were selected
                if (isSixFace)
                {
                    ApplySixFaceOverrides(volume, backOverrideImg, rightOverrideImg, bottomOverrideImg);
                }

                ApplyManualFaceOverrides(volume);

                _lastVolume = volume;
                RefreshOccupiedBoundsCache();
                _document.VoxelModel.SetFromVoxelVolume(volume);
                _document.VoxelModel.SourceKind = _document.VoxelPreviewState.FaceColorOverrides.Count > 0
                    ? VoxelModelSourceKind.Hybrid
                    : VoxelModelSourceKind.TileOrthoGenerated;
                _document.VoxelModel.DirtyFromSource = _document.VoxelPreviewState.FaceColorOverrides.Count > 0;
                _cachedCardinalPixelPreviewImages = null;
                _pixelPreviewSpriteCache = null;

                _backdropFrontProjectionImage = frontImg;
                _backdropBackProjectionImage = isSixFace ? (backOverrideImg ?? frontImg) : frontImg;
                _backdropLeftProjectionImage = sideImg;
                _backdropRightProjectionImage = isSixFace ? (rightOverrideImg ?? sideImg) : sideImg;
                _backdropTopProjectionImage = topImg;
                _backdropBottomProjectionImage = isSixFace ? (bottomOverrideImg ?? topImg) : topImg;

                _camera.ConfigureForVolume(volume.Size);
                _editEngine.Selection.Clear();

                SetExportButtonsEnabled(volume.OccupiedCount > 0);

                LoggingService.Info("Voxel built: {Occupied} occupied",
                    volume.OccupiedCount);

                UpdateVoxelSelectionStatusText();
                RenderViewport();
            }
            catch (Exception ex)
            {
                LoggingService.Error("Voxel build failed", ex);
            }
        }

        /// <summary>
        /// Converts a tile to an <see cref="ImageData"/>, or returns null.
        /// </summary>
        private static ImageData? TileToImage(TileDefinition? tile)
        {
            return tile != null ? ImageData.FromTile(tile) : null;
        }

        private void RefreshBackdropProjectionImagesFromUi()
        {
            bool isSixFace = FaceModeCombo?.SelectedIndex == 1;

            ImageData? frontImg = null, sideImg = null, topImg = null;
            ImageData? backOverrideImg = null, rightOverrideImg = null, bottomOverrideImg = null;

            bool usingMappedLayers = TryGetMappedViewImages(
                isSixFace,
                out frontImg, out sideImg, out topImg,
                out backOverrideImg, out rightOverrideImg, out bottomOverrideImg);

            if (!usingMappedLayers)
            {
                if (isSixFace)
                {
                    frontImg = TileToImage(GetSelectedTile(FrontTilePicker6));
                    sideImg = TileToImage(GetSelectedTile(LeftTilePicker6));
                    topImg = TileToImage(GetSelectedTile(TopTilePicker6));
                    backOverrideImg = TileToImage(GetSelectedTile(BackTilePicker6));
                    rightOverrideImg = TileToImage(GetSelectedTile(RightTilePicker6));
                    bottomOverrideImg = TileToImage(GetSelectedTile(BottomTilePicker6));
                }
                else
                {
                    frontImg = TileToImage(GetSelectedTile(FrontTilePicker));
                    sideImg = TileToImage(GetSelectedTile(SideTilePicker));
                    topImg = TileToImage(GetSelectedTile(TopTilePicker));
                }
            }

            _backdropFrontProjectionImage = frontImg;
            _backdropBackProjectionImage = isSixFace ? (backOverrideImg ?? frontImg) : frontImg;
            _backdropLeftProjectionImage = sideImg;
            _backdropRightProjectionImage = isSixFace ? (rightOverrideImg ?? sideImg) : sideImg;
            _backdropTopProjectionImage = topImg;
            _backdropBottomProjectionImage = isSixFace ? (bottomOverrideImg ?? topImg) : topImg;
        }

        /// <summary>
        /// In 6-face mode, overrides face colors for back/right/bottom faces
        /// using individually selected tiles.
        /// </summary>
        private static void ApplySixFaceOverrides(
            VoxelVolume volume,
            TileDefinition? backTile,
            TileDefinition? rightTile,
            TileDefinition? bottomTile)
        {
            ApplySixFaceOverrides(
                volume,
                backTile != null ? ImageData.FromTile(backTile) : null,
                rightTile != null ? ImageData.FromTile(rightTile) : null,
                bottomTile != null ? ImageData.FromTile(bottomTile) : null);
        }

        /// <summary>
        /// In 6-face mode, overrides face colors for back/right/bottom faces
        /// using per-view images.
        /// </summary>
        private static void ApplySixFaceOverrides(
            VoxelVolume volume,
            ImageData? backImg,
            ImageData? rightImg,
            ImageData? bottomImg)
        {
            int size = volume.Size;

            if (backImg == null && rightImg == null && bottomImg == null) return;

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (!volume.IsOccupied(x, y, z)) continue;

                        int idx = volume.Index(x, y, z);
                        int xFlip = size - 1 - x;
                        int yFlip = size - 1 - y;
                        int zFlip = size - 1 - z;

                        // Back view (camera at +Z): screen X follows +X, screen Y follows -Y.
                        // So the back source image should map as [x, yFlip] to appear unmirrored.
                        if (backImg != null && x < backImg.Width && yFlip < backImg.Height)
                        {
                            volume.FaceColors[volume.FaceIndex(idx, Face.Back)] =
                                backImg.GetPixel(x, yFlip);
                        }

                        if (rightImg != null && zFlip < rightImg.Width && yFlip < rightImg.Height)
                        {
                            volume.FaceColors[volume.FaceIndex(idx, Face.Right)] =
                                rightImg.GetPixel(zFlip, yFlip);
                        }

                        // Bottom view (camera at -Y with canonical bottom up-vector):
                        // screen X follows -X and screen Y follows +Z.
                        if (bottomImg != null && xFlip < bottomImg.Width && z < bottomImg.Height)
                        {
                            volume.FaceColors[volume.FaceIndex(idx, Face.Bottom)] =
                                bottomImg.GetPixel(xFlip, z);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Applies persisted sparse manual face-color overrides onto the generated volume.
        /// This is the phase-1 foundation for a voxel face painter (manual overrides win last).
        /// </summary>
        private void ApplyManualFaceOverrides(VoxelVolume volume)
        {
            if (volume == null) return;

            var overrides = _document.VoxelPreviewState?.FaceColorOverrides;
            if (overrides == null || overrides.Count == 0) return;

            int size = volume.Size;
            for (int i = 0; i < overrides.Count; i++)
            {
                var o = overrides[i];
                if ((uint)o.X >= (uint)size || (uint)o.Y >= (uint)size || (uint)o.Z >= (uint)size)
                    continue;
                if (!volume.IsOccupied(o.X, o.Y, o.Z))
                    continue;

                volume.SetFaceColor(o.X, o.Y, o.Z, o.Face, RgbaFromPackedBgra(o.ColorBgra));
            }
        }

        /// <summary>
        /// Sets or updates one manual face-color override in document state and applies it
        /// to the current preview volume if available. This is intended for a future face-paint tool.
        /// </summary>
        private void SetManualFaceColorOverride(int x, int y, int z, Face face, uint colorBgra)
        {
            var state = _document.VoxelPreviewState;
            state.SetFaceColorOverride(x, y, z, face, colorBgra);

            if (_lastVolume != null &&
                (uint)x < (uint)_lastVolume.Size &&
                (uint)y < (uint)_lastVolume.Size &&
                (uint)z < (uint)_lastVolume.Size &&
                _lastVolume.IsOccupied(x, y, z))
            {
                _lastVolume.SetFaceColor(x, y, z, face, RgbaFromPackedBgra(colorBgra));
                _cachedCardinalPixelPreviewImages = null;
                _pixelPreviewSpriteCache = null;
                RenderViewport();
            }

            if (_document.VoxelModel.HasModel &&
                _document.VoxelModel.IsInBounds(x, y, z) &&
                _document.VoxelModel.IsOccupied(x, y, z))
            {
                _document.VoxelModel.SetFaceColorBgra(x, y, z, face, colorBgra);
                _document.VoxelModel.DirtyFromSource = true;
                _document.VoxelModel.SourceKind = VoxelModelSourceKind.Hybrid;
            }

            PersistVoxelPreviewStateToDocument();
        }

        /// <summary>
        /// Removes one manual face-color override and rebuilds the voxel preview volume so the
        /// face color falls back to generated/mapped data.
        /// </summary>
        private void ClearManualFaceColorOverride(int x, int y, int z, Face face)
        {
            var state = _document.VoxelPreviewState;
            if (!state.RemoveFaceColorOverride(x, y, z, face))
                return;

            PersistVoxelPreviewStateToDocument();

            if (_lastVolume != null)
            {
                BuildAndRender();
            }
            else
            {
                RenderViewport();
            }
        }

        /// <summary>
        /// Clears all manual face overrides from the preview/document and rebuilds if needed.
        /// </summary>
        private void ClearAllManualFaceColorOverrides()
        {
            var state = _document.VoxelPreviewState;
            if (state.FaceColorOverrides.Count == 0)
                return;

            state.ClearFaceColorOverrides();
            PersistVoxelPreviewStateToDocument();

            if (_lastVolume != null)
                BuildAndRender();
            else
                RenderViewport();
        }

        private VoxelWorkspaceHistorySnapshot CaptureWorkspaceHistorySnapshot()
        {
            var overrides = _document.VoxelPreviewState.FaceColorOverrides;
            var clonedOverrides = new VoxelFaceColorOverride[overrides.Count];
            for (int i = 0; i < overrides.Count; i++)
            {
                var o = overrides[i];
                clonedOverrides[i] = new VoxelFaceColorOverride(o.X, o.Y, o.Z, o.Face, o.ColorBgra);
            }

            return new VoxelWorkspaceHistorySnapshot
            {
                Model = _document.VoxelModel.Clone(),
                Selection = _editEngine.Selection.ToArray(),
                FaceOverrides = clonedOverrides,
            };
        }

        private void RestoreWorkspaceHistorySnapshot(VoxelWorkspaceHistorySnapshot snapshot)
        {
            _suppressEditEngineModelSync = true;
            try
            {
                _document.VoxelModel.CopyFrom(snapshot.Model);
                _editEngine.Selection.ReplaceAll(snapshot.Selection);

                var state = _document.VoxelPreviewState;
                state.ClearFaceColorOverrides();
                state.HasState = true;
                for (int i = 0; i < snapshot.FaceOverrides.Length; i++)
                {
                    var o = snapshot.FaceOverrides[i];
                    state.FaceColorOverrides.Add(new VoxelFaceColorOverride(o.X, o.Y, o.Z, o.Face, o.ColorBgra));
                }
            }
            finally
            {
                _suppressEditEngineModelSync = false;
            }

            _cachedCardinalPixelPreviewImages = null;
            _pixelPreviewSpriteCache = null;
            SyncPreviewVolumeFromCanonicalModel();
            SetExportButtonsEnabled(_lastVolume != null && _lastVolume.OccupiedCount > 0);
            UpdateVoxelSelectionStatusText();
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private static Rgba32 RgbaFromPackedBgra(uint bgra)
        {
            byte b = (byte)(bgra & 0xFF);
            byte g = (byte)((bgra >> 8) & 0xFF);
            byte r = (byte)((bgra >> 16) & 0xFF);
            byte a = (byte)((bgra >> 24) & 0xFF);
            return new Rgba32(r, g, b, a);
        }

        private static uint PackedBgraFromRgba(Rgba32 rgba)
        {
            return ((uint)rgba.A << 24) |
                   ((uint)rgba.R << 16) |
                   ((uint)rgba.G << 8) |
                   rgba.B;
        }

        private static string FormatBgraHex(uint bgra) => $"#{bgra:X8}";

        /// <summary>
        /// Attempts to build orthographic voxel input images from tile-mapped document layers.
        /// Layer names are matched by tokens (e.g. "front", "side"/"left", "top").
        /// 6-face mode optionally uses "back", "right", and "bottom" for face overrides.
        /// </summary>
        private bool TryGetMappedViewImages(
            bool isSixFace,
            out ImageData? frontImg,
            out ImageData? sideImg,
            out ImageData? topImg,
            out ImageData? backImg,
            out ImageData? rightImg,
            out ImageData? bottomImg)
        {
            frontImg = sideImg = topImg = null;
            backImg = rightImg = bottomImg = null;

            var tileSet = _document.TileSet;
            if (tileSet == null || tileSet.Count == 0)
                return false;

            var frontLayer = FindNamedMappedLayer("front");
            var sideLayer = FindNamedMappedLayer("side", "left");
            var topLayer = FindNamedMappedLayer("top");

            frontImg = BuildMappedLayerImage(frontLayer, tileSet);
            sideImg = BuildMappedLayerImage(sideLayer, tileSet);
            topImg = BuildMappedLayerImage(topLayer, tileSet);

            if (isSixFace)
            {
                backImg = BuildMappedLayerImage(FindNamedMappedLayer("back"), tileSet);
                rightImg = BuildMappedLayerImage(FindNamedMappedLayer("right"), tileSet);
                bottomImg = BuildMappedLayerImage(FindNamedMappedLayer("bottom"), tileSet);
            }

            bool hasPrimary = frontImg != null || sideImg != null || topImg != null;
            if (!hasPrimary)
                return false;

            LoggingService.Info(
                "Using mapped voxel views front={Front} side={Side} top={Top}",
                frontLayer?.Name ?? "(none)",
                sideLayer?.Name ?? "(none)",
                topLayer?.Name ?? "(none)");

            return true;
        }

        private static ImageData? BuildMappedLayerImage(RasterLayer? layer, TileSet tileSet)
        {
            if (layer?.TileMapping == null || !layer.HasTileMappings())
                return null;

            return TileMappingOrthoImageBuilder.BuildTileCellImage(layer.TileMapping, tileSet);
        }

        private RasterLayer? FindNamedMappedLayer(params string[] expectedTokens)
        {
            var rasters = _document.GetAllRasterLayers();
            for (int i = rasters.Count - 1; i >= 0; i--)
            {
                var layer = rasters[i];
                if (!layer.IsEffectivelyVisible() || !layer.HasTileMappings() || layer.TileMapping == null)
                    continue;

                if (LayerNameHasAnyToken(layer.Name, expectedTokens))
                    return layer;
            }

            return null;
        }

        private static bool LayerNameHasAnyToken(string? name, params string[] expectedTokens)
        {
            if (string.IsNullOrWhiteSpace(name) || expectedTokens == null || expectedTokens.Length == 0)
                return false;

            var tokens = TokenizeName(name);
            foreach (var expected in expectedTokens)
            {
                if (tokens.Contains(expected))
                    return true;
            }
            return false;
        }

        private static HashSet<string> TokenizeName(string name)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder(name.Length);

            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
                tokens.Add(sb.ToString());

            return tokens;
        }
    }
}
