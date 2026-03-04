using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Palette;
using PixlPunkt.UI.CanvasHost;
using PixlPunkt.UI.Voxel;

namespace PixlPunkt.UI.CanvasArea
{
    /// <summary>
    /// In-tab document workspace host that contains the 2D canvas and optional voxel workspace pane.
    /// </summary>
    public sealed partial class DocumentWorkspaceHost : UserControl
    {
        private enum ActiveWorkspacePane
        {
            Canvas = 0,
            Voxel = 1,
        }

        private const double DefaultVoxelPaneWidth = 560d;
        private const double MinVoxelPaneWidth = 360d;
        private double _lastVoxelPaneWidth = DefaultVoxelPaneWidth;
        private ActiveWorkspacePane _activePane = ActiveWorkspacePane.Canvas;

        public DocumentWorkspaceHost(CanvasDocument document, PaletteService? palette = null)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));

            InitializeComponent();

            CanvasHost = new CanvasViewHost(Document);
            VoxelWorkspace = new VoxelWorkspaceControl(Document, palette);

            CanvasHostPresenter.Content = CanvasHost;
            VoxelWorkspacePresenter.Content = VoxelWorkspace;
            var ws = Document.VoxelWorkspace;
            if (ws != null && ws.HasState && ws.VoxelPaneWidth >= MinVoxelPaneWidth)
            {
                _lastVoxelPaneWidth = ws.VoxelPaneWidth;
            }

            PaneSplitter.DefaultExpandedWidth = _lastVoxelPaneWidth;
            PaneSplitter.MinExpandedWidth = MinVoxelPaneWidth;
            HookWorkspaceActivationHandlers();

            if (VoxelPaneBorder != null)
            {
                VoxelPaneBorder.SizeChanged += (_, __) =>
                {
                    if (!IsVoxelPaneVisible)
                        return;

                    if (VoxelColumn.ActualWidth >= MinVoxelPaneWidth)
                    {
                        _lastVoxelPaneWidth = VoxelColumn.ActualWidth;
                        PaneSplitter.DefaultExpandedWidth = _lastVoxelPaneWidth;
                        PersistVoxelPaneState();
                    }
                };
            }

            SetVoxelPaneVisible(ws?.HasState == true && ws.VoxelPaneVisible);
            SetActivePane(ActiveWorkspacePane.Canvas);
        }

        public CanvasDocument Document { get; }

        public CanvasViewHost CanvasHost { get; }

        public VoxelWorkspaceControl VoxelWorkspace { get; }

        public bool IsVoxelPaneVisible { get; private set; }

        public bool IsVoxelPaneActive => IsVoxelPaneVisible && _activePane == ActiveWorkspacePane.Voxel;

        public event Action<bool>? VoxelPaneVisibilityChanged;

        public event Action<bool>? VoxelPaneActiveChanged;

        public void ToggleVoxelPane() => SetVoxelPaneVisible(!IsVoxelPaneVisible);

        public void SetVoxelPaneVisible(bool visible)
        {
            if (IsVoxelPaneVisible == visible)
                return;

            IsVoxelPaneVisible = visible;

            if (visible)
            {
                SplitterColumn.Width = new GridLength(6);
                VoxelColumn.Width = new GridLength(_lastVoxelPaneWidth);
                PaneSplitter.Visibility = Visibility.Visible;
                PaneSplitter.IsCollapsed = false;
                VoxelPaneBorder.Visibility = Visibility.Visible;
            }
            else
            {
                if (VoxelColumn.ActualWidth >= MinVoxelPaneWidth)
                {
                    _lastVoxelPaneWidth = VoxelColumn.ActualWidth;
                    PaneSplitter.DefaultExpandedWidth = _lastVoxelPaneWidth;
                }

                SplitterColumn.Width = new GridLength(0);
                VoxelColumn.Width = new GridLength(0);
                PaneSplitter.Visibility = Visibility.Collapsed;
                VoxelPaneBorder.Visibility = Visibility.Collapsed;
                SetActivePane(ActiveWorkspacePane.Canvas);
            }

            PersistVoxelPaneState();
            VoxelPaneVisibilityChanged?.Invoke(IsVoxelPaneVisible);
            UpdateActivePaneVisuals();
        }

        public void ShowVoxelPane() => SetVoxelPaneVisible(true);

        public void HideVoxelPane() => SetVoxelPaneVisible(false);

        private void PersistVoxelPaneState()
        {
            var ws = Document.VoxelWorkspace;
            ws.HasState = true;
            ws.VoxelPaneVisible = IsVoxelPaneVisible;
            ws.VoxelPaneWidth = Math.Max(MinVoxelPaneWidth, _lastVoxelPaneWidth);
        }

        private void HookWorkspaceActivationHandlers()
        {
            if (CanvasPaneBorder != null)
            {
                CanvasPaneBorder.AddHandler(
                    UIElement.PointerPressedEvent,
                    new PointerEventHandler(CanvasPane_PointerPressed),
                    handledEventsToo: true);
            }

            if (VoxelPaneBorder != null)
            {
                VoxelPaneBorder.AddHandler(
                    UIElement.PointerPressedEvent,
                    new PointerEventHandler(VoxelPane_PointerPressed),
                    handledEventsToo: true);
            }

            CanvasHost.GotFocus += (_, __) => SetActivePane(ActiveWorkspacePane.Canvas);
            VoxelWorkspace.GotFocus += (_, __) =>
            {
                if (IsVoxelPaneVisible)
                    SetActivePane(ActiveWorkspacePane.Voxel);
            };
        }

        private void CanvasPane_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            SetActivePane(ActiveWorkspacePane.Canvas);
        }

        private void VoxelPane_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!IsVoxelPaneVisible)
                return;
            SetActivePane(ActiveWorkspacePane.Voxel);
        }

        private void SetActivePane(ActiveWorkspacePane pane)
        {
            if (!IsVoxelPaneVisible && pane == ActiveWorkspacePane.Voxel)
                pane = ActiveWorkspacePane.Canvas;

            if (_activePane == pane)
            {
                UpdateActivePaneVisuals();
                return;
            }

            _activePane = pane;
            UpdateActivePaneVisuals();
            VoxelPaneActiveChanged?.Invoke(IsVoxelPaneActive);
        }

        private void UpdateActivePaneVisuals()
        {
            if (CanvasActiveIndicator != null)
            {
                CanvasActiveIndicator.Visibility =
                    _activePane == ActiveWorkspacePane.Canvas ? Visibility.Visible : Visibility.Collapsed;
            }

            if (VoxelActiveIndicator != null)
            {
                VoxelActiveIndicator.Visibility =
                    IsVoxelPaneVisible && _activePane == ActiveWorkspacePane.Voxel
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }
        }
    }
}
