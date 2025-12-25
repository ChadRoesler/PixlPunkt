using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using PixlPunkt.Core.Animation;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.Animation
{
    /// <summary>
    /// Renders audio waveforms to Win2D canvases for timeline visualization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supports multiple rendering modes:
    /// <list type="bullet">
    /// <item>Mono: Single color waveform centered on track</item>
    /// <item>Stereo: Split left/right channels with different colors</item>
    /// <item>Spectrum: Color varies based on amplitude</item>
    /// </list>
    /// </para>
    /// <para>
    /// Optimized for real-time rendering during timeline scrolling by:
    /// <list type="bullet">
    /// <item>Only rendering visible portion of waveform</item>
    /// <item>Using geometry batching for efficient drawing</item>
    /// <item>Caching color brushes</item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class AudioWaveformRenderer
    {
        // ====================================================================
        // CONSTANTS
        // ====================================================================

        private const float MinWaveformHeight = 2f;

        // ====================================================================
        // COLOR SCHEMES
        // ====================================================================

        /// <summary>Default mono waveform color (teal/cyan).</summary>
        public static readonly Color MonoColor = Color.FromArgb(200, 0, 200, 200);

        /// <summary>Mono waveform fill color (semi-transparent).</summary>
        public static readonly Color MonoFillColor = Color.FromArgb(80, 0, 200, 200);

        /// <summary>Left channel color for stereo mode (cyan).</summary>
        public static readonly Color LeftChannelColor = Color.FromArgb(200, 0, 200, 255);

        /// <summary>Right channel color for stereo mode (magenta).</summary>
        public static readonly Color RightChannelColor = Color.FromArgb(200, 255, 100, 200);

        /// <summary>Background color for the audio track row.</summary>
        public static readonly Color BackgroundColor = Color.FromArgb(40, 100, 100, 100);

        /// <summary>Playhead position indicator color.</summary>
        public static readonly Color PlayheadColor = Color.FromArgb(255, 255, 100, 100);

        // ====================================================================
        // RENDERING
        // ====================================================================

        /// <summary>
        /// Renders a waveform to the drawing session.
        /// </summary>
        /// <param name="ds">The Win2D drawing session.</param>
        /// <param name="audioTrack">The audio track state containing waveform data.</param>
        /// <param name="bounds">The bounds to render within.</param>
        /// <param name="visibleStartMs">Start of visible time range in milliseconds.</param>
        /// <param name="visibleEndMs">End of visible time range in milliseconds.</param>
        /// <param name="colorMode">The waveform color mode.</param>
        public static void Render(
            CanvasDrawingSession ds,
            AudioTrackState audioTrack,
            Rect bounds,
            double visibleStartMs,
            double visibleEndMs,
            WaveformColorMode colorMode = WaveformColorMode.Mono)
        {
            if (audioTrack == null || !audioTrack.IsLoaded || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            // Draw background
            ds.FillRectangle(bounds, BackgroundColor);

            // If no waveform data yet, show loading indicator
            if (audioTrack.WaveformData.Count == 0)
            {
                DrawLoadingIndicator(ds, audioTrack, bounds);
                return;
            }

            // Calculate scaling factors
            double msPerPixel = (visibleEndMs - visibleStartMs) / bounds.Width;
            float centerY = (float)(bounds.Y + bounds.Height / 2);
            float maxAmplitude = (float)(bounds.Height / 2 - 2);

            // Get waveform points in visible range
            var visiblePoints = audioTrack.GetWaveformRange(visibleStartMs, visibleEndMs);

            switch (colorMode)
            {
                case WaveformColorMode.Mono:
                    RenderMonoWaveform(ds, visiblePoints, bounds, visibleStartMs, msPerPixel, centerY, maxAmplitude);
                    break;
                case WaveformColorMode.Stereo:
                    RenderStereoWaveform(ds, visiblePoints, bounds, visibleStartMs, msPerPixel, centerY, maxAmplitude);
                    break;
                case WaveformColorMode.Spectrum:
                    RenderSpectrumWaveform(ds, visiblePoints, bounds, visibleStartMs, msPerPixel, centerY, maxAmplitude);
                    break;
            }

            // Draw center line
            ds.DrawLine(
                (float)bounds.X, centerY,
                (float)(bounds.X + bounds.Width), centerY,
                Color.FromArgb(60, 255, 255, 255),
                0.5f);
        }

        /// <summary>
        /// Renders a mono (single color) waveform.
        /// </summary>
        private static void RenderMonoWaveform(
            CanvasDrawingSession ds,
            IEnumerable<WaveformPoint> points,
            Rect bounds,
            double startMs,
            double msPerPixel,
            float centerY,
            float maxAmplitude)
        {
            float lastX = float.NaN;
            float lastY1 = centerY;
            float lastY2 = centerY;

            foreach (var point in points)
            {
                float x = (float)(bounds.X + (point.TimeMs - startMs) / msPerPixel);

                // Skip if outside bounds
                if (x < bounds.X - 1 || x > bounds.X + bounds.Width + 1)
                    continue;

                float amplitude = point.AveragePeak * maxAmplitude;
                amplitude = Math.Max(amplitude, MinWaveformHeight);

                float y1 = centerY - amplitude;
                float y2 = centerY + amplitude;

                // Draw filled bar
                ds.FillRectangle(x, y1, 1.5f, amplitude * 2, MonoFillColor);

                // Draw outline
                if (!float.IsNaN(lastX))
                {
                    // Connect to previous point with lines for smooth appearance
                    ds.DrawLine(lastX, lastY1, x, y1, MonoColor, 1f);
                    ds.DrawLine(lastX, lastY2, x, y2, MonoColor, 1f);
                }

                lastX = x;
                lastY1 = y1;
                lastY2 = y2;
            }
        }

        /// <summary>
        /// Renders a stereo waveform with separate left/right channels.
        /// </summary>
        private static void RenderStereoWaveform(
            CanvasDrawingSession ds,
            IEnumerable<WaveformPoint> points,
            Rect bounds,
            double startMs,
            double msPerPixel,
            float centerY,
            float maxAmplitude)
        {
            foreach (var point in points)
            {
                float x = (float)(bounds.X + (point.TimeMs - startMs) / msPerPixel);

                if (x < bounds.X - 1 || x > bounds.X + bounds.Width + 1)
                    continue;

                // Left channel goes up
                float leftHeight = Math.Max(point.LeftPeak * maxAmplitude, MinWaveformHeight);
                ds.FillRectangle(x, centerY - leftHeight, 1f, leftHeight, LeftChannelColor);

                // Right channel goes down
                float rightHeight = Math.Max(point.RightPeak * maxAmplitude, MinWaveformHeight);
                ds.FillRectangle(x, centerY, 1f, rightHeight, RightChannelColor);
            }
        }

        /// <summary>
        /// Renders a spectrum-colored waveform (color varies by amplitude).
        /// </summary>
        private static void RenderSpectrumWaveform(
            CanvasDrawingSession ds,
            IEnumerable<WaveformPoint> points,
            Rect bounds,
            double startMs,
            double msPerPixel,
            float centerY,
            float maxAmplitude)
        {
            foreach (var point in points)
            {
                float x = (float)(bounds.X + (point.TimeMs - startMs) / msPerPixel);

                if (x < bounds.X - 1 || x > bounds.X + bounds.Width + 1)
                    continue;

                float amplitude = point.AveragePeak;
                float height = Math.Max(amplitude * maxAmplitude, MinWaveformHeight);

                // Color interpolation: low amplitude = blue, mid = green, high = red/orange
                var color = GetSpectrumColor(amplitude);

                ds.FillRectangle(x, centerY - height, 1.5f, height * 2, color);
            }
        }

        /// <summary>
        /// Gets a spectrum color based on amplitude (0-1).
        /// </summary>
        private static Color GetSpectrumColor(float amplitude)
        {
            // Blue -> Cyan -> Green -> Yellow -> Orange -> Red
            amplitude = Math.Clamp(amplitude, 0f, 1f);

            byte r, g, b;

            if (amplitude < 0.25f)
            {
                // Blue to Cyan
                float t = amplitude / 0.25f;
                r = 0;
                g = (byte)(t * 200);
                b = 255;
            }
            else if (amplitude < 0.5f)
            {
                // Cyan to Green
                float t = (amplitude - 0.25f) / 0.25f;
                r = 0;
                g = 200;
                b = (byte)((1 - t) * 255);
            }
            else if (amplitude < 0.75f)
            {
                // Green to Yellow
                float t = (amplitude - 0.5f) / 0.25f;
                r = (byte)(t * 255);
                g = 200;
                b = 0;
            }
            else
            {
                // Yellow to Red
                float t = (amplitude - 0.75f) / 0.25f;
                r = 255;
                g = (byte)((1 - t) * 200);
                b = 0;
            }

            return Color.FromArgb(200, r, g, b);
        }

        /// <summary>
        /// Draws a loading indicator when waveform is being generated.
        /// </summary>
        private static void DrawLoadingIndicator(
            CanvasDrawingSession ds,
            AudioTrackState audioTrack,
            Rect bounds)
        {
            float centerY = (float)(bounds.Y + bounds.Height / 2);
            float progress = audioTrack.WaveformGenerationProgress;

            // Draw progress bar
            float barWidth = (float)bounds.Width * 0.6f;
            float barHeight = 4f;
            float barX = (float)bounds.X + ((float)bounds.Width - barWidth) / 2;
            float barY = centerY - barHeight / 2;

            // Background
            ds.FillRoundedRectangle(barX, barY, barWidth, barHeight, 2, 2,
                Color.FromArgb(100, 100, 100, 100));

            // Progress
            if (progress > 0)
            {
                ds.FillRoundedRectangle(barX, barY, barWidth * progress, barHeight, 2, 2,
                    MonoColor);
            }

            // Text
            var textFormat = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
            {
                FontSize = 10,
                HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Center,
                VerticalAlignment = Microsoft.Graphics.Canvas.Text.CanvasVerticalAlignment.Center
            };

            string text = audioTrack.IsGeneratingWaveform
                ? $"Generating waveform... {(int)(progress * 100)}%"
                : "Loading audio...";

            ds.DrawText(text, bounds, Color.FromArgb(180, 200, 200, 200), textFormat);
        }

        /// <summary>
        /// Renders a playhead indicator at the specified position.
        /// </summary>
        /// <param name="ds">The drawing session.</param>
        /// <param name="bounds">The waveform bounds.</param>
        /// <param name="positionMs">Current playback position in milliseconds.</param>
        /// <param name="visibleStartMs">Start of visible range.</param>
        /// <param name="visibleEndMs">End of visible range.</param>
        public static void RenderPlayhead(
            CanvasDrawingSession ds,
            Rect bounds,
            double positionMs,
            double visibleStartMs,
            double visibleEndMs)
        {
            if (positionMs < visibleStartMs || positionMs > visibleEndMs)
                return;

            double msPerPixel = (visibleEndMs - visibleStartMs) / bounds.Width;
            float x = (float)(bounds.X + (positionMs - visibleStartMs) / msPerPixel);

            // Draw playhead line
            ds.DrawLine(x, (float)bounds.Y, x, (float)(bounds.Y + bounds.Height), PlayheadColor, 2f);

            // Draw small triangle at top
            float triangleSize = 6f;
            var pathBuilder = new CanvasPathBuilder(ds);
            pathBuilder.BeginFigure(x - triangleSize / 2, (float)bounds.Y);
            pathBuilder.AddLine(x + triangleSize / 2, (float)bounds.Y);
            pathBuilder.AddLine(x, (float)bounds.Y + triangleSize);
            pathBuilder.EndFigure(CanvasFigureLoop.Closed);

            using var geometry = CanvasGeometry.CreatePath(pathBuilder);
            ds.FillGeometry(geometry, PlayheadColor);
        }

        /// <summary>
        /// Renders the audio track filename label.
        /// </summary>
        public static void RenderLabel(
            CanvasDrawingSession ds,
            AudioTrackState audioTrack,
            Rect bounds)
        {
            if (audioTrack == null || !audioTrack.IsLoaded)
                return;

            var textFormat = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
            {
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };

            string label = $"?? {audioTrack.Settings.DisplayName}";

            // Draw with slight offset from left edge
            float x = (float)bounds.X + 4;
            float y = (float)bounds.Y + 2;

            // Background for readability
            var layout = new Microsoft.Graphics.Canvas.Text.CanvasTextLayout(ds, label, textFormat, (float)bounds.Width - 8, 16);
            var textBounds = layout.LayoutBounds;
            ds.FillRoundedRectangle(
                x - 2, y - 1,
                (float)textBounds.Width + 4, (float)textBounds.Height + 2,
                2, 2,
                Color.FromArgb(180, 0, 0, 0));

            ds.DrawText(label, x, y, Colors.White, textFormat);
        }
    }
}
