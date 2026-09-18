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
    /// <summary>
    /// Reusable voxel workspace control that renders a 3D voxel editor/preview.
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
    public sealed partial class VoxelWorkspaceControl : UserControl
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
            LightHandle,
        }

        private enum ToolPointerPhase
        {
            Pressed,
            Moved,
            Released,
        }

        private sealed record ViewportRenderOverrides
        {
            public bool? DrawOutline { get; init; }
            public bool? DrawBackdropGrid { get; init; }
            public bool? DrawBackdropProjectionTiles { get; init; }
            public bool? DrawSurfaceVoxelGrid { get; init; }
            public bool IncludeSelectionOverlay { get; init; } = true;
            public uint? ClearColor { get; init; }
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
            public bool DrawSurfaceVoxelGrid;
            public uint SurfaceVoxelGridColor;
            public bool LightingEnabled;
            public float LightPosX;
            public float LightPosY;
            public float LightPosZ;
            public uint LightColor;
            public uint ShadowColor;
            public float ShadowStrength;
            public float LightIntensity;
            public float AmbientIntensity;
            public float LightFalloff;
            public bool LightCastShadows;
        }

        private sealed class VoxelWorkspaceHistorySnapshot
        {
            public required VoxelModelDocumentState Model;
            public required Int3[] Selection;
            public required VoxelFaceColorOverride[] FaceOverrides;
        }

        private readonly CanvasDocument _document;
        private readonly PaletteService? _palette;
        private readonly VoxelToolState _voxelToolState;
        private readonly VoxelEditEngine _editEngine;
        private bool _suppressLightingUiSync;
        private OrbitCamera _camera;
        private WorkspaceVoxelToolContext? _toolContext;
        private IVoxelToolHandler? _activeVoxelToolHandler;
        private string? _activeVoxelToolHandlerToolId;

        // Drag tracking (pixel deltas for orbit)
        private Windows.Foundation.Point _lastPointerPos;

        // Render state
        private VoxelVolume? _lastVolume;
        private byte[]? _renderBuffer;
        private byte[]? _pixelPreviewAaBuffer;
        private byte[]? _displayBuffer;
        private WriteableBitmap? _viewportBitmap;
        private int _viewportWidth = 512;
        private int _viewportHeight = 512;
        private float _viewportRasterScale = 1f;
        private int _lastAutoLightingPresetVolumeSize = -1;
        private bool _hasOccupiedBounds;
        private Vector3 _occupiedBoundsMin;
        private Vector3 _occupiedBoundsMax;
        private Vector2 _axisXEnd = new(66f, 42f);
        private Vector2 _axisYEnd = new(42f, 18f);
        private Vector2 _axisZEnd = new(24f, 56f);
        private Dictionary<string, (ImageData Image, Face Face)>? _cachedCardinalPixelPreviewImages;
        private PixelPreviewSpriteCache? _pixelPreviewSpriteCache;
        private ImageData? _backdropFrontProjectionImage;
        private ImageData? _backdropBackProjectionImage;
        private ImageData? _backdropLeftProjectionImage;
        private ImageData? _backdropRightProjectionImage;
        private ImageData? _backdropTopProjectionImage;
        private ImageData? _backdropBottomProjectionImage;
        private bool _suppressVoxelUiEvents = true;
        private bool _suppressEditEngineModelSync;
        private PointerDragMode _pointerDragMode = PointerDragMode.None;
        private PickedVoxelFace? _lastStrokePaintFace;
        private Vector2 _lightHandleHostDip = new(float.NaN, float.NaN);
        private float _lightDragCameraDepth;
        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _lightingHistoryDebounceTimer;
        private VoxelLightingSettings? _pendingLightingHistoryBefore;
        private VoxelLightingSettings? _pendingLightingHistoryAfter;
        private bool _suppressLightingHistoryRecording;
        private int _lastKnownTileSetCount = -1;
        private int _lastImageExportScale = 1;
        private bool _lastImageExportTransparentBackground;
        private bool _lastImageExportIncludeOutline;
        private bool _lastImageExportIncludeBackdropCage = true;
        private bool _lastImageExportIncludeProjectionTiles = true;
        private bool _lastImageExportIncludeModelGrid;
        private bool _lastImageExportTrimTransparentBounds = true;
        private int _lastImageExportTrimPadding = 1;
        private bool _lastImageExportBatchViews;
        private bool _lastImageExportBatchCardinalViews = true;
        private bool _lastImageExportBatchDirectionalViews = true;
        private ModelExportFormat _lastModelExportFormat = ModelExportFormat.Obj;
        private ModelMeshMode _lastModelExportMeshMode = ModelMeshMode.MergeCoplanar;
        private ModelAxisPreset _lastModelExportAxisPreset = ModelAxisPreset.PixlPunkt;
        private float _lastModelExportUnitScale = 1f;
        private ModelPivotPreset _lastModelExportPivotPreset = ModelPivotPreset.Center;
        private bool _lastModelExportGlbDoubleSided = true;

        private static readonly string[] BatchCardinalViewNames = { "front", "back", "left", "right", "top", "bottom" };
        private static readonly string[] BatchDirectionalViewNames = { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };

        // Background color (dark gray, BGRA packed)
        private const uint ClearColor = 0xFF1E1E1E;
        private const float LightHandleHitRadiusDip = 16f;
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        private sealed class WorkspaceVoxelToolContext : IVoxelToolContext
        {
            private readonly VoxelWorkspaceControl _owner;
            private readonly WorkspaceVoxelDocumentInfo _documentInfo;
            private readonly WorkspaceVoxelModelView _modelView;
            private readonly WorkspaceVoxelSelectionView _selectionView;

            public WorkspaceVoxelToolContext(VoxelWorkspaceControl owner)
            {
                _owner = owner;
                _documentInfo = new WorkspaceVoxelDocumentInfo(owner);
                _modelView = new WorkspaceVoxelModelView(owner);
                _selectionView = new WorkspaceVoxelSelectionView(owner);
            }

            public CanvasDocument Document => _owner._document;

            public IVoxelDocumentReadOnly DocumentInfo => _documentInfo;

            public IVoxelModelReadOnly Model => _modelView;

            public IVoxelSelectionReadOnly Selection => _selectionView;

            public bool TryPickFace(float screenX, float screenY, out VoxelFaceHit hit)
                => _owner.TryPickFaceFromScreen(screenX, screenY, out hit);

            public bool TryPickVoxel(float screenX, float screenY, out VoxelVoxelHit hit)
                => _owner.TryPickVoxelFromScreen(screenX, screenY, out hit);

            public void SetFaceColor(int x, int y, int z, VoxelFace face, uint bgra)
                => _owner.SetFaceColorFromTool(x, y, z, face, bgra);

            public void ClearFaceColorOverride(int x, int y, int z, VoxelFace face)
                => _owner.ClearFaceColorOverrideFromTool(x, y, z, face);

            public void SetVoxel(int x, int y, int z, uint colorBgra)
                => _owner.SetVoxelFromTool(x, y, z, colorBgra);

            public void ClearVoxel(int x, int y, int z)
                => _owner.ClearVoxelFromTool(x, y, z);

            public void MoveSelection(Int3 delta, VoxelMoveMode mode = VoxelMoveMode.CutPaste)
                => _owner.MoveSelectionFromTool(delta, mode);

            public void ClearSelection()
                => _owner.ClearSelectionFromTool();

            public void SetSelection(IEnumerable<Int3> voxels, VoxelSelectionMode mode)
                => _owner.SetSelectionFromTool(voxels, mode);

            public void ExpandSelectionConnected()
                => _owner.ExpandSelectionFromTool();

            public uint Foreground => _owner._palette?.Foreground ?? 0xFF000000;

            public uint Background => _owner._palette?.Background ?? 0xFFFFFFFF;

            public void SetForeground(uint bgra)
                => _owner._palette?.SetForeground(bgra);

            public void SetBackground(uint bgra)
                => _owner._palette?.SetBackground(bgra);

            public VoxelViewportState ViewportState => _owner.BuildViewportState();

            public void RequestRedraw()
                => _owner.RenderViewport();

            public void RequestRebuildRenderCache()
            {
                _owner._cachedCardinalPixelPreviewImages = null;
                _owner._pixelPreviewSpriteCache = null;
                _owner.RenderViewport();
            }

            public void BeginHistoryTransaction(string name)
                => _owner._editEngine.BeginHistoryTransaction(name);

            public void CommitHistoryTransaction()
                => _owner._editEngine.CommitHistoryTransaction();

            public void CancelHistoryTransaction()
                => _owner._editEngine.CancelHistoryTransaction();

            public VoxelLightingSettings LightingSettings
                => _owner.GetLightingSettingsSnapshot();

            public void UpdateLightingSettings(Action<VoxelLightingSettings> edit)
                => _owner.UpdateLightingSettingsFromTool(edit);
        }

        private sealed class WorkspaceVoxelDocumentInfo : IVoxelDocumentReadOnly
        {
            private readonly VoxelWorkspaceControl _owner;

            public WorkspaceVoxelDocumentInfo(VoxelWorkspaceControl owner) => _owner = owner;

            public string Name => _owner._document.Name ?? "Untitled";
        }

        private sealed class WorkspaceVoxelModelView : IVoxelModelReadOnly
        {
            private readonly VoxelWorkspaceControl _owner;

            public WorkspaceVoxelModelView(VoxelWorkspaceControl owner) => _owner = owner;

            public int Width => _owner._document.VoxelModel.Width;

            public int Height => _owner._document.VoxelModel.Height;

            public int Depth => _owner._document.VoxelModel.Depth;

            public bool IsOccupied(int x, int y, int z)
                => _owner._document.VoxelModel.IsOccupied(x, y, z);

            public uint GetFaceColor(int x, int y, int z, VoxelFace face)
                => _owner._document.VoxelModel.GetFaceColorBgra(x, y, z, ToCoreFace(face));
        }

        private sealed class WorkspaceVoxelSelectionView : IVoxelSelectionReadOnly
        {
            private readonly VoxelWorkspaceControl _owner;

            public WorkspaceVoxelSelectionView(VoxelWorkspaceControl owner) => _owner = owner;

            public int Count => _owner._editEngine.Selection.Count;

            public bool Contains(Int3 position)
                => _owner._editEngine.Selection.Contains(position);

            public IEnumerable<Int3> Enumerate()
                => _owner._editEngine.Selection.Enumerate();
        }

        public CanvasDocument Document => _document;

        public PaletteService? Palette => _palette;

        public VoxelToolState VoxelTools => _voxelToolState;

        public VoxelWorkspaceControl(CanvasDocument document, PaletteService? palette = null)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _palette = palette;
            _voxelToolState = new VoxelToolState();
            _editEngine = new VoxelEditEngine(_document.VoxelModel, _document.History);

            // XAML checkbox/NumberBox events can fire during InitializeComponent().
            // Create the camera first so early RenderViewport() calls are safe.
            int tileSize = Math.Max(document.TileSet?.TileWidth ?? 16, document.TileSet?.TileHeight ?? 16);
            _camera = new OrbitCamera(tileSize);

            InitializeComponent();
            InitializeLightingHistoryDebounce();
            InitializeVoxelToolScaffold();
            WireEditEngine();
            HookDocumentTileSetChanges();

            PopulateTilePickers(preserveSelection: false);
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

            Unloaded += (_, __) =>
            {
                FlushPendingLightingHistory();
                CancelActiveVoxelToolHandler();
                UnhookDocumentTileSetChanges();
            };

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
            UpdatePixelPreviewAaStrengthLabel();
            UpdateVoxelSelectionStatusText();

            if (_document.VoxelPreviewState.HasState)
                BuildAndRender();
            else if (_document.VoxelModel.TryCreateVoxelVolume(out var modelVolume) && modelVolume != null)
            {
                _lastVolume = modelVolume;
                RefreshOccupiedBoundsCache();
                _camera.ConfigureForVolume(modelVolume.Size);
                SetExportButtonsEnabled(modelVolume.OccupiedCount > 0);
                RenderViewport();
            }
            else
                RenderViewport();
        }

        private void WireEditEngine()
        {
            _editEngine.ModelChanged += OnEditEngineModelChanged;
            _editEngine.SelectionChanged += OnEditEngineSelectionChanged;
        }

        private void HookDocumentTileSetChanges()
        {
            _document.TileSetChanged -= OnDocumentTileSetChanged;
            _document.TileSetChanged += OnDocumentTileSetChanged;
            _lastKnownTileSetCount = _document.TileSet?.Count ?? 0;
        }

        private void UnhookDocumentTileSetChanges()
        {
            _document.TileSetChanged -= OnDocumentTileSetChanged;
        }

        private void OnDocumentTileSetChanged()
        {
            if (DispatcherQueue == null)
            {
                HandleDocumentTileSetChanged();
                return;
            }

            _ = DispatcherQueue.TryEnqueue(HandleDocumentTileSetChanged);
        }

        private void HandleDocumentTileSetChanged()
        {
            int tileCount = _document.TileSet?.Count ?? 0;
            // We only auto-reload on count changes (add/remove/clear). Pixel edits keep IDs stable and
            // are picked up by Build/Rebuild paths without forcing picker repopulation every brush stroke.
            if (tileCount == _lastKnownTileSetCount)
                return;

            _lastKnownTileSetCount = tileCount;
            ReloadTilesFromDocument(rebuildModel: _lastVolume != null);
        }

        private void InitializeVoxelToolScaffold()
        {
            if (ViewportVoxelToolRail != null)
            {
                ViewportVoxelToolRail.Orientation = Orientation.Horizontal;
                ViewportVoxelToolRail.ShowLabels = false;
                ViewportVoxelToolRail.ToolState = _voxelToolState;
            }

            _voxelToolState.ActiveToolChanged += OnVoxelToolChanged;
            EnsureActiveVoxelToolHandler();
        }

        private void InitializeLightingHistoryDebounce()
        {
            if (DispatcherQueue == null)
                return;

            _lightingHistoryDebounceTimer = DispatcherQueue.CreateTimer();
            _lightingHistoryDebounceTimer.Interval = TimeSpan.FromMilliseconds(180);
            _lightingHistoryDebounceTimer.IsRepeating = false;
            _lightingHistoryDebounceTimer.Tick += OnLightingHistoryDebounceTick;
        }

        private void OnLightingHistoryDebounceTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
            => FlushPendingLightingHistory();

        /// <summary>
        /// Pushes lighting state values from the document into the permanent lighting controls.
        /// </summary>
        private void SyncLightingControlsFromDocument()
        {
            var ws = _document.VoxelWorkspace;
            _suppressLightingUiSync = true;
            try
            {
                if (LightingEnabledCheckBox != null)
                    LightingEnabledCheckBox.IsChecked = ws.LightingEnabled;
                if (LightingColorSwatch != null)
                    LightingColorSwatch.Color = ws.LightColorBgra;
                if (LightingShadowColorSwatch != null)
                    LightingShadowColorSwatch.Color = ws.ShadowColorBgra;

                SetSliderAndBox(LightingShadowStrengthSlider, LightingShadowStrengthBox, ws.LightShadowStrength, 0f, 1f);
                SetSliderAndBox(LightingIntensitySlider, LightingIntensityBox, ws.LightIntensity, 0f, 8f);
                SetSliderAndBox(LightingFalloffSlider, LightingFalloffBox, ws.LightFalloff, 0f, 2f);

                if (LightingPosXBox != null)
                    LightingPosXBox.Value = ws.LightPosX;
                if (LightingPosYBox != null)
                    LightingPosYBox.Value = ws.LightPosY;
                if (LightingPosZBox != null)
                    LightingPosZBox.Value = ws.LightPosZ;
                if (LightingCastShadowsCheckBox != null)
                    LightingCastShadowsCheckBox.IsChecked = ws.LightCastShadows;
            }
            finally
            {
                _suppressLightingUiSync = false;
            }
        }

        private static void SetSliderAndBox(Slider? slider, NumberBox? box, float value, float min, float max)
        {
            double clamped = Math.Clamp(value, min, max);
            if (slider != null && !NearlyEqual(slider.Value, clamped))
                slider.Value = clamped;
            if (box != null && !NearlyEqual(box.Value, clamped))
                box.Value = clamped;
        }

        private VoxelLightingSettings BuildLightingSnapshotFromControls()
        {
            var ws = _document.VoxelWorkspace;
            return new VoxelLightingSettings
            {
                Enabled = LightingEnabledCheckBox?.IsChecked == true,
                Position = new Vector3(
                    (float)(LightingPosXBox?.Value ?? ws.LightPosX),
                    (float)(LightingPosYBox?.Value ?? ws.LightPosY),
                    (float)(LightingPosZBox?.Value ?? ws.LightPosZ)),
                LightColorBgra = LightingColorSwatch?.Color ?? ws.LightColorBgra,
                ShadowColorBgra = LightingShadowColorSwatch?.Color ?? ws.ShadowColorBgra,
                ShadowStrength = Math.Clamp((float)(LightingShadowStrengthSlider?.Value ?? ws.LightShadowStrength), 0f, 1f),
                Intensity = Math.Clamp((float)(LightingIntensitySlider?.Value ?? ws.LightIntensity), 0f, 8f),
                Falloff = Math.Clamp((float)(LightingFalloffSlider?.Value ?? ws.LightFalloff), 0f, 2f),
                CastShadows = LightingCastShadowsCheckBox?.IsChecked == true,
            };
        }

        private void ApplyLightingFromControls()
        {
            if (_suppressVoxelUiEvents || _suppressLightingUiSync)
                return;

            var before = GetLightingSettingsSnapshot();
            bool wasEnabled = before.Enabled;
            var next = BuildLightingSnapshotFromControls();

            // First enable should spawn near the model if current position is out of practical range.
            if (!wasEnabled && next.Enabled &&
                TryGetRecommendedLightSpawnPosition(out var spawnPosition) &&
                !IsLightPositionUsableForCurrentVolume(next.Position))
            {
                next.Position = spawnPosition;
            }

            ApplyLightingSnapshotToWorkspace(next, before);
            SyncLightingControlsFromDocument();
        }

        private void ApplyLightingSnapshotToWorkspace(VoxelLightingSettings after, VoxelLightingSettings before)
        {
            var ws = _document.VoxelWorkspace;
            // This is the canonical write path for lighting state. Keep all lighting mutations routed
            // through here so render invalidation + history coalescing stay consistent.
            ws.HasState = true;
            ws.LightingEnabled = after.Enabled;
            ws.LightPosX = after.Position.X;
            ws.LightPosY = after.Position.Y;
            ws.LightPosZ = after.Position.Z;
            ws.LightColorBgra = after.LightColorBgra;
            ws.ShadowColorBgra = after.ShadowColorBgra;
            ws.LightShadowStrength = after.ShadowStrength;
            ws.LightIntensity = after.Intensity;
            ws.LightFalloff = after.Falloff;
            ws.LightCastShadows = after.CastShadows;

            _pixelPreviewSpriteCache = null;
            UpdateLightingQuickActionsState();

            if (!_suppressLightingHistoryRecording)
                QueueLightingHistoryChange(before, after);

            RenderViewport();
        }

        private void QueueLightingHistoryChange(VoxelLightingSettings before, VoxelLightingSettings after)
        {
            if (IsLightingSettingsEqual(before, after))
                return;

            // Debounce all rapid lighting edits (sliders/drag) into a single undo unit.
            _pendingLightingHistoryBefore ??= CloneLightingSettings(before);
            _pendingLightingHistoryAfter = CloneLightingSettings(after);

            if (_lightingHistoryDebounceTimer != null)
            {
                _lightingHistoryDebounceTimer.Stop();
                _lightingHistoryDebounceTimer.Start();
            }
        }

        private void FlushPendingLightingHistory()
        {
            if (_pendingLightingHistoryBefore == null || _pendingLightingHistoryAfter == null)
                return;

            var before = _pendingLightingHistoryBefore;
            var after = _pendingLightingHistoryAfter;
            _pendingLightingHistoryBefore = null;
            _pendingLightingHistoryAfter = null;
            _lightingHistoryDebounceTimer?.Stop();

            if (IsLightingSettingsEqual(before, after))
                return;

            _editEngine.History.Push(new VoxelCommandHistory.DelegateCommand(
                "Adjust lighting",
                () =>
                {
                    _suppressLightingHistoryRecording = true;
                    try
                    {
                        ApplyLightingSnapshotToWorkspace(before, before);
                        SyncLightingControlsFromDocument();
                    }
                    finally
                    {
                        _suppressLightingHistoryRecording = false;
                    }
                },
                () =>
                {
                    _suppressLightingHistoryRecording = true;
                    try
                    {
                        ApplyLightingSnapshotToWorkspace(after, after);
                        SyncLightingControlsFromDocument();
                    }
                    finally
                    {
                        _suppressLightingHistoryRecording = false;
                    }
                }));
        }

        private static VoxelLightingSettings CloneLightingSettings(VoxelLightingSettings src)
            => new()
            {
                Enabled = src.Enabled,
                Position = src.Position,
                LightColorBgra = src.LightColorBgra,
                ShadowColorBgra = src.ShadowColorBgra,
                ShadowStrength = src.ShadowStrength,
                Intensity = src.Intensity,
                Falloff = src.Falloff,
                CastShadows = src.CastShadows,
            };

        private static bool IsLightingSettingsEqual(VoxelLightingSettings a, VoxelLightingSettings b)
            => a.Enabled == b.Enabled &&
               NearlyEqual(a.Position.X, b.Position.X) &&
               NearlyEqual(a.Position.Y, b.Position.Y) &&
               NearlyEqual(a.Position.Z, b.Position.Z) &&
               a.LightColorBgra == b.LightColorBgra &&
               a.ShadowColorBgra == b.ShadowColorBgra &&
               NearlyEqual(a.ShadowStrength, b.ShadowStrength) &&
               NearlyEqual(a.Intensity, b.Intensity) &&
               NearlyEqual(a.Falloff, b.Falloff) &&
               a.CastShadows == b.CastShadows;

        private void OnEditEngineModelChanged()
        {
            if (_suppressEditEngineModelSync)
                return;

            _cachedCardinalPixelPreviewImages = null;
            _pixelPreviewSpriteCache = null;
            SyncPreviewVolumeFromCanonicalModel();
            SetExportButtonsEnabled(_lastVolume != null && _lastVolume.OccupiedCount > 0);
            RenderViewport();
        }

        private void OnEditEngineSelectionChanged()
        {
            UpdateVoxelSelectionStatusText();
            RenderViewport();
        }

        private void SyncPreviewVolumeFromCanonicalModel()
        {
            if (_document.VoxelModel.TryCreateVoxelVolume(out var volume) && volume != null)
            {
                bool sizeChanged = _lastVolume == null || _lastVolume.Size != volume.Size;
                _lastVolume = volume;
                RefreshBackdropProjectionImagesFromUi();
                RefreshOccupiedBoundsCache();
                if (sizeChanged)
                    _camera.ConfigureForVolume(volume.Size);
            }
            else
            {
                _lastVolume = null;
                _backdropFrontProjectionImage = null;
                _backdropBackProjectionImage = null;
                _backdropLeftProjectionImage = null;
                _backdropRightProjectionImage = null;
                _backdropTopProjectionImage = null;
                _backdropBottomProjectionImage = null;
                RefreshOccupiedBoundsCache();
                _lastAutoLightingPresetVolumeSize = -1;
            }

            _cachedCardinalPixelPreviewImages = null;
            _pixelPreviewSpriteCache = null;
        }

        private void OnVoxelToolChanged(string? toolId)
        {
            if (!string.Equals(toolId, VoxelToolIds.Lighting, StringComparison.Ordinal))
                FlushPendingLightingHistory();

            EnsureActiveVoxelToolHandler();
            UpdateVoxelSelectionStatusText();
        }

        private void EnsureActiveVoxelToolHandler()
        {
            string? toolId = _voxelToolState.ActiveToolId;
            if (string.IsNullOrWhiteSpace(toolId))
            {
                CancelActiveVoxelToolHandler();
                return;
            }

            if (string.Equals(toolId, _activeVoxelToolHandlerToolId, StringComparison.Ordinal) &&
                _activeVoxelToolHandler != null)
            {
                return;
            }

            CancelActiveVoxelToolHandler();
            var registration = _voxelToolState.ActiveRegistration;
            if (registration == null)
                return;

            try
            {
                _toolContext ??= new WorkspaceVoxelToolContext(this);
                _activeVoxelToolHandler = registration.CreateHandler(_toolContext);
                _activeVoxelToolHandlerToolId = toolId;
            }
            catch (Exception ex)
            {
                _activeVoxelToolHandler = null;
                _activeVoxelToolHandlerToolId = null;
                LoggingService.Warning("Failed creating voxel tool handler id={ToolId}: {Error}", toolId, ex.Message);
            }
        }

        private void CancelActiveVoxelToolHandler()
        {
            if (_activeVoxelToolHandler != null)
            {
                try
                {
                    _activeVoxelToolHandler.Cancel();
                }
                catch (Exception ex)
                {
                    LoggingService.Warning("Voxel tool handler cancel failed id={ToolId}: {Error}",
                        _activeVoxelToolHandlerToolId ?? "(unknown)", ex.Message);
                }
            }

            _activeVoxelToolHandler = null;
            _activeVoxelToolHandlerToolId = null;
        }
    }
}
