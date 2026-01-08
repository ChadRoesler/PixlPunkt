using System;
using System.Threading.Tasks;

namespace PixlPunkt.Uno.Core.Updates;

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

    /// <summary>
    /// The GitHub releases URL for PixlPunkt.
    /// </summary>
    private const string GitHubReleasesUrl = "https://github.com/ChadRoesler/PixlPunkt";

#if HAS_UNO_SKIA && WINDOWS
    private Velopack.UpdateManager? _updateManager;
    private Velopack.UpdateInfo? _pendingUpdate;
#endif

    private VelopackUpdateService()
    {
#if HAS_UNO_SKIA && WINDOWS
        try
        {
            // Use GitHub releases as the update source
            var source = new Velopack.Sources.GithubSource(GitHubReleasesUrl, null, false);
            _updateManager = new Velopack.UpdateManager(source);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velopack] Failed to initialize UpdateManager: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Gets whether the app was installed via Velopack and can receive updates.
    /// </summary>
    public bool IsUpdateSupported
    {
        get
        {
#if HAS_UNO_SKIA && WINDOWS
            return _updateManager?.IsInstalled ?? false;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Gets the current installed version.
    /// </summary>
    public string? CurrentVersion
    {
        get
        {
#if HAS_UNO_SKIA && WINDOWS
            return _updateManager?.CurrentVersion?.ToString();
#else
            return null;
#endif
        }
    }

    /// <summary>
    /// Gets whether there's a pending update ready to install.
    /// </summary>
    public bool HasPendingUpdate
    {
        get
        {
#if HAS_UNO_SKIA && WINDOWS
            return _pendingUpdate != null;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Gets the version of the pending update, if any.
    /// </summary>
    public string? PendingUpdateVersion
    {
        get
        {
#if HAS_UNO_SKIA && WINDOWS
            return _pendingUpdate?.TargetFullRelease?.Version?.ToString();
#else
            return null;
#endif
        }
    }

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
#if HAS_UNO_SKIA && WINDOWS
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
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    /// <summary>
    /// Downloads the pending update.
    /// </summary>
    /// <returns>True if download succeeded; false otherwise.</returns>
    public async Task<bool> DownloadUpdateAsync()
    {
#if HAS_UNO_SKIA && WINDOWS
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
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    /// <summary>
    /// Applies the downloaded update and restarts the application.
    /// </summary>
    /// <param name="silentRestart">If true, restarts without prompting.</param>
    public void ApplyUpdateAndRestart(bool silentRestart = false)
    {
#if HAS_UNO_SKIA && WINDOWS
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
#endif
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
            if (await CheckForUpdatesAsync())
            {
                if (await DownloadUpdateAsync())
                {
                    onUpdateReady?.Invoke(PendingUpdateVersion ?? "unknown");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velopack] Background update check failed: {ex.Message}");
        }
    }
}
