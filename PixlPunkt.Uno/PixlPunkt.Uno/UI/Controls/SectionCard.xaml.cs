using System;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PixlPunkt.Uno.UI.Controls
{
    /// <summary>
    /// A titled container panel with optional actions and custom control area.
    /// Supports a minimized state and undocking to a separate window.
    /// </summary>
    public sealed partial class SectionCard : UserControl
    {
        // ────────────────────────────────────────────────────────────────────
        // DEPENDENCY PROPERTIES
        // ────────────────────────────────────────────────────────────────────

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(SectionCard),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty BodyProperty =
            DependencyProperty.Register(nameof(Body), typeof(object), typeof(SectionCard),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ActionsProperty =
            DependencyProperty.Register(nameof(Actions), typeof(object), typeof(SectionCard),
                new PropertyMetadata(null));

        public static readonly DependencyProperty CustomControlProperty =
            DependencyProperty.Register(nameof(CustomControl), typeof(UIElement),
                typeof(SectionCard), new PropertyMetadata(null));

        public static readonly DependencyProperty IsMinimizedProperty =
            DependencyProperty.Register(nameof(IsMinimized), typeof(bool), typeof(SectionCard),
                new PropertyMetadata(false, OnIsMinimizedChanged));

        public static readonly DependencyProperty CanUndockProperty =
            DependencyProperty.Register(nameof(CanUndock), typeof(bool), typeof(SectionCard),
                new PropertyMetadata(false));

        public static readonly DependencyProperty PanelIdProperty =
            DependencyProperty.Register(nameof(PanelId), typeof(string), typeof(SectionCard),
                new PropertyMetadata(string.Empty));

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
            card.UpdateMinimizedState();
        }

        // ────────────────────────────────────────────────────────────────────
        // EVENTS
        // ────────────────────────────────────────────────────────────────────

        public event Action<SectionCard>? UndockRequested;
        public event Action<SectionCard>? DockRequested;
        
        /// <summary>
        /// Raised when the minimized state changes, allowing parent containers to adjust layout.
        /// </summary>
        public event Action<SectionCard, bool>? MinimizedChanged;

        // ────────────────────────────────────────────────────────────────────
        // PROPERTIES
        // ────────────────────────────────────────────────────────────────────

        public string DisplayIcon
        {
            set => HeaderIcon.Icon = (Icon)Enum.Parse(typeof(Icon), value);
        }

        public Icon HeaderIconValue => HeaderIcon.Icon;

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public object Body
        {
            get => GetValue(BodyProperty);
            set => SetValue(BodyProperty, value);
        }

        public object Actions
        {
            get => GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }

        public UIElement? CustomControl
        {
            get => (UIElement?)GetValue(CustomControlProperty);
            set => SetValue(CustomControlProperty, value);
        }

        public bool IsMinimized
        {
            get => (bool)GetValue(IsMinimizedProperty);
            set => SetValue(IsMinimizedProperty, value);
        }

        public bool CanUndock
        {
            get => (bool)GetValue(CanUndockProperty);
            set => SetValue(CanUndockProperty, value);
        }

        public string PanelId
        {
            get => (string)GetValue(PanelIdProperty);
            set => SetValue(PanelIdProperty, value);
        }

        public bool IsFloating
        {
            get => (bool)GetValue(IsFloatingProperty);
            set => SetValue(IsFloatingProperty, value);
        }

        // ────────────────────────────────────────────────────────────────────
        // CTOR
        // ────────────────────────────────────────────────────────────────────

        public SectionCard()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateToggleIcon();
            UpdateMinimizedState();
        }

        // ────────────────────────────────────────────────────────────────────
        // UI EVENTS
        // ────────────────────────────────────────────────────────────────────

        private void MinToggle_Click(object sender, RoutedEventArgs e)
        {
            IsMinimized = !IsMinimized;
        }

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

        private void UpdateFloatingState()
        {
            MinToggle.Visibility = IsFloating ? Visibility.Collapsed : Visibility.Visible;

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

        private void UpdateMinimizedState()
        {
            UpdateToggleIcon();
            
            // Set vertical alignment based on minimized state
            // When minimized, align to top so it only takes header height
            // When expanded, stretch to fill available space
            VerticalAlignment = IsMinimized ? VerticalAlignment.Top : VerticalAlignment.Stretch;
            
            // Notify parent containers of the state change
            MinimizedChanged?.Invoke(this, IsMinimized);
        }

        private void UpdateToggleIcon()
        {
            if (MinToggleIcon is null) return;
            MinToggleIcon.Icon = IsMinimized ? Icon.ArrowMaximizeVertical : Icon.ArrowMinimizeVertical;
        }
    }
}
