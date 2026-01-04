using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Settings;

namespace PixlPunkt.Core.Updates
{
    /// <summary>
    /// Service for checking GitHub releases for application updates.
    /// </summary>
    public sealed class UpdateService : IDisposable
    {
        // ????????????????????????????????????????????????????????????????????
        // CONSTANTS
        // ????????????????????????????????????????????????????????????????????

        /// <summary>
        /// GitHub repository owner.
        /// </summary>
        private const string GitHubOwner = "ChadRoesler";

        /// <summary>
        /// GitHub repository name.
        /// </summary>
        private const string GitHubRepo = "PixlPunkt";

        /// <summary>
        /// GitHub API endpoint for latest release.
        /// </summary>
        private const string LatestReleaseUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

        /// <summary>
        /// GitHub releases page URL.
        /// </summary>
        public const string ReleasesPageUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases";

        /// <summary>
        /// Minimum time between automatic update checks (1 hour).
        /// </summary>
        private static readonly TimeSpan MinCheckInterval = TimeSpan.FromHours(1);

        // ????????????????????????????????????????????????????????????????????
        // FIELDS
        // ????????????????????????????????????????????????????????????????????

        private readonly HttpClient _httpClient;
        private readonly Version _currentVersion;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private UpdateInfo? _cachedUpdate;
        private bool _disposed;

        // ????????????????????????????????????????????????????????????????????
        // SINGLETON
        // ????????????????????????????????????????????????????????????????????

        private static UpdateService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// Gets the shared UpdateService instance.
        /// </summary>
        public static UpdateService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new UpdateService();
                    }
                }
                return _instance;
            }
        }

        // ????????????????????????????????????????????????????????????????????
        // CONSTRUCTOR
        // ????????????????????????????????????????????????????????????????????

        private UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("PixlPunkt", GetCurrentVersionString()));
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            _httpClient.Timeout = TimeSpan.FromSeconds(15);

            _currentVersion = GetCurrentVersion();
        }

        // ????????????????????????????????????????????????????????????????????
        // PUBLIC METHODS
        // ????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Checks for updates if enough time has passed since last check.
        /// </summary>
        /// <param name="forceCheck">If true, ignores the time interval and always checks.</param>
        /// <param name="includePreReleases">If true, includes pre-release versions.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>UpdateInfo if a newer version is available, null otherwise.</returns>
        public async Task<UpdateInfo?> CheckForUpdateAsync(
            bool forceCheck = false,
            bool includePreReleases = false,
            CancellationToken cancellationToken = default)
        {
            if (_disposed) return null;

            // Check if we should skip due to recent check
            if (!forceCheck && DateTime.UtcNow - _lastCheckTime < MinCheckInterval)
            {
                LoggingService.Debug("Skipping update check - checked recently. Last check: {LastCheck}", _lastCheckTime);
                return _cachedUpdate;
            }

            try
            {
                LoggingService.Info("Checking for updates... Current version: {Version}", _currentVersion);

                var response = await _httpClient.GetAsync(LatestReleaseUrl, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    LoggingService.Warning("GitHub API returned {StatusCode} when checking for updates", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var release = JsonDocument.Parse(json);
                var root = release.RootElement;

                // Parse release info
                var tagName = root.GetProperty("tag_name").GetString() ?? "";
                var releaseName = root.GetProperty("name").GetString() ?? tagName;
                var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                var htmlUrl = root.GetProperty("html_url").GetString() ?? "";
                var publishedAt = root.GetProperty("published_at").GetDateTimeOffset();
                var isPreRelease = root.GetProperty("prerelease").GetBoolean();

                // Skip pre-releases if not requested
                if (isPreRelease && !includePreReleases)
                {
                    LoggingService.Debug("Latest release is pre-release, skipping");
                    _lastCheckTime = DateTime.UtcNow;
                    _cachedUpdate = null;
                    return null;
                }

                // Parse version from tag (remove 'v' prefix if present)
                var versionString = tagName.TrimStart('v', 'V');
                if (!Version.TryParse(NormalizeVersion(versionString), out var releaseVersion))
                {
                    LoggingService.Warning("Could not parse version from tag: {Tag}", tagName);
                    return null;
                }

                // Find download URLs for assets
                string? downloadX64 = null;
                string? downloadArm64 = null;

                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        var downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";

                        if (name.Contains("x64", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadX64 = downloadUrl;
                        }
                        else if (name.Contains("arm64", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadArm64 = downloadUrl;
                        }
                    }
                }

                _lastCheckTime = DateTime.UtcNow;

                // Compare versions
                if (releaseVersion > _currentVersion)
                {
                    LoggingService.Info("Update available: {NewVersion} (current: {CurrentVersion})", 
                        releaseVersion, _currentVersion);

                    _cachedUpdate = new UpdateInfo
                    {
                        Version = versionString,
                        ParsedVersion = releaseVersion,
                        TagName = tagName,
                        ReleaseName = releaseName,
                        ReleaseNotes = body,
                        ReleaseUrl = htmlUrl,
                        DownloadUrlX64 = downloadX64,
                        DownloadUrlArm64 = downloadArm64,
                        PublishedAt = publishedAt,
                        IsPreRelease = isPreRelease
                    };

                    return _cachedUpdate;
                }
                else
                {
                    LoggingService.Info("No update available. Latest: {LatestVersion}, Current: {CurrentVersion}",
                        releaseVersion, _currentVersion);
                    _cachedUpdate = null;
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                LoggingService.Debug("Update check cancelled");
                return null;
            }
            catch (HttpRequestException ex)
            {
                LoggingService.Warning("Network error checking for updates: {Error}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Error checking for updates", ex);
                return null;
            }
        }

        /// <summary>
        /// Gets the appropriate download URL for the current system architecture.
        /// </summary>
        public static string? GetDownloadUrlForCurrentArchitecture(UpdateInfo update)
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            return arch switch
            {
                Architecture.X64 => update.DownloadUrlX64,
                Architecture.Arm64 => update.DownloadUrlArm64,
                _ => update.DownloadUrlX64 // Default to x64
            };
        }

        /// <summary>
        /// Gets the current application version.
        /// </summary>
        public static Version GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version ?? new Version(0, 0, 0);
        }

        /// <summary>
        /// Gets the current application version as a string.
        /// </summary>
        public static string GetCurrentVersionString()
        {
            return GetCurrentVersion().ToString(3);
        }

        /// <summary>
        /// Opens the GitHub releases page in the default browser.
        /// </summary>
        public static void OpenReleasesPage()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ReleasesPageUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to open releases page", ex);
            }
        }

        /// <summary>
        /// Opens a specific release URL in the default browser.
        /// </summary>
        public static void OpenReleaseUrl(string url)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to open release URL", ex);
            }
        }

        // ????????????????????????????????????????????????????????????????????
        // PRIVATE HELPERS
        // ????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Normalizes a version string to ensure it has at least 3 parts.
        /// </summary>
        private static string NormalizeVersion(string version)
        {
            var parts = version.Split('.');
            while (parts.Length < 3)
            {
                version += ".0";
                parts = version.Split('.');
            }
            // Take only first 3-4 parts (major.minor.patch[.revision])
            if (parts.Length > 4)
            {
                version = string.Join('.', parts[..4]);
            }
            return version;
        }

        // ????????????????????????????????????????????????????????????????????
        // DISPOSAL
        // ????????????????????????????????????????????????????????????????????

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _httpClient.Dispose();
        }
    }
}
