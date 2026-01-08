using Uno.UI.Hosting;

namespace PixlPunkt.Uno;

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

#if HAS_UNO_SKIA && WINDOWS
        // Velopack must be initialized as early as possible in app startup.
        // This handles Squirrel events during install/uninstall/update.
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                      ?? System.Reflection.Assembly.GetExecutingAssembly().Location;

        Velopack.VelopackApp.Build()
            .WithFirstRun(v => 
            {
                // Called on first run after install
                System.Diagnostics.Debug.WriteLine($"[Velopack] First run! Version: {v}");
                
                // Register file associations
                Core.FileAssociations.WindowsFileAssociations.Register(exePath);
            })
            .WithAfterInstallFastCallback(v =>
            {
                // Called immediately after install (before first run)
                System.Diagnostics.Debug.WriteLine($"[Velopack] After install: {v}");
                Core.FileAssociations.WindowsFileAssociations.Register(exePath);
            })
            .WithAfterUpdateFastCallback(v =>
            {
                // Called after an update is applied
                System.Diagnostics.Debug.WriteLine($"[Velopack] After update: {v}");
                // Re-register in case exe path changed
                Core.FileAssociations.WindowsFileAssociations.Register(exePath);
            })
            .WithBeforeUninstallFastCallback(v =>
            {
                // Called before uninstall
                System.Diagnostics.Debug.WriteLine($"[Velopack] Before uninstall: {v}");
                Core.FileAssociations.WindowsFileAssociations.Unregister();
            })
            .Run();
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
}
