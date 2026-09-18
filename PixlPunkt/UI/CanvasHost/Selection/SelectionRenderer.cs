using System;
using System.Collections.Generic;
using Microsoft.UI;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Rendering;
using PixlPunkt.Core.Tools.Selection;
using SkiaSharp;
using Windows.Foundation;
using Windows.UI;
using static PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem;

namespace PixlPunkt.UI.CanvasHost.Selection
{
    /// <summary>
    /// Handles rendering of selection visuals including marching ants, handles, and floating selections.
    /// Uses ICanvasRenderer abstraction for cross-platform compatibility (SkiaSharp).
    /// </summary>
    /// <remarks>
    /// This class caches frequently allocated objects (Lists, byte arrays) to minimize GC pressure
    /// during rendering. Selection visuals are drawn every frame while a selection is active.
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

        // ════════════════════════════════════════════════════════════════════
        // CACHED OBJECTS - Reused across frames to minimize GC pressure
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Cached buffer for premultiplied floating selection when no transform needed.</summary>
        private byte[]? _cachedPremulBuffer;
        private int _cachedPremulBufferSize;

        /// <summary>Debug overlay for hit areas.</summary>
        public bool DebugShowHit { get; set; } = false;

        public Func<Rect>? GetDestRect { get => _getDestRect; set => _getDestRect = value; }
        public Func<double>? GetScale { get => _getScale; set => _getScale = value; }
        public Func<ISelectionTool?>? GetActiveTool { get => _getActiveTool; set => _getActiveTool = value; }
        public Func<SelDrag, bool>? NeedsContinuousRender { get => _needsContinuousRender; set => _needsContinuousRender = value; }

        public SelectionRenderer(SelectionSubsystem state, SelectionHitTesting hitTest)
        {
            _state = state;
            _hitTest = hitTest;
        }

        /// <summary>
        /// Main draw method for selection visuals using ICanvasRenderer.
        /// </summary>
        public void Draw(ICanvasRenderer renderer)
        {
            var tool = _getActiveTool?.Invoke();
            bool hasCustomPreview = tool?.HasPreview ?? false;
            bool isPaintingSelection = _state.Drag == SelDrag.Marquee && (_needsContinuousRender?.Invoke(_state.Drag) ?? false);
            bool hasFloating = _state.Floating && _state.Buffer != null;

            if (!_state.Active && !_state.HavePreview && !hasCustomPreview && !isPaintingSelection && !hasFloating)
                return;

            var dest = _getDestRect?.Invoke() ?? new Rect();
            double scale = _getScale?.Invoke() ?? 1.0;

            // Draw floating selection
            if (hasFloating)
                DrawFloatingSelection(renderer, dest, scale);

            bool isTransforming = _state.Drag == SelDrag.Scale || _state.Drag == SelDrag.Rotate || _state.Drag == SelDrag.Move;

            // Draw marching ants
            if ((_state.Active || isPaintingSelection || hasFloating) && !isTransforming)
                DrawMarchingAnts(renderer, dest, scale, animated: true);
            else if ((_state.Active || hasFloating) && isTransforming)
                DrawMarchingAnts(renderer, dest, scale, animated: false);

            // Draw transform handles if armed
            if (_state.State == SelectionState.Armed || hasFloating)
                DrawTransformHandles(renderer, dest, scale);

            // Draw marquee preview
            if (_state.HavePreview || hasCustomPreview)
                DrawMarqueePreview(renderer, dest, scale);

            // Advance marching ants animation
            if ((_state.Active || isPaintingSelection || hasFloating) && !isTransforming)
                _state.AdvanceAnts();
        }

        /// <summary>
        /// Draws the floating selection buffer with transforms.
        /// </summary>
        public void DrawFloatingSelection(ICanvasRenderer renderer, Rect dest, double scale)
        {
            if (_state.Buffer == null) return;

            bool hasScale = Math.Abs(_state.ScaleX - 1.0) > 0.001 || Math.Abs(_state.ScaleY - 1.0) > 0.001;
            bool hasDragRotation = Math.Abs(_state.AngleDeg) > 0.1;
            bool hasCumulativeRotation = Math.Abs(_state.CumulativeAngleDeg) > 0.1;
            bool needsTransform = hasScale || hasDragRotation || hasCumulativeRotation;

            float pivotDocX = _state.OrigCenterX;
            float pivotDocY = _state.OrigCenterY;
            float pivotViewX = (float)(dest.X + pivotDocX * scale);
            float pivotViewY = (float)(dest.Y + pivotDocY * scale);

            byte[] renderBuf;
            int renderW, renderH;
            float drawX, drawY;

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

                renderBuf = _state.PreviewBuf!;
                renderW = _state.PreviewW;
                renderH = _state.PreviewH;
                drawX = pivotViewX - (float)(renderW * scale / 2.0);
                drawY = pivotViewY - (float)(renderH * scale / 2.0);
            }
            else
            {
                _state.PreviewBuf = null;

                // Use cached buffer to avoid per-frame allocation
                int requiredSize = _state.Buffer.Length;
                if (_cachedPremulBuffer == null || _cachedPremulBufferSize != requiredSize)
                {
                    _cachedPremulBuffer = new byte[requiredSize];
                    _cachedPremulBufferSize = requiredSize;
                }

                Buffer.BlockCopy(_state.Buffer, 0, _cachedPremulBuffer, 0, _state.Buffer.Length);
                PremultiplyAlpha(_cachedPremulBuffer, _state.BufferWidth, _state.BufferHeight);

                renderBuf = _cachedPremulBuffer;
                renderW = _state.BufferWidth;
                renderH = _state.BufferHeight;
                drawX = (float)(dest.X + _state.FloatX * scale);
                drawY = (float)(dest.Y + _state.FloatY * scale);
            }

            var destRect = new Rect(drawX, drawY, renderW * scale, renderH * scale);
            var srcRect = new Rect(0, 0, renderW, renderH);
            renderer.DrawPixels(renderBuf, renderW, renderH, destRect, srcRect, 0.85f, ImageInterpolation.NearestNeighbor);
        }

        private static void PremultiplyAlpha(byte[] buf, int w, int h)
        {
            int len = w * h * 4;
            for (int i = 0; i < len; i += 4)
            {
                byte a = buf[i + 3];
                if (a == 0)
                {
                    buf[i + 0] = 0;
                    buf[i + 1] = 0;
                    buf[i + 2] = 0;
                }
                else if (a < 255)
                {
                    double alpha = a / 255.0;
                    buf[i + 0] = (byte)Math.Round(buf[i + 0] * alpha);
                    buf[i + 1] = (byte)Math.Round(buf[i + 1] * alpha);
                    buf[i + 2] = (byte)Math.Round(buf[i + 2] * alpha);
                }
            }
        }

        /// <summary>
        /// Draws transform handles (scale and rotation).
        /// </summary>
        public void DrawTransformHandles(ICanvasRenderer renderer, Rect dest, double scale)
        {
            int handleW = (int)Math.Round(_state.OrigW * _state.ScaleX);
            int handleH = (int)Math.Round(_state.OrigH * _state.ScaleY);
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

            // Colors
            var scaleHandleFill = Colors.White;
            var scaleHandleStroke = Color.FromArgb(255, 40, 40, 40);
            var rotHandleColor = Color.FromArgb(255, 0, 150, 255);
            var rotHandleStroke = Color.FromArgb(255, 0, 100, 200);
            var cornerHandleAccent = Color.FromArgb(255, 60, 60, 60);

            float handleSize = HANDLE_DRAW_SIZE;
            float handleHalf = handleSize / 2f;

            // Draw scale handles
            var scalePoints = new (float px, float py, bool isCorner)[]
            {
                (x, y, true), (x + w / 2, y, false), (x + w, y, true), (x + w, y + h / 2, false),
                (x + w, y + h, true), (x + w / 2, y + h, false), (x, y + h, true), (x, y + h / 2, false)
            };

            foreach (var (px, py, isCorner) in scalePoints)
            {
                var rp = RotateAround(px, py, cx, cy, rad);
                
                // Draw handle body
                renderer.FillRectangle(rp.x - handleHalf, rp.y - handleHalf, handleSize, handleSize, scaleHandleFill);
                renderer.DrawRectangle(rp.x - handleHalf, rp.y - handleHalf, handleSize, handleSize, 
                    isCorner ? cornerHandleAccent : scaleHandleStroke, 1.5f);
            }

            // Draw rotation handles
            float rotOff = ROT_HANDLE_OFFSET;
            float rotRadius = ROT_HANDLE_RADIUS;

            var rotPoints = new (float px, float py)[]
            {
                (x - rotOff, y - rotOff), (x + w / 2, y - rotOff), (x + w + rotOff, y - rotOff), (x + w + rotOff, y + h / 2),
                (x + w + rotOff, y + h + rotOff), (x + w / 2, y + h + rotOff), (x - rotOff, y + h + rotOff), (x - rotOff, y + h / 2)
            };

            foreach (var (px, py) in rotPoints)
            {
                var rp = RotateAround(px, py, cx, cy, rad);
                renderer.DrawEllipse(rp.x, rp.y, rotRadius, rotRadius, rotHandleStroke, 2f);
                renderer.FillEllipse(rp.x, rp.y, 2f, 2f, rotHandleColor);
            }

            // Draw pivot indicator
            var (pivotX, pivotY) = _hitTest.GetPivotPositionView(dest, scale);
            DrawPivotIndicator(renderer, pivotX, pivotY);

            if (DebugShowHit)
                DrawDebugHitAreas(renderer, x, y, w, h, cx, cy, rad);
        }

        private static void DrawPivotIndicator(ICanvasRenderer renderer, float px, float py)
        {
            float pivotSize = PIVOT_DRAW_SIZE;

            // Shadow
            var shadowColor = Color.FromArgb(100, 0, 0, 0);
            renderer.FillEllipse(px + 1, py + 1, pivotSize + 1, pivotSize + 1, shadowColor);

            // Outer ring (orange/amber)
            var outerColor = Color.FromArgb(255, 255, 140, 0);
            renderer.FillEllipse(px, py, pivotSize, pivotSize, outerColor);

            // Inner ring (darker)
            var innerRingColor = Color.FromArgb(255, 200, 100, 0);
            renderer.DrawEllipse(px, py, pivotSize - 2, pivotSize - 2, innerRingColor, 1.5f);

            // Center dot
            renderer.FillEllipse(px, py, 2.5f, 2.5f, Colors.White);

            // Crosshair lines
            float crossLen = pivotSize + 3f;
            float crossGap = pivotSize - 1f;

            renderer.DrawLine(px - crossLen, py, px - crossGap, py, Colors.White, 1.5f);
            renderer.DrawLine(px + crossGap, py, px + crossLen, py, Colors.White, 1.5f);
            renderer.DrawLine(px, py - crossLen, px, py - crossGap, Colors.White, 1.5f);
            renderer.DrawLine(px, py + crossGap, px, py + crossLen, Colors.White, 1.5f);
        }

        private void DrawDebugHitAreas(ICanvasRenderer renderer, float x, float y, float w, float h, float cx, float cy, float rad)
        {
            float pad = HANDLE_HIT_PAD;
            float off = ROT_HANDLE_OFFSET;
            float rr = ROT_HANDLE_RADIUS;

            var gFill = Color.FromArgb(60, 0, 255, 0);
            var gLine = Color.FromArgb(200, 0, 200, 0);

            var sPts = new (float px, float py)[]
            {
                (x, y), (x + w/2, y), (x + w, y), (x + w, y + h/2),
                (x + w, y + h), (x + w/2, y + h), (x, y + h), (x, y + h/2),
            };

            foreach (var (px, py) in sPts)
            {
                var rp = RotateAround(px, py, cx, cy, rad);
                renderer.FillRectangle(rp.x - pad, rp.y - pad, pad * 2, pad * 2, gFill);
                renderer.DrawRectangle(rp.x - pad, rp.y - pad, pad * 2, pad * 2, gLine, 1f);
            }

            var rFill = Color.FromArgb(80, 255, 0, 0);
            var rLine = Color.FromArgb(200, 255, 0, 0);

            var rPts = new (float px, float py)[]
            {
                (x - off, y - off), (x + w/2, y - off), (x + w + off, y - off), (x + w + off, y + h/2),
                (x + w + off, y + h + off), (x + w/2, y + h + off), (x - off, y + h + off), (x - off, y + h/2),
            };

            foreach (var (px, py) in rPts)
            {
                var rp = RotateAround(px, py, cx, cy, rad);
                renderer.FillRectangle(rp.x - rr, rp.y - rr, rr * 2, rr * 2, rFill);
                renderer.DrawRectangle(rp.x - rr, rp.y - rr, rr * 2, rr * 2, rLine, 1f);
            }
        }

        public void DrawMarchingAnts(ICanvasRenderer renderer, Rect dest, double scale, bool animated)
        {
            bool isPaintingSelection = _state.Drag == SelDrag.Marquee && (_needsContinuousRender?.Invoke(_state.Drag) ?? false);
            bool hasFloating = _state.Floating && _state.Buffer != null;

            if (!_state.Active && !isPaintingSelection && !hasFloating) return;

            bool hasRotation = Math.Abs(_state.CumulativeAngleDeg) > 0.1 || Math.Abs(_state.AngleDeg) > 0.1;
            bool hasScale = Math.Abs(_state.ScaleX - 1.0) > 0.001 || Math.Abs(_state.ScaleY - 1.0) > 0.001;

            if (hasFloating)
            {
                if (hasRotation && !_state.RegionNonRectangular)
                {
                    // A rectangular selection keeps its analytic rotated frame while rotating; the
                    // rasterised outline only takes over after commit.
                    int scaledW = Math.Max(1, (int)Math.Round(_state.OrigW * _state.ScaleX));
                    int scaledH = Math.Max(1, (int)Math.Round(_state.OrigH * _state.ScaleY));
                    float cx = (float)(dest.X + _state.OrigCenterX * scale);
                    float cy = (float)(dest.Y + _state.OrigCenterY * scale);
                    float rectLeft = cx - (float)(scaledW * scale / 2.0);
                    float rectTop = cy - (float)(scaledH * scale / 2.0);
                    float rectW = (float)(scaledW * scale);
                    float rectH = (float)(scaledH * scale);
                    float rad = (float)((_state.CumulativeAngleDeg + _state.AngleDeg) * Math.PI / 180.0);
                    var corners = new[] {
                        RotateAround(rectLeft, rectTop, cx, cy, rad),
                        RotateAround(rectLeft + rectW, rectTop, cx, cy, rad),
                        RotateAround(rectLeft + rectW, rectTop + rectH, cx, cy, rad),
                        RotateAround(rectLeft, rectTop + rectH, cx, cy, rad)
                    };
                    DrawAntsPolygon(renderer, corners, animated);
                }
                else
                {
                    // Everything else draws the region, which is kept equal to the float's mask
                    // under the live transform (bake, flip, undo, options box and, since stage 4,
                    // every scale/rotate pointer move), so the outline is the shape, not the
                    // pixels' silhouette.
                    if (animated)
                        _state.Region.DrawAnts(renderer, dest, scale, _state.AntsPhase, ANTS_ON, ANTS_OFF, ANTS_THICKNESS);
                    else
                        DrawSolidOutline(renderer, dest, scale);
                }
            }
            else
            {
                if (animated)
                    _state.Region.DrawAnts(renderer, dest, scale, _state.AntsPhase, ANTS_ON, ANTS_OFF, ANTS_THICKNESS);
                else
                    DrawSolidOutline(renderer, dest, scale);
            }
        }

        private void DrawAntsPolygon(ICanvasRenderer renderer, (float x, float y)[] corners, bool animated)
        {
            if (animated)
            {
                for (int i = 0; i < 4; i++)
                {
                    var p1 = corners[i];
                    var p2 = corners[(i + 1) % 4];
                    float length = MathF.Sqrt(MathF.Pow(p2.x - p1.x, 2) + MathF.Pow(p2.y - p1.y, 2));
                    float period = ANTS_ON + ANTS_OFF;

                    float dx = (p2.x - p1.x) / length;
                    float dy = (p2.y - p1.y) / length;

                    for (float pos = -_state.AntsPhase; pos < length; pos += period)
                    {
                        float segStart = Math.Max(0, pos);
                        float segEnd = Math.Min(length, pos + ANTS_ON);
                        if (segEnd > segStart)
                        {
                            renderer.DrawLine(
                                p1.x + dx * segStart, p1.y + dy * segStart,
                                p1.x + dx * segEnd, p1.y + dy * segEnd,
                                Colors.White, ANTS_THICKNESS);
                        }
                        float blackStart = Math.Max(0, pos + ANTS_ON);
                        float blackEnd = Math.Min(length, pos + period);
                        if (blackEnd > blackStart)
                        {
                            renderer.DrawLine(
                                p1.x + dx * blackStart, p1.y + dy * blackStart,
                                p1.x + dx * blackEnd, p1.y + dy * blackEnd,
                                Colors.Black, ANTS_THICKNESS);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    var p1 = corners[i];
                    var p2 = corners[(i + 1) % 4];
                    renderer.DrawLine(p1.x, p1.y, p2.x, p2.y, Colors.White, ANTS_THICKNESS);
                }
            }
        }

        private void DrawSolidOutline(ICanvasRenderer renderer, Rect dest, double scale)
            => _state.Region.DrawOutline(renderer, dest, scale, ANTS_THICKNESS);

        private void DrawMarqueePreview(ICanvasRenderer renderer, Rect dest, double scale)
        {
            _getActiveTool?.Invoke()?.DrawPreview(renderer, dest, scale, _state.AntsPhase);
        }

        // ════════════════════════════════════════════════════════════════════
        // BUFFER TRANSFORM HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static (byte[] buf, int w, int h) BuildScaledBuffer(byte[] src, int sw, int sh, double sx, double sy, ScaleMode filter) => Core.Selection.SelectionBufferOps.BuildScaled(src, sw, sh, sx, sy, filter);

        private static (byte[] buf, int w, int h) BuildRotatedBuffer(byte[] src, int sw, int sh, double angleDeg, RotationMode kind) => Core.Selection.SelectionBufferOps.BuildRotated(src, sw, sh, angleDeg, kind);
    }
}
