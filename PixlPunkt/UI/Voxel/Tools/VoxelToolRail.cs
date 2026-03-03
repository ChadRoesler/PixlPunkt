using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using FluentIcons.Common;
using FluentIcons.WinUI;
using PixlPunkt.Core.Voxel.Tools;
using PixlPunkt.UI.Icons;

namespace PixlPunkt.UI.Voxel.Tools
{
    /// <summary>
    /// Voxel tool rail driven by <see cref="VoxelToolState"/>.
    /// </summary>
    public sealed class VoxelToolRail : UserControl
    {
        private readonly record struct ToolVisual(
            string Label,
            Icon FallbackIcon,
            PixlPunktCodicon? CustomGlyph = null,
            double CustomGlyphScale = 1d);

        private const double GlyphSize = 20d;
        private const double CompactButtonSize = 32d;

        private readonly StackPanel _panel;
        private readonly ScrollViewer _scrollViewer;
        private VoxelToolState? _toolState;
        private Orientation _orientation = Orientation.Horizontal;
        private bool _showLabels = true;

        public VoxelToolRail()
        {
            _panel = new StackPanel
            {
                Orientation = _orientation,
                Spacing = 4,
            };

            _scrollViewer = new ScrollViewer
            {
                Content = _panel
            };

            UpdateScrollBars();
            Content = _scrollViewer;

            Loaded += (_, __) => Rebuild();
        }

        /// <summary>
        /// Gets or sets the button layout orientation.
        /// </summary>
        public Orientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation == value)
                    return;

                _orientation = value;
                _panel.Orientation = value;
                UpdateScrollBars();
                Rebuild();
            }
        }

        /// <summary>
        /// Gets or sets whether labels are shown next to icons.
        /// </summary>
        public bool ShowLabels
        {
            get => _showLabels;
            set
            {
                if (_showLabels == value)
                    return;

                _showLabels = value;
                Rebuild();
            }
        }

        public VoxelToolState? ToolState
        {
            get => _toolState;
            set
            {
                if (ReferenceEquals(_toolState, value))
                    return;

                Unwire(_toolState);
                _toolState = value;
                Wire(_toolState);
                Rebuild();
            }
        }

        private void Wire(VoxelToolState? state)
        {
            if (state == null) return;
            state.ActiveToolChanged += OnActiveToolChanged;
            state.OptionsChanged += OnOptionsChanged;
            state.Registry.ToolsChanged += OnRegistryToolsChanged;
        }

        private void Unwire(VoxelToolState? state)
        {
            if (state == null) return;
            state.ActiveToolChanged -= OnActiveToolChanged;
            state.OptionsChanged -= OnOptionsChanged;
            state.Registry.ToolsChanged -= OnRegistryToolsChanged;
        }

        private void OnActiveToolChanged(string? _) => Rebuild();
        private void OnOptionsChanged() { /* no-op for rail */ }
        private void OnRegistryToolsChanged() => Rebuild();

        private void Rebuild()
        {
            _panel.Children.Clear();

            var state = _toolState;
            if (state == null)
            {
                _panel.Children.Add(new TextBlock { Text = "No voxel tool state", Opacity = 0.7 });
                return;
            }

            var tools = state.Registry.GetAll();
            bool any = false;
            foreach (var tool in tools)
            {
                any = true;
                var visual = GetToolVisual(tool.Id, tool.DisplayName);
                var btn = new ToggleButton
                {
                    Content = CreateButtonContent(visual),
                    MinWidth = _showLabels ? 0 : CompactButtonSize,
                    MinHeight = CompactButtonSize,
                    Padding = _showLabels ? new Thickness(6, 2, 6, 2) : new Thickness(0),
                    FontSize = 11,
                    IsChecked = tool.Id == state.ActiveToolId,
                    Tag = tool.Id,
                    CornerRadius = new CornerRadius(8),
                };

                if (!_showLabels)
                {
                    btn.Width = CompactButtonSize;
                    btn.Height = CompactButtonSize;
                }

                btn.Click += (_, __) =>
                {
                    var id = btn.Tag as string;
                    if (!string.IsNullOrWhiteSpace(id))
                        state.SetActiveTool(id);
                };

                ToolTipService.SetToolTip(btn, tool.DisplayName);
                _panel.Children.Add(btn);
            }

            if (!any)
            {
                _panel.Children.Add(new TextBlock
                {
                    Text = "No voxel tools registered",
                    Opacity = 0.7
                });
            }
        }

        private object CreateButtonContent(in ToolVisual visual)
        {
            if (_showLabels)
            {
                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                stack.Children.Add(CreateGlyph(visual));
                stack.Children.Add(new TextBlock
                {
                    Text = visual.Label,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                });

                return stack;
            }

            return CreateGlyph(visual);
        }

        private static UIElement CreateGlyph(in ToolVisual visual)
        {
            if (visual.CustomGlyph is PixlPunktCodicon glyph &&
                PixlPunktIconFont.TryCreateGlyph(glyph, GlyphSize, out var customGlyph, visual.CustomGlyphScale))
            {
                return customGlyph;
            }

            return new FluentIcon
            {
                Icon = visual.FallbackIcon,
                FontSize = GlyphSize,
            };
        }

        private static ToolVisual GetToolVisual(string id, string? displayName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return new ToolVisual(GetCompactLabel(displayName), Icon.Apps);
            }

            return id switch
            {
                VoxelToolIds.FacePaint => new ToolVisual("Paint", Icon.PaintBrush),
                VoxelToolIds.FaceDropper => new ToolVisual("Dropper", Icon.Syringe),
                VoxelToolIds.FaceEraseOverride => new ToolVisual("Erase", Icon.Eraser),
                VoxelToolIds.VoxelCreate => new ToolVisual("Create", Icon.CubeAdd),
                VoxelToolIds.VoxelDelete => new ToolVisual("Delete", Icon.Delete, CustomGlyph: PixlPunktCodicon.CubeSubtract),
                VoxelToolIds.VoxelMove => new ToolVisual("Move", Icon.ArrowMove, CustomGlyph: PixlPunktCodicon.CubeMove),
                VoxelToolIds.VoxelSelect => new ToolVisual("Select", Icon.CubeMultiple),
                VoxelToolIds.Lighting => new ToolVisual("Lighting", Icon.Lightbulb),
                _ => new ToolVisual(GetCompactLabel(displayName), Icon.Apps),
            };
        }

        private static string GetCompactLabel(string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "Tool";

            return displayName switch
            {
                "Face Paint" => "Paint",
                "Face Dropper" => "Dropper",
                "Face Erase" => "Erase",
                "Face Override Erase" => "Erase",
                "Voxel Create" => "Create",
                "Voxel Delete" => "Delete",
                "Voxel Select" => "Select",
                "Voxel Move" => "Move",
                _ => displayName.StartsWith("Voxel ", StringComparison.OrdinalIgnoreCase) ? displayName[6..] :
                     displayName.StartsWith("Face ", StringComparison.OrdinalIgnoreCase) ? displayName[5..] :
                     displayName
            };
        }

        private void UpdateScrollBars()
        {
            if (_orientation == Orientation.Vertical)
            {
                _scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                _scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else
            {
                _scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                _scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
        }
    }
}
