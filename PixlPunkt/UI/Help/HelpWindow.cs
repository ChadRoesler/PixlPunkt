using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Logging;
using PixlPunkt.UI.Helpers;

namespace PixlPunkt.UI.Help
{
    /// <summary>
    /// Offline help viewer window using Docsify to render the bundled wiki documentation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Renders the application's Markdown documentation using a bundled copy of
    /// <see href="https://docsify.js.org/">Docsify</see> inside a <see cref="WebView2"/> control.
    /// All assets (JS, CSS, Markdown files) are packaged with the application for
    /// fully offline access.
    /// </para>
    /// <para>
    /// Uses <see cref="CoreWebView2.SetVirtualHostNameToFolderMapping"/> to serve local
    /// files through a virtual hostname (<c>pixlpunkt.help</c>), which allows Docsify's
    /// AJAX requests to work — <c>file://</c> protocol blocks AJAX for security.
    /// </para>
    /// <para>
    /// The wiki-style <c>[[Page|Link]]</c> syntax is automatically converted to standard
    /// Markdown links by a Docsify plugin defined in <c>index.html</c>.
    /// </para>
    /// </remarks>
    public sealed partial class HelpWindow : Window
    {
        /// <summary>
        /// Virtual hostname used to serve the Help directory via WebView2.
        /// Docsify navigates to <c>https://pixlpunkt.help/index.html</c> which maps
        /// to the local <c>Help/</c> folder.
        /// </summary>
        private const string VirtualHost = "pixlpunkt.help";

        private static HelpWindow? _instance;
        private readonly WebView2 _webView;
        private string? _pendingPage;

        /// <summary>
        /// Gets the path to the Help directory containing the Docsify assets and wiki files.
        /// </summary>
        private static string HelpDirectory
        {
            get
            {
                // Look relative to the application base directory
                var baseDir = AppContext.BaseDirectory;
                var helpDir = Path.Combine(baseDir, "Help");

                if (Directory.Exists(helpDir))
                    return helpDir;

                // Fallback: look relative to the executable
                var exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? "");
                if (!string.IsNullOrEmpty(exeDir))
                {
                    helpDir = Path.Combine(exeDir, "Help");
                    if (Directory.Exists(helpDir))
                        return helpDir;
                }

                return Path.Combine(baseDir, "Help");
            }
        }

        private HelpWindow()
        {
            Title = "PixlPunkt Help";

            _webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var root = new Grid
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30))
            };
            root.Children.Add(_webView);
            Content = root;

            Closed += OnClosed;

            // Initialize WebView2 asynchronously — must wait for CoreWebView2
            // to be ready before setting up the virtual host mapping
            InitializeAsync();
        }

        /// <summary>
        /// Initializes WebView2 and sets up the virtual host mapping.
        /// </summary>
        private async void InitializeAsync()
        {
            try
            {
                var helpDir = HelpDirectory;

                if (!Directory.Exists(helpDir) || !File.Exists(Path.Combine(helpDir, "index.html")))
                {
                    LoggingService.Warning(
                        "Help content not found at {Path}. Expected Help/index.html in application directory.",
                        helpDir);

                    Content = new TextBlock
                    {
                        Text = "Help documentation not found.\n\n" +
                               "Visit https://github.com/ChadRoesler/PixlPunkt/wiki for online documentation.",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(24),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    return;
                }

                // Wait for WebView2 runtime to initialize
                await _webView.EnsureCoreWebView2Async();

                // Disable dev tools and status bar for clean appearance
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                // Map the local Help folder to a virtual hostname.
                // This allows Docsify's AJAX requests to work (file:// blocks AJAX).
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    VirtualHost,
                    helpDir,
                    CoreWebView2HostResourceAccessKind.Allow);

                LoggingService.Info("Help mapped {Host} -> {Path}", VirtualHost, helpDir);

                // Navigate to the docsify index via the virtual host
                string url = $"https://{VirtualHost}/index.html";
                if (!string.IsNullOrEmpty(_pendingPage))
                {
                    url += $"#/{_pendingPage}";
                    _pendingPage = null;
                }

                _webView.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to initialize help WebView", ex);
            }
        }

        /// <summary>
        /// Opens the help window (singleton — reuses existing window if open).
        /// </summary>
        /// <param name="page">Optional page name to navigate to (e.g., "Tools", "Shortcuts").</param>
        public static void Show(string? page = null)
        {
            try
            {
                if (_instance != null)
                {
                    // Window already open — bring to front and navigate if needed
                    _instance.Activate();

                    if (!string.IsNullOrEmpty(page))
                    {
                        _instance.NavigateToPage(page);
                    }

                    return;
                }

                _instance = new HelpWindow();
                _instance._pendingPage = page;

                WindowHost.ApplyChrome(
                    _instance,
                    resizable: true,
                    minimizable: true,
                    maximizable: true,
                    title: "PixlPunkt Help",
                    owner: App.PixlPunktMainWindow);

                // Set initial window size directly instead of FitToContent,
                // which pins explicit Width/Height on the root element and
                // prevents the WebView2 from stretching on resize/maximize.
                try
                {
                    var appWindow = _instance.AppWindow;
                    if (appWindow != null)
                    {
                        appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1100, Height = 750 });
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Warning("Could not set initial help window size: {Error}", ex.Message);
                }

                WindowHost.Place(
                    _instance,
                    WindowPlacement.CenterOnScreen);

                _instance.Activate();
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to open help window", ex);
            }
        }

        /// <summary>
        /// Navigates to a specific help page within the Docsify SPA.
        /// </summary>
        /// <param name="page">Page name (e.g., "Tools", "Shortcuts", "Tile-Animation").</param>
        private void NavigateToPage(string page)
        {
            try
            {
                if (_webView.CoreWebView2 != null)
                {
                    // Docsify uses hash-based routing
                    _webView.CoreWebView2.Navigate($"https://{VirtualHost}/index.html#/{page}");
                }
                else
                {
                    // WebView2 not ready yet — queue the navigation
                    _pendingPage = page;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Failed to navigate help to page {Page}: {Error}", page, ex.Message);
            }
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            _instance = null;
        }
    }
}
