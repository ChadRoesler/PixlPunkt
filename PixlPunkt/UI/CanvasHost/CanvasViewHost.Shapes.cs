using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Painting;
using PixlPunkt.Core.Plugins;
using PixlPunkt.Core.Tools;
using Windows.Foundation;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// Shape tool subsystem for CanvasViewHost:
    /// - Shape drag state machine
    /// - Shape preview rendering
    /// - Modifier key handling (Shift for square, Ctrl for center)
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ════════════════════════════════════════════════════════════════════
        // SHAPE INPUT HANDLERS
        // ════════════════════════════════════════════════════════════════════

        private void HandleShapePressed(PointerPoint p, PointerRoutedEventArgs e)
        {
            if (!TryGetDocInside(p.Position, out var x, out var y)) return;

            _shapeIsEllipse = _toolState!.CurrentToolId == ToolIds.ShapeEllipse;

            // Get filled state from the tool's settings
            // This works for both built-in and plugin shape tools
            var toolId = _toolState.ActiveToolId;
            var settings = _toolState.GetSettingsForToolId(toolId);

            // Try to get Filled from the settings via reflection (for plugin tools)
            // or use the known built-in settings
            if (_shapeIsEllipse)
            {
                _shapeFilled = _toolState.Ellipse.Filled;
            }
            else if (toolId == ToolIds.ShapeRect)
            {
                _shapeFilled = _toolState.Rect.Filled;
            }
            else if (settings is PluginToolSettings pluginSettings)
            {
                // For plugin tools, check if they have a Filled property
                _shapeFilled = pluginSettings.Filled;
            }
            else
            {
                // Default to outlined
                _shapeFilled = false;
            }

            _sx = _ex = x;
            _sy = _ey = y;
            _shapeDrag = true;

            CanvasView.CapturePointer(e.Pointer);
            CanvasView.Invalidate();
        }

        private void HandleShapeMoved(Point pos)
        {
            // For shape dragging, convert screen to doc and clamp to document bounds
            // Don't use TryGetDocInside which rejects positions outside viewport
            var docPt = _zoom.ScreenToDoc(pos);
            int x = (int)Math.Floor(docPt.X);
            int y = (int)Math.Floor(docPt.Y);

            // Clamp to document bounds (allow dragging to edges)
            int w = Document.Surface.Width;
            int h = Document.Surface.Height;
            x = Math.Clamp(x, 0, w - 1);
            y = Math.Clamp(y, 0, h - 1);

            UpdateShapeEndpointsWithModifiers(x, y);
            CanvasView.Invalidate();
        }

        private void HandleShapeReleased()
        {
            _shapeDrag = false;

            OnBrushMoved(new Vector2(_hoverX, _hoverY), (float)((_brushSize - 1) * 0.5));

            if (Document.ActiveLayer is not Core.Document.Layer.RasterLayer rl) return;

            // Get shape registration and settings - use interface to support plugin tools
            var toolId = _toolState?.ActiveToolId ?? ToolIds.ShapeRect;
            var shapeReg = ToolRegistry.Shared.GetById<IShapeToolRegistration>(toolId);

            if (shapeReg?.ShapeBuilder == null) return;

            // Get settings from the tool's settings via IStrokeSettings/IOpacitySettings/IDensitySettings interfaces
            // This works for both built-in and plugin shape tools
            var settings = _toolState?.GetSettingsForToolId(toolId);

            int strokeWidth = 1;
            BrushShape brushShape = BrushShape.Circle;
            byte density = 255;
            byte opacity = 255;

            // Try to get stroke settings from the tool
            if (settings is IStrokeSettings strokeSettings)
            {
                strokeWidth = strokeSettings.Size;
                brushShape = strokeSettings.Shape;
            }
            else if (_shapeIsEllipse)
            {
                // Fallback for built-in tools without IStrokeSettings
                strokeWidth = _toolState?.Ellipse.StrokeWidth ?? 1;
                brushShape = _toolState?.Ellipse.Shape ?? BrushShape.Circle;
            }
            else
            {
                strokeWidth = _toolState?.Rect.StrokeWidth ?? 1;
                brushShape = _toolState?.Rect.Shape ?? BrushShape.Square;
            }

            // Try to get opacity from tool settings
            if (settings is IOpacitySettings opacitySettings)
            {
                opacity = opacitySettings.Opacity;
            }
            else if (_shapeIsEllipse)
            {
                opacity = _toolState?.Ellipse.Opacity ?? 255;
            }
            else
            {
                opacity = _toolState?.Rect.Opacity ?? 255;
            }

            // Try to get density from tool settings
            if (settings is IDensitySettings densitySettings)
            {
                density = densitySettings.Density;
            }
            else if (_shapeIsEllipse)
            {
                density = _toolState?.Ellipse.Density ?? 255;
            }
            else
            {
                density = _toolState?.Rect.Density ?? 255;
            }

            // Apply shape tool's opacity to the color
            uint color = (_fg & 0x00FFFFFFu) | ((uint)opacity << 24);

            // Build shape points using the shape builder
            int lx = Math.Min(_sx, _ex), rx = Math.Max(_sx, _ex);
            int ty = Math.Min(_sy, _ey), by = Math.Max(_sy, _ey);

            var points = _shapeFilled
                ? shapeReg.ShapeBuilder.BuildFilledPoints(lx, ty, rx, by)
                : shapeReg.ShapeBuilder.BuildOutlinePoints(lx, ty, rx, by);

            // Create render context with IsFilled for optimized rendering
            var context = new ShapeRenderContext
            {
                Surface = rl.Surface,
                Color = color,
                StrokeWidth = strokeWidth,
                BrushShape = brushShape,
                Opacity = opacity,
                Density = density,
                IsFilled = _shapeFilled,
                Description = _shapeFilled
                    ? $"Fill {shapeReg.DisplayName}"
                    : shapeReg.DisplayName
            };

            // Check if shape overlaps any mapped tiles
            var bounds = new Windows.Graphics.RectInt32(lx, ty, rx - lx + 1, by - ty + 1);
            bool hasTileMapping = rl.TileMapping != null && Document.TileSet != null;
            bool affectsTiles = false;

            if (hasTileMapping)
            {
                // Check if the shape bounds intersect any mapped tiles
                var mapping = rl.TileMapping!;
                var tileSet = Document.TileSet!;
                int tileW = tileSet.TileWidth;
                int tileH = tileSet.TileHeight;

                int startTileX = Math.Max(0, lx / tileW);
                int startTileY = Math.Max(0, ty / tileH);
                int endTileX = Math.Min(mapping.Width - 1, rx / tileW);
                int endTileY = Math.Min(mapping.Height - 1, by / tileH);

                for (int tileY = startTileY; tileY <= endTileY && !affectsTiles; tileY++)
                {
                    for (int tileX = startTileX; tileX <= endTileX && !affectsTiles; tileX++)
                    {
                        if (mapping.GetTileId(tileX, tileY) >= 0)
                        {
                            affectsTiles = true;
                        }
                    }
                }
            }

            if (affectsTiles)
            {
                // Capture tile states before rendering
                var tileBeforeStates = new Dictionary<int, byte[]>();
                var tileSet = Document.TileSet!;
                foreach (var tileId in tileSet.TileIds)
                {
                    var pixels = tileSet.GetTilePixels(tileId);
                    if (pixels != null)
                    {
                        tileBeforeStates[tileId] = (byte[])pixels.Clone();
                    }
                }

                // Capture layer pixels before rendering
                var pixelsBefore = CopyRectBytes(rl.Surface.Pixels, rl.Surface.Width, rl.Surface.Height, bounds);

                // Render the shape
                shapeReg.EffectiveRenderer.Render(rl, points, context);

                // Capture layer pixels after rendering
                var pixelsAfter = CopyRectBytes(rl.Surface.Pixels, rl.Surface.Width, rl.Surface.Height, bounds);

                // Propagate to mapped tiles
                PropagateSelectionChangesToMappedTiles(bounds);

                // Create a tile-aware history item
                var tileAwareItem = new Core.History.TileAwarePixelChangeItem(
                    rl, tileSet, bounds, pixelsBefore, pixelsAfter, context.Description);

                Document.History.Push(tileAwareItem);
            }
            else
            {
                // No tiles affected - use standard rendering flow
                var result = shapeReg.EffectiveRenderer.Render(rl, points, context);

                // Push to unified history if result supports it
                if (result is { CanPushToHistory: true } and Core.History.IHistoryItem historyItem)
                {
                    Document.History.Push(historyItem);
                }
            }

            // Restore brush settings after shape drawing
            SyncBrushPreviewFromToolSettings();

            // Commit without pushing to history (already pushed above)
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            CanvasView.Invalidate();
            HistoryStateChanged?.Invoke();
            RaiseFrame();

            CanvasView.ReleasePointerCaptures();
        }

        // ════════════════════════════════════════════════════════════════════
        // SHAPE BUILDER ACCESS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the shape builder for the current tool from the registry.
        /// </summary>
        /// <returns>The shape builder, or null if the tool doesn't use one.</returns>
        private IShapeBuilder? GetShapeBuilderForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.ShapeRect;
            // Use interface to support both built-in and plugin shape tools
            var shapeReg = ToolRegistry.Shared.GetById<IShapeToolRegistration>(toolId);
            return shapeReg?.ShapeBuilder;
        }

        // ════════════════════════════════════════════════════════════════════
        // SHAPE MODIFIERS
        // ════════════════════════════════════════════════════════════════════

        private void UpdateShapeEndpointsWithModifiers(int x, int y)
        {
            bool shift = IsKeyDown(Windows.System.VirtualKey.Shift);
            bool ctrl = IsKeyDown(Windows.System.VirtualKey.Control);

            // Try to get shape builder from current tool
            var shapeBuilder = GetShapeBuilderForCurrentTool();

            if (shapeBuilder != null)
            {
                // Use shape builder's modifier logic
                // Pass the ORIGINAL start point (_sx, _sy at drag start) and current cursor (x, y)
                var (x0, y0, x1, y1) = shapeBuilder.ApplyModifiers(_sx, _sy, x, y, shift, ctrl);

                // Update the endpoint - the preview uses Math.Min/Max(_sx,_ex) to normalize
                // so for non-Ctrl mode, _sx stays as anchor and _ex tracks the cursor
                _ex = x1;
                _ey = y1;
            }
            else
            {
                // Fallback to legacy modifier handling
                if (!ctrl)
                {
                    _ex = x; _ey = y;
                    if (shift)
                    {
                        int dx = Math.Abs(_ex - _sx);
                        int dy = Math.Abs(_ey - _sy);
                        int d = Math.Max(dx, dy);
                        _ex = (_ex >= _sx) ? _sx + d : _sx - d;
                        _ey = (_ey >= _sy) ? _sy + d : _sy - d;
                    }
                }
                else
                {
                    int dx = x - _sx, dy = y - _sy;
                    if (shift)
                    {
                        int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        dx = (dx >= 0) ? d : -d;
                        dy = (dy >= 0) ? d : -d;
                    }
                    _ex = _sx + dx;
                    _ey = _sy + dy;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SHAPE PREVIEW RENDERING
        // ════════════════════════════════════════════════════════════════════

        private void DrawShapePreview(CanvasDrawingSession ds, Rect dest)
        {
            double s = _zoom.Scale;

            int lx = Math.Min(_sx, _ex), rx = Math.Max(_sx, _ex);
            int ty = Math.Min(_sy, _ey), by = Math.Max(_sy, _ey);

            // Try to get shape builder from current tool
            var shapeBuilder = GetShapeBuilderForCurrentTool();

            if (shapeBuilder != null)
            {
                // Use shape builder for point generation
                var points = _shapeFilled
                    ? shapeBuilder.BuildFilledPoints(lx, ty, rx, by)
                    : shapeBuilder.BuildOutlinePoints(lx, ty, rx, by);

                if (_shapeFilled)
                    PreviewFilledShape(ds, dest, s, points);
                else
                    PreviewBrushStroke(ds, dest, s, points);
            }
            else
            {
                // Fallback to legacy preview rendering
                if (_shapeIsEllipse)
                {
                    PreviewEllipse(ds, dest, s, lx, ty, rx, by, _shapeFilled);
                }
                else
                {
                    if (_shapeFilled)
                    {
                        var filled = new HashSet<(int x, int y)>();
                        for (int y = ty; y <= by; y++)
                            for (int x = lx; x <= rx; x++)
                                filled.Add((x, y));

                        PreviewFilledShape(ds, dest, s, filled);
                    }
                    else
                    {
                        var outline = new HashSet<(int x, int y)>();

                        for (int x = lx; x <= rx; x++)
                        {
                            outline.Add((x, ty));
                            outline.Add((x, by));
                        }
                        for (int y2 = ty + 1; y2 <= by - 1; y2++)
                        {
                            outline.Add((lx, y2));
                            if (rx != lx) outline.Add((rx, y2));
                        }

                        PreviewBrushStroke(ds, dest, s, outline);
                    }
                }
            }
        }

        private void PreviewPlot(CanvasDrawingSession ds, Rect dest, double scale, int x, int y)
        {
            if ((uint)x >= (uint)Document.Surface.Width || (uint)y >= (uint)Document.Surface.Height) return;

            uint before = ReadCompositeBGRA(x, y);
            uint after = ColorUtil.BlendOver(before, _fg);

            float sx = (float)(dest.X + x * scale);
            float sy = (float)(dest.Y + y * scale);
            float s = (float)scale;

            ds.FillRectangle(sx, sy, s, s, ColorUtil.ToColor(after));
        }

        private void PreviewHSpan(CanvasDrawingSession ds, Rect dest, double s, int y, int x0, int x1)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            for (int x = x0; x <= x1; x++) PreviewPlot(ds, dest, s, x, y);
        }

        private void PreviewEllipse(CanvasDrawingSession ds, Rect dest, double s, int x0, int y0, int x1, int y1, bool filled)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (y0 > y1) (y0, y1) = (y1, y0);

            int a = Math.Abs(x1 - x0);
            int b = Math.Abs(y1 - y0);
            int b1 = b & 1;

            long dx = 4L * (1 - a) * b * b;
            long dy = 4L * (b1 + 1) * a * a;
            long err = dx + dy + b1 * a * a;
            long e2;

            y0 += (b + 1) / 2;
            y1 = y0 - b1;

            long aa8 = 8L * a * a;
            long bb8 = 8L * b * b;

            var outline = new HashSet<(int x, int y)>();

            if (filled)
            {
                void HLine(int y, int lx, int rx)
                {
                    if (lx > rx) (lx, rx) = (rx, lx);
                    for (int x = lx; x <= rx; x++) outline.Add((x, y));
                }

                do
                {
                    HLine(y0, x0, x1);
                    if (y0 != y1) HLine(y1, x0, x1);

                    e2 = 2 * err;
                    if (e2 <= dy) { y0++; y1--; err += dy += aa8; }
                    if (e2 >= dx || 2 * err > dy) { x0++; x1--; err += dx += bb8; }
                }
                while (x0 <= x1);

                while ((y0 - y1) <= b)
                {
                    HLine(y0, x0 - 1, x1 + 1);
                    HLine(y1, x0 - 1, x1 + 1);
                    y0++; y1--;
                }

                PreviewFilledShape(ds, dest, s, outline);
            }
            else
            {
                do
                {
                    outline.Add((x1, y0));
                    outline.Add((x0, y0));
                    outline.Add((x0, y1));
                    outline.Add((x1, y1));

                    e2 = 2 * err;
                    if (e2 <= dy) { y0++; y1--; err += dy += aa8; }
                    if (e2 >= dx || 2 * err > dy) { x0++; x1--; err += dx += bb8; }
                }
                while (x0 <= x1);

                while ((y0 - y1) <= b)
                {
                    outline.Add((x0 - 1, y0));
                    outline.Add((x1 + 1, y0));
                    outline.Add((x0 - 1, y1));
                    outline.Add((x1 + 1, y1));
                    y0++; y1--;
                }

                PreviewBrushStroke(ds, dest, s, outline);
            }
        }

        /// <summary>
        /// Preview for filled shapes: solid interior with opacity, soft edge only on outside.
        /// </summary>
        private void PreviewFilledShape(CanvasDrawingSession ds, Rect dest, double scale, HashSet<(int x, int y)> shapePoints)
        {
            int w = Document.Surface.Width;
            int h = Document.Surface.Height;
            byte fgAlpha = (byte)(_fg >> 24);

            // Step 1: Render all interior pixels with solid opacity
            foreach (var (px, py) in shapePoints)
            {
                if ((uint)px >= (uint)w || (uint)py >= (uint)h)
                    continue;

                uint before = ReadCompositeBGRA(px, py);
                uint src = (_fg & 0x00FFFFFFu) | ((uint)fgAlpha << 24);
                uint after = ColorUtil.BlendOver(before, src);

                float sx = (float)(dest.X + px * scale);
                float sy = (float)(dest.Y + py * scale);
                ds.FillRectangle(sx, sy, (float)scale, (float)scale, ColorUtil.ToColor(after));
            }

            // Step 2: Add soft outer edge if density < 255 or strokeWidth > 1
            var mask = _stroke.GetCurrentBrushOffsets();
            if (mask == null || mask.Count <= 1)
                return;

            // Find boundary pixels
            var boundaryPixels = new HashSet<(int x, int y)>();
            foreach (var (px, py) in shapePoints)
            {
                if ((uint)px >= (uint)w || (uint)py >= (uint)h)
                    continue;

                if (!shapePoints.Contains((px - 1, py)) ||
                    !shapePoints.Contains((px + 1, py)) ||
                    !shapePoints.Contains((px, py - 1)) ||
                    !shapePoints.Contains((px, py + 1)))
                {
                    boundaryPixels.Add((px, py));
                }
            }

            // Stamp brush at boundary pixels, only affecting pixels OUTSIDE the shape
            var outerPixelAlphas = new Dictionary<(int x, int y), byte>();

            foreach (var (bx, by) in boundaryPixels)
            {
                foreach (var (dx, dy) in mask)
                {
                    int px = bx + dx;
                    int py = by + dy;

                    if ((uint)px >= (uint)w || (uint)py >= (uint)h)
                        continue;

                    // Only affect pixels OUTSIDE the shape
                    if (shapePoints.Contains((px, py)))
                        continue;

                    byte effA = _stroke.ComputeBrushAlphaAtOffset(dx, dy);
                    if (effA == 0) continue;

                    var key = (px, py);
                    if (!outerPixelAlphas.TryGetValue(key, out byte currentAlpha) || effA > currentAlpha)
                    {
                        outerPixelAlphas[key] = effA;
                    }
                }
            }

            // Draw outer soft edge
            foreach (var (pos, alpha) in outerPixelAlphas)
            {
                uint before = ReadCompositeBGRA(pos.x, pos.y);
                uint src = (_fg & 0x00FFFFFFu) | ((uint)alpha << 24);
                uint after = ColorUtil.BlendOver(before, src);

                float sx = (float)(dest.X + pos.x * scale);
                float sy = (float)(dest.Y + pos.y * scale);
                ds.FillRectangle(sx, sy, (float)scale, (float)scale, ColorUtil.ToColor(after));
            }
        }

        /// <summary>
        /// Stamps the full brush shape at each outline point independently, matching final rendering.
        /// Used for outlined (non-filled) shapes.
        /// </summary>
        private void PreviewBrushStroke(CanvasDrawingSession ds, Rect dest, double scale, HashSet<(int x, int y)> outlinePoints)
        {
            var mask = _stroke.GetCurrentBrushOffsets();
            if (mask == null || mask.Count == 0)
            {
                foreach (var (x, y) in outlinePoints)
                    PreviewPlot(ds, dest, scale, x, y);
                return;
            }

            var pixelAlphas = new Dictionary<(int x, int y), byte>();

            foreach (var (ox, oy) in outlinePoints)
            {
                foreach (var (dx, dy) in mask)
                {
                    int px = ox + dx;
                    int py = oy + dy;

                    if ((uint)px >= (uint)Document.Surface.Width || (uint)py >= (uint)Document.Surface.Height)
                        continue;

                    byte effA = _stroke.ComputeBrushAlphaAtOffset(dx, dy);
                    if (effA == 0) continue;

                    var key = (px, py);
                    if (!pixelAlphas.TryGetValue(key, out byte currentAlpha) || effA > currentAlpha)
                    {
                        pixelAlphas[key] = effA;
                    }
                }
            }

            foreach (var (pos, alpha) in pixelAlphas)
            {
                uint before = ReadCompositeBGRA(pos.x, pos.y);
                uint src = (_fg & 0x00FFFFFFu) | ((uint)alpha << 24);
                uint after = ColorUtil.BlendOver(before, src);

                float sx = (float)(dest.X + pos.x * scale);
                float sy = (float)(dest.Y + pos.y * scale);
                ds.FillRectangle(sx, sy, (float)scale, (float)scale, ColorUtil.ToColor(after));
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SHAPE START POINT PREVIEW
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws brush hover preview at the shape start point on the main canvas.
        /// </summary>
        private void DrawShapeStartPointHover(CanvasDrawingSession ds, Rect dest)
        {
            double s = _zoom.Scale;
            var mask = _stroke.GetCurrentBrushOffsets();

            int centerX = _hoverX;
            int centerY = _hoverY;

            foreach (var (dx, dy) in mask)
            {
                int px = centerX + dx;
                int py = centerY + dy;

                if ((uint)px >= (uint)Document.Surface.Width || (uint)py >= (uint)Document.Surface.Height)
                    continue;

                byte effA = _stroke.ComputeBrushAlphaAtOffset(dx, dy);
                if (effA == 0) continue;

                uint before = ReadCompositeBGRA(px, py);
                uint src = (_fg & 0x00FFFFFFu) | ((uint)effA << 24);
                uint after = ColorUtil.BlendOver(before, src);

                float sx = (float)(dest.X + px * s);
                float sy = (float)(dest.Y + py * s);
                ds.FillRectangle(sx, sy, (float)s, (float)s, ColorUtil.ToColor(after));
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SHAPE PREVIEW ALPHA HELPERS
        // ════════════════════════════════════════════════════════════════════

        private void ApplyBrushStrokePreview(HashSet<(int x, int y)> points, Dictionary<(int x, int y), byte> pixelAlphas)
        {
            var mask = _stroke?.GetCurrentBrushOffsets();
            if (mask == null || mask.Count == 0)
            {
                foreach (var pt in points)
                    pixelAlphas[pt] = (byte)(_fg >> 24);
                return;
            }

            int sz = _brushSize;
            BrushShape shape = _toolState?.Brush.Shape ?? BrushShape.Square;
            byte density = _brushDensity;
            byte fgAlpha = (byte)(_fg >> 24);

            foreach (var (ox, oy) in points)
            {
                foreach (var (dx, dy) in mask)
                {
                    int px = ox + dx;
                    int py = oy + dy;

                    byte effA = ComputePreviewBrushAlpha(dx, dy, sz, shape, density, fgAlpha);
                    if (effA == 0) continue;

                    var key = (px, py);
                    if (!pixelAlphas.TryGetValue(key, out byte currentAlpha) || effA > currentAlpha)
                    {
                        pixelAlphas[key] = effA;
                    }
                }
            }
        }

        private byte ComputePreviewBrushAlpha(int dx, int dy, int size, BrushShape shape, byte density, byte fgAlpha)
        {
            double Aop = fgAlpha / 255.0;
            if (Aop <= 0.0) return 0;

            int sz = Math.Max(1, size);
            double r = sz / 2.0;

            double frac = (sz & 1) == 0 ? 0.5 : 0.0;
            double px = dx + 0.5 - frac;
            double py = dy + 0.5 - frac;

            double d = shape == BrushShape.Circle
                ? Math.Sqrt(px * px + py * py)
                : Math.Max(Math.Abs(px), Math.Abs(py));

            if (d > r) return 0;

            double D = density / 255.0;
            double Rhard = r * D;

            if (d <= Rhard)
                return (byte)Math.Round(255.0 * Aop);

            double span = Math.Max(1e-6, (r - Rhard));
            double t = (d - Rhard) / span;
            double mask = 1.0 - (t * t) * (3 - 2 * t);
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }
    }
}
