using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Settings;

namespace PixlPunkt.Uno.UI.Dialogs
{
    /// <summary>
    /// Dialog shown at startup when shortcut conflicts are detected.
    /// </summary>
    public sealed class ShortcutConflictWarningDialog
    {
        private readonly List<ShortcutConflict> _conflicts;
        private readonly XamlRoot _xamlRoot;

        public ShortcutConflictWarningDialog(IEnumerable<ShortcutConflict> conflicts, XamlRoot xamlRoot)
        {
            _conflicts = conflicts.ToList();
            _xamlRoot = xamlRoot;
        }

        /// <summary>
        /// Shows the conflict warning dialog.
        /// </summary>
        /// <returns>True if user wants to open settings, false to dismiss.</returns>
        public async System.Threading.Tasks.Task<bool> ShowAsync()
        {
            if (_conflicts.Count == 0)
                return false;

            var content = new StackPanel { Spacing = 12 };

            // Header
            content.Children.Add(new TextBlock
            {
                Text = $"The following keyboard shortcuts are assigned to multiple tools. " +
                       $"Only the first matching tool will activate when the shortcut is pressed.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 450
            });

            // Conflict list
            var listView = new ListView
            {
                MaxHeight = 250,
                SelectionMode = ListViewSelectionMode.None,
                ItemsSource = _conflicts.Select(c => new ConflictDisplayItem
                {
                    ShortcutDisplay = c.ShortcutDisplay,
                    ToolsDisplay = string.Join(", ", c.ConflictingToolNames)
                }).ToList()
            };

            // Build DataTemplate without hardcoded colors - use theme-aware resources
            listView.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(@"
                <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                    <Grid Padding='8,6'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='80'/>
                            <ColumnDefinition Width='*'/>
                        </Grid.ColumnDefinitions>
                        <Border Grid.Column='0' Background='{ThemeResource ControlFillColorDefaultBrush}' CornerRadius='4' Padding='8,4' VerticalAlignment='Center'>
                            <TextBlock Text='{Binding ShortcutDisplay}' FontFamily='Consolas' FontWeight='SemiBold' HorizontalAlignment='Center'/>
                        </Border>
                        <TextBlock Grid.Column='1' Text='{Binding ToolsDisplay}' Margin='12,0,0,0' VerticalAlignment='Center' TextWrapping='Wrap'/>
                    </Grid>
                </DataTemplate>");

            content.Children.Add(listView);

            // Don't show again option
            var dontShowCheckbox = new CheckBox
            {
                Content = "Don't show this warning again",
                Margin = new Thickness(0, 8, 0, 0)
            };
            content.Children.Add(dontShowCheckbox);

            // Tip
            content.Children.Add(new TextBlock
            {
                Text = "Tip: Go to Settings ? Shortcuts to customize keyboard shortcuts.",
                Opacity = 0.7,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            });

            var dialog = new ContentDialog
            {
                XamlRoot = _xamlRoot,
                Title = "Keyboard Shortcut Conflicts",
                Content = content,
                PrimaryButtonText = "Open Settings",
                SecondaryButtonText = "Dismiss",
                DefaultButton = ContentDialogButton.Secondary
            };

            // Apply theme from main window
            if (App.PixlPunktMainWindow is PixlPunktMainWindow mainWindow)
            {
                dialog.RequestedTheme = mainWindow.EffectiveAppTheme;
            }

            var result = await dialog.ShowAsync();

            // Save preference if checkbox is checked
            if (dontShowCheckbox.IsChecked == true)
            {
                var settings = ShortcutSettings.Instance;
                foreach (var conflict in _conflicts)
                {
                    settings.DismissConflict(conflict.BindingKey);
                }
                settings.Save();
            }

            return result == ContentDialogResult.Primary;
        }

        private sealed class ConflictDisplayItem
        {
            public string ShortcutDisplay { get; set; } = "";
            public string ToolsDisplay { get; set; } = "";
        }
    }
}
