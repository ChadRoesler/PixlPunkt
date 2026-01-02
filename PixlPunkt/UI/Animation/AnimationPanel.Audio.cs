using System;
using System.Collections.Generic;
using System.Linq;
using FluentIcons.Common;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Tools;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.Animation
{
    public partial class AnimationPanel
    {
        // ====================================================================
        // FIELDS
        // ====================================================================

        // Audio track dragging
        private bool _isDraggingAudioTrack;
        private int _audioDragStartFrame;
        private int _audioDragStartOffset;
        private int _audioDragTrackIndex = -1;

        private void AudioHeader_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.AudioTracks.ToggleCollapsed();
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();
            UpdateCanvasPlayhead();
        }

        private void AudioAddTrack_Click(object sender, RoutedEventArgs e)
        {
            // Load a new audio file into a new track
            _ = AddNewAudioTrackAsync();
        }

        private async System.Threading.Tasks.Task AddNewAudioTrackAsync()
        {
            if (_canvasAnimationState == null) return;

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".ogg");
            picker.FileTypeFilter.Add(".flac");
            picker.FileTypeFilter.Add(".m4a");
            picker.FileTypeFilter.Add(".wma");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.PixlPunktMainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var newTrack = _canvasAnimationState.AudioTracks.AddTrack();
                bool success = await newTrack.LoadAsync(file.Path);
                if (!success)
                {
                    _canvasAnimationState.AudioTracks.RemoveTrack(newTrack);
                }
                else
                {
                    RefreshCanvasLayerNames();
                    RefreshCanvasKeyframeGrid();
                }
            }
        }

        private void AudioTrackMute_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            if (sender is Button btn && btn.Tag is int trackIndex)
            {
                if (trackIndex >= 0 && trackIndex < _canvasAnimationState.AudioTracks.Count)
                {
                    var track = _canvasAnimationState.AudioTracks[trackIndex];
                    track.Settings.Muted = !track.Settings.Muted;
                    RefreshCanvasLayerNames();
                }
            }
        }

        private void AudioTrackRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            if (sender is Button btn && btn.Tag is int trackIndex)
            {
                if (trackIndex >= 0 && trackIndex < _canvasAnimationState.AudioTracks.Count)
                {
                    _canvasAnimationState.AudioTracks.RemoveTrackAt(trackIndex);
                    RefreshCanvasLayerNames();
                    RefreshCanvasKeyframeGrid();
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - AUDIO TRACK STATE
        // ════════════════════════════════════════════════════════════════════

        private void OnAudioLoadedChanged(bool loaded)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateAudioUI();
            });
        }

        private void OnAudioWaveformUpdated()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshCanvasKeyframeGrid();
            });
        }

        private void OnAudioTracksChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshCanvasLayerNames();
                RefreshCanvasKeyframeGrid();
                UpdateCanvasPlayhead();
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - AUDIO CONTROLS
        // ════════════════════════════════════════════════════════════════════

        private bool _suppressAudioValueChanges;

        private async void AudioLoad_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".ogg");
            picker.FileTypeFilter.Add(".flac");
            picker.FileTypeFilter.Add(".m4a");
            picker.FileTypeFilter.Add(".wma");

            // Get the window handle for the picker
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.PixlPunktMainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                bool success = await _canvasAnimationState.AudioTrack.LoadAsync(file.Path);
                if (success)
                {
                    UpdateAudioUI();
                }
            }
        }

        private void AudioMute_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;

            // Find the toggle button from sender
            if (sender is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton toggle)
            {
                _canvasAnimationState.AudioTrack.Settings.Muted = toggle.IsChecked ?? false;
                UpdateAudioMuteIcon();
            }
        }

        private void AudioVolume_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressAudioValueChanges || _canvasAnimationState == null) return;
            _canvasAnimationState.AudioTrack.Settings.Volume = (float)(e.NewValue / 100.0);
        }

        private void AudioRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.AudioTrack.Unload();
            UpdateAudioUI();
        }

        private void UpdateAudioUI()
        {
            if (_canvasAnimationState == null) return;

            var audioTrack = _canvasAnimationState.AudioTrack;

            _suppressAudioValueChanges = true;

            // Find and update UI controls by name
            if (FindName("AudioVolumeSlider") is Slider volumeSlider)
            {
                volumeSlider.Value = audioTrack.Settings.Volume * 100;
            }

            if (FindName("AudioMuteToggle") is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton muteToggle)
            {
                muteToggle.IsChecked = audioTrack.Settings.Muted;
            }

            UpdateAudioMuteIcon();

            if (FindName("AudioRemoveButton") is Button removeButton)
            {
                removeButton.Visibility = audioTrack.IsLoaded ? Visibility.Visible : Visibility.Collapsed;
            }

            // Refresh timeline to show/hide audio track row
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();

            _suppressAudioValueChanges = false;
        }

        private void UpdateAudioMuteIcon()
        {
            if (_canvasAnimationState == null) return;
            bool muted = _canvasAnimationState.AudioTrack.Settings.Muted;

            if (FindName("AudioMuteIcon") is FluentIcons.WinUI.FluentIcon icon)
            {
                icon.Icon = muted ? FluentIcons.Common.Icon.SpeakerMute : FluentIcons.Common.Icon.Speaker2;
            }
        }

        /// <summary>
        /// Draws the audio section header row (shows collapsed/expanded indicator).
        /// </summary>
        private void DrawAudioHeaderRow(int rowIndex, int frameCount)
        {
            // Background for audio header
            var bgRect = new Rectangle
            {
                Width = frameCount * CellWidth,
                Height = CellHeight,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 200, 200))
            };
            Canvas.SetLeft(bgRect, 0);
            Canvas.SetTop(bgRect, rowIndex * CellHeight);
            CanvasKeyframeCanvas.Children.Add(bgRect);
        }

        /// <summary>
        /// Draws a simplified waveform representation in the audio track row.
        /// Respects the audio start frame offset so the waveform appears at the correct timeline position.
        /// The waveform can be dragged left/right to adjust the offset.
        /// </summary>
        private void DrawAudioTrackWaveform(int trackIndex, AudioTrackState audioTrack, int audioTrackCollectionIndex)
        {
            if (_canvasAnimationState == null) return;

            int frameCount = _canvasAnimationState.FrameCount;
            int fps = _canvasAnimationState.FramesPerSecond;
            double audioDurationMs = audioTrack.DurationMs;
            int startFrameOffset = audioTrack.Settings.StartFrameOffset;

            // Calculate audio duration in frames
            double audioDurationFrames = (audioDurationMs * fps) / 1000.0;

            // Background for audio track (full row)
            var bgRect = new Rectangle
            {
                Width = frameCount * CellWidth,
                Height = CellHeight,
                Fill = new SolidColorBrush(Color.FromArgb(20, 0, 200, 200))
            };
            Canvas.SetLeft(bgRect, 0);
            Canvas.SetTop(bgRect, trackIndex * CellHeight);
            CanvasKeyframeCanvas.Children.Add(bgRect);

            // Draw waveform points
            float centerY = trackIndex * CellHeight + CellHeight / 2f;
            float maxAmplitude = (CellHeight / 2f) - 2;

            var waveformBrush = new SolidColorBrush(Color.FromArgb(150, 0, 200, 200));

            // Calculate where audio is visible in the timeline
            int audioStartFrame = startFrameOffset;
            int audioEndFrame = startFrameOffset + (int)Math.Ceiling(audioDurationFrames);

            // Draw waveform region background (highlight where audio actually plays)
            if (audioEndFrame > 0 && audioStartFrame < frameCount)
            {
                int visibleStartFrame = Math.Max(0, audioStartFrame);
                int visibleEndFrame = Math.Min(frameCount, audioEndFrame);

                var waveformBgRect = new Rectangle
                {
                    Width = (visibleEndFrame - visibleStartFrame) * CellWidth,
                    Height = CellHeight - 4,
                    Fill = new SolidColorBrush(Color.FromArgb(50, 0, 200, 200)),
                    RadiusX = 2,
                    RadiusY = 2
                };
                Canvas.SetLeft(waveformBgRect, visibleStartFrame * CellWidth);
                Canvas.SetTop(waveformBgRect, trackIndex * CellHeight + 2);
                CanvasKeyframeCanvas.Children.Add(waveformBgRect);
            }

            // Draw waveform points
            foreach (var point in audioTrack.WaveformData)
            {
                double audioFrame = (point.TimeMs * fps) / 1000.0;
                double animationFrame = audioFrame + startFrameOffset;

                if (animationFrame < 0 || animationFrame >= frameCount) continue;

                float x = (float)(animationFrame * CellWidth);
                float amplitude = point.AveragePeak * maxAmplitude;
                amplitude = Math.Max(amplitude, 1f);

                var bar = new Rectangle
                {
                    Width = 1.5,
                    Height = amplitude * 2,
                    Fill = waveformBrush
                };
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, centerY - amplitude);
                CanvasKeyframeCanvas.Children.Add(bar);
            }

            // Draw cutoff indicator if audio extends past animation end
            if (audioEndFrame > frameCount)
            {
                float cutoffX = frameCount * CellWidth - 2;
                var cutoffLine = new Line
                {
                    X1 = cutoffX,
                    Y1 = trackIndex * CellHeight + 2,
                    X2 = cutoffX,
                    Y2 = trackIndex * CellHeight + CellHeight - 2,
                    Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 100, 100)),
                    StrokeThickness = 2,
                    StrokeDashArray = [2, 2]
                };
                CanvasKeyframeCanvas.Children.Add(cutoffLine);
            }

            // Draw start indicator (handle) at the beginning of the audio
            if (audioStartFrame >= 0 && audioStartFrame < frameCount)
            {
                float handleX = audioStartFrame * CellWidth;
                var handleLine = new Line
                {
                    X1 = handleX,
                    Y1 = trackIndex * CellHeight + 2,
                    X2 = handleX,
                    Y2 = trackIndex * CellHeight + CellHeight - 2,
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 200, 200)),
                    StrokeThickness = 2
                };
                CanvasKeyframeCanvas.Children.Add(handleLine);

                var triangle = new Polygon
                {
                    Points =
                    [
                        new Point(handleX, trackIndex * CellHeight + 2),
                        new Point(handleX + 6, trackIndex * CellHeight + 2),
                        new Point(handleX, trackIndex * CellHeight + 8)
                    ],
                    Fill = new SolidColorBrush(Color.FromArgb(255, 0, 200, 200))
                };
                CanvasKeyframeCanvas.Children.Add(triangle);
            }

            // Center line
            var centerLine = new Line
            {
                X1 = 0,
                Y1 = centerY,
                X2 = frameCount * CellWidth,
                Y2 = centerY,
                Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                StrokeThickness = 0.5
            };
            CanvasKeyframeCanvas.Children.Add(centerLine);
        }
        private void AudioTrack_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Audio track click - could show audio settings in the future
            // For now, just deselect stage and layers
            DeselectStage();
            _selectedLayerId = Guid.Empty;
            UpdateCanvasLayerSelection();
        }
    }
}
