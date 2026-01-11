using System;
using System.Threading.Tasks;
#if WINDOWS
using Velopack;
using Velopack.Sources;
#endif

namespace PixlPunkt.Core.Updates;

/// <summary>
/// Service for checking and applying application updates via Velopack.
/// Only functional on Windows Desktop (Skia) builds installed via Velopack.
/// </summary>
public sealed class VelopackUpdateService
{
    private static VelopackUpdateService? _instance;
    
    /// <summary>
    /// Gets the singleton instance of the update service.
    /// </summary>
    public static VelopackUpdateService Instance => _instance ??= new VelopackUpdateService();

#if WINDOWS
    /// <summary>
    /// The GitHub releases URL for PixlPunkt.
    /// </summary>
    private const string GitHubReleasesUrl = "https://github.com/ChadRoesler/PixlPunkt";

    private readonly UpdateManager? _updateManager;
    private Velopack.UpdateInfo? _pendingUpdate;

    private VelopackUpdateService()
    {
        try
        {
            // Use GitHub releases as the update source
            var source = new GithubSource(GitHubReleasesUrl, null, false);
            _updateManager = new UpdateManager(source);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velopack] Failed to initialize UpdateManager: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets whether the app was installed via Velopack and can receive updates.
    /// </summary>
    public bool IsUpdateSupported => _updateManager?.IsInstalled ?? false;

    /// <summary>
    /// Gets the current installed version.
    /// </summary>
    public string? CurrentVersion => _updateManager?.CurrentVersion?.ToString();

    /// <summary>
    /// Gets whether there's a pending update ready to install.
    /// </summary>
    public bool HasPendingUpdate => _pendingUpdate != null;

    /// <summary>
    /// Gets the version of the pending update, if any.
    /// </summary>
    public string? PendingUpdateVersion => _pendingUpdate?.TargetFullRelease?.Version?.ToString();

    /// <summary>
    /// Raised when update progress changes during download.
    /// </summary>
    public event Action<int>? DownloadProgress;

    /// <summary>
    /// Checks for available updates.
    /// </summary>
    /// <returns>True if an update is available; false otherwise.</returns>
    public async Task<bool> CheckForUpdatesAsync()
    {
        if (_updateManager == null || !_updateManager.IsInstalled)
        {
            System.Diagnostics.Debug.WriteLine("[Velopack] Not installed via Velopack, skipping update check");
            return false;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("[Velopack] Checking for updates...");
            _pendingUpdate = await _updateManager.CheckForUpdatesAsync();
            
            if (_pendingUpdate != null)
            {
                System.Diagnostics.Debug.WriteLine($"[Velopack] Update available: {_pendingUpdate.TargetFullRelease?.Version}");
                return true;
            }
            
            System.Diagnostics.Debug.WriteLine("[Velopack] No updates available");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velopack] Error checking for updates: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Downloads the pending update.
    /// </summary>
    /// <returns>True if download succeeded; false otherwise.</returns>
    public async Task<bool> DownloadUpdateAsync()
    {
        if (_updateManager == null || _pendingUpdate == null)
        {
            return false;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("[Velopack] Downloading update...");
            
            await _updateManager.DownloadUpdatesAsync(
                _pendingUpdate, 
                progress => DownloadProgress?.Invoke(progress));
            
            System.Diagnostics.Debug.WriteLine("[Velopack] Download complete");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velopack] Error downloading update: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Applies the downloaded update and restarts the application.
    /// </summary>
    public void ApplyUpdateAndRestart()
    {
        if (_updateManager == null || _pendingUpdate == null)
        {
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("[Velopack] Applying update and restarting...");
            _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velopack] Error applying update: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks for updates silently in the background.
    /// If an update is found, downloads it and notifies via the callback.
    /// </summary>
    /// <param name="onUpdateReady">Called when an update has been downloaded and is ready to install.</param>
    public async Task CheckAndDownloadInBackgroundAsync(Action<string>? onUpdateReady = null)
    {
        try
        {
            if (await CheckForUpdatesAsync() && await DownloadUpdateAsync())
            {
                onUpdateReady?.Invoke(PendingUpdateVersion ?? "unknown");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velopack] Background update check failed: {ex.Message}");
        }
    }
#else
    // Non-Windows implementation - all operations are no-ops

    private VelopackUpdateService() { }

    /// <summary>
    /// Gets whether the app was installed via Velopack and can receive updates.
    /// </summary>
    public bool IsUpdateSupported => false;

    /// <summary>
    /// Gets the current installed version.
    /// </summary>
    public string? CurrentVersion => null;

    /// <summary>
    /// Gets whether there's a pending update ready to install.
    /// </summary>
    public bool HasPendingUpdate => false;

    /// <summary>
    /// Gets the version of the pending update, if any.
    /// </summary>
    public string? PendingUpdateVersion => null;

    /// <summary>
    /// Raised when update progress changes during download.
    /// </summary>
    public event Action<int>? DownloadProgress { add { } remove { } }

    /// <summary>
    /// Checks for available updates.
    /// </summary>
    public Task<bool> CheckForUpdatesAsync() => Task.FromResult(false);

    /// <summary>
    /// Downloads the pending update.
    /// </summary>
    public Task<bool> DownloadUpdateAsync() => Task.FromResult(false);

    /// <summary>
    /// Applies the downloaded update and restarts the application.
    /// </summary>
    public void ApplyUpdateAndRestart() { }

    /// <summary>
    /// Checks for updates silently in the background.
    /// </summary>
    public Task CheckAndDownloadInBackgroundAsync(Action<string>? onUpdateReady = null) => Task.CompletedTask;
#endif
}
