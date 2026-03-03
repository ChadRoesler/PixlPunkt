using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Voxel.Tools;
using PixlPunkt.PluginSdk.Settings.Options;
using PixlPunkt.UI.Tools;

namespace PixlPunkt.UI.Voxel.Tools
{
    /// <summary>
    /// Minimal voxel tool options host using the shared <see cref="ToolOptionFactory"/>.
    /// </summary>
    public sealed class VoxelToolOptionsBar : UserControl
    {
        private readonly StackPanel _root;
        private readonly TextBlock _title;
        private readonly StackPanel _optionsPanel;
        private readonly Dictionary<string, (Slider slider, NumberBox? numberBox)> _sliderControls = new();
        private readonly Dictionary<string, NumberBox> _numberBoxControls = new();
        private VoxelToolState? _toolState;
        private bool _dynamicEditInProgress;
        private bool _suppressSync;
        private string? _currentOptionsToolId;
        private int _lastOptionCount = -1;

        public event Action? EditStarted;
        public event Action? EditEnded;

        public VoxelToolOptionsBar()
        {
            _title = new TextBlock
            {
                FontSize = 12,
                Opacity = 0.8
            };

            _optionsPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 6
            };

            _root = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    _title,
                    _optionsPanel
                }
            };

            Content = _root;
            Loaded += (_, __) => Rebuild();
        }

        public VoxelToolState? ToolState
        {
            get => _toolState;
            set
            {
                if (ReferenceEquals(_toolState, value))
                    return;

                Unwire(_toolState);
                _toolState = value;
                Wire(_toolState);
                Rebuild();
            }
        }

        private void Wire(VoxelToolState? state)
        {
            if (state == null) return;
            state.ActiveToolChanged += OnToolStateChanged;
            state.OptionsChanged += OnOptionsChanged;
            state.Registry.ToolsChanged += OnRegistryToolsChanged;
        }

        private void Unwire(VoxelToolState? state)
        {
            if (state == null) return;
            state.ActiveToolChanged -= OnToolStateChanged;
            state.OptionsChanged -= OnOptionsChanged;
            state.Registry.ToolsChanged -= OnRegistryToolsChanged;
        }

        private void OnToolStateChanged(string? _) => Rebuild();
        private void OnOptionsChanged()
        {
            if (_dynamicEditInProgress || _suppressSync || _toolState == null)
                return;

            var settings = _toolState.ActiveSettings;
            var options = settings?.GetOptions()?.OrderBy(o => o.Order).ToList();
            if (options == null)
            {
                Rebuild();
                return;
            }

            if (options.Count != _lastOptionCount)
            {
                Rebuild();
                return;
            }

            _suppressSync = true;
            try
            {
                foreach (var option in options)
                {
                    if (option is SliderOption slider &&
                        _sliderControls.TryGetValue(slider.Id, out var sliderControls))
                    {
                        if (!NearlyEqual(sliderControls.slider.Value, slider.Value))
                            sliderControls.slider.Value = slider.Value;

                        if (sliderControls.numberBox != null &&
                            !NearlyEqual(sliderControls.numberBox.Value, slider.Value))
                        {
                            sliderControls.numberBox.Value = slider.Value;
                        }
                    }

                    if (option is NumberBoxOption numberBox &&
                        _numberBoxControls.TryGetValue(numberBox.Id, out var numberBoxControl))
                    {
                        if (!NearlyEqual(numberBoxControl.Value, numberBox.Value))
                            numberBoxControl.Value = numberBox.Value;
                    }
                }
            }
            finally
            {
                _suppressSync = false;
            }
        }
        private void OnRegistryToolsChanged() => Rebuild();

        private void Rebuild()
        {
            _sliderControls.Clear();
            _numberBoxControls.Clear();
            _optionsPanel.Children.Clear();

            var state = _toolState;
            if (state?.ActiveRegistration == null)
            {
                _title.Text = "Tool Options";
                _currentOptionsToolId = null;
                _lastOptionCount = 0;
                _optionsPanel.Children.Add(new TextBlock { Text = "No active voxel tool", Opacity = 0.6 });
                return;
            }

            string? activeToolId = state.ActiveToolId;
            if (!string.Equals(_currentOptionsToolId, activeToolId, StringComparison.Ordinal))
            {
                _currentOptionsToolId = activeToolId;
            }

            _title.Text = $"Options - {state.ActiveRegistration.DisplayName}";
            var settings = state.ActiveSettings;
            var options = settings?.GetOptions()?.OrderBy(o => o.Order).ToList();
            _lastOptionCount = options?.Count ?? 0;

            if (options == null || options.Count == 0)
            {
                _optionsPanel.Children.Add(new TextBlock { Text = "No options", Opacity = 0.6 });
                return;
            }

            foreach (var option in options)
            {
                var control = ToolOptionFactory.CreateControl(
                    option,
                    onEditStart: HandleEditStarted,
                    onEditEnd: HandleEditEnded);
                if (control != null)
                {
                    _optionsPanel.Children.Add(control);

                    if (option is SliderOption sliderOpt && control is StackPanel sliderPanel)
                    {
                        Slider? slider = null;
                        NumberBox? numberBox = null;
                        foreach (var child in sliderPanel.Children)
                        {
                            if (child is Slider s) slider = s;
                            if (child is NumberBox nb) numberBox = nb;
                        }

                        if (slider != null)
                            _sliderControls[sliderOpt.Id] = (slider, numberBox);
                    }

                    if (option is NumberBoxOption numberBoxOpt && control is StackPanel numberPanel)
                    {
                        foreach (var child in numberPanel.Children)
                        {
                            if (child is NumberBox nb)
                            {
                                _numberBoxControls[numberBoxOpt.Id] = nb;
                                break;
                            }
                        }
                    }
                }
            }

            if (_optionsPanel.Children.Count == 0)
            {
                _optionsPanel.Children.Add(new TextBlock { Text = "No renderable options", Opacity = 0.6 });
            }
        }

        private void HandleEditStarted()
        {
            _dynamicEditInProgress = true;
            EditStarted?.Invoke();
        }

        private void HandleEditEnded()
        {
            _dynamicEditInProgress = false;
            EditEnded?.Invoke();
        }

        private static bool NearlyEqual(double a, double b)
            => Math.Abs(a - b) <= 0.0001;
    }
}
