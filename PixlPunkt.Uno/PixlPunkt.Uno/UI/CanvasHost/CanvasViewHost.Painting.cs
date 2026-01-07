using System;
using System.Collections.Generic;
using FluentIcons.Common;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Brush;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.History;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.Core.Painting;
using PixlPunkt.Uno.Core.Plugins;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Settings;
using Windows.Foundation;

namespace PixlPunkt.Uno.UI.CanvasHost
{
    /// <summary>
    /// Painting subsystem for CanvasViewHost:
    /// - Painting state machine
    /// - Stroke begin/commit
    /// - Painter management
    /// - Tool-specific press/move handlers
    /// - Shift-line and angle snapping support
    /// - Pixel-perfect mode for 1px brushes
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ════════════════════════════════════════════════════════════════════
        // MASK EDITING SUPPORT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the target surface for painting - either the layer surface or the mask surface.
        /// </summary>
        /// <returns>The surface to paint on, or null if no valid target.</returns>
        private PixlPunkt.Uno.Core.Imaging.PixelSurface? GetPaintTargetSurface()
        {
            if (Document.ActiveLayer is not RasterLayer rl) return null;

            // If editing mask, return the mask surface
            if (rl.IsEditingMask && rl.Mask != null)
            {
                return rl.Mask.Surface;
            }

            // Otherwise return the layer surface
            return rl.Surface;
        }

        /// <summary>
        /// Gets whether we're currently editing a mask.
        /// </summary>
        private bool IsEditingMask => Document.ActiveLayer is RasterLayer rl && rl.IsEditingMask && rl.Mask != null;

        // ════════════════════════════════════════════════════════════════════
        // ANGLE SNAPPING CONSTANTS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Angle snap increment in degrees (15° = 24 snaps per circle)</summary>
        private const double AngleSnapDegrees = 15.0;

        // ════════════════════════════════════════════════════════════════════
        // PIXEL PERFECT MODE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Filter for pixel-perfect stroke input.</summary>
        private readonly PixelPerfectFilter _pixelPerfectFilter = new();

        /// <summary>Whether pixel-perfect mode is active for the current stroke.</summary>
        private bool _pixelPerfectActive;

        /// <summary>
        /// Checks if pixel-perfect mode should be active based on current tool settings.
        /// </summary>
        private bool ShouldUsePixelPerfect()
        {
            var strokeSettings = GetStrokeSettingsForCurrentTool();
            if (strokeSettings == null || strokeSettings.Size != 1)
                return false;

            // Check if the settings support pixel-perfect and it's enabled
            if (strokeSettings is BrushToolSettings brushSettings)
                return brushSettings.PixelPerfect;

            return false;
        }

        // ════════════════════════════════════════════════════════════════════
        // SHIFT-LINE / ANGLE SNAPPING HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Computes the target point for a shift-line from origin to the given point,
        /// optionally snapping to angle increments.
        /// </summary>
        private static (int x, int y) ComputeShiftLineTarget(int originX, int originY, int targetX, int targetY, bool snapAngle)
        {
            if (!snapAngle)
            {
                return (targetX, targetY);
            }

            double dx = targetX - originX;
            double dy = targetY - originY;

            if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                return (originX, originY);

            double distance = Math.Sqrt(dx * dx + dy * dy);
            double angleRad = Math.Atan2(dy, dx);
            double angleDeg = angleRad * (180.0 / Math.PI);

            double snappedDeg = Math.Round(angleDeg / AngleSnapDegrees) * AngleSnapDegrees;
            double snappedRad = snappedDeg * (Math.PI / 180.0);

            int snappedX = originX + (int)Math.Round(distance * Math.Cos(snappedRad));
            int snappedY = originY + (int)Math.Round(distance * Math.Sin(snappedRad));

            return (snappedX, snappedY);
        }

        /// <summary>
        /// Returns the brush footprint extents around the stamp point for the ACTIVE tool's brush mask.
        /// </summary>
        private IReadOnlyList<(int dx, int dy)>? _cachedBoundsMask;
        private int _cachedMinDx, _cachedMaxDx, _cachedMinDy, _cachedMaxDy;

        private void GetBrushBoundsFromMask(IStrokeSettings? strokeSettings,
                                           out int minDx, out int minDy,
                                           out int maxDx, out int maxDy)
        {
            if (strokeSettings == null)
            {
                int sz = Math.Max(1, _brushSize);
                int ox = (int)Math.Floor(sz / 2.0);
                minDx = -ox;
                minDy = -ox;
                maxDx = (sz - 1) - ox;
                maxDy = (sz - 1) - ox;
                return;
            }

            IReadOnlyList<(int dx, int dy)> mask;
            if (strokeSettings is ICustomBrushSettings custom && custom.IsCustomBrushSelected)
            {
                var brush = BrushDefinitionService.Instance.GetBrush(custom.CustomBrushFullName!);
                mask = brush != null
                    ? BrushMaskCache.Shared.GetOffsetsForCustomBrush(brush, strokeSettings.Size)
                    : BrushMaskCache.Shared.GetOffsets(strokeSettings.Shape, strokeSettings.Size);
            }
            else
            {
                mask = BrushMaskCache.Shared.GetOffsets(strokeSettings.Shape, strokeSettings.Size);
            }

            if (!ReferenceEquals(mask, _cachedBoundsMask))
            {
                int minX = 0, maxX = 0, minY = 0, maxY = 0;
                bool first = true;

                foreach (var (dx, dy) in mask)
                {
                    if (first)
                    {
                        minX = maxX = dx;
                        minY = maxY = dy;
                        first = false;
                    }
                    else
                    {
                        if (dx < minX) minX = dx;
                        if (dx > maxX) maxX = dx;
                        if (dy < minY) minY = dy;
                        if (dy > maxY) maxY = dy;
                    }
                }

                if (first) { minX = maxX = 0; minY = maxY = 0; }

                _cachedBoundsMask = mask;
                _cachedMinDx = minX;
                _cachedMaxDx = maxX;
                _cachedMinDy = minY;
                _cachedMaxDy = maxY;
            }

            minDx = _cachedMinDx;
            maxDx = _cachedMaxDx;
            minDy = _cachedMinDy;
            maxDy = _cachedMaxDy;
        }

        // ════════════════════════════════════════════════════════════════════
        // PAINTER MANAGEMENT
        // ════════════════════════════════════════════════════════════════════

        private IStrokePainter? GetPainterForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;

            var brushReg = ToolRegistry.Shared.GetById<BrushToolRegistration>(toolId);
            if (brushReg != null) return brushReg.CreatePainter();

            var pluginBrushReg = ToolRegistry.Shared.GetById<PluginBrushToolRegistration>(toolId);
            if (pluginBrushReg != null) return pluginBrushReg.CreatePainter();

            var shapeReg = ToolRegistry.Shared.GetById<ShapeToolRegistration>(toolId);
            if (shapeReg != null) return shapeReg.CreatePainter();

            return null;
        }

        private IStrokeSettings? GetStrokeSettingsForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;

            var brushReg = ToolRegistry.Shared.GetById<BrushToolRegistration>(toolId);
            if (brushReg != null) return brushReg.StrokeSettings;

            var pluginBrushReg = ToolRegistry.Shared.GetById<PluginBrushToolRegistration>(toolId);
            if (pluginBrushReg != null) return pluginBrushReg.StrokeSettings;

            var shapeReg = ToolRegistry.Shared.GetById<IShapeToolRegistration>(toolId);
            if (shapeReg?.Settings is IStrokeSettings shapeStrokeSettings)
                return shapeStrokeSettings;

            var registration = ToolRegistry.Shared.GetById(toolId);
            return registration?.Settings as IStrokeSettings;
        }

        // ════════════════════════════════════════════════════════════════════
        // JUMBLE TOOL HANDLERS
        // ════════════════════════════════════════════════════════════════════

        private void HandleJumblePressed(PointerPoint p, PointerRoutedEventArgs e)
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            bool shiftHeld = IsKeyDown(Windows.System.VirtualKey.Shift);
            UpdateStrokeEngineSelectionMask();
            BeginLiveTilePropagation();

            if (TryGetDocWithBrushOverlap(p.Position, out var x, out var y))
            {
                _isPainting = true;
                _isActivePainting = true;
                _pendingStrokeFromOutside = false;

                // Start rapid invalidation timer for Skia platforms
                StartPaintInvalidationTimer();

                if (shiftHeld)
                {
                    _shiftLineActive = true;
                    _shiftLineOriginX = x;
                    _shiftLineOriginY = y;
                }
                else
                {
                    _shiftLineActive = false;
                    _activePainter = GetPainterForCurrentTool();
                    if (_activePainter != null)
                    {
                        _stroke.BeginWithPainter(_activePainter, rl);
                    }

                    var strokeSettings = GetStrokeSettingsForCurrentTool();
                    if (strokeSettings != null && _activePainter != null)
                    {
                        _stroke.StampAtWithPainter(x, y, _fg, _bgColor, strokeSettings);
                        GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);
                        PropagateLiveTileChanges(x + minDx, y + minDy, x + maxDx, y + maxDy);
                    }
                }

                _hasLastDocPos = true;
                _didMove = false;
                _lastDocX = x;
                _lastDocY = y;
            }
            else
            {
                _isPainting = false;
                _isActivePainting = false;
                _pendingStrokeFromOutside = true;
                _hasLastDocPos = false;
                _didMove = false;
                _shiftLineActive = false;
            }

            CanvasView.CapturePointer(e.Pointer);
        }

        private void HandleJumbleMoved(Point pos)
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            var docPt = _zoom.ScreenToDoc(pos);
            int rawX = (int)Math.Floor(docPt.X);
            int rawY = (int)Math.Floor(docPt.Y);

            bool inside = TryGetDocWithBrushOverlap(pos, out var x, out var y);

            if (_pendingStrokeFromOutside)
            {
                if (!inside) return;

                _pendingStrokeFromOutside = false;
                _isPainting = true;
                _isActivePainting = true;
                _shiftLineActive = false;

                // Start rapid invalidation timer when deferred stroke starts
                StartPaintInvalidationTimer();

                _activePainter = GetPainterForCurrentTool();
                if (_activePainter != null)
                {
                    _stroke.BeginWithPainter(_activePainter, rl);
                }

                _hasLastDocPos = false;
                _didMove = false;
                _lastDocX = x;
                _lastDocY = y;
            }

            if (!_isPainting) return;

            if (_shiftLineActive)
            {
                bool ctrlHeld = IsKeyDown(Windows.System.VirtualKey.Control);
                var (targetX, targetY) = ComputeShiftLineTarget(_shiftLineOriginX, _shiftLineOriginY, rawX, rawY, ctrlHeld);
                _lastDocX = targetX;
                _lastDocY = targetY;
                _didMove = true;
                OnBrushMoved(new System.Numerics.Vector2(_hoverX, _hoverY), (float)((_brushSize - 1) * 0.5));
                ForceInvalidate();
                return;
            }

            if (!inside)
            {
                _hasLastDocPos = false;
                return;
            }

            var strokeSettings = GetStrokeSettingsForCurrentTool();
            if (_activePainter != null && strokeSettings != null && _stroke.HasActivePainterStroke)
            {
                GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);

                if (_hasLastDocPos)
                {
                    _stroke.StampLineWithPainter(_lastDocX, _lastDocY, x, y, _fg, _bgColor, strokeSettings);
                    int minX = Math.Min(_lastDocX, x) + minDx;
                    int minY = Math.Min(_lastDocY, y) + minDy;
                    int maxX = Math.Max(_lastDocX, x) + maxDx;
                    int maxY = Math.Max(_lastDocY, y) + maxDy;
                    PropagateLiveTileChanges(minX, minY, maxX, maxY);
                }
                else
                {
                    _stroke.StampAtWithPainter(x, y, _fg, _bgColor, strokeSettings);
                    PropagateLiveTileChanges(x + minDx, y + minDy, x + maxDx, y + maxDy);
                }

                _lastDocX = x;
                _lastDocY = y;
            }

            _didMove = true;
            _hasLastDocPos = true;

            // Composite the changes to the visible surface
            Document.CompositeTo(Document.Surface);
            Document.RaiseDocumentModified();
            // Timer handles invalidation, but call it explicitly too for responsiveness
            ForceInvalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        // FILL TOOL HANDLER
        // ════════════════════════════════════════════════════════════════════

        private void HandleFillPressed(PointerPoint p)
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            if (TryGetDocInside(p.Position, out var fx, out var fy))
            {
                var fillReg = ToolRegistry.Shared.GetById<FillToolRegistration>(ToolIds.Fill);
                if (fillReg == null) return;

                var icon = fillReg.Settings?.Icon ?? Icon.PaintBucket;

                bool hasActiveSelection = _selState?.Active == true && !(_selState?.Floating ?? true);
                Func<int, int, bool>? selectionMask = null;

                if (hasActiveSelection && !_selRegion.IsEmpty)
                {
                    selectionMask = (x, y) => _selRegion.Contains(x, y);
                }

                var context = new FillContext
                {
                    Surface = rl.Surface,
                    Color = _fg,
                    Tolerance = _fillTolerance,
                    Contiguous = _fillContiguous,
                    Description = _fillContiguous ? "Fill" : "Global Fill",
                    SelectionMask = selectionMask
                };

                var result = fillReg.EffectiveFillPainter.FillAt(rl, fx, fy, context);

                if (result != null) result.HistoryIcon = icon;

                if (result is { CanPushToHistory: true } and IHistoryItem historyItem)
                {
                    Document.History.Push(historyItem);
                }

                UpdateActiveLayerPreview();
                CanvasView.Invalidate();
                HistoryStateChanged?.Invoke();
                RaiseFrame();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // NORMAL PAINTING HANDLERS
        // ════════════════════════════════════════════════════════════════════

        private void HandlePaintingPressed(PointerPoint p, PointerRoutedEventArgs e)
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            bool shiftHeld = IsKeyDown(Windows.System.VirtualKey.Shift);
            UpdateStrokeEngineSelectionMask();
            BeginLiveTilePropagation();

            _pixelPerfectActive = ShouldUsePixelPerfect() && !shiftHeld;
            if (_pixelPerfectActive) _pixelPerfectFilter.Reset();

            if (TryGetDocWithBrushOverlap(p.Position, out var x, out var y))
            {
                _isPainting = true;
                _isActivePainting = true;
                _pendingStrokeFromOutside = false;

                // Start rapid invalidation timer for Skia platforms
                StartPaintInvalidationTimer();

                if (shiftHeld)
                {
                    _shiftLineActive = true;
                    _shiftLineOriginX = x;
                    _shiftLineOriginY = y;
                }
                else
                {
                    _shiftLineActive = false;
                    _activePainter = GetPainterForCurrentTool();
                    if (_activePainter != null)
                    {
                        _stroke.BeginWithPainter(_activePainter, rl);
                    }

                    var strokeSettings = GetStrokeSettingsForCurrentTool();
                    if (strokeSettings != null && _activePainter != null)
                    {
                        if (_pixelPerfectActive)
                        {
                            _pixelPerfectFilter.FilterWithLookahead(x, y, out bool shouldDraw, out int drawX, out int drawY);
                            if (shouldDraw)
                            {
                                _stroke.StampAtWithPainter(drawX, drawY, _fg, _bgColor, strokeSettings);
                                GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);
                                PropagateLiveTileChanges(drawX + minDx, drawX + maxDx, drawY + minDy, drawY + maxDy);
                            }
                        }
                        else
                        {
                            _stroke.StampAtWithPainter(x, y, _fg, _bgColor, strokeSettings);
                            GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);
                            PropagateLiveTileChanges(x + minDx, x + maxDx, y + minDy, y + maxDy);
                        }

                        Document.CompositeTo(Document.Surface);
                        Document.RaiseDocumentModified();
                    }
                }

                _hasLastDocPos = true;
                _didMove = false;
                _lastDocX = x;
                _lastDocY = y;
            }
            else
            {
                _isPainting = false;
                _isActivePainting = false;
                _pendingStrokeFromOutside = true;
                _hasLastDocPos = false;
                _didMove = false;
                _shiftLineActive = false;
            }

            CanvasView.CapturePointer(e.Pointer);
        }

        private void UpdateStrokeEngineSelectionMask()
        {
            bool hasActiveSelection = _selState?.Active == true && !(_selState?.Floating ?? true);

            if (hasActiveSelection && !_selRegion.IsEmpty)
            {
                _stroke.SetSelectionMask((x, y) => _selRegion.Contains(x, y));
            }
            else
            {
                _stroke.SetSelectionMask(null);
            }
        }

        private void HandlePaintingMoved(Point pos)
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            var docPt = _zoom.ScreenToDoc(pos);
            int rawX = (int)Math.Floor(docPt.X);
            int rawY = (int)Math.Floor(docPt.Y);

            bool inside = TryGetDocWithBrushOverlap(pos, out var x, out var y);

            if (_pendingStrokeFromOutside)
            {
                if (!inside) return;

                _pendingStrokeFromOutside = false;
                _isPainting = true;
                _isActivePainting = true;
                _shiftLineActive = false;

                // Start rapid invalidation timer when deferred stroke starts
                StartPaintInvalidationTimer();

                if (_pixelPerfectActive) _pixelPerfectFilter.Reset();

                _activePainter = GetPainterForCurrentTool();
                if (_activePainter != null)
                {
                    _stroke.BeginWithPainter(_activePainter, rl);
                }

                _hasLastDocPos = false;
                _didMove = false;
                _lastDocX = x;
                _lastDocY = y;
                _shiftLineOriginX = x;
                _shiftLineOriginY = y;
            }

            if (!_isPainting) return;

            if (_shiftLineActive)
            {
                bool ctrlHeld = IsKeyDown(Windows.System.VirtualKey.Control);
                var (targetX, targetY) = ComputeShiftLineTarget(_shiftLineOriginX, _shiftLineOriginY, rawX, rawY, ctrlHeld);
                _lastDocX = targetX;
                _lastDocY = targetY;
                _didMove = true;
                OnBrushMoved(new System.Numerics.Vector2(_hoverX, _hoverY), (float)((_brushSize - 1) * 0.5));
                ForceInvalidate();
                return;
            }

            if (!inside)
            {
                _hasLastDocPos = false;
                return;
            }

            var strokeSettings = GetStrokeSettingsForCurrentTool();
            if (_activePainter != null && strokeSettings != null && _stroke.HasActivePainterStroke)
            {
                GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);

                if (_pixelPerfectActive)
                {
                    _pixelPerfectFilter.FilterWithLookahead(x, y, out bool shouldDraw, out int drawX, out int drawY);

                    if (shouldDraw)
                    {
                        if (_hasLastDocPos && (_lastDocX != drawX || _lastDocY != drawY))
                        {
                            _stroke.StampLineWithPainter(_lastDocX, _lastDocY, drawX, drawY, _fg, _bgColor, strokeSettings);
                            _didMove = true;

                            int lineMinX = Math.Min(_lastDocX, drawX) + minDx;
                            int lineMinY = Math.Min(_lastDocY, drawY) + minDy;
                            int lineMaxX = Math.Max(_lastDocX, drawX) + maxDx;
                            int lineMaxY = Math.Max(_lastDocY, drawY) + maxDy;
                            PropagateLiveTileChanges(lineMinX, lineMinY, lineMaxX, lineMaxY);
                        }
                        else
                        {
                            _stroke.StampAtWithPainter(drawX, drawY, _fg, _bgColor, strokeSettings);
                            _didMove = true;
                            PropagateLiveTileChanges(drawX + minDx, drawY + minDy, drawX + maxDx, drawY + maxDy);
                        }

                        _lastDocX = drawX;
                        _lastDocY = drawY;
                        _hasLastDocPos = true;
                    }
                }
                else
                {
                    if (_hasLastDocPos)
                    {
                        _stroke.StampLineWithPainter(_lastDocX, _lastDocY, x, y, _fg, _bgColor, strokeSettings);
                        _didMove = true;

                        int lineMinX = Math.Min(_lastDocX, x) + minDx;
                        int lineMinY = Math.Min(_lastDocY, y) + minDy;
                        int lineMaxX = Math.Max(_lastDocX, x) + maxDx;
                        int lineMaxY = Math.Max(_lastDocY, y) + maxDy;
                        PropagateLiveTileChanges(lineMinX, lineMinY, lineMaxX, lineMaxY);
                    }
                    else
                    {
                        _stroke.StampAtWithPainter(x, y, _fg, _bgColor, strokeSettings);
                        _didMove = true;
                        PropagateLiveTileChanges(x + minDx, y + minDy, x + maxDx, y + maxDy);
                    }

                    _lastDocX = x;
                    _lastDocY = y;
                }

                _hasLastDocPos = true;
            }
            else if (!_pixelPerfectActive)
            {
                _lastDocX = x;
                _lastDocY = y;
                _hasLastDocPos = true;
            }

            Document.CompositeTo(Document.Surface);
            Document.RaiseDocumentModified();
            ForceInvalidate();
        }

        private void HandlePaintingReleased()
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            if (_shiftLineActive && _isPainting)
            {
                _activePainter = GetPainterForCurrentTool();
                if (_activePainter != null)
                {
                    _stroke.BeginWithPainter(_activePainter, rl);
                }

                var strokeSettings = GetStrokeSettingsForCurrentTool();
                if (strokeSettings != null && _activePainter != null)
                {
                    _stroke.StampLineWithPainter(_shiftLineOriginX, _shiftLineOriginY, _lastDocX, _lastDocY, _fg, _bgColor, strokeSettings);

                    GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);

                    int minX = Math.Min(_shiftLineOriginX, _lastDocX) + minDx;
                    int minY = Math.Min(_shiftLineOriginY, _lastDocY) + minDy;
                    int maxX = Math.Max(_shiftLineOriginX, _lastDocX) + maxDx;
                    int maxY = Math.Max(_shiftLineOriginY, _lastDocY) + maxDy;

                    PropagateLiveTileChanges(minX, minY, maxX, maxY);
                }
            }
            else if (_pixelPerfectActive && _isPainting && _activePainter != null)
            {
                _pixelPerfectFilter.Flush(out bool shouldDraw, out int drawX, out int drawY);

                if (shouldDraw)
                {
                    var strokeSettings = GetStrokeSettingsForCurrentTool();
                    if (strokeSettings != null)
                    {
                        if (_hasLastDocPos && (_lastDocX != drawX || _lastDocY != drawY))
                        {
                            _stroke.StampLineWithPainter(_lastDocX, _lastDocY, drawX, drawY, _fg, _bgColor, strokeSettings);
                        }
                        else
                        {
                            _stroke.StampAtWithPainter(drawX, drawY, _fg, _bgColor, strokeSettings);
                        }

                        GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);
                        PropagateLiveTileChanges(drawX + minDx, drawY + minDy, drawX + maxDx, drawY + maxDy);
                    }
                }
            }

            _shiftLineActive = false;
            _pixelPerfectActive = false;
        }

        // ════════════════════════════════════════════════════════════════════
        // STROKE COMMIT
        // ════════════════════════════════════════════════════════════════════

        private void CommitStroke()
        {
            _isActivePainting = false;

            // Stop the rapid invalidation timer
            StopPaintInvalidationTimer();

            if (_activePainter != null && _stroke.HasActivePainterStroke)
            {
                var description = GetDescriptionForCurrentTool();
                var icon = GetIconForCurrentTool();
                var result = _stroke.CommitPainter(description, icon);

                var tileMappedItem = EndLiveTilePropagation(description);

                if (tileMappedItem != null)
                {
                    tileMappedItem.HistoryIcon = icon;
                    Document.History.Push(tileMappedItem);
                }
                else if (result is { CanPushToHistory: true } and Core.History.IHistoryItem historyItem)
                {
                    Document.History.Push(historyItem);
                }

                _activePainter = null;
            }
            else
            {
                EndLiveTilePropagation("Brush Stroke");
            }

            _shiftLineActive = false;

            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            AutoCaptureKeyframeIfNeeded();

            CanvasView.Invalidate();
            HistoryStateChanged?.Invoke();
            RaiseFrame();
        }

        private void AutoCaptureKeyframeIfNeeded()
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            var animState = Document.CanvasAnimationState;
            if (animState == null) return;

            if (animState.HasKeyframe(rl, animState.CurrentFrameIndex))
            {
                animState.CaptureKeyframe(rl, animState.CurrentFrameIndex);
            }
            else
            {
                var track = animState.GetTrackForLayer(rl);
                if (track != null && track.Keyframes.Count > 0)
                {
                    animState.CaptureKeyframe(rl, animState.CurrentFrameIndex);
                }
            }
        }

        private string GetDescriptionForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;

            var description = toolId switch
            {
                ToolIds.Brush => "Brush Stroke",
                ToolIds.Eraser => "Erase",
                ToolIds.Blur => "Blur",
                ToolIds.Smudge => "Smudge",
                ToolIds.Jumble => "Jumble",
                ToolIds.Replacer => "Replace Color",
                ToolIds.Gradient => "Gradient",
                ToolIds.ShapeRect => "Draw Rectangle",
                ToolIds.ShapeEllipse => "Draw Ellipse",
                _ => null
            };

            if (description != null) return description;

            var registration = ToolRegistry.Shared.GetById(toolId);
            return registration?.DisplayName ?? "Paint";
        }

        private Icon GetIconForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;

            var registration = ToolRegistry.Shared.GetById(toolId);
            if (registration?.Settings != null)
            {
                return registration.Settings.Icon;
            }

            return Icon.History;
        }

        private void RaiseFrame()
        {
            EnsureComposite();
            Document.CompositeTo(Document.Surface);
            FrameReady?.Invoke(Document.Surface.Pixels, Document.Surface.Width, Document.Surface.Height);
        }

        private void UpdateActiveLayerPreview()
        {
            if (Document.ActiveLayer is RasterLayer rl)
            {
                rl.UpdatePreview();

                if (rl.IsEditingMask && rl.Mask != null)
                {
                    rl.Mask.UpdatePreview();
                }
            }
        }
    }
}
