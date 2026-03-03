using PixlPunkt.Core.Document;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.Core.Voxel.Tools;
using SdkIVoxelDocumentReadOnly = PixlPunkt.PluginSdk.Voxel.IVoxelDocumentReadOnly;
using SdkIVoxelModelReadOnly = PixlPunkt.PluginSdk.Voxel.IVoxelModelReadOnly;
using SdkIVoxelSelectionReadOnly = PixlPunkt.PluginSdk.Voxel.IVoxelSelectionReadOnly;
using SdkIVoxelToolBehavior = PixlPunkt.PluginSdk.Voxel.Tools.IVoxelToolBehavior;
using SdkIVoxelToolContext = PixlPunkt.PluginSdk.Voxel.Tools.IVoxelToolContext;
using SdkIVoxelToolHandler = PixlPunkt.PluginSdk.Voxel.Tools.IVoxelToolHandler;
using SdkIVoxelToolRegistration = PixlPunkt.PluginSdk.Voxel.Tools.IVoxelToolRegistration;

namespace PixlPunkt.Core.Plugins
{
    /// <summary>Adapts SDK voxel tool registrations to core voxel tool registrations.</summary>
    internal sealed class PluginVoxelToolRegistration : IVoxelToolRegistration
    {
        private readonly SdkIVoxelToolRegistration _sdkRegistration;
        private readonly PluginToolSettings? _adaptedSettings;
        private readonly IVoxelToolBehavior? _adaptedBehavior;

        public PluginVoxelToolRegistration(SdkIVoxelToolRegistration sdkRegistration)
        {
            _sdkRegistration = sdkRegistration ?? throw new ArgumentNullException(nameof(sdkRegistration));

            if (sdkRegistration.Settings != null)
            {
                _adaptedSettings = new PluginToolSettings(sdkRegistration.Settings);
            }

            if (sdkRegistration.Behavior != null)
            {
                _adaptedBehavior = new PluginVoxelToolBehaviorAdapter(sdkRegistration.Behavior);
            }
        }

        public string Id => _sdkRegistration.Id;

        public PixlPunkt.PluginSdk.Voxel.VoxelToolCategory Category => _sdkRegistration.Category;

        public string DisplayName => _sdkRegistration.DisplayName;

        public ToolSettingsBase? Settings => _adaptedSettings;

        public IVoxelToolBehavior? Behavior => _adaptedBehavior;

        public IVoxelToolHandler CreateHandler(IVoxelToolContext context)
        {
            var sdkContext = new PluginVoxelToolContextAdapter(context);
            var sdkHandler = _sdkRegistration.CreateHandler(sdkContext);
            return new PluginVoxelToolHandlerAdapter(sdkHandler);
        }
    }

    internal sealed class PluginVoxelToolBehaviorAdapter : IVoxelToolBehavior
    {
        private readonly SdkIVoxelToolBehavior _sdkBehavior;

        public PluginVoxelToolBehaviorAdapter(SdkIVoxelToolBehavior sdkBehavior)
        {
            _sdkBehavior = sdkBehavior ?? throw new ArgumentNullException(nameof(sdkBehavior));
        }

        public string ToolId => _sdkBehavior.ToolId;
        public PixlPunkt.PluginSdk.Voxel.VoxelToolInputPattern InputPattern => _sdkBehavior.InputPattern;
        public bool HandlesRightClick => _sdkBehavior.HandlesRightClick;
        public bool SuppressRmbSample => _sdkBehavior.SuppressRmbSample;
        public bool ModifiesVoxelData => _sdkBehavior.ModifiesVoxelData;
        public bool RequiresFacePick => _sdkBehavior.RequiresFacePick;
        public bool RequiresVoxelPick => _sdkBehavior.RequiresVoxelPick;
    }

    internal sealed class PluginVoxelToolHandlerAdapter : IVoxelToolHandler
    {
        private readonly SdkIVoxelToolHandler _sdkHandler;

        public PluginVoxelToolHandlerAdapter(SdkIVoxelToolHandler sdkHandler)
        {
            _sdkHandler = sdkHandler ?? throw new ArgumentNullException(nameof(sdkHandler));
        }

        public bool PointerPressed(PixlPunkt.PluginSdk.Voxel.VoxelPointerEvent e) => _sdkHandler.PointerPressed(e);

        public bool PointerMoved(PixlPunkt.PluginSdk.Voxel.VoxelPointerEvent e) => _sdkHandler.PointerMoved(e);

        public bool PointerReleased(PixlPunkt.PluginSdk.Voxel.VoxelPointerEvent e) => _sdkHandler.PointerReleased(e);

        public void Cancel() => _sdkHandler.Cancel();
    }

    internal sealed class PluginVoxelToolContextAdapter : SdkIVoxelToolContext
    {
        private readonly IVoxelToolContext _coreContext;
        private readonly SdkIVoxelDocumentReadOnly _documentAdapter;

        public PluginVoxelToolContextAdapter(IVoxelToolContext coreContext)
        {
            _coreContext = coreContext ?? throw new ArgumentNullException(nameof(coreContext));
            _documentAdapter = new PluginVoxelDocumentAdapter(coreContext.Document);
        }

        public SdkIVoxelDocumentReadOnly Document => _documentAdapter;

        public SdkIVoxelModelReadOnly Model => _coreContext.Model;

        public SdkIVoxelSelectionReadOnly Selection => _coreContext.Selection;

        public bool TryPickFace(float screenX, float screenY, out PixlPunkt.PluginSdk.Voxel.VoxelFaceHit hit)
            => _coreContext.TryPickFace(screenX, screenY, out hit);

        public bool TryPickVoxel(float screenX, float screenY, out PixlPunkt.PluginSdk.Voxel.VoxelVoxelHit hit)
            => _coreContext.TryPickVoxel(screenX, screenY, out hit);

        public void SetFaceColor(int x, int y, int z, PixlPunkt.PluginSdk.Voxel.VoxelFace face, uint bgra)
            => _coreContext.SetFaceColor(x, y, z, face, bgra);

        public void ClearFaceColorOverride(int x, int y, int z, PixlPunkt.PluginSdk.Voxel.VoxelFace face)
            => _coreContext.ClearFaceColorOverride(x, y, z, face);

        public void SetVoxel(int x, int y, int z, uint colorBgra)
            => _coreContext.SetVoxel(x, y, z, colorBgra);

        public void ClearVoxel(int x, int y, int z)
            => _coreContext.ClearVoxel(x, y, z);

        public void MoveSelection(PixlPunkt.PluginSdk.Voxel.Int3 delta, PixlPunkt.PluginSdk.Voxel.VoxelMoveMode mode = PixlPunkt.PluginSdk.Voxel.VoxelMoveMode.CutPaste)
            => _coreContext.MoveSelection(delta, mode);

        public void ClearSelection() => _coreContext.ClearSelection();

        public void SetSelection(IEnumerable<PixlPunkt.PluginSdk.Voxel.Int3> voxels, PixlPunkt.PluginSdk.Voxel.VoxelSelectionMode mode)
            => _coreContext.SetSelection(voxels, mode);

        public void ExpandSelectionConnected() => _coreContext.ExpandSelectionConnected();

        public uint Foreground => _coreContext.Foreground;

        public uint Background => _coreContext.Background;

        public void SetForeground(uint bgra) => _coreContext.SetForeground(bgra);

        public void SetBackground(uint bgra) => _coreContext.SetBackground(bgra);

        public PixlPunkt.PluginSdk.Voxel.VoxelViewportState ViewportState => _coreContext.ViewportState;

        public void RequestRedraw() => _coreContext.RequestRedraw();

        public void RequestRebuildRenderCache() => _coreContext.RequestRebuildRenderCache();

        public void BeginHistoryTransaction(string name) => _coreContext.BeginHistoryTransaction(name);

        public void CommitHistoryTransaction() => _coreContext.CommitHistoryTransaction();

        public void CancelHistoryTransaction() => _coreContext.CancelHistoryTransaction();

        public PixlPunkt.PluginSdk.Voxel.VoxelLightingSettings LightingSettings => _coreContext.LightingSettings;

        public void UpdateLightingSettings(Action<PixlPunkt.PluginSdk.Voxel.VoxelLightingSettings> edit)
            => _coreContext.UpdateLightingSettings(edit);

        private sealed class PluginVoxelDocumentAdapter : SdkIVoxelDocumentReadOnly
        {
            private readonly CanvasDocument _document;

            public PluginVoxelDocumentAdapter(CanvasDocument document)
            {
                _document = document;
            }

            public string Name => _document.Name ?? "Untitled";
        }
    }
}
