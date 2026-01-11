using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Settings;
using PixlPunkt.Core.Tools;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.UI.Settings
{
    /// <summary>
    /// View model for a shortcut list item.
    /// </summary>
    public sealed class ShortcutListItem
    {
        public string ToolId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string CategoryDisplay { get; set; } = string.Empty;
        public ToolCategory Category { get; set; }
        public Icon Icon { get; set; } = Icon.Apps;
        public string ShortcutDisplay { get; set; } = "(none)";
        public KeyBinding? CurrentBinding { get; set; }
        public KeyBinding? DefaultBinding { get; set; }
        public Visibility HasConflict { get; set; } = Visibility.Collapsed;
        public Visibility IsCustomized { get; set; } = Visibility.Collapsed;
        public bool IsPlugin { get; set; }
    }

    /// <summary>
    /// Panel for configuring keyboard shortcuts in the settings window.
    /// </summary>
    public sealed partial class ShortcutSettingsPanel : UserControl
    {
        private readonly ObservableCollection<ShortcutListItem> _allShortcuts = [];
        private readonly ObservableCollection<ShortcutListItem> _filteredShortcuts = [];
        private ToolState? _toolState;
        private ShortcutSettings? _shortcutSettings;
        private ShortcutConflictDetector? _conflictDetector;
        private List<ShortcutConflict>? _currentConflicts;

        public ShortcutSettingsPanel()
        {
            this.InitializeComponent();
            ShortcutListView.ItemsSource = _filteredShortcuts;
        }

        /// <summary>
        /// Initializes the panel with the tool state.
        /// </summary>
        /// <param name="toolState">The application's tool state.</param>
        public void Initialize(ToolState toolState)
        {
            _toolState = toolState ?? throw new ArgumentNullException(nameof(toolState));
            _shortcutSettings = ShortcutSettings.Instance;
            _conflictDetector = new ShortcutConflictDetector(_toolState, _shortcutSettings);

            ShowConflictWarningsCheck.IsChecked = _shortcutSettings.ShowConflictWarnings;

            RefreshShortcutList();
            UpdateConflictWarning();
        }

        /// <summary>
        /// Refreshes the list of shortcuts from the tool state.
        /// </summary>
        public void RefreshShortcutList()
        {
            if (_toolState == null || _shortcutSettings == null)
                return;

            _allShortcuts.Clear();

            // Detect conflicts first
            _currentConflicts = _conflictDetector?.DetectConflicts() ?? [];
            var conflictingToolIds = new HashSet<string>(
                _currentConflicts.SelectMany(c => c.ConflictingToolIds));

            // Built-in tools
            foreach (var (toolId, settings) in _toolState.GetAllToolSettingsById())
            {
                var defaultBinding = settings.Shortcut;
                var effectiveBinding = _shortcutSettings.GetEffectiveBinding(toolId, defaultBinding);
                var isCustomized = _shortcutSettings.CustomBindings.ContainsKey(toolId);

                var item = new ShortcutListItem
                {
                    ToolId = toolId,
                    DisplayName = settings.DisplayName,
                    Category = GetToolCategory(toolId),
                    CategoryDisplay = GetCategoryDisplayName(GetToolCategory(toolId)),
                    Icon = settings.Icon,
                    ShortcutDisplay = effectiveBinding?.ToString() ?? "(none)",
                    CurrentBinding = effectiveBinding,
                    DefaultBinding = defaultBinding,
                    HasConflict = conflictingToolIds.Contains(toolId) ? Visibility.Visible : Visibility.Collapsed,
                    IsCustomized = isCustomized ? Visibility.Visible : Visibility.Collapsed,
                    IsPlugin = false
                };

                _allShortcuts.Add(item);
            }

            // Plugin tools
            foreach (var registration in _toolState.AllRegistrations)
            {
                // Skip built-in tools (already added)
                if (ToolIds.IsBuiltIn(registration.Id))
                    continue;

                var defaultBinding = registration.Settings?.Shortcut;
                var effectiveBinding = _shortcutSettings.GetEffectiveBinding(registration.Id, defaultBinding);
                var isCustomized = _shortcutSettings.CustomBindings.ContainsKey(registration.Id);

                var item = new ShortcutListItem
                {
                    ToolId = registration.Id,
                    DisplayName = registration.DisplayName,
                    Category = registration.Category,
                    CategoryDisplay = $"{GetCategoryDisplayName(registration.Category)} (Plugin)",
                    Icon = registration.Settings?.Icon ?? Icon.Apps,
                    ShortcutDisplay = effectiveBinding?.ToString() ?? "(none)",
                    CurrentBinding = effectiveBinding,
                    DefaultBinding = defaultBinding,
                    HasConflict = conflictingToolIds.Contains(registration.Id) ? Visibility.Visible : Visibility.Collapsed,
                    IsCustomized = isCustomized ? Visibility.Visible : Visibility.Collapsed,
                    IsPlugin = true
                };

                _allShortcuts.Add(item);
            }

            // Sort by category then name
            var sorted = _allShortcuts
                .OrderBy(s => s.Category)
                .ThenBy(s => s.DisplayName)
                .ToList();

            _allShortcuts.Clear();
            foreach (var item in sorted)
                _allShortcuts.Add(item);

            ApplyFilter();
            ShortcutCountText.Text = $"{_allShortcuts.Count} shortcuts";
        }

        private ToolCategory GetToolCategory(string toolId)
        {
            return _toolState?.GetCategory(toolId) ?? ToolCategory.Utility;
        }

        private static string GetCategoryDisplayName(ToolCategory category)
        {
            return category switch
            {
                ToolCategory.Brush => "Brush Tools",
                ToolCategory.Select => "Selection Tools",
                ToolCategory.Shape => "Shape Tools",
                ToolCategory.Utility => "Utility Tools",
                ToolCategory.Tile => "Tile Tools",
                _ => "Other"
            };
        }

        private void ApplyFilter()
        {
            _filteredShortcuts.Clear();

            // Guard against being called before XAML controls are initialized
            if (CategoryFilter == null || SearchBox == null)
                return;

            var categoryIndex = CategoryFilter.SelectedIndex;
            var searchText = SearchBox.Text?.ToLowerInvariant() ?? "";

            foreach (var item in _allShortcuts)
            {
                // Category filter
                bool matchesCategory = categoryIndex switch
                {
                    0 => true, // All
                    1 => item.Category == ToolCategory.Brush,
                    2 => item.Category == ToolCategory.Select,
                    3 => item.Category == ToolCategory.Shape,
                    4 => item.Category == ToolCategory.Utility,
                    5 => item.Category == ToolCategory.Tile,
                    6 => item.IsPlugin,
                    _ => true
                };

                // Search filter
                bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                    item.DisplayName.ToLowerInvariant().Contains(searchText) ||
                    item.ToolId.ToLowerInvariant().Contains(searchText) ||
                    item.ShortcutDisplay.ToLowerInvariant().Contains(searchText);

                if (matchesCategory && matchesSearch)
                {
                    _filteredShortcuts.Add(item);
                }
            }
        }

        private void UpdateConflictWarning()
        {
            if (_currentConflicts == null || _currentConflicts.Count == 0)
            {
                ConflictWarningBanner.Visibility = Visibility.Collapsed;
                return;
            }

            var undismissedConflicts = _currentConflicts
                .Where(c => !(_shortcutSettings?.IsConflictDismissed(c.BindingKey) ?? false))
                .ToList();

            if (undismissedConflicts.Count == 0)
            {
                ConflictWarningBanner.Visibility = Visibility.Collapsed;
                return;
            }

            ConflictWarningBanner.Visibility = Visibility.Visible;
            ConflictDetailsText.Text = $"{undismissedConflicts.Count} shortcut(s) are bound to multiple tools.";
        }

        private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private async void EditShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string toolId)
                return;

            var item = _allShortcuts.FirstOrDefault(s => s.ToolId == toolId);
            if (item == null)
                return;

            var dialog = new ShortcutEditDialog(
                item.DisplayName,
                item.CurrentBinding,
                item.DefaultBinding,
                _conflictDetector,
                toolId);

            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // Save the new binding
                var newBinding = dialog.ResultBinding;
                _shortcutSettings?.SetCustomBinding(toolId, newBinding);
                _shortcutSettings?.Save();

                RefreshShortcutList();
                UpdateConflictWarning();

                LoggingService.Info("Updated shortcut for {ToolId}: {Shortcut}",
                    toolId, newBinding?.ToString() ?? "(none)");
            }
        }

        private async void ViewConflictsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentConflicts == null || _currentConflicts.Count == 0)
                return;

            var conflictText = string.Join("\n\n", _currentConflicts.Select(c =>
                $"Shortcut: {c.ShortcutDisplay}\n" +
                $"Tools: {string.Join(", ", c.ConflictingToolNames)}"));

            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Shortcut Conflicts",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = conflictText,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 400
                    },
                    MaxHeight = 300
                },
                CloseButtonText = "Close",
                RequestedTheme = GetEffectiveTheme()
            };

            await dialog.ShowAsync();
        }

        private void DismissConflictsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentConflicts == null || _shortcutSettings == null)
                return;

            foreach (var conflict in _currentConflicts)
            {
                _shortcutSettings.DismissConflict(conflict.BindingKey);
            }

            _shortcutSettings.Save();
            UpdateConflictWarning();

            LoggingService.Info("Dismissed {Count} shortcut conflicts", _currentConflicts.Count);
        }

        private async void ResetAllBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Reset All Shortcuts",
                Content = "Are you sure you want to reset all keyboard shortcuts to their defaults?\n\nThis cannot be undone.",
                PrimaryButtonText = "Reset",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = GetEffectiveTheme()
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _shortcutSettings?.ResetAllToDefaults();
                _shortcutSettings?.Save();

                RefreshShortcutList();
                UpdateConflictWarning();

                LoggingService.Info("Reset all shortcuts to defaults");
            }
        }

        /// <summary>
        /// Gets the effective theme based on app settings.
        /// </summary>
        private static ElementTheme GetEffectiveTheme()
        {
            try
            {
                var settings = AppSettings.Instance;
                return settings.AppTheme switch
                {
                    AppThemeChoice.Light => ElementTheme.Light,
                    AppThemeChoice.Dark => ElementTheme.Dark,
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
                return foreground.R > 128 ? ElementTheme.Dark : ElementTheme.Light;
            }
            catch
            {
                return ElementTheme.Dark;
            }
        }

        /// <summary>
        /// Applies the current settings.
        /// </summary>
        public void ApplySettings()
        {
            if (_shortcutSettings == null)
                return;

            _shortcutSettings.ShowConflictWarnings = ShowConflictWarningsCheck.IsChecked ?? true;
            _shortcutSettings.Save();
        }
    }

    /// <summary>
    /// Dialog for editing a single keyboard shortcut.
    /// </summary>
    public sealed class ShortcutEditDialog : ContentDialog
    {
        private readonly TextBlock _shortcutDisplay;
        private readonly TextBlock _conflictWarning;
        private readonly Button _clearButton;
        private readonly Button _resetButton;
        private readonly ShortcutConflictDetector? _conflictDetector;
        private readonly string _toolId;
        private readonly KeyBinding? _defaultBinding;

        public KeyBinding? ResultBinding { get; private set; }

        public ShortcutEditDialog(
            string toolName,
            KeyBinding? currentBinding,
            KeyBinding? defaultBinding,
            ShortcutConflictDetector? conflictDetector,
            string toolId)
        {
            _conflictDetector = conflictDetector;
            _toolId = toolId;
            _defaultBinding = defaultBinding;
            ResultBinding = currentBinding;

            Title = $"Edit Shortcut: {toolName}";
            PrimaryButtonText = "Save";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            // Apply theme from app settings
            RequestedTheme = GetEffectiveTheme();

            var panel = new StackPanel { Spacing = 16, MinWidth = 300 };

            panel.Children.Add(new TextBlock
            {
                Text = "Press the key combination you want to use:",
                TextWrapping = TextWrapping.Wrap
            });

            // Shortcut display box - uses theme resource for background
            var displayBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 16, 20, 16),
                MinHeight = 60
            };

            // Set background using theme resource lookup
            displayBorder.SetValue(Border.BackgroundProperty,
                Application.Current.Resources["ControlFillColorDefaultBrush"]);

            _shortcutDisplay = new TextBlock
            {
                Text = currentBinding?.ToString() ?? "(none)",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            displayBorder.Child = _shortcutDisplay;
            panel.Children.Add(displayBorder);

            // Conflict warning
            _conflictWarning = new TextBlock
            {
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Orange),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, -8, 0, 0)
            };
            panel.Children.Add(_conflictWarning);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            _clearButton = new Button { Content = "Clear" };
            _clearButton.Click += (s, e) =>
            {
                ResultBinding = null;
                _shortcutDisplay.Text = "(none)";
                _conflictWarning.Visibility = Visibility.Collapsed;
            };

            _resetButton = new Button { Content = "Reset to Default" };
            _resetButton.Click += (s, e) =>
            {
                ResultBinding = _defaultBinding;
                _shortcutDisplay.Text = _defaultBinding?.ToString() ?? "(none)";
                CheckForConflicts();
            };

            buttonPanel.Children.Add(_clearButton);
            buttonPanel.Children.Add(_resetButton);
            panel.Children.Add(buttonPanel);

            // Instructions
            panel.Children.Add(new TextBlock
            {
                Text = "Tip: Press Escape to cancel, Backspace to clear",
                Opacity = 0.6,
                FontSize = 12
            });

            Content = panel;

            // Use PreviewKeyDown to capture key presses before they're handled by dialog controls
            this.PreviewKeyDown += OnPreviewKeyDown;
        }

        private static ElementTheme GetEffectiveTheme()
        {
            try
            {
                var settings = AppSettings.Instance;
                return settings.AppTheme switch
                {
                    AppThemeChoice.Light => ElementTheme.Light,
                    AppThemeChoice.Dark => ElementTheme.Dark,
                    _ => GetSystemTheme()
                };
            }
            catch
            {
                return ElementTheme.Dark;
            }
        }

        private static ElementTheme GetSystemTheme()
        {
            try
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var foreground = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);
                return foreground.R > 128 ? ElementTheme.Dark : ElementTheme.Light;
            }
            catch
            {
                return ElementTheme.Dark;
            }
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Escape cancels - let this one through to close the dialog naturally
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                return; // Don't handle - let dialog close normally
            }

            // Backspace/Delete clears
            if (e.Key == Windows.System.VirtualKey.Back ||
                e.Key == Windows.System.VirtualKey.Delete)
            {
                ResultBinding = null;
                _shortcutDisplay.Text = "(none)";
                _conflictWarning.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }

            // Ignore modifier-only presses
            if (e.Key == Windows.System.VirtualKey.Control ||
                e.Key == Windows.System.VirtualKey.Shift ||
                e.Key == Windows.System.VirtualKey.Menu ||
                e.Key == Windows.System.VirtualKey.LeftControl ||
                e.Key == Windows.System.VirtualKey.RightControl ||
                e.Key == Windows.System.VirtualKey.LeftShift ||
                e.Key == Windows.System.VirtualKey.RightShift ||
                e.Key == Windows.System.VirtualKey.LeftMenu ||
                e.Key == Windows.System.VirtualKey.RightMenu)
            {
                return;
            }

            // Ignore Tab (used for navigation) and Enter (used for dialog buttons)
            if (e.Key == Windows.System.VirtualKey.Tab ||
                e.Key == Windows.System.VirtualKey.Enter)
            {
                return;
            }

            // Get modifier state
            var ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
                Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            var shift = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
                Windows.System.VirtualKey.Shift) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            var alt = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
                Windows.System.VirtualKey.Menu) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            // Create new binding
            ResultBinding = new KeyBinding((VirtualKey)(int)e.Key, ctrl, shift, alt);
            _shortcutDisplay.Text = ResultBinding.ToString();

            CheckForConflicts();
            e.Handled = true;
        }

        private void CheckForConflicts()
        {
            if (_conflictDetector == null || ResultBinding == null)
            {
                _conflictWarning.Visibility = Visibility.Collapsed;
                return;
            }

            var conflicts = _conflictDetector.CheckForConflicts(_toolId, ResultBinding);
            if (conflicts.Count > 0)
            {
                var names = string.Join(", ", conflicts.Select(c => c.DisplayName));
                _conflictWarning.Text = $"?? Conflicts with: {names}";
                _conflictWarning.Visibility = Visibility.Visible;
            }
            else
            {
                _conflictWarning.Visibility = Visibility.Collapsed;
            }
        }
    }
}
