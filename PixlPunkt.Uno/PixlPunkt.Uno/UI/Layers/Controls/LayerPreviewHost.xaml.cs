using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Uno.UI.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.Uno.UI.Layers.Controls
{
    public sealed partial class LayerPreviewHost : UserControl
    {
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(LayerPreviewHost),
                new PropertyMetadata(null));
        private readonly PatternBackgroundService _pattern = new();

        // Cached checkerboard pattern
        private SKShader? _checkerboardShader;
        private SKBitmap? _checkerboardBitmap;

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
                _checkerboardShader?.Dispose();
                _checkerboardBitmap?.Dispose();
            };

            SizeChanged += (_, __) => BgCanvas.Invalidate();
        }

        private void OnStripeColorsChanged()
        {
            ApplyStripeColors();
            InvalidatePattern();
            InvalidateCheckerboardCache();
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

        private void InvalidateCheckerboardCache()
        {
            _checkerboardShader?.Dispose();
            _checkerboardShader = null;
            _checkerboardBitmap?.Dispose();
            _checkerboardBitmap = null;
        }

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
                InvalidateCheckerboardCache();
                BgCanvas.Invalidate();
            }
        }

        private void BgCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;

            float w = (float)BgCanvas.ActualWidth;
            float h = (float)BgCanvas.ActualHeight;
            if (w <= 0 || h <= 0)
            {
                canvas.Clear(SKColors.Transparent);
                return;
            }

            // Get colors from pattern service
            var (lightColor, darkColor) = _pattern.CurrentScheme;

            // Draw checkerboard background
            int squareSize = 4; // Smaller squares for layer previews
            EnsureCheckerboardShader(squareSize, lightColor, darkColor);

            if (_checkerboardShader != null)
            {
                using var paint = new SKPaint
                {
                    Shader = _checkerboardShader,
                    IsAntialias = false
                };
                canvas.DrawRect(0, 0, w, h, paint);
            }
            else
            {
                // Fallback: just fill with light color
                canvas.Clear(new SKColor(lightColor.R, lightColor.G, lightColor.B, lightColor.A));
            }

            // Clear any explicit RenderTransform previously set; rely on XAML Stretch="Uniform" to fit the image.
            if (PreviewImage != null && PreviewImage.RenderTransform != null)
            {
                PreviewImage.RenderTransform = null;
                PreviewImage.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        private void EnsureCheckerboardShader(int squareSize, Color lightColor, Color darkColor)
        {
            if (_checkerboardBitmap != null && _checkerboardShader != null)
                return;

            _checkerboardShader?.Dispose();
            _checkerboardBitmap?.Dispose();

            int tileSize = squareSize * 2;
            _checkerboardBitmap = new SKBitmap(tileSize, tileSize, SKColorType.Bgra8888, SKAlphaType.Premul);

            var skLight = new SKColor(lightColor.R, lightColor.G, lightColor.B, lightColor.A);
            var skDark = new SKColor(darkColor.R, darkColor.G, darkColor.B, darkColor.A);

            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    int cx = x / squareSize;
                    int cy = y / squareSize;
                    bool isLight = ((cx + cy) & 1) == 0;
                    _checkerboardBitmap.SetPixel(x, y, isLight ? skLight : skDark);
                }
            }

            using var image = SKImage.FromBitmap(_checkerboardBitmap);
            _checkerboardShader = image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        }
    }
}