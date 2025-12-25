namespace PixlPunkt.PluginSdk.Imaging
{
    /// <summary>
    /// Defines brush footprint shapes for painting tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BrushShape determines the geometric pattern of pixels affected by a single brush stamp
    /// during painting operations. The shape, combined with size and density settings, defines
    /// the brush's visual appearance and coverage area.
    /// </para>
    /// <para><strong>Shape Characteristics:</strong></para>
    /// <list type="bullet">
    /// <item><strong><see cref="Square"/></strong>: Rectangular footprint with sharp 90° corners.
    /// Uses Chebyshev distance (max of |dx|, |dy|) for pixel inclusion.</item>
    /// <item><strong><see cref="Circle"/></strong>: Circular footprint with smooth edges.
    /// Uses Euclidean distance for pixel inclusion with soft falloff.</item>
    /// <item><strong><see cref="Custom"/></strong>: Custom brush stamp from loaded brush definitions.</item>
    /// </list>
    /// </remarks>
    public enum BrushShape
    {
        /// <summary>
        /// Square/rectangular brush with hard 90° corners and Chebyshev distance metric.
        /// </summary>
        /// <remarks>
        /// Best for pixel-art, tile-aligned painting, and hard-edge work requiring precise control.
        /// </remarks>
        Square,

        /// <summary>
        /// Circular brush with smooth edges and Euclidean distance metric.
        /// </summary>
        /// <remarks>
        /// Produces natural, organic strokes with soft falloff. Most commonly used for general painting.
        /// </remarks>
        Circle,

        /// <summary>
        /// Custom brush shape from loaded brush definitions.
        /// </summary>
        Custom
    }
}
