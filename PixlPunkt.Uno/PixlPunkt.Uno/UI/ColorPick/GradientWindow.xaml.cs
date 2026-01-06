using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Uno.Core.Coloring.Helpers;
using Windows.UI;

namespace PixlPunkt.Uno.UI.ColorPick
{
    public sealed partial class GradientWindow : Window, INotifyPropertyChanged
    {
        private bool _editingStart = true;

        private double _hS;
        private double _sS;
        private double _lS;
        private byte _aS = 255;

        private double _hE;
        private double _sE;
        private double _lE;
        private byte _aE = 255;

        private int _steps = 7;

        private bool _suppress;
        private bool _toggling;

        private SolidColorBrush _startBrush;
        private SolidColorBrush _endBrush;

        public SolidColorBrush StartBrush
        {
            get => _startBrush;
            set { _startBrush = value; OnPropertyChanged(); }
        }

        public SolidColorBrush EndBrush
        {
            get => _endBrush;
            set { _endBrush = value; OnPropertyChanged(); }
        }

        public bool IsEditingStart => _editingStart;
        public bool IsEditingEnd => !_editingStart;
        public ObservableCollection<uint> PreviewItems { get; } = new();

        public Action<IReadOnlyList<uint>>? Commit { get; set; }
        public Func<uint>? GetStart { get; set; }
        public Func<uint>? GetEnd { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Event raised when the dropper button is toggled on, requesting canvas pick mode.
        /// The host should enable canvas click-to-sample and call <see cref="SetPickedColor"/> when a color is sampled.
        /// </summary>
        public event Action<bool>? DropperModeRequested;

        public GradientWindow()
        {
            InitializeComponent();

            // Set DataContext for bindings
            if (Content is FrameworkElement root)
            {
                root.DataContext = this;
            }

            // Don't seed colors here - the delegates aren't set yet
            // We'll initialize in the deferred queue once delegates are wired

            _startBrush = new SolidColorBrush(Colors.Black);
            _endBrush = new SolidColorBrush(Colors.White);

            TriFalloff.Start_out = new(-0.60f, 0.00f);
            TriFalloff.Mid_left = new(-0.20f, 0.85f);
            TriFalloff.Mid_right = new(0.20f, 0.85f);
            TriFalloff.End_in = new(0.60f, 0.00f);
            TriFalloff.CurveChanged += (_, __) => BuildPreview();

            // Wire reusable controls
            HueSlider.HueChanging += HueSlider_HueChanging;
            HueSlider.HueChanged += HueSlider_HueChanged;

            HslSquare.SVChanging += HslSquare_SVChanging;
            HslSquare.SVChanged += HslSquare_SVChanged;

            // Defer final sync and preview so XAML bindings AND delegate properties are ready
            DispatcherQueue.TryEnqueue(() =>
            {
                // Now GetStart/GetEnd should be set by the caller
                var start = GetStart?.Invoke() ?? 0xFF000000u;
                var end = GetEnd?.Invoke() ?? 0xFFFFFFFFu;

                var cs = ColorUtil.ToColor(start);
                var ce = ColorUtil.ToColor(end);
                ColorUtil.ToHSL(cs, out _hS, out _sS, out _lS);
                ColorUtil.ToHSL(ce, out _hE, out _sE, out _lE);

                _startBrush.Color = cs;
                _endBrush.Color = ce;

                _editingStart = true;
                NotifySelection(); // updates inputs + controls (HueSlider/HslSquare)
                BuildPreview();
            });
        }

        /// <summary>
        /// Called by the host when a color is picked from the canvas via the dropper mode.
        /// Sets the currently selected (Start or End) color and turns off dropper mode.
        /// </summary>
        /// <param name="bgra">The picked color in BGRA format.</param>
        public void SetPickedColor(uint bgra)
        {
            var c = ColorUtil.ToColor(bgra);
            ColorUtil.ToHSL(c, out var h, out var s, out var l);
            SetSel(h, s, l, 255);
            SyncUIFromSelection();
            BuildPreview();

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

        // Selection helpers
        private (double H, double S, double L, byte A) GetSel() =>
            _editingStart ? (_hS, _sS, _lS, _aS) : (_hE, _sE, _lE, _aE);

        private void SetSel(double h, double s, double l, byte a)
        {
            if (_editingStart)
            {
                _hS = h; _sS = s; _lS = l; _aS = a;
                _startBrush.Color = ColorUtil.FromHSL(h, s, l, a);
            }
            else
            {
                _hE = h; _sE = s; _lE = l; _aE = a;
                _endBrush.Color = ColorUtil.FromHSL(h, s, l, a);
            }
        }

        private Color SelColor()
        {
            var (h, s, l, a) = GetSel();
            return ColorUtil.FromHSL(h, s, l, a);
        }

        private void NotifySelection()
        {
            // Notify property changes for bindings
            OnPropertyChanged(nameof(IsEditingStart));
            OnPropertyChanged(nameof(IsEditingEnd));
            SyncUIFromSelection();
        }

        private void SyncUIFromSelection()
        {
            var c = SelColor();
            ColorUtil.ToHSL(c, out var h, out var s, out var l);

            _suppress = true;
            HBox.Value = Math.Round(h);
            SBoxInt.Value = Math.Round(s * 99.0);
            LBoxInt.Value = Math.Round(l * 99.0);
            RBox.Value = c.R;
            GBox.Value = c.G;
            BBox.Value = c.B;
            HexBox.Text = ColorUtil.ToHex(c); // #RRGGBB
            _suppress = false;

            // Keep controls in sync with current selection
            HueSlider.Hue = h;
            HslSquare.Hue = h;
            HslSquare.Saturation = s;
            HslSquare.Lightness = l;
        }

        // Preview generation (unchanged)
        private void BuildPreview()
        {
            var items = new List<uint>(_steps);
            uint? last = null;

            for (int i = 0; i < _steps; i++)
            {
                double t = (_steps == 1) ? 0 : (double)i / (_steps - 1);

                double p;
                if (t <= 0.5)
                {
                    double u = (t <= 0) ? 0 : t / 0.5;
                    double y = TriFalloff?.EvaluateLeft01(u) ?? u;
                    p = 0.5 * y;
                }
                else
                {
                    double u = (t - 0.5) / 0.5;
                    double y = TriFalloff?.EvaluateRight01(u) ?? u;
                    p = 0.5 + 0.5 * (1.0 - y);
                }

                double h = ColorUtil.LerpHueShortest(_hS, _hE, p);
                double s = ColorUtil.Lerp(_sS, _sE, p);
                double l = ColorUtil.Lerp(_lS, _lE, p);

                var u32 = ColorUtil.ToBGRA(ColorUtil.FromHSL(h, s, l, 255));
                if (last.HasValue && last.Value == u32) continue;
                last = u32;
                items.Add(u32);
            }

            PreviewItems.Clear();
            foreach (var u32 in items) PreviewItems.Add(u32);
        }

        // HSL square events
        private void HslSquare_SVChanging(object? sender, (double S, double L) e)
        {
            var (h, _, _, a) = GetSel();
            SetSel(h, e.S, e.L, a);

            // Fast UI updates for S/L boxes and RGB/Hex to reflect live drag
            _suppress = true;
            SBoxInt.Value = Math.Round(e.S * 99.0);
            LBoxInt.Value = Math.Round(e.L * 99.0);
            var c = SelColor();
            RBox.Value = c.R;
            GBox.Value = c.G;
            BBox.Value = c.B;
            HexBox.Text = ColorUtil.ToHex(c);
            _suppress = false;

            BuildPreview();
        }

        private void HslSquare_SVChanged(object? sender, (double S, double L) e)
        {
            var (h, _, _, a) = GetSel();
            SetSel(h, e.S, e.L, a);
            SyncUIFromSelection();
            BuildPreview();
        }

        // Hue slider events
        private void HueSlider_HueChanging(object? sender, double newHue)
        {
            var (_, s, l, a) = GetSel();
            SetSel(newHue, s, l, a);

            _suppress = true;
            HBox.Value = Math.Round(newHue);
            var c = SelColor();
            RBox.Value = c.R;
            GBox.Value = c.G;
            BBox.Value = c.B;
            HexBox.Text = ColorUtil.ToHex(c);
            _suppress = false;

            // Keep square hue aligned during drag
            HslSquare.Hue = newHue;

            BuildPreview();
        }

        private void HueSlider_HueChanged(object? sender, double newHue)
        {
            var (_, s, l, a) = GetSel();
            SetSel(newHue, s, l, a);
            SyncUIFromSelection();
            BuildPreview();
        }

        // NumberBox handlers
        private void HBox_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            if (_suppress) return;
            var (h, S, L, a) = GetSel();
            h = ((e.NewValue % 360) + 360) % 360;
            _suppress = true;
            HBox.Value = Math.Round(h);
            _suppress = false;
            SetSel(h, S, L, a);

            // Sync controls
            HueSlider.Hue = h;
            HslSquare.Hue = h;

            SyncUIFromSelection();
            BuildPreview();
        }

        private void SBoxInt_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            if (_suppress) return;
            var (h, S, L, a) = GetSel();
            S = Math.Clamp(e.NewValue / 99.0, 0, 1);
            SetSel(h, S, L, a);

            // Sync control
            HslSquare.Saturation = S;

            SyncUIFromSelection();
            BuildPreview();
        }

        private void LBoxInt_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            if (_suppress) return;
            var (h, S, L, a) = GetSel();
            L = Math.Clamp(e.NewValue / 99.0, 0, 1);
            SetSel(h, S, L, a);

            // Sync control
            HslSquare.Lightness = L;

            SyncUIFromSelection();
            BuildPreview();
        }

        private void RBox_ValueChanged(object s, NumberBoxValueChangedEventArgs e) => FromRgbBoxes();
        private void GBox_ValueChanged(object s, NumberBoxValueChangedEventArgs e) => FromRgbBoxes();
        private void BBox_ValueChanged(object s, NumberBoxValueChangedEventArgs e) => FromRgbBoxes();

        private void FromRgbBoxes()
        {
            if (_suppress) return;
            byte r = (byte)Math.Clamp(RBox.Value, 0, 255);
            byte g = (byte)Math.Clamp(GBox.Value, 0, 255);
            byte b = (byte)Math.Clamp(BBox.Value, 0, 255);
            var c = Color.FromArgb(255, r, g, b);
            ColorUtil.ToHSL(c, out var h, out var s, out var l);
            SetSel(h, s, l, 255);

            // Sync controls from converted HSL
            _suppress = true;
            HBox.Value = Math.Round(h);
            SBoxInt.Value = Math.Round(s * 99.0);
            LBoxInt.Value = Math.Round(l * 99.0);
            HexBox.Text = ColorUtil.ToHex(c);

            HueSlider.Hue = h;
            HslSquare.Hue = h;
            HslSquare.Saturation = s;
            HslSquare.Lightness = l;
            _suppress = false;

            BuildPreview();
        }

        private void HexBox_TextChanged(object s, TextChangedEventArgs e)
        {
            if (_suppress) return;
            var txt = HexBox.Text.Trim();
            if (ColorUtil.TryParseHex(txt, out var color))
            {
                ColorUtil.ToHSL(color, out var h, out var S, out var L);
                SetSel(h, S, L, 255);

                _suppress = true;
                HBox.Value = Math.Round(h);
                SBoxInt.Value = Math.Round(S * 99.0);
                LBoxInt.Value = Math.Round(L * 99.0);
                _suppress = false;

                HueSlider.Hue = h;
                HslSquare.Hue = h;
                HslSquare.Saturation = S;
                HslSquare.Lightness = L;

                BuildPreview();
            }
        }

        private void StepsBox_ValueChanged(object s, NumberBoxValueChangedEventArgs e)
        {
            _steps = Math.Max(3, (int)Math.Round(e.NewValue));
            StepsBox.Value = _steps;
            BuildPreview();
        }

        private void StartChip_Tapped(object s, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            _editingStart = true;
            NotifySelection();
        }

        private void EndChip_Tapped(object s, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            _editingStart = false;
            NotifySelection();
        }

        private void Swap_Click(object s, RoutedEventArgs e)
        {
            (_hS, _sS, _lS, _aS, _hE, _sE, _lE, _aE) = (_hE, _sE, _lE, _aE, _hS, _sS, _lS, _aS);

            var cS = ColorUtil.FromHSL(_hS, _sS, _lS, _aS);
            var cE = ColorUtil.FromHSL(_hE, _sE, _lE, _aE);
            _startBrush.Color = cS;
            _endBrush.Color = cE;

            NotifySelection();
            BuildPreview();
        }

        private void AddToPalette_Click(object s, RoutedEventArgs e)
        {
            Commit?.Invoke((IReadOnlyList<uint>)PreviewItems);
            Close();
        }

        private void Close_Click(object s, RoutedEventArgs e) => Close();
    }
}
