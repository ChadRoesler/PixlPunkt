namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// Preset camera angles for quick snap-to-view in the voxel viewer.
    /// </summary>
    public enum PresetView
    {
        /// <summary>Camera at +Z looking toward −Z.</summary>
        Front,

        /// <summary>Camera at −Z looking toward +Z.</summary>
        Back,

        /// <summary>Camera at −X looking toward +X.</summary>
        Left,

        /// <summary>Camera at +X looking toward −X.</summary>
        Right,

        /// <summary>Camera at +Y looking toward −Y.</summary>
        Top,

        /// <summary>Camera at −Y looking toward +Y.</summary>
        Bottom
    }
}
