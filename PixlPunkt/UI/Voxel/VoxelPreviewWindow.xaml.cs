using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Tile;
using PixlPunkt.Core.Voxel;
using PixlPunkt.UI.Helpers;
using Windows.Storage.Pickers;

namespace PixlPunkt.UI.Voxel
{
    /// <summary>
    /// Window that renders a 3D voxel preview from tile face selections.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides an interactive 3D viewport using <see cref="SoftwareRasterizer"/>
    /// and <see cref="OrbitCamera"/> for free orbit rotation. Tiles from the current
    /// document's <see cref="TileSet"/> are mapped onto voxel faces via
    /// <see cref="OrthoVoxelBuilder"/>.
    /// </para>
    /// <para>
    /// Supports two modes:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>3-face (mirrored):</strong> Front/Back, Left/Right, Top/Bottom
    /// share tiles (back/right/bottom are mirrored copies).</item>
    /// <item><strong>6-face (individual):</strong> Each face gets its own tile.</item>
    /// </list>
    /// </remarks>
    public sealed partial class VoxelPreviewWindow : Window
    {
        private enum FacePainterMode
        {
            Paint = 0,
            Sample = 1,
            EraseOverride = 2,
        }

        private readonly record struct PickedVoxelFace(int X, int Y, int Z, Face Face);

        private enum PointerDragMode
        {
            None,
            Orbit,
            FacePaintStroke,
        }

        private sealed class PixelPreviewSpriteCache
        {
            public required VoxelVolume Volume;
            public required byte[] Buffer;
            public int Width;
            public int Height;
            public float Pitch;
            public float Yaw;
            public string? SnapName;
            public bool DrawOutline;
            public uint OutlineColor;
            public int OutlineSize;
        }

        private static VoxelPreviewWindow? _instance;

        private readonly CanvasDocument _document;
        private readonly PaletteService? _palette;
        private OrbitCamera _camera;

        // Drag tracking (pixel deltas for orbit)
        private Windows.Foundation.Point _lastPointerPos;

        // Render state
        private VoxelVolume? _lastVolume;
        private byte[]? _renderBuffer;
        private WriteableBitmap? _viewportBitmap;
        private int _viewportWidth = 512;
        private int _viewportHeight = 512;
        private float _viewportRasterScale = 1f;
        private Dictionary<string, (ImageData Image, Face Face)>? _cachedCardinalPixelPreviewImages;
        private PixelPreviewSpriteCache? _pixelPreviewSpriteCache;
        private bool _suppressVoxelUiEvents = true;
        private PointerDragMode _pointerDragMode = PointerDragMode.None;
        private PickedVoxelFace? _lastStrokePaintFace;

        // Background color (dark gray, BGRA packed)
        private const uint ClearColor = 0xFF1E1E1E;

        /// <summary>
        /// Opens the voxel preview window for the specified document.
        /// Reuses the existing window if already open.
        /// </summary>
        public static void Show(CanvasDocument document, Window owner)
        {
            try
            {
                if (_instance != null)
                {
                    _instance.Activate();
                    return;
                }

                var palette = (owner as PixlPunkt.UI.PixlPunktMainWindow)?.Palette;
                _instance = new VoxelPreviewWindow(document, palette);

                WindowHost.ApplyChrome(
                    _instance,
                    resizable: true,
                    minimizable: true,
                    maximizable: true,
                    title: "Voxel Preview",
                    owner: owner);

                try
                {
                    var appWindow = _instance.AppWindow;
                    appWindow?.Resize(new Windows.Graphics.SizeInt32 { Width = 1050, Height = 700 });
                }
                catch (Exception ex)
                {
                    LoggingService.Warning("Could not set voxel window size: {Error}", ex.Message);
                }

                WindowHost.Place(_instance, WindowPlacement.CenterOnScreen);
                _instance.Activate();
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to open voxel preview window", ex);
            }
        }

        private VoxelPreviewWindow(CanvasDocument document, PaletteService? palette = null)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _palette = palette;

            // XAML checkbox/NumberBox events can fire during InitializeComponent().
            // Create the camera first so early RenderViewport() calls are safe.
            int tileSize = Math.Max(document.TileSet?.TileWidth ?? 16, document.TileSet?.TileHeight ?? 16);
            _camera = new OrbitCamera(tileSize);

            InitializeComponent();
            Closed += OnClosed;

            PopulateTilePickers();
            ApplyVoxelPreviewStateFromDocument();

            if (Root != null)
            {
                Root.Loaded += (_, __) =>
                {
                    if (UpdateViewportSizeFromControl())
                        _renderBuffer = null;
                    RenderViewport();
                };
            }

            if (ViewportHost != null)
            {
                ViewportHost.SizeChanged += (_, __) =>
                {
                    if (UpdateViewportSizeFromControl())
                        RenderViewport();
                };
            }

            // Default outline color: black, fully opaque
            if (!_document.VoxelPreviewState.HasState)
                OutlineColorSwatch.Color = 0xFF000000;

            _suppressVoxelUiEvents = false;
            UpdateFacePainterStatusText();

            if (_document.VoxelPreviewState.HasState)
                BuildAndRender();
            else
                RenderViewport();
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            _instance = null;
        }

        // ════════════════════════════════════════════════════════════════════
        // TILE PICKER POPULATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Populates all tile picker ComboBoxes with tiles from the document's TileSet.
        /// </summary>
        private void PopulateTilePickers()
        {
            var tileSet = _document.TileSet;
            if (tileSet == null || tileSet.Count == 0) return;

            var tileItems = new List<TilePickerItem> { new(null, "(None)", null) };
            foreach (var tile in tileSet.Tiles)
            {
                string label = $"#{tile.Id}";
                var thumb = CreateTileThumbnail(tile);
                tileItems.Add(new TilePickerItem(tile, label, thumb));
            }

            // 3-face pickers
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
            if (s == null || !s.HasState) return;

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
            PixelBaseSizeBox.Value = Math.Clamp(s.PixelBaseSize, 1, 256);
            BackdropGridCheckBox.IsChecked = s.BackdropGridEnabled;

            _camera.SetOrientation(s.CameraPitch, s.CameraYaw, allowSnap: true);
            _camera.SetZoomPercent(s.CameraZoomPercent);
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
            s.PixelBaseSize = Math.Max(1, (int)Math.Round(PixelBaseSizeBox?.Value ?? 16d));
            s.BackdropGridEnabled = BackdropGridCheckBox?.IsChecked != false;

            s.CameraPitch = _camera.Pitch;
            s.CameraYaw = _camera.Yaw;
            s.CameraZoomPercent = _camera.ZoomPercent;
        }

        // ════════════════════════════════════════════════════════════════════
        // FACE MODE TOGGLE
        // ════════════════════════════════════════════════════════════════════

        private void FaceModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThreeFacePanel == null || SixFacePanel == null) return;

            bool isSixFace = FaceModeCombo.SelectedIndex == 1;
            ThreeFacePanel.Visibility = isSixFace ? Visibility.Collapsed : Visibility.Visible;
            SixFacePanel.Visibility = isSixFace ? Visibility.Visible : Visibility.Collapsed;

            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
        }

        // ════════════════════════════════════════════════════════════════════
        // COLOR LINKING
        // ════════════════════════════════════════════════════════════════════

        private void ColorLinking_Changed(object sender, RoutedEventArgs e)
        {
            if (ColorTolerancePanel != null)
                ColorTolerancePanel.Visibility = ColorLinkingCheckBox.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();

            if (_lastVolume != null)
                BuildAndRender();
        }

        private void ColorTolerance_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();

            if (_lastVolume != null)
                BuildAndRender();
        }

        /// <summary>
        /// Gets the current color tolerance value, or -1 if color linking is disabled.
        /// </summary>
        private int GetColorTolerance()
        {
            if (ColorLinkingCheckBox?.IsChecked != true) return -1;
            return (int)(ColorToleranceBox?.Value ?? 32);
        }

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
                    _cachedCardinalPixelPreviewImages = null;
                    _pixelPreviewSpriteCache = null;
                    ExportButton.IsEnabled = false;
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
                _cachedCardinalPixelPreviewImages = null;
                _pixelPreviewSpriteCache = null;
                _camera.ConfigureForVolume(volume.Size);

                ExportButton.IsEnabled = volume.OccupiedCount > 0;

                LoggingService.Info("Voxel built: {Occupied} occupied",
                    volume.OccupiedCount);

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

        // ════════════════════════════════════════════════════════════════════
        // OUTLINE
        // ════════════════════════════════════════════════════════════════════

        private void OutlineCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (OutlineColorPanel != null)
                OutlineColorPanel.Visibility = OutlineCheckBox.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void OutlineColor_Changed(object sender, uint e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            if (OutlineCheckBox?.IsChecked == true)
                RenderViewport();
        }

        private void OutlineSize_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            if (OutlineCheckBox?.IsChecked == true)
                RenderViewport();
        }

        private void PixelBaseSize_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            if (_lastVolume != null)
                RenderViewport();
        }

        private void BackdropGrid_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        // ════════════════════════════════════════════════════════════════════
        // FACE PAINTER (PHASE 1)
        // ════════════════════════════════════════════════════════════════════

        private void FacePainter_Changed(object sender, RoutedEventArgs e)
        {
            if (FacePainterPanel != null)
            {
                FacePainterPanel.Visibility = FacePainterCheckBox?.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            _pointerDragMode = PointerDragMode.None;
            _lastStrokePaintFace = null;
            _camera.EndDrag();
            UpdateFacePainterStatusText();
        }

        private void FacePainterMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateFacePainterStatusText();
        }

        private void ClearFaceOverridesButton_Click(object sender, RoutedEventArgs e)
        {
            ClearAllManualFaceColorOverrides();
            UpdateFacePainterStatusText("Cleared all face overrides.");
        }

        private bool IsFacePainterEnabled()
            => FacePainterCheckBox?.IsChecked == true;

        private FacePainterMode GetFacePainterMode()
        {
            int idx = FacePainterModeCombo?.SelectedIndex ?? 0;
            return idx switch
            {
                1 => FacePainterMode.Sample,
                2 => FacePainterMode.EraseOverride,
                _ => FacePainterMode.Paint,
            };
        }

        private void UpdateFacePainterStatusText(string? custom = null)
        {
            if (FacePainterStatusText == null)
                return;

            if (!string.IsNullOrWhiteSpace(custom))
            {
                FacePainterStatusText.Text = custom!;
                return;
            }

            if (!IsFacePainterEnabled())
            {
                FacePainterStatusText.Text = "LMB drag rotates. Enable Face Painter to paint/sample/erase single voxel faces.";
                return;
            }

            string modeText = GetFacePainterMode() switch
            {
                FacePainterMode.Paint => "Paint",
                FacePainterMode.Sample => "Sample",
                FacePainterMode.EraseOverride => "Erase Override",
                _ => "Paint"
            };

            string fgText = FormatBgraHex(_palette?.Foreground ?? 0xFF000000);
            FacePainterStatusText.Text =
                $"Mode: {modeText}. LMB apply at hovered face. RMB drag rotates. FG: {fgText}.";
        }

        // ════════════════════════════════════════════════════════════════════
        // VIEWPORT RENDERING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Renders the current mesh to the viewport Image control.
        /// </summary>
        /// <remarks>
        /// In normal mode, renders at <see cref="_viewportWidth"/> × <see cref="_viewportHeight"/>.
        /// In pixel preview mode, renders at the volume's native resolution (e.g. 16×16)
        /// then nearest-neighbor upscales to the viewport size, preserving the crisp
        /// pixel-art aesthetic at any rotation.
        /// </remarks>
        private void RenderViewport()
        {
            try
            {
                if (ViewportImage == null)
                    return;

                UpdateViewportSizeFromControl();

                bool pixelMode = PixelPreviewCheckBox?.IsChecked == true && _lastVolume != null;
                int renderW, renderH;
                int displayW = _viewportWidth;
                int displayH = _viewportHeight;
                int screenPixelSize = 1;
                int usedDisplayW = displayW;
                int usedDisplayH = displayH;

                if (pixelMode)
                {
                    // ── Pixel-perfect layout ──────────────────────────────
                    // Compute integer screen pixel size from configured base size and zoom
                    int pixelBaseSize = GetPixelPreviewBaseSize();
                    screenPixelSize = ComputePixelPreviewScreenPixelSize(pixelBaseSize, _camera.ZoomPercent);

                    // Snap viewport to multiples of screenPixelSize for clean integer scaling
                    int snappedW = Math.Max(screenPixelSize, (_viewportWidth / screenPixelSize) * screenPixelSize);
                    int snappedH = Math.Max(screenPixelSize, (_viewportHeight / screenPixelSize) * screenPixelSize);

                    // Render target = how many "voxel pixels" fit in the snapped area
                    renderW = Math.Max(1, snappedW / screenPixelSize);
                    renderH = Math.Max(1, snappedH / screenPixelSize);

                    // Odd dimensions for stable center alignment
                    if ((renderW & 1) == 0 && renderW > 1) renderW--;
                    if ((renderH & 1) == 0 && renderH > 1) renderH--;

                    // Recompute used area after odd-snap
                    int usedW = renderW * screenPixelSize;
                    int usedH = renderH * screenPixelSize;
                    usedDisplayW = usedW;
                    usedDisplayH = usedH;

                    // Tell the camera about the pixel-perfect frustum
                    _camera.EnablePixelPerfectFrustum(renderW, renderH);
                    _camera.ResizeViewport(renderW, renderH);
                }
                else
                {
                    renderW = _viewportWidth;
                    renderH = _viewportHeight;
                    _camera.DisablePixelPerfectFrustum();
                    _camera.ResizeViewport(renderW, renderH);
                }

                // Snap the displayed Image size to device pixels so XAML doesn't introduce
                // fractional layout scaling on top of the integer pixel upscale.
                int usedDisplayPhysicalW = Math.Max(1, (int)Math.Round(usedDisplayW * _viewportRasterScale));
                int usedDisplayPhysicalH = Math.Max(1, (int)Math.Round(usedDisplayH * _viewportRasterScale));
                ConfigureViewportImagePresentation(pixelMode, usedDisplayPhysicalW, usedDisplayPhysicalH);

                // Ensure render buffers
                int pixelCount = renderW * renderH;
                if (_renderBuffer == null || _renderBuffer.Length != pixelCount * 4)
                {
                    _renderBuffer = new byte[pixelCount * 4];
                }

                if (_lastVolume != null && _lastVolume.OccupiedCount > 0)
                {
                    int outlineVoxelSize = Math.Max(1, (int)Math.Round(OutlineSizeBox?.Value ?? 1d));
                    int outlineRenderSize = outlineVoxelSize;
                    if (!pixelMode)
                    {
                        var fr = _camera.GetFrustum();
                        float pixelsPerVoxel = renderH / MathF.Max(1e-6f, fr.Height); // 1 voxel = 1 world unit
                        outlineRenderSize = Math.Max(1, (int)MathF.Round(outlineVoxelSize * pixelsPerVoxel));
                        outlineRenderSize = Math.Min(Math.Max(renderW, renderH), outlineRenderSize);
                    }

                    var opts = new VoxelRenderer.RenderOptions
                    {
                        // With z-buffer rendering, disabling backface cull avoids
                        // edge-angle face loss in the preview.
                        BackfaceCull = false,
                        // In pixel preview we draw the backing grid as a separate 2D pass
                        // so it does not change the voxel rasterization path.
                        DrawBackdropGrid = !pixelMode && (BackdropGridCheckBox?.IsChecked != false),
                        DrawOutline = OutlineCheckBox?.IsChecked == true,
                        OutlineColor = OutlineColorSwatch.Color,
                        OutlineSize = outlineRenderSize,
                    };

                    bool renderedExactCardinal = false;
                    bool renderedFromPixelSpriteCache = false;
                    if (pixelMode)
                    {
                        renderedExactCardinal = TryRenderExactCardinalPixelPreview(
                            _lastVolume, renderW, renderH, _renderBuffer, ClearColor, opts);

                        if (!renderedExactCardinal)
                        {
                            renderedFromPixelSpriteCache = TryRenderCachedPixelPreviewSprite(
                                _lastVolume, renderW, renderH, _renderBuffer, ClearColor, opts);
                        }
                    }

                    if (!renderedExactCardinal && !renderedFromPixelSpriteCache)
                    {
                        VoxelRenderer.Render(
                            _lastVolume, _camera,
                            renderW, renderH,
                            _renderBuffer,
                            ClearColor, opts);
                    }
                }
                else
                {
                    FillClear(_renderBuffer, pixelCount);
                }

                // Build the display buffer
                byte[] displayBuffer;

                if (pixelMode && screenPixelSize > 1)
                {
                    // Integer-scale upscale: each render pixel becomes exactly
                    // screenPixelSize × screenPixelSize screen pixels — no distortion
                    displayW = renderW * screenPixelSize;
                    displayH = renderH * screenPixelSize;
                    displayBuffer = new byte[displayW * displayH * 4];
                    FillClear(displayBuffer, displayW * displayH);

                    if (BackdropGridCheckBox?.IsChecked != false && _lastVolume != null)
                    {
                        DrawPixelPreviewBackdropGrid2D(
                            displayBuffer, displayW, displayH,
                            _lastVolume.Size, screenPixelSize,
                            marginVoxels: 5,
                            minorColor: 0xFF2A2F35,
                            majorColor: 0xFF39424B,
                            majorEvery: 4);
                    }

                    // Blit the integer-scaled pixels. Centering is handled by the Image layout.
                    for (int sy = 0; sy < renderH; sy++)
                    {
                        int dstBaseY = sy * screenPixelSize;
                        for (int sx = 0; sx < renderW; sx++)
                        {
                            int si = (sy * renderW + sx) * 4;
                            byte b0 = _renderBuffer[si];
                            byte b1 = _renderBuffer[si + 1];
                            byte b2 = _renderBuffer[si + 2];
                            byte b3 = _renderBuffer[si + 3];

                            int dstBaseX = sx * screenPixelSize;

                            for (int py = 0; py < screenPixelSize; py++)
                            {
                                int dy = dstBaseY + py;
                                if (dy < 0 || dy >= displayH) continue;
                                for (int px = 0; px < screenPixelSize; px++)
                                {
                                    int dx = dstBaseX + px;
                                    if (dx < 0 || dx >= displayW) continue;

                                    int di = (dy * displayW + dx) * 4;
                                    displayBuffer[di]     = b0;
                                    displayBuffer[di + 1] = b1;
                                    displayBuffer[di + 2] = b2;
                                    displayBuffer[di + 3] = b3;
                                }
                            }
                        }
                    }
                }
                else
                {
                    displayW = renderW;
                    displayH = renderH;
                    displayBuffer = _renderBuffer;
                }

                // Push to WriteableBitmap
                _viewportBitmap = new WriteableBitmap(displayW, displayH);
                using var stream = _viewportBitmap.PixelBuffer.AsStream();
                stream.Seek(0, SeekOrigin.Begin);
                stream.Write(displayBuffer, 0, displayW * displayH * 4);

                ViewportImage.Source = _viewportBitmap;
                UpdateCameraStatsText(pixelMode, screenPixelSize, renderW, renderH);
                PersistVoxelPreviewStateToDocument();
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Viewport render failed: {Error}", ex.Message);
            }
        }

        private bool UpdateViewportSizeFromControl()
        {
            FrameworkElement? sizingElement = (FrameworkElement?)ViewportHost ?? ViewportImage;
            if (sizingElement == null)
                return false;

            int w = Math.Max(1, (int)Math.Round(sizingElement.ActualWidth));
            int h = Math.Max(1, (int)Math.Round(sizingElement.ActualHeight));
            float rasterScale = GetViewportRasterScale();

            bool sameSize = (w == _viewportWidth && h == _viewportHeight);
            bool sameScale = MathF.Abs(rasterScale - _viewportRasterScale) < 0.001f;
            if (sameSize && sameScale)
                return false;

            _viewportWidth = w;
            _viewportHeight = h;
            _viewportRasterScale = rasterScale;
            _renderBuffer = null;
            return true;
        }

        private float GetViewportRasterScale()
        {
            try
            {
                double scale = ViewportHost?.XamlRoot?.RasterizationScale
                    ?? ViewportImage?.XamlRoot?.RasterizationScale
                    ?? 1.0;
                return MathF.Max(1f, (float)scale);
            }
            catch
            {
                return 1f;
            }
        }

        private void ConfigureViewportImagePresentation(bool pixelMode, int usedPhysicalPixelWidth, int usedPhysicalPixelHeight)
        {
            if (ViewportImage == null) return;

            // In pixel-preview mode, present the already-upscaled bitmap at an explicit
            // centered size so XAML is not doing an extra Uniform fit pass for us.
            if (pixelMode)
            {
                ViewportImage.Stretch = Stretch.None;
                ViewportImage.HorizontalAlignment = HorizontalAlignment.Center;
                ViewportImage.VerticalAlignment = VerticalAlignment.Center;

                double scale = Math.Max(1e-6, _viewportRasterScale);
                ViewportImage.Width = Math.Max(1, usedPhysicalPixelWidth) / scale;
                ViewportImage.Height = Math.Max(1, usedPhysicalPixelHeight) / scale;
                TrySetViewportImageInterpolationMode(nearest: true);
            }
            else
            {
                ViewportImage.Stretch = Stretch.Uniform;
                ViewportImage.HorizontalAlignment = HorizontalAlignment.Stretch;
                ViewportImage.VerticalAlignment = VerticalAlignment.Stretch;
                ViewportImage.Width = double.NaN;
                ViewportImage.Height = double.NaN;
                TrySetViewportImageInterpolationMode(nearest: false);
            }
        }

        private void TrySetViewportImageInterpolationMode(bool nearest)
        {
            if (ViewportImage == null) return;

            try
            {
                // WinUI/Uno support varies by platform. Use reflection so desktop builds
                // can take advantage of nearest-neighbor presentation when available
                // without hard-failing on targets that omit the API.
                var imageType = ViewportImage.GetType();
                var prop = imageType.GetProperty("BitmapInterpolationMode");
                if (prop != null && prop.PropertyType.IsEnum)
                {
                    string enumName = nearest ? "NearestNeighbor" : "Linear";
                    object value = Enum.Parse(prop.PropertyType, enumName);
                    prop.SetValue(ViewportImage, value);
                    return;
                }

                Type? renderOptionsType =
                    Type.GetType("Microsoft.UI.Xaml.Media.RenderOptions, Microsoft.WinUI")
                    ?? Type.GetType("Windows.UI.Xaml.Media.RenderOptions, Windows");
                if (renderOptionsType == null) return;

                var setMethod = renderOptionsType.GetMethod(
                    "SetBitmapInterpolationMode",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (setMethod == null) return;

                var paramTypes = setMethod.GetParameters();
                if (paramTypes.Length != 2 || !paramTypes[1].ParameterType.IsEnum) return;

                string attachedEnumName = nearest ? "NearestNeighbor" : "Linear";
                object enumValue = Enum.Parse(paramTypes[1].ParameterType, attachedEnumName);
                setMethod.Invoke(null, new object?[] { ViewportImage, enumValue });
            }
            catch
            {
                // Some platforms/backends may not expose this property; pixel mode still
                // benefits from explicit sizing and integer upscaling.
            }
        }

        private void PixelPreview_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();

            PreserveVisualScaleOnPixelPreviewToggle();

            // Force buffer reallocation on mode change
            _renderBuffer = null;
            RenderViewport();
        }

        private int GetPixelPreviewBaseSize()
        {
            return Math.Max(1, (int)Math.Round(PixelBaseSizeBox?.Value ?? 16d));
        }

        private void PreserveVisualScaleOnPixelPreviewToggle()
        {
            if (_lastVolume == null) return;

            UpdateViewportSizeFromControl();

            bool pixelModeNow = PixelPreviewCheckBox?.IsChecked == true;
            float viewportH = MathF.Max(1f, _viewportHeight);
            float gridSize = MathF.Max(1f, _camera.GridSize);

            if (pixelModeNow)
            {
                // Converting from normal ortho zoom -> pixel-preview integer voxel scale
                float normalPixelsPerVoxel = viewportH * (_camera.ZoomPercent / 100f) / gridSize;
                int targetScreenPixelSize = Math.Max(1, (int)MathF.Round(normalPixelsPerVoxel));
                int basePixelSize = GetPixelPreviewBaseSize();
                float targetZoomPercent = (targetScreenPixelSize * 100f) / basePixelSize;
                _camera.SetZoomPercent(targetZoomPercent);
            }
            else
            {
                // Converting from pixel-preview integer voxel scale -> normal ortho zoom
                int basePixelSize = GetPixelPreviewBaseSize();
                int screenPixelSize = ComputePixelPreviewScreenPixelSize(basePixelSize, _camera.ZoomPercent);
                float targetZoomPercent = (screenPixelSize * gridSize * 100f) / viewportH;
                _camera.SetZoomPercent(targetZoomPercent);
            }
        }

        /// <summary>
        /// Fills the buffer with the clear color.
        /// </summary>
        private static void FillClear(byte[] buffer, int pixelCount)
        {
            byte b = (byte)(ClearColor & 0xFF);
            byte g = (byte)((ClearColor >> 8) & 0xFF);
            byte r = (byte)((ClearColor >> 16) & 0xFF);
            byte a = (byte)((ClearColor >> 24) & 0xFF);

            for (int i = 0; i < pixelCount; i++)
            {
                int bi = i * 4;
                buffer[bi] = b;
                buffer[bi + 1] = g;
                buffer[bi + 2] = r;
                buffer[bi + 3] = a;
            }
        }

        private void ApplyFacePainterActionAtHostPoint(Windows.Foundation.Point hostPoint, bool continuousStroke = false)
        {
            if (!IsFacePainterEnabled() || _lastVolume == null || _lastVolume.OccupiedCount == 0)
                return;

            if (!TryPickVoxelFaceAtHostPoint(hostPoint, out var picked))
            {
                if (!continuousStroke)
                    UpdateFacePainterStatusText("No face under cursor.");
                return;
            }

            if (continuousStroke && _lastStrokePaintFace.HasValue && _lastStrokePaintFace.Value.Equals(picked))
                return;

            _lastStrokePaintFace = picked;

            switch (GetFacePainterMode())
            {
                case FacePainterMode.Paint:
                {
                    uint color = _palette?.Foreground ?? 0xFF000000;
                    SetManualFaceColorOverride(picked.X, picked.Y, picked.Z, picked.Face, color);
                    UpdateFacePainterStatusText(
                        $"Painted {picked.Face} @ ({picked.X},{picked.Y},{picked.Z}) with {FormatBgraHex(color)}.");
                    break;
                }

                case FacePainterMode.Sample:
                {
                    var sampled = _lastVolume.GetFaceColor(picked.X, picked.Y, picked.Z, picked.Face);
                    uint color = PackedBgraFromRgba(sampled);
                    _palette?.SetForeground(color);
                    UpdateFacePainterStatusText(
                        $"Sampled {picked.Face} @ ({picked.X},{picked.Y},{picked.Z}) -> FG {FormatBgraHex(color)}.");
                    break;
                }

                case FacePainterMode.EraseOverride:
                {
                    ClearManualFaceColorOverride(picked.X, picked.Y, picked.Z, picked.Face);
                    UpdateFacePainterStatusText(
                        $"Erased override on {picked.Face} @ ({picked.X},{picked.Y},{picked.Z}).");
                    break;
                }
            }
        }

        private bool TryPickVoxelFaceAtHostPoint(Windows.Foundation.Point hostPoint, out PickedVoxelFace picked)
        {
            picked = default;
            if (_lastVolume == null || _lastVolume.OccupiedCount == 0 || ViewportHost == null || _viewportBitmap == null)
                return false;

            if (!TryMapHostPointToCameraViewport(hostPoint, out float viewportX, out float viewportY))
                return false;

            return TryRayPickVoxelFace(_lastVolume, viewportX, viewportY, out picked);
        }

        private bool TryMapHostPointToCameraViewport(
            Windows.Foundation.Point hostPoint,
            out float viewportX,
            out float viewportY)
        {
            viewportX = 0f;
            viewportY = 0f;

            if (ViewportHost == null || _viewportBitmap == null)
                return false;

            if (!TryGetPresentedImageRectInHostDip(out var imageRect))
                return false;

            if (hostPoint.X < imageRect.X || hostPoint.Y < imageRect.Y ||
                hostPoint.X >= imageRect.X + imageRect.Width || hostPoint.Y >= imageRect.Y + imageRect.Height)
            {
                return false;
            }

            double srcW = Math.Max(1, _viewportBitmap.PixelWidth);
            double srcH = Math.Max(1, _viewportBitmap.PixelHeight);
            double u = (hostPoint.X - imageRect.X) / Math.Max(1e-6, imageRect.Width);
            double v = (hostPoint.Y - imageRect.Y) / Math.Max(1e-6, imageRect.Height);

            double imgX = Math.Clamp(u, 0.0, 0.999999) * srcW;
            double imgY = Math.Clamp(v, 0.0, 0.999999) * srcH;

            float camViewportW = MathF.Max(1f, _camera.ViewportWidth);
            float camViewportH = MathF.Max(1f, _camera.ViewportHeight);
            float sx = (float)(srcW / camViewportW);
            float sy = (float)(srcH / camViewportH);

            viewportX = (float)imgX / MathF.Max(1e-6f, sx);
            viewportY = (float)imgY / MathF.Max(1e-6f, sy);
            return true;
        }

        private bool TryGetPresentedImageRectInHostDip(out Windows.Foundation.Rect rect)
        {
            rect = default;
            if (ViewportHost == null || _viewportBitmap == null)
                return false;

            double hostW = Math.Max(1.0, ViewportHost.ActualWidth);
            double hostH = Math.Max(1.0, ViewportHost.ActualHeight);
            double srcW = Math.Max(1.0, _viewportBitmap.PixelWidth);
            double srcH = Math.Max(1.0, _viewportBitmap.PixelHeight);

            bool pixelMode = PixelPreviewCheckBox?.IsChecked == true && _lastVolume != null;
            double drawW;
            double drawH;

            if (pixelMode && ViewportImage != null &&
                !double.IsNaN(ViewportImage.Width) && !double.IsNaN(ViewportImage.Height))
            {
                drawW = Math.Max(1.0, ViewportImage.Width);
                drawH = Math.Max(1.0, ViewportImage.Height);
            }
            else
            {
                double scale = Math.Min(hostW / srcW, hostH / srcH);
                if (!(scale > 0)) return false;
                drawW = srcW * scale;
                drawH = srcH * scale;
            }

            double x = (hostW - drawW) * 0.5;
            double y = (hostH - drawH) * 0.5;
            rect = new Windows.Foundation.Rect(x, y, drawW, drawH);
            return true;
        }

        private bool TryRayPickVoxelFace(VoxelVolume volume, float screenX, float screenY, out PickedVoxelFace picked)
        {
            picked = default;
            if (volume == null) return false;

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            var fr = _camera.GetFrustum();

            float viewportW = MathF.Max(1f, _camera.ViewportWidth);
            float viewportH = MathF.Max(1f, _camera.ViewportHeight);

            float xNdc = ((screenX / viewportW) * 2f) - 1f;
            float yNdc = 1f - ((screenY / viewportH) * 2f);
            float cx = ((xNdc * fr.Width) + (fr.Left + fr.Right)) * 0.5f;
            float cy = ((yNdc * fr.Height) + (fr.Top + fr.Bottom)) * 0.5f;

            var rayOrigin = pose.Position + (basis.Right * cx) + (basis.Up * cy);
            var rayDir = basis.Forward;

            int size = volume.Size;
            float half = size * 0.5f;
            var boxMin = new Vector3(-half, -half, -half);
            var boxMax = new Vector3(half, half, half);

            if (!TryIntersectRayAabb(rayOrigin, rayDir, boxMin, boxMax, out float tEnter, out float tExit, out int hitAxis))
                return false;
            if (tExit < 0f)
                return false;

            tEnter = MathF.Max(0f, tEnter);
            const float eps = 1e-4f;
            var p = rayOrigin + (rayDir * (tEnter + eps));

            int ix = Math.Clamp((int)MathF.Floor(p.X + half), 0, size - 1);
            int iy = Math.Clamp((int)MathF.Floor(p.Y + half), 0, size - 1);
            int iz = Math.Clamp((int)MathF.Floor(p.Z + half), 0, size - 1);

            int stepX = rayDir.X > 0f ? 1 : (rayDir.X < 0f ? -1 : 0);
            int stepY = rayDir.Y > 0f ? 1 : (rayDir.Y < 0f ? -1 : 0);
            int stepZ = rayDir.Z > 0f ? 1 : (rayDir.Z < 0f ? -1 : 0);

            float tMaxX = ComputeNextAxisBoundaryT(rayOrigin.X, rayDir.X, ix, stepX, half);
            float tMaxY = ComputeNextAxisBoundaryT(rayOrigin.Y, rayDir.Y, iy, stepY, half);
            float tMaxZ = ComputeNextAxisBoundaryT(rayOrigin.Z, rayDir.Z, iz, stepZ, half);
            float tDeltaX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1f / rayDir.X);
            float tDeltaY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1f / rayDir.Y);
            float tDeltaZ = stepZ == 0 ? float.PositiveInfinity : MathF.Abs(1f / rayDir.Z);

            var entryFace = EntryFaceFromRayAabbAxis(hitAxis, rayDir);
            int maxSteps = (size * 3) + 8;
            float tCurrent = tEnter;

            for (int step = 0; step < maxSteps; step++)
            {
                if ((uint)ix >= (uint)size || (uint)iy >= (uint)size || (uint)iz >= (uint)size)
                    return false;

                if (volume.IsOccupied(ix, iy, iz))
                {
                    picked = new PickedVoxelFace(ix, iy, iz, entryFace);
                    return true;
                }

                if (tCurrent > tExit)
                    return false;

                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    ix += stepX;
                    tCurrent = tMaxX;
                    tMaxX += tDeltaX;
                    entryFace = stepX > 0 ? Face.Left : Face.Right;
                }
                else if (tMaxY <= tMaxX && tMaxY <= tMaxZ)
                {
                    iy += stepY;
                    tCurrent = tMaxY;
                    tMaxY += tDeltaY;
                    entryFace = stepY > 0 ? Face.Bottom : Face.Top;
                }
                else
                {
                    iz += stepZ;
                    tCurrent = tMaxZ;
                    tMaxZ += tDeltaZ;
                    entryFace = stepZ > 0 ? Face.Front : Face.Back;
                }
            }

            return false;
        }

        private static float ComputeNextAxisBoundaryT(float origin, float dir, int voxelIndex, int step, float half)
        {
            if (step == 0 || MathF.Abs(dir) < 1e-12f)
                return float.PositiveInfinity;

            float boundary = step > 0
                ? (voxelIndex + 1) - half
                : voxelIndex - half;

            return (boundary - origin) / dir;
        }

        private static Face EntryFaceFromRayAabbAxis(int axis, Vector3 dir)
        {
            return axis switch
            {
                0 => dir.X >= 0f ? Face.Left : Face.Right,
                1 => dir.Y >= 0f ? Face.Bottom : Face.Top,
                2 => dir.Z >= 0f ? Face.Front : Face.Back,
                _ => Face.Front,
            };
        }

        private static bool TryIntersectRayAabb(
            Vector3 origin,
            Vector3 dir,
            Vector3 boxMin,
            Vector3 boxMax,
            out float tEnter,
            out float tExit,
            out int enterAxis)
        {
            tEnter = float.NegativeInfinity;
            tExit = float.PositiveInfinity;
            enterAxis = -1;

            for (int axis = 0; axis < 3; axis++)
            {
                float o = axis == 0 ? origin.X : (axis == 1 ? origin.Y : origin.Z);
                float d = axis == 0 ? dir.X : (axis == 1 ? dir.Y : dir.Z);
                float min = axis == 0 ? boxMin.X : (axis == 1 ? boxMin.Y : boxMin.Z);
                float max = axis == 0 ? boxMax.X : (axis == 1 ? boxMax.Y : boxMax.Z);

                if (MathF.Abs(d) < 1e-12f)
                {
                    if (o < min || o > max)
                        return false;
                    continue;
                }

                float inv = 1f / d;
                float t0 = (min - o) * inv;
                float t1 = (max - o) * inv;
                if (t0 > t1)
                {
                    (t0, t1) = (t1, t0);
                }

                if (t0 > tEnter)
                {
                    tEnter = t0;
                    enterAxis = axis;
                }

                if (t1 < tExit)
                    tExit = t1;

                if (tEnter > tExit)
                    return false;
            }

            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        // POINTER INTERACTION (ORBIT + ZOOM)
        // ════════════════════════════════════════════════════════════════════

        private void Viewport_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            UIElement? inputTarget = (UIElement?)ViewportHost ?? ViewportImage;
            if (inputTarget == null) return;

            var point = e.GetCurrentPoint(inputTarget);
            var props = point.Properties;
            _lastPointerPos = point.Position;
            _lastStrokePaintFace = null;

            if (IsFacePainterEnabled())
            {
                // In face-painter mode, preserve RMB drag for camera orbit and use LMB for face actions.
                if (props.IsRightButtonPressed || props.IsMiddleButtonPressed)
                {
                    _pointerDragMode = PointerDragMode.Orbit;
                    _camera.BeginDrag();
                }
                else if (props.IsLeftButtonPressed)
                {
                    _pointerDragMode = PointerDragMode.FacePaintStroke;
                    ApplyFacePainterActionAtHostPoint(point.Position);
                }
                else
                {
                    _pointerDragMode = PointerDragMode.None;
                }
            }
            else
            {
                _pointerDragMode = PointerDragMode.Orbit;
                _camera.BeginDrag();
            }

            ((UIElement)sender).CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Viewport_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            UIElement? inputTarget = (UIElement?)ViewportHost ?? ViewportImage;
            if (inputTarget == null) return;

            var pos = e.GetCurrentPoint(inputTarget).Position;
            float dx = (float)(pos.X - _lastPointerPos.X);
            float dy = (float)(pos.Y - _lastPointerPos.Y);

            if (_pointerDragMode == PointerDragMode.Orbit && _camera.IsDragging)
            {
                _lastPointerPos = pos;
                _camera.UpdateDrag(dx, dy);
                RenderViewport();
                e.Handled = true;
                return;
            }

            if (_pointerDragMode == PointerDragMode.FacePaintStroke && IsFacePainterEnabled())
            {
                _lastPointerPos = pos;
                var props = e.GetCurrentPoint(inputTarget).Properties;
                if (props.IsLeftButtonPressed)
                {
                    ApplyFacePainterActionAtHostPoint(pos, continuousStroke: true);
                }
                else
                {
                    _pointerDragMode = PointerDragMode.None;
                    _lastStrokePaintFace = null;
                }

                e.Handled = true;
                return;
            }

            _lastPointerPos = pos;
        }

        private void Viewport_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_pointerDragMode == PointerDragMode.Orbit)
            {
                _camera.EndDrag();
            }

            _pointerDragMode = PointerDragMode.None;
            _lastStrokePaintFace = null;
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        private void Viewport_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint((UIElement)sender).Properties;
            float delta = props.MouseWheelDelta / 120f;
            AdjustZoomForCurrentMode(delta);
            RenderViewport();
            e.Handled = true;
        }

        // ════════════════════════════════════════════════════════════════════
        // PRESET VIEW BUTTONS
        // ════════════════════════════════════════════════════════════════════

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string viewName)
            {
                _camera.SetView(viewName);
                StartAnimationLoop();
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _camera.Reset();
            RenderViewport();
        }

        /// <summary>
        /// Starts a render loop that ticks until the camera animation completes.
        /// </summary>
        private void StartAnimationLoop()
        {
            if (!_camera.IsAnimating)
            {
                RenderViewport();
                return;
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (s, _) =>
            {
                bool running = _camera.UpdateAnimation();
                RenderViewport();
                if (!running) timer.Stop();
            };
            timer.Start();
        }

        private void AdjustZoomForCurrentMode(float wheelSteps)
        {
            if (wheelSteps == 0f) return;

            bool pixelMode = PixelPreviewCheckBox?.IsChecked == true && _lastVolume != null;
            if (!pixelMode || _lastVolume == null)
            {
                _camera.Zoom(wheelSteps);
                return;
            }

            int basePixelSize = GetPixelPreviewBaseSize();
            int currentScreenPixelSize = ComputePixelPreviewScreenPixelSize(basePixelSize, _camera.ZoomPercent);

            int stepCount = Math.Max(1, (int)MathF.Round(MathF.Abs(wheelSteps)));
            int direction = wheelSteps > 0f ? 1 : -1;
            int targetScreenPixelSize = Math.Max(1, currentScreenPixelSize + (direction * stepCount));

            // Convert integer pixel-preview scale back into zoom percent.
            float targetZoomPercent = (targetScreenPixelSize * 100f) / basePixelSize;
            _camera.SetZoomPercent(targetZoomPercent);
        }

        private static int ComputePixelPreviewScreenPixelSize(int basePixelSize, float zoomPercent)
        {
            float zoomScale = MathF.Max(0.01f, zoomPercent / 100f);
            return Math.Max(1, (int)MathF.Floor((basePixelSize * zoomScale) + 1e-3f));
        }

        /// <summary>
        /// Pixel-preview zoom in PixZel feels stable because zooming mainly scales a low-res
        /// render, rather than re-rasterizing geometry at every zoom step. For free rotation
        /// / isometric views, cache a pixel-perfect low-res sprite per camera orientation and
        /// reuse it while only zoom changes.
        /// </summary>
        private bool TryRenderCachedPixelPreviewSprite(
            VoxelVolume volume,
            int renderW,
            int renderH,
            byte[] buffer,
            uint clearColor,
            VoxelRenderer.RenderOptions opts)
        {
            if (volume == null) return false;
            if (buffer == null || buffer.Length < renderW * renderH * 4) return false;

            string? snap = _camera.CurrentSnapName;
            if (!string.IsNullOrWhiteSpace(snap) && IsCardinalOrthoSnap(snap))
                return false; // exact ortho image path handles cardinal snaps

            int spriteSide = ComputePixelPreviewSpriteSide(volume.Size, opts.OutlineSize);
            EnsurePixelPreviewSpriteCache(volume, spriteSide, opts);
            if (_pixelPreviewSpriteCache == null) return false;

            bool cacheValid =
                MathF.Abs(_pixelPreviewSpriteCache.Pitch - _camera.Pitch) <= 1e-6f &&
                MathF.Abs(_pixelPreviewSpriteCache.Yaw - _camera.Yaw) <= 1e-6f &&
                string.Equals(_pixelPreviewSpriteCache.SnapName, _camera.CurrentSnapName, StringComparison.OrdinalIgnoreCase) &&
                _pixelPreviewSpriteCache.DrawOutline == opts.DrawOutline &&
                _pixelPreviewSpriteCache.OutlineColor == opts.OutlineColor &&
                _pixelPreviewSpriteCache.OutlineSize == opts.OutlineSize;

            if (!cacheValid)
            {
                // The cache render temporarily changes the camera's pixel-perfect render target.
                // Restore the current viewport/frustum immediately afterward.
                _camera.EnablePixelPerfectFrustum(spriteSide, spriteSide);
                _camera.ResizeViewport(spriteSide, spriteSide);

                VoxelRenderer.Render(
                    volume, _camera,
                    spriteSide, spriteSide,
                    _pixelPreviewSpriteCache.Buffer,
                    clearColor: 0x00000000, // transparent background for compositing
                    CloneRenderOptionsForSpriteCache(opts));

                _pixelPreviewSpriteCache.Width = spriteSide;
                _pixelPreviewSpriteCache.Height = spriteSide;
                _pixelPreviewSpriteCache.Pitch = _camera.Pitch;
                _pixelPreviewSpriteCache.Yaw = _camera.Yaw;
                _pixelPreviewSpriteCache.SnapName = _camera.CurrentSnapName;
                _pixelPreviewSpriteCache.DrawOutline = opts.DrawOutline;
                _pixelPreviewSpriteCache.OutlineColor = opts.OutlineColor;
                _pixelPreviewSpriteCache.OutlineSize = opts.OutlineSize;
            }

            _camera.EnablePixelPerfectFrustum(renderW, renderH);
            _camera.ResizeViewport(renderW, renderH);

            FillClear(buffer, renderW * renderH);
            BlitCenteredOpaqueOverBackground(
                _pixelPreviewSpriteCache.Buffer,
                _pixelPreviewSpriteCache.Width, _pixelPreviewSpriteCache.Height,
                buffer, renderW, renderH);

            return true;
        }

        private void EnsurePixelPreviewSpriteCache(
            VoxelVolume volume,
            int spriteSide,
            VoxelRenderer.RenderOptions opts)
        {
            bool needsNew =
                _pixelPreviewSpriteCache == null ||
                !ReferenceEquals(_pixelPreviewSpriteCache.Volume, volume) ||
                _pixelPreviewSpriteCache.Width != spriteSide ||
                _pixelPreviewSpriteCache.Height != spriteSide ||
                _pixelPreviewSpriteCache.Buffer.Length != spriteSide * spriteSide * 4;

            if (needsNew)
            {
                _pixelPreviewSpriteCache = new PixelPreviewSpriteCache
                {
                    Volume = volume,
                    Buffer = new byte[spriteSide * spriteSide * 4],
                    Width = spriteSide,
                    Height = spriteSide,
                    Pitch = float.NaN,
                    Yaw = float.NaN,
                    SnapName = null,
                    DrawOutline = opts.DrawOutline,
                    OutlineColor = opts.OutlineColor,
                    OutlineSize = opts.OutlineSize,
                };
                return;
            }

            // If only zoom changed, the cache remains valid. We don't need to clear or rerender here.
        }

        private static int ComputePixelPreviewSpriteSide(int volumeSize, int outlineSize)
        {
            volumeSize = Math.Max(1, volumeSize);
            outlineSize = Math.Max(0, outlineSize);

            // Max projected cube extent on one screen axis is < size * sqrt(3). Add padding
            // for outline and breathing room so the sprite can be re-centered/cropped safely.
            int pad = Math.Max(8, outlineSize + 6);
            int side = (int)MathF.Ceiling((volumeSize * 1.9f) + (pad * 2));
            side = Math.Max(side, volumeSize + (pad * 2));
            if ((side & 1) == 0) side++;
            return side;
        }

        private static VoxelRenderer.RenderOptions CloneRenderOptionsForSpriteCache(VoxelRenderer.RenderOptions src)
        {
            return new VoxelRenderer.RenderOptions
            {
                BackfaceCull = src.BackfaceCull,
                BackfaceCullEpsilon = src.BackfaceCullEpsilon,
                DrawOutline = src.DrawOutline,
                OutlineColor = src.OutlineColor,
                OutlineSize = src.OutlineSize,
                DrawBackdropGrid = false,
                BackdropGridMinorColor = src.BackdropGridMinorColor,
                BackdropGridMajorColor = src.BackdropGridMajorColor,
                BackdropGridMajorEvery = src.BackdropGridMajorEvery,
                BackdropGridMarginVoxels = src.BackdropGridMarginVoxels,
                LightTop = src.LightTop,
                LightSide = src.LightSide,
                LightFront = src.LightFront,
            };
        }

        private static void BlitCenteredOpaqueOverBackground(
            byte[] src, int srcW, int srcH,
            byte[] dst, int dstW, int dstH)
        {
            if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0) return;
            if (src == null || dst == null) return;

            int offsetX = (dstW - srcW) / 2;
            int offsetY = (dstH - srcH) / 2;

            for (int sy = 0; sy < srcH; sy++)
            {
                int dy = sy + offsetY;
                if ((uint)dy >= (uint)dstH) continue;

                for (int sx = 0; sx < srcW; sx++)
                {
                    int dx = sx + offsetX;
                    if ((uint)dx >= (uint)dstW) continue;

                    int si = (sy * srcW + sx) * 4;
                    byte a = src[si + 3];
                    if (a == 0) continue;

                    int di = (dy * dstW + dx) * 4;
                    dst[di] = src[si];
                    dst[di + 1] = src[si + 1];
                    dst[di + 2] = src[si + 2];
                    dst[di + 3] = a;
                }
            }
        }

        /// <summary>
        /// In pixel preview + snapped orthographic views, bypasses the triangle rasterizer and
        /// stamps an exact voxel-orthographic image into the low-res render target. This makes
        /// zoom behave like scaling a rendered image (PixZel-style) instead of re-quantizing
        /// geometry on each step.
        /// </summary>
        private bool TryRenderExactCardinalPixelPreview(
            VoxelVolume volume,
            int renderW,
            int renderH,
            byte[] buffer,
            uint clearColor,
            VoxelRenderer.RenderOptions opts)
        {
            if (volume == null) return false;
            if (_camera.IsAnimating) return false;

            string? snap = _camera.CurrentSnapName;
            if (string.IsNullOrWhiteSpace(snap)) return false;

            if (!IsCardinalOrthoSnap(snap))
                return false;

            if (!TryGetCachedCardinalPixelPreviewImage(volume, snap, out var img, out var visibleFace))
                return false;

            FillClear(buffer, renderW * renderH);

            int size = img.Width;
            int startX = (renderW - size) / 2;
            int startY = (renderH - size) / 2;

            bool[]? objectMask = opts.DrawOutline ? new bool[renderW * renderH] : null;

            for (int y = 0; y < img.Height; y++)
            {
                int dy = startY + y;
                if ((uint)dy >= (uint)renderH) continue;

                for (int x = 0; x < img.Width; x++)
                {
                    int dx = startX + x;
                    if ((uint)dx >= (uint)renderW) continue;

                    var c = img.GetPixel(x, y);
                    if (c.A == 0) continue;

                    var lit = ApplyFaceLighting(c, visibleFace, opts);
                    int bi = (dy * renderW + dx) * 4;
                    buffer[bi] = lit.B;
                    buffer[bi + 1] = lit.G;
                    buffer[bi + 2] = lit.R;
                    buffer[bi + 3] = lit.A;

                    if (objectMask != null)
                        objectMask[dy * renderW + dx] = true;
                }
            }

            if (objectMask != null && opts.OutlineSize > 0)
            {
                ApplyExteriorSilhouetteOutlineToMask(
                    buffer, objectMask, renderW, renderH,
                    opts.OutlineColor, opts.OutlineSize);
            }

            return true;
        }

        private bool TryGetCachedCardinalPixelPreviewImage(
            VoxelVolume volume,
            string snapName,
            out ImageData image,
            out Face visibleFace)
        {
            _cachedCardinalPixelPreviewImages ??= new Dictionary<string, (ImageData Image, Face Face)>(StringComparer.OrdinalIgnoreCase);

            if (_cachedCardinalPixelPreviewImages.TryGetValue(snapName, out var cached))
            {
                image = cached.Image;
                visibleFace = cached.Face;
                return true;
            }

            if (!TryBuildCardinalPixelPreviewImage(volume, snapName, out image, out visibleFace))
                return false;

            _cachedCardinalPixelPreviewImages[snapName] = (image, visibleFace);
            return true;
        }

        private static bool IsCardinalOrthoSnap(string snapName)
        {
            return snapName.Equals("front", StringComparison.OrdinalIgnoreCase) ||
                   snapName.Equals("back", StringComparison.OrdinalIgnoreCase) ||
                   snapName.Equals("left", StringComparison.OrdinalIgnoreCase) ||
                   snapName.Equals("right", StringComparison.OrdinalIgnoreCase) ||
                   snapName.Equals("top", StringComparison.OrdinalIgnoreCase) ||
                   snapName.Equals("bottom", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryBuildCardinalPixelPreviewImage(
            VoxelVolume volume,
            string snapName,
            out ImageData image,
            out Face visibleFace)
        {
            int size = volume.Size;
            image = new ImageData(size, size);
            visibleFace = Face.Front;

            // Reuse the left view only. Other cardinal views need explicit mappings here
            // because UI-facing face names/orientations differ from OrthoVoxelBuilder's
            // internal projection conventions.
            if (snapName.Equals("left", StringComparison.OrdinalIgnoreCase))
            {
                var p = OrthoVoxelBuilder.ProjectToOrtho(volume);
                if (snapName.Equals("left", StringComparison.OrdinalIgnoreCase))
                {
                    image = p.Side;
                    visibleFace = Face.Left;
                    return true;
                }
            }

            // Opposite cardinal views are generated explicitly so they match the volume colors
            // (6-face overrides included) without going through triangle rasterization.
            image = new ImageData(size, size);

            static int Flip(int v, int n) => n - 1 - v;

            if (snapName.Equals("front", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Front;
                for (int y = 0; y < size; y++)
                {
                    int yFlip = Flip(y, size);
                    for (int x = 0; x < size; x++)
                    {
                        for (int z = 0; z < size; z++) // camera at -Z → nearest is lowest z
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = Flip(x, size);
                            int row = yFlip;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Front));
                            break;
                        }
                    }
                }
                return true;
            }

            if (snapName.Equals("back", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Back;
                for (int y = 0; y < size; y++)
                {
                    int yFlip = Flip(y, size);
                    for (int x = 0; x < size; x++)
                    {
                        for (int z = size - 1; z >= 0; z--) // camera at +Z → nearest is highest z
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = x;
                            int row = yFlip;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Back));
                            break;
                        }
                    }
                }
                return true;
            }

            if (snapName.Equals("right", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Right;
                for (int y = 0; y < size; y++)
                {
                    int yFlip = Flip(y, size);
                    for (int z = 0; z < size; z++)
                    {
                        for (int x = size - 1; x >= 0; x--) // camera at +X → nearest is highest x
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = Flip(z, size);
                            int row = yFlip;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Right));
                            break;
                        }
                    }
                }
                return true;
            }

            if (snapName.Equals("top", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Top;
                for (int z = 0; z < size; z++)
                {
                    int zFlip = Flip(z, size);
                    for (int x = 0; x < size; x++)
                    {
                        for (int y = size - 1; y >= 0; y--) // camera at +Y → nearest is highest y
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = Flip(x, size);
                            int row = zFlip;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Top));
                            break;
                        }
                    }
                }
                return true;
            }

            if (snapName.Equals("bottom", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Bottom;
                for (int z = 0; z < size; z++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        for (int y = 0; y < size; y++) // camera at -Y → nearest is lowest y
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = Flip(x, size);
                            int row = z;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Bottom));
                            break;
                        }
                    }
                }
                return true;
            }

            return false;
        }

        private static Rgba32 ApplyFaceLighting(Rgba32 c, Face face, VoxelRenderer.RenderOptions opts)
        {
            float mul = face switch
            {
                Face.Top => opts.LightTop,
                Face.Left or Face.Right => opts.LightSide,
                _ => opts.LightFront,
            };

            return new Rgba32(
                ClampToByte(c.R * mul),
                ClampToByte(c.G * mul),
                ClampToByte(c.B * mul),
                c.A);
        }

        private static byte ClampToByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 255f) return 255;
            return (byte)MathF.Round(v);
        }

        private static void ApplyExteriorSilhouetteOutlineToMask(
            byte[] buffer,
            bool[] objectMask,
            int width,
            int height,
            uint outlineColor,
            int outlineSize)
        {
            if (outlineSize <= 0) return;
            int pixelCount = width * height;
            if (objectMask.Length != pixelCount) return;

            bool any = false;
            for (int i = 0; i < pixelCount; i++)
            {
                if (objectMask[i]) { any = true; break; }
            }
            if (!any) return;

            var exterior = new bool[pixelCount];
            var flood = new int[pixelCount];
            int fh = 0, ft = 0;

            void TryEnqueueExterior(int x, int y)
            {
                if ((uint)x >= (uint)width || (uint)y >= (uint)height) return;
                int idx = y * width + x;
                if (objectMask[idx] || exterior[idx]) return;
                exterior[idx] = true;
                flood[ft++] = idx;
            }

            for (int x = 0; x < width; x++)
            {
                TryEnqueueExterior(x, 0);
                TryEnqueueExterior(x, height - 1);
            }
            for (int y = 1; y < height - 1; y++)
            {
                TryEnqueueExterior(0, y);
                TryEnqueueExterior(width - 1, y);
            }

            while (fh < ft)
            {
                int idx = flood[fh++];
                int x = idx % width;
                int y = idx / width;

                TryEnqueueExterior(x - 1, y);
                TryEnqueueExterior(x + 1, y);
                TryEnqueueExterior(x, y - 1);
                TryEnqueueExterior(x, y + 1);
            }

            int radius = Math.Max(1, outlineSize);
            var dist = new int[pixelCount];
            var q = new int[pixelCount];
            int qh = 0, qt = 0;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    if (!exterior[idx]) continue;

                    bool nearObject = false;
                    for (int ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1) && !nearObject; ny++)
                    {
                        int nrow = ny * width;
                        for (int nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
                        {
                            if (objectMask[nrow + nx])
                            {
                                nearObject = true;
                                break;
                            }
                        }
                    }

                    if (!nearObject) continue;
                    dist[idx] = 1;
                    q[qt++] = idx;
                }
            }

            while (qh < qt)
            {
                int idx = q[qh++];
                int d = dist[idx];
                if (d >= radius) continue;

                int x = idx % width;
                int y = idx / width;
                for (int ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
                {
                    int nrow = ny * width;
                    for (int nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
                    {
                        int ni = nrow + nx;
                        if (!exterior[ni] || dist[ni] != 0) continue;
                        dist[ni] = d + 1;
                        q[qt++] = ni;
                    }
                }
            }

            byte b = (byte)(outlineColor & 0xFF);
            byte g = (byte)((outlineColor >> 8) & 0xFF);
            byte r = (byte)((outlineColor >> 16) & 0xFF);
            byte a = (byte)((outlineColor >> 24) & 0xFF);

            for (int i = 0; i < qt; i++)
            {
                int idx = q[i];
                if (objectMask[idx]) continue;
                int bi = idx * 4;
                buffer[bi] = b;
                buffer[bi + 1] = g;
                buffer[bi + 2] = r;
                buffer[bi + 3] = a;
            }
        }

        private static void DrawPixelPreviewBackdropGrid2D(
            byte[] buffer,
            int width,
            int height,
            int volumeSize,
            int screenPixelSize,
            int marginVoxels,
            uint minorColor,
            uint majorColor,
            int majorEvery)
        {
            if (buffer == null || buffer.Length < width * height * 4) return;
            if (width <= 0 || height <= 0 || volumeSize <= 0 || screenPixelSize <= 0) return;

            int margin = Math.Max(5, marginVoxels);
            int modelPx = volumeSize * screenPixelSize;

            int modelLeft = (width - modelPx) / 2;
            int modelTop = (height - modelPx) / 2;

            int gridLeft = modelLeft - margin * screenPixelSize;
            int gridTop = modelTop - margin * screenPixelSize;
            int gridCells = volumeSize + margin * 2;
            int gridPx = gridCells * screenPixelSize;

            int gridRight = gridLeft + gridPx;
            int gridBottom = gridTop + gridPx;

            majorEvery = Math.Max(0, majorEvery);

            for (int gx = 0; gx <= gridCells; gx++)
            {
                int x = gridLeft + (gx * screenPixelSize);
                if ((uint)x >= (uint)width) continue;

                bool major = majorEvery > 0 && ((gx - margin) % majorEvery == 0);
                uint color = major ? majorColor : minorColor;
                int y0 = Math.Max(0, gridTop);
                int y1 = Math.Min(height - 1, gridBottom);
                DrawVerticalLine(buffer, width, height, x, y0, y1, color);
            }

            for (int gy = 0; gy <= gridCells; gy++)
            {
                int y = gridTop + (gy * screenPixelSize);
                if ((uint)y >= (uint)height) continue;

                bool major = majorEvery > 0 && ((gy - margin) % majorEvery == 0);
                uint color = major ? majorColor : minorColor;
                int x0 = Math.Max(0, gridLeft);
                int x1 = Math.Min(width - 1, gridRight);
                DrawHorizontalLine(buffer, width, height, y, x0, x1, color);
            }
        }

        private static void DrawVerticalLine(byte[] buffer, int width, int height, int x, int y0, int y1, uint bgra)
        {
            if ((uint)x >= (uint)width) return;
            if (y1 < y0) return;

            byte b = (byte)(bgra & 0xFF);
            byte g = (byte)((bgra >> 8) & 0xFF);
            byte r = (byte)((bgra >> 16) & 0xFF);
            byte a = (byte)((bgra >> 24) & 0xFF);

            y0 = Math.Max(0, y0);
            y1 = Math.Min(height - 1, y1);
            for (int y = y0; y <= y1; y++)
            {
                int bi = (y * width + x) * 4;
                buffer[bi] = b;
                buffer[bi + 1] = g;
                buffer[bi + 2] = r;
                buffer[bi + 3] = a;
            }
        }

        private static void DrawHorizontalLine(byte[] buffer, int width, int height, int y, int x0, int x1, uint bgra)
        {
            if ((uint)y >= (uint)height) return;
            if (x1 < x0) return;

            byte b = (byte)(bgra & 0xFF);
            byte g = (byte)((bgra >> 8) & 0xFF);
            byte r = (byte)((bgra >> 16) & 0xFF);
            byte a = (byte)((bgra >> 24) & 0xFF);

            x0 = Math.Max(0, x0);
            x1 = Math.Min(width - 1, x1);
            int row = y * width;
            for (int x = x0; x <= x1; x++)
            {
                int bi = (row + x) * 4;
                buffer[bi] = b;
                buffer[bi + 1] = g;
                buffer[bi + 2] = r;
                buffer[bi + 3] = a;
            }
        }

        private void UpdateCameraStatsText(bool pixelMode, int screenPixelSize, int renderW, int renderH)
        {
            if (CameraStatsText == null) return;

            float pitchDeg = _camera.Pitch * (180f / MathF.PI);
            float yawDeg = _camera.Yaw * (180f / MathF.PI);
            if (yawDeg < 0f) yawDeg += 360f;
            string snap = _camera.CurrentSnapName ?? "custom";
            float voxPx = 0f;

            if (pixelMode && _lastVolume != null)
            {
                int pixelBaseSize = GetPixelPreviewBaseSize();
                float effectiveZoom = (screenPixelSize * 100f) / Math.Max(1, pixelBaseSize);
                voxPx = screenPixelSize;
                CameraStatsText.Text =
                    $"p {pitchDeg,5:0.0}  y {yawDeg,5:0.0}  z {effectiveZoom,5:0.#}%  vpx {voxPx,4:0.#}  b {pixelBaseSize}  rt {renderW}x{renderH}  {snap}";
            }
            else
            {
                var fr = _camera.GetFrustum();
                voxPx = renderH / MathF.Max(1e-6f, fr.Height);
                CameraStatsText.Text =
                    $"p {pitchDeg,5:0.0}  y {yawDeg,5:0.0}  z {_camera.ZoomPercent,5:0.#}%  vpx {voxPx,4:0.#}  {snap}";
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // EXPORT
        // ════════════════════════════════════════════════════════════════════

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_renderBuffer == null || _lastVolume == null || _lastVolume.OccupiedCount == 0) return;

            try
            {
                var savePicker = new FileSavePicker();
                WinRT.Interop.InitializeWithWindow.Initialize(
                    savePicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                savePicker.SuggestedFileName = "voxel_preview";
                savePicker.FileTypeChoices.Add("PNG Image", new[] { ".png" });
                savePicker.DefaultFileExtension = ".png";

                var file = await savePicker.PickSaveFileAsync();
                if (file == null) return;

                // Render at current viewport size
                _camera.DisablePixelPerfectFrustum();
                _camera.ResizeViewport(_viewportWidth, _viewportHeight);
                var exportBuffer = new byte[_viewportWidth * _viewportHeight * 4];
                VoxelRenderer.Render(
                    _lastVolume, _camera,
                    _viewportWidth, _viewportHeight,
                    exportBuffer, ClearColor);

                // Encode to PNG via WriteableBitmap
                var bmp = new WriteableBitmap(_viewportWidth, _viewportHeight);
                using (var stream = bmp.PixelBuffer.AsStream())
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.Write(exportBuffer, 0, exportBuffer.Length);
                }

                // Save using the existing image export pipeline
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                    Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                    await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite));

                encoder.SetPixelData(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    (uint)_viewportWidth, (uint)_viewportHeight,
                    96, 96,
                    exportBuffer);

                await encoder.FlushAsync();

                LoggingService.Info("Voxel exported to {Path}", file.Path);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Voxel export failed", ex);
            }
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
