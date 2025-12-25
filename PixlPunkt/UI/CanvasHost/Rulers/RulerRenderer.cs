using System;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.CanvasHost.Rulers
{

    /// <summary>
    /// Renders rulers with tile-based tick marks.
    /// </summary>
    /// <remarks>
    /// Tick mark system based on tile size:
    /// - Large mark: Every tile (tileSize pixels)
    /// - Medium mark: Every tile/2 (rounded: >= 0.5 rounds up)
    /// - Small mark: Every tile/4 (rounded: >= 0.5 rounds up)
    /// 
    /// Example for 16x16 tile: Large at 16, Medium at 8, Small at 4, 12
    /// Example for 15x15 tile: Large at 15, Medium at 8, Small at 4, 12
    /// </remarks>
    public sealed class RulerRenderer
    {
        // Ruler dimensions
        public const float RULER_THICKNESS = 20f;
        public const float CORNER_SIZE = 20f;

        // Tick mark heights (from ruler edge)
        private const float LARGE_TICK_HEIGHT = 14f;
        private const float MEDIUM_TICK_HEIGHT = 9f;
        private const float SMALL_TICK_HEIGHT = 5f;



        // Colors
        private static Color RulerBackground = Color.FromArgb(255, 40, 40, 40);
        private static Color RulerBorder = Color.FromArgb(255, 60, 60, 60);
        private static Color TickColor = Color.FromArgb(255, 150, 150, 150);
        private static Color LabelColor = Color.FromArgb(255, 180, 180, 180);
        private static Color CursorHighlight = Color.FromArgb(100, 100, 180, 255);

        private static void SetRuleTheme(ElementTheme theme)
        {
            if (theme == ElementTheme.Dark)
            {
                RulerBackground = Color.FromArgb(255, 40, 40, 40);
                RulerBorder = Color.FromArgb(255, 60, 60, 60);
                TickColor = Color.FromArgb(255, 150, 150, 150);
                LabelColor = Color.FromArgb(255, 180, 180, 180);
                CursorHighlight = Color.FromArgb(100, 100, 180, 255);
            }
            else
            {
                RulerBackground = Color.FromArgb(255, 245, 245, 245);
                RulerBorder = Color.FromArgb(255, 210, 210, 210);
                TickColor = Color.FromArgb(255, 120, 120, 120);
                LabelColor = Color.FromArgb(255, 60, 60, 60);
                CursorHighlight = Color.FromArgb(100, 0, 120, 215);
            }
        }


        /// <summary>
        /// Calculates the rounded interval based on tile size and divisor.
        /// Uses standard rounding: >= 0.5 rounds up, else rounds down.
        /// </summary>
        private static int RoundedInterval(int tileSize, int divisor)
        {
            double result = (double)tileSize / divisor;
            return (int)Math.Round(result, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Draws the horizontal ruler (along the top).
        /// </summary>
        /// <param name="ds">Drawing session.</param>
        /// <param name="canvasDest">The document rectangle in the MAIN canvas coordinates (not ruler coordinates).</param>
        /// <param name="scale">Current zoom scale.</param>
        /// <param name="docWidth">Document width in pixels.</param>
        /// <param name="tileWidth">Tile width for tick marks.</param>
        /// <param name="rulerLeft">Left edge of ruler drawing area.</param>
        /// <param name="rulerRight">Right edge of ruler drawing area.</param>
        /// <param name="cursorDocX">Current cursor document X position (for highlight).</param>
        /// /// <param name="theme">Current theme.</param>
        public static void DrawHorizontalRuler(
            CanvasDrawingSession ds,
            Rect canvasDest,
            double scale,
            int docWidth,
            int tileWidth,
            float rulerLeft,
            float rulerRight,
            int? cursorDocX,
            ElementTheme theme)
        {
            // Ruler background
            SetRuleTheme(theme);
            var rulerRect = new Rect(rulerLeft, 0, rulerRight - rulerLeft, RULER_THICKNESS);
            ds.FillRectangle(rulerRect, RulerBackground);
            ds.DrawLine(rulerLeft, RULER_THICKNESS, rulerRight, RULER_THICKNESS, RulerBorder, 1f);

            // Calculate tick intervals
            int largeInterval = tileWidth;
            int mediumInterval = RoundedInterval(tileWidth, 2);
            int smallInterval = RoundedInterval(tileWidth, 4);

            // Ensure minimum intervals
            if (mediumInterval < 1) mediumInterval = 1;
            if (smallInterval < 1) smallInterval = 1;

            // The horizontal ruler is positioned in the same column as the main canvas.
            // canvasDest.X is where doc X=0 appears in the main canvas coordinate system.
            // Since the ruler is in the same column, the same X offset applies.
            // We just need to convert from main canvas coordinates.
            float docOriginScreenX = (float)canvasDest.X;

            // Draw cursor highlight
            if (cursorDocX.HasValue)
            {
                float cursorScreenX = docOriginScreenX + (float)(cursorDocX.Value * scale);
                if (cursorScreenX >= rulerLeft && cursorScreenX <= rulerRight)
                {
                    ds.FillRectangle(cursorScreenX - 1, 0, 3, RULER_THICKNESS, CursorHighlight);
                }
            }

            // Determine which tick marks to draw based on zoom level
            float pixelsPerDocPixel = (float)scale;
            bool drawSmall = pixelsPerDocPixel >= 2f;
            bool drawMedium = pixelsPerDocPixel >= 1f;
            bool drawLabels = pixelsPerDocPixel >= 4f;

            // Draw tick marks
            for (int docX = 0; docX <= docWidth; docX++)
            {
                float screenX = docOriginScreenX + (float)(docX * scale);
                if (screenX < rulerLeft - 1 || screenX > rulerRight + 1)
                    continue;

                TickType tickType = GetTickType(docX, largeInterval, mediumInterval, smallInterval);

                if (tickType == TickType.None)
                    continue;

                if (tickType == TickType.Small && !drawSmall)
                    continue;

                if (tickType == TickType.Medium && !drawMedium)
                    continue;

                float tickHeight = tickType switch
                {
                    TickType.Large => LARGE_TICK_HEIGHT,
                    TickType.Medium => MEDIUM_TICK_HEIGHT,
                    TickType.Small => SMALL_TICK_HEIGHT,
                    _ => 0
                };

                ds.DrawLine(screenX, RULER_THICKNESS - tickHeight, screenX, RULER_THICKNESS, TickColor, 1f);

                // Draw label for large ticks
                if (tickType == TickType.Large && drawLabels && docX > 0)
                {
                    var text = docX.ToString();
                    ds.DrawText(text, screenX + 2, 2, LabelColor,
                        new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
                        {
                            FontSize = 9,
                            FontFamily = "Segoe UI"
                        });
                }
            }
        }

        /// <summary>
        /// Draws the vertical ruler (along the left).
        /// </summary>
        /// <param name="ds">Drawing session.</param>
        /// <param name="canvasDest">The document rectangle in the MAIN canvas coordinates (not ruler coordinates).</param>
        /// <param name="scale">Current zoom scale.</param>
        /// <param name="docHeight">Document height in pixels.</param>
        /// <param name="tileHeight">Tile height for tick marks.</param>
        /// <param name="rulerTop">Top edge of ruler drawing area.</param>
        /// <param name="rulerBottom">Bottom edge of ruler drawing area.</param>
        /// <param name="cursorDocY">Current cursor document Y position (for highlight).</param>
        /// <param name="theme">Current theme.</param>
        public static void DrawVerticalRuler(
            CanvasDrawingSession ds,
            Rect canvasDest,
            double scale,
            int docHeight,
            int tileHeight,
            float rulerTop,
            float rulerBottom,
            int? cursorDocY,
            ElementTheme theme)
        {
            // Ruler background
            SetRuleTheme(theme);
            var rulerRect = new Rect(0, rulerTop, RULER_THICKNESS, rulerBottom - rulerTop);
            ds.FillRectangle(rulerRect, RulerBackground);
            ds.DrawLine(RULER_THICKNESS, rulerTop, RULER_THICKNESS, rulerBottom, RulerBorder, 1f);

            // Calculate tick intervals
            int largeInterval = tileHeight;
            int mediumInterval = RoundedInterval(tileHeight, 2);
            int smallInterval = RoundedInterval(tileHeight, 4);

            // Ensure minimum intervals
            if (mediumInterval < 1) mediumInterval = 1;
            if (smallInterval < 1) smallInterval = 1;

            // The vertical ruler is positioned in the same row as the main canvas.
            // canvasDest.Y is where doc Y=0 appears in the main canvas coordinate system.
            // Since the ruler is in the same row, the same Y offset applies.
            float docOriginScreenY = (float)canvasDest.Y;

            // Draw cursor highlight
            if (cursorDocY.HasValue)
            {
                float cursorScreenY = docOriginScreenY + (float)(cursorDocY.Value * scale);
                if (cursorScreenY >= rulerTop && cursorScreenY <= rulerBottom)
                {
                    ds.FillRectangle(0, cursorScreenY - 1, RULER_THICKNESS, 3, CursorHighlight);
                }
            }

            // Determine which tick marks to draw based on zoom level
            float pixelsPerDocPixel = (float)scale;
            bool drawSmall = pixelsPerDocPixel >= 2f;
            bool drawMedium = pixelsPerDocPixel >= 1f;
            bool drawLabels = pixelsPerDocPixel >= 4f;

            // Draw tick marks
            for (int docY = 0; docY <= docHeight; docY++)
            {
                float screenY = docOriginScreenY + (float)(docY * scale);
                if (screenY < rulerTop - 1 || screenY > rulerBottom + 1)
                    continue;

                TickType tickType = GetTickType(docY, largeInterval, mediumInterval, smallInterval);

                if (tickType == TickType.None)
                    continue;

                if (tickType == TickType.Small && !drawSmall)
                    continue;

                if (tickType == TickType.Medium && !drawMedium)
                    continue;

                float tickHeight = tickType switch
                {
                    TickType.Large => LARGE_TICK_HEIGHT,
                    TickType.Medium => MEDIUM_TICK_HEIGHT,
                    TickType.Small => SMALL_TICK_HEIGHT,
                    _ => 0
                };

                ds.DrawLine(RULER_THICKNESS - tickHeight, screenY, RULER_THICKNESS, screenY, TickColor, 1f);

                // Draw label for large ticks (rotated text simulation - just offset)
                if (tickType == TickType.Large && drawLabels && docY > 0)
                {
                    var text = docY.ToString();
                    // Draw vertically - each character on its own line
                    float charY = screenY + 2;
                    foreach (char c in text)
                    {
                        ds.DrawText(c.ToString(), 3, charY, LabelColor,
                            new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
                            {
                                FontSize = 8,
                                FontFamily = "Segoe UI"
                            });
                        charY += 8;
                    }
                }
            }
        }

        /// <summary>
        /// Draws the corner square where rulers meet.
        /// </summary>
        public static void DrawCorner(CanvasDrawingSession ds)
        {
            var cornerRect = new Rect(0, 0, CORNER_SIZE, CORNER_SIZE);
            ds.FillRectangle(cornerRect, RulerBackground);
            ds.DrawLine(CORNER_SIZE, 0, CORNER_SIZE, CORNER_SIZE, RulerBorder, 1f);
            ds.DrawLine(0, CORNER_SIZE, CORNER_SIZE, CORNER_SIZE, RulerBorder, 1f);
        }

        /// <summary>
        /// Determines the tick type for a given position.
        /// Priority: Large > Medium > Small > None
        /// </summary>
        private static TickType GetTickType(int position, int largeInterval, int mediumInterval, int smallInterval)
        {
            // Position 0 is always a large tick (origin)
            if (position == 0)
                return TickType.Large;

            // Check large (tile boundary)
            if (largeInterval > 0 && position % largeInterval == 0)
                return TickType.Large;

            // Check medium (tile/2)
            if (mediumInterval > 0 && position % mediumInterval == 0)
                return TickType.Medium;

            // Check small (tile/4)
            if (smallInterval > 0 && position % smallInterval == 0)
                return TickType.Small;

            return TickType.None;
        }

        private enum TickType
        {
            None,
            Small,
            Medium,
            Large
        }
    }
}
