using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Coloring;
using PixlPunkt.Core.Coloring.Helpers;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.ColorPick.Controls
{
    /// <summary>
    /// Custom control that renders a horizontal row of color swatches using Win2D.
    /// Provides visual indicators for center and match states, with click interaction support.
    /// </summary>
    public sealed partial class LadderRow : UserControl
    {
        // ════════════════════════════════════════════════════════════════════
        // CONSTANTS
        // ════════════════════════════════════════════════════════════════════

        private const float TILE_W = 34f;
        private const float TILE_H = 28f;
        private const float GAP = 2f;
        private const float RADIUS = 5f;
        private const float BORDER = 1f;

        // ════════════════════════════════════════════════════════════════════
        // DEPENDENCY PROPERTIES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets or sets the collection of swatches to display.
        /// </summary>
        public IList<Swatch>? ItemsSource
        {
            get => (IList<Swatch>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IList<Swatch>), typeof(LadderRow),
            new PropertyMetadata(null, OnItemsChanged));

        // ════════════════════════════════════════════════════════════════════
        // EVENTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired when a swatch is clicked, providing the BGRA color value.
        /// </summary>
        public event EventHandler<uint>? SwatchClicked;

        // ════════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initializes a new ladder row control.
        /// </summary>
        public LadderRow()
        {
            InitializeComponent();
            SizeChanged += (_, __) => Canvas.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Forces a redraw of the canvas.
        /// </summary>
        public void Invalidate() => Canvas.Invalidate();

        // ════════════════════════════════════════════════════════════════════
        // DEPENDENCY PROPERTY CALLBACKS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles changes to the ItemsSource property.
        /// Auto-sizes the control width based on the number of swatches.
        /// </summary>
        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (LadderRow)d;
            var items = self.ItemsSource;

            // Auto-size width so it lays out cleanly in the column
            var count = (items?.Count ?? 0);
            self.Width = count > 0 ? count * (TILE_W + GAP) - GAP : 0;
            self.Canvas.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles canvas draw operations to render all swatches.
        /// </summary>
        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            var items = ItemsSource;
            if (items is null || items.Count == 0) return;

            // Theme colors
            var stroke = GetThemeColor("CardStrokeColorDefaultBrush", Color.FromArgb(255, 80, 80, 80));
            var accent = GetThemeColor("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 215));
            var centerHint = Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF);

            float x = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                var s = items[i];
                var fill = ColorUtil.ToColor(s.Color);

                var rect = new Rect(x + BORDER, BORDER, TILE_W - 2 * BORDER, TILE_H - 2 * BORDER);

                // Fill + base border
                ds.FillRoundedRectangle(rect, RADIUS, RADIUS, fill);
                ds.DrawRoundedRectangle(rect, RADIUS, RADIUS, stroke, 1f);

                // Center hint
                if (s.IsCenter)
                    ds.DrawRoundedRectangle(rect, RADIUS, RADIUS, centerHint, 1f);

                // Match highlight
                if (s.IsMatch)
                    ds.DrawRoundedRectangle(rect, RADIUS, RADIUS, accent, 2f);

                x += TILE_W + GAP;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // INPUT HANDLING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles pointer press to detect swatch clicks and fire the SwatchClicked event.
        /// </summary>
        private void Canvas_PointerPressed(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var items = ItemsSource;
            if (items is null || items.Count == 0) return;

            var pt = e.GetCurrentPoint(Canvas).Position;
            int idx = (int)Math.Floor(pt.X / (TILE_W + GAP));

            if (idx >= 0 && idx < items.Count)
                SwatchClicked?.Invoke(this, items[idx].Color);
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Retrieves a theme color from application resources with fallback.
        /// </summary>
        /// <param name="key">Resource key for the theme color.</param>
        /// <param name="fallback">Default color if resource not found.</param>
        /// <returns>The theme color or fallback.</returns>
        private static Color GetThemeColor(string key, Color fallback)
        {
            if (Application.Current?.Resources.TryGetValue(key, out var o) == true &&
                o is SolidColorBrush scb)
                return scb.Color;

            return fallback;
        }
    }
}