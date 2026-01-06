using System;
using System.Numerics;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Enums;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;

namespace PixlPunkt.Uno.UI.ColorPick.Controls
{
    /// <summary>
    /// Interactive SkiaSharp control for editing a piecewise cubic Bézier curve with fixed anchors.
    /// The curve forms a triangle shape with a peak at the center, used for gradient falloff editing.
    /// 
    /// Fixed anchors: Start (-1, 0), Mid (0, 1), End (1, 0)
    /// Editable control points: Start_out, Mid_left, Mid_right, End_in
    /// 
    /// The curve is split into two cubic segments:
    /// - Left segment: Start → Mid (controlled by Start_out and Mid_left)
    /// - Right segment: Mid → End (controlled by Mid_right and End_in)
    /// </summary>
    public sealed partial class TriangleCurveEditor : UserControl
    {
        // ════════════════════════════════════════════════════════════════════
        // CONSTANTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Outer (black) ring radius for control point handles.
        /// </summary>
        private const float HANDLE_OUT = 9f;

        /// <summary>
        /// Inner (white) ring radius for control point handles.
        /// </summary>
        private const float HANDLE_IN = 7f;

        /// <summary>
        /// Inset from control edges to leave space for handle rendering.
        /// </summary>
        private const float INSET = HANDLE_OUT + 2f;

        // ════════════════════════════════════════════════════════════════════
        // PROPERTIES - CONTROL POINTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets or sets the outgoing control point for the Start anchor.
        /// Editor space: X ∈ [-1, 0], Y ∈ [0, 1].
        /// </summary>
        public Vector2 Start_out { get; set; } = new(-0.66f, 0.00f);

        /// <summary>
        /// Gets or sets the left incoming control point for the Mid anchor.
        /// Editor space: X ∈ [-1, 0], Y ∈ [0, 1].
        /// </summary>
        public Vector2 Mid_left { get; set; } = new(-0.15f, 0.80f);

        /// <summary>
        /// Gets or sets the right outgoing control point for the Mid anchor.
        /// Editor space: X ∈ [0, 1], Y ∈ [0, 1].
        /// </summary>
        public Vector2 Mid_right { get; set; } = new(0.15f, 0.80f);

        /// <summary>
        /// Gets or sets the incoming control point for the End anchor.
        /// Editor space: X ∈ [0, 1], Y ∈ [0, 1].
        /// </summary>
        public Vector2 End_in { get; set; } = new(0.66f, 0.00f);

        // ════════════════════════════════════════════════════════════════════
        // EVENTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fired when the curve shape changes due to user interaction.
        /// </summary>
        public event EventHandler? CurveChanged;

        // ════════════════════════════════════════════════════════════════════
        // FIELDS
        // ════════════════════════════════════════════════════════════════════

        private Rect _paintRect;                        // pixel rect where the grid/curve is drawn
        private Func<Vector2, SKPoint>? _toPx;          // normalized to pixel transform
        private Func<SKPoint, Vector2>? _fromPx;        // pixel to normalized transform
        private Grab _grab = Grab.None;                 // currently grabbed control point
        private uint _pid;                              // pointer ID for tracking

        // ════════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initializes a new triangle curve editor control.
        /// </summary>
        public TriangleCurveEditor()
        {
            InitializeComponent();
            SizeChanged += (_, __) => Canvas.Invalidate();
            Loaded += (_, __) => Canvas.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        // PUBLIC API - CURVE EVALUATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Evaluates the curve for a given X coordinate.
        /// </summary>
        /// <param name="x">X coordinate in editor space [-1, 1].</param>
        /// <returns>Y coordinate in editor space [0, 1].</returns>
        public double EvaluateX(double x)
        {
            x = Math.Clamp(x, -1.0, 1.0);

            if (x <= 0.0)
            {
                // Left segment: Start → Mid
                float u = (float)((x + 1.0) / 1.0); // [-1, 0] → [0, 1]
                var S = new Vector2(-1f, 0f);
                var M = new Vector2(0f, 1f);
                return Cubic(S, Start_out, Mid_left, M, u).Y;
            }
            else
            {
                // Right segment: Mid → End
                float u = (float)(x / 1.0); // (0, 1] → [0, 1]
                var M = new Vector2(0f, 1f);
                var E = new Vector2(1f, 0f);
                return Cubic(M, Mid_right, End_in, E, u).Y;
            }
        }

        /// <summary>
        /// Evaluates the curve using a normalized parameter [0, 1].
        /// Convenience method that converts t to editor space X coordinate.
        /// </summary>
        /// <param name="t">Normalized parameter [0, 1].</param>
        /// <returns>Y coordinate in editor space [0, 1].</returns>
        public double Evaluate01(double t) => EvaluateX(2.0 * t - 1.0);

        /// <summary>
        /// Evaluates the left segment of the curve with normalized parameter.
        /// </summary>
        /// <param name="u">Segment parameter [0, 1] where 0 = Start, 1 = Mid.</param>
        /// <returns>Y coordinate in editor space [0, 1].</returns>
        public double EvaluateLeft01(double u)
        {
            u = Math.Clamp(u, 0.0, 1.0);
            var S = new Vector2(-1f, 0f);
            var M = new Vector2(0f, 1f);
            return Cubic(S, Start_out, Mid_left, M, (float)u).Y;
        }

        /// <summary>
        /// Evaluates the right segment of the curve with normalized parameter.
        /// </summary>
        /// <param name="u">Segment parameter [0, 1] where 0 = Mid, 1 = End.</param>
        /// <returns>Y coordinate in editor space [0, 1].</returns>
        public double EvaluateRight01(double u)
        {
            u = Math.Clamp(u, 0.0, 1.0);
            var M = new Vector2(0f, 1f);
            var E = new Vector2(1f, 0f);
            return Cubic(M, Mid_right, End_in, E, (float)u).Y;
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles canvas paint operations to render the grid, curve, and control points.
        /// </summary>
        private void Canvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            float W = e.Info.Width, H = e.Info.Height;

            // Everything draws inside this inset rect (leaves space for rings)
            _paintRect = new Rect(INSET, INSET, Math.Max(1, W - 2 * INSET), Math.Max(1, H - 2 * INSET));

            // Setup coordinate space transforms
            // Normalized space: X ∈ [-1, 1], Y ∈ [0, 1] ↔ pixels inside _paintRect
            _toPx = n => new SKPoint(
                (float)(_paintRect.X + (n.X + 1f) * 0.5f * _paintRect.Width),
                (float)(_paintRect.Y + (1f - n.Y) * _paintRect.Height));

            _fromPx = p =>
            {
                // Clamp to paint rect and invert the transforms
                float nx = (float)Math.Clamp((p.X - _paintRect.X) / Math.Max(1, _paintRect.Width), 0, 1);
                float ny = (float)Math.Clamp((p.Y - _paintRect.Y) / Math.Max(1, _paintRect.Height), 0, 1);
                return new Vector2(nx * 2f - 1f, 1f - ny);
            };

            // Draw grid background
            DrawGrid(canvas, (float)_paintRect.Width, (float)_paintRect.Height, _paintRect);

            // Fixed anchor positions
            var S = new Vector2(-1f, 0f);
            var M = new Vector2(0f, 1f);
            var E = new Vector2(1f, 0f);

            // Draw guide lines from anchors to control points
            using (var linePaint = new SKPaint { Color = SKColors.DimGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
            {
                canvas.DrawLine(_toPx(S), _toPx(Start_out), linePaint);
                canvas.DrawLine(_toPx(M), _toPx(Mid_left), linePaint);
                canvas.DrawLine(_toPx(M), _toPx(Mid_right), linePaint);
                canvas.DrawLine(_toPx(E), _toPx(End_in), linePaint);
            }

            // Draw curve segments
            using (var curvePaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true })
            {
                // Draw left curve segment (Start → Mid)
                using (var path = new SKPath())
                {
                    path.MoveTo(_toPx(S));
                    path.CubicTo(_toPx(Start_out), _toPx(Mid_left), _toPx(M));
                    canvas.DrawPath(path, curvePaint);
                }

                // Draw right curve segment (Mid → End)
                using (var path = new SKPath())
                {
                    path.MoveTo(_toPx(M));
                    path.CubicTo(_toPx(Mid_right), _toPx(End_in), _toPx(E));
                    canvas.DrawPath(path, curvePaint);
                }
            }

            // Draw control point handles (draggable)
            DrawHandle(canvas, _toPx(Start_out));
            DrawHandle(canvas, _toPx(Mid_left));
            DrawHandle(canvas, _toPx(Mid_right));
            DrawHandle(canvas, _toPx(End_in));

            // Draw locked anchors (not draggable)
            DrawAnchor(canvas, _toPx(S));
            DrawAnchor(canvas, _toPx(M));
            DrawAnchor(canvas, _toPx(E));
        }

        /// <summary>
        /// Draws the background grid with axes and guide lines.
        /// </summary>
        private static void DrawGrid(SKCanvas canvas, float w, float h, Rect r)
        {
            // Clear whole control with dark background
            canvas.Clear(new SKColor(26, 26, 26));

            // Outer panel border
            using (var borderPaint = new SKPaint { Color = new SKColor(255, 255, 255, 60), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
            {
                canvas.DrawRect(new SKRect(0, 0, (float)(r.X + w + INSET), (float)(r.Y + h + INSET)), borderPaint);
            }

            // Inner paint rect border
            using (var innerBorderPaint = new SKPaint { Color = new SKColor(255, 255, 255, 80), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
            {
                canvas.DrawRect(new SKRect((float)r.X, (float)r.Y, (float)(r.X + r.Width), (float)(r.Y + r.Height)), innerBorderPaint);
            }

            var minor = new SKColor(255, 255, 255, 32);
            var axis = new SKColor(255, 255, 255, 90);

            // Draw 10x10 grid
            using (var minorPaint = new SKPaint { Color = minor, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
            {
                for (int i = 1; i < 10; i++)
                {
                    float x = (float)r.X + i * (float)r.Width / 10f;
                    float y = (float)r.Y + i * (float)r.Height / 10f;
                    canvas.DrawLine(x, (float)r.Y, x, (float)(r.Y + r.Height), minorPaint);
                    canvas.DrawLine((float)r.X, y, (float)(r.X + r.Width), y, minorPaint);
                }
            }

            // Draw major axes
            using (var axisPaint = new SKPaint { Color = axis, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true })
            {
                canvas.DrawLine((float)(r.X + r.Width * 0.5), (float)r.Y, (float)(r.X + r.Width * 0.5), (float)(r.Y + r.Height), axisPaint); // X = 0
            }

            using (var axisPaint = new SKPaint { Color = axis, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true })
            {
                canvas.DrawLine((float)r.X, (float)(r.Y + r.Height), (float)(r.X + r.Width), (float)(r.Y + r.Height), axisPaint); // Y = 0
                canvas.DrawLine((float)r.X, (float)r.Y, (float)(r.X + r.Width), (float)r.Y, axisPaint);                           // Y = 1
            }
        }

        /// <summary>
        /// Draws a draggable control point handle as a ring.
        /// </summary>
        private static void DrawHandle(SKCanvas canvas, SKPoint p)
        {
            using (var outerPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true })
            {
                canvas.DrawCircle(p, HANDLE_OUT, outerPaint);
            }
            using (var innerPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f, IsAntialias = true })
            {
                canvas.DrawCircle(p, HANDLE_IN, innerPaint);
            }
        }

        /// <summary>
        /// Draws a locked anchor point as a filled circle.
        /// </summary>
        private static void DrawAnchor(SKCanvas canvas, SKPoint p)
        {
            using (var fillPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true })
            {
                canvas.DrawCircle(p, HANDLE_IN - 3f, fillPaint);
            }
            using (var strokePaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true })
            {
                canvas.DrawCircle(p, HANDLE_OUT - 3f, strokePaint);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // INPUT HANDLING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles pointer press to begin dragging a control point.
        /// </summary>
        private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _pid = e.Pointer.PointerId;
            var pt = e.GetCurrentPoint(Canvas).Position;
            var P = new SKPoint((float)pt.X, (float)pt.Y);

            (Grab g, float d) best = (Grab.None, float.MaxValue);

            // Test all control points for proximity
            Test(Grab.Start_out, Start_out);
            Test(Grab.Mid_left, Mid_left);
            Test(Grab.Mid_right, Mid_right);
            Test(Grab.End_in, End_in);

            if (best.g != Grab.None && best.d <= HANDLE_OUT * 1.4f)
            {
                _grab = best.g;
                Canvas.CapturePointer(e.Pointer);
                UpdateDrag(P);
                e.Handled = true;
            }

            void Test(Grab g, Vector2 n)
            {
                if (_toPx == null) return;
                var px = _toPx(n);
                var d = SKPoint.Distance(px, P);
                if (d < best.d) best = (g, d);
            }
        }

        /// <summary>
        /// Handles pointer movement to update dragged control point position.
        /// </summary>
        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_grab == Grab.None || e.Pointer.PointerId != _pid) return;

            var pt = e.GetCurrentPoint(Canvas).Position;
            UpdateDrag(new SKPoint((float)pt.X, (float)pt.Y));
        }

        /// <summary>
        /// Handles pointer release to end dragging.
        /// </summary>
        private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_grab == Grab.None || e.Pointer.PointerId != _pid) return;

            _grab = Grab.None;
            Canvas.ReleasePointerCaptures();
        }

        /// <summary>
        /// Updates the position of the currently dragged control point.
        /// Constrains X to the correct half of the editor space and Y to [0, 1].
        /// </summary>
        private void UpdateDrag(SKPoint px)
        {
            if (_fromPx == null) return;
            var n = _fromPx(px);

            // Constrain X to the correct side, Y to [0, 1]
            switch (_grab)
            {
                case Grab.Start_out:
                    Start_out = new Vector2(MathF.Min(n.X, 0f), Math.Clamp(n.Y, 0f, 1f));
                    break;
                case Grab.Mid_left:
                    Mid_left = new Vector2(MathF.Min(n.X, 0f), Math.Clamp(n.Y, 0f, 1f));
                    break;
                case Grab.Mid_right:
                    Mid_right = new Vector2(MathF.Max(n.X, 0f), Math.Clamp(n.Y, 0f, 1f));
                    break;
                case Grab.End_in:
                    End_in = new Vector2(MathF.Max(n.X, 0f), Math.Clamp(n.Y, 0f, 1f));
                    break;
            }

            Canvas.Invalidate();
            CurveChanged?.Invoke(this, EventArgs.Empty);
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS - CUBIC BEZIER MATH
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Evaluates a cubic Bézier curve at parameter t.
        /// </summary>
        /// <param name="p0">Start point.</param>
        /// <param name="p1">First control point.</param>
        /// <param name="p2">Second control point.</param>
        /// <param name="p3">End point.</param>
        /// <param name="t">Parameter [0, 1].</param>
        /// <returns>Point on the curve.</returns>
        private static Vector2 Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1 - t;
            return (u * u * u) * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
        }
    }
}