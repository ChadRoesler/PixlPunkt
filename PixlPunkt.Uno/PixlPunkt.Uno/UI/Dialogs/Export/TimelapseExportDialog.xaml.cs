using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Export;

namespace PixlPunkt.Uno.UI.Dialogs.Export
{
    /// <summary>
    /// Dialog for configuring timelapse export from history.
    /// </summary>
    public sealed partial class TimelapseExportDialog : ContentDialog
    {
        private readonly CanvasDocument _document;
        private readonly int _totalHistorySteps;
        private bool _isInitialized;

        /// <summary>
        /// Gets the configured export settings.
        /// </summary>
        public TimelapseExportSettings Settings { get; } = new();

        /// <summary>
        /// Gets the selected export format tag (gif, mp4, png).
        /// </summary>
        public string SelectedFormat => (FormatComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "gif";

        /// <summary>
        /// Creates a new TimelapseExportDialog.
        /// </summary>
        /// <param name="document">The document to export from.</param>
        public TimelapseExportDialog(CanvasDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _totalHistorySteps = document.History.TotalCount;

            InitializeComponent();

            // Set initial values
            RangeEndBox.Maximum = _totalHistorySteps;
            RangeEndBox.Value = _totalHistorySteps;

            UpdateHistoryInfo();
            UpdateRangeInfo();
            UpdateOutputSize();
            UpdateEstimates();

            _isInitialized = true;
        }

        //////////////////////////////////////////////////////////////////
        // EVENT HANDLERS
        //////////////////////////////////////////////////////////////////

        private void Range_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!_isInitialized) return;

            // Clamp values
            if (RangeStartBox.Value > RangeEndBox.Value - 1)
            {
                RangeStartBox.Value = Math.Max(0, RangeEndBox.Value - 1);
            }

            UpdateRangeInfo();
            UpdateEstimates();
        }

        private void TimingMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            var tag = (TimingModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            TargetDurationPanel.Visibility = tag == "target" ? Visibility.Visible : Visibility.Collapsed;
            FixedPerStepPanel.Visibility = tag == "fixed" ? Visibility.Visible : Visibility.Collapsed;
            FixedFpsPanel.Visibility = tag == "fps" ? Visibility.Visible : Visibility.Collapsed;

            UpdateEstimates();
        }

        private void Transition_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            var tag = (TransitionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            TransitionFramesPanel.Visibility = tag == "dissolve" ? Visibility.Visible : Visibility.Collapsed;

            UpdateEstimates();
        }

        private void Settings_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            UpdateOutputSize();
            UpdateEstimates();

            // Update final hold panel visibility
            if (FinalHoldPanel != null)
            {
                FinalHoldPanel.Visibility = HoldFinalCheckBox?.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void Settings_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!_isInitialized) return;

            UpdateOutputSize();
            UpdateEstimates();

            // Update final hold panel visibility
            if (FinalHoldPanel != null)
            {
                FinalHoldPanel.Visibility = HoldFinalCheckBox?.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        //////////////////////////////////////////////////////////////////
        // UI UPDATES
        //////////////////////////////////////////////////////////////////

        private void UpdateHistoryInfo()
        {
            if (HistoryInfoText == null) return;
            HistoryInfoText.Text = $"{_totalHistorySteps} history steps available";
        }

        private void UpdateRangeInfo()
        {
            if (RangeInfoText == null) return;

            int steps = GetRangeSteps();
            RangeInfoText.Text = $"Will render {steps} step{(steps != 1 ? "s" : "")}";
        }

        private void UpdateOutputSize()
        {
            if (OutputSizeText == null) return;

            int scale = (int)(ScaleNumberBox?.Value ?? 1);
            int w = _document.PixelWidth * scale;
            int h = _document.PixelHeight * scale;
            OutputSizeText.Text = $"{w}x{h} pixels";
        }

        private void UpdateEstimates()
        {
            if (EstimatedDurationText == null || EstimatedFramesText == null) return;

            // Build settings from current UI
            var settings = BuildSettings();
            int steps = GetRangeSteps();

            double duration = settings.EstimateDurationSeconds(steps);
            int frameDurationMs = settings.CalculateFrameDurationMs(steps);
            int fps = frameDurationMs > 0 ? 1000 / frameDurationMs : 12;

            // Calculate total frames including transitions
            int totalFrames = steps;
            if (settings.Transition == TimelapseExportSettings.TransitionMode.Dissolve)
            {
                totalFrames += (steps - 1) * settings.TransitionFrames;
            }

            // Format duration
            string durationStr;
            if (duration < 60)
            {
                durationStr = $"~{duration:F1} seconds";
            }
            else
            {
                int mins = (int)(duration / 60);
                int secs = (int)(duration % 60);
                durationStr = $"~{mins}m {secs}s";
            }

            EstimatedDurationText.Text = $"Duration: {durationStr}";
            EstimatedFramesText.Text = $"Frames: {totalFrames} frames @ ~{fps} fps";
        }

        //////////////////////////////////////////////////////////////////
        // HELPERS
        //////////////////////////////////////////////////////////////////

        private int GetRangeSteps()
        {
            int start = (int)(RangeStartBox?.Value ?? 0);
            int end = (int)(RangeEndBox?.Value ?? _totalHistorySteps);
            return Math.Max(1, end - start);
        }

        /// <summary>
        /// Builds the export settings from current UI values.
        /// </summary>
        public TimelapseExportSettings BuildSettings()
        {
            var settings = new TimelapseExportSettings
            {
                RangeStart = (int)(RangeStartBox?.Value ?? 0),
                RangeEnd = (int)(RangeEndBox?.Value ?? _totalHistorySteps),
                Scale = (int)(ScaleNumberBox?.Value ?? 1),
                HoldFinalFrame = HoldFinalCheckBox?.IsChecked ?? true,
                FinalFrameHoldMs = (int)(FinalHoldBox?.Value ?? 2000)
            };

            // Timing mode
            var timingTag = (TimingModeCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "target";
            settings.Timing = timingTag switch
            {
                "fixed" => TimelapseExportSettings.TimingMode.FixedPerStep,
                "fps" => TimelapseExportSettings.TimingMode.FixedFps,
                _ => TimelapseExportSettings.TimingMode.TargetDuration
            };

            settings.TargetDurationSeconds = (int)(TargetDurationBox?.Value ?? 30);
            settings.MillisecondsPerStep = (int)(MillisecondsPerStepBox?.Value ?? 100);
            settings.FixedFps = (int)(FixedFpsBox?.Value ?? 12);

            // Transition mode
            var transitionTag = (TransitionCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "cut";
            settings.Transition = transitionTag switch
            {
                "dissolve" => TimelapseExportSettings.TransitionMode.Dissolve,
                "flash" => TimelapseExportSettings.TransitionMode.Flash,
                _ => TimelapseExportSettings.TransitionMode.Cut
            };

            settings.TransitionFrames = (int)(TransitionFramesBox?.Value ?? 3);

            return settings;
        }
    }
}
