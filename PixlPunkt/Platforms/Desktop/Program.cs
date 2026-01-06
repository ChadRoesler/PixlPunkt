using Uno.UI.Runtime.Skia.Win32;

namespace PixlPunkt;

/// <summary>
/// Desktop (Skia/Win32) platform entry point for Uno Platform.
/// </summary>
public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Initialize the Skia Win32 host before creating the App
        // This ensures the dispatcher and other Uno runtime components are initialized
        var host = new Win32Host(() => new App());
        host.Run();
    }
}
