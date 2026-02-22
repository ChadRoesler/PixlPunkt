using Uno.UI.Hosting;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

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

        // On Windows, initialize Velopack for auto-updates
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            InitializeVelopackWindows();
        }

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }

    /// <summary>
    /// Initializes Velopack for Windows auto-update support.
    /// Must be called as early as possible in Main().
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void InitializeVelopackWindows()
    {
        try
        {
            // Cache exe path once - Process.GetCurrentProcess() allocates
            var exePath = Environment.ProcessPath
                          ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                          ?? System.Reflection.Assembly.GetExecutingAssembly().Location;

            Velopack.VelopackApp.Build()
                .OnFirstRun(v =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Velopack] First run! Version: {v}");
                    Core.FileAssociations.WindowsFileAssociations.Register(exePath);
                })
                .OnAfterInstallFastCallback(v =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Velopack] After install: {v}");
                    Core.FileAssociations.WindowsFileAssociations.Register(exePath);
                })
                .OnAfterUpdateFastCallback(v =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Velopack] After update: {v}");
                    Core.FileAssociations.WindowsFileAssociations.Register(exePath);
                })
                .OnBeforeUninstallFastCallback(v =>
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
}
