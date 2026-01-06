namespace PixlPunkt.Uno.Core.Export
{
    /// <summary>
    /// Configuration settings for timelapse export from history.
    /// </summary>
    public sealed class TimelapseExportSettings
    {
        /// <summary>
        /// How to handle frame timing when there are few history steps.
        /// </summary>
        public enum TimingMode
        {
            /// <summary>Fixed duration per history step.</summary>
            FixedPerStep,
            /// <summary>Target total video duration, evenly distributed.</summary>
            TargetDuration,
            /// <summary>Fixed frames per second, one history step per frame.</summary>
            FixedFps
        }

        /// <summary>
        /// How to handle transitions between history states.
        /// </summary>
        public enum TransitionMode
        {
            /// <summary>Hard cut between frames (no transition).</summary>
            Cut,
            /// <summary>Cross-fade dissolve between frames.</summary>
            Dissolve,
            /// <summary>Quick flash of white between frames.</summary>
            Flash
        }

        /// <summary>
        /// Gets or sets the timing mode for frame generation.
        /// </summary>
        public TimingMode Timing { get; set; } = TimingMode.TargetDuration;

        /// <summary>
        /// Gets or sets the duration per history step in milliseconds (for FixedPerStep mode).
        /// </summary>
        public int MillisecondsPerStep { get; set; } = 100;

        /// <summary>
        /// Gets or sets the target total duration in seconds (for TargetDuration mode).
        /// </summary>
        public int TargetDurationSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the fixed FPS (for FixedFps mode).
        /// </summary>
        public int FixedFps { get; set; } = 12;

        /// <summary>
        /// Gets or sets the minimum duration per step in milliseconds.
        /// Ensures very long histories don't create frames faster than this.
        /// </summary>
        public int MinMillisecondsPerStep { get; set; } = 33; // ~30fps max

        /// <summary>
        /// Gets or sets the transition mode between frames.
        /// </summary>
        public TransitionMode Transition { get; set; } = TransitionMode.Cut;

        /// <summary>
        /// Gets or sets the number of intermediate frames for transitions (for Dissolve mode).
        /// </summary>
        public int TransitionFrames { get; set; } = 3;

        /// <summary>
        /// Gets or sets the pixel scale factor for output.
        /// </summary>
        public int Scale { get; set; } = 1;

        /// <summary>
        /// Gets or sets whether to hold on the final frame longer.
        /// </summary>
        public bool HoldFinalFrame { get; set; } = true;

        /// <summary>
        /// Gets or sets how long to hold the final frame in milliseconds.
        /// </summary>
        public int FinalFrameHoldMs { get; set; } = 2000;

        /// <summary>
        /// Gets or sets whether to skip consecutive similar frames.
        /// </summary>
        public bool SkipSimilarFrames { get; set; } = false;

        /// <summary>
        /// Gets or sets the similarity threshold for skipping frames (0-100 percent).
        /// </summary>
        public int SimilarityThreshold { get; set; } = 95;

        /// <summary>
        /// Gets or sets the history range start (0 = beginning).
        /// </summary>
        public int RangeStart { get; set; } = 0;

        /// <summary>
        /// Gets or sets the history range end (-1 = current position).
        /// </summary>
        public int RangeEnd { get; set; } = -1;

        /// <summary>
        /// Calculates the frame duration for a given number of history steps.
        /// </summary>
        /// <param name="totalSteps">Total history steps to render.</param>
        /// <returns>Duration per frame in milliseconds.</returns>
        public int CalculateFrameDurationMs(int totalSteps)
        {
            if (totalSteps <= 0) return MillisecondsPerStep;

            return Timing switch
            {
                TimingMode.FixedPerStep => MillisecondsPerStep,
                TimingMode.TargetDuration => System.Math.Max(MinMillisecondsPerStep, (TargetDurationSeconds * 1000) / totalSteps),
                TimingMode.FixedFps => 1000 / System.Math.Max(1, FixedFps),
                _ => MillisecondsPerStep
            };
        }

        /// <summary>
        /// Estimates the total duration of the timelapse.
        /// </summary>
        /// <param name="totalSteps">Total history steps to render.</param>
        /// <returns>Estimated duration in seconds.</returns>
        public double EstimateDurationSeconds(int totalSteps)
        {
            if (totalSteps <= 0) return 0;

            int frameDuration = CalculateFrameDurationMs(totalSteps);
            double baseDuration = (totalSteps * frameDuration) / 1000.0;

            // Add transition time
            if (Transition == TransitionMode.Dissolve && TransitionFrames > 0)
            {
                // Transitions add frames between each step
                baseDuration += ((totalSteps - 1) * TransitionFrames * frameDuration) / 1000.0;
            }

            // Add final frame hold
            if (HoldFinalFrame)
            {
                baseDuration += FinalFrameHoldMs / 1000.0;
            }

            return baseDuration;
        }
    }
}
