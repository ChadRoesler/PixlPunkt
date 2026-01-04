using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Animation;
using System;
using System.Linq;
using System.Collections.Specialized;

namespace PixlPunkt.UI.Animation
{
    /// <summary>
    /// Panel that displays all animation sub-routines in a list.
    /// Handles add/remove/selection operations.
    /// </summary>
    public sealed partial class SubRoutineListPanel : UserControl
    {
        private AnimationSubRoutineTrack? _track;
        private StackPanel? _listPanel;
        private AnimationSubRoutine? _selectedSubRoutine;

        /// <summary>
        /// Gets or sets the sub-routine track being displayed.
        /// </summary>
        public AnimationSubRoutineTrack? Track
        {
            get => _track;
            set
            {
                if (_track != null)
                    _track.SubRoutineAdded -= OnSubRoutineAdded;

                _track = value;

                if (_track != null)
                    _track.SubRoutineAdded += OnSubRoutineAdded;

                RefreshList();
            }
        }

        /// <summary>
        /// Raised when a sub-routine is selected.
        /// </summary>
        public event EventHandler<SubRoutineSelectedEventArgs>? SubRoutineSelected;

        /// <summary>
        /// Gets the currently selected sub-routine, if any.
        /// </summary>
        public AnimationSubRoutine? SelectedSubRoutine => _selectedSubRoutine;

        public SubRoutineListPanel()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            // Root with scrolling
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            // List container
            _listPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 4,
                Padding = new Thickness(4)
            };

            scrollViewer.Content = _listPanel;

            // Root grid
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(scrollViewer);

            this.Content = grid;
        }

        /// <summary>
        /// Refreshes the list display from the current track.
        /// </summary>
        public void RefreshList()
        {
            _listPanel?.Children.Clear();

            if (_track == null)
                return;

            foreach (var subRoutine in _track.SubRoutines)
            {
                AddSubRoutineItem(subRoutine);
            }
        }

        private void AddSubRoutineItem(AnimationSubRoutine subRoutine)
        {
            if (_listPanel == null) return;

            var item = new SubRoutineListItem
            {
                SubRoutine = subRoutine
            };

            item.DeleteRequested += (s, e) => OnDeleteRequested(e.SubRoutine);
            item.SelectionChanged += (s, e) => OnSelectionChanged(e.SubRoutine);

            _listPanel.Children.Add(item);
        }

        private void OnSubRoutineAdded(AnimationSubRoutine subRoutine)
        {
            AddSubRoutineItem(subRoutine);
        }

        private void OnDeleteRequested(AnimationSubRoutine subRoutine)
        {
            if (_track?.Remove(subRoutine) == true)
            {
                _listPanel?.Children.Remove(_listPanel.Children.OfType<SubRoutineListItem>()
                    .FirstOrDefault(i => i.SubRoutine == subRoutine)!);
                
                if (_selectedSubRoutine == subRoutine)
                    _selectedSubRoutine = null;
            }
        }

        private void OnSelectionChanged(AnimationSubRoutine subRoutine)
        {
            _selectedSubRoutine = subRoutine;
            SubRoutineSelected?.Invoke(this, new SubRoutineSelectedEventArgs(subRoutine));
        }
    }
}
