using System;
using PixlPunkt.Core.Rendering;
using Windows.UI;

namespace PixlPunkt.Core.Tools.Selection
{
    /// <summary>Marching-ants style line drawing shared by the selection tools' previews.</summary>
    public static class SelectionToolDrawing
    {
        public static void DrawDashedLine(ICanvasRenderer renderer, float x1, float y1, float x2, float y2,
            Color color1, Color color2, float thickness, float dashOn, float dashOff, float phase)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float length = MathF.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f) return;

            float nx = dx / length;
            float ny = dy / length;
            float dashLength = dashOn + dashOff;
            float pos = -phase % dashLength;
            if (pos < 0) pos += dashLength;

            while (pos < length)
            {
                float startPos = Math.Max(0, pos);
                float endPos = Math.Min(length, pos + dashOn);
                if (endPos > startPos)
                    renderer.DrawLine(x1 + nx * startPos, y1 + ny * startPos, x1 + nx * endPos, y1 + ny * endPos, color1, thickness);

                float offStart = pos + dashOn;
                float offEnd = Math.Min(length, pos + dashLength);
                if (offEnd > offStart && offStart < length)
                    renderer.DrawLine(x1 + nx * Math.Max(0, offStart), y1 + ny * Math.Max(0, offStart), x1 + nx * offEnd, y1 + ny * offEnd, color2, thickness);

                pos += dashLength;
            }
        }
    }
}
