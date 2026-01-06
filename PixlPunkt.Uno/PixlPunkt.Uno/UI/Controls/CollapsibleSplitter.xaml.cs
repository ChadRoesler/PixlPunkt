using System;
using FluentIcons.Common;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace PixlPunkt.Uno.UI.Controls
{
    /// <summary>
    /// A vertical splitter control with a collapse/expand button.
    /// When collapsed, the target column is hidden and the button shows an expand arrow.
    /// Supports click-to-collapse and drag-to-resize on the entire control including the button.
    /// </summary>
    public sealed partial class CollapsibleSplitter : UserControl
    {
        // ====================================================================
        // DEPENDENCY PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets or sets whether to collapse the column to the left (true) or right (false).
        /// </summary>
        public static readonly DependencyProperty CollapseLeftProperty =
            DependencyProperty.Register(nameof(CollapseLeft), typeof(bool), typeof(CollapsibleSplitter),
                new PropertyMetadata(true, OnCollapseDirectionChanged));

        /// <summary>
        /// Gets or sets whether the target panel is currently collapsed.
        /// </summary>
        public static readonly DependencyProperty IsCollapsedProperty =
            DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(CollapsibleSplitter),
                new PropertyMetadata(false, OnIsCollapsedChanged));

        /// <summary>
        /// Gets or sets the default width to restore when expanding (if no previous width saved).
        /// </summary>
        public static readonly DependencyProperty DefaultExpandedWidthProperty =
            DependencyProperty.Register(nameof(DefaultExpandedWidth), typeof(double), typeof(CollapsibleSplitter),
                new PropertyMetadata(200.0));

        /// <summary>
        /// Gets or sets the minimum width when expanded.
        /// </summary>
        public static readonly DependencyProperty MinExpandedWidthProperty =
            DependencyProperty.Register(nameof(MinExpandedWidth), typeof(double), typeof(CollapsibleSplitter),
                new PropertyMetadata(100.0));

        // ====================================================================
        // PROPERTIES
        // ====================================================================

        /// <summary>
        /// If true, collapses the column to the left of this splitter.
        /// If false, collapses the column to the right.
        /// </summary>
        public bool CollapseLeft
        {
            get => (bool)GetValue(CollapseLeftProperty);
            set => SetValue(CollapseLeftProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the panel is collapsed.
        /// </summary>
        public bool IsCollapsed
        {
            get => (bool)GetValue(IsCollapsedProperty);
            set => SetValue(IsCollapsedProperty, value);
        }

        /// <summary>
        /// The default width to expand to if no previous width is saved.
        /// </summary>
        public double DefaultExpandedWidth
        {
            get => (double)GetValue(DefaultExpandedWidthProperty);
            set => SetValue(DefaultExpandedWidthProperty, value);
        }

        /// <summary>
        /// The minimum width when expanded.
        /// </summary>
        public double MinExpandedWidth
        {
            get => (double)GetValue(MinExpandedWidthProperty);
            set => SetValue(MinExpandedWidthProperty, value);
        }

        // ====================================================================
        // FIELDS
        // ====================================================================

        /// <summary>
        /// Stores the width of the target column before it was collapsed.
        /// </summary>
        private double _previousWidth;

        /// <summary>
        /// The index of the target column in the parent grid.
        /// </summary>
        private int _targetColumnIndex = -1;

        /// <summary>
        /// Whether we're currently dragging.
        /// </summary>
        private bool _isDragging;

        /// <summary>
        /// The starting X position when drag began.
        /// </summary>
        private double _dragStartX;

        /// <summary>
        /// The column width when drag began.
        /// </summary>
        private double _dragStartWidth;

        /// <summary>
        /// Threshold in pixels - if drag distance exceeds this, it's a drag not a click.
        /// </summary>
        private const double DragThreshold = 5.0;

        /// <summary>
        /// Whether the drag threshold has been exceeded (true drag vs potential click).
        /// </summary>
        private bool _dragThresholdExceeded;

        /// <summary>
        /// Whether a pointer press is active (for click detection).
        /// </summary>
        private bool _pointerPressed;

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when the collapsed state changes.
        /// </summary>
        public event Action<bool>? CollapsedChanged;

        // ====================================================================
        // CTOR
        // ====================================================================

        public CollapsibleSplitter()
        {
            InitializeComponent();
            Loaded += OnLoaded;

            // Wire up pointer events for drag resizing AND click detection
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += OnPointerCaptureLost;
            PointerEntered += OnPointerEntered;
            PointerExited += OnPointerExited;
        }

        // ====================================================================
        // INITIALIZATION
        // ====================================================================

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateIconDirection();
            ResolveTargetColumnIndex();

            // Initialize previous width
            var col = GetTargetColumn();
            if (col != null && col.ActualWidth > MinExpandedWidth)
            {
                _previousWidth = col.ActualWidth;
            }
            else
            {
                _previousWidth = DefaultExpandedWidth;
            }
        }

        /// <summary>
        /// Resolves the target column index based on CollapseLeft setting.
        /// </summary>
        private void ResolveTargetColumnIndex()
        {
            var grid = FindParent<Grid>(this);
            if (grid == null) return;

            int myColumn = Grid.GetColumn(this);

            // Determine target column index
            _targetColumnIndex = CollapseLeft ? myColumn - 1 : myColumn + 1;
        }

        /// <summary>
        /// Gets the target column definition (fresh lookup each time to avoid stale references).
        /// </summary>
        private ColumnDefinition? GetTargetColumn()
        {
            var grid = FindParent<Grid>(this);
            if (grid == null) return null;

            if (_targetColumnIndex < 0)
            {
                ResolveTargetColumnIndex();
            }

            if (_targetColumnIndex >= 0 && _targetColumnIndex < grid.ColumnDefinitions.Count)
            {
                return grid.ColumnDefinitions[_targetColumnIndex];
            }

            return null;
        }

        // ====================================================================
        // PROPERTY CHANGE HANDLERS
        // ====================================================================

        private static void OnCollapseDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var splitter = (CollapsibleSplitter)d;
            splitter.UpdateIconDirection();
            splitter.ResolveTargetColumnIndex();
        }

        private static void OnIsCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var splitter = (CollapsibleSplitter)d;
            splitter.ApplyCollapsedState();
            splitter.CollapsedChanged?.Invoke((bool)e.NewValue);
        }

        // ====================================================================
        // UI LOGIC
        // ====================================================================

        /// <summary>
        /// Updates the chevron icon direction based on collapse direction and state.
        /// </summary>
        private void UpdateIconDirection()
        {
            if (CollapseIcon == null) return;

            if (IsCollapsed)
            {
                // When collapsed, arrow points toward the collapsed area (to expand)
                CollapseIcon.Icon = CollapseLeft ? Icon.ChevronRight : Icon.ChevronLeft;
                ToolTipService.SetToolTip(CollapseButton, "Expand Panel");
            }
            else
            {
                // When expanded, arrow points toward the edge (to collapse)
                CollapseIcon.Icon = CollapseLeft ? Icon.ChevronLeft : Icon.ChevronRight;
                ToolTipService.SetToolTip(CollapseButton, "Collapse Panel");
            }
        }

        /// <summary>
        /// Applies the collapsed or expanded state to the target column.
        /// </summary>
        private void ApplyCollapsedState()
        {
            UpdateIconDirection();

            var grid = FindParent<Grid>(this);
            var targetColumn = GetTargetColumn();
            if (targetColumn == null || grid == null) return;

            if (IsCollapsed)
            {
                // Save current width before collapsing
                double currentWidth = targetColumn.ActualWidth;
                if (currentWidth > MinExpandedWidth)
                {
                    _previousWidth = currentWidth;
                }

                // Collapse to zero - set MinWidth first to allow zero
                targetColumn.MinWidth = 0;
                targetColumn.Width = new GridLength(0, GridUnitType.Pixel);
            }
            else
            {
                // Restore to previous width or default
                double restoreWidth = _previousWidth > MinExpandedWidth
                    ? _previousWidth
                    : DefaultExpandedWidth;

                targetColumn.MinWidth = MinExpandedWidth;
                targetColumn.Width = new GridLength(restoreWidth, GridUnitType.Pixel);
            }

            // Force the parent Grid to re-measure and re-arrange
            grid.InvalidateMeasure();
            grid.InvalidateArrange();
            grid.UpdateLayout();
        }

        // ====================================================================
        // DRAG RESIZING + CLICK DETECTION (pointer-based)
        // ====================================================================

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Show resize cursor when hovering (not collapsed)
            if (!IsCollapsed)
            {
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
            }
            else
            {
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            }
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging)
            {
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            }
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed) return;

            _pointerPressed = true;
            _dragThresholdExceeded = false;

            var grid = FindParent<Grid>(this);
            if (grid == null) return;

            _dragStartX = e.GetCurrentPoint(grid).Position.X;

            // Only setup drag if not collapsed
            if (!IsCollapsed)
            {
                var targetColumn = GetTargetColumn();
                if (targetColumn != null)
                {
                    _dragStartWidth = targetColumn.ActualWidth;
                }
            }

            CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_pointerPressed) return;

            // If collapsed, no dragging - just wait for release to toggle
            if (IsCollapsed) return;

            var grid = FindParent<Grid>(this);
            if (grid == null) return;

            double currentX = e.GetCurrentPoint(grid).Position.X;
            double totalDelta = Math.Abs(currentX - _dragStartX);

            // Check if we've exceeded the drag threshold
            if (!_dragThresholdExceeded && totalDelta > DragThreshold)
            {
                _dragThresholdExceeded = true;
                _isDragging = true;
            }

            // Only resize if we're actually dragging
            if (_isDragging)
            {
                var targetColumn = GetTargetColumn();
                if (targetColumn == null) return;

                double delta = currentX - _dragStartX;

                // For CollapseLeft=true (left panel), dragging right = wider
                // For CollapseLeft=false (right panel), dragging left = wider
                if (!CollapseLeft)
                {
                    delta = -delta;
                }

                double newWidth = Math.Max(MinExpandedWidth, _dragStartWidth + delta);

                // Clamp to reasonable maximum
                double maxWidth = grid.ActualWidth * 0.6;
                newWidth = Math.Min(newWidth, maxWidth);

                targetColumn.Width = new GridLength(newWidth);
                _previousWidth = newWidth;
            }

            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_pointerPressed)
            {
                // If we didn't exceed drag threshold, treat as a click -> toggle collapse
                if (!_dragThresholdExceeded)
                {
                    IsCollapsed = !IsCollapsed;
                }

                _pointerPressed = false;
                _isDragging = false;
                _dragThresholdExceeded = false;
                ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _pointerPressed = false;
            _isDragging = false;
            _dragThresholdExceeded = false;
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }

        // ====================================================================
        // HELPERS
        // ====================================================================

        private static T? FindParent<T>(DependencyObject start) where T : class
        {
            var parent = VisualTreeHelper.GetParent(start);
            while (parent != null && parent is not T)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }
    }
}
