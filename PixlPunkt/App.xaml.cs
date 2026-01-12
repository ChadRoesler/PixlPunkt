using Microsoft.UI.Xaml;
using PixlPunkt.Core.Brush;
using PixlPunkt.Core.Effects;
using PixlPunkt.Core.IO;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Plugins;
using PixlPunkt.Core.Settings;
using PixlPunkt.UI;
using PixlPunkt.UI.Helpers;
using Uno.Resizetizer;
#if NET10_0_DESKTOP || NET9_0_DESKTOP || NET8_0_DESKTOP
using System.IO;
using System.Linq;
#endif

namespace PixlPunkt;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// Cross-platform entry point for PixlPunkt.
/// </summary>
public partial class App : Application
{
    public static Window PixlPunktMainWindow { get; private set; } = null!;

    /// <summary>
    /// File path to open on startup (from command-line arguments or file association).
    /// </summary>
    public static string? StartupFilePath { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.
    /// This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();

        // Check for startup file from command-line arguments (file association)
        ProcessStartupArguments();

#if DEBUG
        System.Diagnostics.Debug.WriteLine("[PixlPunkt] App constructor completed");
#endif
    }

    /// <summary>
    /// Processes command-line arguments to extract file paths for file association handling.
    /// </summary>
    private void ProcessStartupArguments()
    {
        // Only Desktop (Skia on Windows/macOS/Linux) supports command-line file arguments
        // via the Program.StartupArgs property. This code only compiles for net10.0-desktop.
        // Other platforms handle file activation through platform-specific mechanisms.
#if NET10_0_DESKTOP || NET9_0_DESKTOP || NET8_0_DESKTOP
        try
        {
            // Get args from Desktop Program.cs
            var args = Program.StartupArgs;

            if (args != null && args.Length > 0)
            {
                // Find the first argument that looks like a file path
                var filePath = args.FirstOrDefault(arg =>
                    !string.IsNullOrWhiteSpace(arg) &&
                    !arg.StartsWith("-") &&
                    !arg.StartsWith("/") &&
                    (arg.EndsWith(".pxp", StringComparison.OrdinalIgnoreCase) ||
                     arg.EndsWith(".pxpr", StringComparison.OrdinalIgnoreCase) ||
                     arg.EndsWith(".pxpt", StringComparison.OrdinalIgnoreCase) ||
                     arg.EndsWith(".pbx", StringComparison.OrdinalIgnoreCase) ||
                     arg.EndsWith(".mkr", StringComparison.OrdinalIgnoreCase) ||
                     File.Exists(arg)));

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    StartupFilePath = Path.GetFullPath(filePath);
                    System.Diagnostics.Debug.WriteLine($"[PixlPunkt] Startup file: {StartupFilePath}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PixlPunkt] Error processing startup args: {ex.Message}");
        }
#endif
        // On other platforms (Android, iOS, WASM, WinAppSdk), file association
        // is handled through platform-specific mechanisms (OnFileActivated, etc.)
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Wire up exception handlers now that runtime is fully initialized
        SetupExceptionHandlers();

        // CRITICAL: Ensure the application data directory structure exists FIRST
        // This creates %LocalAppData%\PixlPunkt\ and all subdirectories
        // Must happen before ANY other initialization that might access these directories
        try
        {
            AppPaths.EnsureDirectoriesExist();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] CRITICAL: Failed to create app directories: {ex.Message}");
        }

        // Initialize logging (after directories exist so log files can be written)
        try
        {
            LoggingService.Initialize();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Failed to initialize logging: {ex.Message}");
        }

        // Load settings (will create default settings.json if not present)
        AppSettings settings;
        try
        {
            settings = AppSettings.Instance;
        }
        catch (Exception ex)
        {
            LoggingService.Warning("Failed to load settings, using defaults: {Error}", ex.Message);
            settings = new AppSettings();
        }

        // Apply configured log level from settings
        try
        {
            LoggingService.ApplyLogLevelFromSettings();
        }
        catch (Exception ex)
        {
            LoggingService.Debug("Failed to apply log level from settings: {Error}", ex.Message);
        }

        LoggingService.Info("PixlPunkt application starting");

        // Log startup file if present
        if (!string.IsNullOrEmpty(StartupFilePath))
        {
            LoggingService.Info("Startup file requested: {FilePath}", StartupFilePath);
        }

        // Register built-in effects before any documents are created
        try
        {
            BuiltInEffects.RegisterAll();
            LoggingService.Debug("Built-in effects registered");
        }
        catch (Exception ex)
        {
            LoggingService.Error("Failed to register built-in effects", ex);
        }

        // Load custom ASCII glyph sets from GlyphSets folder
        try
        {
            Core.Ascii.AsciiGlyphSets.LoadCustomSets();
            LoggingService.Debug("Custom ASCII glyph sets loaded");
        }
        catch (Exception ex)
        {
            LoggingService.Error("Failed to load custom ASCII glyph sets", ex);
        }

        // Register built-in import/export handlers
        try
        {
            BuiltInIOHandlers.RegisterAll();
            LoggingService.Debug("Built-in IO handlers registered");
        }
        catch (Exception ex)
        {
            LoggingService.Error("Failed to register built-in IO handlers", ex);
        }

        // Initialize custom brush service
        try
        {
            BrushDefinitionService.Instance.Initialize();
            LoggingService.Debug("Brush definition service initialized");
        }
        catch (Exception ex)
        {
            LoggingService.Error("Failed to initialize brush service", ex);
        }

        // Load plugins
        try
        {
            PluginRegistry.Instance.LoadAllPlugins();
            LoggingService.Info("Loaded {PluginCount} plugins", PluginRegistry.Instance.Count);
        }
        catch (Exception ex)
        {
            LoggingService.Error("Plugin loading failed", ex);
        }

        LoggingService.Debug("Application settings loaded");

        // Create and show main window
        var main = new PixlPunktMainWindow();

        // Apply persisted application theme choice
        try
        {
            main.SetAppTheme(settings.AppTheme);
            LoggingService.Debug("Applied app theme: {Theme}", settings.AppTheme);
        }
        catch (Exception ex)
        {
            LoggingService.Debug("Failed to apply persisted app theme on launch: {Error}", ex.Message);
        }

        // Apply persisted stripe theme choice
        try
        {
            main.SetStripeTheme(settings.StripeTheme);
        }
        catch (Exception ex)
        {
            LoggingService.Debug("Failed to apply persisted stripe theme on launch: {Error}", ex.Message);
        }

        PixlPunktMainWindow = main;
        MainWindow = main;
        
#if DEBUG
        MainWindow.UseStudio();
#endif
        
        PixlPunktMainWindow.Activate();

        // Set window icon AFTER activation to ensure window handle is available
        // This is especially important for Skia Desktop on Windows
        MainWindow.SetWindowIcon("Pixl Punkt");

        LoggingService.Info("PixlPunkt main window activated");
    }

    /// <summary>
    /// Sets up global exception handlers.
    /// Called from OnLaunched when the runtime is fully initialized.
    /// </summary>
    private void SetupExceptionHandlers()
    {
        this.UnhandledException += (s, e) =>
        {
            try
            {
                LoggingService.Fatal("XAML unhandled exception", e.Exception);
                LoggingService.Debug($"XAML unhandled: {e.Exception}");
                if (e.Exception.InnerException != null)
                {
                    LoggingService.Debug($"Inner exception: {e.Exception.InnerException}");
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"[App] Unhandled exception: {e.Exception}");
            }
            // e.Handled = true; // only if you want to swallow during dev
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                try
                {
                    LoggingService.Fatal("AppDomain unhandled exception", ex);
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"[App] AppDomain unhandled exception: {ex}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[App] AppDomain unhandled non-exception object: {e.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                LoggingService.Error("Unobserved task exception", e.Exception);
                LoggingService.Debug($"Unobserved task exception details: {e.Exception}");
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"[App] Unobserved task exception: {e.Exception}");
            }
            // e.SetObserved(); // optional
        };
    }
}
