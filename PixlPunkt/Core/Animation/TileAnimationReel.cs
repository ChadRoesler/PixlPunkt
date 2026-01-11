using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Represents a single frame in a tile animation reel.
    /// Each frame references a tile position on the canvas (tx, ty).
    /// The pixels at that grid position are the frame's content.
    /// </summary>
    public sealed class ReelFrame
    {
        /// <summary>
        /// Gets or sets the tile grid X position for this frame.
        /// </summary>
        public int TileX { get; set; }

        /// <summary>
        /// Gets or sets the tile grid Y position for this frame.
        /// </summary>
        public int TileY { get; set; }

        /// <summary>
        /// Gets or sets the duration of this frame in milliseconds.
        /// If null, uses the reel's default frame time.
        /// </summary>
        public int? DurationMs { get; set; }

        /// <summary>
        /// Gets or sets the embedded pixel data for this frame (BGRA format).
        /// If null, pixels must be extracted from the document at runtime.
        /// When saved in PXPR v2+ format, this contains the actual frame pixels.
        /// </summary>
        [JsonIgnore]
        public byte[]? EmbeddedPixels { get; set; }

        /// <summary>
        /// Creates a new reel frame at the specified tile position.
        /// </summary>
        /// <param name="tileX">The tile grid X position.</param>
        /// <param name="tileY">The tile grid Y position.</param>
        /// <param name="durationMs">Optional custom duration in milliseconds.</param>
        public ReelFrame(int tileX, int tileY, int? durationMs = null)
        {
            TileX = tileX;
            TileY = tileY;
            DurationMs = durationMs;
        }

        /// <summary>
        /// Creates a new reel frame with embedded pixel data.
        /// </summary>
        /// <param name="tileX">The tile grid X position (for reference).</param>
        /// <param name="tileY">The tile grid Y position (for reference).</param>
        /// <param name="pixels">The embedded pixel data (BGRA format).</param>
        /// <param name="durationMs">Optional custom duration in milliseconds.</param>
        public ReelFrame(int tileX, int tileY, byte[] pixels, int? durationMs = null)
        {
            TileX = tileX;
            TileY = tileY;
            EmbeddedPixels = pixels;
            DurationMs = durationMs;
        }

        /// <summary>
        /// Parameterless constructor for serialization.
        /// </summary>
        public ReelFrame() { }
    }

    /// <summary>
    /// Represents a tile-based animation reel (film strip metaphor).
    /// Each reel contains a sequence of tile grid positions that form an animation.
    /// </summary>
    public sealed class TileAnimationReel : INotifyPropertyChanged
    {
        // ====================================================================
        // IDENTITY
        // ====================================================================

        /// <summary>
        /// Gets or sets the unique identifier for this reel.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = "Animation";

        /// <summary>
        /// Gets or sets the display name of this reel.
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        // ====================================================================
        // FRAME DIMENSIONS (for embedded pixel data)
        // ====================================================================

        /// <summary>
        /// Gets or sets the width of each frame in pixels.
        /// Used when frames have embedded pixel data.
        /// </summary>
        public int FrameWidth { get; set; }

        /// <summary>
        /// Gets or sets the height of each frame in pixels.
        /// Used when frames have embedded pixel data.
        /// </summary>
        public int FrameHeight { get; set; }

        /// <summary>
        /// Gets whether this reel has embedded pixel data.
        /// </summary>
        [JsonIgnore]
        public bool HasEmbeddedPixels => FrameWidth > 0 && FrameHeight > 0 &&
            Frames.Count > 0 && Frames[0].EmbeddedPixels != null;

        // ====================================================================
        // FRAMES
        // ====================================================================

        /// <summary>
        /// Gets the frames in this reel.
        /// </summary>
        public ObservableCollection<ReelFrame> Frames { get; } = [];

        /// <summary>
        /// Gets the number of frames in this reel.
        /// </summary>
        [JsonIgnore]
        public int FrameCount => Frames.Count;

        // ====================================================================
        // TIMING
        // ====================================================================

        /// <summary>
        /// Gets or sets the default frame duration in milliseconds.
        /// Individual frames can override this with their own duration.
        /// </summary>
        public int DefaultFrameTimeMs { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether the animation should loop.
        /// </summary>
        public bool Loop { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the animation should ping-pong (reverse at end).
        /// </summary>
        public bool PingPong { get; set; } = false;

        /// <summary>
        /// Gets the total duration of the reel in milliseconds.
        /// </summary>
        [JsonIgnore]
        public int TotalDurationMs
        {
            get
            {
                int total = 0;
                foreach (var frame in Frames)
                {
                    total += frame.DurationMs ?? DefaultFrameTimeMs;
                }
                return total;
            }
        }

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when the reel's frames or settings change.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Raised when a property value changes (for data binding).
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        // ====================================================================
        // CONSTRUCTORS
        // ====================================================================

        /// <summary>
        /// Creates a new empty reel.
        /// </summary>
        public TileAnimationReel()
        {
            Frames.CollectionChanged += (_, __) => Changed?.Invoke();
        }

        /// <summary>
        /// Creates a new reel with the specified name.
        /// </summary>
        /// <param name="name">The display name for the reel.</param>
        public TileAnimationReel(string name) : this()
        {
            _name = name;
        }

        // ====================================================================
        // FRAME MANIPULATION
        // ====================================================================

        /// <summary>
        /// Adds a tile position as a new frame at the end of the reel.
        /// </summary>
        /// <param name="tileX">The tile grid X position.</param>
        /// <param name="tileY">The tile grid Y position.</param>
        public void AddFrame(int tileX, int tileY)
        {
            Frames.Add(new ReelFrame(tileX, tileY));
        }

        /// <summary>
        /// Adds multiple tile positions as frames at the end of the reel.
        /// </summary>
        /// <param name="positions">The tile positions to add.</param>
        public void AddFrames(IEnumerable<(int tileX, int tileY)> positions)
        {
            foreach (var (tx, ty) in positions)
            {
                Frames.Add(new ReelFrame(tx, ty));
            }
        }

        /// <summary>
        /// Inserts a tile position as a frame at the specified index.
        /// </summary>
        /// <param name="index">The index to insert at.</param>
        /// <param name="tileX">The tile grid X position.</param>
        /// <param name="tileY">The tile grid Y position.</param>
        public void InsertFrame(int index, int tileX, int tileY)
        {
            Frames.Insert(index, new ReelFrame(tileX, tileY));
        }

        /// <summary>
        /// Removes the frame at the specified index.
        /// </summary>
        /// <param name="index">The index to remove.</param>
        public void RemoveFrameAt(int index)
        {
            if (index >= 0 && index < Frames.Count)
            {
                Frames.RemoveAt(index);
            }
        }

        /// <summary>
        /// Moves a frame from one index to another.
        /// </summary>
        /// <param name="fromIndex">The source index.</param>
        /// <param name="toIndex">The destination index.</param>
        public void MoveFrame(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= Frames.Count) return;
            if (toIndex < 0 || toIndex >= Frames.Count) return;
            if (fromIndex == toIndex) return;

            var frame = Frames[fromIndex];
            Frames.RemoveAt(fromIndex);
            Frames.Insert(toIndex, frame);
        }

        /// <summary>
        /// Sets the duration for a specific frame.
        /// </summary>
        /// <param name="index">The frame index.</param>
        /// <param name="durationMs">The duration in milliseconds, or null to use default.</param>
        public void SetFrameDuration(int index, int? durationMs)
        {
            if (index >= 0 && index < Frames.Count)
            {
                Frames[index].DurationMs = durationMs;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Gets the duration of a specific frame.
        /// </summary>
        /// <param name="index">The frame index.</param>
        /// <returns>The frame duration in milliseconds.</returns>
        public int GetFrameDuration(int index)
        {
            if (index >= 0 && index < Frames.Count)
            {
                return Frames[index].DurationMs ?? DefaultFrameTimeMs;
            }
            return DefaultFrameTimeMs;
        }

        /// <summary>
        /// Clears all frames from the reel.
        /// </summary>
        public void Clear()
        {
            Frames.Clear();
        }

        // ====================================================================
        // UTILITY
        // ====================================================================

        /// <summary>
        /// Gets the frame index at a given time position (for scrubbing).
        /// </summary>
        /// <param name="timeMs">The time position in milliseconds.</param>
        /// <returns>The frame index at that time.</returns>
        public int GetFrameAtTime(int timeMs)
        {
            if (Frames.Count == 0) return -1;

            int elapsed = 0;
            for (int i = 0; i < Frames.Count; i++)
            {
                int duration = Frames[i].DurationMs ?? DefaultFrameTimeMs;
                if (timeMs < elapsed + duration)
                {
                    return i;
                }
                elapsed += duration;
            }

            // Past the end - return last frame (or handle looping)
            return Frames.Count - 1;
        }

        /// <summary>
        /// Gets the time position at the start of a given frame.
        /// </summary>
        /// <param name="frameIndex">The frame index.</param>
        /// <returns>The time in milliseconds at the start of that frame.</returns>
        public int GetTimeAtFrame(int frameIndex)
        {
            if (frameIndex <= 0) return 0;

            int time = 0;
            for (int i = 0; i < Math.Min(frameIndex, Frames.Count); i++)
            {
                time += Frames[i].DurationMs ?? DefaultFrameTimeMs;
            }
            return time;
        }

        /// <summary>
        /// Creates a deep copy of this reel.
        /// </summary>
        /// <returns>A new reel with copied data.</returns>
        public TileAnimationReel Clone()
        {
            var clone = new TileAnimationReel(Name + " (Copy)")
            {
                DefaultFrameTimeMs = DefaultFrameTimeMs,
                Loop = Loop,
                PingPong = PingPong
            };

            foreach (var frame in Frames)
            {
                clone.Frames.Add(new ReelFrame(frame.TileX, frame.TileY, frame.DurationMs));
            }

            return clone;
        }

        /// <summary>
        /// Raises the Changed event manually.
        /// </summary>
        public void NotifyChanged() => Changed?.Invoke();

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
