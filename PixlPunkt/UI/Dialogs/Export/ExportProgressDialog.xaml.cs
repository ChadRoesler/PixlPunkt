using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PixlPunkt.UI.Dialogs.Export
{
    /// <summary>
    /// Dialog that displays export progress with cancel support.
    /// </summary>
    public sealed partial class ExportProgressDialog : ContentDialog
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Stopwatch _stopwatch = new();
        private readonly DispatcherTimer _timer;
        private bool _isComplete;
        private bool _isCancelled;
        private TaskCompletionSource<bool>? _dialogShownTcs;

        /// <summary>
        /// Gets the cancellation token for the export operation.
        /// </summary>
        public CancellationToken CancellationToken => _cts.Token;

        /// <summary>
        /// Gets whether the operation was cancelled by the user.
        /// </summary>
        public bool WasCancelled => _isCancelled;

        /// <summary>
        /// Creates a new ExportProgressDialog.
        /// </summary>
        public ExportProgressDialog()
        {
            InitializeComponent();

            // Timer to update elapsed time display
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timer.Tick += Timer_Tick;

            // Hook the Opened event to signal when dialog is visible
            Opened += OnDialogOpened;
        }

        private void OnDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            // Signal that the dialog is now visible
            _dialogShownTcs?.TrySetResult(true);
        }

        /// <summary>
        /// Waits until the dialog is fully shown on screen.
        /// Call this before starting the export task to ensure progress updates are visible.
        /// </summary>
        public Task WaitUntilShownAsync()
        {
            _dialogShownTcs = new TaskCompletionSource<bool>();
            return _dialogShownTcs.Task;
        }

        /// <summary>
        /// Starts the progress tracking timer.
        /// </summary>
        public void Start()
        {
            _stopwatch.Start();
            _timer.Start();
        }

        /// <summary>
        /// Updates the progress display.
        /// </summary>
        /// <param name="progress">Progress value from 0.0 to 1.0.</param>
        /// <param name="status">Optional status message.</param>
        /// <param name="details">Optional detail text (e.g., "Frame 45 of 120").</param>
        public void UpdateProgress(double progress, string? status = null, string? details = null)
        {
            if (_isComplete) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                int percent = (int)(progress * 100);
                ProgressBarCtrl.Value = percent;
                PercentText.Text = $"{percent}%";

                if (!string.IsNullOrEmpty(status))
                {
                    StatusText.Text = status;
                }

                if (details != null)
                {
                    DetailsText.Text = details;
                }
            });
        }

        /// <summary>
        /// Marks the export as complete, allowing the dialog to close.
        /// </summary>
        /// <param name="success">Whether the export was successful.</param>
        public void Complete(bool success = true)
        {
            _isComplete = true;
            _timer.Stop();
            _stopwatch.Stop();

            DispatcherQueue.TryEnqueue(() =>
            {
                if (success)
                {
                    ProgressBarCtrl.Value = 100;
                    PercentText.Text = "100%";
                    StatusText.Text = "Export complete!";
                }

                // Auto-close on success
                if (success)
                {
                    Hide();
                }
            });
        }

        /// <summary>
        /// Creates a Progress&lt;double&gt; that updates this dialog.
        /// </summary>
        /// <param name="statusFormat">Optional format string for status (e.g., "Rendering frames... {0:P0}").</param>
        public IProgress<double> CreateProgressReporter(string? statusFormat = null)
        {
            return new Progress<double>(p =>
            {
                string? status = statusFormat != null ? string.Format(statusFormat, p) : null;
                UpdateProgress(p, status);
            });
        }

        /// <summary>
        /// Creates a Progress&lt;(double, string)&gt; that updates progress and details.
        /// </summary>
        public IProgress<(double progress, string details)> CreateDetailedProgressReporter(string status)
        {
            return new Progress<(double progress, string details)>(p =>
            {
                UpdateProgress(p.progress, status, p.details);
            });
        }

        private void Timer_Tick(object? sender, object e)
        {
            var elapsed = _stopwatch.Elapsed;
            string timeStr = elapsed.TotalMinutes >= 1
                ? $"Elapsed: {elapsed.Minutes}m {elapsed.Seconds}s"
                : $"Elapsed: {elapsed.Seconds}s";

            TimeText.Text = timeStr;
        }

        private void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            // If not complete and user clicked Cancel button, trigger cancellation
            if (!_isComplete && args.Result == ContentDialogResult.Secondary)
            {
                _isCancelled = true;
                _cts.Cancel();
            }
            // If not complete and trying to close any other way, prevent it
            else if (!_isComplete)
            {
                // Allow cancellation via the Cancel button
                if (args.Result == ContentDialogResult.None)
                {
                    _isCancelled = true;
                    _cts.Cancel();
                }
            }

            _timer.Stop();
        }
    }
}
