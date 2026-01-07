using System;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.UI.Rendering;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.Layers.Controls
{
    public sealed partial class LayerPreviewHost : UserControl
    {
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(LayerPreviewHost),
                new PropertyMetadata(null));
        private readonly PatternBackgroundService _pattern = new();
        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private double _lastScale = -1.0;

        public LayerPreviewHost()
        {
            InitializeComponent();

            Loaded += (_, __) =>
            {
                HookDpi();
                ApplyStripeColors();
                TransparencyStripeMixer.ColorsChanged += OnStripeColorsChanged;
                InvalidatePattern();
            };

            Unloaded += (_, __) =>
            {
                TransparencyStripeMixer.ColorsChanged -= OnStripeColorsChanged;
            };

            SizeChanged += (_, __) => BgCanvas.Invalidate();
        }
        private void OnStripeColorsChanged()
        {
            ApplyStripeColors();
            InvalidatePattern();
            BgCanvas.Invalidate();
        }

        private void ApplyStripeColors()
        {
            var light = Color.FromArgb(255,
                TransparencyStripeMixer.LightR,
                TransparencyStripeMixer.LightG,
                TransparencyStripeMixer.LightB);

            var dark = Color.FromArgb(255,
                TransparencyStripeMixer.DarkR,
                TransparencyStripeMixer.DarkG,
                TransparencyStripeMixer.DarkB);

            if (_pattern.LightColor != light || _pattern.DarkColor != dark)
            {
                _pattern.LightColor = light;
                _pattern.DarkColor = dark;
            }
        }

        private void InvalidatePattern() => _pattern.Invalidate();
        private void HookDpi()
        {
            var xr = XamlRoot;
            if (xr == null) return;
            xr.Changed -= XamlRoot_Changed;
            xr.Changed += XamlRoot_Changed;
            _lastScale = xr.RasterizationScale;
        }

        private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            var s = sender.RasterizationScale;
            if (Math.Abs(s - _lastScale) > 0.001)
            {
                _lastScale = s;
                InvalidatePattern();
                BgCanvas.Invalidate();
            }
        }



        private void BgCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;

            float w = (float)sender.ActualWidth;
            float h = (float)sender.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double dpi = XamlRoot?.RasterizationScale ?? 1.0;
            var img = _pattern.GetSizedImage(sender.Device, dpi, w, h);

            // snap to whole pixels
            float tx = 0f, ty = 0f;
            var target = new Rect(tx, ty, w, h);
            var src = new Rect(0, 0, img.SizeInPixels.Width, img.SizeInPixels.Height);

            ds.DrawImage(img, target, src);


            // Clear any explicit RenderTransform previously set; rely on XAML Stretch="Uniform" to fit the image.
            // This avoids using ImageSource dimensions (not available) and keeps behavior simple and correct.
            if (PreviewImage != null && PreviewImage.RenderTransform != null)
            {
                PreviewImage.RenderTransform = null;
                PreviewImage.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }
    }
}