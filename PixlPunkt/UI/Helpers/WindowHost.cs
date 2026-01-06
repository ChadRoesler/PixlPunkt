using System;
using Microsoft.UI.Xaml;
using PixlPunkt.Core.Enums;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.Helpers
{
    /// <summary>
    /// Cross-platform window management utilities for Uno Platform.
    /// </summary>
    public static class WindowHost
    {
        // ════════════════════════════════════════════════════════════════════
        // THEME COLORS
        // ════════════════════════════════════════════════════════════════════

        private static (Color bg, Color fg) GetThemeColors(ElementTheme theme) => theme == ElementTheme.Light
            ? (Color.FromArgb(255, 243, 243, 243), Color.FromArgb(255, 0, 0, 0))
            : (Color.FromArgb(255, 32, 32, 32), Color.FromArgb(255, 255, 255, 255));

        // ════════════════════════════════════════════════════════════════════
        // WINDOW SETUP
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies basic window configuration including window chrome.
        /// </summary>
        public static Window ApplyChrome(
            Window window,
            bool resizable = false,
            bool alwaysOnTop = false,
            bool minimizable = false,
            string? title = null,
            Window? owner = null)
        {
            if (title is not null)
                window.Title = title;

            var effectiveTheme = GetEffectiveTheme();
            if (window.Content is FrameworkElement root)
                root.RequestedTheme = effectiveTheme;

            // Configure window presenter for proper chrome (min/max/close buttons)
            try
            {
                var appWindow = window.AppWindow;
                if (appWindow != null)
                {
                    // Use OverlappedPresenter for standard window chrome with min/max/close
                    var presenter = Microsoft.UI.Windowing.OverlappedPresenter.Create();
                    presenter.IsResizable = resizable;
                    presenter.IsMinimizable = minimizable;
                    presenter.IsMaximizable = minimizable; // Usually same as minimizable
                    presenter.IsAlwaysOnTop = alwaysOnTop;
                    
                    // Set the presenter to show standard window chrome
                    appWindow.SetPresenter(presenter);
                }
            }
            catch (Exception ex)
            {
                // Fallback if AppWindow API not available on this platform
                System.Diagnostics.Debug.WriteLine($"WindowHost.ApplyChrome: Could not configure presenter: {ex.Message}");
            }

            return window;
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
                    _ => ElementTheme.Dark // Default to dark
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
        /// Sizes window content to fit.
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

            root.Width = Math.Max(minWidth, desired.Width);
            root.Height = Math.Max(minHeight, desired.Height);

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
        /// Defers content sizing until after layout (compatibility overload with named parameters).
        /// </summary>
        public static void FitToContentAfterLayout(Window window, FrameworkElement root,
            double maxScreenFraction = 0.90, double minLogicalWidth = 480, double minLogicalHeight = 360, bool _compat = true)
        {
            FitToContentAfterLayout(window, root, maxScreenFraction, minLogicalWidth, minLogicalHeight);
        }

        // ════════════════════════════════════════════════════════════════════
        // WINDOW POSITIONING (cross-platform stub)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Places a window (platform-dependent, stub for cross-platform).
        /// </summary>
        public static void Place(Window child, WindowPlacement placement, Window? parent = null,
            FrameworkElement? anchor = null, int offsetX = 16, int offsetY = 16)
        {
            // Window positioning is platform-specific
            // On desktop, this is handled by the window manager
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
            ApplyChrome(window, resizable, false, minimizable, title, owner);
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
        }

        // ════════════════════════════════════════════════════════════════════
        // FILE PICKERS - Cross-platform using Uno's storage APIs
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a file open picker (cross-platform).
        /// </summary>
        public static Windows.Storage.Pickers.FileOpenPicker CreateFileOpenPicker(Window window, params string[] fileTypes)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };

            foreach (var type in fileTypes)
                picker.FileTypeFilter.Add(type);

            return picker;
        }

        /// <summary>
        /// Creates a file save picker (cross-platform).
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

            return picker;
        }
    }
}
