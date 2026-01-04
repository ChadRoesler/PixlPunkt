using System;
using FluentIcons.Common;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Document;
using Windows.Foundation;
using Windows.Graphics.DirectX;

namespace PixlPunkt.UI.Animation
{
    /// <summary>
    /// Panel for animation playback preview.
    /// Shows live preview and transport controls.
    /// </summary>
    public sealed partial class PlaybackPanel : UserControl
    {
        // ====================================================================
        // FIELDS
        // ====================================================================

        private TileAnimationState? _state;
        private CanvasDocument? _document;
        private CanvasControl? _previewCanvas;
        private byte[]? _currentTilePixels;
        private int _currentTileWidth;
        private int _currentTileHeight;

        /// <summary>
        /// Tracks the currently subscribed reel for proper event cleanup.
        /// </summary>
        private TileAnimationReel? _subscribedReel;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public PlaybackPanel()
        {
            InitializeComponent();
            SetupPreviewCanvas();
            Unloaded += PlaybackPanel_Unloaded;
        }

        private void PlaybackPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            // Clean up all subscriptions when the panel is unloaded
            UnsubscribeAll();
        }

        /// <summary>
        /// Unsubscribes from all event handlers to prevent memory leaks.
        /// </summary>
        private void UnsubscribeAll()
        {
            if (_state != null)
            {
                _state.PlaybackStateChanged -= OnPlaybackStateChanged;
                _state.CurrentFrameChanged -= OnCurrentFrameChanged;
                _state.SelectedReelChanged -= OnSelectedReelChanged;
            }

            UnsubscribeFromReel();
        }

        /// <summary>
        /// Unsubscribes from the currently tracked reel's Changed event.
        /// </summary>
        private void UnsubscribeFromReel()
        {
            if (_subscribedReel != null)
            {
                _subscribedReel.Changed -= OnReelChanged;
                _subscribedReel = null;
            }
        }

        /// <summary>
        /// Subscribes to a reel's Changed event, properly tracking for cleanup.
        /// </summary>
        private void SubscribeToReel(TileAnimationReel? reel)
        {
            // First unsubscribe from any existing reel
            UnsubscribeFromReel();

            // Subscribe to new reel if provided
            if (reel != null)
            {
                reel.Changed += OnReelChanged;
                _subscribedReel = reel;
            }
        }

        private void SetupPreviewCanvas()
        {
            _previewCanvas = new CanvasControl();
            _previewCanvas.Draw += PreviewCanvas_Draw;

            // Replace the Image with the CanvasControl
            PreviewBorder.Child = _previewCanvas;
        }

        private void PreviewCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            ds.Antialiasing = CanvasAntialiasing.Aliased;

            // Clear background
            ds.Clear(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            if (_currentTilePixels == null || _currentTilePixels.Length < _currentTileWidth * _currentTileHeight * 4)
                return;

            float w = (float)sender.ActualWidth;
            float h = (float)sender.ActualHeight;

            if (w <= 0 || h <= 0) return;

            using var bitmap = CanvasBitmap.CreateFromBytes(
                sender.Device,
                _currentTilePixels,
                _currentTileWidth,
                _currentTileHeight,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96.0f);

            // Calculate destination rect (centered, uniform scale)
            float scale = Math.Min(w / _currentTileWidth, h / _currentTileHeight);
            float destW = _currentTileWidth * scale;
            float destH = _currentTileHeight * scale;
            float destX = (w - destW) / 2;
            float destY = (h - destH) / 2;

            ds.DrawImage(
                bitmap,
                new Rect(destX, destY, destW, destH),
                new Rect(0, 0, _currentTileWidth, _currentTileHeight),
                1.0f,
                CanvasImageInterpolation.NearestNeighbor);
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Binds the panel to animation state and document.
        /// </summary>
        public void Bind(TileAnimationState? state, CanvasDocument? document)
        {
            // Unbind previous state
            if (_state != null)
            {
                _state.PlaybackStateChanged -= OnPlaybackStateChanged;
                _state.CurrentFrameChanged -= OnCurrentFrameChanged;
                _state.SelectedReelChanged -= OnSelectedReelChanged;
            }

            // Unsubscribe from previous reel
            UnsubscribeFromReel();

            _state = state;
            _document = document;

            // Bind new
            if (_state != null)
            {
                _state.PlaybackStateChanged += OnPlaybackStateChanged;
                _state.CurrentFrameChanged += OnCurrentFrameChanged;
                _state.SelectedReelChanged += OnSelectedReelChanged;

                // Subscribe to selected reel
                SubscribeToReel(_state.SelectedReel);
            }

            RefreshDisplay();
        }

        /// <summary>
        /// Refreshes the display.
        /// </summary>
        public void RefreshDisplay()
        {
            UpdateTransportUI();
            UpdateFrameInfo();
            RefreshPreview();
        }

        // ====================================================================
        // EVENT HANDLERS
        // ====================================================================

        private void OnPlaybackStateChanged(PlaybackState state)
        {
            DispatcherQueue.TryEnqueue(UpdateTransportUI);
        }

        private void OnCurrentFrameChanged(int frameIndex)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateFrameInfo();
                RefreshPreview();
            });
        }

        private void OnSelectedReelChanged(TileAnimationReel? reel)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Properly track and subscribe to the new reel
                SubscribeToReel(reel);

                UpdateTransportUI();
                UpdateFrameInfo();
                RefreshPreview();
            });
        }

        private void OnReelChanged()
        {
            DispatcherQueue.TryEnqueue(RefreshDisplay);
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            _state?.TogglePlayPause();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _state?.Stop();
        }

        private void StepBackButton_Click(object sender, RoutedEventArgs e)
        {
            _state?.PreviousFrame();
        }

        private void StepForwardButton_Click(object sender, RoutedEventArgs e)
        {
            _state?.NextFrame();
        }

        private void LoopToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_state?.SelectedReel == null) return;
            _state.SelectedReel.Loop = LoopToggle.IsChecked == true;
            _state.SelectedReel.NotifyChanged();
        }

        // ====================================================================
        // PRIVATE METHODS
        // ====================================================================

        private void UpdateTransportUI()
        {
            bool hasReel = _state?.SelectedReel != null;
            bool hasFrames = hasReel && _state!.SelectedReel!.FrameCount > 0;

            PlayPauseButton.IsEnabled = hasFrames;
            StopButton.IsEnabled = hasFrames;
            StepBackButton.IsEnabled = hasFrames;
            StepForwardButton.IsEnabled = hasFrames;
            LoopToggle.IsEnabled = hasReel;

            // Update play/pause icon
            bool isPlaying = _state?.IsPlaying == true;
            PlayPauseIcon.Icon = isPlaying ? Icon.Pause : Icon.Play;

            // Update loop toggle
            if (_state?.SelectedReel != null)
            {
                LoopToggle.IsChecked = _state.SelectedReel.Loop;
            }
        }

        private void UpdateFrameInfo()
        {
            if (_state?.SelectedReel == null)
            {
                FrameInfoText.Text = "No animation";
                TimeInfoText.Text = "";
                return;
            }

            var reel = _state.SelectedReel;
            int current = _state.CurrentFrameIndex;
            int total = reel.FrameCount;

            if (total == 0)
            {
                FrameInfoText.Text = "No frames";
                TimeInfoText.Text = "";
                return;
            }

            FrameInfoText.Text = $"Frame {current + 1}/{total}";

            // Calculate time info
            int currentTimeMs = reel.GetTimeAtFrame(current);
            int totalTimeMs = reel.TotalDurationMs;

            TimeInfoText.Text = $"{currentTimeMs / 1000.0:F1}s / {totalTimeMs / 1000.0:F1}s";
        }

        /// <summary>
        /// Reads pixels from the canvas at a tile grid position.
        /// </summary>
        private byte[]? ReadTilePixelsFromCanvas(int tileX, int tileY)
        {
            if (_document == null) return null;

            int tileW = _document.TileSize.Width;
            int tileH = _document.TileSize.Height;

            // Calculate pixel coordinates
            int docX = tileX * tileW;
            int docY = tileY * tileH;

            // Ensure we're within bounds
            if (docX < 0 || docY < 0 ||
                docX + tileW > _document.PixelWidth ||
                docY + tileH > _document.PixelHeight)
            {
                return null;
            }

            // Read from composite surface
            var surface = _document.Surface;
            if (surface == null) return null;

            byte[] tilePixels = new byte[tileW * tileH * 4];

            for (int y = 0; y < tileH; y++)
            {
                for (int x = 0; x < tileW; x++)
                {
                    int srcIdx = ((docY + y) * surface.Width + (docX + x)) * 4;
                    int dstIdx = (y * tileW + x) * 4;

                    if (srcIdx + 3 < surface.Pixels.Length && dstIdx + 3 < tilePixels.Length)
                    {
                        tilePixels[dstIdx + 0] = surface.Pixels[srcIdx + 0];
                        tilePixels[dstIdx + 1] = surface.Pixels[srcIdx + 1];
                        tilePixels[dstIdx + 2] = surface.Pixels[srcIdx + 2];
                        tilePixels[dstIdx + 3] = surface.Pixels[srcIdx + 3];
                    }
                }
            }

            return tilePixels;
        }

        private void RefreshPreview()
        {
            if (_state?.SelectedReel == null || _document == null)
            {
                _currentTilePixels = null;
                _previewCanvas?.Invalidate();
                return;
            }

            var (tileX, tileY) = _state.CurrentTilePosition;
            if (tileX < 0 || tileY < 0)
            {
                _currentTilePixels = null;
                _previewCanvas?.Invalidate();
                return;
            }

            // Read pixels from canvas at tile position
            _currentTilePixels = ReadTilePixelsFromCanvas(tileX, tileY);
            if (_currentTilePixels == null)
            {
                _previewCanvas?.Invalidate();
                return;
            }

            _currentTileWidth = _document.TileSize.Width;
            _currentTileHeight = _document.TileSize.Height;
            _previewCanvas?.Invalidate();
        }
    }
}
