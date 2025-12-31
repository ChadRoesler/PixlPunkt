using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.UI;
using PixlPunkt.Core.Animation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PixlPunkt.UI.Animation
{
    /// <summary>
    /// Renders animation sub-routines on an interactive timeline.
    /// Shows sub-routines as bars with drag handles for timing adjustment.
    /// </summary>
    public sealed partial class SubRoutineTimelineViewer : UserControl
    {
        private const int FrameHeaderHeight = 28;
        private const int SubRoutineBarHeight = 32;
        private const int SubRoutineRowSpacing = 4;
        private const int PixelsPerFrame = 4; // Zoom level

        private AnimationSubRoutineTrack? _track;
        private Canvas? _canvas;
        private AnimationSubRoutine? _draggingSubRoutine;
        private int _totalFrames = 24;

        public AnimationSubRoutineTrack? Track
        {
            get => _track;
            set
            {
                if (_track != null)
                {
                    _track.SubRoutineAdded -= OnSubRoutineChanged;
                    _track.SubRoutineRemoved -= OnSubRoutineChanged;
                    _track.SubRoutineChanged -= OnSubRoutineChanged;
                }

                _track = value;

                if (_track != null)
                {
                    _track.SubRoutineAdded += OnSubRoutineChanged;
                    _track.SubRoutineRemoved += OnSubRoutineChanged;
                    _track.SubRoutineChanged += OnSubRoutineChanged;
                }

                Redraw();
            }
        }

        /// <summary>
        /// Gets or sets the total frame count for the animation.
        /// </summary>
        public int TotalFrames
        {
            get => _totalFrames;
            set
            {
                if (_totalFrames != value)
                {
                    _totalFrames = Math.Max(1, value);
                    Redraw();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current playhead frame position (for visual indicator).
        /// </summary>
        public int CurrentFrame { get; set; }

        /// <summary>
        /// Raised when user modifies a sub-routine's timing via drag.
        /// </summary>
        public event EventHandler<SubRoutineTimingChangedEventArgs>? TimingChanged;

        public SubRoutineTimelineViewer()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
            };

            _canvas = new Canvas
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                MinHeight = 200
            };

            _canvas.PointerMoved += Canvas_PointerMoved;
            _canvas.PointerReleased += Canvas_PointerReleased;

            scrollViewer.Content = _canvas;
            this.Content = scrollViewer;
        }

        /// <summary>
        /// Redraws the timeline with current sub-routines.
        /// </summary>
        public void Redraw()
        {
            if (_canvas == null) return;

            _canvas.Children.Clear();

            int canvasWidth = Math.Max(500, _totalFrames * PixelsPerFrame);
            _canvas.Width = canvasWidth;
            _canvas.Height = FrameHeaderHeight + (_track?.SubRoutines.Count() ?? 0) * (SubRoutineBarHeight + SubRoutineRowSpacing) + 20;

            // Draw frame headers
            DrawFrameHeaders(canvasWidth);

            // Draw sub-routine bars
            DrawSubRoutineBars();
        }

        private void DrawFrameHeaders(int width)
        {
            if (_canvas == null) return;

            // Header background
            var headerBg = new Rectangle
            {
                Width = width,
                Height = FrameHeaderHeight,
                Fill = (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"]
            };
            _canvas.Children.Add(headerBg);
            Canvas.SetZIndex(headerBg, 10);

            // Frame numbers every 10 frames
            for (int frame = 0; frame < _totalFrames; frame += Math.Max(1, _totalFrames / 12))
            {
                int x = frame * PixelsPerFrame;
                
                var line = new Rectangle
                {
                    Width = 1,
                    Height = FrameHeaderHeight,
                    Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
                Canvas.SetLeft(line, x);
                _canvas.Children.Add(line);

                var text = new TextBlock
                {
                    Text = frame.ToString(),
                    FontSize = 9,
                    Opacity = 0.6
                };
                Canvas.SetLeft(text, x + 2);
                Canvas.SetTop(text, 4);
                _canvas.Children.Add(text);
            }
        }

        private void DrawSubRoutineBars()
        {
            if (_canvas == null || _track == null) return;

            int row = 0;
            foreach (var subRoutine in _track.SubRoutines)
            {
                DrawSubRoutineBar(subRoutine, row);
                row++;
            }
        }

        private void DrawSubRoutineBar(AnimationSubRoutine subRoutine, int row)
        {
            if (_canvas == null) return;

            int y = FrameHeaderHeight + (row * (SubRoutineBarHeight + SubRoutineRowSpacing));
            int x = subRoutine.StartFrame * PixelsPerFrame;
            int width = Math.Max(20, subRoutine.DurationFrames * PixelsPerFrame);

            // Main bar
            var bar = new Rectangle
            {
                Width = width,
                Height = SubRoutineBarHeight,
                Fill = new SolidColorBrush(Color.FromArgb(200, 52, 152, 219)), // Nice blue
                RadiusX = 4,
                RadiusY = 4
            };

            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);
            bar.PointerPressed += (s, e) => OnSubRoutineBarPressed(subRoutine, e);
            _canvas.Children.Add(bar);

            // Label
            var label = new TextBlock
            {
                Text = subRoutine.DisplayName,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            Canvas.SetLeft(label, x + 4);
            Canvas.SetTop(label, y + 4);
            _canvas.Children.Add(label);

            // Right resize handle
            var rightHandle = new Rectangle
            {
                Width = 8,
                Height = SubRoutineBarHeight,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.White),
                Opacity = 0.3,
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rightHandle, x + width - 4);
            Canvas.SetTop(rightHandle, y);
            rightHandle.PointerPressed += (s, e) => OnResizeHandlePressed(subRoutine, e);
            _canvas.Children.Add(rightHandle);
            Canvas.SetZIndex(rightHandle, 100);
        }

        private void OnSubRoutineBarPressed(AnimationSubRoutine subRoutine, PointerRoutedEventArgs e)
        {
            _draggingSubRoutine = subRoutine;
            e.Handled = true;
        }

        private void OnResizeHandlePressed(AnimationSubRoutine subRoutine, PointerRoutedEventArgs e)
        {
            _draggingSubRoutine = subRoutine;
            e.Handled = true;
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_draggingSubRoutine == null || _canvas == null) return;

            var position = e.GetCurrentPoint(_canvas).Position;
            int newStartFrame = Math.Max(0, (int)(position.X / PixelsPerFrame));
            
            if (_draggingSubRoutine.StartFrame != newStartFrame)
            {
                _draggingSubRoutine.StartFrame = newStartFrame;
                TimingChanged?.Invoke(this, new SubRoutineTimingChangedEventArgs(_draggingSubRoutine));
                Redraw();
            }

            e.Handled = true;
        }

        private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _draggingSubRoutine = null;
            e.Handled = true;
        }

        private void OnSubRoutineChanged(AnimationSubRoutine subRoutine)
        {
            Redraw();
        }
    }

    /// <summary>
    /// Event args for sub-routine timing changes.
    /// </summary>
    public sealed class SubRoutineTimingChangedEventArgs : EventArgs
    {
        public AnimationSubRoutine SubRoutine { get; }

        public SubRoutineTimingChangedEventArgs(AnimationSubRoutine subRoutine)
        {
            SubRoutine = subRoutine;
        }
    }
}
