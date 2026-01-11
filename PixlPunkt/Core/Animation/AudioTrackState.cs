using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PixlPunkt.Core.Audio;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Manages audio track state including playback and waveform data for the animation timeline.
    /// </summary>
    public sealed class AudioTrackState : IDisposable
    {
        private const int MaxWaveformPoints = 10000;

        private IAudioPlayer? _audioPlayer;
        private CancellationTokenSource? _waveformCts;
        private bool _isDisposed;

        public AudioTrackSettings Settings { get; } = new();
        public bool IsLoaded => _audioPlayer?.IsLoaded ?? false;
        public double DurationMs => _audioPlayer?.DurationMs ?? 0;
        public int SampleRate { get; private set; }
        public int ChannelCount { get; private set; }
        public double PositionMs => _audioPlayer?.PositionMs ?? 0;
        public bool IsPlaying => _audioPlayer?.IsPlaying ?? false;
        public static bool IsPlaybackSupported => AudioPlayerFactory.IsPlaybackSupported;

        /// <summary>
        /// Gets whether FFmpeg is available for enhanced audio features (real waveforms).
        /// </summary>
        public static bool IsFFmpegAvailable => FFmpegService.IsAvailable;

        public IReadOnlyList<WaveformPoint> WaveformData => _waveformData;
        private readonly List<WaveformPoint> _waveformData = [];

        public bool IsGeneratingWaveform { get; private set; }
        public float WaveformGenerationProgress { get; private set; }
        
        /// <summary>
        /// Gets whether the waveform data is a placeholder (FFmpeg not available).
        /// </summary>
        public bool IsWaveformPlaceholder { get; private set; }

        public event Action<bool>? AudioLoadedChanged;
        public event Action<bool>? PlaybackStateChanged;
        public event Action<double>? PositionChanged;
        public event Action? WaveformUpdated;
        public event Action<float>? WaveformProgressChanged;
        public event Action<string>? ErrorOccurred;

        public AudioTrackState()
        {
            Settings.PropertyChanged += (_, e) =>
            {
                if (_audioPlayer == null) return;
                if (e.PropertyName == nameof(AudioTrackSettings.Volume))
                    _audioPlayer.Volume = Settings.Volume;
                else if (e.PropertyName == nameof(AudioTrackSettings.Muted))
                    _audioPlayer.IsMuted = Settings.Muted;
            };
        }

        public async Task<bool> LoadAsync(string filePath)
        {
            if (_isDisposed) return false;
            Unload();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                ErrorOccurred?.Invoke($"Audio file not found: {filePath}");
                return false;
            }

            // Try to get metadata from FFmpeg first (for accurate duration)
            var metadata = await FFmpegWaveformExtractor.GetMetadataAsync(filePath);
            
            _audioPlayer = AudioPlayerFactory.Create();
            _audioPlayer.PositionChanged += OnPositionChanged;
            _audioPlayer.PlaybackStateChanged += OnPlaybackStateChanged;
            _audioPlayer.MediaEnded += OnMediaEnded;
            _audioPlayer.ErrorOccurred += OnError;
            _audioPlayer.Volume = Settings.Volume;
            _audioPlayer.IsMuted = Settings.Muted;

            bool success = await _audioPlayer.LoadAsync(filePath);
            if (success)
            {
                Settings.FilePath = filePath;
                SampleRate = metadata?.SampleRate ?? 44100;
                ChannelCount = metadata?.Channels ?? 2;
                AudioLoadedChanged?.Invoke(true);
                
                // Start waveform generation using FFmpeg
                _ = GenerateWaveformAsync(filePath);
            }
            else
            {
                Unload();
            }
            return success;
        }

        public void Unload()
        {
            _waveformCts?.Cancel();
            _waveformCts?.Dispose();
            _waveformCts = null;

            if (_audioPlayer != null)
            {
                _audioPlayer.PositionChanged -= OnPositionChanged;
                _audioPlayer.PlaybackStateChanged -= OnPlaybackStateChanged;
                _audioPlayer.MediaEnded -= OnMediaEnded;
                _audioPlayer.ErrorOccurred -= OnError;
                _audioPlayer.Dispose();
                _audioPlayer = null;
            }

            _waveformData.Clear();
            SampleRate = 0;
            ChannelCount = 0;
            IsGeneratingWaveform = false;
            WaveformGenerationProgress = 0;
            IsWaveformPlaceholder = false;
            Settings.Clear();
            AudioLoadedChanged?.Invoke(false);
            WaveformUpdated?.Invoke();
        }

        public void Play() => _audioPlayer?.Play();
        public void Pause() => _audioPlayer?.Pause();
        public void Stop() => _audioPlayer?.Stop();

        public void SeekToFrame(int frameIndex, int fps)
        {
            if (_audioPlayer == null || _isDisposed || fps <= 0) return;
            int audioFrame = frameIndex - Settings.StartFrameOffset;
            double audioPositionMs = Math.Clamp((audioFrame * 1000.0) / fps, 0, DurationMs);
            _audioPlayer.Seek(audioPositionMs);
        }

        public int GetCurrentFrame(int fps)
        {
            if (fps <= 0) return 0;
            return (int)((PositionMs * fps) / 1000.0) + Settings.StartFrameOffset;
        }

        public bool ShouldPlayAtFrame(int frameIndex, int fps, int totalFrames)
        {
            if (fps <= 0 || frameIndex >= totalFrames) return false;
            int audioFrame = frameIndex - Settings.StartFrameOffset;
            if (audioFrame < 0) return false;
            double audioDurationFrames = (DurationMs * fps) / 1000.0;
            return audioFrame < audioDurationFrames;
        }

        public (int startFrame, int endFrame)? GetAudibleFrameRange(int fps, int totalAnimationFrames)
        {
            if (fps <= 0 || DurationMs <= 0) return null;
            int audioStartFrame = Math.Max(0, Settings.StartFrameOffset);
            double audioDurationFrames = (DurationMs * fps) / 1000.0;
            int audioEndFrame = Math.Min(Settings.StartFrameOffset + (int)Math.Ceiling(audioDurationFrames), totalAnimationFrames);
            return audioStartFrame >= audioEndFrame ? null : (audioStartFrame, audioEndFrame);
        }

        private void OnPositionChanged(double positionMs) => PositionChanged?.Invoke(positionMs);
        private void OnPlaybackStateChanged(bool isPlaying) => PlaybackStateChanged?.Invoke(isPlaying);
        private void OnMediaEnded() => PlaybackStateChanged?.Invoke(false);
        private void OnError(string message) => ErrorOccurred?.Invoke(message);

        private async Task GenerateWaveformAsync(string filePath)
        {
            _waveformCts?.Cancel();
            _waveformCts = new CancellationTokenSource();
            var ct = _waveformCts.Token;

            IsGeneratingWaveform = true;
            WaveformGenerationProgress = 0;
            WaveformProgressChanged?.Invoke(0);

            try
            {
                // Use FFmpeg to extract real waveform data
                var progressReporter = new Progress<float>(p =>
                {
                    WaveformGenerationProgress = p;
                    WaveformProgressChanged?.Invoke(p);
                });

                var result = await FFmpegWaveformExtractor.ExtractAsync(
                    filePath,
                    targetPoints: Math.Min(MaxWaveformPoints, (int)(DurationMs / 10)),
                    progress: progressReporter,
                    cancellationToken: ct);

                if (!ct.IsCancellationRequested && result.Success)
                {
                    _waveformData.Clear();
                    _waveformData.AddRange(result.Points);
                    IsWaveformPlaceholder = result.IsPlaceholder;
                    
                    // Update duration if FFmpeg gave us more accurate info
                    if (result.DurationMs > 0 && Math.Abs(result.DurationMs - DurationMs) > 100)
                    {
                        // Duration from FFmpeg differs significantly - could update
                    }
                    
                    WaveformUpdated?.Invoke();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Waveform generation failed: {ex.Message}");
            }
            finally
            {
                IsGeneratingWaveform = false;
                WaveformGenerationProgress = 1;
                WaveformProgressChanged?.Invoke(1);
            }
        }

        public IEnumerable<WaveformPoint> GetWaveformRange(double startMs, double endMs)
        {
            foreach (var point in _waveformData)
            {
                if (point.TimeMs >= startMs && point.TimeMs <= endMs)
                    yield return point;
                else if (point.TimeMs > endMs)
                    break;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Unload();
        }

        public async Task<bool> ReloadFromSettingsAsync()
        {
            if (_isDisposed) return false;
            if (string.IsNullOrEmpty(Settings.FilePath)) return true;

            var savedFilePath = Settings.FilePath;
            var savedVolume = Settings.Volume;
            var savedMuted = Settings.Muted;
            var savedLoopWithAnimation = Settings.LoopWithAnimation;
            var savedStartFrameOffset = Settings.StartFrameOffset;
            var savedShowWaveform = Settings.ShowWaveform;
            var savedWaveformColorMode = Settings.WaveformColorMode;

            if (!File.Exists(savedFilePath))
            {
                Settings.FilePath = savedFilePath;
                Settings.Volume = savedVolume;
                Settings.Muted = savedMuted;
                Settings.LoopWithAnimation = savedLoopWithAnimation;
                Settings.StartFrameOffset = savedStartFrameOffset;
                Settings.ShowWaveform = savedShowWaveform;
                Settings.WaveformColorMode = savedWaveformColorMode;
                ErrorOccurred?.Invoke($"Audio file not found: {savedFilePath}");
                return false;
            }

            bool result = await LoadAsync(savedFilePath);
            Settings.Volume = savedVolume;
            Settings.Muted = savedMuted;
            Settings.LoopWithAnimation = savedLoopWithAnimation;
            Settings.StartFrameOffset = savedStartFrameOffset;
            Settings.ShowWaveform = savedShowWaveform;
            Settings.WaveformColorMode = savedWaveformColorMode;

            if (_audioPlayer != null)
            {
                _audioPlayer.Volume = savedVolume;
                _audioPlayer.IsMuted = savedMuted;
            }
            return result;
        }
    }
}
