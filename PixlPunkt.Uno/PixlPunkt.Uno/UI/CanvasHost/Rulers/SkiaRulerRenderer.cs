using System;
using Microsoft.UI.Xaml;
using PixlPunkt.Uno.Core.Rendering;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.Uno.UI.CanvasHost.Rulers;

/// <summary>
/// SkiaSharp-based ruler rendering for canvas rulers.
/// </summary>
public static class SkiaRulerRenderer
{
    private static readonly Color RulerBackgroundLight = Color.FromArgb(255, 245, 245, 245);
    private static readonly Color RulerBackgroundDark = Color.FromArgb(255, 45, 45, 45);
    private static readonly Color RulerTickLight = Color.FromArgb(255, 80, 80, 80);
    private static readonly Color RulerTickDark = Color.FromArgb(255, 180, 180, 180);
    private static readonly Color RulerTextLight = Color.FromArgb(255, 60, 60, 60);
    private static readonly Color RulerTextDark = Color.FromArgb(255, 200, 200, 200);
    private static readonly Color CursorHighlight = Color.FromArgb(100, 0, 120, 215);

    /// <summary>
    /// Draws a horizontal ruler using the ICanvasRenderer abstraction.
    /// </summary>
    public static void DrawHorizontalRuler(
        ICanvasRenderer renderer,
        Rect dest,
        double scale,
        int docWidth,
        int tileWidth,
        float offsetY,
        float rulerWidth,
        int? cursorDocX,
        ElementTheme theme)
    {
        bool isDark = theme == ElementTheme.Dark;
        var bgColor = isDark ? RulerBackgroundDark : RulerBackgroundLight;
        var tickColor = isDark ? RulerTickDark : RulerTickLight;
        var textColor = isDark ? RulerTextDark : RulerTextLight;

        renderer.Clear(bgColor);

        float rulerHeight = renderer.Height;
        float destX = (float)dest.X;
        float s = (float)scale;

        // Draw cursor highlight
        if (cursorDocX.HasValue && cursorDocX.Value >= 0 && cursorDocX.Value < docWidth)
        {
            float highlightX = destX + cursorDocX.Value * s;
            renderer.FillRectangle(highlightX, 0, s, rulerHeight, CursorHighlight);
        }

        // Calculate tick intervals based on zoom
        int majorInterval = CalculateMajorInterval(scale, tileWidth);
        int minorInterval = Math.Max(1, majorInterval / 4);

        // Draw ticks and labels
        using var textFormat = renderer.CreateTextFormat("Segoe UI", 9f);

        for (int x = 0; x <= docWidth; x += minorInterval)
        {
            float screenX = destX + x * s;
            if (screenX < 0 || screenX > rulerWidth) continue;

            bool isMajor = (x % majorInterval) == 0;
            bool isTile = (x % tileWidth) == 0;

            float tickHeight = isMajor ? rulerHeight * 0.6f : (isTile ? rulerHeight * 0.4f : rulerHeight * 0.25f);
            float tickY = rulerHeight - tickHeight;

            renderer.DrawLine(screenX, tickY, screenX, rulerHeight, tickColor, 1f);

            // Draw labels for major ticks
            if (isMajor && scale >= 0.5)
            {
                string label = x.ToString();
                renderer.DrawText(label, screenX + 2, 2, textColor, textFormat);
            }
        }

        // Draw bottom border
        renderer.DrawLine(0, rulerHeight - 1, rulerWidth, rulerHeight - 1, tickColor, 1f);
    }

    /// <summary>
    /// Draws a vertical ruler using the ICanvasRenderer abstraction.
    /// </summary>
    public static void DrawVerticalRuler(
        ICanvasRenderer renderer,
        Rect dest,
        double scale,
        int docHeight,
        int tileHeight,
        float offsetX,
        float rulerHeight,
        int? cursorDocY,
        ElementTheme theme)
    {
        bool isDark = theme == ElementTheme.Dark;
        var bgColor = isDark ? RulerBackgroundDark : RulerBackgroundLight;
        var tickColor = isDark ? RulerTickDark : RulerTickLight;
        var textColor = isDark ? RulerTextDark : RulerTextLight;

        renderer.Clear(bgColor);

        float rulerWidth = renderer.Width;
        float destY = (float)dest.Y;
        float s = (float)scale;

        // Draw cursor highlight
        if (cursorDocY.HasValue && cursorDocY.Value >= 0 && cursorDocY.Value < docHeight)
        {
            float highlightY = destY + cursorDocY.Value * s;
            renderer.FillRectangle(0, highlightY, rulerWidth, s, CursorHighlight);
        }

        // Calculate tick intervals based on zoom
        int majorInterval = CalculateMajorInterval(scale, tileHeight);
        int minorInterval = Math.Max(1, majorInterval / 4);

        // Draw ticks and labels
        using var textFormat = renderer.CreateTextFormat("Segoe UI", 9f);

        for (int y = 0; y <= docHeight; y += minorInterval)
        {
            float screenY = destY + y * s;
            if (screenY < 0 || screenY > rulerHeight) continue;

            bool isMajor = (y % majorInterval) == 0;
            bool isTile = (y % tileHeight) == 0;

            float tickWidth = isMajor ? rulerWidth * 0.6f : (isTile ? rulerWidth * 0.4f : rulerWidth * 0.25f);
            float tickX = rulerWidth - tickWidth;

            renderer.DrawLine(tickX, screenY, rulerWidth, screenY, tickColor, 1f);

            // Draw labels for major ticks (rotated text would be better, but for now just show at low zoom)
            if (isMajor && scale >= 1.0)
            {
                // For vertical ruler, we'd ideally rotate text, but for simplicity just show small labels
                string label = y.ToString();
                // Skip label drawing for vertical ruler in SkiaSharp for now
                // (would need rotated text which is more complex)
            }
        }

        // Draw right border
        renderer.DrawLine(rulerWidth - 1, 0, rulerWidth - 1, rulerHeight, tickColor, 1f);
    }

    private static int CalculateMajorInterval(double scale, int tileSize)
    {
        // At zoom 1.0, major ticks every tile
        // At lower zooms, increase interval to avoid clutter
        // At higher zooms, can show more detail

        double pixelsPerTile = tileSize * scale;

        if (pixelsPerTile >= 100) return tileSize;
        if (pixelsPerTile >= 50) return tileSize * 2;
        if (pixelsPerTile >= 25) return tileSize * 4;
        if (pixelsPerTile >= 12) return tileSize * 8;
        return tileSize * 16;
    }
}
