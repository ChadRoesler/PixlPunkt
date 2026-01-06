using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PixlPunkt.Uno.UI.CanvasArea
{
    public sealed partial class SelectionOverlay : UserControl
    {
        public SelectionOverlay()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _timer.Tick += (_, __) => { _phase = (_phase + 1) % 6; R1.StrokeDashOffset = _phase; R2.StrokeDashOffset = (_phase + 3) % 6; };
        }

        DispatcherTimer _timer; int _phase;

        public void ShowRect(double x, double y, double w, double h, bool showHandles)
        {
            IsHitTestVisible = false;
            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(R1, x); Microsoft.UI.Xaml.Controls.Canvas.SetTop(R1, y); R1.Width = w; R1.Height = h;
            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(R2, x); Microsoft.UI.Xaml.Controls.Canvas.SetTop(R2, y); R2.Width = w; R2.Height = h;
            R1.Visibility = R2.Visibility = Visibility.Visible;

            var sz = 6.0; var pad = sz / 2.0; var rotUp = 14.0;
            void P(FrameworkElement el, double ex, double ey) { Microsoft.UI.Xaml.Controls.Canvas.SetLeft(el, ex - pad); Microsoft.UI.Xaml.Controls.Canvas.SetTop(el, ey - pad); el.Visibility = showHandles ? Visibility.Visible : Visibility.Collapsed; }
            P(HNW, x, y); P(HN, x + w / 2, y); P(HNE, x + w, y);
            P(HW, x, y + h / 2); P(HE, x + w, y + h / 2);
            P(HSW, x, y + h); P(HS, x + w / 2, y + h); P(HSE, x + w, y + h);
            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(HROT, x + w / 2 - 4); Microsoft.UI.Xaml.Controls.Canvas.SetTop(HROT, y - rotUp); HROT.Visibility = showHandles ? Visibility.Visible : Visibility.Collapsed;

            if (!_timer.IsEnabled) _timer.Start();
        }

        public void HideAll()
        {
            R1.Visibility = R2.Visibility = Visibility.Collapsed;
            HNW.Visibility = HN.Visibility = HNE.Visibility =
            HW.Visibility = HE.Visibility =
            HSW.Visibility = HS.Visibility = HSE.Visibility = HROT.Visibility = Visibility.Collapsed;
            if (_timer.IsEnabled) _timer.Stop();
        }
    }
}
