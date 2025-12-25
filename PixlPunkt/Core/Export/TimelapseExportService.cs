using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.Export
{
    /// <summary>
    /// Provides timelapse export functionality by rendering history states as animation frames.
    /// </summary>
    public sealed class TimelapseExportService
    {
        //////////////////////////////////////////////////////////////////
        // FRAME DATA
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Represents a single rendered timelapse frame.
        /// </summary>
        public class RenderedFrame
        {
            /// <summary>Pixel data in BGRA format.</summary>
            public required byte[] Pixels { get; init; }

            /// <summary>Frame width in pixels.</summary>
            public int Width { get; init; }

            /// <summary>Frame height in pixels.</summary>
            public int Height { get; init; }

            /// <summary>Frame duration in milliseconds.</summary>
            public int DurationMs { get; init; }

            /// <summary>Frame index (0-based).</summary>
            public int Index { get; init; }

            /// <summary>History step this frame represents.</summary>
            public int HistoryStep { get; init; }

            /// <summary>Description of the history action (if available).</summary>
            public string? ActionDescription { get; init; }
        }

        //////////////////////////////////////////////////////////////////
        // RENDERING
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Renders a timelapse from history states.
        /// </summary>
        /// <param name="document">The source document.</param>
        /// <param name="settings">Export settings.</param>
        /// <param name="progress">Optional progress callback (0-1).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of rendered frames.</returns>
        public async Task<List<RenderedFrame>> RenderTimelapseAsync(
            CanvasDocument document,
            TimelapseExportSettings settings,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var frames = new List<RenderedFrame>();
            var history = document.History;

            // Determine range
            int rangeStart = Math.Max(0, settings.RangeStart);
            int rangeEnd = settings.RangeEnd < 0 ? history.TotalCount : Math.Min(settings.RangeEnd, history.TotalCount);
            int totalSteps = Math.Max(1, rangeEnd - rangeStart);

            if (totalSteps == 0)
            {
                LoggingService.Warning("Timelapse export: No history steps in range");
                return frames;
            }

            // Calculate frame timing
            int frameDurationMs = settings.CalculateFrameDurationMs(totalSteps);
            int outputW = document.PixelWidth * settings.Scale;
            int outputH = document.PixelHeight * settings.Scale;

            // Save current history position to restore later
            int originalPosition = history.AppliedCount;

            // Get timeline for descriptions
            var timeline = history.GetTimeline();

            try
            {
                // Jump to start of range
                history.JumpTo(rangeStart);
                await Task.Yield(); // Allow UI to update

                int frameIndex = 0;
                byte[]? previousFrame = null;

                for (int step = rangeStart; step <= rangeEnd; step++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Jump to this history position
                    history.JumpTo(step);

                    // Composite the document at this state
                    document.CompositeTo(document.Surface);

                    // Get current frame pixels
                    byte[] currentPixels = CaptureFrame(document, settings.Scale);

                    // Check for similar frames (skip if enabled)
                    if (settings.SkipSimilarFrames && previousFrame != null)
                    {
                        double similarity = CalculateSimilarity(previousFrame, currentPixels);
                        if (similarity >= settings.SimilarityThreshold / 100.0)
                        {
                            progress?.Report((double)(step - rangeStart + 1) / totalSteps);
                            continue;
                        }
                    }

                    // Add transition frames before this frame (if not first)
                    if (previousFrame != null && settings.Transition != TimelapseExportSettings.TransitionMode.Cut)
                    {
                        var transitionFrames = GenerateTransitionFrames(
                            previousFrame, currentPixels,
                            outputW, outputH,
                            settings, frameDurationMs, frameIndex);

                        frames.AddRange(transitionFrames);
                        frameIndex += transitionFrames.Count;
                    }

                    // Get action description
                    string? description = null;
                    if (step > 0 && step - 1 < timeline.Count)
                    {
                        description = timeline[step - 1].Description;
                    }

                    // Determine frame duration (longer for final frame if enabled)
                    int duration = frameDurationMs;
                    if (step == rangeEnd && settings.HoldFinalFrame)
                    {
                        duration = settings.FinalFrameHoldMs;
                    }

                    frames.Add(new RenderedFrame
                    {
                        Pixels = currentPixels,
                        Width = outputW,
                        Height = outputH,
                        DurationMs = duration,
                        Index = frameIndex,
                        HistoryStep = step,
                        ActionDescription = description
                    });

                    previousFrame = currentPixels;
                    frameIndex++;

                    progress?.Report((double)(step - rangeStart + 1) / totalSteps);
                }
            }
            finally
            {
                // Restore original history position
                history.JumpTo(originalPosition);
                document.CompositeTo(document.Surface);
            }

            LoggingService.Info("Rendered {FrameCount} timelapse frames from {TotalSteps} history steps ({OutputW}x{OutputH})",
                frames.Count, totalSteps, outputW, outputH);

            return frames;
        }

        //////////////////////////////////////////////////////////////////
        // PRIVATE HELPERS
        //////////////////////////////////////////////////////////////////

        private byte[] CaptureFrame(CanvasDocument document, int scale)
        {
            var surface = document.Surface;
            int srcW = surface.Width;
            int srcH = surface.Height;

            if (scale == 1)
            {
                return (byte[])surface.Pixels.Clone();
            }

            int dstW = srcW * scale;
            int dstH = srcH * scale;

            return ScalePixels(surface.Pixels, srcW, srcH, dstW, dstH);
        }

        private List<RenderedFrame> GenerateTransitionFrames(
            byte[] fromPixels, byte[] toPixels,
            int width, int height,
            TimelapseExportSettings settings,
            int frameDurationMs, int startFrameIndex)
        {
            var frames = new List<RenderedFrame>();

            switch (settings.Transition)
            {
                case TimelapseExportSettings.TransitionMode.Dissolve:
                    for (int i = 1; i <= settings.TransitionFrames; i++)
                    {
                        float t = (float)i / (settings.TransitionFrames + 1);
                        byte[] blended = BlendFrames(fromPixels, toPixels, t);

                        frames.Add(new RenderedFrame
                        {
                            Pixels = blended,
                            Width = width,
                            Height = height,
                            DurationMs = frameDurationMs,
                            Index = startFrameIndex + i - 1,
                            HistoryStep = -1, // Transition frame
                            ActionDescription = "Transition"
                        });
                    }
                    break;

                case TimelapseExportSettings.TransitionMode.Flash:
                    // Single white flash frame
                    byte[] flashFrame = new byte[width * height * 4];
                    for (int i = 0; i < flashFrame.Length; i += 4)
                    {
                        flashFrame[i + 0] = 255; // B
                        flashFrame[i + 1] = 255; // G
                        flashFrame[i + 2] = 255; // R
                        flashFrame[i + 3] = 255; // A
                    }

                    frames.Add(new RenderedFrame
                    {
                        Pixels = flashFrame,
                        Width = width,
                        Height = height,
                        DurationMs = frameDurationMs / 2, // Shorter duration for flash
                        Index = startFrameIndex,
                        HistoryStep = -1,
                        ActionDescription = "Flash"
                    });
                    break;
            }

            return frames;
        }

        private byte[] BlendFrames(byte[] from, byte[] to, float t)
        {
            if (from.Length != to.Length)
            {
                throw new ArgumentException("Frame sizes must match for blending");
            }

            byte[] result = new byte[from.Length];
            float invT = 1f - t;

            for (int i = 0; i < from.Length; i++)
            {
                result[i] = (byte)(from[i] * invT + to[i] * t);
            }

            return result;
        }

        private double CalculateSimilarity(byte[] frame1, byte[] frame2)
        {
            if (frame1.Length != frame2.Length)
            {
                return 0;
            }

            long totalDiff = 0;
            long maxDiff = frame1.Length * 255L;

            for (int i = 0; i < frame1.Length; i++)
            {
                totalDiff += Math.Abs(frame1[i] - frame2[i]);
            }

            return 1.0 - (double)totalDiff / maxDiff;
        }

        private static byte[] ScalePixels(byte[] src, int srcW, int srcH, int dstW, int dstH)
        {
            byte[] dst = new byte[dstW * dstH * 4];

            for (int y = 0; y < dstH; y++)
            {
                int sy = Math.Min(srcH - 1, y * srcH / dstH);
                for (int x = 0; x < dstW; x++)
                {
                    int sx = Math.Min(srcW - 1, x * srcW / dstW);
                    int srcIdx = (sy * srcW + sx) * 4;
                    int dstIdx = (y * dstW + x) * 4;

                    dst[dstIdx + 0] = src[srcIdx + 0];
                    dst[dstIdx + 1] = src[srcIdx + 1];
                    dst[dstIdx + 2] = src[srcIdx + 2];
                    dst[dstIdx + 3] = src[srcIdx + 3];
                }
            }

            return dst;
        }
    }
}
