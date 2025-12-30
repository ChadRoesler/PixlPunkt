using System;
using System.Collections.Generic;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Painting.Painters;
using PixlPunkt.Core.Symmetry;
using PixlPunkt.UI.CanvasHost;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Shared context passed to <see cref="IStrokePainter"/> during stamping operations.
    /// Contains brush configuration, colors, surface reference, and helper functions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="StrokeContext"/> serves as a value bag that provides painters with
    /// everything they need to perform their pixel operations without coupling them
    /// directly to <see cref="StrokeEngine"/> or <see cref="CanvasViewHost"/>.
    /// </para>
    /// <para><strong>Immutability:</strong></para>
    /// <para>
    /// Context instances are created fresh for each stamp operation and should be
    /// treated as read-only by painters. The <see cref="ComputeAlphaAtOffset"/>
    /// delegate allows painters to compute per-pixel alpha without reimplementing
    /// the density falloff logic.
    /// </para>
    /// <para><strong>Custom Brush Support:</strong></para>
    /// <para>
    /// When <see cref="IsCustomBrush"/> is true, the <see cref="BrushOffsets"/> contain
    /// precomputed offsets from the custom brush mask. Custom brushes use uniform alpha
    /// within their mask (no density-based falloff).
    /// </para>
    /// <para><strong>Selection Masking:</strong></para>
    /// <para>
    /// When <see cref="SelectionMask"/> is provided, painters should check each pixel
    /// against the mask before applying paint. Use <see cref="IsInSelection"/> for
    /// convenient checking that handles null masks.
    /// </para>
    /// <para><strong>Symmetry Support:</strong></para>
    /// <para>
    /// When <see cref="SymmetryService"/> is provided and active, stroke operations
    /// automatically apply to mirrored positions. Use <see cref="GetSymmetryPoints"/>
    /// to obtain all target coordinates for a given input point.
    /// </para>
    /// </remarks>
    public sealed class StrokeContext
    {
        /// <summary>
        /// Gets the target pixel surface for painting operations.
        /// </summary>
        /// <remarks>
        /// Painters read from and write to this surface. For blur/smudge modes,
        /// painters should sample from <see cref="Snapshot"/> instead of Surface
        /// to avoid feedback loops.
        /// </remarks>
        public required PixelSurface Surface { get; init; }

        /// <summary>
        /// Gets the foreground color (BGRA packed 32-bit value).
        /// </summary>
        /// <remarks>
        /// The alpha channel of this color represents the brush opacity.
        /// Painters typically extract RGB and combine with computed per-pixel alpha.
        /// </remarks>
        public uint ForegroundColor { get; init; }

        /// <summary>
        /// Gets the background/target color for replacer mode (BGRA).
        /// </summary>
        /// <remarks>
        /// Used by <see cref="ReplacerPainter"/> to match pixels that should be replaced.
        /// Other painters may ignore this value.
        /// </remarks>
        public uint BackgroundColor { get; init; }

        /// <summary>
        /// Gets the current brush size (diameter).
        /// </summary>
        public int BrushSize { get; init; }

        /// <summary>
        /// Gets the current brush shape.
        /// </summary>
        public BrushShape BrushShape { get; init; }

        /// <summary>
        /// Gets the brush density (0-255).
        /// </summary>
        /// <remarks>
        /// Controls the hard-edge to soft-edge ratio. Higher values mean
        /// more of the brush radius is fully opaque.
        /// </remarks>
        public byte BrushDensity { get; init; }

        /// <summary>
        /// Gets the brush opacity (0-255).
        /// </summary>
        /// <remarks>
        /// Maximum alpha applied during stamping. Extracted from
        /// <see cref="ForegroundColor"/>'s alpha channel.
        /// </remarks>
        public byte BrushOpacity { get; init; }

        /// <summary>
        /// Gets whether this context is using a custom brush.
        /// </summary>
        /// <remarks>
        /// When true, <see cref="BrushOffsets"/> contains custom brush mask data
        /// and <see cref="ComputeAlphaAtOffset"/> returns uniform alpha (no density falloff).
        /// </remarks>
        public bool IsCustomBrush { get; init; }

        /// <summary>
        /// Gets the full name of the custom brush (author.brushname), or null if using built-in shape.
        /// </summary>
        public string? CustomBrushFullName { get; init; }

        /// <summary>
        /// Gets the pre-computed brush mask offsets relative to center.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each tuple (dx, dy) represents a pixel offset from the stamp center
        /// that falls within the brush footprint. Painters iterate over these
        /// offsets to apply their effect.
        /// </para>
        /// <para>
        /// For custom brushes, offsets are computed from the brush's mask tiers
        /// by <see cref="BrushMaskCache.GetOffsetsForCustomBrush"/>.
        /// </para>
        /// </remarks>
        public IReadOnlyList<(int dx, int dy)> BrushOffsets { get; init; } = [];

        /// <summary>
        /// Gets a delegate that computes per-pixel alpha at a brush offset.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For built-in shapes: accounts for opacity, density-based falloff, and distance.
        /// For custom brushes: returns uniform opacity (no density falloff).
        /// </para>
        /// </remarks>
        public required Func<int, int, byte> ComputeAlphaAtOffset { get; init; }

        /// <summary>
        /// Gets the optional snapshot of surface pixels at stroke start.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Non-null only when <see cref="IStrokePainter.NeedsSnapshot"/> is true.
        /// Painters that sample from original state (blur, smudge, jumble) should
        /// read from this array instead of <see cref="Surface"/> to avoid
        /// sampling their own modifications.
        /// </para>
        /// <para>
        /// Format: BGRA bytes, same dimensions as Surface.
        /// </para>
        /// </remarks>
        public byte[]? Snapshot { get; init; }

        /// <summary>
        /// Gets the optional selection mask delegate.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When non-null, painters should only modify pixels where this delegate returns true.
        /// The delegate takes (x, y) document coordinates and returns whether that pixel
        /// is inside the active selection.
        /// </para>
        /// <para>
        /// If null, no selection is active and all pixels can be painted.
        /// </para>
        /// </remarks>
        public Func<int, int, bool>? SelectionMask { get; init; }

        // ????????????????????????????????????????????????????????????????????
        // SYMMETRY SUPPORT
        // ????????????????????????????????????????????????????????????????????

        /// <summary>
        /// Gets the optional symmetry service for live stroke mirroring.
        /// </summary>
        /// <remarks>
        /// When non-null and active, use <see cref="GetSymmetryPoints"/> to obtain
        /// all positions where a stroke should be applied.
        /// </remarks>
        public SymmetryService? SymmetryService { get; init; }

        /// <summary>
        /// Gets all symmetry points for a given input coordinate.
        /// </summary>
        /// <param name="x">Input X coordinate.</param>
        /// <param name="y">Input Y coordinate.</param>
        /// <returns>
        /// Enumerable of all points to paint, including the original point.
        /// If symmetry is disabled, returns only the original point.
        /// </returns>
        public IEnumerable<(int x, int y)> GetSymmetryPoints(int x, int y)
        {
            if (SymmetryService == null || !SymmetryService.IsActive)
            {
                yield return (x, y);
                yield break;
            }

            foreach (var pt in SymmetryService.GetSymmetryPoints(x, y, Surface.Width, Surface.Height))
            {
                yield return pt;
            }
        }

        /// <summary>
        /// Gets whether symmetry is currently active.
        /// </summary>
        public bool IsSymmetryActive => SymmetryService?.IsActive ?? false;

        /// <summary>
        /// Checks if the given document coordinates are within surface bounds.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>True if (x, y) is within [0, Width) x [0, Height).</returns>
        public bool IsInBounds(int x, int y)
            => (uint)x < (uint)Surface.Width && (uint)y < (uint)Surface.Height;

        /// <summary>
        /// Computes the linear pixel index for the given coordinates.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>Byte index into the pixel array (multiply by 4 for BGRA offset).</returns>
        public int IndexOf(int x, int y) => Surface.IndexOf(x, y);

        /// <summary>
        /// Checks if the given coordinates are inside the active selection.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>True if no selection is active or the pixel is inside the selection.</returns>
        /// <remarks>
        /// This is a convenience method that handles null <see cref="SelectionMask"/>.
        /// When no selection is active, all pixels are considered "in selection".
        /// </remarks>
        public bool IsInSelection(int x, int y)
            => SelectionMask == null || SelectionMask(x, y);
    }
}
