using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace PixlPunkt.UI.Controls
{
    /// <summary>
    /// A panel that positions child elements sequentially from left to right, breaking content to the next line at the edge of the containing box.
    /// Subsequent ordering happens sequentially from top to bottom or from right to left, depending on the value of the Orientation property.
    /// This is a simplified implementation compatible with Uno Platform Skia.
    /// </summary>
    public class WrapPanel : Panel
    {
        /// <summary>
        /// Identifies the Orientation dependency property.
        /// </summary>
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(
                nameof(Orientation),
                typeof(Orientation),
                typeof(WrapPanel),
                new PropertyMetadata(Orientation.Horizontal, OnOrientationChanged));

        /// <summary>
        /// Gets or sets the orientation of the WrapPanel.
        /// </summary>
        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((WrapPanel)d).InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var curLineSize = new UvSize(Orientation);
            var panelSize = new UvSize(Orientation);
            var uvConstraint = new UvSize(Orientation, availableSize.Width, availableSize.Height);
            double itemWidth = double.PositiveInfinity;
            double itemHeight = double.PositiveInfinity;

            var children = Children;

            for (int i = 0, count = children.Count; i < count; i++)
            {
                var child = children[i] as UIElement;
                if (child == null) continue;

                // Measure child with infinite space to get its desired size
                child.Measure(availableSize);

                var sz = new UvSize(Orientation, child.DesiredSize.Width, child.DesiredSize.Height);

                if (curLineSize.U + sz.U > uvConstraint.U)
                {
                    // Need to switch to another line
                    panelSize.U = Math.Max(curLineSize.U, panelSize.U);
                    panelSize.V += curLineSize.V;
                    curLineSize = sz;

                    if (sz.U > uvConstraint.U)
                    {
                        // Element is wider than constraint - give it its own line
                        panelSize.U = Math.Max(sz.U, panelSize.U);
                        panelSize.V += sz.V;
                        curLineSize = new UvSize(Orientation);
                    }
                }
                else
                {
                    // Continue adding to current line
                    curLineSize.U += sz.U;
                    curLineSize.V = Math.Max(sz.V, curLineSize.V);
                }
            }

            // Account for the last line
            panelSize.U = Math.Max(curLineSize.U, panelSize.U);
            panelSize.V += curLineSize.V;

            return new Size(panelSize.Width, panelSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int firstInLine = 0;
            double itemWidth = double.PositiveInfinity;
            double itemHeight = double.PositiveInfinity;
            double accumulatedV = 0;
            var curLineSize = new UvSize(Orientation);
            var uvFinalSize = new UvSize(Orientation, finalSize.Width, finalSize.Height);
            var children = Children;

            for (int i = 0, count = children.Count; i < count; i++)
            {
                var child = children[i] as UIElement;
                if (child == null) continue;

                var sz = new UvSize(Orientation, child.DesiredSize.Width, child.DesiredSize.Height);

                if (curLineSize.U + sz.U > uvFinalSize.U)
                {
                    // Need to switch to another line
                    ArrangeLine(accumulatedV, curLineSize.V, firstInLine, i);

                    accumulatedV += curLineSize.V;
                    curLineSize = sz;

                    if (sz.U > uvFinalSize.U)
                    {
                        // Element is wider than available space - give it its own line
                        ArrangeLine(accumulatedV, sz.V, i, ++i);
                        accumulatedV += sz.V;
                        curLineSize = new UvSize(Orientation);
                    }

                    firstInLine = i;
                }
                else
                {
                    // Continue adding to current line
                    curLineSize.U += sz.U;
                    curLineSize.V = Math.Max(sz.V, curLineSize.V);
                }
            }

            // Arrange the last line
            if (firstInLine < children.Count)
            {
                ArrangeLine(accumulatedV, curLineSize.V, firstInLine, children.Count);
            }

            return finalSize;
        }

        private void ArrangeLine(double v, double lineV, int start, int end)
        {
            double u = 0;
            bool isHorizontal = Orientation == Orientation.Horizontal;
            var children = Children;

            for (int i = start; i < end; i++)
            {
                var child = children[i] as UIElement;
                if (child == null) continue;

                var childSize = new UvSize(Orientation, child.DesiredSize.Width, child.DesiredSize.Height);
                var layoutSlotU = childSize.U;
                var layoutSlotV = lineV;

                if (isHorizontal)
                {
                    child.Arrange(new Rect(u, v, layoutSlotU, layoutSlotV));
                }
                else
                {
                    child.Arrange(new Rect(v, u, layoutSlotV, layoutSlotU));
                }

                u += layoutSlotU;
            }
        }

        /// <summary>
        /// Helper struct for orientation-independent size calculations.
        /// U = primary axis (width for horizontal, height for vertical)
        /// V = secondary axis (height for horizontal, width for vertical)
        /// </summary>
        private struct UvSize
        {
            internal UvSize(Orientation orientation, double width, double height)
            {
                U = V = 0d;
                _orientation = orientation;
                Width = width;
                Height = height;
            }

            internal UvSize(Orientation orientation)
            {
                U = V = 0d;
                _orientation = orientation;
            }

            internal double U;
            internal double V;
            private readonly Orientation _orientation;

            internal double Width
            {
                get { return (_orientation == Orientation.Horizontal ? U : V); }
                private set { if (_orientation == Orientation.Horizontal) U = value; else V = value; }
            }

            internal double Height
            {
                get { return (_orientation == Orientation.Horizontal ? V : U); }
                private set { if (_orientation == Orientation.Horizontal) V = value; else U = value; }
            }
        }
    }
}
