using Microsoft.UI.Xaml;

namespace PixlPunkt.Uno.Core.Platform;

/// <summary>
/// Factory for creating platform-specific file service implementations.
/// </summary>
public static class PlatformFileServiceFactory
{
    private static IPlatformFileService? _instance;

    /// <summary>
    /// Gets or creates the platform file service singleton.
    /// </summary>
    /// <param name="window">The main window (required for desktop platforms).</param>
    /// <returns>The platform-appropriate file service implementation.</returns>
    public static IPlatformFileService GetService(Window? window = null)
    {
        if (_instance != null)
        {
            return _instance;
        }

        _instance = CreateService(window);
        return _instance;
    }

    /// <summary>
    /// Creates a new platform file service instance.
    /// </summary>
    /// <param name="window">The main window for picker initialization.</param>
    /// <returns>The platform-appropriate file service.</returns>
    public static IPlatformFileService CreateService(Window? window = null)
    {
#if __IOS__
        return new IosPlatformFileService();
#elif __ANDROID__
        return new AndroidPlatformFileService();
#else
        // Desktop (Windows, Linux, macOS) and WASM
        if (window == null)
        {
            throw new System.ArgumentNullException(nameof(window), 
                "Window is required for desktop file service");
        }
        return new DesktopPlatformFileService(window);
#endif
    }

    /// <summary>
    /// Initializes the platform file service with the main window.
    /// Call this during app startup after the main window is created.
    /// </summary>
    /// <param name="window">The main application window.</param>
    public static void Initialize(Window window)
    {
        _instance = CreateService(window);
    }

    /// <summary>
    /// Gets whether the current platform supports traditional file system access.
    /// </summary>
    public static bool SupportsFileSystem => GetService().SupportsFileSystem;

    /// <summary>
    /// Gets whether the current platform supports file sharing.
    /// </summary>
    public static bool SupportsSharing => GetService().SupportsSharing;

    /// <summary>
    /// Gets whether the current platform supports folder picking.
    /// </summary>
    public static bool SupportsFolderPicking => GetService().SupportsFolderPicking;
}
