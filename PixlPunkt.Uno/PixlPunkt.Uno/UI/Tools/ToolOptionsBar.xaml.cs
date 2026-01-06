using System.Collections.Generic;
using System.Linq;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Tools;

namespace PixlPunkt.Uno.UI.Tools
{
    /// <summary>
    /// Hosts per-tool options dynamically generated from each tool's GetOptions() descriptors.
    /// Uses ToolOptionFactory to create WinUI controls from IToolOption descriptors.
    /// </summary>
    public sealed partial class ToolOptionsBar : UserControl
    {
        // ────────────────────────────────────────────────────────────────────
        // FIELDS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Prevents updating controls while user is actively editing.</summary>
        private bool _dynamicEditInProgress;

        /// <summary>Suppresses value sync during programmatic updates.</summary>
        private bool _suppressSync;

        /// <summary>The bound tool state instance.</summary>
        private ToolState? _toolState;

        /// <summary>The tool ID for which options are currently built.</summary>
        private string? _currentOptionsToolId;

        /// <summary>Maps option IDs to their slider controls for value updates.</summary>
        private readonly Dictionary<string, (Slider slider, NumberBox? numberBox)> _sliderControls = new();

        /// <summary>Maps option IDs to their standalone NumberBox controls for value updates.</summary>
        private readonly Dictionary<string, NumberBox> _numberBoxControls = new();

        /// <summary>Tracks the last known option count to detect structural changes.</summary>
        private int _lastOptionCount;

        /// <summary>Tracks the last known selection active state to detect structural changes.</summary>
        private bool _lastSelectionActive;

        // ────────────────────────────────────────────────────────────────────
        // CTOR
        // ────────────────────────────────────────────────────────────────────

        public ToolOptionsBar()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        // ────────────────────────────────────────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Handles control unload by cleaning up event subscriptions to prevent memory leaks.
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from tool state events
            if (_toolState != null)
            {
                _toolState.ActiveToolIdChanged -= OnToolIdChanged;
                _toolState.OptionsChanged -= OnOptionsChanged;
                _toolState = null;
            }

            // Clear control references
            _sliderControls.Clear();
            _numberBoxControls.Clear();
            DynamicOptionsPanel.Children.Clear();
        }

        // ────────────────────────────────────────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Binds the UI to a <see cref="ToolState"/> instance and wires state-change handlers.
        /// </summary>
        public void BindToolState(ToolState tool)
        {
            // Unsubscribe previous
            if (_toolState != null)
            {
                _toolState.ActiveToolIdChanged -= OnToolIdChanged;
                _toolState.OptionsChanged -= OnOptionsChanged;
            }

            _toolState = tool;

            // Subscribe current
            _toolState.ActiveToolIdChanged += OnToolIdChanged;
            _toolState.OptionsChanged += OnOptionsChanged;

            // Initial UI setup - force rebuild
            _currentOptionsToolId = null;
            _lastOptionCount = -1;
            _lastSelectionActive = false;
            OnToolIdChanged(_toolState.ActiveToolId);
        }

        // ────────────────────────────────────────────────────────────────────
        // TOOL CHANGE HANDLING
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Handles tool changes by updating the icon and rebuilding dynamic options.
        /// </summary>
        private void OnToolIdChanged(string toolId)
        {
            UpdateToolIconFromSettings(toolId);

            // Only rebuild if tool actually changed
            if (_currentOptionsToolId != toolId)
            {
                _currentOptionsToolId = toolId;
                _lastOptionCount = -1; // Force rebuild
                _lastSelectionActive = false;
                RebuildDynamicOptions();
            }
        }

        /// <summary>
        /// Handles options changes by updating control values without rebuilding,
        /// unless the structure of options has changed (e.g., selection became active).
        /// </summary>
        private void OnOptionsChanged()
        {
            // Skip if user is currently editing via UI
            if (_dynamicEditInProgress || _suppressSync) return;
            if (_toolState == null) return;

            var settings = _toolState.ActiveSettings;
            if (settings == null) return;

            // Check if selection state changed (which affects option structure for selection tools)
            bool currentSelectionActive = _toolState.Selection.Active;

            // Get current options to check if structure changed
            var options = settings.GetOptions().ToList();
            int currentOptionCount = options.Count;

            // Rebuild if option count changed or selection state changed
            if (currentOptionCount != _lastOptionCount || currentSelectionActive != _lastSelectionActive)
            {
                _lastOptionCount = currentOptionCount;
                _lastSelectionActive = currentSelectionActive;
                RebuildDynamicOptions();
                return;
            }

            // Update control values from current settings
            _suppressSync = true;
            try
            {
                foreach (var option in options)
                {
                    // Update SliderOption controls
                    if (option is SliderOption slider && _sliderControls.TryGetValue(slider.Id, out var sliderControls))
                    {
                        if (sliderControls.slider.Value != slider.Value)
                        {
                            sliderControls.slider.Value = slider.Value;
                        }
                        if (sliderControls.numberBox != null && sliderControls.numberBox.Value != slider.Value)
                        {
                            sliderControls.numberBox.Value = slider.Value;
                        }
                    }

                    // Update NumberBoxOption controls
                    if (option is NumberBoxOption numBoxOpt && _numberBoxControls.TryGetValue(numBoxOpt.Id, out var numberBox))
                    {
                        if (numberBox.Value != numBoxOpt.Value)
                        {
                            numberBox.Value = numBoxOpt.Value;
                        }
                    }
                }
            }
            finally
            {
                _suppressSync = false;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // DYNAMIC OPTIONS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the dynamic options panel from the current tool's GetOptions() descriptors.
        /// </summary>
        private void RebuildDynamicOptions()
        {
            if (_toolState == null) return;

            DynamicOptionsPanel.Children.Clear();
            _sliderControls.Clear();
            _numberBoxControls.Clear();

            var settings = _toolState.ActiveSettings;
            if (settings == null)
            {
                // Tool has no settings (e.g., Pan, Magnifier, Dropper)
                DynamicOptionsPanel.Visibility = Visibility.Collapsed;
                _lastOptionCount = 0;
                return;
            }

            // Get options and sort by order
            var options = settings.GetOptions().OrderBy(o => o.Order).ToList();
            _lastOptionCount = options.Count;
            _lastSelectionActive = _toolState.Selection.Active;

            if (options.Count == 0)
            {
                DynamicOptionsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // Create controls for each option, wrapping callbacks to set edit flag
            foreach (var option in options)
            {
                var control = ToolOptionFactory.CreateControl(
                    option,
                    onEditStart: () => _dynamicEditInProgress = true,
                    onEditEnd: () => _dynamicEditInProgress = false);

                if (control != null)
                {
                    DynamicOptionsPanel.Children.Add(control);

                    // Track slider controls for value updates
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
                        {
                            _sliderControls[sliderOpt.Id] = (slider, numberBox);
                        }
                    }

                    // Track standalone NumberBox controls for value updates
                    if (option is NumberBoxOption numBoxOpt && control is StackPanel numBoxPanel)
                    {
                        foreach (var child in numBoxPanel.Children)
                        {
                            if (child is NumberBox nb)
                            {
                                _numberBoxControls[numBoxOpt.Id] = nb;
                                break;
                            }
                        }
                    }
                }
            }

            DynamicOptionsPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Updates the tool icon from the current tool's settings.
        /// </summary>
        private void UpdateToolIconFromSettings(string toolId)
        {
            if (_toolState == null) return;

            var settings = _toolState.GetSettingsForToolId(toolId);
            if (settings != null)
            {
                ToolSymbol.Icon = settings.Icon;
                ToolTipService.SetToolTip(ToolIconBorder, settings.TooltipText);
            }
            else
            {
                // Fallback for tools without settings (Pan, Zoom, Dropper)
                ToolSymbol.Icon = toolId switch
                {
                    ToolIds.Pan => Icon.HandLeft,
                    ToolIds.Zoom => Icon.ZoomIn,
                    ToolIds.Dropper => Icon.Eyedropper,
                    _ => Icon.Apps
                };

                // Extract display name from tool ID (last segment)
                var displayName = toolId.Split('.').LastOrDefault() ?? toolId;
                ToolTipService.SetToolTip(ToolIconBorder, char.ToUpper(displayName[0]) + displayName[1..]);
            }
        }
    }
}