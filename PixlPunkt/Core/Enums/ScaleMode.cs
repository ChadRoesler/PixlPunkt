namespace PixlPunkt.Core.Enums
{
    /// <summary>
    /// Defines the interpolation algorithms used for scaling pixel art.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scale modes control how pixels are sampled and interpolated when resizing images.
    /// Different algorithms are optimized for different types of content and scaling factors.
    /// </para>
    /// <para>
    /// These modes are implemented in <see cref="Imaging.PixelOps"/> scaling methods.
    /// </para>
    /// </remarks>
    public enum ScaleMode
    {
        /// <summary>
        /// Uses the closest pixel without interpolation, preserving hard edges and exact colors.
        /// </summary>
        /// <remarks>
        /// Best for integer scaling factors (2x, 3x, 4x) where pixel art should remain crisp.
        /// Produces blocky results for non-integer scaling.
        /// </remarks>
        NearestNeighbor,

        /// <summary>
        /// Eric's Pixel Expansion algorithm for 2x upscaling, preserving pixel art edges and details.
        /// </summary>
        /// <remarks>
        /// Analyzes neighboring pixels to intelligently scale pixel art while maintaining
        /// sharp edges and reducing blocky appearance. Only works for 2x upscaling.
        /// </remarks>
        EPX,

        /// <summary>
        /// Weighted average of surrounding pixels for smooth gradients and photographs.
        /// </summary>
        /// <remarks>
        /// Produces smooth results but may blur pixel art or sharp edges. Best for
        /// photographic content or smooth gradients at arbitrary scaling factors.
        /// </remarks>
        Bilinear,

        /// <summary>
        /// Advanced 2x pixel art upscaling algorithm that preserves edges better than EPX.
        /// </summary>
        /// <remarks>
        /// Analyzes more neighbors than EPX to produce cleaner edges and better diagonal
        /// line interpolation. Only works for 2x upscaling.
        /// </remarks>
        Scale2x
    }
}
