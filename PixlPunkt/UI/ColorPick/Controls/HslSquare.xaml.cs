using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Coloring.Helpers;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.ColorPick.Controls
{
    public sealed partial class HslSquare : UserControl
    {
        public event EventHandler<(double S, double L)>? SVChanging;
        public event EventHandler<(double S, double L)>? SVChanged;

        public static readonly DependencyProperty HueProperty =
            DependencyProperty.Register(nameof(Hue), typeof(double), typeof(HslSquare),
                new PropertyMetadata(0d, OnHueChanged));

        public static readonly DependencyProperty SaturationProperty =
            DependencyProperty.Register(nameof(Saturation), typeof(double), typeof(HslSquare),
                new PropertyMetadata(0d, OnSelectionChanged));

        public static readonly DependencyProperty LightnessProperty =
            DependencyProperty.Register(nameof(Lightness), typeof(double), typeof(HslSquare),
                new PropertyMetadata(0d, OnSelectionChanged));

        public double Hue
        {
            get => (double)GetValue(HueProperty);
            set => SetValue(HueProperty, NormalizeHue(value));
        }
        public double Saturation
        {
            get => (double)GetValue(SaturationProperty);
            set => SetValue(SaturationProperty, Clamp01(value));
        }
        public double Lightness
        {
            get => (double)GetValue(LightnessProperty);
            set => SetValue(LightnessProperty, Clamp01(value));
        }

        private static void OnHueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HslSquare)d;
            ctrl._needsGradient = true;
            ctrl.PART_Canvas?.Invalidate();
        }
        private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HslSquare)d;
            ctrl.PART_Canvas?.Invalidate();
        }

        private bool _drag;
        private uint _pid;
        private SKBitmap? _gradientBitmap;
        private bool _needsGradient = true;
        private int _lastGradientWidth;
        private int _lastGradientHeight;

        private const float HANDLE_OUT = 9f;
        private const float HANDLE_IN = 7f;
        private const float INSET = HANDLE_OUT + 2f;

        public HslSquare()
        {
            InitializeComponent();
            SizeChanged += (_, __) =>
            {
                _needsGradient = true;
                PART_Canvas.Invalidate();
            };
            Unloaded += (_, __) =>
            {
                _gradientBitmap?.Dispose();
                _gradientBitmap = null;
            };
        }

        private static double NormalizeHue(double h)
        {
            h %= 360.0;
            if (h < 0) h += 360.0;
            return h;
        }
        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        private void Canvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            float W = (float)PART_Canvas.ActualWidth;
            float H = (float)PART_Canvas.ActualHeight;

            canvas.Clear(SKColors.Transparent);

            var paintRect = new SKRect(INSET, INSET,
                Math.Max(INSET + 1, W - INSET),
                Math.Max(INSET + 1, H - INSET));

            int paintW = Math.Max(1, (int)paintRect.Width);
            int paintH = Math.Max(1, (int)paintRect.Height);

            // Rebuild gradient bitmap if needed
            if (_needsGradient || _gradientBitmap == null ||
                _lastGradientWidth != paintW || _lastGradientHeight != paintH)
            {
                _gradientBitmap?.Dispose();
                _gradientBitmap = new SKBitmap(paintW, paintH, SKColorType.Bgra8888, SKAlphaType.Premul);

                for (int iy = 0; iy < paintH; iy++)
                {
                    double l = 1.0 - (iy / (double)(paintH - 1));
                    for (int ix = 0; ix < paintW; ix++)
                    {
                        double s = ix / (double)(paintW - 1);
                        var c = ColorUtil.FromHSL(Hue, s, l, 255);
                        _gradientBitmap.SetPixel(ix, iy, new SKColor(c.R, c.G, c.B, c.A));
                    }
                }

                _lastGradientWidth = paintW;
                _lastGradientHeight = paintH;
                _needsGradient = false;
            }

            // Draw gradient
            canvas.DrawBitmap(_gradientBitmap, paintRect.Left, paintRect.Top);

            // Draw selection handle
            float cx = paintRect.Left + (float)(Saturation * paintRect.Width);
            float cy = paintRect.Top + (float)((1.0 - Lightness) * paintRect.Height);

            using var blackPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.Black, StrokeWidth = 2f, IsAntialias = true };
            using var whitePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.White, StrokeWidth = 1.6f, IsAntialias = true };

            canvas.DrawCircle(cx, cy, HANDLE_OUT, blackPaint);
            canvas.DrawCircle(cx, cy, HANDLE_IN, whitePaint);
        }

        private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _drag = true;
            _pid = e.Pointer.PointerId;
            PART_Canvas.CapturePointer(e.Pointer);
            UpdateFromPoint(e.GetCurrentPoint(PART_Canvas).Position, live: true);
            e.Handled = true;
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_drag || e.Pointer.PointerId != _pid) return;
            UpdateFromPoint(e.GetCurrentPoint(PART_Canvas).Position, live: true);
        }

        private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_drag || e.Pointer.PointerId != _pid) return;
            _drag = false;
            PART_Canvas.ReleasePointerCaptures();
            UpdateFromPoint(e.GetCurrentPoint(PART_Canvas).Position, live: false);
            e.Handled = true;
        }

        private void UpdateFromPoint(Point p, bool live)
        {
            float W = (float)PART_Canvas.ActualWidth;
            float H = (float)PART_Canvas.ActualHeight;
            var paintRect = new SKRect(INSET, INSET,
                Math.Max(INSET + 1, W - INSET),
                Math.Max(INSET + 1, H - INSET));

            double s = Clamp01((p.X - paintRect.Left) / paintRect.Width);
            double l = Clamp01(1.0 - ((p.Y - paintRect.Top) / paintRect.Height));

            Saturation = s;
            Lightness = l;

            if (live) SVChanging?.Invoke(this, (s, l));
            else SVChanged?.Invoke(this, (s, l));
        }
    }
}
