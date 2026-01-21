using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace PixlPunkt.Core.Logging
{
    /// <summary>
    /// Application logging service using Serilog.
    /// </summary>
    public static class LoggingService
    {
        private static Logger? _logger;
        private static bool _initialized;
        private static readonly object _lock = new();
        private static LoggingLevelSwitch? _levelSwitch;

        private static string? _sessionId;
        private static string? _deviceId;

        /// <summary>
        /// Gets whether the logging service has been initialized.
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// Gets the directory where log files are stored.
        /// </summary>
        public static string LogDirectory => Settings.AppPaths.LogsDirectory;

        /// <summary>
        /// Gets the path to the current log file.
        /// </summary>
        public static string CurrentLogFilePath => Path.Combine(LogDirectory, $"pixlpunkt-{DateTime.Now:yyyyMMdd}.log");

        /// <summary>
        /// Gets the runtime minimum log level.
        /// </summary>
        public static LogEventLevel CurrentLevel => _levelSwitch?.MinimumLevel ?? LogEventLevel.Information;

        /// <summary>
        /// Initializes the logging service.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    Settings.AppPaths.EnsureDirectoryExists(LogDirectory);

                    var logPath = Path.Combine(LogDirectory, "pixlpunkt-.log");

                    // Use a level switch so we can change minimum level at runtime
                    _levelSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);

                    // Telemetry: session and device identifiers
                    _sessionId = Guid.NewGuid().ToString();
                    _deviceId = Environment.MachineName ?? "unknown";

                    _logger = new LoggerConfiguration()
                        .MinimumLevel.ControlledBy(_levelSwitch)
                        .Enrich.WithProperty("SessionId", _sessionId)
                        .Enrich.WithProperty("DeviceId", _deviceId)
                        .Enrich.WithProperty("Application", "PixlPunkt")
                        .WriteTo.File(
                            logPath,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 7,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                        .CreateLogger();

                    _initialized = true;

                    Info("PixlPunkt logging initialized");
                    Info("Log directory: {LogDirectory}", LogDirectory);
                    Info("SessionId={SessionId} DeviceId={DeviceId}", _sessionId, _deviceId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoggingService] Failed to initialize: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Apply log level from settings (called after settings are loaded).
        /// </summary>
        public static void ApplyLogLevelFromSettings()
        {
            if (_levelSwitch == null) return;

            try
            {
                var settings = Settings.AppSettings.Instance;
                if (Enum.TryParse<LogEventLevel>(settings.LogLevel ?? "Information", true, out var lvl))
                {
                    _levelSwitch.MinimumLevel = lvl;
                    Info("Log level set from settings: {LogLevel}", lvl);
                }
            }
            catch (Exception ex)
            {
                Error("Failed to apply log level from settings", ex);
            }
        }

        /// <summary>
        /// Sets the minimum log level at runtime.
        /// </summary>
        public static void SetMinimumLevel(LogEventLevel level)
        {
            if (_levelSwitch != null)
            {
                _levelSwitch.MinimumLevel = level;
                Info("Log level changed at runtime to {LogLevel}", level);
            }
        }

        /// <summary>
        /// Logs comprehensive build and environment information at startup.
        /// This helps identify which build type is running (WinAppSdk, Desktop Skia, etc.).
        /// </summary>
        public static void LogBuildEnvironment()
        {
            try
            {
                // Determine build type based on compile-time constants
                string buildType;
                string framework;
                
#if WINDOWS
                // WinAppSdk build (net10.0-windows10.0.26100)
                buildType = "WinAppSdk";
                framework = "net10.0-windows10.0.26100";
#elif HAS_UNO_SKIA
                // Desktop Skia build (net10.0-desktop)
                buildType = "Desktop-Skia";
                framework = "net10.0-desktop";
#else
                // Unknown/other platform
                buildType = "Unknown";
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
#endif

                // Detect package type (MSIX vs unpackaged)
                string packageType = DetectPackageType();

                // OS and architecture info
                string osDescription = RuntimeInformation.OSDescription;
                string osArchitecture = RuntimeInformation.OSArchitecture.ToString();
                string processArchitecture = RuntimeInformation.ProcessArchitecture.ToString();
                
                // Windows version detection
                string windowsVersion = "N/A";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    windowsVersion = Environment.OSVersion.Version.ToString();
                    
                    // Friendly name for Windows version
                    var ver = Environment.OSVersion.Version;
                    if (ver.Build >= 22000)
                        windowsVersion += " (Windows 11)";
                    else if (ver.Build >= 10240)
                        windowsVersion += " (Windows 10)";
                }

                // Runtime info
                string runtimeVersion = Environment.Version.ToString();
                string clrVersion = RuntimeInformation.FrameworkDescription;

                // App version
                string appVersion = Core.Updates.UpdateService.GetCurrentVersionString();

                // Log all the info
                Info("===============================================================");
                Info("PixlPunkt Build Environment");
                Info("===============================================================");
                Info("  App Version:      {AppVersion}", appVersion);
                Info("  Build Type:       {BuildType}", buildType);
                Info("  Target Framework: {Framework}", framework);
                Info("  Package Type:     {PackageType}", packageType);
                Info("===============================================================");
                Info("  OS:               {OSDescription}", osDescription);
                Info("  Windows Version:  {WindowsVersion}", windowsVersion);
                Info("  OS Architecture:  {OSArch}", osArchitecture);
                Info("  Process Arch:     {ProcessArch}", processArchitecture);
                Info("===============================================================");
                Info("  .NET Version:     {RuntimeVersion}", runtimeVersion);
                Info("  CLR:              {CLRVersion}", clrVersion);
                Info("  Base Directory:   {BaseDir}", AppContext.BaseDirectory);
                Info("===============================================================");
            }
            catch (Exception ex)
            {
                Warning("Failed to log build environment: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Detects whether the app is running as MSIX packaged, Velopack installed, or portable.
        /// </summary>
        private static string DetectPackageType()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;

                // Check for Velopack installation FIRST (more reliable detection)
                // Velopack uses a "current" folder or "app-X.X.X" folder structure
                if (baseDir.Contains(Path.DirectorySeparatorChar + "current" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    baseDir.EndsWith(Path.DirectorySeparatorChar + "current" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    baseDir.EndsWith(Path.DirectorySeparatorChar + "current", StringComparison.OrdinalIgnoreCase))
                {
                    return "Velopack (Installed)";
                }

                // Also check for app-X.X.X pattern
                if (System.Text.RegularExpressions.Regex.IsMatch(baseDir, @"[/\\]app-\d+\.\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return "Velopack (Installed)";
                }

                // Check for Update.exe in parent directory (another Velopack indicator)
                var parentDir = Path.GetDirectoryName(baseDir.TrimEnd(Path.DirectorySeparatorChar));
                if (parentDir != null)
                {
                    var updateExe = Path.Combine(parentDir, "Update.exe");
                    if (File.Exists(updateExe))
                    {
                        return "Velopack (Installed)";
                    }
                }

#if WINDOWS
                // Check if running as MSIX packaged app (only if not Velopack)
                try
                {
                    var package = Windows.ApplicationModel.Package.Current;
                    if (package != null)
                    {
                        var id = package.Id;
                        // Additional check: MSIX apps run from WindowsApps folder
                        if (baseDir.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
                        {
                            return $"MSIX ({id.Name} v{id.Version.Major}.{id.Version.Minor}.{id.Version.Build})";
                        }
                    }
                }
                catch
                {
                    // Not packaged
                }
#endif

                return "Portable/Unpackaged";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Shuts down the logging service and flushes any pending log entries.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized) return;
            
            lock (_lock)
            {
                if (!_initialized) return;

                try
                {
                    Info("PixlPunkt logging shutting down");
                    _logger?.Dispose();
                    _logger = null;
                    _levelSwitch = null;
                    _initialized = false;
                }
                catch
                {
                    // Ignore shutdown errors
                }
            }
        }

        // LOG METHODS - with level checks to avoid allocations when disabled
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Debug(string message)
        {
            if (!IsLevelEnabled(LogEventLevel.Debug)) return;
            WriteLog(LogEventLevel.Debug, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Debug(string messageTemplate, params object[] propertyValues)
        {
            if (!IsLevelEnabled(LogEventLevel.Debug)) return;
            WriteLog(LogEventLevel.Debug, messageTemplate, propertyValues);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Info(string message)
        {
            if (!IsLevelEnabled(LogEventLevel.Information)) return;
            WriteLog(LogEventLevel.Information, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Info(string messageTemplate, params object[] propertyValues)
        {
            if (!IsLevelEnabled(LogEventLevel.Information)) return;
            WriteLog(LogEventLevel.Information, messageTemplate, propertyValues);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Warning(string message)
        {
            if (!IsLevelEnabled(LogEventLevel.Warning)) return;
            WriteLog(LogEventLevel.Warning, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Warning(string messageTemplate, params object[] propertyValues)
        {
            if (!IsLevelEnabled(LogEventLevel.Warning)) return;
            WriteLog(LogEventLevel.Warning, messageTemplate, propertyValues);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string message)
        {
            WriteLog(LogEventLevel.Error, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string message, Exception exception)
        {
            WriteLog(LogEventLevel.Error, exception, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string messageTemplate, params object[] propertyValues)
        {
            WriteLog(LogEventLevel.Error, messageTemplate, propertyValues);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fatal(string message)
        {
            WriteLog(LogEventLevel.Fatal, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fatal(string message, Exception exception)
        {
            WriteLog(LogEventLevel.Fatal, exception, message);
        }

        // INTERNAL HELPERS
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLevelEnabled(LogEventLevel level)
        {
            return _levelSwitch == null || level >= _levelSwitch.MinimumLevel;
        }

        private static void WriteLog(LogEventLevel level, string message)
        {
            _logger?.Write(level, message);

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
#endif
        }

        private static void WriteLog(LogEventLevel level, string messageTemplate, object[] propertyValues)
        {
            _logger?.Write(level, messageTemplate, propertyValues);

#if DEBUG
            // Best-effort debug output - just log template if formatting fails
            System.Diagnostics.Debug.WriteLine($"[{level}] {messageTemplate}");
#endif
        }

        private static void WriteLog(LogEventLevel level, Exception exception, string message)
        {
            _logger?.Write(level, exception, message);

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
            System.Diagnostics.Debug.WriteLine(exception.ToString());
#endif
        }
    }
}
