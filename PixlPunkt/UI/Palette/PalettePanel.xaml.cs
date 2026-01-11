using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Palette;
using PixlPunkt.UI.ColorPick;
using PixlPunkt.UI.Helpers;
using PixlPunkt.UI.Layers.Controls;
using PixlPunkt.UI.Palette.Controls;
using System;
using System.Collections.Generic;

namespace PixlPunkt.UI.Palette
{
    /// <summary>
    /// User control for displaying and managing a color palette.
    /// Provides visual swatches with context menus for editing, removing, and selecting colors.
    /// Supports gradient generation, color picker integration, and foreground/background indicators.
    /// </summary>
    public sealed partial class PalettePanel : UserControl
    {
        // ════════════════════════════════════════════════════════════════════
        // DEPENDENCY PROPERTIES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets or sets the palette service that manages color data and events.
        /// </summary>
        public PaletteService Service
        {
            get => (PaletteService)GetValue(ServiceProperty);
            set => SetValue(ServiceProperty, value);
        }

        public static readonly DependencyProperty ServiceProperty =
            DependencyProperty.Register(nameof(Service), typeof(PaletteService),
            typeof(PalettePanel), new PropertyMetadata(null, OnServiceChanged));

        // Adds palette swatch size setting (bindable)
        public int SwatchSize
        {
            get => (int)GetValue(SwatchSizeProperty);
            set => SetValue(SwatchSizeProperty, value);
        }

        public static readonly DependencyProperty SwatchSizeProperty =
            DependencyProperty.Register(nameof(SwatchSize), typeof(int), typeof(PalettePanel), new PropertyMetadata(16, OnSwatchSizeChanged));

        // ════════════════════════════════════════════════════════════════════
        // EVENTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired when a foreground color is picked from the palette.
        /// </summary>
        public event Action<uint>? ForegroundPicked;

        /// <summary>
        /// Fired when a background color is picked from the palette.
        /// </summary>
        public event Action<uint>? BackgroundPicked;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS
        // ════════════════════════════════════════════════════════════════════

        private readonly List<(Border Swatch, Border FgRing, Border BgRing, uint Value)> _cells = new();
        private readonly PalettePanelMenuFlyout _panelMenuFlyout = new();
        private readonly SwatchMenuFlyout _swatchMenuFlyout = new();
        // ════════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initializes a new palette panel control.
        /// </summary>
        public PalettePanel()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                Rebuild();
                WireFlyoutEvents();
            };
            SwatchList.UpdateLayout();
            PaletteBox.RightTapped += SwatchList_RightTapped;
            DispatcherQueue.TryEnqueue(UpdateAllRings);
        }

        // ════════════════════════════════════════════════════════════════════
        // DEPENDENCY PROPERTY CALLBACKS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles changes to the Service property, wiring/unwiring event handlers.
        /// </summary>
        private static void OnServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (PalettePanel)d;

            if (e.OldValue is PaletteService oldS)
            {
                oldS.PaletteChanged -= self.Rebuild;
                oldS.ForegroundChanged -= self.OnFgChanged;
                oldS.BackgroundChanged -= self.OnBgChanged;
            }

            if (e.NewValue is PaletteService newS)
            {
                newS.PaletteChanged += self.Rebuild;
                newS.ForegroundChanged += self.OnFgChanged;
                newS.BackgroundChanged += self.OnBgChanged;
                self.Rebuild();
            }
        }

        // Adds swatch size property callback
        private static void OnSwatchSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PalettePanel self)
            {
                // Update items panel layout when swatch size changes
                self.DispatcherQueue.TryEnqueue(() =>
                {
                    self.SwatchList.InvalidateMeasure();
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - SERVICE SYNCHRONIZATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles foreground color changes to update selection rings.
        /// </summary>
        private void OnFgChanged(uint _) => UpdateAllRings();

        /// <summary>
        /// Handles background color changes to update selection rings.
        /// </summary>
        private void OnBgChanged(uint _) => UpdateAllRings();

        // ════════════════════════════════════════════════════════════════════
        // UI BUILDING & REFRESH
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Rebuilds the entire palette UI from the current service data.
        /// </summary>
        private void Rebuild()
        {
            if (Service is null)
            {
                SwatchList.ItemsSource = null;
                _cells.Clear();
                return;
            }

            SwatchList.ItemsSource = new List<uint>(Service.Colors);
            _cells.Clear();
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateAllRings();
                //EnsurePanelFlyout();
            });
        }

        /// <summary>
        /// Updates foreground/background selection indicators on all visible swatches.
        /// </summary>
        private void UpdateAllRings()
        {
            foreach (var c in _cells)
                UpdateCellRings(c);
        }

        /// <summary>
        /// Updates the selection ring visibility for a single swatch cell.
        /// </summary>
        private void UpdateCellRings((Border Swatch, Border FgRing, Border BgRing, uint Value) c)
        {
            if (Service is null)
            {
                c.FgRing.Visibility = c.BgRing.Visibility = Visibility.Collapsed;
                return;
            }

            c.FgRing.Visibility = c.Value == Service.Foreground ? Visibility.Visible : Visibility.Collapsed;
            c.BgRing.Visibility = c.Value == Service.Background ? Visibility.Visible : Visibility.Collapsed;
        }

        // ════════════════════════════════════════════════════════════════════
        // CONTEXT MENU SETUP
        // ════════════════════════════════════════════════════════════════════


        public void WireFlyoutEvents()
        {
            // Panel menu flyout (empty area right-click)
            _panelMenuFlyout.AddFgPalette += (s, e) => { if (Service is not null) Service.AddColor(Service.Foreground); };
            _panelMenuFlyout.AddBgPalette += (s, e) => { if (Service is not null) Service.AddColor(Service.Background); };
            _panelMenuFlyout.ClearPalette += (s, e) => Panel_Clear_Click(null, new RoutedEventArgs());

            // Swatch flyout
            _swatchMenuFlyout.EditSwatch += Swatch_Edit_Click;
            _swatchMenuFlyout.RemoveSwatch += Swatch_Remove_Click;
            _swatchMenuFlyout.SetAsForeground += Swatch_SetFg_Click;
            _swatchMenuFlyout.SetAsBackground += Swatch_SetBg_Click;
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - TOOLBAR BUTTONS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles the "+" button to add a new color via color picker.
        /// </summary>
        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Service is null) return;

            var anchor = (FrameworkElement)sender;
            var win = new ColorPickerWindow
            {
                // Start from current FG so user sees what they expect
                GetCurrent = () => ColorUtil.ToColor(Service.Foreground),

                // Live-preview updates FG so tools reflect it
                SetLive = c => Service.SetForeground(ColorUtil.ToBGRA(c)),

                // Commit = actually add to the palette
                Commit = c => Service.AddColor(ColorUtil.ToBGRA(c))
            };

            // Wire up external dropper mode for canvas color sampling
            win.DropperModeRequested += active =>
            {
                var mainWindow = App.PixlPunktMainWindow as PixlPunktMainWindow;
                if (active)
                {
                    mainWindow?.BeginExternalDropperMode(bgra =>
                    {
                        win.SetPickedColor(bgra);
                    });
                }
                else
                {
                    mainWindow?.EndExternalDropperMode();
                }
            };

            // Ensure dropper mode is disabled when window closes
            win.Closed += (_, __) => (App.PixlPunktMainWindow as PixlPunktMainWindow)?.EndExternalDropperMode();

            win.Load(ColorUtil.ToColor(Service.Foreground), ColorUtil.ToColor(Service.Foreground));
            win.Activate();

            var appW = WindowHost.ApplyChrome(win, resizable: false, alwaysOnTop: true, minimizable: false, title: "Color Picker", owner: App.PixlPunktMainWindow);
            WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.90, minLogicalWidth: 560, minLogicalHeight: 360);
            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }

        /// <summary>
        /// Handles the "Add FG" button to quickly add foreground color to palette.
        /// </summary>
        private void AddFg_Click(object sender, RoutedEventArgs e)
        {
            if (Service is null) return;
            Service.AddColor(Service.Foreground);
        }

        /// <summary>
        /// Handles the remove button to delete current FG color or last color from palette.
        /// </summary>
        private void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Service is null || Service.Colors.Count == 0) return;

            int idx = IndexOf(Service.Colors, Service.Foreground);
            if (idx < 0) idx = Service.Colors.Count - 1;

            Service.RemoveAt(idx);
        }

        /// <summary>
        /// Handles the gradient button to open gradient generator window.
        /// </summary>
        private void Gradient_Click(object sender, RoutedEventArgs e)
        {
            if (Service is null) return;

            var win = new ColorPick.GradientWindow
            {
                GetStart = () => Service.Foreground,
                GetEnd = () => Service.Background,
                Commit = colors => { foreach (var c in colors) Service.AddColor(c); }
            };

            // Wire up external dropper mode for canvas color sampling
            win.DropperModeRequested += active =>
            {
                var mainWindow = App.PixlPunktMainWindow as PixlPunktMainWindow;
                if (active)
                {
                    mainWindow?.BeginExternalDropperMode(bgra =>
                    {
                        win.SetPickedColor(bgra);
                    });
                }
                else
                {
                    mainWindow?.EndExternalDropperMode();
                }
            };

            // Ensure dropper mode is disabled when window closes
            win.Closed += (_, __) => (App.PixlPunktMainWindow as PixlPunktMainWindow)?.EndExternalDropperMode();

            win.Activate();

            var appW = WindowHost.ApplyChrome(win, resizable: false, alwaysOnTop: true, minimizable: false, title: "Gradient Swatch Maker", owner: App.PixlPunktMainWindow);
            WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.90, minLogicalWidth: 560, minLogicalHeight: 360);
            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - SWATCH INTERACTIONS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles left-click on a swatch to set it as foreground color.
        /// </summary>
        private void Swatch_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is Border b && ColorUtil.TryGetBGRA(b.Tag, out var bgra))
            {
                Service?.SetForeground(bgra);
                ForegroundPicked?.Invoke(bgra);
                UpdateAllRings();
            }
        }

        /// <summary>
        /// Handles right-click on a swatch to show its context menu.
        /// </summary>
        private void Swatch_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;

            if (fe is Border swatch && ColorUtil.TryGetBGRA(swatch.Tag, out _))
            {
                // Get XamlRoot from element (works correctly in both docked and undocked scenarios)
                var xamlRoot = fe.XamlRoot ?? this.XamlRoot;
                if (xamlRoot != null)
                {
                    _swatchMenuFlyout.ShowAt(fe, swatch, xamlRoot);
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles right-click on empty palette area to show panel menu.
        /// </summary>
        private void SwatchList_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;

            // If the tap is on a swatch, its own RightTapped handler will show its menu
            if (FindAncestorNamed(e.OriginalSource as DependencyObject, "Swatch") is not null)
                return;

            // Get XamlRoot from element (works correctly in both docked and undocked scenarios)
            var xamlRoot = fe.XamlRoot ?? this.XamlRoot;
            if (xamlRoot != null)
            {
                _panelMenuFlyout.ShowAt(fe, xamlRoot);
            }

            e.Handled = true;
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - SWATCH CONTEXT MENU ITEMS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Opens the color picker to edit the selected palette color.
        /// </summary>
        private void Swatch_Edit_Click(object sender, Border? e)
        {
            if (Service is null || e == null) return;
            if (!ColorUtil.TryGetBGRA(e.Tag, out uint original)) return;

            int idx = IndexOf(Service.Colors, original);
            if (idx < 0 || idx >= Service.Colors.Count) return;

            var win = new ColorPickerWindow
            {
                GetCurrent = () => ColorUtil.ToColor(original),

                // Optional: preview goes to FG so tools reflect it while editing
                SetLive = c => Service.SetForeground(ColorUtil.ToBGRA(c)),

                // Commit = write back into that palette slot
                Commit = c =>
                {
                    uint v = ColorUtil.ToBGRA(c);
                    Service.UpdateAt(idx, v);
                    // Also set FG to the edited color for convenience
                    Service.SetForeground(v);
                }
            };

            // Wire up external dropper mode for canvas color sampling
            win.DropperModeRequested += active =>
            {
                var mainWindow = App.PixlPunktMainWindow as PixlPunktMainWindow;
                if (active)
                {
                    mainWindow?.BeginExternalDropperMode(bgra =>
                    {
                        win.SetPickedColor(bgra);
                    });
                }
                else
                {
                    mainWindow?.EndExternalDropperMode();
                }
            };

            // Ensure dropper mode is disabled when window closes
            win.Closed += (_, __) => (App.PixlPunktMainWindow as PixlPunktMainWindow)?.EndExternalDropperMode();

            var col = ColorUtil.ToColor(original);
            win.Load(col, col);
            win.Activate();

            var appW = WindowHost.ApplyChrome(win, resizable: false, alwaysOnTop: true, minimizable: false, title: "Color Picker", owner: App.PixlPunktMainWindow);
            WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.90, minLogicalWidth: 560, minLogicalHeight: 360);
            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }

        /// <summary>
        /// Sets the clicked swatch as the foreground color.
        /// </summary>
        private void Swatch_SetFg_Click(object sender, Border? e)
        {
            if (Service is null || e == null) return;
            if (!ColorUtil.TryGetBGRA(e.Tag, out uint value)) return;

            Service.SetForeground(value);
            ForegroundPicked?.Invoke(value);
            UpdateAllRings();
        }

        /// <summary>
        /// Sets the clicked swatch as the background color.
        /// </summary>
        private void Swatch_SetBg_Click(object sender, Border? e)
        {
            if (Service is null || e == null) return;
            if (!ColorUtil.TryGetBGRA(e.Tag, out uint value)) return;

            Service.SetBackground(value);
            BackgroundPicked?.Invoke(value);
            UpdateAllRings();
        }

        /// <summary>
        /// Removes the clicked swatch from the palette.
        /// </summary>
        private void Swatch_Remove_Click(object sender, Border? e)
        {
            if (Service is null || e == null) return;
            if (!ColorUtil.TryGetBGRA(e.Tag, out uint value)) return;

            int idx = IndexOf(Service.Colors, value);
            if (idx >= 0)
                Service.RemoveAt(idx);
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - PANEL MENU
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Shows confirmation dialog and clears the entire palette.
        /// </summary>
        private async void Panel_Clear_Click(object sender, RoutedEventArgs e)
        {
            if (Service is null) return;

            var dlg = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Clear palette?",
                Content = "Are you sure? This cannot be undone.",
                PrimaryButtonText = "Clear",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            var res = await dlg.ShowAsync();
            if (res == ContentDialogResult.Primary)
                ClearPalette();
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - CELL LIFETIME
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles cell loaded event to cache references and attach context menu.
        /// </summary>
        private void Cell_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid root) return;

            var swatch = VisualTreeHelper.GetChild(root, 0) as Border;
            var fgRing = VisualTreeHelper.GetChild(root, 1) as Border;
            var bgRing = VisualTreeHelper.GetChild(root, 2) as Border;

            if (swatch is null || fgRing is null || bgRing is null) return;

            if (ColorUtil.TryGetBGRA(swatch.Tag, out var val))
            {
                _cells.Add((swatch, fgRing, bgRing, val));
                UpdateCellRings((swatch, fgRing, bgRing, val));
            }

            //AttachSwatchFlyout(swatch);
        }

        /// <summary>
        /// Handles cell unloaded event to clean up cached references.
        /// </summary>
        private void Cell_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid root) return;
            var swatch = VisualTreeHelper.GetChild(root, 0) as Border;
            _cells.RemoveAll(c => ReferenceEquals(c.Swatch, swatch));
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Finds the palette index for a menu item's associated color.
        /// Checks both Tag and DataContext for the color value.
        /// </summary>
        private int GetIndexFromSender(object sender)
        {
            if (Service is null) return -1;

            if (sender is FrameworkElement fe)
            {
                if (fe.Tag is uint viaTag) return IndexOf(Service.Colors, viaTag);
                if (fe.DataContext is uint viaCtx) return IndexOf(Service.Colors, viaCtx);
            }

            return -1;
        }

        /// <summary>
        /// Finds the index of a color in the palette.
        /// </summary>
        private static int IndexOf(IReadOnlyList<uint> list, uint value)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == value) return i;
            return -1;
        }

        /// <summary>
        /// Removes all colors from the palette.
        /// </summary>
        private void ClearPalette()
        {
            if (Service is null) return;
            for (int i = Service.Colors.Count - 1; i >= 0; i--)
                Service.RemoveAt(i);
        }

        /// <summary>
        /// Finds an ancestor element in the visual tree with the specified name.
        /// </summary>
        private static FrameworkElement? FindAncestorNamed(DependencyObject? d, string name)
        {
            while (d is not null)
            {
                if (d is FrameworkElement fe && fe.Name == name) return fe;
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }
    }
}
