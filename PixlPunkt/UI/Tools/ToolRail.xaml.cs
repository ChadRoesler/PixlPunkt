using System;
using System.Collections.Generic;
using System.Linq;
using FluentIcons.Common;
using FluentIcons.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Tools;
using PixlPunkt.UI.ColorPick;

namespace PixlPunkt.UI.Tools
{
    /// <summary>
    /// Vertical toolbar hosting dynamically generated tool selection buttons
    /// organized by category, with foreground/background swatches linked to the PaletteService.
    /// </summary>
    public sealed partial class ToolRail : UserControl
    {
        // ════════════════════════════════════════════════════════════════════
        // FIELDS
        // ════════════════════════════════════════════════════════════════════

        private ToolState? _toolState;
        private ColorPickerWindow? _openPicker;

        /// <summary>Tool buttons keyed by string tool ID.</summary>
        private readonly Dictionary<string, ToggleButton> _toolButtons = new();

        /// <summary>Category display order and labels.</summary>
        private static readonly (ToolCategory Category, string Label)[] CategoryOrder =
        [
            (ToolCategory.Utility, "Utility"),
            (ToolCategory.Select, "Selection"),
            (ToolCategory.Brush, "Brush"),
            (ToolCategory.Tile, "Tile"),
            (ToolCategory.Shape, "Shape")
        ];

        // ════════════════════════════════════════════════════════════════════
        // PROPERTIES - UI BRUSHES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Foreground swatch brush (kept in sync with PaletteService).</summary>
        public SolidColorBrush FgBrush { get; } = new(Colors.Black);

        /// <summary>Background swatch brush (kept in sync with PaletteService).</summary>
        public SolidColorBrush BgBrush { get; } = new(Colors.White);

        // ════════════════════════════════════════════════════════════════════
        // DEPENDENCY PROPERTIES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets or sets the palette service backing this rail.
        /// </summary>
        public PaletteService? Service
        {
            get => (PaletteService)GetValue(ServiceProperty);
            set => SetValue(ServiceProperty, value);
        }

        public static readonly DependencyProperty ServiceProperty =
            DependencyProperty.Register(
                nameof(Service),
                typeof(PaletteService),
                typeof(ToolRail),
                new PropertyMetadata(null, OnServiceChanged));

        // ════════════════════════════════════════════════════════════════════
        // EVENTS (PUBLIC)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Raised when the currently effective tool changes (string ID version).</summary>
        public event Action<string>? ToolIdChanged;


        // ════════════════════════════════════════════════════════════════════
        // CALLBACKS (OPTIONAL INTEGRATIONS)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Optional provider for current brush opacity (0-255).</summary>
        public Func<byte>? GetBrushOpacity { get; set; }

        /// <summary>Optional setter for brush opacity (0-255) requested by this rail.</summary>
        public Action<byte>? RequestSetBrushOpacity { get; set; }

        /// <summary>Optional setter for brush color (BGRA) requested by this rail.</summary>
        public Action<uint>? RequestSetBrushColor { get; set; }

        // ════════════════════════════════════════════════════════════════════
        // CTOR
        // ════════════════════════════════════════════════════════════════════

        public ToolRail()
        {
            InitializeComponent();
            Quick.HostRail = this;

            // Subscribe to tool registry changes for plugin support
            if (ToolRegistry.Shared is ToolRegistry registry)
            {
                registry.ToolsChanged += OnToolsChanged;
            }

            // Subscribe to theme changes to rebuild tool buttons with correct theme resources
            ActualThemeChanged += OnActualThemeChanged;
        }

        /// <summary>
        /// Handles theme changes by rebuilding the tool buttons so theme resources resolve correctly.
        /// </summary>
        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            // Only rebuild if we have a tool state bound
            if (_toolState != null)
            {
                RebuildToolButtons();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Binds the toolbar to a ToolState and generates tool buttons dynamically.
        /// </summary>
        public void BindToolState(ToolState tool)
        {
            if (_toolState != null)
            {
                _toolState.ActiveToolIdChanged -= OnToolIdChanged;
            }

            _toolState = tool;
            _toolState.ActiveToolIdChanged += OnToolIdChanged;

            // Generate tool buttons
            RebuildToolButtons();

            // Reflect initial state
            OnToolIdChanged(_toolState.ActiveToolId);
        }

        /// <summary>
        /// Registers a newly opened ColorPickerWindow so this rail can push external alpha changes.
        /// </summary>
        public void RegisterOpenPicker(ColorPickerWindow window)
        {
            _openPicker = window;
            window.Closed += (_, __) =>
            {
                if (_openPicker == window)
                    _openPicker = null;
            };
        }

        /// <summary>
        /// Unregisters a previously tracked ColorPickerWindow reference.
        /// </summary>
        public void UnregisterOpenPicker(ColorPickerWindow window)
        {
            if (_openPicker == window)
                _openPicker = null;
        }

        /// <summary>
        /// Notifies the registered ColorPickerWindow that brush alpha changed externally.
        /// </summary>
        public void NotifyBrushOpacityChanged(byte alpha)
        {
            _openPicker?.SetExternalAlpha(alpha);
        }

        // ════════════════════════════════════════════════════════════════════
        // TOOL BUTTON GENERATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles tool registry changes (e.g., plugin tools added/removed).
        /// </summary>
        private void OnToolsChanged()
        {
            // Rebuild buttons on UI thread
            DispatcherQueue.TryEnqueue(() => RebuildToolButtons());
        }

        /// <summary>
        /// Rebuilds the tool container with categorized sections.
        /// </summary>
        private void RebuildToolButtons()
        {
            if (_toolState == null) return;

            string currentToolId = _toolState.ActiveToolId;

            ToolContainer.Children.Clear();
            _toolButtons.Clear();

            // Group tools by category
            var registry = ToolRegistry.Shared;
            var allTools = registry.GetAll().ToList();
            var toolsByCategory = allTools
                .GroupBy(t => t.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

            bool isFirstCategory = true;

            foreach (var (category, label) in CategoryOrder)
            {
                if (!toolsByCategory.TryGetValue(category, out var tools) || tools.Count == 0)
                    continue;

                // Sort tools: built-in tools first (IDs starting with "pixlpunkt."), then plugin tools at end
                // Within each group, maintain registration order
                var sortedTools = tools
                    .OrderBy(t => IsBuiltInTool(t.Id) ? 0 : 1)
                    .ToList();

                // Add divider between categories (not before first)
                if (!isFirstCategory)
                {
                    // Use the style defined in XAML resources which has proper ThemeResource binding
                    var divider = new Border();
                    if (Resources.TryGetValue("CategoryDivider", out var dividerStyle) && dividerStyle is Style style)
                    {
                        divider.Style = style;
                    }
                    else
                    {
                        // Fallback: set properties directly (won't be theme-aware)
                        divider.Height = 1;
                        divider.Margin = new Thickness(0, 8, 0, 4);
                        divider.HorizontalAlignment = HorizontalAlignment.Stretch;
                    }
                    ToolContainer.Children.Add(divider);
                }
                isFirstCategory = false;

                // Add category label - use style if available
                var labelBlock = new TextBlock { Text = label };
                if (Resources.TryGetValue("CategoryLabel", out var labelStyle) && labelStyle is Style lblStyle)
                {
                    labelBlock.Style = lblStyle;
                }
                else
                {
                    labelBlock.FontSize = 11;
                    labelBlock.Opacity = 0.6;
                    labelBlock.Margin = new Thickness(2, 0, 0, 4);
                }
                ToolContainer.Children.Add(labelBlock);

                // Create 2-column grid for this category
                // Don't set Background - let it be transparent so parent background shows through
                var grid = new Grid
                {
                    ColumnSpacing = 8,
                    RowSpacing = 8
                };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Calculate number of rows needed
                int rowCount = (sortedTools.Count + 1) / 2;
                for (int r = 0; r < rowCount; r++)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                int index = 0;
                foreach (var registration in sortedTools)
                {
                    var settings = registration.Settings;
                    Icon icon = settings?.Icon ?? GetDefaultIconForCategory(registration.Category);
                    string tooltip = settings?.TooltipText ?? registration.DisplayName;

                    var button = CreateToolButton(registration.Id, icon, tooltip);
                    Grid.SetColumn(button, index % 2);
                    Grid.SetRow(button, index / 2);
                    grid.Children.Add(button);
                    _toolButtons[registration.Id] = button;
                    index++;
                }

                // Add empty spacer if odd number of tools (keeps grid even)
                if (sortedTools.Count % 2 == 1)
                {
                    var spacer = new Border();
                    Grid.SetColumn(spacer, 1);
                    Grid.SetRow(spacer, (sortedTools.Count - 1) / 2);
                    grid.Children.Add(spacer);
                }

                ToolContainer.Children.Add(grid);
            }

            // Restore selection
            if (_toolButtons.TryGetValue(currentToolId, out var btn))
            {
                btn.IsChecked = true;
            }
        }

        /// <summary>
        /// Determines if a tool ID represents a built-in tool.
        /// Built-in tools use the "pixlpunkt." prefix with standard naming.
        /// </summary>
        private static bool IsBuiltInTool(string toolId)
        {
            // Built-in tools start with "pixlpunkt." and have exactly 3 parts (vendor.category.name)
            // Plugin tools either don't start with "pixlpunkt." or have different structure
            if (!toolId.StartsWith("pixlpunkt.", StringComparison.OrdinalIgnoreCase))
                return false;

            // Count dots to ensure it's a simple built-in ID pattern
            int dotCount = toolId.Count(c => c == '.');
            return dotCount == 2; // e.g., "pixlpunkt.brush.brush"
        }

        /// <summary>
        /// Gets a default icon for tools without settings.
        /// </summary>
        private static Icon GetDefaultIconForCategory(ToolCategory category)
        {
            return category switch
            {
                ToolCategory.Utility => Icon.Wrench,
                ToolCategory.Select => Icon.SelectObject,
                ToolCategory.Brush => Icon.PaintBrush,
                ToolCategory.Tile => Icon.Table,
                ToolCategory.Shape => Icon.ShapeOrganic,
                _ => Icon.Apps
            };
        }

        /// <summary>
        /// Creates a tool toggle button with the specified icon and tooltip.
        /// </summary>
        private ToggleButton CreateToolButton(string toolId, Icon icon, string tooltip)
        {
            // Don't explicitly set Background - let ToggleButton use its default theme-aware styling
            var button = new ToggleButton
            {
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(0),
                Tag = toolId,
                Content = new FluentIcon
                {
                    Icon = icon,
                    Style = (Style)Application.Current.Resources["ToolRailIcon"]
                }
            };

            ToolTipService.SetToolTip(button, tooltip);
            button.Click += ToolButton_Click;

            return button;
        }

        /// <summary>
        /// Handles tool button clicks.
        /// </summary>
        private void ToolButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && btn.Tag is string toolId)
            {
                SetTool(toolId);
            }
        }

        /// <summary>
        /// Sets the active tool by string ID and updates button states.
        /// </summary>
        private void SetTool(string toolId)
        {
            ClearAllToolButtons();
            if (_toolButtons.TryGetValue(toolId, out var btn))
            {
                btn.IsChecked = true;
            }
            _toolState?.SetById(toolId);
        }

        // ════════════════════════════════════════════════════════════════════
        // TOOL STATE MIRRORING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Mirrors the current effective tool into toggle button states (string ID version).
        /// </summary>
        private void OnToolIdChanged(string toolId)
        {
            ClearAllToolButtons();
            if (_toolButtons.TryGetValue(toolId, out var btn))
            {
                btn.IsChecked = true;
            }

            // Raise string ID event
            ToolIdChanged?.Invoke(toolId);
        }

        /// <summary>
        /// Unchecks all tool buttons.
        /// </summary>
        private void ClearAllToolButtons()
        {
            foreach (var btn in _toolButtons.Values)
            {
                btn.IsChecked = false;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PALETTE SERVICE WIRING
        // ════════════════════════════════════════════════════════════════════

        private static void OnServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (ToolRail)d;

            if (e.OldValue is PaletteService oldService)
            {
                oldService.ForegroundChanged -= self.OnForegroundChanged;
                oldService.BackgroundChanged -= self.OnBackgroundChanged;
                oldService.PaletteChanged -= self.OnPaletteChanged;
            }

            if (e.NewValue is PaletteService newService)
            {
                self.Quick.Service = newService;
                newService.ForegroundChanged += self.OnForegroundChanged;
                newService.BackgroundChanged += self.OnBackgroundChanged;
                newService.PaletteChanged += self.OnPaletteChanged;
                self.SyncSwatchChips();
            }
        }

        private void OnForegroundChanged(uint color) => SyncSwatchChips();
        private void OnBackgroundChanged(uint color) => SyncSwatchChips();
        private void OnPaletteChanged() => SyncSwatchChips();

        private void SyncSwatchChips()
        {
            if (Service is null) return;
            FgBrush.Color = ColorUtil.ToColor(Service.Foreground);
            BgBrush.Color = ColorUtil.ToColor(Service.Background);
        }
    }
}
