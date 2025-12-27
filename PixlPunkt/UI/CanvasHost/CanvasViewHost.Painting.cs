using System;
using System.Collections.Generic;
using FluentIcons.Common;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Brush;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Painting;
using PixlPunkt.Core.Plugins;
using PixlPunkt.Core.Tools;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Settings;
using Windows.Foundation;

namespace PixlPunkt.UI.CanvasHost
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
        private PixlPunkt.Core.Imaging.PixelSurface? GetPaintTargetSurface()
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
        ///
        /// This is the "belt + suspenders" version:
        /// - Uses the actual cached BrushOffsets list (built-in shapes or custom brushes)
        /// - Computes exact min/max dx/dy (so bounds are correct for even sizes AND any asymmetric custom masks)
        /// - Caches the computed extents per mask instance (no per-move O(n) re-scan unless the mask changes)
        ///
        /// Why this matters:
        /// Tile-mapped undo relies on accurate _liveStrokeMin/Max bounds (see EndLiveTilePropagation).
        /// If bounds miss even a 1px strip (common with even sizes and symmetric-radius math), those pixels won't be captured
        /// and undo will "leave ghosts" on the top/left.
        /// </summary>
        private IReadOnlyList<(int dx, int dy)>? _cachedBoundsMask;
        private int _cachedMinDx, _cachedMaxDx, _cachedMinDy, _cachedMaxDy;

        private void GetBrushBoundsFromMask(IStrokeSettings? strokeSettings,
                                           out int minDx, out int minDy,
                                           out int maxDx, out int maxDy)
        {
            // Fallback to the old symmetric math if we somehow have no settings.
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

            // Resolve the EXACT offsets list that painters iterate.
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

            // Cache extents for this mask instance.
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

                // Defensive: empty mask shouldn't happen, but keep it safe.
                if (first)
                {
                    minX = maxX = 0;
                    minY = maxY = 0;
                }

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

            // Check for built-in brush tool
            var brushReg = ToolRegistry.Shared.GetById<BrushToolRegistration>(toolId);
            if (brushReg != null)
                return brushReg.CreatePainter();

            // Check for plugin brush tool
            var pluginBrushReg = ToolRegistry.Shared.GetById<PluginBrushToolRegistration>(toolId);
            if (pluginBrushReg != null)
                return pluginBrushReg.CreatePainter();

            // Check for shape tool
            var shapeReg = ToolRegistry.Shared.GetById<ShapeToolRegistration>(toolId);
            if (shapeReg != null)
                return shapeReg.CreatePainter();

            return null;
        }

        private IStrokeSettings? GetStrokeSettingsForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;

            // Check for built-in brush tool
            var brushReg = ToolRegistry.Shared.GetById<BrushToolRegistration>(toolId);
            if (brushReg != null)
                return brushReg.StrokeSettings;

            // Check for plugin brush tool
            var pluginBrushReg = ToolRegistry.Shared.GetById<PluginBrushToolRegistration>(toolId);
            if (pluginBrushReg != null)
                return pluginBrushReg.StrokeSettings;

            // Check for shape tool (built-in or plugin) - uses IShapeToolRegistration interface
            var shapeReg = ToolRegistry.Shared.GetById<IShapeToolRegistration>(toolId);
            if (shapeReg?.Settings is IStrokeSettings shapeStrokeSettings)
                return shapeStrokeSettings;

            // Fallback to generic settings lookup
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

            // Wire up selection mask if there's an active, non-floating selection
            UpdateStrokeEngineSelectionMask();

            // Begin live tile propagation tracking
            BeginLiveTilePropagation();

            if (TryGetDocWithBrushOverlap(p.Position, out var x, out var y))
            {
                _isPainting = true;
                _pendingStrokeFromOutside = false;

                // Check if this is a shift-line start
                if (shiftHeld)
                {
                    _shiftLineActive = true;
                    _shiftLineOriginX = x;
                    _shiftLineOriginY = y;
                    // Don't begin painter yet - we'll draw the line on release
                }
                else
                {
                    _shiftLineActive = false;
                    _activePainter = GetPainterForCurrentTool();
                    if (_activePainter != null)
                    {
                        _stroke.BeginWithPainter(_activePainter, rl);
                        LoggingService.Debug("Begin stroke with tool {ToolId} on document {Doc}", _toolState?.ActiveToolId ?? "Missing ActiveToolId", Document.Name ?? "Missing DocumentName");
                    }

                    var strokeSettings = GetStrokeSettingsForCurrentTool();
                    if (strokeSettings != null && _activePainter != null)
                    {
                        _stroke.StampAtWithPainter(x, y, _fg, _bgColor, strokeSettings);

                        // Live propagate tile changes
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

            // Get raw document coordinates (even if outside bounds)
            var docPt = _zoom.ScreenToDoc(pos);
            int rawX = (int)Math.Floor(docPt.X);
            int rawY = (int)Math.Floor(docPt.Y);

            bool inside = TryGetDocWithBrushOverlap(pos, out var x, out var y);

            if (_pendingStrokeFromOutside)
            {
                if (!inside)
                    return;

                _pendingStrokeFromOutside = false;
                _isPainting = true;
                _shiftLineActive = false;

                _activePainter = GetPainterForCurrentTool();
                if (_activePainter != null)
                {
                    _stroke.BeginWithPainter(_activePainter, rl);
                    LoggingService.Debug("Began deferred stroke for tool {ToolId} at {X},{Y}", _toolState?.ActiveToolId ?? "Missing ActiveToolId", x, y);
                }

                _hasLastDocPos = false;
                _didMove = false;
                _lastDocX = x;
                _lastDocY = y;
            }

            if (!_isPainting)
                return;

            // Shift-line mode: track endpoint even when outside bounds for preview
            if (_shiftLineActive)
            {
                bool ctrlHeld = IsKeyDown(Windows.System.VirtualKey.Control);
                // Use raw coordinates so line can extend outside canvas
                var (targetX, targetY) = ComputeShiftLineTarget(_shiftLineOriginX, _shiftLineOriginY, rawX, rawY, ctrlHeld);
                _lastDocX = targetX;
                _lastDocY = targetY;
                _didMove = true;
                // Update brush overlay for preview
                OnBrushMoved(new System.Numerics.Vector2(_hoverX, _hoverY), (float)((_brushSize - 1) * 0.5));
                CanvasView.Invalidate();
                return;
            }

            // Normal freehand painting - requires being inside bounds
            if (!inside)
            {
                _hasLastDocPos = false;
                return;
            }

            var strokeSettings = GetStrokeSettingsForCurrentTool();
            // SAFETY: Verify both the local painter reference AND the stroke engine agree there's an active stroke
            if (_activePainter != null && strokeSettings != null && _stroke.HasActivePainterStroke)
            {
                GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);

                if (_hasLastDocPos)
                {
                    _stroke.StampLineWithPainter(_lastDocX, _lastDocY, x, y, _fg, _bgColor, strokeSettings);

                    // Live propagate - calculate bounds of the line
                    int minX = Math.Min(_lastDocX, x) + minDx;
                    int minY = Math.Min(_lastDocY, y) + minDy;
                    int maxX = Math.Max(_lastDocX, x) + maxDx;
                    int maxY = Math.Max(_lastDocY, y) + maxDy;
                    PropagateLiveTileChanges(minX, minY, maxX, maxY);
                }
                else
                {
                    _stroke.StampAtWithPainter(x, y, _fg, _bgColor, strokeSettings);

                    // Live propagate tile changes
                    PropagateLiveTileChanges(x + minDx, y + minDy, x + maxDx, y + maxDy);
                }

                _lastDocX = x;
                _lastDocY = y;
            }

            _didMove = true;
            _hasLastDocPos = true;

            CanvasView.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        // FILL TOOL HANDLER
        // ════════════════════════════════════════════════════════════════════

        private void HandleFillPressed(PointerPoint p)
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            if (TryGetDocInside(p.Position, out var fx, out var fy))
            {
                // Get fill registration for the fill painter
                var fillReg = ToolRegistry.Shared.GetById<FillToolRegistration>(ToolIds.Fill);
                if (fillReg == null) return;

                // Get the icon for the fill tool
                var icon = fillReg.Settings?.Icon ?? Icon.PaintBucket;

                // Determine if we should use selection masking
                bool hasActiveSelection = _selState?.Active == true && !(_selState?.Floating ?? true);
                Func<int, int, bool>? selectionMask = null;

                if (hasActiveSelection && !_selRegion.IsEmpty)
                {
                    selectionMask = (x, y) => _selRegion.Contains(x, y);
                }

                // Create fill context from current tool settings
                var context = new FillContext
                {
                    Surface = rl.Surface,
                    Color = _fg,
                    Tolerance = _fillTolerance,
                    Contiguous = _fillContiguous,
                    Description = _fillContiguous ? "Fill" : "Global Fill",
                    SelectionMask = selectionMask
                };

                // Use the fill painter (default or custom)
                var result = fillReg.EffectiveFillPainter.FillAt(rl, fx, fy, context);

                // Set the icon on the result
                if (result != null)
                {
                    result.HistoryIcon = icon;
                }

                // Push to unified history if result supports it
                if (result is { CanPushToHistory: true } and IHistoryItem historyItem)
                {
                    Document.History.Push(historyItem);
                    LoggingService.Info("Fill performed by tool {ToolId} at {X},{Y} on document {Doc}", _toolState?.ActiveToolId ?? "MissingToolId", fx, fy, Document.Name ?? "MissingDocumentName");
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

            // Wire up selection mask if there's an active, non-floating selection
            UpdateStrokeEngineSelectionMask();

            // Begin live tile propagation tracking
            BeginLiveTilePropagation();

            // Check if pixel-perfect mode should be used for this stroke
            _pixelPerfectActive = ShouldUsePixelPerfect() && !shiftHeld;
            if (_pixelPerfectActive)
            {
                _pixelPerfectFilter.Reset();
            }

            if (TryGetDocWithBrushOverlap(p.Position, out var x, out var y))
            {
                _isPainting = true;
                _pendingStrokeFromOutside = false;

                // Check if this is a shift-line start
                if (shiftHeld)
                {
                    _shiftLineActive = true;
                    _shiftLineOriginX = x;
                    _shiftLineOriginY = y;
                    // Don't begin painter yet - we'll draw the line on release
                }
                else
                {
                    _shiftLineActive = false;
                    _activePainter = GetPainterForCurrentTool();
                    if (_activePainter != null)
                    {
                        _stroke.BeginWithPainter(_activePainter, rl);
                        LoggingService.Debug("Paint begin: tool={ToolId}, doc={Doc}, pixelPerfect={PixelPerfect}",
                            _toolState?.ActiveToolId ?? "Missing ActiveToolId",
                            Document.Name ?? "Missing DocumentName",
                            _pixelPerfectActive);
                    }

                    var strokeSettings = GetStrokeSettingsForCurrentTool();
                    if (strokeSettings != null && _activePainter != null)
                    {
                        if (_pixelPerfectActive)
                        {
                            // Use pixel-perfect filter - first point is always drawn
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
                            // Live propagate tile changes
                            GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);
                            PropagateLiveTileChanges(x + minDx, x + maxDx, y + minDy, y + maxDy);
                        }

                        // Recomposite for live preview and notify external listeners
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
                _pendingStrokeFromOutside = true;
                _hasLastDocPos = false;
                _didMove = false;
                _shiftLineActive = false;
            }

            CanvasView.CapturePointer(e.Pointer);
        }

        /// <summary>
        /// Updates the StrokeEngine's selection mask based on the current selection state.
        /// When a non-floating selection is active, painting is constrained to selected pixels.
        /// </summary>
        private void UpdateStrokeEngineSelectionMask()
        {
            // Only constrain painting when there's an active, non-floating selection
            bool hasActiveSelection = _selState?.Active == true && !(_selState?.Floating ?? true);

            if (hasActiveSelection && !_selRegion.IsEmpty)
            {
                // Wire up the selection region's Contains method as the mask
                _stroke.SetSelectionMask((x, y) => _selRegion.Contains(x, y));
            }
            else
            {
                // No active selection - clear the mask so painting is unconstrained
                _stroke.SetSelectionMask(null);
            }
        }

        private void HandlePaintingMoved(Point pos)
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            // Get raw document coordinates (even if outside bounds)
            var docPt = _zoom.ScreenToDoc(pos);
            int rawX = (int)Math.Floor(docPt.X);
            int rawY = (int)Math.Floor(docPt.Y);

            bool inside = TryGetDocWithBrushOverlap(pos, out var x, out var y);

            if (_pendingStrokeFromOutside)
            {
                if (!inside) return;

                _pendingStrokeFromOutside = false;
                _isPainting = true;
                _shiftLineActive = false;

                // Reset pixel-perfect filter for deferred strokes
                if (_pixelPerfectActive)
                {
                    _pixelPerfectFilter.Reset();
                }

                _activePainter = GetPainterForCurrentTool();
                if (_activePainter != null)
                {
                    _stroke.BeginWithPainter(_activePainter, rl);
                    LoggingService.Debug("Deferred paint started: tool={ToolId}", _toolState?.ActiveToolId ?? "Missing ActiveToolId");
                }

                _hasLastDocPos = false;
                _didMove = false;
                _lastDocX = x;
                _lastDocY = y;
                _shiftLineOriginX = x;
                _shiftLineOriginY = y;
            }

            if (!_isPainting) return;

            // Shift-line mode: track endpoint even when outside bounds for preview
            if (_shiftLineActive)
            {
                bool ctrlHeld = IsKeyDown(Windows.System.VirtualKey.Control);
                // Use raw coordinates so line can extend outside canvas
                var (targetX, targetY) = ComputeShiftLineTarget(_shiftLineOriginX, _shiftLineOriginY, rawX, rawY, ctrlHeld);
                _lastDocX = targetX;
                _lastDocY = targetY;
                _didMove = true;
                // Update brush overlay for preview
                OnBrushMoved(new System.Numerics.Vector2(_hoverX, _hoverY), (float)((_brushSize - 1) * 0.5));
                CanvasView.Invalidate();
                return;
            }

            // Normal freehand painting - requires being inside bounds
            if (!inside)
            {
                _hasLastDocPos = false;
                return;
            }

            var strokeSettings = GetStrokeSettingsForCurrentTool();
            // SAFETY: Verify both the local painter reference AND the stroke engine agree there's an active stroke
            if (_activePainter != null && strokeSettings != null && _stroke.HasActivePainterStroke)
            {
                GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);

                if (_pixelPerfectActive)
                {
                    // Pixel-perfect mode: filter input through lookahead algorithm
                    _pixelPerfectFilter.FilterWithLookahead(x, y, out bool shouldDraw, out int drawX, out int drawY);

                    if (shouldDraw)
                    {
                        if (_hasLastDocPos && (_lastDocX != drawX || _lastDocY != drawY))
                        {
                            _stroke.StampLineWithPainter(_lastDocX, _lastDocY, drawX, drawY, _fg, _bgColor, strokeSettings);
                            _didMove = true;

                            // Live propagate - calculate bounds of the line
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
                    // If shouldDraw is false, the filter is buffering - don't update lastDoc
                }
                else
                {
                    // Normal mode - draw immediately
                    if (_hasLastDocPos)
                    {
                        _stroke.StampLineWithPainter(_lastDocX, _lastDocY, x, y, _fg, _bgColor, strokeSettings);
                        _didMove = true;

                        // Live propagate - calculate bounds of the line
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

                        // Live propagate tile changes
                        PropagateLiveTileChanges(x + minDx, y + minDy, x + maxDx, y + maxDy);
                    }

                    _lastDocX = x;
                    _lastDocY = y;
                }

                _hasLastDocPos = true;
            }
            else if (!_pixelPerfectActive)
            {
                // Only update last position when not using pixel-perfect
                // (pixel-perfect manages its own lastDoc state)
                _lastDocX = x;
                _lastDocY = y;
                _hasLastDocPos = true;
            }

            // Recomposite for live preview and notify external listeners (e.g., TileFrameEditorCanvas)
            Document.CompositeTo(Document.Surface);
            Document.RaiseDocumentModified();
            CanvasView.Invalidate();
        }

        /// <summary>
        /// Called when painting ends (mouse released). Handles shift-line commit and pixel-perfect flush.
        /// </summary>
        private void HandlePaintingReleased()
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            // If we were in shift-line mode, draw the line now
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
                    // Draw the line from origin to last tracked position
                    _stroke.StampLineWithPainter(_shiftLineOriginX, _shiftLineOriginY, _lastDocX, _lastDocY, _fg, _bgColor, strokeSettings);

                    // Live propagate - calculate bounds of the line
                    GetBrushBoundsFromMask(strokeSettings, out int minDx, out int minDy, out int maxDx, out int maxDy);

                    int minX = Math.Min(_shiftLineOriginX, _lastDocX) + minDx;
                    int minY = Math.Min(_shiftLineOriginY, _lastDocY) + minDy;
                    int maxX = Math.Max(_shiftLineOriginX, _lastDocX) + maxDx;
                    int maxY = Math.Max(_shiftLineOriginY, _lastDocY) + maxDy;

                    PropagateLiveTileChanges(minX, minY, maxX, maxY);
                }
            }
            // Flush pixel-perfect filter to draw any pending point
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

        /// <summary>Commits the current stroke to history, invalidates, and raises frame events.</summary>
        private void CommitStroke()
        {
            if (_activePainter != null && _stroke.HasActivePainterStroke)
            {
                var description = GetDescriptionForCurrentTool();
                var icon = GetIconForCurrentTool();
                var result = _stroke.CommitPainter(description, icon);

                // Check if tiles were affected during this stroke (live propagation was active)
                var tileMappedItem = EndLiveTilePropagation(description);

                if (tileMappedItem != null)
                {
                    // Use the tile-mapped history item (includes tile definition changes)
                    tileMappedItem.HistoryIcon = icon;
                    Document.History.Push(tileMappedItem);
                    LoggingService.Info("Committed stroke with tile mapping: {Description} on {Doc}", description, Document.Name ?? "Missing DocumentName");
                }
                else if (result is { CanPushToHistory: true } and Core.History.IHistoryItem historyItem)
                {
                    // No tiles affected, use regular history item
                    Document.History.Push(historyItem);
                    LoggingService.Info("Committed stroke: {Description} on {Doc}", description, Document.Name ?? "Missing DocumentName");
                }

                _activePainter = null;
            }
            else
            {
                // No painter active, but still clean up tile propagation state
                EndLiveTilePropagation("Brush Stroke");
            }

            _shiftLineActive = false;

            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();

            // Auto-capture keyframe if the active layer has a keyframe at the current animation frame
            AutoCaptureKeyframeIfNeeded();

            CanvasView.Invalidate();
            HistoryStateChanged?.Invoke();
            RaiseFrame();
        }

        /// <summary>
        /// Auto-captures the current layer state to its keyframe if one exists at the current frame,
        /// or creates a new keyframe if editing on a frame without one (auto-keyframe mode).
        /// This ensures that edits to a layer are automatically saved to animation keyframes.
        /// </summary>
        private void AutoCaptureKeyframeIfNeeded()
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            var animState = Document.CanvasAnimationState;
            if (animState == null) return;

            // Check if this layer has a keyframe at the current frame
            if (animState.HasKeyframe(rl, animState.CurrentFrameIndex))
            {
                // Re-capture the keyframe with the updated pixel data
                animState.CaptureKeyframe(rl, animState.CurrentFrameIndex);
                LoggingService.Debug("Auto-captured keyframe for layer {Layer} at frame {Frame}",
                    rl.Name, animState.CurrentFrameIndex);
            }
            else
            {
                // No keyframe exists at this frame - auto-generate one
                // This enables "edit anywhere, keyframe auto-creates" workflow
                var track = animState.GetTrackForLayer(rl);
                if (track != null && track.Keyframes.Count > 0)
                {
                    // Only auto-create if the layer already has at least one keyframe
                    // (indicates it's part of the animation system)
                    animState.CaptureKeyframe(rl, animState.CurrentFrameIndex);
                    LoggingService.Debug("Auto-created keyframe for layer {Layer} at frame {Frame}",
                        rl.Name, animState.CurrentFrameIndex);
                }
            }
        }

        /// <summary>
        /// Gets a human-readable description for the current tool's operation.
        /// </summary>
        private string GetDescriptionForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;

            // Check if it's a known built-in tool
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

            if (description != null)
                return description;

            // For plugin tools, use the display name
            var registration = ToolRegistry.Shared.GetById(toolId);
            return registration?.DisplayName ?? "Paint";
        }

        /// <summary>
        /// Gets the icon for the current tool for history display.
        /// </summary>
        private Icon GetIconForCurrentTool()
        {
            var toolId = _toolState?.ActiveToolId ?? ToolIds.Brush;

            // Get the tool's settings which contains the icon
            var registration = ToolRegistry.Shared.GetById(toolId);
            if (registration?.Settings != null)
            {
                return registration.Settings.Icon;
            }

            // Fallback to History icon if no settings available
            return Icon.History;
        }

        /// <summary>Raises FrameReady with the current composited pixels.</summary>
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

                // Also update mask preview if editing mask
                if (rl.IsEditingMask && rl.Mask != null)
                {
                    rl.Mask.UpdatePreview();
                }
            }
        }
    }
}
