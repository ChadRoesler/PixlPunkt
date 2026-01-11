using System.Collections.Generic;
using FluentIcons.Common;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.History;
using PixlPunkt.UI.CanvasHost;
using Windows.UI.Text;

namespace PixlPunkt.UI.History
{
    public sealed partial class HistoryPanel : UserControl
    {
        private CanvasViewHost? _host;

        public UnifiedHistoryStack History
        {
            get => (UnifiedHistoryStack)GetValue(HistoryProperty);
            set => SetValue(HistoryProperty, value);
        }

        public static readonly DependencyProperty HistoryProperty =
            DependencyProperty.Register(nameof(History), typeof(UnifiedHistoryStack),
                typeof(HistoryPanel), new PropertyMetadata(null, OnHistoryChanged));

        private static void OnHistoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (HistoryPanel)d;

            if (e.OldValue is UnifiedHistoryStack oldH)
                oldH.HistoryChanged -= self.Rebuild;

            if (e.NewValue is UnifiedHistoryStack newH)
                newH.HistoryChanged += self.Rebuild;

            self.Rebuild();
        }

        private sealed class Row
        {
            public Icon Icon { get; init; } = Icon.History;
            public string Title { get; init; } = "";
            public int AppliedCount { get; init; } // 0 = Start
            public Brush Foreground { get; init; } = new SolidColorBrush();
            public FontWeight Weight { get; init; }
        }

        private bool _internalSelect;

        public HistoryPanel()
        {
            InitializeComponent();
            Loaded += (_, __) => Rebuild();
        }

        private void OnHostHistoryChanged()
        {
            _ = DispatcherQueue.TryEnqueue(Rebuild);
        }

        public void Bind(CanvasViewHost? host)
        {
            if (_host != null)
                _host.HistoryStateChanged -= OnHostHistoryChanged;

            _host = host;

            if (_host != null)
            {
                _host.HistoryStateChanged += OnHostHistoryChanged;
                History = _host.Document.History; // point at the REAL one
            }
            else
            {
                History = null!;
            }

            Rebuild();
        }

        private void Rebuild()
        {
            if (History is null)
            {
                HistoryList.ItemsSource = null;
                StatusText.Text = "";
                return;
            }

            var timeline = History.GetTimeline();
            var rows = new List<Row>(timeline.Count + 1);

            var primary = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            var secondary = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

            // Operations: 1..N
            for (int i = timeline.Count - 1; i >= 0; i--)
            {
                int applied = i + 1;
                bool isFuture = applied > History.AppliedCount;

                rows.Add(new Row
                {
                    Icon = timeline[i].HistoryIcon,
                    Title = timeline[i].Description,
                    AppliedCount = applied,
                    Foreground = isFuture ? secondary : primary,
                    Weight = applied == History.AppliedCount ? FontWeights.SemiBold : FontWeights.Normal
                });
            }

            // "Start" at bottom
            rows.Add(new Row { Title = "Start", AppliedCount = 0, Foreground = secondary, Weight = FontWeights.Normal });

            _internalSelect = true;
            HistoryList.ItemsSource = rows;
            var current = rows.Find(r => r.AppliedCount == History.AppliedCount);
            HistoryList.SelectedItem = current;
            if (current != null) HistoryList.ScrollIntoView(current);

            _internalSelect = false;

            UndoBtn.IsEnabled = History.CanUndo;
            RedoBtn.IsEnabled = History.CanRedo;

            StatusText.Text =
                (History.UndoDescription is not null ? $"Undo: {History.UndoDescription}" : "Undo: -")
                + "   |   " +
                (History.RedoDescription is not null ? $"Redo: {History.RedoDescription}" : "Redo: -");
        }

        private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_internalSelect) return;
            if (_host is null) return;
            if (HistoryList.SelectedItem is not Row row) return;

            _host.JumpHistoryTo(row.AppliedCount);
        }

        private void UndoBtn_Click(object sender, RoutedEventArgs e) => History?.Undo();
        private void RedoBtn_Click(object sender, RoutedEventArgs e) => History?.Redo();

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            // You can decide your preferred behavior here.
            // I usually only clear on "New/Open" not as a user action.
            History?.Clear(resetSaveState: false);
        }
    }
}
