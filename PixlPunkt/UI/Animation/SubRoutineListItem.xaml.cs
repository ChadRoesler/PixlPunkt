using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Animation;
using System;

namespace PixlPunkt.UI.Animation
{
    /// <summary>
    /// Represents a single sub-routine item in the animation sub-routine list.
    /// Shows reel name, timing, and provides quick actions (enable/disable, delete).
    /// </summary>
    public sealed partial class SubRoutineListItem : UserControl
    {
        private TextBlock? _nameBlock;
        private TextBlock? _timingBlock;
        private TextBlock? _durationBlock;
        private ToggleSwitch? _enabledToggle;
        private Button? _deleteButton;

        /// <summary>
        /// Gets or sets the sub-routine this item represents.
        /// </summary>
        public AnimationSubRoutine SubRoutine
        {
            get => (AnimationSubRoutine)GetValue(SubRoutineProperty);
            set => SetValue(SubRoutineProperty, value);
        }

        public static readonly DependencyProperty SubRoutineProperty =
            DependencyProperty.Register(
                nameof(SubRoutine),
                typeof(AnimationSubRoutine),
                typeof(SubRoutineListItem),
                new PropertyMetadata(null, OnSubRoutineChanged));

        /// <summary>
        /// Raised when the user requests to delete this sub-routine.
        /// </summary>
        public event EventHandler<SubRoutineDeletedEventArgs>? DeleteRequested;

        /// <summary>
        /// Raised when the user selects this sub-routine.
        /// </summary>
        public event EventHandler<SubRoutineSelectedEventArgs>? SelectionChanged;

        /// <summary>
        /// Gets or sets whether this item is selected.
        /// </summary>
        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(
                nameof(IsSelected),
                typeof(bool),
                typeof(SubRoutineListItem),
                new PropertyMetadata(false));

        public SubRoutineListItem()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            // Root border
            var root = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(4)
            };

            // Main grid
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            // Icon
            var iconBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
            };
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            // Info stack
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            _nameBlock = new TextBlock { Text = "Sub-Routine", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 };
            infoStack.Children.Add(_nameBlock);

            var timingStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            _timingBlock = new TextBlock { Text = "Frame 0 - 24", FontSize = 10, Opacity = 0.7 };
            timingStack.Children.Add(_timingBlock);
            timingStack.Children.Add(new TextBlock { Text = "•", Opacity = 0.5 });
            _durationBlock = new TextBlock { Text = "24 frames", FontSize = 10, Opacity = 0.7 };
            timingStack.Children.Add(_durationBlock);
            infoStack.Children.Add(timingStack);

            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            // Toggle
            _enabledToggle = new ToggleSwitch { IsOn = true, VerticalAlignment = VerticalAlignment.Center };
            _enabledToggle.Toggled += EnabledToggle_Toggled;
            ToolTipService.SetToolTip(_enabledToggle, "Enable/Disable this sub-routine");
            Grid.SetColumn(_enabledToggle, 2);
            grid.Children.Add(_enabledToggle);

            // Delete button
            _deleteButton = new Button
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Padding = new Thickness(4),
                VerticalAlignment = VerticalAlignment.Center
            };
            _deleteButton.Click += DeleteButton_Click;
            ToolTipService.SetToolTip(_deleteButton, "Delete this sub-routine");
            // Icon would be set here if we had access to FluentIcon
            _deleteButton.Content = "?";
            Grid.SetColumn(_deleteButton, 3);
            grid.Children.Add(_deleteButton);

            root.Child = grid;
            this.Content = root;
        }

        private static void OnSubRoutineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SubRoutineListItem item && e.NewValue is AnimationSubRoutine subRoutine)
            {
                item.UpdateDisplay(subRoutine);
            }
        }

        private void UpdateDisplay(AnimationSubRoutine subRoutine)
        {
            if (_nameBlock == null) return;

            _nameBlock.Text = subRoutine.DisplayName;
            _timingBlock!.Text = $"Frame {subRoutine.StartFrame} - {subRoutine.EndFrame}";
            _durationBlock!.Text = $"{subRoutine.DurationFrames} frames";
            _enabledToggle!.IsOn = subRoutine.IsEnabled;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (SubRoutine == null) return;
            DeleteRequested?.Invoke(this, new SubRoutineDeletedEventArgs(SubRoutine));
        }

        private void EnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (SubRoutine == null || _enabledToggle == null) return;
            SubRoutine.IsEnabled = _enabledToggle.IsOn;
        }
    }

    /// <summary>
    /// Event args for sub-routine deletion.
    /// </summary>
    public sealed class SubRoutineDeletedEventArgs : EventArgs
    {
        public AnimationSubRoutine SubRoutine { get; }

        public SubRoutineDeletedEventArgs(AnimationSubRoutine subRoutine)
        {
            SubRoutine = subRoutine;
        }
    }

    /// <summary>
    /// Event args for sub-routine selection.
    /// </summary>
    public sealed class SubRoutineSelectedEventArgs : EventArgs
    {
        public AnimationSubRoutine SubRoutine { get; }

        public SubRoutineSelectedEventArgs(AnimationSubRoutine subRoutine)
        {
            SubRoutine = subRoutine;
        }
    }
}
