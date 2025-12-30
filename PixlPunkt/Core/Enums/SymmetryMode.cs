namespace PixlPunkt.Core.Enums
{
    /// <summary>
    /// Defines the symmetry drawing mode for stroke mirroring.
    /// </summary>
    public enum SymmetryMode
    {
        /// <summary>
        /// No symmetry - strokes are not mirrored.
        /// </summary>
        None = 0,

        /// <summary>
        /// Horizontal axis symmetry - strokes are mirrored across a vertical line.
        /// Creates left/right mirroring.
        /// </summary>
        Horizontal = 1,

        /// <summary>
        /// Vertical axis symmetry - strokes are mirrored across a horizontal line.
        /// Creates top/bottom mirroring.
        /// </summary>
        Vertical = 2,

        /// <summary>
        /// Both horizontal and vertical symmetry - strokes are mirrored across both axes.
        /// Creates four-way mirroring (quadrants).
        /// </summary>
        Both = 3,

        /// <summary>
        /// Radial symmetry - strokes are rotated around a center point.
        /// Number of segments is controlled by RadialSegments setting.
        /// </summary>
        Radial = 4,

        /// <summary>
        /// Kaleidoscope symmetry - radial with mirroring within each segment.
        /// Creates kaleidoscope-like patterns.
        /// </summary>
        Kaleidoscope = 5
    }

    /// <summary>
    /// Extension methods for <see cref="SymmetryMode"/>.
    /// </summary>
    public static class SymmetryModeExtensions
    {
        /// <summary>
        /// Gets whether the mode is a radial (mandala) symmetry mode.
        /// </summary>
        /// <param name="mode">The symmetry mode.</param>
        /// <returns>True if the mode is radial.</returns>
        public static bool IsRadial(this SymmetryMode mode)
        {
            return mode == SymmetryMode.Radial || mode == SymmetryMode.Kaleidoscope;
        }

        /// <summary>
        /// Gets a human-readable display name for the symmetry mode.
        /// </summary>
        /// <param name="mode">The symmetry mode.</param>
        /// <returns>The display name.</returns>
        public static string GetDisplayName(this SymmetryMode mode)
        {
            return mode switch
            {
                SymmetryMode.None => "Off",
                SymmetryMode.Horizontal => "Horizontal",
                SymmetryMode.Vertical => "Vertical",
                SymmetryMode.Both => "Both Axes",
                SymmetryMode.Radial => "Radial",
                SymmetryMode.Kaleidoscope => "Kaleidoscope",
                _ => "Unknown"
            };
        }
    }
}
