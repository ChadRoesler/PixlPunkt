#if WINDOWS
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace PixlPunkt.Core.Audio
{
    /// <summary>
    /// Windows implementation of <see cref="IAudioPlayer"/> using Windows.Media.Playback.
    /// </summary>
    public sealed class WindowsAudioPlayer : IAudioPlayer
    {
        private MediaPlayer? _mediaPlayer;
        private MediaSource? _mediaSource;
        private bool _isDisposed;
        private double _volume = 1.0;
        private bool _isMuted;

        /// <inheritdoc/>
        public bool IsLoaded { get; private set; }

        /// <inheritdoc/>
        public bool IsPlaying => _mediaPlayer?.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;

        /// <inheritdoc/>
        public double DurationMs { get; private set; }

        /// <inheritdoc/>
        public double PositionMs => _mediaPlayer?.PlaybackSession.Position.TotalMilliseconds ?? 0;

        /// <inheritdoc/>
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0.0, 1.0);
                ApplyVolume();
            }
        }

        /// <inheritdoc/>
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                ApplyVolume();
            }
        }

        /// <inheritdoc/>
        public event Action<double>? PositionChanged;

        /// <inheritdoc/>
        public event Action<bool>? PlaybackStateChanged;

        /// <inheritdoc/>
        public event Action? MediaEnded;

        /// <inheritdoc/>
        public event Action<string>? ErrorOccurred;

        /// <inheritdoc/>
        public async Task<bool> LoadAsync(string filePath)
        {
            if (_isDisposed) return false;

            Unload();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                ErrorOccurred?.Invoke($"Audio file not found: {filePath}");
                return false;
            }

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                _mediaSource = MediaSource.CreateFromStorageFile(file);

                await _mediaSource.OpenAsync();

                DurationMs = _mediaSource.Duration?.TotalMilliseconds ?? 0;

                _mediaPlayer = new MediaPlayer
                {
                    Source = _mediaSource,
                    AutoPlay = false,
                    IsLoopingEnabled = false
                };

                _mediaPlayer.PlaybackSession.PositionChanged += OnPositionChanged;
                _mediaPlayer.MediaEnded += OnMediaEnded;
                _mediaPlayer.MediaFailed += OnMediaFailed;

                ApplyVolume();

                IsLoaded = true;
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Failed to load audio: {ex.Message}");
                Unload();
                return false;
            }
        }

        /// <inheritdoc/>
        public void Unload()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.PlaybackSession.PositionChanged -= OnPositionChanged;
                _mediaPlayer.MediaEnded -= OnMediaEnded;
                _mediaPlayer.MediaFailed -= OnMediaFailed;
                _mediaPlayer.Pause();
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            _mediaSource?.Dispose();
            _mediaSource = null;

            IsLoaded = false;
            DurationMs = 0;
        }

        /// <inheritdoc/>
        public void Play()
        {
            if (!IsLoaded || _mediaPlayer == null || _isDisposed) return;
            _mediaPlayer.Play();
            PlaybackStateChanged?.Invoke(true);
        }

        /// <inheritdoc/>
        public void Pause()
        {
            if (_mediaPlayer == null || _isDisposed) return;
            _mediaPlayer.Pause();
            PlaybackStateChanged?.Invoke(false);
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (_mediaPlayer == null || _isDisposed) return;
            _mediaPlayer.Pause();
            _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            PlaybackStateChanged?.Invoke(false);
        }

        /// <inheritdoc/>
        public void Seek(double positionMs)
        {
            if (_mediaPlayer == null || _isDisposed) return;
            positionMs = Math.Clamp(positionMs, 0, DurationMs);
            _mediaPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(positionMs);
        }

        private void ApplyVolume()
        {
            if (_mediaPlayer == null) return;
            _mediaPlayer.Volume = _isMuted ? 0 : _volume;
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            PositionChanged?.Invoke(sender.Position.TotalMilliseconds);
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            PlaybackStateChanged?.Invoke(false);
            MediaEnded?.Invoke();
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            ErrorOccurred?.Invoke($"Media playback failed: {args.ErrorMessage}");
            PlaybackStateChanged?.Invoke(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Unload();
        }
    }
}
#endif
