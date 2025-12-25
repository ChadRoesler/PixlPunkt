using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Palette;
using PixlPunkt.UI.ColorPick;
using PixlPunkt.UI.Helpers;
using Windows.Foundation;

namespace PixlPunkt.UI.Palette
{
    public sealed partial class QuickSwatches : UserControl
    {
        // Cached brushes to avoid repeated allocations
        private readonly SolidColorBrush _fgBrush = new();
        private readonly SolidColorBrush _bgBrush = new();

        private Flyout _flyout;
        private ColorPicker _picker;
        private enum PickTarget { None, FG, BG }
        private PickTarget _target = PickTarget.None;
        public PixlPunkt.UI.Tools.ToolRail? HostRail { get; set; }

        public PaletteService? Service
        {
            get => (PaletteService)GetValue(ServiceProperty);
            set => SetValue(ServiceProperty, value);
        }

        public static readonly DependencyProperty ServiceProperty =
            DependencyProperty.Register(nameof(Service), typeof(PaletteService),
            typeof(QuickSwatches),
            new PropertyMetadata(null, OnServiceChanged));

        private static void OnServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (QuickSwatches)d;
            if (e.OldValue is PaletteService oldS)
            {
                oldS.ForegroundChanged -= self.OnFgChanged;
                oldS.BackgroundChanged -= self.OnBgChanged;
            }
            if (e.NewValue is PaletteService s)
            {
                s.ForegroundChanged += self.OnFgChanged;
                s.BackgroundChanged += self.OnBgChanged;
                self.SyncAll();
            }
        }

        public QuickSwatches()
        {
            InitializeComponent();

            // Set cached brushes immediately
            FgSwatch.Background = _fgBrush;
            BgSwatch.Background = _bgBrush;

            Loaded += OnLoaded;

            // Build the flyout UI in code
            _picker = new ColorPicker { MinWidth = 620, MinHeight = 360, IsAlphaEnabled = true };

            var applyBtn = new Button { Content = "Apply" };
            var cancelBtn = new Button { Content = "Cancel" };
            applyBtn.Click += PickerApply_Click;
            cancelBtn.Click += PickerCancel_Click;

            var btns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btns.Children.Add(applyBtn);
            btns.Children.Add(cancelBtn);

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(_picker);
            panel.Children.Add(btns);

            _flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.Bottom,
                Content = panel,
                FlyoutPresenterStyle = (Style)Application.Current.Resources["PickerFlyoutPresenterStyle"]
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SyncAll();
            Loaded -= OnLoaded; // Unsubscribe to prevent multiple calls
        }

        private void SyncAll()
        {
            if (Service is null) return;
            OnFgChanged(Service.Foreground);
            OnBgChanged(Service.Background);
        }

        private void OnFgChanged(uint c)
        {
            _fgBrush.Color = ColorUtil.ToColor(c);
        }

        private void OnBgChanged(uint c)
        {
            _bgBrush.Color = ColorUtil.ToColor(c);

        }

        // taps: left = set active color in engine as well (host can also listen to Service events)
        private void FgSwatch_Tapped(object s, TappedRoutedEventArgs e)
        {
            // no-op; FG already selected. Show picker on double/right via context.
        }

        private void BgSwatch_Tapped(object s, TappedRoutedEventArgs e)
        {
            // no-op
        }

        private void Swap_Click(object sender, RoutedEventArgs e) => Service?.Swap();

        private void Default_Click(object sender, RoutedEventArgs e)
        {
            if (Service is null) return;
            Service.SetForeground(0xFF000000);
            Service.SetBackground(0xFFFFFFFF);
        }

        // Context actions
        private void FgSetAsBg_Click(object sender, RoutedEventArgs e)
        {
            if (Service is null) return;
            Service.SetBackground(Service.Foreground);
        }

        private void BgSetAsFg_Click(object sender, RoutedEventArgs e)
        {
            if (Service is null) return;
            Service.SetForeground(Service.Background);
        }

        private void PickerApply_Click(object sender, RoutedEventArgs e)
        {
            if (Service is null) return;
            var v = ColorUtil.ToBGRA(_picker.Color);
            if (_target == PickTarget.FG)
                Service.SetForeground(v);
            else if (_target == PickTarget.BG)
                Service.SetBackground(v);
            _target = PickTarget.None;
            _flyout.Hide();
        }

        private void PickerCancel_Click(object sender, RoutedEventArgs e)
        {
            _target = PickTarget.None;
            _flyout.Hide();
        }

        // Double-click → open picker
        private void FgSwatch_DoubleTapped(object s, DoubleTappedRoutedEventArgs e) => OpenColorWindow(true, FgSwatch);
        private void BgSwatch_DoubleTapped(object s, DoubleTappedRoutedEventArgs e) => OpenColorWindow(false, BgSwatch);

        private void FgPick_Click(object sender, RoutedEventArgs e) => OpenColorWindow(true, FgSwatch);
        private void BgPick_Click(object sender, RoutedEventArgs e) => OpenColorWindow(false, BgSwatch);

        private void FgAddToPalette_Click(object sender, RoutedEventArgs e)
        {
            Service?.AddColor(Service.Foreground);
        }

        private void BgAddToPalette_Click(object sender, RoutedEventArgs e)
        {
            Service?.AddColor(Service.Background);
        }

        private void OpenColorWindow(bool isFg, FrameworkElement anchor, Point? cursorInRoot = null)
        {
            if (Service is null) return;

            uint prev = isFg ? Service.Foreground : Service.Background;
            var prevColor = ColorUtil.ToColor(prev);

            // ask ToolRail/host what the current brush opacity is
            var rail = HostRail;
            byte brushA = rail?.GetBrushOpacity?.Invoke() ?? prevColor.A;

            var win = new ColorPickerWindow
            {
                // live: update RGB in palette, keep A from palette
                SetLive = c =>
                {
                    // keep swatch alpha, only replace RGB
                    uint merged = (prev & 0xFF000000u) | (ColorUtil.ToBGRA(c) & 0x00FFFFFFu);

                    if (isFg) Service.SetForeground(merged);
                    else Service.SetBackground(merged);

                    // push to active brush immediately
                    rail?.RequestSetBrushColor?.Invoke(merged);

                    // sync picker alpha → brush opacity
                    rail?.RequestSetBrushOpacity?.Invoke(c.A);
                },
                Commit = c =>
                {
                    uint prevA = isFg ? Service.Foreground : Service.Background;
                    uint merged = (prevA & 0xFF000000u) | (ColorUtil.ToBGRA(c) & 0x00FFFFFFu);

                    if (isFg) Service.SetForeground(merged);
                    else Service.SetBackground(merged);

                    rail?.RequestSetBrushColor?.Invoke(merged);
                    rail?.RequestSetBrushOpacity?.Invoke(c.A);
                }
            };

            // Old swatch shows actual palette color; current is seeded with brush opacity
            var current = prevColor; current.A = brushA;
            win.Load(prevColor, current);

            // keep a handle so we can push slider->picker alpha updates
            rail?.RegisterOpenPicker(win);

            // If you want an explicit unregister (optional; the Register handler already cleans up):
            win.Closed += (_, __) => rail?.UnregisterOpenPicker(win);

            win.Activate();

            var appW = WindowHost.ApplyChrome(win, resizable: false, alwaysOnTop: true, minimizable: false, title: "Color Picker", owner: App.PixlPunktMainWindow);
            WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.90, minLogicalWidth: 560, minLogicalHeight: 360);
            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }
    }
}
