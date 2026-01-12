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
            // Create the docking manager with the sidebar grid
            _dockingManager = new DockingManager(RightSidebar, OnPanelLayoutChanged);

            // Register all panels with the docking manager
            // Row indices match the XAML Grid.Row values for each SectionCard
            _dockingManager.RegisterPanel(new PanelInfo
            {
                Id = "Preview",
                Title = "Preview",
                Icon = Icon.PreviewLink,
                DockedContainer = PreviewCard,
                DockedRowIndex = 0
            });

            _dockingManager.RegisterPanel(new PanelInfo
            {
                Id = "Palette",
                Title = "Palette",
                Icon = Icon.Color,
                DockedContainer = PaletteCard,
                DockedRowIndex = 2
            });

            _dockingManager.RegisterPanel(new PanelInfo
            {
                Id = "Tiles",
                Title = "Tiles",
                Icon = Icon.Grid,
                DockedContainer = TilesCard,
                DockedRowIndex = 4
            });

            _dockingManager.RegisterPanel(new PanelInfo
            {
                Id = "Layers",
                Title = "Layers",
                Icon = Icon.LayerDiagonal,
                DockedContainer = LayersCard,
                DockedRowIndex = 6
            });

            _dockingManager.RegisterPanel(new PanelInfo
            {
                Id = "History",
                Title = "History",
                Icon = Icon.Clock,
                DockedContainer = HistoryCard,
                DockedRowIndex = 8
            });

            // Wire up undock events to use the docking manager
            PreviewCard.UndockRequested += OnPanelUndockRequested;
            PaletteCard.UndockRequested += OnPanelUndockRequested;
            TilesCard.UndockRequested += OnPanelUndockRequested;
            LayersCard.UndockRequested += OnPanelUndockRequested;
            HistoryCard.UndockRequested += OnPanelUndockRequested;

            // Wire up minimized changed events for dynamic row height adjustment
            PreviewCard.MinimizedChanged += OnPanelMinimizedChanged;
            PaletteCard.MinimizedChanged += OnPanelMinimizedChanged;
            TilesCard.MinimizedChanged += OnPanelMinimizedChanged;
            LayersCard.MinimizedChanged += OnPanelMinimizedChanged;
            HistoryCard.MinimizedChanged += OnPanelMinimizedChanged;

            // Wire up animation preview undock events
            AnimationPanel.AnimationPreviewUndockRequested += OnAnimationPreviewUndockRequested;
            AnimationPanel.AnimationPreviewDockRequested += OnAnimationPreviewDockRequested;

            // Initialize row heights based on initial minimized states
            UpdateSidebarRowHeights();
        }

        // ====================================================================
        // PANEL COLLAPSE/EXPAND HANDLING
        // ====================================================================

        /// <summary>
        /// Called when a panel's minimized state changes.
        /// Updates the sidebar row heights to redistribute space.
        /// </summary>
        private void OnPanelMinimizedChanged(SectionCard card, bool isMinimized)
        {
            UpdateSidebarRowHeights();
        }

        /// <summary>
        /// Helper to check if a panel is currently docked.
        /// </summary>
        private bool IsPanelDocked(string panelId)
        {
            if (_dockingManager == null) return true;
            if (_dockingManager.Panels.TryGetValue(panelId, out var panel))
            {
                return panel.IsDocked;
            }
            return true; // Default to docked if not found
        }

        /// <summary>
        /// Updates the sidebar row heights based on each panel's docked and minimized state.
        /// Undocked panels get 0 height, minimized panels get Auto height, 
        /// expanded panels share the remaining space with star sizing.
        /// </summary>
        private void UpdateSidebarRowHeights()
        {
            // Preview panel (row 0) and spacer (row 1)
            bool previewDocked = IsPanelDocked("Preview");
            bool previewMinimized = PreviewCard?.IsMinimized ?? false;
            if (PreviewRow != null)
            {
                if (!previewDocked)
                {
                    PreviewRow.Height = new GridLength(0);
                    PreviewRow.MinHeight = 0;
                }
                else if (previewMinimized)
                {
                    PreviewRow.Height = GridLength.Auto;
                    PreviewRow.MinHeight = 0;
                }
                else
                {
                    PreviewRow.Height = new GridLength(1, GridUnitType.Star);
                    PreviewRow.MinHeight = 60;
                }
            }
            if (PreviewSpacerRow != null)
                PreviewSpacerRow.Height = previewDocked && !previewMinimized ? new GridLength(4) : new GridLength(0);
            if (PreviewSpacer != null)
                PreviewSpacer.Visibility = previewDocked && !previewMinimized ? Visibility.Visible : Visibility.Collapsed;

            // Palette panel (row 2) and spacer (row 3)
            bool paletteDocked = IsPanelDocked("Palette");
            bool paletteMinimized = PaletteCard?.IsMinimized ?? false;
            if (PaletteRow != null)
            {
                if (!paletteDocked)
                {
                    PaletteRow.Height = new GridLength(0);
                    PaletteRow.MinHeight = 0;
                }
                else if (paletteMinimized)
                {
                    PaletteRow.Height = GridLength.Auto;
                    PaletteRow.MinHeight = 0;
                }
                else
                {
                    PaletteRow.Height = new GridLength(1, GridUnitType.Star);
                    PaletteRow.MinHeight = 60;
                }
            }
            if (PaletteSpacerRow != null)
                PaletteSpacerRow.Height = paletteDocked && !paletteMinimized ? new GridLength(4) : new GridLength(0);
            if (PaletteSpacer != null)
                PaletteSpacer.Visibility = paletteDocked && !paletteMinimized ? Visibility.Visible : Visibility.Collapsed;

            // Tiles panel (row 4) and spacer (row 5)
            bool tilesDocked = IsPanelDocked("Tiles");
            bool tilesMinimized = TilesCard?.IsMinimized ?? false;
            if (TilesRow != null)
            {
                if (!tilesDocked)
                {
                    TilesRow.Height = new GridLength(0);
                    TilesRow.MinHeight = 0;
                }
                else if (tilesMinimized)
                {
                    TilesRow.Height = GridLength.Auto;
                    TilesRow.MinHeight = 0;
                }
                else
                {
                    TilesRow.Height = new GridLength(1, GridUnitType.Star);
                    TilesRow.MinHeight = 60;
                }
            }
            if (TilesSpacerRow != null)
                TilesSpacerRow.Height = tilesDocked && !tilesMinimized ? new GridLength(4) : new GridLength(0);
            if (TilesSpacer != null)
                TilesSpacer.Visibility = tilesDocked && !tilesMinimized ? Visibility.Visible : Visibility.Collapsed;

            // Layers panel (row 6) and spacer (row 7)
            bool layersDocked = IsPanelDocked("Layers");
            bool layersMinimized = LayersCard?.IsMinimized ?? false;
            if (LayersRow != null)
            {
                if (!layersDocked)
                {
                    LayersRow.Height = new GridLength(0);
                    LayersRow.MinHeight = 0;
                }
                else if (layersMinimized)
                {
                    LayersRow.Height = GridLength.Auto;
                    LayersRow.MinHeight = 0;
                }
                else
                {
                    LayersRow.Height = new GridLength(1, GridUnitType.Star);
                    LayersRow.MinHeight = 60;
                }
            }
            if (LayersSpacerRow != null)
                LayersSpacerRow.Height = layersDocked && !layersMinimized ? new GridLength(4) : new GridLength(0);
            if (LayersSpacer != null)
                LayersSpacer.Visibility = layersDocked && !layersMinimized ? Visibility.Visible : Visibility.Collapsed;

            // History panel (row 8) - always Auto height when docked, 0 when undocked
            bool historyDocked = IsPanelDocked("History");
            if (HistoryRow != null)
                HistoryRow.Height = historyDocked ? GridLength.Auto : new GridLength(0);

            // Force the sidebar grid to recalculate layout
            if (RightSidebar != null)
            {
                RightSidebar.InvalidateMeasure();
                RightSidebar.InvalidateArrange();
                RightSidebar.UpdateLayout();
            }
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

        // ====================================================================
        // SIDEBAR PANEL DOCKING
        // ====================================================================

        private void OnPanelUndockRequested(SectionCard card)
        {
            if (_dockingManager == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Undock requested but DockingManager is null for panel: {card.PanelId}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MainWindow] Undocking panel: {card.PanelId}");
            _dockingManager.UndockPanel(card.PanelId);
        }

        /// <summary>
        /// Called when the panel layout changes (dock/undock events).
        /// Updates sidebar row heights to redistribute space when panels are docked/undocked.
        /// </summary>
        private void OnPanelLayoutChanged()
        {
            if (_dockingManager == null) return;

            // Update row heights - this will collapse undocked panels and redistribute space
            UpdateSidebarRowHeights();

            System.Diagnostics.Debug.WriteLine($"[MainWindow] Panel layout changed. Docked count: {_dockingManager.DockedCount}");
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
