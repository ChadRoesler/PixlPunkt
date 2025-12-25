using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Tools.Selection;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using static PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem;

namespace PixlPunkt.UI.CanvasHost.Selection
{
    /// <summary>
    /// Handles rendering of selection visuals including marching ants, handles, and floating selections.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionRenderer draws all visual elements of the selection system:
    /// - Marching ants outline (animated dashed border)
    /// - Scale handles (8 points at corners and edges)
    /// - Rotation handles (8 points outside the selection)
    /// - Pivot indicator (⊕ style crosshair)
    /// - Floating selection buffer with transforms
    /// - Debug visualization for hit areas
    /// </para>
    /// <para>
    /// <strong>Rendering Order</strong>: Floating buffer → Marching ants → Transform handles
    /// </para>
    /// </remarks>
    public sealed class SelectionRenderer
    {
        private readonly SelectionSubsystem _state;
        private readonly SelectionHitTesting _hitTest;

        // External dependencies
        private Func<Rect>? _getDestRect;
        private Func<double>? _getScale;
        private Func<ISelectionTool?>? _getActiveTool;
        private Func<SelDrag, bool>? _needsContinuousRender;

        /// <summary>Debug overlay for hit areas.</summary>
        public bool DebugShowHit { get; set; } = false;

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
        /// Gets or sets the function to get the active selection tool.
        /// </summary>
        public Func<ISelectionTool?>? GetActiveTool
        {
            get => _getActiveTool;
            set => _getActiveTool = value;
        }

        /// <summary>
        /// Gets or sets the function to check if continuous rendering is needed.
        /// </summary>
        public Func<SelDrag, bool>? NeedsContinuousRender
        {
            get => _needsContinuousRender;
            set => _needsContinuousRender = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionRenderer"/> class.
        /// </summary>
        /// <param name="state">The selection subsystem state.</param>
        /// <param name="hitTest">The hit testing handler.</param>
        public SelectionRenderer(SelectionSubsystem state, SelectionHitTesting hitTest)
        {
            _state = state;
            _hitTest = hitTest;
        }

        /// <summary>
        /// Main draw method for selection visuals.
        /// </summary>
        /// <param name="sender">The canvas control.</param>
        /// <param name="args">Draw event args.</param>
        public void Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var tool = _getActiveTool?.Invoke();
            bool hasCustomPreview = tool?.HasPreview ?? false;
            bool isPaintingSelection = _state.Drag == SelDrag.Marquee && (_needsContinuousRender?.Invoke(_state.Drag) ?? false);
            bool hasFloating = _state.Floating && _state.Buffer != null;

            if (!_state.Active && !_state.HavePreview && !hasCustomPreview && !isPaintingSelection && !hasFloating)
                return;

            var ds = args.DrawingSession;
            var dest = _getDestRect?.Invoke() ?? new Rect();
            double scale = _getScale?.Invoke() ?? 1.0;

            // Draw floating selection
            if (hasFloating)
                DrawFloatingSelection(ds, sender.Device, dest, scale);

            bool isTransforming = _state.Drag == SelDrag.Scale || _state.Drag == SelDrag.Rotate || _state.Drag == SelDrag.Move;

            // Draw marching ants
            if ((_state.Active || isPaintingSelection || hasFloating) && !isTransforming)
                DrawMarchingAnts(ds, dest, scale, animated: true);
            else if ((_state.Active || hasFloating) && isTransforming)
                DrawMarchingAnts(ds, dest, scale, animated: false);

            // Draw transform handles if armed
            if (_state.State == SelectionState.Armed || hasFloating)
                DrawTransformHandles(ds, dest, scale);

            // Draw marquee preview
            if (_state.HavePreview || hasCustomPreview)
                DrawMarqueePreview(ds, dest, scale);

            // Advance marching ants animation
            if ((_state.Active || isPaintingSelection || hasFloating) && !isTransforming)
                _state.AdvanceAnts();
        }

        /// <summary>
        /// Draws the floating selection buffer with transforms.
        /// </summary>
        public void DrawFloatingSelection(CanvasDrawingSession ds, CanvasDevice device, Rect dest, double scale)
        {
            if (_state.Buffer == null) return;

            bool hasScale = Math.Abs(_state.ScaleX - 1.0) > 0.001 || Math.Abs(_state.ScaleY - 1.0) > 0.001;
            bool hasDragRotation = Math.Abs(_state.AngleDeg) > 0.1;
            bool hasCumulativeRotation = Math.Abs(_state.CumulativeAngleDeg) > 0.1;
            bool needsTransform = hasScale || hasDragRotation || hasCumulativeRotation;

            float pivotDocX = _state.OrigCenterX != 0 ? _state.OrigCenterX : (_state.FloatX + _state.BufferWidth / 2f);
            float pivotDocY = _state.OrigCenterY != 0 ? _state.OrigCenterY : (_state.FloatY + _state.BufferHeight / 2f);
            float pivotViewX = (float)(dest.X + pivotDocX * scale);
            float pivotViewY = (float)(dest.Y + pivotDocY * scale);

            if (needsTransform)
            {
                double totalRotation = _state.CumulativeAngleDeg + _state.AngleDeg;
                bool needsRebuild = _state.PreviewBuf == null ||
                    Math.Abs(_state.PreviewScaleX - _state.ScaleX) > 0.001 ||
                    Math.Abs(_state.PreviewScaleY - _state.ScaleY) > 0.001 ||
                    Math.Abs(_state.PreviewAngle - totalRotation) > 0.1 ||
                    _state.PreviewScaleFilter != _state.ScaleFilter ||
                    _state.PreviewRotMode != _state.RotMode;

                if (needsRebuild)
                {
                    var (scaledBuf, scaledW, scaledH) = BuildScaledBuffer(
                        _state.Buffer, _state.BufferWidth, _state.BufferHeight,
                        _state.ScaleX, _state.ScaleY, _state.ScaleFilter);
                    var (rotBuf, rotW, rotH) = BuildRotatedBuffer(
                        scaledBuf, scaledW, scaledH,
                        totalRotation, _state.RotMode);

                    // Premultiply alpha for correct Win2D rendering
                    // Win2D's B8G8R8A8UIntNormalized expects premultiplied alpha
                    PremultiplyAlpha(rotBuf, rotW, rotH);

                    _state.PreviewBuf = rotBuf;
                    _state.PreviewW = rotW;
                    _state.PreviewH = rotH;
                    _state.PreviewScaleX = _state.ScaleX;
                    _state.PreviewScaleY = _state.ScaleY;
                    _state.PreviewAngle = totalRotation;
                    _state.PreviewScaleFilter = _state.ScaleFilter;
                    _state.PreviewRotMode = _state.RotMode;
                }

                using var bitmap = CanvasBitmap.CreateFromBytes(device, _state.PreviewBuf,
                    _state.PreviewW, _state.PreviewH, DirectXPixelFormat.B8G8R8A8UIntNormalized, 96.0f);
                float drawX = pivotViewX - (float)(_state.PreviewW * scale / 2.0);
                float drawY = pivotViewY - (float)(_state.PreviewH * scale / 2.0);
                ds.DrawImage(bitmap,
                    new Rect(drawX, drawY, _state.PreviewW * scale, _state.PreviewH * scale),
                    new Rect(0, 0, _state.PreviewW, _state.PreviewH),
                    0.85f, CanvasImageInterpolation.NearestNeighbor);
            }
            else
            {
                _state.PreviewBuf = null;

                // Create a premultiplied copy for rendering
                // Win2D's B8G8R8A8UIntNormalized expects premultiplied alpha for correct
                // compositing of semi-transparent pixels (from RotSprite or bilinear transforms)
                var renderBuf = new byte[_state.Buffer.Length];
                Buffer.BlockCopy(_state.Buffer, 0, renderBuf, 0, _state.Buffer.Length);
                PremultiplyAlpha(renderBuf, _state.BufferWidth, _state.BufferHeight);

                using var bitmap = CanvasBitmap.CreateFromBytes(device, renderBuf,
                    _state.BufferWidth, _state.BufferHeight, DirectXPixelFormat.B8G8R8A8UIntNormalized, 96.0f);
                float drawX = (float)(dest.X + _state.FloatX * scale);
                float drawY = (float)(dest.Y + _state.FloatY * scale);
                ds.DrawImage(bitmap,
                    new Rect(drawX, drawY, _state.BufferWidth * scale, _state.BufferHeight * scale),
                    new Rect(0, 0, _state.BufferWidth, _state.BufferHeight),
                    0.85f, CanvasImageInterpolation.NearestNeighbor);
            }
        }

        /// <summary>
        /// Converts straight alpha to premultiplied alpha in-place for Win2D rendering.
        /// </summary>
        private static void PremultiplyAlpha(byte[] buf, int w, int h)
        {
            int len = w * h * 4;
            for (int i = 0; i < len; i += 4)
            {
                byte a = buf[i + 3];
                if (a == 0)
                {
                    // Fully transparent - set RGB to 0 for correct premultiplied representation
                    buf[i + 0] = 0;
                    buf[i + 1] = 0;
                    buf[i + 2] = 0;
                }
                else if (a < 255)
                {
                    // Semi-transparent - premultiply RGB by alpha
                    double alpha = a / 255.0;
                    buf[i + 0] = (byte)Math.Round(buf[i + 0] * alpha); // B
                    buf[i + 1] = (byte)Math.Round(buf[i + 1] * alpha); // G
                    buf[i + 2] = (byte)Math.Round(buf[i + 2] * alpha); // R
                }
                // Fully opaque (a == 255) - no change needed
            }
        }

        /// <summary>
        /// Draws transform handles (scale and rotation).
        /// </summary>
        public void DrawTransformHandles(CanvasDrawingSession ds, Rect dest, double scale)
        {
            int handleW = (int)Math.Round((_state.OrigW > 0 ? _state.OrigW : _state.BufferWidth) * _state.ScaleX);
            int handleH = (int)Math.Round((_state.OrigH > 0 ? _state.OrigH : _state.BufferHeight) * _state.ScaleY);
            float selX = _state.Floating ? _state.FloatX : _state.Rect.X;
            float selY = _state.Floating ? _state.FloatY : _state.Rect.Y;
            float x = (float)(dest.X + selX * scale);
            float y = (float)(dest.Y + selY * scale);
            float w = (float)(handleW * scale);
            float h = (float)(handleH * scale);
            float cx = x + w / 2f;
            float cy = y + h / 2f;
            float totalAngle = (float)(_state.CumulativeAngleDeg + _state.AngleDeg);
            float rad = (float)(totalAngle * Math.PI / 180.0);

            // Colors for professional look
            var scaleHandleFill = Colors.White;
            var scaleHandleStroke = Windows.UI.Color.FromArgb(255, 40, 40, 40);
            var scaleHandleShadow = Windows.UI.Color.FromArgb(80, 0, 0, 0);
            var rotHandleColor = Windows.UI.Color.FromArgb(255, 0, 150, 255); // Bright blue
            var rotHandleStroke = Windows.UI.Color.FromArgb(255, 0, 100, 200);
            var cornerHandleAccent = Windows.UI.Color.FromArgb(255, 60, 60, 60);

            float handleSize = HANDLE_DRAW_SIZE;
            float handleHalf = handleSize / 2f;
            float cornerRadius = 2.5f;

            // Draw scale handles (rounded squares with shadow)
            var scalePoints = new (float px, float py, bool isCorner)[]
            {
                (x, y, true),              // NW - corner
                (x + w / 2, y, false),     // N - edge
                (x + w, y, true),          // NE - corner
                (x + w, y + h / 2, false), // E - edge
                (x + w, y + h, true),      // SE - corner
                (x + w / 2, y + h, false), // S - edge
                (x, y + h, true),          // SW - corner
                (x, y + h / 2, false)      // W - edge
            };

            foreach (var (px, py, isCorner) in scalePoints)
            {
                var rp = RotateAround(px, py, cx, cy, rad);

                // Draw shadow (offset down-right)
                using (var shadowGeo = CanvasGeometry.CreateRoundedRectangle(ds,
                    rp.x - handleHalf + 1.5f, rp.y - handleHalf + 1.5f,
                    handleSize, handleSize, cornerRadius, cornerRadius))
                {
                    ds.FillGeometry(shadowGeo, scaleHandleShadow);
                }

                // Draw handle body
                using (var handleGeo = CanvasGeometry.CreateRoundedRectangle(ds,
                    rp.x - handleHalf, rp.y - handleHalf,
                    handleSize, handleSize, cornerRadius, cornerRadius))
                {
                    ds.FillGeometry(handleGeo, scaleHandleFill);
                    ds.DrawGeometry(handleGeo, isCorner ? cornerHandleAccent : scaleHandleStroke, 1.5f);
                }

                // Corner handles get a subtle inner accent
                if (isCorner)
                {
                    float innerSize = handleSize - 4f;
                    float innerHalf = innerSize / 2f;
                    using (var innerGeo = CanvasGeometry.CreateRoundedRectangle(ds,
                        rp.x - innerHalf, rp.y - innerHalf,
                        innerSize, innerSize, 1.5f, 1.5f))
                    {
                        ds.DrawGeometry(innerGeo, Windows.UI.Color.FromArgb(40, 0, 0, 0), 0.5f);
                    }
                }
            }

            // Draw rotation handles (arc-arrow style)
            float rotOff = ROT_HANDLE_OFFSET;
            float rotRadius = ROT_HANDLE_RADIUS;

            var rotPoints = new (float px, float py, float arrowAngle)[]
            {
                (x - rotOff, y - rotOff, -135f),       // RNW
                (x + w / 2, y - rotOff, -90f),        // RN
                (x + w + rotOff, y - rotOff, -45f),   // RNE
                (x + w + rotOff, y + h / 2, 0f),      // RE
                (x + w + rotOff, y + h + rotOff, 45f),// RSE
                (x + w / 2, y + h + rotOff, 90f),     // RS
                (x - rotOff, y + h + rotOff, 135f),   // RSW
                (x - rotOff, y + h / 2, 180f)         // RW
            };

            foreach (var (px, py, arrowAngle) in rotPoints)
            {
                var rp = RotateAround(px, py, cx, cy, rad);
                DrawRotationHandle(ds, rp.x, rp.y, rotRadius, arrowAngle + totalAngle * (180f / (float)Math.PI), rotHandleColor, rotHandleStroke);
            }

            // Draw pivot indicator (professional crosshair style)
            var (pivotX, pivotY) = _hitTest.GetPivotPositionView(dest, scale);
            DrawPivotIndicator(ds, pivotX, pivotY);

            // Draw debug hit areas if enabled
            if (DebugShowHit)
                DrawDebugHitAreas(ds, x, y, w, h);
        }

        /// <summary>
        /// Draws a rotation handle with a curved arrow appearance.
        /// </summary>
        private static void DrawRotationHandle(CanvasDrawingSession ds, float cx, float cy, float radius, float angleDeg, Windows.UI.Color fill, Windows.UI.Color stroke)
        {
            // Outer ring
            ds.DrawCircle(cx, cy, radius, stroke, 2f);

            // Arc arrow indicator
            float arrowRad = (angleDeg - 90) * (float)Math.PI / 180f;
            float arcRadius = radius * 0.6f;

            // Draw a small arc with arrow
            float startAngle = arrowRad - 0.8f;
            float endAngle = arrowRad + 0.8f;

            // Arc path
            using (var builder = new CanvasPathBuilder(ds))
            {
                float x1 = cx + arcRadius * (float)Math.Cos(startAngle);
                float y1 = cy + arcRadius * (float)Math.Sin(startAngle);
                builder.BeginFigure(x1, y1);

                // Arc to end
                float x2 = cx + arcRadius * (float)Math.Cos(endAngle);
                float y2 = cy + arcRadius * (float)Math.Sin(endAngle);
                builder.AddArc(new System.Numerics.Vector2(x2, y2), arcRadius, arcRadius, endAngle - startAngle, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);

                builder.EndFigure(CanvasFigureLoop.Open);

                using (var arcGeo = CanvasGeometry.CreatePath(builder))
                {
                    ds.DrawGeometry(arcGeo, fill, 2f);
                }
            }

            // Arrow head at the end of the arc
            float arrowSize = 3.5f;
            float arrowEndX = cx + arcRadius * (float)Math.Cos(endAngle);
            float arrowEndY = cy + arcRadius * (float)Math.Sin(endAngle);

            // Arrow direction (tangent to arc, pointing clockwise)
            float tangentAngle = endAngle + (float)Math.PI / 2f;
            float ax1 = arrowEndX - arrowSize * (float)Math.Cos(tangentAngle - 0.5f);
            float ay1 = arrowEndY - arrowSize * (float)Math.Sin(tangentAngle - 0.5f);
            float ax2 = arrowEndX - arrowSize * (float)Math.Cos(tangentAngle + 0.5f);
            float ay2 = arrowEndY - arrowSize * (float)Math.Sin(tangentAngle + 0.5f);

            ds.FillCircle(cx, cy, 2f, fill); // Center dot
        }

        /// <summary>
        /// Draws the pivot indicator with a professional crosshair style.
        /// </summary>
        private static void DrawPivotIndicator(CanvasDrawingSession ds, float px, float py)
        {
            float pivotSize = PIVOT_DRAW_SIZE;

            // Shadow
            var shadowColor = Windows.UI.Color.FromArgb(100, 0, 0, 0);
            ds.FillCircle(px + 1, py + 1, pivotSize + 1, shadowColor);

            // Outer ring (orange/amber)
            var outerColor = Windows.UI.Color.FromArgb(255, 255, 140, 0);
            ds.FillCircle(px, py, pivotSize, outerColor);

            // Inner ring (darker)
            var innerRingColor = Windows.UI.Color.FromArgb(255, 200, 100, 0);
            ds.DrawCircle(px, py, pivotSize - 2, innerRingColor, 1.5f);

            // Center dot
            ds.FillCircle(px, py, 2.5f, Colors.White);

            // Crosshair lines
            float crossLen = pivotSize + 3f;
            float crossGap = pivotSize - 1f;

            // Horizontal line (with gap in center)
            ds.DrawLine(px - crossLen, py, px - crossGap, py, Colors.White, 1.5f);
            ds.DrawLine(px + crossGap, py, px + crossLen, py, Colors.White, 1.5f);

            // Vertical line (with gap in center)
            ds.DrawLine(px, py - crossLen, px, py - crossGap, Colors.White, 1.5f);
            ds.DrawLine(px, py + crossGap, px, py + crossLen, Colors.White, 1.5f);

            // Dark outline for crosshair
            ds.DrawLine(px - crossLen - 0.5f, py, px - crossGap, py, Colors.Black, 0.5f);
            ds.DrawLine(px + crossGap, py, px + crossLen + 0.5f, py, Colors.Black, 0.5f);
            ds.DrawLine(px, py - crossLen - 0.5f, px, py - crossGap, Colors.Black, 0.5f);
            ds.DrawLine(px, py + crossGap, px, py + crossLen + 0.5f, Colors.Black, 0.5f);
        }

        /// <summary>
        /// Draws the debug hit areas visualization.
        /// </summary>
        private void DrawDebugHitAreas(CanvasDrawingSession ds, float x, float y, float w, float h)
        {
            float pad = HANDLE_HIT_PAD;
            float off = ROT_HANDLE_OFFSET;
            float rr = ROT_HANDLE_RADIUS;

            float cx = x + w * 0.5f, cy = y + h * 0.5f;
            float rad = (float)(_state.CumulativeAngleDeg * Math.PI / 180.0);

            // Scale handle hit rects (green)
            var gFill = Windows.UI.Color.FromArgb(60, 0, 255, 0);
            var gLine = Windows.UI.Color.FromArgb(200, 0, 200, 0);

            var sPts = new (float px, float py)[]
            {
                (x, y), (x + w/2, y), (x + w, y), (x + w, y + h/2),
                (x + w, y + h), (x + w/2, y + h), (x, y + h), (x, y + h/2),
            };

            foreach (var (px, py) in sPts)
            {
                var rp = RotateAround(px, py, cx, cy, rad);
                var r = new Rect(rp.x - pad, rp.y - pad, pad * 2, pad * 2);
                ds.FillRectangle(r, gFill);
                ds.DrawRectangle(r, gLine, 1f);
            }

            // Rotation handle hit rects (red)
            var rFill = Windows.UI.Color.FromArgb(80, 255, 0, 0);
            var rLine = Windows.UI.Color.FromArgb(200, 255, 0, 0);

            var rPts = new (float px, float py)[]
            {
                (x - off, y - off), (x + w/2, y - off), (x + w + off, y - off), (x + w + off, y + h/2),
                (x + w + off, y + h + off), (x + w/2, y + h + off), (x - off, y + h + off), (x - off, y + h/2),
            };

            foreach (var (px, py) in rPts)
            {
                var rp = RotateAround(px, py, cx, cy, rad);
                var rrBox = new Rect(rp.x - rr, rp.y - rr, rr * 2, rr * 2);
                ds.FillRectangle(rrBox, rFill);
                ds.DrawRectangle(rrBox, rLine, 1f);
            }

            // Near-border expanded rect (magenta)
            var nearFill = Windows.UI.Color.FromArgb(36, 255, 0, 255);
            var nearLine = Windows.UI.Color.FromArgb(180, 255, 0, 255);
            var near = new Rect(x - pad, y - pad, w + pad * 2, h + pad * 2);
            ds.FillRectangle(near, nearFill);
            ds.DrawRectangle(near, nearLine, 1f);
            ds.FillCircle(cx, cy, 2.5f, Colors.Yellow);
            ds.DrawCircle(cx, cy, 5f, Colors.Black, 1f);
        }

        /// <summary>
        /// Draws ants from a buffer by detecting boundary edges.
        /// </summary>
        private void DrawAntsFromBuffer(CanvasDrawingSession ds, byte[] buffer, int bufW, int bufH,
            float offsetX, float offsetY, float scale, bool animated)
        {
            bool IsOpaque(int x, int y) => x >= 0 && y >= 0 && x < bufW && y < bufH && buffer[(y * bufW + x) * 4 + 3] > 0;

            var hEdges = new List<(float x0, float y, float x1)>();
            var vEdges = new List<(float x, float y0, float y1)>();

            for (int y = 0; y < bufH; y++)
            {
                for (int x = 0; x < bufW; x++)
                {
                    if (!IsOpaque(x, y)) continue;
                    if (!IsOpaque(x, y - 1)) hEdges.Add((offsetX + x * scale, offsetY + y * scale, offsetX + (x + 1) * scale));
                    if (!IsOpaque(x, y + 1)) hEdges.Add((offsetX + x * scale, offsetY + (y + 1) * scale, offsetX + (x + 1) * scale));
                    if (!IsOpaque(x - 1, y)) vEdges.Add((offsetX + x * scale, offsetY + y * scale, offsetY + (y + 1) * scale));
                    if (!IsOpaque(x + 1, y)) vEdges.Add((offsetX + (x + 1) * scale, offsetY + y * scale, offsetY + (y + 1) * scale));
                }
            }

            float period = ANTS_ON + ANTS_OFF;
            if (animated)
            {
                foreach (var (x0, y, x1) in hEdges) DrawAntsLineH(ds, x0, y, x1 - x0, _state.AntsPhase, period);
                foreach (var (x, y0, y1) in vEdges) DrawAntsLineV(ds, x, y0, y1 - y0, _state.AntsPhase, period);
            }
            else
            {
                foreach (var (x0, y, x1) in hEdges) ds.DrawLine(x0, y, x1, y, Colors.White, ANTS_THICKNESS);
                foreach (var (x, y0, y1) in vEdges) ds.DrawLine(x, y0, x, y1, Colors.White, ANTS_THICKNESS);
            }
        }

        private void DrawAntsLineH(CanvasDrawingSession ds, float x, float y, float length, float phase, float period)
        {
            for (float pos = -phase; pos < length; pos += period)
            {
                float segStart = Math.Max(0, pos);
                float segEnd = Math.Min(length, pos + ANTS_ON);
                if (segEnd > segStart)
                    ds.DrawLine(x + segStart, y, x + segEnd, y, Colors.White, ANTS_THICKNESS);
                float blackStart = Math.Max(0, pos + ANTS_ON);
                float blackEnd = Math.Min(length, pos + period);
                if (blackEnd > blackStart)
                    ds.DrawLine(x + blackStart, y, x + blackEnd, y, Colors.Black, ANTS_THICKNESS);
            }
        }

        private void DrawAntsLineV(CanvasDrawingSession ds, float x, float y, float length, float phase, float period)
        {
            for (float pos = -phase; pos < length; pos += period)
            {
                float segStart = Math.Max(0, pos);
                float segEnd = Math.Min(length, pos + ANTS_ON);
                if (segEnd > segStart)
                    ds.DrawLine(x, y + segStart, x, y + segEnd, Colors.White, ANTS_THICKNESS);
                float blackStart = Math.Max(0, pos + ANTS_ON);
                float blackEnd = Math.Min(length, pos + period);
                if (blackEnd > blackStart)
                    ds.DrawLine(x, y + blackStart, x, y + blackEnd, Colors.Black, ANTS_THICKNESS);
            }
        }

        /// <summary>
        /// Draws marching ants selection outline.
        /// </summary>
        public void DrawMarchingAnts(CanvasDrawingSession ds, Rect dest, double scale, bool animated)
        {
            bool isPaintingSelection = _state.Drag == SelDrag.Marquee && (_needsContinuousRender?.Invoke(_state.Drag) ?? false);
            bool hasFloating = _state.Floating && _state.Buffer != null;

            if (!_state.Active && !isPaintingSelection && !hasFloating) return;

            bool hasRotation = Math.Abs(_state.CumulativeAngleDeg) > 0.1 || Math.Abs(_state.AngleDeg) > 0.1;
            bool hasScale = Math.Abs(_state.ScaleX - 1.0) > 0.001 || Math.Abs(_state.ScaleY - 1.0) > 0.001;
            bool hasTransform = hasRotation || hasScale || _state.BufferFlipped;

            if (hasFloating)
            {
                if (hasTransform)
                {
                    if (_state.PreviewBuf != null && _state.PreviewW > 0 && _state.PreviewH > 0)
                    {
                        float pivotX = _state.OrigCenterX != 0 ? _state.OrigCenterX : (_state.FloatX + _state.BufferWidth / 2f);
                        float pivotY = _state.OrigCenterY != 0 ? _state.OrigCenterY : (_state.FloatY + _state.BufferHeight / 2f);
                        float cx = (float)(dest.X + pivotX * scale);
                        float cy = (float)(dest.Y + pivotY * scale);
                        float bufferLeft = cx - (float)(_state.PreviewW * scale / 2.0);
                        float bufferTop = cy - (float)(_state.PreviewH * scale / 2.0);

                        DrawAntsFromBuffer(ds, _state.PreviewBuf, _state.PreviewW, _state.PreviewH,
                            bufferLeft, bufferTop, (float)scale, animated);
                    }
                    else if (_state.BufferFlipped && !hasRotation && !hasScale)
                    {
                        float x = (float)(dest.X + _state.FloatX * scale);
                        float y = (float)(dest.Y + _state.FloatY * scale);
                        DrawAntsFromBuffer(ds, _state.Buffer!, _state.BufferWidth, _state.BufferHeight,
                            x, y, (float)scale, animated);
                    }
                    else
                    {
                        int scaledW = Math.Max(1, (int)Math.Round((_state.OrigW > 0 ? _state.OrigW : _state.BufferWidth) * _state.ScaleX));
                        int scaledH = Math.Max(1, (int)Math.Round((_state.OrigH > 0 ? _state.OrigH : _state.BufferHeight) * _state.ScaleY));

                        float pivotX = _state.OrigCenterX != 0 ? _state.OrigCenterX : (_state.FloatX + _state.BufferWidth / 2f);
                        float pivotY = _state.OrigCenterY != 0 ? _state.OrigCenterY : (_state.FloatY + _state.BufferHeight / 2f);
                        float cx = (float)(dest.X + pivotX * scale);
                        float cy = (float)(dest.Y + pivotY * scale);

                        float rectLeft = cx - (float)(scaledW * scale / 2.0);
                        float rectTop = cy - (float)(scaledH * scale / 2.0);
                        float rectW = (float)(scaledW * scale);
                        float rectH = (float)(scaledH * scale);

                        if (hasRotation)
                        {
                            float totalAngle = (float)(_state.CumulativeAngleDeg + _state.AngleDeg);
                            float rad = (float)(totalAngle * Math.PI / 180.0);

                            var corners = new[] {
                                RotateAround(rectLeft, rectTop, cx, cy, rad),
                                RotateAround(rectLeft + rectW, rectTop, cx, cy, rad),
                                RotateAround(rectLeft + rectW, rectTop + rectH, cx, cy, rad),
                                RotateAround(rectLeft, rectTop + rectH, cx, cy, rad)
                            };

                            DrawAntsPolygon(ds, corners, animated);
                        }
                        else
                        {
                            DrawAntsRectangle(ds, rectLeft, rectTop, rectW, rectH, animated);
                        }
                    }
                }
                else
                {
                    float x = (float)(dest.X + _state.FloatX * scale);
                    float y = (float)(dest.Y + _state.FloatY * scale);
                    DrawAntsFromBuffer(ds, _state.Buffer!, _state.BufferWidth, _state.BufferHeight,
                        x, y, (float)scale, animated);
                }
            }
            else if (hasRotation)
            {
                DrawTransformedMarchingAnts(ds, dest, scale, animated);
            }
            else
            {
                if (animated)
                    _state.Region.DrawAnts(ds, dest, scale, _state.AntsPhase, ANTS_ON, ANTS_OFF, ANTS_THICKNESS);
                else
                    DrawSolidOutline(ds, dest, scale);
            }
        }

        /// <summary>
        /// Draws marching ants as a simple rectangle.
        /// </summary>
        private void DrawAntsRectangle(CanvasDrawingSession ds, float x, float y, float w, float h, bool animated)
        {
            if (animated)
            {
                var dashStyle = new CanvasStrokeStyle
                {
                    DashStyle = CanvasDashStyle.Dash,
                    DashOffset = _state.AntsPhase,
                    CustomDashStyle = new float[] { ANTS_ON, ANTS_OFF }
                };
                ds.DrawRectangle(x, y, w, h, Colors.White, ANTS_THICKNESS, dashStyle);

                var dashStyleBlack = new CanvasStrokeStyle
                {
                    DashStyle = CanvasDashStyle.Dash,
                    DashOffset = _state.AntsPhase + ANTS_ON,
                    CustomDashStyle = new float[] { ANTS_ON, ANTS_OFF }
                };
                ds.DrawRectangle(x, y, w, h, Colors.Black, ANTS_THICKNESS, dashStyleBlack);
            }
            else
            {
                ds.DrawRectangle(x, y, w, h, Colors.White, ANTS_THICKNESS);
            }
        }

        /// <summary>
        /// Draws marching ants as a polygon (4 corners).
        /// </summary>
        private void DrawAntsPolygon(CanvasDrawingSession ds, (float x, float y)[] corners, bool animated)
        {
            if (animated)
            {
                var dashStyle = new CanvasStrokeStyle
                {
                    DashStyle = CanvasDashStyle.Dash,
                    DashOffset = _state.AntsPhase,
                    CustomDashStyle = new float[] { ANTS_ON, ANTS_OFF }
                };
                for (int i = 0; i < 4; i++)
                {
                    var p1 = corners[i];
                    var p2 = corners[(i + 1) % 4];
                    ds.DrawLine(p1.x, p1.y, p2.x, p2.y, Colors.White, ANTS_THICKNESS, dashStyle);
                }

                var dashStyleBlack = new CanvasStrokeStyle
                {
                    DashStyle = CanvasDashStyle.Dash,
                    DashOffset = _state.AntsPhase + ANTS_ON,
                    CustomDashStyle = new float[] { ANTS_ON, ANTS_OFF }
                };
                for (int i = 0; i < 4; i++)
                {
                    var p1 = corners[i];
                    var p2 = corners[(i + 1) % 4];
                    ds.DrawLine(p1.x, p1.y, p2.x, p2.y, Colors.Black, ANTS_THICKNESS, dashStyleBlack);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    var p1 = corners[i];
                    var p2 = corners[(i + 1) % 4];
                    ds.DrawLine(p1.x, p1.y, p2.x, p2.y, Colors.White, ANTS_THICKNESS);
                }
            }
        }

        /// <summary>
        /// Draws transformed marching ants from preview buffer.
        /// </summary>
        private void DrawTransformedMarchingAnts(CanvasDrawingSession ds, Rect dest, double scale, bool animated)
        {
            if (_state.PreviewBuf != null && _state.PreviewW > 0 && _state.PreviewH > 0)
            {
                float pivotX = _state.OrigCenterX != 0 ? _state.OrigCenterX : (_state.FloatX + _state.BufferWidth / 2f);
                float pivotY = _state.OrigCenterY != 0 ? _state.OrigCenterY : (_state.FloatY + _state.BufferHeight / 2f);
                float cx = (float)(dest.X + pivotX * scale);
                float cy = (float)(dest.Y + pivotY * scale);
                float bufferLeft = cx - (float)(_state.PreviewW * scale / 2.0);
                float bufferTop = cy - (float)(_state.PreviewH * scale / 2.0);
                DrawAntsFromBuffer(ds, _state.PreviewBuf, _state.PreviewW, _state.PreviewH,
                    bufferLeft, bufferTop, (float)scale, animated);
            }
            else
            {
                DrawRotatedRectangleAnts(ds, dest, scale, animated);
            }
        }

        /// <summary>
        /// Draws marching ants for rotated rectangle.
        /// </summary>
        public void DrawRotatedRectangleAnts(CanvasDrawingSession ds, Rect dest, double scale, bool animated)
        {
            int handleW = (int)Math.Round((_state.OrigW > 0 ? _state.OrigW : _state.BufferWidth) * _state.ScaleX);
            int handleH = (int)Math.Round((_state.OrigH > 0 ? _state.OrigH : _state.BufferHeight) * _state.ScaleY);
            float selX = _state.Floating ? _state.FloatX : _state.Rect.X;
            float selY = _state.Floating ? _state.FloatY : _state.Rect.Y;
            float x = (float)(dest.X + selX * scale);
            float y = (float)(dest.Y + selY * scale);
            float w = (float)(handleW * scale);
            float h = (float)(handleH * scale);
            float cx = x + w / 2f;
            float cy = y + h / 2f;
            float totalAngle = (float)(_state.CumulativeAngleDeg + _state.AngleDeg);
            float rad = (float)(totalAngle * Math.PI / 180.0);

            var corners = new[] {
                RotateAround(x, y, cx, cy, rad),
                RotateAround(x + w, y, cx, cy, rad),
                RotateAround(x + w, y + h, cx, cy, rad),
                RotateAround(x, y + h, cx, cy, rad)
            };

            DrawAntsPolygon(ds, corners, animated);
        }

        /// <summary>
        /// Draws solid (non-animated) selection outline.
        /// </summary>
        private void DrawSolidOutline(CanvasDrawingSession ds, Rect dest, double scale)
        {
            if (_state.Region.IsEmpty) return;
            float ox = (float)dest.X, oy = (float)dest.Y, s = (float)scale;
            var b = _state.Region.Bounds;
            for (int y = b.Y; y < b.Y + b.Height; y++)
            {
                int runX0 = -1;
                for (int x = b.X; x <= b.X + b.Width; x++)
                {
                    bool edge = _state.Region.Contains(x, y) && !_state.Region.Contains(x, y - 1);
                    if (edge && runX0 < 0) runX0 = x;
                    if (!edge && runX0 >= 0)
                    {
                        ds.FillRectangle(ox + runX0 * s, oy + y * s - ANTS_THICKNESS * 0.5f, (x - runX0) * s, ANTS_THICKNESS, Colors.White);
                        runX0 = -1;
                    }
                }
            }
        }

        /// <summary>
        /// Draws marquee preview using active tool.
        /// </summary>
        private void DrawMarqueePreview(CanvasDrawingSession ds, Rect dest, double scale)
        {
            _getActiveTool?.Invoke()?.DrawPreview(ds, dest, scale, _state.AntsPhase);
        }

        // ════════════════════════════════════════════════════════════════════
        // BUFFER TRANSFORM HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static (byte[] buf, int w, int h) BuildScaledBuffer(byte[] src, int sw, int sh, double sx, double sy, ScaleMode filter)
        {
            int outW = Math.Max(1, (int)Math.Round(sw * sx));
            int outH = Math.Max(1, (int)Math.Round(sh * sy));
            if (outW == sw && outH == sh) return (src, sw, sh);

            return filter switch
            {
                ScaleMode.NearestNeighbor => (PixelOps.ResizeNearest(src, sw, sh, outW, outH), outW, outH),
                ScaleMode.Bilinear => (PixelOps.ResizeBilinear(src, sw, sh, outW, outH), outW, outH),
                ScaleMode.EPX => PixelOps.ScaleBy2xStepsThenNearest(src, sw, sh, outW, outH, epx: true),
                ScaleMode.Scale2x => PixelOps.ScaleBy2xStepsThenNearest(src, sw, sh, outW, outH, epx: false),
                _ => (PixelOps.ResizeNearest(src, sw, sh, outW, outH), outW, outH)
            };
        }

        private static (byte[] buf, int w, int h) BuildRotatedBuffer(byte[] src, int sw, int sh, double angleDeg, RotationMode kind)
        {
            double a = angleDeg % 360.0;
            if (Math.Abs(a) < 1e-6) return (src, sw, sh);
            return kind switch
            {
                RotationMode.RotSprite => PixelOps.RotateSpriteApprox(src, sw, sh, a),
                _ => PixelOps.RotateNearest(src, sw, sh, a)
            };
        }
    }
}