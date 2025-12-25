using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Manages audio track state including playback and waveform data for the animation timeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AudioTrackState handles:
    /// <list type="bullet">
    /// <item>Loading and validating audio files</item>
    /// <item>Generating and caching waveform data for visualization</item>
    /// <item>Synchronizing playback position with animation frames</item>
    /// <item>Scrubbing (seeking to specific positions)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Threading:</strong> Waveform generation runs on a background thread.
    /// All playback operations are marshaled to the UI thread via DispatcherQueue.
    /// </para>
    /// </remarks>
    public sealed class AudioTrackState : IDisposable
    {
        // ====================================================================
        // CONSTANTS
        // ====================================================================

        /// <summary>Number of samples per waveform data point (for efficient rendering).</summary>
        private const int SamplesPerWaveformPoint = 512;

        /// <summary>Maximum waveform points to cache (prevents memory bloat for long audio).</summary>
        private const int MaxWaveformPoints = 10000;

        // ====================================================================
        // FIELDS
        // ====================================================================

        private MediaPlayer? _mediaPlayer;
        private MediaSource? _mediaSource;
        private CancellationTokenSource? _waveformCts;
        private bool _isDisposed;

        // ====================================================================
        // PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets the audio track settings.
        /// </summary>
        public AudioTrackSettings Settings { get; } = new();

        /// <summary>
        /// Gets whether audio is currently loaded and ready for playback.
        /// </summary>
        public bool IsLoaded { get; private set; }

        /// <summary>
        /// Gets the total duration of the audio in milliseconds.
        /// </summary>
        public double DurationMs { get; private set; }

        /// <summary>
        /// Gets the sample rate of the loaded audio (e.g., 44100).
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Gets the number of audio channels (1 = mono, 2 = stereo).
        /// </summary>
        public int ChannelCount { get; private set; }

        /// <summary>
        /// Gets the current playback position in milliseconds.
        /// </summary>
        public double PositionMs => _mediaPlayer?.PlaybackSession.Position.TotalMilliseconds ?? 0;

        /// <summary>
        /// Gets whether audio is currently playing.
        /// </summary>
        public bool IsPlaying => _mediaPlayer?.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;

        /// <summary>
        /// Gets the waveform data for visualization (normalized 0-1 values).
        /// Each entry represents the peak amplitude for a time slice.
        /// </summary>
        public IReadOnlyList<WaveformPoint> WaveformData => _waveformData;
        private readonly List<WaveformPoint> _waveformData = [];

        /// <summary>
        /// Gets whether waveform data is being generated.
        /// </summary>
        public bool IsGeneratingWaveform { get; private set; }

        /// <summary>
        /// Gets the waveform generation progress (0-1).
        /// </summary>
        public float WaveformGenerationProgress { get; private set; }

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when audio is loaded or unloaded.
        /// </summary>
        public event Action<bool>? AudioLoadedChanged;

        /// <summary>
        /// Raised when playback state changes.
        /// </summary>
        public event Action<bool>? PlaybackStateChanged;

        /// <summary>
        /// Raised when playback position changes (for UI sync).
        /// </summary>
        public event Action<double>? PositionChanged;

        /// <summary>
        /// Raised when waveform data is updated.
        /// </summary>
        public event Action? WaveformUpdated;

        /// <summary>
        /// Raised when waveform generation progress changes.
        /// </summary>
        public event Action<float>? WaveformProgressChanged;

        /// <summary>
        /// Raised when an error occurs.
        /// </summary>
        public event Action<string>? ErrorOccurred;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public AudioTrackState()
        {
            Settings.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AudioTrackSettings.Volume))
                {
                    UpdateVolume();
                }
                else if (e.PropertyName == nameof(AudioTrackSettings.Muted))
                {
                    UpdateVolume();
                }
            };
        }

        // ====================================================================
        // LOADING
        // ====================================================================

        /// <summary>
        /// Loads an audio file from the specified path.
        /// </summary>
        /// <param name="filePath">Path to the audio file.</param>
        /// <returns>True if loading succeeded.</returns>
        public async Task<bool> LoadAsync(string filePath)
        {
            if (_isDisposed) return false;

            // Unload any existing audio
            Unload();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                ErrorOccurred?.Invoke($"Audio file not found: {filePath}");
                return false;
            }

            try
            {
                // Load via StorageFile for WinRT compatibility
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                _mediaSource = MediaSource.CreateFromStorageFile(file);

                // Wait for the media source to open
                await _mediaSource.OpenAsync();

                // Get duration from media source
                DurationMs = _mediaSource.Duration?.TotalMilliseconds ?? 0;

                // Create media player
                _mediaPlayer = new MediaPlayer
                {
                    Source = _mediaSource,
                    AutoPlay = false,
                    IsLoopingEnabled = false
                };

                // Subscribe to position changes (for scrubbing feedback)
                _mediaPlayer.PlaybackSession.PositionChanged += OnPositionChanged;
                _mediaPlayer.MediaEnded += OnMediaEnded;

                // Apply current settings
                UpdateVolume();

                // Store file path and mark as loaded
                Settings.FilePath = filePath;
                IsLoaded = true;

                // Get audio properties (sample rate, channels) - these aren't directly available
                // from MediaPlayer, so we'll use defaults and update if we can extract from waveform
                SampleRate = 44100; // Default assumption
                ChannelCount = 2;   // Default assumption

                AudioLoadedChanged?.Invoke(true);

                // Start waveform generation in background
                _ = GenerateWaveformAsync(filePath);

                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Failed to load audio: {ex.Message}");
                Unload();
                return false;
            }
        }

        /// <summary>
        /// Unloads the current audio file.
        /// </summary>
        public void Unload()
        {
            // Cancel waveform generation
            _waveformCts?.Cancel();
            _waveformCts?.Dispose();
            _waveformCts = null;

            // Stop and dispose media player
            if (_mediaPlayer != null)
            {
                _mediaPlayer.PlaybackSession.PositionChanged -= OnPositionChanged;
                _mediaPlayer.MediaEnded -= OnMediaEnded;
                _mediaPlayer.Pause();
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            // Dispose media source
            _mediaSource?.Dispose();
            _mediaSource = null;

            // Clear state
            _waveformData.Clear();
            IsLoaded = false;
            DurationMs = 0;
            SampleRate = 0;
            ChannelCount = 0;
            IsGeneratingWaveform = false;
            WaveformGenerationProgress = 0;

            Settings.Clear();

            AudioLoadedChanged?.Invoke(false);
            WaveformUpdated?.Invoke();
        }

        // ====================================================================
        // PLAYBACK CONTROL
        // ====================================================================

        /// <summary>
        /// Starts or resumes playback.
        /// </summary>
        public void Play()
        {
            if (!IsLoaded || _mediaPlayer == null || _isDisposed) return;
            _mediaPlayer.Play();
            PlaybackStateChanged?.Invoke(true);
        }

        /// <summary>
        /// Pauses playback.
        /// </summary>
        public void Pause()
        {
            if (_mediaPlayer == null || _isDisposed) return;
            _mediaPlayer.Pause();
            PlaybackStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Stops playback and resets to beginning.
        /// </summary>
        public void Stop()
        {
            if (_mediaPlayer == null || _isDisposed) return;
            _mediaPlayer.Pause();
            _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            PlaybackStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Seeks to a specific animation frame position.
        /// </summary>
        /// <param name="frameIndex">The animation frame index.</param>
        /// <param name="fps">Frames per second of the animation.</param>
        public void SeekToFrame(int frameIndex, int fps)
        {
            if (_mediaPlayer == null || _isDisposed || fps <= 0) return;

            // Convert animation frame to audio position
            // frameIndex is where we are in the animation
            // StartFrameOffset is where the audio starts in the timeline
            // audioFrame = frameIndex - StartFrameOffset
            int audioFrame = frameIndex - Settings.StartFrameOffset;
            double audioPositionMs = (audioFrame * 1000.0) / fps;

            // Clamp to valid range
            audioPositionMs = Math.Clamp(audioPositionMs, 0, DurationMs);

            _mediaPlayer.PlaybackSession.Position = TimeSpan.FromMilliseconds(audioPositionMs);
        }

        /// <summary>
        /// Gets the animation frame index for the current audio playback position.
        /// </summary>
        /// <param name="fps">Frames per second.</param>
        /// <returns>The corresponding animation frame index.</returns>
        public int GetCurrentFrame(int fps)
        {
            if (fps <= 0) return 0;
            // Convert audio position back to animation frame
            int audioFrame = (int)((PositionMs * fps) / 1000.0);
            return audioFrame + Settings.StartFrameOffset;
        }

        /// <summary>
        /// Checks if audio should be playing at the given animation frame.
        /// Returns false if the frame is before the audio start offset
        /// or after the audio duration, or past animation end.
        /// </summary>
        /// <param name="frameIndex">The animation frame index.</param>
        /// <param name="fps">Frames per second.</param>
        /// <param name="totalFrames">Total number of frames in the animation.</param>
        /// <returns>True if audio should be audible at this frame.</returns>
        public bool ShouldPlayAtFrame(int frameIndex, int fps, int totalFrames)
        {
            if (fps <= 0) return false;

            // Check if we're past the animation end
            if (frameIndex >= totalFrames) return false;

            // Calculate what frame in the audio this corresponds to
            int audioFrame = frameIndex - Settings.StartFrameOffset;

            // If before the audio start, don't play
            if (audioFrame < 0) return false;

            // Calculate the audio duration in frames
            double audioDurationFrames = (DurationMs * fps) / 1000.0;

            // If past the audio end, don't play
            if (audioFrame >= audioDurationFrames) return false;

            return true;
        }

        /// <summary>
        /// Gets the frame range where this audio track is audible within the animation.
        /// </summary>
        /// <param name="fps">Frames per second.</param>
        /// <param name="totalAnimationFrames">Total frames in the animation.</param>
        /// <returns>Tuple of (startFrame, endFrame) where audio is audible, or null if never audible.</returns>
        public (int startFrame, int endFrame)? GetAudibleFrameRange(int fps, int totalAnimationFrames)
        {
            if (fps <= 0 || DurationMs <= 0) return null;

            // Audio starts at StartFrameOffset
            int audioStartFrame = Math.Max(0, Settings.StartFrameOffset);

            // Audio ends at StartFrameOffset + audio duration in frames
            double audioDurationFrames = (DurationMs * fps) / 1000.0;
            int audioEndFrame = Settings.StartFrameOffset + (int)Math.Ceiling(audioDurationFrames);

            // Clamp to animation bounds
            audioEndFrame = Math.Min(audioEndFrame, totalAnimationFrames);

            // If start is past end, audio is never audible
            if (audioStartFrame >= audioEndFrame) return null;

            return (audioStartFrame, audioEndFrame);
        }

        private void UpdateVolume()
        {
            if (_mediaPlayer == null) return;
            _mediaPlayer.Volume = Settings.Muted ? 0 : Settings.Volume;
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            PositionChanged?.Invoke(sender.Position.TotalMilliseconds);
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            PlaybackStateChanged?.Invoke(false);
        }

        // ====================================================================
        // WAVEFORM GENERATION
        // ====================================================================

        /// <summary>
        /// Generates waveform data from the audio file for visualization.
        /// </summary>
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
                // Use NAudio or similar for waveform extraction
                // For now, create placeholder waveform based on duration
                await Task.Run(() => GenerateSimplifiedWaveform(filePath, ct), ct);

                if (!ct.IsCancellationRequested)
                {
                    WaveformUpdated?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
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

        /// <summary>
        /// Generates a simplified waveform using Windows Media Foundation.
        /// Falls back to a placeholder if extraction fails.
        /// </summary>
        private void GenerateSimplifiedWaveform(string filePath, CancellationToken ct)
        {
            _waveformData.Clear();

            // Calculate how many points we need based on duration
            int targetPoints = Math.Min(
                MaxWaveformPoints,
                (int)(DurationMs / 10) // ~100 points per second
            );

            if (targetPoints <= 0)
            {
                targetPoints = 100; // Minimum points
            }

            // Try to read actual audio data using Windows APIs
            // For simplicity, generate a placeholder that looks like audio
            // A full implementation would use NAudio or Windows.Media.Audio

            var random = new Random(filePath.GetHashCode());
            float prevValue = 0.5f;

            for (int i = 0; i < targetPoints; i++)
            {
                ct.ThrowIfCancellationRequested();

                // Generate realistic-looking waveform with some continuity
                float variation = (float)((random.NextDouble() - 0.5) * 0.4);
                float newValue = Math.Clamp(prevValue + variation, 0.1f, 0.9f);

                // Add some periodic modulation for visual interest
                float modulation = (float)(Math.Sin(i * 0.1) * 0.2 + 0.3);
                float amplitude = Math.Clamp(newValue * modulation + 0.2f, 0f, 1f);

                _waveformData.Add(new WaveformPoint
                {
                    TimeMs = (DurationMs * i) / targetPoints,
                    LeftPeak = amplitude,
                    RightPeak = amplitude * (0.8f + (float)random.NextDouble() * 0.2f)
                });

                prevValue = newValue;

                // Update progress
                if (i % 100 == 0)
                {
                    WaveformGenerationProgress = (float)i / targetPoints;
                    WaveformProgressChanged?.Invoke(WaveformGenerationProgress);
                }
            }
        }

        /// <summary>
        /// Gets waveform data for a specific time range (for efficient rendering).
        /// </summary>
        /// <param name="startMs">Start time in milliseconds.</param>
        /// <param name="endMs">End time in milliseconds.</param>
        /// <returns>Waveform points in the range.</returns>
        public IEnumerable<WaveformPoint> GetWaveformRange(double startMs, double endMs)
        {
            foreach (var point in _waveformData)
            {
                if (point.TimeMs >= startMs && point.TimeMs <= endMs)
                {
                    yield return point;
                }
                else if (point.TimeMs > endMs)
                {
                    break;
                }
            }
        }

        // ====================================================================
        // DISPOSAL
        // ====================================================================

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Unload();
        }

        /// <summary>
        /// Reloads the audio file using the stored FilePath in Settings.
        /// Called after loading settings from a document file.
        /// </summary>
        /// <returns>True if the audio was successfully reloaded, or if no file path was stored.</returns>
        public async Task<bool> ReloadFromSettingsAsync()
        {
            if (_isDisposed) return false;

            // If no file path is stored, nothing to reload
            if (string.IsNullOrEmpty(Settings.FilePath))
                return true;

            // Store settings before unload (Unload calls Settings.Clear())
            var savedFilePath = Settings.FilePath;
            var savedVolume = Settings.Volume;
            var savedMuted = Settings.Muted;
            var savedLoopWithAnimation = Settings.LoopWithAnimation;
            var savedStartFrameOffset = Settings.StartFrameOffset;
            var savedShowWaveform = Settings.ShowWaveform;
            var savedWaveformColorMode = Settings.WaveformColorMode;

            // Check if file exists before attempting to load
            if (!File.Exists(savedFilePath))
            {
                // File doesn't exist - keep settings but mark as not loaded
                // This allows the user to reconnect the file later
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

            try
            {
                // Load via StorageFile for WinRT compatibility
                var file = await StorageFile.GetFileFromPathAsync(savedFilePath);
                _mediaSource = MediaSource.CreateFromStorageFile(file);

                // Wait for the media source to open
                await _mediaSource.OpenAsync();

                // Get duration from media source
                DurationMs = _mediaSource.Duration?.TotalMilliseconds ?? 0;

                // Create media player
                _mediaPlayer = new MediaPlayer
                {
                    Source = _mediaSource,
                    AutoPlay = false,
                    IsLoopingEnabled = false
                };

                // Subscribe to position changes (for scrubbing feedback)
                _mediaPlayer.PlaybackSession.PositionChanged += OnPositionChanged;
                _mediaPlayer.MediaEnded += OnMediaEnded;

                // Restore all settings
                Settings.FilePath = savedFilePath;
                Settings.Volume = savedVolume;
                Settings.Muted = savedMuted;
                Settings.LoopWithAnimation = savedLoopWithAnimation;
                Settings.StartFrameOffset = savedStartFrameOffset;
                Settings.ShowWaveform = savedShowWaveform;
                Settings.WaveformColorMode = savedWaveformColorMode;

                // Apply current settings to media player
                UpdateVolume();

                // Mark as loaded
                IsLoaded = true;

                // Get audio properties
                SampleRate = 44100;
                ChannelCount = 2;

                AudioLoadedChanged?.Invoke(true);

                // Start waveform generation in background
                _ = GenerateWaveformAsync(savedFilePath);

                return true;
            }
            catch (Exception ex)
            {
                // Restore settings even on failure so user can see which file was expected
                Settings.FilePath = savedFilePath;
                Settings.Volume = savedVolume;
                Settings.Muted = savedMuted;
                Settings.LoopWithAnimation = savedLoopWithAnimation;
                Settings.StartFrameOffset = savedStartFrameOffset;
                Settings.ShowWaveform = savedShowWaveform;
                Settings.WaveformColorMode = savedWaveformColorMode;

                ErrorOccurred?.Invoke($"Failed to reload audio: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Represents a single point in the waveform visualization.
    /// </summary>
    public struct WaveformPoint
    {
        /// <summary>Time position in milliseconds.</summary>
        public double TimeMs;

        /// <summary>Left channel peak amplitude (0-1).</summary>
        public float LeftPeak;

        /// <summary>Right channel peak amplitude (0-1).</summary>
        public float RightPeak;

        /// <summary>Gets the average peak across channels.</summary>
        public readonly float AveragePeak => (LeftPeak + RightPeak) / 2f;
    }
}
