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
    /// A horizontal splitter control with a collapse/expand button.
    /// When collapsed, the target row is hidden and the button shows an expand arrow.
    /// Used for bottom panels like the Animation Panel.
    /// Supports click-to-collapse and drag-to-resize on the entire control including the button.
    /// </summary>
    public sealed partial class CollapsibleSplitterHorizontal : UserControl
    {
        // ====================================================================
        // DEPENDENCY PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets or sets whether to collapse the row above (true) or below (false).
        /// </summary>
        public static readonly DependencyProperty CollapseAboveProperty =
            DependencyProperty.Register(nameof(CollapseAbove), typeof(bool), typeof(CollapsibleSplitterHorizontal),
                new PropertyMetadata(false, OnCollapseDirectionChanged));

        /// <summary>
        /// Gets or sets whether the target panel is currently collapsed.
        /// </summary>
        public static readonly DependencyProperty IsCollapsedProperty =
            DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(CollapsibleSplitterHorizontal),
                new PropertyMetadata(false, OnIsCollapsedChanged));

        /// <summary>
        /// Gets or sets the default height to restore when expanding (if no previous height saved).
        /// </summary>
        public static readonly DependencyProperty DefaultExpandedHeightProperty =
            DependencyProperty.Register(nameof(DefaultExpandedHeight), typeof(double), typeof(CollapsibleSplitterHorizontal),
                new PropertyMetadata(180.0));

        /// <summary>
        /// Gets or sets the minimum height when expanded.
        /// </summary>
        public static readonly DependencyProperty MinExpandedHeightProperty =
            DependencyProperty.Register(nameof(MinExpandedHeight), typeof(double), typeof(CollapsibleSplitterHorizontal),
                new PropertyMetadata(100.0));

        // ====================================================================
        // PROPERTIES
        // ====================================================================

        /// <summary>
        /// If true, collapses the row above this splitter.
        /// If false, collapses the row below.
        /// </summary>
        public bool CollapseAbove
        {
            get => (bool)GetValue(CollapseAboveProperty);
            set => SetValue(CollapseAboveProperty, value);
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
        /// The default height to expand to if no previous height is saved.
        /// </summary>
        public double DefaultExpandedHeight
        {
            get => (double)GetValue(DefaultExpandedHeightProperty);
            set => SetValue(DefaultExpandedHeightProperty, value);
        }

        /// <summary>
        /// The minimum height when expanded.
        /// </summary>
        public double MinExpandedHeight
        {
            get => (double)GetValue(MinExpandedHeightProperty);
            set => SetValue(MinExpandedHeightProperty, value);
        }

        // ====================================================================
        // FIELDS
        // ====================================================================

        /// <summary>
        /// Stores the height of the target row before it was collapsed.
        /// </summary>
        private double _previousHeight;

        /// <summary>
        /// The index of the target row in the parent grid.
        /// </summary>
        private int _targetRowIndex = -1;

        /// <summary>
        /// Whether we're currently dragging.
        /// </summary>
        private bool _isDragging;

        /// <summary>
        /// The starting Y position when drag began.
        /// </summary>
        private double _dragStartY;

        /// <summary>
        /// The row height when drag began.
        /// </summary>
        private double _dragStartHeight;

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

        /// <summary>
        /// Debounce timer to prevent rapid toggle operations.
        /// </summary>
        private DispatcherTimer? _debounceTimer;

        /// <summary>
        /// Whether we're currently in a debounce period (ignore clicks).
        /// </summary>
        private bool _isDebouncing;

        /// <summary>
        /// Debounce delay in milliseconds.
        /// </summary>
        private const int DebounceDelayMs = 200;

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

        public CollapsibleSplitterHorizontal()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

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
            ResolveTargetRowIndex();

            // Initialize previous height
            var row = GetTargetRow();
            if (row != null && row.ActualHeight > MinExpandedHeight)
            {
                _previousHeight = row.ActualHeight;
            }
            else
            {
                _previousHeight = DefaultExpandedHeight;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Cleanup timer
            _debounceTimer?.Stop();
            _debounceTimer = null;
        }

        /// <summary>
        /// Resolves the target row index based on CollapseAbove setting.
        /// </summary>
        private void ResolveTargetRowIndex()
        {
            var grid = FindParent<Grid>(this);
            if (grid == null) return;

            int myRow = Grid.GetRow(this);

            // Determine target row index
            _targetRowIndex = CollapseAbove ? myRow - 1 : myRow + 1;
        }

        /// <summary>
        /// Gets the target row definition (fresh lookup each time to avoid stale references).
        /// </summary>
        private RowDefinition? GetTargetRow()
        {
            var grid = FindParent<Grid>(this);
            if (grid == null) return null;

            if (_targetRowIndex < 0)
            {
                ResolveTargetRowIndex();
            }

            if (_targetRowIndex >= 0 && _targetRowIndex < grid.RowDefinitions.Count)
            {
                return grid.RowDefinitions[_targetRowIndex];
            }

            return null;
        }

        // ====================================================================
        // PROPERTY CHANGE HANDLERS
        // ====================================================================

        private static void OnCollapseDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var splitter = (CollapsibleSplitterHorizontal)d;
            splitter.UpdateIconDirection();
            splitter.ResolveTargetRowIndex();
        }

        private static void OnIsCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var splitter = (CollapsibleSplitterHorizontal)d;
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
                CollapseIcon.Icon = CollapseAbove ? Icon.ChevronDown : Icon.ChevronUp;
                ToolTipService.SetToolTip(CollapseButton, "Expand Panel");
            }
            else
            {
                // When expanded, arrow points toward the edge (to collapse)
                CollapseIcon.Icon = CollapseAbove ? Icon.ChevronUp : Icon.ChevronDown;
                ToolTipService.SetToolTip(CollapseButton, "Collapse Panel");
            }
        }

        /// <summary>
        /// Applies the collapsed or expanded state to the target row.
        /// </summary>
        private void ApplyCollapsedState()
        {
            UpdateIconDirection();

            var grid = FindParent<Grid>(this);
            var targetRow = GetTargetRow();
            if (targetRow == null || grid == null) return;

            if (IsCollapsed)
            {
                // Save current height before collapsing
                double currentHeight = targetRow.ActualHeight;
                if (currentHeight > MinExpandedHeight)
                {
                    _previousHeight = currentHeight;
                }

                // Collapse to zero - set MinHeight first to allow zero
                targetRow.MinHeight = 0;
                targetRow.Height = new GridLength(0, GridUnitType.Pixel);
            }
            else
            {
                // Restore to previous height or default
                double restoreHeight = _previousHeight > MinExpandedHeight
                    ? _previousHeight
                    : DefaultExpandedHeight;

                targetRow.MinHeight = MinExpandedHeight;
                targetRow.Height = new GridLength(restoreHeight, GridUnitType.Pixel);
            }

            // Force immediate layout update to ensure changes propagate throughout the visual tree
            // This prevents layout issues on Desktop/WSL where panels appear misaligned until interaction
            grid.InvalidateMeasure();
            grid.InvalidateArrange();
            grid.UpdateLayout();
        }

        /// <summary>
        /// Starts the debounce timer to prevent rapid toggle operations.
        /// </summary>
        private void StartDebounce()
        {
            _isDebouncing = true;

            _debounceTimer?.Stop();
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DebounceDelayMs)
            };
            _debounceTimer.Tick += (_, __) =>
            {
                _isDebouncing = false;
                _debounceTimer?.Stop();
            };
            _debounceTimer.Start();
        }

        // ====================================================================
        // DRAG RESIZING + CLICK DETECTION (pointer-based)
        // ====================================================================

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Show resize cursor when hovering (not collapsed)
            if (!IsCollapsed)
            {
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
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

            _dragStartY = e.GetCurrentPoint(grid).Position.Y;

            // Only setup drag if not collapsed
            if (!IsCollapsed)
            {
                var targetRow = GetTargetRow();
                if (targetRow != null)
                {
                    _dragStartHeight = targetRow.ActualHeight;
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

            double currentY = e.GetCurrentPoint(grid).Position.Y;
            double totalDelta = Math.Abs(currentY - _dragStartY);

            // Check if we've exceeded the drag threshold
            if (!_dragThresholdExceeded && totalDelta > DragThreshold)
            {
                _dragThresholdExceeded = true;
                _isDragging = true;
            }

            // Only resize if we're actually dragging
            if (_isDragging)
            {
                var targetRow = GetTargetRow();
                if (targetRow == null) return;

                double delta = currentY - _dragStartY;

                // For CollapseAbove=true (above panel), dragging down = larger
                // For CollapseAbove=false (below panel), dragging up = larger
                if (!CollapseAbove)
                {
                    delta = -delta;
                }

                double newHeight = Math.Max(MinExpandedHeight, _dragStartHeight + delta);

                // Clamp to reasonable maximum
                double maxHeight = grid.ActualHeight * 0.6;
                newHeight = Math.Min(newHeight, maxHeight);

                targetRow.Height = new GridLength(newHeight);
                _previousHeight = newHeight;
            }

            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_pointerPressed)
            {
                // If we didn't exceed drag threshold, treat as a click -> toggle collapse
                // But only if we're not in a debounce period (prevents double-click issues)
                if (!_dragThresholdExceeded && !_isDebouncing)
                {
                    IsCollapsed = !IsCollapsed;
                    StartDebounce();
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
