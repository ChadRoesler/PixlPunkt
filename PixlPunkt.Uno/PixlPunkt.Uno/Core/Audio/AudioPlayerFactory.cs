using System;
using System.Runtime.InteropServices;

namespace PixlPunkt.Uno.Core.Audio
{
    /// <summary>
    /// Factory for creating platform-appropriate <see cref="IAudioPlayer"/> instances.
    /// </summary>
    public static class AudioPlayerFactory
    {
        private static bool? _crossPlatformSupported;

        /// <summary>
        /// Gets whether full audio playback is supported on the current platform.
        /// </summary>
        public static bool IsPlaybackSupported
        {
            get
            {
#if WINDOWS
                return true;
#else
                // Check if cross-platform player is supported (cached)
                _crossPlatformSupported ??= CrossPlatformAudioPlayer.IsSupported();
                return _crossPlatformSupported.Value;
#endif
            }
        }

        /// <summary>
        /// Gets whether FFmpeg is available for enhanced audio features.
        /// </summary>
        public static bool IsFFmpegAvailable => FFmpegService.IsAvailable;

        /// <summary>
        /// Creates a new <see cref="IAudioPlayer"/> instance appropriate for the current platform.
        /// </summary>
        /// <returns>
        /// On Windows: <see cref="WindowsAudioPlayer"/> with full functionality.
        /// On Linux/macOS: <see cref="CrossPlatformAudioPlayer"/> using native tools.
        /// Fallback: <see cref="StubAudioPlayer"/> with limited functionality.
        /// </returns>
        public static IAudioPlayer Create()
        {
#if WINDOWS
            return new WindowsAudioPlayer();
#else
            // Try cross-platform player first
            if (CrossPlatformAudioPlayer.IsSupported())
            {
                return new CrossPlatformAudioPlayer();
            }
            
            // Fallback to stub
            return new StubAudioPlayer();
#endif
        }

        /// <summary>
        /// Gets a description of the audio backend being used.
        /// </summary>
        public static string BackendDescription
        {
            get
            {
#if WINDOWS
                return "Windows Media Foundation";
#else
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return CrossPlatformAudioPlayer.IsSupported() 
                        ? "macOS (afplay)" 
                        : "Stub (no audio playback)";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return CrossPlatformAudioPlayer.IsSupported() 
                        ? "Linux (aplay/ffplay/mpv)" 
                        : "Stub (no audio playback)";
                }
                return "Stub (no audio playback)";
#endif
            }
        }

        /// <summary>
        /// Gets a comprehensive status message about audio capabilities.
        /// </summary>
        public static string GetStatusMessage()
        {
            var playback = IsPlaybackSupported ? "Supported" : "Not supported";
            var ffmpeg = FFmpegService.IsAvailable 
                ? $"Available (v{FFmpegService.Version})" 
                : "Not found";
            
            return $"Audio Playback: {playback} ({BackendDescription})\nFFmpeg: {ffmpeg}";
        }

        /// <summary>
        /// Gets detailed information about audio support on the current platform.
        /// </summary>
        public static string GetDetailedAudioInfo()
        {
            var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux"
                : "Unknown";

            var backend = BackendDescription;
            var ffmpegStatus = FFmpegService.GetStatusMessage();
            
            return $"Platform: {platform}\n" +
                   $"Audio Backend: {backend}\n" +
                   $"Playback: {(IsPlaybackSupported ? "✓ Supported" : "✗ Not supported")}\n" +
                   $"FFmpeg: {(FFmpegService.IsAvailable ? "✓ Available" : "✗ Not found")}\n" +
                   $"Waveform Generation: {(FFmpegService.IsAvailable ? "✓ Full quality" : "⚠ Estimated")}";
        }
    }
}
