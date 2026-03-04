using System;
using Microsoft.UI.Xaml;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Palette;
using PixlPunkt.UI.Helpers;

namespace PixlPunkt.UI.Voxel
{
    /// <summary>
    /// Thin floating-window host wrapper for <see cref="VoxelWorkspaceControl"/>.
    /// </summary>
    public sealed partial class VoxelPreviewWindow : Window
    {
        private static VoxelPreviewWindow? _instance;

        private readonly VoxelWorkspaceControl _workspace;

        /// <summary>
        /// Opens the voxel workspace in a floating window for the specified document.
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

        private VoxelPreviewWindow(CanvasDocument document, PaletteService? palette)
        {
            _workspace = new VoxelWorkspaceControl(document, palette);

            InitializeComponent();
            Closed += OnClosed;
            if (Root != null)
                Root.Children.Add(_workspace);
            else
                Content = _workspace;
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            _instance = null;
        }
    }
}
