namespace PixlPunkt.Uno.UITests;

/// <summary>
/// Platform-specific configuration constants for UI tests.
/// Configure these values based on your test environment.
/// </summary>
/// <remarks>
/// To run tests on different platforms:
/// 
/// **Windows (Desktop)**
/// - Build and run the net10.0-windows10.0.26100 target
/// - Set CurrentPlatform = Platform.Browser (uses automation bridge)
/// - Or use the Windows App SDK testing approach
/// 
/// **macOS (Desktop/Catalyst)**
/// - Build and run the net10.0-maccatalyst target  
/// - Set CurrentPlatform = Platform.Browser or Platform.iOS
/// - Note: Requires macOS development machine
/// 
/// **Linux (Desktop)**
/// - Build and run the net10.0-desktop (Skia GTK) target
/// - Set CurrentPlatform = Platform.Browser
/// - Runs via browser automation or direct GTK automation
/// 
/// **Android**
/// - Build and deploy to emulator or device
/// - Set CurrentPlatform = Platform.Android
/// - Configure AndroidAppName to match package name
/// 
/// **iOS**
/// - Build and deploy to simulator or device
/// - Set CurrentPlatform = Platform.iOS
/// - Configure iOSAppName and iOSDeviceNameOrId
/// - Requires macOS with Xcode
/// 
/// **WebAssembly (Browser)**
/// - Build and run the net10.0-browserwasm target
/// - Set CurrentPlatform = Platform.Browser
/// - Configure WebAssemblyDefaultUri to match localhost port
/// - Configure WebAssemblyBrowser (Chrome recommended)
/// </remarks>
public static class PlatformTestConfig
{
    // ═══════════════════════════════════════════════════════════════════════
    // PLATFORM DETECTION HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets whether the current test target is Windows.
    /// </summary>
    public static bool IsWindows => 
        Constants.CurrentPlatform == Platform.Browser && 
        Environment.OSVersion.Platform == PlatformID.Win32NT;

    /// <summary>
    /// Gets whether the current test target is macOS.
    /// </summary>
    public static bool IsMacOS =>
        Constants.CurrentPlatform == Platform.iOS ||
        (Constants.CurrentPlatform == Platform.Browser && 
         Environment.OSVersion.Platform == PlatformID.Unix && 
         Directory.Exists("/Applications"));

    /// <summary>
    /// Gets whether the current test target is Linux.
    /// </summary>
    public static bool IsLinux =>
        Constants.CurrentPlatform == Platform.Browser &&
        Environment.OSVersion.Platform == PlatformID.Unix &&
        !Directory.Exists("/Applications");

    /// <summary>
    /// Gets whether the current test target is Android.
    /// </summary>
    public static bool IsAndroid => Constants.CurrentPlatform == Platform.Android;

    /// <summary>
    /// Gets whether the current test target is iOS.
    /// </summary>
    public static bool IsIOS => Constants.CurrentPlatform == Platform.iOS;

    /// <summary>
    /// Gets whether the current test target is WebAssembly.
    /// </summary>
    public static bool IsWebAssembly => Constants.CurrentPlatform == Platform.Browser;

    // ═══════════════════════════════════════════════════════════════════════
    // PLATFORM-SPECIFIC TIMEOUTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the appropriate startup delay for the current platform.
    /// Mobile platforms typically need more time for app initialization.
    /// </summary>
    public static TimeSpan StartupDelay => Constants.CurrentPlatform switch
    {
        Platform.Android => TimeSpan.FromSeconds(8),
        Platform.iOS => TimeSpan.FromSeconds(6),
        Platform.Browser => TimeSpan.FromSeconds(4),
        _ => TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// Gets the appropriate element wait timeout for the current platform.
    /// </summary>
    public static TimeSpan ElementTimeout => Constants.CurrentPlatform switch
    {
        Platform.Android => TimeSpan.FromSeconds(45),
        Platform.iOS => TimeSpan.FromSeconds(40),
        Platform.Browser => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Gets the appropriate animation/transition wait time for the current platform.
    /// </summary>
    public static TimeSpan AnimationDelay => Constants.CurrentPlatform switch
    {
        Platform.Android => TimeSpan.FromMilliseconds(800),
        Platform.iOS => TimeSpan.FromMilliseconds(600),
        Platform.Browser => TimeSpan.FromMilliseconds(500),
        _ => TimeSpan.FromMilliseconds(500)
    };
}
