using System;
using PixlPunkt.Core.Imaging;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Provides context objects for tool painting operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IToolContextProvider"/> abstracts the creation of context objects used by
    /// the three painter types. This decouples painters from specific UI implementations
    /// and enables plugin authors to:
    /// </para>
    /// <list type="bullet">
    /// <item>Create contexts without direct access to CanvasViewHost</item>
    /// <item>Test painters in isolation with mock contexts</item>
    /// <item>Implement custom context providers for batch processing</item>
    /// </list>
    /// <para><strong>Context Types:</strong></para>
    /// <list type="bullet">
    /// <item><see cref="StrokeContext"/>: For <see cref="IStrokePainter"/> operations</item>
    /// <item><see cref="FillContext"/>: For <see cref="IFillPainter"/> operations</item>
    /// <item><see cref="ShapeRenderContext"/>: For <see cref="IShapeRenderer"/> operations</item>
    /// </list>
    /// </remarks>
    public interface IToolContextProvider
    {
        /// <summary>
        /// Creates a <see cref="StrokeContext"/> for stroke painting operations.
        /// </summary>
        /// <param name="surface">Target surface for painting.</param>
        /// <param name="foreground">Foreground color (BGRA).</param>
        /// <param name="background">Background color (BGRA) for replacer tool.</param>
        /// <param name="strokeSettings">
        /// Stroke configuration implementing <see cref="IStrokeSettings"/>.
        /// May optionally implement <see cref="IOpacitySettings"/> and/or <see cref="IDensitySettings"/>.
        /// </param>
        /// <param name="snapshot">Optional snapshot for blur/smudge operations.</param>
        /// <returns>A configured <see cref="StrokeContext"/>.</returns>
        StrokeContext CreateStrokeContext(
            PixelSurface surface,
            uint foreground,
            uint background,
            IStrokeSettings strokeSettings,
            byte[]? snapshot = null);

        /// <summary>
        /// Creates a <see cref="FillContext"/> for fill operations.
        /// </summary>
        /// <param name="surface">Target surface for filling.</param>
        /// <param name="color">Fill color (BGRA).</param>
        /// <param name="tolerance">Color matching tolerance (0-255).</param>
        /// <param name="contiguous">True for flood fill, false for global replace.</param>
        /// <param name="description">Description for history.</param>
        /// <param name="selectionMask">Optional selection mask delegate.</param>
        /// <returns>A configured <see cref="FillContext"/>.</returns>
        FillContext CreateFillContext(
            PixelSurface surface,
            uint color,
            int tolerance,
            bool contiguous,
            string description = "Fill",
            Func<int, int, bool>? selectionMask = null);

        /// <summary>
        /// Creates a <see cref="ShapeRenderContext"/> for shape rendering.
        /// </summary>
        /// <param name="surface">Target surface for rendering.</param>
        /// <param name="color">Shape color (BGRA).</param>
        /// <param name="strokeWidth">Stroke width for outlined shapes.</param>
        /// <param name="brushShape">Brush shape for stroke rendering.</param>
        /// <param name="opacity">Opacity (0-255).</param>
        /// <param name="density">Density for soft edges (0-255).</param>
        /// <param name="isFilled">True for filled shapes, false for outlined.</param>
        /// <param name="description">Description for history.</param>
        /// <returns>A configured <see cref="ShapeRenderContext"/>.</returns>
        ShapeRenderContext CreateShapeContext(
            PixelSurface surface,
            uint color,
            int strokeWidth,
            BrushShape brushShape,
            byte opacity,
            byte density,
            bool isFilled,
            string description = "Draw Shape");
    }
}
