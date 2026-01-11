using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NetCoreAudio;

namespace PixlPunkt.Core.Audio
{
    /// <summary>
    /// Cross-platform audio player implementation using NetCoreAudio.
    /// Works on Windows, Linux (via aplay/ffplay), and macOS (via afplay).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This implementation uses NetCoreAudio which provides cross-platform
    /// audio playback by leveraging native command-line tools:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Windows:</strong> Uses Windows Media Player COM</item>
    /// <item><strong>macOS:</strong> Uses afplay command</item>
    /// <item><strong>Linux:</strong> Uses aplay, ffplay, or mpv</item>
    /// </list>
    /// </remarks>
    public sealed class CrossPlatformAudioPlayer : IAudioPlayer
    {
        private readonly Player _player;
        private readonly Stopwatch _playbackTimer;
        private readonly Timer? _positionTimer;
        private bool _isDisposed;
        private string? _currentFilePath;
        private double _durationMs;
        private double _seekOffset;
        private double _volume = 1.0;
        private bool _isMuted;
        private bool _isPlaying;

        /// <inheritdoc/>
        public bool IsLoaded => !string.IsNullOrEmpty(_currentFilePath);

        /// <inheritdoc/>
        public bool IsPlaying => _isPlaying;

        /// <inheritdoc/>
        public double DurationMs => _durationMs;

        /// <inheritdoc/>
        public double PositionMs
        {
            get
            {
                if (!_isPlaying) return _seekOffset;
                return _seekOffset + _playbackTimer.Elapsed.TotalMilliseconds;
            }
        }

        /// <inheritdoc/>
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0.0, 1.0);
                // NetCoreAudio doesn't support dynamic volume changes
                // Volume would need to be set before playback or use system volume
            }
        }

        /// <inheritdoc/>
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                _isMuted = value;
                if (_isMuted && _isPlaying)
                {
                    // Pause when muted to simulate mute behavior
                    _player.Pause();
                }
                else if (!_isMuted && _isPlaying)
                {
                    _player.Resume();
                }
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

        /// <summary>
        /// Creates a new cross-platform audio player instance.
        /// </summary>
        public CrossPlatformAudioPlayer()
        {
            _player = new Player();
            _playbackTimer = new Stopwatch();
            _player.PlaybackFinished += OnPlaybackFinished;

            // Timer to update position during playback
            _positionTimer = new Timer(OnPositionTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <inheritdoc/>
        public async Task<bool> LoadAsync(string filePath)
        {
            if (_isDisposed) return false;

            try
            {
                Unload();

                if (!File.Exists(filePath))
                {
                    ErrorOccurred?.Invoke($"File not found: {filePath}");
                    return false;
                }

                _currentFilePath = filePath;

                // Try to get duration using FFmpeg if available
                _durationMs = await GetAudioDurationAsync(filePath);

                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Failed to load audio: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public void Unload()
        {
            Stop();
            _currentFilePath = null;
            _durationMs = 0;
            _seekOffset = 0;
        }

        /// <inheritdoc/>
        public async void Play()
        {
            if (_isDisposed || string.IsNullOrEmpty(_currentFilePath)) return;

            try
            {
                await _player.Play(_currentFilePath);
                _isPlaying = true;
                _playbackTimer.Restart();
                _positionTimer?.Change(0, 100); // Update position every 100ms
                PlaybackStateChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Playback failed: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public void Pause()
        {
            if (_isDisposed) return;

            try
            {
                _player.Pause();
                _playbackTimer.Stop();
                _seekOffset = PositionMs;
                _isPlaying = false;
                _positionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                PlaybackStateChanged?.Invoke(false);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Pause failed: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (_isDisposed) return;

            try
            {
                _player.Stop();
                _playbackTimer.Stop();
                _playbackTimer.Reset();
                _seekOffset = 0;
                _isPlaying = false;
                _positionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                PlaybackStateChanged?.Invoke(false);
                PositionChanged?.Invoke(0);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Stop failed: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public void Seek(double positionMs)
        {
            if (_isDisposed) return;

            // NetCoreAudio doesn't support seeking directly
            // We track the position manually but actual seek requires restart
            _seekOffset = Math.Clamp(positionMs, 0, _durationMs);
            PositionChanged?.Invoke(_seekOffset);

            // If playing, we'd need to restart from the new position
            // This is a limitation of the simple cross-platform approach
            if (_isPlaying)
            {
                // Note: True seeking would require stopping and restarting
                // with an offset, which isn't directly supported
                _playbackTimer.Restart();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _positionTimer?.Dispose();
            _player.PlaybackFinished -= OnPlaybackFinished;
            
            try
            {
                _player.Stop();
            }
            catch { }

            Unload();
        }

        private void OnPlaybackFinished(object? sender, EventArgs e)
        {
            _isPlaying = false;
            _playbackTimer.Stop();
            _positionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            PlaybackStateChanged?.Invoke(false);
            MediaEnded?.Invoke();
        }

        private void OnPositionTimerTick(object? state)
        {
            if (_isPlaying)
            {
                var pos = PositionMs;
                PositionChanged?.Invoke(pos);

                // Check if we've exceeded duration
                if (pos >= _durationMs && _durationMs > 0)
                {
                    Stop();
                    MediaEnded?.Invoke();
                }
            }
        }

        /// <summary>
        /// Gets the duration of an audio file using FFprobe if available.
        /// </summary>
        private static async Task<double> GetAudioDurationAsync(string filePath)
        {
            // Try FFprobe first (most accurate)
            if (FFmpegService.IsAvailable && FFmpegService.FFprobePath != null)
            {
                try
                {
                    var mediaInfo = await FFMpegCore.FFProbe.AnalyseAsync(filePath);
                    return mediaInfo.Duration.TotalMilliseconds;
                }
                catch
                {
                    // Fall through to estimate
                }
            }

            // Estimate based on file size (very rough)
            try
            {
                var fileInfo = new FileInfo(filePath);
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                // Rough bitrate estimates
                double kbps = extension switch
                {
                    ".mp3" => 128,
                    ".wav" => 1411, // CD quality
                    ".ogg" => 128,
                    ".flac" => 800,
                    ".m4a" => 128,
                    ".aac" => 128,
                    _ => 128
                };

                // Duration (ms) = (file size in bits) / (bitrate in bits per second) * 1000
                double fileSizeKb = fileInfo.Length / 1024.0;
                double durationSeconds = (fileSizeKb * 8) / kbps;
                return durationSeconds * 1000;
            }
            catch
            {
                return 60000; // Default to 1 minute
            }
        }

        /// <summary>
        /// Checks if cross-platform audio playback is supported.
        /// </summary>
        public static bool IsSupported()
        {
            // Check if required command-line tools are available
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return true; // Windows Media Player COM is usually available
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return HasCommand("afplay"); // macOS built-in
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return HasCommand("aplay") || HasCommand("ffplay") || HasCommand("mpv");
            }

            return false;
        }

        private static bool HasCommand(string command)
        {
            try
            {
                var whichCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
                var startInfo = new ProcessStartInfo
                {
                    FileName = whichCommand,
                    Arguments = command,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit(1000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
