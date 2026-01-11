namespace PixlPunkt.Core.Enums
{
    /// <summary>
    /// Defines the interpolation algorithms used for rotating pixel art.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rotation modes control how pixels are sampled and interpolated when rotating images.
    /// Different algorithms produce varying results in terms of sharpness, smoothness, and
    /// preservation of pixel art aesthetics.
    /// </para>
    /// <para>
    /// These modes are implemented in <see cref="Imaging.PixelOps"/> rotation methods.
    /// </para>
    /// </remarks>
    public enum RotationMode
    {
        /// <summary>
        /// Uses the closest pixel without interpolation, preserving hard edges but producing jagged results.
        /// </summary>
        /// <remarks>
        /// Best for precise 90-degree rotations or when maintaining exact pixel values is critical.
        /// </remarks>
        NearestNeighbor = 0,

        /// <summary>
        /// Uses a specialized algorithm that upsamples, rotates with bilinear interpolation, and downsamples
        /// to preserve pixel art characteristics while reducing jagged edges.
        /// </summary>
        /// <remarks>
        /// Inspired by RotSprite algorithm, this mode provides smoother rotation while maintaining
        /// the crisp aesthetic of pixel art. Best for arbitrary angle rotations of low-resolution artwork.
        /// </remarks>
        RotSprite = 1
    }
}
