using PixlPunkt.PluginSdk.Voxel.Tools;

namespace PixlPunkt.ExamplePlugin.Tools.Voxel
{
    /// <summary>Example voxel face paint/sample tool used to demonstrate the voxel SDK.</summary>
    public sealed class VoxelFaceTintTool : IVoxelToolHandler
    {
        private readonly IVoxelToolContext _context;
        private readonly VoxelFaceTintSettings _settings;

        public VoxelFaceTintTool(IVoxelToolContext context, VoxelFaceTintSettings settings)
        {
            _context = context;
            _settings = settings;
        }

        public bool PointerPressed(PixlPunkt.PluginSdk.Voxel.VoxelPointerEvent e)
        {
            if (!_context.TryPickFace(e.ScreenX, e.ScreenY, out var hit))
                return false;

            if (_settings.SampleInsteadOfPaint)
            {
                _context.SetForeground(hit.ColorBgra);
                return true;
            }

            uint color = _settings.UseBackgroundColor ? _context.Background : _context.Foreground;

            _context.BeginHistoryTransaction("Voxel Face Tint");
            try
            {
                _context.SetFaceColor(hit.Position.X, hit.Position.Y, hit.Position.Z, hit.Face, color);
                _context.CommitHistoryTransaction();
            }
            catch
            {
                _context.CancelHistoryTransaction();
                throw;
            }

            _context.RequestRedraw();
            return true;
        }

        public bool PointerMoved(PixlPunkt.PluginSdk.Voxel.VoxelPointerEvent e) => false;

        public bool PointerReleased(PixlPunkt.PluginSdk.Voxel.VoxelPointerEvent e) => false;

        public void Cancel()
        {
        }
    }
}
