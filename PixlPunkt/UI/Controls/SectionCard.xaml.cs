using System;
using System.Linq;
using CommunityToolkit.WinUI.Controls;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PixlPunkt.UI.Controls
{
    /// <summary>
    /// A titled container panel with optional actions and custom control area.
    /// Supports a minimized state and cooperates with a parent Grid to resize rows.
    /// Also supports undocking to a separate window.
    /// </summary>
    public sealed partial class SectionCard : UserControl
    {
        // ────────────────────────────────────────────────────────────────────
        // DEPENDENCY PROPERTIES
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the title text displayed in the card header.
        /// </summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(SectionCard),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Gets or sets the content object shown in the body area of the card.
        /// </summary>
        public static readonly DependencyProperty BodyProperty =
            DependencyProperty.Register(nameof(Body), typeof(object), typeof(SectionCard),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the actions element placed in the header's actions slot.
        /// </summary>
        public static readonly DependencyProperty ActionsProperty =
            DependencyProperty.Register(nameof(Actions), typeof(object), typeof(SectionCard),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets an optional custom control displayed in the header.
        /// </summary>
        public static readonly DependencyProperty CustomControlProperty =
            DependencyProperty.Register(nameof(CustomControl), typeof(UIElement),
                typeof(SectionCard), new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets whether the card is minimized. When minimized,
        /// the card's Grid row is set to Auto; otherwise it uses Star sizing.
        /// </summary>
        public static readonly DependencyProperty IsMinimizedProperty =
            DependencyProperty.Register(nameof(IsMinimized), typeof(bool), typeof(SectionCard),
                new PropertyMetadata(false, OnIsMinimizedChanged));

        /// <summary>
        /// Gets or sets whether this card can be undocked to a separate window.
        /// </summary>
        public static readonly DependencyProperty CanUndockProperty =
            DependencyProperty.Register(nameof(CanUndock), typeof(bool), typeof(SectionCard),
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets the unique panel identifier used for docking/undocking.
        /// </summary>
        public static readonly DependencyProperty PanelIdProperty =
            DependencyProperty.Register(nameof(PanelId), typeof(string), typeof(SectionCard),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Gets or sets whether the card is currently in a floating (undocked) window.
        /// </summary>
        public static readonly DependencyProperty IsFloatingProperty =
            DependencyProperty.Register(nameof(IsFloating), typeof(bool), typeof(SectionCard),
                new PropertyMetadata(false, OnIsFloatingChanged));

        private static void OnIsFloatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var card = (SectionCard)d;
            card.UpdateFloatingState();
        }

        private static void OnIsMinimizedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var card = (SectionCard)d;
            card.UpdateToggleIcon();
            card.ApplyRowSizing();
        }

        // ────────────────────────────────────────────────────────────────────
        // EVENTS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Occurs when the user clicks the undock button.
        /// </summary>
        public event Action<SectionCard>? UndockRequested;

        /// <summary>
        /// Occurs when the user clicks the dock button (when floating).
        /// </summary>
        public event Action<SectionCard>? DockRequested;

        // ────────────────────────────────────────────────────────────────────
        // PROPERTIES
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the header icon by its <see cref="FluentIcons.Common.Icon"/> name.
        /// </summary>
        public string DisplayIcon
        {
            set => HeaderIcon.Icon = (Icon)Enum.Parse(typeof(Icon), value);
        }

        /// <summary>
        /// Gets the current header icon.
        /// </summary>
        public Icon HeaderIconValue => HeaderIcon.Icon;

        /// <inheritdoc cref="TitleProperty"/>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <inheritdoc cref="BodyProperty"/>
        public object Body
        {
            get => GetValue(BodyProperty);
            set => SetValue(BodyProperty, value);
        }

        /// <inheritdoc cref="ActionsProperty"/>
        public object Actions
        {
            get => GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }

        /// <inheritdoc cref="CustomControlProperty"/>
        public UIElement? CustomControl
        {
            get => (UIElement?)GetValue(CustomControlProperty);
            set => SetValue(CustomControlProperty, value);
        }

        /// <inheritdoc cref="IsMinimizedProperty"/>
        public bool IsMinimized
        {
            get => (bool)GetValue(IsMinimizedProperty);
            set => SetValue(IsMinimizedProperty, value);
        }

        /// <inheritdoc cref="CanUndockProperty"/>
        public bool CanUndock
        {
            get => (bool)GetValue(CanUndockProperty);
            set => SetValue(CanUndockProperty, value);
        }

        /// <inheritdoc cref="PanelIdProperty"/>
        public string PanelId
        {
            get => (string)GetValue(PanelIdProperty);
            set => SetValue(PanelIdProperty, value);
        }

        /// <inheritdoc cref="IsFloatingProperty"/>
        public bool IsFloating
        {
            get => (bool)GetValue(IsFloatingProperty);
            set => SetValue(IsFloatingProperty, value);
        }

        // ────────────────────────────────────────────────────────────────────
        // CTOR
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new instance of <see cref="SectionCard"/>.
        /// </summary>
        public SectionCard()
        {
            InitializeComponent();

            // Apply initial row sizing and toggle icon when loaded into visual tree
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Apply the initial state now that we're in the visual tree
            UpdateToggleIcon();
            ApplyRowSizing();
        }

        // ────────────────────────────────────────────────────────────────────
        // UI EVENTS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Toggles minimized/expanded state from the header button.
        /// </summary>
        private void MinToggle_Click(object sender, RoutedEventArgs e)
        {
            IsMinimized = !IsMinimized;
        }

        /// <summary>
        /// Handles the undock/dock button click.
        /// </summary>
        private void UndockButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsFloating)
            {
                DockRequested?.Invoke(this);
            }
            else
            {
                UndockRequested?.Invoke(this);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // LAYOUT HELPERS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates the UI to reflect the floating (undocked) state.
        /// </summary>
        private void UpdateFloatingState()
        {
            // Hide collapse/expand button when floating
            MinToggle.Visibility = IsFloating ? Visibility.Collapsed : Visibility.Visible;

            // Update undock button icon and tooltip
            if (UndockButtonIcon != null)
            {
                UndockButtonIcon.Icon = IsFloating ? Icon.PanelRight : Icon.Open;
            }

            if (UndockButton != null)
            {
                ToolTipService.SetToolTip(UndockButton,
                    IsFloating ? "Dock back to main window" : "Undock to separate window");
            }
        }

        /// <summary>
        /// Updates the header toggle glyph to reflect the current minimized state.
        /// </summary>
        private void UpdateToggleIcon()
        {
            if (MinToggleIcon is null) return;
            MinToggleIcon.Icon = IsMinimized ? Icon.ArrowMaximizeVertical : Icon.ArrowMinimizeVertical;
        }

        /// <summary>
        /// Applies Grid row sizing rules:
        /// - This card's row uses Auto when minimized, or Star when expanded.
        /// - Other SectionCard rows use Star unless they are minimized themselves.
        /// - Rows containing a GridSplitter are ignored.
        /// </summary>
        private void ApplyRowSizing()
        {
            // Find the parent Grid
            var grid = FindParent<Grid>(this);
            if (grid is null) return;

            var rowIndex = Grid.GetRow(this);
            if ((uint)rowIndex >= (uint)grid.RowDefinitions.Count) return;

            // Set current row height based on minimized state
            grid.RowDefinitions[rowIndex].Height = IsMinimized
                ? GridLength.Auto
                : new GridLength(1, GridUnitType.Star);

            // Build set of splitter rows once
            var splitterRows = grid.Children
                .OfType<FrameworkElement>()
                .Where(fe => fe is GridSplitter)
                .Select(Grid.GetRow)
                .ToHashSet();

            // Ensure other SectionCard rows are Star unless they're minimized or splitters
            foreach (var other in grid.Children.OfType<SectionCard>())
            {
                var r = Grid.GetRow(other);
                if (r == rowIndex || splitterRows.Contains(r)) continue;

                grid.RowDefinitions[r].Height = other.IsMinimized
                    ? GridLength.Auto
                    : new GridLength(1, GridUnitType.Star);
            }
        }

        /// <summary>
        /// Walks up the visual tree and returns the first parent of the requested type, if any.
        /// </summary>
        private static T? FindParent<T>(DependencyObject start) where T : DependencyObject
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
