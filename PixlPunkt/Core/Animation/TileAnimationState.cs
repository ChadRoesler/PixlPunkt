using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using PixlPunkt.Core.Document;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Playback state for animations.
    /// </summary>
    public enum PlaybackState
    {
        /// <summary>Animation is stopped at frame 0.</summary>
        Stopped,

        /// <summary>Animation is playing forward.</summary>
        Playing,

        /// <summary>Animation is paused at current frame.</summary>
        Paused
    }

    /// <summary>
    /// Playback direction for ping-pong animations.
    /// </summary>
    public enum PlaybackDirection
    {
        /// <summary>Playing forward.</summary>
        Forward,

        /// <summary>Playing backward (ping-pong mode).</summary>
        Backward
    }

    /// <summary>
    /// Manages tile animation state for a document.
    /// Handles reel management, playback, and frame selection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each <see cref="CanvasDocument"/> has its own TileAnimationState that stores
    /// all reels and playback state. This is serialized with the .pxp file.
    /// </para>
    /// </remarks>
    public sealed class TileAnimationState
    {
        // ====================================================================
        // REELS
        // ====================================================================

        /// <summary>
        /// Gets the collection of animation reels.
        /// </summary>
        public ObservableCollection<TileAnimationReel> Reels { get; } = [];

        /// <summary>
        /// Gets or sets the currently selected reel.
        /// </summary>
        [JsonIgnore]
        public TileAnimationReel? SelectedReel { get; private set; }

        /// <summary>
        /// Gets the index of the selected reel, or -1 if none.
        /// </summary>
        [JsonIgnore]
        public int SelectedReelIndex => SelectedReel != null ? Reels.IndexOf(SelectedReel) : -1;

        // ====================================================================
        // PLAYBACK STATE
        // ====================================================================

        /// <summary>
        /// Gets or sets the current playback state.
        /// </summary>
        [JsonIgnore]
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        /// <summary>
        /// Gets or sets the current playback direction.
        /// </summary>
        [JsonIgnore]
        public PlaybackDirection Direction { get; private set; } = PlaybackDirection.Forward;

        /// <summary>
        /// Gets or sets the current frame index in the selected reel.
        /// </summary>
        [JsonIgnore]
        public int CurrentFrameIndex { get; private set; }

        /// <summary>
        /// Gets the current tile grid position being displayed, or (-1, -1) if none.
        /// </summary>
        [JsonIgnore]
        public (int TileX, int TileY) CurrentTilePosition
        {
            get
            {
                if (SelectedReel == null || CurrentFrameIndex < 0 || CurrentFrameIndex >= SelectedReel.FrameCount)
                    return (-1, -1);
                var frame = SelectedReel.Frames[CurrentFrameIndex];
                return (frame.TileX, frame.TileY);
            }
        }

        /// <summary>
        /// Gets whether playback is currently active.
        /// </summary>
        [JsonIgnore]
        public bool IsPlaying => PlaybackState == PlaybackState.Playing;

        // ====================================================================
        // ONION SKINNING
        // ====================================================================

        /// <summary>
        /// Gets or sets whether onion skinning is enabled.
        /// </summary>
        public bool OnionSkinEnabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the number of frames to show before the current frame.
        /// </summary>
        public int OnionSkinFramesBefore { get; set; } = 2;

        /// <summary>
        /// Gets or sets the number of frames to show after the current frame.
        /// </summary>
        public int OnionSkinFramesAfter { get; set; } = 1;

        /// <summary>
        /// Gets or sets the opacity for onion skin frames (0-1).
        /// </summary>
        public float OnionSkinOpacity { get; set; } = 0.3f;

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when the selected reel changes.
        /// </summary>
        public event Action<TileAnimationReel?>? SelectedReelChanged;

        /// <summary>
        /// Raised when the current frame changes.
        /// </summary>
        public event Action<int>? CurrentFrameChanged;

        /// <summary>
        /// Raised when playback state changes.
        /// </summary>
        public event Action<PlaybackState>? PlaybackStateChanged;

        /// <summary>
        /// Raised when the reels collection changes.
        /// </summary>
        public event Action? ReelsChanged;

        /// <summary>
        /// Raised when onion skin settings change.
        /// </summary>
        public event Action? OnionSkinSettingsChanged;

        // ====================================================================
        // PLAYBACK TIMER
        // ====================================================================

        private DispatcherTimer? _playbackTimer;
        private DateTime _frameStartTime;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        /// <summary>
        /// Creates a new tile animation state.
        /// </summary>
        public TileAnimationState()
        {
            Reels.CollectionChanged += (_, __) => ReelsChanged?.Invoke();
        }

        // ====================================================================
        // REEL MANAGEMENT
        // ====================================================================

        /// <summary>
        /// Adds a new empty reel.
        /// </summary>
        /// <param name="name">Optional name for the reel.</param>
        /// <returns>The created reel.</returns>
        public TileAnimationReel AddReel(string? name = null)
        {
            var reel = new TileAnimationReel(name ?? $"Animation {Reels.Count + 1}");
            Reels.Add(reel);

            // Auto-select if it's the first reel
            if (Reels.Count == 1)
            {
                SelectReel(reel);
            }

            return reel;
        }

        /// <summary>
        /// Removes a reel.
        /// </summary>
        /// <param name="reel">The reel to remove.</param>
        public void RemoveReel(TileAnimationReel reel)
        {
            if (SelectedReel == reel)
            {
                Stop();
                SelectReel(null);
            }
            Reels.Remove(reel);
        }

        /// <summary>
        /// Duplicates a reel.
        /// </summary>
        /// <param name="reel">The reel to duplicate.</param>
        /// <returns>The new duplicated reel.</returns>
        public TileAnimationReel DuplicateReel(TileAnimationReel reel)
        {
            var clone = reel.Clone();
            Reels.Add(clone);
            return clone;
        }

        /// <summary>
        /// Selects a reel for editing and playback.
        /// </summary>
        /// <param name="reel">The reel to select, or null to deselect.</param>
        public void SelectReel(TileAnimationReel? reel)
        {
            if (SelectedReel == reel) return;

            Stop();
            SelectedReel = reel;
            CurrentFrameIndex = 0;

            SelectedReelChanged?.Invoke(reel);
            CurrentFrameChanged?.Invoke(CurrentFrameIndex);
        }

        /// <summary>
        /// Selects a reel by index.
        /// </summary>
        /// <param name="index">The index to select.</param>
        public void SelectReelByIndex(int index)
        {
            if (index >= 0 && index < Reels.Count)
            {
                SelectReel(Reels[index]);
            }
            else
            {
                SelectReel(null);
            }
        }

        // ====================================================================
        // FRAME NAVIGATION
        // ====================================================================

        /// <summary>
        /// Sets the current frame index.
        /// </summary>
        /// <param name="index">The frame index to set.</param>
        public void SetCurrentFrame(int index)
        {
            if (SelectedReel == null) return;

            int maxIndex = SelectedReel.FrameCount - 1;
            int newIndex = Math.Clamp(index, 0, Math.Max(0, maxIndex));

            if (CurrentFrameIndex != newIndex)
            {
                CurrentFrameIndex = newIndex;
                CurrentFrameChanged?.Invoke(CurrentFrameIndex);
            }
        }

        /// <summary>
        /// Advances to the next frame.
        /// </summary>
        public void NextFrame()
        {
            if (SelectedReel == null || SelectedReel.FrameCount == 0) return;

            int nextIndex = CurrentFrameIndex + 1;
            if (nextIndex >= SelectedReel.FrameCount)
            {
                if (SelectedReel.Loop)
                {
                    nextIndex = 0;
                }
                else
                {
                    return; // Stay at last frame
                }
            }

            SetCurrentFrame(nextIndex);
        }

        /// <summary>
        /// Goes to the previous frame.
        /// </summary>
        public void PreviousFrame()
        {
            if (SelectedReel == null || SelectedReel.FrameCount == 0) return;

            int prevIndex = CurrentFrameIndex - 1;
            if (prevIndex < 0)
            {
                if (SelectedReel.Loop)
                {
                    prevIndex = SelectedReel.FrameCount - 1;
                }
                else
                {
                    return; // Stay at first frame
                }
            }

            SetCurrentFrame(prevIndex);
        }

        /// <summary>
        /// Goes to the first frame.
        /// </summary>
        public void FirstFrame()
        {
            SetCurrentFrame(0);
        }

        /// <summary>
        /// Goes to the last frame.
        /// </summary>
        public void LastFrame()
        {
            if (SelectedReel == null) return;
            SetCurrentFrame(SelectedReel.FrameCount - 1);
        }

        // ====================================================================
        // PLAYBACK CONTROL
        // ====================================================================

        /// <summary>
        /// Starts or resumes playback.
        /// </summary>
        public void Play()
        {
            if (SelectedReel == null || SelectedReel.FrameCount == 0) return;
            if (PlaybackState == PlaybackState.Playing) return;

            PlaybackState = PlaybackState.Playing;
            Direction = PlaybackDirection.Forward;
            _frameStartTime = DateTime.Now;

            EnsureTimer();
            _playbackTimer!.Start();

            PlaybackStateChanged?.Invoke(PlaybackState);
        }

        /// <summary>
        /// Pauses playback at current frame.
        /// </summary>
        public void Pause()
        {
            if (PlaybackState != PlaybackState.Playing) return;

            PlaybackState = PlaybackState.Paused;
            _playbackTimer?.Stop();

            PlaybackStateChanged?.Invoke(PlaybackState);
        }

        /// <summary>
        /// Stops playback and returns to frame 0.
        /// </summary>
        public void Stop()
        {
            _playbackTimer?.Stop();

            PlaybackState = PlaybackState.Stopped;
            Direction = PlaybackDirection.Forward;
            CurrentFrameIndex = 0;

            PlaybackStateChanged?.Invoke(PlaybackState);
            CurrentFrameChanged?.Invoke(CurrentFrameIndex);
        }

        /// <summary>
        /// Toggles between play and pause.
        /// </summary>
        public void TogglePlayPause()
        {
            if (PlaybackState == PlaybackState.Playing)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }

        private void EnsureTimer()
        {
            if (_playbackTimer == null)
            {
                _playbackTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS check rate
                };
                _playbackTimer.Tick += OnPlaybackTick;
            }
        }

        private void OnPlaybackTick(object? sender, object e)
        {
            if (SelectedReel == null || PlaybackState != PlaybackState.Playing) return;

            int frameDuration = SelectedReel.GetFrameDuration(CurrentFrameIndex);
            var elapsed = (DateTime.Now - _frameStartTime).TotalMilliseconds;

            if (elapsed >= frameDuration)
            {
                AdvancePlayback();
                _frameStartTime = DateTime.Now;
            }
        }

        private void AdvancePlayback()
        {
            if (SelectedReel == null) return;

            int frameCount = SelectedReel.FrameCount;
            if (frameCount == 0) return;

            if (SelectedReel.PingPong)
            {
                // Ping-pong mode
                if (Direction == PlaybackDirection.Forward)
                {
                    if (CurrentFrameIndex >= frameCount - 1)
                    {
                        Direction = PlaybackDirection.Backward;
                        SetCurrentFrame(CurrentFrameIndex - 1);
                    }
                    else
                    {
                        SetCurrentFrame(CurrentFrameIndex + 1);
                    }
                }
                else
                {
                    if (CurrentFrameIndex <= 0)
                    {
                        if (!SelectedReel.Loop)
                        {
                            Stop();
                            return;
                        }
                        Direction = PlaybackDirection.Forward;
                        SetCurrentFrame(1);
                    }
                    else
                    {
                        SetCurrentFrame(CurrentFrameIndex - 1);
                    }
                }
            }
            else
            {
                // Normal mode
                int nextIndex = CurrentFrameIndex + 1;
                if (nextIndex >= frameCount)
                {
                    if (SelectedReel.Loop)
                    {
                        SetCurrentFrame(0);
                    }
                    else
                    {
                        Stop();
                    }
                }
                else
                {
                    SetCurrentFrame(nextIndex);
                }
            }
        }

        // ====================================================================
        // ONION SKIN
        // ====================================================================

        /// <summary>
        /// Sets onion skin settings.
        /// </summary>
        public void SetOnionSkin(bool enabled, int framesBefore = 2, int framesAfter = 1, float opacity = 0.3f)
        {
            OnionSkinEnabled = enabled;
            OnionSkinFramesBefore = framesBefore;
            OnionSkinFramesAfter = framesAfter;
            OnionSkinOpacity = opacity;
            OnionSkinSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Gets the tile positions for onion skin display.
        /// </summary>
        /// <returns>List of ((tileX, tileY), opacity) tuples for frames to overlay.</returns>
        public List<((int tileX, int tileY) position, float opacity)> GetOnionSkinTiles()
        {
            var result = new List<((int, int), float)>();

            if (!OnionSkinEnabled || SelectedReel == null) return result;

            // Frames before
            for (int i = 1; i <= OnionSkinFramesBefore; i++)
            {
                int idx = CurrentFrameIndex - i;
                if (idx >= 0 && idx < SelectedReel.FrameCount)
                {
                    float opacity = OnionSkinOpacity * (1f - (float)i / (OnionSkinFramesBefore + 1));
                    var frame = SelectedReel.Frames[idx];
                    result.Add(((frame.TileX, frame.TileY), opacity));
                }
            }

            // Frames after
            for (int i = 1; i <= OnionSkinFramesAfter; i++)
            {
                int idx = CurrentFrameIndex + i;
                if (idx >= 0 && idx < SelectedReel.FrameCount)
                {
                    float opacity = OnionSkinOpacity * (1f - (float)i / (OnionSkinFramesAfter + 1));
                    var frame = SelectedReel.Frames[idx];
                    result.Add(((frame.TileX, frame.TileY), opacity));
                }
            }

            return result;
        }

        // ====================================================================
        // CLEANUP
        // ====================================================================

        /// <summary>
        /// Stops playback and releases resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
            _playbackTimer?.Stop();
            _playbackTimer = null;
        }
    }
}
