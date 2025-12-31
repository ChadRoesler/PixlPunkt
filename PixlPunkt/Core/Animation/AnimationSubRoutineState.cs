using System;
using System.Collections.Generic;
using System.Linq;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Manages animation sub-routine playback state and transform interpolation.
    /// Handles stepping through active sub-routines and computing interpolated transforms.
    /// </summary>
    public sealed class AnimationSubRoutineState
    {
        /// <summary>
        /// Represents an active sub-routine with its runtime state.
        /// </summary>
        private sealed class ActiveSubRoutine
        {
            public AnimationSubRoutine SubRoutine { get; set; }
            public float NormalizedProgress { get; set; } // 0.0 to 1.0

            public ActiveSubRoutine(AnimationSubRoutine subRoutine, float progress)
            {
                SubRoutine = subRoutine;
                NormalizedProgress = progress;
            }
        }

        private readonly AnimationSubRoutineTrack _track;
        private readonly List<ActiveSubRoutine> _activeSubRoutines = new();

        /// <summary>
        /// Creates a new sub-routine playback state manager.
        /// </summary>
        /// <param name="track">The animation sub-routine track to manage.</param>
        public AnimationSubRoutineState(AnimationSubRoutineTrack track)
        {
            _track = track ?? throw new ArgumentNullException(nameof(track));
        }

        /// <summary>
        /// Updates the sub-routine state for a specific animation frame.
        /// </summary>
        /// <param name="frameIndex">The current frame index in the canvas animation.</param>
        public void UpdateForFrame(int frameIndex)
        {
            _activeSubRoutines.Clear();

            // Get all sub-routines active at this frame
            foreach (var subRoutine in _track.GetActiveSubRoutinesAtFrame(frameIndex))
            {
                float progress = subRoutine.GetNormalizedProgress(frameIndex);
                _activeSubRoutines.Add(new ActiveSubRoutine(subRoutine, progress));
            }
        }

        /// <summary>
        /// Gets the interpolated position for all active sub-routines.
        /// If multiple sub-routines are active, blends their positions.
        /// </summary>
        /// <returns>Tuple of (X, Y) position in canvas coordinates.</returns>
        public (double X, double Y) GetInterpolatedPosition()
        {
            if (_activeSubRoutines.Count == 0)
                return (0, 0);

            if (_activeSubRoutines.Count == 1)
            {
                var active = _activeSubRoutines[0];
                return active.SubRoutine.InterpolatePosition(active.NormalizedProgress);
            }

            // Blend multiple sub-routines by averaging their positions
            double totalX = 0;
            double totalY = 0;

            foreach (var active in _activeSubRoutines)
            {
                var (x, y) = active.SubRoutine.InterpolatePosition(active.NormalizedProgress);
                totalX += x;
                totalY += y;
            }

            return (totalX / _activeSubRoutines.Count, totalY / _activeSubRoutines.Count);
        }

        /// <summary>
        /// Gets the interpolated scale for all active sub-routines.
        /// If multiple sub-routines are active, blends their scales.
        /// </summary>
        public float GetInterpolatedScale()
        {
            if (_activeSubRoutines.Count == 0)
                return 1.0f;

            if (_activeSubRoutines.Count == 1)
            {
                var active = _activeSubRoutines[0];
                return active.SubRoutine.InterpolateScale(active.NormalizedProgress);
            }

            // Blend multiple sub-routines by averaging their scales
            float totalScale = 0;

            foreach (var active in _activeSubRoutines)
            {
                totalScale += active.SubRoutine.InterpolateScale(active.NormalizedProgress);
            }

            return totalScale / _activeSubRoutines.Count;
        }

        /// <summary>
        /// Gets the interpolated rotation for all active sub-routines.
        /// If multiple sub-routines are active, blends their rotations.
        /// </summary>
        public float GetInterpolatedRotation()
        {
            if (_activeSubRoutines.Count == 0)
                return 0.0f;

            if (_activeSubRoutines.Count == 1)
            {
                var active = _activeSubRoutines[0];
                return active.SubRoutine.InterpolateRotation(active.NormalizedProgress);
            }

            // Blend multiple sub-routines by averaging their rotations
            // Note: This is a simple average - for proper rotation blending,
            // you might want to use quaternions or angle normalization
            float totalRotation = 0;

            foreach (var active in _activeSubRoutines)
            {
                totalRotation += active.SubRoutine.InterpolateRotation(active.NormalizedProgress);
            }

            return totalRotation / _activeSubRoutines.Count;
        }

        /// <summary>
        /// Gets the number of sub-routines currently active.
        /// </summary>
        public int ActiveSubRoutineCount => _activeSubRoutines.Count;

        /// <summary>
        /// Checks if any sub-routines are currently active.
        /// </summary>
        public bool HasActiveSubRoutines => _activeSubRoutines.Count > 0;

        /// <summary>
        /// Gets information about all active sub-routines for debugging/visualization.
        /// </summary>
        public IEnumerable<(string Name, float Progress)> GetActiveSubRoutineInfo()
        {
            return _activeSubRoutines.Select(active => 
                (active.SubRoutine.DisplayName, active.NormalizedProgress));
        }
    }
}
