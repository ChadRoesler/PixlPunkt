using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using PixlPunkt.Core.Enums;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace PixlPunkt.UI.Helpers
{
    /// <summary>
    /// Provides window management utilities for WinUI 3 including chrome configuration, DPI-aware sizing, and positioning.
    /// </summary>
    /// <remarks>
    /// WindowHost offers comprehensive window management with proper DPI handling, system chrome configuration,
    /// content-based sizing, and intelligent positioning across multiple displays.
    /// </remarks>
    public static class WindowHost
    {
        // ════════════════════════════════════════════════════════════════════
        // WIN32 INTEROP CONSTANTS
        // ════════════════════════════════════════════════════════════════════

        const int SM_CXSIZEFRAME = 32, SM_CYSIZEFRAME = 33, SM_CXPADDEDBORDER = 92, SM_CYCAPTION = 4;
        const int GWL_HWNDPARENT = -8;

        // ════════════════════════════════════════════════════════════════════
        // TITLE BAR COLORS - Dynamic based on theme
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets title bar colors based on the specified theme.
        /// </summary>
        private static (Color bg, Color bgInactive, Color fg, Color fgInactive, Color hoverBg, Color pressedBg) GetTitleBarColors(ElementTheme theme)
        {
            if (theme == ElementTheme.Light)
            {
                return (
                    bg: Color.FromArgb(255, 243, 243, 243),      // Light gray background
                    bgInactive: Color.FromArgb(255, 249, 249, 249),
                    fg: Color.FromArgb(255, 0, 0, 0),            // Black text
                    fgInactive: Color.FromArgb(255, 100, 100, 100),
                    hoverBg: Color.FromArgb(255, 230, 230, 230),
                    pressedBg: Color.FromArgb(255, 220, 220, 220)
                );
            }
            else
            {
                // Dark theme (default)
                return (
                    bg: Color.FromArgb(255, 32, 32, 32),         // Dark background
                    bgInactive: Color.FromArgb(255, 43, 43, 43),
                    fg: Colors.White,                            // White text
                    fgInactive: Color.FromArgb(255, 140, 140, 140),
                    hoverBg: Color.FromArgb(255, 50, 50, 50),
                    pressedBg: Color.FromArgb(255, 60, 60, 60)
                );
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // WIN32 INTEROP - OWNER WINDOW
        // ════════════════════════════════════════════════════════════════════

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

        /// <summary>
        /// Sets the owner window for a child window, making it stay on top of the owner only.
        /// </summary>
        /// <param name="child">The child window that should stay on top of the owner.</param>
        /// <param name="owner">The owner window. If null, removes any existing owner.</param>
        /// <remarks>
        /// <para>
        /// This uses the Win32 concept of window ownership (GWL_HWNDPARENT / GWLP_HWNDPARENT).
        /// An owned window:
        /// </para>
        /// <list type="bullet">
        /// <item>Always appears above its owner in Z-order</item>
        /// <item>Is minimized when the owner is minimized</item>
        /// <item>Is hidden when the owner is hidden</item>
        /// <item>Does NOT block interaction with the owner (not modal)</item>
        /// </list>
        /// <para>
        /// This is different from <c>IsAlwaysOnTop</c> which keeps the window above ALL windows.
        /// Use this for tool windows that should float above the main app but not above other apps.
        /// </para>
        /// </remarks>
        public static void SetOwner(Window child, Window? owner)
        {
            var childHwnd = WindowNative.GetWindowHandle(child);
            var ownerHwnd = owner is null ? nint.Zero : WindowNative.GetWindowHandle(owner);
            SetWindowLongPtr(childHwnd, GWL_HWNDPARENT, ownerHwnd);
        }

        /// <summary>
        /// Sets the owner window using AppWindow references.
        /// </summary>
        public static void SetOwner(AppWindow child, AppWindow? owner)
        {
            var childHwnd = Win32Interop.GetWindowFromWindowId(child.Id);
            var ownerHwnd = owner is null ? nint.Zero : Win32Interop.GetWindowFromWindowId(owner.Id);
            SetWindowLongPtr(childHwnd, GWL_HWNDPARENT, ownerHwnd);
        }

        // ════════════════════════════════════════════════════════════════════
        // CHROME & WINDOW SETUP
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies system chrome flags to a window (title bar, resizability, always-on-top, etc.).
        /// </summary>
        /// <param name="window">Target window to configure.</param>
        /// <param name="resizable">If true, enables resize and maximize buttons. Default is false.</param>
        /// <param name="alwaysOnTop">If true, keeps window above all others. Default is false for owned windows.</param>
        /// <param name="minimizable">If true, enables minimize button. Default is false.</param>
        /// <param name="title">Optional window title. If null, leaves existing title unchanged.</param>
        /// <param name="owner">Optional owner window. If set, the window stays on top of owner only (not all windows).</param>
        /// <returns>The window's <see cref="AppWindow"/> for further configuration or chaining.</returns>
        /// <remarks>
        /// <para>
        /// Configures OverlappedPresenter properties for standard chrome control. Attempts to set window icon
        /// from "Assets/Icons/PixlPunkt.ico" for both title bar and taskbar. Silently ignores missing icon file.
        /// </para>
        /// <para>
        /// When <paramref name="owner"/> is provided, the window will stay above the owner but not above
        /// other applications. This is the preferred behavior for tool windows.
        /// </para>
        /// <para>
        /// Title bar colors are determined by the current app theme setting for visual consistency.
        /// The window content's RequestedTheme is also set to match the app's effective theme.
        /// </para>
        /// </remarks>
        public static AppWindow ApplyChrome(
            Window window,
            bool resizable = false,
            bool alwaysOnTop = false,
            bool minimizable = false,
            string? title = null,
            Window? owner = null)
        {
            var appW = GetAppWindow(window);
            if (title is not null) appW.Title = title;

            // Set window icon
            if (!string.IsNullOrEmpty("Assets/Icons/PixlPunkt.ico"))
            {
                try { appW.SetTitleBarIcon("Assets/Icons/PixlPunkt.ico"); } catch { /* ignore if missing */ }
                try { appW.SetIcon("Assets/Icons/PixlPunkt.ico"); } catch { /* ignore if missing */ }
            }

            // Get effective theme from app settings
            var effectiveTheme = GetEffectiveTheme();

            // Apply theme to window content so theme resources resolve correctly
            if (window.Content is FrameworkElement root)
            {
                root.RequestedTheme = effectiveTheme;
            }

            // Configure title bar colors based on current app theme
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var tb = appW.TitleBar;
                tb.ExtendsContentIntoTitleBar = false;

                var colors = GetTitleBarColors(effectiveTheme);

                // Background colors
                tb.BackgroundColor = colors.bg;
                tb.InactiveBackgroundColor = colors.bgInactive;

                // Button background colors (match title bar)
                tb.ButtonBackgroundColor = colors.bg;
                tb.ButtonInactiveBackgroundColor = colors.bgInactive;
                tb.ButtonHoverBackgroundColor = colors.hoverBg;
                tb.ButtonPressedBackgroundColor = colors.pressedBg;

                // Foreground colors
                tb.ForegroundColor = colors.fg;
                tb.InactiveForegroundColor = colors.fgInactive;
                tb.ButtonForegroundColor = colors.fg;
                tb.ButtonInactiveForegroundColor = colors.fgInactive;
                tb.ButtonHoverForegroundColor = colors.fg;
                tb.ButtonPressedForegroundColor = colors.fg;
            }

            // Configure presenter (resize, maximize, minimize, always-on-top)
            if (appW.Presenter is OverlappedPresenter ov)
            {
                ov.SetBorderAndTitleBar(true, true);
                ov.IsResizable = resizable;
                ov.IsMaximizable = resizable;
                ov.IsMinimizable = minimizable;
                // Only use system always-on-top if no owner is set and explicitly requested
                ov.IsAlwaysOnTop = alwaysOnTop && owner is null;
            }

            // Set owner window for "on top of main window only" behavior
            if (owner is not null)
            {
                SetOwner(window, owner);
            }

            return appW;
        }

        /// <summary>
        /// Gets the effective theme based on app settings.
        /// </summary>
        private static ElementTheme GetEffectiveTheme()
        {
            try
            {
                var settings = PixlPunkt.Core.Settings.AppSettings.Instance;
                return settings.AppTheme switch
                {
                    PixlPunkt.Core.Settings.AppThemeChoice.Light => ElementTheme.Light,
                    PixlPunkt.Core.Settings.AppThemeChoice.Dark => ElementTheme.Dark,
                    _ => GetSystemTheme()
                };
            }
            catch
            {
                return ElementTheme.Dark;
            }
        }

        /// <summary>
        /// Gets the system theme preference.
        /// </summary>
        private static ElementTheme GetSystemTheme()
        {
            try
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var foreground = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);
                // If foreground is light (close to white), system is in dark mode
                return foreground.R > 128 ? ElementTheme.Dark : ElementTheme.Light;
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
        /// Resizes a window to fit its content, accounting for DPI, non-client area, and work area constraints.
        /// </summary>
        /// <param name="window">Target window to resize.</param>
        /// <param name="root">Root content element whose desired size determines window dimensions.</param>
        /// <param name="maxScreenFraction">Maximum fraction of work area to occupy (0.0-1.0). Default is 0.90 (90%).</param>
        /// <param name="minLogicalWidth">Minimum content width in logical (DPI-independent) pixels. Default is 480.</param>
        /// <param name="minLogicalHeight">Minimum content height in logical pixels. Default is 360.</param>
        /// <returns>The window's <see cref="AppWindow"/> for chaining operations.</returns>
        /// <remarks>
        /// <para><strong>DPI-Aware Sizing Algorithm:</strong></para>
        /// <list type="number">
        /// <item>Get work area (screen minus taskbar) in physical pixels</item>
        /// <item>Get rasterization scale (DPI scaling factor)</item>
        /// <item>Set root.MaxWidth/MaxHeight to work area fraction in logical pixels</item>
        /// <item>Measure root with infinite bounds to get desired size</item>
        /// <item>Apply min/max constraints in logical pixels</item>
        /// <item>Convert to physical pixels using rasterization scale</item>
        /// <item>Add non-client area (title bar + window frame) via Win32 metrics</item>
        /// <item>Cap total size to work area fraction</item>
        /// <item>Resize window to final physical pixel dimensions</item>
        /// </list>
        /// <para><strong>Non-Client Area Calculation:</strong></para>
        /// <para>
        /// Uses Win32 GetSystemMetricsForDpi to query window-specific DPI for accurate frame sizes:
        /// <br/>- Frame thickness: SM_CXSIZEFRAME + SM_CXPADDEDBORDER (left/right)
        /// <br/>- Frame thickness: SM_CYSIZEFRAME + SM_CXPADDEDBORDER (top/bottom)
        /// <br/>- Caption height: SM_CYCAPTION (title bar)
        /// </para>
        /// <para>
        /// This ensures the content area matches requested size after adding window chrome.
        /// </para>
        /// </remarks>
        public static AppWindow FitToContent(
            Window window,
            FrameworkElement root,
            double maxScreenFraction = 0.90,
            double minLogicalWidth = 480,
            double minLogicalHeight = 360)
        {
            var appW = GetAppWindow(window);
            var (ncW, ncH) = GetNonClientPx(window);   // title bar + frame (px)
            var work = GetWorkAreaRect(appW);   // usable screen (px)
            double scale = root.XamlRoot?.RasterizationScale ?? 1.0;

            // cap content size (logical) so Measure won't request infinity
            root.MaxWidth = work.Width / scale * maxScreenFraction;
            root.MaxHeight = work.Height / scale * maxScreenFraction;

            root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = root.DesiredSize;

            double logicalW = Math.Max(minLogicalWidth, Math.Min(desired.Width, root.MaxWidth));
            double logicalH = Math.Max(minLogicalHeight, Math.Min(desired.Height, root.MaxHeight));

            int contentPxW = (int)Math.Ceiling(logicalW * scale);
            int contentPxH = (int)Math.Ceiling(logicalH * scale);

            // add non-client so the content has the room it asked for
            int totalPxW = contentPxW + ncW;
            int totalPxH = contentPxH + ncH;

            // cap against work-area
            totalPxW = Math.Min(totalPxW, (int)(work.Width * maxScreenFraction));
            totalPxH = Math.Min(totalPxH, (int)(work.Height * maxScreenFraction));

            appW.Resize(new SizeInt32(totalPxW, totalPxH));
            return appW;
        }

        /// <summary>
        /// Defers <see cref="FitToContent"/> until after the next layout pass completes.
        /// </summary>
        /// <param name="window">Target window to resize.</param>
        /// <param name="root">Root content element to measure.</param>
        /// <param name="maxScreenFraction">Maximum fraction of work area (0.0-1.0). Default is 0.90.</param>
        /// <param name="minLogicalWidth">Minimum content width in logical pixels. Default is 480.</param>
        /// <param name="minLogicalHeight">Minimum content height in logical pixels. Default is 360.</param>
        /// <remarks>
        /// <para>
        /// Use this when content hasn't been measured yet (e.g., immediately after window creation or
        /// content changes). Attaches to <see cref="FrameworkElement.LayoutUpdated"/> event, removes
        /// handler after first fire, then calls <see cref="FitToContent"/>.
        /// </para>
        /// <para><strong>When to Use:</strong></para>
        /// <para>
        /// - Window just created, content not yet laid out (ActualWidth/Height = 0)
        /// <br/>- Content dynamically changed and needs remeasure
        /// <br/>- Controls with deferred loading (e.g., ItemsControl with data binding)
        /// </para>
        /// <para>
        /// This pattern prevents sizing errors when XAML controls haven't computed their desired sizes yet.
        /// </para>
        /// </remarks>
        public static void FitToContentAfterLayout(Window window, FrameworkElement root,
            double maxScreenFraction = 0.90, double minLogicalWidth = 480, double minLogicalHeight = 360)
        {
            void OnLayoutUpdated(object? _, object __)
            {
                root.LayoutUpdated -= OnLayoutUpdated;
                FitToContent(window, root, maxScreenFraction, minLogicalWidth, minLogicalHeight);
            }
            root.LayoutUpdated += OnLayoutUpdated;
        }

        // ════════════════════════════════════════════════════════════════════
        // WINDOW POSITIONING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Positions a child window relative to a parent window or screen using specified placement strategy.
        /// </summary>
        /// <param name="child">AppWindow to position.</param>
        /// <param name="placement">Positioning strategy from <see cref="WindowPlacement"/> enum.</param>
        /// <param name="parent">Optional parent window for relative positioning. If null, uses primary screen bounds.</param>
        /// <param name="anchor">Optional XAML element for <see cref="WindowPlacement.NearAnchor"/> mode. Ignored for other modes.</param>
        /// <param name="offsetX">Horizontal offset in physical pixels for anchor/edge placement. Default is 16.</param>
        /// <param name="offsetY">Vertical offset in physical pixels for anchor/edge placement. Default is 16.</param>
        /// <remarks>
        /// <para><strong>Placement Modes:</strong></para>
        /// <list type="table">
        /// <listheader>
        /// <term>Mode</term>
        /// <description>Behavior</description>
        /// </listheader>
        /// <item>
        /// <term>CenterOnParent / CenterOnScreen</term>
        /// <description>Centers child within parent bounds (or screen if no parent)</description>
        /// </item>
        /// <item>
        /// <term>CenterTop</term>
        /// <description>Centers horizontally, positions offsetY pixels from top edge</description>
        /// </item>
        /// <item>
        /// <term>CenterBottom</term>
        /// <description>Centers horizontally, positions offsetY pixels from bottom edge</description>
        /// </item>
        /// <item>
        /// <term>NearAnchor</term>
        /// <description>Positions relative to anchor element using TransformToVisual with DPI scaling</description>
        /// </item>
        /// </list>
        /// <para><strong>Multi-Display Support:</strong></para>
        /// <para>
        /// Uses DisplayArea.GetFromWindowId to determine correct work area when positioning across
        /// multiple monitors with different DPI settings. Ensures window appears on same display as parent.
        /// </para>
        /// </remarks>
        public static void Place(
            AppWindow child,
            WindowPlacement placement,
            Window? parent = null,
            FrameworkElement? anchor = null,
            int offsetX = 16,
            int offsetY = 16)
        {
            var parentApp = parent is null ? null : GetAppWindow(parent);
            var work = GetWorkAreaRect(child);

            var basePos = parentApp?.Position ?? new PointInt32(work.X, work.Y);
            var baseSize = parentApp?.Size ?? new SizeInt32(work.Width, work.Height);

            var centeredX = basePos.X + (baseSize.Width - child.Size.Width) / 2;
            var centeredY = basePos.Y + (baseSize.Height - child.Size.Height) / 2;

            PointInt32 pos = placement switch
            {
                WindowPlacement.CenterOnParent => new(centeredX, centeredY),
                WindowPlacement.CenterTop => new(centeredX, basePos.Y + offsetY),
                WindowPlacement.CenterBottom => new(centeredX, basePos.Y + baseSize.Height - child.Size.Height - offsetY),
                WindowPlacement.NearAnchor when parent is not null && anchor is not null =>
                    NearAnchor(parent, anchor, basePos, offsetX, offsetY),
                _ => new(centeredX, centeredY),
            };

            child.Move(pos);
        }

        /// <summary>
        /// Configures and shows a tool window (palette, inspector, settings) with standard chrome and positioning.
        /// </summary>
        /// <param name="window">Window to configure.</param>
        /// <param name="title">Window title text.</param>
        /// <param name="resizable">Enable resize/maximize buttons. Default is false (fixed size).</param>
        /// <param name="minimizable">Enable minimize button. Default is false.</param>
        /// <param name="minLogicalWidth">Minimum content width in logical pixels. Default is 560.</param>
        /// <param name="minLogicalHeight">Minimum content height in logical pixels. Default is 360.</param>
        /// <param name="maxScreenFraction">Maximum screen fraction (0.0-1.0). Default is 0.90.</param>
        /// <param name="owner">Owner window (tool window stays on top of this). Defaults to main window.</param>
        /// <returns>The window's <see cref="AppWindow"/> for further customization if needed.</returns>
        /// <remarks>
        /// <para><strong>One-Shot Configuration:</strong></para>
        /// <para>
        /// Convenience method that combines three operations in standard order:
        /// <br/>1. <see cref="ApplyChrome"/> - Configure system chrome settings with owner
        /// <br/>2. <see cref="FitToContentAfterLayout"/> - Size window to content with DPI handling
        /// <br/>3. <see cref="Place"/> - Center window on main application window
        /// </para>
        /// <para>
        /// The tool window will stay on top of the owner window (main window by default) but
        /// not on top of other applications. This mimics traditional tool window behavior.
        /// </para>
        /// </remarks>
        public static AppWindow ShowToolWindow(
            Window window,
            string title,
            bool resizable = false,
            bool minimizable = false,
            double minLogicalWidth = 560,
            double minLogicalHeight = 360,
            double maxScreenFraction = 0.90,
            Window? owner = null)
        {
            // Default to main window as owner
            owner ??= App.PixlPunktMainWindow;

            var appW = ApplyChrome(
                window,
                resizable: resizable,
                alwaysOnTop: false,  // Don't use system always-on-top
                minimizable: minimizable,
                title: title,
                owner: owner);

            // Apply theme to window content
            ApplyThemeToWindow(window);

            FitToContentAfterLayout(window, (FrameworkElement)window.Content, maxScreenFraction, minLogicalWidth, minLogicalHeight);
            Place(appW, WindowPlacement.CenterOnScreen, owner);

            return appW;
        }

        /// <summary>
        /// Applies the current app theme to a window's content.
        /// </summary>
        /// <param name="window">The window to apply the theme to.</param>
        /// <remarks>
        /// This sets the RequestedTheme on the root FrameworkElement of the window's content,
        /// ensuring that all theme resources resolve correctly based on the app's theme setting.
        /// </remarks>
        public static void ApplyThemeToWindow(Window window)
        {
            if (window.Content is FrameworkElement root)
            {
                root.RequestedTheme = GetEffectiveTheme();
            }
        }

        /// <summary>
        /// Updates the theme on all child windows.
        /// Call this when the app theme changes.
        /// </summary>
        /// <param name="window">The window to update.</param>
        /// <param name="theme">The new theme to apply.</param>
        public static void UpdateWindowTheme(Window window, ElementTheme theme)
        {
            if (window.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
            }

            // Update title bar colors
            var appW = GetAppWindow(window);
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var tb = appW.TitleBar;
                var colors = GetTitleBarColors(theme);

                tb.BackgroundColor = colors.bg;
                tb.InactiveBackgroundColor = colors.bgInactive;
                tb.ButtonBackgroundColor = colors.bg;
                tb.ButtonInactiveBackgroundColor = colors.bgInactive;
                tb.ButtonHoverBackgroundColor = colors.hoverBg;
                tb.ButtonPressedBackgroundColor = colors.pressedBg;
                tb.ForegroundColor = colors.fg;
                tb.InactiveForegroundColor = colors.fgInactive;
                tb.ButtonForegroundColor = colors.fg;
                tb.ButtonInactiveForegroundColor = colors.fgInactive;
                tb.ButtonHoverForegroundColor = colors.fg;
                tb.ButtonPressedForegroundColor = colors.fg;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        [DllImport("user32.dll")]
        static extern uint GetDpiForWindow(nint hwnd);

        [DllImport("user32.dll")]
        static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

        static (int w, int h) GetNonClientPx(Window w)
        {
            var hwnd = WindowNative.GetWindowHandle(w);
            uint dpi = GetDpiForWindow(hwnd);
            int frameX = GetSystemMetricsForDpi(SM_CXSIZEFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
            int frameY = GetSystemMetricsForDpi(SM_CYSIZEFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
            int cap = GetSystemMetricsForDpi(SM_CYCAPTION, dpi);
            return (frameX * 2, frameY * 2 + cap);
        }

        public static AppWindow GetAppWindow(Window w)
        {
            var hwnd = WindowNative.GetWindowHandle(w);
            var id = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(id);
        }

        static RectInt32 GetWorkAreaRect(AppWindow appW) =>
            DisplayArea.GetFromWindowId(appW.Id, DisplayAreaFallback.Primary).WorkArea;

        static PointInt32 NearAnchor(Window parent, FrameworkElement anchor, PointInt32 basePos, int dx, int dy)
        {
            double scale = anchor.XamlRoot?.RasterizationScale ?? 1.0;
            var pt = anchor.TransformToVisual(null).TransformPoint(new Point(0, 0));
            return new PointInt32(basePos.X + (int)(pt.X * scale) + dx,
                basePos.Y + (int)(pt.Y * scale) + dy);
        }

        // ════════════════════════════════════════════════════════════════════
        // FILE PICKER HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates and initializes a FileOpenPicker for the window.
        /// </summary>
        public static FileOpenPicker CreateFileOpenPicker(Window window, params string[] fileTypes)
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);

            foreach (var type in fileTypes)
                picker.FileTypeFilter.Add(type);

            return picker;
        }

        /// <summary>
        /// Creates and initializes a FileSavePicker for the window.
        /// </summary>
        public static FileSavePicker CreateFileSavePicker(
            Window window,
            string suggestedFileName,
            params string[] fileTypes)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedFileName
            };

            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);

            foreach (var type in fileTypes)
                picker.FileTypeChoices.Add(type, new[] { type });

            return picker;
        }
    }
}
