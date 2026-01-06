using System;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Animation;
using PixlPunkt.Uno.Core.Document;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace PixlPunkt.Uno.UI.Animation
{
    /// <summary>
    /// Panel for animation playback preview.
    /// </summary>
    public sealed partial class PlaybackPanel : UserControl
    {
        // ====================================================================
        // FIELDS
        // ====================================================================

        private TileAnimationState? _state;
        private CanvasDocument? _document;
        private SKXamlCanvas? _previewCanvas;
        private byte[]? _currentTilePixels;
        private int _currentTileWidth;
        private int _currentTileHeight;

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
            UnsubscribeAll();
        }

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

        private void UnsubscribeFromReel()
        {
            if (_subscribedReel != null)
            {
                _subscribedReel.Changed -= OnReelChanged;
                _subscribedReel = null;
            }
        }

        private void SubscribeToReel(TileAnimationReel? reel)
        {
            UnsubscribeFromReel();

            if (reel != null)
            {
                reel.Changed += OnReelChanged;
                _subscribedReel = reel;
            }
        }

        private void SetupPreviewCanvas()
        {
            _previewCanvas = new SKXamlCanvas();
            _previewCanvas.PaintSurface += PreviewCanvas_PaintSurface;
            PreviewBorder.Child = _previewCanvas;
        }

        private void PreviewCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (_currentTilePixels == null || _currentTilePixels.Length < _currentTileWidth * _currentTileHeight * 4)
                return;

            float w = e.Info.Width;
            float h = e.Info.Height;

            if (w <= 0 || h <= 0) return;

            var info = new SKImageInfo(_currentTileWidth, _currentTileHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(info);

            var handle = System.Runtime.InteropServices.GCHandle.Alloc(_currentTilePixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes);

                float scale = Math.Min(w / _currentTileWidth, h / _currentTileHeight);
                float destW = _currentTileWidth * scale;
                float destH = _currentTileHeight * scale;
                float destX = (w - destW) / 2;
                float destY = (h - destH) / 2;

                var destRect = new SKRect(destX, destY, destX + destW, destY + destH);
                var srcRect = new SKRect(0, 0, _currentTileWidth, _currentTileHeight);

                using var paint = new SKPaint
                {
                    FilterQuality = SKFilterQuality.None,
                    IsAntialias = false
                };

                canvas.DrawBitmap(bitmap, srcRect, destRect, paint);
            }
            finally
            {
                handle.Free();
            }
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        public void Bind(TileAnimationState? state, CanvasDocument? document)
        {
            if (_state != null)
            {
                _state.PlaybackStateChanged -= OnPlaybackStateChanged;
                _state.CurrentFrameChanged -= OnCurrentFrameChanged;
                _state.SelectedReelChanged -= OnSelectedReelChanged;
            }

            UnsubscribeFromReel();

            _state = state;
            _document = document;

            if (_state != null)
            {
                _state.PlaybackStateChanged += OnPlaybackStateChanged;
                _state.CurrentFrameChanged += OnCurrentFrameChanged;
                _state.SelectedReelChanged += OnSelectedReelChanged;

                SubscribeToReel(_state.SelectedReel);
            }

            RefreshDisplay();
        }

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

            bool isPlaying = _state?.IsPlaying == true;
            PlayPauseIcon.Icon = isPlaying ? Icon.Pause : Icon.Play;

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

            int currentTimeMs = reel.GetTimeAtFrame(current);
            int totalTimeMs = reel.TotalDurationMs;

            TimeInfoText.Text = $"{currentTimeMs / 1000.0:F1}s / {totalTimeMs / 1000.0:F1}s";
        }

        private byte[]? ReadTilePixelsFromCanvas(int tileX, int tileY)
        {
            if (_document == null) return null;

            int tileW = _document.TileSize.Width;
            int tileH = _document.TileSize.Height;

            int docX = tileX * tileW;
            int docY = tileY * tileH;

            if (docX < 0 || docY < 0 ||
                docX + tileW > _document.PixelWidth ||
                docY + tileH > _document.PixelHeight)
            {
                return null;
            }

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
