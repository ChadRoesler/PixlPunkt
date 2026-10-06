namespace PixlPunkt.Core.Document
{
    /// <summary>What kind of three-dimensional content a volumetric document holds.</summary>
    /// <remarks>
    /// Recorded from the start so the format is not boxed into voxels by omission. Only
    /// <see cref="Voxel"/> is produced today; the rest name where the format is allowed to go
    /// without committing to how any of it is stored.
    /// </remarks>
    public enum VolumetricKind
    {
        /// <summary>A grid of cubes textured from the document's tiles.</summary>
        Voxel = 0,

        /// <summary>Tile-textured faces placed on a grid, rather than whole cubes.</summary>
        Geometry = 1,
    }

    /// <summary>
    /// Marks a document as a volumetric project and holds what belongs to the project as a whole
    /// rather than to any one workspace.
    /// </summary>
    /// <remarks>
    /// This is deliberately separate from <see cref="VoxelWorkspaceDocumentState"/>. That state
    /// says how the voxel workspace is currently set up, and any ordinary document picks it up the
    /// moment someone opens the voxel preview. This one says the document *is* a volumetric
    /// project, which is a different claim and the one the file extension follows.
    ///
    /// It is named volumetric rather than voxel on purpose. Voxels are the first thing it can
    /// describe, not the only thing it is ever allowed to describe, and a name is the hardest part
    /// of a format to change later.
    /// </remarks>
    public sealed class VolumetricDocumentState
    {
        /// <summary>Whether this document is a volumetric project, and so saves as <c>.pxpv</c>.</summary>
        public bool HasState { get; set; }

        /// <summary>The project name, shown wherever the document is described.</summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>What kind of content the project holds.</summary>
        public VolumetricKind Kind { get; set; } = VolumetricKind.Voxel;

        /// <summary>Takes on another state's values wholesale, used to put one back on undo.</summary>
        public void CopyFrom(VolumetricDocumentState other)
        {
            if (other is null) return;

            HasState = other.HasState;
            ProjectName = other.ProjectName;
            Kind = other.Kind;
        }

        /// <summary>A complete copy.</summary>
        public VolumetricDocumentState Clone() => new()
        {
            HasState = HasState,
            ProjectName = ProjectName,
            Kind = Kind,
        };
    }
}
