using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Animation;
using System;

namespace PixlPunkt.UI.Animation
{
    /// <summary>
    /// Panel for editing animation sub-routine properties.
    /// Allows configuration of timing, transforms, and interpolation modes.
    /// </summary>
    public sealed partial class SubRoutinePropertyPanel : UserControl
    {
        private AnimationSubRoutine? _subRoutine;
        private bool _isUpdating = false;

        public AnimationSubRoutine? SubRoutine
        {
            get => _subRoutine;
            set
            {
                _subRoutine = value;
                UpdateUI();
            }
        }

        /// <summary>
        /// Raised when properties change.
        /// </summary>
        public event EventHandler<EventArgs>? SubRoutinePropertyChanged;

        public SubRoutinePropertyPanel()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var mainStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 16,
                Padding = new Thickness(12)
            };

            // Timing Section
            mainStack.Children.Add(BuildTimingSection());

            // Transform Section
            mainStack.Children.Add(BuildTransformSection());

            // Interpolation Section
            mainStack.Children.Add(BuildInterpolationSection());

            scrollViewer.Content = mainStack;
            this.Content = scrollViewer;
        }

        private UIElement BuildTimingSection()
        {
            var section = new StackPanel { Spacing = 8 };

            var header = new TextBlock
            {
                Text = "Timing",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            section.Children.Add(header);

            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var startLabel = new TextBlock { Text = "Start Frame:", Opacity = 0.7, FontSize = 11 };
            var startBox = new NumberBox { Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            startBox.ValueChanged += (s, e) =>
            {
                if (_subRoutine != null && !_isUpdating)
                    _subRoutine.StartFrame = (int)startBox.Value;
            };
            Grid.SetColumn(startBox, 0);

            var durationLabel = new TextBlock { Text = "Duration (Frames):", Opacity = 0.7, FontSize = 11 };
            var durationBox = new NumberBox { Minimum = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            durationBox.ValueChanged += (s, e) =>
            {
                if (_subRoutine != null && !_isUpdating)
                    _subRoutine.DurationFrames = (int)durationBox.Value;
            };
            Grid.SetColumn(durationBox, 1);

            section.Children.Add(startLabel);
            section.Children.Add(startBox);
            section.Children.Add(durationLabel);
            section.Children.Add(durationBox);

            return section;
        }

        private UIElement BuildTransformSection()
        {
            var section = new StackPanel { Spacing = 8 };

            var header = new TextBlock
            {
                Text = "Transform Keyframes",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            section.Children.Add(header);

            var info = new TextBlock
            {
                Text = "Position, scale, and rotation are interpolated between keyframes at 0% and 100% of the sub-routine duration.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Opacity = 0.7
            };
            section.Children.Add(info);

            var positionLabel = new TextBlock { Text = "Position", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            section.Children.Add(positionLabel);

            var posGrid = new Grid { ColumnSpacing = 8 };
            posGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            posGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var xBox = new NumberBox { Header = "X", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var yBox = new NumberBox { Header = "Y", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            Grid.SetColumn(yBox, 1);
            posGrid.Children.Add(xBox);
            posGrid.Children.Add(yBox);
            section.Children.Add(posGrid);

            var scaleLabel = new TextBlock { Text = "Scale", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            section.Children.Add(scaleLabel);

            var scaleBox = new NumberBox { Header = "Scale", Value = 1.0, Minimum = 0.1, Maximum = 10, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            section.Children.Add(scaleBox);

            var rotationLabel = new TextBlock { Text = "Rotation", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            section.Children.Add(rotationLabel);

            var rotationBox = new Slider { Minimum = -180, Maximum = 180, StepFrequency = 1, Header = "Degrees" };
            section.Children.Add(rotationBox);

            return section;
        }

        private UIElement BuildInterpolationSection()
        {
            var section = new StackPanel { Spacing = 8 };

            var header = new TextBlock
            {
                Text = "Interpolation",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            section.Children.Add(header);

            var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            combo.Items.Add("Linear");
            combo.Items.Add("EaseInQuad");
            combo.Items.Add("EaseOutQuad");
            combo.Items.Add("EaseInOutQuad");
            combo.Items.Add("EaseInCubic");
            combo.Items.Add("EaseOutCubic");
            combo.Items.Add("EaseInOutCubic");
            combo.SelectedIndex = 0;

            var label = new TextBlock { Text = "Easing Mode", Opacity = 0.7, FontSize = 11 };
            section.Children.Add(label);
            section.Children.Add(combo);

            return section;
        }

        private void UpdateUI()
        {
            _isUpdating = true;
            // Would update all controls with SubRoutine data here
            _isUpdating = false;
        }
    }
}
