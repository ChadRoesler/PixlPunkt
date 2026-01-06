using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentIcons.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PixlPunkt.Uno.Core.Brush;
using PixlPunkt.Uno.Core.Coloring.Helpers;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Settings.Options;
using PixlPunkt.Uno.UI.ColorPick;
using PixlPunkt.Uno.UI.ColorPick.Controls;
using PixlPunkt.Uno.UI.Controls;
using PixlPunkt.Uno.UI.Helpers;
using Windows.Storage.Streams;
using Windows.UI;
using WinUIGradientStop = Microsoft.UI.Xaml.Media.GradientStop;

namespace PixlPunkt.Uno.UI.Tools
{
    /// <summary>
    /// Factory for creating WinUI controls from <see cref="IToolOption"/> descriptors.
    /// Used by <see cref="ToolOptionsBar"/> to dynamically generate tool option UI.
    /// </summary>
    public static class ToolOptionFactory
    {
        /// <summary>
        /// Creates a WinUI control for the given tool option.
        /// </summary>
        /// <param name="option">The option descriptor.</param>
        /// <param name="onEditStart">Called when user begins editing (e.g., slider drag start).</param>
        /// <param name="onEditEnd">Called when user finishes editing (e.g., slider drag end).</param>
        /// <returns>A UIElement representing the option, or null if unsupported.</returns>
        public static UIElement? CreateControl(IToolOption option, Action? onEditStart = null, Action? onEditEnd = null) => option switch
        {
            SliderOption slider => CreateSlider(slider, onEditStart, onEditEnd),
            NumberBoxOption numBox => CreateNumberBox(numBox, onEditStart, onEditEnd),
            ToggleOption toggle => CreateToggle(toggle),
            IconToggleOption iconToggle => CreateIconToggle(iconToggle),
            ShapeOption shape => CreateShapeSelector(shape, onEditStart, onEditEnd),
            CustomBrushOption customBrush => CreateCustomBrushSelector(customBrush, onEditStart, onEditEnd),
            DropdownOption dropdown => CreateDropdown(dropdown),
            ButtonOption button => CreateButton(button),
            IconButtonOption iconBtn => CreateIconButton(iconBtn),
            SeparatorOption => CreateSeparator(),
            DynamicLabelOption dynamicLabel => CreateDynamicLabel(dynamicLabel),
            LabelOption label => CreateLabel(label),
            IconOption icon => CreateIcon(icon),
            ColorOption color => CreateColorPicker(color),
            PaletteOption palette => CreatePalette(palette),
            ColorPickerWindowOption cpw => CreateColorPickerWindowButton(cpw),
            GradientPickerWindowOption gpw => CreateGradientPickerWindowButton(gpw),
            GradientPreviewOption gradient => CreateGradientPreview(gradient),
            HueSliderOption hue => CreateHueSlider(hue, onEditStart, onEditEnd),
            CustomWindowOption customWin => CreateCustomWindowButton(customWin),
            PluginWindowOption pluginWin => CreatePluginWindowButton(pluginWin),
            _ => null
        };

        // ====================================================================
        // SLIDER
        // ====================================================================

        private static UIElement CreateSlider(SliderOption opt, Action? onEditStart, Action? onEditEnd)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (opt.ShowLabel)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = opt.Label,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var slider = new Slider
            {
                Minimum = opt.Min,
                Maximum = opt.Max,
                Value = opt.Value,
                StepFrequency = opt.Step,
                Width = 140
            };

            NumberBox? numberBox = null;
            if (opt.ShowNumberBox)
            {
                numberBox = new NumberBox
                {
                    Minimum = opt.Min,
                    Maximum = opt.Max,
                    Value = opt.Value,
                    SmallChange = opt.Step,
                    Width = 64
                };
            }

            // Wire events with suppression to avoid loops
            bool suppress = false;

            slider.ValueChanged += (s, e) =>
            {
                if (suppress) return;
                suppress = true;
                onEditStart?.Invoke();
                opt.OnChanged(e.NewValue);
                if (numberBox != null) numberBox.Value = e.NewValue;
                onEditEnd?.Invoke();
                suppress = false;
            };

            if (numberBox != null)
            {
                numberBox.ValueChanged += (s, e) =>
                {
                    if (suppress || double.IsNaN(e.NewValue)) return;
                    suppress = true;
                    onEditStart?.Invoke();
                    opt.OnChanged(e.NewValue);
                    slider.Value = e.NewValue;
                    onEditEnd?.Invoke();
                    suppress = false;
                };
            }

            panel.Children.Add(slider);
            if (numberBox != null) panel.Children.Add(numberBox);

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(panel, opt.Tooltip);

            return panel;
        }

        // ====================================================================
        // NUMBER BOX (STANDALONE)
        // ====================================================================

        private static UIElement CreateNumberBox(NumberBoxOption opt, Action? onEditStart, Action? onEditEnd)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (opt.ShowLabel)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = opt.Label,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var numberBox = new NumberBox
            {
                Minimum = opt.Min,
                Maximum = opt.Max,
                Value = opt.Value,
                SmallChange = opt.Step,
                MinWidth = opt.Width,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };

            numberBox.ValueChanged += (s, e) =>
            {
                if (double.IsNaN(e.NewValue)) return;
                onEditStart?.Invoke();
                opt.OnChanged(e.NewValue);
                onEditEnd?.Invoke();
            };

            panel.Children.Add(numberBox);

            if (!string.IsNullOrEmpty(opt.Suffix))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = opt.Suffix,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(panel, opt.Tooltip);

            return panel;
        }

        // ====================================================================
        // TOGGLE (CHECKBOX)
        // ====================================================================

        private static UIElement CreateToggle(ToggleOption opt)
        {
            var check = new CheckBox
            {
                Content = opt.Label,
                IsChecked = opt.Value,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                MinWidth = 0,
                Padding = new Thickness(4, 0, 0, 0) // Reduce padding for tighter spacing
            };

            check.Checked += (s, e) => opt.OnChanged(true);
            check.Unchecked += (s, e) => opt.OnChanged(false);

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(check, opt.Tooltip);

            return check;
        }

        // ====================================================================
        // ICON TOGGLE
        // ====================================================================

        private static UIElement CreateIconToggle(IconToggleOption opt)
        {
            var icon = new FluentIcon
            {
                Icon = opt.Value ? opt.IconOn : opt.IconOff
            };

            var toggle = new ToggleButton
            {
                IsChecked = opt.Value,
                Padding = new Thickness(3),
                Content = icon
            };

            // Update icon and tooltip when state changes
            toggle.Checked += (s, e) =>
            {
                icon.Icon = opt.IconOn;
                opt.OnChanged(true);
                var tip = opt.TooltipOn ?? opt.Tooltip ?? opt.Label;
                ToolTipService.SetToolTip(toggle, tip);
            };

            toggle.Unchecked += (s, e) =>
            {
                icon.Icon = opt.IconOff;
                opt.OnChanged(false);
                var tip = opt.TooltipOff ?? opt.Tooltip ?? opt.Label;
                ToolTipService.SetToolTip(toggle, tip);
            };

            // Set initial tooltip
            var initialTip = opt.Value
                ? (opt.TooltipOn ?? opt.Tooltip ?? opt.Label)
                : (opt.TooltipOff ?? opt.Tooltip ?? opt.Label);
            ToolTipService.SetToolTip(toggle, initialTip);

            return toggle;
        }

        // ====================================================================
        // SHAPE SELECTOR
        // ====================================================================

        // ════════════════════════════════════════════════════════════════════
        // SHAPE SELECTOR (COMBOBOX WITH ICONS)
        // ════════════════════════════════════════════════════════════════════

        private static UIElement CreateShapeSelector(ShapeOption opt, Action? onEditStart, Action? onEditEnd)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (opt.ShowLabel)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = opt.Label,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            // Use a custom approach: a button that shows the icon, with a flyout for selection
            var selectedIcon = new FluentIcon
            {
                Icon = opt.Value == BrushShape.Circle ? FluentIcons.Common.Icon.Circle : FluentIcons.Common.Icon.Square,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };

            var dropdownButton = new DropDownButton
            {
                Content = selectedIcon,
                Padding = new Thickness(8, 4, 4, 4),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Create the flyout menu with icon + label items
            var menuFlyout = new MenuFlyout
            {
                Placement = FlyoutPlacementMode.Bottom
            };

            var circleItem = new MenuFlyoutItem
            {
                Text = "Circle",
                Icon = new FluentIcon { Icon = FluentIcons.Common.Icon.Circle, FontSize = 16 },
                Tag = BrushShape.Circle
            };

            var squareItem = new MenuFlyoutItem
            {
                Text = "Square",
                Icon = new FluentIcon { Icon = FluentIcons.Common.Icon.Square, FontSize = 16 },
                Tag = BrushShape.Square
            };

            // Handle selection
            void OnItemClick(object sender, RoutedEventArgs e)
            {
                if (sender is MenuFlyoutItem item && item.Tag is BrushShape shape)
                {
                    onEditStart?.Invoke();
                    opt.OnChanged(shape);

                    // Update the button's icon to reflect the new selection
                    selectedIcon.Icon = shape switch
                    {
                        BrushShape.Circle => FluentIcons.Common.Icon.Circle,
                        BrushShape.Square => FluentIcons.Common.Icon.Square,
                        _ => FluentIcons.Common.Icon.Circle
                    };

                    onEditEnd?.Invoke();
                }
            }

            circleItem.Click += OnItemClick;
            squareItem.Click += OnItemClick;

            menuFlyout.Items.Add(circleItem);
            menuFlyout.Items.Add(squareItem);

            dropdownButton.Flyout = menuFlyout;

            panel.Children.Add(dropdownButton);

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(panel, opt.Tooltip);

            return panel;
        }

        /// <summary>
        /// Creates a ComboBoxItem with icon + label for the shape selector.
        /// Shows both icon and label in the dropdown and when selected.
        /// </summary>
        private static ComboBoxItem CreateShapeComboItem(BrushShape shape, FluentIcons.Common.Icon icon, string label)
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            stack.Children.Add(new FluentIcon
            {
                Icon = icon,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            });

            stack.Children.Add(new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center
            });

            return new ComboBoxItem
            {
                Content = stack,
                Tag = shape
            };
        }

        // ====================================================================
        // DROPDOWN (COMBOBOX)
        // ====================================================================

        private static UIElement CreateDropdown(DropdownOption opt)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (opt.ShowLabel)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = opt.Label,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var combo = new ComboBox
            {
                MinWidth = 120,
                SelectedIndex = opt.SelectedIndex
            };

            foreach (var item in opt.Items)
                combo.Items.Add(new ComboBoxItem { Content = item });

            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex >= 0)
                    opt.OnChanged(combo.SelectedIndex);
            };

            panel.Children.Add(combo);

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(panel, opt.Tooltip);

            return panel;
        }

        // ====================================================================
        // BUTTON
        // ====================================================================

        private static UIElement CreateButton(ButtonOption opt)
        {
            var btn = new Button
            {
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 8, 4)
            };

            if (opt.Icon.HasValue)
            {
                var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                stack.Children.Add(new FluentIcon { Icon = opt.Icon.Value });
                stack.Children.Add(new TextBlock { Text = opt.Label, VerticalAlignment = VerticalAlignment.Center });
                btn.Content = stack;
            }
            else
            {
                btn.Content = opt.Label;
            }

            btn.Click += (s, e) => opt.OnClicked();

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(btn, opt.Tooltip);

            return btn;
        }

        // ====================================================================
        // ICON BUTTON
        // ====================================================================

        private static UIElement CreateIconButton(IconButtonOption opt)
        {
            if (opt.IsToggle)
            {
                var toggle = new ToggleButton
                {
                    IsChecked = opt.IsChecked,
                    Padding = new Thickness(3),
                    Content = new FluentIcon { Icon = opt.Icon }
                };

                toggle.Click += (s, e) => opt.OnClicked();
                ToolTipService.SetToolTip(toggle, opt.Tooltip ?? opt.Label);
                return toggle;
            }

            var btn = new Button
            {
                Padding = new Thickness(3),
                Content = new FluentIcon { Icon = opt.Icon }
            };

            btn.Click += (s, e) => opt.OnClicked();
            ToolTipService.SetToolTip(btn, opt.Tooltip ?? opt.Label);
            return btn;
        }

        // ====================================================================
        // SEPARATOR
        // ====================================================================

        private static UIElement CreateSeparator()
        {
            return new Border
            {
                Width = 1,
                Height = 24,
                Margin = new Thickness(8, 0, 8, 0),
                Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // ====================================================================
        // LABEL
        // ====================================================================

        private static UIElement CreateLabel(LabelOption opt)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = opt.Label,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center
            });

            panel.Children.Add(new TextBlock
            {
                Text = opt.Value,
                VerticalAlignment = VerticalAlignment.Center
            });

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(panel, opt.Tooltip);

            return panel;
        }

        // ====================================================================
        // DYNAMIC LABEL (LIVE-UPDATING)
        // ====================================================================

        private static UIElement CreateDynamicLabel(DynamicLabelOption opt)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = opt.Label,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center
            });

            var valueText = new TextBlock
            {
                Text = opt.GetValue(),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Apply monospaced font if requested (useful for hex codes)
            if (opt.MonospacedValue)
            {
                valueText.FontFamily = new FontFamily("Consolas");
            }

            panel.Children.Add(valueText);

            // Set up a timer to update the value periodically for live updates
            var timer = new Microsoft.UI.Xaml.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 FPS update rate
            };

            timer.Tick += (s, e) =>
            {
                var newValue = opt.GetValue();
                if (valueText.Text != newValue)
                {
                    valueText.Text = newValue;
                }
            };

            // Start timer when loaded, stop when unloaded
            panel.Loaded += (s, e) => timer.Start();
            panel.Unloaded += (s, e) => timer.Stop();

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(panel, opt.Tooltip);

            return panel;
        }

        // ====================================================================
        // ICON (STATIC DISPLAY)
        // ====================================================================

        private static UIElement CreateIcon(IconOption opt)
        {
            var icon = new FluentIcon
            {
                Icon = opt.Icon,
                FontSize = (int)opt.Size,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(icon, opt.Tooltip);

            return icon;
        }

        // ====================================================================
        // COLOR PICKER
        // ====================================================================

        // ════════════════════════════════════════════════════════════════════
        // COLOR PICKER (INLINE SWATCH)
        // ════════════════════════════════════════════════════════════════════

        private static UIElement CreateColorPicker(ColorOption opt)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (opt.ShowLabel)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = opt.Label,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            // Convert uint to Color
            byte a = (byte)(opt.Color >> 24);
            byte r = (byte)(opt.Color >> 16);
            byte g = (byte)(opt.Color >> 8);
            byte b = (byte)(opt.Color);

            var swatch = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(opt.ShowAlpha ? a : (byte)255, r, g, b)),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1)
            };

            swatch.Tapped += (s, e) =>
            {
                // If custom pick handler provided, use it
                if (opt.OnPickRequested != null)
                {
                    opt.OnPickRequested();
                    return;
                }

                // Otherwise, open a color picker window
                OpenColorPickerForSwatch(opt, swatch);
            };

            panel.Children.Add(swatch);

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(panel, opt.Tooltip);

            return panel;
        }

        /// <summary>
        /// Opens a ColorPickerWindow for a ColorOption swatch.
        /// </summary>
        private static void OpenColorPickerForSwatch(ColorOption opt, Border swatch)
        {
            uint initialColor = opt.Color;

            var win = new ColorPickerWindow
            {
                GetCurrent = () => ColorUtil.ToColor(initialColor),

                SetLive = c =>
                {
                    uint bgra = ColorUtil.ToBGRA(c);
                    opt.OnChanged(bgra);

                    // Update the swatch visually
                    byte na = (byte)(bgra >> 24);
                    byte nr = (byte)(bgra >> 16);
                    byte ng = (byte)(bgra >> 8);
                    byte nb = (byte)bgra;
                    swatch.Background = new SolidColorBrush(Color.FromArgb(opt.ShowAlpha ? na : (byte)255, nr, ng, nb));
                },

                Commit = c =>
                {
                    uint bgra = ColorUtil.ToBGRA(c);
                    opt.OnChanged(bgra);

                    // Update the swatch visually
                    byte na = (byte)(bgra >> 24);
                    byte nr = (byte)(bgra >> 16);
                    byte ng = (byte)(bgra >> 8);
                    byte nb = (byte)bgra;
                    swatch.Background = new SolidColorBrush(Color.FromArgb(opt.ShowAlpha ? na : (byte)255, nr, ng, nb));
                }
            };

            var col = ColorUtil.ToColor(initialColor);
            win.Load(col, col);
            win.Activate();

            var appW = WindowHost.ApplyChrome(
                win,
                resizable: false,
                alwaysOnTop: false,
                minimizable: false,
                title: opt.Label,
                owner: App.PixlPunktMainWindow);

            WindowHost.FitToContentAfterLayout(
                win,
                (FrameworkElement)win.Content,
                maxScreenFraction: 0.90,
                minLogicalWidth: 560,
                minLogicalHeight: 360);

            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }

        // ════════════════════════════════════════════════════════════════════
        // PALETTE (COLOR SWATCHES WITH INTERACTIVE EDITING)
        // ════════════════════════════════════════════════════════════════════

        private static UIElement CreatePalette(PaletteOption opt)
        {
            var paletteRow = new PaletteSwatchRow
            {
                Colors = opt.Colors as IList<uint> ?? [],
                SelectedIndex = opt.SelectedIndex,
                ShowButtons = true,
                // Wire FG/BG color providers from main window's PaletteService
                GetForegroundColor = () => (App.PixlPunktMainWindow as PixlPunktMainWindow)?.Palette?.Foreground ?? 0xFF000000u,
                GetBackgroundColor = () => (App.PixlPunktMainWindow as PixlPunktMainWindow)?.Palette?.Background ?? 0xFFFFFFFFu
            };

            // Wire events to the option callbacks - only if the callback is provided
            paletteRow.SelectionChanged += (s, idx) => opt.OnSelectionChanged?.Invoke(idx);

            // Only subscribe if there's a callback, otherwise let the control handle it directly
            if (opt.OnMoveRequested != null)
            {
                paletteRow.ColorMoved += (s, args) =>
                {
                    opt.OnMoveRequested(args.from, args.to);
                    paletteRow.Refresh();
                };
            }
            // else: PaletteSwatchRow will move directly in the list

            if (opt.OnAddRequested != null)
            {
                paletteRow.AddRequested += (s, e) => opt.OnAddRequested();
            }
            // else: PaletteSwatchRow will use its default (open color picker with FG color)

            if (opt.OnAddRampRequested != null)
            {
                paletteRow.AddRampRequested += (s, e) => opt.OnAddRampRequested();
            }
            // else: PaletteSwatchRow will use its default (open gradient picker with FG/BG colors)

            if (opt.OnReverseRequested != null)
            {
                paletteRow.ReverseRequested += (s, e) =>
                {
                    opt.OnReverseRequested();
                    paletteRow.Refresh();
                };
            }
            // else: PaletteSwatchRow will use its default (reverse in place)

            if (opt.OnEditRequested != null)
            {
                paletteRow.EditRequested += (s, idx) => opt.OnEditRequested(idx);
            }
            // else: PaletteSwatchRow will use its default (open color picker for editing)

            if (opt.OnRemoveRequested != null)
            {
                paletteRow.RemoveRequested += (s, idx) =>
                {
                    opt.OnRemoveRequested(idx);
                    paletteRow.Refresh();
                };
            }
            // else: PaletteSwatchRow will use its default (remove from list)

            if (opt.OnClearRequested != null)
            {
                paletteRow.ClearRequested += (s, e) =>
                {
                    opt.OnClearRequested();
                    paletteRow.Refresh();
                };
            }
            // else: PaletteSwatchRow will use its default (clear list)

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(paletteRow, opt.Tooltip);

            return paletteRow;
        }

        // ════════════════════════════════════════════════════════════════════
        // COLOR PICKER WINDOW
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a button that opens the full ColorPickerWindow for advanced color selection.
        /// </summary>
        private static UIElement CreateColorPickerWindowButton(ColorPickerWindowOption opt)
        {
            var btn = new Button
            {
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 8, 4)
            };

            if (opt.Icon.HasValue)
            {
                var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                stack.Children.Add(new FluentIcon { Icon = opt.Icon.Value });
                stack.Children.Add(new TextBlock { Text = opt.Label, VerticalAlignment = VerticalAlignment.Center });
                btn.Content = stack;
            }
            else
            {
                btn.Content = opt.Label;
            }

            btn.Click += (s, e) =>
            {
                uint initialColor = opt.GetCurrentColor();

                var win = new ColorPickerWindow
                {
                    GetCurrent = () => ColorUtil.ToColor(initialColor),

                    SetLive = c =>
                    {
                        uint bgra = ColorUtil.ToBGRA(c);
                        opt.OnLivePreview?.Invoke(bgra);
                    },

                    Commit = c =>
                    {
                        uint bgra = ColorUtil.ToBGRA(c);
                        opt.OnCommit(bgra);
                    }
                };

                var col = ColorUtil.ToColor(initialColor);
                win.Load(col, col);
                win.Activate();

                var appW = WindowHost.ApplyChrome(
                    win,
                    resizable: false,
                    alwaysOnTop: false,
                    minimizable: false,
                    title: opt.WindowTitle,
                    owner: App.PixlPunktMainWindow);

                WindowHost.FitToContentAfterLayout(
                    win,
                    (FrameworkElement)win.Content,
                    maxScreenFraction: 0.90,
                    minLogicalWidth: 560,
                    minLogicalHeight: 360);

                WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
            };

            ToolTipService.SetToolTip(btn, opt.Tooltip ?? opt.Label);
            return btn;
        }

        // ════════════════════════════════════════════════════════════════════
        // GRADIENT PICKER WINDOW
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a button that opens the GradientWindow for generating color ramps.
        /// </summary>
        private static UIElement CreateGradientPickerWindowButton(GradientPickerWindowOption opt)
        {
            var btn = new Button
            {
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 8, 4)
            };

            if (opt.Icon.HasValue)
            {
                var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                stack.Children.Add(new FluentIcon { Icon = opt.Icon.Value });
                stack.Children.Add(new TextBlock { Text = opt.Label, VerticalAlignment = VerticalAlignment.Center });
                btn.Content = stack;
            }
            else
            {
                btn.Content = opt.Label;
            }

            btn.Click += (s, e) =>
            {
                var win = new GradientWindow
                {
                    GetStart = opt.GetStartColor,
                    GetEnd = opt.GetEndColor,
                    Commit = colors => opt.OnCommit(colors)
                };

                win.Activate();

                var appW = WindowHost.ApplyChrome(
                    win,
                    resizable: false,
                    alwaysOnTop: false,
                    minimizable: false,
                    title: opt.WindowTitle,
                    owner: App.PixlPunktMainWindow);

                WindowHost.FitToContentAfterLayout(
                    win,
                    (FrameworkElement)win.Content,
                    maxScreenFraction: 0.90,
                    minLogicalWidth: 560,
                    minLogicalHeight: 360);

                WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
            };

            ToolTipService.SetToolTip(btn, opt.Tooltip ?? opt.Label);
            return btn;
        }

        // ════════════════════════════════════════════════════════════════════
        // GRADIENT PREVIEW
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a gradient preview strip that shows the current gradient colors.
        /// Clicking opens the gradient editor (if Custom mode).
        /// </summary>
        private static UIElement CreateGradientPreview(GradientPreviewOption opt)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Create the gradient preview border with a canvas for custom drawing
            var gradientBorder = new Border
            {
                Width = opt.Width,
                Height = opt.Height,
                CornerRadius = new CornerRadius(4),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1)
            };

            // Use a Grid to layer the checkered background and gradient
            var layerGrid = new Grid();

            // Checkered background for transparency
            var checkerBg = new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = CreateCheckerBrush()
            };
            layerGrid.Children.Add(checkerBg);

            // Gradient overlay
            var gradientRect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                RadiusX = 3,
                RadiusY = 3
            };
            layerGrid.Children.Add(gradientRect);

            gradientBorder.Child = layerGrid;

            // Update the gradient brush
            void UpdateGradient()
            {
                var stops = opt.GetStops();
                if (stops == null || stops.Count == 0)
                {
                    gradientRect.Fill = new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));
                    return;
                }

                var gradientStops = new GradientStopCollection();
                foreach (var stop in stops)
                {
                    byte a = (byte)(stop.Color >> 24);
                    byte r = (byte)(stop.Color >> 16);
                    byte g = (byte)(stop.Color >> 8);
                    byte b = (byte)(stop.Color);

                    gradientStops.Add(new WinUIGradientStop
                    {
                        Color = Color.FromArgb(a, r, g, b),
                        Offset = stop.Position
                    });
                }

                gradientRect.Fill = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0, 0.5),
                    EndPoint = new Windows.Foundation.Point(1, 0.5),
                    GradientStops = gradientStops
                };
            }

            // Initial update
            UpdateGradient();

            // Set up a timer to update the gradient periodically (for live updates)
            var timer = new Microsoft.UI.Xaml.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };

            timer.Tick += (s, e) => UpdateGradient();
            panel.Loaded += (s, e) => timer.Start();
            panel.Unloaded += (s, e) => timer.Stop();

            // Handle click to edit
            if (opt.OnEditRequested != null)
            {
                gradientBorder.Tapped += (s, e) => opt.OnEditRequested();
            }

            panel.Children.Add(gradientBorder);

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(panel, opt.Tooltip);

            return panel;
        }

        /// <summary>
        /// Creates a checkered brush pattern for transparency visualization.
        /// </summary>
        private static Brush CreateCheckerBrush()
        {
            // For simplicity, just return a solid light gray - proper tiling would need more setup
            return new SolidColorBrush(Color.FromArgb(255, 240, 240, 240));
        }

        // ════════════════════════════════════════════════════════════════════
        // HUE SLIDER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a HueSlider control for selecting hue values (0-360).
        /// </summary>
        private static UIElement CreateHueSlider(HueSliderOption opt, Action? onEditStart, Action? onEditEnd)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (opt.ShowLabel)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = opt.Label,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var slider = new HueSlider
            {
                Hue = opt.Value,
                Width = opt.Width,
                Height = 20
            };

            slider.HueChanging += (s, hue) =>
            {
                onEditStart?.Invoke();
                opt.OnChanged(hue);
            };

            slider.HueChanged += (s, hue) =>
            {
                opt.OnChanged(hue);
                onEditEnd?.Invoke();
            };

            panel.Children.Add(slider);

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(panel, opt.Tooltip);

            return panel;
        }

        // ════════════════════════════════════════════════════════════════════
        // CUSTOM BRUSH SELECTOR
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a dropdown selector for custom brushes and built-in shapes.
        /// Shows brush icons when available, with brush names in the expanded dropdown.
        /// </summary>
        private static UIElement CreateCustomBrushSelector(CustomBrushOption opt, Action? onEditStart, Action? onEditEnd)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (opt.ShowLabel)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = opt.Label,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            // Container for the dropdown button content (icon or image)
            var selectedContent = new Grid
            {
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Default to built-in shape icon
            var selectedIcon = new FluentIcon
            {
                Icon = opt.BuiltInShape == BrushShape.Circle ? FluentIcons.Common.Icon.Circle : FluentIcons.Common.Icon.Square,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Brush preview image (hidden by default)
            var selectedImage = new Image
            {
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            selectedContent.Children.Add(selectedIcon);
            selectedContent.Children.Add(selectedImage);

            // If a custom brush is selected, show its icon
            if (!string.IsNullOrEmpty(opt.SelectedBrushFullName))
            {
                var iconData = CustomBrushIcons.Instance.GetIconByFullName(opt.SelectedBrushFullName);
                if (iconData != null)
                {
                    _ = LoadBrushIconAsync(iconData, selectedImage, selectedIcon);
                }
            }

            var dropdownButton = new DropDownButton
            {
                Content = selectedContent,
                Padding = new Thickness(4),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 40
            };

            // Create the flyout menu
            var menuFlyout = new MenuFlyout
            {
                Placement = FlyoutPlacementMode.Bottom
            };

            // Add built-in shapes first
            var circleItem = new MenuFlyoutItem
            {
                Text = "Circle",
                Icon = new FluentIcon { Icon = FluentIcons.Common.Icon.Circle, FontSize = 16 },
                Tag = ("builtin", BrushShape.Circle)
            };

            var squareItem = new MenuFlyoutItem
            {
                Text = "Square",
                Icon = new FluentIcon { Icon = FluentIcons.Common.Icon.Square, FontSize = 16 },
                Tag = ("builtin", BrushShape.Square)
            };

            circleItem.Click += (s, e) =>
            {
                onEditStart?.Invoke();
                opt.OnBuiltInShapeSelected(BrushShape.Circle);
                selectedIcon.Icon = FluentIcons.Common.Icon.Circle;
                selectedIcon.Visibility = Visibility.Visible;
                selectedImage.Visibility = Visibility.Collapsed;
                onEditEnd?.Invoke();
            };

            squareItem.Click += (s, e) =>
            {
                onEditStart?.Invoke();
                opt.OnBuiltInShapeSelected(BrushShape.Square);
                selectedIcon.Icon = FluentIcons.Common.Icon.Square;
                selectedIcon.Visibility = Visibility.Visible;
                selectedImage.Visibility = Visibility.Collapsed;
                onEditEnd?.Invoke();
            };

            menuFlyout.Items.Add(circleItem);
            menuFlyout.Items.Add(squareItem);

            // Add custom brushes if any are loaded
            var brushService = BrushDefinitionService.Instance;
            if (brushService.Count > 0)
            {
                menuFlyout.Items.Add(new MenuFlyoutSeparator());

                foreach (var brushFullName in brushService.GetBrushNames())
                {
                    var brush = brushService.GetBrush(brushFullName);
                    if (brush == null) continue;

                    var brushItem = new MenuFlyoutItem
                    {
                        Text = brush.DisplayName,
                        Tag = ("custom", brushFullName)
                    };

                    // Set tooltip with full name
                    ToolTipService.SetToolTip(brushItem, brushFullName);

                    // Try to load icon for the menu item
                    var iconData = CustomBrushIcons.Instance.GetIconByFullName(brushFullName);
                    if (iconData != null)
                    {
                        var itemImage = new Image
                        {
                            Width = 16,
                            Height = 16,
                            Stretch = Stretch.Uniform
                        };
                        _ = LoadBrushIconAsync(iconData, itemImage, null);
                        brushItem.Icon = new ImageIcon { Source = itemImage.Source };

                        // Since ImageIcon needs an ImageSource directly, load it async
                        _ = SetMenuItemIconAsync(brushItem, iconData);
                    }

                    var capturedFullName = brushFullName;
                    brushItem.Click += (s, e) =>
                    {
                        onEditStart?.Invoke();
                        opt.OnBrushSelected(capturedFullName);

                        // Update the button's icon
                        var brushIconData = CustomBrushIcons.Instance.GetIconByFullName(capturedFullName);
                        if (brushIconData != null)
                        {
                            _ = LoadBrushIconAsync(brushIconData, selectedImage, selectedIcon);
                        }

                        onEditEnd?.Invoke();
                    };

                    menuFlyout.Items.Add(brushItem);
                }
            }

            dropdownButton.Flyout = menuFlyout;
            panel.Children.Add(dropdownButton);

            if (!string.IsNullOrEmpty(opt.Tooltip))
                ToolTipService.SetToolTip(panel, opt.Tooltip);

            return panel;
        }

        /// <summary>
        /// Loads a brush icon asynchronously and updates the Image control.
        /// Hides the FluentIcon when image is loaded.
        /// </summary>
        private static async Task LoadBrushIconAsync(byte[] iconData, Image imageControl, FluentIcon? iconToHide)
        {
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                    Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);

                encoder.SetPixelData(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    32, 32, 96, 96, iconData);

                await encoder.FlushAsync();
                stream.Seek(0);

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);

                imageControl.Source = bitmap;
                imageControl.Visibility = Visibility.Visible;

                if (iconToHide != null)
                {
                    iconToHide.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                // If icon loading fails, keep the default icon visible
            }
        }

        /// <summary>
        /// Sets a menu flyout item's icon from brush icon data.
        /// </summary>
        private static async Task SetMenuItemIconAsync(MenuFlyoutItem item, byte[] iconData)
        {
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                    Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);

                encoder.SetPixelData(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    32, 32, 96, 96, iconData);

                await encoder.FlushAsync();
                stream.Seek(0);

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);

                item.Icon = new ImageIcon { Source = bitmap };
            }
            catch
            {
                // If icon loading fails, leave without icon
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // CUSTOM WINDOW BUTTON
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a button that opens a custom tool configuration window using WindowHost.
        /// </summary>
        private static UIElement CreateCustomWindowButton(CustomWindowOption opt)
        {
            var btn = new Button
            {
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 8, 4)
            };

            if (opt.Icon.HasValue)
            {
                var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                stack.Children.Add(new FluentIcon { Icon = opt.Icon.Value });
                stack.Children.Add(new TextBlock { Text = opt.Label, VerticalAlignment = VerticalAlignment.Center });
                btn.Content = stack;
            }
            else
            {
                btn.Content = opt.Label;
            }

            btn.Click += (s, e) =>
            {
                // Create the window using the factory function
                var win = opt.CreateWindow();
                win.Activate();

                // Apply chrome and positioning using WindowHost
                var appW = WindowHost.ApplyChrome(
                    win,
                    resizable: opt.Resizable,
                    alwaysOnTop: false,
                    minimizable: opt.Minimizable,
                    title: opt.WindowTitle ?? opt.Label,
                    owner: App.PixlPunktMainWindow);

                WindowHost.FitToContentAfterLayout(
                    win,
                    (FrameworkElement)win.Content,
                    maxScreenFraction: opt.MaxScreenFraction,
                    minLogicalWidth: opt.MinWidth,
                    minLogicalHeight: opt.MinHeight);

                WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
            };

            ToolTipService.SetToolTip(btn, opt.Tooltip ?? opt.Label);
            return btn;
        }

        // ════════════════════════════════════════════════════════════════════
        // PLUGIN WINDOW BUTTON (SDK-based)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a button that opens a plugin-defined window.
        /// The window content is built from IToolOption descriptors provided by the plugin.
        /// </summary>
        private static UIElement CreatePluginWindowButton(PluginWindowOption opt)
        {
            var btn = new Button
            {
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4, 8, 4)
            };

            if (opt.Icon.HasValue)
            {
                var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                stack.Children.Add(new FluentIcon { Icon = opt.Icon.Value });
                stack.Children.Add(new TextBlock { Text = opt.Label, VerticalAlignment = VerticalAlignment.Center });
                btn.Content = stack;
            }
            else
            {
                btn.Content = opt.Label;
            }

            btn.Click += (s, e) =>
            {
                // Get the window descriptor from the plugin
                var descriptor = opt.GetWindowDescriptor();

                // Notify plugin that window is opening
                descriptor.OnOpening?.Invoke();

                // Create a window with content built from the descriptor's options
                var win = new Window();

                // Build the content panel from the tool options
                var contentPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 12,
                    Padding = new Thickness(16),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                // Add controls for each option from the descriptor
                foreach (var toolOption in descriptor.GetContent())
                {
                    var control = CreateControl(toolOption);
                    if (control != null)
                    {
                        contentPanel.Children.Add(control);
                    }
                }

                // Wrap in a ScrollViewer for longer content
                var scrollViewer = new ScrollViewer
                {
                    Content = contentPanel,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                win.Content = scrollViewer;

                // Handle window closing
                win.Closed += (sender, args) =>
                {
                    descriptor.OnClosed?.Invoke();
                };

                win.Activate();

                // Apply chrome and positioning using WindowHost
                var appW = WindowHost.ApplyChrome(
                    win,
                    resizable: descriptor.Resizable,
                    alwaysOnTop: false,
                    minimizable: false,
                    title: descriptor.Title,
                    owner: App.PixlPunktMainWindow);

                WindowHost.FitToContentAfterLayout(
                    win,
                    (FrameworkElement)win.Content,
                    maxScreenFraction: 0.90,
                    minLogicalWidth: descriptor.MinWidth,
                    minLogicalHeight: descriptor.MinHeight);

                WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
            };

            ToolTipService.SetToolTip(btn, opt.Tooltip ?? opt.Label);
            return btn;
        }
    }
}
