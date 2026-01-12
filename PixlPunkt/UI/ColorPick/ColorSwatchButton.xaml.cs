using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Enums;
using PixlPunkt.UI.ColorPick;
using PixlPunkt.UI.Helpers;

namespace PixlPunkt.UI.Controls
{
    public sealed partial class ColorSwatchButton : UserControl
    {
        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register(
                nameof(Color),
                typeof(uint),
                typeof(ColorSwatchButton),
                new PropertyMetadata(0x00000000u, OnColorChanged));

        public uint Color
        {
            get => (uint)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public event EventHandler<uint>? ColorChanged;

        private readonly SolidColorBrush _brush = new();

        public ColorSwatchButton()
        {
            InitializeComponent();

            // Attach our brush to the *button* background
            RootButton.Background = _brush;

            // Optional: nice stroke
            RootButton.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

            UpdateBrush();
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (ColorSwatchButton)d;
            self.UpdateBrush();
            self.ColorChanged?.Invoke(self, (uint)e.NewValue);
        }

        private void UpdateBrush()
        {
            // Take uint BGRA and turn it into a Color using your existing util
            _brush.Color = ColorUtil.ToColor(Color);
        }

        private void RootButton_Click(object sender, RoutedEventArgs e)
        {
            OpenColorPicker();
        }

        private void OpenColorPicker()
        {
            uint initial = Color == 0 ? 0xFFFFFFFFu : Color;

            var win = new ColorPickerWindow
            {
                GetCurrent = () => ColorUtil.ToColor(initial),

                SetLive = c =>
                {
                    uint bgra = ColorUtil.ToBGRA(c);
                    Color = bgra;        // updates DP → brush → button background
                },

                Commit = c =>
                {
                    uint bgra = ColorUtil.ToBGRA(c);
                    Color = bgra;
                }
            };

            var col = ColorUtil.ToColor(initial);
            win.Load(col, col);
            win.Activate();

            var appW = WindowHost.ApplyChrome(
                win,
                resizable: false,
                alwaysOnTop: true,
                minimizable: false,
                title: "Color");

            WindowHost.FitToContentAfterLayout(
                win,
                (FrameworkElement)win.Content,
                maxScreenFraction: 0.90,
                minLogicalWidth: 560,
                minLogicalHeight: 360);

            WindowHost.Place(
                appW,
                WindowPlacement.CenterOnScreen,
                App.PixlPunktMainWindow);
        }
    }
}
