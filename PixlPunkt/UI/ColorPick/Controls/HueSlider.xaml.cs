using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Coloring.Helpers;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;

namespace PixlPunkt.UI.ColorPick.Controls
{
    public sealed partial class HueSlider : UserControl
    {
        public event EventHandler<double>? HueChanging;
        public event EventHandler<double>? HueChanged;

        public static readonly DependencyProperty HueProperty =
            DependencyProperty.Register(nameof(Hue), typeof(double), typeof(HueSlider),
                new PropertyMetadata(0d, OnHueChangedDp));

        public double Hue
        {
            get => (double)GetValue(HueProperty);
            set => SetValue(HueProperty, NormalizeHue(value));
        }

        private static void OnHueChangedDp(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HueSlider)d;
            ctrl.PART_Canvas?.Invalidate();
        }

        private bool _drag;
        private uint _pid;
        private bool _invalidateGradient = true;
        private SKBitmap? _cache;
        private int _lastCacheWidth;
        private int _lastCacheHeight;

        private const float HANDLE_W = 12f;
        private const float HANDLE_H = 18f;
        private const float HANDLE_R = 6f;
        private const float INSET = HANDLE_R + 2f;

        public HueSlider()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                PART_Canvas.Width = ActualWidth;
                PART_Canvas.Height = Math.Max(18, ActualHeight);
                PART_Canvas.Invalidate();
            };
            Unloaded += (_, __) =>
            {
                _cache?.Dispose();
                _cache = null;
            };
            IsTabStop = true;
            KeyDown += HueSlider_KeyDown;
            SizeChanged += (_, __) =>
            {
                _invalidateGradient = true;
                PART_Canvas.Invalidate();
            };
        }

        private void HueSlider_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            double step = 1;
            if ((e.Key == Windows.System.VirtualKey.Left) || (e.Key == Windows.System.VirtualKey.Right))
            {
                bool left = e.Key == Windows.System.VirtualKey.Left;

                var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
                var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
                bool shift = shiftState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                bool ctrl = ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

                if (shift) step = 10;
                else if (ctrl) step = 0.25;
                Hue = WrapHue(Hue + (left ? -step : step));
                PART_Canvas.Invalidate();
                HueChanged?.Invoke(this, Hue);
                e.Handled = true;
            }
        }

        public void InvalidateGradient()
        {
            _invalidateGradient = true;
            PART_Canvas?.Invalidate();
        }

        private static double NormalizeHue(double h) => h switch
        {
            < 0 => WrapHue(h),
            > 360 => WrapHue(h),
            _ => h
        };

        private static double WrapHue(double h)
        {
            h %= 360.0;
            if (h < 0) h += 360.0;
            return h;
        }

        private void Canvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            float W = (float)PART_Canvas.ActualWidth;
            float H = (float)PART_Canvas.ActualHeight;

            canvas.Clear(SKColors.Transparent);

            var paintRect = new SKRect(INSET, 0, Math.Max(INSET + 1, W - INSET), H);
            int paintW = Math.Max(1, (int)paintRect.Width);
            int paintH = Math.Max(1, (int)H);

            // Build gradient cache
            if (_invalidateGradient || _cache == null ||
                _lastCacheWidth != paintW || _lastCacheHeight != paintH)
            {
                _cache?.Dispose();
                _cache = new SKBitmap(paintW, paintH, SKColorType.Bgra8888, SKAlphaType.Premul);

                for (int x = 0; x < paintW; x++)
                {
                    double h = 360.0 * x / (paintW - 1);
                    var c = ColorUtil.FromHSL(h, 1, 0.5, 255);
                    var skColor = new SKColor(c.R, c.G, c.B, c.A);

                    for (int y = 0; y < paintH; y++)
                        _cache.SetPixel(x, y, skColor);
                }

                _lastCacheWidth = paintW;
                _lastCacheHeight = paintH;
                _invalidateGradient = false;
            }

            // Draw gradient
            canvas.DrawBitmap(_cache, paintRect.Left, 0);

            // Selection Pill
            float ix = paintRect.Left + (float)(Hue / 360.0) * paintRect.Width;
            var rect = new SKRect(ix - HANDLE_W / 2f, H / 2f - HANDLE_H / 2f,
                                  ix + HANDLE_W / 2f, H / 2f + HANDLE_H / 2f);
            var inner = new SKRect(rect.Left + 1.2f, rect.Top + 1.2f, rect.Right - 1.2f, rect.Bottom - 1.2f);

            using var blackPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.Black, StrokeWidth = 2f, IsAntialias = true };
            using var whitePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.White, StrokeWidth = 1.5f, IsAntialias = true };

            canvas.DrawRoundRect(rect, HANDLE_R, HANDLE_R, blackPaint);
            canvas.DrawRoundRect(inner, HANDLE_R - 1.2f, HANDLE_R - 1.2f, whitePaint);
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
            var paintRect = new SKRect(INSET, 0, Math.Max(INSET + 1, W - INSET), 1);
            double h = Math.Clamp((p.X - paintRect.Left) / paintRect.Width, 0, 1) * 360.0;
            Hue = h;
            PART_Canvas.Invalidate();
            if (live) HueChanging?.Invoke(this, Hue);
            else HueChanged?.Invoke(this, Hue);
        }
    }
}