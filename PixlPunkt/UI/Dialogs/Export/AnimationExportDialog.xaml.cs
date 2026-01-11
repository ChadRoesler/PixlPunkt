using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Export;
using PixlPunkt.Core.Imaging;
using PixlPunkt.UI.Animation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PixlPunkt.UI.Dialogs.Export
{
    /// <summary>
    /// Dialog for configuring animation export settings.
    /// Supports GIF, video, and image sequence export.
    /// </summary>
    public sealed partial class AnimationExportDialog : ContentDialog
    {
        //////////////////////////////////////////////////////////////////
        // FIELDS
        //////////////////////////////////////////////////////////////////

        private readonly CanvasDocument _document;
        private readonly TileAnimationReel? _selectedReel;
        private readonly AnimationMode _preferredMode;
        private List<AnimationExportService.RenderedFrame>? _previewFrames;
        private int _previewFrameIndex;
        private DispatcherTimer? _previewTimer;
        private bool _isInitialized;
        private bool _isClosing;

        //////////////////////////////////////////////////////////////////
        // PROPERTIES
        //////////////////////////////////////////////////////////////////

        /// <summary>Gets whether to export tile animation (true) or canvas animation (false).</summary>
        public bool ExportTileAnimation => TileAnimationRadio?.IsChecked == true;

        /// <summary>Gets the selected export format tag.</summary>
        public string SelectedFormat => (FormatComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "gif";

        /// <summary>Gets the pixel scale factor.</summary>
        public new int Scale => (int)(ScaleNumberBox?.Value ?? 1);

        /// <summary>Gets whether to use stage bounds.</summary>
        public bool UseStage => UseStageCheckBox?.IsChecked == true;

        /// <summary>Gets whether animation should loop (GIF).</summary>
        public bool Loop => LoopCheckBox?.IsChecked == true;

        /// <summary>Gets video quality (0-100).</summary>
        public int VideoQuality => (int)(QualitySlider?.Value ?? 80);

        /// <summary>Gets whether to export layers separately.</summary>
        public bool SeparateLayers => SeparateLayersCheckBox?.IsChecked == true;

        /// <summary>Gets whether FPS is overridden.</summary>
        public bool OverrideFps => OverrideFpsCheckBox?.IsChecked == true;

        /// <summary>Gets the FPS override value.</summary>
        public int Fps => OverrideFps ? (int)(FpsNumberBox?.Value ?? 12) : 0;

        /// <summary>Gets whether the document has a valid tile animation reel.</summary>
        public bool HasTileAnimation => _selectedReel != null && _selectedReel.FrameCount > 0;

        /// <summary>Gets whether the document has canvas animation frames.</summary>
        public bool HasCanvasAnimation => _document.CanvasAnimationState.FrameCount > 0;

        //////////////////////////////////////////////////////////////////
        // CONSTRUCTORS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new AnimationExportDialog with the default mode selection.
        /// </summary>
        public AnimationExportDialog(CanvasDocument document)
            : this(document, null)
        {
        }

        /// <summary>
        /// Creates a new AnimationExportDialog with a preferred animation mode.
        /// </summary>
        /// <param name="document">The document to export from.</param>
        /// <param name="preferredMode">The preferred animation mode to default to, or null to auto-select.</param>
        public AnimationExportDialog(CanvasDocument document, AnimationMode? preferredMode)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _selectedReel = document.TileAnimationState.SelectedReel;
            _preferredMode = preferredMode ?? (HasTileAnimation ? AnimationMode.Tile : AnimationMode.Canvas);

            InitializeComponent();
            _isInitialized = true;

            // Set default format
            FormatComboBox.SelectedIndex = 0;

            // Configure based on available animations and preferred mode
            ConfigureSourceOptions();
            UpdateSourceInfo();
            UpdateFormatOptions();
            UpdateOutputSize();

            // Wire up events
            ScaleNumberBox.ValueChanged += (s, e) => UpdateOutputSize();
            QualitySlider.ValueChanged += (s, e) => QualityText.Text = $"{(int)QualitySlider.Value}%";
        }

        //////////////////////////////////////////////////////////////////
        // PUBLIC API
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads preview frames and starts animation preview.
        /// </summary>
        public async Task LoadPreviewAsync()
        {
            if (!_isInitialized || _isClosing) return;

            try
            {
                var service = new AnimationExportService();
                var options = new AnimationExportService.ExportOptions
                {
                    Scale = 1, // Preview at 1x
                    UseStage = UseStage
                };

                if (ExportTileAnimation && _selectedReel != null)
                {
                    _previewFrames = await service.RenderTileAnimationAsync(_document, _selectedReel, options);
                }
                else if (HasCanvasAnimation)
                {
                    _previewFrames = await service.RenderCanvasAnimationAsync(_document, options);
                }

                if (_isClosing) return; // Check again after async operation

                if (_previewFrames != null && _previewFrames.Count > 0)
                {
                    _previewFrameIndex = 0;
                    await ShowPreviewFrameAsync(_previewFrameIndex);
                    StartPreviewAnimation();
                }
            }
            catch (Exception ex)
            {
                if (!_isClosing && PreviewInfoText != null)
                    PreviewInfoText.Text = $"Preview error: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets export options based on dialog settings.
        /// </summary>
        public AnimationExportService.ExportOptions GetExportOptions()
        {
            return new AnimationExportService.ExportOptions
            {
                Scale = Scale,
                UseStage = UseStage,
                SeparateLayers = SeparateLayers,
                FrameDelayMs = OverrideFps ? (int)(1000.0 / Math.Max(1, Fps)) : 0
            };
        }

        //////////////////////////////////////////////////////////////////
        // EVENT HANDLERS
        //////////////////////////////////////////////////////////////////

        private void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            // Mark as closing to prevent timer from accessing disposed UI elements
            _isClosing = true;
            StopPreviewAnimation();
            _previewFrames = null;
        }

        private void SourceRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _isClosing) return;

            UpdateSourceInfo();
            UpdateStageOptions();
            UpdateOutputSize();
            _ = LoadPreviewAsync();
        }

        private void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _isClosing) return;

            UpdateFormatOptions();
        }

        private void UseStageCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _isClosing) return;

            UpdateStageInfo();
            UpdateOutputSize();
            _ = LoadPreviewAsync();
        }

        private void OverrideFpsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _isClosing) return;

            if (FpsNumberBox != null)
                FpsNumberBox.IsEnabled = OverrideFpsCheckBox?.IsChecked == true;
        }

        //////////////////////////////////////////////////////////////////
        // PRIVATE METHODS
        //////////////////////////////////////////////////////////////////

        private void ConfigureSourceOptions()
        {
            if (TileAnimationRadio == null || CanvasAnimationRadio == null) return;

            TileAnimationRadio.IsEnabled = HasTileAnimation;
            CanvasAnimationRadio.IsEnabled = HasCanvasAnimation;

            // Determine which mode to select based on preferred mode and availability
            bool selectTile = false;
            bool selectCanvas = false;

            if (_preferredMode == AnimationMode.Tile && HasTileAnimation)
            {
                selectTile = true;
            }
            else if (_preferredMode == AnimationMode.Canvas && HasCanvasAnimation)
            {
                selectCanvas = true;
            }
            else if (HasTileAnimation)
            {
                // Fallback: prefer tile if available
                selectTile = true;
            }
            else if (HasCanvasAnimation)
            {
                // Fallback: canvas if tile not available
                selectCanvas = true;
            }
            // else: neither available - both remain unchecked and disabled

            // Apply selection
            TileAnimationRadio.IsChecked = selectTile;
            CanvasAnimationRadio.IsChecked = selectCanvas;

            UpdateStageOptions();
        }

        private void UpdateSourceInfo()
        {
            if (SourceInfoText == null) return;

            if (ExportTileAnimation && _selectedReel != null)
            {
                int fps = (int)(1000.0 / Math.Max(1, _selectedReel.DefaultFrameTimeMs));
                SourceInfoText.Text = $"{_selectedReel.FrameCount} frames @ {fps} fps ({_selectedReel.Name})";
            }
            else if (HasCanvasAnimation)
            {
                var animState = _document.CanvasAnimationState;
                SourceInfoText.Text = $"{animState.FrameCount} frames @ {animState.FramesPerSecond} fps";
            }
            else
            {
                SourceInfoText.Text = "No animation available";
            }
        }

        private void UpdateStageOptions()
        {
            if (StageOptionsPanel == null) return;

            // Stage options only apply to canvas animation
            bool showStage = !ExportTileAnimation && HasCanvasAnimation;
            StageOptionsPanel.Visibility = showStage ? Visibility.Visible : Visibility.Collapsed;

            if (showStage && UseStageCheckBox != null)
            {
                var stage = _document.CanvasAnimationState.Stage;
                UseStageCheckBox.IsEnabled = stage.Enabled;
                UseStageCheckBox.IsChecked = stage.Enabled;
                UpdateStageInfo();
            }
        }

        private void UpdateStageInfo()
        {
            if (StageInfoText == null) return;

            // Only show stage info for canvas animation with stage enabled
            if (!ExportTileAnimation && _document.CanvasAnimationState.Stage.Enabled && UseStage)
            {
                var stage = _document.CanvasAnimationState.Stage;
                StageInfoText.Text = $"Stage: {stage.StageWidth}×{stage.StageHeight} ? {stage.OutputWidth}×{stage.OutputHeight} output";
                StageInfoText.Visibility = Visibility.Visible;
            }
            else
            {
                StageInfoText.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateFormatOptions()
        {
            if (LoopCheckBox == null || VideoQualityPanel == null || SeparateLayersPanel == null) return;

            string format = SelectedFormat;
            bool isVideo = format == "mp4" || format == "wmv" || format == "avi";
            bool isImageSequence = format == "png" || format == "jpg";
            bool isGif = format == "gif";

            // Show/hide format-specific options
            LoopCheckBox.Visibility = isGif ? Visibility.Visible : Visibility.Collapsed;
            VideoQualityPanel.Visibility = isVideo ? Visibility.Visible : Visibility.Collapsed;
            SeparateLayersPanel.Visibility = isImageSequence ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateOutputSize()
        {
            if (OutputSizeText == null) return;

            int w, h;

            if (ExportTileAnimation && HasTileAnimation)
            {
                w = _document.TileSize.Width;
                h = _document.TileSize.Height;
            }
            else if (UseStage && _document.CanvasAnimationState.Stage.Enabled)
            {
                var stage = _document.CanvasAnimationState.Stage;
                w = stage.OutputWidth;
                h = stage.OutputHeight;
            }
            else
            {
                w = _document.PixelWidth;
                h = _document.PixelHeight;
            }

            int scale = Scale;
            OutputSizeText.Text = $"{w * scale}×{h * scale} pixels";
        }

        private async Task ShowPreviewFrameAsync(int index)
        {
            // Early exit if dialog is closing or disposed
            if (_isClosing || _previewFrames == null || index < 0 || index >= _previewFrames.Count)
                return;

            try
            {
                var frame = _previewFrames[index];

                // Encode frame to PNG for display
                using var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)frame.Width,
                    (uint)frame.Height,
                    96, 96,
                    frame.Pixels);
                await encoder.FlushAsync();

                // Check again after async operations
                if (_isClosing) return;

                stream.Seek(0);

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);

                // Final check before touching UI
                if (_isClosing) return;

                if (PreviewImage != null)
                    PreviewImage.Source = bitmap;

                if (PreviewInfoText != null)
                    PreviewInfoText.Text = $"Frame {index + 1}/{_previewFrames.Count} - {frame.DurationMs}ms";
            }
            catch (Exception) when (_isClosing)
            {
                // Swallow exceptions that occur during closing
            }
        }

        private void StartPreviewAnimation()
        {
            StopPreviewAnimation();

            if (_isClosing || _previewFrames == null || _previewFrames.Count == 0)
                return;

            _previewTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_previewFrames[0].DurationMs)
            };

            _previewTimer.Tick += async (s, e) =>
            {
                // Check if closing before any work
                if (_isClosing || _previewFrames == null || _previewFrames.Count == 0)
                {
                    StopPreviewAnimation();
                    return;
                }

                _previewFrameIndex = (_previewFrameIndex + 1) % _previewFrames.Count;
                await ShowPreviewFrameAsync(_previewFrameIndex);

                // Update timer interval for next frame (with safety checks)
                if (!_isClosing && _previewTimer != null && _previewFrameIndex < _previewFrames.Count)
                {
                    _previewTimer.Interval = TimeSpan.FromMilliseconds(_previewFrames[_previewFrameIndex].DurationMs);
                }
            };

            _previewTimer.Start();
        }

        private void StopPreviewAnimation()
        {
            _previewTimer?.Stop();
            _previewTimer = null;
        }
    }
}
