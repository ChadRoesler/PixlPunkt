using System.Runtime.InteropServices;

namespace PixlPunkt.Uno.Core.Platform;

/// <summary>
/// Helper class for detecting the current platform/runtime environment.
/// </summary>
public static class PlatformHelper
{
    // Cached platform detection - computed once at startup
    private static readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static readonly bool _isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    private static readonly bool _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    private static readonly bool _isSkiaDesktop = DetectSkiaDesktop();
    private static readonly bool _isWSL = DetectWSL();
    private static readonly string _platformName = ComputePlatformName();

    /// <summary>
    /// Gets whether the app is running on Windows (native WinUI or Skia).
    /// </summary>
    public static bool IsWindows => _isWindows;

    /// <summary>
    /// Gets whether the app is running on macOS (Mac Catalyst or Skia).
    /// </summary>
    public static bool IsMacOS => _isMacOS;

    /// <summary>
    /// Gets whether the app is running on Linux (X11, Wayland, or Framebuffer).
    /// </summary>
    public static bool IsLinux => _isLinux;

    /// <summary>
    /// Gets whether the app is running on WebAssembly.
    /// </summary>
    public static bool IsWebAssembly =>
#if __WASM__
        true;
#else
        false;
#endif

    /// <summary>
    /// Gets whether the app is running on Android.
    /// </summary>
    public static bool IsAndroid =>
#if __ANDROID__
        true;
#else
        false;
#endif

    /// <summary>
    /// Gets whether the app is running on iOS.
    /// </summary>
    public static bool IsIOS =>
#if __IOS__
        true;
#else
        false;
#endif

    /// <summary>
    /// Gets whether the app is running on a Skia-based desktop target (Linux, macOS Skia, or Windows Skia).
    /// </summary>
    public static bool IsSkiaDesktop => _isSkiaDesktop;

    /// <summary>
    /// Detects if we're running on a Skia-based desktop platform at runtime.
    /// </summary>
    private static bool DetectSkiaDesktop()
    {
        // Linux is always Skia for desktop
        if (_isLinux) return true;

        // Check for Uno Platform Skia host types at runtime
        try
        {
            // Try to find Uno.UI.Runtime.Skia assembly - if it's loaded, we're on Skia
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (name != null && name.StartsWith("Uno.UI.Runtime.Skia", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore reflection errors
        }

        return false;
    }

    /// <summary>
    /// Gets whether the app is running on native WinUI (Windows App SDK).
    /// </summary>
    public static bool IsNativeWinUI => _isWindows && !_isSkiaDesktop;

    /// <summary>
    /// Gets whether the app is running inside WSL (Windows Subsystem for Linux).
    /// </summary>
    public static bool IsWSL => _isWSL;

    /// <summary>
    /// Detects if we're running inside WSL (cached at startup).
    /// </summary>
    private static bool DetectWSL()
    {
        if (!_isLinux) return false;

        try
        {
            // Check for WSL-specific environment variable
            var wslEnv = Environment.GetEnvironmentVariable("WSL_DISTRO_NAME");
            if (!string.IsNullOrEmpty(wslEnv)) return true;

            // Check /proc/version for Microsoft
            const string procVersion = "/proc/version";
            if (File.Exists(procVersion))
            {
                var version = File.ReadAllText(procVersion);
                return version.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                       version.Contains("WSL", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    /// <summary>
    /// Gets a friendly name for the current platform.
    /// </summary>
    public static string PlatformName => _platformName;

    private static string ComputePlatformName()
    {
        if (IsWebAssembly) return "WebAssembly";
        if (IsAndroid) return "Android";
        if (IsIOS) return "iOS";
        if (_isWSL) return "Linux (WSL)";
        if (_isLinux) return "Linux";
        if (_isMacOS) return _isSkiaDesktop ? "macOS (Skia)" : "macOS";
        if (_isWindows) return !_isSkiaDesktop ? "Windows (WinUI)" : "Windows (Skia)";
        return "Unknown";
    }

    /// <summary>
    /// Gets whether hardware acceleration is likely available.
    /// </summary>
    public static bool HasHardwareAcceleration => true; // All modern platforms support GPU acceleration
}
