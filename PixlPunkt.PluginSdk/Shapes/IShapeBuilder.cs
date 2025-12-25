namespace PixlPunkt.PluginSdk.Shapes
{
    /// <summary>
    /// Defines the contract for shape building tools that generate pixel sets from two-point input.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shape builders encapsulate the geometry logic for drawing primitives like rectangles and ellipses.
    /// They handle both the pixel generation (which pixels to affect) and modifier key behavior
    /// (Shift for constrained proportions, Ctrl for center-out drawing).
    /// </para>
    /// <para>
    /// The workflow is:
    /// <list type="number">
    /// <item>Call <see cref="ApplyModifiers"/> to transform raw endpoints based on modifier keys</item>
    /// <item>Call <see cref="BuildOutlinePoints"/> or <see cref="BuildFilledPoints"/> to get affected pixels</item>
    /// <item>Use the resulting point set for preview rendering or final stroke application</item>
    /// </list>
    /// </para>
    /// </remarks>
    public interface IShapeBuilder
    {
        /// <summary>
        /// Gets the display name of this shape builder (e.g., "Rectangle", "Ellipse").
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Applies modifier key transformations to the shape endpoints.
        /// </summary>
        /// <param name="startX">Start point X (anchor).</param>
        /// <param name="startY">Start point Y (anchor).</param>
        /// <param name="endX">End point X (current cursor position).</param>
        /// <param name="endY">End point Y (current cursor position).</param>
        /// <param name="shift">True if Shift is held (constrain proportions: square/circle).</param>
        /// <param name="ctrl">True if Ctrl is held (draw from center outward).</param>
        /// <returns>Transformed endpoints (x0, y0, x1, y1) after modifier application.</returns>
        /// <remarks>
        /// <para>
        /// <strong>Shift modifier:</strong> Constrains the shape to equal width/height (square for rectangles,
        /// circle for ellipses). The larger dimension is applied to both axes.
        /// </para>
        /// <para>
        /// <strong>Ctrl modifier:</strong> Treats the start point as the center of the shape rather than
        /// a corner. The end point defines the radius/extent in each direction.
        /// </para>
        /// <para>
        /// <strong>Shift + Ctrl:</strong> Both effects combine - center-out with constrained proportions.
        /// </para>
        /// </remarks>
        (int x0, int y0, int x1, int y1) ApplyModifiers(int startX, int startY, int endX, int endY, bool shift, bool ctrl);

        /// <summary>
        /// Builds the set of outline points for this shape (unfilled mode).
        /// </summary>
        /// <param name="x0">Left/top X coordinate.</param>
        /// <param name="y0">Left/top Y coordinate.</param>
        /// <param name="x1">Right/bottom X coordinate.</param>
        /// <param name="y1">Right/bottom Y coordinate.</param>
        /// <returns>Set of (x, y) pixel coordinates on the shape's outline.</returns>
        /// <remarks>
        /// The returned points represent the 1-pixel-wide outline of the shape. For stroked shapes
        /// with width > 1, the caller should apply brush stamping at each point.
        /// </remarks>
        HashSet<(int x, int y)> BuildOutlinePoints(int x0, int y0, int x1, int y1);

        /// <summary>
        /// Builds the set of filled points for this shape (filled mode).
        /// </summary>
        /// <param name="x0">Left/top X coordinate.</param>
        /// <param name="y0">Left/top Y coordinate.</param>
        /// <param name="x1">Right/bottom X coordinate.</param>
        /// <param name="y1">Right/bottom Y coordinate.</param>
        /// <returns>Set of (x, y) pixel coordinates inside the shape.</returns>
        /// <remarks>
        /// The returned points include all pixels inside the shape boundary, suitable for
        /// solid fill rendering. For soft-edged fills, the caller should apply brush stamping.
        /// </remarks>
        HashSet<(int x, int y)> BuildFilledPoints(int x0, int y0, int x1, int y1);
    }
}
