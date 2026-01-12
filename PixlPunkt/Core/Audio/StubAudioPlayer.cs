using System;
using System.Threading.Tasks;

namespace PixlPunkt.Core.Audio
{
    /// <summary>
    /// Stub implementation of <see cref="IAudioPlayer"/> for platforms without native audio support.
    /// </summary>
    /// <remarks>
    /// This implementation tracks state but does not actually play audio.
    /// It can be extended in the future to use cross-platform audio libraries like NAudio or BASS.
    /// </remarks>
    public sealed class StubAudioPlayer : IAudioPlayer
    {
        private bool _isDisposed;
        private double _positionMs;
        private double _volume = 1.0;
        private bool _isMuted;

        /// <inheritdoc/>
        public bool IsLoaded { get; private set; }

        /// <inheritdoc/>
        public bool IsPlaying { get; private set; }

        /// <inheritdoc/>
        public double DurationMs { get; private set; }

        /// <inheritdoc/>
        public double PositionMs => _positionMs;

        /// <inheritdoc/>
        public double Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0.0, 1.0);
        }

        /// <inheritdoc/>
        public bool IsMuted
        {
            get => _isMuted;
            set => _isMuted = value;
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
        public Task<bool> LoadAsync(string filePath)
        {
            if (_isDisposed) return Task.FromResult(false);

            Unload();

            // We can't actually load the audio, but we can mark it as "loaded"
            // and estimate duration. In the future, this could use a cross-platform
            // audio library to at least read the metadata.
            
            IsLoaded = true;
            DurationMs = 60000; // Default to 1 minute - unknown actual duration
            
            // Notify that audio "loaded" but playback won't work
            ErrorOccurred?.Invoke("Audio playback is not supported on this platform. Waveform visualization will use placeholder data.");
            
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public void Unload()
        {
            IsLoaded = false;
            IsPlaying = false;
            DurationMs = 0;
            _positionMs = 0;
        }

        /// <inheritdoc/>
        public void Play()
        {
            if (!IsLoaded || _isDisposed) return;
            
            // Can't actually play, but update state for UI consistency
            IsPlaying = true;
            PlaybackStateChanged?.Invoke(true);
            
            // Immediately "end" since we can't play
            // In future, could simulate playback with a timer
        }

        /// <inheritdoc/>
        public void Pause()
        {
            if (_isDisposed) return;
            IsPlaying = false;
            PlaybackStateChanged?.Invoke(false);
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (_isDisposed) return;
            IsPlaying = false;
            _positionMs = 0;
            PlaybackStateChanged?.Invoke(false);
            PositionChanged?.Invoke(0);
        }

        /// <inheritdoc/>
        public void Seek(double positionMs)
        {
            if (_isDisposed) return;
            _positionMs = Math.Clamp(positionMs, 0, DurationMs);
            PositionChanged?.Invoke(_positionMs);
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
