using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace PixlPunkt.Uno.UI
{
    public sealed partial class PixlPunktMainWindow : Window
    {
        private Button? _tabAddButton;
        private bool _measuringGoodTop;

        private void FixTabViewAddButtonAlignment(TabView tabView)
        {
            tabView.Loaded += (_, __) =>
            {
                tabView.ApplyTemplate();

                // Template part name is typically "AddButton" (sometimes "AddTabButton" depending on WinUI version)
                _tabAddButton =
                    FindDescendantByName<Button>(tabView, "AddButton") ??
                    FindDescendantByName<Button>(tabView, "AddTabButton");

                if (_tabAddButton == null)
                    return;

                // Ensure we have a transform we can nudge
                if (_tabAddButton.RenderTransform is not TranslateTransform)
                    _tabAddButton.RenderTransform = new TranslateTransform();

                // Re-evaluate whenever tabs change / layout changes
                // Re-evaluate whenever tabs change / layout changes
                if (tabView.TabItems is IObservableVector<object> ov)
                {
                    ov.VectorChanged += (_, __) => UpdateAddButtonOffset(tabView);
                }
                else if (tabView.TabItems is INotifyCollectionChanged ncc)
                {
                    ncc.CollectionChanged += (_, __) => UpdateAddButtonOffset(tabView);
                }

                tabView.SizeChanged += (_, __) => UpdateAddButtonOffset(tabView);

                UpdateAddButtonOffset(tabView);
            };
        }

        private void UpdateAddButtonOffset(TabView tabView)
        {
            if (_tabAddButton == null)
                return;

            if (_tabAddButton.RenderTransform is not TranslateTransform tt)
                _tabAddButton.RenderTransform = tt = new TranslateTransform();

            // ✅ RULE: When there are tabs, do NOT modify the button. Let TabView handle it.
            if (tabView.TabItems.Count > 0)
            {
                tt.Y = 0;
                return;
            }

            // ✅ Only when EMPTY: apply an offset to match the learned "good" top
            tabView.DispatcherQueue.TryEnqueue(() =>
            {
                if (_tabAddButton == null) return;

                var t = (TranslateTransform)_tabAddButton.RenderTransform;

                // Measure natural position with transform disabled
                t.Y = 0;
                var naturalTop =
                    _tabAddButton.TransformToVisual(tabView)
                        .TransformPoint(new Point(0, 0)).Y;

                // If we've never had a "good" top yet (app starts empty), use a small fallback
                var targetTop = naturalTop == 0 ? naturalTop + 13 : naturalTop;

                t.Y = targetTop;
            });
        }

        private static T? FindDescendantByName<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T fe && fe.Name == name)
                    return fe;

                var found = FindDescendantByName<T>(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
