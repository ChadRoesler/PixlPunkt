using System;

namespace PixlPunkt.Uno.Core.Updates
{
    /// <summary>
    /// Contains information about an available update from GitHub releases.
    /// </summary>
    public sealed class UpdateInfo
    {
        /// <summary>
        /// Gets the version string (e.g., "1.0.0").
        /// </summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Gets the parsed version for comparison.
        /// </summary>
        public Version? ParsedVersion { get; init; }

        /// <summary>
        /// Gets the release tag name (e.g., "v1.0.0").
        /// </summary>
        public string TagName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the release title/name.
        /// </summary>
        public string ReleaseName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the release notes/body (markdown).
        /// </summary>
        public string ReleaseNotes { get; init; } = string.Empty;

        /// <summary>
        /// Gets the URL to the release page on GitHub.
        /// </summary>
        public string ReleaseUrl { get; init; } = string.Empty;

        /// <summary>
        /// Gets the direct download URL for the x64 package.
        /// </summary>
        public string? DownloadUrlX64 { get; init; }

        /// <summary>
        /// Gets the direct download URL for the ARM64 package.
        /// </summary>
        public string? DownloadUrlArm64 { get; init; }

        /// <summary>
        /// Gets the publish date of the release.
        /// </summary>
        public DateTimeOffset PublishedAt { get; init; }

        /// <summary>
        /// Gets whether this is a pre-release.
        /// </summary>
        public bool IsPreRelease { get; init; }
    }
}
