using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.AutoSave;
using PixlPunkt.Uno.Core.Brush;
using PixlPunkt.Uno.Core.Canvas;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.Core.Palette;
using PixlPunkt.Uno.Core.Plugins;
using PixlPunkt.Uno.Core.Session;
using PixlPunkt.Uno.Core.Settings;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.Core.Updates;
using PixlPunkt.Uno.UI.CanvasHost;
using PixlPunkt.Uno.UI.Dialogs;
using PixlPunkt.Uno.UI.Helpers;
using PixlPunkt.Uno.UI.Settings;
using Windows.Graphics;
using Windows.System;
using PixlPunkt.Uno.UI.Ascii;
using static PixlPunkt.Uno.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Uno.UI
{
    /// <summary>
    /// Main application window. Hosts:
    /// - Document tab workspace (pixel canvases).
    /// - Tool rail / options bar.
    /// - Palette panel and live color bindings.
    /// - Preview panel (attached to active CanvasViewHost).
    /// - Layer panel (bound to active document).
    /// - Keyboard accelerators for tools and editing.
    /// Handles creation, attachment, and detachment of canvas hosts; custom title bar setup; palette import/export.
    /// </summary>
    /// <remarks>
    /// This class is split across multiple partial files:
    /// - PixlPunktMainWindow.xaml.cs: Core initialization and fields
    /// - PixlPunktMainWindow.File.cs: File import/export operations
    /// - PixlPunktMainWindow.Shortcuts.cs: Keyboard shortcuts and accelerators
    /// - PixlPunktMainWindow.Palette.cs: Palette management (presets, import, export)
    /// - PixlPunktMainWindow.Tabs.cs: Tab/document management and host attachment
    /// - PixlPunktMainWindow.Theme.cs: Theme/stripe pattern management
    /// </remarks>
    public sealed partial class PixlPunktMainWindow : Window
    {
        // ─────────────────────────────────────────────────────────────
        // FIELDS: Window / Workspace
        // ─────────────────────────────────────────────────────────────

        private readonly DocumentWorkspace _workspace = new();
        private CanvasViewHost? CurrentHost;
        private bool _suspendToolAccelerators;
        private readonly List<KeyboardAccelerator> _toolAccels = [];

        // ─────────────────────────────────────────────────────────────
        // FIELDS: Palette / Tools / Document state
        // ─────────────────────────────────────────────────────────────

        private PaletteService _palette;
        private uint _fg;               // Current foreground BGRA (with brush opacity merged)
        private readonly ToolState _toolState = new();
        private readonly Dictionary<CanvasDocument, string?> _documentPaths = [];
        private int _newCanvasCounter = 1;  // Counter for "NewCanvas1", "NewCanvas2", etc.

        // ─────────────────────────────────────────────────────────────
        // FIELDS: Auto-Save
        // ─────────────────────────────────────────────────────────────

        private readonly AutoSaveService _autoSave = new();

        // Cached delegates to avoid repeated lambda allocations
        private readonly Action<uint> _onForegroundPicked;
        private readonly Action<uint> _onPaletteForegroundChanged;
        private Action<int>? _layersPanelSelectionHandler;

        // Keyboard modifier tracking
        private bool _shiftDown;
        private bool _ctrlDown;

        // ─────────────────────────────────────────────────────────────
        // CONSTRUCTOR
        // ─────────────────────────────────────────────────────────────

        public PixlPunktMainWindow()
        {
            // Pre-initialize delegates used during component wiring
            _onForegroundPicked = OnForegroundColorPicked;
            _onPaletteForegroundChanged = OnPaletteBackgroundChanged;

            InitializeComponent();

            // Wire up the glyph editor callback for AsciiEffectSettings
            Core.Effects.Settings.AsciiEffectSettings.OpenGlyphEditorCallback = OpenGlyphSetEditorWindow;

            // Apply theme settings FIRST, before any dynamic UI is created
            // This ensures theme resources resolve correctly
            try
            {
                SetAppTheme(AppSettings.Instance.AppTheme);
                SetStripeTheme(AppSettings.Instance.StripeTheme);
            }
            catch (Exception ex)
            {
                LoggingService.Debug("Failed to apply persisted theme settings: {Error}", ex.Message);
            }

            FixTabViewAddButtonAlignment(DocsTab);
            InitOpenRecentMenus();
            Root.ActualThemeChanged += (_, __) => SyncStripeTheme();
            SyncStripeTheme();

            // Apply custom window chrome (resizable, proper title bar merging)
            WindowHost.ApplyChrome(
                this,
                resizable: true,
                alwaysOnTop: false,
                minimizable: true,
                maximizable: true,
                title: "Pixl Punkt");

            // Note: Window closing handled via Closed event in Uno

            // Collect tool accelerators defined in XAML (B,E,F,R,G,J,U etc.)
            foreach (var accel in Root.KeyboardAccelerators)
            {
                if (accel.Modifiers == VirtualKeyModifiers.None)
                {
                    _toolAccels.Add(accel);
                }
            }

            Root.GotFocus += OnAnyGotFocus;
            Root.LostFocus += OnAnyLostFocus;
            SetupMergedTitleBar();

            // Palette / tool initialization - track if configured palette failed to load
            var configuredPalette = AppSettings.Instance.DefaultPalette;
            _palette = new PaletteService();
            var paletteLoadedSuccessfully = _palette.LoadPaletteByName(configuredPalette);

            // Now bind ToolRail AFTER theme is applied
            ToolRail.BindToolState(_toolState);
            OptionsBar.BindToolState(_toolState);

            Activated += (_, __) => Root.Focus(FocusState.Programmatic);

            PalettePanel.Service = _palette;
            PalettePanel.ForegroundPicked += _onForegroundPicked;
            PalettePanel.BackgroundPicked += OnBackgroundColorPicked;

            _palette.ForegroundChanged += c => ApplyBrushColor(c);
            _palette.BackgroundChanged += OnPaletteBackgroundChanged;

            // Initial foreground color merged with current brush opacity
            _fg = (_palette.Foreground & 0x00FFFFFFu) | ((uint)_toolState.Brush.Opacity << 24);

            ToolRail.Service = _palette;
            ToolRail.GetBrushOpacity = () => _toolState.Brush.Opacity;
            ToolRail.RequestSetBrushOpacity = a =>
            {
                _toolState.UpdateBrush(b => b.Opacity = a);
                ToolRail.NotifyBrushOpacityChanged(a);
            };
            ToolRail.RequestSetBrushColor = c => ApplyBrushColor(c);

            // NOTE: ForegroundPicked already subscribed above via _onForegroundPicked
            // BackgroundPicked reserved for future BG tool bindings (no-op for now)

            BuildPalettePresetsMenu();
            BuildCustomPalettesMenu();
            BuildPluginIOMenus();
            BuildAdvancedTemplatesMenu();

            // Subscribe to plugin changes to rebuild menus
            PluginRegistry.Instance.PluginsRefreshed += BuildPluginIOMenus;
            PluginRegistry.Instance.PluginLoaded += _ => BuildPluginIOMenus();
            PluginRegistry.Instance.PluginUnloaded += _ => BuildPluginIOMenus();

            // Apply persisted palette swatch size
            try
            {
                PalettePanel.SwatchSize = AppSettings.Instance.PaletteSwatchSize;
            }
            catch (Exception ex)
            {
                LoggingService.Debug("Failed to apply persisted palette swatch size: {Error}", ex.Message);
            }

            // Wire up plugin canvas context provider for API v2 features
            // This allows plugins to sample pixels, query colors, and access selection state
            WirePluginCanvasContextProvider();

            // Initialize panel docking system
            InitializePanelDocking();

            // Start auto-save service
            _autoSave.Start();

            // Handle window closing to cleanup auto-save
            Closed += OnWindowClosed;

            // Store palette load status for deferred warning
            _deferredPaletteWarning = !paletteLoadedSuccessfully ? configuredPalette : null;

            // Use Root.Loaded event to ensure XAML tree is fully loaded before showing dialogs
            Root.Loaded += OnRootLoaded;

            LoggingService.Info("Main window initialized");
        }

        // Deferred initialization state
        private string? _deferredPaletteWarning;
        private bool _deferredInitCompleted;

        /// <summary>
        /// Called when the Root element is fully loaded. Safe to show dialogs here.
        /// </summary>
        private async void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            // Only run once
            if (_deferredInitCompleted) return;
            _deferredInitCompleted = true;

            // Unsubscribe to prevent multiple calls
            Root.Loaded -= OnRootLoaded;

            // Re-apply theme now that everything is fully loaded
            // This ensures UserControls like ToolRail get the correct theme
            try
            {
                SetAppTheme(AppSettings.Instance.AppTheme);
            }
            catch (Exception ex)
            {
                LoggingService.Debug("Failed to reapply theme on load: {Error}", ex.Message);
            }

            try
            {
                // Initialize session recovery
                await InitializeSessionAsync();

                // Show warning if configured palette failed to load
                if (!string.IsNullOrEmpty(_deferredPaletteWarning) &&
                    !_deferredPaletteWarning.Equals(AppSettings.FallbackPaletteName, StringComparison.OrdinalIgnoreCase))
                {
                    await ShowPaletteLoadWarningAsync(_deferredPaletteWarning);
                }

                // Check for keyboard shortcut conflicts
                await CheckShortcutConflictsAsync();

                // Check for updates (if enabled in settings)
                await CheckForUpdatesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Error("Error during deferred initialization", ex);
            }
        }

        /// <summary>
        /// Shows a warning dialog when the configured default palette failed to load.
        /// </summary>
        private async Task ShowPaletteLoadWarningAsync(string paletteName)
        {
            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Palette Not Found",
                Content = $"The configured default palette \"{paletteName}\" could not be found.\n\n" +
                          $"This may happen if a custom palette was deleted or renamed.\n\n" +
                          $"The default \"{AppSettings.FallbackPaletteName}\" palette has been loaded instead.",
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close
            };

            await ShowDialogGuardedAsync(dlg);
        }

        /// <summary>
        /// Checks for keyboard shortcut conflicts and shows a warning dialog if any are found.
        /// </summary>
        private async Task CheckShortcutConflictsAsync()
        {
            try
            {
                var detector = new ShortcutConflictDetector(_toolState);
                var conflicts = detector.DetectUndismissedConflicts();

                if (conflicts.Count > 0)
                {
                    var dialog = new ShortcutConflictWarningDialog(conflicts, Content.XamlRoot);
                    var openSettings = await dialog.ShowAsync();

                    if (openSettings)
                    {
                        // Open settings window to the Shortcuts tab
                        var win = new SettingsWindow();
                        win.InitializeWithToolState(_toolState);
                        win.Activate();
                        var appW = WindowHost.ApplyChrome(win, resizable: false, alwaysOnTop: false, minimizable: false, title: "Settings", owner: App.PixlPunktMainWindow);
                        WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.9, minLogicalWidth: 560, minLogicalHeight: 360);
                        WindowHost.Place(appW, WindowPlacement.CenterOnScreen, this);
                    }

                    LoggingService.Warning("Detected {Count} shortcut conflicts at startup", conflicts.Count);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Debug("Failed to check shortcut conflicts: {Error}", ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // WINDOW LIFECYCLE
        // ─────────────────────────────────────────────────────────────

        private bool _closingHandled = false;

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            // Prevent re-entrancy
            if (_closingHandled) return;
            _closingHandled = true;

            // Stop and dispose auto-save service
            _autoSave.Dispose();

            // Dispose the dialog gate semaphore
            _dialogGate.Dispose();

            LoggingService.Info("Main window closed and resources cleaned up");

            // Close all child windows (detached document windows, settings, color picker, etc.)
            CloseAllChildWindows();

            // Exit the application completely
            Application.Current.Exit();
        }

        /// <summary>
        /// Closes all child windows (detached document windows).
        /// </summary>
        private void CloseAllChildWindows()
        {
            // Close all detached document windows
            var windowsToClose = _docWindows.Values.ToList();
            foreach (var win in windowsToClose)
            {
                try
                {
                    win.Close();
                }
                catch (Exception ex)
                {
                    LoggingService.Warning("Failed to close child window: {Error}", ex.Message);
                }
            }
            _docWindows.Clear();
        }

        /// <summary>
        /// Gets all open documents that have unsaved changes.
        /// </summary>
        private List<CanvasDocument> GetDocumentsWithUnsavedChanges()
        {
            var dirtyDocs = new List<CanvasDocument>();

            foreach (TabViewItem tab in DocsTab.TabItems)
            {
                if (tab.Content is CanvasViewHost host && host.Document.IsDirty)
                {
                    dirtyDocs.Add(host.Document);
                }
            }

            return dirtyDocs;
        }

        /// <summary>
        /// Prompts the user to save changes for multiple documents when closing the app.
        /// </summary>
        private async Task<SaveChangesResult> PromptSaveAllChangesAsync(List<CanvasDocument> dirtyDocs)
        {
            string docList = string.Join("\n", dirtyDocs.ConvertAll(d => $"• {d.Name ?? "Untitled"}"));

            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Unsaved Changes",
                Content = $"The following documents have unsaved changes:\n\n{docList}\n\nDo you want to save before closing?",
                PrimaryButtonText = "Save All",
                SecondaryButtonText = "Don't Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await ShowDialogGuardedAsync(dlg);

            return result switch
            {
                ContentDialogResult.Primary => SaveChangesResult.Save,
                ContentDialogResult.Secondary => SaveChangesResult.DontSave,
                _ => SaveChangesResult.Cancel
            };
        }

        // ─────────────────────────────────────────────────────────────
        // PALETTE SWATCH SIZE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Update palette swatch size at runtime.
        /// </summary>
        public void SetPaletteSwatchSize(int size)
        {
            try
            {
                PalettePanel.SwatchSize = Math.Max(4, size);
            }
            catch (Exception ex)
            {
                LoggingService.Debug("Failed to set palette swatch size: {Error}", ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // TITLE BAR MERGE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Configures the title bar based on platform capabilities.
        /// On Windows: Extends content into title bar for a seamless menu bar.
        /// On Linux/macOS: Uses standard window chrome with the menu bar below the native title bar.
        /// </summary>
        private void SetupMergedTitleBar()
        {
#if WINDOWS
            // On Windows, extend content into title bar for seamless menu bar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarRoot);
            
            // Configure title bar colors to match theme
            try
            {
                var appWindow = this.AppWindow;
                if (appWindow?.TitleBar != null)
                {
                    var titleBar = appWindow.TitleBar;
                    
                    // Get theme-appropriate colors
                    var isDark = Root.ActualTheme == ElementTheme.Dark;
                    var bgColor = isDark 
                        ? Windows.UI.Color.FromArgb(255, 32, 32, 32)
                        : Windows.UI.Color.FromArgb(255, 243, 243, 243);
                    var fgColor = isDark
                        ? Windows.UI.Color.FromArgb(255, 255, 255, 255)
                        : Windows.UI.Color.FromArgb(255, 0, 0, 0);
                    var inactiveFgColor = isDark
                        ? Windows.UI.Color.FromArgb(255, 128, 128, 128)
                        : Windows.UI.Color.FromArgb(255, 128, 128, 128);
                    
                    titleBar.BackgroundColor = bgColor;
                    titleBar.ForegroundColor = fgColor;
                    titleBar.InactiveBackgroundColor = bgColor;
                    titleBar.InactiveForegroundColor = inactiveFgColor;
                    titleBar.ButtonBackgroundColor = bgColor;
                    titleBar.ButtonForegroundColor = fgColor;
                    titleBar.ButtonInactiveBackgroundColor = bgColor;
                    titleBar.ButtonInactiveForegroundColor = inactiveFgColor;
                    titleBar.ButtonHoverBackgroundColor = isDark
                        ? Windows.UI.Color.FromArgb(255, 64, 64, 64)
                        : Windows.UI.Color.FromArgb(255, 220, 220, 220);
                    titleBar.ButtonHoverForegroundColor = fgColor;
                    titleBar.ButtonPressedBackgroundColor = isDark
                        ? Windows.UI.Color.FromArgb(255, 96, 96, 96)
                        : Windows.UI.Color.FromArgb(255, 200, 200, 200);
                    titleBar.ButtonPressedForegroundColor = fgColor;
                }
            }
            catch (Exception ex)
            {
                Core.Logging.LoggingService.Debug("Failed to configure title bar colors: {Error}", ex.Message);
            }
#else
            // On Linux/macOS (Skia), use standard window chrome
            // The native window manager provides the title bar
            // The menu bar (TitleBarRoot) remains visible as a regular menu bar
            ExtendsContentIntoTitleBar = false;
#endif
        }

        // ─────────────────────────────────────────────────────────────
        // KEYBOARD FOCUS / INPUT UTILITIES
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks if a text input control currently has focus.
        /// </summary>
        private static bool IsTextInputFocused()
        {
            var focused = FocusManager.GetFocusedElement();
            return focused switch
            {
                TextBox => true,
                RichEditBox => true,
                AutoSuggestBox => true,
                NumberBox => true,
                ComboBox cb when cb.IsEditable => true,
                _ => false
            };
        }

        /// <summary>
        /// Checks if a control that might intercept keyboard shortcuts has focus.
        /// </summary>
        private static bool IsKeyboardCapturingControlFocused()
        {
            var focused = FocusManager.GetFocusedElement();
            return focused switch
            {
                TextBox => true,
                RichEditBox => true,
                AutoSuggestBox => true,
                NumberBox => true,
                ComboBox cb when cb.IsEditable => true,
                _ => false
            };
        }

        /// <summary>
        /// Forces focus back to the main content area to ensure shortcuts work.
        /// Call this after interactions with panels that might steal focus.
        /// </summary>
        private void RestoreFocusToCanvas()
        {
            // Try to focus the canvas view if we have an active host
            if (CurrentHost != null)
            {
                CurrentHost.Focus(FocusState.Programmatic);
            }
            else
            {
                // Otherwise focus the root to keep shortcuts working
                Root.Focus(FocusState.Programmatic);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // HISTORY UI SYNC
        // ─────────────────────────────────────────────────────────────

        private void Host_HistoryStateChanged() => UpdateHistoryUI();

        private void UpdateHistoryUI()
        {
            if (CurrentHost != null)
            {
                bool canUndo = CurrentHost.CanUndo;
                bool canRedo = CurrentHost.CanRedo;
                // Hook up buttons if added later
            }
        }

        private void UndoBtn_Click(object sender, RoutedEventArgs e)
        {
            CurrentHost?.Undo();
            UpdateHistoryUI();
        }

        private void RedoBtn_Click(object sender, RoutedEventArgs e)
        {
            CurrentHost?.Redo();
            UpdateHistoryUI();
        }

        // ─────────────────────────────────────────────────────────────
        // DOCUMENT I/O HELPERS
        // ─────────────────────────────────────────────────────────────

        private readonly SemaphoreSlim _dialogGate = new(1, 1);
        
        /// <summary>
        /// Timeout for waiting on the dialog gate semaphore (30 seconds).
        /// Prevents infinite blocking if a dialog hangs.
        /// </summary>
        private static readonly TimeSpan DialogGateTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Centralized, guarded dialog show to prevent multiple dialogs.
        /// Waits for XamlRoot to be available if needed.
        /// Ensures the dialog respects the app's theme setting.
        /// </summary>
        private async Task<ContentDialogResult> ShowDialogGuardedAsync(ContentDialog dlg)
        {
            // Wait for XamlRoot to be available (window must be fully loaded)
            int retries = 0;
            while (Content?.XamlRoot == null && retries < 50)
            {
                await Task.Delay(100);
                retries++;
            }

            if (Content?.XamlRoot == null)
            {
                LoggingService.Warning("ShowDialogGuardedAsync: XamlRoot not available after waiting");
                return ContentDialogResult.None;
            }

            dlg.XamlRoot ??= Content.XamlRoot;

            // Apply the app's theme setting to the dialog
            dlg.RequestedTheme = Root.RequestedTheme;

            // Use timeout to prevent infinite blocking if dialog hangs
            bool acquired = await _dialogGate.WaitAsync(DialogGateTimeout);
            if (!acquired)
            {
                LoggingService.Warning("ShowDialogGuardedAsync: Timed out waiting for dialog gate after {Timeout}s", DialogGateTimeout.TotalSeconds);
                return ContentDialogResult.None;
            }
            
            try
            {
                return await dlg.ShowAsync();
            }
            finally
            {
                _dialogGate.Release();
            }
        }

        private async Task NewCanvasAsync()
        {
            if (IsTextInputFocused()) return;

            var dlg = new NewCanvasDialog { XamlRoot = Content.XamlRoot };
            var res = await ShowDialogGuardedAsync(dlg);
            if (res != ContentDialogResult.Primary) return;

            var values = dlg.GetValues();
            CreateAndOpenCanvas(values.name, values.tileSize, values.tileCounts);
        }

        private async Task OpenDocumentAsync()
        {
            if (IsTextInputFocused()) return;

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".pxp");

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            try
            {
                CanvasDocument doc;
                if (file.FileType.Equals(".pxp", StringComparison.OrdinalIgnoreCase))
                {
                    // Load native .pxp document
                    doc = DocumentIO.Load(file.Path);

                    // Reload audio tracks from stored file paths
                    await doc.CanvasAnimationState.ReloadAudioTracksAsync();

                    // Reload sub-routine reels from stored file paths
                    doc.CanvasAnimationState.ReloadSubRoutineReels(doc);
                }
                else if (file.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = await file.OpenReadAsync();
                    var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                    var w = (int)decoder.PixelWidth;
                    var h = (int)decoder.PixelHeight;
                    doc = new CanvasDocument(file.Name, w, h, CreateSize(8, 8), CreateSize(Math.Max(1, w / 8), Math.Max(1, h / 8)));
                }
                else
                {
                    // Unsupported format
                    _ = new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Unsupported format",
                        Content = $"Cannot open files with extension '{file.FileType}'.",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                    return;
                }

                // Mark document as clean (just loaded)
                doc.MarkSaved();

                _workspace.Add(doc);
                _documentPaths[doc] = file.Path;
                TrackRecent(file.Path);
                // Register document for auto-save
                _autoSave.RegisterDocument(doc);

                var tab = MakeTab(doc);
                DocsTab.TabItems.Add(tab);
                // Defer selection to ensure tab is fully in visual tree (fixes Release build timing issue)
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    DocsTab.SelectedItem = tab;
                });

                LoggingService.Info($"Opened document: {file.Path}");

                // Update session state
                UpdateSessionState();
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open document: {file.Path}", ex);
                _ = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Open failed",
                    Content = $"Could not open file.\n{ex.Message}",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = LoggingService.LogDirectory;

            try
            {
                AppPaths.EnsureDirectoryExists(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info("Opened log folder: {LogPath}", path);
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open log folder: {path}", ex);
            }
        }

        private void OpenPluginFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = PluginRegistry.Instance.PluginsDirectory;

            try
            {
                System.IO.Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info($"Opened plugin folder: {path}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open plugin folder: {path}", ex);
            }
        }

        private void OpenBrushFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = BrushMarkIO.GetBrushDirectory();

            try
            {
                System.IO.Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info($"Opened brush folder: {path}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open brush folder: {path}", ex);
            }
        }

        private void OpenPaletteFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = CustomPaletteIO.GetPalettesDirectory();

            try
            {
                CustomPaletteIO.EnsureDirectoryExists();
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info($"Opened palette folder: {path}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open palette folder: {path}", ex);
            }
        }

        private void OpenTemplateFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = CustomTemplateIO.GetTemplatesDirectory();

            try
            {
                CustomTemplateIO.EnsureDirectoryExists();
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info($"Opened template folder: {path}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open template folder: {path}", ex);
            }
        }

        private void OpenGlyphSetsFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = AppPaths.GlyphSetsDirectory;

            try
            {
                AppPaths.EnsureDirectoryExists(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                LoggingService.Info("Opened glyph sets folder: {GlyphSetsPath}", path);
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open glyph sets folder: {path}", ex);
            }
        }

        private void OpenGlyphSetEditor_Click(object sender, RoutedEventArgs e)
        {
            OpenGlyphSetEditorWindow();
        }

        /// <summary>
        /// Opens the Glyph Set Editor window. Can be called from menu or from effect settings.
        /// </summary>
        private void OpenGlyphSetEditorWindow()
        {
            var win = new GlyphSetEditorWindow();
            win.Activate();

            var appW = WindowHost.ApplyChrome(
                win,
                resizable: true,
                alwaysOnTop: false,
                minimizable: true,
                title: "Glyph Set Editor",
                owner: this);

            WindowHost.FitToContentAfterLayout(
                win,
                (FrameworkElement)win.Content,
                maxScreenFraction: 0.90,
                minLogicalWidth: 900,
                minLogicalHeight: 700);

            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, this);
        }

        private async Task<bool> SaveDocumentInternalAsync(CanvasDocument doc, string path)
        {
            try
            {
                var tmp = Path.GetTempFileName();

                doc.CompositeTo(doc.Surface);
                var surf = doc.Surface;

                using (var fs = File.OpenWrite(tmp))
                {
                    var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                        Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                        fs.AsRandomAccessStream());

                    var pixels = surf.Pixels;
                    encoder.SetPixelData(
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                        (uint)surf.Width,
                        (uint)surf.Height,
                        96, 96,
                        pixels);

                    await encoder.FlushAsync();
                }

                System.IO.File.Copy(tmp, path, overwrite: true);
                System.IO.File.Delete(tmp);
                LoggingService.Info($"Saved document image to {path}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to save document to {path}", ex);
                return false;
            }
        }

        private async Task SaveDocumentAsync()
        {
            if (IsTextInputFocused()) return;

            var host = CurrentHost;
            if (host == null) return;
            var doc = host.Document;

            var existingPath = _documentPaths.TryGetValue(doc, out var p) ? p : null;

            if (string.IsNullOrEmpty(existingPath))
            {
                await SaveDocumentAsAsync();
                return;
            }

            // Save as .pxp native format
            try
            {
                DocumentIO.Save(doc, existingPath);
                doc.MarkSaved();
                TrackRecent(existingPath);
                LoggingService.Info($"Saved document: {existingPath}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to save document: {existingPath}", ex);
                _ = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Save failed",
                    Content = "Could not save current document.",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // VIEW COMMANDS
        // ─────────────────────────────────────────────────────────────

        private void View_Fit_Click(object sender, RoutedEventArgs e) { CurrentHost?.Fit(); LoggingService.Debug("View: Fit"); }
        private void View_Actual_Click(object sender, RoutedEventArgs e) { CurrentHost?.CanvasActualSize(); LoggingService.Debug("View: Actual Size"); }
        private void View_TogglePixelGrid_Click(object sender, RoutedEventArgs e) { CurrentHost?.TogglePixelGrid(); LoggingService.Debug("Toggled pixel grid"); }
        private void View_ToggleTileGrid_Click(object sender, RoutedEventArgs e) { CurrentHost?.ToggleTileGrid(); LoggingService.Debug("Toggled tile grid"); }

        private void View_ToggleTileMappings_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost == null) return;
            CurrentHost.ShowTileMappings = !CurrentHost.ShowTileMappings;
        }

        private void View_ToggleTileAnimationMappings_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost == null) return;
            CurrentHost.ShowTileAnimationMappings = !CurrentHost.ShowTileAnimationMappings;
        }

        private void View_ToggleRulers_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost != null)
            {
                CurrentHost.ShowRulers = !CurrentHost.ShowRulers;
                LoggingService.Debug($"Toggled rulers: {CurrentHost.ShowRulers}");
            }
        }

        private void View_ToggleGuides_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost != null)
            {
                CurrentHost.ShowGuides = !CurrentHost.ShowGuides;
                LoggingService.Debug($"Toggled guides: {CurrentHost.ShowGuides}");
            }
        }

        private void View_ToggleSnapToGuides_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost != null)
            {
                CurrentHost.SnapToGuides = !CurrentHost.SnapToGuides;
                LoggingService.Debug($"Toggled snap to guides: {CurrentHost.SnapToGuides}");
            }
        }

        private void View_ToggleLockGuides_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost != null)
            {
                CurrentHost.GuidesLocked = !CurrentHost.GuidesLocked;
                LoggingService.Debug($"Toggled lock guides: {CurrentHost.GuidesLocked}");
            }
        }

        private void View_ClearGuides_Click(object sender, RoutedEventArgs e)
        {
            CurrentHost?.ClearAllGuides();
            LoggingService.Debug("Cleared guides");
        }

        private void View_RefreshBrushes_Click(object sender, RoutedEventArgs e)
        {
            Core.Brush.BrushDefinitionService.Instance.RefreshBrushes();

            // Show confirmation
            _ = ShowDialogGuardedAsync(new ContentDialog
            {
                Title = "Brushes Refreshed",
                Content = $"Loaded {Core.Brush.BrushDefinitionService.Instance.Count} custom brush(es) from:\n%AppData%\\PixlPunkt\\Brushes\\",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            });

            LoggingService.Info("Brushes refreshed from main window");
        }

        // ─────────────────────────────────────────────────────────────
        // FILE COMMANDS
        // ─────────────────────────────────────────────────────────────

        private async void File_NewCanvas_Click(object sender, RoutedEventArgs e) => await NewCanvasAsync();
        private async void File_OpenDocument_Click(object sender, RoutedEventArgs e) => await OpenDocumentAsync();
        private async void File_SaveDocument_Click(object sender, RoutedEventArgs e) => await SaveDocumentAsync();
        private async void File_SaveDocumentAs_Click(object sender, RoutedEventArgs e) => await SaveDocumentAsAsync();
        private void File_Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Exit();

        // ─────────────────────────────────────────────────────────────
        // EDIT COMMANDS
        // ─────────────────────────────────────────────────────────────

        private void Edit_Cut_Click(object sender, RoutedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            if (CurrentHost?.HasSelection == true) CurrentHost.CutSelection();
        }

        private void Edit_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            if (CurrentHost?.HasSelection == true) CurrentHost.CopySelection();
        }

        private void Edit_Paste_Click(object sender, RoutedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            CurrentHost?.PasteClipboard();
        }

        private void Edit_Undo_Click(object sender, RoutedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            if (CurrentHost?.CanUndo == true)
            {
                CurrentHost.Undo();
                UpdateHistoryUI();
            }
        }

        private void Edit_Redo_Click(object sender, RoutedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            if (CurrentHost?.CanRedo == true)
            {
                CurrentHost.Redo();
                UpdateHistoryUI();
            }
        }

        private void Edit_EditCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost == null) return;
            var doc = CurrentHost.Document;

            var win = new EditCanvasWindow(doc);

            // Hook up callback to refresh canvas after changes
            win.OnCanvasChanged = (contentOffsetX, contentOffsetY) =>
            {
                // Refresh the canvas view, adjusting viewport for content offset
                CurrentHost?.InvalidateCanvasAfterResize(contentOffsetX, contentOffsetY);

                // Update the tab header with new name
                if (DocsTab.SelectedItem is TabViewItem tab)
                {
                    tab.Header = doc.Name ?? "Untitled";
                }

                // Refresh layer previews (they need to regenerate with new dimensions)
                LayersPanel.RefreshAllPreviews();
            };

            win.Activate();
            var appW = WindowHost.ApplyChrome(win, resizable: false, alwaysOnTop: false, minimizable: false, title: "Edit Canvas", owner: App.PixlPunktMainWindow);
            WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.9, minLogicalWidth: 340, minLogicalHeight: 480);
            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, this);
        }

        // ─────────────────────────────────────────────────────────────
        // SETTINGS
        // ─────────────────────────────────────────────────────────────

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            win.InitializeWithToolState(_toolState);
            win.Activate();
            var appW = WindowHost.ApplyChrome(win, resizable: false, alwaysOnTop: false, minimizable: false, title: "Settings", owner: App.PixlPunktMainWindow);
            WindowHost.FitToContentAfterLayout(win, (FrameworkElement)win.Content, maxScreenFraction: 0.9, minLogicalWidth: 560, minLogicalHeight: 360);
            WindowHost.Place(appW, WindowPlacement.CenterOnScreen, this);
        }

        // ─────────────────────────────────────────────────────────────
        // FOCUS HANDLING
        // ─────────────────────────────────────────────────────────────

        private void OnAnyGotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TextBox or PasswordBox or RichEditBox or AutoSuggestBox or NumberBox ||
                (e.OriginalSource is ComboBox cb && cb.IsEditable))
            {
                SuspendToolAccelerators(true);
            }
        }

        private void OnAnyLostFocus(object sender, RoutedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            SuspendToolAccelerators(false);
        }

        private void SuspendToolAccelerators(bool suspend)
        {
            if (_suspendToolAccelerators == suspend) return;
            _suspendToolAccelerators = suspend;
            foreach (var accel in _toolAccels)
                accel.IsEnabled = !suspend;
        }

        /// <summary>
        /// Wires up the plugin canvas context provider to give plugins access to document data.
        /// </summary>
        private void WirePluginCanvasContextProvider()
        {
            PluginLoader.CanvasContextProvider = () =>
            {
                // Return null if no document is open
                if (CurrentHost?.Document == null)
                    return null;

                var doc = CurrentHost.Document;

                return new CanvasContext(
                    document: doc,
                    getForeground: () => _palette.Foreground,
                    getBackground: () => _palette.Background,
                    hasSelection: () => CurrentHost?.HasSelection ?? false,
                    getSelectionBounds: () =>
                    {
                        // Selection bounds from CanvasViewHost if available
                        // For now, return null - full implementation would query SelectionSubsystem
                        return null;
                    },
                    isPointSelected: (x, y) =>
                    {
                        // Default to true (all points selected) when no selection
                        return !(CurrentHost?.HasSelection ?? false) || true;
                    },
                    getSelectionMask: (x, y) =>
                    {
                        // Default to 255 (fully selected) when no selection
                        return 255;
                    }
                );
            };
        }

        // ─────────────────────────────────────────────────────────────
        // PUBLIC API - EXTERNAL DROPPER MODE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the currently active canvas host, or null if no document is open.
        /// </summary>
        public CanvasViewHost? ActiveCanvasHost => CurrentHost;

        /// <summary>
        /// Gets the palette service for accessing foreground/background colors.
        /// </summary>
        public PaletteService Palette => _palette;

        /// <summary>
        /// Enables external dropper mode on the active canvas for color picker windows.
        /// While active, canvas clicks sample colors instead of using the current tool.
        /// </summary>
        /// <param name="callback">Callback invoked with the sampled BGRA color.</param>
        public void BeginExternalDropperMode(Action<uint> callback)
        {
            CurrentHost?.BeginExternalDropperMode(callback);
        }

        /// <summary>
        /// Disables external dropper mode on the active canvas.
        /// </summary>
        public void EndExternalDropperMode()
        {
            CurrentHost?.EndExternalDropperMode();
        }

        // ─────────────────────────────────────────────────────────────
        // UPDATE CHECKING
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks for updates on startup if enabled in settings.
        /// </summary>
        private async Task CheckForUpdatesAsync(bool forceCheck = false, bool showNoUpdateMessage = false)
        {
            // Skip if disabled and not forced
            if (!forceCheck && !AppSettings.Instance.CheckForUpdatesOnStartup)
            {
                LoggingService.Debug("Update check skipped - disabled in settings");
                return;
            }

            try
            {
                var updateInfo = await UpdateService.Instance.CheckForUpdateAsync(
                    forceCheck: forceCheck,
                    includePreReleases: AppSettings.Instance.IncludePreReleaseUpdates);

                if (updateInfo != null)
                {
                    // Check if this version was skipped by user
                    if (!forceCheck && 
                        !string.IsNullOrEmpty(AppSettings.Instance.SkippedUpdateVersion) &&
                        AppSettings.Instance.SkippedUpdateVersion == updateInfo.Version)
                    {
                        LoggingService.Info("Update {Version} was previously skipped by user", updateInfo.Version);
                        return;
                    }

                    await ShowUpdateAvailableDialogAsync(updateInfo);
                }
                else if (showNoUpdateMessage)
                {
                    // Show "up to date" message when manually checking
                    await ShowDialogGuardedAsync(new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "You're Up to Date!",
                        Content = $"PixlPunkt v{UpdateService.GetCurrentVersionString()} is the latest version.",
                        CloseButtonText = "OK",
                        DefaultButton = ContentDialogButton.Close
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Failed to check for updates: {Error}", ex.Message);
                
                if (showNoUpdateMessage)
                {
                    await ShowDialogGuardedAsync(new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "Update Check Failed",
                        Content = "Could not connect to GitHub to check for updates.\n\nPlease check your internet connection and try again.",
                        CloseButtonText = "OK",
                        DefaultButton = ContentDialogButton.Close
                    });
                }
            }
        }

        /// <summary>
        /// Shows the update available dialog and handles the user's response.
        /// </summary>
        private async Task ShowUpdateAvailableDialogAsync(UpdateInfo updateInfo)
        {
            var dlg = new UpdateAvailableDialog(updateInfo)
            {
                XamlRoot = Content.XamlRoot
            };

            var result = await ShowDialogGuardedAsync(dlg);

            switch (result)
            {
                case ContentDialogResult.Primary:
                    // Download Update - open in browser
                    var downloadUrl = UpdateService.GetDownloadUrlForCurrentArchitecture(updateInfo);
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        UpdateService.OpenReleaseUrl(downloadUrl);
                    }
                    else
                    {
                        // Fallback to release page
                        UpdateService.OpenReleaseUrl(updateInfo.ReleaseUrl);
                    }
                    break;

                case ContentDialogResult.Secondary:
                    // Remind Me Later - do nothing, will check again next launch
                    LoggingService.Info("User chose to be reminded later for update {Version}", updateInfo.Version);
                    break;

                case ContentDialogResult.None:
                    // Skip This Version
                    if (dlg.SkipThisVersion)
                    {
                        AppSettings.Instance.SkippedUpdateVersion = updateInfo.Version;
                        AppSettings.Instance.Save();
                        LoggingService.Info("User chose to skip update {Version}", updateInfo.Version);
                    }
                    break;
            }
        }

        /// <summary>
        /// Manually check for updates (triggered from Help menu).
        /// </summary>
        private async void Help_CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync(forceCheck: true, showNoUpdateMessage: true);
        }

        /// <summary>
        /// Opens the GitHub releases page.
        /// </summary>
        private void Help_ViewReleases_Click(object sender, RoutedEventArgs e)
        {
            UpdateService.OpenReleasesPage();
        }

        /// <summary>
        /// Opens the GitHub repository page.
        /// </summary>
        private void Help_GitHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/ChadRoesler/PixlPunkt",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Failed to open GitHub page: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Shows an About dialog with version information.
        /// </summary>
        private async void Help_About_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "About PixlPunkt",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = "PixlPunkt", 
                            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
                        },
                        new TextBlock 
                        { 
                            Text = $"Version {UpdateService.GetCurrentVersionString()}",
                            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                        },
                        new TextBlock 
                        { 
                            Text = "A modern pixel art editor for Windows.",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 8, 0, 0)
                        },
                        new HyperlinkButton
                        {
                            Content = "View on GitHub",
                            NavigateUri = new Uri("https://github.com/ChadRoesler/PixlPunkt"),
                            Margin = new Thickness(0, 8, 0, 0)
                        }
                    }
                },
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close
            };

            await ShowDialogGuardedAsync(dlg);
        }
    }
}
