namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// Identifies one of the six faces of a voxel cube.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used to index per-face color data in <see cref="VoxelVolume.FaceColors"/>.
    /// The face index is computed as <c>voxelIndex * 6 + (int)face</c>.
    /// </para>
    /// <para>
    /// Face orientation follows the voxel coordinate convention documented in
    /// <see cref="OrthoVoxelBuilder"/>:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Front/Back</strong> — perpendicular to the Z axis.</item>
    /// <item><strong>Left/Right</strong> — perpendicular to the X axis.</item>
    /// <item><strong>Top/Bottom</strong> — perpendicular to the Y axis.</item>
    /// </list>
    /// </remarks>
    public enum Face
    {
        /// <summary>Face pointing toward +Z (visible from the front camera).</summary>
        Front = 0,

        /// <summary>Face pointing toward −Z (visible from the back camera).</summary>
        Back = 1,

        /// <summary>Face pointing toward −X (visible from the left camera).</summary>
        Left = 2,

        /// <summary>Face pointing toward +X (visible from the right camera).</summary>
        Right = 3,

        /// <summary>Face pointing toward +Y (visible from the top camera).</summary>
        Top = 4,

        /// <summary>Face pointing toward −Y (visible from the bottom camera).</summary>
        Bottom = 5
    }
}
