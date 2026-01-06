using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PixlPunkt.Uno.Core.Animation
{
    /// <summary>
    /// Settings for an audio track in the animation timeline.
    /// Stores file path reference and playback configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Audio tracks provide reference audio for animation timing (lip-sync, sound effects, music).
    /// The audio file is referenced by path, not embedded, to keep project files small.
    /// </para>
    /// <para>
    /// <strong>Supported Formats:</strong> MP3, WAV, OGG, FLAC (via Windows Media Foundation).
    /// </para>
    /// </remarks>
    public sealed class AudioTrackSettings : INotifyPropertyChanged
    {
        // ====================================================================
        // FILE REFERENCE
        // ====================================================================

        private string _filePath = string.Empty;

        /// <summary>
        /// Gets or sets the path to the audio file.
        /// </summary>
        /// <remarks>
        /// Can be absolute or relative to the project file.
        /// Empty string indicates no audio loaded.
        /// </remarks>
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value ?? string.Empty);
        }

        /// <summary>
        /// Gets whether an audio file is loaded.
        /// </summary>
        public bool HasAudio => !string.IsNullOrEmpty(_filePath);

        /// <summary>
        /// Gets the audio file name (without path) for display.
        /// </summary>
        public string DisplayName => HasAudio
            ? System.IO.Path.GetFileName(_filePath)
            : "(No audio)";

        // ====================================================================
        // PLAYBACK SETTINGS
        // ====================================================================

        private float _volume = 1.0f;

        /// <summary>
        /// Gets or sets the playback volume (0.0 to 1.0).
        /// </summary>
        public float Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, Math.Clamp(value, 0f, 1f));
        }

        private bool _muted;

        /// <summary>
        /// Gets or sets whether the audio track is muted.
        /// </summary>
        public bool Muted
        {
            get => _muted;
            set => SetProperty(ref _muted, value);
        }

        private bool _loopWithAnimation = true;

        /// <summary>
        /// Gets or sets whether audio loops when animation loops.
        /// </summary>
        public bool LoopWithAnimation
        {
            get => _loopWithAnimation;
            set => SetProperty(ref _loopWithAnimation, value);
        }

        // ====================================================================
        // TIMING / OFFSET
        // ====================================================================

        private int _startFrameOffset;

        /// <summary>
        /// Gets or sets the start offset in frames.
        /// Positive values delay audio start; negative values skip into audio.
        /// </summary>
        public int StartFrameOffset
        {
            get => _startFrameOffset;
            set => SetProperty(ref _startFrameOffset, value);
        }

        // ====================================================================
        // WAVEFORM DISPLAY
        // ====================================================================

        private bool _showWaveform = true;

        /// <summary>
        /// Gets or sets whether to show the waveform visualization.
        /// </summary>
        public bool ShowWaveform
        {
            get => _showWaveform;
            set => SetProperty(ref _showWaveform, value);
        }

        private WaveformColorMode _waveformColorMode = WaveformColorMode.Mono;

        /// <summary>
        /// Gets or sets the waveform color mode.
        /// </summary>
        public WaveformColorMode WaveformColorMode
        {
            get => _waveformColorMode;
            set => SetProperty(ref _waveformColorMode, value);
        }

        // ====================================================================
        // METHODS
        // ====================================================================

        /// <summary>
        /// Clears the audio track settings.
        /// </summary>
        public void Clear()
        {
            FilePath = string.Empty;
            Volume = 1.0f;
            Muted = false;
            LoopWithAnimation = true;
            StartFrameOffset = 0;
            ShowWaveform = true;
            WaveformColorMode = WaveformColorMode.Mono;
        }

        /// <summary>
        /// Creates a deep copy of the settings.
        /// </summary>
        public AudioTrackSettings Clone()
        {
            return new AudioTrackSettings
            {
                FilePath = FilePath,
                Volume = Volume,
                Muted = Muted,
                LoopWithAnimation = LoopWithAnimation,
                StartFrameOffset = StartFrameOffset,
                ShowWaveform = ShowWaveform,
                WaveformColorMode = WaveformColorMode
            };
        }

        // ====================================================================
        // INOTIFYPROPERTYCHANGED
        // ====================================================================

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Waveform visualization color modes.
    /// </summary>
    public enum WaveformColorMode
    {
        /// <summary>Single color waveform.</summary>
        Mono,

        /// <summary>Stereo split (left = one color, right = another).</summary>
        Stereo,

        /// <summary>Color mapped by frequency/amplitude.</summary>
        Spectrum
    }
}
