using System;
using System.Threading.Tasks;

namespace PixlPunkt.Uno.Core.Audio
{
    /// <summary>
    /// Platform-agnostic audio player interface.
    /// </summary>
    /// <remarks>
    /// Implementations:
    /// <list type="bullet">
    /// <item><strong>Windows:</strong> Uses Windows.Media.Playback.MediaPlayer</item>
    /// <item><strong>Other platforms:</strong> Stub implementation (no audio)</item>
    /// </list>
    /// </remarks>
    public interface IAudioPlayer : IDisposable
    {
        /// <summary>
        /// Gets whether audio is currently loaded and ready for playback.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Gets whether audio is currently playing.
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Gets the total duration of the loaded audio in milliseconds.
        /// </summary>
        double DurationMs { get; }

        /// <summary>
        /// Gets the current playback position in milliseconds.
        /// </summary>
        double PositionMs { get; }

        /// <summary>
        /// Gets or sets the playback volume (0.0 to 1.0).
        /// </summary>
        double Volume { get; set; }

        /// <summary>
        /// Gets or sets whether audio is muted.
        /// </summary>
        bool IsMuted { get; set; }

        /// <summary>
        /// Loads an audio file from the specified path.
        /// </summary>
        /// <param name="filePath">Full path to the audio file.</param>
        /// <returns>True if loading succeeded, false otherwise.</returns>
        Task<bool> LoadAsync(string filePath);

        /// <summary>
        /// Unloads the current audio file and releases resources.
        /// </summary>
        void Unload();

        /// <summary>
        /// Starts or resumes playback.
        /// </summary>
        void Play();

        /// <summary>
        /// Pauses playback.
        /// </summary>
        void Pause();

        /// <summary>
        /// Stops playback and resets position to beginning.
        /// </summary>
        void Stop();

        /// <summary>
        /// Seeks to a specific position in the audio.
        /// </summary>
        /// <param name="positionMs">Position in milliseconds.</param>
        void Seek(double positionMs);

        /// <summary>
        /// Raised when playback position changes.
        /// </summary>
        event Action<double>? PositionChanged;

        /// <summary>
        /// Raised when playback state changes (playing/paused/stopped).
        /// </summary>
        event Action<bool>? PlaybackStateChanged;

        /// <summary>
        /// Raised when media playback ends.
        /// </summary>
        event Action? MediaEnded;

        /// <summary>
        /// Raised when an error occurs.
        /// </summary>
        event Action<string>? ErrorOccurred;
    }
}
