using System;
using PixlPunkt.Core.Viewport;
using Windows.Foundation;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;
using static PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem;

namespace PixlPunkt.UI.CanvasHost.Selection
{
    /// <summary>
    /// Handles hit testing for selection handles, rotation handles, pivot, and selection body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionHitTesting provides coordinate-space-aware hit detection for interactive selection elements:
    /// - Scale handles (8 points: corners and edge midpoints)
    /// - Rotation handles (8 points outside the selection)
    /// - Pivot handle (center or custom position)
    /// - Selection body (inside the transformed selection area)
    /// </para>
    /// <para>
    /// <strong>Coordinate Spaces</strong>: Hit testing operates in view coordinates but accounts for
    /// document-space transformations (rotation, scale) via the ZoomController.
    /// </para>
    /// </remarks>
    public sealed class SelectionHitTesting
    {
        private readonly SelectionSubsystem _state;

        // External dependencies
        private Func<ZoomController>? _getZoom;
        private Func<Rect>? _getDestRect;
        private Func<double>? _getScale;

        /// <summary>
        /// Gets or sets the function to retrieve the zoom controller.
        /// </summary>
        public Func<ZoomController>? GetZoom
        {
            get => _getZoom;
            set => _getZoom = value;
        }

        /// <summary>
        /// Gets or sets the function to retrieve the destination rect.
        /// </summary>
        public Func<Rect>? GetDestRect
        {
            get => _getDestRect;
            set => _getDestRect = value;
        }

        /// <summary>
        /// Gets or sets the function to retrieve the zoom scale.
        /// </summary>
        public Func<double>? GetScale
        {
            get => _getScale;
            set => _getScale = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionHitTesting"/> class.
        /// </summary>
        /// <param name="state">The selection subsystem state.</param>
        public SelectionHitTesting(SelectionSubsystem state)
        {
            _state = state;
        }

        /// <summary>
        /// Hit tests for scale handles.
        /// </summary>
        /// <param name="viewPt">Point in view coordinates.</param>
        /// <returns>The handle that was hit, or None.</returns>
        public SelHandle HitTestHandle(Point viewPt)
        {
            if (_state.State != SelectionState.Armed) return SelHandle.None;

            var dest = _getDestRect?.Invoke() ?? new Rect();
            double s = _getScale?.Invoke() ?? 1.0;

            // Use current scaled dimensions and position
            int handleW = (int)Math.Round((_state.OrigW > 0 ? _state.OrigW : _state.BufferWidth) * _state.ScaleX);
            int handleH = (int)Math.Round((_state.OrigH > 0 ? _state.OrigH : _state.BufferHeight) * _state.ScaleY);

            float selX = _state.Floating ? _state.FloatX : _state.Rect.X;
            float selY = _state.Floating ? _state.FloatY : _state.Rect.Y;

            float x = (float)(dest.X + selX * s);
            float y = (float)(dest.Y + selY * s);
            float w = (float)(handleW * s);
            float h = (float)(handleH * s);

            float cx = x + w / 2f;
            float cy = y + h / 2f;

            float rad = (float)(_state.CumulativeAngleDeg * Math.PI / 180.0);

            var pts = new (SelHandle h, float px, float py)[]
            {
                (SelHandle.NW, x, y),
                (SelHandle.N,  x + w / 2, y),
                (SelHandle.NE, x + w, y),
                (SelHandle.E,  x + w, y + h / 2),
                (SelHandle.SE, x + w, y + h),
                (SelHandle.S,  x + w / 2, y + h),
                (SelHandle.SW, x, y + h),
                (SelHandle.W,  x, y + h / 2),
            };

            float pad = HANDLE_HIT_PAD;
            foreach (var (e, px, py) in pts)
            {
                var rp = RotateAround(px, py, cx, cy, rad);
                if (viewPt.X >= rp.x - pad && viewPt.X <= rp.x + pad &&
                    viewPt.Y >= rp.y - pad && viewPt.Y <= rp.y + pad)
                    return e;
            }

            return SelHandle.None;
        }

        /// <summary>
        /// Hit tests for rotation handles.
        /// </summary>
        /// <param name="viewPt">Point in view coordinates.</param>
        /// <returns>The rotation handle that was hit, or None.</returns>
        public RotHandle HitTestRotateHandle(Point viewPt)
        {
            if (_state.State != SelectionState.Armed) return RotHandle.None;

            var dest = _getDestRect?.Invoke() ?? new Rect();
            double s = _getScale?.Invoke() ?? 1.0;

            int handleW = (int)Math.Round((_state.OrigW > 0 ? _state.OrigW : _state.BufferWidth) * _state.ScaleX);
            int handleH = (int)Math.Round((_state.OrigH > 0 ? _state.OrigH : _state.BufferHeight) * _state.ScaleY);

            float selX = _state.Floating ? _state.FloatX : _state.Rect.X;
            float selY = _state.Floating ? _state.FloatY : _state.Rect.Y;

            float x = (float)(dest.X + selX * s);
            float y = (float)(dest.Y + selY * s);
            float w = (float)(handleW * s);
            float h = (float)(handleH * s);

            float cx = x + w / 2f;
            float cy = y + h / 2f;

            float off = ROT_HANDLE_OFFSET;
            float radR = ROT_HANDLE_RADIUS;
            float rad = (float)(_state.CumulativeAngleDeg * Math.PI / 180.0);

            var pts = new (RotHandle h, float px, float py)[]
            {
                (RotHandle.RNW, x - off,     y - off),
                (RotHandle.RN,  x + w/2,     y - off),
                (RotHandle.RNE, x + w + off, y - off),
                (RotHandle.RE,  x + w + off, y + h/2),
                (RotHandle.RSE, x + w + off, y + h + off),
                (RotHandle.RS,  x + w/2,     y + h + off),
                (RotHandle.RSW, x - off,     y + h + off),
                (RotHandle.RW,  x - off,     y + h/2),
            };

            foreach (var (e, px, py) in pts)
            {
                var rp = RotateAround(px, py, cx, cy, rad);
                if (Math.Abs(viewPt.X - rp.x) <= radR && Math.Abs(viewPt.Y - rp.y) <= radR)
                    return e;
            }

            return RotHandle.None;
        }

        /// <summary>
        /// Hit tests for the pivot handle.
        /// </summary>
        /// <param name="viewPt">Point in view coordinates.</param>
        /// <returns>True if the pivot was hit.</returns>
        public bool HitTestPivotHandle(Point viewPt)
        {
            if (_state.State != SelectionState.Armed) return false;
            if (!_state.Floating && !_state.Active) return false;

            var dest = _getDestRect?.Invoke() ?? new Rect();
            double scale = _getScale?.Invoke() ?? 1.0;
            var (pivotX, pivotY) = GetPivotPositionView(dest, scale);

            double dx = viewPt.X - pivotX;
            double dy = viewPt.Y - pivotY;
            return (dx * dx + dy * dy) <= (PIVOT_HIT_RADIUS * PIVOT_HIT_RADIUS);
        }

        /// <summary>
        /// Tests if a view-space point is inside the selection body.
        /// Supports selections that are partially or fully off-canvas.
        /// </summary>
        /// <param name="viewPt">Point in view coordinates.</param>
        /// <returns>True if the point is inside the selection.</returns>
        public bool IsInsideSelectionBodyInView(Point viewPt)
        {
            if (!_state.Active) return false;

            var dest = _getDestRect?.Invoke() ?? new Rect();
            double s = _getScale?.Invoke() ?? 1.0;

            // Get selection rect in doc space (can have negative coordinates when off-canvas)
            var rDoc = _state.Floating
                ? CreateRect(_state.FloatX, _state.FloatY, _state.ScaledW, _state.ScaledH)
                : Normalize(_state.Rect);

            // Convert to view space - note that x/y can be negative relative to canvas origin
            float x = (float)(dest.X + rDoc.X * s);
            float y = (float)(dest.Y + rDoc.Y * s);
            float w = (float)(rDoc.Width * s);
            float h = (float)(rDoc.Height * s);

            var rect = new Rect(x, y, w, h);
            return rect.Contains(viewPt);
        }

        /// <summary>
        /// Tests if a document-space point is inside the transformed selection.
        /// Supports selections that are partially or fully off-canvas.
        /// </summary>
        /// <param name="docX">X coordinate in document space.</param>
        /// <param name="docY">Y coordinate in document space.</param>
        /// <returns>True if the point is inside the selection.</returns>
        public bool IsInsideTransformedSelection(int docX, int docY)
        {
            if (!_state.Active) return false;

            // Check preview buffer if we have one (during scale/rotate transforms)
            if (_state.Floating && _state.PreviewBuf != null && _state.PreviewW > 0 && _state.PreviewH > 0)
            {
                float pivotX = _state.OrigCenterX != 0 ? _state.OrigCenterX : (_state.FloatX + _state.BufferWidth / 2f);
                float pivotY = _state.OrigCenterY != 0 ? _state.OrigCenterY : (_state.FloatY + _state.BufferHeight / 2f);
                float bufferLeft = pivotX - _state.PreviewW / 2f;
                float bufferTop = pivotY - _state.PreviewH / 2f;
                int localX = (int)(docX - bufferLeft);
                int localY = (int)(docY - bufferTop);
                if (localX >= 0 && localX < _state.PreviewW && localY >= 0 && localY < _state.PreviewH)
                    return _state.PreviewBuf[(localY * _state.PreviewW + localX) * 4 + 3] > 0;
                return false;
            }

            // Check floating buffer - supports off-canvas positions
            if (_state.Floating && _state.Buffer != null)
            {
                // Convert doc coords to local buffer coords
                // Note: FloatX/FloatY can be negative when selection is dragged off-canvas
                int localX = docX - _state.FloatX;
                int localY = docY - _state.FloatY;

                // Check if within buffer bounds (local coords are always 0 to BufferWidth/Height)
                if (localX >= 0 && localX < _state.BufferWidth && localY >= 0 && localY < _state.BufferHeight)
                    return _state.Buffer[(localY * _state.BufferWidth + localX) * 4 + 3] > 0;
                return false;
            }

            // Check region - supports off-canvas via world-space offset
            return _state.Region.Contains(docX, docY);
        }

        /// <summary>
        /// Tests if a document-space point is inside the active (non-floating) selection rect.
        /// </summary>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns>True if the point is inside the selection.</returns>
        public bool IsInActiveSelection(int x, int y)
        {
            if (!_state.Active) return true;
            if (_state.Floating) return false;
            var r = Normalize(_state.Rect);
            return x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height;
        }

        /// <summary>
        /// Gets the pivot position in view space.
        /// </summary>
        /// <param name="dest">The destination rect.</param>
        /// <param name="scale">The zoom scale.</param>
        /// <returns>The pivot position in view coordinates.</returns>
        public (float X, float Y) GetPivotPositionView(Rect dest, double scale)
        {
            if (_state.Drag == SelDrag.Rotate)
            {
                return ((float)(_state.RotFixedPivotX * scale + dest.X),
                        (float)(_state.RotFixedPivotY * scale + dest.Y));
            }

            var (docX, docY) = GetPivotPositionDoc();
            return ((float)(dest.X + docX * scale), (float)(dest.Y + docY * scale));
        }

        /// <summary>
        /// Gets the pivot position in document space.
        /// </summary>
        /// <returns>The pivot position in document coordinates.</returns>
        public (double X, double Y) GetPivotPositionDoc()
        {
            double centerX = _state.OrigCenterX != 0 ? _state.OrigCenterX : (_state.FloatX + _state.ScaledW / 2.0);
            double centerY = _state.OrigCenterY != 0 ? _state.OrigCenterY : (_state.FloatY + _state.ScaledH / 2.0);

            if (!_state.PivotCustom || (_state.PivotOffsetX == 0 && _state.PivotOffsetY == 0))
                return (centerX, centerY);

            // Transform pivot offset from local to global space
            double radians = _state.CumulativeAngleDeg * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            double globalOffsetX = _state.PivotOffsetX * cos - _state.PivotOffsetY * sin;
            double globalOffsetY = _state.PivotOffsetX * sin + _state.PivotOffsetY * cos;

            return (centerX + globalOffsetX, centerY + globalOffsetY);
        }
    }
}
