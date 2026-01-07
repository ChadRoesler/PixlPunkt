using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Selection;
using PixlPunkt.Core.Tools.Settings;
using Windows.Foundation;
using Windows.Graphics;

namespace PixlPunkt.Core.Tools.Selection
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
        public override void DrawPreview(CanvasDrawingSession ds, Rect destRect, double scale, float antsPhase)
        {
            if (!_hasPreview || _previewRect.Width <= 0 || _previewRect.Height <= 0)
                return;

            float x = (float)(destRect.X + _previewRect.X * scale);
            float y = (float)(destRect.Y + _previewRect.Y * scale);
            float w = (float)(_previewRect.Width * scale);
            float h = (float)(_previewRect.Height * scale);

            var dashStyleWhite = new CanvasStrokeStyle
            {
                DashStyle = CanvasDashStyle.Dash,
                DashOffset = antsPhase,
                CustomDashStyle = new float[] { ANTS_ON, ANTS_OFF }
            };
            var dashStyleBlack = new CanvasStrokeStyle
            {
                DashStyle = CanvasDashStyle.Dash,
                DashOffset = antsPhase + ANTS_ON,
                CustomDashStyle = new float[] { ANTS_ON, ANTS_OFF }
            };

            ds.DrawRectangle(x, y, w, h, Colors.White, ANTS_THICKNESS, dashStyleWhite);
            ds.DrawRectangle(x, y, w, h, Colors.Black, ANTS_THICKNESS, dashStyleBlack);

            // Draw dimension label near the cursor (bottom-right of selection)
            DrawDimensionLabel(ds, x, y, w, h);
        }

        /// <summary>
        /// Draws a dimension label showing width × height near the selection rectangle.
        /// </summary>
        private void DrawDimensionLabel(CanvasDrawingSession ds, float x, float y, float w, float h)
        {
            string label = $"{_previewRect.Width} × {_previewRect.Height}";

            // Use a simple text format
            using var textFormat = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };

            // Measure the text
            using var textLayout = new Microsoft.Graphics.Canvas.Text.CanvasTextLayout(ds, label, textFormat, 200, 20);
            float textWidth = (float)textLayout.LayoutBounds.Width;
            float textHeight = (float)textLayout.LayoutBounds.Height;

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
            ds.FillRoundedRectangle(bgX, bgY, bgW, bgH, 4, 4, Windows.UI.Color.FromArgb(200, 30, 30, 30));
            ds.DrawRoundedRectangle(bgX, bgY, bgW, bgH, 4, 4, Windows.UI.Color.FromArgb(180, 100, 100, 100), 1);

            // Draw text
            ds.DrawText(label, labelX, labelY, Colors.White, textFormat);
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

            _previewRect = new RectInt32(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
        }
    }
}
