using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.UI.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Windows;
#if HAS_UNO
using Uno.WinUI.Graphics2DSK;
#endif
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

#if HAS_UNO
        // SKCanvasElement instance for hardware-accelerated rendering (Uno platforms)
        private LayerPreviewElement? _bgCanvasElement;
#endif
        // SKXamlCanvas fallback for WinAppSdk
        private SKXamlCanvas? _bgCanvasXaml;

        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private double _lastScale = -1.0;

        public LayerPreviewHost()
        {
            InitializeComponent();

            // Initialize canvas element based on platform
            InitializeCanvasElement();

            // Apply stripe colors immediately to ensure correct colors before first render
            ApplyStripeColors();

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

            SizeChanged += (_, __) => InvalidateBgCanvas();
        }

        /// <summary>
        /// Creates and initializes the appropriate canvas element based on platform.
        /// </summary>
        private void InitializeCanvasElement()
        {
#if HAS_UNO
            // Use SKCanvasElement on Uno platforms for better performance
            if (SKCanvasElement.IsSupportedOnCurrentPlatform())
            {
                _bgCanvasElement = new LayerPreviewElement
                {
                    DrawCallback = RenderBackground
                };
                BgCanvasContainer.Children.Add(_bgCanvasElement);
                return;
            }
#endif
            // Fall back to SKXamlCanvas on WinAppSdk or unsupported Uno platforms
            _bgCanvasXaml = new SKXamlCanvas();
            _bgCanvasXaml.PaintSurface += BgCanvas_PaintSurface;
            BgCanvasContainer.Children.Add(_bgCanvasXaml);
        }

        /// <summary>
        /// Invalidates the background canvas to trigger a redraw.
        /// </summary>
        private void InvalidateBgCanvas()
        {
#if HAS_UNO
            _bgCanvasElement?.Invalidate();
#endif
            _bgCanvasXaml?.Invalidate();
        }

        /// <summary>
        /// Paint surface handler for SKXamlCanvas (WinAppSdk fallback).
        /// </summary>
        private void BgCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            RenderBackground(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        }

        private void OnStripeColorsChanged()
        {
            ApplyStripeColors();
            InvalidatePattern();
            Rendering.TransparencyPatternShader.InvalidateAll();
            InvalidateBgCanvas();
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
                InvalidateBgCanvas();
            }
        }

        /// <summary>
        /// Main render callback for both SKCanvasElement and SKXamlCanvas.
        /// </summary>
        private void RenderBackground(SKCanvas canvas, float width, float height)
        {
            if (width <= 0 || height <= 0)
            {
                canvas.Clear(SKColors.Transparent);
                return;
            }

            var (lightColor, darkColor) = _pattern.CurrentScheme;
            var skLight = new SKColor(lightColor.R, lightColor.G, lightColor.B, lightColor.A);
            var skDark = new SKColor(darkColor.R, darkColor.G, darkColor.B, darkColor.A);
            var shader = Rendering.TransparencyPatternShader.GetShader(4, skLight, skDark);

            if (shader != null)
            {
                using var paint = new SKPaint
                {
                    Shader = shader,
                    IsAntialias = false
                };
                canvas.DrawRect(0, 0, width, height, paint);
            }
            else
            {
                canvas.Clear(new SKColor(lightColor.R, lightColor.G, lightColor.B, lightColor.A));
            }

            if (PreviewImage != null && PreviewImage.RenderTransform != null)
            {
                PreviewImage.RenderTransform = null;
                PreviewImage.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }
    }
}
