using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Selection;
using PixlPunkt.Core.Tools.Settings;
using Windows.Foundation;

namespace PixlPunkt.Core.Tools.Selection
{
    /// <summary>
    /// Polygon lasso selection tool for creating freehand polygon selections.
    /// </summary>
    public sealed class LassoSelectionTool : SelectionToolBase
    {
        private readonly Func<SelectionRegion> _getSelectionRegion;
        private readonly Action _requestRedraw;
        private readonly Func<(int w, int h)> _getDocumentSize;

        private readonly List<Point> _vertices = new();
        private Point? _currentMousePos;
        private bool _polygonActive;

        private const double CloseDistanceThreshold = 5.0;
        private const float ANTS_THICKNESS = 2.0f;
        private const float ANTS_ON = 4.0f;
        private const float ANTS_OFF = 4.0f;
        private const float VERTEX_SIZE = 4f;

        /// <summary>
        /// Gets the list of polygon vertices for rendering.
        /// </summary>
        public IReadOnlyList<Point> Vertices => _vertices;

        /// <summary>
        /// Gets the current mouse position for rubber-band preview.
        /// </summary>
        public Point? CurrentMousePos => _currentMousePos;

        /// <summary>
        /// Gets a value indicating whether a polygon is being actively drawn.
        /// </summary>
        public bool IsPolygonActive => _polygonActive;

        /// <summary>
        /// Initializes a new instance of the <see cref="LassoSelectionTool"/> class.
        /// </summary>
        /// <param name="getSelectionRegion">Function to get the selection region.</param>
        /// <param name="requestRedraw">Action to request canvas redraw.</param>
        /// <param name="getDocumentSize">Function to get document dimensions.</param>
        public LassoSelectionTool(
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
        public LassoSelectionTool(SelectionToolContext context)
            : this(context.GetSelectionRegion, context.RequestRedraw, context.GetDocumentSize)
        {
        }

        // ====================================================================
        // ISelectionTool IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        public override string Id => ToolIds.Lasso;

        /// <inheritdoc/>
        public override bool HasPreview => _polygonActive;

        /// <inheritdoc/>
        public override void Configure(ToolSettingsBase settings) { }

        /// <inheritdoc/>
        public override void DrawPreview(CanvasDrawingSession ds, Rect destRect, double scale, float antsPhase)
        {
            if (!_polygonActive || _vertices.Count == 0)
                return;

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

            for (int i = 0; i < _vertices.Count - 1; i++)
            {
                float x1 = (float)(destRect.X + _vertices[i].X * scale);
                float y1 = (float)(destRect.Y + _vertices[i].Y * scale);
                float x2 = (float)(destRect.X + _vertices[i + 1].X * scale);
                float y2 = (float)(destRect.Y + _vertices[i + 1].Y * scale);

                ds.DrawLine(x1, y1, x2, y2, Colors.White, ANTS_THICKNESS, dashStyleWhite);
                ds.DrawLine(x1, y1, x2, y2, Colors.Black, ANTS_THICKNESS, dashStyleBlack);
            }

            if (_currentMousePos.HasValue)
            {
                var lastVertex = _vertices[_vertices.Count - 1];
                float x1 = (float)(destRect.X + lastVertex.X * scale);
                float y1 = (float)(destRect.Y + lastVertex.Y * scale);
                float x2 = (float)(destRect.X + _currentMousePos.Value.X * scale);
                float y2 = (float)(destRect.Y + _currentMousePos.Value.Y * scale);

                ds.DrawLine(x1, y1, x2, y2, Colors.White, ANTS_THICKNESS, dashStyleWhite);
                ds.DrawLine(x1, y1, x2, y2, Colors.Black, ANTS_THICKNESS, dashStyleBlack);

                if (_vertices.Count >= 2)
                {
                    var firstVertex = _vertices[0];
                    float fx = (float)(destRect.X + firstVertex.X * scale);
                    float fy = (float)(destRect.Y + firstVertex.Y * scale);

                    ds.DrawLine(x2, y2, fx, fy, Windows.UI.Color.FromArgb(128, 255, 255, 255), 1f, dashStyleWhite);
                    ds.DrawLine(x2, y2, fx, fy, Windows.UI.Color.FromArgb(128, 0, 0, 0), 1f, dashStyleBlack);
                }
            }

            for (int i = 0; i < _vertices.Count; i++)
            {
                float vx = (float)(destRect.X + _vertices[i].X * scale);
                float vy = (float)(destRect.Y + _vertices[i].Y * scale);

                if (i == 0 && _vertices.Count >= 3)
                {
                    ds.FillCircle(vx, vy, VERTEX_SIZE + 2f, Colors.LimeGreen);
                    ds.DrawCircle(vx, vy, VERTEX_SIZE + 2f, Colors.Black, 1f);
                }
                else
                {
                    ds.FillCircle(vx, vy, VERTEX_SIZE, Colors.White);
                    ds.DrawCircle(vx, vy, VERTEX_SIZE, Colors.Black, 1f);
                }
            }
        }

        // ====================================================================
        // SELECTION LOGIC
        // ====================================================================

        protected override bool OnPressed(Point docPos, PointerRoutedEventArgs e)
        {
            // Check if we're clicking near the first point to close the polygon
            if (_polygonActive && _vertices.Count >= 3)
            {
                var firstPt = _vertices[0];
                double dist = Math.Sqrt(
                    Math.Pow(docPos.X - firstPt.X, 2) +
                    Math.Pow(docPos.Y - firstPt.Y, 2)
                );

                if (dist < CloseDistanceThreshold)
                {
                    // Close polygon and finalize selection
                    FinalizePolygon();
                    return true;
                }
            }

            // Add new vertex
            _vertices.Add(docPos);
            _polygonActive = true;
            _currentMousePos = docPos;

            _requestRedraw?.Invoke();

            return true;
        }

        protected override bool OnMoved(Point docPos, PointerRoutedEventArgs e)
        {
            // Always update mouse position when polygon is active OR when we might start one
            // This ensures the rubber-band preview works correctly
            if (_polygonActive)
            {
                _currentMousePos = docPos;
                _requestRedraw?.Invoke();
                return true;
            }

            return false;
        }

        protected override bool OnReleased(Point docPos, PointerRoutedEventArgs e)
        {
            // Lasso is click-based, not drag-based
            // Update mouse position on release to ensure preview is accurate
            if (_polygonActive)
            {
                _currentMousePos = docPos;
                _requestRedraw?.Invoke();
            }
            return _polygonActive;
        }

        public override void Deactivate()
        {
            base.Deactivate();
            CancelPolygon();
        }

        public override void Cancel()
        {
            base.Cancel();
            CancelPolygon();
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Handles double-click to close polygon.
        /// </summary>
        public void HandleDoubleClick()
        {
            if (_polygonActive && _vertices.Count >= 3)
            {
                FinalizePolygon();
            }
        }

        /// <summary>
        /// Cancels the current polygon selection.
        /// </summary>
        public void CancelPolygon()
        {
            _vertices.Clear();
            _currentMousePos = null;
            _polygonActive = false;
            _requestRedraw?.Invoke();
        }

        /// <summary>
        /// Commits the current polygon selection (Enter key).
        /// </summary>
        public void CommitPolygon()
        {
            if (_polygonActive && _vertices.Count >= 3)
                FinalizePolygon();
        }

        // ====================================================================
        // POLYGON FINALIZATION
        // ====================================================================

        private void FinalizePolygon()
        {
            if (_vertices.Count < 3)
            {
                CancelPolygon();
                return;
            }

            var region = _getSelectionRegion();
            var (w, h) = _getDocumentSize();
            region.EnsureSize(w, h);

            FillPolygon(region, w, h);

            _vertices.Clear();
            _currentMousePos = null;
            _polygonActive = false;
            _requestRedraw?.Invoke();
        }

        private void FillPolygon(SelectionRegion region, int w, int h)
        {
            if (_vertices.Count < 3)
                return;

            int minY = int.MaxValue;
            int maxY = int.MinValue;

            foreach (var v in _vertices)
            {
                int y = (int)v.Y;
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }

            minY = Math.Clamp(minY, 0, h - 1);
            maxY = Math.Clamp(maxY, 0, h - 1);

            for (int y = minY; y <= maxY; y++)
            {
                var intersections = new List<int>();

                for (int i = 0; i < _vertices.Count; i++)
                {
                    var v1 = _vertices[i];
                    var v2 = _vertices[(i + 1) % _vertices.Count];

                    int y1 = (int)v1.Y;
                    int y2 = (int)v2.Y;

                    if (y1 == y2)
                        continue;

                    if ((y >= y1 && y < y2) || (y >= y2 && y < y1))
                    {
                        double t = (y - v1.Y) / (v2.Y - v1.Y);
                        int x = (int)(v1.X + t * (v2.X - v1.X));
                        intersections.Add(x);
                    }
                }

                intersections.Sort();

                for (int i = 0; i < intersections.Count - 1; i += 2)
                {
                    int x0 = Math.Clamp(intersections[i], 0, w - 1);
                    int x1 = Math.Clamp(intersections[i + 1], 0, w - 1);

                    for (int x = x0; x <= x1; x++)
                    {
                        switch (CombineMode)
                        {
                            case SelectionCombineMode.Add:
                            case SelectionCombineMode.Replace:
                                region.AddRect(new Windows.Graphics.RectInt32(x, y, 1, 1));
                                break;

                            case SelectionCombineMode.Subtract:
                                region.SubtractRect(new Windows.Graphics.RectInt32(x, y, 1, 1));
                                break;
                        }
                    }
                }
            }
        }
    }
}
