using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FFMpegCore;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace PixlPunkt.Uno.Core.Audio
{
    /// <summary>
    /// Service for managing FFmpeg initialization and configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FFmpeg binaries are automatically downloaded on first use if not found on the system.
    /// Binaries are stored in the app's local data folder.
    /// </para>
    /// </remarks>
    public static class FFmpegService
    {
        private static bool _initialized;
        private static bool _isAvailable;
        private static bool _isDownloading;
        private static string? _ffmpegPath;
        private static string? _ffprobePath;
        private static string? _binaryFolder;
        private static readonly object _lock = new();

        /// <summary>
        /// Gets whether FFmpeg is available (either system-installed or bundled).
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                EnsureInitialized();
                return _isAvailable;
            }
        }

        /// <summary>
        /// Gets whether FFmpeg binaries are currently being downloaded.
        /// </summary>
        public static bool IsDownloading => _isDownloading;

        /// <summary>
        /// Gets the path to the FFmpeg executable, or null if not found.
        /// </summary>
        public static string? FFmpegPath
        {
            get
            {
                EnsureInitialized();
                return _ffmpegPath;
            }
        }

        /// <summary>
        /// Gets the path to the FFprobe executable, or null if not found.
        /// </summary>
        public static string? FFprobePath
        {
            get
            {
                EnsureInitialized();
                return _ffprobePath;
            }
        }

        /// <summary>
        /// Gets the FFmpeg version string, or null if not available.
        /// </summary>
        public static string? Version { get; private set; }

        /// <summary>
        /// Raised when FFmpeg availability changes.
        /// </summary>
        public static event Action<bool>? AvailabilityChanged;

        /// <summary>
        /// Raised when download progress updates.
        /// </summary>
        public static event Action<float, string>? DownloadProgress;

        /// <summary>
        /// Gets the folder where bundled FFmpeg binaries are stored.
        /// </summary>
        public static string BinaryFolder
        {
            get
            {
                if (_binaryFolder == null)
                {
                    // Store in app's local data folder
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    _binaryFolder = Path.Combine(appData, "PixlPunkt", "ffmpeg");
                }
                return _binaryFolder;
            }
        }

        /// <summary>
        /// Ensures FFmpeg is initialized and configured.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;
                Initialize();
            }
        }

        /// <summary>
        /// Downloads FFmpeg binaries if not already present.
        /// </summary>
        /// <param name="progress">Progress callback (0-1).</param>
        /// <returns>True if FFmpeg is available after download attempt.</returns>
        public static async Task<bool> EnsureDownloadedAsync(IProgress<(float progress, string status)>? progress = null)
        {
            EnsureInitialized();

            // Already available
            if (_isAvailable)
            {
                progress?.Report((1f, "FFmpeg ready"));
                return true;
            }

            // Check if download is already in progress
            if (_isDownloading)
            {
                progress?.Report((0f, "Download already in progress..."));
                return false;
            }

            _isDownloading = true;

            try
            {
                // Ensure binary folder exists
                Directory.CreateDirectory(BinaryFolder);

                progress?.Report((0f, "Downloading FFmpeg binaries..."));
                DownloadProgress?.Invoke(0f, "Starting download...");

                // Use Xabe.FFmpeg.Downloader to get the binaries
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, BinaryFolder, new Progress<ProgressInfo>(p =>
                {
                    float pct = p.DownloadedBytes / (float)Math.Max(1, p.TotalBytes);
                    string status = $"Downloading: {p.DownloadedBytes / 1024 / 1024:F1} MB / {p.TotalBytes / 1024 / 1024:F1} MB";
                    progress?.Report((pct * 0.9f, status)); // 0-90% for download
                    DownloadProgress?.Invoke(pct * 0.9f, status);
                }));

                progress?.Report((0.95f, "Extracting..."));
                DownloadProgress?.Invoke(0.95f, "Extracting...");

                // Reinitialize to find the new binaries
                _initialized = false;
                Initialize();

                progress?.Report((1f, _isAvailable ? "FFmpeg ready!" : "Download failed"));
                DownloadProgress?.Invoke(1f, _isAvailable ? "FFmpeg ready!" : "Download failed");

                return _isAvailable;
            }
            catch (Exception ex)
            {
                progress?.Report((0f, $"Download failed: {ex.Message}"));
                DownloadProgress?.Invoke(0f, $"Download failed: {ex.Message}");
                return false;
            }
            finally
            {
                _isDownloading = false;
            }
        }

        /// <summary>
        /// Reinitializes FFmpeg detection.
        /// </summary>
        public static void Reinitialize()
        {
            lock (_lock)
            {
                _initialized = false;
                Initialize();
            }
        }

        private static void Initialize()
        {
            // First check bundled location
            _ffmpegPath = FindExecutableInFolder(BinaryFolder, "ffmpeg");
            _ffprobePath = FindExecutableInFolder(BinaryFolder, "ffprobe");

            // If not bundled, check system PATH
            if (_ffmpegPath == null)
            {
                _ffmpegPath = FindExecutable("ffmpeg");
            }
            if (_ffprobePath == null)
            {
                _ffprobePath = FindExecutable("ffprobe");
            }

            if (_ffmpegPath != null && _ffprobePath != null)
            {
                try
                {
                    var binFolder = Path.GetDirectoryName(_ffmpegPath);
                    if (!string.IsNullOrEmpty(binFolder))
                    {
                        GlobalFFOptions.Configure(options =>
                        {
                            options.BinaryFolder = binFolder;
                            options.TemporaryFilesFolder = Path.GetTempPath();
                        });
                    }

                    Version = GetFFmpegVersion();
                    _isAvailable = !string.IsNullOrEmpty(Version);
                }
                catch (Exception)
                {
                    _isAvailable = false;
                    Version = null;
                }
            }
            else
            {
                _isAvailable = false;
                Version = null;
            }

            _initialized = true;
            AvailabilityChanged?.Invoke(_isAvailable);
        }

        private static string? FindExecutableInFolder(string folder, string name)
        {
            if (!Directory.Exists(folder)) return null;

            var execName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? $"{name}.exe"
                : name;

            var fullPath = Path.Combine(folder, execName);
            return File.Exists(fullPath) ? fullPath : null;
        }

        private static string? FindExecutable(string name)
        {
            var execName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? $"{name}.exe"
                : name;

            var searchPaths = GetSearchPaths();

            foreach (var path in searchPaths)
            {
                var fullPath = Path.Combine(path, execName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            // Check PATH environment
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var pathDirs = pathEnv.Split(Path.PathSeparator);

            foreach (var dir in pathDirs)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;

                var fullPath = Path.Combine(dir.Trim(), execName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        private static string[] GetSearchPaths()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new[]
                {
                    @"C:\ProgramData\chocolatey\bin",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims"),
                    @"C:\ffmpeg\bin",
                    @"C:\Program Files\ffmpeg\bin",
                    @"C:\Program Files (x86)\ffmpeg\bin",
                    Path.Combine(AppContext.BaseDirectory, "ffmpeg"),
                    AppContext.BaseDirectory
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new[]
                {
                    "/usr/local/bin",
                    "/opt/homebrew/bin",
                    "/usr/bin",
                    Path.Combine(AppContext.BaseDirectory, "ffmpeg"),
                    AppContext.BaseDirectory
                };
            }
            else
            {
                return new[]
                {
                    "/usr/bin",
                    "/usr/local/bin",
                    "/snap/bin",
                    Path.Combine(AppContext.BaseDirectory, "ffmpeg"),
                    AppContext.BaseDirectory
                };
            }
        }

        private static string? GetFFmpegVersion()
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadLine();
                    process.WaitForExit(5000);
                    
                    if (!string.IsNullOrEmpty(output) && output.StartsWith("ffmpeg version"))
                    {
                        var parts = output.Split(' ');
                        if (parts.Length >= 3)
                        {
                            return parts[2];
                        }
                    }
                    return output;
                }
            }
            catch
            {
                // Ignore
            }

            return "unknown";
        }

        /// <summary>
        /// Gets a user-friendly status message about FFmpeg availability.
        /// </summary>
        public static string GetStatusMessage()
        {
            if (!_initialized)
            {
                return "FFmpeg: Not initialized";
            }

            if (_isDownloading)
            {
                return "FFmpeg: Downloading...";
            }

            if (_isAvailable)
            {
                var location = _ffmpegPath?.StartsWith(BinaryFolder) == true ? "bundled" : "system";
                return $"FFmpeg: Available (v{Version}, {location})";
            }

            return "FFmpeg: Not found - will download automatically when needed";
        }
    }
}
