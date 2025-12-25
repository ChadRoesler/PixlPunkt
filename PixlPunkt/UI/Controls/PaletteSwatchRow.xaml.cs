using System;
using System.Collections.Generic;
using FluentIcons.Common;
using FluentIcons.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.UI.ColorPick;
using PixlPunkt.UI.Helpers;
using Windows.UI;

namespace PixlPunkt.UI.Controls
{
    /// <summary>
    /// A reusable control that displays a row of color swatches with drag-to-reorder support.
    /// Supports add, remove, edit, reverse, and clear operations.
    /// </summary>
    public sealed partial class PaletteSwatchRow : UserControl
    {
        // ====================================================================
        // CONSTANTS
        // ====================================================================

        private const double SwatchSlotWidth = 18.0; // 16px swatch + 2px margin

        // ====================================================================
        // DEPENDENCY PROPERTIES
        // ====================================================================

        public static readonly DependencyProperty ColorsProperty =
            DependencyProperty.Register(
                nameof(Colors),
                typeof(IList<uint>),
                typeof(PaletteSwatchRow),
                new PropertyMetadata(null, OnColorsChanged));

        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(
                nameof(SelectedIndex),
                typeof(int),
                typeof(PaletteSwatchRow),
                new PropertyMetadata(-1, OnSelectedIndexChanged));

        public static readonly DependencyProperty ShowButtonsProperty =
            DependencyProperty.Register(
                nameof(ShowButtons),
                typeof(bool),
                typeof(PaletteSwatchRow),
                new PropertyMetadata(true, OnShowButtonsChanged));

        // ====================================================================
        // PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets or sets the list of colors (BGRA format) to display.
        /// </summary>
        public IList<uint> Colors
        {
            get => (IList<uint>)GetValue(ColorsProperty);
            set => SetValue(ColorsProperty, value);
        }

        /// <summary>
        /// Gets or sets the currently selected swatch index.
        /// </summary>
        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the action buttons (add, ramp, reverse).
        /// </summary>
        public bool ShowButtons
        {
            get => (bool)GetValue(ShowButtonsProperty);
            set => SetValue(ShowButtonsProperty, value);
        }

        /// <summary>
        /// Gets or sets a function that provides the current foreground color.
        /// Used as the initial color for add/edit operations.
        /// </summary>
        public Func<uint>? GetForegroundColor { get; set; }

        /// <summary>
        /// Gets or sets a function that provides the current background color.
        /// Used as the end color for gradient ramp generation.
        /// </summary>
        public Func<uint>? GetBackgroundColor { get; set; }

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>Raised when a swatch is selected (clicked).</summary>
        public event EventHandler<int>? SelectionChanged;

        /// <summary>Raised when a color is moved from one index to another.</summary>
        public event EventHandler<(int from, int to)>? ColorMoved;

        /// <summary>Raised when the add button is clicked.</summary>
        public event EventHandler? AddRequested;

        /// <summary>Raised when the add ramp button is clicked.</summary>
        public event EventHandler? AddRampRequested;

        /// <summary>Raised when the reverse button is clicked.</summary>
        public event EventHandler? ReverseRequested;

        /// <summary>Raised when edit is requested for a swatch (via context menu).</summary>
        public event EventHandler<int>? EditRequested;

        /// <summary>Raised when remove is requested for a swatch (via context menu).</summary>
        public event EventHandler<int>? RemoveRequested;

        /// <summary>Raised when clear palette is requested (via context menu).</summary>
        public event EventHandler? ClearRequested;

        // ====================================================================
        // FIELDS
        // ====================================================================

        private int _dragIndex = -1;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public PaletteSwatchRow()
        {
            InitializeComponent();

            // Replace FontIcon with FluentIcon for buttons
            AddBtn.Content = new FluentIcon { Icon = Icon.AddSquare };
            RampBtn.Content = new FluentIcon { Icon = Icon.StackAdd };
            ReverseBtn.Content = new FluentIcon { Icon = Icon.ArrowSwap };

            // Wire pointer events on the container for drag-to-reorder
            SwatchRow.PointerPressed += SwatchRow_PointerPressed;
            SwatchRow.PointerMoved += SwatchRow_PointerMoved;
            SwatchRow.PointerReleased += SwatchRow_PointerReleased;
            SwatchRow.PointerCaptureLost += SwatchRow_PointerCaptureLost;
            SwatchRow.Tapped += SwatchRow_Tapped;
        }

        // ====================================================================
        // DEPENDENCY PROPERTY CALLBACKS
        // ====================================================================

        private static void OnColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (PaletteSwatchRow)d;
            self.RebuildSwatches();
        }

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (PaletteSwatchRow)d;
            self.RebuildSwatches();
        }

        private static void OnShowButtonsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (PaletteSwatchRow)d;
            self.ActionsPanel.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        // ====================================================================
        // PUBLIC METHODS
        // ====================================================================

        /// <summary>
        /// Refreshes the swatch display. Call this after modifying the Colors list externally.
        /// </summary>
        public void Refresh() => RebuildSwatches();

        // ====================================================================
        // SWATCH BUILDING
        // ====================================================================

        private void RebuildSwatches()
        {
            SwatchRow.Children.Clear();

            if (Colors == null) return;

            for (int i = 0; i < Colors.Count; i++)
            {
                int index = i; // Capture for closure
                uint color = Colors[i];
                byte a = (byte)(color >> 24);
                byte r = (byte)(color >> 16);
                byte g = (byte)(color >> 8);
                byte b = (byte)color;

                var swatch = new Border
                {
                    Width = 16,
                    Height = 16,
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(1, 0, 1, 0),
                    Background = new SolidColorBrush(Color.FromArgb(a, r, g, b)),
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(index == SelectedIndex ? 2 : 1),
                    Tag = index
                };

                // Context menu
                var flyout = new MenuFlyout();

                var editItem = new MenuFlyoutItem { Text = "Edit" };
                editItem.Click += (s, e) => OnEditRequested(index);
                flyout.Items.Add(editItem);

                var removeItem = new MenuFlyoutItem { Text = "Remove" };
                removeItem.Click += (s, e) => OnRemoveRequested(index);
                flyout.Items.Add(removeItem);

                flyout.Items.Add(new MenuFlyoutSeparator());

                var clearItem = new MenuFlyoutItem { Text = "Clear palette" };
                clearItem.Click += (s, e) => OnClearRequested();
                flyout.Items.Add(clearItem);

                swatch.ContextFlyout = flyout;

                SwatchRow.Children.Add(swatch);
            }
        }

        // ====================================================================
        // POINTER EVENTS (DRAG-TO-REORDER)
        // ====================================================================

        private void SwatchRow_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(SwatchRow);
            if (!pt.Properties.IsLeftButtonPressed) return;

            if (Colors == null || Colors.Count == 0) return;

            int idx = (int)Math.Floor(pt.Position.X / SwatchSlotWidth);
            idx = Math.Clamp(idx, 0, Colors.Count - 1);

            _dragIndex = idx;
            SelectedIndex = idx;
            SwatchRow.CapturePointer(e.Pointer);
            e.Handled = true;
            RebuildSwatches();
        }

        private void SwatchRow_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_dragIndex < 0 || Colors == null || Colors.Count == 0) return;

            var rowPt = e.GetCurrentPoint(SwatchRow).Position;
            int target = (int)Math.Floor(rowPt.X / SwatchSlotWidth);
            target = Math.Clamp(target, 0, Colors.Count - 1);

            if (target != _dragIndex)
            {
                int from = _dragIndex;

                // If there's a ColorMoved handler, let it do the actual move
                // Otherwise, move directly in the list
                if (ColorMoved != null)
                {
                    // Notify listeners - they handle the actual move
                    ColorMoved.Invoke(this, (from, target));
                }
                else
                {
                    // Default behavior: move the color in the list directly
                    var item = Colors[from];
                    Colors.RemoveAt(from);
                    Colors.Insert(target, item);
                }

                _dragIndex = target;
                SelectedIndex = target;

                RebuildSwatches();
            }
        }

        private void SwatchRow_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_dragIndex >= 0)
            {
                _dragIndex = -1;
                SwatchRow.ReleasePointerCaptures();
                RebuildSwatches();
            }
            e.Handled = true;
        }

        private void SwatchRow_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (_dragIndex >= 0)
            {
                _dragIndex = -1;
                RebuildSwatches();
            }
        }

        private void SwatchRow_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (Colors == null || Colors.Count == 0) return;

            var pos = e.GetPosition(SwatchRow);
            int idx = (int)Math.Floor(pos.X / SwatchSlotWidth);
            idx = Math.Clamp(idx, 0, Colors.Count - 1);

            SelectedIndex = idx;
            SelectionChanged?.Invoke(this, idx);
        }

        // ====================================================================
        // BUTTON HANDLERS
        // ====================================================================

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            if (AddRequested != null)
            {
                AddRequested.Invoke(this, EventArgs.Empty);
            }
            else if (Colors != null)
            {
                // Default behavior: open color picker
                OpenColorPickerForAdd();
            }
        }

        private void RampBtn_Click(object sender, RoutedEventArgs e)
        {
            if (AddRampRequested != null)
            {
                AddRampRequested.Invoke(this, EventArgs.Empty);
            }
            else if (Colors != null)
            {
                // Default behavior: open gradient picker
                OpenGradientPicker();
            }
        }

        private void ReverseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ReverseRequested != null)
            {
                ReverseRequested.Invoke(this, EventArgs.Empty);
            }
            else if (Colors != null && Colors.Count > 1)
            {
                // Default behavior: reverse in place using the same list
                int count = Colors.Count;
                for (int i = 0; i < count / 2; i++)
                {
                    int j = count - 1 - i;
                    (Colors[i], Colors[j]) = (Colors[j], Colors[i]);
                }
                RebuildSwatches();
            }
        }

        // ====================================================================
        // CONTEXT MENU HANDLERS
        // ====================================================================

        private void OnEditRequested(int index)
        {
            if (EditRequested != null)
            {
                EditRequested.Invoke(this, index);
            }
            else if (Colors != null && index >= 0 && index < Colors.Count)
            {
                // Default behavior: open color picker for editing
                OpenColorPickerForEdit(index);
            }
        }

        private void OnRemoveRequested(int index)
        {
            if (RemoveRequested != null)
            {
                RemoveRequested.Invoke(this, index);
            }
            else if (Colors != null && index >= 0 && index < Colors.Count)
            {
                Colors.RemoveAt(index);
                if (SelectedIndex >= Colors.Count)
                    SelectedIndex = Colors.Count - 1;
                RebuildSwatches();
            }
        }

        private void OnClearRequested()
        {
            if (ClearRequested != null)
            {
                ClearRequested.Invoke(this, EventArgs.Empty);
            }
            else if (Colors != null)
            {
                Colors.Clear();
                SelectedIndex = -1;
                RebuildSwatches();
            }
        }

        // ====================================================================
        // COLOR PICKER HELPERS
        // ====================================================================

        private void OpenColorPickerForAdd()
        {
            // Use foreground color if available, otherwise fall back to black
            uint initialColor = GetForegroundColor?.Invoke() ?? 0xFF000000u;

            var win = new ColorPickerWindow
            {
                GetCurrent = () => ColorUtil.ToColor(initialColor),
                SetLive = c => { },
                Commit = c =>
                {
                    uint bgra = ColorUtil.ToBGRA(c);
                    Colors?.Add(bgra);
                    RebuildSwatches();
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

            var col = ColorUtil.ToColor(initialColor);
            win.Load(col, col);
            win.Activate();

            var appW = WindowHost.ApplyChrome(
                win,
                resizable: false,
                alwaysOnTop: false,
                minimizable: false,
                title: "Add Color",
                owner: App.PixlPunktMainWindow);

            WindowHost.FitToContentAfterLayout(
                win,
                (FrameworkElement)win.Content,
                maxScreenFraction: 0.90,
                minLogicalWidth: 560,
                minLogicalHeight: 360);

            WindowHost.Place(appW, Core.Enums.WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }

        private void OpenColorPickerForEdit(int index)
        {
            if (Colors == null || index < 0 || index >= Colors.Count) return;

            uint initialColor = Colors[index];

            var win = new ColorPickerWindow
            {
                GetCurrent = () => ColorUtil.ToColor(initialColor),
                SetLive = c =>
                {
                    uint bgra = ColorUtil.ToBGRA(c);
                    if (index < Colors.Count)
                    {
                        Colors[index] = bgra;
                        RebuildSwatches();
                    }
                },
                Commit = c =>
                {
                    uint bgra = ColorUtil.ToBGRA(c);
                    if (index < Colors.Count)
                    {
                        Colors[index] = bgra;
                        RebuildSwatches();
                    }
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

            var col = ColorUtil.ToColor(initialColor);
            win.Load(col, col);
            win.Activate();

            var appW = WindowHost.ApplyChrome(
                win,
                resizable: false,
                alwaysOnTop: false,
                minimizable: false,
                title: "Edit Color",
                owner: App.PixlPunktMainWindow);

            WindowHost.FitToContentAfterLayout(
                win,
                (FrameworkElement)win.Content,
                maxScreenFraction: 0.90,
                minLogicalWidth: 560,
                minLogicalHeight: 360);

            WindowHost.Place(appW, Core.Enums.WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }

        private void OpenGradientPicker()
        {
            if (Colors == null) return;

            // Use FG/BG colors if available, otherwise fall back to first/last palette colors or defaults
            uint startColor = GetForegroundColor?.Invoke() ?? (Colors.Count > 0 ? Colors[0] : 0xFF000000u);
            uint endColor = GetBackgroundColor?.Invoke() ?? (Colors.Count > 1 ? Colors[^1] : 0xFFFFFFFFu);

            var win = new GradientWindow
            {
                GetStart = () => startColor,
                GetEnd = () => endColor,
                Commit = rampColors =>
                {
                    foreach (var c in rampColors)
                    {
                        Colors.Add(c);
                    }
                    RebuildSwatches();
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

            win.Activate();

            var appW = WindowHost.ApplyChrome(
                win,
                resizable: false,
                alwaysOnTop: false,
                minimizable: false,
                title: "Add Gradient",
                owner: App.PixlPunktMainWindow);

            WindowHost.FitToContentAfterLayout(
                win,
                (FrameworkElement)win.Content,
                maxScreenFraction: 0.90,
                minLogicalWidth: 560,
                minLogicalHeight: 360);

            WindowHost.Place(appW, Core.Enums.WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }
    }
}
