using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Logging;
using PixlPunkt.UI.Helpers;

namespace PixlPunkt.UI.Voxel
{
    /// <summary>
    /// A floating window that hosts a document's <see cref="VoxelWorkspaceControl"/> after it has
    /// been popped out of the workspace pane. The control instance is moved, not copied, so the
    /// edit engine, camera and history stay exactly as they were; closing the window hands the
    /// control back to the pane.
    /// </summary>
    public sealed partial class VoxelPaneWindow : Window
    {
        private static readonly List<VoxelPaneWindow> _open = new();

        private readonly VoxelWorkspaceControl _workspace;
        private readonly Action _onClosed;

        public VoxelWorkspaceControl Workspace => _workspace;

        public VoxelPaneWindow(VoxelWorkspaceControl workspace, string title, Action onClosed)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _onClosed = onClosed ?? throw new ArgumentNullException(nameof(onClosed));

            InitializeComponent();
            Title = title;
            Closed += OnClosed;
            Root.Children.Add(_workspace);
            _open.Add(this);
        }

        /// <summary>Removes the workspace from this window so the caller can re-parent it.</summary>
        public void ReleaseWorkspace()
        {
            Root.Children.Remove(_workspace);
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            Closed -= OnClosed;
            _open.Remove(this);
            try
            {
                ReleaseWorkspace();
                _onClosed();
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to dock the voxel workspace back", ex);
            }
        }

        public void Show(Window? owner)
        {
            try
            {
                // Same chrome as the detached document windows: themed title bar, min/max/close, app icon.
                WindowHost.ApplyChrome(this, resizable: true, alwaysOnTop: false, minimizable: true, maximizable: true, title: Title, owner: owner);
                WindowHost.Place(this, WindowPlacement.CenterOnScreen, owner);
            }
            catch (Exception ex)
            {
                LoggingService.Debug("Voxel pane window placement: {Error}", ex.Message);
            }
            Activate();
        }

        /// <summary>Closes every popped-out voxel window (app exit).</summary>
        public static void CloseAll()
        {
            foreach (var w in _open.ToArray())
            {
                try { w.Close(); } catch { }
            }
        }
    }
}
