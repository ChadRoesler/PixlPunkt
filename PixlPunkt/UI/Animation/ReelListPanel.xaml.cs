using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Animation;

namespace PixlPunkt.UI.Animation
{
    /// <summary>
    /// Panel displaying the list of animation reels.
    /// Allows adding, removing, duplicating, and selecting reels.
    /// </summary>
    public sealed partial class ReelListPanel : UserControl
    {
        // ====================================================================
        // FIELDS
        // ====================================================================

        private TileAnimationState? _state;
        private bool _suppressSelectionChange;

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when a text input gains focus (to suppress tool shortcuts).
        /// </summary>
        public event Action? TextInputFocused;

        /// <summary>
        /// Raised when a text input loses focus (to restore tool shortcuts).
        /// </summary>
        public event Action? TextInputUnfocused;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public ReelListPanel()
        {
            InitializeComponent();
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Binds the panel to a tile animation state.
        /// </summary>
        public void Bind(TileAnimationState? state)
        {
            // Unbind previous
            if (_state != null)
            {
                _state.ReelsChanged -= OnReelsChanged;
                _state.SelectedReelChanged -= OnSelectedReelChanged;
            }

            _state = state;

            // Bind new
            if (_state != null)
            {
                _state.ReelsChanged += OnReelsChanged;
                _state.SelectedReelChanged += OnSelectedReelChanged;
            }

            RefreshList();
        }

        // ====================================================================
        // EVENT HANDLERS
        // ====================================================================

        private void OnReelsChanged()
        {
            DispatcherQueue.TryEnqueue(RefreshList);
        }

        private void OnSelectedReelChanged(TileAnimationReel? reel)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _suppressSelectionChange = true;
                ReelListView.SelectedItem = reel;
                _suppressSelectionChange = false;
                UpdateButtonStates();
            });
        }

        private void ReelListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChange || _state == null) return;

            var selected = ReelListView.SelectedItem as TileAnimationReel;
            _state.SelectReel(selected);
            UpdateButtonStates();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (_state == null) return;

            var reel = _state.AddReel();
            _state.SelectReel(reel);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_state?.SelectedReel == null) return;

            var reel = _state.SelectedReel;
            _state.RemoveReel(reel);
        }

        private void DuplicateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_state?.SelectedReel == null) return;

            var duplicate = _state.DuplicateReel(_state.SelectedReel);
            _state.SelectReel(duplicate);
        }

        private void ReelSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TileAnimationReel reel)
            {
                ShowReelSettingsFlyout(btn, reel);
            }
        }

        // ====================================================================
        // PRIVATE METHODS
        // ====================================================================

        private void RefreshList()
        {
            _suppressSelectionChange = true;

            ReelListView.ItemsSource = null;
            ReelListView.ItemsSource = _state?.Reels;
            ReelListView.SelectedItem = _state?.SelectedReel;

            _suppressSelectionChange = false;
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = _state?.SelectedReel != null;
            DeleteButton.IsEnabled = hasSelection;
            DuplicateButton.IsEnabled = hasSelection;
        }

        private void ShowReelSettingsFlyout(FrameworkElement anchor, TileAnimationReel reel)
        {
            var flyout = new Flyout();

            var panel = new StackPanel { Spacing = 8, MinWidth = 200 };

            // Name editor
            var nameBox = new TextBox
            {
                Header = "Name",
                Text = reel.Name,
                PlaceholderText = "Animation name"
            };
            nameBox.TextChanged += (_, __) => reel.Name = nameBox.Text;

            // Suppress tool shortcuts while editing text
            nameBox.GotFocus += (_, __) => TextInputFocused?.Invoke();
            nameBox.LostFocus += (_, __) => TextInputUnfocused?.Invoke();

            panel.Children.Add(nameBox);

            // Frame time
            var frameTimeBox = new NumberBox
            {
                Header = "Frame Time (ms)",
                Value = reel.DefaultFrameTimeMs,
                Minimum = 10,
                Maximum = 10000,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            frameTimeBox.ValueChanged += (_, __) =>
            {
                if (!double.IsNaN(frameTimeBox.Value))
                {
                    reel.DefaultFrameTimeMs = (int)frameTimeBox.Value;
                    reel.NotifyChanged();
                }
            };

            // Suppress tool shortcuts while editing number
            frameTimeBox.GotFocus += (_, __) => TextInputFocused?.Invoke();
            frameTimeBox.LostFocus += (_, __) => TextInputUnfocused?.Invoke();

            panel.Children.Add(frameTimeBox);

            // Loop toggle
            var loopToggle = new ToggleSwitch
            {
                Header = "Loop",
                IsOn = reel.Loop
            };
            loopToggle.Toggled += (_, __) =>
            {
                reel.Loop = loopToggle.IsOn;
                reel.NotifyChanged();
            };
            panel.Children.Add(loopToggle);

            // Ping-pong toggle
            var pingPongToggle = new ToggleSwitch
            {
                Header = "Ping-Pong",
                IsOn = reel.PingPong
            };
            pingPongToggle.Toggled += (_, __) =>
            {
                reel.PingPong = pingPongToggle.IsOn;
                reel.NotifyChanged();
            };
            panel.Children.Add(pingPongToggle);

            // Restore shortcuts when flyout closes
            flyout.Closed += (_, __) => TextInputUnfocused?.Invoke();

            flyout.Content = panel;
            flyout.ShowAt(anchor);
        }
    }
}
