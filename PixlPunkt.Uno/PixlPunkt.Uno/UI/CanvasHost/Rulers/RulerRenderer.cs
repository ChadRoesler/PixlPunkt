using System;
using Microsoft.UI.Xaml;
using SkiaSharp;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.Uno.UI.CanvasHost.Rulers
{
    /// <summary>
    /// Renders rulers with tile-based tick marks using SkiaSharp.
    /// </summary>
    /// <remarks>
    /// Tick mark system based on tile size:
    /// - Large mark: Every tile (tileSize pixels)
    /// - Medium mark: Every tile/2 (rounded: >= 0.5 rounds up)
    /// - Small mark: Every tile/4 (rounded: >= 0.5 rounds up)
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
        /// </summary>
        private static int RoundedInterval(int tileSize, int divisor)
        {
            double result = (double)tileSize / divisor;
            return (int)Math.Round(result, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Draws the horizontal ruler (along the top).
        /// </summary>
        public static void DrawHorizontalRuler(
            SKCanvas canvas,
            Rect canvasDest,
            double scale,
            int docWidth,
            int tileWidth,
            float rulerLeft,
            float rulerRight,
            int? cursorDocX,
            ElementTheme theme)
        {
            SetRuleTheme(theme);

            // Ruler background
            using var bgPaint = new SKPaint { Color = ToSKColor(RulerBackground), IsAntialias = false };
            canvas.DrawRect(rulerLeft, 0, rulerRight - rulerLeft, RULER_THICKNESS, bgPaint);

            // Border
            using var borderPaint = new SKPaint { Color = ToSKColor(RulerBorder), IsAntialias = false, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(rulerLeft, RULER_THICKNESS, rulerRight, RULER_THICKNESS, borderPaint);

            // Calculate tick intervals
            int largeInterval = tileWidth;
            int mediumInterval = RoundedInterval(tileWidth, 2);
            int smallInterval = RoundedInterval(tileWidth, 4);

            if (mediumInterval < 1) mediumInterval = 1;
            if (smallInterval < 1) smallInterval = 1;

            float docOriginScreenX = (float)canvasDest.X;

            // Draw cursor highlight
            if (cursorDocX.HasValue)
            {
                float cursorScreenX = docOriginScreenX + (float)(cursorDocX.Value * scale);
                if (cursorScreenX >= rulerLeft && cursorScreenX <= rulerRight)
                {
                    using var highlightPaint = new SKPaint { Color = ToSKColor(CursorHighlight), IsAntialias = false };
                    canvas.DrawRect(cursorScreenX - 1, 0, 3, RULER_THICKNESS, highlightPaint);
                }
            }

            // Determine which tick marks to draw based on zoom level
            float pixelsPerDocPixel = (float)scale;
            bool drawSmall = pixelsPerDocPixel >= 2f;
            bool drawMedium = pixelsPerDocPixel >= 1f;
            bool drawLabels = pixelsPerDocPixel >= 4f;

            using var tickPaint = new SKPaint { Color = ToSKColor(TickColor), IsAntialias = false, StrokeWidth = 1 };
            using var textPaint = new SKPaint
            {
                Color = ToSKColor(LabelColor),
                IsAntialias = true,
                TextSize = 9,
                Typeface = SKTypeface.FromFamilyName("Segoe UI")
            };

            // Draw tick marks
            for (int docX = 0; docX <= docWidth; docX++)
            {
                float screenX = docOriginScreenX + (float)(docX * scale);
                if (screenX < rulerLeft - 1 || screenX > rulerRight + 1)
                    continue;

                TickType tickType = GetTickType(docX, largeInterval, mediumInterval, smallInterval);

                if (tickType == TickType.None) continue;
                if (tickType == TickType.Small && !drawSmall) continue;
                if (tickType == TickType.Medium && !drawMedium) continue;

                float tickHeight = tickType switch
                {
                    TickType.Large => LARGE_TICK_HEIGHT,
                    TickType.Medium => MEDIUM_TICK_HEIGHT,
                    TickType.Small => SMALL_TICK_HEIGHT,
                    _ => 0
                };

                canvas.DrawLine(screenX, RULER_THICKNESS - tickHeight, screenX, RULER_THICKNESS, tickPaint);

                // Draw label for large ticks
                if (tickType == TickType.Large && drawLabels && docX > 0)
                {
                    canvas.DrawText(docX.ToString(), screenX + 2, 11, textPaint);
                }
            }
        }

        /// <summary>
        /// Draws the vertical ruler (along the left).
        /// </summary>
        public static void DrawVerticalRuler(
            SKCanvas canvas,
            Rect canvasDest,
            double scale,
            int docHeight,
            int tileHeight,
            float rulerTop,
            float rulerBottom,
            int? cursorDocY,
            ElementTheme theme)
        {
            SetRuleTheme(theme);

            // Ruler background
            using var bgPaint = new SKPaint { Color = ToSKColor(RulerBackground), IsAntialias = false };
            canvas.DrawRect(0, rulerTop, RULER_THICKNESS, rulerBottom - rulerTop, bgPaint);

            // Border
            using var borderPaint = new SKPaint { Color = ToSKColor(RulerBorder), IsAntialias = false, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(RULER_THICKNESS, rulerTop, RULER_THICKNESS, rulerBottom, borderPaint);

            // Calculate tick intervals
            int largeInterval = tileHeight;
            int mediumInterval = RoundedInterval(tileHeight, 2);
            int smallInterval = RoundedInterval(tileHeight, 4);

            if (mediumInterval < 1) mediumInterval = 1;
            if (smallInterval < 1) smallInterval = 1;

            float docOriginScreenY = (float)canvasDest.Y;

            // Draw cursor highlight
            if (cursorDocY.HasValue)
            {
                float cursorScreenY = docOriginScreenY + (float)(cursorDocY.Value * scale);
                if (cursorScreenY >= rulerTop && cursorScreenY <= rulerBottom)
                {
                    using var highlightPaint = new SKPaint { Color = ToSKColor(CursorHighlight), IsAntialias = false };
                    canvas.DrawRect(0, cursorScreenY - 1, RULER_THICKNESS, 3, highlightPaint);
                }
            }

            // Determine which tick marks to draw based on zoom level
            float pixelsPerDocPixel = (float)scale;
            bool drawSmall = pixelsPerDocPixel >= 2f;
            bool drawMedium = pixelsPerDocPixel >= 1f;
            bool drawLabels = pixelsPerDocPixel >= 4f;

            using var tickPaint = new SKPaint { Color = ToSKColor(TickColor), IsAntialias = false, StrokeWidth = 1 };
            using var textPaint = new SKPaint
            {
                Color = ToSKColor(LabelColor),
                IsAntialias = true,
                TextSize = 8,
                Typeface = SKTypeface.FromFamilyName("Segoe UI")
            };

            // Draw tick marks
            for (int docY = 0; docY <= docHeight; docY++)
            {
                float screenY = docOriginScreenY + (float)(docY * scale);
                if (screenY < rulerTop - 1 || screenY > rulerBottom + 1)
                    continue;

                TickType tickType = GetTickType(docY, largeInterval, mediumInterval, smallInterval);

                if (tickType == TickType.None) continue;
                if (tickType == TickType.Small && !drawSmall) continue;
                if (tickType == TickType.Medium && !drawMedium) continue;

                float tickHeight = tickType switch
                {
                    TickType.Large => LARGE_TICK_HEIGHT,
                    TickType.Medium => MEDIUM_TICK_HEIGHT,
                    TickType.Small => SMALL_TICK_HEIGHT,
                    _ => 0
                };

                canvas.DrawLine(RULER_THICKNESS - tickHeight, screenY, RULER_THICKNESS, screenY, tickPaint);

                // Draw label for large ticks (vertical text)
                if (tickType == TickType.Large && drawLabels && docY > 0)
                {
                    var text = docY.ToString();
                    float charY = screenY + 10;
                    foreach (char c in text)
                    {
                        canvas.DrawText(c.ToString(), 3, charY, textPaint);
                        charY += 8;
                    }
                }
            }
        }

        /// <summary>
        /// Draws the corner square where rulers meet.
        /// </summary>
        public static void DrawCorner(SKCanvas canvas, ElementTheme theme)
        {
            SetRuleTheme(theme);

            using var bgPaint = new SKPaint { Color = ToSKColor(RulerBackground), IsAntialias = false };
            canvas.DrawRect(0, 0, CORNER_SIZE, CORNER_SIZE, bgPaint);

            using var borderPaint = new SKPaint { Color = ToSKColor(RulerBorder), IsAntialias = false, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(CORNER_SIZE, 0, CORNER_SIZE, CORNER_SIZE, borderPaint);
            canvas.DrawLine(0, CORNER_SIZE, CORNER_SIZE, CORNER_SIZE, borderPaint);
        }

        /// <summary>
        /// Determines the tick type for a given position.
        /// </summary>
        private static TickType GetTickType(int position, int largeInterval, int mediumInterval, int smallInterval)
        {
            if (position == 0) return TickType.Large;
            if (largeInterval > 0 && position % largeInterval == 0) return TickType.Large;
            if (mediumInterval > 0 && position % mediumInterval == 0) return TickType.Medium;
            if (smallInterval > 0 && position % smallInterval == 0) return TickType.Small;
            return TickType.None;
        }

        private static SKColor ToSKColor(Color c) => new SKColor(c.R, c.G, c.B, c.A);

        private enum TickType { None, Small, Medium, Large }
    }
}
