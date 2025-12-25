using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Manages a collection of audio tracks for the animation timeline.
    /// Supports multiple audio tracks that can be collapsed/expanded in the UI.
    /// </summary>
    public sealed class AudioTracksCollection : IEnumerable<AudioTrackState>, IDisposable
    {
        // ====================================================================
        // FIELDS
        // ====================================================================

        private readonly List<AudioTrackState> _tracks = [];
        private bool _isDisposed;

        // ====================================================================
        // PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets the number of audio tracks in the collection.
        /// </summary>
        public int Count => _tracks.Count;

        /// <summary>
        /// Gets whether any audio tracks are loaded.
        /// </summary>
        public bool HasLoadedTracks => _tracks.Any(t => t.IsLoaded);

        /// <summary>
        /// Gets the number of loaded audio tracks.
        /// </summary>
        public int LoadedCount => _tracks.Count(t => t.IsLoaded);

        /// <summary>
        /// Gets or sets whether the audio tracks section is collapsed in the UI.
        /// </summary>
        public bool IsCollapsed { get; set; }

        /// <summary>
        /// Gets an audio track by index.
        /// </summary>
        public AudioTrackState this[int index] => _tracks[index];

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when a track is added to the collection.
        /// </summary>
        public event Action<AudioTrackState>? TrackAdded;

        /// <summary>
        /// Raised when a track is removed from the collection.
        /// </summary>
        public event Action<AudioTrackState>? TrackRemoved;

        /// <summary>
        /// Raised when a track's loaded state changes.
        /// </summary>
        public event Action<AudioTrackState, bool>? TrackLoadedChanged;

        /// <summary>
        /// Raised when the collection changes (add, remove, reorder).
        /// </summary>
        public event Action? CollectionChanged;

        /// <summary>
        /// Raised when the collapsed state changes.
        /// </summary>
        public event Action<bool>? CollapsedStateChanged;

        // ====================================================================
        // TRACK MANAGEMENT
        // ====================================================================

        /// <summary>
        /// Creates and adds a new audio track to the collection.
        /// </summary>
        /// <returns>The newly created audio track.</returns>
        public AudioTrackState AddTrack()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AudioTracksCollection));

            var track = new AudioTrackState();
            track.AudioLoadedChanged += OnTrackLoadedChanged;
            _tracks.Add(track);

            TrackAdded?.Invoke(track);
            CollectionChanged?.Invoke();

            return track;
        }

        /// <summary>
        /// Removes an audio track from the collection.
        /// </summary>
        /// <param name="track">The track to remove.</param>
        /// <returns>True if the track was removed.</returns>
        public bool RemoveTrack(AudioTrackState track)
        {
            if (_isDisposed) return false;

            if (_tracks.Remove(track))
            {
                track.AudioLoadedChanged -= OnTrackLoadedChanged;
                track.Dispose();

                TrackRemoved?.Invoke(track);
                CollectionChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes an audio track at the specified index.
        /// </summary>
        /// <param name="index">The index of the track to remove.</param>
        public void RemoveTrackAt(int index)
        {
            if (_isDisposed) return;
            if (index < 0 || index >= _tracks.Count) return;

            var track = _tracks[index];
            track.AudioLoadedChanged -= OnTrackLoadedChanged;
            _tracks.RemoveAt(index);
            track.Dispose();

            TrackRemoved?.Invoke(track);
            CollectionChanged?.Invoke();
        }

        /// <summary>
        /// Moves an audio track from one index to another.
        /// </summary>
        /// <param name="fromIndex">The current index of the track.</param>
        /// <param name="toIndex">The target index.</param>
        public void MoveTrack(int fromIndex, int toIndex)
        {
            if (_isDisposed) return;
            if (fromIndex < 0 || fromIndex >= _tracks.Count) return;
            if (toIndex < 0 || toIndex >= _tracks.Count) return;
            if (fromIndex == toIndex) return;

            var track = _tracks[fromIndex];
            _tracks.RemoveAt(fromIndex);
            _tracks.Insert(toIndex, track);

            CollectionChanged?.Invoke();
        }

        /// <summary>
        /// Gets the index of the specified track.
        /// </summary>
        public int IndexOf(AudioTrackState track) => _tracks.IndexOf(track);

        /// <summary>
        /// Clears all audio tracks from the collection.
        /// </summary>
        public void Clear()
        {
            if (_isDisposed) return;

            foreach (var track in _tracks)
            {
                track.AudioLoadedChanged -= OnTrackLoadedChanged;
                track.Dispose();
            }
            _tracks.Clear();

            CollectionChanged?.Invoke();
        }

        /// <summary>
        /// Gets all loaded tracks.
        /// </summary>
        public IEnumerable<AudioTrackState> GetLoadedTracks()
        {
            return _tracks.Where(t => t.IsLoaded);
        }

        /// <summary>
        /// Toggles the collapsed state of the audio tracks section.
        /// </summary>
        public void ToggleCollapsed()
        {
            IsCollapsed = !IsCollapsed;
            CollapsedStateChanged?.Invoke(IsCollapsed);
        }

        /// <summary>
        /// Sets the collapsed state of the audio tracks section.
        /// </summary>
        public void SetCollapsed(bool collapsed)
        {
            if (IsCollapsed != collapsed)
            {
                IsCollapsed = collapsed;
                CollapsedStateChanged?.Invoke(IsCollapsed);
            }
        }

        private void OnTrackLoadedChanged(bool loaded)
        {
            // Find which track changed (we need to iterate since the event doesn't tell us)
            // This could be optimized with a dictionary but with few tracks it's fine
            foreach (var track in _tracks)
            {
                if (track.IsLoaded == loaded)
                {
                    TrackLoadedChanged?.Invoke(track, loaded);
                    break;
                }
            }
        }

        // ====================================================================
        // PLAYBACK CONTROL
        // ====================================================================

        /// <summary>
        /// Plays all loaded and unmuted audio tracks.
        /// </summary>
        public void PlayAll()
        {
            foreach (var track in _tracks)
            {
                if (track.IsLoaded && !track.Settings.Muted)
                {
                    track.Play();
                }
            }
        }

        /// <summary>
        /// Pauses all audio tracks.
        /// </summary>
        public void PauseAll()
        {
            foreach (var track in _tracks)
            {
                track.Pause();
            }
        }

        /// <summary>
        /// Stops all audio tracks.
        /// </summary>
        public void StopAll()
        {
            foreach (var track in _tracks)
            {
                track.Stop();
            }
        }

        /// <summary>
        /// Seeks all audio tracks to the specified frame.
        /// </summary>
        public void SeekAllToFrame(int frameIndex, int fps)
        {
            foreach (var track in _tracks)
            {
                if (track.IsLoaded)
                {
                    track.SeekToFrame(frameIndex, fps);
                }
            }
        }

        /// <summary>
        /// Checks if any audio track should play at the given frame.
        /// </summary>
        public bool AnyTrackShouldPlayAtFrame(int frameIndex, int fps, int totalFrames)
        {
            return _tracks.Any(t => t.IsLoaded && !t.Settings.Muted && t.ShouldPlayAtFrame(frameIndex, fps, totalFrames));
        }

        /// <summary>
        /// Updates audio playback state for all tracks based on the current frame.
        /// </summary>
        public void UpdatePlaybackStateForFrame(int frameIndex, int fps, int totalFrames, bool isAnimationPlaying)
        {
            foreach (var track in _tracks)
            {
                if (!track.IsLoaded || track.Settings.Muted) continue;

                bool shouldPlay = track.ShouldPlayAtFrame(frameIndex, fps, totalFrames);
                bool isPlaying = track.IsPlaying;

                if (isAnimationPlaying)
                {
                    if (shouldPlay && !isPlaying)
                    {
                        track.SeekToFrame(frameIndex, fps);
                        track.Play();
                    }
                    else if (!shouldPlay && isPlaying)
                    {
                        track.Pause();
                    }
                }
                else
                {
                    if (isPlaying)
                    {
                        track.Pause();
                    }
                }
            }
        }

        // ====================================================================
        // PERSISTENCE SUPPORT
        // ====================================================================

        /// <summary>
        /// Reloads all audio files from their stored file paths.
        /// Called after loading a document to restore audio playback state.
        /// </summary>
        /// <returns>True if all tracks with file paths were successfully reloaded.</returns>
        public async System.Threading.Tasks.Task<bool> ReloadAllFromSettingsAsync()
        {
            if (_isDisposed) return false;

            bool allSuccess = true;
            foreach (var track in _tracks)
            {
                if (!string.IsNullOrEmpty(track.Settings.FilePath))
                {
                    var success = await track.ReloadFromSettingsAsync();
                    if (!success)
                        allSuccess = false;
                }
            }
            return allSuccess;
        }

        // ====================================================================
        // IENUMERABLE IMPLEMENTATION
        // ====================================================================

        public IEnumerator<AudioTrackState> GetEnumerator() => _tracks.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ====================================================================
        // DISPOSAL
        // ====================================================================

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (var track in _tracks)
            {
                track.AudioLoadedChanged -= OnTrackLoadedChanged;
                track.Dispose();
            }
            _tracks.Clear();
        }
    }
}
