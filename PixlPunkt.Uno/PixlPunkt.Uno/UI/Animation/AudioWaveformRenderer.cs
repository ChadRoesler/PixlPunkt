using System;
using System.Collections.Generic;
using Microsoft.UI;
using PixlPunkt.Uno.Core.Animation;
using PixlPunkt.Uno.Core.Audio;
using SkiaSharp;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.Uno.UI.Animation
{
    /// <summary>
    /// Renders audio waveforms using SkiaSharp for timeline visualization.
    /// </summary>
    public sealed class AudioWaveformRenderer
    {
        // ====================================================================
        // CONSTANTS
        // ====================================================================

        private const float MinWaveformHeight = 2f;

        // ====================================================================
        // COLOR SCHEMES
        // ====================================================================

        public static readonly Color MonoColor = Color.FromArgb(200, 0, 200, 200);
        public static readonly Color MonoFillColor = Color.FromArgb(80, 0, 200, 200);
        public static readonly Color LeftChannelColor = Color.FromArgb(200, 0, 200, 255);
        public static readonly Color RightChannelColor = Color.FromArgb(200, 255, 100, 200);
        public static readonly Color BackgroundColor = Color.FromArgb(40, 100, 100, 100);
        public static readonly Color PlayheadColor = Color.FromArgb(255, 255, 100, 100);

        // ====================================================================
        // RENDERING
        // ====================================================================

        /// <summary>
        /// Renders a waveform to the SKCanvas.
        /// </summary>
        public static void Render(
            SKCanvas canvas,
            AudioTrackState audioTrack,
            Rect bounds,
            double visibleStartMs,
            double visibleEndMs,
            WaveformColorMode colorMode = WaveformColorMode.Mono)
        {
            if (audioTrack == null || !audioTrack.IsLoaded || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            // Draw background
            using var bgPaint = new SKPaint { Color = ToSKColor(BackgroundColor), IsAntialias = false };
            canvas.DrawRect(bounds.ToSKRect(), bgPaint);

            // If no waveform data yet, show loading indicator
            if (audioTrack.WaveformData.Count == 0)
            {
                DrawLoadingIndicator(canvas, audioTrack, bounds);
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
                    RenderMonoWaveform(canvas, visiblePoints, bounds, visibleStartMs, msPerPixel, centerY, maxAmplitude);
                    break;
                case WaveformColorMode.Stereo:
                    RenderStereoWaveform(canvas, visiblePoints, bounds, visibleStartMs, msPerPixel, centerY, maxAmplitude);
                    break;
                case WaveformColorMode.Spectrum:
                    RenderSpectrumWaveform(canvas, visiblePoints, bounds, visibleStartMs, msPerPixel, centerY, maxAmplitude);
                    break;
            }

            // Draw center line
            using var centerPaint = new SKPaint { Color = new SKColor(255, 255, 255, 60), StrokeWidth = 0.5f, IsAntialias = false };
            canvas.DrawLine((float)bounds.X, centerY, (float)(bounds.X + bounds.Width), centerY, centerPaint);
        }

        private static void RenderMonoWaveform(
            SKCanvas canvas,
            IEnumerable<WaveformPoint> points,
            Rect bounds,
            double startMs,
            double msPerPixel,
            float centerY,
            float maxAmplitude)
        {
            using var fillPaint = new SKPaint { Color = ToSKColor(MonoFillColor), IsAntialias = false };
            using var linePaint = new SKPaint { Color = ToSKColor(MonoColor), StrokeWidth = 1f, IsAntialias = false };

            float lastX = float.NaN;
            float lastY1 = centerY;
            float lastY2 = centerY;

            foreach (var point in points)
            {
                float x = (float)(bounds.X + (point.TimeMs - startMs) / msPerPixel);

                if (x < bounds.X - 1 || x > bounds.X + bounds.Width + 1)
                    continue;

                float amplitude = point.AveragePeak * maxAmplitude;
                amplitude = Math.Max(amplitude, MinWaveformHeight);

                float y1 = centerY - amplitude;
                float y2 = centerY + amplitude;

                // Draw filled bar
                canvas.DrawRect(x, y1, 1.5f, amplitude * 2, fillPaint);

                // Draw outline
                if (!float.IsNaN(lastX))
                {
                    canvas.DrawLine(lastX, lastY1, x, y1, linePaint);
                    canvas.DrawLine(lastX, lastY2, x, y2, linePaint);
                }

                lastX = x;
                lastY1 = y1;
                lastY2 = y2;
            }
        }

        private static void RenderStereoWaveform(
            SKCanvas canvas,
            IEnumerable<WaveformPoint> points,
            Rect bounds,
            double startMs,
            double msPerPixel,
            float centerY,
            float maxAmplitude)
        {
            using var leftPaint = new SKPaint { Color = ToSKColor(LeftChannelColor), IsAntialias = false };
            using var rightPaint = new SKPaint { Color = ToSKColor(RightChannelColor), IsAntialias = false };

            foreach (var point in points)
            {
                float x = (float)(bounds.X + (point.TimeMs - startMs) / msPerPixel);

                if (x < bounds.X - 1 || x > bounds.X + bounds.Width + 1)
                    continue;

                // Left channel goes up
                float leftHeight = Math.Max(point.LeftPeak * maxAmplitude, MinWaveformHeight);
                canvas.DrawRect(x, centerY - leftHeight, 1f, leftHeight, leftPaint);

                // Right channel goes down
                float rightHeight = Math.Max(point.RightPeak * maxAmplitude, MinWaveformHeight);
                canvas.DrawRect(x, centerY, 1f, rightHeight, rightPaint);
            }
        }

        private static void RenderSpectrumWaveform(
            SKCanvas canvas,
            IEnumerable<WaveformPoint> points,
            Rect bounds,
            double startMs,
            double msPerPixel,
            float centerY,
            float maxAmplitude)
        {
            using var paint = new SKPaint { IsAntialias = false };

            foreach (var point in points)
            {
                float x = (float)(bounds.X + (point.TimeMs - startMs) / msPerPixel);

                if (x < bounds.X - 1 || x > bounds.X + bounds.Width + 1)
                    continue;

                float amplitude = point.AveragePeak;
                float height = Math.Max(amplitude * maxAmplitude, MinWaveformHeight);

                paint.Color = ToSKColor(GetSpectrumColor(amplitude));
                canvas.DrawRect(x, centerY - height, 1.5f, height * 2, paint);
            }
        }

        private static Color GetSpectrumColor(float amplitude)
        {
            amplitude = Math.Clamp(amplitude, 0f, 1f);

            byte r, g, b;

            if (amplitude < 0.25f)
            {
                float t = amplitude / 0.25f;
                r = 0;
                g = (byte)(t * 200);
                b = 255;
            }
            else if (amplitude < 0.5f)
            {
                float t = (amplitude - 0.25f) / 0.25f;
                r = 0;
                g = 200;
                b = (byte)((1 - t) * 255);
            }
            else if (amplitude < 0.75f)
            {
                float t = (amplitude - 0.5f) / 0.25f;
                r = (byte)(t * 255);
                g = 200;
                b = 0;
            }
            else
            {
                float t = (amplitude - 0.75f) / 0.25f;
                r = 255;
                g = (byte)((1 - t) * 200);
                b = 0;
            }

            return Color.FromArgb(200, r, g, b);
        }

        private static void DrawLoadingIndicator(
            SKCanvas canvas,
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
            using var bgPaint = new SKPaint { Color = new SKColor(100, 100, 100, 100), IsAntialias = true };
            canvas.DrawRoundRect(barX, barY, barWidth, barHeight, 2, 2, bgPaint);

            // Progress
            if (progress > 0)
            {
                using var progressPaint = new SKPaint { Color = ToSKColor(MonoColor), IsAntialias = true };
                canvas.DrawRoundRect(barX, barY, barWidth * progress, barHeight, 2, 2, progressPaint);
            }

            // Text
            string text = audioTrack.IsGeneratingWaveform
                ? $"Generating waveform... {(int)(progress * 100)}%"
                : "Loading audio...";

            using var textPaint = new SKPaint
            {
                Color = new SKColor(200, 200, 200, 180),
                TextSize = 10,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Segoe UI"),
                TextAlign = SKTextAlign.Center
            };

            canvas.DrawText(text, (float)(bounds.X + bounds.Width / 2), centerY + 16, textPaint);
        }

        /// <summary>
        /// Renders a playhead indicator at the specified position.
        /// </summary>
        public static void RenderPlayhead(
            SKCanvas canvas,
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
            using var linePaint = new SKPaint { Color = ToSKColor(PlayheadColor), StrokeWidth = 2f, IsAntialias = false };
            canvas.DrawLine(x, (float)bounds.Y, x, (float)(bounds.Y + bounds.Height), linePaint);

            // Draw small triangle at top
            float triangleSize = 6f;
            using var path = new SKPath();
            path.MoveTo(x - triangleSize / 2, (float)bounds.Y);
            path.LineTo(x + triangleSize / 2, (float)bounds.Y);
            path.LineTo(x, (float)bounds.Y + triangleSize);
            path.Close();

            using var fillPaint = new SKPaint { Color = ToSKColor(PlayheadColor), IsAntialias = true };
            canvas.DrawPath(path, fillPaint);
        }

        /// <summary>
        /// Renders the audio track filename label.
        /// </summary>
        public static void RenderLabel(
            SKCanvas canvas,
            AudioTrackState audioTrack,
            Rect bounds)
        {
            if (audioTrack == null || !audioTrack.IsLoaded)
                return;

            string label = $"? {audioTrack.Settings.DisplayName}";

            float x = (float)bounds.X + 4;
            float y = (float)bounds.Y + 12;

            // Measure text for background
            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 10,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };

            var textBounds = new SKRect();
            textPaint.MeasureText(label, ref textBounds);

            // Background for readability
            using var bgPaint = new SKPaint { Color = new SKColor(0, 0, 0, 180), IsAntialias = true };
            canvas.DrawRoundRect(x - 2, (float)bounds.Y + 2, textBounds.Width + 4, textBounds.Height + 4, 2, 2, bgPaint);

            canvas.DrawText(label, x, y, textPaint);
        }

        private static SKColor ToSKColor(Color c) => new SKColor(c.R, c.G, c.B, c.A);
    }

    internal static class RectExtensions
    {
        public static SKRect ToSKRect(this Rect r) => new SKRect((float)r.X, (float)r.Y, (float)(r.X + r.Width), (float)(r.Y + r.Height));
    }
}
