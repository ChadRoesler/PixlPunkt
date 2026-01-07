using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using PixlPunkt.Uno.Core.Brush;
using PixlPunkt.Uno.Core.Coloring.Helpers;
using PixlPunkt.Uno.Core.Painting;
using PixlPunkt.Uno.Core.Painting.Helpers;
using PixlPunkt.Uno.Core.Rendering;
using PixlPunkt.Uno.Core.Structs;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.PluginSdk.Settings;
using SkiaSharp;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.Uno.UI.CanvasHost
{
    /// <summary>
    /// Brush overlay subsystem for CanvasViewHost:
    /// - Brush cursor overlay rendering
    /// - OnBrushMoved snapshot management
    /// - Brush outline drawing
    /// - Tile overlay rendering
    /// </summary>
    /// <remarks>
    /// This class caches frequently allocated objects (Dictionaries, HashSets, arrays) to minimize 
    /// GC pressure during rendering. Shift-line preview and outline rendering are called every frame
    /// during painting operations.
    /// </remarks>
    public sealed partial class CanvasViewHost
    {
        // ════════════════════════════════════════════════════════════════════
        // CACHED BRUSH OVERLAY OBJECTS - Reused across frames to minimize GC
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Cached dictionary for shift-line preview pixel accumulation.</summary>
        private Dictionary<(int x, int y), byte>? _cachedDrawnPixels;
        
        /// <summary>Cached HashSet for shift-line outline preview affected pixels.</summary>
        private HashSet<(int x, int y)>? _cachedAffectedPixels;
        
        /// <summary>Cached grid array for shift-line outline detection.</summary>
        private bool[]? _cachedOutlineGrid;
        private int _cachedOutlineGridSize;

        /// <summary>Cached ghost pixels array for tile ghost preview.</summary>
        private byte[]? _cachedTileGhostPixels;
        private int _cachedTileGhostPixelsSize;

        // ════════════════════════════════════════════════════════════════════
        // BRUSH CURSOR OVERLAY
        // ════════════════════════════════════════════════════════════════════

        private void DrawBrushCursorOverlay(ICanvasRenderer renderer, Rect dest)
        {
            // External dropper mode - always show 1x1 pixel cursor
            if (_externalDropperActive)
            {
                if (_hoverValid)
                {
                    DrawPixelCursor(renderer, dest);
                }
                return;
            }

            // Tile tools - show tile overlay
            if (_toolState?.IsActiveTileTool == true)
            {
                DrawTileOverlay(renderer, dest);
                return;
            }

            // Draw shift-line preview if active
            if (_shiftLineActive && _isPainting)
            {
                var activeToolId = _toolState?.ActiveToolId ?? ToolIds.Brush;
                bool fillGhost = activeToolId == ToolIds.Brush;

                if (fillGhost)
                {
                    // Brush tool: show filled ghost preview of the line
                    DrawShiftLinePreview(renderer, dest);
                }
                else
                {
                    // Other tools (Eraser, etc.): show outline preview of the line
                    DrawShiftLineOutlinePreview(renderer, dest);
                }
                return;
            }

            if (!_hoverValid) return;
            if (_shapeDrag) return;

            var activeToolId2 = _toolState?.ActiveToolId ?? ToolIds.Brush;

            // Handle ALL shape tools (built-in and plugin) via category check
            if (_toolState?.ActiveCategory == ToolCategory.Shape)
            {
                DrawShapeStartPointHover(renderer, dest);
                return;
            }

            // Draw 1x1 pixel cursor for utility tools (Dropper, Pan, Zoom) and precision tools (Fill, Selection)
            if (ShouldShowPixelCursor(activeToolId2))
            {
                DrawPixelCursor(renderer, dest);
                return;
            }

            // Exclude selection tools (except PaintSelect which behaves like a brush tool)
            bool isSelectCategory = _toolState?.ActiveCategory == ToolCategory.Select;
            bool isPaintSelect = activeToolId2 == ToolIds.PaintSelect;
            if (isSelectCategory && !isPaintSelect)
                return;

            bool fillGhost2 = activeToolId2 == ToolIds.Brush;
            var mask = GetCurrentBrushMask();

            if (mask == null || mask.Count == 0)
                return;

            double s = _zoom.Scale;
            float baseX = (float)dest.X;
            float baseY = (float)dest.Y;

            if (fillGhost2)
            {
                foreach (var (dx, dy) in mask)
                {
                    int x = _hoverX + dx;
                    int y = _hoverY + dy;
                    if ((uint)x >= (uint)Document.Surface.Width || (uint)y >= (uint)Document.Surface.Height) continue;

                    byte effA = ComputeBrushAlphaAtOffset(dx, dy);
                    if (effA == 0) continue;

                    uint before = ReadCompositeBGRA(x, y);
                    uint src = (_fg & 0x00FFFFFFu) | ((uint)effA << 24);
                    uint after = ColorUtil.BlendOver(before, src);

                    float sx = (float)(dest.X + x * s);
                    float sy = (float)(dest.Y + y * s);
                    renderer.FillRectangle(sx, sy, (float)s, (float)s, ColorUtil.ToColor(after));
                }
                return;
            }

            DrawBrushOutline(renderer, dest, mask, s, baseX, baseY);
        }

        /// <summary>
        /// Determines if a tool should show the 1x1 pixel cursor (like Pan/Zoom have).
        /// </summary>
        private bool ShouldShowPixelCursor(string toolId)
        {
            return toolId == ToolIds.Dropper ||
                   toolId == ToolIds.Fill ||
                   toolId == ToolIds.Pan ||
                   toolId == ToolIds.Zoom ||
                   toolId == ToolIds.SelectRect ||
                   toolId == ToolIds.Lasso ||
                   toolId == ToolIds.Wand;
        }

        /// <summary>
        /// Draws a 1x1 pixel cursor outline at the hover position.
        /// This shows the exact pixel the user is pointing at for precision tools.
        /// </summary>
        private void DrawPixelCursor(ICanvasRenderer renderer, Rect dest)
        {
            double s = _zoom.Scale;
            float x = (float)(dest.X + _hoverX * s);
            float y = (float)(dest.Y + _hoverY * s);
            float size = (float)s;

            // Get contrasting color based on pixel under cursor
            Color ink = SampleInkAtDoc(_hoverX, _hoverY);

            // Draw pixel outline
            renderer.DrawRectangle(x, y, size, size, ink, 1f);
        }

        /// <summary>
        /// Gets the current brush mask offsets, handling both built-in shapes and custom brushes.
        /// Uses the ACTIVE tool's settings (not always BrushTool).
        /// </summary>
        private IReadOnlyList<(int dx, int dy)> GetCurrentBrushMask()
        {
            // Get the active tool's stroke settings
            var strokeSettings = GetStrokeSettingsForCurrentTool();

            // Check if active tool has a custom brush selected
            if (strokeSettings is ICustomBrushSettings customBrushSettings && customBrushSettings.IsCustomBrushSelected)
            {
                var brush = BrushDefinitionService.Instance.GetBrush(customBrushSettings.CustomBrushFullName!);
                if (brush != null)
                {
                    return BrushMaskCache.Shared.GetOffsetsForCustomBrush(brush, _brushSize);
                }
            }

            // Fall back to built-in shape from active tool or stroke engine
            if (strokeSettings != null)
            {
                return BrushMaskCache.Shared.GetOffsets(strokeSettings.Shape, strokeSettings.Size);
            }

            // Final fallback to stroke engine's current settings
            return _stroke.GetCurrentBrushOffsets();
        }

        /// <summary>
        /// Computes the brush alpha at an offset, handling both built-in shapes and custom brushes.
        /// Uses the ACTIVE tool's settings (not always BrushTool).
        /// </summary>
        private byte ComputeBrushAlphaAtOffset(int dx, int dy)
        {
            // Get the active tool's stroke settings
            var strokeSettings = GetStrokeSettingsForCurrentTool();

            // Check if active tool has a custom brush selected
            if (strokeSettings is ICustomBrushSettings customBrushSettings && customBrushSettings.IsCustomBrushSelected)
            {
                // Custom brushes use radial density-based falloff (matches DefaultToolContextProvider.ComputeCustomBrushAlpha)
                return ComputeCustomBrushAlpha(dx, dy, _brushSize, _brushOpacity, _brushDensity);
            }

            // Built-in shapes use the stroke engine's alpha computation
            return _stroke.ComputeBrushAlphaAtOffset(dx, dy);
        }

        /// <summary>
        /// Computes per-pixel alpha for custom brushes using radial density-based falloff.
        /// Matches DefaultToolContextProvider.ComputeCustomBrushAlpha exactly.
        /// </summary>
        private static byte ComputeCustomBrushAlpha(int dx, int dy, int size, byte opacity, byte density)
        {
            if (opacity == 0) return 0;

            int sz = Math.Max(1, size);
            double Aop = opacity / 255.0;

            double r = sz / 2.0;

            // Use circular distance - offsets are already relative to brush center
            double d = Math.Sqrt((double)dx * dx + (double)dy * dy);

            // Apply density falloff based on distance
            double D = density / 255.0;
            double Rhard = r * D;

            // Full opacity within the hard radius
            if (d <= Rhard)
                return (byte)Math.Round(255.0 * Aop);

            // Falloff region beyond hard radius
            double span = Math.Max(1e-6, r - Rhard);
            double t = Math.Min(1.0, (d - Rhard) / span); // Clamp t to [0, 1]
            double mask = 1.0 - (t * t) * (3 - 2 * t); // Smoothstep falloff
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }

        private void DrawBrushOutline(ICanvasRenderer renderer, Rect dest, IReadOnlyList<(int dx, int dy)> mask,
            double s, float baseX, float baseY)
        {
            StrokeUtil.BuildMaskGrid(mask, out var grid, out int minDx, out int minDy, out int w, out int h);

            int docX0 = _hoverX + minDx;
            int docY0 = _hoverY + minDy;

            float sf = (float)s;
            float thick = 1f;

            // Horizontal edges
            for (int y = 0; y <= h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    bool edge = StrokeUtil.Occup(grid, w, h, x, y) ^ StrokeUtil.Occup(grid, w, h, x, y - 1);
                    if (!edge) { x++; continue; }

                    bool insideBelow = StrokeUtil.Occup(grid, w, h, x, y);
                    int outDocY = docY0 + (insideBelow ? y - 1 : y);
                    int sampleDocX = docX0 + Math.Clamp(x, 0, w - 1);
                    Color ink = SampleInkAtDoc(sampleDocX, outDocY);

                    int x0 = x++;
                    while (x < w)
                    {
                        bool e2 = StrokeUtil.Occup(grid, w, h, x, y) ^ StrokeUtil.Occup(grid, w, h, x, y - 1);
                        if (!e2) break;

                        bool insideBelow2 = StrokeUtil.Occup(grid, w, h, x, y);
                        int outDocY2 = docY0 + (insideBelow2 ? y - 1 : y);
                        int sampleDocX2 = docX0 + Math.Clamp(x, 0, w - 1);
                        Color ink2 = SampleInkAtDoc(sampleDocX2, outDocY2);
                        if (!ink2.Equals(ink)) break;

                        x++;
                    }

                    float sx0 = baseX + (docX0 + x0) * sf;
                    float sx1 = baseX + (docX0 + x) * sf;
                    float sy = baseY + (docY0 + y) * sf;
                    renderer.DrawLine(sx0, sy, sx1, sy, ink, thick);
                }
            }

            // Vertical edges
            for (int x = 0; x <= w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    bool edge = StrokeUtil.Occup(grid, w, h, x, y) ^ StrokeUtil.Occup(grid, w, h, x - 1, y);
                    if (!edge) { y++; continue; }

                    bool insideRight = StrokeUtil.Occup(grid, w, h, x, y);
                    int outDocX = docX0 + (insideRight ? x - 1 : x);
                    int sampleDocY = docY0 + Math.Clamp(y, 0, h - 1);
                    Color ink = SampleInkAtDoc(outDocX, sampleDocY);

                    int y0 = y++;
                    while (y < h)
                    {
                        bool e2 = StrokeUtil.Occup(grid, w, h, x, y) ^ StrokeUtil.Occup(grid, w, h, x - 1, y);
                        if (!e2) break;

                        bool insideRight2 = StrokeUtil.Occup(grid, w, h, x, y);
                        int outDocX2 = docX0 + (insideRight2 ? x - 1 : x);
                        int sampleDocY2 = docY0 + Math.Clamp(y, 0, h - 1);
                        Color ink2 = SampleInkAtDoc(outDocX2, sampleDocY2);
                        if (!ink2.Equals(ink)) break;

                        y++;
                    }

                    float sy0 = baseY + (docY0 + y0) * sf;
                    float sy1 = baseY + (docY0 + y) * sf;
                    float sx = baseX + (docX0 + x) * sf;
                    renderer.DrawLine(sx, sy0, sx, sy1, ink, thick);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // BRUSH OVERLAY SNAPSHOT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Notifies observers that the brush overlay moved.</summary>
        private void OnBrushMoved(Vector2 pos, float radius)
        {
            var activeToolId = _toolState?.ActiveToolId ?? ToolIds.Brush;

            bool isOutlineTool = activeToolId == ToolIds.Blur || activeToolId == ToolIds.Jumble || activeToolId == ToolIds.Smudge
                || activeToolId == ToolIds.Eraser || activeToolId == ToolIds.Replacer || activeToolId == ToolIds.Gradient || activeToolId == ToolIds.PaintSelect;

            bool hideForPainting = _isPainting && !isOutlineTool && !_shiftLineActive;

            bool visible = _hoverValid && _toolState?.IsDropper != true && !_shapeDrag && !hideForPainting;

            IReadOnlyList<(int dx, int dy)> mask = Array.Empty<(int dx, int dy)>();

            bool fillGhost = false;

            // Custom brush support - get from ACTIVE tool's settings
            string? customBrushFullName = null;

            // Check if active tool is a shape tool (built-in or plugin)
            bool isShapeCategory = _toolState?.ActiveCategory == ToolCategory.Shape;

            // Get the active tool's stroke settings for brush shape/size
            var activeStrokeSettings = GetStrokeSettingsForCurrentTool();

            if (visible)
            {
                // Exclude shapes (via category), fill, and selection tools - but allow PaintSelect (it behaves like a brush tool)
                bool isSelectCategory = _toolState?.ActiveCategory == ToolCategory.Select;
                bool isPaintSelect = activeToolId == ToolIds.PaintSelect;
                if (isShapeCategory || activeToolId == ToolIds.Fill
                    || (isSelectCategory && !isPaintSelect))
                {
                    visible = false;
                }
                else
                {
                    fillGhost = activeToolId == ToolIds.Brush && !_shiftLineActive;
                    mask = GetCurrentBrushMask();

                    // Check for custom brush from ACTIVE tool
                    if (activeStrokeSettings is ICustomBrushSettings customBrushSettings && customBrushSettings.IsCustomBrushSelected)
                    {
                        customBrushFullName = customBrushSettings.CustomBrushFullName;
                    }
                }
            }
            else if (_hoverValid && isOutlineTool)
            {
                mask = GetCurrentBrushMask();

                // Check for custom brush from ACTIVE tool
                if (activeStrokeSettings is ICustomBrushSettings customBrushSettings && customBrushSettings.IsCustomBrushSelected)
                {
                    customBrushFullName = customBrushSettings.CustomBrushFullName;
                }
            }

            int brushSize = _brushSize;
            // Get brush shape from ACTIVE tool's settings, not universal Brush
            BrushShape brushShape = activeStrokeSettings?.Shape ?? _toolState?.Brush.Shape ?? BrushShape.Square;
            byte brushDensity = _brushDensity;
            byte brushOpacity = _brushOpacity;

            bool isShapeDrag = _shapeDrag;
            bool isEllipse = _shapeIsEllipse;
            bool isFilled = _shapeFilled;
            int shapeX0 = _sx;
            int shapeY0 = _sy;
            int shapeX1 = _ex;
            int shapeY1 = _ey;

            // Get shape-specific settings from the tool
            // This works for built-in (Rect, Ellipse) and plugin shape tools
            int shapeStrokeWidth;
            BrushShape shapeBrushShape;
            byte shapeBrushDensity;
            byte shapeBrushOpacity;

            if (isShapeCategory)
            {
                // Get settings from the active tool's settings via interfaces
                var settings = _toolState?.GetSettingsForToolId(activeToolId);

                if (settings is IStrokeSettings strokeSettings)
                {
                    shapeStrokeWidth = strokeSettings.Size;
                    shapeBrushShape = strokeSettings.Shape;
                }
                else if (_shapeIsEllipse)
                {
                    shapeStrokeWidth = _toolState?.Ellipse.StrokeWidth ?? 1;
                    shapeBrushShape = _toolState?.Ellipse.Shape ?? BrushShape.Circle;
                }
                else
                {
                    shapeStrokeWidth = _toolState?.Rect.StrokeWidth ?? 1;
                    shapeBrushShape = _toolState?.Rect.Shape ?? BrushShape.Square;
                }

                if (settings is IOpacitySettings opacitySettings)
                {
                    shapeBrushOpacity = opacitySettings.Opacity;
                }
                else if (_shapeIsEllipse)
                {
                    shapeBrushOpacity = _toolState?.Ellipse.Opacity ?? 255;
                }
                else
                {
                    shapeBrushOpacity = _toolState?.Rect.Opacity ?? 255;
                }

                if (settings is IDensitySettings densitySettings)
                {
                    shapeBrushDensity = densitySettings.Density;
                }
                else if (_shapeIsEllipse)
                {
                    shapeBrushDensity = _toolState?.Ellipse.Density ?? 255;
                }
                else
                {
                    shapeBrushDensity = _toolState?.Rect.Density ?? 255;
                }
            }
            else if (_shapeIsEllipse)
            {
                shapeStrokeWidth = _toolState?.Ellipse.StrokeWidth ?? 1;
                shapeBrushShape = _toolState?.Ellipse.Shape ?? BrushShape.Circle;
                shapeBrushDensity = _toolState?.Ellipse.Density ?? 255;
                shapeBrushOpacity = _toolState?.Ellipse.Opacity ?? 255;
            }
            else
            {
                shapeStrokeWidth = _toolState?.Rect.StrokeWidth ?? 1;
                shapeBrushShape = _toolState?.Rect.Shape ?? BrushShape.Square;
                shapeBrushDensity = _toolState?.Rect.Density ?? 255;
                shapeBrushOpacity = _toolState?.Rect.Opacity ?? 255;
            }

            // Shift-line preview state
            bool isShiftLineDrag = _shiftLineActive && _isPainting;
            int shiftLineX0 = _shiftLineOriginX;
            int shiftLineY0 = _shiftLineOriginY;
            int shiftLineX1 = _lastDocX;
            int shiftLineY1 = _lastDocY;

            _currentBrushOverlay = new BrushOverlaySnapshot(
                pos,
                radius,
                mask,
                fillGhost,
                visible,
                brushSize,
                brushShape,
                brushDensity,
                brushOpacity,
                customBrushFullName,
                isShapeDrag,
                isEllipse,
                isFilled,
                shapeX0,
                shapeY0,
                shapeX1,
                shapeY1,
                shapeStrokeWidth,
                shapeBrushShape,
                shapeBrushDensity,
                shapeBrushOpacity,
                _shapeShowStartPoint,
                _hoverX,
                _hoverY,
                isShiftLineDrag,
                shiftLineX0,
                shiftLineY0,
                shiftLineX1,
                shiftLineY1
            );

            BrushOverlayChanged?.Invoke(pos, radius);
        }

        /// <summary>
        /// Draws a preview line from shift-line origin to current endpoint.
        /// Uses smooth stamping (stride of 1) matching PainterBase.StampLine for accurate preview.
        /// </summary>
        private void DrawShiftLinePreview(ICanvasRenderer renderer, Rect dest)
        {
            double s = _zoom.Scale;
            var mask = GetCurrentBrushMask();

            int x0 = _shiftLineOriginX;
            int y0 = _shiftLineOriginY;
            int x1 = _lastDocX;
            int y1 = _lastDocY;

            int dx = x1 - x0, dy = y1 - y0;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

            // Use cached dictionary instead of allocating new one each frame
            _cachedDrawnPixels ??= new Dictionary<(int x, int y), byte>(256);
            _cachedDrawnPixels.Clear();

            if (steps == 0)
            {
                // Single stamp at origin
                AccumulateStampAtPoint(x0, y0, mask, _cachedDrawnPixels);
            }
            else
            {
                // Use stride of 1 for smooth, continuous line drawing (matches PainterBase.StampLine)
                double sx = dx / (double)steps;
                double sy = dy / (double)steps;

                double x = x0, y = y0;
                for (int i = 0; i <= steps; i++)
                {
                    AccumulateStampAtPoint((int)Math.Round(x), (int)Math.Round(y), mask, _cachedDrawnPixels);
                    x += sx;
                    y += sy;
                }
            }

            // Draw all accumulated pixels
            foreach (var ((px, py), effA) in _cachedDrawnPixels)
            {
                if ((uint)px >= (uint)Document.Surface.Width || (uint)py >= (uint)Document.Surface.Height) continue;

                uint before = ReadCompositeBGRA(px, py);
                uint src = (_fg & 0x00FFFFFFu) | ((uint)effA << 24);
                uint after = ColorUtil.BlendOver(before, src);

                float screenX = (float)(dest.X + px * s);
                float screenY = (float)(dest.Y + py * s);
                renderer.FillRectangle(screenX, screenY, (float)s, (float)s, ColorUtil.ToColor(after));
            }
        }

        /// <summary>
        /// Draws an outline preview for shift-line operations for non-brush tools (eraser, blur, etc.).
        /// Shows the outline of the combined brush path rather than filled pixels.
        /// Uses smooth stamping (stride of 1) matching PainterBase.StampLine.
        /// </summary>
        private void DrawShiftLineOutlinePreview(ICanvasRenderer renderer, Rect dest)
        {
            double s = _zoom.Scale;
            var mask = GetCurrentBrushMask();
            if (mask == null || mask.Count == 0) return;

            int x0 = _shiftLineOriginX;
            int y0 = _shiftLineOriginY;
            int x1 = _lastDocX;
            int y1 = _lastDocY;

            int dx = x1 - x0, dy = y1 - y0;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

            // Use cached HashSet instead of allocating new one each frame
            _cachedAffectedPixels ??= new HashSet<(int x, int y)>(256);
            _cachedAffectedPixels.Clear();

            if (steps == 0)
            {
                // Single stamp at origin
                foreach (var (mdx, mdy) in mask)
                {
                    _cachedAffectedPixels.Add((x0 + mdx, y0 + mdy));
                }
            }
            else
            {
                // Use stride of 1 for smooth, continuous line preview (matches PainterBase.StampLine)
                double sx = dx / (double)steps;
                double sy = dy / (double)steps;

                double x = x0, y = y0;
                for (int i = 0; i <= steps; i++)
                {
                    int cx = (int)Math.Round(x);
                    int cy = (int)Math.Round(y);

                    foreach (var (mdx, mdy) in mask)
                    {
                        _cachedAffectedPixels.Add((cx + mdx, cy + mdy));
                    }

                    x += sx;
                    y += sy;
                }
            }

            // Build grid for outline detection
            if (_cachedAffectedPixels.Count == 0) return;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var (px, py) in _cachedAffectedPixels)
            {
                if (px < minX) minX = px;
                if (py < minY) minY = py;
                if (px > maxX) maxX = px;
                if (py > maxY) maxY = py;
            }

            int gridW = maxX - minX + 1;
            int gridH = maxY - minY + 1;
            int gridSize = gridW * gridH;
            
            // Use cached grid array instead of allocating new one each frame
            if (_cachedOutlineGrid == null || _cachedOutlineGridSize < gridSize)
            {
                _cachedOutlineGrid = new bool[gridSize];
                _cachedOutlineGridSize = gridSize;
            }
            else
            {
                // Clear the portion we'll use
                Array.Clear(_cachedOutlineGrid, 0, gridSize);
            }

            foreach (var (px, py) in _cachedAffectedPixels)
            {
                int gx = px - minX;
                int gy = py - minY;
                if (gx >= 0 && gx < gridW && gy >= 0 && gy < gridH)
                {
                    _cachedOutlineGrid[gy * gridW + gx] = true;
                }
            }

            // Draw outline edges
            float sf = (float)s;
            float thick = 1f;
            float baseX = (float)dest.X;
            float baseY = (float)dest.Y;

            bool Occup(int gx, int gy)
            {
                if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) return false;
                return _cachedOutlineGrid[gy * gridW + gx];
            }

            // Horizontal edges
            for (int gy = 0; gy <= gridH; gy++)
            {
                int gx = 0;
                while (gx < gridW)
                {
                    bool edge = Occup(gx, gy) ^ Occup(gx, gy - 1);
                    if (!edge) { gx++; continue; }

                    int docX = minX + gx;
                    int docY = minY + gy;
                    Color ink = SampleInkAtDoc(docX, docY);

                    int gx0 = gx++;
                    while (gx < gridW)
                    {
                        bool e2 = Occup(gx, gy) ^ Occup(gx, gy - 1);
                        if (!e2) break;

                        int docX2 = minX + gx;
                        int docY2 = minY + gy;
                        Color ink2 = SampleInkAtDoc(docX2, docY2);
                        if (!ink2.Equals(ink)) break;

                        gx++;
                    }

                    float sx0 = baseX + (minX + gx0) * sf;
                    float sx1 = baseX + (minX + gx) * sf;
                    float sy = baseY + (minY + gy) * sf;
                    renderer.DrawLine(sx0, sy, sx1, sy, ink, thick);
                }
            }

            // Vertical edges
            for (int gx = 0; gx <= gridW; gx++)
            {
                int gy = 0;
                while (gy < gridH)
                {
                    bool edge = Occup(gx, gy) ^ Occup(gx - 1, gy);
                    if (!edge) { gy++; continue; }

                    int docX = minX + gx;
                    int docY = minY + gy;
                    Color ink = SampleInkAtDoc(docX, docY);

                    int gy0 = gy++;
                    while (gy < gridH)
                    {
                        bool e2 = Occup(gx, gy) ^ Occup(gx - 1, gy);
                        if (!e2) break;

                        int docX2 = minX + gx;
                        int docY2 = minY + gy;
                        Color ink2 = SampleInkAtDoc(docX2, docY2);
                        if (!ink2.Equals(ink)) break;

                        gy++;
                    }

                    float sy0 = baseY + (minY + gy0) * sf;
                    float sy1 = baseY + (minY + gy) * sf;
                    float sx = baseX + (minX + gx) * sf;
                    renderer.DrawLine(sx, sy0, sx, sy1, ink, thick);
                }
            }
        }

        /// <summary>
        /// Accumulates a brush stamp at the given point, tracking max alpha per pixel.
        /// </summary>
        private void AccumulateStampAtPoint(int cx, int cy, IReadOnlyList<(int dx, int dy)> mask, Dictionary<(int x, int y), byte> drawnPixels)
        {
            foreach (var (dx, dy) in mask)
            {
                int px = cx + dx;
                int py = cy + dy;

                byte effA = ComputeBrushAlphaAtOffset(dx, dy);
                if (effA == 0) continue;

                var key = (px, py);
                if (!drawnPixels.TryGetValue(key, out byte currentAlpha) || effA > currentAlpha)
                {
                    drawnPixels[key] = effA;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // TILE OVERLAY RENDERING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws the tile overlay preview for tile tools.
        /// </summary>
        private void DrawTileOverlay(ICanvasRenderer renderer, Rect dest)
        {
            var overlay = _currentTileOverlay;
            if (overlay == null)
                return;

            var preview = overlay.Value;
            var tileSet = Document?.TileSet;
            if (tileSet == null)
                return;

            double scale = _zoom.Scale;
            int tileW = tileSet.TileWidth;
            int tileH = tileSet.TileHeight;

            // Handle animation selection overlay (Tile Animation tool)
            if (preview.AnimationSelection.HasValue)
            {
                DrawAnimationSelectionOverlay(renderer, dest, scale, tileW, tileH, preview.AnimationSelection.Value);
                return;
            }

            // Calculate screen position for the tile
            int docX = preview.TileX * tileW;
            int docY = preview.TileY * tileH;

            if (!preview.SnapToGrid)
            {
                docX += preview.PixelOffsetX;
                docY += preview.PixelOffsetY;
            }

            float screenX = (float)(dest.X + docX * scale);
            float screenY = (float)(dest.Y + docY * scale);
            float screenW = (float)(tileW * scale);
            float screenH = (float)(tileH * scale);

            // Draw ghost preview of selected tile
            if (preview.ShowGhost && preview.TileId >= 0)
            {
                var pixels = tileSet.GetTilePixels(preview.TileId);
                if (pixels != null)
                {
                    DrawTileGhost(renderer, dest, scale, docX, docY, tileW, tileH, pixels);
                }
            }

            // Draw tile boundary outline
            if (preview.ShowOutline)
            {
                var outlineColor = Color.FromArgb(180, 0, 160, 255);
                renderer.DrawRectangle(screenX, screenY, screenW, screenH, outlineColor, 2f);
            }
        }

        /// <summary>
        /// Draws the animation selection overlay for the Tile Animation tool.
        /// Shows: Blue border on start tile, Orange border on end tile, 
        /// Semi-transparent highlight on all selected tiles in the range.
        /// </summary>
        private void DrawAnimationSelectionOverlay(ICanvasRenderer renderer, Rect dest, double scale,
            int tileW, int tileH, PixlPunkt.PluginSdk.Tile.AnimationSelectionOverlay selection)
        {
            // Normalize the selection bounds
            int startX = Math.Min(selection.StartTileX, selection.EndTileX);
            int startY = Math.Min(selection.StartTileY, selection.EndTileY);
            int endX = Math.Max(selection.StartTileX, selection.EndTileX);
            int endY = Math.Max(selection.StartTileY, selection.EndTileY);

            // Colors
            var startColor = Color.FromArgb(255, 0, 140, 255);    // Blue for start
            var endColor = Color.FromArgb(255, 255, 140, 0);      // Orange for end
            var highlightFill = Color.FromArgb(40, 100, 200, 255); // Light blue fill for selection
            var highlightStroke = Color.FromArgb(100, 100, 180, 255); // Subtle outline for selection

            float strokeWidth = 3f;
            float innerStroke = 1.5f;

            // Draw semi-transparent highlight fill over all selected tiles
            for (int ty = startY; ty <= endY; ty++)
            {
                for (int tx = startX; tx <= endX; tx++)
                {
                    // Skip start and end tiles (they get special treatment)
                    bool isStart = (tx == selection.StartTileX && ty == selection.StartTileY);
                    bool isEnd = (tx == selection.EndTileX && ty == selection.EndTileY);

                    int docX = tx * tileW;
                    int docY = ty * tileH;
                    float screenX = (float)(dest.X + docX * scale);
                    float screenY = (float)(dest.Y + docY * scale);
                    float screenW = (float)(tileW * scale);
                    float screenH = (float)(tileH * scale);

                    if (!isStart && !isEnd)
                    {
                        // Fill interior tiles with highlight
                        renderer.FillRectangle(screenX, screenY, screenW, screenH, highlightFill);
                        renderer.DrawRectangle(screenX, screenY, screenW, screenH, highlightStroke, innerStroke);
                    }
                }
            }

            // Draw START tile border (Blue)
            {
                int docX = selection.StartTileX * tileW;
                int docY = selection.StartTileY * tileH;
                float screenX = (float)(dest.X + docX * scale);
                float screenY = (float)(dest.Y + docY * scale);
                float screenW = (float)(tileW * scale);
                float screenH = (float)(tileH * scale);

                // Fill start tile with blue tint
                renderer.FillRectangle(screenX, screenY, screenW, screenH, Color.FromArgb(60, 0, 140, 255));

                // Draw thick border
                renderer.DrawRectangle(screenX, screenY, screenW, screenH, startColor, strokeWidth);

                // Inner white highlight for visibility
                renderer.DrawRectangle(screenX + 2, screenY + 2, screenW - 4, screenH - 4,
                    Color.FromArgb(180, 255, 255, 255), 1f);
            }

            // Draw END tile border (Orange) - only if different from start
            if (selection.StartTileX != selection.EndTileX || selection.StartTileY != selection.EndTileY)
            {
                int docX = selection.EndTileX * tileW;
                int docY = selection.EndTileY * tileH;
                float screenX = (float)(dest.X + docX * scale);
                float screenY = (float)(dest.Y + docY * scale);
                float screenW = (float)(tileW * scale);
                float screenH = (float)(tileH * scale);

                // Fill end tile with orange tint
                renderer.FillRectangle(screenX, screenY, screenW, screenH, Color.FromArgb(60, 255, 140, 0));

                // Draw thick border
                renderer.DrawRectangle(screenX, screenY, screenW, screenH, endColor, strokeWidth);

                // Inner white highlight for visibility
                renderer.DrawRectangle(screenX + 2, screenY + 2, screenW - 4, screenH - 4,
                    Color.FromArgb(180, 255, 255, 255), 1f);
            }
        }

        /// <summary>
        /// Draws a semi-transparent ghost preview of a tile's pixels using SkiaSharp.
        /// Uses NearestNeighbor interpolation for crisp pixel-perfect rendering.
        /// </summary>
        private void DrawTileGhost(ICanvasRenderer renderer, Rect dest, double scale,
            int docX, int docY, int tileW, int tileH, byte[] pixels)
        {
            int requiredSize = pixels.Length;
            
            // Use cached buffer instead of allocating new one each frame
            if (_cachedTileGhostPixels == null || _cachedTileGhostPixelsSize < requiredSize)
            {
                _cachedTileGhostPixels = new byte[requiredSize];
                _cachedTileGhostPixelsSize = requiredSize;
            }

            // Create ghost transparency (50% alpha)
            for (int i = 0; i < pixels.Length; i += 4)
            {
                _cachedTileGhostPixels[i] = pixels[i];         // B
                _cachedTileGhostPixels[i + 1] = pixels[i + 1]; // G
                _cachedTileGhostPixels[i + 2] = pixels[i + 2]; // R
                _cachedTileGhostPixels[i + 3] = (byte)(pixels[i + 3] / 2); // A at 50%
            }

            // Calculate screen position and size
            float screenX = (float)(dest.X + docX * scale);
            float screenY = (float)(dest.Y + docY * scale);
            float screenW = (float)(tileW * scale);
            float screenH = (float)(tileH * scale);

            // Draw using the renderer abstraction
            var destRect = new Rect(screenX, screenY, screenW, screenH);
            var srcRect = new Rect(0, 0, tileW, tileH);

            renderer.DrawPixels(_cachedTileGhostPixels, tileW, tileH, destRect, srcRect, 1.0f, ImageInterpolation.NearestNeighbor);
        }
    }
}
