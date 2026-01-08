using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PixlPunkt.Uno.Core.FileAssociations;

/// <summary>
/// Handles Windows file type associations for PixlPunkt.
/// Called during Velopack install/uninstall events.
/// </summary>
public static class WindowsFileAssociations
{
    private const string AppName = "PixlPunkt";
    private const string AppDescription = "PixlPunkt Pixel Art Editor";

    /// <summary>
    /// File type definitions for PixlPunkt.
    /// </summary>
    public static readonly FileTypeInfo[] FileTypes =
    [
        new(".pxp", "PixlPunkt.Document", "PixlPunkt Document", "document"),
        new(".pxpr", "PixlPunkt.AnimationReel", "PixlPunkt Animation Reel", "reel"),
        new(".pxpt", "PixlPunkt.Tileset", "PixlPunkt Tileset", "tileset"),
        new(".punk", "PixlPunkt.Plugin", "PixlPunkt Plugin", "plugin"),
        new(".mkr", "PixlPunkt.Marker", "PixlPunkt Marker", "marker"),
    ];

    /// <summary>
    /// Registers all PixlPunkt file associations.
    /// Called during Velopack install.
    /// </summary>
    /// <param name="exePath">Full path to PixlPunkt.Uno.exe</param>
    public static void Register(string exePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            var appDir = Path.GetDirectoryName(exePath) ?? "";
            var iconsDir = Path.Combine(appDir, "Assets", "Icons", "FileTypes");

            foreach (var fileType in FileTypes)
            {
                RegisterFileType(fileType, exePath, iconsDir);
            }

            // Register application capabilities
            RegisterApplicationCapabilities(exePath);

            // Notify shell of changes
            NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

            System.Diagnostics.Debug.WriteLine("[FileAssociations] Registered all file types");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileAssociations] Error registering: {ex.Message}");
        }
    }

    /// <summary>
    /// Unregisters all PixlPunkt file associations.
    /// Called during Velopack uninstall.
    /// </summary>
    public static void Unregister()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            foreach (var fileType in FileTypes)
            {
                UnregisterFileType(fileType);
            }

            // Remove application capabilities
            UnregisterApplicationCapabilities();

            // Notify shell of changes
            NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

            System.Diagnostics.Debug.WriteLine("[FileAssociations] Unregistered all file types");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileAssociations] Error unregistering: {ex.Message}");
        }
    }

    private static void RegisterFileType(FileTypeInfo fileType, string exePath, string iconsDir)
    {
        // Determine icon path - use specific icon if exists, otherwise use main exe
        var specificIcon = Path.Combine(iconsDir, $"{fileType.IconName}.ico");
        var iconPath = File.Exists(specificIcon) 
            ? $"\"{specificIcon}\",0" 
            : $"\"{exePath}\",0";

        // Create ProgId: HKCU\Software\Classes\PixlPunkt.Document
        using var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{fileType.ProgId}");
        if (progIdKey == null) return;

        progIdKey.SetValue("", fileType.Description);
        progIdKey.SetValue("FriendlyTypeName", fileType.Description);

        // Default icon
        using (var iconKey = progIdKey.CreateSubKey("DefaultIcon"))
        {
            iconKey?.SetValue("", iconPath);
        }

        // Shell\Open\Command
        using (var commandKey = progIdKey.CreateSubKey(@"shell\open\command"))
        {
            commandKey?.SetValue("", $"\"{exePath}\" \"%1\"");
        }

        // Shell\Open - friendly name
        using (var openKey = progIdKey.CreateSubKey(@"shell\open"))
        {
            openKey?.SetValue("FriendlyAppName", AppName);
        }

        // Associate extension with ProgId: HKCU\Software\Classes\.pxp
        using var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{fileType.Extension}");
        if (extKey == null) return;

        extKey.SetValue("", fileType.ProgId);
        extKey.SetValue("Content Type", GetContentType(fileType.Extension));
        extKey.SetValue("PerceivedType", "document");
        
        // OpenWithProgIds
        using var openWithKey = extKey.CreateSubKey("OpenWithProgIds");
        openWithKey?.SetValue(fileType.ProgId, "");
    }

    private static string GetContentType(string extension) => extension switch
    {
        ".pxp" => "application/x-pixlpunkt",
        ".pxpr" => "application/x-pixlpunkt-reel",
        ".pxpt" => "application/x-pixlpunkt-tileset",
        ".pbx" => "application/x-pixlpunkt-brush",
        ".mkr" => "application/x-pixlpunkt-marker",
        _ => "application/octet-stream"
    };

    private static void UnregisterFileType(FileTypeInfo fileType)
    {
        try
        {
            // Remove ProgId
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{fileType.ProgId}", false);
            
            // Remove extension association (be careful - only remove our ProgId reference)
            using var extKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{fileType.Extension}", true);
            if (extKey != null)
            {
                var defaultValue = extKey.GetValue("") as string;
                if (defaultValue == fileType.ProgId)
                {
                    extKey.DeleteValue("", false);
                }

                using var openWithKey = extKey.OpenSubKey("OpenWithProgIds", true);
                openWithKey?.DeleteValue(fileType.ProgId, false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileAssociations] Error unregistering {fileType.Extension}: {ex.Message}");
        }
    }

    private static void RegisterApplicationCapabilities(string exePath)
    {
        // Register app in RegisteredApplications
        using var regAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications");
        regAppsKey?.SetValue(AppName, $@"Software\{AppName}\Capabilities");

        // Create Capabilities key
        using var capKey = Registry.CurrentUser.CreateSubKey($@"Software\{AppName}\Capabilities");
        if (capKey == null) return;

        capKey.SetValue("ApplicationName", AppName);
        capKey.SetValue("ApplicationDescription", AppDescription);
        capKey.SetValue("ApplicationIcon", $"\"{exePath}\",0");

        // File associations under Capabilities
        using var assocKey = capKey.CreateSubKey("FileAssociations");
        if (assocKey != null)
        {
            foreach (var fileType in FileTypes)
            {
                assocKey.SetValue(fileType.Extension, fileType.ProgId);
            }
        }

        // MIME associations
        using var mimeKey = capKey.CreateSubKey("MimeAssociations");
        if (mimeKey != null)
        {
            mimeKey.SetValue("application/x-pixlpunkt", "PixlPunkt.Document");
            mimeKey.SetValue("application/x-pixlpunkt-reel", "PixlPunkt.AnimationReel");
            mimeKey.SetValue("application/x-pixlpunkt-tileset", "PixlPunkt.Tileset");
            mimeKey.SetValue("application/x-pixlpunkt-brush", "PixlPunkt.Brush");
            mimeKey.SetValue("application/x-pixlpunkt-marker", "PixlPunkt.Marker");
        }
    }

    private static void UnregisterApplicationCapabilities()
    {
        try
        {
            // Remove from RegisteredApplications
            using var regAppsKey = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true);
            regAppsKey?.DeleteValue(AppName, false);

            // Remove Capabilities
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\{AppName}", false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileAssociations] Error removing capabilities: {ex.Message}");
        }
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
    }
}

/// <summary>
/// Information about a file type association.
/// </summary>
public readonly record struct FileTypeInfo(
    string Extension,
    string ProgId,
    string Description,
    string IconName
);
