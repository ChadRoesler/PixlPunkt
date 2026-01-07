using System.Runtime.InteropServices;

namespace PixlPunkt.Uno.Core.Platform;

/// <summary>
/// Helper class for detecting the current platform/runtime environment.
/// </summary>
public static class PlatformHelper
{
    /// <summary>
    /// Gets whether the app is running on Windows (native WinUI or Skia).
    /// </summary>
    public static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Gets whether the app is running on macOS (Mac Catalyst or Skia).
    /// </summary>
    public static bool IsMacOS =>
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Gets whether the app is running on Linux (X11, Wayland, or Framebuffer).
    /// </summary>
    public static bool IsLinux =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

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
    /// Cached value for Skia desktop detection (computed once at startup).
    /// </summary>
    private static readonly bool _isSkiaDesktop = DetectSkiaDesktop();

    /// <summary>
    /// Gets whether the app is running on a Skia-based desktop target (Linux, macOS Skia, or Windows Skia).
    /// </summary>
    public static bool IsSkiaDesktop => _isSkiaDesktop;

    /// <summary>
    /// Detects if we're running on a Skia-based desktop platform at runtime.
    /// </summary>
    private static bool DetectSkiaDesktop()
    {
        // Linux is always Skia
        if (IsLinux) return true;

        // Check for Uno Platform Skia host types at runtime
        // This works for Windows Desktop (Skia) and macOS (Skia)
        try
        {
            // Try to find Uno.UI.Runtime.Skia assembly - if it's loaded, we're on Skia
            var skiaAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.StartsWith("Uno.UI.Runtime.Skia", StringComparison.OrdinalIgnoreCase) == true);
            
            if (skiaAssembly != null)
                return true;

            // Alternative: Check for SkiaSharp.Views.Windows assembly usage pattern
            // The SKXamlCanvas type only exists in Skia builds
            var skiaViewsType = Type.GetType("SkiaSharp.Views.Windows.SKXamlCanvas, SkiaSharp.Views.Windows");
            if (skiaViewsType != null)
            {
                // Check if it's the Uno-specific implementation
                // Native WinUI uses Win2D, not SkiaSharp for canvas
                return true;
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
    public static bool IsNativeWinUI =>
        IsWindows && !IsSkiaDesktop;

    /// <summary>
    /// Gets whether the app is running inside WSL (Windows Subsystem for Linux).
    /// </summary>
    public static bool IsWSL
    {
        get
        {
            if (!IsLinux) return false;

            try
            {
                // Check for WSL-specific environment variable or /proc/version content
                var wslEnv = Environment.GetEnvironmentVariable("WSL_DISTRO_NAME");
                if (!string.IsNullOrEmpty(wslEnv)) return true;

                // Check /proc/version for Microsoft
                if (File.Exists("/proc/version"))
                {
                    var version = File.ReadAllText("/proc/version");
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
    }

    /// <summary>
    /// Gets a friendly name for the current platform.
    /// </summary>
    public static string PlatformName
    {
        get
        {
            if (IsWebAssembly) return "WebAssembly";
            if (IsAndroid) return "Android";
            if (IsIOS) return "iOS";
            if (IsWSL) return "Linux (WSL)";
            if (IsLinux) return "Linux";
            if (IsMacOS) return IsSkiaDesktop ? "macOS (Skia)" : "macOS";
            if (IsWindows) return IsNativeWinUI ? "Windows (WinUI)" : "Windows (Skia)";
            return "Unknown";
        }
    }

    /// <summary>
    /// Gets whether hardware acceleration is likely available.
    /// </summary>
    public static bool HasHardwareAcceleration
    {
        get
        {
            // WSL2 with WSLg should have GPU acceleration
            // Native platforms generally have it
            // WASM depends on WebGL support
            if (IsWSL) return true; // WSL2 with WSLg
            if (IsWebAssembly) return true; // WebGL
            return true; // Most platforms support it
        }
    }
}
