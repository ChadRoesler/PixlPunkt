using System;
using System.Collections.Generic;
using PixlPunkt.Core.Brush;
using PixlPunkt.Core.Imaging;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Default implementation of <see cref="IToolContextProvider"/> for standard painting operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This implementation provides the standard context creation logic used by built-in tools.
    /// Plugin authors can use this directly or implement custom providers for specialized needs.
    /// </para>
    /// </remarks>
    public sealed class DefaultToolContextProvider : IToolContextProvider
    {
        /// <summary>
        /// Shared singleton instance for common usage.
        /// </summary>
        public static DefaultToolContextProvider Shared { get; } = new();

        /// <inheritdoc/>
        public StrokeContext CreateStrokeContext(
            PixelSurface surface,
            uint foreground,
            uint background,
            IStrokeSettings strokeSettings,
            byte[]? snapshot = null)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (strokeSettings == null)
                throw new ArgumentNullException(nameof(strokeSettings));

            int size = strokeSettings.Size;
            var shape = strokeSettings.Shape;

            // Extract optional settings with sensible defaults
            byte density = strokeSettings is IDensitySettings ds ? ds.Density : (byte)255;
            byte opacity = strokeSettings is IOpacitySettings os ? os.Opacity : (byte)255;

            // Check if we're using a custom brush - support all tools that implement ICustomBrushSettings
            bool isCustomBrush = false;
            string? customBrushFullName = null;
            IReadOnlyList<(int dx, int dy)> offsets;

            if (strokeSettings is ICustomBrushSettings customBrushSettings && customBrushSettings.IsCustomBrushSelected)
            {
                // Custom brush - get offsets from the custom brush cache
                isCustomBrush = true;
                customBrushFullName = customBrushSettings.CustomBrushFullName;
                var brush = BrushDefinitionService.Instance.GetBrush(customBrushFullName!);
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
                // This allows proper soft edge control for custom brush shapes
                computeAlpha = (dx, dy) => ComputeCustomBrushAlpha(dx, dy, size, density, opacity);
            }
            else
            {
                // Built-in shapes use density-based falloff
                computeAlpha = (dx, dy) => ComputePerPixelAlpha(dx, dy, size, shape, density, opacity);
            }

            return new StrokeContext
            {
                Surface = surface,
                ForegroundColor = foreground,
                BackgroundColor = background,
                BrushSize = size,
                BrushShape = shape,
                BrushDensity = density,
                BrushOpacity = opacity,
                IsCustomBrush = isCustomBrush,
                CustomBrushFullName = customBrushFullName,
                BrushOffsets = offsets,
                Snapshot = snapshot,
                ComputeAlphaAtOffset = computeAlpha
            };
        }

        /// <inheritdoc/>
        public FillContext CreateFillContext(
            PixelSurface surface,
            uint color,
            int tolerance,
            bool contiguous,
            string description = "Fill",
            Func<int, int, bool>? selectionMask = null)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            return new FillContext
            {
                Surface = surface,
                Color = color,
                Tolerance = tolerance,
                Contiguous = contiguous,
                Description = description,
                SelectionMask = selectionMask
            };
        }

        /// <inheritdoc/>
        public ShapeRenderContext CreateShapeContext(
            PixelSurface surface,
            uint color,
            int strokeWidth,
            BrushShape brushShape,
            byte opacity,
            byte density,
            bool isFilled,
            string description = "Draw Shape")
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            return new ShapeRenderContext
            {
                Surface = surface,
                Color = color,
                StrokeWidth = strokeWidth,
                BrushShape = brushShape,
                Opacity = opacity,
                Density = density,
                IsFilled = isFilled,
                Description = description
            };
        }

        /// <summary>
        /// Computes per-pixel alpha for custom brushes using radial density-based falloff.
        /// </summary>
        /// <remarks>
        /// Custom brushes use circular distance from center for density falloff,
        /// similar to how Circle brushes work. This provides consistent soft/hard edge
        /// control across all brush types.
        /// </remarks>
        private static byte ComputeCustomBrushAlpha(int dx, int dy, int size, byte density, byte opacity)
        {
            int sz = Math.Max(1, size);
            double Aop = opacity / 255.0;
            if (Aop <= 0.0) return 0;

            double r = sz / 2.0;

            // Use circular distance - offsets are already relative to brush center
            double d = Math.Sqrt((double)dx * dx + (double)dy * dy);

            // Don't clip to radius for custom brushes since they define their own shape
            // But still apply density falloff based on distance

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

        /// <summary>
        /// Computes per-pixel alpha using density-based falloff.
        /// </summary>
        private static byte ComputePerPixelAlpha(int dx, int dy, int size, BrushShape shape, byte density, byte opacity)
        {
            int sz = Math.Max(1, size);
            double Aop = opacity / 255.0;
            if (Aop <= 0.0) return 0;

            double r = sz / 2.0;
            double d = DistanceForShape(dx, dy, shape);
            if (d > r) return 0;

            double D = density / 255.0;
            double Rhard = r * D;

            if (d <= Rhard)
                return (byte)Math.Round(255.0 * Aop);

            double span = Math.Max(1e-6, r - Rhard);
            double t = (d - Rhard) / span;
            double mask = 1.0 - (t * t) * (3 - 2 * t); // Smoothstep falloff
            return (byte)Math.Round(255.0 * Math.Clamp(Aop * mask, 0.0, 1.0));
        }

        /// <summary>
        /// Computes distance metric for a brush shape (circle: Euclidean, square: Chebyshev).
        /// </summary>
        /// <remarks>
        /// Distance is calculated from the offset (dx, dy) to the brush center.
        /// Since offsets are computed relative to the brush center, the distance
        /// is simply the magnitude of the offset vector.
        /// </remarks>
        private static double DistanceForShape(int dx, int dy, BrushShape shape)
        {
            return shape == BrushShape.Circle
                ? Math.Sqrt((double)dx * dx + (double)dy * dy)
                : Math.Max(Math.Abs(dx), Math.Abs(dy));
        }
    }
}
