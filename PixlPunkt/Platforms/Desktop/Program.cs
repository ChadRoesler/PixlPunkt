using Uno.UI.Hosting;

namespace PixlPunkt;

public class Program
{
    /// <summary>
    /// Stores command-line arguments for the app to access during startup.
    /// Used for file association handling (opening files by double-click).
    /// </summary>
    public static string[] StartupArgs { get; private set; } = [];

    [STAThread]
    public static void Main(string[] args)
    {
        // Store args for file association handling
        StartupArgs = args;

#if WINDOWS
        // Velopack must be initialized as early as possible in app startup.
        // This handles Squirrel events during install/uninstall/update.
        InitializeVelopack();
#endif

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }

#if WINDOWS
    /// <summary>
    /// Initializes Velopack for Windows auto-update support.
    /// Must be called as early as possible in Main().
    /// </summary>
    private static void InitializeVelopack()
    {
        try
        {
            // Cache exe path once - Process.GetCurrentProcess() allocates
            var exePath = Environment.ProcessPath
                          ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                          ?? System.Reflection.Assembly.GetExecutingAssembly().Location;

            Velopack.VelopackApp.Build()
                .WithFirstRun(v =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Velopack] First run! Version: {v}");
                    Core.FileAssociations.WindowsFileAssociations.Register(exePath);
                })
                .WithAfterInstallFastCallback(v =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Velopack] After install: {v}");
                    Core.FileAssociations.WindowsFileAssociations.Register(exePath);
                })
                .WithAfterUpdateFastCallback(v =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Velopack] After update: {v}");
                    Core.FileAssociations.WindowsFileAssociations.Register(exePath);
                })
                .WithBeforeUninstallFastCallback(v =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Velopack] Before uninstall: {v}");
                    Core.FileAssociations.WindowsFileAssociations.Unregister();
                })
                .Run();
        }
        catch (Exception ex)
        {
            // Don't crash if Velopack fails - the app can still run
            System.Diagnostics.Debug.WriteLine($"[Velopack] Initialization failed: {ex.Message}");
        }
    }
#endif
}
