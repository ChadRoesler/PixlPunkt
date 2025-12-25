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
            _dockingManager = new DockingManager(RightSidebar, OnPanelLayoutChanged);

            // Register panels with the docking manager
            RegisterPanel(PreviewCard, 0, Icon.PreviewLink);
            RegisterPanel(PaletteCard, 2, Icon.Color);
            RegisterPanel(TilesCard, 4, Icon.Grid);
            RegisterPanel(LayersCard, 6, Icon.LayerDiagonal);
            RegisterPanel(HistoryCard, 8, Icon.Clock);

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

        private void RegisterPanel(SectionCard card, int rowIndex, Icon icon)
        {
            var panelInfo = new PanelInfo
            {
                Id = card.PanelId,
                Title = card.Title,
                Icon = icon,
                DockedContainer = card,
                DockedRowIndex = rowIndex,
                IsDocked = true
            };

            _dockingManager?.RegisterPanel(panelInfo);
        }

        private void OnPanelUndockRequested(SectionCard card)
        {
            if (_dockingManager == null || string.IsNullOrEmpty(card.PanelId))
                return;

            _dockingManager.UndockPanel(card.PanelId);
        }

        /// <summary>
        /// Called when the panel layout changes (dock/undock events).
        /// Updates sidebar visibility based on docked panel count.
        /// </summary>
        private void OnPanelLayoutChanged()
        {
            if (_dockingManager == null) return;

            bool hasDockedPanels = !_dockingManager.AllUndocked;

            // Update visibility of sidebar and splitter
            RightSidebar.Visibility = hasDockedPanels ? Visibility.Visible : Visibility.Collapsed;
            RightSplitter.Visibility = hasDockedPanels ? Visibility.Visible : Visibility.Collapsed;

            // Update column widths
            if (hasDockedPanels)
            {
                RightSplitterColumn.Width = new GridLength(6);
                RightSidebarColumn.Width = new GridLength(360);
            }
            else
            {
                RightSplitterColumn.Width = new GridLength(0);
                RightSidebarColumn.Width = new GridLength(0);
            }

            // Update splitter visibility based on adjacent docked panels
            UpdateSplitterVisibility();
        }

        /// <summary>
        /// Updates the visibility of splitters between panels based on which panels are docked.
        /// </summary>
        private void UpdateSplitterVisibility()
        {
            if (_dockingManager == null) return;

            var panels = _dockingManager.Panels;

            // Get docked state for each panel
            bool previewDocked = panels.TryGetValue("Preview", out var p1) && p1.IsDocked;
            bool paletteDocked = panels.TryGetValue("Palette", out var p2) && p2.IsDocked;
            bool tilesDocked = panels.TryGetValue("Tiles", out var p3) && p3.IsDocked;
            bool layersDocked = panels.TryGetValue("Layers", out var p4) && p4.IsDocked;
            bool historyDocked = panels.TryGetValue("History", out var p5) && p5.IsDocked;

            // Splitter between Preview and Palette - visible if both are docked
            PreviewSplitter.Visibility = (previewDocked && paletteDocked) ? Visibility.Visible : Visibility.Collapsed;

            // Splitter between Palette and Tiles - visible if both are docked
            PaletteSplitter.Visibility = (paletteDocked && tilesDocked) ? Visibility.Visible : Visibility.Collapsed;

            // Splitter between Tiles and Layers - visible if both are docked
            TilesSplitter.Visibility = (tilesDocked && layersDocked) ? Visibility.Visible : Visibility.Collapsed;

            // Splitter between Layers and History - visible if both are docked
            HistorySplitter.Visibility = (layersDocked && historyDocked) ? Visibility.Visible : Visibility.Collapsed;

            // Update row sizing for hidden elements
            UpdateSidebarRowSizing();
        }

        /// <summary>
        /// Updates the row sizing in the sidebar grid based on which panels are visible.
        /// </summary>
        private void UpdateSidebarRowSizing()
        {
            if (_dockingManager == null) return;

            var panels = _dockingManager.Panels;

            // Count docked panels to determine star sizing
            int dockedCount = 0;
            foreach (var kvp in panels)
            {
                if (kvp.Value.IsDocked) dockedCount++;
            }

            if (dockedCount == 0) return;

            // Set row heights based on docked state
            // Panel rows get Star(*) if docked, Auto if not
            // Splitter rows get fixed height if adjacent panels are docked, 0 otherwise

            var rowDefs = RightSidebar.RowDefinitions;

            // Row 0: Preview
            SetPanelRowHeight(rowDefs, 0, panels.TryGetValue("Preview", out var preview) && preview.IsDocked);

            // Row 1: Preview-Palette splitter (6px or 0)
            bool previewDocked = panels.TryGetValue("Preview", out var p1) && p1.IsDocked;
            bool paletteDocked = panels.TryGetValue("Palette", out var p2) && p2.IsDocked;
            rowDefs[1].Height = (previewDocked && paletteDocked) ? new GridLength(6) : new GridLength(0);

            // Row 2: Palette
            SetPanelRowHeight(rowDefs, 2, paletteDocked);

            // Row 3: Palette-Tiles splitter
            bool tilesDocked = panels.TryGetValue("Tiles", out var p3) && p3.IsDocked;
            rowDefs[3].Height = (paletteDocked && tilesDocked) ? new GridLength(6) : new GridLength(0);

            // Row 4: Tiles
            SetPanelRowHeight(rowDefs, 4, tilesDocked);

            // Row 5: Tiles-Layers splitter
            bool layersDocked = panels.TryGetValue("Layers", out var p4) && p4.IsDocked;
            rowDefs[5].Height = (tilesDocked && layersDocked) ? new GridLength(6) : new GridLength(0);

            // Row 6: Layers
            SetPanelRowHeight(rowDefs, 6, layersDocked);

            // Row 7: Layers-History splitter
            bool historyDocked = panels.TryGetValue("History", out var p5) && p5.IsDocked;
            rowDefs[7].Height = (layersDocked && historyDocked) ? new GridLength(6) : new GridLength(0);

            // Row 8: History
            SetPanelRowHeight(rowDefs, 8, historyDocked);
        }

        private static void SetPanelRowHeight(RowDefinitionCollection rowDefs, int index, bool isDocked)
        {
            if (index < rowDefs.Count)
            {
                rowDefs[index].Height = isDocked
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
            }
        }

        /// <summary>
        /// Docks all floating panels back to the sidebar.
        /// </summary>
        private void DockAllPanels()
        {
            _dockingManager?.DockAll();
        }
    }
}
