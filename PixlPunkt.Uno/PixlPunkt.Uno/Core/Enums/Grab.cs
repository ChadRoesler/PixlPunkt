namespace PixlPunkt.Uno.Core.Enums
{
    /// <summary>
    /// Defines the grab handle positions for bezier curve or gradient control points.
    /// </summary>
    /// <remarks>
    /// This enum is used to identify which control point or tangent handle is being manipulated
    /// during interactive curve or gradient editing operations.
    /// </remarks>
    public enum Grab
    {
        /// <summary>
        /// No handle is currently grabbed.
        /// </summary>
        None,

        /// <summary>
        /// The outgoing tangent handle at the start point.
        /// </summary>
        Start_out,

        /// <summary>
        /// The left middle control point.
        /// </summary>
        Mid_left,

        /// <summary>
        /// The right middle control point.
        /// </summary>
        Mid_right,

        /// <summary>
        /// The incoming tangent handle at the end point.
        /// </summary>
        End_in
    }
}
