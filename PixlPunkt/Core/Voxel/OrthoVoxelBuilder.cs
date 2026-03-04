using System;

namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// Contains the three orthographic projection images produced by
    /// <see cref="OrthoVoxelBuilder.ProjectToOrtho"/>.
    /// </summary>
    public sealed class OrthoProjections
    {
        /// <summary>Front view projection (X–Y plane, camera at +Z).</summary>
        public readonly ImageData Front;

        /// <summary>Side view projection (Z–Y plane, camera at −X).</summary>
        public readonly ImageData Side;

        /// <summary>Top view projection (X–Z plane, camera at +Y).</summary>
        public readonly ImageData Top;

        /// <summary>
        /// Creates a new set of orthographic projections.
        /// </summary>
        public OrthoProjections(ImageData front, ImageData side, ImageData top)
        {
            Front = front;
            Side = side;
            Top = top;
        }
    }

    /// <summary>
    /// Builds a <see cref="VoxelVolume"/> from 1–3 orthographic tile images
    /// using visual hull (silhouette intersection), and projects volumes
    /// back to 2D views.
    /// </summary>
    /// <remarks>
    /// <para><strong>Coordinate convention:</strong></para>
    /// <code>
    ///   Voxel space:
    ///     X = right    (+X = screen-right in front view)
    ///     Y = up       (+Y = screen-up, row 0 = bottom of image)
    ///     Z = depth    (+Z = toward camera / front)
    ///
    ///   Image space (all input/output images):
    ///     col  = horizontal, 0 = left
    ///     row  = vertical,   0 = top  (screen convention)
    ///
    ///   View cameras (orthographic):
    ///     Front : at +Z looking toward −Z  → sees X (col, flipped), Y (row, flipped)
    ///     Side  : at −X looking toward +X  → sees Z (col, flipped), Y (row, flipped)
    ///     Top   : at +Y looking toward −Y  → sees X (col), Z (row, flipped)
    /// </code>
    /// <para>
    /// The <c>Flip</c> helper converts between screen coordinates (top-left origin)
    /// and voxel coordinates (bottom-left/right-hand origin).
    /// </para>
    /// <para><strong>Algorithm (visual hull):</strong></para>
    /// <para>
    /// For each candidate voxel position (x, y, z), the builder checks whether
    /// all provided views have a non-transparent pixel at the corresponding
    /// screen coordinate. A voxel is placed only if every view "agrees" that
    /// the position is solid. This produces the maximal volume consistent with
    /// all silhouettes.
    /// </para>
    /// <para>
    /// Face colors are assigned from the source view that corresponds to each
    /// face direction: front/back faces use the front view color, left/right
    /// faces use the side view color, and top/bottom faces use the top view color.
    /// </para>
    /// </remarks>
    public static class OrthoVoxelBuilder
    {
        /// <summary>
        /// Flips a coordinate for screen ↔ voxel conversion.
        /// </summary>
        private static int Flip(int v, int size) => size - 1 - v;

        /// <summary>
        /// Determines the volume size from the provided images.
        /// Uses the minimum image width, or <paramref name="forced"/> if specified.
        /// </summary>
        private static int MinSize(ImageData? front, ImageData? side, ImageData? top, int? forced)
        {
            if (forced.HasValue) return forced.Value;

            int size = int.MaxValue;
            if (front != null) size = Math.Min(size, Math.Min(front.Width, front.Height));
            if (side != null)  size = Math.Min(size, Math.Min(side.Width, side.Height));
            if (top != null)   size = Math.Min(size, Math.Min(top.Width, top.Height));

            if (size == int.MaxValue)
                throw new InvalidOperationException("At least one input image is required.");

            return size;
        }

        /// <summary>
        /// Builds a <see cref="VoxelVolume"/> from 1–3 orthographic tile images.
        /// </summary>
        /// <param name="front">
        /// Front view image (X–Y plane). Null if not provided.
        /// </param>
        /// <param name="side">
        /// Side (left) view image (Z–Y plane). Null if not provided.
        /// </param>
        /// <param name="top">
        /// Top view image (X–Z plane). Null if not provided.
        /// </param>
        /// <param name="fallbackColor">
        /// Color used for face directions that have no source image.
        /// </param>
        /// <param name="sizeOverride">
        /// Forced volume size. If null, uses the minimum dimension of the input images.
        /// </param>
        /// <param name="singleViewMidPlane">
        /// When true and only one view is provided, voxels are placed on a single
        /// mid-plane slice instead of filling the full depth. This produces a more
        /// meaningful result from a single tile.
        /// </param>
        /// <param name="colorTolerance">
        /// Maximum per-channel RGB difference (0–255) for two view colors to be
        /// considered "matching." When ≥ 0 and multiple views are provided, a voxel
        /// is placed only if the colors from all present view pairs agree within this
        /// tolerance. Set to −1 to disable color linking (default behavior).
        /// </param>
        /// <returns>A new <see cref="VoxelVolume"/> representing the visual hull.</returns>
        /// <exception cref="InvalidOperationException">If no input images are provided.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If the computed size exceeds <see cref="VoxelVolume.MaxSize"/>.
        /// </exception>
        public static VoxelVolume BuildFromOrtho(
            ImageData? front,
            ImageData? side,
            ImageData? top,
            Rgba32 fallbackColor,
            int? sizeOverride = null,
            bool singleViewMidPlane = true,
            int colorTolerance = -1)
        {
            int size = MinSize(front, side, top, sizeOverride);
            var volume = new VoxelVolume(size);

            int viewCount = (front != null ? 1 : 0) + (side != null ? 1 : 0) + (top != null ? 1 : 0);

            // ── Single-view mode: place on mid-plane ──────────────────────
            if (viewCount == 1 && singleViewMidPlane)
            {
                BuildSingleViewMidPlane(volume, front, side, top, size);
                return volume;
            }

            // ── Multi-view intersection (visual hull) ─────────────────────
            BuildMultiViewHull(volume, front, side, top, fallbackColor, size, colorTolerance);
            return volume;
        }

        /// <summary>
        /// Single-view mode: places opaque pixels on the middle slice of the
        /// corresponding axis, with uniform face coloring.
        /// </summary>
        private static void BuildSingleViewMidPlane(
            VoxelVolume volume, ImageData? front, ImageData? side, ImageData? top, int size)
        {
            int mid = size / 2;

            if (front != null)
            {
                for (int x = 0; x < size; x++)
                {
                    int xFlip = Flip(x, size);
                    for (int y = 0; y < size; y++)
                    {
                        int yFlip = Flip(y, size);
                        var c = front.GetPixel(xFlip, yFlip);
                        if (c.A == 0) continue;
                        volume.SetVoxel(x, y, Flip(mid, size), c);
                    }
                }
            }
            else if (side != null)
            {
                for (int z = 0; z < size; z++)
                {
                    int zFlip = Flip(z, size);
                    for (int y = 0; y < size; y++)
                    {
                        int yFlip = Flip(y, size);
                        var c = side.GetPixel(zFlip, yFlip);
                        if (c.A == 0) continue;
                        volume.SetVoxel(mid, y, Flip(z, size), c);
                    }
                }
            }
            else if (top != null)
            {
                for (int x = 0; x < size; x++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        int zFlip = Flip(z, size);
                        var c = top.GetPixel(x, zFlip);
                        if (c.A == 0) continue;
                        volume.SetVoxel(x, mid, Flip(z, size), c);
                    }
                }
            }
        }

        /// <summary>
        /// Multi-view intersection: a voxel is placed only where all provided
        /// views have a non-transparent pixel at the corresponding position.
        /// When color linking is enabled, the colors from each pair of views must
        /// also agree within <paramref name="colorTolerance"/> per channel.
        /// </summary>
        private static void BuildMultiViewHull(
            VoxelVolume volume,
            ImageData? front, ImageData? side, ImageData? top,
            Rgba32 fallbackColor, int size, int colorTolerance)
        {
            bool linkColors = colorTolerance >= 0;

            for (int x = 0; x < size; x++)
            {
                int xFlip = Flip(x, size);
                for (int y = 0; y < size; y++)
                {
                    int yFlip = Flip(y, size);
                    for (int z = 0; z < size; z++)
                    {
                        int zFlip = Flip(z, size);

                        Rgba32 f = fallbackColor;
                        Rgba32 s = fallbackColor;
                        Rgba32 t = fallbackColor;

                        // Front view → col = flipped x, row = flipped y
                        if (front != null)
                        {
                            f = front.GetPixel(xFlip, yFlip);
                            if (f.A == 0) continue; // silhouette rejects this voxel
                        }

                        // Side view → col = flipped z, row = flipped y
                        if (side != null)
                        {
                            s = side.GetPixel(zFlip, yFlip);
                            if (s.A == 0) continue;
                        }

                        // Top view → col = x, row = flipped z
                        if (top != null)
                        {
                            t = top.GetPixel(x, zFlip);
                            if (t.A == 0) continue;
                        }

                        // Color linking: reject voxels where view colors disagree
                        if (linkColors)
                        {
                            if (front != null && side != null && !ColorsMatch(f, s, colorTolerance))
                                continue;
                            if (front != null && top != null && !ColorsMatch(f, t, colorTolerance))
                                continue;
                            if (side != null && top != null && !ColorsMatch(s, t, colorTolerance))
                                continue;
                        }

                        // Front/back faces ← front view color
                        // Left/right faces ← side view color
                        // Top/bottom faces ← top view color
                        volume.SetVoxel(x, y, Flip(z, size), f, f, s, s, t, t);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if two colors are within the specified per-channel tolerance.
        /// </summary>
        private static bool ColorsMatch(Rgba32 a, Rgba32 b, int tolerance)
        {
            return Math.Abs(a.R - b.R) <= tolerance &&
                   Math.Abs(a.G - b.G) <= tolerance &&
                   Math.Abs(a.B - b.B) <= tolerance;
        }

        /// <summary>
        /// Projects a <see cref="VoxelVolume"/> back to three orthographic images.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the inverse of <see cref="BuildFromOrtho"/>. For each view direction,
        /// the nearest occupied voxel is selected and its corresponding face color is
        /// written to the output image:
        /// </para>
        /// <list type="bullet">
        /// <item><strong>Front:</strong> highest Z wins, uses <see cref="Face.Back"/> color
        /// (the face pointing toward −Z, which faces the +Z camera).</item>
        /// <item><strong>Side:</strong> lowest X wins, uses <see cref="Face.Left"/> color
        /// (the face pointing toward −X, which faces the −X camera).</item>
        /// <item><strong>Top:</strong> lowest Y wins (highest on screen), uses
        /// <see cref="Face.Bottom"/> color (the face pointing toward −Y, which
        /// faces the +Y camera).</item>
        /// </list>
        /// </remarks>
        /// <param name="volume">The voxel volume to project.</param>
        /// <returns>Three orthographic projection images.</returns>
        public static OrthoProjections ProjectToOrtho(VoxelVolume volume)
        {
            int size = volume.Size;
            var front = new ImageData(size, size);
            var side = new ImageData(size, size);
            var top = new ImageData(size, size);

            int pixelCount = size * size;
            int[] depthFront = new int[pixelCount];
            int[] depthSide = new int[pixelCount];
            int[] depthTop = new int[pixelCount];

            // Initialize depth buffers
            for (int i = 0; i < pixelCount; i++)
            {
                depthFront[i] = -1;            // highest z wins
                depthSide[i] = int.MaxValue;    // lowest x wins
                depthTop[i] = -1;               // tracks whether set (lowest y wins)
            }

            for (int z = 0; z < size; z++)
            {
                int zFlip = Flip(z, size);
                for (int y = 0; y < size; y++)
                {
                    int yFlip = Flip(y, size);
                    for (int x = 0; x < size; x++)
                    {
                        if (!volume.IsOccupied(x, y, z)) continue;

                        // ── Front: col = flipped x, row = flipped y ──
                        // Nearest to front camera = highest z
                        // Uses Face.Back color (the face pointing toward −Z, facing the +Z camera)
                        int pFront = Flip(x, size) + yFlip * size;
                        if (z > depthFront[pFront])
                        {
                            depthFront[pFront] = z;
                            var c = volume.GetFaceColor(x, y, z, Face.Back);
                            front.SetPixel(Flip(x, size), yFlip, new Rgba32(c.R, c.G, c.B, 255));
                        }

                        // ── Side: col = z, row = flipped y ──
                        // Nearest to side camera (at −X) = lowest x
                        // Uses Face.Left color (the face pointing toward −X, facing the camera)
                        int pSide = z + yFlip * size;
                        if (x < depthSide[pSide])
                        {
                            depthSide[pSide] = x;
                            var c = volume.GetFaceColor(x, y, z, Face.Left);
                            side.SetPixel(z, yFlip, new Rgba32(c.R, c.G, c.B, 255));
                        }

                        // ── Top: col = x, row = flipped z ──
                        // Nearest to top camera (at +Y) = lowest y
                        // Uses Face.Bottom color (the face pointing toward −Y, facing the +Y camera)
                        int pTop = x + zFlip * size;
                        if (depthTop[pTop] < 0 || y < depthTop[pTop])
                        {
                            depthTop[pTop] = y;
                            var c = volume.GetFaceColor(x, y, z, Face.Bottom);
                            top.SetPixel(x, zFlip, new Rgba32(c.R, c.G, c.B, 255));
                        }
                    }
                }
            }

            return new OrthoProjections(front, side, top);
        }
    }
}
