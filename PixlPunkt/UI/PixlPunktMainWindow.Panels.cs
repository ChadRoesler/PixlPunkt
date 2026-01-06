using System;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.UI.Controls;
using PixlPunkt.UI.Helpers;
using PixlPunkt.UI.Panels;

namespace PixlPunkt.UI
{
    /// <summary>
    /// Partial class for panel docking/undocking management.
    /// </summary>
    public sealed partial class PixlPunktMainWindow : Window
    {
        private DockingManager? _dockingManager;
        private PanelWindow? _animationPreviewWindow;
        private Grid? _animationPreviewContainerBackup;
        private double _animationPreviewColumnWidth = 200;

        /// <summary>
        /// Initializes the panel docking system.
        /// </summary>
        private void InitializePanelDocking()
        {
            // Simplified initialization - no grid-based docking manager for now
            // Just wire up undock events directly

            // Wire up undock events
            PreviewCard.UndockRequested += OnPanelUndockRequested;
            PaletteCard.UndockRequested += OnPanelUndockRequested;
            TilesCard.UndockRequested += OnPanelUndockRequested;
            LayersCard.UndockRequested += OnPanelUndockRequested;
            HistoryCard.UndockRequested += OnPanelUndockRequested;

            // Wire up animation preview undock events
            AnimationPanel.AnimationPreviewUndockRequested += OnAnimationPreviewUndockRequested;
            AnimationPanel.AnimationPreviewDockRequested += OnAnimationPreviewDockRequested;
        }

        // ====================================================================
        // ANIMATION PREVIEW DOCKING
        // ====================================================================

        private void OnAnimationPreviewUndockRequested()
        {
            // Prevent double-undocking
            if (_animationPreviewWindow != null) return;

            try
            {
                // Get the container from AnimationPanel
                var container = AnimationPanel.GetStagePreviewContainer();
                if (container == null) return;

                // Save the current column width
                var col = AnimationPanel.GetStagePreviewColumn();
                if (col.ActualWidth > 0)
                {
                    _animationPreviewColumnWidth = col.ActualWidth;
                }

                // Remove from parent
                var parent = container.Parent as Grid;
                if (parent != null)
                {
                    parent.Children.Remove(container);
                }

                // Hide the column and splitter in the animation panel
                AnimationPanel.GetStagePreviewColumn().Width = new GridLength(0);
                AnimationPanel.GetStagePreviewSplitterColumn().Width = new GridLength(0);

                // Also hide the splitter element if accessible
                if (AnimationPanel.FindName("StagePreviewSplitter") is UIElement splitter)
                {
                    splitter.Visibility = Visibility.Collapsed;
                }

                // Create floating window
                _animationPreviewWindow = new PanelWindow("AnimationPreview", "Animation Preview", container);
                _animationPreviewWindow.WindowClosing += OnAnimationPreviewWindowClosing;

                // Update state
                AnimationPanel.IsAnimationPreviewFloating = true;

                // Show window
                _animationPreviewWindow.Activate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] ERROR undocking animation preview: {ex.Message}");
            }
        }

        private void OnAnimationPreviewDockRequested()
        {
            DockAnimationPreview();
        }

        private void OnAnimationPreviewWindowClosing(PanelWindow window)
        {
            // Only dock if this is our window (prevents issues with stale references)
            if (window == _animationPreviewWindow)
            {
                DockAnimationPreview();
            }
        }

        private void DockAnimationPreview()
        {
            if (_animationPreviewWindow == null) return;

            try
            {
                // Get the container back
                var container = _animationPreviewWindow.DetachContent() as Grid;
                
                // Unhook events first
                _animationPreviewWindow.WindowClosing -= OnAnimationPreviewWindowClosing;

                // Close the window
                try { _animationPreviewWindow.Close(); } catch { }
                _animationPreviewWindow = null;

                if (container == null) return;

                // Re-add to animation panel's canvas content grid
                // The container goes into row 1, column 2 of CanvasAnimationContent
                var canvasContent = AnimationPanel.FindName("CanvasAnimationContent") as Grid;
                if (canvasContent != null)
                {
                    Grid.SetRow(container, 1);
                    Grid.SetColumn(container, 2);
                    canvasContent.Children.Add(container);
                }

                // Show the splitter element
                if (AnimationPanel.FindName("StagePreviewSplitter") is UIElement splitter)
                {
                    splitter.Visibility = Visibility.Visible;
                }

                // Restore column widths
                AnimationPanel.GetStagePreviewColumn().Width = new GridLength(_animationPreviewColumnWidth);
                AnimationPanel.GetStagePreviewSplitterColumn().Width = new GridLength(6);

                // Update state
                AnimationPanel.IsAnimationPreviewFloating = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] ERROR docking animation preview: {ex.Message}");
            }
        }

        private void OnPanelUndockRequested(SectionCard card)
        {
            // Simplified undocking - just hide the card for now
            // Full floating window support would require more work
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Undock requested for panel: {card.PanelId}");
        }

        /// <summary>
        /// Called when the panel layout changes (dock/undock events).
        /// Updates sidebar visibility based on docked panel count.
        /// </summary>
        private void OnPanelLayoutChanged()
        {
            // Simplified - sidebar is always visible when we have panels
            // No need to manage visibility anymore with StackPanel layout
        }

        /// <summary>
        /// Docks all floating panels back to the sidebar.
        /// </summary>
        private void DockAllPanels()
        {
            // Simplified - no floating panels in simplified mode
        }
    }
}
