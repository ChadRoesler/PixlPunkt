using System;
using System.Collections.Generic;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.UI.Controls;
using PixlPunkt.UI.Layers;
using PixlPunkt.UI.Palette;

namespace PixlPunkt.UI.Panels
{
    /// <summary>
    /// Defines information about a dockable panel.
    /// </summary>
    public sealed class PanelInfo
    {
        /// <summary>
        /// Unique identifier for the panel (e.g., "Preview", "Palette", "Tiles", "Layers").
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// Display title for the panel.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Icon to display for the panel.
        /// </summary>
        public Icon Icon { get; init; } = Icon.PanelLeft;

        /// <summary>
        /// The content element for the panel.
        /// </summary>
        public UIElement? Content { get; set; }

        /// <summary>
        /// The SectionCard that wraps this panel when docked.
        /// </summary>
        public FrameworkElement? DockedContainer { get; set; }

        /// <summary>
        /// The row index in the sidebar grid when docked.
        /// </summary>
        public int DockedRowIndex { get; init; }

        /// <summary>
        /// Whether the panel is currently docked (true) or floating (false).
        /// </summary>
        public bool IsDocked { get; set; } = true;

        /// <summary>
        /// The floating window when undocked.
        /// </summary>
        public PanelWindow? FloatingWindow { get; set; }
    }

    /// <summary>
    /// Manages docking and undocking of sidebar panels.
    /// </summary>
    public sealed class DockingManager
    {
        private readonly Dictionary<string, PanelInfo> _panels = new();
        private readonly Action _onLayoutChanged;
        private readonly Grid _sidebarGrid;

        /// <summary>
        /// Gets all registered panels.
        /// </summary>
        public IReadOnlyDictionary<string, PanelInfo> Panels => _panels;

        /// <summary>
        /// Gets the number of currently docked panels.
        /// </summary>
        public int DockedCount
        {
            get
            {
                int count = 0;
                foreach (var p in _panels.Values)
                {
                    if (p.IsDocked) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Gets whether all panels are undocked.
        /// </summary>
        public bool AllUndocked => DockedCount == 0;

        /// <summary>
        /// Creates a new docking manager.
        /// </summary>
        /// <param name="sidebarGrid">The sidebar grid that hosts docked panels.</param>
        /// <param name="onLayoutChanged">Callback invoked when dock state changes.</param>
        public DockingManager(Grid sidebarGrid, Action onLayoutChanged)
        {
            _sidebarGrid = sidebarGrid;
            _onLayoutChanged = onLayoutChanged;
        }

        /// <summary>
        /// Registers a panel with the docking manager.
        /// </summary>
        public void RegisterPanel(PanelInfo panel)
        {
            _panels[panel.Id] = panel;

            // Wire up DockRequested event from SectionCard
            if (panel.DockedContainer is SectionCard card)
            {
                card.DockRequested += OnSectionCardDockRequested;
            }
        }

        /// <summary>
        /// Undocks a panel to a floating window.
        /// </summary>
        /// <param name="panelId">The panel identifier.</param>
        /// <returns>The created floating window, or null if panel not found or already undocked.</returns>
        public PanelWindow? UndockPanel(string panelId)
        {
            if (!_panels.TryGetValue(panelId, out var panel))
                return null;

            if (!panel.IsDocked || panel.DockedContainer == null)
                return null;

            try
            {
                // Capture the docked panel's current size before removing
                double dockedWidth = 0;
                double dockedHeight = 0;
                if (panel.DockedContainer is FrameworkElement fe)
                {
                    dockedWidth = fe.ActualWidth;
                    dockedHeight = fe.ActualHeight;
                }

                // Remove the SectionCard from the sidebar grid
                _sidebarGrid.Children.Remove(panel.DockedContainer);
                // Set floating state on SectionCard
                if (panel.DockedContainer is SectionCard card)
                {
                    card.IsFloating = true;
                    card.IsMinimized = false;
                    if (card.CustomControl is LayersPanel layersPanel)
                    {
                        layersPanel.WireFlyoutEvents();
                    }
                    else if (card.CustomControl is PalettePanel palettePanel)
                    {
                        palettePanel.WireFlyoutEvents();
                    }
                }
                // Create floating window with the SectionCard as content
                var window = new PanelWindow(panelId, panel.Title, panel.DockedContainer, dockedWidth, dockedHeight);
                panel.FloatingWindow = window;
                panel.IsDocked = false;

                // Wire events
                window.WindowClosing += OnWindowClosing;

                // Notify layout changed
                _onLayoutChanged();

                // Show window
                window.Activate();

                return window;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DockingManager] ERROR undocking panel '{panelId}': {ex.Message}");

                // Try to restore state on failure
                if (panel.DockedContainer != null && !_sidebarGrid.Children.Contains(panel.DockedContainer))
                {
                    try
                    {
                        Grid.SetRow(panel.DockedContainer, panel.DockedRowIndex);
                        _sidebarGrid.Children.Add(panel.DockedContainer);
                    }
                    catch { }
                }
                panel.IsDocked = true;
                panel.FloatingWindow = null;

                return null;
            }
        }

        /// <summary>
        /// Docks a panel back to the sidebar.
        /// </summary>
        /// <param name="panelId">The panel identifier.</param>
        /// <returns>True if successfully docked.</returns>
        public bool DockPanel(string panelId)
        {
            if (!_panels.TryGetValue(panelId, out var panel))
                return false;

            if (panel.IsDocked)
                return true;

            try
            {
                // Detach content from floating window
                if (panel.FloatingWindow != null)
                {
                    panel.FloatingWindow.WindowClosing -= OnWindowClosing;
                    panel.FloatingWindow.DetachContent();
                    try { panel.FloatingWindow.Close(); } catch { }
                    panel.FloatingWindow = null;
                }

                // Clear floating state on SectionCard
                if (panel.DockedContainer is SectionCard card)
                {
                    card.IsFloating = false;
                }

                // Re-add the SectionCard to the sidebar grid at its original row
                if (panel.DockedContainer != null)
                {
                    Grid.SetRow(panel.DockedContainer, panel.DockedRowIndex);
                    if (!_sidebarGrid.Children.Contains(panel.DockedContainer))
                    {
                        _sidebarGrid.Children.Add(panel.DockedContainer);
                    }
                }

                panel.IsDocked = true;

                // Notify layout changed
                _onLayoutChanged();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DockingManager] ERROR docking panel '{panelId}': {ex.Message}");
                return false;
            }
        }

        private void OnSectionCardDockRequested(SectionCard card)
        {
            DockPanel(card.PanelId);
        }

        private void OnWindowClosing(PanelWindow window)
        {
            try
            {
                // When window is closed via X button, dock the panel back
                if (_panels.TryGetValue(window.PanelId, out var panel) && !panel.IsDocked)
                {
                    // Detach content before window fully closes
                    window.DetachContent();
                    panel.FloatingWindow = null;

                    // Clear floating state on SectionCard
                    if (panel.DockedContainer is SectionCard card)
                    {
                        card.IsFloating = false;
                    }

                    // Re-add the SectionCard to the sidebar grid at its original row
                    if (panel.DockedContainer != null)
                    {
                        Grid.SetRow(panel.DockedContainer, panel.DockedRowIndex);
                        if (!_sidebarGrid.Children.Contains(panel.DockedContainer))
                        {
                            _sidebarGrid.Children.Add(panel.DockedContainer);
                        }
                    }

                    panel.IsDocked = true;

                    // Notify layout changed
                    _onLayoutChanged();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DockingManager] ERROR on window closing '{window.PanelId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Closes all floating windows and docks all panels.
        /// </summary>
        public void DockAll()
        {
            foreach (var panel in _panels.Values)
            {
                if (!panel.IsDocked)
                {
                    DockPanel(panel.Id);
                }
            }
        }
    }
}
