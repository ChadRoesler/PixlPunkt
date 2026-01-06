using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.History;
using PixlPunkt.Uno.Core.Imaging;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.Core.Painting.Helpers;
using PixlPunkt.Uno.Core.Selection;
using PixlPunkt.Uno.Core.Symmetry;

namespace PixlPunkt.Uno.Core.Painting
{
    /// <summary>
    /// Manages interactive brush stroke painting operations.
    /// </summary>
    public sealed class StrokeEngine
    {
        // ════════════════════════════════════════════════════════════════════
        // IStrokePainter-based painting support (plugin-friendly API)
        // ════════════════════════════════════════════════════════════════════

        private IStrokePainter? _activePainter;
        private RasterLayer? _activeLayer;
        private bool _inPainterStroke;

        public void BeginWithPainter(IStrokePainter painter, RasterLayer layer)
        {
            ArgumentNullException.ThrowIfNull(painter);
            ArgumentNullException.ThrowIfNull(layer);

            _activePainter = painter;
            _activeLayer = layer;
            _inPainterStroke = true;

            var targetSurface = layer.GetPaintingSurface();
            byte[]? snapshot = painter.NeedsSnapshot
                ? (byte[])targetSurface.Pixels.Clone()
                : null;

            LoggingService.Debug("Stroke began painter={PainterType} layer={LayerName} needsSnapshot={NeedsSnapshot} editingMask={EditingMask}",
                painter.GetType().Name, layer.Name, painter.NeedsSnapshot, layer.IsEditingMask);

            painter.Begin(layer, snapshot);
        }

        // ════════════════════════════════════════════════════════════════════
        // SELECTION MASK SUPPORT
        // ════════════════════════════════════════════════════════════════════

        private Func<int, int, bool>? _selectionMask;

        public void SetSelectionMask(Func<int, int, bool>? mask)
        {
            _selectionMask = mask;
        }

        public Func<int, int, bool>? SelectionMask => _selectionMask;

        // ════════════════════════════════════════════════════════════════════
        // SYMMETRY SUPPORT
        // ════════════════════════════════════════════════════════════════════

        private SymmetryService? _symmetryService;

        public void SetSymmetryService(SymmetryService? service)
        {
            _symmetryService = service;
        }

        public SymmetryService? SymmetryService => _symmetryService;

        // ════════════════════════════════════════════════════════════════════
        // STAMP OPERATIONS WITH SYMMETRY
        // ════════════════════════════════════════════════════════════════════

        public void StampAtWithPainter(int cx, int cy, uint foreground, uint background, IStrokeSettings strokeSettings)
        {
            if (!_inPainterStroke || _activePainter == null)
                throw new InvalidOperationException("No painter stroke is active. Call BeginWithPainter first.");

            var context = BuildStrokeContext(foreground, background, strokeSettings);

            // Log symmetry state for debugging
            bool symmetryActive = _symmetryService?.IsActive == true;
            LoggingService.Info("StampAt ({CX},{CY}): symmetryService={HasService}, isActive={IsActive}",
                cx, cy, _symmetryService != null, symmetryActive);

            if (symmetryActive)
            {
                var points = new List<(int x, int y)>(_symmetryService!.GetSymmetryPoints(cx, cy, _surface.Width, _surface.Height));
                LoggingService.Info("Symmetry painting {PointCount} points from ({CX},{CY})", points.Count, cx, cy);
                
                foreach (var (px, py) in points)
                {
                    _activePainter.StampAt(px, py, context);
                }
            }
            else
            {
                _activePainter.StampAt(cx, cy, context);
            }
        }

        public void StampLineWithPainter(int x0, int y0, int x1, int y1, uint foreground, uint background, IStrokeSettings strokeSettings)
        {
            if (!_inPainterStroke || _activePainter == null)
                throw new InvalidOperationException("No painter stroke is active. Call BeginWithPainter first.");

            var context = BuildStrokeContext(foreground, background, strokeSettings);

            if (_symmetryService?.IsActive == true)
            {
                var startPoints = new List<(int x, int y)>(_symmetryService.GetSymmetryPoints(x0, y0, _surface.Width, _surface.Height));
                var endPoints = new List<(int x, int y)>(_symmetryService.GetSymmetryPoints(x1, y1, _surface.Width, _surface.Height));

                LoggingService.Debug("Symmetry line from ({X0},{Y0}) to ({X1},{Y1}): {PointCount} segments", x0, y0, x1, y1, startPoints.Count);

                int count = Math.Min(startPoints.Count, endPoints.Count);
                for (int i = 0; i < count; i++)
                {
                    _activePainter.StampLine(startPoints[i].x, startPoints[i].y, endPoints[i].x, endPoints[i].y, context);
                }
            }
            else
            {
                _activePainter.StampLine(x0, y0, x1, y1, context);
            }
        }

        public IRenderResult? CommitPainter(string description = "Brush Stroke", Icon icon = Icon.History)
        {
            if (!_inPainterStroke || _activePainter == null)
                return null;

            var result = _activePainter.End(description, icon);

            LoggingService.Debug("Stroke committed painter={PainterType} description={Description} hasChanges={HasChanges}",
                _activePainter.GetType().Name, description, result?.CanPushToHistory ?? false);

            _activePainter = null;
            _activeLayer = null;
            _inPainterStroke = false;

            return result;
        }

        public bool HasActivePainterStroke => _inPainterStroke && _activePainter != null;

        // ════════════════════════════════════════════════════════════════════
        // SURFACE AND BRUSH STATE
        // ════════════════════════════════════════════════════════════════════

        private readonly PixelSurface _surface;
        private uint _fg = 0xFFFFFFFF;
        private int _size = 1;
        private BrushShape _shape = BrushShape.Circle;
        private byte _density = 255;

        public StrokeEngine(PixelSurface surface)
        {
            _surface = surface;
        }

        public PixelSurface Surface => _surface;

        public void SetForeground(uint bgra) => _fg = bgra;
        public void SetBrushSize(int s) => _size = Math.Max(1, s);
        public void SetBrushShape(BrushShape shape) => _shape = shape;
        public void SetOpacity(byte a) => _fg = _fg & 0x00FFFFFFu | (uint)a << 24;
        public void SetDensity(byte d) => _density = d;

        public IReadOnlyList<(int dx, int dy)> GetCurrentBrushOffsets()
            => BrushMaskCache.Shared.GetOffsets(_shape, _size);

        public byte ComputeBrushAlphaAtOffset(int dx, int dy)
            => ComputePerPixelAlpha(dx, dy);

        private byte ComputePerPixelAlpha(int dx, int dy)
        {
            int sz = Math.Max(1, _size);
            double Aop = ((byte)(_fg >> 24)) / 255.0;
            if (Aop <= 0.0) return 0;

            double r = sz / 2.0;
            double d = DistanceForCurrentShape(dx, dy, sz);
            if (d > r) return 0;

            double D = _density / 255.0;
            double Rhard = r * D;

            if (d <= Rhard)
                return (byte)Math.Round(255.0 * Aop);

            double span = Math.Max(1e-6, (r - Rhard));
            double t = (d - Rhard) / span;
            double mask = 1.0 - (t * t) * (3 - 2 * t);
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double DistanceForCurrentShape(int dx, int dy, int sz)
        {
            double frac = StrokeUtil.ParityFrac(sz);
            double px = dx + 0.5 - frac;
            double py = dy + 0.5 - frac;

            return (_shape == BrushShape.Circle)
                ? Math.Sqrt(px * px + py * py)
                : Math.Max(Math.Abs(px), Math.Abs(py));
        }

        // ════════════════════════════════════════════════════════════════════
        // STROKE CONTEXT BUILDER
        // ════════════════════════════════════════════════════════════════════

        private StrokeContext BuildStrokeContext(uint foreground, uint background, IStrokeSettings strokeSettings)
        {
            int size = strokeSettings.Size;
            var shape = strokeSettings.Shape;

            byte density = strokeSettings is IDensitySettings ds ? ds.Density : (byte)255;
            byte opacity = strokeSettings is IOpacitySettings os ? os.Opacity : (byte)255;

            bool isCustomBrush = false;
            string? customBrushFullName = null;
            IReadOnlyList<(int dx, int dy)> offsets;

            if (strokeSettings is PluginSdk.Settings.ICustomBrushSettings customBrushSettings && customBrushSettings.IsCustomBrushSelected)
            {
                isCustomBrush = true;
                customBrushFullName = customBrushSettings.CustomBrushFullName;
                var brush = Brush.BrushDefinitionService.Instance.GetBrush(customBrushFullName!);
                if (brush != null)
                {
                    offsets = BrushMaskCache.Shared.GetOffsetsForCustomBrush(brush, size);
                }
                else
                {
                    isCustomBrush = false;
                    offsets = BrushMaskCache.Shared.GetOffsets(shape, size);
                }
            }
            else
            {
                offsets = BrushMaskCache.Shared.GetOffsets(shape, size);
            }

            Func<int, int, byte> computeAlpha;
            if (isCustomBrush)
            {
                computeAlpha = (dx, dy) => ComputeCustomBrushAlphaForContext(dx, dy, size, density, opacity);
            }
            else
            {
                computeAlpha = (dx, dy) => ComputePerPixelAlphaForContext(dx, dy, size, shape, density, opacity);
            }

            var targetSurface = _activeLayer?.GetPaintingSurface() ?? _surface;

            return new StrokeContext
            {
                Surface = targetSurface,
                ForegroundColor = foreground,
                BackgroundColor = background,
                BrushSize = size,
                BrushShape = shape,
                BrushDensity = density,
                BrushOpacity = opacity,
                IsCustomBrush = isCustomBrush,
                CustomBrushFullName = customBrushFullName,
                BrushOffsets = offsets,
                Snapshot = null,
                ComputeAlphaAtOffset = computeAlpha,
                SelectionMask = _selectionMask,
                SymmetryService = _symmetryService
            };
        }

        private static byte ComputeCustomBrushAlphaForContext(int dx, int dy, int size, byte density, byte opacity)
        {
            int sz = Math.Max(1, size);
            double Aop = opacity / 255.0;
            if (Aop <= 0.0) return 0;

            double r = sz / 2.0;

            double frac = StrokeUtil.ParityFrac(sz);
            double px = dx + 0.5 - frac;
            double py = dy + 0.5 - frac;
            double d = Math.Sqrt(px * px + py * py);

            double D = density / 255.0;
            double Rhard = r * D;

            if (d <= Rhard)
                return (byte)Math.Round(255.0 * Aop);

            double span = Math.Max(1e-6, r - Rhard);
            double t = Math.Min(1.0, (d - Rhard) / span);
            double mask = 1.0 - (t * t) * (3 - 2 * t);
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }

        private static byte ComputePerPixelAlphaForContext(int dx, int dy, int size, BrushShape shape, byte density, byte opacity)
        {
            int sz = Math.Max(1, size);
            double Aop = opacity / 255.0;
            if (Aop <= 0.0) return 0;

            double r = sz / 2.0;
            double d = DistanceForShape(dx, dy, sz, shape);
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

        private static double DistanceForShape(int dx, int dy, int sz, BrushShape shape)
        {
            double frac = StrokeUtil.ParityFrac(sz);
            double px = dx + 0.5 - frac;
            double py = dy + 0.5 - frac;

            return (shape == BrushShape.Circle)
                ? Math.Sqrt(px * px + py * py)
                : Math.Max(Math.Abs(px), Math.Abs(py));
        }
    }
}