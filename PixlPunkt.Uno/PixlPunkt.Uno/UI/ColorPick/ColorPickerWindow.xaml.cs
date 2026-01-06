using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Uno.Core.Coloring;
using PixlPunkt.Uno.Core.Coloring.Helpers;
using Windows.UI;

namespace PixlPunkt.Uno.UI.ColorPick
{
    public sealed partial class ColorPickerWindow : Window
    {
        private const int LADDER_UPDATE_DELAY_MS = 50;

        public Func<Color> GetCurrent { get; set; } = null!;
        public Action<Color> SetLive { get; set; } = null!;
        public Action<Color> Commit { get; set; } = null!;

        /// <summary>
        /// Event raised when the dropper button is toggled on, requesting canvas pick mode.
        /// The host should enable canvas click-to-sample and call <see cref="SetPickedColor"/> when a color is sampled.
        /// </summary>
        public event Action<bool>? DropperModeRequested;

        public SolidColorBrush OldBrush => _oldBrush;
        public SolidColorBrush NewBrush => _newBrush;

        private double _h;
        private double _s;
        private double _l;
        private byte _a = 255;
        private Color _currentColor;

        private bool _suppress;
        private bool _freezeLadders;

        private readonly SolidColorBrush _oldBrush = new(Colors.Black);
        private readonly SolidColorBrush _newBrush = new(Colors.Black);

        public ColorPickerWindow()
        {
            InitializeComponent();

            ShadeRow.SwatchClicked += LadderRow_SwatchClicked;
            TintRow.SwatchClicked += LadderRow_SwatchClicked;
            HueRow.SwatchClicked += LadderRow_SwatchClicked;
            ToneRow.SwatchClicked += LadderRow_SwatchClicked;

            Closed += (_, __) => { /* no canvas cleanup needed now */ };

            var c = Colors.White;
            SetFromColor(c);
            BuildLadders();

            // Wire hue slider
            HueSlider.HueChanging += HueSlider_HueChanging;
            HueSlider.HueChanged += HueSlider_HueChanged;
            HueSlider.Hue = _h;

            // Wire HSL square initial values
            HslSquare.Hue = _h;
            HslSquare.Saturation = _s;
            HslSquare.Lightness = _l;
            HslSquare.SVChanging += HslSquare_SVChanging;
            HslSquare.SVChanged += HslSquare_SVChanged;
        }

        public void Load(Color old, Color current)
        {
            _oldBrush.Color = ColorUtil.MakeOpaque(old);
            SetFromColor(current);
            HueSlider.Hue = _h;
            HslSquare.Hue = _h;
            HslSquare.Saturation = _s;
            HslSquare.Lightness = _l;
        }

        public void SetExternalAlpha(byte a)
        {
            _a = a;
            _suppress = true;
            AlphaSlider.Value = a;
            AlphaBox.Value = a;
            _suppress = false;
            _newBrush.Color = ColorUtil.MakeOpaque(CurrentColor());
        }

        /// <summary>
        /// Called by the host when a color is picked from the canvas via the dropper mode.
        /// Sets the current color and turns off dropper mode.
        /// </summary>
        /// <param name="bgra">The picked color in BGRA format.</param>
        public void SetPickedColor(uint bgra)
        {
            var c = ColorUtil.ToColor(bgra);
            c.A = _a; // Preserve current alpha
            SetFromColor(c);

            // Turn off dropper mode
            if (DropperButton != null)
            {
                DropperButton.IsChecked = false;
            }
        }

        /// <summary>
        /// Gets whether dropper mode is currently active.
        /// </summary>
        public bool IsDropperModeActive => DropperButton?.IsChecked == true;

        private void DropperButton_Checked(object sender, RoutedEventArgs e)
        {
            DropperModeRequested?.Invoke(true);
        }

        private void DropperButton_Unchecked(object sender, RoutedEventArgs e)
        {
            DropperModeRequested?.Invoke(false);
        }

        private Color CurrentColor()
        {
            _currentColor = ColorUtil.FromHSL(_h, _s, _l, _a);
            return _currentColor;
        }

        private void SetFromColor(Color c, bool freezeLadders = false)
        {
            _a = c.A;
            ColorUtil.ToHSL(c, out _h, out _s, out _l);
            _freezeLadders = freezeLadders;
            HueSlider.Hue = _h;
            HslSquare.Hue = _h;
            HslSquare.Saturation = _s;
            HslSquare.Lightness = _l;
            Push();
            _freezeLadders = false;
        }

        private void Push()
        {
            if (_suppress) return;
            var c = CurrentColor();

            _suppress = true;
            HBox.Value = Math.Round(_h);
            SBoxInt.Value = Math.Round(_s * 99.0);
            LBoxInt.Value = Math.Round(_l * 99.0);
            RBox.Value = c.R;
            GBox.Value = c.G;
            BBox.Value = c.B;
            AlphaSlider.Value = c.A;
            AlphaBox.Value = c.A;
            HexBox.Text = ColorUtil.ToHex(c);
            _suppress = false;

            UpdateLadders();
            _newBrush.Color = ColorUtil.MakeOpaque(c);
            SetLive?.Invoke(c);
        }

        private void PushFast()
        {
            if (_suppress) return;
            var c = CurrentColor();

            _suppress = true;
            HBox.Value = Math.Round(_h);
            SBoxInt.Value = Math.Round(_s * 99.0);
            LBoxInt.Value = Math.Round(_l * 99.0);
            RBox.Value = c.R;
            GBox.Value = c.G;
            BBox.Value = c.B;
            AlphaSlider.Value = c.A;
            AlphaBox.Value = c.A;
            HexBox.Text = ColorUtil.ToHex(c);
            _suppress = false;

            _newBrush.Color = ColorUtil.MakeOpaque(c);
            SetLive?.Invoke(c);
        }

        // HSL square events
        private void HslSquare_SVChanging(object? sender, (double S, double L) e)
        {
            _s = e.S;
            _l = e.L;
            PushFast();
        }

        private void HslSquare_SVChanged(object? sender, (double S, double L) e)
        {
            _s = e.S;
            _l = e.L;
            Push();
        }

        // Hue slider events
        private void HueSlider_HueChanging(object? sender, double newHue)
        {
            _h = newHue;
            HslSquare.Hue = _h;
            PushFast();
        }

        private void HueSlider_HueChanged(object? sender, double newHue)
        {
            _h = newHue;
            HslSquare.Hue = _h;
            Push();
        }

        // Numeric input handlers
        private void HBox_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            if (_suppress) return;
            _h = ColorUtil.WrapHue(e.NewValue);
            _suppress = true;
            HBox.Value = Math.Round(_h);
            _suppress = false;
            HueSlider.Hue = _h;
            HslSquare.Hue = _h;
            Push();
        }

        private void SBoxInt_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            if (_suppress) return;
            _s = Math.Clamp(e.NewValue, 0, 99) / 99.0;
            HslSquare.Saturation = _s;
            Push();
        }

        private void LBoxInt_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            if (_suppress) return;
            _l = Math.Clamp(e.NewValue, 0, 99) / 99.0;
            HslSquare.Lightness = _l;
            Push();
        }

        private void RBox_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            if (_suppress) return;
            _currentColor.R = (byte)e.NewValue;
            SetFromColor(_currentColor);
        }

        private void GBox_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            if (_suppress) return;
            _currentColor.G = (byte)e.NewValue;
            SetFromColor(_currentColor);
        }

        private void BBox_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            if (_suppress) return;
            _currentColor.B = (byte)e.NewValue;
            SetFromColor(_currentColor);
        }

        private void AlphaBox_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            if (_suppress) return;
            _a = (byte)Math.Clamp(e.NewValue, 0, 255);
            AlphaSlider.Value = _a;
            Push();
        }

        private void AlphaSlider_ValueChanged(object s, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppress) return;
            _a = (byte)e.NewValue;
            Push();
        }

        private void HexBox_TextChanged(object s, TextChangedEventArgs e)
        {
            if (_suppress) return;
            if (ColorUtil.TryParseHex(HexBox.Text, out var c))
                SetFromColor(c);
        }

        private void BuildLadders() => UpdateLadders();

        private void UpdateLadders()
        {
            var currentBgraOpaque = ColorUtil.MakeOpaqueBGRA(ColorUtil.ToBGRA(_currentColor));

            ShadeRow.ItemsSource = BuildSeries(-3, 3, i =>
                ColorUtil.FromHSL(_h, ColorUtil.Clamp01(_s + 0.05 * i), ColorUtil.Clamp01(_l - 0.12 * i), 255),
                currentBgraOpaque).Reverse().ToList();

            TintRow.ItemsSource = BuildSeries(-3, 3, i =>
                ColorUtil.FromHSL(_h, _s, ColorUtil.Clamp01(_l + 0.08 * i), 255),
                currentBgraOpaque).ToList();

            HueRow.ItemsSource = BuildSeries(-3, 3, i =>
                ColorUtil.FromHSL(ColorUtil.WrapHue(_h + 12 * i), _s, _l, 255),
                currentBgraOpaque).ToList();

            ToneRow.ItemsSource = BuildSeries(-3, 3, i =>
                ColorUtil.FromHSL(_h, ColorUtil.Clamp01(_s + 0.15 * i), _l, 255),
                currentBgraOpaque).ToList();

            ShadeRow.Invalidate();
            TintRow.Invalidate();
            HueRow.Invalidate();
            ToneRow.Invalidate();
        }

        private IList<Swatch> BuildSeries(int lSteps, int rSteps, Func<int, Color> map, uint currentBgra)
        {
            var list = new List<Swatch>(rSteps - lSteps + 1);
            int center = (list.Capacity - 1) / 2;
            for (int i = lSteps; i <= rSteps; i++)
            {
                var color = ColorUtil.ToBGRA(map(i));
                bool isMatch = color == currentBgra;
                bool isCenter = (i - lSteps) == center;
                list.Add(new Swatch(color, isCenter, isMatch));
            }
            return list;
        }

        private void LadderRow_SwatchClicked(object? sender, uint bgra)
        {
            var c = ColorUtil.ToColor(bgra);
            c.A = _a;
            SetFromColor(c, freezeLadders: true);
        }

        private void Close_Click(object s, RoutedEventArgs e) => Close();

        private void Okay_Click(object sender, RoutedEventArgs e)
        {
            Commit?.Invoke(_currentColor);
            Close();
        }
    }
}