using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PixlPunkt.Uno.Core.Animation
{
    /// <summary>
    /// Collection of animation sub-routines in a canvas animation.
    /// Manages multiple nested tile animations with their timeline positions and transforms.
    /// </summary>
    public sealed class AnimationSubRoutineTrack
    {
        // ====================================================================
        // COLLECTION
        // ====================================================================

        /// <summary>
        /// Gets the read-only collection of sub-routines in this track.
        /// </summary>
        public IReadOnlyList<AnimationSubRoutine> SubRoutines => _subRoutines;
        private readonly ObservableCollection<AnimationSubRoutine> _subRoutines = new();

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when a sub-routine is added to the track.
        /// </summary>
        public event Action<AnimationSubRoutine>? SubRoutineAdded;

        /// <summary>
        /// Raised when a sub-routine is removed from the track.
        /// </summary>
        public event Action<AnimationSubRoutine>? SubRoutineRemoved;

        /// <summary>
        /// Raised when a sub-routine's properties change.
        /// </summary>
        public event Action<AnimationSubRoutine>? SubRoutineChanged;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public AnimationSubRoutineTrack()
        {
            _subRoutines.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is AnimationSubRoutine subRoutine)
                        {
                            subRoutine.PropertyChanged += (_, args) =>
                            {
                                SubRoutineChanged?.Invoke(subRoutine);
                            };
                            SubRoutineAdded?.Invoke(subRoutine);
                        }
                    }
                }

                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        if (item is AnimationSubRoutine subRoutine)
                        {
                            SubRoutineRemoved?.Invoke(subRoutine);
                        }
                    }
                }
            };
        }

        // ====================================================================
        // MANAGEMENT METHODS
        // ====================================================================

        /// <summary>
        /// Adds a new sub-routine to the track.
        /// </summary>
        public void Add(AnimationSubRoutine subRoutine)
        {
            if (subRoutine == null) return;
            _subRoutines.Add(subRoutine);
        }

        /// <summary>
        /// Removes a sub-routine from the track.
        /// </summary>
        public bool Remove(AnimationSubRoutine subRoutine)
        {
            return _subRoutines.Remove(subRoutine);
        }

        /// <summary>
        /// Removes the sub-routine at the specified index.
        /// </summary>
        public void RemoveAt(int index)
        {
            if (index >= 0 && index < _subRoutines.Count)
                _subRoutines.RemoveAt(index);
        }

        /// <summary>
        /// Clears all sub-routines from the track.
        /// </summary>
        public void Clear()
        {
            _subRoutines.Clear();
        }

        /// <summary>
        /// Gets the index of a sub-routine in the track.
        /// </summary>
        public int IndexOf(AnimationSubRoutine subRoutine)
        {
            return _subRoutines.IndexOf(subRoutine);
        }

        /// <summary>
        /// Moves a sub-routine to a new position in the track.
        /// </summary>
        public void Move(int oldIndex, int newIndex)
        {
            if (oldIndex >= 0 && oldIndex < _subRoutines.Count &&
                newIndex >= 0 && newIndex < _subRoutines.Count && oldIndex != newIndex)
            {
                _subRoutines.Move(oldIndex, newIndex);
            }
        }

        // ====================================================================
        // QUERY METHODS
        // ====================================================================

        /// <summary>
        /// Gets all sub-routines that are active at the specified frame.
        /// </summary>
        public IEnumerable<AnimationSubRoutine> GetActiveSubRoutinesAtFrame(int frameIndex)
        {
            return _subRoutines.Where(sr => sr.IsFrameInRange(frameIndex));
        }

        /// <summary>
        /// Gets the first enabled sub-routine that overlaps the specified frame range.
        /// </summary>
        public AnimationSubRoutine? GetSubRoutineInRange(int startFrame, int endFrame)
        {
            return _subRoutines.FirstOrDefault(sr =>
                sr.IsEnabled &&
                sr.StartFrame < endFrame &&
                sr.EndFrame > startFrame);
        }

        /// <summary>
        /// Gets sub-routines sorted by start frame.
        /// </summary>
        public IOrderedEnumerable<AnimationSubRoutine> GetSortedByStartFrame()
        {
            return _subRoutines.OrderBy(sr => sr.StartFrame);
        }

        /// <summary>
        /// Checks if any sub-routine is active (enabled and within valid timeline).
        /// </summary>
        public bool HasActiveSubRoutines()
        {
            return _subRoutines.Any(sr => sr.IsEnabled);
        }

        /// <summary>
        /// Gets the total bounding box of all sub-routines in frames.
        /// </summary>
        public (int startFrame, int endFrame)? GetTimlineBounds()
        {
            if (!_subRoutines.Any()) return null;

            int minStart = _subRoutines.Min(sr => sr.StartFrame);
            int maxEnd = _subRoutines.Max(sr => sr.EndFrame);

            return (minStart, maxEnd);
        }

        // ====================================================================
        // SERIALIZATION
        // ====================================================================

        /// <summary>
        /// Creates a deep copy of the track with all sub-routines cloned.
        /// </summary>
        public AnimationSubRoutineTrack Clone()
        {
            var clone = new AnimationSubRoutineTrack();
            foreach (var subRoutine in _subRoutines)
            {
                clone.Add(subRoutine.Clone());
            }
            return clone;
        }
    }
}
