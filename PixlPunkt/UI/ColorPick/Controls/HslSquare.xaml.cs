using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Coloring.Helpers;
using Windows.Foundation;

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
        private CanvasRenderTarget? _gradient;
        private bool _needsGradient = true;

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
                _gradient?.Dispose();
                _gradient = null;
            };
        }

        private static double NormalizeHue(double h)
        {
            h %= 360.0;
            if (h < 0) h += 360.0;
            return h;
        }
        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            float W = (float)sender.ActualWidth;
            float H = (float)sender.ActualHeight;
            var paint = new Rect(INSET, INSET,
                Math.Max(1, W - 2 * INSET),
                Math.Max(1, H - 2 * INSET));

            if (_needsGradient || _gradient == null ||
                _gradient.Size.Width != paint.Width || _gradient.Size.Height != paint.Height)
            {
                _gradient?.Dispose();
                _gradient = new CanvasRenderTarget(sender, (float)paint.Width, (float)paint.Height);
                using (var gs = _gradient.CreateDrawingSession())
                {
                    int cols = 64, rows = 64;
                    for (int iy = 0; iy < rows; iy++)
                    {
                        double l = 1.0 - (iy / (rows - 1.0));
                        for (int ix = 0; ix < cols; ix++)
                        {
                            double s = ix / (cols - 1.0);
                            var c = ColorUtil.FromHSL(Hue, s, l, 255);
                            float x = ix * (float)paint.Width / cols;
                            float y = iy * (float)paint.Height / rows;
                            gs.FillRectangle(new Rect(x, y, paint.Width / cols + 1, paint.Height / rows + 1), c);
                        }
                    }
                }
                _needsGradient = false;
            }

            ds.DrawImage(_gradient, new Vector2((float)paint.X, (float)paint.Y));

            float cx = (float)(paint.X + Saturation * paint.Width);
            float cy = (float)(paint.Y + (1.0 - Lightness) * paint.Height);
            ds.DrawCircle(cx, cy, HANDLE_OUT, Colors.Black, 2f);
            ds.DrawCircle(cx, cy, HANDLE_IN, Colors.White, 1.6f);
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
            var paint = new Rect(INSET, INSET,
                Math.Max(1, W - 2 * INSET),
                Math.Max(1, H - 2 * INSET));

            double s = Clamp01((p.X - paint.X) / paint.Width);
            double l = Clamp01(1.0 - ((p.Y - paint.Y) / paint.Height));

            Saturation = s;
            Lightness = l;

            if (live) SVChanging?.Invoke(this, (s, l));
            else SVChanged?.Invoke(this, (s, l));
        }
    }
}