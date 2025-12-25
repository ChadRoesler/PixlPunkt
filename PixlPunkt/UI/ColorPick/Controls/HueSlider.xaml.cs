using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Coloring.Helpers;
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
        private CanvasRenderTarget? _cache;
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

                // Use modern WinUI 3 API for checking modifier keys
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

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            float W = (float)sender.ActualWidth;
            float H = (float)sender.ActualHeight;
            var paint = new Rect(INSET, 0, Math.Max(1, W - 2 * INSET), H);

            if (_invalidateGradient || _cache == null ||
                _cache.Size.Width != paint.Width || _cache.Size.Height != paint.Height)
            {
                _cache?.Dispose();
                _cache = new CanvasRenderTarget(sender, (float)paint.Width, (float)paint.Height);
                using (var gds = _cache.CreateDrawingSession())
                {
                    int steps = 60;
                    float seg = (float)paint.Width / steps;
                    for (int i = 0; i < steps; i++)
                    {
                        double h0 = 360.0 * i / steps;
                        double h1 = 360.0 * (i + 1) / steps;
                        var c0 = ColorUtil.FromHSL(h0, 1, 0.5, 255);
                        var c1 = ColorUtil.FromHSL(h1, 1, 0.5, 255);
                        float x = i * seg;
                        using var gb = new CanvasLinearGradientBrush(sender, c0, c1)
                        {
                            StartPoint = new Vector2(x, 0),
                            EndPoint = new Vector2(x + seg, 0)
                        };
                        gds.FillRectangle(new Rect(x, 0, seg + 1, H), gb);
                    }
                }
                _invalidateGradient = false;
            }

            ds.DrawImage(_cache, new Vector2((float)paint.X, 0));

            // Selection Pill
            float ix = (float)(paint.X + (Hue / 360.0) * paint.Width);
            var rect = new Rect(ix - HANDLE_W / 2f, H / 2f - HANDLE_H / 2f, HANDLE_W, HANDLE_H);
            var inner = new Rect(rect.X + 1.2f, rect.Y + 1.2f, rect.Width - 2.4f, rect.Height - 2.4f);
            ds.DrawRoundedRectangle(rect, HANDLE_R, HANDLE_R, Colors.Black, 2f);
            ds.DrawRoundedRectangle(inner, HANDLE_R - 1.2f, HANDLE_R - 1.2f, Colors.White, 1.5f);
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
            var paint = new Rect(INSET, 0, Math.Max(1, W - 2 * INSET), 1);
            double h = Math.Clamp((p.X - paint.X) / paint.Width, 0, 1) * 360.0;
            Hue = h;
            PART_Canvas.Invalidate();
            if (live) HueChanging?.Invoke(this, Hue);
            else HueChanged?.Invoke(this, Hue);
        }
    }
}