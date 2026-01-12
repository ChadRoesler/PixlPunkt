using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Pipes;

namespace PixlPunkt.Core.Audio
{
    /// <summary>
    /// Extracts waveform data from audio files using FFmpeg.
    /// </summary>
    /// <remarks>
    /// Uses FFmpeg to decode audio and extract peak amplitude data for visualization.
    /// Will automatically download FFmpeg if not available.
    /// </remarks>
    public static class FFmpegWaveformExtractor
    {
        /// <summary>
        /// Extracts waveform data from an audio file.
        /// </summary>
        /// <param name="filePath">Path to the audio file.</param>
        /// <param name="targetPoints">Approximate number of waveform points to generate.</param>
        /// <param name="progress">Optional progress callback (0-1).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of waveform points with peak amplitude data.</returns>
        public static async Task<WaveformResult> ExtractAsync(
            string filePath,
            int targetPoints = 1000,
            IProgress<float>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                return WaveformResult.Error("File not found");
            }

            // Ensure FFmpeg is available (auto-download if needed)
            if (!FFmpegService.IsAvailable)
            {
                progress?.Report(0.05f);
                var downloadProgress = new Progress<(float p, string s)>(x => progress?.Report(x.p * 0.3f));
                bool downloaded = await FFmpegService.EnsureDownloadedAsync(downloadProgress);
                
                if (!downloaded)
                {
                    // Fall back to placeholder
                    return await GeneratePlaceholderAsync(filePath, targetPoints, progress, cancellationToken);
                }
            }

            try
            {
                progress?.Report(0.35f);

                // Get audio metadata
                var mediaInfo = await FFProbe.AnalyseAsync(filePath, cancellationToken: cancellationToken);
                var audioStream = mediaInfo.PrimaryAudioStream;

                if (audioStream == null)
                {
                    return WaveformResult.Error("No audio stream found in file");
                }

                var durationMs = mediaInfo.Duration.TotalMilliseconds;
                var sampleRate = audioStream.SampleRateHz;
                var channels = audioStream.Channels;

                progress?.Report(0.4f);

                // Extract raw audio samples using FFmpeg
                var waveformPoints = await ExtractWaveformPointsAsync(
                    filePath,
                    targetPoints,
                    durationMs,
                    sampleRate,
                    channels,
                    progress,
                    cancellationToken);

                return new WaveformResult
                {
                    Success = true,
                    Points = waveformPoints,
                    DurationMs = durationMs,
                    SampleRate = sampleRate,
                    Channels = channels
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FFmpeg waveform extraction failed: {ex.Message}");
                return await GeneratePlaceholderAsync(filePath, targetPoints, progress, cancellationToken);
            }
        }

        private static async Task<List<WaveformPoint>> ExtractWaveformPointsAsync(
            string filePath,
            int targetPoints,
            double durationMs,
            int sampleRate,
            int channels,
            IProgress<float>? progress,
            CancellationToken cancellationToken)
        {
            var points = new List<WaveformPoint>(targetPoints);
            var tempFile = Path.Combine(Path.GetTempPath(), $"waveform_{Guid.NewGuid():N}.raw");

            try
            {
                // Convert to raw 16-bit PCM for analysis
                await FFMpegArguments
                    .FromFileInput(filePath)
                    .OutputToFile(tempFile, overwrite: true, options => options
                        .WithAudioCodec("pcm_s16le")
                        .WithAudioSamplingRate(Math.Min(sampleRate, 22050))
                        .ForceFormat("s16le")
                        .WithCustomArgument("-ac 1"))
                    .ProcessAsynchronously();

                progress?.Report(0.7f);

                if (File.Exists(tempFile))
                {
                    var rawData = await File.ReadAllBytesAsync(tempFile, cancellationToken);
                    var samples = new short[rawData.Length / 2];

                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] = BitConverter.ToInt16(rawData, i * 2);
                    }

                    var actualSampleRate = Math.Min(sampleRate, 22050);
                    var actualDurationMs = (samples.Length * 1000.0) / actualSampleRate;
                    var samplesPerSegment = Math.Max(1, samples.Length / targetPoints);
                    
                    for (int i = 0; i < targetPoints && i * samplesPerSegment < samples.Length; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var startIdx = i * samplesPerSegment;
                        var endIdx = Math.Min(startIdx + samplesPerSegment, samples.Length);

                        float maxAbs = 0;
                        for (int j = startIdx; j < endIdx; j++)
                        {
                            var absValue = Math.Abs(samples[j]) / 32768f;
                            if (absValue > maxAbs) maxAbs = absValue;
                        }

                        points.Add(new WaveformPoint
                        {
                            TimeMs = (actualDurationMs * i) / targetPoints,
                            LeftPeak = maxAbs,
                            RightPeak = maxAbs
                        });

                        if (i % 100 == 0)
                        {
                            progress?.Report(0.7f + (0.3f * i / targetPoints));
                        }
                    }
                }
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }

            progress?.Report(1f);
            return points;
        }

        /// <summary>
        /// Gets audio metadata without extracting full waveform.
        /// </summary>
        public static async Task<AudioMetadata?> GetMetadataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                return null;

            // Try to ensure FFmpeg is available
            if (!FFmpegService.IsAvailable)
            {
                await FFmpegService.EnsureDownloadedAsync();
            }

            if (!FFmpegService.IsAvailable)
            {
                return new AudioMetadata
                {
                    DurationMs = 60000,
                    SampleRate = 44100,
                    Channels = 2,
                    Codec = "unknown",
                    Bitrate = 0
                };
            }

            try
            {
                var mediaInfo = await FFProbe.AnalyseAsync(filePath, cancellationToken: cancellationToken);
                var audioStream = mediaInfo.PrimaryAudioStream;

                if (audioStream == null)
                    return null;

                return new AudioMetadata
                {
                    DurationMs = mediaInfo.Duration.TotalMilliseconds,
                    SampleRate = audioStream.SampleRateHz,
                    Channels = audioStream.Channels,
                    Codec = audioStream.CodecName ?? "unknown",
                    Bitrate = audioStream.BitRate
                };
            }
            catch
            {
                return null;
            }
        }

        private static Task<WaveformResult> GeneratePlaceholderAsync(
            string filePath,
            int targetPoints,
            IProgress<float>? progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var points = new List<WaveformPoint>(targetPoints);
                var random = new Random(filePath.GetHashCode());
                float prevValue = 0.5f;

                for (int i = 0; i < targetPoints; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    float variation = (float)((random.NextDouble() - 0.5) * 0.4);
                    float newValue = Math.Clamp(prevValue + variation, 0.1f, 0.9f);
                    float modulation = (float)(Math.Sin(i * 0.1) * 0.2 + 0.3);
                    float amplitude = Math.Clamp(newValue * modulation + 0.2f, 0f, 1f);

                    points.Add(new WaveformPoint
                    {
                        TimeMs = (60000.0 * i) / targetPoints,
                        LeftPeak = amplitude,
                        RightPeak = amplitude * (0.8f + (float)random.NextDouble() * 0.2f)
                    });

                    prevValue = newValue;

                    if (i % 100 == 0)
                    {
                        progress?.Report((float)i / targetPoints);
                    }
                }

                progress?.Report(1f);

                return new WaveformResult
                {
                    Success = true,
                    Points = points,
                    DurationMs = 60000,
                    SampleRate = 44100,
                    Channels = 2,
                    IsPlaceholder = true
                };
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Result of waveform extraction.
    /// </summary>
    public class WaveformResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public List<WaveformPoint> Points { get; init; } = new();
        public double DurationMs { get; init; }
        public int SampleRate { get; init; }
        public int Channels { get; init; }
        public bool IsPlaceholder { get; init; }

        public static WaveformResult Error(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }

    /// <summary>
    /// Audio file metadata.
    /// </summary>
    public class AudioMetadata
    {
        public double DurationMs { get; init; }
        public int SampleRate { get; init; }
        public int Channels { get; init; }
        public string Codec { get; init; } = "unknown";
        public long Bitrate { get; init; }
    }

    /// <summary>
    /// Represents a single point in the waveform visualization.
    /// </summary>
    public struct WaveformPoint
    {
        public double TimeMs;
        public float LeftPeak;
        public float RightPeak;
        public readonly float AveragePeak => (LeftPeak + RightPeak) / 2f;
    }
}
