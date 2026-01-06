using System;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace PixlPunkt.Uno.Core.Logging
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
        /// Shuts down the logging service and flushes any pending log entries.
        /// </summary>
        public static void Shutdown()
        {
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

        // LOG METHODS
        public static void Debug(string message)
        {
            WriteLog(LogEventLevel.Debug, message);
        }

        public static void Debug(string messageTemplate, params object[] propertyValues)
        {
            WriteLog(LogEventLevel.Debug, messageTemplate, propertyValues);
        }

        public static void Info(string message)
        {
            WriteLog(LogEventLevel.Information, message);
        }

        public static void Info(string messageTemplate, params object[] propertyValues)
        {
            WriteLog(LogEventLevel.Information, messageTemplate, propertyValues);
        }

        public static void Warning(string message)
        {
            WriteLog(LogEventLevel.Warning, message);
        }

        public static void Warning(string messageTemplate, params object[] propertyValues)
        {
            WriteLog(LogEventLevel.Warning, messageTemplate, propertyValues);
        }

        public static void Error(string message)
        {
            WriteLog(LogEventLevel.Error, message);
        }

        public static void Error(string message, Exception exception)
        {
            WriteLog(LogEventLevel.Error, exception, message);
        }

        public static void Error(string messageTemplate, params object[] propertyValues)
        {
            WriteLog(LogEventLevel.Error, messageTemplate, propertyValues);
        }

        public static void Fatal(string message)
        {
            WriteLog(LogEventLevel.Fatal, message);
        }

        public static void Fatal(string message, Exception exception)
        {
            WriteLog(LogEventLevel.Fatal, exception, message);
        }

        // INTERNAL HELPERS
        private static void WriteLog(LogEventLevel level, string message)
        {
            if (_logger != null)
            {
                _logger.Write(level, message);
            }

            System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
        }

        private static void WriteLog(LogEventLevel level, string messageTemplate, object[] propertyValues)
        {
            if (_logger != null)
            {
                _logger.Write(level, messageTemplate, propertyValues);
            }

            try
            {
                var formattedMessage = string.Format(messageTemplate.Replace("{", "{{").Replace("}", "}}"), propertyValues);
                System.Diagnostics.Debug.WriteLine($"[{level}] {formattedMessage}");
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"[{level}] {messageTemplate}");
            }
        }

        private static void WriteLog(LogEventLevel level, Exception exception, string message)
        {
            if (_logger != null)
            {
                _logger.Write(level, exception, message);
            }

            System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
            System.Diagnostics.Debug.WriteLine(exception.ToString());
        }
    }
}
