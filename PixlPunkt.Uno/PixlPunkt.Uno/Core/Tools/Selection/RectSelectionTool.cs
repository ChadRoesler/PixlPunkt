using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Rendering;
using PixlPunkt.Uno.Core.Selection;
using PixlPunkt.Uno.Core.Tools.Settings;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using static PixlPunkt.Uno.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Uno.Core.Tools.Selection
{
    /// <summary>
    /// Rectangular marquee selection tool for creating axis-aligned rectangular selections.
    /// Supports starting and dragging selection outside canvas bounds - the preview marquee
    /// and final selection area are clamped to canvas bounds.
    /// </summary>
    public sealed class RectSelectionTool : SelectionToolBase
    {
        private readonly Func<SelectionRegion> _getSelectionRegion;
        private readonly Action _requestRedraw;
        private readonly Func<(int w, int h)> _getDocumentSize;

        // Raw coordinates (can be outside canvas)
        private Point _startPoint;
        private Point _endPoint;

        // Clamped preview rect (always within canvas)
        private RectInt32 _previewRect;
        private bool _hasPreview;

        private const float ANTS_THICKNESS = 2.0f;
        private const float ANTS_ON = 4.0f;
        private const float ANTS_OFF = 4.0f;

        /// <summary>
        /// Initializes a new instance of the <see cref="RectSelectionTool"/> class.
        /// </summary>
        public RectSelectionTool(
            Func<SelectionRegion> getSelectionRegion,
            Action requestRedraw,
            Func<(int w, int h)> getDocumentSize)
        {
            _getSelectionRegion = getSelectionRegion;
            _requestRedraw = requestRedraw;
            _getDocumentSize = getDocumentSize;
        }

        /// <summary>
        /// Creates a new instance from a SelectionToolContext.
        /// </summary>
        public RectSelectionTool(SelectionToolContext context)
            : this(context.GetSelectionRegion, context.RequestRedraw, context.GetDocumentSize)
        {
        }

        //////////////////////////////////////////////////////////////////
        // ISelectionTool IMPLEMENTATION
        //////////////////////////////////////////////////////////////////

        /// <inheritdoc/>
        public override string Id => ToolIds.SelectRect;

        /// <inheritdoc/>
        public override bool HasPreview => _hasPreview;

        /// <summary>
        /// Gets the current preview rectangle for rendering (clamped to canvas).
        /// </summary>
        public RectInt32 PreviewRect => _previewRect;

        /// <inheritdoc/>
        public override void Configure(ToolSettingsBase settings)
        {
        }

        /// <inheritdoc/>
        public override void DrawPreview(ICanvasRenderer renderer, Rect destRect, double scale, float antsPhase)
        {
            if (!_hasPreview || _previewRect.Width <= 0 || _previewRect.Height <= 0)
                return;

            float x = (float)(destRect.X + _previewRect.X * scale);
            float y = (float)(destRect.Y + _previewRect.Y * scale);
            float w = (float)(_previewRect.Width * scale);
            float h = (float)(_previewRect.Height * scale);

            // Draw marching ants rectangle (simplified dashed lines)
            DrawDashedRectangle(renderer, x, y, w, h, Colors.White, Colors.Black, ANTS_THICKNESS, ANTS_ON, ANTS_OFF, antsPhase);

            // Draw dimension label near the cursor (bottom-right of selection)
            DrawDimensionLabel(renderer, x, y, w, h);
        }

        /// <summary>
        /// Draws a dashed rectangle with alternating colors (simulating marching ants).
        /// </summary>
        private static void DrawDashedRectangle(ICanvasRenderer renderer, float x, float y, float w, float h,
            Color color1, Color color2, float thickness, float dashOn, float dashOff, float phase)
        {
            // Top edge
            DrawDashedLine(renderer, x, y, x + w, y, color1, color2, thickness, dashOn, dashOff, phase);
            // Right edge
            DrawDashedLine(renderer, x + w, y, x + w, y + h, color1, color2, thickness, dashOn, dashOff, phase);
            // Bottom edge
            DrawDashedLine(renderer, x + w, y + h, x, y + h, color1, color2, thickness, dashOn, dashOff, phase);
            // Left edge
            DrawDashedLine(renderer, x, y + h, x, y, color1, color2, thickness, dashOn, dashOff, phase);
        }

        /// <summary>
        /// Draws a dashed line with alternating colors.
        /// </summary>
        private static void DrawDashedLine(ICanvasRenderer renderer, float x1, float y1, float x2, float y2,
            Color color1, Color color2, float thickness, float dashOn, float dashOff, float phase)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float length = MathF.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f) return;

            // Normalize direction
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
                {
                    float sx = x1 + nx * startPos;
                    float sy = y1 + ny * startPos;
                    float ex = x1 + nx * endPos;
                    float ey = y1 + ny * endPos;

                    renderer.DrawLine(sx, sy, ex, ey, color1, thickness);
                }

                // Draw the "off" portion with color2
                float offStart = pos + dashOn;
                float offEnd = Math.Min(length, pos + dashLength);
                if (offEnd > offStart && offStart < length)
                {
                    float sx = x1 + nx * Math.Max(0, offStart);
                    float sy = y1 + ny * Math.Max(0, offStart);
                    float ex = x1 + nx * offEnd;
                    float ey = y1 + ny * offEnd;

                    renderer.DrawLine(sx, sy, ex, ey, color2, thickness);
                }

                pos += dashLength;
            }
        }

        /// <summary>
        /// Draws a dimension label showing width × height near the selection rectangle.
        /// </summary>
        private void DrawDimensionLabel(ICanvasRenderer renderer, float x, float y, float w, float h)
        {
            string label = $"{_previewRect.Width} × {_previewRect.Height}";

            // Create text format
            using var textFormat = renderer.CreateTextFormat("Segoe UI", 12, FontWeight.SemiBold);

            // Measure the text
            using var textLayout = renderer.CreateTextLayout(label, textFormat, 200, 20);
            float textWidth = textLayout.LayoutWidth;
            float textHeight = textLayout.LayoutHeight;

            // Position label at bottom-right of selection with offset
            float labelX = x + w + 12;
            float labelY = y + h + 12;

            // Draw background pill
            float padding = 4;
            float bgX = labelX - padding;
            float bgY = labelY - padding / 2;
            float bgW = textWidth + padding * 2;
            float bgH = textHeight + padding;

            // Semi-transparent dark background with rounded corners
            renderer.FillRoundedRectangle(new Rect(bgX, bgY, bgW, bgH), 4, 4, Color.FromArgb(200, 30, 30, 30));
            renderer.DrawRoundedRectangle(new Rect(bgX, bgY, bgW, bgH), 4, 4, Color.FromArgb(180, 100, 100, 100), 1);

            // Draw text
            renderer.DrawText(label, labelX, labelY, Colors.White, textFormat);
        }

        //////////////////////////////////////////////////////////////////
        // SELECTION LOGIC
        //////////////////////////////////////////////////////////////////

        /// <inheritdoc/>
        protected override bool OnPressed(Point docPos, PointerRoutedEventArgs e)
        {
            // Store raw start point (can be outside canvas)
            _startPoint = docPos;
            _endPoint = docPos;
            _hasPreview = true;

            // Update clamped preview
            UpdateClampedPreview();

            _requestRedraw?.Invoke();
            return true;
        }

        /// <inheritdoc/>
        protected override bool OnMoved(Point docPos, PointerRoutedEventArgs e)
        {
            // Store raw end point (can be outside canvas)
            _endPoint = docPos;

            // Update clamped preview
            UpdateClampedPreview();

            _requestRedraw?.Invoke();
            return true;
        }

        /// <inheritdoc/>
        protected override bool OnReleased(Point docPos, PointerRoutedEventArgs e)
        {
            _hasPreview = false;

            // Use the clamped preview rect as the final selection
            var finalRect = _previewRect;

            if (finalRect.Width <= 0 || finalRect.Height <= 0)
            {
                _requestRedraw?.Invoke();
                return true;
            }

            var (docW, docH) = _getDocumentSize();
            var region = _getSelectionRegion();
            region.EnsureSize(docW, docH);

            switch (CombineMode)
            {
                case SelectionCombineMode.Replace:
                    region.Clear();
                    region.AddRect(finalRect);
                    break;

                case SelectionCombineMode.Add:
                    region.AddRect(finalRect);
                    break;

                case SelectionCombineMode.Subtract:
                    region.SubtractRect(finalRect);
                    break;
            }

            _requestRedraw?.Invoke();
            return true;
        }

        /// <inheritdoc/>
        public override void Deactivate()
        {
            base.Deactivate();
            _hasPreview = false;
        }

        /// <summary>
        /// Updates the clamped preview rect from raw start/end points.
        /// </summary>
        private void UpdateClampedPreview()
        {
            var (docW, docH) = _getDocumentSize();

            // Compute raw rect from start/end points
            int x0 = (int)_startPoint.X;
            int y0 = (int)_startPoint.Y;
            int x1 = (int)_endPoint.X;
            int y1 = (int)_endPoint.Y;

            int minX = Math.Min(x0, x1);
            int minY = Math.Min(y0, y1);
            int maxX = Math.Max(x0, x1);
            int maxY = Math.Max(y0, y1);

            // Clamp to canvas bounds
            minX = Math.Clamp(minX, 0, docW);
            minY = Math.Clamp(minY, 0, docH);
            maxX = Math.Clamp(maxX, 0, docW);
            maxY = Math.Clamp(maxY, 0, docH);

            _previewRect = CreateRect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
        }
    }
}
