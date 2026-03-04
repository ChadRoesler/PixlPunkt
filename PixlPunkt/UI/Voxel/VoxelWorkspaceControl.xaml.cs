using System;
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

        private delegate bool TryGetPlaneColor(int u, int v, out uint color);

        private enum ToolPointerPhase
        {
            Pressed,
            Moved,
            Released,
        }

        private enum ModelMeshMode
        {
            MergeCoplanar = 0,
            PerVoxel = 1,
        }

        private enum ModelAxisPreset
        {
            PixlPunkt = 0,
            BlenderZUp = 1,
        }

        private enum ModelExportFormat
        {
            Obj = 0,
            Glb = 1,
            Stl = 2,
            Vox = 3,
        }

        private enum ModelPivotPreset
        {
            Center = 0,
            BottomCenter = 1,
            Origin = 2,
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

        private readonly record struct VoxelImageExportOptions(
            int Scale,
            bool TransparentBackground,
            bool IncludeOutline,
            bool IncludeBackdropCage,
            bool IncludeProjectionTiles,
            bool IncludeModelGrid,
            bool TrimTransparentBounds,
            int TrimPadding,
            bool BatchExportViews,
            bool BatchIncludeCardinalViews,
            bool BatchIncludeDirectionalViews);

        private readonly record struct VoxelModelExportOptions(
            ModelExportFormat Format,
            ModelMeshMode MeshMode,
            ModelAxisPreset AxisPreset,
            float UnitScale,
            ModelPivotPreset PivotPreset,
            bool GlbDoubleSided);

        private readonly record struct VoxelExportPreset(
            VoxelImageExportOptions Image,
            VoxelModelExportOptions Model);

        private sealed class VoxelExportPresetFile
        {
            public int Version { get; set; } = 1;
            public VoxelImageExportPresetFile? Image { get; set; }
            public VoxelModelExportPresetFile? Model { get; set; }
        }

        private sealed class VoxelImageExportPresetFile
        {
            public int Scale { get; set; } = 1;
            public bool TransparentBackground { get; set; }
            public bool IncludeOutline { get; set; } = true;
            public bool IncludeBackdropCage { get; set; } = true;
            public bool IncludeProjectionTiles { get; set; } = true;
            public bool IncludeModelGrid { get; set; }
            public bool TrimTransparentBounds { get; set; }
            public int TrimPadding { get; set; } = 1;
            public bool BatchExportViews { get; set; }
            public bool BatchIncludeCardinalViews { get; set; } = true;
            public bool BatchIncludeDirectionalViews { get; set; } = true;
        }

        private sealed class VoxelModelExportPresetFile
        {
            public int Format { get; set; }
            public int MeshMode { get; set; }
            public int AxisPreset { get; set; }
            public float UnitScale { get; set; } = 1f;
            public int PivotPreset { get; set; }
            public bool GlbDoubleSided { get; set; } = true;
        }

        private readonly record struct ExportFaceQuad(
            Face Face,
            int X,
            int Y,
            int Z,
            int Width,
            int Height,
            uint ColorBgra);

        private readonly record struct ExportTriangle(
            Vector3 A,
            Vector3 B,
            Vector3 C,
            Vector3 Normal,
            uint ColorBgra);

        private readonly record struct TransformedVoxelBounds(
            int MinX,
            int MinY,
            int MinZ,
            int MaxX,
            int MaxY,
            int MaxZ)
        {
            public int SizeX => (MaxX - MinX) + 1;
            public int SizeY => (MaxY - MinY) + 1;
            public int SizeZ => (MaxZ - MinZ) + 1;
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
            _editEngine = new VoxelEditEngine(_document.VoxelModel);

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

        private void BackdropProjectionTiles_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void BackdropCageScale_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void SurfaceVoxelGrid_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        // ════════════════════════════════════════════════════════════════════
        // LIGHTING CONTROLS
        // ════════════════════════════════════════════════════════════════════

        private void LightingControls_Changed(object sender, RoutedEventArgs e)
            => ApplyLightingFromControls();

        private void LightingColor_Changed(object sender, uint e)
            => ApplyLightingFromControls();

        private void LightingShadowColor_Changed(object sender, uint e)
            => ApplyLightingFromControls();

        private void LightingShadowStrengthSlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressLightingUiSync) return;
            if (LightingShadowStrengthBox != null && !NearlyEqual(LightingShadowStrengthBox.Value, e.NewValue))
            {
                _suppressLightingUiSync = true;
                LightingShadowStrengthBox.Value = e.NewValue;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingShadowStrengthBox_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressLightingUiSync) return;
            if (LightingShadowStrengthSlider != null && !NearlyEqual(LightingShadowStrengthSlider.Value, sender.Value))
            {
                _suppressLightingUiSync = true;
                LightingShadowStrengthSlider.Value = sender.Value;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingIntensitySlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressLightingUiSync) return;
            if (LightingIntensityBox != null && !NearlyEqual(LightingIntensityBox.Value, e.NewValue))
            {
                _suppressLightingUiSync = true;
                LightingIntensityBox.Value = e.NewValue;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingIntensityBox_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressLightingUiSync) return;
            if (LightingIntensitySlider != null && !NearlyEqual(LightingIntensitySlider.Value, sender.Value))
            {
                _suppressLightingUiSync = true;
                LightingIntensitySlider.Value = sender.Value;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingFalloffSlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressLightingUiSync) return;
            if (LightingFalloffBox != null && !NearlyEqual(LightingFalloffBox.Value, e.NewValue))
            {
                _suppressLightingUiSync = true;
                LightingFalloffBox.Value = e.NewValue;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingFalloffBox_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressLightingUiSync) return;
            if (LightingFalloffSlider != null && !NearlyEqual(LightingFalloffSlider.Value, sender.Value))
            {
                _suppressLightingUiSync = true;
                LightingFalloffSlider.Value = sender.Value;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingPosition_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
            => ApplyLightingFromControls();

        private void UpdateVoxelSelectionStatusText(string? custom = null)
        {
            if (VoxelSelectionStatusText == null)
                return;

            if (!string.IsNullOrWhiteSpace(custom))
            {
                VoxelSelectionStatusText.Text = custom!;
                return;
            }

            int count = _editEngine.Selection.Count;
            string activeTool = _voxelToolState.ActiveRegistration?.DisplayName ?? "None";
            VoxelSelectionStatusText.Text =
                $"Selection: {count} voxel{(count == 1 ? "" : "s")}. Active Tool: {activeTool}. " +
                "RMB drag orbits. Use move buttons for nudges.";
        }

        private void ClearVoxelSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editEngine.ClearSelection())
            {
                UpdateVoxelSelectionStatusText("Cleared voxel selection.");
            }
            else
            {
                UpdateVoxelSelectionStatusText();
            }
        }

        private void ExpandVoxelSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editEngine.ExpandSelectionConnected())
            {
                UpdateVoxelSelectionStatusText("Expanded selection to connected voxels.");
            }
            else
            {
                UpdateVoxelSelectionStatusText("Expand selection: no connected voxels to expand.");
            }
        }

        private void MoveSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string tag })
                return;

            var parts = tag.Split(',');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out int dx) ||
                !int.TryParse(parts[1], out int dy) ||
                !int.TryParse(parts[2], out int dz))
            {
                return;
            }

            if (_editEngine.MoveSelection(new Int3(dx, dy, dz)))
            {
                UpdateVoxelSelectionStatusText($"Moved selection by ({dx},{dy},{dz}).");
            }
            else
            {
                UpdateVoxelSelectionStatusText($"Move blocked for delta ({dx},{dy},{dz}).");
            }
        }

        public bool CanUndoVoxelEdits => _editEngine.History.CanUndo;

        public bool CanRedoVoxelEdits => _editEngine.History.CanRedo;

        public bool TryUndoVoxelEdit()
        {
            FlushPendingLightingHistory();
            if (!_editEngine.Undo())
                return false;

            UpdateVoxelSelectionStatusText("Undo voxel edit.");
            return true;
        }

        public bool TryRedoVoxelEdit()
        {
            FlushPendingLightingHistory();
            if (!_editEngine.Redo())
                return false;

            UpdateVoxelSelectionStatusText("Redo voxel edit.");
            return true;
        }

        public bool IsWorkspaceFocused()
        {
            if (XamlRoot == null)
                return false;

            var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (focused == null)
                return false;

            return IsDescendantOf(this, focused);
        }

        private bool IsViewportFocused()
        {
            if (ViewportHost == null || XamlRoot == null)
                return false;

            var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (focused == null)
                return false;

            return IsDescendantOf(ViewportHost, focused);
        }

        private static bool IsDescendantOf(DependencyObject ancestor, DependencyObject? element)
        {
            var cur = element;
            while (cur != null)
            {
                if (ReferenceEquals(cur, ancestor))
                    return true;
                cur = VisualTreeHelper.GetParent(cur);
            }

            return false;
        }

        private bool TryPickFaceFromScreen(float screenX, float screenY, out VoxelFaceHit hit)
        {
            hit = default;
            if (!TryPickVoxelFaceAtHostPoint(new Windows.Foundation.Point(screenX, screenY), out var picked) || _lastVolume == null)
                return false;

            float half = _lastVolume.Size * 0.5f;
            var center = new Vector3(
                picked.X + 0.5f - half,
                picked.Y + 0.5f - half,
                picked.Z + 0.5f - half);
            var pose = _camera.GetCameraPose();
            float distance = Vector3.Distance(pose.Position, center);
            uint color = PackedBgraFromRgba(_lastVolume.GetFaceColor(picked.X, picked.Y, picked.Z, picked.Face));

            hit = new VoxelFaceHit(
                new Int3(picked.X, picked.Y, picked.Z),
                ToVoxelFace(picked.Face),
                distance,
                color);
            return true;
        }

        private bool TryPickVoxelFromScreen(float screenX, float screenY, out VoxelVoxelHit hit)
        {
            hit = default;
            if (!TryPickVoxelFaceAtHostPoint(new Windows.Foundation.Point(screenX, screenY), out var picked) || _lastVolume == null)
                return false;

            float half = _lastVolume.Size * 0.5f;
            var center = new Vector3(
                picked.X + 0.5f - half,
                picked.Y + 0.5f - half,
                picked.Z + 0.5f - half);
            var pose = _camera.GetCameraPose();
            float distance = Vector3.Distance(pose.Position, center);

            hit = new VoxelVoxelHit(
                new Int3(picked.X, picked.Y, picked.Z),
                distance,
                ToVoxelFace(picked.Face));
            return true;
        }

        private void SetFaceColorFromTool(int x, int y, int z, VoxelFace face, uint bgra)
        {
            var coreFace = ToCoreFace(face);
            var state = _document.VoxelPreviewState;
            bool hadOverrideBefore = state.TryGetFaceColorOverride(x, y, z, coreFace, out uint beforeOverrideColor);
            bool openedTransaction = !_editEngine.History.IsTransactionOpen;

            if (openedTransaction)
                _editEngine.BeginHistoryTransaction("Voxel Face Color");

            try
            {
                if (!_editEngine.SetFaceColor(x, y, z, coreFace, bgra))
                {
                    if (openedTransaction)
                        _editEngine.CancelHistoryTransaction();
                    return;
                }

                state.HasState = true;
                state.SetFaceColorOverride(x, y, z, coreFace, bgra);

                _editEngine.History.Push(new VoxelCommandHistory.DelegateCommand(
                    "Voxel Face Override",
                    undo: () =>
                    {
                        if (hadOverrideBefore)
                            state.SetFaceColorOverride(x, y, z, coreFace, beforeOverrideColor);
                        else
                            state.RemoveFaceColorOverride(x, y, z, coreFace);

                        PersistVoxelPreviewStateToDocument();
                    },
                    redo: () =>
                    {
                        state.SetFaceColorOverride(x, y, z, coreFace, bgra);
                        PersistVoxelPreviewStateToDocument();
                    }));

                _document.VoxelModel.SourceKind = VoxelModelSourceKind.Hybrid;
                PersistVoxelPreviewStateToDocument();
                if (openedTransaction)
                    _editEngine.CommitHistoryTransaction();
            }
            catch
            {
                if (openedTransaction)
                    _editEngine.CancelHistoryTransaction();
                throw;
            }
        }

        private void ClearFaceColorOverrideFromTool(int x, int y, int z, VoxelFace face)
        {
            var coreFace = ToCoreFace(face);
            var state = _document.VoxelPreviewState;
            if (!state.TryGetFaceColorOverride(x, y, z, coreFace, out _))
                return;

            var before = CaptureWorkspaceHistorySnapshot();
            ClearManualFaceColorOverride(x, y, z, coreFace);
            var after = CaptureWorkspaceHistorySnapshot();

            _editEngine.History.Push(new VoxelCommandHistory.DelegateCommand(
                "Erase Face Override",
                undo: () => RestoreWorkspaceHistorySnapshot(before),
                redo: () => RestoreWorkspaceHistorySnapshot(after)));
        }

        private void SetVoxelFromTool(int x, int y, int z, uint colorBgra)
        {
            if (_editEngine.CreateVoxel(x, y, z, colorBgra))
            {
                _document.VoxelModel.SourceKind = VoxelModelSourceKind.Hybrid;
            }
        }

        private void ClearVoxelFromTool(int x, int y, int z)
        {
            if (_editEngine.DeleteVoxel(x, y, z))
            {
                _document.VoxelModel.SourceKind = VoxelModelSourceKind.Hybrid;
            }
        }

        private void MoveSelectionFromTool(Int3 delta, VoxelMoveMode mode)
        {
            if (_editEngine.MoveSelection(delta, mode))
            {
                _document.VoxelModel.SourceKind = VoxelModelSourceKind.Hybrid;
            }
        }

        private void ClearSelectionFromTool()
            => _editEngine.ClearSelection();

        private void SetSelectionFromTool(IEnumerable<Int3> voxels, VoxelSelectionMode mode)
            => _editEngine.SetSelection(voxels, mode);

        private void ExpandSelectionFromTool()
            => _editEngine.ExpandSelectionConnected();

        private VoxelViewportState BuildViewportState()
            => new(
                (int)MathF.Round(MathF.Max(1f, _camera.ViewportWidth)),
                (int)MathF.Round(MathF.Max(1f, _camera.ViewportHeight)),
                _camera.Pitch,
                _camera.Yaw,
                _camera.ZoomPercent,
                _camera.CurrentSnapName,
                PixelPreviewCheckBox?.IsChecked == true);

        private VoxelLightingSettings GetLightingSettingsSnapshot()
        {
            var ws = _document.VoxelWorkspace;
            return new VoxelLightingSettings
            {
                Enabled = ws.LightingEnabled,
                Position = new Vector3(ws.LightPosX, ws.LightPosY, ws.LightPosZ),
                LightColorBgra = ws.LightColorBgra,
                ShadowColorBgra = ws.ShadowColorBgra,
                ShadowStrength = ws.LightShadowStrength,
                Intensity = ws.LightIntensity,
                Falloff = ws.LightFalloff,
                CastShadows = ws.LightCastShadows,
            };
        }

        private void EnsureRecommendedLightingDefaultsForCurrentVolume()
        {
            if (_lastVolume == null || _lastVolume.OccupiedCount <= 0)
                return;

            int size = Math.Max(1, _lastVolume.Size);
            if (_lastAutoLightingPresetVolumeSize == size)
                return;

            var ws = _document.VoxelWorkspace;
            if (!IsLegacyLightingDefaults(ws))
            {
                _lastAutoLightingPresetVolumeSize = size;
                return;
            }

            float scale = MathF.Max(1f, size);
            ws.LightPosX = scale * 1.15f;
            ws.LightPosY = scale * 1.55f;
            ws.LightPosZ = scale * 1.15f;
            ws.LightIntensity = 1.1f;
            ws.LightFalloff = 0.75f / scale;
            ws.LightColorBgra = 0xFFFFFFFF;
            ws.ShadowColorBgra = 0xC0000000;
            ws.LightShadowStrength = 1f;

            _lastAutoLightingPresetVolumeSize = size;
            SyncLightingControlsFromDocument();
        }

        private static bool IsLegacyLightingDefaults(VoxelWorkspaceDocumentState ws)
        {
            return NearlyEqual(ws.LightPosX, 32f) &&
                   NearlyEqual(ws.LightPosY, 48f) &&
                   NearlyEqual(ws.LightPosZ, 32f) &&
                   ws.LightColorBgra == 0xFFFFFFFF &&
                   ws.ShadowColorBgra == 0xC0000000 &&
                   NearlyEqual(ws.LightShadowStrength, 1f) &&
                   NearlyEqual(ws.LightIntensity, 1f) &&
                   NearlyEqual(ws.LightFalloff, 0.05f);
        }

        private void UpdateLightingSettingsFromTool(Action<VoxelLightingSettings> edit)
        {
            if (edit == null)
                return;

            var before = GetLightingSettingsSnapshot();
            var next = CloneLightingSettings(before);
            edit(next);
            if (!IsLightingSettingsEqual(before, next))
            {
                ApplyLightingSnapshotToWorkspace(next, before);
                SyncLightingControlsFromDocument();
            }
        }

        private static Face ToCoreFace(VoxelFace face)
            => face switch
            {
                VoxelFace.Front => Face.Front,
                VoxelFace.Back => Face.Back,
                VoxelFace.Left => Face.Left,
                VoxelFace.Right => Face.Right,
                VoxelFace.Top => Face.Top,
                VoxelFace.Bottom => Face.Bottom,
                _ => Face.Front,
            };

        private static VoxelFace ToVoxelFace(Face face)
            => face switch
            {
                Face.Front => VoxelFace.Front,
                Face.Back => VoxelFace.Back,
                Face.Left => VoxelFace.Left,
                Face.Right => VoxelFace.Right,
                Face.Top => VoxelFace.Top,
                Face.Bottom => VoxelFace.Bottom,
                _ => VoxelFace.Front,
            };

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
        private void RenderViewport(
            ViewportRenderOverrides? renderOverrides = null,
            bool presentOnViewport = true,
            Action<byte[], int, int>? renderedBufferSink = null)
        {
            try
            {
                if (ViewportImage == null)
                    return;

                UpdateViewportSizeFromControl();

                bool pixelMode = PixelPreviewCheckBox?.IsChecked == true && _lastVolume != null;
                bool pixelPreviewAntialias = pixelMode && PixelPreviewAntialiasCheckBox?.IsChecked == true;
                float pixelPreviewAaStrength = Math.Clamp((float)(PixelPreviewAaStrengthSlider?.Value ?? 0.35d), 0f, 1f);
                bool drawOutline = renderOverrides?.DrawOutline ?? (OutlineCheckBox?.IsChecked == true);
                bool drawBackdropGrid = renderOverrides?.DrawBackdropGrid ?? (BackdropGridCheckBox?.IsChecked != false);
                bool drawBackdropProjectionTiles = renderOverrides?.DrawBackdropProjectionTiles ?? (BackdropProjectionTilesCheckBox?.IsChecked == true);
                bool drawSurfaceVoxelGridUi = renderOverrides?.DrawSurfaceVoxelGrid ?? (SurfaceVoxelGridCheckBox?.IsChecked == true);
                bool includeSelectionOverlay = renderOverrides?.IncludeSelectionOverlay ?? true;
                uint clearColor = renderOverrides?.ClearColor ?? ClearColor;
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
                ConfigureViewportImagePresentation(pixelMode, pixelPreviewAntialias, usedDisplayPhysicalW, usedDisplayPhysicalH);

                // Ensure render buffers
                int pixelCount = renderW * renderH;
                if (_renderBuffer == null || _renderBuffer.Length != pixelCount * 4)
                {
                    _renderBuffer = new byte[pixelCount * 4];
                }

                bool renderedExactCardinal = false;
                bool drawSurfaceVoxelGrid = !pixelMode && (SurfaceVoxelGridCheckBox?.IsChecked == true);
                if (_lastVolume != null && _lastVolume.OccupiedCount > 0)
                {
                    EnsureRecommendedLightingDefaultsForCurrentVolume();
                    drawSurfaceVoxelGrid = !pixelMode && drawSurfaceVoxelGridUi;

                    int outlineVoxelSize = Math.Max(1, (int)Math.Round(OutlineSizeBox?.Value ?? 1d));
                    int outlineRenderSize = outlineVoxelSize;
                    if (!pixelMode)
                    {
                        var fr = _camera.GetFrustum();
                        float pixelsPerVoxel = renderH / MathF.Max(1e-6f, fr.Height); // 1 voxel = 1 world unit
                        outlineRenderSize = Math.Max(1, (int)MathF.Round(outlineVoxelSize * pixelsPerVoxel));
                        outlineRenderSize = Math.Min(Math.Max(renderW, renderH), outlineRenderSize);
                    }

                    var ws = _document.VoxelWorkspace;
                    var opts = new VoxelRenderer.RenderOptions
                    {
                        // With z-buffer rendering, disabling backface cull avoids
                        // edge-angle face loss in the preview.
                        BackfaceCull = false,
                        // Phase 4: default to flat/unlit unless the lighting utility enables preview lighting.
                        LightingEnabled = ws?.LightingEnabled == true,
                        LightPosition = ws != null
                            ? new Vector3(ws.LightPosX, ws.LightPosY, ws.LightPosZ)
                            : new Vector3(32f, 48f, 32f),
                        LightColor = ws?.LightColorBgra ?? 0xFFFFFFFF,
                        ShadowColor = ws?.ShadowColorBgra ?? 0xC0000000,
                        ShadowStrength = ws?.LightShadowStrength ?? 1f,
                        LightIntensity = ws?.LightIntensity ?? 1f,
                        // Standard ambient fill for pixel workflows so lit previews are readable
                        // without forcing users to position a perfect key light.
                        AmbientIntensity = 0.22f,
                        LightFalloff = ws?.LightFalloff ?? 0.05f,
                        LightCastShadows = ws?.LightCastShadows ?? false,
                        // In pixel preview we draw the backing grid as a separate 2D pass
                        // so it does not change the voxel rasterization path.
                        DrawBackdropGrid = !pixelMode && drawBackdropGrid,
                        DrawBackdropProjectionTiles = !pixelMode && drawBackdropProjectionTiles,
                        BackdropCageScale = Math.Clamp((float)(BackdropCageScaleBox?.Value ?? ws?.BackdropCageScale ?? 1.6d), 1.05f, 4f),
                        BackdropFrontProjection = _backdropFrontProjectionImage,
                        BackdropBackProjection = _backdropBackProjectionImage,
                        BackdropLeftProjection = _backdropLeftProjectionImage,
                        BackdropRightProjection = _backdropRightProjectionImage,
                        BackdropTopProjection = _backdropTopProjectionImage,
                        BackdropBottomProjection = _backdropBottomProjectionImage,
                        DrawSurfaceVoxelGrid = drawSurfaceVoxelGrid,
                        DrawOutline = drawOutline,
                        OutlineColor = OutlineColorSwatch.Color,
                        OutlineSize = outlineRenderSize,
                    };

                    bool renderedFromPixelSpriteCache = false;
                    if (pixelMode)
                    {
                        renderedExactCardinal = TryRenderExactCardinalPixelPreview(
                            _lastVolume, renderW, renderH, _renderBuffer, clearColor, opts);

                        if (!renderedExactCardinal)
                        {
                            renderedFromPixelSpriteCache = TryRenderCachedPixelPreviewSprite(
                                _lastVolume, renderW, renderH, _renderBuffer, clearColor, opts);
                        }
                    }

                    if (!renderedExactCardinal && !renderedFromPixelSpriteCache)
                    {
                        VoxelRenderer.Render(
                            _lastVolume, _camera,
                            renderW, renderH,
                            _renderBuffer,
                            clearColor, opts);
                    }
                }
                else
                {
                    FillClear(_renderBuffer, pixelCount, clearColor);
                }

                byte[] renderSource = _renderBuffer;
                if (pixelMode && pixelPreviewAntialias)
                {
                    EnsurePixelPreviewAaBuffer(pixelCount * 4);
                    ApplyPixelPreviewEdgeAa(_renderBuffer, renderW, renderH, _pixelPreviewAaBuffer!, pixelPreviewAaStrength);
                    renderSource = _pixelPreviewAaBuffer!;
                }

                // Build the display buffer
                byte[] displayBuffer;

                if (pixelMode && screenPixelSize > 1)
                {
                    // Integer-scale upscale: each render pixel becomes exactly
                    // screenPixelSize × screenPixelSize screen pixels — no distortion
                    displayW = renderW * screenPixelSize;
                    displayH = renderH * screenPixelSize;
                    EnsureDisplayBuffer(displayW * displayH * 4);
                    displayBuffer = _displayBuffer!;
                    FillClear(displayBuffer, displayW * displayH, clearColor);

                    if (drawBackdropGrid && _lastVolume != null)
                    {
                        float cageScale = Math.Clamp((float)(BackdropCageScaleBox?.Value ?? _document.VoxelWorkspace.BackdropCageScale), 1.05f, 4f);
                        DrawPixelPreviewBackdropCage3D(
                            displayBuffer, displayW, displayH,
                            renderW, renderH, screenPixelSize,
                            _lastVolume.Size,
                            minorColor: 0xFF2A2F35,
                            majorColor: 0xFF39424B,
                            majorEvery: 4,
                            cageScale: cageScale);
                    }

                    UpscaleNearestBgra(renderSource, renderW, renderH, displayBuffer, displayW, displayH, screenPixelSize);

                    if (renderedExactCardinal &&
                        screenPixelSize > 1 &&
                        drawSurfaceVoxelGrid)
                    {
                        DrawExactCardinalPixelPreviewSurfaceGrid(
                            _renderBuffer, renderW, renderH,
                            displayBuffer, displayW, displayH,
                            screenPixelSize,
                            0xB0000000);
                    }
                }
                else
                {
                    displayW = renderW;
                    displayH = renderH;
                    displayBuffer = renderSource;
                }

                if (includeSelectionOverlay)
                {
                    OverlaySelectionHighlight(
                        displayBuffer,
                        displayW,
                        displayH,
                        renderW,
                        renderH,
                        pixelMode,
                        screenPixelSize);
                }

                renderedBufferSink?.Invoke(displayBuffer, displayW, displayH);

                if (!presentOnViewport)
                    return;

                // Push to WriteableBitmap
                if (_viewportBitmap == null ||
                    _viewportBitmap.PixelWidth != displayW ||
                    _viewportBitmap.PixelHeight != displayH)
                {
                    _viewportBitmap = new WriteableBitmap(displayW, displayH);
                }

                using var stream = _viewportBitmap.PixelBuffer.AsStream();
                stream.Seek(0, SeekOrigin.Begin);
                stream.Write(displayBuffer, 0, displayW * displayH * 4);

                ViewportImage.Source = _viewportBitmap;
                UpdateCameraStatsText(pixelMode, screenPixelSize, renderW, renderH);
                UpdateAxisGizmo();
                UpdateLightingQuickActionsState();
                UpdateLightHandleOverlay();
                PersistVoxelPreviewStateToDocument();
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Viewport render failed: {Error}", ex.Message);
            }
        }

        private void EnsurePixelPreviewAaBuffer(int byteLength)
        {
            if (_pixelPreviewAaBuffer == null || _pixelPreviewAaBuffer.Length != byteLength)
            {
                _pixelPreviewAaBuffer = new byte[byteLength];
            }
        }

        private void EnsureDisplayBuffer(int byteLength)
        {
            if (_displayBuffer == null || _displayBuffer.Length != byteLength)
            {
                _displayBuffer = new byte[byteLength];
            }
        }

        private static void UpscaleNearestBgra(
            byte[] source,
            int sourceWidth,
            int sourceHeight,
            byte[] destination,
            int destinationWidth,
            int destinationHeight,
            int scale)
        {
            if (source == null || destination == null)
                return;
            if (sourceWidth <= 0 || sourceHeight <= 0 || destinationWidth <= 0 || destinationHeight <= 0 || scale <= 0)
                return;
            if (source.Length < sourceWidth * sourceHeight * 4 || destination.Length < destinationWidth * destinationHeight * 4)
                return;

            for (int sy = 0; sy < sourceHeight; sy++)
            {
                int dstBaseY = sy * scale;
                for (int sx = 0; sx < sourceWidth; sx++)
                {
                    int si = (sy * sourceWidth + sx) * 4;
                    byte b0 = source[si];
                    byte b1 = source[si + 1];
                    byte b2 = source[si + 2];
                    byte b3 = source[si + 3];
                    int dstBaseX = sx * scale;

                    for (int py = 0; py < scale; py++)
                    {
                        int dy = dstBaseY + py;
                        if (dy < 0 || dy >= destinationHeight) continue;
                        for (int px = 0; px < scale; px++)
                        {
                            int dx = dstBaseX + px;
                            if (dx < 0 || dx >= destinationWidth) continue;
                            int di = (dy * destinationWidth + dx) * 4;
                            destination[di] = b0;
                            destination[di + 1] = b1;
                            destination[di + 2] = b2;
                            destination[di + 3] = b3;
                        }
                    }
                }
            }
        }

        private static void ApplyPixelPreviewEdgeAa(
            byte[] source,
            int width,
            int height,
            byte[] destination,
            float strengthMultiplier)
        {
            if (source == null || destination == null)
                return;
            if (width <= 0 || height <= 0)
                return;
            strengthMultiplier = Math.Clamp(strengthMultiplier, 0f, 1f);
            if (strengthMultiplier <= 0f)
            {
                Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
                return;
            }
            int byteLength = width * height * 4;
            if (source.Length < byteLength || destination.Length < byteLength)
                return;

            static int Luma(byte b, byte g, byte r) => (r * 77 + g * 150 + b * 29) >> 8;
            static byte Blend(byte from, int to, float t)
                => (byte)Math.Clamp((int)MathF.Round(from + ((to - from) * t)), 0, 255);

            const int edgeThreshold = 22;
            const int orientationSlack = 6;

            for (int y = 0; y < height; y++)
            {
                int yUp = y > 0 ? y - 1 : y;
                int yDn = y < height - 1 ? y + 1 : y;

                for (int x = 0; x < width; x++)
                {
                    int xLt = x > 0 ? x - 1 : x;
                    int xRt = x < width - 1 ? x + 1 : x;

                    int iC = (y * width + x) * 4;
                    int iL = (y * width + xLt) * 4;
                    int iR = (y * width + xRt) * 4;
                    int iU = (yUp * width + x) * 4;
                    int iD = (yDn * width + x) * 4;

                    byte cb = source[iC];
                    byte cg = source[iC + 1];
                    byte cr = source[iC + 2];
                    byte ca = source[iC + 3];

                    int lumC = Luma(cb, cg, cr);
                    int lumL = Luma(source[iL], source[iL + 1], source[iL + 2]);
                    int lumR = Luma(source[iR], source[iR + 1], source[iR + 2]);
                    int lumU = Luma(source[iU], source[iU + 1], source[iU + 2]);
                    int lumD = Luma(source[iD], source[iD + 1], source[iD + 2]);

                    int dL = Math.Abs(lumC - lumL);
                    int dR = Math.Abs(lumC - lumR);
                    int dU = Math.Abs(lumC - lumU);
                    int dD = Math.Abs(lumC - lumD);
                    int maxDiff = Math.Max(Math.Max(dL, dR), Math.Max(dU, dD));

                    if (maxDiff < edgeThreshold)
                    {
                        destination[iC] = cb;
                        destination[iC + 1] = cg;
                        destination[iC + 2] = cr;
                        destination[iC + 3] = ca;
                        continue;
                    }

                    int gradX = dL + dR;
                    int gradY = dU + dD;
                    int avgB, avgG, avgR;
                    if (gradX > gradY + orientationSlack)
                    {
                        avgB = (source[iL] + source[iR]) >> 1;
                        avgG = (source[iL + 1] + source[iR + 1]) >> 1;
                        avgR = (source[iL + 2] + source[iR + 2]) >> 1;
                    }
                    else if (gradY > gradX + orientationSlack)
                    {
                        avgB = (source[iU] + source[iD]) >> 1;
                        avgG = (source[iU + 1] + source[iD + 1]) >> 1;
                        avgR = (source[iU + 2] + source[iD + 2]) >> 1;
                    }
                    else
                    {
                        avgB = (source[iL] + source[iR] + source[iU] + source[iD]) >> 2;
                        avgG = (source[iL + 1] + source[iR + 1] + source[iU + 1] + source[iD + 1]) >> 2;
                        avgR = (source[iL + 2] + source[iR + 2] + source[iU + 2] + source[iD + 2]) >> 2;
                    }

                    float strength = Math.Clamp((maxDiff - edgeThreshold) / 140f, 0f, 1f) * 0.45f * strengthMultiplier;
                    destination[iC] = Blend(cb, avgB, strength);
                    destination[iC + 1] = Blend(cg, avgG, strength);
                    destination[iC + 2] = Blend(cr, avgR, strength);
                    destination[iC + 3] = ca;
                }
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

        private void ConfigureViewportImagePresentation(
            bool pixelMode,
            bool pixelPreviewAntialias,
            int usedPhysicalPixelWidth,
            int usedPhysicalPixelHeight)
        {
            if (ViewportImage == null) return;

            // In pixel-preview mode, present the already-upscaled bitmap at an explicit
            // centered size so XAML is not doing an extra Uniform fit pass for us.
            if (pixelMode)
            {
                _ = pixelPreviewAntialias;
                ViewportImage.Stretch = Stretch.None;
                ViewportImage.HorizontalAlignment = HorizontalAlignment.Center;
                ViewportImage.VerticalAlignment = VerticalAlignment.Center;

                double scale = Math.Max(1e-6, _viewportRasterScale);
                ViewportImage.Width = Math.Max(1, usedPhysicalPixelWidth) / scale;
                ViewportImage.Height = Math.Max(1, usedPhysicalPixelHeight) / scale;
                // Keep viewport presentation nearest-neighbor even when AA is enabled.
                // Pixel-preview AA is applied in render-space per pixel, not as a post-scale blur.
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

        private void PixelPreviewAntialias_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            UpdatePixelPreviewAaStrengthLabel();
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void PixelPreviewAaStrength_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            UpdatePixelPreviewAaStrengthLabel();
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void UpdatePixelPreviewAaStrengthLabel()
        {
            if (PixelPreviewAaStrengthText == null)
                return;

            float strength = Math.Clamp((float)(PixelPreviewAaStrengthSlider?.Value ?? 0.35d), 0f, 1f);
            PixelPreviewAaStrengthText.Text = $"{MathF.Round(strength * 100f):0}%";
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
        private static void FillClear(byte[] buffer, int pixelCount, uint clearColor)
        {
            byte b = (byte)(clearColor & 0xFF);
            byte g = (byte)((clearColor >> 8) & 0xFF);
            byte r = (byte)((clearColor >> 16) & 0xFF);
            byte a = (byte)((clearColor >> 24) & 0xFF);

            for (int i = 0; i < pixelCount; i++)
            {
                int bi = i * 4;
                buffer[bi] = b;
                buffer[bi + 1] = g;
                buffer[bi + 2] = r;
                buffer[bi + 3] = a;
            }
        }

        private void OverlaySelectionHighlight(
            byte[]? displayBuffer,
            int displayW,
            int displayH,
            int renderW,
            int renderH,
            bool pixelMode,
            int screenPixelSize)
        {
            if (displayBuffer == null || _lastVolume == null)
                return;
            if (_editEngine.Selection.Count == 0)
                return;
            if (displayBuffer.Length < displayW * displayH * 4)
                return;

            // Visual color intentionally distinct from outline/grid.
            uint lineColor = pixelMode ? 0xFF3CFBFFu : 0xE03CFBFFu;
            int thickness = pixelMode ? Math.Max(1, screenPixelSize / 6) : 1;

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            var fr = _camera.GetFrustum();
            float vw = MathF.Max(1f, _camera.ViewportWidth);
            float vh = MathF.Max(1f, _camera.ViewportHeight);
            float frWidth = MathF.Max(1e-6f, fr.Width);
            float frHeight = MathF.Max(1e-6f, fr.Height);
            float frCenterX = (fr.Left + fr.Right) * 0.5f;
            float frCenterY = (fr.Top + fr.Bottom) * 0.5f;

            int size = _lastVolume.Size;
            float half = size * 0.5f;
            Span<Vector3> corners = stackalloc Vector3[8];
            var proj = new (float X, float Y, bool Valid)[8];

            int drawn = 0;
            foreach (var sel in _editEngine.Selection.Enumerate())
            {
                if ((uint)sel.X >= (uint)size || (uint)sel.Y >= (uint)size || (uint)sel.Z >= (uint)size)
                    continue;
                if (!_lastVolume.IsOccupied(sel.X, sel.Y, sel.Z))
                    continue;

                if (++drawn > 256)
                    break;

                var basePos = new Vector3(sel.X - half, sel.Y - half, sel.Z - half);
                corners[0] = basePos + new Vector3(0, 0, 0);
                corners[1] = basePos + new Vector3(1, 0, 0);
                corners[2] = basePos + new Vector3(1, 1, 0);
                corners[3] = basePos + new Vector3(0, 1, 0);
                corners[4] = basePos + new Vector3(0, 0, 1);
                corners[5] = basePos + new Vector3(1, 0, 1);
                corners[6] = basePos + new Vector3(1, 1, 1);
                corners[7] = basePos + new Vector3(0, 1, 1);
                for (int i = 0; i < 8; i++)
                {
                    var rel = corners[i] - pose.Position;
                    float cx = Vector3.Dot(rel, basis.Right);
                    float cy = Vector3.Dot(rel, basis.Up);
                    float cz = Vector3.Dot(rel, basis.Forward);
                    if (cz <= 0f)
                    {
                        proj[i] = (0f, 0f, false);
                        continue;
                    }

                    float xNdc = (2f * cx - 2f * frCenterX) / frWidth;
                    float yNdc = (2f * cy - 2f * frCenterY) / frHeight;
                    float sx = (xNdc * 0.5f + 0.5f) * vw;
                    float sy = (1f - (yNdc * 0.5f + 0.5f)) * vh;

                    if (pixelMode)
                    {
                        sx *= screenPixelSize;
                        sy *= screenPixelSize;
                    }

                    proj[i] = (sx, sy, true);
                }

                DrawEdge(0, 1); DrawEdge(1, 2); DrawEdge(2, 3); DrawEdge(3, 0);
                DrawEdge(4, 5); DrawEdge(5, 6); DrawEdge(6, 7); DrawEdge(7, 4);
                DrawEdge(0, 4); DrawEdge(1, 5); DrawEdge(2, 6); DrawEdge(3, 7);

                void DrawEdge(int a, int b)
                {
                    if (!proj[a].Valid || !proj[b].Valid)
                        return;

                    DrawLineBgra(
                        displayBuffer, displayW, displayH,
                        proj[a].X, proj[a].Y,
                        proj[b].X, proj[b].Y,
                        lineColor, thickness);
                }
            }
        }

        private static void DrawLineBgra(
            byte[] buffer,
            int width,
            int height,
            float x0f,
            float y0f,
            float x1f,
            float y1f,
            uint color,
            int thickness)
        {
            int x0 = (int)MathF.Round(x0f);
            int y0 = (int)MathF.Round(y0f);
            int x1 = (int)MathF.Round(x1f);
            int y1 = (int)MathF.Round(y1f);

            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            thickness = Math.Max(1, thickness);
            int radius = thickness / 2;

            while (true)
            {
                for (int oy = -radius; oy <= radius; oy++)
                {
                    for (int ox = -radius; ox <= radius; ox++)
                    {
                        int px = x0 + ox;
                        int py = y0 + oy;
                        if ((uint)px >= (uint)width || (uint)py >= (uint)height)
                            continue;
                        AlphaBlendPackedBgra(buffer, (py * width + px) * 4, color);
                    }
                }

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = err * 2;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private bool IsKeyDown(VirtualKey key)
        {
            var st = InputKeyboardSource.GetKeyStateForCurrentThread(key);
            return (st & CoreVirtualKeyStates.Down) != 0;
        }

        private bool IsFaceToolActive(string? toolId)
            => toolId == VoxelToolIds.FacePaint ||
               toolId == VoxelToolIds.FaceDropper ||
               toolId == VoxelToolIds.FaceEraseOverride;

        private static bool IsBuiltInVoxelTool(string? toolId)
            => toolId == VoxelToolIds.FacePaint ||
               toolId == VoxelToolIds.FaceDropper ||
               toolId == VoxelToolIds.FaceEraseOverride ||
               toolId == VoxelToolIds.VoxelCreate ||
               toolId == VoxelToolIds.VoxelDelete ||
               toolId == VoxelToolIds.VoxelSelect ||
               toolId == VoxelToolIds.VoxelMove ||
               toolId == VoxelToolIds.Lighting;

        private bool IsVoxelClickToolActive(string? toolId)
        {
            if (string.IsNullOrWhiteSpace(toolId))
                return false;

            var behavior = _voxelToolState.ActiveRegistration?.Behavior;
            if (behavior == null)
            {
                return IsBuiltInVoxelTool(toolId);
            }

            return behavior.InputPattern != VoxelToolInputPattern.Utility;
        }

        private bool TryApplyActiveVoxelToolAtHostPoint(
            Windows.Foundation.Point hostPoint,
            Microsoft.UI.Input.PointerPointProperties? pointerProps,
            ToolPointerPhase phase)
        {
            bool continuousStroke = phase != ToolPointerPhase.Pressed;
            string? toolId = _voxelToolState.ActiveToolId;
            if (string.IsNullOrWhiteSpace(toolId))
                return false;

            EnsureActiveVoxelToolHandler();

            if (!IsBuiltInVoxelTool(toolId))
            {
                return TryDispatchActiveVoxelToolHandler(hostPoint, pointerProps, phase);
            }

            if (IsFaceToolActive(toolId))
            {
                ApplyFacePainterActionAtHostPoint(hostPoint, continuousStroke);
                return true;
            }

            if (_lastVolume == null || _lastVolume.OccupiedCount == 0)
            {
                UpdateVoxelSelectionStatusText("No voxel model loaded. Build a voxel model first.");
                return false;
            }

            if (!TryPickVoxelFaceAtHostPoint(hostPoint, out var picked))
            {
                if (!continuousStroke && (toolId == VoxelToolIds.VoxelSelect || toolId == VoxelToolIds.VoxelMove))
                {
                    if (!IsKeyDown(VirtualKey.Control) && !IsKeyDown(VirtualKey.Shift) && !IsKeyDown(VirtualKey.Menu))
                    {
                        _editEngine.ClearSelection();
                        UpdateVoxelSelectionStatusText("Selection cleared.");
                    }
                }
                return false;
            }

            switch (toolId)
            {
                case VoxelToolIds.VoxelCreate:
                {
                    var delta = FaceToOffset(picked.Face);
                    int tx = picked.X + delta.X;
                    int ty = picked.Y + delta.Y;
                    int tz = picked.Z + delta.Z;
                    uint color = _palette?.Foreground ?? 0xFF000000;
                    if (_editEngine.CreateVoxel(tx, ty, tz, color))
                    {
                        UpdateVoxelSelectionStatusText($"Created voxel @ ({tx},{ty},{tz}).");
                    }
                    else if (!continuousStroke)
                    {
                        UpdateVoxelSelectionStatusText($"Create blocked @ ({tx},{ty},{tz}).");
                    }
                    return true;
                }

                case VoxelToolIds.VoxelDelete:
                {
                    if (_editEngine.DeleteVoxel(picked.X, picked.Y, picked.Z))
                    {
                        UpdateVoxelSelectionStatusText($"Deleted voxel @ ({picked.X},{picked.Y},{picked.Z}).");
                    }
                    else if (!continuousStroke)
                    {
                        UpdateVoxelSelectionStatusText($"Delete blocked @ ({picked.X},{picked.Y},{picked.Z}).");
                    }
                    return true;
                }

                case VoxelToolIds.VoxelSelect:
                case VoxelToolIds.VoxelMove:
                {
                    var mode = GetSelectionModeFromModifiers();
                    if (_editEngine.SetSelection(new[] { new Int3(picked.X, picked.Y, picked.Z) }, mode))
                    {
                        UpdateVoxelSelectionStatusText(
                            $"Selected voxel @ ({picked.X},{picked.Y},{picked.Z}) [{mode}].");
                    }
                    else if (!continuousStroke)
                    {
                        UpdateVoxelSelectionStatusText();
                    }
                    return true;
                }
            }

            return false;
        }

        private bool TryDispatchActiveVoxelToolHandler(
            Windows.Foundation.Point hostPoint,
            Microsoft.UI.Input.PointerPointProperties? pointerProps,
            ToolPointerPhase phase)
        {
            EnsureActiveVoxelToolHandler();
            if (_activeVoxelToolHandler == null)
                return false;

            var evt = BuildVoxelPointerEvent(hostPoint, pointerProps);
            try
            {
                return phase switch
                {
                    ToolPointerPhase.Pressed => _activeVoxelToolHandler.PointerPressed(evt),
                    ToolPointerPhase.Moved => _activeVoxelToolHandler.PointerMoved(evt),
                    ToolPointerPhase.Released => _activeVoxelToolHandler.PointerReleased(evt),
                    _ => false,
                };
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Voxel tool handler pointer event failed id={ToolId}: {Error}",
                    _activeVoxelToolHandlerToolId ?? "(unknown)", ex.Message);
                return false;
            }
        }

        private VoxelPointerEvent BuildVoxelPointerEvent(
            Windows.Foundation.Point hostPoint,
            Microsoft.UI.Input.PointerPointProperties? pointerProps)
        {
            bool shift = IsKeyDown(VirtualKey.Shift);
            bool ctrl = IsKeyDown(VirtualKey.Control);
            bool alt = IsKeyDown(VirtualKey.Menu);

            return new VoxelPointerEvent(
                (float)hostPoint.X,
                (float)hostPoint.Y,
                pointerProps?.IsLeftButtonPressed == true,
                pointerProps?.IsRightButtonPressed == true,
                pointerProps?.IsMiddleButtonPressed == true,
                shift,
                ctrl,
                alt);
        }

        private static Int3 FaceToOffset(Face face)
        {
            return face switch
            {
                Face.Front => new Int3(0, 0, -1),
                Face.Back => new Int3(0, 0, 1),
                Face.Left => new Int3(-1, 0, 0),
                Face.Right => new Int3(1, 0, 0),
                Face.Top => new Int3(0, 1, 0),
                Face.Bottom => new Int3(0, -1, 0),
                _ => new Int3(0, 0, 0),
            };
        }

        private VoxelSelectionMode GetSelectionModeFromModifiers()
        {
            bool shift = IsKeyDown(VirtualKey.Shift);
            bool ctrl = IsKeyDown(VirtualKey.Control);
            bool alt = IsKeyDown(VirtualKey.Menu);

            if (alt) return VoxelSelectionMode.Remove;
            if (ctrl && shift) return VoxelSelectionMode.Toggle;
            if (ctrl) return VoxelSelectionMode.Toggle;
            if (shift) return VoxelSelectionMode.Add;
            return VoxelSelectionMode.Replace;
        }

        private void ApplyFacePainterActionAtHostPoint(Windows.Foundation.Point hostPoint, bool continuousStroke = false)
        {
            if (_lastVolume == null || _lastVolume.OccupiedCount == 0)
                return;

            if (!TryPickVoxelFaceAtHostPoint(hostPoint, out var picked))
            {
                return;
            }

            if (continuousStroke && _lastStrokePaintFace.HasValue && _lastStrokePaintFace.Value.Equals(picked))
                return;

            _lastStrokePaintFace = picked;

            FacePainterMode mode = _voxelToolState.ActiveToolId switch
            {
                VoxelToolIds.FaceDropper => FacePainterMode.Sample,
                VoxelToolIds.FaceEraseOverride => FacePainterMode.EraseOverride,
                _ => FacePainterMode.Paint,
            };

            switch (mode)
            {
                case FacePainterMode.Paint:
                {
                    uint color = _palette?.Foreground ?? 0xFF000000;
                    SetFaceColorFromTool(picked.X, picked.Y, picked.Z, ToVoxelFace(picked.Face), color);
                    break;
                }

                case FacePainterMode.Sample:
                {
                    var sampled = _lastVolume.GetFaceColor(picked.X, picked.Y, picked.Z, picked.Face);
                    uint color = PackedBgraFromRgba(sampled);
                    _palette?.SetForeground(color);
                    break;
                }

                case FacePainterMode.EraseOverride:
                {
                    ClearFaceColorOverrideFromTool(picked.X, picked.Y, picked.Z, ToVoxelFace(picked.Face));
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

        private void RefreshOccupiedBoundsCache()
        {
            if (!TryComputeOccupiedBounds(_lastVolume, out var min, out var max))
            {
                _hasOccupiedBounds = false;
                _occupiedBoundsMin = default;
                _occupiedBoundsMax = default;
                return;
            }

            _hasOccupiedBounds = true;
            _occupiedBoundsMin = min;
            _occupiedBoundsMax = max;
        }

        private static bool TryComputeOccupiedBounds(VoxelVolume? volume, out Vector3 min, out Vector3 max)
        {
            min = default;
            max = default;
            if (volume == null || volume.OccupiedCount <= 0)
                return false;

            int size = volume.Size;
            float half = size * 0.5f;
            bool found = false;

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (!volume.IsOccupied(x, y, z))
                            continue;

                        var center = new Vector3(
                            (x - half) + 0.5f,
                            (y - half) + 0.5f,
                            (z - half) + 0.5f);

                        if (!found)
                        {
                            min = center;
                            max = center;
                            found = true;
                            continue;
                        }

                        min = Vector3.Min(min, center);
                        max = Vector3.Max(max, center);
                    }
                }
            }

            return found;
        }

        private bool TryGetRecommendedLightSpawnPosition(out Vector3 spawnPosition)
        {
            spawnPosition = default;
            if (!_hasOccupiedBounds)
                return false;

            var center = (_occupiedBoundsMin + _occupiedBoundsMax) * 0.5f;
            var extents = _occupiedBoundsMax - _occupiedBoundsMin;
            float spanX = MathF.Abs(extents.X);
            float spanY = MathF.Abs(extents.Y);
            float spanZ = MathF.Abs(extents.Z);

            float lift = MathF.Max(5f, (spanY * 0.5f) + 2f);
            float sideOffset = MathF.Max(2f, MathF.Max(spanX, spanZ) * 0.25f);

            spawnPosition = new Vector3(
                center.X + sideOffset,
                _occupiedBoundsMax.Y + lift,
                center.Z + sideOffset);

            spawnPosition = ClampLightPositionToModelNeighborhood(spawnPosition);
            return true;
        }

        private bool TryGetCameraFacingLightSpawnPosition(out Vector3 spawnPosition)
        {
            spawnPosition = default;
            if (!_hasOccupiedBounds)
                return false;

            var center = (_occupiedBoundsMin + _occupiedBoundsMax) * 0.5f;
            var extents = _occupiedBoundsMax - _occupiedBoundsMin;
            float span = MathF.Max(1f, MathF.Max(MathF.Abs(extents.X), MathF.Max(MathF.Abs(extents.Y), MathF.Abs(extents.Z))));

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            Vector3 toCamera = Vector3.Normalize(pose.Position - center);

            float frontOffset = MathF.Max(5f, span * 0.9f);
            float lift = MathF.Max(4f, span * 0.45f);
            float side = MathF.Max(1.5f, span * 0.15f);

            spawnPosition = center + (toCamera * frontOffset) + (basis.Up * lift) + (basis.Right * side);
            spawnPosition = ClampLightPositionToModelNeighborhood(spawnPosition);
            return true;
        }

        private bool IsLightPositionUsableForCurrentVolume(Vector3 position)
        {
            if (!_hasOccupiedBounds)
                return true;

            var center = (_occupiedBoundsMin + _occupiedBoundsMax) * 0.5f;
            var extents = _occupiedBoundsMax - _occupiedBoundsMin;
            float spanX = MathF.Abs(extents.X);
            float spanY = MathF.Abs(extents.Y);
            float spanZ = MathF.Abs(extents.Z);
            float span = MathF.Max(spanX, MathF.Max(spanY, spanZ));
            float range = MathF.Max(12f, (span * 2.5f) + 5f);

            if (MathF.Abs(position.X - center.X) > range ||
                MathF.Abs(position.Y - center.Y) > range ||
                MathF.Abs(position.Z - center.Z) > range)
            {
                return false;
            }

            if (!TryProjectWorldToHostDip(position, out var hostDip, out var depth) || depth <= 0f)
                return false;

            if (TryGetPresentedImageRectInHostDip(out var imageRect))
            {
                const float pad = 4f;
                if (hostDip.X < imageRect.X + pad || hostDip.X > imageRect.X + imageRect.Width - pad ||
                    hostDip.Y < imageRect.Y + pad || hostDip.Y > imageRect.Y + imageRect.Height - pad)
                {
                    return false;
                }
            }

            return true;
        }

        private Vector3 ClampLightPositionToModelNeighborhood(Vector3 worldPosition)
        {
            if (!_hasOccupiedBounds)
            {
                return new Vector3(
                    Math.Clamp(worldPosition.X, -1024f, 1024f),
                    Math.Clamp(worldPosition.Y, -1024f, 1024f),
                    Math.Clamp(worldPosition.Z, -1024f, 1024f));
            }

            var center = (_occupiedBoundsMin + _occupiedBoundsMax) * 0.5f;
            var extents = _occupiedBoundsMax - _occupiedBoundsMin;
            float spanX = MathF.Abs(extents.X);
            float spanY = MathF.Abs(extents.Y);
            float spanZ = MathF.Abs(extents.Z);
            float span = MathF.Max(spanX, MathF.Max(spanY, spanZ));
            float range = MathF.Max(12f, (span * 2.5f) + 5f);

            var min = center - new Vector3(range, range, range);
            var max = center + new Vector3(range, range, range);
            return Vector3.Clamp(worldPosition, min, max);
        }

        private Vector3 GetCurrentLightPosition()
        {
            var ws = _document.VoxelWorkspace;
            return new Vector3(ws.LightPosX, ws.LightPosY, ws.LightPosZ);
        }

        private void SetCurrentLightPosition(Vector3 worldPosition, bool commitUiRefresh)
        {
            var ws = _document.VoxelWorkspace;
            var clamped = ClampLightPositionToModelNeighborhood(worldPosition);
            ws.HasState = true;
            ws.LightPosX = clamped.X;
            ws.LightPosY = clamped.Y;
            ws.LightPosZ = clamped.Z;

            _pixelPreviewSpriteCache = null;
            // During drag we avoid syncing NumberBoxes every pointer tick (commitUiRefresh=false) and
            // perform one UI sync at the end of the drag for smoother interaction.
            if (commitUiRefresh)
                SyncLightingControlsFromDocument();
            RenderViewport();
        }

        private float GetCameraDepthForWorldPoint(Vector3 worldPoint)
        {
            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            return Vector3.Dot(worldPoint - pose.Position, basis.Forward);
        }

        private bool TryMapHostPointToWorldAtCameraDepth(
            Windows.Foundation.Point hostPoint,
            float cameraDepth,
            out Vector3 world)
        {
            world = default;
            if (!TryMapHostPointToCameraViewport(hostPoint, out float viewportX, out float viewportY))
                return false;

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            var fr = _camera.GetFrustum();

            float viewportW = MathF.Max(1f, _camera.ViewportWidth);
            float viewportH = MathF.Max(1f, _camera.ViewportHeight);
            float xNdc = ((viewportX / viewportW) * 2f) - 1f;
            float yNdc = 1f - ((viewportY / viewportH) * 2f);
            float cx = ((xNdc * fr.Width) + (fr.Left + fr.Right)) * 0.5f;
            float cy = ((yNdc * fr.Height) + (fr.Top + fr.Bottom)) * 0.5f;

            world = pose.Position + (basis.Right * cx) + (basis.Up * cy) + (basis.Forward * cameraDepth);
            return true;
        }

        private bool TryProjectWorldToHostDip(Vector3 worldPoint, out Vector2 hostPoint, out float cameraDepth)
        {
            hostPoint = default;
            cameraDepth = 0f;

            if (!TryGetPresentedImageRectInHostDip(out var imageRect))
                return false;

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            var fr = _camera.GetFrustum();

            float viewportW = MathF.Max(1f, _camera.ViewportWidth);
            float viewportH = MathF.Max(1f, _camera.ViewportHeight);
            float frWidth = MathF.Max(1e-6f, fr.Width);
            float frHeight = MathF.Max(1e-6f, fr.Height);
            float frCenterX = (fr.Left + fr.Right) * 0.5f;
            float frCenterY = (fr.Top + fr.Bottom) * 0.5f;

            var rel = worldPoint - pose.Position;
            float cx = Vector3.Dot(rel, basis.Right);
            float cy = Vector3.Dot(rel, basis.Up);
            cameraDepth = Vector3.Dot(rel, basis.Forward);

            float xNdc = (2f * cx - 2f * frCenterX) / frWidth;
            float yNdc = (2f * cy - 2f * frCenterY) / frHeight;
            float camSx = (xNdc * 0.5f + 0.5f) * viewportW;
            float camSy = (1f - (yNdc * 0.5f + 0.5f)) * viewportH;

            double hostX = imageRect.X + (camSx / viewportW) * imageRect.Width;
            double hostY = imageRect.Y + (camSy / viewportH) * imageRect.Height;
            hostPoint = new Vector2((float)hostX, (float)hostY);
            return true;
        }

        private void UpdateLightHandleOverlay()
        {
            if (LightHandleVisual == null)
                return;

            if (_lastVolume == null || _lastVolume.OccupiedCount <= 0 || !_document.VoxelWorkspace.LightingEnabled || !IsViewportFocused())
            {
                _lightHandleHostDip = new Vector2(float.NaN, float.NaN);
                LightHandleVisual.Visibility = Visibility.Collapsed;
                return;
            }

            Vector3 lightPos = GetCurrentLightPosition();
            if (!TryProjectWorldToHostDip(lightPos, out var host, out var depth) || depth <= 0f)
            {
                _lightHandleHostDip = new Vector2(float.NaN, float.NaN);
                LightHandleVisual.Visibility = Visibility.Collapsed;
                return;
            }

            if (!TryGetPresentedImageRectInHostDip(out var imageRect))
            {
                _lightHandleHostDip = new Vector2(float.NaN, float.NaN);
                LightHandleVisual.Visibility = Visibility.Collapsed;
                return;
            }

            float handleHalfW = (float)(Math.Max(1.0, LightHandleVisual.Width) * 0.5);
            float handleHalfH = (float)(Math.Max(1.0, LightHandleVisual.Height) * 0.5);
            float left = (float)imageRect.X;
            float top = (float)imageRect.Y;
            float right = (float)(imageRect.X + imageRect.Width);
            float bottom = (float)(imageRect.Y + imageRect.Height);

            bool fullyInsideImageRect =
                host.X - handleHalfW >= left &&
                host.X + handleHalfW <= right &&
                host.Y - handleHalfH >= top &&
                host.Y + handleHalfH <= bottom;

            if (!fullyInsideImageRect)
            {
                _lightHandleHostDip = new Vector2(float.NaN, float.NaN);
                LightHandleVisual.Visibility = Visibility.Collapsed;
                return;
            }

            _lightHandleHostDip = host;
            LightHandleVisual.Visibility = Visibility.Visible;
            Canvas.SetLeft(LightHandleVisual, host.X - ((float)LightHandleVisual.Width * 0.5f));
            Canvas.SetTop(LightHandleVisual, host.Y - ((float)LightHandleVisual.Height * 0.5f));

            if (LightHandleCore != null || LightHandleRing != null)
            {
                var lightColor = BgraToColor(_document.VoxelWorkspace.LightColorBgra);
                var rayStroke = new SolidColorBrush(lightColor);
                var coreFill = new SolidColorBrush(lightColor);

                if (LightHandleCore != null)
                    LightHandleCore.Fill = coreFill;
                if (LightHandleRing != null)
                    LightHandleRing.Stroke = rayStroke;

                if (LightHandleVisual.Children != null)
                {
                    foreach (var child in LightHandleVisual.Children)
                    {
                        if (child is Line line)
                            line.Stroke = rayStroke;
                    }
                }
            }
        }

        private bool TryBeginLightHandleDrag(Windows.Foundation.Point hostPoint)
        {
            if (_lastVolume == null || _lastVolume.OccupiedCount <= 0 || !_document.VoxelWorkspace.LightingEnabled)
                return false;
            if (float.IsNaN(_lightHandleHostDip.X) || float.IsNaN(_lightHandleHostDip.Y))
                return false;

            float dist = Vector2.Distance(_lightHandleHostDip, new Vector2((float)hostPoint.X, (float)hostPoint.Y));
            if (dist > LightHandleHitRadiusDip)
                return false;

            _lightDragCameraDepth = GetCameraDepthForWorldPoint(GetCurrentLightPosition());
            return true;
        }

        private void UpdateLightHandleDrag(Windows.Foundation.Point hostPoint)
        {
            if (!TryMapHostPointToWorldAtCameraDepth(hostPoint, _lightDragCameraDepth, out var world))
                return;

            SetCurrentLightPosition(world, commitUiRefresh: false);
        }

        private void EndLightHandleDrag()
        {
            SyncLightingControlsFromDocument();
            _lightDragCameraDepth = 0f;
        }

        private void NudgeLightPosition(Vector3 delta)
        {
            var current = GetCurrentLightPosition();
            SetCurrentLightPosition(current + delta, commitUiRefresh: true);
        }

        private static Windows.UI.Color BgraToColor(uint bgra)
        {
            byte b = (byte)(bgra & 0xFF);
            byte g = (byte)((bgra >> 8) & 0xFF);
            byte r = (byte)((bgra >> 16) & 0xFF);
            byte a = (byte)((bgra >> 24) & 0xFF);
            return Windows.UI.Color.FromArgb(a, r, g, b);
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

            if (ViewportHost != null)
            {
                _ = ViewportHost.Focus(FocusState.Programmatic);
            }

            var point = e.GetCurrentPoint(inputTarget);
            var props = point.Properties;
            _lastPointerPos = point.Position;
            _lastStrokePaintFace = null;
            string? activeToolId = _voxelToolState.ActiveToolId;
            bool canUseVoxelTool =
                (!string.IsNullOrWhiteSpace(activeToolId)) &&
                IsVoxelClickToolActive(activeToolId);
            bool activeToolHandlesRightClick = _voxelToolState.ActiveRegistration?.Behavior?.HandlesRightClick == true;

            bool forceOrbit = props.IsMiddleButtonPressed ||
                              (props.IsRightButtonPressed && (!activeToolHandlesRightClick || !canUseVoxelTool));

            if (props.IsLeftButtonPressed && TryBeginLightHandleDrag(point.Position))
            {
                _pointerDragMode = PointerDragMode.LightHandle;
            }
            else if (forceOrbit)
            {
                _pointerDragMode = PointerDragMode.Orbit;
                _camera.BeginDrag();
            }
            else if ((props.IsLeftButtonPressed || (props.IsRightButtonPressed && activeToolHandlesRightClick)) &&
                     canUseVoxelTool)
            {
                _pointerDragMode = PointerDragMode.FacePaintStroke;
                TryApplyActiveVoxelToolAtHostPoint(point.Position, props, ToolPointerPhase.Pressed);
            }
            else
            {
                _pointerDragMode = PointerDragMode.Orbit;
                _camera.BeginDrag();
            }

            ((UIElement)sender).CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void ViewportHost_GotFocus(object sender, RoutedEventArgs e)
        {
            UpdateLightingQuickActionsState();
            UpdateLightHandleOverlay();
        }

        private void ViewportHost_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateLightingQuickActionsState();
            UpdateLightHandleOverlay();
        }

        private void Viewport_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            UIElement? inputTarget = (UIElement?)ViewportHost ?? ViewportImage;
            if (inputTarget == null) return;

            var current = e.GetCurrentPoint(inputTarget);
            var pos = current.Position;
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

            if (_pointerDragMode == PointerDragMode.FacePaintStroke)
            {
                _lastPointerPos = pos;
                var props = current.Properties;
                if (props.IsLeftButtonPressed || props.IsRightButtonPressed)
                {
                    TryApplyActiveVoxelToolAtHostPoint(pos, props, ToolPointerPhase.Moved);
                }
                else
                {
                    _pointerDragMode = PointerDragMode.None;
                    _lastStrokePaintFace = null;
                }

                e.Handled = true;
                return;
            }

            if (_pointerDragMode == PointerDragMode.LightHandle)
            {
                _lastPointerPos = pos;
                if (current.Properties.IsLeftButtonPressed)
                {
                    UpdateLightHandleDrag(pos);
                }
                else
                {
                    EndLightHandleDrag();
                    _pointerDragMode = PointerDragMode.None;
                }

                e.Handled = true;
                return;
            }

            _lastPointerPos = pos;
        }

        private void Viewport_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            UIElement? inputTarget = (UIElement?)ViewportHost ?? ViewportImage;

            if (_pointerDragMode == PointerDragMode.FacePaintStroke && inputTarget != null)
            {
                var released = e.GetCurrentPoint(inputTarget);
                TryApplyActiveVoxelToolAtHostPoint(released.Position, released.Properties, ToolPointerPhase.Released);
            }

            if (_pointerDragMode == PointerDragMode.Orbit)
            {
                _camera.EndDrag();
            }
            else if (_pointerDragMode == PointerDragMode.LightHandle)
            {
                EndLightHandleDrag();
            }

            _pointerDragMode = PointerDragMode.None;
            _lastStrokePaintFace = null;
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        private void ViewportHost_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            bool ctrl = IsKeyDown(VirtualKey.Control);
            bool shift = IsKeyDown(VirtualKey.Shift);
            bool alt = IsKeyDown(VirtualKey.Menu);

            if (alt)
            {
                float step = shift ? 5f : 1f;
                Vector3 lightDelta = e.Key switch
                {
                    VirtualKey.Left => new Vector3(-step, 0f, 0f),
                    VirtualKey.Right => new Vector3(step, 0f, 0f),
                    VirtualKey.Up => new Vector3(0f, step, 0f),
                    VirtualKey.Down => new Vector3(0f, -step, 0f),
                    VirtualKey.PageUp => new Vector3(0f, 0f, step),
                    VirtualKey.PageDown => new Vector3(0f, 0f, -step),
                    _ => default
                };

                if (lightDelta != default)
                {
                    NudgeLightPosition(lightDelta);
                    e.Handled = true;
                    return;
                }
            }

            if (ctrl && e.Key == VirtualKey.Z)
            {
                bool redid = shift ? _editEngine.Redo() : _editEngine.Undo();
                if (redid)
                {
                    UpdateVoxelSelectionStatusText(shift ? "Redo voxel edit." : "Undo voxel edit.");
                }
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == VirtualKey.Y)
            {
                if (_editEngine.Redo())
                {
                    UpdateVoxelSelectionStatusText("Redo voxel edit.");
                }
                e.Handled = true;
                return;
            }

            if (_editEngine.Selection.Count == 0)
                return;

            Int3 delta = e.Key switch
            {
                VirtualKey.Left => new Int3(-1, 0, 0),
                VirtualKey.Right => new Int3(1, 0, 0),
                VirtualKey.Up => new Int3(0, 1, 0),
                VirtualKey.Down => new Int3(0, -1, 0),
                VirtualKey.PageUp => new Int3(0, 0, 1),
                VirtualKey.PageDown => new Int3(0, 0, -1),
                _ => default
            };

            if (delta != default)
            {
                if (_editEngine.MoveSelection(delta))
                {
                    UpdateVoxelSelectionStatusText($"Moved selection by ({delta.X},{delta.Y},{delta.Z}).");
                }
                else
                {
                    UpdateVoxelSelectionStatusText($"Move blocked for delta ({delta.X},{delta.Y},{delta.Z}).");
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Delete)
            {
                DeleteSelectedVoxels();
                e.Handled = true;
            }
        }

        private void Viewport_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint((UIElement)sender).Properties;
            float delta = props.MouseWheelDelta / 120f;
            AdjustZoomForCurrentMode(delta);
            RenderViewport();
            e.Handled = true;
        }

        private void DeleteSelectedVoxels()
        {
            var selected = _editEngine.Selection.ToArray();
            if (selected.Length == 0)
                return;

            _editEngine.BeginHistoryTransaction("Delete Selected Voxels");
            bool any = false;
            try
            {
                for (int i = 0; i < selected.Length; i++)
                {
                    var s = selected[i];
                    any |= _editEngine.DeleteVoxel(s.X, s.Y, s.Z);
                }

                if (any)
                {
                    _editEngine.ClearSelection();
                    _editEngine.CommitHistoryTransaction();
                    UpdateVoxelSelectionStatusText("Deleted selected voxels.");
                }
                else
                {
                    _editEngine.CancelHistoryTransaction();
                    UpdateVoxelSelectionStatusText("Delete selected voxels: nothing changed.");
                }
            }
            catch
            {
                _editEngine.CancelHistoryTransaction();
                throw;
            }
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

        private void ApplyIsoPreset_Click(object sender, RoutedEventArgs e)
        {
            var viewName = GetPresetTagFromCombo(IsoPresetCombo);
            if (string.IsNullOrWhiteSpace(viewName))
                return;

            _camera.SetView(viewName);
            StartAnimationLoop();
        }

        private void ApplyCardinalPreset_Click(object sender, RoutedEventArgs e)
        {
            var viewName = GetPresetTagFromCombo(CardinalPresetCombo);
            if (string.IsNullOrWhiteSpace(viewName))
                return;

            _camera.SetView(viewName);
            StartAnimationLoop();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _camera.Reset();
            RenderViewport();
        }

        private void FocusLightButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_document.VoxelWorkspace.LightingEnabled)
                return;

            if (!TryGetCameraFacingLightSpawnPosition(out var spawn) &&
                !TryGetRecommendedLightSpawnPosition(out spawn))
            {
                return;
            }

            SetCurrentLightPosition(spawn, commitUiRefresh: true);
            FlushPendingLightingHistory();
        }

        private void ResetLightButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_document.VoxelWorkspace.LightingEnabled)
                return;

            if (!TryGetRecommendedLightSpawnPosition(out var spawn))
                return;

            SetCurrentLightPosition(spawn, commitUiRefresh: true);
            FlushPendingLightingHistory();
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

        private static string? GetPresetTagFromCombo(ComboBox? combo)
        {
            if (combo?.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
                return tag;
            return null;
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

            int spriteSide = ComputePixelPreviewSpriteSide(volume.Size, opts.OutlineSize);
            EnsurePixelPreviewSpriteCache(volume, spriteSide, opts);
            if (_pixelPreviewSpriteCache == null) return false;

            bool cacheValid =
                MathF.Abs(_pixelPreviewSpriteCache.Pitch - _camera.Pitch) <= 1e-6f &&
                MathF.Abs(_pixelPreviewSpriteCache.Yaw - _camera.Yaw) <= 1e-6f &&
                string.Equals(_pixelPreviewSpriteCache.SnapName, _camera.CurrentSnapName, StringComparison.OrdinalIgnoreCase) &&
                _pixelPreviewSpriteCache.DrawOutline == opts.DrawOutline &&
                _pixelPreviewSpriteCache.OutlineColor == opts.OutlineColor &&
                _pixelPreviewSpriteCache.OutlineSize == opts.OutlineSize &&
                _pixelPreviewSpriteCache.DrawSurfaceVoxelGrid == opts.DrawSurfaceVoxelGrid &&
                _pixelPreviewSpriteCache.SurfaceVoxelGridColor == opts.SurfaceVoxelGridColor &&
                _pixelPreviewSpriteCache.LightingEnabled == opts.LightingEnabled &&
                NearlyEqual(_pixelPreviewSpriteCache.LightPosX, opts.LightPosition.X) &&
                NearlyEqual(_pixelPreviewSpriteCache.LightPosY, opts.LightPosition.Y) &&
                NearlyEqual(_pixelPreviewSpriteCache.LightPosZ, opts.LightPosition.Z) &&
                _pixelPreviewSpriteCache.LightColor == opts.LightColor &&
                _pixelPreviewSpriteCache.ShadowColor == opts.ShadowColor &&
                NearlyEqual(_pixelPreviewSpriteCache.ShadowStrength, opts.ShadowStrength) &&
                NearlyEqual(_pixelPreviewSpriteCache.LightIntensity, opts.LightIntensity) &&
                NearlyEqual(_pixelPreviewSpriteCache.AmbientIntensity, opts.AmbientIntensity) &&
                NearlyEqual(_pixelPreviewSpriteCache.LightFalloff, opts.LightFalloff) &&
                _pixelPreviewSpriteCache.LightCastShadows == opts.LightCastShadows;

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
                _pixelPreviewSpriteCache.DrawSurfaceVoxelGrid = opts.DrawSurfaceVoxelGrid;
                _pixelPreviewSpriteCache.SurfaceVoxelGridColor = opts.SurfaceVoxelGridColor;
                _pixelPreviewSpriteCache.LightingEnabled = opts.LightingEnabled;
                _pixelPreviewSpriteCache.LightPosX = opts.LightPosition.X;
                _pixelPreviewSpriteCache.LightPosY = opts.LightPosition.Y;
                _pixelPreviewSpriteCache.LightPosZ = opts.LightPosition.Z;
                _pixelPreviewSpriteCache.LightColor = opts.LightColor;
                _pixelPreviewSpriteCache.ShadowColor = opts.ShadowColor;
                _pixelPreviewSpriteCache.ShadowStrength = opts.ShadowStrength;
                _pixelPreviewSpriteCache.LightIntensity = opts.LightIntensity;
                _pixelPreviewSpriteCache.AmbientIntensity = opts.AmbientIntensity;
                _pixelPreviewSpriteCache.LightFalloff = opts.LightFalloff;
                _pixelPreviewSpriteCache.LightCastShadows = opts.LightCastShadows;
            }

            _camera.EnablePixelPerfectFrustum(renderW, renderH);
            _camera.ResizeViewport(renderW, renderH);

            FillClear(buffer, renderW * renderH, clearColor);
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
                    DrawSurfaceVoxelGrid = opts.DrawSurfaceVoxelGrid,
                    SurfaceVoxelGridColor = opts.SurfaceVoxelGridColor,
                    LightingEnabled = opts.LightingEnabled,
                    LightPosX = opts.LightPosition.X,
                    LightPosY = opts.LightPosition.Y,
                    LightPosZ = opts.LightPosition.Z,
                    LightColor = opts.LightColor,
                    ShadowColor = opts.ShadowColor,
                    ShadowStrength = opts.ShadowStrength,
                    LightIntensity = opts.LightIntensity,
                    AmbientIntensity = opts.AmbientIntensity,
                    LightFalloff = opts.LightFalloff,
                    LightCastShadows = opts.LightCastShadows,
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
                DrawBackdropProjectionTiles = false,
                BackdropGridMinorColor = src.BackdropGridMinorColor,
                BackdropGridMajorColor = src.BackdropGridMajorColor,
                BackdropGridMajorEvery = src.BackdropGridMajorEvery,
                BackdropGridMarginVoxels = src.BackdropGridMarginVoxels,
                BackdropCageScale = src.BackdropCageScale,
                BackdropFrontProjection = null,
                BackdropBackProjection = null,
                BackdropLeftProjection = null,
                BackdropRightProjection = null,
                BackdropTopProjection = null,
                BackdropBottomProjection = null,
                DrawSurfaceVoxelGrid = src.DrawSurfaceVoxelGrid,
                SurfaceVoxelGridColor = src.SurfaceVoxelGridColor,
                LightingEnabled = src.LightingEnabled,
                LightPosition = src.LightPosition,
                LightColor = src.LightColor,
                ShadowColor = src.ShadowColor,
                ShadowStrength = src.ShadowStrength,
                LightIntensity = src.LightIntensity,
                AmbientIntensity = src.AmbientIntensity,
                LightFalloff = src.LightFalloff,
                LightCastShadows = src.LightCastShadows,
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

            // Keep cardinal pixel-preview on the exact image path even with lighting enabled.
            // This avoids low-res triangle-raster cracks when lighting is toggled on.

            FillClear(buffer, renderW * renderH, clearColor);

            int size = img.Width;
            int startX = (renderW - size) / 2;
            int startY = (renderH - size) / 2;

            bool needMask = opts.DrawOutline || opts.DrawSurfaceVoxelGrid;
            bool[]? objectMask = needMask ? new bool[renderW * renderH] : null;

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

            if (opts.DrawOutline && objectMask != null && opts.OutlineSize > 0)
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

            // Cardinal views are generated explicitly so they match the volume colors
            // (6-face overrides included) without going through triangle rasterization.
            image = new ImageData(size, size);

            static int Flip(int v, int n) => n - 1 - v;

            if (snapName.Equals("front", StringComparison.OrdinalIgnoreCase))
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

            if (snapName.Equals("back", StringComparison.OrdinalIgnoreCase))
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

            if (snapName.Equals("left", StringComparison.OrdinalIgnoreCase))
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

            if (snapName.Equals("right", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Left;
                for (int y = 0; y < size; y++)
                {
                    int yFlip = Flip(y, size);
                    for (int z = 0; z < size; z++)
                    {
                        for (int x = 0; x < size; x++) // camera at -X → nearest is lowest x
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = z;
                            int row = yFlip;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Left));
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
                            int col = x;
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
            if (!opts.LightingEnabled)
                return c;

            var normal = face switch
            {
                Face.Front => new Vector3(0f, 0f, -1f),
                Face.Back => new Vector3(0f, 0f, 1f),
                Face.Left => new Vector3(-1f, 0f, 0f),
                Face.Right => new Vector3(1f, 0f, 0f),
                Face.Top => new Vector3(0f, 1f, 0f),
                Face.Bottom => new Vector3(0f, -1f, 0f),
                _ => new Vector3(0f, 0f, -1f),
            };

            Vector3 toLight = opts.LightPosition;
            float distSq = MathF.Max(1e-6f, toLight.LengthSquared());
            float invDist = 1f / MathF.Sqrt(distSq);
            float dist = distSq * invDist;
            Vector3 lightDir = toLight * invDist;

            float ndotl = MathF.Max(0f, Vector3.Dot(normal, lightDir));
            float attenuation = 1f / (1f + MathF.Max(0f, opts.LightFalloff) * dist);
            float diffuse = ndotl * MathF.Max(0f, opts.LightIntensity) * attenuation;

            float lr = ((opts.LightColor >> 16) & 0xFF) / 255f;
            float lg = ((opts.LightColor >> 8) & 0xFF) / 255f;
            float lb = (opts.LightColor & 0xFF) / 255f;
            float shadowR = ((opts.ShadowColor >> 16) & 0xFF) / 255f;
            float shadowG = ((opts.ShadowColor >> 8) & 0xFF) / 255f;
            float shadowB = (opts.ShadowColor & 0xFF) / 255f;
            float shadowA = ((opts.ShadowColor >> 24) & 0xFF) / 255f;
            float litScalar = Math.Clamp(diffuse, 0f, 1f);
            float shadowStrength = Math.Clamp(opts.ShadowStrength, 0f, 1f);
            float shadowMix = (1f - litScalar) * shadowA * shadowStrength;

            float litR = c.R * (diffuse * lr);
            float litG = c.G * (diffuse * lg);
            float litB = c.B * (diffuse * lb);
            float outR = litR * (1f - shadowMix) + (shadowR * 255f * shadowMix);
            float outG = litG * (1f - shadowMix) + (shadowG * 255f * shadowMix);
            float outB = litB * (1f - shadowMix) + (shadowB * 255f * shadowMix);

            return new Rgba32(
                ClampToByte(outR),
                ClampToByte(outG),
                ClampToByte(outB),
                c.A);
        }

        private static void AlphaBlendPackedBgra(byte[] buffer, int bi, uint bgra)
        {
            byte srcB = (byte)(bgra & 0xFF);
            byte srcG = (byte)((bgra >> 8) & 0xFF);
            byte srcR = (byte)((bgra >> 16) & 0xFF);
            byte srcA = (byte)((bgra >> 24) & 0xFF);
            if (srcA == 0) return;

            if (srcA == 255)
            {
                buffer[bi] = srcB;
                buffer[bi + 1] = srcG;
                buffer[bi + 2] = srcR;
                buffer[bi + 3] = 255;
                return;
            }

            int invA = 255 - srcA;
            buffer[bi] = (byte)((srcB * srcA + buffer[bi] * invA) / 255);
            buffer[bi + 1] = (byte)((srcG * srcA + buffer[bi + 1] * invA) / 255);
            buffer[bi + 2] = (byte)((srcR * srcA + buffer[bi + 2] * invA) / 255);
            buffer[bi + 3] = 255;
        }

        private static byte ClampToByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 255f) return 255;
            return (byte)MathF.Round(v);
        }

        private static bool NearlyEqual(float a, float b)
            => MathF.Abs(a - b) <= 0.0001f;

        private static bool NearlyEqual(double a, double b)
            => Math.Abs(a - b) <= 0.0001d;

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

        private void DrawPixelPreviewBackdropCage3D(
            byte[] buffer,
            int width,
            int height,
            int renderW,
            int renderH,
            int screenPixelSize,
            int volumeSize,
            uint minorColor,
            uint majorColor,
            int majorEvery,
            float cageScale)
        {
            if (buffer == null || buffer.Length < width * height * 4) return;
            if (width <= 0 || height <= 0 || renderW <= 0 || renderH <= 0) return;
            if (volumeSize <= 0 || screenPixelSize <= 0) return;

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            var fr = _camera.GetFrustum();
            float frWidth = MathF.Max(1e-6f, fr.Width);
            float frHeight = MathF.Max(1e-6f, fr.Height);
            float frCenterX = (fr.Left + fr.Right) * 0.5f;
            float frCenterY = (fr.Top + fr.Bottom) * 0.5f;

            Vector3 ProjectToDisplay(Vector3 world)
            {
                var rel = world - pose.Position;
                float cx = Vector3.Dot(rel, basis.Right);
                float cy = Vector3.Dot(rel, basis.Up);
                float cz = Vector3.Dot(rel, basis.Forward);
                float xNdc = (2f * cx - 2f * frCenterX) / frWidth;
                float yNdc = (2f * cy - 2f * frCenterY) / frHeight;
                float sxRender = (xNdc * 0.5f + 0.5f) * renderW;
                float syRender = (1f - (yNdc * 0.5f + 0.5f)) * renderH;
                return new Vector3(sxRender * screenPixelSize, syRender * screenPixelSize, cz);
            }

            void DrawLine(Vector3 a, Vector3 b, uint color)
            {
                if (a.Z <= 0f && b.Z <= 0f) return;
                RasterizePackedBgraLine(buffer, width, height, a.X, a.Y, b.X, b.Y, color);
            }

            void DrawGridPlane(Vector3 center, Vector3 axisA, Vector3 axisB, int extentA, int extentB)
            {
                float gridPhase = (volumeSize & 1) == 0 ? 0f : 0.5f;
                for (int a = -extentA; a <= extentA; a++)
                {
                    bool major = majorEvery > 0 && (Math.Abs(a) % majorEvery) == 0;
                    uint color = major ? majorColor : minorColor;
                    float aPos = a + gridPhase;
                    var p0 = ProjectToDisplay(center + (axisA * aPos) + (axisB * (-extentB + gridPhase)));
                    var p1 = ProjectToDisplay(center + (axisA * aPos) + (axisB * (extentB + gridPhase)));
                    DrawLine(p0, p1, color);
                }

                for (int b = -extentB; b <= extentB; b++)
                {
                    bool major = majorEvery > 0 && (Math.Abs(b) % majorEvery) == 0;
                    uint color = major ? majorColor : minorColor;
                    float bPos = b + gridPhase;
                    var p0 = ProjectToDisplay(center + (axisB * bPos) + (axisA * (-extentA + gridPhase)));
                    var p1 = ProjectToDisplay(center + (axisB * bPos) + (axisA * (extentA + gridPhase)));
                    DrawLine(p0, p1, color);
                }
            }

            float modelHalf = MathF.Max(0.5f, volumeSize * 0.5f);
            float cageHalf = MathF.Max(modelHalf + 1f, modelHalf * MathF.Max(1.05f, cageScale));
            int extent = Math.Max(1, (int)MathF.Round(cageHalf));
            majorEvery = Math.Max(0, majorEvery);
            bool farXPositive = basis.Forward.X >= 0f;
            bool farYPositive = basis.Forward.Y >= 0f;
            bool farZPositive = basis.Forward.Z >= 0f;

            DrawGridPlane(new Vector3(0f, 0f, farZPositive ? cageHalf : -cageHalf), Vector3.UnitX, Vector3.UnitY, extent, extent);
            DrawGridPlane(new Vector3(farXPositive ? cageHalf : -cageHalf, 0f, 0f), Vector3.UnitZ, Vector3.UnitY, extent, extent);
            DrawGridPlane(new Vector3(0f, farYPositive ? cageHalf : -cageHalf, 0f), Vector3.UnitX, Vector3.UnitZ, extent, extent);
        }

        private static void DrawExactCardinalPixelPreviewSurfaceGrid(
            byte[] renderBuffer,
            int renderW,
            int renderH,
            byte[] displayBuffer,
            int displayW,
            int displayH,
            int screenPixelSize,
            uint gridColor)
        {
            if (screenPixelSize <= 1) return;
            if (renderBuffer == null || displayBuffer == null) return;
            if (renderBuffer.Length < renderW * renderH * 4) return;
            if (displayBuffer.Length < displayW * displayH * 4) return;

            static bool IsOpaque(byte[] buf, int w, int x, int y)
                => buf[((y * w + x) * 4) + 3] != 0;

            for (int y = 0; y < renderH; y++)
            {
                for (int x = 0; x < renderW; x++)
                {
                    if (!IsOpaque(renderBuffer, renderW, x, y))
                        continue;

                    int baseX = x * screenPixelSize;
                    int baseY = y * screenPixelSize;

                    if (x + 1 < renderW && IsOpaque(renderBuffer, renderW, x + 1, y))
                    {
                        int lineX = baseX + screenPixelSize - 1;
                        for (int py = 0; py < screenPixelSize; py++)
                        {
                            int dy = baseY + py;
                            if ((uint)lineX >= (uint)displayW || (uint)dy >= (uint)displayH) continue;
                            AlphaBlendPackedBgra(displayBuffer, (dy * displayW + lineX) * 4, gridColor);
                        }
                    }

                    if (y + 1 < renderH && IsOpaque(renderBuffer, renderW, x, y + 1))
                    {
                        int lineY = baseY + screenPixelSize - 1;
                        for (int px = 0; px < screenPixelSize; px++)
                        {
                            int dx = baseX + px;
                            if ((uint)dx >= (uint)displayW || (uint)lineY >= (uint)displayH) continue;
                            AlphaBlendPackedBgra(displayBuffer, (lineY * displayW + dx) * 4, gridColor);
                        }
                    }
                }
            }
        }

        private static void RasterizePackedBgraLine(
            byte[] buffer, int width, int height,
            float x0, float y0, float x1, float y1,
            uint color)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            int steps = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(dx), MathF.Abs(dy))));
            float ix = dx / steps;
            float iy = dy / steps;
            float px = x0;
            float py = y0;

            for (int i = 0; i <= steps; i++)
            {
                int sx = (int)MathF.Round(px);
                int sy = (int)MathF.Round(py);
                if ((uint)sx < (uint)width && (uint)sy < (uint)height)
                {
                    AlphaBlendPackedBgra(buffer, (sy * width + sx) * 4, color);
                }

                px += ix;
                py += iy;
            }
        }

        private void UpdateCameraStatsText(bool pixelMode, int screenPixelSize, int renderW, int renderH)
        {
            if (CameraStatsText == null) return;

            float pitchDeg = _camera.Pitch * (180f / MathF.PI);
            float yawDeg = _camera.Yaw * (180f / MathF.PI);
            if (yawDeg < 0f) yawDeg += 360f;
            int pitchDegInt = (int)MathF.Round(pitchDeg);
            int yawDegInt = ((int)MathF.Round(yawDeg)) % 360;
            if (yawDegInt < 0) yawDegInt += 360;
            string snap = _camera.CurrentSnapName ?? "custom";
            float voxPx = 0f;

            if (pixelMode && _lastVolume != null)
            {
                int pixelBaseSize = GetPixelPreviewBaseSize();
                float effectiveZoom = (screenPixelSize * 100f) / Math.Max(1, pixelBaseSize);
                voxPx = screenPixelSize;
                CameraStatsText.Text =
                    $"p {pitchDegInt,4:0}  y {yawDegInt,4:0}  z {effectiveZoom,5:0.#}%  vpx {voxPx,4:0.#}  b {pixelBaseSize}  rt {renderW}x{renderH}  {snap}";
            }
            else
            {
                var fr = _camera.GetFrustum();
                voxPx = renderH / MathF.Max(1e-6f, fr.Height);
                CameraStatsText.Text =
                    $"p {pitchDegInt,4:0}  y {yawDegInt,4:0}  z {_camera.ZoomPercent,5:0.#}%  vpx {voxPx,4:0.#}  {snap}";
            }
        }

        private void UpdateLightingQuickActionsState()
        {
            bool enabled = _document.VoxelWorkspace.LightingEnabled && _hasOccupiedBounds;

            if (FocusLightButton != null)
            {
                FocusLightButton.IsEnabled = enabled;
                FocusLightButton.Opacity = enabled ? 1.0 : 0.6;
            }

            if (ResetLightButton != null)
            {
                ResetLightButton.IsEnabled = enabled;
                ResetLightButton.Opacity = enabled ? 1.0 : 0.6;
            }
        }

        private void UpdateAxisGizmo()
        {
            if (AxisXLine == null || AxisYLine == null || AxisZLine == null ||
                AxisXLabel == null || AxisYLabel == null || AxisZLabel == null)
            {
                return;
            }

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);

            const float center = 42f;
            const float axisLen = 22f;

            static (float cx, float cy, float cz) ProjectAxis(Vector3 axis, OrbitCamera.CameraBasis basis)
            {
                // Match the intended viewport triad look:
                // - horizontal mirror so +X appears on the expected side,
                // - invert vertical only for X/Z so SW view reads as Y up,
                //   X down-right, Z down-left (Maya-like visual cue).
                float cx = -Vector3.Dot(axis, basis.Right);
                float cy = Vector3.Dot(axis, basis.Up);
                if (MathF.Abs(axis.Y) < 0.5f)
                    cy = -cy;
                float cz = Vector3.Dot(axis, basis.Forward);
                return (cx, cy, cz);
            }

            void SetAxis(Line line, TextBlock label, Vector3 axis, bool placeLabelAtTip)
            {
                var (cx, cy, cz) = ProjectAxis(axis, basis);
                float ex = center + cx * axisLen;
                float ey = center - cy * axisLen;

                line.X1 = center;
                line.Y1 = center;
                line.X2 = ex;
                line.Y2 = ey;
                line.Opacity = 0.5 + 0.5 * Math.Clamp((double)(cz * 0.5f + 0.5f), 0.0, 1.0);

                if (placeLabelAtTip)
                {
                    // Place label along the axis direction so it sits at the line tip,
                    // not laterally offset to screen-right.
                    float vx = ex - center;
                    float vy = ey - center;
                    float vLen = MathF.Sqrt(vx * vx + vy * vy);
                    if (vLen < 1e-5f) vLen = 1f;
                    float ux = vx / vLen;
                    float uy = vy / vLen;
                    const float tipGap = 7f;
                    float lx = ex + ux * tipGap;
                    float ly = ey + uy * tipGap;

                    label.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                    float lw = (float)label.DesiredSize.Width;
                    float lh = (float)label.DesiredSize.Height;
                    Canvas.SetLeft(label, lx - lw * 0.5f);
                    Canvas.SetTop(label, ly - lh * 0.5f);
                }
                else
                {
                    Canvas.SetLeft(label, ex + 2f);
                    Canvas.SetTop(label, ey - 8f);
                }
                label.Opacity = line.Opacity;
            }

            SetAxis(AxisXLine, AxisXLabel, Vector3.UnitX, placeLabelAtTip: true);
            SetAxis(AxisYLine, AxisYLabel, Vector3.UnitY, placeLabelAtTip: true);
            SetAxis(AxisZLine, AxisZLabel, Vector3.UnitZ, placeLabelAtTip: true);
            _axisXEnd = new Vector2((float)AxisXLine.X2, (float)AxisXLine.Y2);
            _axisYEnd = new Vector2((float)AxisYLine.X2, (float)AxisYLine.Y2);
            _axisZEnd = new Vector2((float)AxisZLine.X2, (float)AxisZLine.Y2);
        }

        private void AxisGizmoCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (AxisGizmoCanvas == null)
                return;

            var p = e.GetCurrentPoint(AxisGizmoCanvas).Position;
            var point = new Vector2((float)p.X, (float)p.Y);

            const float hitRadius = 16f;
            string? target = ResolveAxisClickTarget(point, hitRadius);
            if (string.IsNullOrWhiteSpace(target))
                return;

            _camera.SetView(target);
            StartAnimationLoop();
            e.Handled = true;
        }

        private string? ResolveAxisClickTarget(Vector2 point, float hitRadius)
        {
            float dX = Vector2.Distance(point, _axisXEnd);
            float dY = Vector2.Distance(point, _axisYEnd);
            float dZ = Vector2.Distance(point, _axisZEnd);

            float min = MathF.Min(dX, MathF.Min(dY, dZ));
            if (min > hitRadius)
                return null;

            if (min == dX)
            {
                return ToggleTarget(_camera.CurrentSnapName, positive: "left", negative: "right");
            }
            if (min == dY)
            {
                return ToggleTarget(_camera.CurrentSnapName, positive: "top", negative: "bottom");
            }

            return ToggleTarget(_camera.CurrentSnapName, positive: "front", negative: "back");
        }

        private static string ToggleTarget(string? currentSnap, string positive, string negative)
        {
            if (string.Equals(currentSnap, positive, StringComparison.OrdinalIgnoreCase))
                return negative;
            return positive;
        }

        // ════════════════════════════════════════════════════════════════════
        // EXPORT
        // ════════════════════════════════════════════════════════════════════

        private void SetExportButtonsEnabled(bool enabled)
        {
            if (ExportButton != null)
                ExportButton.IsEnabled = enabled;
            if (ExportModelButton != null)
                ExportModelButton.IsEnabled = enabled;
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastVolume == null || _lastVolume.OccupiedCount == 0) return;

            try
            {
                var options = await PromptImageExportOptionsAsync();
                if (options is null)
                    return;

                var ownerWindow = App.PixlPunktMainWindow;
                if (ownerWindow == null)
                    return;

                if (options.Value.BatchExportViews)
                {
                    await ExportImageBatchAsync(ownerWindow, options.Value);
                    return;
                }

                if (!TryBuildImageExportBuffer(options.Value, out var exportBuffer, out int exportWidth, out int exportHeight))
                    return;

                var savePicker = WindowHost.CreateFileSavePicker(ownerWindow, "voxel_preview", ".png");
                savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                WindowHost.TrySetDefaultFileExtension(savePicker, ".png");

                var file = await savePicker.PickSaveFileAsync();
                if (file == null) return;

                if (!string.IsNullOrWhiteSpace(file.Path))
                {
                    await SaveBgraPngAsync(file.Path, exportWidth, exportHeight, exportBuffer, options.Value.TransparentBackground);
                }
                else
                {
                    using var outStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
                    var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                        Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, outStream);
                    encoder.SetPixelData(
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        options.Value.TransparentBackground
                            ? Windows.Graphics.Imaging.BitmapAlphaMode.Straight
                            : Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                        (uint)exportWidth, (uint)exportHeight,
                        96, 96,
                        exportBuffer);
                    await encoder.FlushAsync();
                }

                LoggingService.Info("Voxel exported to {Path}", file.Path);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Voxel export failed", ex);
            }
        }

        private async void ExportModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastVolume == null || _lastVolume.OccupiedCount == 0)
                return;

            try
            {
                var options = await PromptModelExportOptionsAsync();
                if (options is null)
                    return;

                if (options.Value.Format == ModelExportFormat.Vox)
                {
                    var bounds = ComputeTransformedVoxelBounds(_lastVolume, options.Value.AxisPreset);
                    if (bounds.HasValue)
                    {
                        var b = bounds.Value;
                        if (b.SizeX > 255 || b.SizeY > 255 || b.SizeZ > 255)
                        {
                            var owner = App.PixlPunktMainWindow;
                            if (owner != null)
                            {
                                var warn = new ContentDialog
                                {
                                    XamlRoot = XamlRoot,
                                    Title = "VOX Export Limit",
                                    PrimaryButtonText = "OK",
                                    DefaultButton = ContentDialogButton.Primary,
                                    Content = $"VOX supports up to 255 units per axis. Current transformed size is {b.SizeX}x{b.SizeY}x{b.SizeZ}.",
                                };
                                await warn.ShowAsync();
                            }

                            return;
                        }
                    }
                }

                var ownerWindow = App.PixlPunktMainWindow;
                if (ownerWindow == null)
                    return;

                var extension = GetModelExportPrimaryExtension(options.Value.Format);
                var savePicker = WindowHost.CreateFileSavePicker(ownerWindow, "voxel_model", extension);
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                WindowHost.TrySetDefaultFileExtension(savePicker, extension);

                var outputFile = await savePicker.PickSaveFileAsync();
                if (outputFile == null)
                    return;

                var outputPath = outputFile.Path;
                if (string.IsNullOrWhiteSpace(outputPath))
                    return;

                var folder = System.IO.Path.GetDirectoryName(outputPath);
                if (string.IsNullOrWhiteSpace(folder))
                    return;

                var baseName = System.IO.Path.GetFileNameWithoutExtension(outputPath);
                if (string.IsNullOrWhiteSpace(baseName))
                    baseName = "voxel_model";

                switch (options.Value.Format)
                {
                    case ModelExportFormat.Glb:
                    {
                        var glbBytes = await BuildGlbExportAsync(_lastVolume, options.Value);
                        await System.IO.File.WriteAllBytesAsync(outputPath, glbBytes);
                        break;
                    }
                    case ModelExportFormat.Stl:
                    {
                        var stlBytes = BuildStlExport(_lastVolume, options.Value);
                        await System.IO.File.WriteAllBytesAsync(outputPath, stlBytes);
                        break;
                    }
                    case ModelExportFormat.Vox:
                    {
                        var voxBytes = BuildVoxExport(_lastVolume, options.Value);
                        await System.IO.File.WriteAllBytesAsync(outputPath, voxBytes);
                        break;
                    }
                    default:
                    {
                        // Keep external references OBJ/MTL-friendly (avoid spaces and odd tokens that some importers misread).
                        var refBaseName = SanitizeFileName(baseName).Replace(' ', '_');
                        if (string.IsNullOrWhiteSpace(refBaseName))
                            refBaseName = "voxel_model";

                        var mtlFileName = $"{refBaseName}.mtl";
                        var textureFileName = $"{refBaseName}.png";
                        var mtlPath = System.IO.Path.Combine(folder, mtlFileName);
                        var texturePath = System.IO.Path.Combine(folder, textureFileName);

                        var export = BuildObjExport(_lastVolume, mtlFileName, textureFileName, options.Value);

                        await System.IO.File.WriteAllTextAsync(outputPath, export.ObjText, Utf8NoBom);
                        await System.IO.File.WriteAllTextAsync(mtlPath, export.MtlText, Utf8NoBom);
                        await SaveBgraTextureToPngAsync(texturePath, export.TextureWidth, export.TextureHeight, export.TexturePixelsBgra);
                        break;
                    }
                }

                LoggingService.Info("Voxel model exported to {Path} as {Format}", outputPath, options.Value.Format);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Voxel model export failed", ex);
            }
        }

        private async Task ExportImageBatchAsync(Window ownerWindow, VoxelImageExportOptions options)
        {
            var folderPicker = WindowHost.CreateFolderPicker(ownerWindow, PickerLocationId.PicturesLibrary);
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null || string.IsNullOrWhiteSpace(folder.Path))
                return;

            var views = new List<string>(16);
            if (options.BatchIncludeCardinalViews)
                views.AddRange(BatchCardinalViewNames);
            if (options.BatchIncludeDirectionalViews)
                views.AddRange(BatchDirectionalViewNames);
            if (views.Count == 0)
                return;

            float oldPitch = _camera.Pitch;
            float oldYaw = _camera.Yaw;
            float oldZoom = _camera.ZoomPercent;
            string? oldSnap = _camera.CurrentSnapName;

            string rawBaseName = string.IsNullOrWhiteSpace(_document.Name) ? "voxel_preview" : _document.Name;
            string safeBaseName = SanitizeFileName(rawBaseName);

            try
            {
                foreach (var view in views)
                {
                    _camera.SetView(view, animated: false);
                    _camera.SetZoomPercent(oldZoom);

                    if (!TryBuildImageExportBuffer(options, out var exportBuffer, out int exportWidth, out int exportHeight))
                        continue;

                    string safeView = SanitizeFileName(view);
                    string fileName = $"{safeBaseName}_{safeView}.png";
                    string path = System.IO.Path.Combine(folder.Path, fileName);
                    await SaveBgraPngAsync(path, exportWidth, exportHeight, exportBuffer, options.TransparentBackground);
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(oldSnap))
                    _camera.SetView(oldSnap, animated: false);
                else
                    _camera.SetOrientation(oldPitch, oldYaw, allowSnap: false);

                _camera.SetZoomPercent(oldZoom);
                RenderViewport();
            }
        }

        private bool TryBuildImageExportBuffer(
            VoxelImageExportOptions options,
            out byte[] exportBuffer,
            out int exportWidth,
            out int exportHeight)
        {
            exportBuffer = Array.Empty<byte>();
            exportWidth = 0;
            exportHeight = 0;

            byte[]? rendered = null;
            int renderedW = 0;
            int renderedH = 0;
            uint exportClearColor = options.TransparentBackground ? 0x00000000u : ClearColor;

            RenderViewport(
                new ViewportRenderOverrides
                {
                    DrawOutline = options.IncludeOutline,
                    DrawBackdropGrid = options.IncludeBackdropCage,
                    DrawBackdropProjectionTiles = options.IncludeProjectionTiles,
                    DrawSurfaceVoxelGrid = options.IncludeModelGrid,
                    IncludeSelectionOverlay = false,
                    ClearColor = exportClearColor,
                },
                presentOnViewport: false,
                renderedBufferSink: (buffer, w, h) =>
                {
                    renderedW = w;
                    renderedH = h;
                    rendered = new byte[w * h * 4];
                    Buffer.BlockCopy(buffer, 0, rendered, 0, rendered.Length);
                });

            if (rendered == null || renderedW <= 0 || renderedH <= 0)
                return false;

            int scale = Math.Max(1, options.Scale);
            if (scale == 1)
            {
                exportBuffer = rendered;
                exportWidth = renderedW;
                exportHeight = renderedH;
            }
            else
            {
                exportWidth = renderedW * scale;
                exportHeight = renderedH * scale;
                exportBuffer = new byte[exportWidth * exportHeight * 4];
                UpscaleNearestBgra(rendered, renderedW, renderedH, exportBuffer, exportWidth, exportHeight, scale);
            }

            if (options.TransparentBackground && options.TrimTransparentBounds)
            {
                TrimTransparentBoundsBgra(
                    exportBuffer,
                    exportWidth,
                    exportHeight,
                    Math.Clamp(options.TrimPadding, 0, 128),
                    out var trimmedBuffer,
                    out var trimmedWidth,
                    out var trimmedHeight);
                exportBuffer = trimmedBuffer;
                exportWidth = trimmedWidth;
                exportHeight = trimmedHeight;
            }

            return true;
        }

        private async Task<VoxelImageExportOptions?> PromptImageExportOptionsAsync()
        {
            var defaults = GetImageExportDefaultsFromLastOrUi();
            int defaultScale = Math.Clamp(defaults.Scale, 1, 64);
            bool useCustomScale = defaultScale != 1 && defaultScale != 2 && defaultScale != 4;

            var scaleModeCombo = new ComboBox
            {
                MinWidth = 170,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            scaleModeCombo.Items.Add(new ComboBoxItem { Content = "1x", Tag = 1 });
            scaleModeCombo.Items.Add(new ComboBoxItem { Content = "2x", Tag = 2 });
            scaleModeCombo.Items.Add(new ComboBoxItem { Content = "4x", Tag = 4 });
            scaleModeCombo.Items.Add(new ComboBoxItem { Content = "Custom", Tag = 0 });
            scaleModeCombo.SelectedIndex = useCustomScale
                ? 3
                : (defaultScale == 2 ? 1 : (defaultScale == 4 ? 2 : 0));

            var customScaleBox = new NumberBox
            {
                Minimum = 1,
                Maximum = 64,
                Value = defaultScale,
                Width = 110,
                IsEnabled = useCustomScale,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            scaleModeCombo.SelectionChanged += (_, _) =>
            {
                bool custom = scaleModeCombo.SelectedIndex == 3;
                customScaleBox.IsEnabled = custom;
                if (!custom && scaleModeCombo.SelectedItem is ComboBoxItem item && item.Tag is int p && p > 0)
                {
                    customScaleBox.Value = p;
                }
            };

            var transparentBackgroundBox = new CheckBox
            {
                Content = "Transparent background",
                IsChecked = defaults.TransparentBackground,
            };

            var includeOutlineBox = new CheckBox
            {
                Content = "Include outline",
                IsChecked = defaults.IncludeOutline,
            };
            var includeCageBox = new CheckBox
            {
                Content = "Include backdrop cage",
                IsChecked = defaults.IncludeBackdropCage,
            };
            var includeProjectionTilesBox = new CheckBox
            {
                Content = "Include projection tiles",
                IsChecked = defaults.IncludeProjectionTiles,
            };
            var includeModelGridBox = new CheckBox
            {
                Content = "Include model voxel grid",
                IsChecked = defaults.IncludeModelGrid,
            };

            var trimTransparentBoundsBox = new CheckBox
            {
                Content = "Trim transparent bounds",
                IsChecked = defaults.TrimTransparentBounds,
                Margin = new Thickness(0, 4, 0, 0),
            };
            var trimPaddingRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(20, 0, 0, 0),
            };
            trimPaddingRow.Children.Add(new TextBlock { Text = "Trim padding", Opacity = 0.85, VerticalAlignment = VerticalAlignment.Center });
            var trimPaddingBox = new NumberBox
            {
                Width = 90,
                Minimum = 0,
                Maximum = 128,
                Value = defaults.TrimPadding,
            };
            trimPaddingRow.Children.Add(trimPaddingBox);

            var batchExportViewsBox = new CheckBox
            {
                Content = "Batch export views (pack)",
                IsChecked = defaults.BatchExportViews,
                Margin = new Thickness(0, 6, 0, 0),
            };
            var includeBatchCardinalViewsBox = new CheckBox
            {
                Content = "Include Front/Back/Left/Right/Top/Bottom",
                IsChecked = defaults.BatchIncludeCardinalViews,
                Margin = new Thickness(20, 0, 0, 0),
            };
            var includeBatchDirectionalViewsBox = new CheckBox
            {
                Content = "Include N/NE/E/SE/S/SW/W/NW",
                IsChecked = defaults.BatchIncludeDirectionalViews,
                Margin = new Thickness(20, 0, 0, 0),
            };

            void SyncImageExportDependentUi()
            {
                bool transparent = transparentBackgroundBox.IsChecked == true;
                trimTransparentBoundsBox.IsEnabled = transparent;
                bool trimEnabled = transparent && (trimTransparentBoundsBox.IsChecked == true);
                trimPaddingRow.IsHitTestVisible = trimEnabled;
                trimPaddingRow.Opacity = trimEnabled ? 1.0 : 0.55;

                bool batch = batchExportViewsBox.IsChecked == true;
                includeBatchCardinalViewsBox.IsEnabled = batch;
                includeBatchDirectionalViewsBox.IsEnabled = batch;
            }
            transparentBackgroundBox.Checked += (_, _) => SyncImageExportDependentUi();
            transparentBackgroundBox.Unchecked += (_, _) => SyncImageExportDependentUi();
            trimTransparentBoundsBox.Checked += (_, _) => SyncImageExportDependentUi();
            trimTransparentBoundsBox.Unchecked += (_, _) => SyncImageExportDependentUi();
            batchExportViewsBox.Checked += (_, _) => SyncImageExportDependentUi();
            batchExportViewsBox.Unchecked += (_, _) => SyncImageExportDependentUi();
            SyncImageExportDependentUi();

            VoxelImageExportOptions BuildCurrentImageOptionsFromControls()
            {
                int scale = 1;
                if (scaleModeCombo.SelectedItem is ComboBoxItem selectedScaleItem &&
                    selectedScaleItem.Tag is int presetScale &&
                    presetScale > 0)
                {
                    scale = presetScale;
                }
                else
                {
                    scale = (int)Math.Round(double.IsFinite(customScaleBox.Value) ? customScaleBox.Value : defaultScale);
                }

                return new VoxelImageExportOptions(
                    Scale: Math.Clamp(scale, 1, 64),
                    TransparentBackground: transparentBackgroundBox.IsChecked == true,
                    IncludeOutline: includeOutlineBox.IsChecked == true,
                    IncludeBackdropCage: includeCageBox.IsChecked == true,
                    IncludeProjectionTiles: includeProjectionTilesBox.IsChecked == true,
                    IncludeModelGrid: includeModelGridBox.IsChecked == true,
                    TrimTransparentBounds: trimTransparentBoundsBox.IsChecked == true,
                    TrimPadding: Math.Clamp((int)Math.Round(double.IsFinite(trimPaddingBox.Value) ? trimPaddingBox.Value : defaults.TrimPadding), 0, 128),
                    BatchExportViews: batchExportViewsBox.IsChecked == true,
                    BatchIncludeCardinalViews: includeBatchCardinalViewsBox.IsChecked != false,
                    BatchIncludeDirectionalViews: includeBatchDirectionalViewsBox.IsChecked != false);
            }

            void ApplyImageOptionsToControls(VoxelImageExportOptions loadedOptions)
            {
                int loadedScale = Math.Clamp(loadedOptions.Scale, 1, 64);
                bool loadedCustomScale = loadedScale != 1 && loadedScale != 2 && loadedScale != 4;
                scaleModeCombo.SelectedIndex = loadedCustomScale
                    ? 3
                    : (loadedScale == 2 ? 1 : (loadedScale == 4 ? 2 : 0));
                customScaleBox.Value = loadedScale;
                customScaleBox.IsEnabled = loadedCustomScale;

                transparentBackgroundBox.IsChecked = loadedOptions.TransparentBackground;
                includeOutlineBox.IsChecked = loadedOptions.IncludeOutline;
                includeCageBox.IsChecked = loadedOptions.IncludeBackdropCage;
                includeProjectionTilesBox.IsChecked = loadedOptions.IncludeProjectionTiles;
                includeModelGridBox.IsChecked = loadedOptions.IncludeModelGrid;
                trimTransparentBoundsBox.IsChecked = loadedOptions.TrimTransparentBounds;
                trimPaddingBox.Value = loadedOptions.TrimPadding;
                batchExportViewsBox.IsChecked = loadedOptions.BatchExportViews;
                includeBatchCardinalViewsBox.IsChecked = loadedOptions.BatchIncludeCardinalViews;
                includeBatchDirectionalViewsBox.IsChecked = loadedOptions.BatchIncludeDirectionalViews;
                SyncImageExportDependentUi();
            }

            var savePresetButton = new Button { Content = "Save Preset…" };
            savePresetButton.Click += async (_, _) =>
            {
                try
                {
                    var image = BuildCurrentImageOptionsFromControls();
                    var model = GetModelExportDefaultsFromLast();
                    await SaveExportPresetAsync(new VoxelExportPreset(image, model));
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to save voxel export preset", ex);
                }
            };

            var loadPresetButton = new Button { Content = "Load Preset…" };
            loadPresetButton.Click += async (_, _) =>
            {
                try
                {
                    var loadedPreset = await LoadExportPresetAsync();
                    if (loadedPreset is null)
                        return;

                    ApplyImageOptionsToControls(loadedPreset.Value.Image);
                    ApplyModelExportOptionDefaults(loadedPreset.Value.Model);
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to load voxel export preset", ex);
                }
            };

            var layout = new StackPanel { Spacing = 10 };
            layout.Children.Add(new TextBlock { Text = "Scale", Opacity = 0.85 });
            var scaleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            scaleRow.Children.Add(scaleModeCombo);
            scaleRow.Children.Add(customScaleBox);
            layout.Children.Add(scaleRow);
            var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            presetRow.Children.Add(savePresetButton);
            presetRow.Children.Add(loadPresetButton);
            layout.Children.Add(presetRow);
            layout.Children.Add(transparentBackgroundBox);
            layout.Children.Add(includeOutlineBox);
            layout.Children.Add(includeCageBox);
            layout.Children.Add(includeProjectionTilesBox);
            layout.Children.Add(includeModelGridBox);
            layout.Children.Add(trimTransparentBoundsBox);
            layout.Children.Add(trimPaddingRow);
            layout.Children.Add(batchExportViewsBox);
            layout.Children.Add(includeBatchCardinalViewsBox);
            layout.Children.Add(includeBatchDirectionalViewsBox);

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Export Image",
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = layout,
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return null;

            var chosen = BuildCurrentImageOptionsFromControls();
            ApplyImageExportOptionDefaults(chosen);
            return chosen;
        }

        private async Task<VoxelModelExportOptions?> PromptModelExportOptionsAsync()
        {
            var formatCombo = new ComboBox
            {
                MinWidth = 230,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            formatCombo.Items.Add(new ComboBoxItem
            {
                Content = "OBJ (.obj + .mtl + .png)",
                Tag = ModelExportFormat.Obj,
            });
            formatCombo.Items.Add(new ComboBoxItem
            {
                Content = "glTF Binary (.glb)",
                Tag = ModelExportFormat.Glb,
            });
            formatCombo.Items.Add(new ComboBoxItem
            {
                Content = "STL Binary (.stl)",
                Tag = ModelExportFormat.Stl,
            });
            formatCombo.Items.Add(new ComboBoxItem
            {
                Content = "MagicaVoxel (.vox)",
                Tag = ModelExportFormat.Vox,
            });
            formatCombo.SelectedIndex = _lastModelExportFormat switch
            {
                ModelExportFormat.Glb => 1,
                ModelExportFormat.Stl => 2,
                ModelExportFormat.Vox => 3,
                _ => 0,
            };

            var meshModeCombo = new ComboBox
            {
                MinWidth = 230,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            meshModeCombo.Items.Add(new ComboBoxItem
            {
                Content = "Merge coplanar faces",
                Tag = ModelMeshMode.MergeCoplanar,
            });
            meshModeCombo.Items.Add(new ComboBoxItem
            {
                Content = "Keep per-voxel faces",
                Tag = ModelMeshMode.PerVoxel,
            });
            meshModeCombo.SelectedIndex = _lastModelExportMeshMode == ModelMeshMode.PerVoxel ? 1 : 0;

            var axisPresetCombo = new ComboBox
            {
                MinWidth = 230,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            axisPresetCombo.Items.Add(new ComboBoxItem
            {
                Content = "PixlPunkt (Y-up)",
                Tag = ModelAxisPreset.PixlPunkt,
            });
            axisPresetCombo.Items.Add(new ComboBoxItem
            {
                Content = "Blender (Z-up)",
                Tag = ModelAxisPreset.BlenderZUp,
            });
            axisPresetCombo.SelectedIndex = _lastModelExportAxisPreset == ModelAxisPreset.BlenderZUp ? 1 : 0;

            var pivotPresetCombo = new ComboBox
            {
                MinWidth = 230,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            pivotPresetCombo.Items.Add(new ComboBoxItem
            {
                Content = "Center",
                Tag = ModelPivotPreset.Center,
            });
            pivotPresetCombo.Items.Add(new ComboBoxItem
            {
                Content = "Bottom Center",
                Tag = ModelPivotPreset.BottomCenter,
            });
            pivotPresetCombo.Items.Add(new ComboBoxItem
            {
                Content = "Origin",
                Tag = ModelPivotPreset.Origin,
            });
            pivotPresetCombo.SelectedIndex = _lastModelExportPivotPreset switch
            {
                ModelPivotPreset.BottomCenter => 1,
                ModelPivotPreset.Origin => 2,
                _ => 0,
            };

            var unitScaleBox = new NumberBox
            {
                MinWidth = 120,
                Maximum = 10000,
                Minimum = 0.0001,
                Value = _lastModelExportUnitScale <= 0f ? 1f : _lastModelExportUnitScale,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            var glbDoubleSidedBox = new CheckBox
            {
                Content = "GLB: Double-sided material",
                IsChecked = _lastModelExportGlbDoubleSided,
            };

            var voxWarningText = new TextBlock
            {
                TextWrapping = TextWrapping.WrapWholeWords,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Goldenrod),
                Opacity = 0.95,
                Visibility = Visibility.Collapsed,
            };

            VoxelModelExportOptions BuildCurrentModelOptionsFromControls()
            {
                var format = formatCombo.SelectedItem is ComboBoxItem formatItem && formatItem.Tag is ModelExportFormat formatValue
                    ? formatValue
                    : ModelExportFormat.Obj;
                var meshMode = meshModeCombo.SelectedItem is ComboBoxItem meshItem && meshItem.Tag is ModelMeshMode meshValue
                    ? meshValue
                    : ModelMeshMode.MergeCoplanar;
                var axisPreset = axisPresetCombo.SelectedItem is ComboBoxItem axisItem && axisItem.Tag is ModelAxisPreset axisValue
                    ? axisValue
                    : ModelAxisPreset.PixlPunkt;
                var pivotPreset = pivotPresetCombo.SelectedItem is ComboBoxItem pivotItem && pivotItem.Tag is ModelPivotPreset pivotValue
                    ? pivotValue
                    : ModelPivotPreset.Center;
                float unitScale = (float)(double.IsFinite(unitScaleBox.Value) ? unitScaleBox.Value : 1d);
                unitScale = Math.Clamp(unitScale, 0.0001f, 10000f);
                bool glbDoubleSided = glbDoubleSidedBox.IsChecked != false;
                return new VoxelModelExportOptions(format, meshMode, axisPreset, unitScale, pivotPreset, glbDoubleSided);
            }

            void ApplyModelOptionsToControls(VoxelModelExportOptions loaded)
            {
                formatCombo.SelectedIndex = loaded.Format switch
                {
                    ModelExportFormat.Glb => 1,
                    ModelExportFormat.Stl => 2,
                    ModelExportFormat.Vox => 3,
                    _ => 0,
                };
                meshModeCombo.SelectedIndex = loaded.MeshMode == ModelMeshMode.PerVoxel ? 1 : 0;
                axisPresetCombo.SelectedIndex = loaded.AxisPreset == ModelAxisPreset.BlenderZUp ? 1 : 0;
                pivotPresetCombo.SelectedIndex = loaded.PivotPreset switch
                {
                    ModelPivotPreset.BottomCenter => 1,
                    ModelPivotPreset.Origin => 2,
                    _ => 0,
                };
                unitScaleBox.Value = Math.Clamp(loaded.UnitScale, 0.0001f, 10000f);
                glbDoubleSidedBox.IsChecked = loaded.GlbDoubleSided;
            }

            void RefreshFormatSpecificControls()
            {
                var selectedFormat = formatCombo.SelectedItem is ComboBoxItem selectedItem &&
                                     selectedItem.Tag is ModelExportFormat selectedValue
                    ? selectedValue
                    : ModelExportFormat.Obj;

                glbDoubleSidedBox.Visibility = selectedFormat == ModelExportFormat.Glb
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (selectedFormat != ModelExportFormat.Vox)
                {
                    voxWarningText.Visibility = Visibility.Collapsed;
                    return;
                }

                if (_lastVolume == null || _lastVolume.OccupiedCount <= 0)
                {
                    voxWarningText.Text = "VOX exports palette-indexed voxel colors only.";
                    voxWarningText.Visibility = Visibility.Visible;
                    return;
                }

                var currentOptions = BuildCurrentModelOptionsFromControls();
                var bounds = ComputeTransformedVoxelBounds(_lastVolume, currentOptions.AxisPreset);
                if (!bounds.HasValue)
                {
                    voxWarningText.Text = "VOX exports palette-indexed voxel colors only.";
                    voxWarningText.Visibility = Visibility.Visible;
                    return;
                }

                var b = bounds.Value;
                bool outOfRange = b.SizeX > 255 || b.SizeY > 255 || b.SizeZ > 255;
                voxWarningText.Text = outOfRange
                    ? $"Warning: VOX supports <=255 units per axis. Current transformed size is {b.SizeX}x{b.SizeY}x{b.SizeZ}."
                    : $"VOX note: size {b.SizeX}x{b.SizeY}x{b.SizeZ}, 255 max per axis, pivot/unit scale are ignored.";
                voxWarningText.Visibility = Visibility.Visible;
            }

            var savePresetButton = new Button { Content = "Save Preset…" };
            savePresetButton.Click += async (_, _) =>
            {
                try
                {
                    var model = BuildCurrentModelOptionsFromControls();
                    var image = GetImageExportDefaultsFromLastOrUi();
                    await SaveExportPresetAsync(new VoxelExportPreset(image, model));
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to save voxel export preset", ex);
                }
            };

            var loadPresetButton = new Button { Content = "Load Preset…" };
            loadPresetButton.Click += async (_, _) =>
            {
                try
                {
                    var loadedPreset = await LoadExportPresetAsync();
                    if (loadedPreset is null)
                        return;

                    ApplyModelOptionsToControls(loadedPreset.Value.Model);
                    ApplyImageExportOptionDefaults(loadedPreset.Value.Image);
                    RefreshFormatSpecificControls();
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to load voxel export preset", ex);
                }
            };

            var layout = new StackPanel { Spacing = 10 };
            var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            presetRow.Children.Add(savePresetButton);
            presetRow.Children.Add(loadPresetButton);
            layout.Children.Add(presetRow);
            layout.Children.Add(new TextBlock { Text = "Format", Opacity = 0.85 });
            layout.Children.Add(formatCombo);
            layout.Children.Add(new TextBlock { Text = "Mesh", Opacity = 0.85 });
            layout.Children.Add(meshModeCombo);
            layout.Children.Add(new TextBlock { Text = "Axis Preset", Opacity = 0.85 });
            layout.Children.Add(axisPresetCombo);
            layout.Children.Add(new TextBlock { Text = "Pivot", Opacity = 0.85 });
            layout.Children.Add(pivotPresetCombo);
            layout.Children.Add(new TextBlock { Text = "Unit Scale", Opacity = 0.85 });
            layout.Children.Add(unitScaleBox);
            layout.Children.Add(glbDoubleSidedBox);
            layout.Children.Add(voxWarningText);

            formatCombo.SelectionChanged += (_, _) => RefreshFormatSpecificControls();
            axisPresetCombo.SelectionChanged += (_, _) => RefreshFormatSpecificControls();
            RefreshFormatSpecificControls();

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Export Model",
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = layout,
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return null;

            var chosen = BuildCurrentModelOptionsFromControls();
            ApplyModelExportOptionDefaults(chosen);
            return chosen;
        }

        private VoxelImageExportOptions GetImageExportDefaultsFromLastOrUi()
            => new(
                Scale: Math.Clamp(_lastImageExportScale, 1, 64),
                TransparentBackground: _lastImageExportTransparentBackground,
                IncludeOutline: _lastImageExportIncludeOutline,
                IncludeBackdropCage: _lastImageExportIncludeBackdropCage,
                IncludeProjectionTiles: _lastImageExportIncludeProjectionTiles,
                IncludeModelGrid: _lastImageExportIncludeModelGrid,
                TrimTransparentBounds: _lastImageExportTrimTransparentBounds,
                TrimPadding: Math.Clamp(_lastImageExportTrimPadding, 0, 128),
                BatchExportViews: _lastImageExportBatchViews,
                BatchIncludeCardinalViews: _lastImageExportBatchCardinalViews,
                BatchIncludeDirectionalViews: _lastImageExportBatchDirectionalViews);

        private VoxelModelExportOptions GetModelExportDefaultsFromLast()
            => new(
                Format: _lastModelExportFormat,
                MeshMode: _lastModelExportMeshMode,
                AxisPreset: _lastModelExportAxisPreset,
                UnitScale: _lastModelExportUnitScale <= 0f ? 1f : _lastModelExportUnitScale,
                PivotPreset: _lastModelExportPivotPreset,
                GlbDoubleSided: _lastModelExportGlbDoubleSided);

        private void ApplyImageExportOptionDefaults(VoxelImageExportOptions options)
        {
            _lastImageExportScale = Math.Clamp(options.Scale, 1, 64);
            _lastImageExportTransparentBackground = options.TransparentBackground;
            _lastImageExportIncludeOutline = options.IncludeOutline;
            _lastImageExportIncludeBackdropCage = options.IncludeBackdropCage;
            _lastImageExportIncludeProjectionTiles = options.IncludeProjectionTiles;
            _lastImageExportIncludeModelGrid = options.IncludeModelGrid;
            _lastImageExportTrimTransparentBounds = options.TrimTransparentBounds;
            _lastImageExportTrimPadding = Math.Clamp(options.TrimPadding, 0, 128);
            _lastImageExportBatchViews = options.BatchExportViews;
            _lastImageExportBatchCardinalViews = options.BatchIncludeCardinalViews;
            _lastImageExportBatchDirectionalViews = options.BatchIncludeDirectionalViews;
        }

        private void ApplyModelExportOptionDefaults(VoxelModelExportOptions options)
        {
            _lastModelExportFormat = options.Format;
            _lastModelExportMeshMode = options.MeshMode;
            _lastModelExportAxisPreset = options.AxisPreset;
            _lastModelExportUnitScale = Math.Clamp(options.UnitScale, 0.0001f, 10000f);
            _lastModelExportPivotPreset = options.PivotPreset;
            _lastModelExportGlbDoubleSided = options.GlbDoubleSided;
        }

        private async Task SaveExportPresetAsync(VoxelExportPreset preset)
        {
            var ownerWindow = App.PixlPunktMainWindow;
            if (ownerWindow == null)
                return;

            var picker = WindowHost.CreateFileSavePicker(ownerWindow, "voxel_export_preset", ".pxvexpreset", ".json");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            WindowHost.TrySetDefaultFileExtension(picker, ".pxvexpreset");
            var file = await picker.PickSaveFileAsync();
            if (file == null)
                return;

            var payload = new VoxelExportPresetFile
            {
                Version = 1,
                Image = new VoxelImageExportPresetFile
                {
                    Scale = preset.Image.Scale,
                    TransparentBackground = preset.Image.TransparentBackground,
                    IncludeOutline = preset.Image.IncludeOutline,
                    IncludeBackdropCage = preset.Image.IncludeBackdropCage,
                    IncludeProjectionTiles = preset.Image.IncludeProjectionTiles,
                    IncludeModelGrid = preset.Image.IncludeModelGrid,
                    TrimTransparentBounds = preset.Image.TrimTransparentBounds,
                    TrimPadding = preset.Image.TrimPadding,
                    BatchExportViews = preset.Image.BatchExportViews,
                    BatchIncludeCardinalViews = preset.Image.BatchIncludeCardinalViews,
                    BatchIncludeDirectionalViews = preset.Image.BatchIncludeDirectionalViews,
                },
                Model = new VoxelModelExportPresetFile
                {
                    Format = (int)preset.Model.Format,
                    MeshMode = (int)preset.Model.MeshMode,
                    AxisPreset = (int)preset.Model.AxisPreset,
                    UnitScale = preset.Model.UnitScale,
                    PivotPreset = (int)preset.Model.PivotPreset,
                    GlbDoubleSided = preset.Model.GlbDoubleSided,
                },
            };

            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await Windows.Storage.FileIO.WriteTextAsync(file, json);
        }

        private async Task<VoxelExportPreset?> LoadExportPresetAsync()
        {
            var ownerWindow = App.PixlPunktMainWindow;
            if (ownerWindow == null)
                return null;

            var picker = WindowHost.CreateFileOpenPicker(ownerWindow, ".pxvexpreset", ".json");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            var file = await picker.PickSingleFileAsync();
            if (file == null)
                return null;

            string json = await Windows.Storage.FileIO.ReadTextAsync(file);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            VoxelExportPresetFile? payload = JsonSerializer.Deserialize<VoxelExportPresetFile>(json);
            if (payload == null)
                return null;

            var fallbackImage = GetImageExportDefaultsFromLastOrUi();
            var fallbackModel = GetModelExportDefaultsFromLast();

            var loadedImage = payload.Image != null
                ? new VoxelImageExportOptions(
                    Scale: Math.Clamp(payload.Image.Scale, 1, 64),
                    TransparentBackground: payload.Image.TransparentBackground,
                    IncludeOutline: payload.Image.IncludeOutline,
                    IncludeBackdropCage: payload.Image.IncludeBackdropCage,
                    IncludeProjectionTiles: payload.Image.IncludeProjectionTiles,
                    IncludeModelGrid: payload.Image.IncludeModelGrid,
                    TrimTransparentBounds: payload.Image.TrimTransparentBounds,
                    TrimPadding: Math.Clamp(payload.Image.TrimPadding, 0, 128),
                    BatchExportViews: payload.Image.BatchExportViews,
                    BatchIncludeCardinalViews: payload.Image.BatchIncludeCardinalViews,
                    BatchIncludeDirectionalViews: payload.Image.BatchIncludeDirectionalViews)
                : fallbackImage;

            var loadedModel = payload.Model != null
                ? new VoxelModelExportOptions(
                    Format: Enum.IsDefined(typeof(ModelExportFormat), payload.Model.Format)
                        ? (ModelExportFormat)payload.Model.Format
                        : fallbackModel.Format,
                    MeshMode: Enum.IsDefined(typeof(ModelMeshMode), payload.Model.MeshMode)
                        ? (ModelMeshMode)payload.Model.MeshMode
                        : fallbackModel.MeshMode,
                    AxisPreset: Enum.IsDefined(typeof(ModelAxisPreset), payload.Model.AxisPreset)
                        ? (ModelAxisPreset)payload.Model.AxisPreset
                        : fallbackModel.AxisPreset,
                    UnitScale: Math.Clamp(payload.Model.UnitScale, 0.0001f, 10000f),
                    PivotPreset: Enum.IsDefined(typeof(ModelPivotPreset), payload.Model.PivotPreset)
                        ? (ModelPivotPreset)payload.Model.PivotPreset
                        : fallbackModel.PivotPreset,
                    GlbDoubleSided: payload.Model.GlbDoubleSided)
                : fallbackModel;

            return new VoxelExportPreset(loadedImage, loadedModel);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "voxel_export";

            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            }

            var safe = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(safe) ? "voxel_export" : safe;
        }

        private static string GetModelExportPrimaryExtension(ModelExportFormat format)
            => format switch
            {
                ModelExportFormat.Glb => ".glb",
                ModelExportFormat.Stl => ".stl",
                ModelExportFormat.Vox => ".vox",
                _ => ".obj",
            };

        private static ObjExportData BuildObjExport(
            VoxelVolume volume,
            string mtlFileName,
            string textureFileName,
            VoxelModelExportOptions options)
        {
            int size = volume.Size;
            float unitScale = Math.Clamp(options.UnitScale, 0.0001f, 10000f);
            Vector3 pivotOffset = GetModelPivotOffset(size, options.PivotPreset);
            var exportQuads = BuildExportFaceQuads(volume, options.MeshMode);
            var colorToIndex = new Dictionary<uint, int>();
            var colors = new List<uint>();
            for (int i = 0; i < exportQuads.Count; i++)
            {
                uint color = exportQuads[i].ColorBgra;
                if (!colorToIndex.ContainsKey(color))
                {
                    colorToIndex[color] = colors.Count;
                    colors.Add(color);
                }
            }

            int texSize = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, colors.Count))));
            var texPixels = new byte[texSize * texSize * 4];
            for (int i = 0; i < colors.Count; i++)
            {
                uint c = colors[i];
                int tx = i % texSize;
                int ty = i / texSize;
                int o = (ty * texSize + tx) * 4;
                texPixels[o + 0] = (byte)(c & 0xFF);         // B
                texPixels[o + 1] = (byte)((c >> 8) & 0xFF);  // G
                texPixels[o + 2] = (byte)((c >> 16) & 0xFF); // R
                texPixels[o + 3] = (byte)((c >> 24) & 0xFF); // A
            }

            var obj = new StringBuilder(exportQuads.Count * 120 + 256);
            var mtl = new StringBuilder(256);
            obj.AppendLine("# PixlPunkt Voxel Export");
            obj.Append("mtllib ").AppendLine(EncodeObjPathToken(mtlFileName));
            obj.AppendLine("o voxel_model");
            obj.AppendLine("usemtl voxel_material");

            int vIndex = 1;
            int vtIndex = 1;
            foreach (var f in exportQuads)
            {
                GetFaceQuadVertices(
                    f.Face,
                    f.X + pivotOffset.X,
                    f.Y + pivotOffset.Y,
                    f.Z + pivotOffset.Z,
                    f.Width,
                    f.Height,
                    out var p0,
                    out var p1,
                    out var p2,
                    out var p3);
                p0 *= unitScale;
                p1 *= unitScale;
                p2 *= unitScale;
                p3 *= unitScale;
                p0 = TransformExportVertex(p0, options.AxisPreset);
                p1 = TransformExportVertex(p1, options.AxisPreset);
                p2 = TransformExportVertex(p2, options.AxisPreset);
                p3 = TransformExportVertex(p3, options.AxisPreset);
                AppendVertex(obj, p0);
                AppendVertex(obj, p1);
                AppendVertex(obj, p2);
                AppendVertex(obj, p3);

                int ci = colorToIndex[f.ColorBgra];
                float u = ((ci % texSize) + 0.5f) / texSize;
                float v = 1f - (((ci / texSize) + 0.5f) / texSize);

                AppendUv(obj, u, v);
                AppendUv(obj, u, v);
                AppendUv(obj, u, v);
                AppendUv(obj, u, v);

                obj.Append("f ")
                   .Append(vIndex).Append('/').Append(vtIndex).Append(' ')
                   .Append(vIndex + 1).Append('/').Append(vtIndex + 1).Append(' ')
                   .Append(vIndex + 2).Append('/').Append(vtIndex + 2).Append(' ')
                   .Append(vIndex + 3).Append('/').Append(vtIndex + 3).AppendLine();

                vIndex += 4;
                vtIndex += 4;
            }

            mtl.AppendLine("newmtl voxel_material");
            mtl.AppendLine("Ka 1.000000 1.000000 1.000000");
            mtl.AppendLine("Kd 1.000000 1.000000 1.000000");
            mtl.AppendLine("Ks 0.000000 0.000000 0.000000");
            mtl.AppendLine("d 1.0");
            mtl.AppendLine("illum 1");
            mtl.Append("map_Kd ").AppendLine(EncodeObjPathToken(textureFileName));

            return new ObjExportData(obj.ToString(), mtl.ToString(), texSize, texSize, texPixels);
        }

        private static async Task<byte[]> BuildGlbExportAsync(VoxelVolume volume, VoxelModelExportOptions options)
        {
            int size = volume.Size;
            var quads = BuildExportFaceQuads(volume, options.MeshMode);
            if (quads.Count == 0)
            {
                const string emptyJson = "{\"asset\":{\"version\":\"2.0\",\"generator\":\"PixlPunkt\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{}]}";
                return BuildGlbContainer(Encoding.UTF8.GetBytes(emptyJson), Array.Empty<byte>());
            }

            var colorToIndex = new Dictionary<uint, int>();
            var colors = new List<uint>();
            for (int i = 0; i < quads.Count; i++)
            {
                uint color = quads[i].ColorBgra;
                if (!colorToIndex.ContainsKey(color))
                {
                    colorToIndex[color] = colors.Count;
                    colors.Add(color);
                }
            }

            int texSize = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, colors.Count))));
            var texPixels = new byte[texSize * texSize * 4];
            for (int i = 0; i < colors.Count; i++)
            {
                uint c = colors[i];
                int tx = i % texSize;
                int ty = i / texSize;
                int o = (ty * texSize + tx) * 4;
                texPixels[o + 0] = (byte)(c & 0xFF);
                texPixels[o + 1] = (byte)((c >> 8) & 0xFF);
                texPixels[o + 2] = (byte)((c >> 16) & 0xFF);
                texPixels[o + 3] = (byte)((c >> 24) & 0xFF);
            }
            byte[] texturePng = await EncodeBgraPngBytesAsync(texSize, texSize, texPixels, transparentBackground: true);

            int vertexCount = quads.Count * 6; // two triangles per quad, no index buffer
            var binStream = new MemoryStream(Math.Max(2048, vertexCount * 32));
            using var binWriter = new BinaryWriter(binStream, Utf8NoBom, leaveOpen: true);

            float minX = float.PositiveInfinity, minY = float.PositiveInfinity, minZ = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity, maxZ = float.NegativeInfinity;

            static void UpdateMinMax(Vector3 p, ref float minX, ref float minY, ref float minZ, ref float maxX, ref float maxY, ref float maxZ)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            static void PadTo4(BinaryWriter writer, MemoryStream stream)
            {
                while ((stream.Position & 3) != 0)
                    writer.Write((byte)0);
            }

            int posOffset = (int)binStream.Position;
            for (int i = 0; i < quads.Count; i++)
            {
                GetTransformedFaceVertices(quads[i], size, options, out var p0, out var p1, out var p2, out var p3);
                binWriter.Write(p0.X); binWriter.Write(p0.Y); binWriter.Write(p0.Z);
                binWriter.Write(p1.X); binWriter.Write(p1.Y); binWriter.Write(p1.Z);
                binWriter.Write(p2.X); binWriter.Write(p2.Y); binWriter.Write(p2.Z);
                binWriter.Write(p0.X); binWriter.Write(p0.Y); binWriter.Write(p0.Z);
                binWriter.Write(p2.X); binWriter.Write(p2.Y); binWriter.Write(p2.Z);
                binWriter.Write(p3.X); binWriter.Write(p3.Y); binWriter.Write(p3.Z);

                UpdateMinMax(p0, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                UpdateMinMax(p1, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                UpdateMinMax(p2, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                UpdateMinMax(p3, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            }
            int posLength = (int)binStream.Position - posOffset;

            PadTo4(binWriter, binStream);
            int normalOffset = (int)binStream.Position;
            for (int i = 0; i < quads.Count; i++)
            {
                GetTransformedFaceVertices(quads[i], size, options, out var p0, out var p1, out var p2, out _);
                var n = Vector3.Cross(p1 - p0, p2 - p0);
                if (n.LengthSquared() > 1e-12f)
                    n = Vector3.Normalize(n);
                else
                    n = Vector3.UnitY;

                for (int v = 0; v < 6; v++)
                {
                    binWriter.Write(n.X);
                    binWriter.Write(n.Y);
                    binWriter.Write(n.Z);
                }
            }
            int normalLength = (int)binStream.Position - normalOffset;

            PadTo4(binWriter, binStream);
            int uvOffset = (int)binStream.Position;
            for (int i = 0; i < quads.Count; i++)
            {
                int colorIndex = colorToIndex[quads[i].ColorBgra];
                float u = ((colorIndex % texSize) + 0.5f) / texSize;
                float v = 1f - (((colorIndex / texSize) + 0.5f) / texSize);
                for (int vt = 0; vt < 6; vt++)
                {
                    binWriter.Write(u);
                    binWriter.Write(v);
                }
            }
            int uvLength = (int)binStream.Position - uvOffset;

            PadTo4(binWriter, binStream);
            int imageOffset = (int)binStream.Position;
            binWriter.Write(texturePng);
            int imageLength = texturePng.Length;

            var bin = binStream.ToArray();
            string F(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

            var json = new StringBuilder(1400);
            string glbDoubleSided = options.GlbDoubleSided ? "true" : "false";
            json.Append('{')
                .Append("\"asset\":{\"version\":\"2.0\",\"generator\":\"PixlPunkt\"},")
                .Append("\"scene\":0,")
                .Append("\"scenes\":[{\"nodes\":[0]}],")
                .Append("\"nodes\":[{\"mesh\":0}],")
                .Append("\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0,\"NORMAL\":1,\"TEXCOORD_0\":2},\"material\":0,\"mode\":4}]}],")
                .Append("\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0},\"metallicFactor\":0,\"roughnessFactor\":1},\"doubleSided\":")
                .Append(glbDoubleSided)
                .Append(",\"alphaMode\":\"BLEND\"}],")
                .Append("\"textures\":[{\"sampler\":0,\"source\":0}],")
                .Append("\"samplers\":[{\"magFilter\":9728,\"minFilter\":9728,\"wrapS\":33071,\"wrapT\":33071}],")
                .Append("\"images\":[{\"bufferView\":3,\"mimeType\":\"image/png\"}],")
                .Append("\"buffers\":[{\"byteLength\":").Append(bin.Length).Append("}],")
                .Append("\"bufferViews\":[")
                .Append("{\"buffer\":0,\"byteOffset\":").Append(posOffset).Append(",\"byteLength\":").Append(posLength).Append(",\"target\":34962},")
                .Append("{\"buffer\":0,\"byteOffset\":").Append(normalOffset).Append(",\"byteLength\":").Append(normalLength).Append(",\"target\":34962},")
                .Append("{\"buffer\":0,\"byteOffset\":").Append(uvOffset).Append(",\"byteLength\":").Append(uvLength).Append(",\"target\":34962},")
                .Append("{\"buffer\":0,\"byteOffset\":").Append(imageOffset).Append(",\"byteLength\":").Append(imageLength).Append("}")
                .Append("],")
                .Append("\"accessors\":[")
                .Append("{\"bufferView\":0,\"componentType\":5126,\"count\":").Append(vertexCount).Append(",\"type\":\"VEC3\",\"min\":[")
                .Append(F(minX)).Append(',').Append(F(minY)).Append(',').Append(F(minZ)).Append("],\"max\":[")
                .Append(F(maxX)).Append(',').Append(F(maxY)).Append(',').Append(F(maxZ)).Append("]},")
                .Append("{\"bufferView\":1,\"componentType\":5126,\"count\":").Append(vertexCount).Append(",\"type\":\"VEC3\"},")
                .Append("{\"bufferView\":2,\"componentType\":5126,\"count\":").Append(vertexCount).Append(",\"type\":\"VEC2\"}")
                .Append("]")
                .Append('}');

            return BuildGlbContainer(Encoding.UTF8.GetBytes(json.ToString()), bin);
        }

        private static byte[] BuildGlbContainer(byte[] jsonUtf8, byte[] bin)
        {
            jsonUtf8 ??= Array.Empty<byte>();
            bin ??= Array.Empty<byte>();

            int jsonPaddedLength = (jsonUtf8.Length + 3) & ~3;
            int binPaddedLength = (bin.Length + 3) & ~3;
            int totalLength = 12 + 8 + jsonPaddedLength + 8 + binPaddedLength;

            using var stream = new MemoryStream(totalLength);
            using var writer = new BinaryWriter(stream, Utf8NoBom, leaveOpen: true);

            writer.Write(0x46546C67); // glTF
            writer.Write(2);          // version
            writer.Write(totalLength);

            writer.Write(jsonPaddedLength);
            writer.Write(0x4E4F534A); // JSON
            writer.Write(jsonUtf8);
            for (int i = jsonUtf8.Length; i < jsonPaddedLength; i++)
                writer.Write((byte)0x20);

            writer.Write(binPaddedLength);
            writer.Write(0x004E4942); // BIN
            writer.Write(bin);
            for (int i = bin.Length; i < binPaddedLength; i++)
                writer.Write((byte)0x00);

            return stream.ToArray();
        }

        private static byte[] BuildStlExport(VoxelVolume volume, VoxelModelExportOptions options)
        {
            var triangles = BuildExportTriangles(volume, options);
            uint triangleCount = (uint)triangles.Count;

            using var stream = new MemoryStream(84 + (int)triangleCount * 50);
            using var writer = new BinaryWriter(stream, Utf8NoBom, leaveOpen: true);

            var header = new byte[80];
            var headerText = Encoding.ASCII.GetBytes("PixlPunkt Voxel STL");
            Buffer.BlockCopy(headerText, 0, header, 0, Math.Min(headerText.Length, header.Length));
            writer.Write(header);
            writer.Write(triangleCount);

            for (int i = 0; i < triangles.Count; i++)
            {
                var t = triangles[i];
                writer.Write(t.Normal.X);
                writer.Write(t.Normal.Y);
                writer.Write(t.Normal.Z);

                writer.Write(t.A.X); writer.Write(t.A.Y); writer.Write(t.A.Z);
                writer.Write(t.B.X); writer.Write(t.B.Y); writer.Write(t.B.Z);
                writer.Write(t.C.X); writer.Write(t.C.Y); writer.Write(t.C.Z);
                writer.Write((ushort)0);
            }

            return stream.ToArray();
        }

        private static byte[] BuildVoxExport(VoxelVolume volume, VoxelModelExportOptions options)
        {
            var voxels = new List<(int X, int Y, int Z, byte PaletteIndex)>(Math.Max(1, volume.OccupiedCount));
            var palette = new List<uint>(255);
            var paletteLookup = new Dictionary<uint, byte>();
            int size = volume.Size;

            var bounds = ComputeTransformedVoxelBounds(volume, options.AxisPreset);
            int minX = bounds?.MinX ?? 0;
            int minY = bounds?.MinY ?? 0;
            int minZ = bounds?.MinZ ?? 0;
            int sizeX = bounds?.SizeX ?? 1;
            int sizeY = bounds?.SizeY ?? 1;
            int sizeZ = bounds?.SizeZ ?? 1;

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (!volume.IsOccupied(x, y, z))
                            continue;

                        var tc = TransformVoxelCoordinate(x, y, z, options.AxisPreset);

                        uint color = GetRepresentativeVoxelColor(volume, x, y, z);
                        byte paletteIndex = ResolveVoxPaletteIndex(color, palette, paletteLookup);
                        voxels.Add((tc.X, tc.Y, tc.Z, paletteIndex));
                    }
                }
            }
            if (sizeX > 255 || sizeY > 255 || sizeZ > 255)
            {
                throw new InvalidOperationException(
                    $"VOX export supports <=255 units per axis. Current transformed size is {sizeX}x{sizeY}x{sizeZ}.");
            }

            using var childrenStream = new MemoryStream();
            using var childrenWriter = new BinaryWriter(childrenStream, Utf8NoBom, leaveOpen: true);

            WriteChunk(childrenWriter, "SIZE", contentWriter =>
            {
                contentWriter.Write(sizeX);
                contentWriter.Write(sizeY);
                contentWriter.Write(sizeZ);
            });

            WriteChunk(childrenWriter, "XYZI", contentWriter =>
            {
                contentWriter.Write(voxels.Count);
                for (int i = 0; i < voxels.Count; i++)
                {
                    var v = voxels[i];
                    contentWriter.Write((byte)(v.X - minX));
                    contentWriter.Write((byte)(v.Y - minY));
                    contentWriter.Write((byte)(v.Z - minZ));
                    contentWriter.Write(v.PaletteIndex);
                }
            });

            WriteChunk(childrenWriter, "RGBA", contentWriter =>
            {
                for (int i = 0; i < 256; i++)
                {
                    uint rgba = i == 0 || i > palette.Count ? 0u : BgraToRgba(palette[i - 1]);
                    contentWriter.Write((byte)(rgba & 0xFF));
                    contentWriter.Write((byte)((rgba >> 8) & 0xFF));
                    contentWriter.Write((byte)((rgba >> 16) & 0xFF));
                    contentWriter.Write((byte)((rgba >> 24) & 0xFF));
                }
            });

            byte[] childrenBytes = childrenStream.ToArray();
            using var outStream = new MemoryStream(32 + childrenBytes.Length);
            using var writer = new BinaryWriter(outStream, Utf8NoBom, leaveOpen: true);
            writer.Write(Encoding.ASCII.GetBytes("VOX "));
            writer.Write(150); // VOX version
            writer.Write(Encoding.ASCII.GetBytes("MAIN"));
            writer.Write(0); // main content size
            writer.Write(childrenBytes.Length); // child chunks size
            writer.Write(childrenBytes);

            return outStream.ToArray();
        }

        private static TransformedVoxelBounds? ComputeTransformedVoxelBounds(VoxelVolume? volume, ModelAxisPreset axisPreset)
        {
            if (volume == null || volume.OccupiedCount <= 0)
                return null;

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            int maxZ = int.MinValue;
            int size = volume.Size;

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (!volume.IsOccupied(x, y, z))
                            continue;

                        var tc = TransformVoxelCoordinate(x, y, z, axisPreset);
                        if (tc.X < minX) minX = tc.X;
                        if (tc.Y < minY) minY = tc.Y;
                        if (tc.Z < minZ) minZ = tc.Z;
                        if (tc.X > maxX) maxX = tc.X;
                        if (tc.Y > maxY) maxY = tc.Y;
                        if (tc.Z > maxZ) maxZ = tc.Z;
                    }
                }
            }

            if (minX == int.MaxValue)
                return null;

            return new TransformedVoxelBounds(minX, minY, minZ, maxX, maxY, maxZ);
        }

        private static void WriteChunk(BinaryWriter writer, string id, Action<BinaryWriter> writeContent)
        {
            using var contentStream = new MemoryStream();
            using (var contentWriter = new BinaryWriter(contentStream, Utf8NoBom, leaveOpen: true))
            {
                writeContent(contentWriter);
            }

            byte[] contentBytes = contentStream.ToArray();
            writer.Write(Encoding.ASCII.GetBytes(id));
            writer.Write(contentBytes.Length);
            writer.Write(0); // no nested children
            writer.Write(contentBytes);
        }

        private static uint GetRepresentativeVoxelColor(VoxelVolume volume, int x, int y, int z)
        {
            int sumR = 0, sumG = 0, sumB = 0, count = 0;
            foreach (Face face in Enum.GetValues(typeof(Face)))
            {
                var c = volume.GetFaceColor(x, y, z, face);
                if (c.A == 0)
                    continue;
                sumR += c.R;
                sumG += c.G;
                sumB += c.B;
                count++;
            }

            if (count <= 0)
            {
                var fallback = volume.GetFaceColor(x, y, z, Face.Front);
                return ToBgra(fallback);
            }

            byte r = (byte)(sumR / count);
            byte g = (byte)(sumG / count);
            byte b = (byte)(sumB / count);
            return (0xFFu << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        }

        private static byte ResolveVoxPaletteIndex(uint color, List<uint> palette, Dictionary<uint, byte> lookup)
        {
            if (lookup.TryGetValue(color, out byte existing))
                return existing;

            if (palette.Count < 255)
            {
                byte next = (byte)(palette.Count + 1);
                palette.Add(color);
                lookup[color] = next;
                return next;
            }

            byte nearest = FindNearestPaletteIndex(color, palette);
            lookup[color] = nearest;
            return nearest;
        }

        private static byte FindNearestPaletteIndex(uint color, List<uint> palette)
        {
            int r = (int)((color >> 16) & 0xFF);
            int g = (int)((color >> 8) & 0xFF);
            int b = (int)(color & 0xFF);
            int bestScore = int.MaxValue;
            byte bestIndex = 1;

            for (int i = 0; i < palette.Count; i++)
            {
                uint c = palette[i];
                int dr = r - (int)((c >> 16) & 0xFF);
                int dg = g - (int)((c >> 8) & 0xFF);
                int db = b - (int)(c & 0xFF);
                int score = (dr * dr) + (dg * dg) + (db * db);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = (byte)(i + 1);
                }
            }

            return bestIndex;
        }

        private static uint BgraToRgba(uint bgra)
        {
            byte b = (byte)(bgra & 0xFF);
            byte g = (byte)((bgra >> 8) & 0xFF);
            byte r = (byte)((bgra >> 16) & 0xFF);
            byte a = (byte)((bgra >> 24) & 0xFF);
            return ((uint)r) | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        }

        private static (int X, int Y, int Z) TransformVoxelCoordinate(int x, int y, int z, ModelAxisPreset axisPreset)
            => axisPreset switch
            {
                ModelAxisPreset.BlenderZUp => (x, z, -y),
                _ => (x, y, z),
            };

        private static List<ExportTriangle> BuildExportTriangles(VoxelVolume volume, VoxelModelExportOptions options)
        {
            int size = volume.Size;
            var quads = BuildExportFaceQuads(volume, options.MeshMode);
            var triangles = new List<ExportTriangle>(Math.Max(1, quads.Count * 2));

            for (int i = 0; i < quads.Count; i++)
            {
                GetTransformedFaceVertices(quads[i], size, options, out var p0, out var p1, out var p2, out var p3);

                var n = Vector3.Cross(p1 - p0, p2 - p0);
                if (n.LengthSquared() <= 1e-12f)
                    continue;
                n = Vector3.Normalize(n);

                triangles.Add(new ExportTriangle(p0, p1, p2, n, quads[i].ColorBgra));
                triangles.Add(new ExportTriangle(p0, p2, p3, n, quads[i].ColorBgra));
            }

            return triangles;
        }

        private static void GetTransformedFaceVertices(
            ExportFaceQuad faceQuad,
            int volumeSize,
            VoxelModelExportOptions options,
            out Vector3 p0,
            out Vector3 p1,
            out Vector3 p2,
            out Vector3 p3)
        {
            float unitScale = Math.Clamp(options.UnitScale, 0.0001f, 10000f);
            Vector3 pivotOffset = GetModelPivotOffset(volumeSize, options.PivotPreset);

            GetFaceQuadVertices(
                faceQuad.Face,
                faceQuad.X + pivotOffset.X,
                faceQuad.Y + pivotOffset.Y,
                faceQuad.Z + pivotOffset.Z,
                faceQuad.Width,
                faceQuad.Height,
                out p0,
                out p1,
                out p2,
                out p3);

            p0 *= unitScale;
            p1 *= unitScale;
            p2 *= unitScale;
            p3 *= unitScale;

            p0 = TransformExportVertex(p0, options.AxisPreset);
            p1 = TransformExportVertex(p1, options.AxisPreset);
            p2 = TransformExportVertex(p2, options.AxisPreset);
            p3 = TransformExportVertex(p3, options.AxisPreset);
        }

        private static List<ExportFaceQuad> BuildExportFaceQuads(VoxelVolume volume, ModelMeshMode mode)
            => mode == ModelMeshMode.MergeCoplanar
                ? BuildMergedExportFaceQuads(volume)
                : BuildPerVoxelExportFaceQuads(volume);

        private static List<ExportFaceQuad> BuildPerVoxelExportFaceQuads(VoxelVolume volume)
        {
            int size = volume.Size;
            var quads = new List<ExportFaceQuad>(Math.Max(128, volume.OccupiedCount * 3));

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (!volume.IsOccupied(x, y, z))
                            continue;

                        TryAddFace(Face.Front, x, y, z, 0, 0, -1);
                        TryAddFace(Face.Back, x, y, z, 0, 0, 1);
                        TryAddFace(Face.Left, x, y, z, -1, 0, 0);
                        TryAddFace(Face.Right, x, y, z, 1, 0, 0);
                        TryAddFace(Face.Top, x, y, z, 0, 1, 0);
                        TryAddFace(Face.Bottom, x, y, z, 0, -1, 0);
                    }
                }
            }

            return quads;

            void TryAddFace(Face face, int x, int y, int z, int nx, int ny, int nz)
            {
                if (volume.IsOccupied(x + nx, y + ny, z + nz))
                    return;
                quads.Add(new ExportFaceQuad(face, x, y, z, 1, 1, ToBgra(volume.GetFaceColor(x, y, z, face))));
            }
        }

        private static List<ExportFaceQuad> BuildMergedExportFaceQuads(VoxelVolume volume)
        {
            int size = volume.Size;
            var quads = new List<ExportFaceQuad>(Math.Max(64, volume.OccupiedCount));

            // Front/back faces (planes across Z; merged over X/Y).
            for (int z = 0; z < size; z++)
            {
                BuildPlaneQuads(
                    width: size,
                    height: size,
                    tryGetColor: (int u, int v, out uint color) =>
                    {
                        int x = u;
                        int y = v;
                        if (!volume.IsOccupied(x, y, z) || volume.IsOccupied(x, y, z - 1))
                        {
                            color = 0;
                            return false;
                        }

                        color = ToBgra(volume.GetFaceColor(x, y, z, Face.Front));
                        return true;
                    },
                    emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Front, u, v, z, w, h, color)));

                BuildPlaneQuads(
                    width: size,
                    height: size,
                    tryGetColor: (int u, int v, out uint color) =>
                    {
                        int x = u;
                        int y = v;
                        if (!volume.IsOccupied(x, y, z) || volume.IsOccupied(x, y, z + 1))
                        {
                            color = 0;
                            return false;
                        }

                        color = ToBgra(volume.GetFaceColor(x, y, z, Face.Back));
                        return true;
                    },
                    emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Back, u, v, z, w, h, color)));
            }

            // Left/right faces (planes across X; merged over Z/Y).
            for (int x = 0; x < size; x++)
            {
                BuildPlaneQuads(
                    width: size,
                    height: size,
                    tryGetColor: (int u, int v, out uint color) =>
                    {
                        int z = u;
                        int y = v;
                        if (!volume.IsOccupied(x, y, z) || volume.IsOccupied(x - 1, y, z))
                        {
                            color = 0;
                            return false;
                        }

                        color = ToBgra(volume.GetFaceColor(x, y, z, Face.Left));
                        return true;
                    },
                    emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Left, x, v, u, w, h, color)));

                BuildPlaneQuads(
                    width: size,
                    height: size,
                    tryGetColor: (int u, int v, out uint color) =>
                    {
                        int z = u;
                        int y = v;
                        if (!volume.IsOccupied(x, y, z) || volume.IsOccupied(x + 1, y, z))
                        {
                            color = 0;
                            return false;
                        }

                        color = ToBgra(volume.GetFaceColor(x, y, z, Face.Right));
                        return true;
                    },
                    emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Right, x, v, u, w, h, color)));
            }

            // Top/bottom faces (planes across Y; merged over X/Z).
            for (int y = 0; y < size; y++)
            {
                BuildPlaneQuads(
                    width: size,
                    height: size,
                    tryGetColor: (int u, int v, out uint color) =>
                    {
                        int x = u;
                        int z = v;
                        if (!volume.IsOccupied(x, y, z) || volume.IsOccupied(x, y + 1, z))
                        {
                            color = 0;
                            return false;
                        }

                        color = ToBgra(volume.GetFaceColor(x, y, z, Face.Top));
                        return true;
                    },
                    emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Top, u, y, v, w, h, color)));

                BuildPlaneQuads(
                    width: size,
                    height: size,
                    tryGetColor: (int u, int v, out uint color) =>
                    {
                        int x = u;
                        int z = v;
                        if (!volume.IsOccupied(x, y, z) || volume.IsOccupied(x, y - 1, z))
                        {
                            color = 0;
                            return false;
                        }

                        color = ToBgra(volume.GetFaceColor(x, y, z, Face.Bottom));
                        return true;
                    },
                    emit: (u, v, w, h, color) => quads.Add(new ExportFaceQuad(Face.Bottom, u, y, v, w, h, color)));
            }

            return quads;
        }

        private static void BuildPlaneQuads(
            int width,
            int height,
            TryGetPlaneColor tryGetColor,
            Action<int, int, int, int, uint> emit)
        {
            var colors = new uint[width * height];
            var mask = new bool[width * height];

            for (int v = 0; v < height; v++)
            {
                for (int u = 0; u < width; u++)
                {
                    int idx = (v * width) + u;
                    if (!tryGetColor(u, v, out uint color))
                        continue;
                    mask[idx] = true;
                    colors[idx] = color;
                }
            }

            var visited = new bool[width * height];
            for (int v = 0; v < height; v++)
            {
                for (int u = 0; u < width; u++)
                {
                    int idx = (v * width) + u;
                    if (!mask[idx] || visited[idx])
                        continue;

                    uint color = colors[idx];
                    int quadW = 1;
                    while ((u + quadW) < width)
                    {
                        int n = (v * width) + (u + quadW);
                        if (!mask[n] || visited[n] || colors[n] != color)
                            break;
                        quadW++;
                    }

                    int quadH = 1;
                    while ((v + quadH) < height)
                    {
                        bool rowOk = true;
                        for (int ux = 0; ux < quadW; ux++)
                        {
                            int n = ((v + quadH) * width) + (u + ux);
                            if (!mask[n] || visited[n] || colors[n] != color)
                            {
                                rowOk = false;
                                break;
                            }
                        }

                        if (!rowOk)
                            break;
                        quadH++;
                    }

                    for (int vy = 0; vy < quadH; vy++)
                    {
                        for (int ux = 0; ux < quadW; ux++)
                        {
                            visited[((v + vy) * width) + (u + ux)] = true;
                        }
                    }

                    emit(u, v, quadW, quadH, color);
                }
            }
        }

        private static uint ToBgra(Rgba32 c)
            => ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;

        private static string EncodeObjPathToken(string fileName)
            => (fileName ?? string.Empty)
                .Replace('\\', '/')
                .Replace(" ", "\\ ");

        private static Vector3 GetModelPivotOffset(int size, ModelPivotPreset pivotPreset)
        {
            float half = MathF.Max(1f, size) * 0.5f;
            return pivotPreset switch
            {
                ModelPivotPreset.BottomCenter => new Vector3(-half, 0f, -half),
                ModelPivotPreset.Origin => Vector3.Zero,
                _ => new Vector3(-half, -half, -half),
            };
        }

        private static Vector3 TransformExportVertex(Vector3 p, ModelAxisPreset axisPreset)
            => axisPreset switch
            {
                ModelAxisPreset.BlenderZUp => new Vector3(p.X, p.Z, -p.Y),
                _ => p,
            };

        private static void AppendVertex(StringBuilder sb, Vector3 p)
        {
            sb.Append("v ")
              .Append(p.X.ToString("0.######", CultureInfo.InvariantCulture)).Append(' ')
              .Append(p.Y.ToString("0.######", CultureInfo.InvariantCulture)).Append(' ')
              .Append(p.Z.ToString("0.######", CultureInfo.InvariantCulture)).AppendLine();
        }

        private static void AppendUv(StringBuilder sb, float u, float v)
        {
            sb.Append("vt ")
              .Append(u.ToString("0.######", CultureInfo.InvariantCulture)).Append(' ')
              .Append(v.ToString("0.######", CultureInfo.InvariantCulture)).AppendLine();
        }

        private static void GetFaceQuadVertices(
            Face face,
            float x,
            float y,
            float z,
            int width,
            int height,
            out Vector3 p0,
            out Vector3 p1,
            out Vector3 p2,
            out Vector3 p3)
        {
            // Local voxel corners in [0,1] space; matches renderer face orientation.
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            float w = width;
            float h = height;

            switch (face)
            {
                case Face.Back:
                    p0 = new Vector3(x + 0f, y + 0f, z + 1f);
                    p1 = new Vector3(x + w, y + 0f, z + 1f);
                    p2 = new Vector3(x + w, y + h, z + 1f);
                    p3 = new Vector3(x + 0f, y + h, z + 1f);
                    break;
                case Face.Front:
                    p0 = new Vector3(x + w, y + 0f, z + 0f);
                    p1 = new Vector3(x + 0f, y + 0f, z + 0f);
                    p2 = new Vector3(x + 0f, y + h, z + 0f);
                    p3 = new Vector3(x + w, y + h, z + 0f);
                    break;
                case Face.Left:
                    p0 = new Vector3(x + 0f, y + 0f, z + 0f);
                    p1 = new Vector3(x + 0f, y + 0f, z + w);
                    p2 = new Vector3(x + 0f, y + h, z + w);
                    p3 = new Vector3(x + 0f, y + h, z + 0f);
                    break;
                case Face.Right:
                    p0 = new Vector3(x + 1f, y + 0f, z + w);
                    p1 = new Vector3(x + 1f, y + 0f, z + 0f);
                    p2 = new Vector3(x + 1f, y + h, z + 0f);
                    p3 = new Vector3(x + 1f, y + h, z + w);
                    break;
                case Face.Top:
                    p0 = new Vector3(x + 0f, y + 1f, z + h);
                    p1 = new Vector3(x + w, y + 1f, z + h);
                    p2 = new Vector3(x + w, y + 1f, z + 0f);
                    p3 = new Vector3(x + 0f, y + 1f, z + 0f);
                    break;
                default:
                    p0 = new Vector3(x + 0f, y + 0f, z + 0f);
                    p1 = new Vector3(x + w, y + 0f, z + 0f);
                    p2 = new Vector3(x + w, y + 0f, z + h);
                    p3 = new Vector3(x + 0f, y + 0f, z + h);
                    break;
            }
        }

        private static void TrimTransparentBoundsBgra(
            byte[] source,
            int sourceWidth,
            int sourceHeight,
            int padding,
            out byte[] trimmed,
            out int trimmedWidth,
            out int trimmedHeight)
        {
            trimmed = Array.Empty<byte>();
            trimmedWidth = 1;
            trimmedHeight = 1;

            if (source == null || sourceWidth <= 0 || sourceHeight <= 0 || source.Length < sourceWidth * sourceHeight * 4)
            {
                trimmed = new byte[4];
                return;
            }

            int minX = sourceWidth;
            int minY = sourceHeight;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < sourceHeight; y++)
            {
                int row = y * sourceWidth;
                for (int x = 0; x < sourceWidth; x++)
                {
                    int a = source[((row + x) * 4) + 3];
                    if (a == 0)
                        continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
            {
                trimmed = new byte[4];
                return;
            }

            padding = Math.Clamp(padding, 0, 128);
            minX = Math.Max(0, minX - padding);
            minY = Math.Max(0, minY - padding);
            maxX = Math.Min(sourceWidth - 1, maxX + padding);
            maxY = Math.Min(sourceHeight - 1, maxY + padding);

            trimmedWidth = Math.Max(1, (maxX - minX) + 1);
            trimmedHeight = Math.Max(1, (maxY - minY) + 1);
            trimmed = new byte[trimmedWidth * trimmedHeight * 4];

            for (int y = 0; y < trimmedHeight; y++)
            {
                int srcY = minY + y;
                int srcOffset = ((srcY * sourceWidth) + minX) * 4;
                int dstOffset = y * trimmedWidth * 4;
                Buffer.BlockCopy(source, srcOffset, trimmed, dstOffset, trimmedWidth * 4);
            }
        }

        private static async Task<byte[]> EncodeBgraPngBytesAsync(
            int width,
            int height,
            byte[] pixels,
            bool transparentBackground)
        {
            try
            {
                using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                    Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                    stream);

                encoder.SetPixelData(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    transparentBackground
                        ? Windows.Graphics.Imaging.BitmapAlphaMode.Straight
                        : Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    (uint)Math.Max(1, width),
                    (uint)Math.Max(1, height),
                    96,
                    96,
                    pixels ?? Array.Empty<byte>());

                await encoder.FlushAsync();
                stream.Seek(0);

                using var ms = new MemoryStream();
                using var src = stream.AsStreamForRead();
                await src.CopyToAsync(ms);
                return ms.ToArray();
            }
            catch (NotImplementedException)
            {
                return await Task.Run(() =>
                    SkiaImageEncoder.EncodeToBytes(
                        pixels ?? Array.Empty<byte>(),
                        Math.Max(1, width),
                        Math.Max(1, height),
                        SkiaImageEncoder.ImageFormat.Png));
            }
        }

        private static async Task SaveBgraPngAsync(
            string filePath,
            int width,
            int height,
            byte[] pixels,
            bool transparentBackground)
        {
            if (!System.IO.File.Exists(filePath))
            {
                await System.IO.File.WriteAllBytesAsync(filePath, Array.Empty<byte>());
            }

            try
            {
                var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                using var stream = await storageFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                    Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);

                encoder.SetPixelData(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    transparentBackground
                        ? Windows.Graphics.Imaging.BitmapAlphaMode.Straight
                        : Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    (uint)Math.Max(1, width), (uint)Math.Max(1, height),
                    96, 96,
                    pixels);

                await encoder.FlushAsync();
            }
            catch (NotImplementedException)
            {
                await Task.Run(() =>
                    SkiaImageEncoder.Encode(
                        pixels,
                        Math.Max(1, width),
                        Math.Max(1, height),
                        filePath,
                        SkiaImageEncoder.ImageFormat.Png));
            }
        }

        private static async Task SaveBgraTextureToPngAsync(string filePath, int width, int height, byte[] pixels)
            => await SaveBgraPngAsync(filePath, width, height, pixels, transparentBackground: false);

        private readonly record struct ObjExportData(
            string ObjText,
            string MtlText,
            int TextureWidth,
            int TextureHeight,
            byte[] TexturePixelsBgra);

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

