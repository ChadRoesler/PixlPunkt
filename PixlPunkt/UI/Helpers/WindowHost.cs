using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Platform;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.Helpers
{
    /// <summary>
    /// Cross-platform window management utilities for Uno Platform.
    /// </summary>
    public static class WindowHost
    {
#if WINDOWS
        // Win32 API for setting window icon directly (more reliable for unpackaged apps)
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr hInstance, string lpIconName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;
        private const uint LR_DEFAULTSIZE = 0x00000040;
        private const uint WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
#endif

        // ════════════════════════════════════════════════════════════════════
        // THEME COLORS
        // ════════════════════════════════════════════════════════════════════

        private static (Color bg, Color fg) GetThemeColors(ElementTheme theme) => theme == ElementTheme.Light
            ? (Color.FromArgb(255, 243, 243, 243), Color.FromArgb(255, 0, 0, 0))
            : (Color.FromArgb(255, 32, 32, 32), Color.FromArgb(255, 255, 255, 255));

        // ════════════════════════════════════════════════════════════════════
        // PLATFORM DETECTION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets whether we're running on Windows (where WinRT interop is needed).
        /// </summary>
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Gets whether we're running on macOS.
        /// </summary>
        public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// Gets whether we're running on Linux.
        /// </summary>
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        /// <summary>
        /// Gets whether title bar customization is supported on the current platform.
        /// Only WinAppSdk (net10.0-windows) supports full title bar theming.
        /// </summary>
        private static bool SupportsTitleBarCustomization =>
#if WINDOWS
            // WinAppSdk supports title bar customization
            true;
#else
            // Skia Desktop, WASM, Android, iOS do not support AppWindow.TitleBar properties
            false;
#endif

        // ════════════════════════════════════════════════════════════════════
        // WINDOW SETUP
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies basic window configuration including window chrome, title bar theming, and icon.
        /// </summary>
        public static Window ApplyChrome(
            Window window,
            bool resizable = false,
            bool alwaysOnTop = false,
            bool minimizable = false,
            bool maximizable = false,
            string? title = null,
            Window? owner = null)
        {
            if (title is not null)
                window.Title = title;

            var effectiveTheme = GetEffectiveTheme();
            if (window.Content is FrameworkElement root)
                root.RequestedTheme = effectiveTheme;

            // Configure window presenter for proper chrome (min/max/close buttons)
            // Only supported on desktop platforms with windowing support
            try
            {
                var appWindow = window.AppWindow;
                if (appWindow != null)
                {
#if WINDOWS
                    // WinAppSdk supports OverlappedPresenter
                    var presenter = Microsoft.UI.Windowing.OverlappedPresenter.Create();
                    presenter.IsResizable = resizable;
                    presenter.IsMinimizable = minimizable;
                    presenter.IsMaximizable = maximizable;
                    presenter.IsAlwaysOnTop = alwaysOnTop;
                    appWindow.SetPresenter(presenter);
#endif

                    // Apply title bar colors to match theme (WinAppSdk only)
                    if (SupportsTitleBarCustomization)
                    {
                        ApplyTitleBarTheme(appWindow, effectiveTheme);
                    }

                    // Set window icon using AppWindow API
                    SetWindowIcon(appWindow);
                    
#if WINDOWS
                    // Also set the icon using Win32 API for better taskbar support
                    SetWindowIconWin32(window);
#endif
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WindowHost.ApplyChrome: Could not configure presenter: {ex.Message}");
            }

            return window;
        }

        /// <summary>
        /// Sets the window icon from the application's icon asset using AppWindow.SetIcon.
        /// </summary>
        private static void SetWindowIcon(Microsoft.UI.Windowing.AppWindow appWindow)
        {
            try
            {
                string? iconPath = FindIconPath();

                if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                    System.Diagnostics.Debug.WriteLine($"WindowHost.SetWindowIcon: Set icon from {iconPath}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"WindowHost.SetWindowIcon: Icon file not found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WindowHost.SetWindowIcon: Could not set window icon: {ex.Message}");
            }
        }

#if WINDOWS
        /// <summary>
        /// Sets the window icon using Win32 API for better taskbar/title bar support on unpackaged apps.
        /// </summary>
        private static void SetWindowIconWin32(Window window)
        {
            try
            {
                string? iconPath = FindIconPath();
                if (string.IsNullOrEmpty(iconPath) || !System.IO.File.Exists(iconPath))
                    return;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                if (hwnd == IntPtr.Zero)
                    return;

                // Load the icon from file
                IntPtr hIconBig = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
                IntPtr hIconSmall = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);

                if (hIconBig != IntPtr.Zero)
                {
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, hIconBig);
                    System.Diagnostics.Debug.WriteLine($"WindowHost.SetWindowIconWin32: Set big icon");
                }

                if (hIconSmall != IntPtr.Zero)
                {
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, hIconSmall);
                    System.Diagnostics.Debug.WriteLine($"WindowHost.SetWindowIconWin32: Set small icon");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WindowHost.SetWindowIconWin32: Could not set window icon: {ex.Message}");
            }
        }
#endif

        /// <summary>
        /// Finds the icon file path, checking multiple possible locations.
        /// </summary>
        private static string? FindIconPath()
        {
            var baseDir = AppContext.BaseDirectory;

            if (IsWindows)
            {
                // Try multiple possible locations for the icon
                var possiblePaths = new[]
                {
                    // Uno Resizetizer generates icon.ico in the output root
                    System.IO.Path.Combine(baseDir, "icon.ico"),
                    // Custom app icon in Assets folder
                    System.IO.Path.Combine(baseDir, "Assets", "Icons", "PixlPunkt.ico"),
                    // Fallback locations
                    System.IO.Path.Combine(baseDir, "PixlPunkt.ico"),
                    System.IO.Path.Combine(baseDir, "Assets", "PixlPunkt.ico")
                };

                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                }
            }
            else
            {
                // For Linux/macOS, use PNG
                var possiblePaths = new[]
                {
                    System.IO.Path.Combine(baseDir, "Assets", "Icons", "Icon.png"),
                    System.IO.Path.Combine(baseDir, "Icon.png"),
                    System.IO.Path.Combine(baseDir, "Assets", "Icon.png")
                };

                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Applies theme colors to the window's title bar.
        /// Only supported on WinAppSdk (net10.0-windows).
        /// </summary>
        private static void ApplyTitleBarTheme(Microsoft.UI.Windowing.AppWindow appWindow, ElementTheme theme)
        {
#if WINDOWS
            // Title bar customization is only available on WinAppSdk
            try
            {
                var titleBar = appWindow.TitleBar;
                if (titleBar == null) return;

                var (bg, fg) = GetThemeColors(theme);

                // Title bar background and text
                titleBar.BackgroundColor = bg;
                titleBar.ForegroundColor = fg;

                // Inactive state colors (slightly muted)
                titleBar.InactiveBackgroundColor = bg;
                titleBar.InactiveForegroundColor = theme == ElementTheme.Light
                    ? Color.FromArgb(255, 100, 100, 100)
                    : Color.FromArgb(255, 150, 150, 150);

                // Button colors
                titleBar.ButtonBackgroundColor = bg;
                titleBar.ButtonForegroundColor = fg;
                titleBar.ButtonInactiveBackgroundColor = bg;
                titleBar.ButtonInactiveForegroundColor = titleBar.InactiveForegroundColor;

                // Button hover colors
                titleBar.ButtonHoverBackgroundColor = theme == ElementTheme.Light
                    ? Color.FromArgb(255, 220, 220, 220)
                    : Color.FromArgb(255, 60, 60, 60);
                titleBar.ButtonHoverForegroundColor = fg;

                // Button pressed colors
                titleBar.ButtonPressedBackgroundColor = theme == ElementTheme.Light
                    ? Color.FromArgb(255, 200, 200, 200)
                    : Color.FromArgb(255, 80, 80, 80);
                titleBar.ButtonPressedForegroundColor = fg;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WindowHost.ApplyTitleBarTheme: Could not apply title bar theme: {ex.Message}");
            }
#endif
            // On non-Windows platforms, title bar theming is not supported
        }

        /// <summary>
        /// Gets the effective theme based on app settings.
        /// </summary>
        public static ElementTheme GetEffectiveTheme()
        {
            try
            {
                var settings = Core.Settings.AppSettings.Instance;
                return settings.AppTheme switch
                {
                    Core.Settings.AppThemeChoice.Light => ElementTheme.Light,
                    Core.Settings.AppThemeChoice.Dark => ElementTheme.Dark,
                    _ => ElementTheme.Dark
                };
            }
            catch
            {
                return ElementTheme.Dark;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // WINDOW SIZING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sizes window content to fit and resizes the window to match.
        /// </summary>
        public static Window FitToContent(
            Window window,
            FrameworkElement root,
            double maxScreenFraction = 0.90,
            double minWidth = 480,
            double minHeight = 360)
        {
            root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = root.DesiredSize;

            double contentWidth = Math.Max(minWidth, desired.Width);
            double contentHeight = Math.Max(minHeight, desired.Height);

            root.Width = contentWidth;
            root.Height = contentHeight;

            // Also resize the window itself to fit the content
            try
            {
                var appWindow = window.AppWindow;
                if (appWindow != null)
                {
                    // Add some padding for window chrome (title bar, borders)
                    // This is approximate - different platforms may have different chrome sizes
                    int chromeHeight = 32; // Title bar height
                    int chromeBorder = 2;  // Window border

                    int windowWidth = (int)contentWidth + (chromeBorder * 2);
                    int windowHeight = (int)contentHeight + chromeHeight + chromeBorder;

                    var size = new Windows.Graphics.SizeInt32 { Width = windowWidth, Height = windowHeight };
                    appWindow.Resize(size);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WindowHost.FitToContent: Could not resize window: {ex.Message}");
            }

            return window;
        }

        /// <summary>
        /// Defers content sizing until after layout.
        /// </summary>
        public static void FitToContentAfterLayout(Window window, FrameworkElement root,
            double maxScreenFraction = 0.90, double minWidth = 480, double minHeight = 360)
        {
            void OnLayoutUpdated(object? _, object __)
            {
                root.LayoutUpdated -= OnLayoutUpdated;
                FitToContent(window, root, maxScreenFraction, minWidth, minHeight);
            }
            root.LayoutUpdated += OnLayoutUpdated;
        }

        /// <summary>
        /// Defers content sizing until after layout (compatibility overload).
        /// </summary>
        public static void FitToContentAfterLayout(Window window, FrameworkElement root,
            double maxScreenFraction = 0.90, double minLogicalWidth = 480, double minLogicalHeight = 360, bool _compat = true)
        {
            FitToContentAfterLayout(window, root, maxScreenFraction, minLogicalWidth, minLogicalHeight);
        }

        // ════════════════════════════════════════════════════════════════════
        // WINDOW POSITIONING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Places a window (platform-dependent).
        /// </summary>
        public static void Place(Window child, WindowPlacement placement, Window? parent = null,
            FrameworkElement? anchor = null, int offsetX = 16, int offsetY = 16)
        {
            // Window positioning is platform-specific and handled by the window manager
        }

        /// <summary>
        /// Shows a tool window with standard configuration.
        /// </summary>
        public static Window ShowToolWindow(
            Window window,
            string title,
            bool resizable = false,
            bool minimizable = false,
            double minWidth = 560,
            double minHeight = 360,
            double maxScreenFraction = 0.90,
            Window? owner = null)
        {
            ApplyChrome(window, resizable: resizable, alwaysOnTop: false, minimizable: minimizable, maximizable: false, title: title, owner: owner);
            ApplyThemeToWindow(window);

            if (window.Content is FrameworkElement root)
                FitToContentAfterLayout(window, root, maxScreenFraction, minWidth, minHeight);

            return window;
        }

        /// <summary>
        /// Applies current app theme to window content.
        /// </summary>
        public static void ApplyThemeToWindow(Window window)
        {
            if (window.Content is FrameworkElement root)
                root.RequestedTheme = GetEffectiveTheme();
        }

        /// <summary>
        /// Updates window theme.
        /// </summary>
        public static void UpdateWindowTheme(Window window, ElementTheme theme)
        {
            if (window.Content is FrameworkElement root)
                root.RequestedTheme = theme;

            // Also update title bar colors (WinAppSdk only)
            if (SupportsTitleBarCustomization)
            {
                try
                {
                    var appWindow = window.AppWindow;
                    if (appWindow != null)
                    {
                        ApplyTitleBarTheme(appWindow, theme);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WindowHost.UpdateWindowTheme: Could not update title bar: {ex.Message}");
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // FILE PICKERS - Cross-platform with proper initialization
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initializes a picker with the window handle (Windows-only, no-op on other platforms).
        /// </summary>
        private static void InitializePickerWithWindow(object picker, Window window)
        {
#if WINDOWS
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize picker with window: {ex.Message}");
            }
#endif
            // On Linux/macOS/WASM, Uno Platform handles picker initialization automatically
        }

        /// <summary>
        /// Creates and initializes a file open picker (cross-platform).
        /// </summary>
        public static Windows.Storage.Pickers.FileOpenPicker CreateFileOpenPicker(Window window, params string[] fileTypes)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };

            foreach (var type in fileTypes)
                picker.FileTypeFilter.Add(type);

            InitializePickerWithWindow(picker, window);

            return picker;
        }

        /// <summary>
        /// Creates and initializes a file save picker (cross-platform).
        /// </summary>
        public static Windows.Storage.Pickers.FileSavePicker CreateFileSavePicker(
            Window window,
            string suggestedFileName,
            params string[] fileTypes)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedFileName
            };

            foreach (var type in fileTypes)
                picker.FileTypeChoices.Add(type, new[] { type });

            InitializePickerWithWindow(picker, window);

            return picker;
        }

        /// <summary>
        /// Creates and initializes a folder picker (cross-platform).
        /// </summary>
        public static Windows.Storage.Pickers.FolderPicker CreateFolderPicker(
            Window window,
            Windows.Storage.Pickers.PickerLocationId startLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = startLocation
            };

            // FolderPicker requires at least one file type filter on some platforms
            picker.FileTypeFilter.Add("*");

            InitializePickerWithWindow(picker, window);

            return picker;
        }

        /// <summary>
        /// Safely sets the default file extension on a FileSavePicker.
        /// This property is not implemented on all platforms (Android, iOS).
        /// </summary>
        public static void TrySetDefaultFileExtension(Windows.Storage.Pickers.FileSavePicker picker, string extension)
        {
            // DefaultFileExtension is only supported on Windows/Desktop platforms
            // On Android/iOS, this property throws NotImplementedException
#if WINDOWS || HAS_UNO_SKIA
            try
            {
                picker.DefaultFileExtension = extension;
            }
            catch (NotImplementedException)
            {
                // Silently ignore on platforms that don't support this
                System.Diagnostics.Debug.WriteLine($"DefaultFileExtension not supported on this platform");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not set DefaultFileExtension: {ex.Message}");
            }
#endif
        }
    }
}
