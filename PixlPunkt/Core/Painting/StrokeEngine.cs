using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentIcons.Common;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Painting.Helpers;
using PixlPunkt.Core.Selection;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Manages interactive brush stroke painting operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// StrokeEngine is the central system for brush-based pixel manipulation in PixlPunkt. It handles:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Brush painting</strong>: Normal painting with opacity, blending, and brush shapes (circle/square).</item>
    /// <item><strong>Special modes</strong>: Eraser, replacer, blur, jumble (pixel shuffling), gradient cycling, smudge.</item>
    /// <item><strong>History integration</strong>: All operations return <see cref="PixelChangeItem"/> for direct push to history.</item>
    /// </list>
    /// <para>
    /// The engine operates in stroke sessions: call <see cref="BeginWithPainter"/> to start, perform paint operations,
    /// then <see cref="CommitPainter"/> to finalize changes into history.
    /// </para>
    /// <para>
    /// Shape drawing (Rectangle, Ellipse) is handled by <see cref="IShapeRenderer"/> implementations.
    /// Fill operations (Flood fill, Global fill) are handled by <see cref="IFillPainter"/> implementations.
    /// </para>
    /// </remarks>
    public sealed class StrokeEngine
    {
        // ════════════════════════════════════════════════════════════════════
        // IStrokePainter-based painting support (plugin-friendly API)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>The currently active painter for IStrokePainter-based strokes.</summary>
        private IStrokePainter? _activePainter;

        /// <summary>The layer being painted on during a painter stroke.</summary>
        private RasterLayer? _activeLayer;

        /// <summary>Indicates if a painter-based stroke is active.</summary>
        private bool _inPainterStroke;

        /// <summary>
        /// Begins a stroke using the provided <see cref="IStrokePainter"/>.
        /// </summary>
        /// <param name="painter">The painter to use for this stroke.</param>
        /// <param name="layer">The target layer for painting.</param>
        /// <remarks>
        /// <para>
        /// This method initializes a painter-based stroke session. The painter's
        /// <see cref="IStrokePainter.Begin"/> method is called with the layer
        /// and optional snapshot (if <see cref="IStrokePainter.NeedsSnapshot"/> is true).
        /// </para>
        /// <para>
        /// Use <see cref="StampAtWithPainter"/> and <see cref="StampLineWithPainter"/>
        /// to apply strokes, then <see cref="CommitPainter"/> to finalize.
        /// </para>
        /// </remarks>
        public void BeginWithPainter(IStrokePainter painter, RasterLayer layer)
        {
            ArgumentNullException.ThrowIfNull(painter);
            ArgumentNullException.ThrowIfNull(layer);

            _activePainter = painter;
            _activeLayer = layer;
            _inPainterStroke = true;

            // Get the appropriate surface (layer or mask depending on edit mode)
            var targetSurface = layer.GetPaintingSurface();

            // Take snapshot if painter needs it (from the target surface)
            byte[]? snapshot = painter.NeedsSnapshot
                ? (byte[])targetSurface.Pixels.Clone()
                : null;

            LoggingService.Debug("Stroke began painter={PainterType} layer={LayerName} needsSnapshot={NeedsSnapshot} editingMask={EditingMask}",
                painter.GetType().Name, layer.Name, painter.NeedsSnapshot, layer.IsEditingMask);

            painter.Begin(layer, snapshot);
        }

        /// <summary>
        /// Stamps at a single point using the active painter.
        /// </summary>
        /// <param name="cx">Center X coordinate.</param>
        /// <param name="cy">Center Y coordinate.</param>
        /// <param name="foreground">Foreground color (BGRA).</param>
        /// <param name="background">Background/target color (BGRA) for replacer.</param>
        /// <param name="strokeSettings">Stroke configuration implementing <see cref="IStrokeSettings"/>.</param>
        /// <exception cref="InvalidOperationException">Thrown if no painter stroke is active.</exception>
        public void StampAtWithPainter(int cx, int cy, uint foreground, uint background, IStrokeSettings strokeSettings)
        {
            if (!_inPainterStroke || _activePainter == null)
                throw new InvalidOperationException("No painter stroke is active. Call BeginWithPainter first.");

            var context = BuildStrokeContext(foreground, background, strokeSettings);
            _activePainter.StampAt(cx, cy, context);
        }

        /// <summary>
        /// Stamps along a line using the active painter.
        /// </summary>
        /// <param name="x0">Start X coordinate.</param>
        /// <param name="y0">Start Y coordinate.</param>
        /// <param name="x1">End X coordinate.</param>
        /// <param name="y1">End Y coordinate.</param>
        /// <param name="foreground">Foreground color (BGRA).</param>
        /// <param name="background">Background/target color (BGRA) for replacer.</param>
        /// <param name="strokeSettings">Stroke configuration implementing <see cref="IStrokeSettings"/>.</param>
        /// <exception cref="InvalidOperationException">Thrown if no painter stroke is active.</exception>
        public void StampLineWithPainter(int x0, int y0, int x1, int y1, uint foreground, uint background, IStrokeSettings strokeSettings)
        {
            if (!_inPainterStroke || _activePainter == null)
                throw new InvalidOperationException("No painter stroke is active. Call BeginWithPainter first.");

            var context = BuildStrokeContext(foreground, background, strokeSettings);
            _activePainter.StampLine(x0, y0, x1, y1, context);
        }

        /// <summary>
        /// Commits the active painter stroke, returning the accumulated changes.
        /// </summary>
        /// <param name="description">Description of the operation for undo/redo UI.</param>
        /// <param name="icon">Icon representing the tool for history display.</param>
        /// <returns>
        /// An <see cref="IRenderResult"/> containing all pixel modifications, or null if no changes.
        /// Typically returns <see cref="PixelChangeItem"/> which can be cast to <see cref="IHistoryItem"/>
        /// for history integration.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method calls <see cref="IStrokePainter.End"/> to collect all pixel changes.
        /// The returned result can be checked with <see cref="IRenderResult.CanPushToHistory"/>
        /// and cast to <see cref="IHistoryItem"/> if appropriate.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// Gets whether a painter-based stroke is currently active.
        /// </summary>
        public bool HasActivePainterStroke => _inPainterStroke && _activePainter != null;

        // ════════════════════════════════════════════════════════════════════
        // SURFACE AND BRUSH STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Target pixel surface for all painting operations.</summary>
        private readonly PixelSurface _surface;

        /// <summary>Foreground color (BGRA) used for painting.</summary>
        private uint _fg = 0xFFFFFFFF;
        /// <summary>Brush size (diameter or side length depending on shape).</summary>
        private int _size = 1;
        /// <summary>Brush shape (circle, square, etc.).</summary>
        private BrushShape _shape = BrushShape.Circle;
        /// <summary>Brush density (0-255): controls soft falloff thickness.</summary>
        private byte _density = 255;

        /// <summary>
        /// Constructs a stroke engine bound to a pixel surface.
        /// </summary>
        /// <param name="surface">Target pixel surface for painting.</param>
        public StrokeEngine(PixelSurface surface)
        {
            _surface = surface;
        }

        /// <summary>
        /// Gets the surface this engine operates on.
        /// </summary>
        public PixelSurface Surface => _surface;

        /// <summary>
        /// Sets the current foreground color (BGRA).
        /// </summary>
        /// <param name="bgra">BGRA packed 32-bit color value.</param>
        public void SetForeground(uint bgra) => _fg = bgra;

        /// <summary>
        /// Sets the brush size (diameter / side depending on shape). Minimum is 1.
        /// </summary>
        /// <param name="s">Desired size (clamped to ≥1).</param>
        public void SetBrushSize(int s) => _size = Math.Max(1, s);

        /// <summary>
        /// Sets active brush shape for stamping (circle / square / custom).
        /// </summary>
        /// <param name="shape">Brush shape enum value.</param>
        public void SetBrushShape(BrushShape shape) => _shape = shape;

        /// <summary>
        /// Overrides foreground alpha (opacity) channel.
        /// </summary>
        /// <param name="a">Alpha byte (0–255).</param>
        public void SetOpacity(byte a) => _fg = _fg & 0x00FFFFFFu | (uint)a << 24;

        /// <summary>
        /// Sets brush density controlling soft falloff thickness.
        /// </summary>
        /// <param name="d">Density (0–255). Lower values expand fade region.</param>
        public void SetDensity(byte d) => _density = d;

        /// <summary>
        /// Gets current brush footprint offsets relative to center.
        /// </summary>
        /// <returns>List of (dx, dy) offsets used for stamping.</returns>
        public IReadOnlyList<(int dx, int dy)> GetCurrentBrushOffsets()
            => BrushMaskCache.Shared.GetOffsets(_shape, _size);

        /// <summary>
        /// Computes effective per-pixel alpha for the current brush at an offset.
        /// </summary>
        /// <param name="dx">X offset from center.</param>
        /// <param name="dy">Y offset from center.</param>
        /// <returns>Alpha (0–255) contributed by this offset.</returns>
        public byte ComputeBrushAlphaAtOffset(int dx, int dy)
            => ComputePerPixelAlpha(dx, dy);

        /// <returns>Alpha byte.</returns>
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

        /// <summary>
        /// Computes distance metric for current brush shape (circle: Euclidean, square: Chebyshev).
        /// </summary>
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
        // SELECTION MASK SUPPORT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Optional selection mask delegate. When set, painting is constrained to pixels
        /// where this delegate returns true.
        /// </summary>
        private Func<int, int, bool>? _selectionMask;

        /// <summary>
        /// Sets the selection mask delegate for constraining paint operations.
        /// </summary>
        /// <param name="mask">
        /// A delegate that returns true if the pixel at (x, y) is inside the selection,
        /// or null to disable selection masking.
        /// </param>
        /// <remarks>
        /// <para>
        /// When a selection mask is active, all painting operations (brush, eraser, fill, etc.)
        /// will only affect pixels where the mask returns true.
        /// </para>
        /// <para>
        /// Typically bound to <see cref="SelectionRegion.Contains(int, int)"/> from the
        /// active selection subsystem.
        /// </para>
        /// </remarks>
        public void SetSelectionMask(Func<int, int, bool>? mask)
        {
            _selectionMask = mask;
        }

        /// <summary>
        /// Gets the current selection mask delegate.
        /// </summary>
        public Func<int, int, bool>? SelectionMask => _selectionMask;

        /// <summary>
        /// Builds a <see cref="StrokeContext"/> from the current stroke settings.
        /// Supports both built-in shapes and custom brushes.
        /// </summary>
        private StrokeContext BuildStrokeContext(uint foreground, uint background, IStrokeSettings strokeSettings)
        {
            int size = strokeSettings.Size;
            var shape = strokeSettings.Shape;

            // Extract optional settings with sensible defaults
            byte density = strokeSettings is IDensitySettings ds ? ds.Density : (byte)255;
            byte opacity = strokeSettings is IOpacitySettings os ? os.Opacity : (byte)255;

            // Check if we're using a custom brush - support all tools that implement ICustomBrushSettings
            bool isCustomBrush = false;
            string? customBrushFullName = null;
            IReadOnlyList<(int dx, int dy)> offsets;

            if (strokeSettings is PluginSdk.Settings.ICustomBrushSettings customBrushSettings && customBrushSettings.IsCustomBrushSelected)
            {
                // Custom brush - get offsets from the custom brush cache
                isCustomBrush = true;
                customBrushFullName = customBrushSettings.CustomBrushFullName;
                var brush = Brush.BrushDefinitionService.Instance.GetBrush(customBrushFullName!);
                if (brush != null)
                {
                    offsets = BrushMaskCache.Shared.GetOffsetsForCustomBrush(brush, size);
                }
                else
                {
                    // Fallback to built-in shape if brush not found
                    isCustomBrush = false;
                    offsets = BrushMaskCache.Shared.GetOffsets(shape, size);
                }
            }
            else
            {
                // Built-in shape
                offsets = BrushMaskCache.Shared.GetOffsets(shape, size);
            }

            // Create the alpha computation delegate
            Func<int, int, byte> computeAlpha;
            if (isCustomBrush)
            {
                // Custom brushes use radial density-based falloff like built-in shapes
                computeAlpha = (dx, dy) => ComputeCustomBrushAlphaForContext(dx, dy, size, density, opacity);
            }
            else
            {
                // Built-in shapes use density-based falloff
                computeAlpha = (dx, dy) => ComputePerPixelAlphaForContext(dx, dy, size, shape, density, opacity);
            }

            // Get the appropriate surface (layer or mask depending on edit mode)
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
                Snapshot = null, // Painters manage their own snapshot reference
                ComputeAlphaAtOffset = computeAlpha,
                SelectionMask = _selectionMask
            };
        }

        /// <summary>
        /// Computes per-pixel alpha for custom brushes using radial density-based falloff.
        /// </summary>
        private static byte ComputeCustomBrushAlphaForContext(int dx, int dy, int size, byte density, byte opacity)
        {
            int sz = Math.Max(1, size);
            double Aop = opacity / 255.0;
            if (Aop <= 0.0) return 0;

            double r = sz / 2.0;

            // Use circular distance for custom brushes (like Circle shape)
            double frac = StrokeUtil.ParityFrac(sz);
            double px = dx + 0.5 - frac;
            double py = dy + 0.5 - frac;
            double d = Math.Sqrt(px * px + py * py);

            double D = density / 255.0;
            double Rhard = r * D;

            // Full opacity within the hard radius
            if (d <= Rhard)
                return (byte)Math.Round(255.0 * Aop);

            // Falloff region beyond hard radius
            double span = Math.Max(1e-6, r - Rhard);
            double t = Math.Min(1.0, (d - Rhard) / span);
            double mask = 1.0 - (t * t) * (3 - 2 * t);
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }

        /// <summary>
        /// Computes per-pixel alpha for StrokeContext, using the same density/opacity logic as brush stamping.
        /// </summary>
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

        /// <summary>
        /// Computes distance metric for a brush shape (circle: Euclidean, square: Chebyshev).
        /// </summary>
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